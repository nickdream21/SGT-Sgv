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
                else if (rol.ToUpper() == "OPERADOR")
                {
                    Response.Redirect("~/Views/DashboardOperador.aspx");
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

            if (!IsPostBack)
            {
                // Pre-llenar usuario desde cookie "Recordarme"
                HttpCookie cookie = Request.Cookies["SGVUserInfo"];
                if (cookie != null && !string.IsNullOrEmpty(cookie.Values["Usuario"]))
                {
                    txtUsername.Text = cookie.Values["Usuario"];
                    chkRemember.Checked = true;
                }

                // Mostrar mensajes según el código de error en la query string
                string err = Request.QueryString["error"];
                if (!string.IsNullOrEmpty(err))
                {
                    switch (err.ToLowerInvariant())
                    {
                        case "sin_conductor":
                            MostrarMensaje("Tu cuenta de usuario no está vinculada a un registro de conductor. " +
                                           "Contacta al administrador del sistema para que configure el vínculo correspondiente.");
                            break;
                        case "sesion":
                            MostrarMensaje("Tu sesión ha expirado o no es válida. Por favor, inicia sesión nuevamente.");
                            break;
                    }
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

                if (resultado.Rol.ToUpper() == "OPERADOR" && resultado.IdOperador.HasValue)
                {
                    Session["IdOperador"] = resultado.IdOperador.Value;
                }

                // ✅ Crear cookie temporal de respaldo para reconstruir sesión si se pierde
                HttpCookie authTemp = new HttpCookie("SGV_AuthTemp");
                authTemp.Values["uid"] = resultado.IdUsuario.ToString();
                authTemp.Values["rol"] = HttpUtility.UrlEncode(resultado.Rol);
                authTemp.Values["nombre"] = HttpUtility.UrlEncode(resultado.Nombre);
                authTemp.Values["nombreUsuario"] = HttpUtility.UrlEncode(resultado.NombreUsuario);
                if (resultado.Rol.ToUpper() == "CONDUCTOR" && resultado.IdConductor.HasValue)
                {
                    authTemp.Values["idConductor"] = resultado.IdConductor.Value.ToString();
                }
                if (resultado.Rol.ToUpper() == "OPERADOR" && resultado.IdOperador.HasValue)
                {
                    authTemp.Values["idOperador"] = resultado.IdOperador.Value.ToString();
                }
                authTemp.Expires = DateTime.Now.AddMinutes(30);
                authTemp.HttpOnly = true;
                authTemp.Secure = Request.IsSecureConnection;  // Solo Secure si es HTTPS
                Response.Cookies.Add(authTemp);

                System.Diagnostics.Debug.WriteLine($"🍪 Cookie SGV_AuthTemp creada - Rol: {resultado.Rol}");

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
                else if (resultado.Rol.ToUpper() == "OPERADOR")
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Login exitoso - OPERADOR: {resultado.Nombre}");
                    Response.Redirect("~/Views/DashboardOperador.aspx");
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
                 int IdUsuario, int? IdConductor, int? IdOperador) ValidarUsuario(string usuario, string contrasena)
        {
            bool esValido = false;
            string rol = "";
            string nombre = "";
            string nombreUsuario = "";
            int idUsuario = 0;
            int? idConductor = null;
            int? idOperador = null;

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
                        u.idOperador,
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
                            nombreUsuario = reader["nombreUsuario"].ToString().Trim();
                            nombre = reader["nombre"].ToString().Trim();
                            rol = reader["rol"].ToString().Trim();

                            if (rol.ToUpper() == "CONDUCTOR" && reader["idConductor"] != DBNull.Value)
                            {
                                idConductor = Convert.ToInt32(reader["idConductor"]);
                                System.Diagnostics.Debug.WriteLine($"🚗 Conductor detectado: {nombre} (ID: {idConductor})");
                            }
                            else if (rol.ToUpper() == "OPERADOR" && reader["idOperador"] != DBNull.Value)
                            {
                                idOperador = Convert.ToInt32(reader["idOperador"]);
                                System.Diagnostics.Debug.WriteLine($"🛠️ Operador detectado: {nombre} (ID: {idOperador})");
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

            return (esValido, rol, nombre, nombreUsuario, idUsuario, idConductor, idOperador);
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
            lblError.Text = mensaje;
            pnlError.Visible = true;
        }

        protected void lnkForgotPassword_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/RecuperarContrasena.aspx");
        }
    }
}