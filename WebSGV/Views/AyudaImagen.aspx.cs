using System;
using WebSGV.Helpers;
using WebSGV.Services.Ayuda;

namespace WebSGV.Views
{
    /// <summary>
    /// Imagen de un tema de la ayuda (ContenidoAyuda/img/&lt;tema&gt;/NN.webp, que no se sirve por URL directa):
    /// con sesión y solo si el rol puede ver el tema (como Ayuda.aspx). Cualquier otro caso: 404 sin detalle.
    /// </summary>
    public partial class AyudaImagen : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();
            SecurityHelper.AgregarHeadersSeguridad();

            string tema = Request.QueryString["tema"];
            string archivo = Request.QueryString["img"];
            string ruta = null;
            try
            {
                var catalogo = AyudaCatalogo.Cargar(Server.MapPath("~/ContenidoAyuda"));
                if (!catalogo.PuedeVer(tema, RolesHelper.ObtenerRolActual(), Ayuda.InterruptorEncendido)
                    || !catalogo.TryRutaImagen(tema, archivo, out ruta))
                    ruta = null;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Ayuda: no se pudo leer el catálogo de temas para una imagen");
                ruta = null;
            }

            if (ruta == null)
            {
                Response.Clear();
                Response.StatusCode = 404;
                Response.TrySkipIisCustomErrors = true;
                Response.ContentType = "text/plain";
                Response.Write("No disponible.");
                Response.End();
                return;
            }

            Response.Clear();
            Response.ContentType = "image/webp";
            Response.TransmitFile(ruta);
            Response.End();
        }
    }
}
