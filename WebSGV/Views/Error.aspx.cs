using System;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class Error : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // Registrar el error en el log del servidor
            Exception lastError = Server.GetLastError();
            if (lastError != null)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR 500: {lastError.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {lastError.StackTrace}");
                Server.ClearError();
            }

            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}