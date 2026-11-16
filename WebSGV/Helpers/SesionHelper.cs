using System.Web;
using System.Web.SessionState;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Único punto de lectura y escritura de las claves de sesión de autenticación
    /// (<c>UsuarioID</c>, <c>Rol</c>, <c>Nombre</c>). Ningún otro archivo debe indexar la sesión
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

        // ── Escritura ──────────────────────────────────────────────────────

        /// <summary>
        /// Carga en la sesión la identidad del usuario recién autenticado: el id como entero, el
        /// rol y el nombre tal como vienen de la BD.
        /// </summary>
        public static void Iniciar(HttpSessionState sesion, int idUsuario, string rol, string nombre)
        {
            sesion[ClaveUsuarioId] = idUsuario;
            sesion[ClaveRol] = rol;
            sesion[ClaveNombre] = nombre;
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
