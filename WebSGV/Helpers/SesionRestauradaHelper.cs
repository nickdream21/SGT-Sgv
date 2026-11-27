using System;
using System.Globalization;
using System.Web;
using System.Web.SessionState;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Página restaurada con "Atrás" (bfcache) después de cerrar sesión: Chrome Android muestra la copia en memoria
    /// aunque la respuesta lleve no-store, con el nombre y los datos del usuario. Las páginas con sesión llevan en el
    /// &lt;html&gt; el id del usuario y las dos rutas (sin datos personales); <c>Scripts/paginas/SesionRestaurada.js</c>
    /// oculta la página al salir, y al volver pregunta a <c>Views/SesionActiva.ashx</c> si la sesión sigue siendo la
    /// misma: 204 la muestra, 401 (sin sesión) lleva al Login con <c>?error=sesion</c> y cualquier otra cosa al Login sin
    /// parámetro (con sesión viva, Login lleva al inicio del rol).
    /// </summary>
    public static class SesionRestauradaHelper
    {
        /// <summary>Manejador que responde si la sesión sigue viva.</summary>
        public const string RutaComprobacion = "~/Views/SesionActiva.ashx";

        /// <summary>Página a la que se va si no.</summary>
        public const string RutaLogin = "~/Views/Login.aspx";

        /// <summary>
        /// 204 si hay sesión y es del usuario que mostraba la página (<paramref name="idPagina"/>, el
        /// <c>data-sgv-usuario</c>); 401 si no hay sesión; 409 si hay sesión pero de otro usuario (o el id no llegó o no
        /// es un número). Con 401 el script va a Login?error=sesion; con 409 a Login sin parámetro, que con la sesión
        /// viva lleva al inicio de su rol sin pasar por el rompe-bucles de ?error=sesion (que la cerraría).
        /// </summary>
        public static int CodigoRespuesta(int? idSesion, string idPagina)
        {
            if (!idSesion.HasValue)
                return 401;
            return string.Equals((idPagina ?? "").Trim(), idSesion.Value.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                ? 204 : 409;
        }

        /// <summary>
        /// Atributos para el &lt;html&gt; de una página con sesión (con espacio inicial), o "" sin sesión: así el script
        /// no hace nada en Login y demás páginas públicas.
        /// </summary>
        public static string AtributosHtml(int? idUsuario, Func<string, string> aAbsoluta)
        {
            if (!idUsuario.HasValue || aAbsoluta == null)
                return "";
            return " data-sgv-usuario=\"" + idUsuario.Value.ToString(CultureInfo.InvariantCulture) + "\""
                + " data-sgv-sesion=\"" + HttpUtility.HtmlAttributeEncode(aAbsoluta(RutaComprobacion)) + "\""
                + " data-sgv-login=\"" + HttpUtility.HtmlAttributeEncode(aAbsoluta(RutaLogin)) + "\"";
        }

        /// <summary>Atributos para la sesión de la petición (rutas absolutas del sitio, con su directorio virtual).</summary>
        public static string AtributosHtml(HttpSessionState sesion) =>
            AtributosHtml(SesionHelper.IdUsuario(sesion), VirtualPathUtility.ToAbsolute);
    }
}
