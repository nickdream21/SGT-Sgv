using System.IO;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// URL de archivos .js/.css propios con la versión del archivo (?v=fecha de modificación), para que
    /// el navegador descargue la versión nueva tras cada cambio en lugar de usar la de su caché.
    /// Uso en .aspx: &lt;script src="&lt;%= RecursoHelper.Url("~/Scripts/paginas/Dashboard.js") %&gt;"&gt;&lt;/script&gt;
    /// </summary>
    public static class RecursoHelper
    {
        public static string Url(string rutaVirtual)
        {
            string url = VirtualPathUtility.ToAbsolute(rutaVirtual);
            string fisica = HttpContext.Current?.Server.MapPath(rutaVirtual);
            if (fisica == null || !File.Exists(fisica)) return url;
            return url + "?v=" + File.GetLastWriteTimeUtc(fisica).ToString("yyyyMMddHHmmss");
        }
    }
}
