using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class ReporteResultado : System.Web.UI.Page
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