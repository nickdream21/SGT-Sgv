using System;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// Dirección antigua del PDF de la Orden de Viaje (la usaba la pantalla de firma). Ya no genera
    /// PDF: un documento firmado nunca se regenera, así que solo redirige a
    /// <c>DescargarPdfOrdenViaje.aspx</c>, que entrega el archivo guardado tras verificar su huella.
    /// Se conserva para que los enlaces viejos sigan funcionando.
    /// </summary>
    public partial class TestGenerarPdfOrdenViaje : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();

            if (!int.TryParse(Request.QueryString["id"], out int idOrdenViaje) || idOrdenViaje <= 0)
            {
                Response.Redirect("~/Views/Inicio.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            string url = "~/Views/DescargarPdfOrdenViaje.aspx?id=" + idOrdenViaje +
                         (Request.QueryString["download"] == "1" ? "&download=1" : "");
            Response.Redirect(url, false);
            Context.ApplicationInstance.CompleteRequest();
        }
    }
}
