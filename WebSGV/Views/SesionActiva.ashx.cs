using System;
using System.Web;
using System.Web.SessionState;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// ¿Sigue viva la sesión de esta página? Lo pregunta <c>Scripts/paginas/SesionRestaurada.js</c> cuando el navegador
    /// restaura una página con "Atrás" (bfcache), sin volver a enviar formularios. Solo GET (HEAD), sin cuerpo:
    /// 204 si hay sesión y es del usuario <c>?u=</c> que mostraba la página, 401 si no hay sesión y 409 si es de otro
    /// usuario (<see cref="SesionRestauradaHelper.CodigoRespuesta"/>).
    /// No requiere rol. Sesión de solo lectura: no bloquea las peticiones de la página. Si la sesión se cerró por
    /// Global.asax (duración máxima, datos de acceso cambiados) responde él con 401; con cambio de contraseña
    /// pendiente, 403: el script va al Login sin parámetro, que con sesión vigente lleva a su inicio.
    /// </summary>
    public class SesionActiva : IHttpHandler, IReadOnlySessionState
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext ctx)
        {
            ctx.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            ctx.Response.Cache.SetNoStore();
            Atender(ctx);
        }

        /// <summary>El código de respuesta (las pruebas lo llaman con una petición falsa; las cabeceras van en ProcessRequest).</summary>
        public static void Atender(HttpContext ctx)
        {
            HttpResponse resp = ctx.Response;
            resp.TrySkipIisCustomErrors = true;

            if (!Salud.EsMetodoPermitido(ctx.Request.HttpMethod))
            {
                resp.StatusCode = 405;
                resp.AppendHeader("Allow", "GET, HEAD");
                return;
            }

            // Sin validar: un "?u=<x>" no debe dar 500 (HttpRequestValidationException); solo se compara con el id.
            string idPagina = ctx.Request.Unvalidated.QueryString["u"];
            resp.StatusCode = SesionRestauradaHelper.CodigoRespuesta(SesionHelper.IdUsuario(ctx.Session), idPagina);
        }
    }
}
