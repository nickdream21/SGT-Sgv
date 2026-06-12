using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using WebSGV.Helpers;

namespace WebSGV
{
    public class Global : HttpApplication
    {
        void Application_Start(object sender, EventArgs e)
        {
            // Logging centralizado (Serilog): a la ventana Output (local) y a
            // ~/App_Data/logs/sgv-AAAAMMDD.log (producción). MapPath aquí para no
            // acoplar el helper a System.Web.
            LogSGV.Inicializar(System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/logs"));
            LogSGV.Info("Aplicación SGV iniciada");

            // Código que se ejecuta al iniciar la aplicación
            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // QuestPDF: licencia Community (empresa con ingresos < 1M USD anuales)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Crear tabla de auditoría si no existe
            AuditoriaHelper.CrearTablaAuditoriaSiNoExiste();
        }

        void Application_End(object sender, EventArgs e)
        {
            // Vaciar y cerrar los sinks de log al apagar la aplicación.
            LogSGV.Cerrar();
        }

        void Application_Error(object sender, EventArgs e)
        {
            Exception ex = Server.GetLastError();
            if (ex != null)
            {
                // Desenvolver para tener la excepción real (HttpUnhandledException la envuelve)
                Exception real = ex;
                while ((real is HttpUnhandledException) && real.InnerException != null)
                    real = real.InnerException;

                // Si es un error de ViewState, redirigir al login
                if (real is HttpException &&
                    (real.Message.Contains("ViewState") || real.Message.Contains("viewstate")))
                {
                    Server.ClearError();
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                    return;
                }

                // Guardar la excepción REAL en Application (estado global) para que Error.aspx
                // pueda recuperarla incluso cuando IIS ejecute Error.aspx en un sub-request donde
                // Server.GetLastError() retornaría null.
                try
                {
                    string key = "LastError_" + (HttpContext.Current?.Session?.SessionID
                                                 ?? Guid.NewGuid().ToString());
                    Application["LastError_LastKey"] = key;
                    Application[key] = real;
                }
                catch { /* nunca dejar reventar Application_Error */ }
            }
        }
    }
}