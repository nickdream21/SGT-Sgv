using System;
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

        protected void Page_Load(object sender, EventArgs e)
        {
            // 1. DETECTAR SI ESTAMOS EN LOGIN
            string rutaActual = Request.Url.AbsolutePath.ToLower();
            bool esLoginPage = rutaActual.EndsWith("/login.aspx") || rutaActual.EndsWith("/login");

            // 2. SI ES LOGIN: Ocultar navbar, inicializar propiedades y SALIR
            if (esLoginPage)
            {
                Navbar.Visible = false;
                InicializarPropiedadesVacias();
                return; // ← CRUCIAL: Salir aquí para evitar bucle
            }

            // 3. SI NO ES LOGIN: Verificar sesión
            if (!RolesHelper.TieneSesionActiva())
            {
                // Sin sesión → Redirigir a login
                Response.Redirect("~/Views/Login.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            // 4. TIENE SESIÓN VÁLIDA: Cargar información del usuario
            CargarInformacionUsuario();

            // 5. Mostrar navbar
            Navbar.Visible = true;
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

                // Limpiar cookies
                if (Request.Cookies["SGVUserInfo"] != null)
                {
                    var cookie = new System.Web.HttpCookie("SGVUserInfo")
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