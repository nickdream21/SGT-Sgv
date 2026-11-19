using System;
using System.Data;
using System.Web;
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
            public bool RequiereCambioContrasena;
            /// <summary>La contraseña con la que entró no cumple PoliticaContrasena (solo con Seguridad.ExigirPoliticaAlIngresar).</summary>
            public bool ContrasenaFueraDePolitica;
            public Guid SelloSesion;
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
            string errNormalizado = (errParam ?? "").ToLowerInvariant();
            if (errNormalizado == "sesion" || errNormalizado == "sesion_cerrada")
            {
                if (SesionHelper.HaySesion(Session))
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
            else if (SesionHelper.HaySesion(Session))
            {
                string urlInicio = RolesHelper.UrlInicioSegunRol(SesionHelper.Rol(Session));

                if (urlInicio != null)
                {
                    Response.Redirect(urlInicio);
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
                        case "sesion_cerrada":
                            MostrarMensaje(SesionVigenteHelper.MensajeSesionCerrada);
                            break;
                    }
                }
            }
        }

        protected void btnLogin_Click(object sender, EventArgs e)
        {
            // Las cabeceras de seguridad vienen de Web.config; aquí solo se evita la caché.
            Response.Cache.SetCacheability(HttpCacheability.NoCache);
            Response.Cache.SetNoStore();

            string usuario = txtUsername.Text.Trim();
            // Sin Trim: se verifica tal cual y, si no coincide, recortada (PasswordHelper.VerificarIngresada).
            string contrasena = txtPassword.Text;

            // Validar longitud máxima para prevenir ataques de buffer
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrWhiteSpace(contrasena))
            {
                MostrarMensaje("Por favor, ingrese usuario y contraseña.");
                return;
            }

            if (usuario.Length > 100 || contrasena.Length > 200)
            {
                MostrarMensaje("Usuario o contraseña incorrectos. Por favor, intente nuevamente.");
                return;
            }

            // Anti-fuerza-bruta por IP y por cuenta (ControlIntentosLogin): el intento se reserva
            // como fallo ANTES de ir a la BD, para que peticiones en paralelo no se salten el bloqueo.
            var control = ControlIntentosLogin.Sitio;
            var reserva = control.Reservar(ControlIntentosLogin.ClaveIp(IpClienteHelper.Obtener(Request)),
                                           ControlIntentosLogin.ClaveCuenta(usuario));
            if (reserva.Rechazado)
            {
                int minutos = Math.Max(1, (int)Math.Ceiling(reserva.SegundosRestantes / 60.0));
                MostrarMensaje($"Demasiados intentos fallidos. Intente nuevamente en {minutos} minuto(s).");
                return;
            }

            (bool EsValido, string Rol, string Nombre, string NombreUsuario,
             int IdUsuario, int? IdConductor, bool RequiereCambioContrasena, Guid SelloSesion) resultado;
            try
            {
                resultado = ValidarUsuario(usuario, contrasena);
            }
            catch (Exception)
            {
                // El detalle ya quedó logueado en ValidarUsuario. Una caída de BD NO debe
                // mostrarse como "credenciales incorrectas" ni contar como intento fallido
                // (se devuelve el intento reservado): se avisa que el servicio no está disponible.
                control.ErrorAlValidar(reserva);
                MostrarMensaje("El servicio no está disponible en este momento. Por favor, intente nuevamente en unos minutos.");
                return;
            }

            if (resultado.EsValido)
            {
                // Login correcto: se limpia el contador de la cuenta y se devuelve a la IP solo el
                // intento reservado (una cuenta válida no sirve para "resetear" los de esa IP).
                control.LoginCorrecto(reserva);

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
                else if (Request.Cookies["SGVUserInfo"] != null)
                {
                    // Desmarcó "Recordarme": dejar de recordar el usuario en este equipo.
                    Response.Cookies.Add(new HttpCookie("SGVUserInfo", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
                }

                // Paso 1 de la regeneración de sesión: ticket de un solo uso + sesión nueva.
                var datos = new DatosLogin
                {
                    IdUsuario = resultado.IdUsuario,
                    Rol = resultado.Rol,
                    Nombre = resultado.Nombre,
                    NombreUsuario = resultado.NombreUsuario,
                    IdConductor = resultado.IdConductor,
                    RequiereCambioContrasena = resultado.RequiereCambioContrasena,
                    ContrasenaFueraDePolitica = ContrasenaFueraDePolitica(resultado.IdUsuario, contrasena),
                    SelloSesion = resultado.SelloSesion
                };
                string token = GenerarTokenAleatorio();
                HttpRuntime.Cache.Insert(PrefijoCacheTicket + token, datos, null,
                    DateTime.UtcNow.AddSeconds(60), System.Web.Caching.Cache.NoSlidingExpiration);

                Session.Clear();
                Session.Abandon();
                Response.Cookies.Add(new HttpCookie("SGV_SessionId", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
                // Cookie de sesión (sin Expires): el vencimiento de 60 s lo controla la caché del
                // servidor. Con Expires, un celular con la hora adelantada la descartaba al llegar
                // y el login terminaba en "Tu sesión ha expirado".
                Response.Cookies.Add(new HttpCookie(CookieTicketLogin, token)
                {
                    HttpOnly = true,
                    Secure = Request.IsSecureConnection
                });

                Response.Redirect("~/Views/Login.aspx?completar=1", false);
                Context.ApplicationInstance.CompleteRequest();
            }
            else
            {
                // El fallo ya quedó contado en la reserva.
                if (reserva.BloqueoIp || reserva.BloqueoCuenta)
                    MostrarMensaje("Demasiados intentos fallidos. El acceso quedó bloqueado temporalmente; intente más tarde.");
                else
                    MostrarMensaje("Usuario o contraseña incorrectos. Por favor, intente nuevamente.");
            }
        }

        /// <summary>
        /// Con <c>Seguridad.ExigirPoliticaAlIngresar</c> en true: si la contraseña recién escrita (la
        /// que coincidió, recortada como se guardan las nuevas) no cumple <see cref="PoliticaContrasena"/>,
        /// la sesión queda con el cambio de contraseña obligatorio. No se guarda nada en la BD. Si falla
        /// la consulta de los datos de la persona, el login sigue sin exigirlo.
        /// </summary>
        private static bool ContrasenaFueraDePolitica(int idUsuario, string contrasena)
        {
            try
            {
                if (!PoliticaContrasena.ExigeCambioAlIngresar(PoliticaContrasena.ExigirAlIngresar,
                        () => ContrasenaNuevaHelper.ValidarParaUsuario((contrasena ?? "").Trim(), idUsuario)))
                    return false;
                LogSGV.Info("El usuario {IdUsuario} entró con una contraseña fuera de la política: debe cambiarla", idUsuario);
                return true;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo revisar la política de contraseña del usuario {IdUsuario} al iniciar sesión", idUsuario);
                return false;
            }
        }

        /// <summary>Hash de una contraseña que nadie tiene: si el usuario no existe se verifica igual,
        /// para que el tiempo de respuesta no revele qué usuarios existen.</summary>
        private static readonly string HashFicticio = PasswordHelper.HashPassword(Guid.NewGuid().ToString("N"));

        private (bool EsValido, string Rol, string Nombre, string NombreUsuario,
                 int IdUsuario, int? IdConductor, bool RequiereCambioContrasena, Guid SelloSesion) ValidarUsuario(string usuario, string contrasena)
        {
            bool esValido = false;
            string rol = "", nombre = "", nombreUsuario = "";
            int idUsuario = 0;
            int? idConductor = null;
            bool requiereCambioContrasena = false;
            Guid selloSesion = Guid.Empty;

            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT u.idUsuario, u.nombreUsuario, u.nombre, u.rol,
                           u.idConductor, u.contrasena,
                           ISNULL(u.requiereCambioContrasena, 0) AS requiereCambioContrasena,
                           u.selloSesion
                    FROM Usuarios u
                    WHERE u.nombreUsuario = @Usuario AND u.activo = 1",
                    DbHelper.Param("@Usuario", usuario));

                if (dt.Rows.Count > 0)
                {
                    DataRow reader = dt.Rows[0];
                    string storedHash = reader["contrasena"].ToString();
                    idUsuario = Convert.ToInt32(reader["idUsuario"]);

                    string coincidente;
                    bool coincide = PasswordHelper.VerificarIngresada(contrasena, storedHash, out coincidente);

                    // Hash del formato anterior (PBKDF2-SHA1) → PBKDF2-SHA256. Si falla, el login sigue.
                    bool convertido = false;
                    if (coincide)
                    {
                        try
                        {
                            convertido = PasswordHelper.ActualizarHashSiCorresponde(idUsuario, storedHash, coincidente);
                        }
                        catch (Exception exConv)
                        {
                            LogSGV.Error(exConv, "No se pudo convertir el hash de contraseña del usuario {IdUsuario}", idUsuario);
                        }
                    }

                    // Un hash del formato anterior se verifica mucho más rápido que HashFicticio: sin
                    // esto, el tiempo de respuesta delataría qué usuarios existen. La conversión ya pagó
                    // una derivación con las iteraciones objetivo.
                    if (!convertido)
                        PasswordHelper.IgualarCostoVerificacion(contrasena, storedHash, HashFicticio);

                    if (coincide)
                    {
                        nombreUsuario = reader["nombreUsuario"].ToString().Trim();
                        nombre        = reader["nombre"].ToString().Trim();
                        rol           = reader["rol"].ToString().Trim();

                        if (RolesHelper.EsRolConductor(rol) && reader["idConductor"] != DBNull.Value)
                            idConductor = Convert.ToInt32(reader["idConductor"]);

                        esValido = true;
                        requiereCambioContrasena = Convert.ToBoolean(reader["requiereCambioContrasena"]);
                        selloSesion = (Guid)reader["selloSesion"];
                    }
                }
                else
                {
                    // VerificarIngresada, igual que con un usuario existente: con espacios al inicio o al
                    // final hace dos PBKDF2, y el tiempo no debe delatar que el usuario no existe.
                    PasswordHelper.VerificarIngresada(contrasena, HashFicticio);
                }
            }
            catch (Exception ex)
            {
                // No tragar el error: un fallo de BD/lectura se registra y se propaga para
                // que el llamador lo distinga de "credenciales incorrectas".
                LogSGV.Error(ex, "Error de BD al validar credenciales del usuario {Usuario}", usuario);
                throw;
            }

            return (esValido, rol, nombre, nombreUsuario, idUsuario, idConductor, requiereCambioContrasena, selloSesion);
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

            SesionHelper.Iniciar(Session, datos.IdUsuario, datos.Rol, datos.Nombre);
            Session["NombreUsuario"] = datos.NombreUsuario;
            // Leído por varias páginas (RegistroDespacho...) para
            // registrar quién sube o edita.
            Session["Usuario"] = datos.NombreUsuario;
            Session["RequiereCambioContrasena"] = datos.RequiereCambioContrasena || datos.ContrasenaFueraDePolitica;
            SesionVigenteHelper.MarcarContrasenaFueraDePolitica(Session, datos.ContrasenaFueraDePolitica);
            // Sello de sesión: si cambian los datos de acceso del usuario, deja de coincidir con
            // el de la BD y Global.asax cierra esta sesión (SesionVigenteHelper).
            Session[SesionVigenteHelper.ClaveSello] = datos.SelloSesion;
            // Estado recién leído en el login: sin esto, un valor en caché de antes podía, por ejemplo,
            // apagar el cambio de contraseña obligatorio recién activado hasta que venciera la caché.
            SesionVigenteHelper.InvalidarCache(datos.IdUsuario);

            string rol = (datos.Rol ?? "").Trim().ToUpper();
            if (RolesHelper.EsRolConductor(rol) && datos.IdConductor.HasValue)
                Session["IdConductor"] = datos.IdConductor.Value;

            AuditoriaHelper.Registrar("LOGIN", "Usuarios", datos.IdUsuario.ToString(),
                $"Inicio de sesión - Usuario: {datos.NombreUsuario}, Rol: {datos.Rol}");

            // Rol no reconocido: Inicio.aspx lo rechaza y Login limpia la sesión (sin loops).
            Response.Redirect(RolesHelper.UrlInicioSegunRol(rol) ?? "~/Views/Inicio.aspx", true);
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
            lblError.Text = MensajeHtml.Texto(mensaje);
            pnlError.Visible = true;
        }
    }
}
