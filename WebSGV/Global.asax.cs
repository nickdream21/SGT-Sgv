using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
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

            // Recurso "jquery" del ScriptManager (validación unobtrusive). Sin MVC ni bundling:
            // el sitio es WebForms puro con URLs .aspx directas.
            BundleConfig.RegisterJQueryScriptManager();

            // QuestPDF: licencia Community (empresa con ingresos < 1M USD anuales)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Crear tabla de auditoría si no existe
            AuditoriaHelper.CrearTablaAuditoriaSiNoExiste();

            // Cargar QuestPDF en segundo plano para que el primer conductor que firme no espere.
            System.Threading.ThreadPool.QueueUserWorkItem(_ => WebSGV.Services.PdfOrdenViajeService.Precalentar());
        }

        void Application_End(object sender, EventArgs e)
        {
            // Vaciar y cerrar los sinks de log al apagar la aplicación.
            LogSGV.Cerrar();
        }

        /// <summary>
        /// Sesión vigente (auditoría de seguridad, tarea 5). Corre en cada petición con sesión, antes
        /// de la página y de los PageMethods (ScriptModule los ejecuta en PostAcquireRequestState):
        /// - Si cambiaron los datos de acceso del usuario (sello, activo, rol, conductor), cierra la
        ///   sesión: páginas → Login; PageMethods/.ashx → 401 JSON; UpdatePanel → redirección delta.
        /// - Si debe cambiar la contraseña: PageMethods/.ashx → 403 JSON (salvo el keepalive) y las
        ///   páginas sin master → a su inicio, que abre el modal obligatorio. Las páginas con master
        ///   ya lo aplican en Site.Master.
        /// Sin sesión o sin UsuarioID (estáticos, páginas públicas, GpsAutomatico.ashx con su clave)
        /// no hace nada.
        /// </summary>
        void Application_AcquireRequestState(object sender, EventArgs e)
        {
            HttpContext ctx = Context;
            HttpSessionState sesion = ctx.Session;
            if (sesion == null)
                return;

            int? idUsuario = SesionVigenteHelper.EnteroDeSesion(sesion["UsuarioID"]);
            if (!idUsuario.HasValue)
                return;

            HttpRequest req = ctx.Request;
            bool publica = SesionVigenteHelper.EsRutaPublica(System.IO.Path.GetFileName(req.FilePath));
            bool ajax = SesionVigenteHelper.EsLlamadaAjax(req, ctx.Handler);

            Guid? selloSesion = SesionVigenteHelper.SelloDeSesion(sesion[SesionVigenteHelper.ClaveSello]);
            EstadoSesionUsuario estado = SesionVigenteHelper.ObtenerEstado(idUsuario.Value, selloSesion);
            if (estado != null)
            {
                ResultadoSesion resultado = SesionVigenteHelper.EvaluarSesion(
                    selloSesion,
                    sesion["Rol"] as string,
                    SesionVigenteHelper.EnteroDeSesion(sesion["IdConductor"]),
                    estado);

                if (resultado == ResultadoSesion.Cerrada)
                {
                    LogSGV.Info("Sesión cerrada: cambiaron los datos de acceso del usuario {IdUsuario} ({Ruta})",
                        idUsuario.Value, req.FilePath);
                    CerrarSesionActual(ctx);
                    if (publica)
                        return; // Login y demás públicas se muestran sin sesión.

                    if (ajax)
                        ResponderJson(ctx, 401, "{\"Message\":\"" + SesionVigenteHelper.MensajeSesionCerrada + "\",\"sesionCerrada\":true}");
                    else
                        Redirigir(ctx, "~/Views/Login.aspx?error=sesion_cerrada");
                    return;
                }

                // Site.Master decide el modal obligatorio con este valor: se refresca desde la BD.
                try { sesion["RequiereCambioContrasena"] = estado.RequiereCambioContrasena; }
                catch { /* sesión de solo lectura: basta con usar el valor de la BD abajo */ }
            }

            bool requiereCambio = estado != null
                ? estado.RequiereCambioContrasena
                : sesion["RequiereCambioContrasena"] is bool b && b;
            if (!requiereCambio || publica)
                return;

            if (ajax)
            {
                if (string.Equals(SesionVigenteHelper.NombreMetodo(req.PathInfo), SesionVigenteHelper.MetodoMantenerSesion,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                ResponderJson(ctx, 403, "{\"Message\":\"" + SesionVigenteHelper.MensajeCambioContrasena + "\",\"requiereCambioContrasena\":true}");
                return;
            }

            // Páginas sin master (firma, conformidad, descargas de PDF): a su inicio, que abre el modal.
            if (ctx.Handler is System.Web.UI.Page pagina)
            {
                string rol = sesion["Rol"] as string;
                pagina.InitComplete += (s, a) =>
                {
                    if (pagina.Master == null)
                        pagina.Response.Redirect(RolesHelper.UrlInicioSegunRol(rol) ?? "~/Views/Login.aspx", true);
                };
            }
        }

        private static void CerrarSesionActual(HttpContext ctx)
        {
            try
            {
                ctx.Session.Clear();
                ctx.Session.Abandon();
            }
            catch (Exception ex)
            {
                LogSGV.Advertencia("Sesión vigente: no se pudo abandonar la sesión. {Error}", ex.Message);
            }
            ctx.Response.Cookies.Add(new HttpCookie("SGV_SessionId", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
        }

        private void ResponderJson(HttpContext ctx, int estado, string json)
        {
            HttpResponse resp = ctx.Response;
            resp.Clear();
            resp.TrySkipIisCustomErrors = true;
            resp.StatusCode = estado;
            resp.ContentType = "application/json";
            resp.Cache.SetCacheability(HttpCacheability.NoCache);
            resp.Cache.SetNoStore();
            resp.Write(json);
            CompleteRequest();
        }

        private void Redirigir(HttpContext ctx, string urlVirtual)
        {
            HttpResponse resp = ctx.Response;
            if (SesionVigenteHelper.EsPostbackAsincrono(ctx.Request))
            {
                // Postback de UpdatePanel: un 302 rompería el parser de MS Ajax; se responde con
                // el formato "delta" (longitud|pageRedirect||url|), igual que ScriptManager.
                string url = VirtualPathUtility.ToAbsolute(urlVirtual.Split('?')[0])
                             + (urlVirtual.Contains("?") ? urlVirtual.Substring(urlVirtual.IndexOf('?')) : "");
                resp.Clear();
                resp.ContentType = "text/plain";
                resp.Cache.SetCacheability(HttpCacheability.NoCache);
                resp.Write(url.Length + "|pageRedirect||" + url + "|");
            }
            else
            {
                resp.Redirect(urlVirtual, false);
            }
            CompleteRequest();
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

                // Guardar la excepción REAL en Application para que Error.aspx pueda recuperarla
                // incluso cuando IIS ejecute Error.aspx en un sub-request donde
                // Server.GetLastError() retornaría null. La clave es por sesión: antes existía
                // una clave global "LastError_LastKey" y cualquier visitante veía el último
                // error de otro usuario.
                try
                {
                    string sessionId = HttpContext.Current?.Session?.SessionID;
                    if (!string.IsNullOrEmpty(sessionId))
                        Application["LastError_" + sessionId] = real;
                }
                catch { /* nunca dejar reventar Application_Error */ }
            }
        }
    }
}