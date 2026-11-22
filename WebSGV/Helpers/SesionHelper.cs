using System;
using System.Web;
using System.Web.SessionState;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Paso intermedio del login con segundo factor (E4-14): la contraseña fue correcta y falta el código.
    /// Lo mínimo para terminar el login después: el id (no es <see cref="SesionHelper.ClaveUsuarioId"/>), el
    /// nombre de usuario (etiqueta de la app y auditoría), el instante UTC y la marca de contraseña fuera de
    /// la política (no se puede recalcular: hace falta la contraseña escrita). Sin rol: rol, nombre y
    /// conductor se vuelven a leer de la BD al aceptar el código (<see cref="InicioSesionHelper.CargarDatosLogin"/>).
    /// </summary>
    [Serializable]
    public sealed class SegundoFactorPendiente
    {
        public int IdUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public DateTime InicioUtc { get; set; }
        public bool ContrasenaFueraDePolitica { get; set; }
        /// <summary>No tiene el factor y el interruptor lo exige: primero lo da de alta.</summary>
        public bool ExigirAlta { get; set; }

        /// <summary>
        /// True durante <see cref="WebSGV.Services.Usuarios.SegundoFactorReglas.VidaPendiente"/> desde <see cref="InicioUtc"/>
        /// (<see cref="WebSGV.Services.Usuarios.SegundoFactorReglas.VidaPendienteAlta"/> si debe dar de alta el factor).
        /// </summary>
        public bool Vigente(DateTime ahoraUtc) =>
            IdUsuario > 0 && WebSGV.Services.Usuarios.SegundoFactorReglas.PendienteVigente(InicioUtc, ahoraUtc, ExigirAlta);
    }

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

        /// <summary>
        /// Clave de la hora (UTC) en que se inició la sesión: Global.asax la cierra al pasar
        /// <see cref="SesionVigenteHelper.DuracionMaxima"/> aunque haya actividad (E4-12).
        /// </summary>
        public const string ClaveInicioSesionUtc = "InicioSesionUtc";

        /// <summary>
        /// Clave de la hora (UTC) de la última contraseña correcta del administrador en Gestión de usuarios
        /// (<see cref="WebSGV.Services.Usuarios.AdminUsuariosReglas.ReautenticacionVigente"/>, E4-12).
        /// </summary>
        public const string ClaveReautenticacionAdminUtc = "ReautenticacionAdminUtc";

        // ── Escritura ──────────────────────────────────────────────────────

        /// <summary>
        /// Carga en la sesión la identidad del usuario recién autenticado: el id como entero, el
        /// rol y el nombre tal como vienen de la BD, y el id del conductor solo si el rol es
        /// conductor y lo tiene (si no, la clave se quita).
        /// </summary>
        public static void Iniciar(HttpSessionState sesion, int idUsuario, string rol, string nombre, int? idConductor = null)
        {
            sesion.Remove(ClaveSegundoFactorPendiente);
            sesion[ClaveUsuarioId] = idUsuario;
            sesion[ClaveRol] = rol;
            sesion[ClaveNombre] = nombre;
            if (idConductor.HasValue && RolesHelper.EsRolConductor(rol))
                sesion[ClaveIdConductor] = idConductor.Value;
            else
                sesion.Remove(ClaveIdConductor);
            sesion[ClaveInicioSesionUtc] = DateTime.UtcNow;
            sesion.Remove(ClaveReautenticacionAdminUtc);
        }

        /// <summary>
        /// Clave del paso intermedio del segundo factor (E4-14): contraseña correcta, falta el código. Guarda un
        /// <see cref="SegundoFactorPendiente"/>, nunca el rol ni el id en <see cref="ClaveUsuarioId"/>: para
        /// <see cref="HaySesion"/>, <c>SecurityHelper</c>, Site.Master y Global.asax no hay sesión.
        /// </summary>
        public const string ClaveSegundoFactorPendiente = "SegundoFactorPendiente";

        /// <summary>
        /// Deja la sesión (recién creada por el login, sin identidad) en el paso del segundo factor. Quita
        /// cualquier identidad que tuviera, por si acaso.
        /// </summary>
        public static void IniciarSegundoFactorPendiente(HttpSessionState sesion, SegundoFactorPendiente pendiente)
        {
            if (sesion == null) throw new ArgumentNullException(nameof(sesion));
            if (pendiente == null) throw new ArgumentNullException(nameof(pendiente));
            sesion.Remove(ClaveUsuarioId);
            sesion.Remove(ClaveRol);
            sesion.Remove(ClaveNombre);
            sesion.Remove(ClaveIdConductor);
            sesion.Remove(ClaveInicioSesionUtc);
            sesion.Remove(ClaveReautenticacionAdminUtc);
            sesion[ClaveSegundoFactorPendiente] = pendiente;
        }

        /// <summary>
        /// Estado pendiente del segundo factor si sigue vigente (<see cref="SegundoFactorPendiente.Vigente"/>), o
        /// null. Uno vencido se quita de la sesión. Con una sesión ya iniciada siempre null.
        /// </summary>
        public static SegundoFactorPendiente ObtenerSegundoFactorPendiente(HttpSessionState sesion, DateTime ahoraUtc)
        {
            if (sesion == null || HaySesion(sesion)) return null;
            var p = sesion[ClaveSegundoFactorPendiente] as SegundoFactorPendiente;
            if (p == null) return null;
            if (!p.Vigente(ahoraUtc))
            {
                sesion.Remove(ClaveSegundoFactorPendiente);
                return null;
            }
            return p;
        }

        /// <summary>Quita el estado pendiente del segundo factor.</summary>
        public static void QuitarSegundoFactorPendiente(HttpSessionState sesion) =>
            sesion?.Remove(ClaveSegundoFactorPendiente);

        /// <summary>Guarda la hora (UTC) de una contraseña del administrador correcta en Gestión de usuarios.</summary>
        public static void MarcarReautenticacionAdmin(HttpSessionState sesion, DateTime ahoraUtc)
        {
            if (sesion != null)
                sesion[ClaveReautenticacionAdminUtc] = ahoraUtc;
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

        /// <summary>Hora (UTC) en que se inició la sesión, o null (sesión sin ese dato o sin sesión).</summary>
        public static DateTime? InicioSesionUtc(HttpSessionState sesion) =>
            sesion?[ClaveInicioSesionUtc] as DateTime?;

        /// <summary>Hora (UTC) de la última reautenticación correcta en Gestión de usuarios, o null.</summary>
        public static DateTime? ReautenticacionAdminUtc(HttpSessionState sesion) =>
            sesion?[ClaveReautenticacionAdminUtc] as DateTime?;

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
