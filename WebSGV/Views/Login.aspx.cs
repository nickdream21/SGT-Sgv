using System;
using System.Data;
using System.Web;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class WebForm1 : PaginaBase
    {
        // ── Regeneración del ID de sesión al iniciar sesión (anti session fixation) ──
        // Con sesión InProc no es fiable cambiar el SessionID dentro del mismo request, así
        // que el login se completa en dos pasos:
        //   1) btnLogin_Click valida credenciales, guarda los datos en caché bajo un token
        //      aleatorio de un solo uso (60 s), abandona la sesión, borra su cookie y deja el
        //      token en una cookie HttpOnly.
        //   2) Login.aspx?completar=1 llega SIN cookie de sesión → ASP.NET crea un ID nuevo;
        //      aquí se consume el token y se carga la sesión.
        private const string CookieTicketLogin = "SGV_LoginTicket";
        private const string PrefijoCacheTicket = "SGV_LoginTicket_";

        [Serializable]
        private class DatosLogin
        {
            public int IdUsuario;
            public string Rol;
            public string Nombre;
            public string NombreUsuario;
            public int? IdConductor;
            public int? IdOperador;
            public bool RequiereCambioContrasena;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.QueryString["completar"] == "1")
            {
                CompletarLogin();
                return;
            }

            // ✅ ROMPE-LOOPS: si llegamos a Login.aspx con ?error=sesion y hay sesión,
            // significa que una página protegida nos rebotó. Limpiar sesión y mostrar el form
            // en lugar de reenviar al usuario al destino que ya falló.
            string errParam = Request.QueryString["error"];
            if (!string.IsNullOrEmpty(errParam) && errParam.ToLowerInvariant() == "sesion")
            {
                if (Session["UsuarioID"] != null)
                {
                    Session.Clear();
                    Session.Abandon();
                    HttpCookie sessionCookie = new HttpCookie("SGV_SessionId")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(sessionCookie);
                }
            }
            // Si el usuario ya ha iniciado sesión, redireccionar según su rol
            else if (Session["UsuarioID"] != null)
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
                else if (rol.ToUpper() == "CONTABILIDAD")
                {
                    Response.Redirect("~/Views/LiquidacionesAprobadasContabilidad.aspx");
                }
                else if (rol.ToUpper() == "ADMIN" ||
                         rol.ToUpper() == "ADMINISTRADOR" ||
                         rol.ToUpper() == "SUPERVISOR" ||
                         rol.ToUpper() == "ADMINISTRADOR DE MAQUINARIA" ||
                         rol.ToUpper() == "ADMINISTRADOR DE TRANSPORTE")
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
            (bool EsValido, string Rol, string Nombre, string NombreUsuario,
             int IdUsuario, int? IdConductor, int? IdOperador, bool RequiereCambioContrasena) resultado;
            try
            {
                resultado = ValidarUsuario(usuario, contrasena);
            }
            catch (Exception)
            {
                // El detalle ya quedó logueado en ValidarUsuario. Una caída de BD NO debe
                // mostrarse como "credenciales incorrectas" ni contar como intento fallido
                // (no toca el contador anti-fuerza-bruta): se avisa que el servicio no está
                // disponible.
                MostrarMensaje("El servicio no está disponible en este momento. Por favor, intente nuevamente en unos minutos.");
                return;
            }

            if (resultado.EsValido)
            {
                // Limpiar contador de intentos fallidos
                Application.Lock();
                Application.Remove(claveFallidos);
                Application.Remove(claveBloqueo);
                Application.UnLock();

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

                // Paso 1 de la regeneración de sesión: ticket de un solo uso + sesión nueva.
                var datos = new DatosLogin
                {
                    IdUsuario = resultado.IdUsuario,
                    Rol = resultado.Rol,
                    Nombre = resultado.Nombre,
                    NombreUsuario = resultado.NombreUsuario,
                    IdConductor = resultado.IdConductor,
                    IdOperador = resultado.IdOperador,
                    RequiereCambioContrasena = resultado.RequiereCambioContrasena
                };
                string token = GenerarTokenAleatorio();
                HttpRuntime.Cache.Insert(PrefijoCacheTicket + token, datos, null,
                    DateTime.UtcNow.AddSeconds(60), System.Web.Caching.Cache.NoSlidingExpiration);

                Session.Clear();
                Session.Abandon();
                Response.Cookies.Add(new HttpCookie("SGV_SessionId", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
                Response.Cookies.Add(new HttpCookie(CookieTicketLogin, token)
                {
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection,
                    Expires = DateTime.Now.AddMinutes(1)
                });

                Response.Redirect("~/Views/Login.aspx?completar=1", false);
                Context.ApplicationInstance.CompleteRequest();
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
                 int IdUsuario, int? IdConductor, int? IdOperador, bool RequiereCambioContrasena) ValidarUsuario(string usuario, string contrasena)
        {
            bool esValido = false;
            string rol = "", nombre = "", nombreUsuario = "";
            int idUsuario = 0;
            int? idConductor = null;
            int? idOperador = null;
            bool requiereCambioContrasena = false;

            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT u.idUsuario, u.nombreUsuario, u.nombre, u.rol,
                           u.idConductor, u.idOperador, u.contrasena,
                           ISNULL(u.requiereCambioContrasena, 0) AS requiereCambioContrasena
                    FROM Usuarios u
                    WHERE u.nombreUsuario = @Usuario AND u.activo = 1",
                    DbHelper.Param("@Usuario", usuario));

                if (dt.Rows.Count > 0)
                {
                    DataRow reader = dt.Rows[0];
                    string storedHash = reader["contrasena"].ToString();
                    idUsuario = Convert.ToInt32(reader["idUsuario"]);

                    if (PasswordHelper.VerifyPassword(contrasena, storedHash))
                    {
                        nombreUsuario = reader["nombreUsuario"].ToString().Trim();
                        nombre        = reader["nombre"].ToString().Trim();
                        rol           = reader["rol"].ToString().Trim();

                        if (rol.ToUpper() == "CONDUCTOR" && reader["idConductor"] != DBNull.Value)
                            idConductor = Convert.ToInt32(reader["idConductor"]);
                        else if (rol.ToUpper() == "OPERADOR" && reader["idOperador"] != DBNull.Value)
                            idOperador = Convert.ToInt32(reader["idOperador"]);

                        esValido = true;
                        requiereCambioContrasena = Convert.ToBoolean(reader["requiereCambioContrasena"]);

                        if (PasswordHelper.NeedsMigration(storedHash))
                        {
                            requiereCambioContrasena = true;
                            MigrarContrasena(idUsuario, contrasena);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // No tragar el error: un fallo de BD/lectura se registra y se propaga para
                // que el llamador lo distinga de "credenciales incorrectas".
                LogSGV.Error(ex, "Error de BD al validar credenciales del usuario {Usuario}", usuario);
                throw;
            }

            return (esValido, rol, nombre, nombreUsuario, idUsuario, idConductor, idOperador, requiereCambioContrasena);
        }

        /// <summary>
        /// Migra automáticamente una contraseña en texto plano al formato hash PBKDF2.
        /// Se ejecuta una sola vez por usuario al hacer login exitoso con contraseña antigua.
        /// </summary>
        private void MigrarContrasena(int idUsuario, string contrasenaPlana)
        {
            try
            {
                string nuevoHash = PasswordHelper.HashPassword(contrasenaPlana);
                DbHelper.EjecutarNonQuery(
                    "UPDATE Usuarios SET contrasena = @NuevoHash, requiereCambioContrasena = 1 WHERE idUsuario = @IdUsuario",
                    DbHelper.Param("@NuevoHash", nuevoHash),
                    DbHelper.Param("@IdUsuario", idUsuario));
            }
            catch (Exception ex)
            {
                // La migración de hash es best-effort: si falla, el login ya fue válido y no
                // se rompe. Se registra para no dejar al usuario con contraseña sin migrar
                // de forma silenciosa e indefinida.
                LogSGV.Advertencia("No se pudo migrar la contraseña del usuario {IdUsuario} a PBKDF2: {Error}",
                    idUsuario, ex.Message);
            }
        }

        /// <summary>
        /// Paso 2 del login: consume el ticket (un solo uso) y carga la sesión, que en este
        /// request ya tiene un SessionID nuevo. Sin ticket válido vuelve al formulario.
        /// </summary>
        private void CompletarLogin()
        {
            string token = Request.Cookies[CookieTicketLogin]?.Value;
            Response.Cookies.Add(new HttpCookie(CookieTicketLogin, "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });

            DatosLogin datos = string.IsNullOrEmpty(token)
                ? null
                : HttpRuntime.Cache.Remove(PrefijoCacheTicket + token) as DatosLogin;

            if (datos == null)
            {
                Response.Redirect("~/Views/Login.aspx?error=sesion", true);
                return;
            }

            Session["UsuarioID"] = datos.IdUsuario.ToString();
            Session["IdUsuario"] = datos.IdUsuario;
            Session["Rol"] = datos.Rol;
            Session["Nombre"] = datos.Nombre;
            Session["NombreUsuario"] = datos.NombreUsuario;
            // Leído por varias páginas (AgregarCPIC, BuscarFactura, EditarDespacho...) para
            // registrar quién sube o edita.
            Session["Usuario"] = datos.NombreUsuario;
            Session["RequiereCambioContrasena"] = datos.RequiereCambioContrasena;

            string rol = (datos.Rol ?? "").ToUpper();
            if (rol == "CONDUCTOR" && datos.IdConductor.HasValue)
                Session["IdConductor"] = datos.IdConductor.Value;
            if (rol == "OPERADOR" && datos.IdOperador.HasValue)
                Session["IdOperador"] = datos.IdOperador.Value;

            AuditoriaHelper.Registrar("LOGIN", "Usuarios", datos.IdUsuario.ToString(),
                $"Inicio de sesión - Usuario: {datos.NombreUsuario}, Rol: {datos.Rol}");

            Response.Redirect(UrlInicioSegunRol(rol), true);
        }

        private static string UrlInicioSegunRol(string rolMayus)
        {
            switch (rolMayus)
            {
                case "CONDUCTOR":                return "~/Views/DashboardConductor.aspx";
                case "OPERADOR":                 return "~/Views/DashboardOperador.aspx";
                case "ADMINISTRADOR DE GRIFO":   return "~/Views/DashboardGrifo.aspx";
                case "ADMINISTRADOR DE SISTEMA": return "~/Views/DashboardAdminSistema.aspx";
                case "CONTABILIDAD":             return "~/Views/LiquidacionesAprobadasContabilidad.aspx";
                default:                         return "~/Views/Inicio.aspx";
            }
        }

        private static string GenerarTokenAleatorio()
        {
            byte[] bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return HttpServerUtility.UrlTokenEncode(bytes);
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
