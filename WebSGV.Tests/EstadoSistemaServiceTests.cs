using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Operacion;
using WebSGV.Tests.Integracion;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-12: "Estado del sistema" (<see cref="EstadoSistemaService"/>). El conteo por día coincide con el archivo, la
    /// plantilla de un mensaje no deja pasar datos (DN-35: casos hostiles y todas las plantillas de LogSGV del código con
    /// valores hostiles), las rutas lentas del log, el texto del Query Store sin literales y el tiempo con 7 días de log
    /// de 20 MB. Sin BD: el log se escribe en una carpeta temporal.
    /// </summary>
    public sealed class EstadoSistemaServiceTests : IDisposable
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "sgv-estado-" + Guid.NewGuid().ToString("N"));
        public EstadoSistemaServiceTests()
        {
            Directory.CreateDirectory(_carpeta);
        }

        public void Dispose()
        {
            try { Directory.Delete(_carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        internal static string Linea(DateTime dia, string nivel, string mensaje, string hora = "10:15:02.123", bool conContexto = true) =>
            dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + " " + hora + " [" + nivel + "] "
            + (conContexto ? "[3f9a0c1e] u12 " : "") + mensaje;

        private string Archivo(DateTime dia, string sufijo = "") => Archivo(_carpeta, dia, sufijo);

        /// <summary>El log con el servidor en la zona de Perú (la fecha escrita es la del día), sea cual sea la zona de esta máquina.</summary>
        internal static EstadoLog LeerEnPeru(string carpeta, DateTime hoy) =>
            EstadoSistemaService.LeerLog(carpeta, hoy, zonaServidor: FechaHelper.ZonaPeru, zonaPeru: FechaHelper.ZonaPeru);

        internal static string Archivo(string carpeta, DateTime dia, string sufijo = "") =>
            Path.Combine(carpeta, "sgv-" + dia.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + sufijo + ".log");

        // ── Cabecera ────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("2026-11-25 10:15:02.123 [ERR] [3f9a0c1e] u12 Error no controlado en /Views/X.aspx", "ERR", "Error no controlado en /Views/X.aspx")]
        [InlineData("2026-11-25 10:15:02.123 [WRN] [--------] u- Petición lenta ~/views/x.aspx 3500 ms", "WRN", "Petición lenta ~/views/x.aspx 3500 ms")]
        [InlineData("2026-11-25 10:15:02.123 [INF] Aplicación SGV iniciada", "INF", "Aplicación SGV iniciada")] // antes de E6-10
        [InlineData("2026-11-25 10:15:02.123 [ERR] [3f9a0c1e] u12 ", "ERR", "")]
        public void LeerCabecera_FormatoConYSinContexto(string linea, string nivel, string mensaje)
        {
            Assert.True(EstadoSistemaService.LeerCabecera(linea, out DateTime momento, out string n, out string m));
            Assert.Equal(Hoy.AddHours(10).AddMinutes(15).AddSeconds(2), momento);
            Assert.Equal(nivel, n);
            Assert.Equal(mensaje, m);
        }

        [Theory]
        [InlineData("System.InvalidOperationException: 2026-11-25 10:15:02.123 [ERR] falla")]
        [InlineData("   en ASP.pagina.Fallar() en C:\\x.aspx:línea 6")]
        [InlineData("2026-13-25 10:15:02.123 [ERR] mes imposible")]
        [InlineData("2026-02-30 10:15:02.123 [ERR] día imposible")]
        [InlineData("2026-11-25 10:15:02 [ERR] sin milisegundos")]
        [InlineData("2026-11-25 24:15:02.123 [ERR] hora imposible")]
        [InlineData("2026-11-25 10:60:02.123 [ERR] minuto imposible")]
        [InlineData("")]
        [InlineData(null)]
        public void LeerCabecera_LoQueNoEsCabeceraNoCuenta(string linea)
        {
            Assert.False(EstadoSistemaService.LeerCabecera(linea, out _, out _, out _));
        }

        [Theory]
        [InlineData("sgv-20261125.log", true)]
        [InlineData("sgv-20261125_001.log", true)]
        [InlineData("SGV-20261125.LOG", true)]
        [InlineData("sgv-2026112.log", false)]
        [InlineData("sgv-20261325.log", false)]
        [InlineData("sgv-20261125x.log", false)]
        [InlineData("otro-20261125.log", false)]
        [InlineData("sgv-20261125.txt", false)]
        public void FechaDeArchivo_SoloLosDelLog(string nombre, bool esDelLog)
        {
            Assert.Equal(esDelLog, EstadoSistemaService.FechaDeArchivo(nombre).HasValue);
        }

        // ── Conteo por día ──────────────────────────────────────────────────────────────────

        [Fact]
        public void LeerLog_ElConteoPorDiaCoincideConElArchivo()
        {
            var azar = new Random(12);
            string[] niveles = { "INF", "INF", "INF", "WRN", "ERR", "DBG", "FTL" };
            // 8 días hacia atrás (el más viejo queda fuera de los 7), uno partido en dos archivos y uno sin contexto.
            for (int d = 0; d <= 7; d++)
            {
                DateTime dia = Hoy.AddDays(-d);
                foreach (string sufijo in d == 2 ? new[] { "", "_001" } : new[] { "" })
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < 300 + d * 37; i++)
                    {
                        string nivel = niveles[azar.Next(niveles.Length)];
                        sb.AppendLine(Linea(dia, nivel, "Mensaje " + i + " de prueba", conContexto: d != 3));
                        if (nivel == "ERR" && i % 3 == 0)
                        {
                            // Pila de la excepción: no es cabecera aunque contenga "[ERR]".
                            sb.AppendLine("System.Exception: falla [ERR] 2026-11-25 10:00:00.000 [ERR] x");
                            sb.AppendLine("   en WebSGV.Clase.Metodo() en C:\\x.cs:línea 10");
                        }
                    }
                    File.WriteAllText(Archivo(dia, sufijo), sb.ToString(), new UTF8Encoding(false));
                }
            }
            File.WriteAllText(Path.Combine(_carpeta, "otro.log"), Linea(Hoy, "ERR", "no es del log") + "\n");
            File.WriteAllText(Archivo(Hoy, "-copia"), Linea(Hoy, "ERR", "nombre que no es del log") + "\n");
            // Una línea de hoy en el archivo de ayer (Serilog nombra con la hora del servidor): cuenta por su fecha.
            File.AppendAllText(Archivo(Hoy.AddDays(-1)), Linea(Hoy, "ERR", "pasada la medianoche", "00:00:01.000") + "\n");

            EstadoLog log = LeerEnPeru(_carpeta, Hoy);

            // Conteo independiente: las líneas que empiezan por "aaaa-MM-dd hh:mm:ss.fff [NIV]".
            var cabecera = new Regex(@"^(\d{4}-\d\d-\d\d) \d\d:\d\d:\d\d\.\d{3} \[(ERR|WRN|FTL)\]", RegexOptions.Multiline);
            var esperado = Directory.GetFiles(_carpeta, "sgv-*.log")
                .Where(f => EstadoSistemaService.FechaDeArchivo(Path.GetFileName(f)).HasValue)
                .SelectMany(f => cabecera.Matches(File.ReadAllText(f)).Cast<Match>())
                .Select(m => new { Dia = DateTime.ParseExact(m.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture), Nivel = m.Groups[2].Value })
                .Where(x => x.Dia > Hoy.AddDays(-7) && x.Dia <= Hoy)
                .ToList();

            Assert.Equal(7, log.PorDia.Count);
            Assert.Equal(Hoy, log.PorDia[0].Dia); // hoy primero
            foreach (ConteoDiaLog c in log.PorDia)
            {
                Assert.Equal(esperado.Count(x => x.Dia == c.Dia && x.Nivel != "WRN"), c.Errores);
                Assert.Equal(esperado.Count(x => x.Dia == c.Dia && x.Nivel == "WRN"), c.Avisos);
                Assert.True(c.Errores > 0 && c.Avisos > 0, "el archivo de prueba debía tener de los dos niveles");
            }
            Assert.Equal(9, log.Archivos); // 7 días + el partido + el de hace 7 días (fuera del rango de líneas)
            Assert.Equal(0, log.ArchivosNoLeidos);
            // Los mensajes "Mensaje N de prueba" son una sola plantilla por nivel ("prueba" no es de ninguna plantilla del código).
            Assert.Contains(log.Repetidos, r => r.Nivel == "ERR" && r.Plantilla == "Mensaje # de …");
            Assert.Contains(log.Repetidos, r => r.Nivel == "WRN" && r.Plantilla == "Mensaje # de …");
            Assert.Equal(esperado.Count(x => x.Nivel == "WRN"), log.Repetidos.Single(r => r.Nivel == "WRN" && r.Plantilla == "Mensaje # de …").Veces);
        }

        [Fact]
        public void LeerLog_LeeElArchivoQueSerilogTieneAbierto()
        {
            using (var fs = new FileStream(Archivo(Hoy), FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            using (var w = new StreamWriter(fs, new UTF8Encoding(false)))
            {
                w.WriteLine(Linea(Hoy, "ERR", "Error abierto"));
                w.WriteLine(Linea(Hoy, "WRN", "Aviso abierto"));
                w.Flush();
                EstadoLog log = LeerEnPeru(_carpeta, Hoy);
                Assert.Equal(1, log.PorDia[0].Errores);
                Assert.Equal(1, log.PorDia[0].Avisos);
                Assert.Equal(0, log.ArchivosNoLeidos);
            }
        }

        [Fact]
        public void LeerLog_SinCarpetaNiArchivos_TodoEnCero()
        {
            EstadoLog sin = LeerEnPeru(Path.Combine(_carpeta, "no-existe"), Hoy);
            Assert.False(sin.CarpetaExiste);
            Assert.Equal(7, sin.PorDia.Count);
            Assert.All(sin.PorDia, d => Assert.Equal(0, d.Errores + d.Avisos));
            Assert.Empty(sin.Repetidos);
            Assert.Empty(sin.LentasAnteriores);
            Assert.False(LeerEnPeru(null, Hoy).CarpetaExiste);
        }

        // ── Zona del servidor ───────────────────────────────────────────────────────────────

        private static TimeZoneInfo Zona(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);

        [Fact]
        public void LeerLog_ServidorEnUtc_LasHorasPasanAlDiaDePeru()
        {
            // Servidor en UTC: las 03:00 del 25 son las 22:00 del 24 en Perú; las 05:00 del 25 ya son el 25.
            File.WriteAllLines(Archivo(Hoy), new[]
            {
                Linea(Hoy, "ERR", "Error de madrugada", "03:00:00.000"),
                Linea(Hoy, "ERR", "Error de la mañana", "05:00:00.000"),
                Linea(Hoy, "WRN", "Aviso de la mañana", "12:00:00.000"),
            });
            // 00:30 UTC del 26 (archivo de mañana para el servidor) = 19:30 del 25 en Perú: cuenta hoy.
            File.WriteAllLines(Archivo(Hoy.AddDays(1)), new[] { Linea(Hoy.AddDays(1), "WRN", "Aviso de la noche", "00:30:00.000") });

            EstadoLog log = EstadoSistemaService.LeerLog(_carpeta, Hoy, zonaServidor: TimeZoneInfo.Utc, zonaPeru: Zona("SA Pacific Standard Time"));

            Assert.Equal(1, log.PorDia.Single(d => d.Dia == Hoy).Errores);
            Assert.Equal(2, log.PorDia.Single(d => d.Dia == Hoy).Avisos);
            Assert.Equal(1, log.PorDia.Single(d => d.Dia == Hoy.AddDays(-1)).Errores);

            // Con el servidor en la zona de Perú (o sin decir nada en esta máquina) cuenta la fecha escrita.
            EstadoLog igual = EstadoSistemaService.LeerLog(_carpeta, Hoy, zonaServidor: Zona("SA Pacific Standard Time"),
                zonaPeru: Zona("SA Pacific Standard Time"));
            Assert.Equal(2, igual.PorDia.Single(d => d.Dia == Hoy).Errores);
            Assert.Equal(1, igual.PorDia.Single(d => d.Dia == Hoy).Avisos);
        }

        [Fact]
        public void ConvertidorAPeru_ConHorarioDeVeranoYSinCambio()
        {
            var peru = Zona("SA Pacific Standard Time");
            // Pacífico de EE. UU.: en noviembre UTC-8 (3 h antes que Perú), en julio UTC-7 (2 h antes).
            var aPeru = EstadoSistemaService.ConvertidorAPeru(Zona("Pacific Standard Time"), peru);
            Assert.Equal(new DateTime(2026, 11, 25, 1, 30, 0), aPeru(new DateTime(2026, 11, 24, 22, 30, 0)));
            Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 0), aPeru(new DateTime(2026, 7, 1, 10, 0, 0)));
            // La hora que salta el cambio de horario (8 de marzo de 2026, 02:30 no existe) no rompe la lectura.
            DateTime inexistente = new DateTime(2026, 3, 8, 2, 30, 0);
            Assert.Equal(inexistente, aPeru(inexistente));
            // Misma zona: sin cambio.
            DateTime t = new DateTime(2026, 11, 25, 23, 59, 59);
            Assert.Equal(t, EstadoSistemaService.ConvertidorAPeru(peru, peru)(t));
            Assert.Throws<ArgumentNullException>(() => EstadoSistemaService.ConvertidorAPeru(null, peru));
        }

        // ── Rutas lentas del log ────────────────────────────────────────────────────────────

        [Fact]
        public void LeerLog_RutasLentasDeLosDiasAnteriores()
        {
            File.WriteAllLines(Archivo(Hoy.AddDays(-1)), new[]
            {
                Linea(Hoy.AddDays(-1), "WRN", "Petición lenta ~/views/listadespachos.aspx 4200 ms (sesión 0 ms)"),
                Linea(Hoy.AddDays(-1), "WRN", "Petición lenta ~/views/listadespachos.aspx 3100 ms (sesión 5 ms)"),
                Linea(Hoy.AddDays(-1), "WRN", "Petición lenta ~/views/liquidacionespendientes.aspx/buscarconductores 3300 ms (sesión 0 ms)"),
                // Una ruta que no es de página (con querystring o datos) no se muestra: va a "(otra)".
                Linea(Hoy.AddDays(-1), "WRN", "Petición lenta /Uploads/Juan_Perez.pdf?dni=12345678 3300 ms"),
                Linea(Hoy.AddDays(-1), "INF", "Petición lenta ~/views/inicio.aspx 9999 ms"), // INF: no es el aviso
            });
            File.WriteAllLines(Archivo(Hoy.AddDays(-3)), new[]
            {
                Linea(Hoy.AddDays(-3), "WRN", "Petición lenta ~/views/listadespachos.aspx 5000 ms (sesión 0 ms)"),
            });
            File.WriteAllLines(Archivo(Hoy), new[]
            {
                Linea(Hoy, "WRN", "Petición lenta ~/views/reportegps.aspx 8000 ms (sesión 0 ms)"), // hoy sale de la memoria
            });

            var lentas = LeerEnPeru(_carpeta, Hoy).LentasAnteriores;

            Assert.Equal(new[] { "~/views/listadespachos.aspx", "(otra)", "~/views/liquidacionespendientes.aspx/buscarconductores" },
                lentas.Select(r => r.Ruta));
            Assert.Equal(3, lentas[0].Lentas);
            Assert.Equal(Hoy.AddDays(-1), lentas[0].UltimoDia);
            Assert.DoesNotContain(lentas, r => r.Ruta.Contains("reportegps") || r.Ruta.Contains("inicio"));
        }

        [Fact]
        public void RutasDeHoy_DeLaMedicionEnMemoria()
        {
            var resumen = new ResumenDia(Hoy, Hoy.AddHours(6), new List<FilaMedicion>
            {
                new FilaMedicion("~/views/a.aspx", 10, 5000, 900, 0, 0, 0),
                new FilaMedicion("~/views/b.aspx", 4, 16000, 6000, 2, 0, 10),
                new FilaMedicion("~/views/c.aspx", 0, 0, 0, 0, 0, 0),
            });
            var rutas = EstadoSistemaService.RutasDeHoy(resumen);
            Assert.Equal(new[] { "~/views/b.aspx", "~/views/a.aspx" }, rutas.Select(r => r.Ruta));
            Assert.Equal(4000, rutas[0].MsMedio);
            Assert.Equal(6000, rutas[0].MsMaximo);
            Assert.Empty(EstadoSistemaService.RutasDeHoy(null));
        }

        // ── Plantillas (DN-35) ──────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("Petición lenta ~/views/listadespachos.aspx 4200 ms (sesión 12 ms)", "Petición lenta ~/views/listadespachos.aspx # ms (sesión # ms)")]
        [InlineData("Error no controlado en /Views/DetalleOrdenViaje.aspx?id=1234 (usuario 15)", "Error no controlado en /Views/DetalleOrdenViaje.aspx?… (usuario #)")]
        [InlineData("Llamada inválida a /Views/Auditoria.aspx/ObtenerDetalle (WebMethod; ASP.NET la rechazó antes de ejecutar el método: ArgumentException)",
                    "Llamada inválida a /Views/Auditoria.aspx/ObtenerDetalle (WebMethod; ASP.NET la rechazó antes de ejecutar el método: …)")]
        // Casos del revisor (E6-12): nombres de archivo subidos, ex.Message y motivos con nombres en minúsculas.
        [InlineData("Temporales: no se pudo borrar juan perez.pdf. El proceso no puede obtener acceso al archivo",
                    "Temporales: no se pudo borrar …. El proceso no puede obtener acceso al archivo")]
        [InlineData("Temporales: no se pudo leer juana quispe mamani.pdf. Acceso denegado",
                    "Temporales: no se pudo leer …")]
        [InlineData("Motivo: El conductor jose luis ramos no estaba de turno", "Motivo: El conductor … no … de …")]
        [InlineData("Sesión vigente: no se pudo leer el estado del usuario 15; se deja pasar la petición. maría ñahui",
                    "Sesión vigente: no se pudo leer el estado del usuario #; se deja pasar la petición. …")]
        [InlineData("Error no controlado en DeLaCruz McGregor jPerez", "Error no controlado en …")]
        [InlineData("No se puede insertar una fila de clave duplicada en el objeto. El valor de la clave duplicada es (jperez).",
                    "No se puede … una fila de clave … en el objeto. El … de la clave … es (…).")]
        [InlineData("No se pudo enviar el correo 'Clave nueva de Juan Pérez' a ju***@correo.pe", "No se pudo enviar el correo '…' a …")]
        [InlineData("GPS automático, viaje con error: VP-000123 ABC-123 sin respuesta de Onway", "GPS automático, viaje con error: # sin respuesta de Onway")]
        [InlineData("Reporte GPS: error con la placa B7X-921", "Reporte GPS: error con la placa #")]
        [InlineData("Consulta de dni en apisperu sin respuesta: 12345678 JUAN CARLOS PEREZ", "Consulta de … en apisperu sin respuesta: # …")]
        public void Plantilla_TapaLosValores(string mensaje, string plantilla)
        {
            Assert.Equal(plantilla, EstadoSistemaService.Plantilla(mensaje));
        }

        /// <summary>Valores que nunca deben verse en una plantilla, cada uno con las partes que lo delatarían.</summary>
        internal static readonly (string Valor, string[] Delatores)[] ValoresHostiles =
        {
            ("12345678", new[] { "12345678", "1234" }),                            // DNI
            ("ABC-123", new[] { "ABC", "123" }),                                     // placa
            ("B7X921", new[] { "B7X", "921" }),
            ("Juan Pérez", new[] { "Juan", "Pérez" }),                               // nombre
            ("JUAN CARLOS PEREZ QUISPE", new[] { "JUAN", "CARLOS", "PEREZ", "QUISPE" }),
            ("María José de la Cruz", new[] { "María", "José", "Cruz" }),
            ("'Ana Lucía'", new[] { "Ana", "Lucía" }),                               // entre comillas
            ("\"rosa flores\"", new[] { "rosa", "flores" }),
            ("S/ 1,234.50", new[] { "1,234", "234", ".50" }),                        // montos
            ("US$ 980.00", new[] { "980" }),
            ("juan.perez@correo.pe", new[] { "juan", "perez", "correo" }),           // correo
            ("192.168.10.25", new[] { "192", "168" }),                               // IP
            ("3f9a0c1e-1b2c-4d5e-8f90-123456789abc", new[] { "3f9a", "4d5e" }),      // GUID
            ("25/11/2026 10:15", new[] { "25/11", "2026", "10:15" }),                // fecha
            ("C:\\inetpub\\Uploads\\Licencia Juan Perez.pdf", new[] { "inetpub", "Uploads", "Licencia", "Juan", "Perez" }),
            ("https://api.onway.pe/v1/posicion?placa=ABC123&token=xyz", new[] { "onway", "placa", "ABC123", "token", "xyz" }),
            ("Programación SEMANA 47 - Transportes Quispe.docx", new[] { "Programación", "SEMANA", "Transportes", "Quispe" }),
            ("VITAPRO S.A.", new[] { "VITAPRO" }),                                    // cliente en mayúsculas
            ("McGregor", new[] { "McGregor" }),                                       // apellido CamelCase
            ("DeLaCruz jPerez", new[] { "DeLaCruz", "jPerez" }),
            ("juan perez.pdf", new[] { "juan", "perez" }),                            // archivo subido en minúsculas
            ("juana quispe mamani.pdf", new[] { "juana", "quispe", "mamani" }),
            ("El conductor jose luis ramos", new[] { "jose", "luis", "ramos" }),      // motivo escrito a mano
            ("maría ñahui", new[] { "maría", "ñahui" }),
            ("(jperez)", new[] { "jperez" }),                                         // valor de una clave duplicada
        };

        [Fact]
        public void Plantilla_CasosHostiles_NingunDatoSeEscapa()
        {
            var fallas = new List<string>();
            foreach (var (valor, delatores) in ValoresHostiles)
                foreach (string mensaje in new[]
                {
                    "Error al guardar el conductor " + valor,
                    "Error al guardar: " + valor + " ya existe",
                    "Motivo: " + valor,
                    "Aviso (" + valor + "); se reintenta",
                    "Falló la firma de la orden 1532. Motivo: " + valor + ".",
                })
                {
                    string p = EstadoSistemaService.Plantilla(mensaje);
                    foreach (string d in delatores)
                        if (p.IndexOf(d, StringComparison.Ordinal) >= 0)
                            fallas.Add("\"" + mensaje + "\" → \"" + p + "\" deja ver \"" + d + "\"");
                }
            Assert.True(fallas.Count == 0, string.Join("\n", fallas));
        }

        /// <summary>
        /// Todas las plantillas de LogSGV (y de MensajeErrorHelper.ParaUsuario) del código, con cada valor reemplazado
        /// por uno hostil: ninguno queda visible. Si un mensaje nuevo pone un dato de una forma que la normalización no
        /// tapa, esta prueba lo dice.
        /// </summary>
        [Fact]
        public void Plantilla_TodasLasPlantillasDelCodigoConValoresHostiles()
        {
            string raiz = Path.Combine(BaseDePruebas.RaizRepo, "WebSGV");
            var llamada = new Regex(
                @"(?:LogSGV\.(?:Info|Advertencia|Error)|ParaUsuario)\(\s*(?:[\w.]+\s*,\s*)?""(?<t>(?:[^""\\]|\\.)*)""", RegexOptions.Compiled);
            var marcador = new Regex(@"\{[A-Za-z]+(?::[^}]*)?\}");
            var plantillas = Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                         && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .Concat(new[] { Path.Combine(raiz, "Global.asax.cs") })
                .Distinct()
                .SelectMany(f => llamada.Matches(File.ReadAllText(f)).Cast<Match>().Select(m => m.Groups["t"].Value))
                .Where(t => marcador.IsMatch(t))
                .Distinct()
                .ToList();
            Assert.True(plantillas.Count > 100, "Se esperaban más de 100 plantillas con valores; hay " + plantillas.Count);

            var hostiles = ValoresHostiles.ToList();
            var fallas = new List<string>();
            for (int k = 0; k < hostiles.Count; k++)
                foreach (string t in plantillas)
                {
                    int i = k;
                    var usados = new List<string[]>();
                    string mensaje = marcador.Replace(t, _ =>
                    {
                        var h = hostiles[i++ % hostiles.Count];
                        usados.Add(h.Delatores);
                        return h.Valor;
                    }) + " (aviso de negocio al usuario: " + hostiles[k].Valor + ")";
                    usados.Add(hostiles[k].Delatores);
                    string p = EstadoSistemaService.Plantilla(mensaje);
                    foreach (string d in usados.SelectMany(u => u).Distinct())
                        // Solo cuenta si el delator no estaba ya en el texto fijo de la plantilla (p. ej. "Onway", "placa").
                        if (p.IndexOf(d, StringComparison.Ordinal) >= 0 && t.IndexOf(d, StringComparison.OrdinalIgnoreCase) < 0)
                            fallas.Add("\"" + t + "\" → \"" + p + "\" deja ver \"" + d + "\"");
                }
            Assert.True(fallas.Count == 0, fallas.Count + " fugas:\n" + string.Join("\n", fallas.Distinct().Take(40)));
        }

        /// <summary>
        /// Las mismas plantillas con valores inofensivos (números): todas las palabras de su texto fijo siguen a la vista,
        /// así que la lista blanca no deja el log ilegible.
        /// </summary>
        [Fact]
        public void Plantilla_ElTextoFijoDeLasPlantillasSigueLegible()
        {
            var marcador = new Regex(@"\{[A-Za-z]+(?::[^}]*)?\}");
            var fallas = new List<string>();
            int plantillas = 0;
            foreach (string literal in LiteralesDePlantillas())
            {
                plantillas++;
                string mensaje = marcador.Replace(literal, "15");
                string p = EstadoSistemaService.Plantilla(mensaje);
                var visibles = new HashSet<string>(EstadoSistemaService.PalabrasDe(p), StringComparer.Ordinal);
                foreach (string w in EstadoSistemaService.PalabrasDe(marcador.Replace(literal, " ")))
                    if (!visibles.Contains(w) && p.Length < EstadoSistemaService.LargoMaximoPlantilla - 1)
                        fallas.Add("\"" + literal + "\" → \"" + p + "\" tapa \"" + w + "\"");
            }
            Assert.True(plantillas > 250, "Se esperaban más de 250 textos de plantillas; hay " + plantillas);
            Assert.True(fallas.Count == 0, fallas.Count + " palabras fijas tapadas:\n" + string.Join("\n", fallas.Distinct().Take(40)));
        }

        // ── Vocabulario (lista blanca) ──────────────────────────────────────────────────────

        private static string ArchivoVocabulario =>
            Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Services", "Operacion", EstadoSistemaService.RecursoVocabulario);

        private static readonly Regex InicioLlamada = new Regex(
            @"(?:\bLogSGV\.(?:Info|Advertencia|Error|InfoCon|AdvertenciaCon)|\bParaUsuario(?:OTexto)?)\s*\(", RegexOptions.Compiled);

        /// <summary>
        /// Los textos literales de los argumentos de cada llamada a LogSGV y ParaUsuario del código (WebSGV/**/*.cs),
        /// con las secuencias de escape resueltas y los huecos de las cadenas interpoladas como espacios.
        /// </summary>
        internal static IEnumerable<string> LiteralesDePlantillas()
        {
            string raiz = Path.Combine(BaseDePruebas.RaizRepo, "WebSGV");
            string sep = Path.DirectorySeparatorChar.ToString();
            var archivos = Directory.GetFiles(raiz, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
                .OrderBy(f => f, StringComparer.Ordinal);
            foreach (string archivo in archivos)
            {
                string texto = File.ReadAllText(archivo);
                foreach (Match m in InicioLlamada.Matches(texto))
                    foreach (string literal in LiteralesDeArgumentos(texto, m.Index + m.Length))
                        yield return literal;
            }
        }

        /// <summary>Los literales de cadena entre la posición (justo después del "(") y su ")" correspondiente.</summary>
        private static IEnumerable<string> LiteralesDeArgumentos(string t, int i)
        {
            var literales = new List<string>();
            int profundidad = 1;
            while (i < t.Length && profundidad > 0)
            {
                char c = t[i];
                if (c == '/' && i + 1 < t.Length && t[i + 1] == '/') { while (i < t.Length && t[i] != '\n') i++; }
                else if (c == '/' && i + 1 < t.Length && t[i + 1] == '*') { int fin = t.IndexOf("*/", i + 2, StringComparison.Ordinal); i = fin < 0 ? t.Length : fin + 2; }
                else if (c == '(') { profundidad++; i++; }
                else if (c == ')') { profundidad--; i++; }
                else if (c == '\'') { i++; while (i < t.Length && t[i] != '\'') i += t[i] == '\\' ? 2 : 1; i++; }
                else if (c == '"' || ((c == '@' || c == '$') && i + 1 < t.Length && (t[i + 1] == '"' || t[i + 1] == '@' || t[i + 1] == '$')))
                {
                    bool literal = false, interpolada = false;
                    while (t[i] != '"') { if (t[i] == '@') literal = true; if (t[i] == '$') interpolada = true; i++; }
                    i++;
                    var sb = new StringBuilder();
                    while (i < t.Length)
                    {
                        char d = t[i];
                        if (literal && d == '"' && i + 1 < t.Length && t[i + 1] == '"') { sb.Append('"'); i += 2; }
                        else if (d == '"') { i++; break; }
                        else if (!literal && d == '\\' && i + 1 < t.Length) { sb.Append(Escape(t, ref i)); }
                        else if (interpolada && d == '{' && i + 1 < t.Length && t[i + 1] == '{') { sb.Append('{'); i += 2; }
                        else if (interpolada && d == '{')
                        {
                            int nivel = 1; i++;
                            while (i < t.Length && nivel > 0) { if (t[i] == '{') nivel++; else if (t[i] == '}') nivel--; i++; }
                            sb.Append(' ');
                        }
                        else { sb.Append(d); i++; }
                    }
                    literales.Add(sb.ToString());
                }
                else i++;
            }
            return literales;
        }

        private static string Escape(string t, ref int i)
        {
            char e = t[i + 1];
            i += 2;
            switch (e)
            {
                case 'n': case 'r': case 't': return " ";
                case 'u':
                    string hex = t.Substring(i, 4);
                    i += 4;
                    return ((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString();
                default: return e.ToString();
            }
        }

        /// <summary>El vocabulario que sale del código actual (palabras sin dígitos de los textos fijos, sin los marcadores).</summary>
        internal static SortedSet<string> VocabularioDelCodigo()
        {
            var marcador = new Regex(@"\{[A-Za-z]+(?::[^}]*)?\}");
            var palabras = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string literal in LiteralesDePlantillas())
                foreach (string w in EstadoSistemaService.PalabrasDe(marcador.Replace(literal, " ")))
                    palabras.Add(w);
            return palabras;
        }

        /// <summary>
        /// VocabularioLog.txt está al día con los textos de las plantillas del código. Si falla tras agregar o cambiar un
        /// mensaje de LogSGV o de ParaUsuario: correr esta prueba con la variable de entorno SGV_REGENERAR_VOCABULARIO=1
        /// (reescribe el archivo), revisar el diff (ninguna palabra puede ser un dato) y volver a compilar.
        /// </summary>
        [Fact]
        public void Vocabulario_EstaAlDiaConElCodigo()
        {
            SortedSet<string> esperado = VocabularioDelCodigo();
            Assert.True(esperado.Count > 400, "Se esperaban más de 400 palabras; hay " + esperado.Count);
            if (Environment.GetEnvironmentVariable("SGV_REGENERAR_VOCABULARIO") == "1")
            {
                var sb = new StringBuilder();
                sb.Append("# Vocabulario de las plantillas del log (E6-12, DN-35): las palabras de los textos fijos de las llamadas a\n");
                sb.Append("# LogSGV y MensajeErrorHelper.ParaUsuario del código. EstadoSistemaService.Plantilla solo deja a la vista\n");
                sb.Append("# estas palabras, las siglas conocidas y las rutas de páginas. Generado por EstadoSistemaServiceTests\n");
                sb.Append("# (Vocabulario_EstaAlDiaConElCodigo, con SGV_REGENERAR_VOCABULARIO=1): no editar a mano.\n");
                foreach (string w in esperado) sb.Append(w).Append('\n');
                File.WriteAllText(ArchivoVocabulario, sb.ToString(), new UTF8Encoding(false));
            }

            var archivo = new HashSet<string>(File.ReadAllLines(ArchivoVocabulario, Encoding.UTF8)
                .Select(l => l.Trim()).Where(l => l.Length > 0 && l[0] != '#'), StringComparer.Ordinal);
            var faltan = esperado.Where(w => !archivo.Contains(w)).ToList();
            var sobran = archivo.Where(w => !esperado.Contains(w)).OrderBy(w => w, StringComparer.Ordinal).ToList();
            Assert.True(faltan.Count == 0 && sobran.Count == 0,
                "VocabularioLog.txt no está al día con el código (regenerar con SGV_REGENERAR_VOCABULARIO=1; ver el comentario de esta prueba).\n"
                + "Faltan: " + string.Join(", ", faltan.Take(30)) + "\nSobran: " + string.Join(", ", sobran.Take(30)));
            // El recurso incrustado en WebSGV.dll es el mismo archivo (registrado como EmbeddedResource).
            Assert.True(archivo.SetEquals(EstadoSistemaService.Vocabulario), "El recurso incrustado no coincide con el archivo: recompilar WebSGV.");
        }

        [Fact]
        public void Vocabulario_SinPalabrasQueParezcanDatos()
        {
            // Ningún nombre de pila ni apellido frecuente de los casos hostiles, y sin palabras con @ ni rutas de archivo.
            foreach (string w in new[] { "juan", "Juan", "perez", "Pérez", "jose", "luis", "ramos", "quispe", "mamani", "maría", "jperez" })
                Assert.DoesNotContain(w, EstadoSistemaService.Vocabulario);
            Assert.DoesNotContain(EstadoSistemaService.Vocabulario, w => w.Contains("@") || w.Contains("\\"));
        }

        [Fact]
        public void Plantilla_LoQueNoEstaEnElVocabularioSeTapa()
        {
            // Una palabra suelta en minúsculas o un identificador CamelCase que no es de una plantilla ya no se ven.
            Assert.Equal("Usuario … no encontrado: …", EstadoSistemaService.Plantilla("Usuario Juan no encontrado: jperez"));
            Assert.Equal("Error en …", EstadoSistemaService.Plantilla("Error en QuispeMamani: SqlException"));
            // Un identificador que sí es de una plantilla del código queda.
            Assert.Equal("Error en DashboardConductor: …", EstadoSistemaService.Plantilla("Error en DashboardConductor: SqlException"));
            // Varios tapados seguidos quedan en uno.
            Assert.Equal("Motivo: …)", EstadoSistemaService.Plantilla("Motivo: yupanqui quispe, mamani; huamán (ccori)"));
            // La primera palabra puede ir con mayúscula aunque el vocabulario la tenga en minúsculas, y al revés.
            Assert.StartsWith("Error", EstadoSistemaService.Plantilla("Error de prueba"));
            Assert.Contains("error", EstadoSistemaService.Plantilla("Motivo: error de red"));
            // Largo máximo.
            string largo = EstadoSistemaService.Plantilla("Error " + string.Join(" ", Enumerable.Repeat("palabra", 60)));
            Assert.True(largo.Length <= EstadoSistemaService.LargoMaximoPlantilla);
            Assert.EndsWith("…", largo);
            Assert.Equal("", EstadoSistemaService.Plantilla("   "));
            Assert.Equal("", EstadoSistemaService.Plantilla(null));
        }

        // ── GPS y Query Store ───────────────────────────────────────────────────────────────

        [Fact]
        public void ResumenGps_SoloCifras()
        {
            string cifras = "12 consultados, 3 sin movimiento, 0 horas nuevas, 1 aproximados con su grupo, 0 pendientes, 0 con error, 45 s";
            Assert.Equal(cifras, EstadoSistemaService.ResumenGps(cifras));
            Assert.Equal(EstadoSistemaService.ErrorGps, EstadoSistemaService.ResumenGps("Error: No se encontró la placa ABC-123 de Juan Pérez"));
            Assert.DoesNotContain("Pérez", EstadoSistemaService.ResumenGps("12 consultados, viaje de Juan Pérez"));
            Assert.Null(EstadoSistemaService.ResumenGps(null));
        }

        [Theory]
        [InlineData("SELECT * FROM Conductores WHERE dni = '12345678' AND idConductor = 15", "SELECT * FROM Conductores WHERE dni = '?' AND idConductor = ?")]
        [InlineData("SELECT TOP (@maximo) t.*, COUNT(*) OVER () AS total FROM T1 t WHERE t.x = @p1", "SELECT TOP (@maximo) t.*, COUNT(*) OVER () AS total FROM T1 t WHERE t.x = @p1")]
        [InlineData("UPDATE Usuarios SET nombre = N'María O''Brien' WHERE id = 3", "UPDATE Usuarios SET nombre = '?' WHERE id = ?")]
        [InlineData("SELECT a FROM T WHERE nombre = 'Juan Pérez cortado al llegar a los 200 caract", "SELECT a FROM T WHERE nombre = '?'")]
        [InlineData("SELECT   a\t FROM   T WHERE m = 1234.50", "SELECT a FROM T WHERE m = ?")]
        // Lista de parámetros del principio, con paréntesis dentro.
        [InlineData("(@id int,@nombre nvarchar(100),@monto decimal(18,2))SELECT a FROM T WHERE id = @id", "SELECT a FROM T WHERE id = @id")]
        [InlineData("(@a int) UPDATE T SET x = 1 WHERE id = @a", "UPDATE T SET x = ? WHERE id = @a")]
        // Comentarios: se quitan antes de recortar (un comentario puede nombrar a un cliente).
        [InlineData("-- Reporte de Vitapro (cliente)\r\nSELECT a FROM T -- fin\r\nWHERE b = 2", "SELECT a FROM T WHERE b = ?")]
        [InlineData("SELECT /* Transportes Quispe: 12345678 */ a FROM T", "SELECT a FROM T")]
        [InlineData("SELECT a FROM T /* sin cerrar Juan Pérez", "SELECT a FROM T")]
        [InlineData("SELECT '--no es comentario' AS x, b FROM T", "SELECT '?' AS x, b FROM T")]
        [InlineData("SELECT a FROM T -- it's Juan's\r\nWHERE c = 'x'", "SELECT a FROM T WHERE c = '?'")]
        // Comillas dobles, binarios y notación científica.
        [InlineData("SELECT \"Juan Pérez\" FROM T WHERE h = 0x1F2A AND f = 1.5E+10 AND g = 2e-3", "SELECT \"?\" FROM T WHERE h = ? AND f = ? AND g = ?")]
        [InlineData("SELECT x FROM T WHERE y = .5 AND z = 0x", "SELECT x FROM T WHERE y = ? AND z = ?")]
        [InlineData(null, "")]
        public void TaparLiterales_SinValores(string sql, string esperado)
        {
            Assert.Equal(esperado, EstadoSistemaService.TaparLiterales(sql));
        }

        [Fact]
        public void TaparLiterales_RecortaDespuesDeLimpiar()
        {
            // Un comentario largo al principio no se come el texto: se quita y después se recorta a 200.
            string sql = "/* " + new string('x', 3000) + " */\nSELECT " + string.Join(", ", Enumerable.Range(1, 80).Select(i => "columna" + (char)('a' + i % 26)))
                + " FROM T WHERE n = 'Juan'";
            string r = EstadoSistemaService.TaparLiterales(sql);
            Assert.StartsWith("SELECT columnab, columnac", r);
            Assert.True(r.Length > 190 && r.Length <= EstadoSistemaService.LargoMaximoConsulta, "largo " + r.Length);
            Assert.EndsWith("…", r);
            Assert.Equal("", EstadoSistemaService.TaparLiterales("(@a int"));
        }

        // ── Página ──────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Pagina_CodificaLoQueVieneDelLogYDelQueryStore()
        {
            string raiz = Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Views");
            string aspx = File.ReadAllText(Path.Combine(raiz, "EstadoSistema.aspx"));
            foreach (string campo in new[] { "Plantilla", "Consulta", "Ruta" })
                Assert.Matches(new Regex(@"<%#:\s*Eval\(""" + campo + @"""\)\s*%>"), aspx);
            Assert.DoesNotMatch(new Regex(@"<%#\s*Eval\("), aspx);
            string cs = File.ReadAllText(Path.Combine(raiz, "EstadoSistema.aspx.cs"));
            Assert.Contains("EstadoSistemaService.Obtener()", cs);
            Assert.DoesNotContain("DbHelper", cs);
            Assert.DoesNotContain("SqlCommand", cs);
        }

    }

    /// <summary>
    /// E6-12: 7 días de log de 20 MB (el tope de cada archivo de Serilog) se leen en menos de 2 s. Sin otras pruebas en
    /// paralelo: con la suite entera compitiendo por la CPU el tiempo no dice nada del servidor.
    /// </summary>
    [CollectionDefinition(Coleccion, DisableParallelization = true)]
    public sealed class EstadoSistemaRendimientoColeccion
    {
        public const string Coleccion = "Rendimiento de Estado del sistema";
    }

    [Collection(EstadoSistemaRendimientoColeccion.Coleccion)]
    public sealed class EstadoSistemaRendimientoTests : IDisposable
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 25);
        private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "sgv-estado-rend-" + Guid.NewGuid().ToString("N"));
        private readonly ITestOutputHelper _salida;

        public EstadoSistemaRendimientoTests(ITestOutputHelper salida)
        {
            _salida = salida;
            Directory.CreateDirectory(_carpeta);
        }

        public void Dispose()
        {
            try { Directory.Delete(_carpeta, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        // ── Rendimiento ─────────────────────────────────────────────────────────────────────

        [Fact]
        public void LeerLog_SieteDiasDe20Mb_EnMenosDe2Segundos()
        {
            // Mezcla parecida a un día malo: casi todo INF, avisos y errores con valores distintos y pilas.
            const long Tope = 20L * 1024 * 1024;
            var azar = new Random(7);
            string[] avisos =
            {
                "Petición lenta ~/views/listadespachos.aspx {0} ms (sesión {1} ms)",
                "GPS automático, viaje con error: VP-{0} ABC-{1} sin respuesta",
                "ViewState inválido o vencido en /Views/RegistroDespacho.aspx; se envía a /Views/Login.aspx. {0}",
                "No se pudo precargar el historial GPS de B{0}X del {1}/11: timeout",
            };
            string[] errores =
            {
                "Error no controlado en /Views/DetalleOrdenViaje.aspx?id={0} (usuario {1})",
                "No se pudo enviar el correo 'Liquidación {0}' a ju***@correo.pe",
                "Bandeja: no se pudo leer el PDF Factura F{0}-{1}.pdf",
            };
            for (int d = 0; d < 7; d++)
            {
                DateTime dia = Hoy.AddDays(-d);
                using (var w = new StreamWriter(EstadoSistemaServiceTests.Archivo(_carpeta, dia), false, new UTF8Encoding(false), 1 << 16))
                {
                    long escritos = 0;
                    int n = 0;
                    while (escritos < Tope)
                    {
                        n++;
                        string hora = (n % 86400 / 3600).ToString("00", CultureInfo.InvariantCulture) + ":" + (n % 3600 / 60).ToString("00", CultureInfo.InvariantCulture) + ":00.000";
                        int r = azar.Next(100);
                        string linea;
                        if (r < 8)
                            linea = EstadoSistemaServiceTests.Linea(dia, "WRN", string.Format(CultureInfo.InvariantCulture, avisos[n % avisos.Length], azar.Next(100000), azar.Next(1000)), hora);
                        else if (r < 12)
                        {
                            linea = EstadoSistemaServiceTests.Linea(dia, "ERR", string.Format(CultureInfo.InvariantCulture, errores[n % errores.Length], azar.Next(100000), azar.Next(100)), hora)
                                + "\r\nSystem.Data.SqlClient.SqlException (0x80131904): Timeout expired.\r\n   en System.Data.SqlClient.SqlConnection.OnError()"
                                + "\r\n   en WebSGV.Services.Clase.Metodo() en C:\\sgv\\Services\\Clase.cs:línea " + n;
                        }
                        else
                            linea = EstadoSistemaServiceTests.Linea(dia, "INF", "Sesión cerrada: llegó a su duración máxima (usuario " + n + ", /Views/Inicio.aspx)", hora);
                        w.Write(linea);
                        w.Write("\r\n");
                        escritos += linea.Length + 2;
                    }
                }
            }

            EstadoSistemaServiceTests.LeerEnPeru(Path.Combine(_carpeta, "no-existe"), Hoy); // carga de la clase y de las regex
            var reloj = Stopwatch.StartNew();
            EstadoLog log = EstadoSistemaService.LeerLog(_carpeta, Hoy, zonaServidor: TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
                zonaPeru: TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time")); // servidor en otra zona, con horario de verano: el caso más lento
            reloj.Stop();
            _salida.WriteLine("7 archivos, {0:N1} MB en {1} ms", log.Bytes / 1048576.0, reloj.ElapsedMilliseconds);

            Assert.Equal(7, log.Archivos);
            Assert.True(log.Bytes >= 7 * Tope);
            Assert.All(log.PorDia, c => Assert.True(c.Errores > 1000 && c.Avisos > 1000));
            Assert.True(log.Repetidos.Count > 0 && log.Repetidos.Count <= 10);
            Assert.All(log.Repetidos, r => Assert.DoesNotMatch(new Regex(@"\d|correo\.pe|ju\*|Factura|ABC|B\d"), r.Plantilla));
            Assert.True(reloj.ElapsedMilliseconds < 2000, "Leer 7 días de log tardó " + reloj.ElapsedMilliseconds + " ms (máximo 2 000).");
        }
    }
}
