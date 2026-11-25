using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;

namespace WebSGV.Services.Operacion
{
    /// <summary>Errores (ERR y FTL) y avisos (WRN) de un día, contados en el log.</summary>
    public sealed class ConteoDiaLog
    {
        public DateTime Dia { get; internal set; }
        public int Errores { get; internal set; }
        public int Avisos { get; internal set; }
    }

    /// <summary>Un mensaje del log repetido, solo como plantilla (DN-35: nunca la línea cruda).</summary>
    public sealed class MensajeRepetido
    {
        /// <summary>"ERR" o "WRN".</summary>
        public string Nivel { get; internal set; }
        public string Plantilla { get; internal set; }
        public int Veces { get; internal set; }
    }

    /// <summary>Una ruta de hoy, medida en memoria por <see cref="MedicionPeticiones"/>.</summary>
    public sealed class RutaMedidaHoy
    {
        public string Ruta { get; internal set; }
        public long Peticiones { get; internal set; }
        public long Lentas { get; internal set; }
        public long MsMedio { get; internal set; }
        public long MsMaximo { get; internal set; }
    }

    /// <summary>Una ruta con "Petición lenta" en el log de los días anteriores.</summary>
    public sealed class RutaLentaAnterior
    {
        public string Ruta { get; internal set; }
        public int Lentas { get; internal set; }
        public DateTime UltimoDia { get; internal set; }
    }

    /// <summary>Una consulta del Query Store (texto parametrizado, recortado y con los literales tapados).</summary>
    public sealed class ConsultaCostosa
    {
        public string Consulta { get; internal set; }
        public long Ejecuciones { get; internal set; }
        public double CpuTotalMs { get; internal set; }
        public double CpuMediaMs { get; internal set; }
        public double DuracionMediaMs { get; internal set; }
    }

    /// <summary>Lo que se sabe del GPS automático (en memoria: se pierde al reciclarse el sitio). Sin datos personales.</summary>
    public sealed class EstadoGpsSistema
    {
        public DateTime? UltimoInicio { get; internal set; }
        public DateTime? UltimoFin { get; internal set; }
        public DateTime? UltimoInicioProgramado { get; internal set; }
        public DateTime? UltimaRespuestaOnway { get; internal set; }
        public bool EnCurso { get; internal set; }
        public bool UltimoConFallas { get; internal set; }
        public bool UltimaProgramadaConError { get; internal set; }

        /// <summary>Solo cifras ("12 consultados, ..."), o el aviso de que terminó con error (el detalle queda en el log).</summary>
        public string Resumen { get; internal set; }
    }

    /// <summary>Conexión a la BD y versión del esquema (<c>dbo.SchemaVersion</c>).</summary>
    public sealed class EstadoBdSistema
    {
        public bool Conectada { get; internal set; }
        public int Migraciones { get; internal set; }
        public string UltimaMigracion { get; internal set; }
        public DateTime? FechaUltimaMigracion { get; internal set; }
    }

    /// <summary>Lo leído de los archivos de log de los últimos días.</summary>
    public sealed class EstadoLog
    {
        public bool CarpetaExiste { get; internal set; }
        public int Archivos { get; internal set; }
        public int ArchivosNoLeidos { get; internal set; }
        public long Bytes { get; internal set; }
        public IReadOnlyList<ConteoDiaLog> PorDia { get; internal set; }
        public IReadOnlyList<MensajeRepetido> Repetidos { get; internal set; }
        public IReadOnlyList<RutaLentaAnterior> LentasAnteriores { get; internal set; }
    }

    /// <summary>Todo lo que muestra Views/EstadoSistema.aspx.</summary>
    public sealed class EstadoDelSistema
    {
        public DateTime Generado { get; internal set; }
        public long MsCalculo { get; internal set; }
        public EstadoLog Log { get; internal set; }
        public DateTime LentasHoyDesde { get; internal set; }
        public long PeticionesHoy { get; internal set; }
        public int UmbralLentaMs { get; internal set; }
        public IReadOnlyList<RutaMedidaHoy> RutasHoy { get; internal set; }
        public EstadoGpsSistema Gps { get; internal set; }
        public EstadoBdSistema Bd { get; internal set; }
        public bool ConsultasDisponibles { get; internal set; }
        public IReadOnlyList<ConsultaCostosa> Consultas { get; internal set; }
    }

