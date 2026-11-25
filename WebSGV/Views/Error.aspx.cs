using System;
using System.Web.UI;

namespace WebSGV.Views
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5368:Set ViewStateUserKey For Classes Derived From Page",
        Justification = "Página pública sin sesión: como en Site.Master, ViewStateUserKey se omite porque el SessionID puede cambiar entre GET y POST.")]
    public partial class Error : Page
    {
        public string ErrorDetalle { get; private set; } = "";
        public string ErrorTipo { get; private set; } = "";
        public string ErrorStack { get; private set; } = "";

        protected void Page_Load(object sender, EventArgs e)
        {
            // Solo hay excepción si Error.aspx se ejecuta en la misma petición que falló; si llega
            // por el 302 de customErrors (Release) o por httpErrors de IIS es null y se muestra
            // el mensaje genérico. El registro en el log lo hace Global.asax.
            Exception lastError = Server.GetLastError();

            if (lastError != null)
            {
                Exception real = WebSGV.Helpers.ErrorAplicacionHelper.Desenvolver(lastError);

                // El detalle técnico (tipo, mensaje SQL, stack) solo se muestra con
                // compilation debug="true" (desarrollo). El registro en el log lo hace
                // Global.asax (Application_Error), una sola vez.
                if (Context.IsDebuggingEnabled)
                {
                    ErrorTipo = System.Web.HttpUtility.HtmlEncode(real.GetType().FullName);
                    ErrorDetalle = System.Web.HttpUtility.HtmlEncode(real.Message ?? "");
                    ErrorStack = System.Web.HttpUtility.HtmlEncode(real.StackTrace ?? "");
                }

                Server.ClearError();
            }

            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}