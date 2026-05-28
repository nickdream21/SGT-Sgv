using System;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class LiquidacionesAprobadasContabilidad : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("LIQUIDACIONES_CONTABILIDAD");
            SecurityHelper.AgregarHeadersSeguridad();
        }
    }
}
