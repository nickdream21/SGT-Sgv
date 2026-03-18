using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RestablecerContrasena : Page
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

                // Validar que el token exista y no haya expirado
                if (!ValidarToken(token))
                {
                    MostrarError("El enlace ha expirado o no es válido. Solicite uno nuevo desde la página de recuperación.");
                    return;
                }

                // Guardar token para el PostBack
                TokenActual = token;
            }
        }

        protected void btnRestablecer_Click(object sender, EventArgs e)
        {
            string nuevaContrasena = txtNuevaContrasena.Text.Trim();
            string confirmarContrasena = txtConfirmarContrasena.Text.Trim();

            // Validaciones
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

            // Actualizar la contraseña en la base de datos (ahora con hash)
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
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"SELECT COUNT(*) FROM Usuarios 
                                WHERE resetToken = @Token 
                                AND resetTokenExpira > @FechaActual 
                                AND activo = 1";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@Token", token);
                cmd.Parameters.AddWithValue("@FechaActual", FechaHelper.Ahora());

                try
                {
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al validar token: {ex.Message}");
                    return false;
                }
            }
        }

        private bool ActualizarContrasena(string token, string nuevaContrasena)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                // Generar hash seguro de la nueva contraseña
                string hashedPassword = PasswordHelper.HashPassword(nuevaContrasena);

                // Actualizar contraseña con hash y limpiar el token (uso único)
                string query = @"UPDATE Usuarios 
                                SET contrasena = @NuevaContrasena, 
                                    resetToken = NULL, 
                                    resetTokenExpira = NULL 
                                WHERE resetToken = @Token 
                                AND resetTokenExpira > @FechaActual 
                                AND activo = 1";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@NuevaContrasena", hashedPassword);
                cmd.Parameters.AddWithValue("@Token", token);
                cmd.Parameters.AddWithValue("@FechaActual", FechaHelper.Ahora());

                try
                {
                    conn.Open();
                    int filasAfectadas = cmd.ExecuteNonQuery();
                    return filasAfectadas > 0;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error al actualizar contraseña: {ex.Message}");
                    return false;
                }
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

        // Asegúrese de que las siguientes declaraciones estén presentes en la clase RestablecerContrasena
        // Esto asume que los controles txtConfirmarContrasena y pnlMensaje existen en el archivo .aspx
        protected global::System.Web.UI.WebControls.TextBox txtConfirmarContrasena;
        protected global::System.Web.UI.WebControls.Panel pnlMensaje;
        protected global::System.Web.UI.WebControls.Label lblMensaje;
        protected global::System.Web.UI.WebControls.Panel pnlFormulario;
        protected global::System.Web.UI.WebControls.TextBox txtNuevaContrasena;
    }
}