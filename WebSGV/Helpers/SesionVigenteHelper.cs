using System;
using System.Data;
using System.Web;
using System.Web.Caching;
using System.Web.SessionState;
using System.Web.UI;

namespace WebSGV.Helpers
{
    /// <summary>Datos de acceso de un usuario tal como están en la BD (<c>sp_US_ObtenerEstadoSesion</c>).</summary>
    [Serializable]
    public sealed class EstadoSesionUsuario
    {
        /// <summary>False si el usuario ya no existe en la BD.</summary>
        public bool Existe { get; set; }
        public Guid SelloSesion { get; set; }
        public bool Activo { get; set; }
        public string Rol { get; set; }
        public int? IdConductor { get; set; }
        public bool RequiereCambioContrasena { get; set; }

        public static EstadoSesionUsuario Inexistente() => new EstadoSesionUsuario { Existe = false };
    }

    public enum ResultadoSesion
    {
        Vigente,
        Cerrada
    }

    /// <summary>Qué hace <c>Login.aspx?error=sesion</c> (<see cref="SesionVigenteHelper.DecidirLoginErrorSesion"/>).</summary>
    public enum DecisionLoginErrorSesion
    {
        /// <summary>No hay sesión: se muestra el formulario con el aviso de sesión vencida.</summary>
        MostrarFormulario,
        /// <summary>Sesión a medias (sin sello o con un rol sin inicio) o rebote repetido: se cierra y se muestra el formulario.</summary>
        LimpiarSesion,
        /// <summary>Sesión vigente: a la página de inicio de su rol, sin cerrarla.</summary>
        IrAInicio
    }

    /// <summary>
    /// Sesión vigente (auditoría de seguridad, tarea 5): en cada petición con sesión
    /// (<c>Global.Application_AcquireRequestState</c>) se compara lo que quedó en la sesión al
    /// iniciarla con los datos de acceso actuales del usuario. Desactivarlo, cambiarle el rol, el
    /// conductor vinculado o la contraseña renueva <c>Usuarios.selloSesion</c> y cierra sus sesiones
    /// abiertas. El estado se guarda en caché 60 s: los cambios hechos desde el sistema llaman a
    /// <see cref="InvalidarCache"/> y se aplican al instante; los hechos directo en la BD, en 60 s.
    /// </summary>
    public static class SesionVigenteHelper
    {
        /// <summary>Clave de sesión con el sello (<see cref="Guid"/>) leído al iniciar sesión.</summary>
        public const string ClaveSello = "SelloSesion";

        public const string MensajeSesionCerrada =
            "Tu sesión se cerró porque cambiaron tus datos de acceso. Vuelve a ingresar.";

        /// <summary>
        /// Clave de sesión (bool): entró con una contraseña que no cumple <see cref="PoliticaContrasena"/>
        /// y <c>Seguridad.ExigirPoliticaAlIngresar</c> está activo. Solo vive en la sesión (no se
        /// guarda en la BD): se suma a <c>requiereCambioContrasena</c> hasta que la cambie.
        /// </summary>
        public const string ClaveContrasenaFueraDePolitica = "ContrasenaFueraDePolitica";

        public const string MensajeCambioContrasena =
            "Debes cambiar tu contraseña antes de continuar.";

        /// <summary>WebMethod que se deja pasar aunque falte cambiar la contraseña (keepalive del conductor).</summary>
        public const string MetodoMantenerSesion = "MantenerSesionActiva";

