using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;
using WebSGV.Helpers;


namespace WebSGV
{
    public partial class SiteMaster : MasterPage
    {
        // Propiedades públicas para el Site.Master
        public string RolUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public bool EsAdmin { get; set; }
        public bool EsConductor { get; set; }
        public bool EsAdminSistema { get; set; }
        public bool EsContabilidad { get; set; }
        /// <summary>Enlace "Mi seguridad" (segundo factor, E4-14): solo los roles de RolesHelper.ExigeSegundoFactor.</summary>
        public bool MuestraMiSeguridad { get; set; }
        /// <summary>
        /// Rol SUPERVISOR. Calculada desde <see cref="RolUsuario"/> (igual que la comparación que
        /// había en el markup), así queda en sintonía con InicializarPropiedadesVacias y
        /// CargarInformacionUsuario sin tener que asignarla en cada uno.
        /// </summary>
        public bool EsSupervisor => RolUsuario == RolesHelper.ROL_SUPERVISOR;
        /// <summary>Página de inicio del rol actual (logo y enlace "Inicio" del menú).</summary>
        public string UrlInicio { get; set; }

        /// <summary>
        /// Nombre de usuario con el que se inicia sesión (no el nombre para mostrar). Va en el campo
        /// oculto autocomplete="username" del modal "Cambiar contraseña", para que el gestor de
        /// contraseñas del navegador asocie el cambio a la cuenta correcta. Vacío sin sesión.
        /// </summary>
        public string UsuarioLogin
        {
            get
            {
                try
                {
                    return Convert.ToString(Session?["Usuario"] ?? Session?["NombreUsuario"]) ?? "";
                }
                catch
                {
                    return "";
                }
            }
        }

        /// <summary>
        /// Page_Init: Vincula el ViewState a la sesión del usuario.
        /// Solo se aplica a páginas protegidas (no públicas) para evitar
        /// HttpException cuando el SessionID cambia entre GET y POST.
        /// </summary>
        protected void Page_Init(object sender, EventArgs e)
        {
            // Sin caché (no-store) en toda página con master, con o sin sesión: tras cerrar sesión,
            // el botón Atrás no vuelve a mostrar datos de la sesión anterior. El Init del master
            // corre antes del Page_Load de la página, así que cubre también sus redirecciones.
            SecurityHelper.AgregarHeadersSeguridad();

            // Postback sin sesión en una página protegida: al Login antes de validar el ViewState
            // (si no, la validación MAC falla primero y el usuario no ve nada). En un postback
            // parcial el ScriptManager convierte el redirect en "pageRedirect".
            if (Page.IsPostBack && !EsPaginaPublica() && !TieneSesionActivaLocal())
            {
                Response.Redirect("~/Views/Login.aspx?error=sesion", true);
                return;
            }

            // ✅ NO establecer ViewStateUserKey en páginas públicas
            // porque si el SessionID cambia (expiración, regeneración, cookie corrupta),
            // el ViewState firmado con el ID anterior será inválido → HttpException
            if (Page.Session != null && !EsPaginaPublica())
            {
                Page.ViewStateUserKey = Session.SessionID;
                BloquearAccionesSiDebeCambiarContrasena();
            }
        }

        /// <summary>
        /// Aviso que la página muestra al cargar (parámetro <c>?aviso=</c>, p. ej. tras recargarla por
        /// ViewState vencido). Solo valores conocidos (un valor desconocido no se muestra); el markup
        /// lo codifica con HttpUtility.JavaScriptStringEncode.
        /// </summary>
        public string AvisoInicial =>
            (IsPostBack ? null : ErrorAplicacionHelper.TextoAviso(Request.QueryString["aviso"])) ?? "";

