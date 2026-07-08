using System;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class Error : Page
    {
        public string ErrorDetalle { get; private set; } = "";
        public string ErrorTipo { get; private set; } = "";
        public string ErrorStack { get; private set; } = "";

        protected void Page_Load(object sender, EventArgs e)
        {
            Exception lastError = null;

            // 1) Buscar primero en HttpContext.Items (mismo request)
            if (Context.Items["LastErrorReal"] is Exception fromItems)
                lastError = fromItems;

            // 2) Server.GetLastError (puede ser null en sub-request de IIS httpErrors)
            if (lastError == null)
                lastError = Server.GetLastError();

            // 3) Application state (guardado por Global.asax cuando IIS hace sub-request)
            if (lastError == null)
            {
                try
                {
                    string key = Application["LastError_LastKey"] as string;
                    if (!string.IsNullOrEmpty(key))
                    {
                        lastError = Application[key] as Exception;
                        if (lastError != null)
                        {
                            // limpiar para no mostrar el mismo error en navegaciones futuras
                            Application.Remove(key);
                            Application.Remove("LastError_LastKey");
                        }
                    }
                }
                catch
                {
                    // Recuperación best-effort del último error guardado: si falla, se traga
                    // a propósito (esta ES la página de error; no debe lanzar ni encadenar logging).
                }
            }

            if (lastError != null)
            {
                // Desenvolver capas
                Exception real = lastError;
                while ((real is System.Web.HttpUnhandledException) && real.InnerException != null)
                    real = real.InnerException;

                ErrorTipo = System.Web.HttpUtility.HtmlEncode(real.GetType().FullName);
                ErrorDetalle = System.Web.HttpUtility.HtmlEncode(real.Message ?? "");
                ErrorStack = System.Web.HttpUtility.HtmlEncode(real.StackTrace ?? "");

                System.Diagnostics.Debug.WriteLine($"❌ ERROR 500 mostrado en Error.aspx: {real.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {real.StackTrace}");
                Server.ClearError();
            }

            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}