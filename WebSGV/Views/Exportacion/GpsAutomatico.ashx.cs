using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.SessionState;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;

namespace WebSGV.Views.Exportacion
{
    /// <summary>
    /// Actualización automática del GPS del seguimiento.
    /// - Con el encabezado <c>X-SGV-Clave</c> igual a <c>GpsAutomaticoClave</c> (appSettings.Secrets.config): inicia
    ///   una corrida en segundo plano y responde al instante (lo llama cron-job.org o el Programador de tareas).
    /// - Con <c>?estado=1</c>, sesión iniciada y permiso del seguimiento: la hora de la última consulta al GPS de los viajes abiertos, para que
    ///   el tablero abierto avise que hay datos nuevos.
    /// - Con <c>?iniciar=1</c> (POST JSON, sesión y permiso): el botón "Actualizar GPS" del tablero.
    /// La decisión está en <see cref="Atender"/>, sin HttpContext (E6-9).
    /// </summary>
    public class GpsAutomatico : IHttpHandler, IReadOnlySessionState
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            ctx.Response.Cache.SetNoStore();

            var solicitud = new SolicitudGps
            {
                Estado = ctx.Request.QueryString["estado"],
                Iniciar = ctx.Request.QueryString["iniciar"],
                Metodo = ctx.Request.HttpMethod,
                ContentType = ctx.Request.ContentType,
                ConSesion = SesionHelper.HaySesion(ctx.Session),
                Rol = SesionHelper.Rol(ctx.Session),
                ClaveConfigurada = ConfigurationManager.AppSettings["GpsAutomaticoClave"],
                ClaveRecibida = ctx.Request.Headers["X-SGV-Clave"],
            };
            var respuesta = Atender(solicitud, ActualizacionGpsAutomatica.Actual, SeguimientoViajesService.UltimaConsultaGpsAbiertos);
            if (respuesta.ClaveInvalida)
                LogSGV.Advertencia("GPS automático: llamada sin clave válida desde {Ip}", IpClienteHelper.Obtener(ctx.Request));
            if (respuesta.Allow != null) ctx.Response.AppendHeader("Allow", respuesta.Allow);
            ctx.Response.StatusCode = respuesta.Codigo;
            ctx.Response.Write(JsonConvert.SerializeObject(respuesta.Cuerpo));
        }

        /// <summary>Lo que trae la llamada, ya leído del HttpContext (para decidir sin él).</summary>
        internal sealed class SolicitudGps
        {
            public string Estado { get; set; }
            public string Iniciar { get; set; }
            public string Metodo { get; set; }
            public string ContentType { get; set; }
            public bool ConSesion { get; set; }
            public string Rol { get; set; }
            public string ClaveConfigurada { get; set; }
            public string ClaveRecibida { get; set; }
        }

        /// <summary>Código, cuerpo JSON y encabezado Allow; <see cref="ClaveInvalida"/> pide registrar la IP en el log.</summary>
        internal sealed class RespuestaGps
        {
            public int Codigo { get; set; }
            public object Cuerpo { get; set; }
            public string Allow { get; set; }
            public bool ClaveInvalida { get; set; }
        }

        /// <summary>
        /// Decide la respuesta sin HttpContext. Tres llamadas:
        /// - <c>?estado=1</c> con permiso del tablero: la última consulta al GPS (<paramref name="ultimaConsulta"/>, va a la BD) y el estado del motor.
        /// - <c>?iniciar=1</c>: el botón "Actualizar GPS". La consulta corre en segundo plano y esta llamada responde al
        ///   instante: como postback tardaba hasta 80 s con la sesión tomada, y ASP.NET atiende de a una petición por
        ///   sesión, cualquier otra pantalla quedaba esperando. Solo POST con Content-Type: application/json (E4-8): un
        ///   formulario de otro sitio solo puede mandar text/plain, x-www-form-urlencoded o multipart sin preflight, así
        ///   que no puede lanzarla con la cookie.
        /// - Lo demás es el programador externo: sin clave configurada, sin encabezado o con otra clave, 403 y no inicia nada.
        /// </summary>
        internal static RespuestaGps Atender(SolicitudGps s, ActualizacionGpsAutomatica.Motor motor, Func<DateTime?> ultimaConsulta)
        {
            if (s.Estado == "1")
            {
                if (!PuedeUsarTablero(s.ConSesion, s.Rol)) return SinPermiso(s.ConSesion);
                DateTime? ultima = ultimaConsulta();
                return new RespuestaGps
                {
                    Codigo = 200,
                    Cuerpo = new
                    {
                        ultima = ultima?.ToString("yyyy-MM-ddTHH:mm:ss"),
                        enCurso = motor.EnCurso,
                        ultimoFin = motor.UltimoFin?.ToString("yyyy-MM-ddTHH:mm:ss"),
                        mensaje = motor.UltimoMensaje,
                        conFallas = motor.UltimoConFallas,
                    },
                };
            }

            if (s.Iniciar == "1")
            {
                int rechazo = RechazoIniciar(s.Metodo, PuedeUsarTablero(s.ConSesion, s.Rol), s.ContentType);
                if (rechazo == 405) return new RespuestaGps { Codigo = 405, Allow = "POST", Cuerpo = new { error = "Solo POST" } };
                if (rechazo == 403) return SinPermiso(s.ConSesion);
                if (rechazo == 415) return new RespuestaGps { Codigo = 415, Cuerpo = new { error = "Se espera Content-Type: application/json" } };
                bool pedida = motor.Iniciar(aPedido: true);
                return new RespuestaGps { Codigo = 200, Cuerpo = new { iniciada = pedida, enCurso = motor.EnCurso } };
            }

            if (!ClaveValida(s.ClaveConfigurada, s.ClaveRecibida))
                return new RespuestaGps { Codigo = 403, ClaveInvalida = true, Cuerpo = new { error = "Clave inválida" } };

            bool iniciada = motor.Iniciar(aPedido: false);
            return new RespuestaGps
            {
                Codigo = iniciada ? 202 : 200,
                Cuerpo = new
                {
                    iniciada,
                    motivo = iniciada ? null : motor.EnCurso ? "Ya hay una actualización en curso" : "La anterior fue hace menos de 5 minutos",
                    ultimoInicio = motor.UltimoInicio?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ultimoFin = motor.UltimoFin?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    ultimoResumen = motor.UltimoResumen,
                },
            };
        }

        /// <summary>La clave del programador: configurada (no vacía) y la recibida igual a ella.</summary>
        internal static bool ClaveValida(string configurada, string recibida) =>
            !string.IsNullOrWhiteSpace(configurada) && IgualSeguro(configurada, recibida);

        private static RespuestaGps SinPermiso(bool conSesion) =>
            new RespuestaGps { Codigo = 403, Cuerpo = new { error = conSesion ? "Sin permiso" : "Sesión vencida" } };

        /// <summary>
        /// Quién puede usar el tablero de seguimiento (estado e "Actualizar GPS"): sesión iniciada y la
        /// misma regla que la página (sección SEGUIMIENTO_EXPORTACION). Pura, para probarla sin HttpContext.
        /// </summary>
        public static bool PuedeUsarTablero(bool conSesion, string rol) =>
            conSesion && RolesHelper.TienePermiso("SEGUIMIENTO_EXPORTACION", rol);

        /// <summary>
        /// Decide si se rechaza "Actualizar GPS" (<c>?iniciar=1</c>) antes de lanzarla. Pura, para probarla sin
        /// HttpContext. 0 = seguir; 405 si no es POST; 403 sin sesión o sin permiso (antes que el 415: sin sesión
        /// responde lo de siempre); 415 si el cuerpo no se declara <c>application/json</c>.
        /// </summary>
        public static int RechazoIniciar(string metodo, bool puedeUsarTablero, string contentType)
        {
            if (!string.Equals(metodo, "POST", StringComparison.Ordinal)) return 405;
            if (!puedeUsarTablero) return 403;
            if (!EsContentTypeJson(contentType)) return 415;
            return 0;
        }

        /// <summary>True si el Content-Type es <c>application/json</c> (sin distinguir mayúsculas, con o sin <c>; charset=...</c>).</summary>
        public static bool EsContentTypeJson(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return false;
            string tipo = contentType.Split(';')[0].Trim();
            return string.Equals(tipo, "application/json", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Compara sin cortar en el primer carácter distinto (no deja adivinar la clave por el tiempo de respuesta).</summary>
        private static bool IgualSeguro(string esperada, string recibida)
        {
            if (recibida == null) return false;
            byte[] a = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(esperada));
            byte[] b = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(recibida));
            int dif = 0;
            for (int i = 0; i < a.Length; i++) dif |= a[i] ^ b[i];
            return dif == 0;
        }
    }
}
