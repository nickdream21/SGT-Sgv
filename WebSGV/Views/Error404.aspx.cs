using System;
using System.Web.UI;

namespace WebSGV.Views
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5368:Set ViewStateUserKey For Classes Derived From Page",
        Justification = "Página pública sin sesión: como en Site.Master, ViewStateUserKey se omite porque el SessionID puede cambiar entre GET y POST.")]
    public partial class Error404 : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.StatusCode = 404;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}