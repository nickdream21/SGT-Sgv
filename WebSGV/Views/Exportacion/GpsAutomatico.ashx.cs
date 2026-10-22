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
    /// - Con <c>?estado=1</c> y sesión iniciada: la hora de la última consulta al GPS de los viajes abiertos, para que
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
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";

            if (ctx.Request.QueryString["estado"] == "1")
            {
                if (ctx.Session?["UsuarioID"] == null) { Responder(ctx, 403, new { error = "Sesión vencida" }); return; }
                object ultima = DbHelper.EjecutarEscalar(
                    "SELECT MAX(fhUltimaConsultaGps) FROM SeguimientoExportacion WHERE activo = 1 AND ISNULL(estado, '') NOT IN ('FINALIZADO', 'CANCELADO')");
                Responder(ctx, 200, new
                {
                    ultima = ultima == null || ultima == DBNull.Value ? null : Convert.ToDateTime(ultima).ToString("yyyy-MM-ddTHH:mm:ss"),
                    enCurso = ActualizacionGpsAutomatica.EnCurso,
                });
                return;
            }

            string clave = ConfigurationManager.AppSettings["GpsAutomaticoClave"];
            if (string.IsNullOrWhiteSpace(clave) || !IgualSeguro(clave, ctx.Request.Headers["X-SGV-Clave"]))
            {
                LogSGV.Advertencia("GPS automático: llamada sin clave válida desde {Ip}", ctx.Request.UserHostAddress);
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
