using System;
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

        /// <summary>
        /// Page_Init: Vincula el ViewState a la sesión del usuario.
        /// Solo se aplica a páginas protegidas (no públicas) para evitar
        /// HttpException cuando el SessionID cambia entre GET y POST.
        /// </summary>
        protected void Page_Init(object sender, EventArgs e)
        {
            // ✅ NO establecer ViewStateUserKey en páginas públicas
            // porque si el SessionID cambia (expiración, regeneración, cookie corrupta),
            // el ViewState firmado con el ID anterior será inválido → HttpException
            if (Page.Session != null && !EsPaginaPublica())
            {
                Page.ViewStateUserKey = Session.SessionID;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // 1. DETECTAR SI ESTAMOS EN PÁGINAS PÚBLICAS (sin autenticación)
            System.Diagnostics.Debug.WriteLine($"🔍 Ruta actual: {Request.Url.AbsolutePath.ToLower()}");

            bool esPaginaPublica = EsPaginaPublica();

            System.Diagnostics.Debug.WriteLine($"🔍 Archivo: {System.IO.Path.GetFileName(Request.Url.AbsolutePath).ToLower()}, Es pública: {esPaginaPublica}");

            // 2. SI ES PÁGINA PÚBLICA: Ocultar navbar, inicializar propiedades y SALIR
            if (esPaginaPublica)
            {
                Navbar.Visible = false;
                InicializarPropiedadesVacias();
                return; // ← CRUCIAL: Salir aquí para evitar bucle
            }

            // 3. ✅ RECONSTRUIR SESIÓN desde cookie temporal post-login
            if (!TieneSesionActivaLocal())
            {
                if (ReconstruirSesionDesdeAuthTemp())
                {
                    // Sesión reconstruida exitosamente, continuar
                    System.Diagnostics.Debug.WriteLine("🔄 Sesión reconstruida desde cookie temporal");
                }
                else
                {
                    // Sin sesión y sin cookie temporal → Redirigir a login
                    Response.Redirect("~/Views/Login.aspx", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
            }

            // 4. TIENE SESIÓN VÁLIDA: Cargar información del usuario
            CargarInformacionUsuario();

            // 5. Mostrar navbar
            Navbar.Visible = true;
        }

        /// <summary>
        /// Determina si la página actual es pública (no requiere autenticación).
        /// Se usa tanto en Page_Init como en Page_Load.
        /// </summary>
        private bool EsPaginaPublica()
        {
            string nombreArchivo = System.IO.Path.GetFileName(Request.Url.AbsolutePath).ToLower();

            return nombreArchivo == "login.aspx" ||
                   nombreArchivo == "recuperarcontrasena.aspx" ||
                   nombreArchivo == "restablecercontrasena.aspx" ||
                   nombreArchivo == "error.aspx" ||
                   nombreArchivo == "error404.aspx" ||
                   string.IsNullOrEmpty(nombreArchivo);
        }

        /// <summary>
        /// Reconstruye la sesión desde la cookie temporal creada durante el login.
        /// Esto completa el flujo de anti-fijación de sesión.
        /// </summary>
        private bool ReconstruirSesionDesdeAuthTemp()
        {
            try
            {
                HttpCookie authTemp = Request.Cookies["SGV_AuthTemp"];

                if (authTemp == null || string.IsNullOrEmpty(authTemp.Values["uid"]))
                    return false;

                // Reconstruir la sesión con los datos de la cookie
                Session["UsuarioID"] = authTemp.Values["uid"];
                Session["IdUsuario"] = Convert.ToInt32(authTemp.Values["uid"]);
                Session["Rol"] = authTemp.Values["rol"];
                Session["Nombre"] = HttpUtility.UrlDecode(authTemp.Values["nombre"]);
                Session["NombreUsuario"] = HttpUtility.UrlDecode(authTemp.Values["nombreUsuario"]);

                string rol = authTemp.Values["rol"] ?? "";
                string idConductorStr = authTemp.Values["idConductor"];

                if (rol.ToUpper() == "CONDUCTOR" && !string.IsNullOrEmpty(idConductorStr))
                {
                    Session["IdConductor"] = Convert.ToInt32(idConductorStr);
                }

                // ✅ Eliminar la cookie temporal inmediatamente (uso único)
                HttpCookie expiredCookie = new HttpCookie("SGV_AuthTemp")
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(expiredCookie);

                System.Diagnostics.Debug.WriteLine($"✅ Sesión reconstruida - Usuario: {Session["Nombre"]}, Rol: {Session["Rol"]}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error reconstruyendo sesión: {ex.Message}");
                return false;
            }
        }

        // Método local para evitar problemas con HttpContext
        private bool TieneSesionActivaLocal()
        {
            try
            {
                return Session["UsuarioID"] != null && !string.IsNullOrEmpty(Session["UsuarioID"].ToString());
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
        }

        private void CargarInformacionUsuario()
        {
            try
            {
                RolUsuario = RolesHelper.ObtenerRolActual() ?? "Sin rol";
                NombreUsuario = RolesHelper.ObtenerNombreUsuario() ?? "Usuario";
                EsAdmin = RolesHelper.EsAdmin();
                EsConductor = RolesHelper.EsConductor();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cargar usuario: {ex.Message}");
                InicializarPropiedadesVacias();
            }
        }

        protected void btnCerrarSesion_Click(object sender, EventArgs e)
        {
            try
            {
                // Limpiar sesión
                Session.Clear();
                Session.Abandon();

                // ✅ Invalidar la cookie de sesión (expirarla, NO vaciarla)
                HttpCookie sessionCookie = new HttpCookie("SGV_SessionId")
                {
                    Expires = DateTime.Now.AddDays(-1)
                };
                Response.Cookies.Add(sessionCookie);

                // Limpiar cookie de recordarme
                if (Request.Cookies["SGVUserInfo"] != null)
                {
                    var cookie = new HttpCookie("SGVUserInfo")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(cookie);
                }

                // Limpiar cookie temporal si existiera
                if (Request.Cookies["SGV_AuthTemp"] != null)
                {
                    var cookie = new HttpCookie("SGV_AuthTemp")
                    {
                        Expires = DateTime.Now.AddDays(-1)
                    };
                    Response.Cookies.Add(cookie);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al cerrar sesión: {ex.Message}");
            }

            // Redirigir al login
            Response.Redirect("~/Views/Login.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}