        private const string PrefijoCache = "SGV_EstadoSesion_";
        private static readonly TimeSpan DuracionCache = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Páginas que no exigen sesión. Única lista: la usan también <c>Site.Master</c> (navbar
        /// oculto) y el control de sesión de <c>Global.asax</c>.
        /// </summary>
        private static readonly string[] RutasPublicas =
        {
            "login.aspx",
            "recuperarcontrasena.aspx",
            "restablecercontrasena.aspx",
            "verificardocumento.aspx",
            "error.aspx",
            "error404.aspx"
        };

        // ── Lógica pura (probada en SesionVigenteTests) ─────────────────────

        /// <summary>
        /// Decide si una sesión abierta sigue valiendo frente al estado actual del usuario en la BD.
        /// Se cierra si: la sesión no tiene sello (abierta antes de este control), el usuario no existe
        /// o está inactivo, el sello cambió, el rol cambió (sin distinguir mayúsculas ni espacios) o
        /// cambió el conductor vinculado (solo cuenta para el rol conductor, como en el login).
        /// </summary>
        public static ResultadoSesion EvaluarSesion(Guid? selloSesion, string rolSesion, int? idConductorSesion, EstadoSesionUsuario bd)
        {
            if (bd == null || !bd.Existe || !bd.Activo)
                return ResultadoSesion.Cerrada;
            if (!selloSesion.HasValue || selloSesion.Value != bd.SelloSesion)
                return ResultadoSesion.Cerrada;
            if (NormalizarRol(rolSesion) != NormalizarRol(bd.Rol))
                return ResultadoSesion.Cerrada;

            // El login solo guarda el id del conductor (SesionHelper.IdConductor) si el rol es conductor.
            int? idConductorBd = RolesHelper.EsRolConductor(bd.Rol) ? bd.IdConductor : null;
            if (idConductorBd != idConductorSesion)
                return ResultadoSesion.Cerrada;

            return ResultadoSesion.Vigente;
        }

        private static string NormalizarRol(string rol) => (rol ?? "").Trim().ToUpperInvariant();

        /// <summary>
        /// Duración máxima de una sesión desde que se inició, aunque haya actividad (E4-12). La inactividad
        /// la corta antes el <c>timeout</c> de 30 min de <c>sessionState</c>.
        /// </summary>
        public static readonly TimeSpan DuracionMaxima = TimeSpan.FromHours(12);

        /// <summary>Código de <c>Login.aspx?error=</c> para una sesión cerrada por <see cref="DuracionMaxima"/>.</summary>
        public const string CodigoSesionVencida = "sesion_vencida";

        public const string MensajeDuracionMaxima =
            "Tu sesión llegó a su duración máxima (12 horas). Vuelve a ingresar.";

        /// <summary>
        /// True si la sesión ya pasó <see cref="DuracionMaxima"/> desde <paramref name="inicioUtc"/>. Sin hora de
        /// inicio (sesión abierta antes de este control) también: ante la duda, se cierra, como una sesión sin
        /// sello. Un inicio en el futuro (reloj que volvió atrás) no la cierra: no hay cómo medirla mejor y la
        /// inactividad de 30 min sigue valiendo. Lógica pura (reloj inyectado).
        /// </summary>
        public static bool SuperoDuracionMaxima(DateTime? inicioUtc, DateTime ahoraUtc)
        {
            if (!inicioUtc.HasValue)
                return true;
            return ahoraUtc - inicioUtc.Value >= DuracionMaxima;
        }

        /// <summary>
        /// Clave de sesión (<see cref="DateTime"/> UTC): cuándo <c>Login.aspx?error=sesion</c> mandó por última vez
        /// una sesión vigente a su inicio. Si vuelve a llegar antes de <see cref="VentanaReboteLogin"/>, el inicio
        /// la rechazó (bucle Login ↔ página protegida) y se cierra.
        /// </summary>
        public const string ClaveReboteLogin = "LoginErrorSesionReboteUtc";

        public static readonly TimeSpan VentanaReboteLogin = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Login con <c>?error=sesion</c> (E4-8). Antes cerraba siempre la sesión; ahora una sesión vigente
        /// (usuario, sello de <see cref="ClaveSello"/> y un rol con inicio) va a su inicio sin cerrarse. Rompe-bucles:
        /// las guardas mandan aquí con la sesión abierta cuando el inicio de su rol la rechaza
        /// (<c>SecurityHelper.RedirigirSinPermiso</c> con la misma página, postback del conductor sin conductor);
        /// si vuelve antes de <see cref="VentanaReboteLogin"/> desde el último envío al inicio, se cierra
        /// (la siguiente llegada ya no tiene sesión y muestra el formulario). Lógica pura.
        /// </summary>
        public static DecisionLoginErrorSesion DecidirLoginErrorSesion(bool haySesion, bool tieneSello, string urlInicio,
            DateTime? ultimoReboteUtc, DateTime ahoraUtc)
        {
            if (!haySesion)
                return DecisionLoginErrorSesion.MostrarFormulario;
            if (!tieneSello || string.IsNullOrEmpty(urlInicio))
                return DecisionLoginErrorSesion.LimpiarSesion;
            if (ultimoReboteUtc.HasValue)
            {
                TimeSpan desde = ahoraUtc - ultimoReboteUtc.Value;
                // Un reloj que vuelve atrás (desde < 0) también cuenta como rebote: ante la duda, se cierra.
                if (desde < VentanaReboteLogin)
                    return DecisionLoginErrorSesion.LimpiarSesion;
            }
            return DecisionLoginErrorSesion.IrAInicio;
        }

        /// <summary>True si la página (nombre de archivo, p. ej. "Login.aspx") no exige sesión.</summary>
        public static bool EsRutaPublica(string nombreArchivo)
        {
            string nombre = (nombreArchivo ?? "").Trim().ToLowerInvariant();
            if (nombre.Length == 0)
                return true;
            return Array.IndexOf(RutasPublicas, nombre) >= 0;
        }

        /// <summary>
        /// True si la petición espera JSON y no una página: un PageMethod (<c>Pagina.aspx/Metodo</c>,
        /// con PathInfo) o un handler que no es una página (.ashx).
        /// </summary>
        public static bool EsLlamadaAjax(string rutaArchivo, string pathInfo, bool handlerEsPagina)
        {
            if (!handlerEsPagina)
                return true;
            return (rutaArchivo ?? "").EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty((pathInfo ?? "").Trim('/'));
        }

        public static bool EsLlamadaAjax(HttpRequest request, IHttpHandler handler) =>
            EsLlamadaAjax(request.FilePath, request.PathInfo, handler == null || handler is Page);

        /// <summary>Postback parcial de un UpdatePanel (espera la respuesta "delta" de MS Ajax).</summary>
        public static bool EsPostbackAsincrono(HttpRequest request)
        {
            string cabecera = request.Headers["X-MicrosoftAjax"];
            return cabecera != null && cabecera.IndexOf("Delta=true", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Nombre del PageMethod llamado ("" si no es un PageMethod).</summary>
        public static string NombreMetodo(string pathInfo) => (pathInfo ?? "").Trim('/');

        /// <summary>True si la sesión entró con una contraseña fuera de la política (ver <see cref="ClaveContrasenaFueraDePolitica"/>).</summary>
        public static bool ContrasenaFueraDePolitica(HttpSessionState sesion)
        {
            return sesion != null && sesion[ClaveContrasenaFueraDePolitica] is bool b && b;
        }

        public static void MarcarContrasenaFueraDePolitica(HttpSessionState sesion, bool valor)
        {
            if (sesion != null)
                sesion[ClaveContrasenaFueraDePolitica] = valor;
        }

        // ── BD y caché ─────────────────────────────────────────────────────

        /// <summary>
        /// Estado del usuario (caché 60 s). <see cref="EstadoSesionUsuario.Existe"/> = false si ya no
        /// existe; null si la BD no respondió (el llamador deja pasar la petición).
        /// </summary>
        public static EstadoSesionUsuario ObtenerEstado(int idUsuario, Guid? selloSesion)
        {
            string clave = PrefijoCache + idUsuario;
            return ResolverEstado(
                HttpRuntime.Cache[clave] as EstadoSesionUsuario,
                selloSesion,
                () =>
                {
                    try
                    {
                        return LeerEstadoDeBd(idUsuario);
                    }
                    catch (Exception ex)
                    {
                        LogSGV.Advertencia("Sesión vigente: no se pudo leer el estado del usuario {IdUsuario}; se deja pasar la petición. {Error}",
                            idUsuario, ex.Message);
                        return null;
                    }
                },
                estado => HttpRuntime.Cache.Insert(clave, estado, null, DateTime.UtcNow.Add(DuracionCache), Cache.NoSlidingExpiration));
        }

        /// <summary>
        /// Decide de dónde sale el estado (lógica pura, probada en SesionVigenteTests):
        /// - Sin caché: se lee de la BD y se guarda en caché.
        /// - En caché y con el mismo sello de la sesión (o sin sello en la sesión): el de la caché.
        /// - En caché pero con otro sello: se vuelve a leer de la BD una vez y se refresca la caché. Así una
        ///   sesión recién abierta o re-sellada no se cierra por un valor viejo que otra petición guardó
        ///   justo antes del cambio (carrera entre la lectura y el UPDATE).
        /// Si la BD no responde (<paramref name="leerBd"/> devuelve null) se devuelve null: se deja pasar.
        /// </summary>
        public static EstadoSesionUsuario ResolverEstado(EstadoSesionUsuario enCache, Guid? selloSesion,
            Func<EstadoSesionUsuario> leerBd, Action<EstadoSesionUsuario> guardarEnCache)
        {
            if (enCache != null)
            {
                bool selloDistinto = enCache.Existe && selloSesion.HasValue && selloSesion.Value != enCache.SelloSesion;
                if (!selloDistinto)
                    return enCache;
            }

            EstadoSesionUsuario estado = leerBd();
            if (estado != null)
                guardarEnCache(estado);
            return estado;
        }

        /// <summary>Lee el estado directo de la BD, sin caché.</summary>
        public static EstadoSesionUsuario LeerEstadoDeBd(int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_US_ObtenerEstadoSesion",
                DbHelper.Param("@idUsuario", idUsuario));
            if (dt.Rows.Count == 0)
                return EstadoSesionUsuario.Inexistente();

            DataRow r = dt.Rows[0];
            return new EstadoSesionUsuario
            {
                Existe = true,
                SelloSesion = (Guid)r["selloSesion"],
                Activo = r["activo"] != DBNull.Value && Convert.ToBoolean(r["activo"]),
                Rol = Convert.ToString(r["rol"]),
                IdConductor = r["idConductor"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["idConductor"]),
                RequiereCambioContrasena = r["requiereCambioContrasena"] != DBNull.Value && Convert.ToBoolean(r["requiereCambioContrasena"])
            };
        }

        /// <summary>Descarta el estado en caché: llamarlo después de cambiar los datos de acceso del usuario.</summary>
        public static void InvalidarCache(int idUsuario)
        {
            try { HttpRuntime.Cache.Remove(PrefijoCache + idUsuario); }
            catch { /* sin caché (pruebas): nada que invalidar */ }
        }

        /// <summary>Sello guardado en la sesión, o null.</summary>
        public static Guid? SelloDeSesion(object valor)
        {
            if (valor is Guid g) return g;
            if (valor != null && Guid.TryParse(valor.ToString(), out Guid p)) return p;
            return null;
        }

        /// <summary>Entero guardado en la sesión, o null.</summary>
        public static int? EnteroDeSesion(object valor)
        {
            if (valor == null) return null;
            if (valor is int i) return i;
            return int.TryParse(valor.ToString(), out int p) ? p : (int?)null;
        }
    }
}
