using System;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class Error404 : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}