using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;

namespace WebSGV
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Código que se ejecuta al iniciar la aplicación
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                // Log detallado del error para diagnóstico
                System.Diagnostics.Debug.WriteLine("========== APPLICATION ERROR ==========");
                System.Diagnostics.Debug.WriteLine($"URL: {Request?.Url}");
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Tipo: {ex.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner: {ex.InnerException.Message}");
                    System.Diagnostics.Debug.WriteLine($"Inner Stack: {ex.InnerException.StackTrace}");
                }

                System.Diagnostics.Debug.WriteLine("=======================================");

                // Si es un error de ViewState, redirigir al login
                if (ex is HttpException httpEx && (
                    ex.Message.Contains("ViewState") ||
                    ex.Message.Contains("viewstate")))
                {
                    Server.ClearError();
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                }
            }
        }
    }
}