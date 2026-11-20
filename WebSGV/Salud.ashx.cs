using System;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Caching;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Operacion;

namespace WebSGV
{
    /// <summary>
    /// Página de salud para la vigilancia externa (UptimeRobot, E1-6). Anónima a propósito y la respuesta solo
    /// dice {"ok":true|false}; el detalle va al log.
    /// - Sin parámetros: la BD responde a un SELECT 1 en 5 s → 200; si no → 503.
    /// - <c>?gps=1</c>: además 503 si el GPS automático dejó de correr (<see cref="EstadoSalud.EvaluarConGps"/>).
    /// El resultado se reutiliza unos segundos (<see cref="EstadoSalud.DuracionCache"/>) para que llamadas anónimas
    /// seguidas no carguen la BD. Solo lee: no escribe en la BD ni en la auditoría.
    /// No implementa IRequiresSessionState: sin sesión, Global.asax (sesión vigente, cambio de contraseña) no la
    /// toca y el vigilante no crea sesiones (EstadoSaludTests lo fija).
    /// </summary>
    public class Salud : IHttpHandler
    {
        /// <summary>Tiempo límite de la conexión y del SELECT 1.</summary>
        public const int SegundosLimiteBd = 5;

        /// <summary>Cuándo arrancó la aplicación (UTC; lo fija Application_Start): tras un reciclaje no se sabe cuándo corrió el GPS.</summary>
        public static DateTime InicioAplicacionUtc { get; set; } = DateTime.UtcNow;

        private static readonly object CandadoLog = new object();
        private static string _estadoRegistrado;
        private static DateTime? _registradoEnUtc;

        public bool IsReusable => true;

        public void ProcessRequest(HttpContext ctx)
        {
            HttpResponse resp = ctx.Response;
            resp.ContentType = "application/json";
            resp.Cache.SetCacheability(HttpCacheability.NoCache);
            resp.Cache.SetNoStore();
            resp.TrySkipIisCustomErrors = true; // el 503 sale con su JSON, no con la página de error de IIS

            // POST y otros verbos que IIS entrega al handler: 405. PUT, DELETE y OPTIONS los responde IIS antes.
            if (!EsMetodoPermitido(ctx.Request.HttpMethod))
            {
                resp.StatusCode = 405;
                resp.AppendHeader("Allow", "GET, HEAD");
                resp.Write("{\"ok\":false}");
                return;
            }

            // Sin validar: un "?gps=<script>" no debe dar 500 (HttpRequestValidationException); solo se compara con "1".
            bool conGps = ctx.Request.Unvalidated.QueryString["gps"] == "1";
            ResultadoSalud r = Cacheado("SGV|Salud|" + (conGps ? "gps" : "bd"), EstadoSalud.DuracionCache(conGps),
                () => Evaluar(conGps, DateTime.UtcNow));
            resp.StatusCode = r.CodigoHttp;
            resp.Write(r.ComoJson()); // en HEAD, ASP.NET no envía el cuerpo
        }

