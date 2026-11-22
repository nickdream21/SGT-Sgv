using System.Web;
using System.Web.SessionState;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Único punto de lectura y escritura de las claves de sesión de autenticación
    /// (<c>UsuarioID</c>, <c>Rol</c>, <c>Nombre</c> e <c>IdConductor</c>). Ningún otro archivo debe indexar la sesión
    /// con esas claves (prueba <c>CodigoFuenteLimpiezaTests.ClavesDeSesion_SoloEnSesionHelper</c>).
    ///
    /// Auditoría de seguridad, tarea 8D-2: antes Login guardaba el id dos veces ("UsuarioID" como
    /// texto e "IdUsuario" como entero) y cada lector usaba una u otra con conversiones distintas.
    /// Ahora hay una sola clave, <c>UsuarioID</c>, guardada como <c>int</c>. Los accesores no
    /// lanzan: toleran una sesión nula y valores con otro tipo (una sesión antigua con texto).
    /// </summary>
    public static class SesionHelper
    {
        /// <summary>Clave del ID de usuario (entero; presencia válida = autenticado).</summary>
        public const string ClaveUsuarioId = "UsuarioID";

        /// <summary>Clave del rol (se compara normalizado: sin espacios y en mayúsculas).</summary>
        public const string ClaveRol = "Rol";

        /// <summary>Clave del nombre para mostrar.</summary>
        public const string ClaveNombre = "Nombre";

        /// <summary>
        /// Clave del id del conductor vinculado (entero). Solo existe si el rol es conductor y el
        /// usuario tiene conductor en la BD; Global.asax la compara con la BD (SesionVigenteHelper).
        /// </summary>
        public const string ClaveIdConductor = "IdConductor";

        // ── Escritura ──────────────────────────────────────────────────────

        /// <summary>
        /// Carga en la sesión la identidad del usuario recién autenticado: el id como entero, el
        /// rol y el nombre tal como vienen de la BD, y el id del conductor solo si el rol es
        /// conductor y lo tiene (si no, la clave se quita).
        /// </summary>
        public static void Iniciar(HttpSessionState sesion, int idUsuario, string rol, string nombre, int? idConductor = null)
        {
            sesion[ClaveUsuarioId] = idUsuario;
            sesion[ClaveRol] = rol;
            sesion[ClaveNombre] = nombre;
            if (idConductor.HasValue && RolesHelper.EsRolConductor(rol))
                sesion[ClaveIdConductor] = idConductor.Value;
            else
                sesion.Remove(ClaveIdConductor);
        }

        // ── Lectura ────────────────────────────────────────────────────────

        /// <summary>ID del usuario en sesión, o null si no hay sesión o el valor no es un entero.</summary>
        public static int? IdUsuario(HttpSessionState sesion) =>
            sesion == null ? null : SesionVigenteHelper.EnteroDeSesion(sesion[ClaveUsuarioId]);

        /// <summary>Rol en sesión sin espacios y en mayúsculas; cadena vacía si no hay.</summary>
        public static string Rol(HttpSessionState sesion) =>
            (sesion?[ClaveRol]?.ToString() ?? "").Trim().ToUpperInvariant();

        /// <summary>Nombre para mostrar en sesión; cadena vacía si no hay.</summary>
        public static string Nombre(HttpSessionState sesion) =>
            sesion?[ClaveNombre] as string ?? "";

        /// <summary>Id del conductor vinculado a la sesión, o null si no hay o no es un entero.</summary>
        public static int? IdConductor(HttpSessionState sesion) =>
            sesion == null ? null : SesionVigenteHelper.EnteroDeSesion(sesion[ClaveIdConductor]);

        /// <summary>True si la sesión tiene un ID de usuario entero válido.</summary>
        public static bool HaySesion(HttpSessionState sesion) => IdUsuario(sesion).HasValue;

        // ── Atajos sobre la petición actual ────────────────────────────────

        private static HttpSessionState SesionActual => HttpContext.Current?.Session;

        /// <summary>ID del usuario de la petición actual, o null.</summary>
        public static int? IdUsuarioActual => IdUsuario(SesionActual);

        /// <summary>Rol normalizado de la petición actual, o cadena vacía.</summary>
        public static string RolActual => Rol(SesionActual);

        /// <summary>Nombre de la petición actual, o cadena vacía.</summary>
        public static string NombreActual => Nombre(SesionActual);

        /// <summary>Id del conductor vinculado a la petición actual, o null.</summary>
        public static int? IdConductorActual => IdConductor(SesionActual);

        /// <summary>True si la petición actual tiene sesión autenticada.</summary>
        public static bool HaySesionActual => HaySesion(SesionActual);

        // ── Firmas anteriores (envoltorios) ────────────────────────────────

        /// <summary>ID de usuario en sesión, o 0 si no está.</summary>
        public static int ObtenerUsuarioId(HttpSessionState session) => IdUsuario(session) ?? 0;

        /// <summary>Nombre en sesión, o cadena vacía.</summary>
        public static string ObtenerNombre(HttpSessionState session) => Nombre(session);

        /// <summary>Rol en sesión normalizado a mayúsculas, o cadena vacía.</summary>
        public static string ObtenerRolNormalizado(HttpSessionState session) => Rol(session);
    }
}
