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
    /// </summary>
    public class GpsAutomatico : IHttpHandler, IReadOnlySessionState
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.ContentType = "application/json";
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            ctx.Response.Cache.SetNoStore();

            if (ctx.Request.QueryString["estado"] == "1")
            {
                if (!ExigirTablero(ctx)) return;
                object ultima = DbHelper.EjecutarEscalar(
                    "SELECT MAX(fhUltimaConsultaGps) FROM SeguimientoExportacion WHERE activo = 1 AND ISNULL(estado, '') NOT IN ('FINALIZADO', 'CANCELADO')");
                Responder(ctx, 200, new
                {
                    ultima = ultima == null || ultima == DBNull.Value ? null : Convert.ToDateTime(ultima).ToString("yyyy-MM-ddTHH:mm:ss"),
                    enCurso = ActualizacionGpsAutomatica.EnCurso,
                    ultimoFin = ActualizacionGpsAutomatica.UltimoFin?.ToString("yyyy-MM-ddTHH:mm:ss"),
                    mensaje = ActualizacionGpsAutomatica.UltimoMensaje,
                    conFallas = ActualizacionGpsAutomatica.UltimoConFallas,
                });
                return;
            }

            // El botón "Actualizar GPS" del tablero: la consulta corre en segundo plano y esta llamada responde
            // al instante. Como postback tardaba hasta 80 s con la sesión tomada, y ASP.NET atiende de a una
            // petición por sesión: cualquier otra pantalla que se abriera mientras tanto (entrar a un viaje)
            // quedaba esperando a que terminara.
            // Solo POST con Content-Type: application/json (E4-8): un formulario de otro sitio solo puede mandar
            // text/plain, x-www-form-urlencoded o multipart sin preflight, así que no puede lanzarla con la cookie.
            if (ctx.Request.QueryString["iniciar"] == "1")
            {
                bool conSesion = SesionHelper.HaySesion(ctx.Session);
                int rechazo = RechazoIniciar(ctx.Request.HttpMethod, PuedeUsarTablero(conSesion, SesionHelper.Rol(ctx.Session)), ctx.Request.ContentType);
                if (rechazo == 405) { ctx.Response.AppendHeader("Allow", "POST"); Responder(ctx, 405, new { error = "Solo POST" }); return; }
                if (rechazo == 403) { Responder(ctx, 403, new { error = conSesion ? "Sin permiso" : "Sesión vencida" }); return; }
                if (rechazo == 415) { Responder(ctx, 415, new { error = "Se espera Content-Type: application/json" }); return; }
                bool pedida = ActualizacionGpsAutomatica.Iniciar(aPedido: true);
                Responder(ctx, 200, new { iniciada = pedida, enCurso = ActualizacionGpsAutomatica.EnCurso });
                return;
            }

            string clave = ConfigurationManager.AppSettings["GpsAutomaticoClave"];
            if (string.IsNullOrWhiteSpace(clave) || !IgualSeguro(clave, ctx.Request.Headers["X-SGV-Clave"]))
            {
                LogSGV.Advertencia("GPS automático: llamada sin clave válida desde {Ip}", IpClienteHelper.Obtener(ctx.Request));
                Responder(ctx, 403, new { error = "Clave inválida" });
                return;
            }

            bool iniciada = ActualizacionGpsAutomatica.Iniciar();
            Responder(ctx, iniciada ? 202 : 200, new
            {
                iniciada,
                motivo = iniciada ? null : ActualizacionGpsAutomatica.EnCurso ? "Ya hay una actualización en curso" : "La anterior fue hace menos de 5 minutos",
                ultimoInicio = ActualizacionGpsAutomatica.UltimoInicio?.ToString("yyyy-MM-ddTHH:mm:ss"),
                ultimoFin = ActualizacionGpsAutomatica.UltimoFin?.ToString("yyyy-MM-ddTHH:mm:ss"),
                ultimoResumen = ActualizacionGpsAutomatica.UltimoResumen,
            });
        }

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

        /// <summary>Responde 403 y devuelve false si la sesión venció o el rol no tiene permiso.</summary>
        private static bool ExigirTablero(HttpContext ctx)
        {
            bool conSesion = SesionHelper.HaySesion(ctx.Session);
            if (PuedeUsarTablero(conSesion, SesionHelper.Rol(ctx.Session))) return true;
            Responder(ctx, 403, new { error = conSesion ? "Sin permiso" : "Sesión vencida" });
            return false;
        }

        private static void Responder(HttpContext ctx, int codigo, object cuerpo)
        {
            ctx.Response.StatusCode = codigo;
            ctx.Response.Write(JsonConvert.SerializeObject(cuerpo));
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
