using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Operacion;

namespace WebSGV
{
    /// <summary>
    /// Receptor de los informes de la Content-Security-Policy (E4-5; <c>report-uri</c> del Web.config; también acepta
    /// el formato de la Reporting API por si algún navegador lo usa). Anónimo a propósito: lo llama el navegador, también sin sesión. No implementa
    /// IRequiresSessionState (Global.asax no lo toca y no crea sesiones). La lógica y los límites están en
    /// <see cref="InformeCsp"/>; responde 204 (405 si no es POST, 413 si pasa de 8 KB) y nunca lanza.
    /// </summary>
    public class CspInforme : IHttpHandler
    {
        public bool IsReusable => true;

        public void ProcessRequest(HttpContext ctx)
        {
            HttpResponse resp = ctx.Response;
            resp.Cache.SetCacheability(HttpCacheability.NoCache);
            resp.Cache.SetNoStore();
            resp.TrySkipIisCustomErrors = true;

            int codigo = 204;
            try
            {
                HttpRequest req = ctx.Request;
                long largo = req.Headers["Content-Length"] == null ? -1 : req.ContentLength;
                codigo = InformeCsp.Procesar(req.HttpMethod, req.ContentType, largo,
                    InformeCsp.EsMetodoPermitido(req.HttpMethod) && largo <= InformeCsp.BytesMaximos ? req.GetBufferlessInputStream() : null,
                    IpClienteHelper.Obtener(req), LimiteInformesCsp.Sitio,
                    v => LogSGV.Advertencia("CSP ({Politica}): {Directiva} bloqueó {Origen} en {Ruta}", v.Politica, v.Directiva, v.Origen, v.Ruta));
            }
            catch
            {
                codigo = 204;
            }

            resp.StatusCode = codigo;
            if (codigo == 405) resp.AppendHeader("Allow", "POST");
        }
    }
}
