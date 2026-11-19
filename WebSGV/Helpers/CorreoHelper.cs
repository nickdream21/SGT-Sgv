using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Configuración SMTP de appSettings.Secrets.config (claves SmtpHost, SmtpPort, SmtpSSL, SmtpUser,
    /// SmtpPass y SmtpFrom). Nunca se escribe en el log ni en un correo.
    /// </summary>
    public sealed class ConfiguracionSmtp
    {
        public string Host { get; set; }
        public int Puerto { get; set; }
        public bool Ssl { get; set; }
        public string Usuario { get; set; }
        public string Contrasena { get; set; }

        /// <summary>Dirección del remitente: SmtpFrom si existe; si no, SmtpUser.</summary>
        public string Remitente { get; set; }

        /// <summary>Sin servidor o sin usuario no se intenta enviar.</summary>
        public bool EstaCompleta => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Usuario);
    }

    /// <summary>
    /// Único punto de envío de correo de SGV (avisos de liquidaciones, recuperación de contraseña,
    /// avisos de errores). TLS por defecto, 30 s de espera como máximo (corre en segundo plano), y nunca lanza: devuelve
    /// false y deja el motivo en el log (sin el cuerpo del mensaje, que puede llevar un enlace con token).
    /// Es síncrono: quien no deba esperar al SMTP lo llama en segundo plano.
    /// </summary>
    public static class CorreoHelper
    {
        public const string NombreRemitente = "SGV - Sistema de Gestión";

        /// <summary>Espera máxima del SMTP: un servidor caído no debe dejar colgado un hilo.</summary>
        public const int TimeoutMs = 30000;

        public const int PuertoPorDefecto = 587;

        /// <summary>
        /// Remitente: <paramref name="smtpFrom"/> (alias verificado de la cuenta, p. ej. sgv-avisos@) si
        /// viene; si no, <paramref name="smtpUser"/>, para un servidor que aún no tenga SmtpFrom.
        /// </summary>
        public static string Remitente(string smtpFrom, string smtpUser) =>
            !string.IsNullOrWhiteSpace(smtpFrom) ? smtpFrom.Trim() : (smtpUser ?? "").Trim();

        /// <summary>
        /// Arma la configuración con un lector de claves (en el sitio, <c>ConfigurationManager.AppSettings</c>).
        /// SmtpSSL ausente o inválido = true (TLS); solo un "false" explícito lo apaga. SmtpPort inválido = 587.
        /// </summary>
        public static ConfiguracionSmtp LeerConfiguracion(Func<string, string> leer)
        {
            if (leer == null) throw new ArgumentNullException(nameof(leer));
            string usuario = (leer("SmtpUser") ?? "").Trim();
            return new ConfiguracionSmtp
            {
                Host = (leer("SmtpHost") ?? "").Trim(),
                Puerto = int.TryParse(leer("SmtpPort"), out int p) && p > 0 ? p : PuertoPorDefecto,
                Ssl = !bool.TryParse(leer("SmtpSSL"), out bool s) || s,
                Usuario = usuario,
                Contrasena = leer("SmtpPass") ?? "",
                Remitente = Remitente(leer("SmtpFrom"), usuario),
            };
        }

        /// <summary>Configuración del sitio (Web.config + appSettings.Secrets.config).</summary>
        public static ConfiguracionSmtp ConfiguracionDelSitio() =>
            LeerConfiguracion(clave => ConfigurationManager.AppSettings[clave]);

        /// <summary>
        /// Envía un correo con la configuración del sitio. <paramref name="para"/> admite varias
        /// direcciones separadas por comas. True si el SMTP lo aceptó; false (y queda en el log) si
        /// falta configuración, la dirección es inválida o el SMTP falla.
        /// </summary>
        public static bool Enviar(string para, string asunto, string cuerpo, bool esHtml)
        {
            try
            {
                return Enviar(ConfiguracionDelSitio(), para, asunto, cuerpo, esHtml);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Correo: no se pudo leer la configuración SMTP para '{Asunto}'", asunto);
                return false;
            }
        }

        /// <summary>Como <see cref="Enviar(string,string,string,bool)"/>, con una configuración dada.</summary>
        public static bool Enviar(ConfiguracionSmtp config, string para, string asunto, string cuerpo, bool esHtml)
        {
            try
            {
                if (config == null || !config.EstaCompleta)
                {
                    LogSGV.Advertencia("SMTP no configurado: no se envió el correo '{Asunto}'", asunto);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(para))
                {
                    LogSGV.Advertencia("Correo sin destinatario: no se envió '{Asunto}'", asunto);
                    return false;
                }

                using (var mail = new MailMessage())
                using (var smtp = new SmtpClient(config.Host, config.Puerto))
                {
                    mail.From = new MailAddress(config.Remitente, NombreRemitente);
                    mail.To.Add(para);
                    mail.Subject = asunto ?? "";
                    mail.IsBodyHtml = esHtml;
                    mail.Body = cuerpo ?? "";
                    mail.BodyEncoding = System.Text.Encoding.UTF8;
                    mail.SubjectEncoding = System.Text.Encoding.UTF8;

                    smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    smtp.EnableSsl = config.Ssl;
                    smtp.Timeout = TimeoutMs;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(config.Usuario, config.Contrasena);
                    smtp.Send(mail);
                }
                return true;
            }
            catch (Exception ex)
            {
                // Los errores SMTP no incluyen el cuerpo (donde puede ir un enlace con token).
                LogSGV.Error(ex, "No se pudo enviar el correo '{Asunto}' a {Destinatario}", asunto, OcultarDestinatario(para));
                return false;
            }
        }

        /// <summary>
        /// Destinatario para el log, sin la parte local: "***@dominio.com" (varios separados por comas). El log
        /// no debe guardar el correo de un usuario (p. ej. el de la recuperación de contraseña).
        /// </summary>
        public static string OcultarDestinatario(string para)
        {
            if (string.IsNullOrWhiteSpace(para)) return "";
            var partes = new System.Collections.Generic.List<string>();
            foreach (string d in para.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = d.Trim();
                if (t.Length == 0) continue;
                int arroba = t.LastIndexOf('@');
                partes.Add(arroba >= 0 ? "***" + t.Substring(arroba).TrimEnd('>') : "***");
            }
            return string.Join(", ", partes);
        }
    }
}
