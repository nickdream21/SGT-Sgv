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
    /// Todas las llamadas son fire-and-forget: si el envío falla (o el SMTP no está
    /// configurado), se registra con <see cref="LogSGV"/> pero nunca interrumpe la operación principal.
    /// </summary>
    public static class NotificacionService
    {
        // ─── Destinatarios de la operación ───────────────────────────────────────

        /// <summary>
        /// Usuarios activos con correo cuyo rol está en <see cref="RolesHelper.RolesAvisosOperacion"/>
        /// (administradores según <see cref="RolesHelper.EsRolAdmin"/> + SUPERVISOR). Los roles van
        /// como parámetros: una sola fuente de verdad, sin copiar la lista en SQL.
        /// </summary>
        private static DataTable ConsultarCorreosAvisosOperacion(string columnas)
        {
            var parametros = ParametrosRoles(RolesHelper.RolesAvisosOperacion(), out string lista);
            return DbHelper.ConsultarTabla($@"
                    SELECT {columnas} FROM Usuarios
                    WHERE  UPPER(LTRIM(RTRIM(rol))) IN ({lista})
                      AND  activo = 1
                      AND  email  IS NOT NULL
                      AND  LTRIM(RTRIM(email)) <> ''", parametros);
        }

        /// <summary>
        /// Arma los parámetros @rol0, @rol1, ... (un valor por rol) y la lista "@rol0, @rol1" para
        /// un <c>IN (...)</c>. Solo los nombres de parámetro entran en el texto SQL.
        /// </summary>
        internal static System.Data.SqlClient.SqlParameter[] ParametrosRoles(
            System.Collections.Generic.IReadOnlyList<string> roles, out string lista)
        {
            var ps = new System.Data.SqlClient.SqlParameter[roles.Count];
            var nombres = new string[roles.Count];
            for (int i = 0; i < roles.Count; i++)
            {
                nombres[i] = "@rol" + i;
                ps[i] = DbHelper.Param(nombres[i], roles[i]);
            }
            lista = string.Join(", ", nombres);
            return ps;
        }

        // ─── Punto de envío central ──────────────────────────────────────────────

        private static void Enviar(string para, string asunto, string cuerpoHtml)
        {
            try
            {
                string host = ConfigurationManager.AppSettings["SmtpHost"];
                string user = ConfigurationManager.AppSettings["SmtpUser"];
                string pass = ConfigurationManager.AppSettings["SmtpPass"];

                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
                {
                    LogSGV.Advertencia("SMTP no configurado: no se envió el correo '{Asunto}'", asunto);
                    return;
                }

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
                LogSGV.Error(ex, "No se pudo enviar el correo '{Asunto}' a {Destinatario}", asunto, para);
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
                LogSGV.Error(ex, "NotificacionService.NotificarLiquidacionRechazada: no se pudo preparar la notificación");
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
                DataTable dt = ConsultarCorreosAvisosOperacion("email, nombre");

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
                LogSGV.Error(ex, "NotificacionService.NotificarLiquidacionPendiente: no se pudo preparar la notificación");
            }
        }

        /// <summary>
        /// Avisa al conductor que la administración revisó su liquidación y que debe revisar la
        /// liquidación final (dinero entregado, descuentos y reintegros con su motivo) y firmar su
        /// conformidad. Sin correo registrado no se envía nada: el aviso también aparece en su panel.
        /// </summary>
        public static void NotificarLiquidacionPorConformar(int idOrdenViaje, string numeroOrden)
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT COALESCE(NULLIF(LTRIM(RTRIM(c.correo)), ''), NULLIF(LTRIM(RTRIM(u.email)), '')) AS correo,
                           c.nombre + ' ' + c.apPaterno AS nombreCompleto
                    FROM   OrdenViaje ov
                    JOIN   Conductor  c ON ov.idConductor = c.idConductor
                    LEFT JOIN Usuarios u ON u.idConductor = c.idConductor AND u.activo = 1
                    WHERE  ov.idOrdenViaje = @id",
                    DbHelper.Param("@id", idOrdenViaje));

                if (dt.Rows.Count == 0) return;
                string correo = dt.Rows[0]["correo"]?.ToString();
                string nombre = dt.Rows[0]["nombreCompleto"]?.ToString() ?? "Conductor";
                if (string.IsNullOrWhiteSpace(correo)) return;

                string asunto = $"Revisa y firma tu liquidación – Orden {numeroOrden}";
                string cuerpo = $@"
<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:auto;'>
  <h2 style='color:#0B3D91;'>Tu liquidación fue revisada</h2>
  <p>Hola {HtmlEncode(nombre)},</p>
  <p>La administración revisó tu liquidación de la orden <strong>{HtmlEncode(numeroOrden)}</strong>
     y registró el dinero que te entregaron y los descuentos o reintegros, cada uno con su motivo.</p>
  <p>Ingresa al sistema, revisa la liquidación final y <strong>firma tu conformidad</strong>. Si no estás
     de acuerdo, puedes enviar tu observación desde la misma pantalla.</p>
  <br><p>Saludos,<br><em>Administración – SGV</em></p>
</body></html>";

                Enviar(correo, asunto, cuerpo);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "NotificacionService.NotificarLiquidacionPorConformar: no se pudo preparar la notificación");
            }
        }

        /// <summary>Avisa a la administración que un conductor observó la liquidación final.</summary>
        public static void NotificarObservacionConductor(string numeroOrden, string nombreConductor, string observacion)
        {
            try
            {
                DataTable dt = ConsultarCorreosAvisosOperacion("email");
                if (dt.Rows.Count == 0) return;

                string asunto = $"Liquidación observada por el conductor – Orden {numeroOrden}";
                string cuerpo = $@"
<html><body style='font-family:Arial,sans-serif;max-width:600px;margin:auto;'>
  <h2 style='color:#b45309;'>El conductor no dio su conformidad</h2>
  <p>El conductor <strong>{HtmlEncode(nombreConductor)}</strong> observó la liquidación final de la orden
     <strong>{HtmlEncode(numeroOrden)}</strong>:</p>
  <blockquote style='border-left:4px solid #b45309;padding:8px 12px;background:#fffbeb;color:#555;'>
    {HtmlEncode(observacion)}
  </blockquote>
  <p>La liquidación volvió a <em>Liquidaciones pendientes</em> para revisarla de nuevo.</p>
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
                LogSGV.Error(ex, "NotificacionService.NotificarObservacionConductor: no se pudo preparar la notificación");
            }
        }

        private static string HtmlEncode(string s) =>
            HttpUtility.HtmlEncode(s ?? "");
    }
}