        /// <summary>
        /// Error en un postback parcial (UpdatePanel). Sin este manejador el navegador recibía
        /// PageRequestManagerServerErrorException y el usuario no veía nada. El texto lo interpreta
        /// SiteMaster-2.js (endRequest): SGV:SESION_VENCIDA → Login, SGV:PAGINA_VENCIDA → recarga,
        /// otro → aviso. El ScriptManager NO limpia el error: después de este evento llega también a
        /// Application_Error. Por eso se marca en HttpContext.Items
        /// (ErrorAplicacionHelper.ClaveErrorPostbackParcialManejado) y Application_Error solo lo limpia,
        /// sin registrarlo otra vez ni reemplazar la respuesta que armó el ScriptManager.
        /// </summary>
        protected void ScriptManager1_AsyncPostBackError(object sender, AsyncPostBackErrorEventArgs e)
        {
            Context.Items[ErrorAplicacionHelper.ClaveErrorPostbackParcialManejado] = true;
            try
            {
                Exception real = ErrorAplicacionHelper.Desenvolver(e.Exception);
                bool haySesion = TieneSesionActivaLocal();
                ScriptManager1.AsyncPostBackErrorMessage = ErrorAplicacionHelper.MensajePostbackParcial(real, haySesion);

                // Para el log: sin los valores del querystring (p. ej. el token de RestablecerContrasena).
                string ruta = Request.Path + WebSGV.Services.Operacion.AvisoErrores.QuerySinValores(Request.Url.Query);
                if (ErrorAplicacionHelper.EsErrorViewState(real))
                    LogSGV.Advertencia("ViewState inválido o vencido en postback parcial de {Ruta} (con sesión: {HaySesion}). {Error}",
                        ruta, haySesion, real?.Message);
                else if (real != null && WebSGV.Services.Common.MensajeError.ExtraerMensajeDeNegocio(real) == null)
                {
                    LogSGV.Error(real, "Error en postback parcial de {Ruta} (usuario {IdUsuario})",
                        ruta, haySesion ? SesionHelper.IdUsuario(Session) : (int?)null);
                    // E1-7: Application_Error sale antes del aviso para los postbacks parciales ya manejados.
                    WebSGV.Services.Operacion.AvisoErrores.Notificar(real, Context);
                }
            }
            catch
            {
                // Nunca lanzar aquí: el ScriptManager respondería con su propio error.
                ScriptManager1.AsyncPostBackErrorMessage = WebSGV.Services.Common.MensajeError.Generico;
            }
        }

        /// <summary>
        /// Cambio de contraseña obligatorio aplicado en el servidor: antes solo se abría un modal
        /// y el usuario podía seguir operando. Se ejecuta en Page_Init (antes del Load de la página
        /// y de los eventos): cualquier postback que no sea "Cambiar contraseña" o "Cerrar sesión"
        /// se descarta recargando la página, que vuelve a mostrar el modal obligatorio.
        /// </summary>
        private void BloquearAccionesSiDebeCambiarContrasena()
        {
            if (!Page.IsPostBack || !TieneSesionActivaLocal() || !RequiereCambioContrasena())
                return;

            string origen = Request.Form["__EVENTTARGET"] ?? "";
            bool esCambioContrasena = Request.Form[btnCambiarContrasena.UniqueID] != null
                                      || origen == btnCambiarContrasena.UniqueID;
            bool esCerrarSesion = origen == btnCerrarSesion.UniqueID
                                  || Request.Form[btnCerrarSesion.UniqueID] != null;
            // "Ya los guardé" de MiSeguridad: sin él, los códigos del alta obligatoria quedan en pantalla
            // y el modal de cambio de contraseña no se abre en esa página (E4-14).
            string idCodigosGuardados = (Page as Views.MiSeguridad)?.UniqueIdCodigosGuardados;
            bool esCodigosGuardados = idCodigosGuardados != null
                                      && (origen == idCodigosGuardados || Request.Form[idCodigosGuardados] != null);

            if (!esCambioContrasena && !esCerrarSesion && !esCodigosGuardados)
                Response.Redirect(Request.RawUrl, true);
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            txtNuevaContrasena.Attributes["placeholder"] = PasswordHelper.TextoMinimoNueva;

            bool esPaginaPublica = EsPaginaPublica();

            // Si es página pública: ocultar navbar y salir
            if (esPaginaPublica)
            {
                Navbar.Visible = false;
                InicializarPropiedadesVacias();
                return;
            }

            // Sin sesión válida → redirigir a login
            if (!TieneSesionActivaLocal())
            {
                // EndResponse = true: detiene la página (incluidos los eventos de postback)
                Response.Redirect("~/Views/Login.aspx", true);
                return;
            }

            // Sesión válida: cargar datos del usuario y mostrar navbar
            CargarInformacionUsuario();
            Navbar.Visible = true;

            // Con códigos de recuperación recién generados en pantalla, el modal obligatorio los taparía: se abre
            // cuando el usuario pulsa "Ya los guardé" (recarga sin códigos). Solo en MiSeguridad, como mucho 10 min.
            bool mostrandoCodigos = (Page as Views.MiSeguridad)?.MostrandoCodigosRecuperacion == true;
            if (!IsPostBack && RequiereCambioContrasena() && !mostrandoCodigos)
            {
                // Entró con una contraseña que no cumple las reglas (Seguridad.ExigirPoliticaAlIngresar).
                if (SesionVigenteHelper.ContrasenaFueraDePolitica(Session))
                    MostrarMensajeContrasena("Por seguridad debes cambiar tu contraseña: la actual no cumple las reglas de seguridad. "
                        + PoliticaContrasena.TextoAyuda, "warning");
                AbrirModalContrasenaObligatoria();
            }
        }

