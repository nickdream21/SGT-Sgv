using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class WebForm1 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Si el usuario ya ha iniciado sesión, redireccionar al inicio
            if (Session["UsuarioID"] != null)
            {
                Response.Redirect("~/Views/Inicio.aspx");

            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string usuario = txtUsername.Text.Trim();
            string contrasena = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                // Mostrar mensaje de error si los campos están vacíos
                ClientScript.RegisterStartupScript(this.GetType(), "alert",
                    "alert('Por favor, ingrese usuario y contraseña.');", true);
                return;
            }

            // Verificar credenciales en la base de datos
            if (ValidarUsuario(usuario, contrasena))
            {
                // Si la opción "Recordarme" está marcada, guardar una cookie
                if (chkRemember.Checked)
                {
                    HttpCookie cookie = new HttpCookie("SGVUserInfo");
                    cookie.Values.Add("Usuario", usuario);
                    cookie.Expires = DateTime.Now.AddDays(15); // La cookie expira en 15 días
                    Response.Cookies.Add(cookie);
                }

                // ✅ CORRECCIÓN: Redirigir a la página de inicio con la ruta correcta
                Response.Redirect("~/Views/Inicio.aspx");
            }
            else
            {
                // Mostrar mensaje de error para credenciales incorrectas
                ClientScript.RegisterStartupScript(this.GetType(), "alert",
                    "alert('Usuario o contraseña incorrectos. Por favor, intente nuevamente.');", true);
            }
        }

        private bool ValidarUsuario(string usuario, string contrasena)
        {
            bool esValido = false;
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT idUsuario, nombreUsuario, nombre, rol FROM Usuarios WHERE nombreUsuario = @Usuario AND contrasena = @Contrasena AND activo = 1";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Usuario", usuario);
                command.Parameters.AddWithValue("@Contrasena", contrasena);

                try
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        // Guardar información del usuario en la sesión
                        Session["UsuarioID"] = reader["idUsuario"].ToString();
                        Session["NombreUsuario"] = reader["nombreUsuario"].ToString();
                        Session["Nombre"] = reader["nombre"].ToString();
                        Session["Rol"] = reader["rol"].ToString();
                        esValido = true;
                    }

                    reader.Close();
                }
                catch (Exception ex)
                {
                    // Registrar el error en algún log
                    System.Diagnostics.Debug.WriteLine("Error al validar usuario: " + ex.Message);
                }
            }

            return esValido;
        }
    }
}