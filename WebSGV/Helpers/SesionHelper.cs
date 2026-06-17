using System;
using System.Web.SessionState;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Punto único para las claves de <see cref="System.Web.SessionState.HttpSessionState"/>
    /// del sistema (autenticación basada en sesión) y accesores tipados que replican el
    /// patrón repetido en los code-behind.
    ///
    /// Centraliza los literales <c>"UsuarioID"</c>/<c>"Rol"</c>/<c>"Nombre"</c> (antes
    /// dispersos como cadenas mágicas) para evitar errores de tipeo y dar una sola fuente
    /// de verdad. Los accesores preservan la semántica exacta del código original
    /// (sin null-check del propio <c>Session</c>, igual que antes).
    /// </summary>
    public static class SesionHelper
    {
        /// <summary>Clave del ID de usuario (presencia = autenticado).</summary>
        public const string ClaveUsuarioId = "UsuarioID";

        /// <summary>Clave del rol (se compara en mayúsculas).</summary>
        public const string ClaveRol = "Rol";

        /// <summary>Clave del nombre para mostrar.</summary>
        public const string ClaveNombre = "Nombre";

        /// <summary>
        /// ID de usuario en sesión, o 0 si no está. Equivale a
        /// <c>session["UsuarioID"] != null ? Convert.ToInt32(session["UsuarioID"]) : 0</c>.
        /// </summary>
        public static int ObtenerUsuarioId(HttpSessionState session) =>
            session[ClaveUsuarioId] != null ? Convert.ToInt32(session[ClaveUsuarioId]) : 0;

        /// <summary>Nombre en sesión, o cadena vacía. Equivale a <c>session["Nombre"] as string ?? ""</c>.</summary>
        public static string ObtenerNombre(HttpSessionState session) =>
            session[ClaveNombre] as string ?? "";

        /// <summary>
        /// Rol en sesión normalizado a mayúsculas, o cadena vacía. Equivale a
        /// <c>(session["Rol"] as string ?? "").ToUpperInvariant()</c>.
        /// </summary>
        public static string ObtenerRolNormalizado(HttpSessionState session) =>
            (session[ClaveRol] as string ?? "").ToUpperInvariant();
    }
}
