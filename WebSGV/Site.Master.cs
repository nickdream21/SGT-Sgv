using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>Menús que ve el rol actual (MenuSitio.Para, E5-19c); vacío sin sesión.</summary>
        public IReadOnlyList<GrupoMenu> Menu { get; private set; } = new GrupoMenu[0];
        /// <summary>Opciones de Configuración que vienen de MenuSitio ("Ayuda", E5-21); vacío sin sesión.</summary>
        public IReadOnlyList<EntradaMenu> MenuConfiguracion { get; private set; } = new EntradaMenu[0];
        /// <summary>Enlace "Mi seguridad" (segundo factor, E4-14): solo los roles de RolesHelper.ExigeSegundoFactor.</summary>
        public bool MuestraMiSeguridad { get; set; }
        /// <summary>Página de inicio del rol actual (logo y enlace "Inicio" del menú).</summary>
        public string UrlInicio { get; set; }
        /// <summary>Atributos del &lt;html&gt; para SesionRestaurada.js (solo con sesión, en páginas no públicas).</summary>
        public string AtributosSesion { get; private set; } = "";

        /// <summary>Página actual como ruta virtual ("~/Views/X.aspx"), para marcar la opción activa del menú.</summary>
        public string RutaActual => Request.AppRelativeCurrentExecutionFilePath;

        /// <summary>Clases del menú desplegable: "active" si contiene la página actual (M-076).</summary>
        public string ClaseGrupo(GrupoMenu g) =>
            (g.EsEnlaceDirecto ? "nav-item" : "nav-item dropdown") + (MenuSitio.EsActivo(g, RutaActual) ? " active" : "");

        /// <summary>Clases de una opción: dropdown-item o nav-link, más "active" si es la página actual.</summary>
        public string ClaseEntrada(GrupoMenu g, EntradaMenu e) =>
            (g.EsEnlaceDirecto ? "nav-link" : "dropdown-item") + (MenuSitio.EsActiva(e, RutaActual) ? " active" : "");

        /// <summary>aria-current="page" en la opción de la página actual; nada en las demás (texto fijo, sin datos).</summary>
        public string AriaActual(EntradaMenu e) => MenuSitio.EsActiva(e, RutaActual) ? " aria-current=\"page\"" : "";

        private bool EnMiSeguridad => MenuSitio.EsActiva(UrlMiSeguridad, RutaActual);

        /// <summary>Menú Configuración: "active" cuando la página actual es Mi seguridad o una de sus opciones de MenuSitio.</summary>
        public string ClaseConfiguracion => "nav-item dropdown"
            + (EnMiSeguridad || MenuConfiguracion.Any(o => MenuSitio.EsActiva(o, RutaActual)) ? " active" : "");

        /// <summary>Clases de una opción de Configuración que viene de MenuSitio.</summary>
        public string ClaseOpcionConfiguracion(EntradaMenu e) => "dropdown-item" + (MenuSitio.EsActiva(e, RutaActual) ? " active" : "");

        /// <summary>El rol tiene al menos un tema de ayuda abierto (si no, "Ayuda" no sale en el menú).</summary>
        private static bool RolTieneAyuda(string rol)
        {
            try
            {
                var catalogo = WebSGV.Services.Ayuda.AyudaCatalogo.CargarConCache(
                    System.Web.Hosting.HostingEnvironment.MapPath("~/ContenidoAyuda"));
                return catalogo.TemasDelRol(rol, WebSGV.Views.Ayuda.InterruptorEncendido).Count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Menú: no se pudo leer el catálogo de la ayuda");
                return false;
            }
        }

        /// <summary>Opción "Mi seguridad": "active" en su propia página.</summary>
        public string ClaseMiSeguridad => "dropdown-item" + (EnMiSeguridad ? " active" : "");

        /// <summary>aria-current="page" en "Mi seguridad" cuando es la página actual (texto fijo).</summary>
        public string AriaMiSeguridad => EnMiSeguridad ? " aria-current=\"page\"" : "";

        /// <summary>Opción "Mi seguridad" de Configuración (fuera de MenuSitio: depende del segundo factor).</summary>
        public const string UrlMiSeguridad = "~/Views/MiSeguridad.aspx";

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

            // NO establecer ViewStateUserKey en páginas públicas
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA3007:Review code for open redirect vulnerabilities",
            Justification = "El destino empieza siempre por la ruta \"~/...\" del archivo que se ejecuta; la consulta de la petición va detrás de '?' y no puede cambiar el sitio.")]
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
                // Ruta "~/..." del archivo que se ejecuta (no RawUrl: una ruta que empiece por "//" sería
                // relativa al protocolo y llevaría a otro sitio) más la consulta de esta petición.
                Response.Redirect(Request.AppRelativeCurrentExecutionFilePath + Request.Url.Query, true);
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
            AtributosSesion = SesionRestauradaHelper.AtributosHtml(Session);

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
            Menu = new GrupoMenu[0];
            MenuConfiguracion = new EntradaMenu[0];
            MuestraMiSeguridad = false;
            UrlInicio = "~/Views/Login.aspx";
        }

        private void CargarInformacionUsuario()
        {
            try
            {
                RolUsuario = RolesHelper.ObtenerRolActual() ?? "Sin rol";
                NombreUsuario = RolesHelper.ObtenerNombreUsuario() ?? "Usuario";
                Menu = MenuSitio.Para(RolesHelper.ObtenerRolActual(),
                    WebSGV.Services.Liquidaciones.MovimientosViajeService.Habilitado);
                MenuConfiguracion = MenuSitio.ConfiguracionPara(RolesHelper.ObtenerRolActual(), RolTieneAyuda);
                MuestraMiSeguridad = RolesHelper.ExigeSegundoFactor(RolesHelper.ObtenerRolActual());
                UrlInicio = RolesHelper.UrlInicioSegunRol(RolUsuario) ?? "~/Views/Login.aspx";
            }
            catch (Exception ex)
            {
                // Sin menú ni rol a la vista; que quede en el log (E6-13: antes no dejaba rastro).
                LogSGV.Error(ex, "Site.Master: no se pudo armar el menú y los datos del usuario");
                InicializarPropiedadesVacias();
            }
        }

        private int IdUsuarioActual => SesionHelper.IdUsuario(Session) ?? 0;

        protected void btnCambiarContrasena_Click(object sender, EventArgs e)
        {
            try
            {
                // Validación (mismos mensajes y orden), la actual con el contador de la firma, el UPDATE con
                // sello nuevo y la auditoría: ContrasenaService (E6-16). La nueva se guarda recortada.
                var cambio = Services.Usuarios.ContrasenaService.CambiarPropia(IdUsuarioActual,
                    txtContrasenaActual.Text, txtNuevaContrasena.Text, txtConfirmarContrasena.Text);
                if (!cambio.Cambiada)
                {
                    MostrarMensajeContrasena(cambio.Error, "danger");
                    AbrirModalContrasena();
                    return;
                }

                // Esta sesión guarda el sello nuevo y sigue abierta; las demás del usuario se cierran.
                Session[SesionVigenteHelper.ClaveSello] = cambio.SelloNuevo;

                txtContrasenaActual.Text = txtNuevaContrasena.Text = txtConfirmarContrasena.Text = "";
                pnlMensajeContrasena.Visible = false;
                Session["RequiereCambioContrasena"] = false;
                SesionVigenteHelper.MarcarContrasenaFueraDePolitica(Session, false);

                ScriptManager.RegisterStartupScript(this, GetType(), "SuccessContrasena",
                    "$('#modalCambiarContrasena').modal('hide'); SGV.avisar('Contraseña cambiada correctamente.', { tipo: 'success' });", true);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cambiar la contraseña del usuario {IdUsuario}", IdUsuarioActual);
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

                // Invalidar la cookie de sesión (expirarla, NO vaciarla)
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
