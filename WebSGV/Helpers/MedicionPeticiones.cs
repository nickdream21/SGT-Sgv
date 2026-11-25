using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.SessionState;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Medición de peticiones en memoria (E6-11): por ruta, cantidad, ms totales, máximo, lentas, respuestas 5xx y ms
    /// esperando la sesión. La engancha <c>Global.asax</c>: <see cref="AlIniciar"/> (BeginRequest),
    /// <see cref="AlMapearManejador"/> (PostMapRequestHandler: empieza la espera de la sesión),
    /// <see cref="AlTenerSesion"/> (primera línea de Application_AcquireRequestState, que corre después del módulo de
    /// sesión: termina la espera, sin contar el control de sesión vigente de SGV) y <see cref="AlTerminar"/>
    /// (EndRequest). Una petición de más de <c>Medicion.UmbralMs</c> (appSettings, 3 000 por defecto, DN-35) deja
    /// "Petición lenta" (WRN); la primera petición de un día nuevo (hora de Perú) escribe "Resumen del día" (INF) del
    /// anterior y reinicia el acumulador, sin temporizadores. Application_End escribe el parcial: el sitio se recicla y
    /// lo de memoria se pierde, así que el resumen cubre desde que arrancó el sitio ("desde las HH:mm").
    /// No mide estáticos, <c>.axd</c>, <c>Salud.ashx</c> ni <c>CspInforme.ashx</c> (<see cref="SeMide"/>); la ruta es la
    /// del archivo (más el método de un PageMethod), en minúsculas y nunca con el querystring.
    /// </summary>
    public sealed class MedicionPeticiones
    {
        internal const string ClaveUmbral = "Medicion.UmbralMs";
        internal const int UmbralPorDefectoMs = 3000;
        /// <summary>Tope de rutas distintas por día: un barrido de bots no llena la memoria; el resto va a <see cref="RutaOtras"/>.</summary>
        internal const int MaxRutas = 500;
        internal const string RutaOtras = "(otras)";
        internal const int TopResumen = 5;

        internal const string ClaveInicio = "SGV_MedicionInicio";
        internal const string ClaveInicioSesion = "SGV_MedicionInicioSesion";
        internal const string ClaveFinSesion = "SGV_MedicionFinSesion";
        /// <summary>Nombre y rol de la sesión al cargarse (el id va en <see cref="LogSGV.ClaveIdUsuario"/>).</summary>
        internal const string ClaveNombreUsuario = "SGV_NombreUsuarioPeticion";
        internal const string ClaveRolUsuario = "SGV_RolUsuarioPeticion";

        private static readonly string[] ExtensionesEstaticas =
        {
            ".css", ".js", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".ico",
            ".woff", ".woff2", ".ttf", ".eot", ".map", ".axd",
        };

        private static readonly string[] ArchivosExcluidos = { "salud.ashx", "cspinforme.ashx" };

        /// <summary>El acumulador del sitio (hora de Perú y el umbral de Web.config).</summary>
        internal static MedicionPeticiones Instancia { get; } =
            new MedicionPeticiones(FechaHelper.Ahora, LeerUmbral(ConfigurationManager.AppSettings[ClaveUmbral]));

        private readonly Func<DateTime> ahora;
        private readonly object candado = new object();
        private Periodo actual;

        internal int UmbralMs { get; }

        /// <param name="ahora">Reloj en hora de Perú (las pruebas lo inyectan).</param>
        /// <param name="umbralMs">Por encima de esto una petición es lenta.</param>
        internal MedicionPeticiones(Func<DateTime> ahora, int umbralMs)
        {
            this.ahora = ahora ?? throw new ArgumentNullException(nameof(ahora));
            UmbralMs = umbralMs > 0 ? umbralMs : UmbralPorDefectoMs;
            actual = new Periodo(this.ahora());
        }

        /// <summary>Umbral de appSettings: un entero positivo, o <see cref="UmbralPorDefectoMs"/>.</summary>
        internal static int LeerUmbral(string valor) =>
            int.TryParse((valor ?? "").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0
                ? n
                : UmbralPorDefectoMs;

        /// <summary>
        /// False para estáticos (por extensión), <c>.axd</c>, <c>Salud.ashx</c> y <c>CspInforme.ashx</c>.
        /// <paramref name="rutaArchivo"/> es la ruta del archivo (<c>~/Views/X.aspx</c>, sin PathInfo).
        /// </summary>
        internal static bool SeMide(string rutaArchivo)
        {
            string r = SinQuery(rutaArchivo).ToLowerInvariant();
            if (r.Length == 0)
                return false;
            string archivo = r.Substring(r.LastIndexOf('/') + 1);
            if (ArchivosExcluidos.Contains(archivo))
                return false;
            int punto = archivo.LastIndexOf('.');
            return punto < 0 || !ExtensionesEstaticas.Contains(archivo.Substring(punto));
        }

        /// <summary>Ruta para medir: archivo + método del PageMethod, en minúsculas y sin querystring.</summary>
        internal static string Ruta(string rutaArchivo, string pathInfo)
        {
            string metodo = SinQuery(pathInfo).TrimEnd('/');
            return (SinQuery(rutaArchivo) + metodo).ToLowerInvariant();
        }

        private static string SinQuery(string texto)
        {
            texto = texto ?? "";
            int q = texto.IndexOf('?');
            return q >= 0 ? texto.Substring(0, q) : texto;
        }

        /// <summary>
        /// Suma una petición. Si cambió el día (hora de Perú), primero cierra el anterior: lo devuelve en
        /// <see cref="ResultadoMedicion.ResumenDiaAnterior"/> y empieza vacío.
        /// </summary>
        internal ResultadoMedicion Registrar(string ruta, long ms, long msSesion, bool error5xx)
        {
            DateTime momento = ahora();
            ResumenDia anterior = null;
            Periodo periodo = Volatile.Read(ref actual);
            if (momento.Date != periodo.Dia)
            {
                lock (candado)
                {
                    periodo = actual;
                    if (momento.Date != periodo.Dia)
                    {
                        anterior = periodo.Resumir();
                        periodo = new Periodo(momento);
                        Volatile.Write(ref actual, periodo);
                    }
                }
            }

            // Cuenta aproximada en el borde del día: una petición que leyó el período justo antes del cambio puede
            // sumarse (microsegundos después) al que ya se resumió y perderse. Se acepta: sin candado en cada suma.
            bool lenta = ms > UmbralMs;
            periodo.Obtener(ruta).Sumar(Math.Max(0, ms), Math.Max(0, msSesion), lenta, error5xx);
            return new ResultadoMedicion(lenta, anterior);
        }

        /// <summary>Lo acumulado hoy, sin reiniciar (lo usará la pantalla de estado, E6-12).</summary>
        internal ResumenDia Instantanea() => Volatile.Read(ref actual).Resumir();

        /// <summary>Cierra lo acumulado (Application_End) y empieza vacío.</summary>
        internal ResumenDia Cerrar()
        {
            lock (candado)
            {
                ResumenDia resumen = actual.Resumir();
                Volatile.Write(ref actual, new Periodo(ahora()));
                return resumen;
            }
        }

        // ── Enganche con el pipeline (Global.asax) ──────────────────────────────────────────

        internal static void AlIniciar(HttpContext ctx)
        {
            if (ctx != null)
                ctx.Items[ClaveInicio] = Stopwatch.GetTimestamp();
        }

        internal static void AlMapearManejador(HttpContext ctx)
        {
            if (ctx != null)
                ctx.Items[ClaveInicioSesion] = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Fin de la espera de la sesión. Guarda también el usuario en <c>Items</c>: el módulo de sesión la quita del
        /// contexto en ReleaseRequestState, y las líneas de EndRequest (lenta, error del WebMethod) lo siguen llevando.
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Medir nunca debe cortar la petición; sin sesión no hay usuario que guardar.")]
        internal static void AlTenerSesion(HttpContext ctx)
        {
            if (ctx == null)
                return;
            ctx.Items[ClaveFinSesion] = Stopwatch.GetTimestamp();
            try
            {
                HttpSessionState sesion = ctx.Session;
                int? idUsuario = SesionHelper.IdUsuario(sesion);
                if (idUsuario.HasValue)
                {
                    ctx.Items[LogSGV.ClaveIdUsuario] = idUsuario.Value;
                    // Para el correo de AvisoErrores de un WebMethod (EndRequest, ya sin sesión).
                    ctx.Items[ClaveNombreUsuario] = SesionHelper.Nombre(sesion);
                    ctx.Items[ClaveRolUsuario] = SesionHelper.Rol(sesion);
                }
            }
            catch { /* sesión no disponible */ }
        }

        /// <summary>EndRequest: suma la petición, avisa si fue lenta y escribe el resumen si cambió el día. Nunca lanza.</summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Medir nunca debe cambiar la respuesta ni dejar al usuario con la pantalla de IIS.")]
        internal static void AlTerminar(HttpContext ctx)
        {
            try
            {
                if (ctx == null || !(ctx.Items[ClaveInicio] is long inicio))
                    return;
                HttpRequest req = ctx.Request;
                string rutaArchivo = req.AppRelativeCurrentExecutionFilePath;
                if (!SeMide(rutaArchivo))
                    return;

                long fin = Stopwatch.GetTimestamp();
                long msSesion = ctx.Items[ClaveInicioSesion] is long s1 && ctx.Items[ClaveFinSesion] is long s2 && s2 >= s1
                    ? Milisegundos(s2 - s1)
                    : 0;
                Instancia.Procesar(Ruta(rutaArchivo, req.PathInfo), Milisegundos(fin - inicio), msSesion,
                    EsError5xx(ctx.Response.StatusCode, ctx.Error));
            }
            catch { /* la medición es secundaria */ }
        }

        /// <summary>Application_End: el resumen parcial (lo de memoria se pierde al detenerse).</summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Application_End no debe lanzar: el log se cierra justo después.")]
        internal static void AlDetener()
        {
            try
            {
                ResumenDia resumen = Instancia.Cerrar();
                if (resumen.Peticiones > 0)
                    LogSGV.Info("Resumen del día {Dia} (parcial: el sitio se detiene): {Resumen}",
                        resumen.Dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), resumen.Texto());
            }
            catch { /* sin resumen */ }
        }

        /// <summary>Suma y escribe en el log lo que corresponda (el resumen del día anterior y el aviso de lenta).</summary>
        internal ResultadoMedicion Procesar(string ruta, long ms, long msSesion, bool error5xx)
        {
            ResultadoMedicion r = Registrar(ruta, ms, msSesion, error5xx);
            if (r.ResumenDiaAnterior != null && r.ResumenDiaAnterior.Peticiones > 0)
                LogSGV.Info("Resumen del día {Dia}: {Resumen}",
                    r.ResumenDiaAnterior.Dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.ResumenDiaAnterior.Texto());
            if (r.EsLenta)
                LogSGV.Advertencia("Petición lenta {Ruta} {Ms} ms (sesión {MsSesion} ms)", ruta, ms, msSesion);
            return r;
        }

        /// <summary>
        /// 5xx: el código de la respuesta o, si un error no controlado terminó en la redirección a Error.aspx
        /// (customErrors en Release), ese error salvo que sea un 4xx (404, petición inválida).
        /// </summary>
        internal static bool EsError5xx(int codigoEstado, Exception error)
        {
            if (codigoEstado >= 500)
                return true;
            Exception real = ErrorAplicacionHelper.Desenvolver(error);
            if (real == null)
                return false;
            return !(real is HttpException http) || http.GetHttpCode() >= 500;
        }

        internal static long Milisegundos(long ticks) => ticks <= 0 ? 0 : ticks * 1000 / Stopwatch.Frequency;

        // ── Datos ───────────────────────────────────────────────────────────────────────────

        private sealed class Periodo
        {
            internal readonly DateTime Dia;
            internal readonly DateTime Desde;
            private readonly ConcurrentDictionary<string, Acumulado> rutas =
                new ConcurrentDictionary<string, Acumulado>(StringComparer.Ordinal);

            internal Periodo(DateTime desde)
            {
                Desde = desde;
                Dia = desde.Date;
            }

            internal Acumulado Obtener(string ruta)
            {
                ruta = string.IsNullOrEmpty(ruta) ? RutaOtras : ruta;
                if (rutas.TryGetValue(ruta, out Acumulado a))
                    return a;
                if (rutas.Count >= MaxRutas)
                    ruta = RutaOtras;
                return rutas.GetOrAdd(ruta, _ => new Acumulado());
            }

            internal ResumenDia Resumir() =>
                new ResumenDia(Dia, Desde, rutas.Select(kv => kv.Value.Fila(kv.Key)).ToList());
        }

        private sealed class Acumulado
        {
            private long cantidad, msTotal, msMaximo, lentas, errores5xx, msSesion;

            internal void Sumar(long ms, long msEsperaSesion, bool lenta, bool error5xx)
            {
                Interlocked.Increment(ref cantidad);
                Interlocked.Add(ref msTotal, ms);
                Interlocked.Add(ref msSesion, msEsperaSesion);
                if (lenta) Interlocked.Increment(ref lentas);
                if (error5xx) Interlocked.Increment(ref errores5xx);
                long maximo = Volatile.Read(ref msMaximo);
                while (ms > maximo)
                {
                    long visto = Interlocked.CompareExchange(ref msMaximo, ms, maximo);
                    if (visto == maximo) break;
                    maximo = visto;
                }
            }

            internal FilaMedicion Fila(string ruta) => new FilaMedicion(ruta,
                Interlocked.Read(ref cantidad), Interlocked.Read(ref msTotal), Interlocked.Read(ref msMaximo),
                Interlocked.Read(ref lentas), Interlocked.Read(ref errores5xx), Interlocked.Read(ref msSesion));
        }
    }

    internal sealed class ResultadoMedicion
    {
        internal ResultadoMedicion(bool esLenta, ResumenDia resumenDiaAnterior)
        {
            EsLenta = esLenta;
            ResumenDiaAnterior = resumenDiaAnterior;
        }

        internal bool EsLenta { get; }

        /// <summary>El día que terminó, si esta petición fue la primera del día nuevo; si no, null.</summary>
        internal ResumenDia ResumenDiaAnterior { get; }
    }

    internal sealed class FilaMedicion
    {
        internal FilaMedicion(string ruta, long cantidad, long msTotal, long msMaximo, long lentas, long errores5xx, long msSesion)
        {
            Ruta = ruta;
            Cantidad = cantidad;
            MsTotal = msTotal;
            MsMaximo = msMaximo;
            Lentas = lentas;
            Errores5xx = errores5xx;
            MsSesion = msSesion;
        }

        internal string Ruta { get; }
        internal long Cantidad { get; }
        internal long MsTotal { get; }
        internal long MsMaximo { get; }
        internal long Lentas { get; }
        internal long Errores5xx { get; }
        internal long MsSesion { get; }
    }

    internal sealed class ResumenDia
    {
        internal ResumenDia(DateTime dia, DateTime desde, IReadOnlyList<FilaMedicion> rutas)
        {
            Dia = dia;
            Desde = desde;
            Rutas = rutas ?? new List<FilaMedicion>();
        }

        internal DateTime Dia { get; }
        internal DateTime Desde { get; }
        internal IReadOnlyList<FilaMedicion> Rutas { get; }
        internal long Peticiones => Rutas.Sum(r => r.Cantidad);
        internal long Lentas => Rutas.Sum(r => r.Lentas);
        internal long Errores5xx => Rutas.Sum(r => r.Errores5xx);

        /// <summary>
        /// Una línea: totales, las <see cref="MedicionPeticiones.TopResumen"/> rutas con más ms totales, las que más
        /// lentas tuvieron y las que más 5xx. Números sin separador de miles (para buscarlos en el log).
        /// </summary>
        internal string Texto()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(Peticiones.ToString(ci)).Append(" peticiones desde las ").Append(Desde.ToString("HH:mm", ci))
              .Append(", ").Append(Lentas.ToString(ci)).Append(" lentas, ").Append(Errores5xx.ToString(ci)).Append(" con 5xx");

            var porTiempo = Rutas.Where(r => r.Cantidad > 0)
                .OrderByDescending(r => r.MsTotal).ThenBy(r => r.Ruta, StringComparer.Ordinal).Take(MedicionPeticiones.TopResumen)
                .Select(r => string.Format(ci, "{0} {1} pet. {2} ms (máx {3} ms, sesión {4} ms)",
                    r.Ruta, r.Cantidad, r.MsTotal, r.MsMaximo, r.MsSesion));
            Lista(sb, "; más tiempo: ", porTiempo);

            var lentas = Rutas.Where(r => r.Lentas > 0)
                .OrderByDescending(r => r.Lentas).ThenBy(r => r.Ruta, StringComparer.Ordinal).Take(MedicionPeticiones.TopResumen)
                .Select(r => string.Format(ci, "{0} {1}", r.Ruta, r.Lentas));
            Lista(sb, "; lentas: ", lentas);

            var errores = Rutas.Where(r => r.Errores5xx > 0)
                .OrderByDescending(r => r.Errores5xx).ThenBy(r => r.Ruta, StringComparer.Ordinal).Take(MedicionPeticiones.TopResumen)
                .Select(r => string.Format(ci, "{0} {1}", r.Ruta, r.Errores5xx));
            Lista(sb, "; 5xx: ", errores);
            return sb.ToString();
        }

        private static void Lista(StringBuilder sb, string titulo, IEnumerable<string> elementos)
        {
            var lista = elementos.ToList();
            if (lista.Count > 0)
                sb.Append(titulo).Append(string.Join(", ", lista));
        }
    }
}
