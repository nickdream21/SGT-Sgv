using System;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class Inicio : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();
        }
    }
}