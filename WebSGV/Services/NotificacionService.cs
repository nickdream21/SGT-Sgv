using System;
using System.Configuration;
using System.Data;
using System.Net;
using System.Net.Mail;
using System.Web;
using WebSGV.Helpers;

namespace WebSGV.Services
{
    /// <summary>
    /// Envía notificaciones por email para eventos clave del flujo de liquidaciones.
    /// Todas las llamadas son fire-and-forget: si el envío falla, se registra en Debug
    /// pero nunca interrumpe la operación principal.
    /// </summary>
    public static class NotificacionService
    {
        // ─── Punto de envío central ──────────────────────────────────────────────

        private static void Enviar(string para, string asunto, string cuerpoHtml)
        {
            try
            {
                string host = ConfigurationManager.AppSettings["SmtpHost"];
                string user = ConfigurationManager.AppSettings["SmtpUser"];
                string pass = ConfigurationManager.AppSettings["SmtpPass"];

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                    return; // SMTP no configurado

                int  port = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int p) ? p : 587;
                bool ssl  = bool.TryParse(ConfigurationManager.AppSettings["SmtpSSL"],  out bool s) && s;

                var mail = new MailMessage();
                mail.From       = new MailAddress(user, "SGV - Sistema de Gestión");
                mail.To.Add(para);
                mail.Subject    = asunto;
                mail.IsBodyHtml = true;
                mail.Body       = cuerpoHtml;

                var smtp = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(user, pass),
                    EnableSsl   = ssl
                };
                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificacionService] Error enviando a {para}: {ex.Message}");
            }
        }

        // ─── Notificaciones públicas ─────────────────────────────────────────────

        /// <summary>
        /// Avisa al conductor que su liquidación fue rechazada e indica el motivo.
        /// Se llama desde LiquidacionesPendientes después de sp_RechazarLiquidacion exitoso.
        /// </summary>
        public static void NotificarLiquidacionRechazada(int idOrdenViaje, string numeroOrden, string observaciones)
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT c.correo,
                           c.nombre + ' ' + c.apPaterno AS nombreCompleto
                    FROM   OrdenViaje ov
                    JOIN   Conductor  c ON ov.idConductor = c.idConductor
                    WHERE  ov.idOrdenViaje = @id",
                    DbHelper.Param("@id", idOrdenViaje));

                if (dt.Rows.Count == 0) return;

                string correo = dt.Rows[0]["correo"]?.ToString();
                string nombre = dt.Rows[0]["nombreCompleto"]?.ToString() ?? "Conductor";

                if (string.IsNullOrWhiteSpace(correo)) return;

                string asunto = $"Liquidación rechazada – Orden {numeroOrden}";
                string cuerpo = $@"
<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:auto;'>
  <h2 style='color:#c0392b;'>Liquidación Rechazada</h2>
  <p>Hola {HtmlEncode(nombre)},</p>
  <p>Tu liquidación con número de orden <strong>{HtmlEncode(numeroOrden)}</strong>
     ha sido <strong>rechazada</strong> por la administración.</p>
  <p><strong>Motivo del rechazo:</strong></p>
  <blockquote style='border-left:4px solid #c0392b;padding:8px 12px;background:#fdf2f2;color:#555;'>
    {HtmlEncode(observaciones)}
  </blockquote>
  <p>Accede al sistema para revisar el detalle y reenviar la liquidación corregida.</p>
  <br><p>Saludos,<br><em>Administración – SGV</em></p>
</body></html>";

                Enviar(correo, asunto, cuerpo);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificacionService] NotificarLiquidacionRechazada: {ex.Message}");
            }
        }

        /// <summary>
        /// Avisa a todos los admins/supervisores activos que hay una nueva liquidación
        /// pendiente de revisión.
        /// Se llama después de que el conductor firma y envía la liquidación a revisión.
        /// </summary>
        public static void NotificarLiquidacionPendiente(string numeroOrden, string nombreConductor)
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT email, nombre FROM Usuarios
                    WHERE  rol IN ('ADMIN','ADMINISTRADOR','SUPERVISOR',
                                   'ADMINISTRADOR DE TRANSPORTE','ADMINISTRADOR DE SISTEMA')
                      AND  activo = 1
                      AND  email  IS NOT NULL
                      AND  LTRIM(RTRIM(email)) <> ''");

                if (dt.Rows.Count == 0) return;

                string asunto = $"Nueva liquidación pendiente – Orden {numeroOrden}";
                string cuerpo = $@"
<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:auto;'>
  <h2 style='color:#2980b9;'>Nueva Liquidación Pendiente</h2>
  <p>El conductor <strong>{HtmlEncode(nombreConductor)}</strong> ha enviado
     una liquidación que requiere revisión.</p>
  <table style='border-collapse:collapse;margin:12px 0;'>
    <tr><td style='padding:4px 8px;font-weight:bold;'>Número de orden:</td>
        <td style='padding:4px 8px;'>{HtmlEncode(numeroOrden)}</td></tr>
  </table>
  <p>Ingresa al sistema para revisar y aprobar o rechazar la liquidación.</p>
  <br><p>Saludos,<br><em>Sistema SGV</em></p>
</body></html>";

                foreach (DataRow row in dt.Rows)
                {
                    string email = row["email"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(email))
                        Enviar(email, asunto, cuerpo);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificacionService] NotificarLiquidacionPendiente: {ex.Message}");
            }
        }

        private static string HtmlEncode(string s) =>
            HttpUtility.HtmlEncode(s ?? "");
    }
}