        /// <summary>UptimeRobot usa HEAD o GET; los demás verbos que llegan al handler reciben 405.</summary>
        public static bool EsMetodoPermitido(string metodo) =>
            string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(metodo, "HEAD", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Devuelve el valor guardado en <see cref="HttpRuntime.Cache"/> con <paramref name="clave"/> o lo calcula y lo
        /// guarda por <paramref name="duracion"/> (vencimiento absoluto). Dos llamadas a la vez pueden calcularlo las dos: no importa.
        /// </summary>
        public static T Cacheado<T>(string clave, TimeSpan duracion, Func<T> calcular) where T : class
        {
            if (HttpRuntime.Cache[clave] is T guardado) return guardado;
            T valor = calcular();
            HttpRuntime.Cache.Insert(clave, valor, null, DateTime.UtcNow.Add(duracion), Cache.NoSlidingExpiration);
            return valor;
        }

        /// <summary>
        /// Consulta real de Salud (solo lectura, sin caché). <paramref name="ahoraUtc"/> en UTC. Los viajes activos son los
        /// mismos que consulta el GPS automático (<see cref="ActualizacionGpsAutomatica.ViajesDelTablero"/> +
        /// <see cref="SeguimientoViajesService.AbiertosParaGps"/>); el 503 lo decide la última corrida programada
        /// (<see cref="ActualizacionGpsAutomatica.UltimoInicioProgramado"/>) y, si el cron corre, la última respuesta válida
        /// de Onway en una corrida programada (<see cref="ActualizacionGpsAutomatica.UltimaRespuestaOnwayProgramada"/>).
        /// fhUltimaConsultaGps (hora de Perú) solo deja una advertencia en el log si es muy vieja.
        /// </summary>
        public static ResultadoSalud Evaluar(bool conGps, DateTime ahoraUtc)
        {
            if (!BdResponde())
                return EstadoSalud.Evaluar(false);
            if (!conGps)
                return EstadoSalud.Evaluar(true);

            try
            {
                DateTime ahoraPeru = FechaHelper.ConvertirDeUtc(DateTime.SpecifyKind(ahoraUtc, DateTimeKind.Utc));
                var activos = SeguimientoViajesService.AbiertosParaGps(ActualizacionGpsAutomatica.ViajesDelTablero(ahoraPeru.Date), ahoraPeru.Date).ToList();
                DateTime? ultimaConsulta = activos.Max(v => v.UltimaConsultaGps);
                ResultadoSalud r = EstadoSalud.EvaluarConGpsYOnway(true, activos.Count, InicioAplicacionUtc,
                    ActualizacionGpsAutomatica.UltimoInicioProgramado, ActualizacionGpsAutomatica.UltimaRespuestaOnwayProgramada, ahoraUtc,
                    ultimaProgramadaConError: ActualizacionGpsAutomatica.UltimaProgramadaConError);
                bool consultaVieja = EstadoSalud.ConsultaVieja(activos.Count, ultimaConsulta, ahoraPeru);
                RegistrarAviso(r, consultaVieja, activos.Count, ultimaConsulta, ahoraUtc);
                return r;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Salud: no se pudo revisar el GPS automático");
                return EstadoSalud.Evaluar(false);
            }
        }

        /// <summary>El detalle del GPS va al log, solo cuando cambia o como mucho una vez por hora (EstadoSalud.DebeRegistrar).</summary>
        private static void RegistrarAviso(ResultadoSalud r, bool consultaVieja, int viajes, DateTime? ultimaConsulta, DateTime ahoraUtc)
        {
            string estado = (r.Ok ? "" : r.Gps) + (consultaVieja ? "|consulta_vieja" : "");
            lock (CandadoLog)
            {
                if (!EstadoSalud.DebeRegistrar(_estadoRegistrado, _registradoEnUtc, estado, ahoraUtc)) return;
                _estadoRegistrado = estado;
                _registradoEnUtc = ahoraUtc;
            }
            if (estado.Length == 0)
                LogSGV.Info("Salud: el GPS automático volvió a estar al día ({Gps}, {Viajes} viajes activos)", r.Gps, viajes);
            else
                LogSGV.Advertencia("Salud: GPS {Gps} ({Regla}), {Minutos} min desde la última corrida programada, {MinutosOnway} min " +
                    "desde la última respuesta válida de Onway; {Viajes} viajes activos, última consulta GPS {Ultima}{Vieja}",
                    r.Gps, ReglaQueFallo(r), r.Minutos, r.MinutosOnway, viajes, ultimaConsulta,
                    consultaVieja ? $" (más de {EstadoSalud.UmbralConsultaViejaMinutos} min)" : "");
        }

        /// <summary>Qué regla dio el 503, en palabras, para el log.</summary>
        public static string ReglaQueFallo(ResultadoSalud r)
        {
            if (r == null || r.Ok) return "sin falla";
            switch (r.Gps)
            {
                case EstadoSalud.GpsAtrasado: return $"el cron no corre hace más de {EstadoSalud.UmbralGpsMinutos} min";
                case EstadoSalud.GpsOnwaySinRespuesta: return $"el cron corre pero Onway no responde bien hace más de {EstadoSalud.UmbralOnwayMinutos} min";
                case EstadoSalud.GpsCronConError: return $"el cron corre pero su última corrida terminó con error (sin respuesta de Onway hace más de {EstadoSalud.UmbralOnwayMinutos} min)";
                default: return "la base de datos no respondió";
            }
        }

        /// <summary>SELECT 1 con <see cref="SegundosLimiteBd"/> s para conectar y para ejecutar.</summary>
        public static bool BdResponde()
        {
            try
            {
                var cadena = new SqlConnectionStringBuilder(DbHelper.ConnectionString) { ConnectTimeout = SegundosLimiteBd };
                using (var conn = new SqlConnection(cadena.ConnectionString))
                using (var cmd = new SqlCommand("SELECT 1", conn) { CommandTimeout = SegundosLimiteBd })
                {
                    conn.Open();
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Salud: la base de datos no respondió");
                return false;
            }
        }
    }
}