    /// <summary>
    /// "Estado del sistema" para el administrador de sistema (E6-12, DN-35 punto 5): errores y avisos por día de los
    /// últimos <see cref="Dias"/> días y los mensajes más repetidos (de los archivos <c>App_Data/logs/sgv-aaaaMMdd*.log</c>),
    /// las rutas lentas (hoy de <see cref="MedicionPeticiones"/>, antes de las líneas "Petición lenta" del log), el GPS
    /// automático, la BD y las consultas más costosas del Query Store (<c>sp_OP_ConsultasCostosas</c>).
    /// Del log solo salen conteos y plantillas: <see cref="Plantilla"/> tapa los valores de cada mensaje. El resultado
    /// se guarda <see cref="DuracionCache"/> en la caché del sitio.
    /// </summary>
    public static class EstadoSistemaService
    {
        public const int Dias = 7;
        internal const int TopMensajes = 10;
        internal const int TopRutas = 10;
        internal const int TopConsultas = 10;
        /// <summary>Plantillas distintas que se cuentan por archivo: un log lleno de mensajes distintos no llena la memoria.</summary>
        internal const int MaxPlantillasPorArchivo = 5000;
        internal const int LargoMaximoPlantilla = 160;
        internal const string RutaOtra = "(otra)";
        internal const string Tapado = "…";
        internal const string Numero = "#";
        internal const string ErrorGps = "Terminó con error (el detalle está en el log).";

        public static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(5);
        private const string ClaveCache = "SGV_EstadoSistema";
        private const string PrefijoLenta = "Petición lenta ";

        /// <summary>El estado (de la caché si se calculó hace menos de <see cref="DuracionCache"/>).</summary>
        public static EstadoDelSistema Obtener()
        {
            if (HttpRuntime.Cache[ClaveCache] is EstadoDelSistema guardado)
                return guardado;
            EstadoDelSistema nuevo = Calcular(HostingEnvironment.MapPath("~/App_Data/logs"));
            HttpRuntime.Cache.Insert(ClaveCache, nuevo, null, DateTime.UtcNow.Add(DuracionCache), Cache.NoSlidingExpiration);
            return nuevo;
        }

        internal static EstadoDelSistema Calcular(string carpetaLogs)
        {
            var reloj = Stopwatch.StartNew();
            DateTime ahora = FechaHelper.Ahora();
            ResumenDia hoy = MedicionPeticiones.Instancia.Instantanea();
            bool consultasDisponibles = LeerConsultas(out List<ConsultaCostosa> consultas);
            var estado = new EstadoDelSistema
            {
                Generado = ahora,
                Log = LeerLog(carpetaLogs, ahora.Date),
                LentasHoyDesde = hoy.Desde,
                PeticionesHoy = hoy.Peticiones,
                UmbralLentaMs = MedicionPeticiones.Instancia.UmbralMs,
                RutasHoy = RutasDeHoy(hoy),
                Gps = LeerGps(),
                Bd = LeerBd(),
                ConsultasDisponibles = consultasDisponibles,
                Consultas = consultas,
            };
            estado.MsCalculo = reloj.ElapsedMilliseconds;
            return estado;
        }

