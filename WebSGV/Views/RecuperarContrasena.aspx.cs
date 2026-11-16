using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Net;
using System.Net.Mail;
using System.Web.Hosting;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// Solicitud de enlace para restablecer la contraseña. Muestra siempre el mismo mensaje
    /// (<see cref="SolicitudRecuperacion.MensajeUnico"/>) y envía el correo en segundo plano, para
    /// que ni el texto ni el tiempo de respuesta revelen qué cuentas existen. Reglas y límites en
    /// <see cref="SolicitudRecuperacion"/>.
    /// </summary>
    public partial class RecuperarContrasena : PaginaBase
    {
        protected System.Web.UI.WebControls.TextBox txtUsuarioEmail;
        protected System.Web.UI.WebControls.Panel pnlMensaje;
        protected System.Web.UI.WebControls.Label lblMensaje;

        /// <summary>Toda solicitud tarda al menos esto, exista o no la cuenta.</summary>
        private const int TiempoMinimoRespuestaMs = 1000;

        protected void Page_Load(object sender, EventArgs e)
        {
        }

        protected void btnEnviar_Click(object sender, EventArgs e)
        {
            string usuarioEmail = txtUsuarioEmail.Text.Trim();

            if (string.IsNullOrEmpty(usuarioEmail))
            {
                MostrarMensaje("Por favor, ingrese su usuario o correo electrónico.", false);
                return;
            }

            // Tiempo de respuesta fijo: sin esto, un usuario inexistente respondía en ~185 ms y uno
            // existente en ~300 ms o más (auditoría, token), y midiendo se sabía qué cuentas existen.
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Procesar(usuarioEmail);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Recuperar contraseña: error al procesar la solicitud");
            }

            int resto = TiempoMinimoRespuestaMs - (int)reloj.ElapsedMilliseconds;
            if (resto > 0)
                System.Threading.Thread.Sleep(resto);
            else
                LogSGV.Advertencia("Recuperar contraseña: la solicitud tardó {Ms} ms (más que el tiempo fijo)", reloj.ElapsedMilliseconds);

            MostrarMensaje(SolicitudRecuperacion.MensajeUnico, true);
            txtUsuarioEmail.Text = "";
        }

        private void Procesar(string usuarioEmail)
        {
            string ip = IpClienteHelper.Obtener(Request);
            bool permitido = SolicitudRecuperacion.Permitir(SolicitudRecuperacion.Control, ip, usuarioEmail);
            if (!permitido)
            {
                LogSGV.Advertencia("Recuperar contraseña: solicitud rechazada por límite (IP {Ip})", ip);
                return;
            }

            var usuario = SolicitudRecuperacion.Elegir(BuscarUsuarios(usuarioEmail));
            switch (SolicitudRecuperacion.Decidir(permitido, usuario))
            {
                case SolicitudRecuperacion.Accion.SinCorreo:
                    LogSGV.Info("Recuperar contraseña: el usuario {IdUsuario} no tiene correo registrado", usuario.IdUsuario);
                    AuditoriaHelper.Registrar("RECUPERAR_CONTRASENA", "Usuarios", usuario.IdUsuario,
                        "Solicitud sin correo registrado");
                    break;

                case SolicitudRecuperacion.Accion.EnviarCorreo:
                    var (token, hash) = TokenRecuperacion.Generar();
                    if (!GuardarTokenRecuperacion(usuario.IdUsuario, hash, FechaHelper.Ahora().AddHours(1)))
                        return;
                    AuditoriaHelper.Registrar("RECUPERAR_CONTRASENA", "Usuarios", usuario.IdUsuario,
                        "Enlace de recuperación generado (correo en cola de envío)");
                    EncolarCorreoRecuperacion(usuario, token);
                    break;
            }
        }

        protected void btnVolver_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/Login.aspx");
        }

        /// <summary>Usuarios activos que coinciden por nombre de usuario o por email (la elección la hace <see cref="SolicitudRecuperacion.Elegir"/>).</summary>
        private List<SolicitudRecuperacion.Candidato> BuscarUsuarios(string usuarioEmail)
        {
            var lista = new List<SolicitudRecuperacion.Candidato>();
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT idUsuario, email, nombre,
                             CASE WHEN nombreUsuario = @UsuarioEmail THEN 1 ELSE 0 END AS porNombreUsuario
                      FROM Usuarios
                      WHERE (nombreUsuario = @UsuarioEmail OR email = @UsuarioEmail)
                      AND activo = 1",
                    DbHelper.Param("@UsuarioEmail", usuarioEmail));

                foreach (DataRow r in dt.Rows)
                {
                    lista.Add(new SolicitudRecuperacion.Candidato
                    {
                        IdUsuario = Convert.ToInt32(r["idUsuario"]),
                        Email = r["email"] == DBNull.Value ? "" : r["email"].ToString().Trim(),
                        Nombre = r["nombre"] == DBNull.Value ? "" : r["nombre"].ToString(),
                        PorNombreUsuario = Convert.ToInt32(r["porNombreUsuario"]) == 1
                    });
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Recuperar contraseña: error al buscar el usuario");
                lista.Clear();
            }
            return lista;
        }

        /// <summary>Guarda el SHA-256 del token (nunca el token en claro) y su vencimiento.</summary>
        private bool GuardarTokenRecuperacion(int idUsuario, string hashToken, DateTime expiracion)
        {
            try
            {
                int filas = DbHelper.EjecutarNonQuery(
                    @"UPDATE Usuarios
                      SET resetToken = @Hash,
                          resetTokenExpira = @Expiracion
                      WHERE idUsuario = @IdUsuario",
                    DbHelper.Param("@Hash", hashToken),
                    DbHelper.Param("@Expiracion", expiracion),
                    DbHelper.Param("@IdUsuario", idUsuario));
                return filas > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Recuperar contraseña: error al guardar el token del usuario {IdUsuario}", idUsuario);
                return false;
            }
        }

        /// <summary>
        /// Arma el correo dentro del request (configuración, URL) y lo envía en segundo plano con
        /// <see cref="HostingEnvironment.QueueBackgroundWorkItem(Action{System.Threading.CancellationToken})"/>:
        /// la respuesta no espera al SMTP, así que tarda lo mismo exista o no la cuenta.
        /// </summary>
        private void EncolarCorreoRecuperacion(SolicitudRecuperacion.Candidato usuario, string token)
        {
            int idUsuario = usuario.IdUsuario;
            MailMessage mail;
            string smtpHost, smtpUser, smtpPass;
            int smtpPort;
            bool smtpSSL;
            try
            {
                smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
                smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
                smtpUser = ConfigurationManager.AppSettings["SmtpUser"];
                smtpPass = ConfigurationManager.AppSettings["SmtpPass"];
                smtpSSL  = bool.Parse(ConfigurationManager.AppSettings["SmtpSSL"]);

                // La URL base sale de configuración, no del Host del request: un atacante podría
                // enviar un Host falso y hacer que el enlace con el token apunte a su dominio.
                // Si App.UrlPublica está vacía solo se acepta el host del request en el sitio local;
                // publicado sin ella (p. ej. sin la transformación Release) no se envía el correo.
                string urlBase = (ConfigurationManager.AppSettings["App.UrlPublica"] ?? "").Trim().TrimEnd('/');
                if (string.IsNullOrEmpty(urlBase))
                {
                    if (!Request.IsLocal)
                        throw new InvalidOperationException("App.UrlPublica está vacía en el sitio publicado: no se envía el enlace de recuperación.");
                    urlBase = $"{Request.Url.Scheme}://{Request.Url.Authority}";
                }
                string enlaceRecuperacion = $"{urlBase}/Views/RestablecerContrasena.aspx?token={Uri.EscapeDataString(token)}";

                mail = new MailMessage();
                mail.From = new MailAddress(smtpUser, "SGV - Sistema de Gestión");
                mail.To.Add(usuario.Email);
                mail.Subject = "Recuperación de Contraseña - SGV";
                mail.IsBodyHtml = true;
                mail.Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Recuperación de Contraseña</h2>
                        <p>Hola {System.Web.HttpUtility.HtmlEncode(usuario.Nombre)},</p>
                        <p>Recibimos una solicitud para restablecer tu contraseña. Haz clic en el siguiente enlace:</p>
                        <p><a href='{enlaceRecuperacion}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Restablecer Contraseña</a></p>
                        <p>Este enlace expirará en 1 hora.</p>
                        <p>Si no solicitaste este cambio, puedes ignorar este correo.</p>
                        <br>
                        <p>Saludos,<br>Equipo SGV</p>
                    </body>
                    </html>";
            }
            catch (Exception ex)
            {
                // Sin el mensaje ni la URL: el token no debe llegar al log.
                LogSGV.Error("Recuperar contraseña: no se pudo armar el correo del usuario {IdUsuario} ({Tipo})",
                    idUsuario, ex.GetType().Name);
                return;
            }

            try
            {
                HostingEnvironment.QueueBackgroundWorkItem(ct =>
                {
                    try
                    {
                        using (mail)
                        using (var smtp = new SmtpClient(smtpHost, smtpPort))
                        {
                            smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
                            smtp.EnableSsl = smtpSSL;
                            smtp.Send(mail);
                        }
                        LogSGV.Info("Recuperar contraseña: correo enviado al usuario {IdUsuario}", idUsuario);
                    }
                    catch (Exception ex)
                    {
                        // Los errores SMTP no incluyen el cuerpo del mensaje (donde va el token).
                        LogSGV.Error(ex, "Recuperar contraseña: no se pudo enviar el correo al usuario {IdUsuario}", idUsuario);
                    }
                });
            }
            catch (Exception ex)
            {
                mail.Dispose();
                LogSGV.Error(ex, "Recuperar contraseña: no se pudo encolar el correo del usuario {IdUsuario}", idUsuario);
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito)
        {
            lblMensaje.Text = MensajeHtml.Texto(mensaje);
            pnlMensaje.Visible = true;
            pnlMensaje.CssClass = esExito ? "alert alert-success" : "alert alert-danger";
        }
    }
}
