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
                else if (rol.ToUpper() == "ADMINISTRADOR DE SISTEMA")
                {
                    Response.Redirect("~/Views/DashboardAdminSistema.aspx");
                }
                else if (rol.ToUpper() == "ADMIN" ||
                         rol.ToUpper() == "ADMINISTRADOR" ||
                         rol.ToUpper() == "SUPERVISOR" ||
                         rol.ToUpper() == "ADMINISTRADOR DE MAQUINARIA")
                {
                    Response.Redirect("~/Views/Inicio.aspx");
                }
                else
                {
                    // Rol desconocido: cerrar sesión para evitar loops Login <-> páginas protegidas
                    Session.Clear();
                    Session.Abandon();
                    Response.Redirect("~/Views/Login.aspx?error=sesion");
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
            // Agregar cabeceras de seguridad en la respuesta del login
            Response.Headers.Add("X-Frame-Options", "SAMEORIGIN");
            Response.Headers.Add("X-Content-Type-Options", "nosniff");
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            string usuario = txtUsername.Text.Trim();
            string contrasena = txtPassword.Text.Trim();

            // Validar longitud máxima para prevenir ataques de buffer
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                MostrarMensaje("Por favor, ingrese usuario y contraseña.");
                return;
            }

            if (usuario.Length > 100 || contrasena.Length > 200)
            {
                MostrarMensaje("Usuario o contraseña incorrectos. Por favor, intente nuevamente.");
                return;
            }

            // Protección básica anti-fuerza-bruta por IP (Application state)
            // Application.Lock() evita race conditions en entornos multi-hilo.
            string ip = Request.UserHostAddress ?? "unknown";
            string claveFallidos = "LoginFail_" + ip;
            string claveBloqueo  = "LoginBlock_" + ip;

            Application.Lock();
            try
            {
                if (Application[claveBloqueo] is DateTime bloqueadoHasta && bloqueadoHasta > DateTime.UtcNow)
                {
                    int segundos = (int)(bloqueadoHasta - DateTime.UtcNow).TotalSeconds;
                    Application.UnLock();
                    MostrarMensaje($"Demasiados intentos fallidos. Intente nuevamente en {segundos} segundos.");
                    return;
                }
            }
            finally
            {
                Application.UnLock();
            }

            // Verificar credenciales en la base de datos (fuera del lock para no retenerlo durante I/O)
            var resultado = ValidarUsuario(usuario, contrasena);

            if (resultado.EsValido)
            {
                // Limpiar contador de intentos fallidos
                Application.Lock();
                Application.Remove(claveFallidos);
                Application.Remove(claveBloqueo);
                Application.UnLock();

                // Limpiar sesión anterior
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

                // La sesión de servidor (Session[]) almacena todos los datos de autenticación.
                // No se emite ninguna cookie adicional con datos sensibles (uid, rol, nombre).

                // Si la opción "Recordarme" está marcada, guardar solo el usuario (no el rol ni ID)
                if (chkRemember.Checked)
                {
                    HttpCookie cookie = new HttpCookie("SGVUserInfo");
                    cookie.Values.Add("Usuario", usuario);
                    cookie.Expires = DateTime.Now.AddDays(15);
                    cookie.HttpOnly = true;
                    cookie.Secure = Request.IsSecureConnection;
                    Response.Cookies.Add(cookie);
                }

                // Redirigir según el rol
                if (resultado.Rol.ToUpper() == "CONDUCTOR")
                {
                    Response.Redirect("~/Views/DashboardConductor.aspx");
                }
                else if (resultado.Rol.ToUpper() == "OPERADOR")
                {
                    Response.Redirect("~/Views/DashboardOperador.aspx");
                }
                else if (resultado.Rol.ToUpper() == "ADMINISTRADOR DE GRIFO")
                {
                    Response.Redirect("~/Views/DashboardGrifo.aspx");
                }
                else if (resultado.Rol.ToUpper() == "ADMINISTRADOR DE SISTEMA")
                {
                    Response.Redirect("~/Views/DashboardAdminSistema.aspx");
                }
                else
                {
                    Response.Redirect("~/Views/Inicio.aspx");
                }
            }
            else
            {
                // Incrementar contador de intentos fallidos con lock para evitar race condition
                Application.Lock();
                int intentos = (Application[claveFallidos] as int?) ?? 0;
                intentos++;
                Application[claveFallidos] = intentos;

                // Bloquear IP por 5 minutos tras 5 intentos fallidos
                if (intentos >= 5)
                {
                    Application[claveBloqueo] = DateTime.UtcNow.AddMinutes(5);
                    Application.Remove(claveFallidos);
                }
                Application.UnLock();

                if (intentos >= 5)
                {
                    MostrarMensaje("Demasiados intentos fallidos. Su acceso ha sido bloqueado temporalmente por 5 minutos.");
                }
                else
                {
                    MostrarMensaje("Usuario o contraseña incorrectos. Por favor, intente nuevamente.");
                }
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
                            }
                            else if (rol.ToUpper() == "OPERADOR" && reader["idOperador"] != DBNull.Value)
                            {
                                idOperador = Convert.ToInt32(reader["idOperador"]);
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
                catch
                {
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
            }
            catch
            {
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