        /// <summary>
        /// Determina si la página actual es pública (no requiere autenticación).
        /// Se usa tanto en Page_Init como en Page_Load.
        /// </summary>
        private bool EsPaginaPublica()
        {
            // Misma lista que el control de sesión de Global.asax (SesionVigenteHelper).
            return SesionVigenteHelper.EsRutaPublica(System.IO.Path.GetFileName(Request.Url.AbsolutePath));
        }


        private bool TieneSesionActivaLocal()
        {
            try
            {
                return SesionHelper.HaySesion(Session);
            }
            catch
            {
                return false;
            }
        }

        private void InicializarPropiedadesVacias()
        {
            RolUsuario = "";
            NombreUsuario = "";
            EsAdmin = false;
            EsConductor = false;
            EsAdminSistema = false;
            EsContabilidad = false;
            MuestraMiSeguridad = false;
            UrlInicio = "~/Views/Login.aspx";
        }

        private void CargarInformacionUsuario()
        {
            try
            {
                RolUsuario = RolesHelper.ObtenerRolActual() ?? "Sin rol";
                NombreUsuario = RolesHelper.ObtenerNombreUsuario() ?? "Usuario";
                EsAdmin = RolesHelper.EsAdmin();
                EsConductor = RolesHelper.EsConductor();
                EsAdminSistema = RolesHelper.EsAdminSistema();
                EsContabilidad = RolesHelper.EsContabilidad();
                MuestraMiSeguridad = RolesHelper.ExigeSegundoFactor(RolesHelper.ObtenerRolActual());
                UrlInicio = RolesHelper.UrlInicioSegunRol(RolUsuario) ?? "~/Views/Login.aspx";
            }
            catch
            {
                InicializarPropiedadesVacias();
            }
        }

        private int IdUsuarioActual => SesionHelper.IdUsuario(Session) ?? 0;

