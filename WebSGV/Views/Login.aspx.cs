using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;
using WebSGV.Helpers;

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
                else if (rol.ToUpper() == "ADMINISTRADOR DE GRIFO")
                {
                    Response.Redirect("~/Views/DashboardGrifo.aspx");
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
                // ✅ Limpiar sesión anterior sin abandonarla (mantiene el SessionID
                // para que ViewStateUserKey no entre en conflicto)
                Session.Clear();

                // Guardar datos directamente en la sesión actual
                Session["UsuarioID"] = resultado.IdUsuario.ToString();
                Session["IdUsuario"] = resultado.IdUsuario;
                Session["Rol"] = resultado.Rol;
                Session["Nombre"] = resultado.Nombre;
                Session["NombreUsuario"] = resultado.NombreUsuario;

                if (resultado.Rol.ToUpper() == "CONDUCTOR" && resultado.IdConductor.HasValue)
                {
                    Session["IdConductor"] = resultado.IdConductor.Value;
                }

                // Si la opción "Recordarme" está marcada, guardar una cookie
                if (chkRemember.Checked)
                {
                    HttpCookie cookie = new HttpCookie("SGVUserInfo");
                    cookie.Values.Add("Usuario", usuario);
                    cookie.Expires = DateTime.Now.AddDays(15);
                    cookie.HttpOnly = true;
                    Response.Cookies.Add(cookie);
                }

                // Redirigir según el rol
                if (resultado.Rol.ToUpper() == "CONDUCTOR")
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso - CONDUCTOR: {resultado.Nombre}");
                    Response.Redirect("~/Views/DashboardConductor.aspx");
                }
                else if (resultado.Rol.ToUpper() == "ADMINISTRADOR DE GRIFO")
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso - ADMIN GRIFO: {resultado.Nombre}");
                    Response.Redirect("~/Views/DashboardGrifo.aspx");
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

        private (bool EsValido, string Rol, string Nombre, string NombreUsuario, 
                 int IdUsuario, int? IdConductor) ValidarUsuario(string usuario, string contrasena)
        {
            bool esValido = false;
            string rol = "";
            string nombre = "";
            string nombreUsuario = "";
            int idUsuario = 0;
            int? idConductor = null;

            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // 1. Obtener el hash almacenado (ya NO comparamos en el WHERE)
                string query = @"
                    SELECT 
                        u.idUsuario, 
                        u.nombreUsuario, 
                        u.nombre, 
                        u.rol,
                        u.idConductor,
                        u.contrasena
                    FROM Usuarios u
                    WHERE u.nombreUsuario = @Usuario 
                        AND u.activo = 1";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Usuario", usuario);

                try
                {
                    connection.Open();
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.Read())
                    {
                        string storedHash = reader["contrasena"].ToString();
                        idUsuario = Convert.ToInt32(reader["idUsuario"]);

                        // 2. Verificar contraseña con PasswordHelper
                        if (PasswordHelper.VerifyPassword(contrasena, storedHash))
                        {
                            nombreUsuario = reader["nombreUsuario"].ToString();
                            nombre = reader["nombre"].ToString();
                            rol = reader["rol"].ToString();

                            if (rol.ToUpper() == "CONDUCTOR" && reader["idConductor"] != DBNull.Value)
                            {
                                idConductor = Convert.ToInt32(reader["idConductor"]);
                                System.Diagnostics.Debug.WriteLine($"🚗 Conductor detectado: {nombre} (ID: {idConductor})");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"👨‍💼 Admin detectado: {nombre}");
                            }

                            esValido = true;

                            // Cerrar reader antes de ejecutar UPDATE
                            reader.Close();

                            // 3. Migrar contraseña antigua a hash si es necesario
                            if (PasswordHelper.NeedsMigration(storedHash))
                            {
                                MigrarContrasena(connection, idUsuario, contrasena);
                            }
                        }
                    }

                    if (!reader.IsClosed)
                        reader.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error al validar usuario: {ex.Message}");
                }
            }

            return (esValido, rol, nombre, nombreUsuario, idUsuario, idConductor);
        }

        /// <summary>
        /// Migra automáticamente una contraseña en texto plano al formato hash PBKDF2.
        /// Se ejecuta una sola vez por usuario al hacer login exitoso con contraseña antigua.
        /// </summary>
        private void MigrarContrasena(SqlConnection connection, int idUsuario, string contrasenaPlana)
        {
            try
            {
                string nuevoHash = PasswordHelper.HashPassword(contrasenaPlana);

                string queryUpdate = "UPDATE Usuarios SET contrasena = @NuevoHash WHERE idUsuario = @IdUsuario";

                using (SqlCommand cmdUpdate = new SqlCommand(queryUpdate, connection))
                {
                    cmdUpdate.Parameters.AddWithValue("@NuevoHash", nuevoHash);
                    cmdUpdate.Parameters.AddWithValue("@IdUsuario", idUsuario);
                    cmdUpdate.ExecuteNonQuery();
                }

                System.Diagnostics.Debug.WriteLine($"🔒 Contraseña migrada a hash para usuario ID: {idUsuario}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Error migrando contraseña: {ex.Message}");
            }
        }

        private void MostrarMensaje(string mensaje)
        {
            ClientScript.RegisterStartupScript(
                this.GetType(),
                "alert",
                $"alert('{HttpUtility.JavaScriptStringEncode(mensaje)}');",
                true
            );
        }

        protected void lnkForgotPassword_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/RecuperarContrasena.aspx");
        }
    }
}