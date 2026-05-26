using System;
using System.Linq;
using System.Web;
using System.Web.UI;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Helper centralizado de seguridad para el sistema SGV.
    /// Proporciona verificación de sesión, control de acceso por rol
    /// y protección anti-clickjacking para todas las páginas.
    /// </summary>
    public static class SecurityHelper
    {
        // ── Roles reconocidos ──────────────────────────────────────────────
        private const string ROL_ADMIN          = "ADMIN";
        private const string ROL_ADMIN_LEGADO   = "ADMINISTRADOR";
        private const string ROL_ADMIN_SISTEMA  = "ADMINISTRADOR DE SISTEMA";
        private const string ROL_SUPERVISOR     = "SUPERVISOR";
        private const string ROL_ADMIN_GRIFO    = "ADMINISTRADOR DE GRIFO";
        private const string ROL_ADMIN_MAQ      = "ADMINISTRADOR DE MAQUINARIA";
        private const string ROL_CONDUCTOR      = "CONDUCTOR";
        private const string ROL_OPERADOR       = "OPERADOR";

        // ── Acceso a sesión ────────────────────────────────────────────────

        /// <summary>Devuelve true si existe una sesión autenticada válida.</summary>
        public static bool TieneSesionActiva()
        {
            var session = HttpContext.Current?.Session;
            return session != null && session["UsuarioID"] != null;
        }

        /// <summary>Obtiene el rol del usuario actual en mayúsculas.</summary>
        public static string ObtenerRol()
        {
            var session = HttpContext.Current?.Session;
            return session?["Rol"]?.ToString().Trim().ToUpperInvariant() ?? string.Empty;
        }

        /// <summary>Obtiene el ID de usuario como entero (0 si no hay sesión).</summary>
        public static int ObtenerIdUsuario()
        {
            var session = HttpContext.Current?.Session;
            if (session?["IdUsuario"] != null && int.TryParse(session["IdUsuario"].ToString(), out int id))
                return id;
            return 0;
        }

        // ── Comprobadores de rol ───────────────────────────────────────────

        public static bool EsAdmin()
        {
            string rol = ObtenerRol();
            return rol == ROL_ADMIN || rol == ROL_ADMIN_LEGADO || rol == ROL_ADMIN_SISTEMA;
        }

        public static bool EsAdminOSupervisor()
        {
            string rol = ObtenerRol();
            return rol == ROL_ADMIN || rol == ROL_ADMIN_LEGADO || rol == ROL_ADMIN_SISTEMA || rol == ROL_SUPERVISOR;
        }

        public static bool EsAdminSistema()    => ObtenerRol() == ROL_ADMIN_SISTEMA;
        public static bool EsAdminGrifo()      => ObtenerRol() == ROL_ADMIN_GRIFO;
        public static bool EsAdminMaquinaria() => ObtenerRol() == ROL_ADMIN_MAQ;
        public static bool EsConductor()       => ObtenerRol() == ROL_CONDUCTOR;
        public static bool EsOperador()        => ObtenerRol() == ROL_OPERADOR;

        // ── Guardias de acceso (redirigen si no pasan) ─────────────────────

        /// <summary>
        /// Exige sesión activa. Si no hay sesión redirige a Login.
        /// Detiene la ejecución del pipeline con EndResponse = false + CompleteRequest.
        /// </summary>
        public static void ExigirSesion()
        {
            if (!TieneSesionActiva())
            {
                Redirigir("~/Views/Login.aspx?error=sesion");
            }
        }

        /// <summary>
        /// Exige que el usuario sea ADMIN o ADMIN DE SISTEMA.
        /// Primero valida sesión, luego valida rol.
        /// </summary>
        public static void ExigirRolAdmin()
        {
            ExigirSesion();
            if (!EsAdmin())
                Redirigir("~/Views/Login.aspx?error=sesion");
        }

        /// <summary>
        /// Exige ADMIN, ADMIN_SISTEMA o SUPERVISOR.
        /// </summary>
        public static void ExigirRolAdminOSupervisor()
        {
            ExigirSesion();
            if (!EsAdminOSupervisor())
                Redirigir("~/Views/Login.aspx?error=sesion");
        }

        /// <summary>
        /// Exige exclusivamente ADMINISTRADOR DE SISTEMA.
        /// Usado en páginas de administración de usuarios y roles.
        /// </summary>
        public static void ExigirRolAdminSistema()
        {
            ExigirSesion();
            if (!EsAdminSistema())
                Redirigir("~/Views/Login.aspx?error=sesion");
        }

        /// <summary>
        /// Exige ADMIN, ADMIN_SISTEMA, SUPERVISOR o ADMIN_GRIFO.
        /// (Usado en secciones de conductores y abastecimiento)
        /// </summary>
        public static void ExigirRolAdminOGrifo()
        {
            ExigirSesion();
            string rol = ObtenerRol();
            bool permitido = rol == ROL_ADMIN || rol == ROL_ADMIN_SISTEMA
                          || rol == ROL_SUPERVISOR || rol == ROL_ADMIN_GRIFO;
            if (!permitido)
                Redirigir("~/Views/Login.aspx?error=sesion");
        }

        // ── Headers de seguridad HTTP ──────────────────────────────────────

        /// <summary>
        /// Agrega cabeceras HTTP de seguridad a la respuesta actual.
        /// Llamar desde Page_Load de cada página protegida.
        /// </summary>
        public static void AgregarHeadersSeguridad()
        {
            var response = HttpContext.Current?.Response;
            if (response == null) return;

            // Anti-clickjacking
            if (!response.Headers.AllKeys.Contains("X-Frame-Options"))
                response.Headers.Add("X-Frame-Options", "SAMEORIGIN");

            // Prevenir MIME sniffing
            if (!response.Headers.AllKeys.Contains("X-Content-Type-Options"))
                response.Headers.Add("X-Content-Type-Options", "nosniff");

            // XSS básico (IE legacy)
            if (!response.Headers.AllKeys.Contains("X-XSS-Protection"))
                response.Headers.Add("X-XSS-Protection", "1; mode=block");

            // No cachear páginas protegidas
            response.Cache.SetCacheability(HttpCacheability.NoCache);
            response.Cache.SetNoStore();
            response.Cache.SetExpires(DateTime.UtcNow.AddMinutes(-1));
        }

        // ── Validación de entradas ─────────────────────────────────────────

        /// <summary>
        /// Valida que un ID de QueryString sea un entero positivo.
        /// Devuelve el valor o redirige a la URL destino si no es válido.
        /// </summary>
        public static int ValidarQueryStringId(string parametro, string urlRedireccion)
        {
            string valor = HttpContext.Current?.Request.QueryString[parametro];
            if (!int.TryParse(valor, out int id) || id <= 0)
            {
                Redirigir(urlRedireccion);
                return 0;
            }
            return id;
        }

        // ── Utilidad interna ───────────────────────────────────────────────

        private static void Redirigir(string url)
        {
            var context = HttpContext.Current;
            if (context == null) return;
            context.Response.Redirect(url, false);
            context.ApplicationInstance.CompleteRequest();
        }
    }
}
