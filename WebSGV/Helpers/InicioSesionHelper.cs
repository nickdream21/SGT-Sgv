using System;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using WebSGV.Services.Usuarios;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Datos con los que se inicia la sesión. Viajan en la caché del servidor bajo un token de un solo uso
    /// (<see cref="InicioSesionHelper.EmitirTicket"/>), nunca al navegador.
    /// </summary>
    [Serializable]
    public sealed class DatosLogin
    {
        public int IdUsuario;
        public string Rol;
        public string Nombre;
        public string NombreUsuario;
        public int? IdConductor;
        public bool RequiereCambioContrasena;
        /// <summary>La contraseña con la que entró no cumple PoliticaContrasena (solo con Seguridad.ExigirPoliticaAlIngresar).</summary>
        public bool ContrasenaFueraDePolitica;
        public Guid SelloSesion;
        /// <summary>Tiene el segundo factor activo (UsuarioSegundoFactor.activo = 1, E4-14).</summary>
        public bool SegundoFactorActivo;
        /// <summary>Ya dio el código del segundo factor en SegundoFactor.aspx: el login no lo vuelve a pedir.</summary>
        public bool SegundoFactorVerificado;
        /// <summary>Códigos de recuperación recién creados en un alta obligatoria: se muestran una vez en MiSeguridad.</summary>
        public string[] CodigosRecuperacionNuevos;
    }

    /// <summary>
    /// Regeneración del id de sesión al iniciar sesión (anti session fixation), compartida por Login y
    /// SegundoFactor (E4-14). Con sesión InProc no es fiable cambiar el SessionID dentro del mismo request,
    /// así que el login se completa en dos pasos:
    ///   1) <see cref="EmitirTicket"/>: guarda los datos en caché bajo un token aleatorio de un solo uso
    ///      (60 s), abandona la sesión, borra su cookie, deja el token en una cookie HttpOnly y redirige.
    ///   2) Login.aspx?completar=1 llega SIN cookie de sesión → ASP.NET crea un ID nuevo; ahí se consume el
    ///      token (<see cref="ConsumirTicket"/>) y se carga la sesión (o el paso del segundo factor).
    /// </summary>
    public static class InicioSesionHelper
    {
        private const string CookieTicketLogin = "SGV_LoginTicket";
        private const string PrefijoCacheTicket = "SGV_LoginTicket_";

        /// <summary>Paso 1: ticket de un solo uso + sesión nueva, y redirige a Login.aspx?completar=1.</summary>
        public static void EmitirTicket(HttpContext ctx, DatosLogin datos)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (datos == null) throw new ArgumentNullException(nameof(datos));

            string token = GenerarTokenAleatorio();
            HttpRuntime.Cache.Insert(PrefijoCacheTicket + token, datos, null,
                DateTime.UtcNow.AddSeconds(60), System.Web.Caching.Cache.NoSlidingExpiration);

            ctx.Session.Clear();
            ctx.Session.Abandon();
            ctx.Response.Cookies.Add(new HttpCookie("SGV_SessionId", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
            // Cookie de sesión (sin Expires): el vencimiento de 60 s lo controla la caché del
            // servidor. Con Expires, un celular con la hora adelantada la descartaba al llegar
            // y el login terminaba en "Tu sesión ha expirado".
            ctx.Response.Cookies.Add(new HttpCookie(CookieTicketLogin, token)
            {
                HttpOnly = true,
                Secure = ctx.Request.IsSecureConnection
            });

            ctx.Response.Redirect("~/Views/Login.aspx?completar=1", false);
            ctx.ApplicationInstance.CompleteRequest();
        }

        /// <summary>Paso 2: consume el ticket (un solo uso) y borra su cookie. Null si no hay o venció.</summary>
        public static DatosLogin ConsumirTicket(HttpContext ctx)
        {
            string token = ctx.Request.Cookies[CookieTicketLogin]?.Value;
            ctx.Response.Cookies.Add(new HttpCookie(CookieTicketLogin, "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
            return string.IsNullOrEmpty(token)
                ? null
                : HttpRuntime.Cache.Remove(PrefijoCacheTicket + token) as DatosLogin;
        }

        private static readonly LimiteFrecuencia AvisoSinTablaFactor = new LimiteFrecuencia(TimeSpan.FromMinutes(1));

        /// <summary>
        /// Expresión SQL "tiene el segundo factor activo" (BIT) para el usuario <paramref name="columnaId"/>; sin la
        /// tabla (<paramref name="conTabla"/> false), 0.
        /// </summary>
        public static string SqlFactorActivo(bool conTabla, string columnaId) =>
            conTabla
                ? @"CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.UsuarioSegundoFactor f
                                           WHERE f.idUsuario = " + columnaId + @" AND f.activo = 1)
                         THEN 1 ELSE 0 END AS BIT)"
                : "CAST(0 AS BIT)";

        /// <summary>
        /// <see cref="SegundoFactorReglas.ConsultarToleraFaltaTabla"/> con el interruptor de Web.config: una
        /// publicación sin la migración 62 sigue dejando entrar con el interruptor apagado (nadie puede tener el
        /// factor sin la tabla) y lo registra en el log como mucho una vez por minuto.
        /// </summary>
        public static T ConsultarConSegundoFactor<T>(Func<bool, T> consultar) =>
            SegundoFactorReglas.ConsultarToleraFaltaTabla(consultar, SegundoFactorReglas.ExigirSegundoFactor,
                ex => ex is SqlException sql && SegundoFactorReglas.EsFaltaTablaFactor(sql.Number, sql.Message),
                ex =>
                {
                    if (AvisoSinTablaFactor.Toca(DateTime.UtcNow))
                        LogSGV.Error(ex, "Falta la tabla dbo.UsuarioSegundoFactor (migración 62 sin aplicar): "
                            + "se entra sin segundo factor mientras Seguridad.ExigirSegundoFactor esté apagado");
                });

        /// <summary>
        /// Datos de sesión del usuario leídos de nuevo de la BD (tras el segundo factor: el estado pendiente no
        /// guarda el rol). Null si el usuario ya no existe o está inactivo.
        /// </summary>
        public static DatosLogin CargarDatosLogin(int idUsuario)
        {
            DataTable dt = ConsultarConSegundoFactor(conTabla => DbHelper.ConsultarTabla(@"
                SELECT u.idUsuario, u.nombreUsuario, u.nombre, u.rol, u.idConductor,
                       ISNULL(u.requiereCambioContrasena, 0) AS requiereCambioContrasena,
                       u.selloSesion,
                       " + SqlFactorActivo(conTabla, "u.idUsuario") + @" AS segundoFactorActivo
                FROM Usuarios u
                WHERE u.idUsuario = @id AND u.activo = 1",
                DbHelper.Param("@id", idUsuario)));
            if (dt.Rows.Count == 0)
                return null;

            DataRow r = dt.Rows[0];
            string rol = r["rol"].ToString().Trim();
            return new DatosLogin
            {
                IdUsuario = Convert.ToInt32(r["idUsuario"]),
                NombreUsuario = r["nombreUsuario"].ToString().Trim(),
                Nombre = r["nombre"].ToString().Trim(),
                Rol = rol,
                IdConductor = RolesHelper.EsRolConductor(rol) && r["idConductor"] != DBNull.Value
                    ? Convert.ToInt32(r["idConductor"]) : (int?)null,
                RequiereCambioContrasena = Convert.ToBoolean(r["requiereCambioContrasena"]),
                SelloSesion = (Guid)r["selloSesion"],
                SegundoFactorActivo = Convert.ToBoolean(r["segundoFactorActivo"])
            };
        }

        private static string GenerarTokenAleatorio()
        {
            byte[] bytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return HttpServerUtility.UrlTokenEncode(bytes);
        }
    }
}
