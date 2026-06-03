using System;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class ReporteResultado : PaginaBase
    {

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                if (Session["DatosReporte"] != null)
                {
                    gvReporteVista.DataSource = Session["DatosReporte"];
                    gvReporteVista.DataBind();
                }
                else
                {
                    // Opcional: puedes mostrar un mensaje bonito si no hay datos
                    lblMensaje.Text = "No hay datos para mostrar.";
                }
            }
        }



    }
}