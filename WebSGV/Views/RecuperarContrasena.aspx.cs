using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RecuperarContrasena : Page
    {
        protected System.Web.UI.WebControls.TextBox txtUsuarioEmail;
        protected System.Web.UI.WebControls.Panel pnlMensaje;
        protected System.Web.UI.WebControls.Label lblMensaje;

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

            // Buscar usuario en la base de datos
            var datosUsuario = BuscarUsuario(usuarioEmail);

            if (datosUsuario.encontrado)
            {
                // Generar token único
                string token = Guid.NewGuid().ToString();
                DateTime expiracion = FechaHelper.Ahora().AddHours(1); // Token válido por 1 hora

                // Guardar token en la base de datos
                if (GuardarTokenRecuperacion(datosUsuario.idUsuario, token, expiracion))
                {
                    // Enviar correo
                    if (EnviarCorreoRecuperacion(datosUsuario.email, datosUsuario.nombre, token))
                    {
                        MostrarMensaje("Se ha enviado un enlace de recuperación a su correo electrónico.", true);
                        txtUsuarioEmail.Text = "";
                    }
                    else
                    {
                        MostrarMensaje("Error al enviar el correo. Verifique la configuración SMTP.", false);
                    }
                }
            }
            else
            {
                // Por seguridad, mostrar mensaje genérico
                MostrarMensaje("Si el usuario existe, recibirá un correo con las instrucciones.", true);
            }
        }

        protected void btnVolver_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/Login.aspx");
        }

        private (bool encontrado, int idUsuario, string email, string nombre) BuscarUsuario(string usuarioEmail)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT idUsuario, email, nombre 
                               FROM Usuarios 
                               WHERE (nombreUsuario = @UsuarioEmail OR email = @UsuarioEmail) 
                               AND activo = 1";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@UsuarioEmail", usuarioEmail);

                try
                {
                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        return (
                            true,
                            Convert.ToInt32(reader["idUsuario"]),
                            reader["email"].ToString(),
                            reader["nombre"].ToString()             
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al buscar usuario: {ex.Message}");
                }
            }

            return (false, 0, "", "");
        }

        private bool GuardarTokenRecuperacion(int idUsuario, string token, DateTime expiracion)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"UPDATE Usuarios 
                               SET resetToken = @Token, 
                                   resetTokenExpira = @Expiracion 
                               WHERE idUsuario = @IdUsuario";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", token);
                cmd.Parameters.AddWithValue("@Expiracion", expiracion);
                cmd.Parameters.AddWithValue("@IdUsuario", idUsuario);

                try
                {
                    conn.Open();
                    return cmd.ExecuteNonQuery() > 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al guardar token: {ex.Message}");
                    return false;
                }
            }
        }

        private bool EnviarCorreoRecuperacion(string emailDestino, string nombreUsuario, string token)
        {
            try
            {
                // Configurar desde Web.config
                string smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
                int smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
                string smtpUser = ConfigurationManager.AppSettings["SmtpUser"];
                string smtpPass = ConfigurationManager.AppSettings["SmtpPass"];
                bool smtpSSL = bool.Parse(ConfigurationManager.AppSettings["SmtpSSL"]);

                string enlaceRecuperacion = $"{Request.Url.Scheme}://{Request.Url.Authority}/Views/RestablecerContrasena.aspx?token={token}";

                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(smtpUser, "SGV - Sistema de Gestión");
                mail.To.Add(emailDestino);
                mail.Subject = "Recuperación de Contraseña - SGV";
                mail.IsBodyHtml = true;
                mail.Body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif;'>
                        <h2>Recuperación de Contraseña</h2>
                        <p>Hola {nombreUsuario},</p>
                        <p>Recibimos una solicitud para restablecer tu contraseña. Haz clic en el siguiente enlace:</p>
                        <p><a href='{enlaceRecuperacion}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; display: inline-block;'>Restablecer Contraseña</a></p>
                        <p>Este enlace expirará en 1 hora.</p>
                        <p>Si no solicitaste este cambio, puedes ignorar este correo.</p>
                        <br>
                        <p>Saludos,<br>Equipo SGV</p>
                    </body>
                    </html>";

                SmtpClient smtp = new SmtpClient(smtpHost, smtpPort);
                smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
                smtp.EnableSsl = smtpSSL;

                smtp.Send(mail);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al enviar correo: {ex.Message}");
                return false;
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito)
        {
            lblMensaje.Text = mensaje;
            pnlMensaje.Visible = true;
            pnlMensaje.CssClass = esExito ? "alert alert-success" : "alert alert-danger";
        }
    }
}