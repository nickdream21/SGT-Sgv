using System;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Ayuda;

namespace WebSGV.Views
{
    /// <summary>
    /// Ayuda dentro de la app (E5-21, M-074). Sin <c>?tema</c>: índice de los temas que ve el rol. Con
    /// <c>?tema=X</c> permitido: el fragmento de <c>ContenidoAyuda/X.html</c> con sus imágenes servidas por
    /// AyudaImagen.aspx. Tema de otro rol, borrador o inexistente: el índice con un aviso que no dice si existe.
    /// El "?" de cada pantalla abre esta página en el modal de Site.Master (SiteMaster.js) o en otra pestaña.
    /// </summary>
    public partial class Ayuda : System.Web.UI.Page
    {
        /// <summary>Tema que se muestra, o null en el índice.</summary>
        protected TemaAyuda TemaActual { get; private set; }

        /// <summary>Enlace del video del tema solo si es https (lo demás no se pinta).</summary>
        protected string UrlVideo =>
            TemaActual != null && !string.IsNullOrWhiteSpace(TemaActual.Video)
            && TemaActual.Video.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? TemaActual.Video : null;

        /// <summary>True si el appSetting indicado está en "true" (interruptor del tema).</summary>
        internal static bool InterruptorEncendido(string clave) =>
            bool.TryParse(ConfigurationManager.AppSettings[clave], out bool v) && v;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();
            SecurityHelper.AgregarHeadersSeguridad();

            string rol = RolesHelper.ObtenerRolActual();
            AyudaCatalogo catalogo;
            try
            {
                catalogo = AyudaCatalogo.Cargar(Server.MapPath("~/ContenidoAyuda"));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Ayuda: no se pudo leer el catálogo de temas");
                pnlNoDisponible.Visible = true;
                return;
            }

            string clave = Request.QueryString["tema"];
            if (!string.IsNullOrEmpty(clave))
            {
                string html = catalogo.PuedeVer(clave, rol, InterruptorEncendido) ? catalogo.LeerFragmento(clave) : null;
                if (html != null)
                {
                    TemaActual = catalogo.Tema(clave);
                    string conImagenes = AyudaCatalogo.ReescribirImagenes(html,
                        archivo => ResolveUrl("~/Views/AyudaImagen.aspx") + "?tema=" + HttpUtility.UrlEncode(clave) + "&img=" + HttpUtility.UrlEncode(archivo));
                    litFragmento.Text = AyudaCatalogo.ReescribirEnlaces(conImagenes,
                        otra => ResolveUrl("~/Views/Ayuda.aspx") + "?tema=" + HttpUtility.UrlEncode(otra));
                    phTema.Visible = true;
                    return;
                }
                pnlNoDisponible.Visible = true;
            }

            litIndice.Text = HtmlIndice(catalogo, rol);
            phIndice.Visible = true;
        }

        /// <summary>Índice por sección; títulos y secciones codificados.</summary>
        private string HtmlIndice(AyudaCatalogo catalogo, string rol)
        {
            var temas = catalogo.TemasDelRol(rol, InterruptorEncendido);
            if (temas.Count == 0)
                return "<p class=\"sgv-vacio mb-0\">Todav&#xED;a no hay temas de ayuda para su usuario.</p>";

            var sb = new StringBuilder();
            foreach (var seccion in temas.GroupBy(t => t.Seccion))
            {
                sb.Append("<h3 class=\"sgv-ayuda-seccion\">").Append(HttpUtility.HtmlEncode(seccion.Key)).Append("</h3><ul>");
                foreach (var t in seccion)
                    sb.Append("<li><a href=\"")
                      .Append(HttpUtility.HtmlAttributeEncode(ResolveUrl("~/Views/Ayuda.aspx") + "?tema=" + HttpUtility.UrlEncode(t.Clave)))
                      .Append("\">").Append(HttpUtility.HtmlEncode(t.Titulo)).Append("</a></li>");
                sb.Append("</ul>");
            }
            return sb.ToString();
        }
    }
}
