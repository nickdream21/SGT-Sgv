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

            // ROMPE-BUCLES (E4-8): ?error=sesion con una sesión vigente (un enlace viejo, otra pestaña) va al
            // inicio de su rol sin cerrarla. Si ese inicio la rechaza y la devuelve aquí antes de 30 s, o la
            // sesión está a medias, se cierra y se muestra el formulario (SesionVigenteHelper.DecidirLoginErrorSesion).
            string errParam = Request.QueryString["error"];
            string errNormalizado = (errParam ?? "").ToLowerInvariant();
            if (errNormalizado == "sesion")
            {
                bool haySesion = SesionHelper.HaySesion(Session);
                string urlInicio = haySesion ? RolesHelper.UrlInicioSegunRol(SesionHelper.Rol(Session)) : null;
                DateTime ahora = DateTime.UtcNow;
                var decision = SesionVigenteHelper.DecidirLoginErrorSesion(
                    haySesion,
                    haySesion && SesionVigenteHelper.SelloDeSesion(Session[SesionVigenteHelper.ClaveSello]).HasValue,
                    urlInicio,
                    Session[SesionVigenteHelper.ClaveReboteLogin] as DateTime?,
                    ahora);

                if (decision == DecisionLoginErrorSesion.IrAInicio)
                {
                    Session[SesionVigenteHelper.ClaveReboteLogin] = ahora;
                    Response.Redirect(urlInicio, true);
                    return;
                }
                if (decision == DecisionLoginErrorSesion.LimpiarSesion)
                {
                    LogSGV.Advertencia("Login con error=sesion: se cierra la sesión del usuario {IdUsuario} (a medias o rebotada por su inicio)",
                        SesionHelper.IdUsuario(Session));
                    CerrarSesionActual();
                }
            }
            else if (errNormalizado == "sesion_cerrada")
            {
                // Global.asax ya la cerró; por si llega con una sesión, se limpia como antes.
                if (SesionHelper.HaySesion(Session))
                    CerrarSesionActual();
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

            // Anti-fuerza-bruta por IP, por cuenta desde esa IP y por cuenta en todas las IP
            // (ControlIntentosLogin, tabla IntentoAcceso): el intento se reserva como fallo ANTES de ir
            // a la BD, para que peticiones en paralelo no se salten el bloqueo.
            var control = ControlIntentosLogin.Sitio;
            string ip = IpClienteHelper.Obtener(Request);
            var reserva = control.ReservarLogin(ip, usuario);
            if (reserva.Rechazado)
            {
                int minutos = Math.Max(1, (int)Math.Ceiling(reserva.SegundosRestantes / 60.0));
                MostrarMensaje($"Demasiados intentos fallidos. Intente nuevamente en {minutos} minuto(s).");
                return;
            }

            ResultadoValidacion resultado;
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
                // El fallo ya quedó contado en la reserva. LOGIN_FALLIDO solo si la cuenta existe (también
                // la inactiva) y LOGIN_BLOQUEADO si esta reserva bloqueó; nunca el texto escrito.
                AuditoriaAccesoLogin.RegistrarRechazo(reserva,
                    resultado.CuentaExiste ? resultado.IdUsuario : (int?)null,
                    resultado.NombreUsuario, resultado.CuentaInactiva, ip);

                // Mismo mensaje para contraseña incorrecta, usuario inexistente y cuenta inactiva.
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

        /// <summary>Resultado de <see cref="ValidarUsuario"/>. <c>CuentaExiste</c>/<c>CuentaInactiva</c>,
        /// <c>IdUsuario</c> y <c>NombreUsuario</c> (el de la BD) se llenan también si la contraseña no
        /// coincide, solo para la auditoría; el mensaje al usuario es el mismo en todos los rechazos.</summary>
        private struct ResultadoValidacion
        {
            public bool EsValido;
            public bool CuentaExiste;
            public bool CuentaInactiva;
            public string Rol, Nombre, NombreUsuario;
            public int IdUsuario;
            public int? IdConductor;
            public bool RequiereCambioContrasena;
            public Guid SelloSesion;
        }

        private ResultadoValidacion ValidarUsuario(string usuario, string contrasena)
        {
            var r = new ResultadoValidacion { Rol = "", Nombre = "", NombreUsuario = "", SelloSesion = Guid.Empty };

            // Sin formato de usuario (ancho completo, U+FEFF, NUL...): la colación de Usuarios lo igualaría a
            // la cuenta real con otra clave de intentos. No se busca: mismo costo y mismo mensaje.
            if (!ControlIntentosLogin.UsuarioConFormatoValido(usuario))
            {
                PasswordHelper.VerificarIngresada(contrasena, HashFicticio);
                return r;
            }

            try
            {
                // Sin filtrar por activo: una cuenta inactiva se verifica igual (mismo costo) y se rechaza
                // con el mismo mensaje, pero queda su LOGIN_FALLIDO (M-062).
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT u.idUsuario, u.nombreUsuario, u.nombre, u.rol,
                           u.idConductor, u.contrasena, u.activo,
                           ISNULL(u.requiereCambioContrasena, 0) AS requiereCambioContrasena,
                           u.selloSesion
                    FROM Usuarios u
                    WHERE u.nombreUsuario = @Usuario",
                    DbHelper.Param("@Usuario", usuario));

                if (dt.Rows.Count > 0)
                {
                    DataRow reader = dt.Rows[0];
                    string storedHash = reader["contrasena"].ToString();
                    r.IdUsuario = Convert.ToInt32(reader["idUsuario"]);
                    r.CuentaExiste = true;
                    r.CuentaInactiva = reader["activo"] == DBNull.Value || !Convert.ToBoolean(reader["activo"]);
                    r.NombreUsuario = reader["nombreUsuario"].ToString().Trim();
                    int idUsuario = r.IdUsuario;

                    string coincidente;
                    bool coincide = PasswordHelper.VerificarIngresada(contrasena, storedHash, out coincidente);

                    // Hash del formato anterior (PBKDF2-SHA1) → PBKDF2-SHA256. Si falla, el login sigue.
                    // Una cuenta inactiva no se convierte: IgualarCostoVerificacion paga el mismo costo.
                    bool convertido = false;
                    if (coincide && !r.CuentaInactiva)
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

                    if (coincide && !r.CuentaInactiva)
                    {
                        r.Nombre = reader["nombre"].ToString().Trim();
                        r.Rol    = reader["rol"].ToString().Trim();

                        if (RolesHelper.EsRolConductor(r.Rol) && reader["idConductor"] != DBNull.Value)
                            r.IdConductor = Convert.ToInt32(reader["idConductor"]);

                        r.EsValido = true;
                        r.RequiereCambioContrasena = Convert.ToBoolean(reader["requiereCambioContrasena"]);
                        r.SelloSesion = (Guid)reader["selloSesion"];
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
                // Sin el texto escrito: a veces es la contraseña.
                LogSGV.Error(ex, "Error de BD al validar credenciales de un inicio de sesión");
                throw;
            }

            return r;
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

            // El id del conductor solo queda si el rol es conductor (SesionHelper.Iniciar).
            SesionHelper.Iniciar(Session, datos.IdUsuario, datos.Rol, datos.Nombre, datos.IdConductor);
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

        /// <summary>Cierra la sesión de esta petición y borra su cookie (el formulario se muestra sin sesión).</summary>
        private void CerrarSesionActual()
        {
            Session.Clear();
            Session.Abandon();
            Response.Cookies.Add(new HttpCookie("SGV_SessionId", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
        }

        private void MostrarMensaje(string mensaje)
        {
            lblError.Text = MensajeHtml.Texto(mensaje);
            pnlError.Visible = true;
        }
    }
}
