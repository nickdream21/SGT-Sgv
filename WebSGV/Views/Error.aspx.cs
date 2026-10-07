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

            // 3) Application state (guardado por Global.asax cuando IIS hace sub-request).
            //    Solo el error de ESTA sesión, nunca el de otro usuario.
            if (lastError == null)
            {
                try
                {
                    string sessionId = Context.Session?.SessionID;
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        string key = "LastError_" + sessionId;
                        lastError = Application[key] as Exception;
                        if (lastError != null)
                            Application.Remove(key); // no mostrar el mismo error en navegaciones futuras
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

                // El detalle técnico (tipo, mensaje SQL, stack) solo se muestra con
                // compilation debug="true" (desarrollo). En Release se registra en el log.
                if (Context.IsDebuggingEnabled)
                {
                    ErrorTipo = System.Web.HttpUtility.HtmlEncode(real.GetType().FullName);
                    ErrorDetalle = System.Web.HttpUtility.HtmlEncode(real.Message ?? "");
                    ErrorStack = System.Web.HttpUtility.HtmlEncode(real.StackTrace ?? "");
                }
                else
                {
                    WebSGV.Helpers.LogSGV.Error(real, "Error no controlado mostrado en Error.aspx");
                }

                System.Diagnostics.Debug.WriteLine($"❌ ERROR 500 mostrado en Error.aspx: {real.Message}");
                System.Diagnostics.Debug.WriteLine($"   Stack: {real.StackTrace}");
                Server.ClearError();
            }

            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}