        protected void btnCambiarContrasena_Click(object sender, EventArgs e)
        {
            try
            {
                string contrasenaActual = txtContrasenaActual.Text;
                // La nueva se guarda recortada, como en Restablecer contraseña: el autocompletado del
                // celular agrega un espacio al final y después la persona no podía entrar sin él.
                string nuevaContrasena = (txtNuevaContrasena.Text ?? "").Trim();
                string confirmarContrasena = (txtConfirmarContrasena.Text ?? "").Trim();

                if (string.IsNullOrEmpty(contrasenaActual) || string.IsNullOrEmpty(nuevaContrasena) || string.IsNullOrEmpty(confirmarContrasena))
                {
                    MostrarMensajeContrasena("Todos los campos son obligatorios.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                string errorNueva = ContrasenaNuevaHelper.ValidarParaUsuario(nuevaContrasena, IdUsuarioActual);
                if (errorNueva != null)
                {
                    MostrarMensajeContrasena(errorNueva, "danger");
                    AbrirModalContrasena();
                    return;
                }

                if (nuevaContrasena != confirmarContrasena)
                {
                    MostrarMensajeContrasena("Las contraseñas nuevas no coinciden.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                if (nuevaContrasena == contrasenaActual || nuevaContrasena == contrasenaActual.Trim())
                {
                    MostrarMensajeContrasena("La nueva contraseña debe ser distinta de la actual.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                if (IdUsuarioActual == 0)
                {
                    MostrarMensajeContrasena("No se encontró la cuenta de usuario activa.", "danger");
                    AbrirModalContrasena();
                    return;
                }

                // Mismo contador de intentos que la firma: 5 incorrectas en 15 min bloquean ambas.
                string errorActual = ReautenticacionHelper.Verificar(IdUsuarioActual, contrasenaActual,
                    PropositoReautenticacion.CambioContrasena);
                if (errorActual != null)
                {
                    MostrarMensajeContrasena(errorActual, "danger");
                    AbrirModalContrasena();
                    return;
                }

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string nuevoHash = PasswordHelper.HashPassword(nuevaContrasena);
                    // Nuevo sello: se cierran las demás sesiones del usuario; esta sigue abierta
                    // porque guarda el sello nuevo (decisión del usuario, auditoría tarea 5).
                    Guid nuevoSello = Guid.NewGuid();
                    using (SqlCommand cmd = new SqlCommand(
                        "UPDATE Usuarios SET contrasena = @contrasena, requiereCambioContrasena = 0, resetToken = NULL, resetTokenExpira = NULL, selloSesion = @sello WHERE idUsuario = @idUsuario", conn))
                    {
                        cmd.Parameters.AddWithValue("@contrasena", nuevoHash);
                        cmd.Parameters.AddWithValue("@sello", nuevoSello);
                        cmd.Parameters.AddWithValue("@idUsuario", IdUsuarioActual);
                        cmd.ExecuteNonQuery();
                    }
                    Session[SesionVigenteHelper.ClaveSello] = nuevoSello;
                    SesionVigenteHelper.InvalidarCache(IdUsuarioActual);
                }

                txtContrasenaActual.Text = "";
                txtNuevaContrasena.Text = "";
                txtConfirmarContrasena.Text = "";
                pnlMensajeContrasena.Visible = false;
                Session["RequiereCambioContrasena"] = false;
                SesionVigenteHelper.MarcarContrasenaFueraDePolitica(Session, false);

                AuditoriaHelper.Registrar("UPDATE", "Usuarios", IdUsuarioActual, "Contrasena cambiada por el usuario");

                ScriptManager.RegisterStartupScript(this, GetType(), "SuccessContrasena",
                    "$('#modalCambiarContrasena').modal('hide'); SGV.avisar('Contraseña cambiada correctamente.', { tipo: 'success' });", true);
            }
            catch
            {
                MostrarMensajeContrasena("Ocurrió un error al cambiar la contraseña. Inténtalo de nuevo.", "danger");
                AbrirModalContrasena();
            }
        }

        private void MostrarMensajeContrasena(string mensaje, string tipo)
        {
            // Dentro del modal también el éxito; el texto se codifica (antes iba tal cual).
            AvisoHelper.Pintar(lblMensajeContrasena, mensaje, tipo, "mb-0");
            pnlMensajeContrasena.Visible = true;
        }

        private void AbrirModalContrasena()
        {
            if (RequiereCambioContrasena())
                AbrirModalContrasenaObligatoria();
            else
                ScriptManager.RegisterStartupScript(this, GetType(), "AbrirModalContrasena", "$('#modalCambiarContrasena').modal('show');", true);
        }

        private bool RequiereCambioContrasena()
        {
            return Session["RequiereCambioContrasena"] != null
                && Convert.ToBoolean(Session["RequiereCambioContrasena"]);
        }

        private void AbrirModalContrasenaObligatoria()
        {
            const string script = @"
                $(function () {
                    var modal = $('#modalCambiarContrasena');
                    modal.find('.js-cerrar-contrasena').hide();
                    modal.modal({ backdrop: 'static', keyboard: false, show: true });
                });";
            ScriptManager.RegisterStartupScript(this, GetType(), "CambioContrasenaObligatorio", script, true);
        }

        protected void btnCerrarSesion_Click(object sender, EventArgs e)
        {
            try
            {
                // Registrar auditoría de logout antes de limpiar sesión
                AuditoriaHelper.Registrar("LOGOUT", "Usuarios",
                    descripcion: $"Cierre de sesión - Usuario: {NombreUsuario}, Rol: {RolUsuario}");

                // Limpiar sesión
                Session.Clear();
                Session.Abandon();

                // ✅ Invalidar la cookie de sesión (expirarla, NO vaciarla)
                HttpCookie sessionCookie = new HttpCookie("SGV_SessionId")
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(sessionCookie);

                // La cookie "Recordarme" (SGVUserInfo) NO se borra al cerrar sesión: si no, nunca
                // recordaría el usuario. Se borra en Login cuando entra con la casilla desmarcada.
            }
            catch (Exception ex)
            {
                // La limpieza de la cookie no debe impedir el logout; se registra el fallo.
                LogSGV.Advertencia("No se pudo limpiar la cookie de sesión en el logout: {Error}", ex.Message);
            }

            // Redirigir al login
            Response.Redirect("~/Views/Login.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
