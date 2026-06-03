using System;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RestablecerContrasena : PaginaBase
    {
        private string TokenActual
        {
            get { return ViewState["TokenActual"]?.ToString() ?? ""; }
            set { ViewState["TokenActual"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string token = Request.QueryString["token"];

                if (string.IsNullOrEmpty(token))
                {
                    MostrarError("Enlace inválido. No se proporcionó un token de recuperación.");
                    return;
                }

                if (!ValidarToken(token))
                {
                    MostrarError("El enlace ha expirado o no es válido. Solicite uno nuevo desde la página de recuperación.");
                    return;
                }

                TokenActual = token;
            }
        }

        protected void btnRestablecer_Click(object sender, EventArgs e)
        {
            string nuevaContrasena = txtNuevaContrasena.Text.Trim();
            string confirmarContrasena = txtConfirmarContrasena.Text.Trim();

            if (string.IsNullOrEmpty(nuevaContrasena) || string.IsNullOrEmpty(confirmarContrasena))
            {
                MostrarMensaje("Por favor, complete ambos campos.", false);
                return;
            }

            if (nuevaContrasena.Length < 6)
            {
                MostrarMensaje("La contraseña debe tener al menos 6 caracteres.", false);
                return;
            }

            if (nuevaContrasena != confirmarContrasena)
            {
                MostrarMensaje("Las contraseñas no coinciden.", false);
                return;
            }

            if (string.IsNullOrEmpty(TokenActual))
            {
                MostrarError("Sesión expirada. Solicite un nuevo enlace de recuperación.");
                return;
            }

            if (ActualizarContrasena(TokenActual, nuevaContrasena))
            {
                MostrarMensaje("¡Contraseña restablecida exitosamente! Ahora puede iniciar sesión.", true);
                pnlFormulario.Visible = false;
            }
            else
            {
                MostrarError("Error al restablecer la contraseña. El enlace puede haber expirado.");
            }
        }

        protected void btnVolverLogin_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/Login.aspx");
        }

        private bool ValidarToken(string token)
        {
            try
            {
                int count = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    @"SELECT COUNT(*) FROM Usuarios
                      WHERE resetToken = @Token
                      AND resetTokenExpira > @FechaActual
                      AND activo = 1",
                    DbHelper.Param("@Token", token),
                    DbHelper.Param("@FechaActual", FechaHelper.Ahora())));
                return count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al validar token: {ex.Message}");
                return false;
            }
        }

        private bool ActualizarContrasena(string token, string nuevaContrasena)
        {
            try
            {
                string hashedPassword = PasswordHelper.HashPassword(nuevaContrasena);

                int filasAfectadas = DbHelper.EjecutarNonQuery(
                    @"UPDATE Usuarios
                      SET contrasena = @NuevaContrasena,
                          resetToken = NULL,
                          resetTokenExpira = NULL
                      WHERE resetToken = @Token
                      AND resetTokenExpira > @FechaActual
                      AND activo = 1",
                    DbHelper.Param("@NuevaContrasena", hashedPassword),
                    DbHelper.Param("@Token", token),
                    DbHelper.Param("@FechaActual", FechaHelper.Ahora()));

                return filasAfectadas > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al actualizar contraseña: {ex.Message}");
                return false;
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito)
        {
            lblMensaje.Text = mensaje;
            pnlMensaje.Visible = true;
            pnlMensaje.CssClass = esExito ? "alert alert-success" : "alert alert-danger";
        }

        private void MostrarError(string mensaje)
        {
            MostrarMensaje(mensaje, false);
            pnlFormulario.Visible = false;
        }

        protected global::System.Web.UI.WebControls.TextBox txtConfirmarContrasena;
        protected global::System.Web.UI.WebControls.Panel pnlMensaje;
        protected global::System.Web.UI.WebControls.Label lblMensaje;
        protected global::System.Web.UI.WebControls.Panel pnlFormulario;
        protected global::System.Web.UI.WebControls.TextBox txtNuevaContrasena;
    }
}
