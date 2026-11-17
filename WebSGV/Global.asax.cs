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

            // Recurso "jquery" del ScriptManager (validación unobtrusive) y MS Ajax con sus textos. Sin MVC ni bundling:
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

            int? idUsuario = SesionHelper.IdUsuario(sesion);
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
                    SesionHelper.Rol(sesion),
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
                string rol = SesionHelper.Rol(sesion);
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

        /// <summary>
        /// Errores no controlados (auditoría de seguridad, tarea 6). Cada error se registra una sola
        /// vez, aquí: ViewState → advertencia y a la misma página por GET (con sesión) o al Login; 404 → info; el resto → error con la ruta y
        /// el usuario. En Release (customErrors RemoteOnly) el usuario remoto llega a Error.aspx por
        /// un 302 (petición nueva, sin la excepción) y ve solo el mensaje genérico; el detalle queda
        /// en el log. En desarrollo (customErrors Off) se ve la pantalla de error de ASP.NET.
        /// </summary>
        void Application_Error(object sender, EventArgs e)
        {
            try
            {
                Exception real = ErrorAplicacionHelper.Desenvolver(Server.GetLastError());
                if (real == null)
                    return;

                // Postback parcial que Site.Master ya registró y respondió (ScriptManager1_AsyncPostBackError):
                // solo se limpia el error para que salga la respuesta "error|..." del ScriptManager.
                if (ErrorAplicacionHelper.ErrorPostbackParcialYaManejado(Context.Items))
                {
                    Server.ClearError();
                    return;
                }

                string ruta = Request.RawUrl;

                if (ErrorAplicacionHelper.EsErrorViewState(real))
                {
                    // Con sesión válida (otra pestaña volvió a iniciar sesión y cambió el SessionID,
                    // así que el ViewState de esta pestaña ya no valida): se recarga la misma página
                    // por GET con un aviso, sin cerrar la sesión. Sin sesión: al Login.
                    bool haySesion = false;
                    try { haySesion = SesionHelper.HaySesion(Context.Session); }
                    catch { /* sin sesión en este punto del pipeline */ }

                    string destino = ErrorAplicacionHelper.DestinoTrasErrorViewState(
                        Request.HttpMethod, haySesion, Request.AppRelativeCurrentExecutionFilePath, Request.Url.Query);

                    LogSGV.Advertencia("ViewState inválido o vencido en {Ruta}; se envía a {Destino}. {Error}", ruta, destino, real.Message);
                    Server.ClearError();
                    Redirigir(Context, destino);
                    return;
                }

                if (ErrorAplicacionHelper.EsNoEncontrado(real))
                {
                    LogSGV.Info("Recurso no encontrado: {Ruta}", ruta);
                    return;
                }

                int? idUsuario = null;
                try { idUsuario = SesionHelper.IdUsuario(Context.Session); }
                catch { /* sin sesión en este punto del pipeline */ }

                LogSGV.Error(real, "Error no controlado en {Ruta} (usuario {IdUsuario})", ruta, idUsuario);
            }
            catch
            {
                // Application_Error nunca debe lanzar: dejaría al usuario con la pantalla de IIS.
            }
        }

        /// <summary>Quita la cabecera "Server" (versión de IIS) de todas las respuestas.</summary>
        void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            try { Response.Headers.Remove("Server"); }
            catch { /* modo clásico o cabeceras ya enviadas: no hay nada que hacer */ }
        }
    }
}