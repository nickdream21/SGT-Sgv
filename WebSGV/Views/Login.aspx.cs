using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class WebForm1 : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Si el usuario ya ha iniciado sesión, redireccionar según su rol
            if (Session["UsuarioID"] != null)
            {
                string rol = Session["Rol"]?.ToString() ?? "";

                if (rol.ToUpper() == "CONDUCTOR")
                {
                    Response.Redirect("~/Views/DashboardConductor.aspx");
                }
                else
                {
                    Response.Redirect("~/Views/Inicio.aspx");
                }
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            string usuario = txtUsername.Text.Trim();
            string contrasena = txtPassword.Text.Trim();

            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                MostrarMensaje("Por favor, ingrese usuario y contraseña.");
                return;
            }

            // Verificar credenciales en la base de datos
            var resultado = ValidarUsuario(usuario, contrasena);

            if (resultado.EsValido)
            {
                // Si la opción "Recordarme" está marcada, guardar una cookie
                if (chkRemember.Checked)
                {
                    HttpCookie cookie = new HttpCookie("SGVUserInfo");
                    cookie.Values.Add("Usuario", usuario);
                    cookie.Expires = DateTime.Now.AddDays(15);
                    Response.Cookies.Add(cookie);
                }

                // Redirigir según el rol
                if (resultado.Rol.ToUpper() == "CONDUCTOR")
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso - CONDUCTOR: {resultado.Nombre}");
                    Response.Redirect("~/Views/DashboardConductor.aspx");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso - ADMIN: {resultado.Nombre}");
                    Response.Redirect("~/Views/Inicio.aspx");
                }
            }
            else
            {
                MostrarMensaje("Usuario o contraseña incorrectos. Por favor, intente nuevamente.");
            }
        }

        private (bool EsValido, string Rol, string Nombre) ValidarUsuario(string usuario, string contrasena)
        {
            bool esValido = false;
            string rol = "";
            string nombre = "";

            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Query que incluye idConductor para conductores
                string query = @"
                    SELECT 
                        u.idUsuario, 
                        u.nombreUsuario, 
                        u.nombre, 
                        u.rol,
                        u.idConductor
                    FROM Usuarios u
                    WHERE u.nombreUsuario = @Usuario 
                        AND u.contrasena = @Contrasena 
                        AND u.activo = 1";

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
                        Session["IdUsuario"] = Convert.ToInt32(reader["idUsuario"]); // Como int también
                        Session["NombreUsuario"] = reader["nombreUsuario"].ToString();
                        Session["Nombre"] = reader["nombre"].ToString();
                        Session["Rol"] = reader["rol"].ToString();

                        rol = reader["rol"].ToString();
                        nombre = reader["nombre"].ToString();

                        // Si es conductor, guardar también el idConductor
                        if (rol.ToUpper() == "CONDUCTOR" && reader["idConductor"] != DBNull.Value)
                        {
                            int idConductor = Convert.ToInt32(reader["idConductor"]);
                            Session["IdConductor"] = idConductor;

                            System.Diagnostics.Debug.WriteLine($"🚗 Conductor detectado: {nombre} (ID: {idConductor})");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"👨‍💼 Admin detectado: {nombre}");
                        }

                        esValido = true;
                    }

                    reader.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error al validar usuario: {ex.Message}");
                }
            }

            return (esValido, rol, nombre);
        }

        private void MostrarMensaje(string mensaje)
        {
            ClientScript.RegisterStartupScript(
                this.GetType(),
                "alert",
                $"alert('{mensaje}');",
                true
            );
        }
    }
}