        // ── Log ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Lee los archivos <c>sgv-aaaaMMdd*.log</c> de la carpeta con fecha entre <paramref name="hoy"/> menos
        /// <paramref name="dias"/> y mañana (Serilog nombra el archivo con la hora del servidor) y cuenta las líneas de
        /// cabecera de evento (las que empiezan por la fecha) de los <paramref name="dias"/> días hasta
        /// <paramref name="hoy"/> (día de Perú). Serilog escribe la hora del servidor (<paramref name="zonaServidor"/>,
        /// por defecto <see cref="TimeZoneInfo.Local"/>): cada marca se pasa a la de Perú antes de ver su día, para que
        /// "hoy" cuadre aunque el servidor esté en otra zona. Los archivos se leen en streaming, compartidos con Serilog,
        /// varios a la vez.
        /// </summary>
        internal static EstadoLog LeerLog(string carpeta, DateTime hoy, int dias = Dias, TimeZoneInfo zonaServidor = null,
            TimeZoneInfo zonaPeru = null)
        {
            DateTime desde = hoy.Date.AddDays(1 - dias);
            Func<DateTime, DateTime> aPeru = ConvertidorAPeru(zonaServidor ?? TimeZoneInfo.Local, zonaPeru ?? FechaHelper.ZonaPeru);
            var total = new ParcialLog(dias, aPeru);
            var estado = new EstadoLog { CarpetaExiste = !string.IsNullOrEmpty(carpeta) && Directory.Exists(carpeta) };
            if (estado.CarpetaExiste)
            {
                var archivos = Directory.EnumerateFiles(carpeta, "sgv-*.log")
                    .Where(f => FechaDeArchivo(Path.GetFileName(f)) is DateTime d && d >= desde.AddDays(-1) && d <= hoy.Date.AddDays(1))
                    .ToList();
                estado.Archivos = archivos.Count;
                var opciones = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount)) };
                Parallel.ForEach(archivos, opciones, archivo =>
                {
                    ParcialLog parcial = LeerArchivo(archivo, desde, dias, aPeru);
                    lock (total) total.Sumar(parcial);
                });
                estado.ArchivosNoLeidos = total.NoLeidos;
                estado.Bytes = total.Bytes;
            }

            estado.PorDia = Enumerable.Range(0, dias)
                .Select(i => new ConteoDiaLog { Dia = desde.AddDays(i), Errores = total.Errores[i], Avisos = total.Avisos[i] })
                .OrderByDescending(c => c.Dia)
                .ToList();
            estado.Repetidos = total.Plantillas
                .OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Take(TopMensajes)
                .Select(p => new MensajeRepetido { Nivel = p.Key.Substring(0, 3), Plantilla = p.Key.Substring(4), Veces = p.Value })
                .ToList();
            DateTime limiteAnteriores = hoy.Date;
            estado.LentasAnteriores = total.Lentas
                .Select(p => new { Ruta = p.Key, Dias = p.Value.Where(d => d.Key < limiteAnteriores).ToList() })
                .Where(x => x.Dias.Count > 0)
                .Select(x => new RutaLentaAnterior { Ruta = x.Ruta, Lentas = x.Dias.Sum(d => d.Value), UltimoDia = x.Dias.Max(d => d.Key) })
                .OrderByDescending(r => r.Lentas).ThenBy(r => r.Ruta, StringComparer.Ordinal)
                .Take(TopRutas)
                .ToList();
            return estado;
        }

        /// <summary>Fecha del nombre <c>sgv-aaaaMMdd.log</c> o <c>sgv-aaaaMMdd_001.log</c>; null si no es un archivo del log.</summary>
        internal static DateTime? FechaDeArchivo(string nombre)
        {
            if (nombre == null || nombre.Length < 16 || !nombre.StartsWith("sgv-", StringComparison.OrdinalIgnoreCase)
                || !nombre.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
                return null;
            return DateTime.TryParseExact(nombre.Substring(4, 8), "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d)
                && (nombre.Length == 16 || nombre[12] == '_')
                ? d
                : (DateTime?)null;
        }

        /// <summary>
        /// Pasa una hora de <paramref name="servidor"/> a la de <paramref name="peru"/> (sin cambio si son la misma; una
        /// hora que no existe o que no se puede convertir, como la que salta el horario de verano, queda como está).
        /// </summary>
        internal static Func<DateTime, DateTime> ConvertidorAPeru(TimeZoneInfo servidor, TimeZoneInfo peru)
        {
            if (servidor == null) throw new ArgumentNullException(nameof(servidor));
            if (peru == null) throw new ArgumentNullException(nameof(peru));
            if (string.Equals(servidor.Id, peru.Id, StringComparison.Ordinal))
                return t => t;
            return t =>
            {
                try { return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(t, DateTimeKind.Unspecified), servidor, peru); }
                catch (ArgumentException) { return t; }
            };
        }

        private static ParcialLog LeerArchivo(string archivo, DateTime desde, int dias, Func<DateTime, DateTime> aPeru)
        {
            var parcial = new ParcialLog(dias, aPeru);
            var cachePlantillas = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                using (var fs = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
                    1 << 16, FileOptions.SequentialScan))
                using (var lector = new StreamReader(fs, Encoding.UTF8, true, 1 << 16))
                {
                    parcial.Bytes = fs.Length;
                    string linea;
                    while ((linea = lector.ReadLine()) != null)
                        parcial.Linea(linea, desde, cachePlantillas);
                }
            }
            catch (IOException) { parcial.NoLeidos = 1; }
            catch (UnauthorizedAccessException) { parcial.NoLeidos = 1; }
            return parcial;
        }

        /// <summary>Lo contado en un archivo (o en todos, al sumarlos).</summary>
        private sealed class ParcialLog
        {
            internal readonly int[] Errores;
            internal readonly int[] Avisos;
            /// <summary>"NIV\tplantilla" → veces.</summary>
            internal readonly Dictionary<string, int> Plantillas = new Dictionary<string, int>(StringComparer.Ordinal);
            /// <summary>Ruta → (día → lentas).</summary>
            internal readonly Dictionary<string, Dictionary<DateTime, int>> Lentas = new Dictionary<string, Dictionary<DateTime, int>>(StringComparer.Ordinal);
            internal int NoLeidos;
            internal long Bytes;
            private readonly Func<DateTime, DateTime> _aPeru;

            internal ParcialLog(int dias, Func<DateTime, DateTime> aPeru)
            {
                Errores = new int[dias];
                Avisos = new int[dias];
                _aPeru = aPeru;
            }

            internal void Linea(string l, DateTime desde, Dictionary<string, string> cache)
            {
                // Filtro rápido antes de armar textos: casi todo el log es INF o la pila de una excepción.
                if (l.Length < 29 || l[24] != '[' || l[28] != ']' || !EsNivelContado(l[25], l[26], l[27]))
                    return;
                if (!LeerCabecera(l, out DateTime momento, out string nivel, out string mensaje))
                    return;
                DateTime dia = _aPeru(momento).Date;
                int i = (dia - desde).Days;
                if (i < 0 || i >= Errores.Length)
                    return;
                bool error = nivel == "ERR" || nivel == "FTL";
                if (error) Errores[i]++;
                else if (nivel == "WRN") Avisos[i]++;
                else return;

                if (!error && mensaje.StartsWith(PrefijoLenta, StringComparison.Ordinal))
                    SumarLenta(RutaDeLenta(mensaje), dia, 1);

                if (!cache.TryGetValue(mensaje, out string plantilla))
                {
                    plantilla = Plantilla(mensaje);
                    if (cache.Count < 5000) cache[mensaje] = plantilla;
                }
                string clave = (error ? "ERR" : "WRN") + "\t" + plantilla;
                if (Plantillas.TryGetValue(clave, out int n)) Plantillas[clave] = n + 1;
                else if (Plantillas.Count < MaxPlantillasPorArchivo) Plantillas[clave] = 1;
            }

            private void SumarLenta(string ruta, DateTime dia, int cantidad)
            {
                if (!Lentas.TryGetValue(ruta, out var porDia))
                {
                    if (Lentas.Count >= MedicionPeticiones.MaxRutas) ruta = RutaOtra;
                    if (!Lentas.TryGetValue(ruta, out porDia)) Lentas[ruta] = porDia = new Dictionary<DateTime, int>();
                }
                porDia.TryGetValue(dia, out int n);
                porDia[dia] = n + cantidad;
            }

            internal void Sumar(ParcialLog otro)
            {
                for (int i = 0; i < Errores.Length; i++)
                {
                    Errores[i] += otro.Errores[i];
                    Avisos[i] += otro.Avisos[i];
                }
                foreach (var p in otro.Plantillas)
                {
                    Plantillas.TryGetValue(p.Key, out int n);
                    Plantillas[p.Key] = n + p.Value;
                }
                foreach (var r in otro.Lentas)
                    foreach (var d in r.Value)
                        SumarLenta(r.Key, d.Key, d.Value);
                NoLeidos += otro.NoLeidos;
                Bytes += otro.Bytes;
            }
        }

        /// <summary>
        /// Cabecera de evento de <see cref="LogSGV"/>: <c>aaaa-MM-dd HH:mm:ss.fff [NIV] [idPetición] uUsuario mensaje</c>
        /// (o sin el id y el usuario, como antes de E6-10). Las demás líneas (la pila de una excepción) no cuentan.
        /// <paramref name="momento"/> es la fecha y hora tal como la escribió Serilog (hora del servidor, sin milisegundos).
        /// </summary>
        internal static bool LeerCabecera(string l, out DateTime momento, out string nivel, out string mensaje)
        {
            momento = default(DateTime);
            nivel = null;
            mensaje = null;
            if (l == null || l.Length < 29 || l[4] != '-' || l[7] != '-' || l[10] != ' ' || l[13] != ':' || l[16] != ':'
                || l[19] != '.' || l[23] != ' ' || l[24] != '[' || l[28] != ']')
                return false;
            int anio = Digitos(l, 0, 4), mes = Digitos(l, 5, 2), d = Digitos(l, 8, 2);
            if (anio < 2000 || mes < 1 || mes > 12 || d < 1 || d > DateTime.DaysInMonth(anio, mes))
                return false;
            int hh = Digitos(l, 11, 2), mm = Digitos(l, 14, 2), ss = Digitos(l, 17, 2);
            if (hh < 0 || hh > 23 || mm < 0 || mm > 59 || ss < 0 || ss > 59)
                return false;
            momento = new DateTime(anio, mes, d, hh, mm, ss);
            nivel = l.Substring(25, 3);

            int i = 29;
            if (i < l.Length && l[i] == ' ') i++;
            // [idPetición] de 8 caracteres y uUsuario (E6-10).
            if (i + 10 < l.Length && l[i] == '[' && l[i + 9] == ']' && l[i + 10] == ' ')
            {
                i += 11;
                if (i < l.Length && l[i] == 'u')
                {
                    int espacio = l.IndexOf(' ', i);
                    i = espacio < 0 ? l.Length : espacio + 1;
                }
            }
            mensaje = i < l.Length ? l.Substring(i) : "";
            return true;
        }

        /// <summary>ERR, WRN o FTL (los niveles que se cuentan).</summary>
        private static bool EsNivelContado(char a, char b, char c) =>
            (a == 'E' && b == 'R' && c == 'R') || (a == 'W' && b == 'R' && c == 'N') || (a == 'F' && b == 'T' && c == 'L');

        private static int Digitos(string l, int inicio, int largo)
        {
            int n = 0;
            for (int i = inicio; i < inicio + largo; i++)
            {
                char c = l[i];
                if (c < '0' || c > '9') return -1;
                n = n * 10 + (c - '0');
            }
            return n;
        }

        /// <summary>La ruta de "Petición lenta {Ruta} {Ms} ms ...", o <see cref="RutaOtra"/> si no es una ruta de página.</summary>
        internal static string RutaDeLenta(string mensaje)
        {
            string resto = mensaje.Substring(PrefijoLenta.Length);
            int espacio = resto.IndexOf(' ');
            string ruta = espacio < 0 ? resto : resto.Substring(0, espacio);
            return EsRuta(ruta) && ruta.IndexOf('?') < 0 ? ruta : RutaOtra;
        }

        // ── Plantilla de un mensaje (DN-35) ─────────────────────────────────────────────────

        private static readonly Regex Comillas = new Regex(@"(?<a>['""“”‘’«»`])(?<c>[^'""“”‘’«»`]*)(?<z>['""“”‘’«»`]|$)", RegexOptions.Compiled);
        private static readonly Regex RutaPagina = new Regex(@"^~?/[A-Za-z0-9_./-]*\.(aspx|ashx|asmx|axd)(/[A-Za-z_]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Tapados = new Regex(@"…(?:[\s,.;:/()-]*…)+", RegexOptions.Compiled);
        private static readonly Regex Numeros = new Regex(@"#(?:[\s,.:/()-]*#)+", RegexOptions.Compiled);
        private static readonly char[] CaracteresComilla = "'\"“”‘’«»`".ToCharArray();
        private const string Apertura = "([{\u00BF\u00A1'\"“”‘’«»`";
        private const string Cierre = ".,;:)]}!?'\"“”‘’«»`";

        /// <summary>Siglas que se dejan aunque vayan en mayúsculas (un nombre en mayúsculas se tapa).</summary>
        private static readonly HashSet<string> Siglas = new HashSet<string>(StringComparer.Ordinal)
        {
            "GPS", "SQL", "PDF", "ZIP", "CPIC", "SUNAT", "RUC", "DNI", "HTTP", "HTTPS", "URL", "API", "ID", "CSP", "SMTP",
            "BD", "OK", "XML", "JSON", "CSV", "IP", "TOTP", "POST", "GET", "HTML", "CSS", "JS", "PNG", "SHA", "UTC",
            "ERR", "WRN", "INF", "ASP", "NET", "ASP.NET", "IIS", "TLS", "SSL", "JWT", "PEN", "USD", "UIT", "VP", "OV",
        };

        /// <summary>
        /// Plantilla de un mensaje del log para mostrarla sin datos (DN-35, U-102). El archivo guarda el mensaje ya
        /// armado (Serilog no escribe la plantilla), así que se tapa todo lo que puede ser un valor, de forma
        /// conservadora, con lista blanca: lo que va entre comillas pasa a <c>…</c>, todo lo que tiene un dígito (DNI,
        /// placas, montos, fechas, horas, IP, GUID, ids) pasa a <c>#</c>, y una palabra solo se deja si está en el
        /// vocabulario de los textos fijos de las plantillas del código (<see cref="Vocabulario"/>), es una sigla
        /// conocida (<see cref="Siglas"/>) o es una ruta de página (<c>/Views/X.aspx</c>, sin su querystring). Todo lo
        /// demás (nombres en cualquier forma, palabras sueltas en minúsculas, nombres de archivo, correos, URL, textos de
        /// excepción, tipos de excepción) pasa a <c>…</c>, y varios <c>…</c> seguidos quedan en uno. Lo que puede
        /// quedar: una palabra del vocabulario que además sea parte de un valor (p. ej. "error" dentro del texto de una
        /// excepción), que por estar en un texto fijo de SGV no es un dato personal.
        /// </summary>
        internal static string Plantilla(string mensaje)
        {
            if (string.IsNullOrWhiteSpace(mensaje))
                return "";
            string s = mensaje.Length > 2000 ? mensaje.Substring(0, 2000) : mensaje;
            if (s.IndexOfAny(CaracteresComilla) >= 0)
                s = Comillas.Replace(s, m => m.Groups["a"].Value + Tapado + m.Groups["z"].Value);

            string[] tokens = s.Split(Separadores, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder(s.Length);
            for (int t = 0; t < tokens.Length; t++)
            {
                string tok = tokens[t];
                Nucleo(tok, out int a, out int z);
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(tok, 0, a);
                sb.Append(Palabra(tok.Substring(a, z - a), t == 0));
                sb.Append(tok, z, tok.Length - z);
            }
            string r = sb.ToString();
            if (r.IndexOf(Tapado, StringComparison.Ordinal) >= 0) r = Tapados.Replace(r, Tapado);
            if (r.IndexOf('#') >= 0) r = Numeros.Replace(r, Numero);
            return r.Length > LargoMaximoPlantilla ? r.Substring(0, LargoMaximoPlantilla - 1).TrimEnd() + Tapado : r;
        }

        private static string Palabra(string w, bool primera)
        {
            if (w.Length == 0 || w == Tapado)
                return w;
            int q = w.IndexOf('?');
            string sinConsulta = q >= 0 ? w.Substring(0, q) : w;
            if (EsRuta(sinConsulta))
                return q >= 0 ? sinConsulta + "?" + Tapado : sinConsulta;
            if (w.Any(char.IsDigit))
                return Numero;
            return Siglas.Contains(w) || EnVocabulario(w, primera) ? w : Tapado;
        }

        private static bool EsRuta(string w) => w.Length <= 200 && RutaPagina.IsMatch(w);

        private static readonly char[] Separadores = { ' ', '\t', '\r', '\n' };

        /// <summary>Lo que queda de un token sin los signos de apertura del principio ni los de cierre del final.</summary>
        private static void Nucleo(string tok, out int a, out int z)
        {
            a = 0;
            z = tok.Length;
            while (a < z && Apertura.IndexOf(tok[a]) >= 0) a++;
            while (z > a && Cierre.IndexOf(tok[z - 1]) >= 0) z--;
        }

        /// <summary>
        /// Las palabras de un texto cortado como lo corta <see cref="Plantilla"/> (sin signos alrededor ni las que
        /// tienen dígitos). Con esto se arma <c>VocabularioLog.txt</c> a partir de los textos fijos de las plantillas.
        /// </summary>
        internal static IEnumerable<string> PalabrasDe(string texto)
        {
            foreach (string tok in (texto ?? "").Split(Separadores, StringSplitOptions.RemoveEmptyEntries))
            {
                Nucleo(tok, out int a, out int z);
                string w = tok.Substring(a, z - a);
                if (w.Length > 0 && w.Length <= 40 && w[0] != '#' && !w.Any(char.IsDigit) && w != Tapado && !EsRuta(w))
                    yield return w;
            }
        }

        /// <summary>
        /// Exacta como está en el vocabulario; también con la inicial cambiada si es la primera palabra del mensaje
        /// ("Motivo" / "motivo") o si va en minúsculas y el vocabulario la tiene con mayúscula inicial ("error" / "Error").
        /// </summary>
        private static bool EnVocabulario(string w, bool primera)
        {
            if (Vocabulario.Contains(w))
                return true;
            char c = w[0];
            if (char.IsLower(c))
                return Vocabulario.Contains(char.ToUpperInvariant(c) + w.Substring(1));
            return primera && char.IsUpper(c) && Vocabulario.Contains(char.ToLowerInvariant(c) + w.Substring(1));
        }

        internal const string RecursoVocabulario = "VocabularioLog.txt";

        /// <summary>
        /// Las palabras de los textos fijos de las plantillas de <c>LogSGV</c> y <c>MensajeErrorHelper.ParaUsuario</c> del
        /// código (recurso incrustado <c>Services/Operacion/VocabularioLog.txt</c>; EstadoSistemaServiceTests comprueba que
        /// está al día con el código y explica cómo regenerarlo). Sin el recurso queda vacío y todo se tapa.
        /// </summary>
        internal static readonly HashSet<string> Vocabulario = CargarVocabulario();

        private static HashSet<string> CargarVocabulario()
        {
            var palabras = new HashSet<string>(StringComparer.Ordinal);
            var asm = typeof(EstadoSistemaService).Assembly;
            string nombre = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("." + RecursoVocabulario, StringComparison.OrdinalIgnoreCase));
            if (nombre == null)
                return palabras;
            using (Stream s = asm.GetManifestResourceStream(nombre))
            using (var r = new StreamReader(s, Encoding.UTF8))
            {
                string linea;
                while ((linea = r.ReadLine()) != null)
                {
                    linea = linea.Trim();
                    if (linea.Length > 0 && linea[0] != '#')
                        palabras.Add(linea);
                }
            }
            return palabras;
        }

        // ── Rutas de hoy (memoria) ──────────────────────────────────────────────────────────

        internal static IReadOnlyList<RutaMedidaHoy> RutasDeHoy(ResumenDia hoy) =>
            (hoy?.Rutas ?? new List<FilaMedicion>())
                .Where(r => r.Cantidad > 0)
                .Select(r => new RutaMedidaHoy
                {
                    Ruta = r.Ruta,
                    Peticiones = r.Cantidad,
                    Lentas = r.Lentas,
                    MsMedio = r.MsTotal / r.Cantidad,
                    MsMaximo = r.MsMaximo,
                })
                .OrderByDescending(r => r.Lentas).ThenByDescending(r => r.MsMedio).ThenBy(r => r.Ruta, StringComparer.Ordinal)
                .Take(TopRutas)
                .ToList();

        // ── GPS ─────────────────────────────────────────────────────────────────────────────

        private static EstadoGpsSistema LeerGps()
        {
            string resumen = ActualizacionGpsAutomatica.UltimoResumen;
            return new EstadoGpsSistema
            {
                UltimoInicio = ActualizacionGpsAutomatica.UltimoInicio,
                UltimoFin = ActualizacionGpsAutomatica.UltimoFin,
                // Estas dos viven en UTC (Salud.ashx las compara con DateTime.UtcNow).
                UltimoInicioProgramado = DeUtc(ActualizacionGpsAutomatica.UltimoInicioProgramado),
                UltimaRespuestaOnway = DeUtc(ActualizacionGpsAutomatica.UltimaRespuestaOnwayProgramada),
                EnCurso = ActualizacionGpsAutomatica.EnCurso,
                UltimoConFallas = ActualizacionGpsAutomatica.UltimoConFallas,
                UltimaProgramadaConError = ActualizacionGpsAutomatica.UltimaProgramadaConError,
                Resumen = ResumenGps(resumen),
            };
        }

        /// <summary>
        /// El resumen de la corrida es solo de cifras (<see cref="ActualizacionGpsAutomatica.Resumen"/>); el de una corrida
        /// que falló lleva el mensaje del error, que no se muestra.
        /// </summary>
        internal static string ResumenGps(string resumen)
        {
            if (string.IsNullOrWhiteSpace(resumen)) return null;
            if (resumen.StartsWith("Error", StringComparison.Ordinal)) return ErrorGps;
            // "12 consultados, 3 sin movimiento, ..., 45 s": tal cual solo si cada parte es "número palabras".
            return CifrasGps.IsMatch(resumen) ? resumen : Plantilla(resumen);
        }

        private static readonly Regex CifrasGps = new Regex(@"^\d+ [a-záéíóúñ ]+(, \d+ [a-záéíóúñ ]+)*$", RegexOptions.Compiled);

        private static DateTime? DeUtc(DateTime? utc) => utc.HasValue ? FechaHelper.ConvertirDeUtc(utc.Value) : (DateTime?)null;

        // ── BD ──────────────────────────────────────────────────────────────────────────────

        private static EstadoBdSistema LeerBd()
        {
            try
            {
                using (DataTable t = DbHelper.ConsultarTabla(@"
                    SELECT COUNT(*) AS migraciones,
                           (SELECT TOP 1 archivo FROM dbo.SchemaVersion ORDER BY idSchemaVersion DESC) AS ultima,
                           MAX(fechaAplicacion) AS fecha
                    FROM dbo.SchemaVersion"))
                {
                    DataRow r = t.Rows[0];
                    return new EstadoBdSistema
                    {
                        Conectada = true,
                        Migraciones = Convert.ToInt32(r["migraciones"], CultureInfo.InvariantCulture),
                        UltimaMigracion = r["ultima"] as string,
                        FechaUltimaMigracion = r["fecha"] == DBNull.Value ? (DateTime?)null : (DateTime)r["fecha"],
                    };
                }
            }
            catch (SqlException ex)
            {
                LogSGV.Error(ex, "Estado del sistema: no se pudo leer la versión del esquema");
                return new EstadoBdSistema { Conectada = false };
            }
            catch (InvalidOperationException ex)
            {
                LogSGV.Error(ex, "Estado del sistema: no se pudo conectar a la BD");
                return new EstadoBdSistema { Conectada = false };
            }
        }

        /// <summary>
        /// Tiempo límite de <c>sp_OP_ConsultasCostosas</c>: si el Query Store tarda más, el bloque dice "No disponible" y
        /// la página no espera los 30 s por defecto de un comando.
        /// </summary>
        internal const int SegundosConsultas = 8;
        internal const int LargoMaximoConsulta = 200;
        private const string SpConsultas = "sp_OP_ConsultasCostosas";

        /// <summary>Las consultas de <c>sp_OP_ConsultasCostosas</c>; false si no se pudo (sin permiso, sin Query Store, sin BD o tardó demasiado).</summary>
        private static bool LeerConsultas(out List<ConsultaCostosa> consultas) =>
            LeerConsultas(ComandoConsultas, SegundosConsultas, out consultas);

        private static SqlCommand ComandoConsultas(SqlConnection conexion)
        {
            var cmd = new SqlCommand(SpConsultas, conexion) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@Top", TopConsultas);
            cmd.Parameters.AddWithValue("@Dias", Dias);
            return cmd;
        }

        /// <summary>
        /// Lee las filas del comando que arma <paramref name="crear"/> (en la aplicación, el SP; las pruebas pasan uno
        /// que tarda, para ver el tiempo límite) con <paramref name="segundos"/> de tiempo límite.
        /// </summary>
        internal static bool LeerConsultas(Func<SqlConnection, SqlCommand> crear, int segundos, out List<ConsultaCostosa> consultas)
        {
            if (crear == null) throw new ArgumentNullException(nameof(crear));
            consultas = new List<ConsultaCostosa>();
            try
            {
                using (var conexion = new SqlConnection(DbHelper.ConnectionString))
                using (SqlCommand cmd = crear(conexion))
                {
                    cmd.CommandTimeout = segundos;
                    conexion.Open();
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read())
                            consultas.Add(new ConsultaCostosa
                            {
                                Consulta = TaparLiterales(r["consulta"] as string),
                                Ejecuciones = Convert.ToInt64(r["ejecuciones"], CultureInfo.InvariantCulture),
                                CpuTotalMs = Convert.ToDouble(r["cpuTotalMs"], CultureInfo.InvariantCulture),
                                CpuMediaMs = Convert.ToDouble(r["cpuMediaMs"], CultureInfo.InvariantCulture),
                                DuracionMediaMs = Convert.ToDouble(r["duracionMediaMs"], CultureInfo.InvariantCulture),
                            });
                }
                // Vacío = sin permiso VIEW DATABASE STATE o Query Store apagado (el SP no distingue a propósito).
                return consultas.Count > 0;
            }
            catch (SqlException ex) when (ex.Number == -2)
            {
                LogSGV.Advertencia("Estado del sistema: el Query Store tardó más de {Segundos} s", segundos);
                consultas.Clear();
                return false;
            }
            catch (SqlException ex)
            {
                LogSGV.Advertencia("Estado del sistema: Query Store no disponible ({Numero})", ex.Number);
                consultas.Clear();
                return false;
            }
            catch (InvalidOperationException)
            {
                consultas.Clear();
                return false;
            }
        }

        // Cadenas ('…', N'…'), comentarios (-- y /* */) e identificadores entre comillas dobles, de izquierda a derecha
        // (un -- dentro de una cadena no es un comentario, ni un ' dentro de un comentario abre una cadena).
        private static readonly Regex PiezaSql = new Regex(
            @"(?<t>N?'(?:[^']|'')*(?:'|$))|(?<l>--[^\r\n]*)|(?<b>/\*[\s\S]*?(?:\*/|$))|(?<q>""(?:[^""]|"""")*(?:""|$))",
            RegexOptions.Compiled);
        // Números sueltos: enteros, decimales, notación científica (1.5E+10) y binarios (0x1F).
        private static readonly Regex LiteralNumero = new Regex(
            @"(?<![\w@#$.])(?:0[xX][0-9A-Fa-f]*|\d+(?:\.\d*)?(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)(?![\w.])",
            RegexOptions.Compiled);
        private static readonly Regex Espacios = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Texto de una consulta del Query Store para mostrarlo: sin la lista de parámetros del principio
        /// (<c>(@id int,@x varchar(10))SELECT ...</c>), sin comentarios (el código de SGV comenta sus consultas; un
        /// comentario puede nombrar a un cliente), sin literales (las cadenas y los identificadores entre comillas dobles
        /// pasan a <c>'?'</c> y <c>"?"</c>, los números a <c>?</c>: una consulta ad hoc puede traer un valor escrito) y
        /// recortado a <see cref="LargoMaximoConsulta"/> caracteres después de limpiarlo.
        /// </summary>
        internal static string TaparLiterales(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return "";
            string s = SinListaDeParametros(sql);
            s = PiezaSql.Replace(s, m =>
                m.Groups["t"].Success ? "'?'" : m.Groups["q"].Success ? "\"?\"" : " ");
            s = LiteralNumero.Replace(s, "?");
            s = Espacios.Replace(s, " ").Trim();
            return s.Length > LargoMaximoConsulta ? s.Substring(0, LargoMaximoConsulta - 1).TrimEnd() + Tapado : s;
        }

        /// <summary>"(@a int,@b varchar(10))SELECT ..." → "SELECT ..." (hasta el paréntesis que cierra el primero).</summary>
        private static string SinListaDeParametros(string sql)
        {
            string s = sql.TrimStart();
            if (!s.StartsWith("(@", StringComparison.Ordinal))
                return s;
            int profundidad = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(') profundidad++;
                else if (s[i] == ')' && --profundidad == 0)
                    return s.Substring(i + 1);
            }
            return "";
        }
    }
}
