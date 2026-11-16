using System;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Helper centralizado de seguridad para el sistema SGV.
    /// Proporciona verificación de sesión, control de acceso por rol
    /// y protección anti-clickjacking para todas las páginas.
    /// </summary>
    public static class SecurityHelper
    {
        // Los nombres de rol viven solo en RolesHelper (ROL_*, EsRolAdmin, EsRolConductor).

        // ── Acceso a sesión ────────────────────────────────────────────────

        /// <summary>Devuelve true si existe una sesión autenticada válida.</summary>
        public static bool TieneSesionActiva() => SesionHelper.HaySesionActual;

        /// <summary>Obtiene el rol del usuario actual en mayúsculas.</summary>
        public static string ObtenerRol() => SesionHelper.RolActual;

        /// <summary>Obtiene el ID de usuario como entero (0 si no hay sesión).</summary>
        public static int ObtenerIdUsuario() => SesionHelper.IdUsuarioActual ?? 0;

        // ── Comprobadores de rol ───────────────────────────────────────────

        /// <summary>
        /// Administrador de la operación (<see cref="RolesHelper.EsRolAdmin"/>). ADMINISTRADOR DE
        /// SISTEMA no cuenta: solo gestiona usuarios y auditoría (<see cref="EsAdminSistema"/>).
        /// </summary>
        public static bool EsAdmin() => RolesHelper.EsRolAdmin(ObtenerRol());

        public static bool EsAdminOSupervisor() => EsAdmin() || ObtenerRol() == RolesHelper.ROL_SUPERVISOR;

        public static bool EsAdminSistema()    => ObtenerRol() == RolesHelper.ROL_ADMIN_SISTEMA;
        public static bool EsAdminGrifo()      => ObtenerRol() == RolesHelper.ROL_ADMIN_GRIFO;
        public static bool EsAdminMaquinaria() => ObtenerRol() == RolesHelper.ROL_ADMIN_MAQUINARIA;
        /// <summary>CONDUCTOR o CHOFER (<see cref="RolesHelper.EsRolConductor"/>).</summary>
        public static bool EsConductor()       => RolesHelper.EsRolConductor(ObtenerRol());
        public static bool EsOperador()        => ObtenerRol() == RolesHelper.ROL_OPERADOR;
        public static bool EsContabilidad()    => ObtenerRol() == RolesHelper.ROL_CONTABILIDAD;

        /// <summary>Roles autorizados para aprobar, rechazar o corregir liquidaciones.</summary>
        public static bool PuedeGestionarLiquidaciones() => EsAdminOSupervisor();

        /// <summary>Roles con consulta global de liquidaciones (sin limitar por conductor).</summary>
        public static bool PuedeConsultarLiquidacionesGlobales() =>
            EsAdminOSupervisor() || EsContabilidad();

        // ── Guardias de acceso (redirigen si no pasan) ─────────────────────

        /// <summary>
        /// Exige sesión activa. Si no hay sesión redirige a Login y termina la respuesta.
        /// </summary>
        public static void ExigirSesion()
        {
            if (!TieneSesionActiva())
            {
                Redirigir("~/Views/Login.aspx?error=sesion");
            }
        }

        /// <summary>
        /// Exige un administrador de la operación (no ADMINISTRADOR DE SISTEMA).
        /// Primero valida sesión, luego valida rol.
        /// </summary>
        public static void ExigirRolAdmin()
        {
            ExigirSesion();
            if (!EsAdmin())
                RedirigirSinPermiso();
        }

        /// <summary>
        /// Exige un administrador de la operación o SUPERVISOR.
        /// </summary>
        public static void ExigirRolAdminOSupervisor()
        {
            ExigirSesion();
            if (!EsAdminOSupervisor())
                RedirigirSinPermiso();
        }

        /// <summary>
        /// Exige exclusivamente ADMINISTRADOR DE SISTEMA.
        /// Usado en páginas de administración de usuarios y roles.
        /// </summary>
        public static void ExigirRolAdminSistema()
        {
            ExigirSesion();
            if (!EsAdminSistema())
                RedirigirSinPermiso();
        }

        /// <summary>
        /// Exige exclusivamente rol OPERADOR. Con otro rol va al inicio de ese rol (no cierra la sesiÃ³n).
        /// </summary>
        public static void ExigirRolOperador()
        {
            ExigirSesion();
            if (!EsOperador())
                RedirigirSinPermiso();
        }

        /// <summary>
        /// Exige rol CONDUCTOR (o CHOFER). Con otro rol va al inicio de ese rol (no cierra la sesión).
        /// </summary>
        public static void ExigirRolConductor()
        {
            ExigirSesion();
            if (!EsConductor())
                RedirigirSinPermiso();
        }

        /// <summary>
        /// Exige un administrador de la operación, SUPERVISOR o ADMIN_GRIFO.
        /// (Usado en secciones de conductores y abastecimiento)
        /// </summary>
        public static void ExigirRolAdminOGrifo()
        {
            ExigirSesion();
            if (!(EsAdminOSupervisor() || EsAdminGrifo()))
                RedirigirSinPermiso();
        }

        // ── Headers de seguridad HTTP ──────────────────────────────────────

        /// <summary>
        /// Evita que el navegador o un proxy guarden en caché la página protegida actual.
        /// Llamar desde Page_Load de cada página protegida. Las cabeceras de seguridad
        /// (anti-clickjacking, nosniff, Referrer-Policy, HSTS) se envían a todo el sitio
        /// desde Web.config / Web.Release.config (system.webServer/httpProtocol), no desde aquí.
        /// </summary>
        public static void AgregarHeadersSeguridad()
        {
            var response = HttpContext.Current?.Response;
            if (response == null) return;

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

        /// <summary>
        /// Redirige y TERMINA la ejecución de la página (EndResponse = true).
        /// Con EndResponse = false + CompleteRequest la página seguía ejecutando
        /// Page_Load, los eventos de postback y el render, filtrando datos y
        /// permitiendo acciones a usuarios sin permiso.
        /// </summary>
        /// <summary>
        /// Hay sesión pero el rol no tiene acceso: vuelve a la página de inicio de su rol en vez
        /// de ir a Login.aspx?error=sesion (que cierra la sesión). Si el rol no se reconoce, o
        /// su inicio es la misma página que lo rechaza (evita bucles), sí va al login.
        /// </summary>
        private static void RedirigirSinPermiso()
        {
            var context = HttpContext.Current;
            if (context == null) return;

            string inicio = RolesHelper.UrlInicioSegunRol(ObtenerRol());
            bool mismaPagina = inicio != null && string.Equals(
                VirtualPathUtility.ToAbsolute(inicio), context.Request.Path, StringComparison.OrdinalIgnoreCase);

            Redirigir(inicio == null || mismaPagina ? "~/Views/Login.aspx?error=sesion" : inicio);
        }

        private static void Redirigir(string url)
        {
            var context = HttpContext.Current;
            if (context == null) return;
            context.Response.Redirect(url, true);
        }
    }
}
