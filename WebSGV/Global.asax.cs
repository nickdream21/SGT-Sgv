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
            // E6-11: guarda la excepción de un [WebMethod] que no la captura (el manejador de scripts no la pasa a Application_Error).
            ErrorMetodoPagina.Instalar();
            // Salud.ashx?gps=1 no avisa durante la primera hora: tras un reciclaje no se sabe cuándo corrió el GPS.
            Salud.InicioAplicacionUtc = DateTime.UtcNow;

            // Recurso "jquery" del ScriptManager (validación unobtrusive) y MS Ajax con sus textos. Sin MVC ni bundling:
            // el sitio es WebForms puro con URLs .aspx directas.
            BundleConfig.RegisterJQueryScriptManager();

            // QuestPDF: licencia Community (empresa con ingresos < 1M USD anuales)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            // Crear tabla de auditoría si no existe
            AuditoriaHelper.CrearTablaAuditoriaSiNoExiste();

            // Cargar QuestPDF en segundo plano para que el primer conductor que firme no espere.
            System.Threading.ThreadPool.QueueUserWorkItem(_ => WebSGV.Services.PdfOrdenViajeService.Precalentar());

            // Temporales huérfanos de más de 24 h en ~/Uploads/*/_temp (sesiones vencidas, lotes abandonados).
            // En segundo plano y sin lanzar: un fallo aquí no debe impedir que arranque el sitio.
            try
            {
                string raizUploads = System.Web.Hosting.HostingEnvironment.MapPath("~/Uploads/");
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { ArchivosTemporalesHelper.LimpiarSiCorresponde(raizUploads, DateTime.UtcNow); }
                    catch (Exception ex) { LogSGV.Error(ex, "No se pudieron limpiar los temporales al iniciar"); }
                });
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo programar la limpieza de temporales al iniciar");
            }
        }

        /// <summary>
        /// Id de la petición (E6-10): 8 hex al azar en <c>Context.Items</c>, que <c>LogSGV</c> pone en cada línea
        /// (<c>[3f9a0c1e] u12</c>) para juntar todo lo que dejó una petición. También va en el encabezado de respuesta
        /// <c>X-SGV-Peticion</c> (al azar, no dice nada del usuario ni del servidor), que se pone al enviar las cabeceras
        /// (<see cref="Application_PreSendRequestHeaders"/>) para que sobreviva a un <c>Response.ClearHeaders()</c> de
        /// las descargas: con las herramientas del navegador se llega a las líneas del log de una respuesta concreta.
        /// </summary>
        void Application_BeginRequest(object sender, EventArgs e)
        {
            Context.Items[LogSGV.ClaveIdPeticion] = LogSGV.NuevoIdPeticion();
            MedicionPeticiones.AlIniciar(Context);
            ErrorMetodoPagina.AlIniciar(Context);
        }

        /// <summary>Empieza la espera de la sesión (E6-11): el módulo de sesión la toma en AcquireRequestState.</summary>
        void Application_PostMapRequestHandler(object sender, EventArgs e)
        {
            MedicionPeticiones.AlMapearManejador(Context);
        }

        /// <summary>
        /// E6-11: un 500 de un [WebMethod] deja una línea ERR (ErrorMetodoPagina) y la petición se suma a la medición
        /// por ruta, con el aviso "Petición lenta" y el "Resumen del día" (MedicionPeticiones). Ninguna de las dos lanza.
        /// </summary>
        void Application_EndRequest(object sender, EventArgs e)
        {
            ErrorMetodoPagina.RegistrarSiFallo(Context);
            MedicionPeticiones.AlTerminar(Context);
        }

        void Application_End(object sender, EventArgs e)
        {
            // Resumen parcial de la medición (lo de memoria se pierde) y luego vaciar y cerrar los sinks de log.
            MedicionPeticiones.AlDetener();
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
            MedicionPeticiones.AlTenerSesion(Context); // E6-11: fin de la espera de la sesión (corre tras el módulo de sesión)
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

            // Duración máxima (E4-12): 12 h desde el inicio de sesión aunque haya actividad. Se cierra aquí, también
            // en las páginas públicas: Login llega ya sin sesión y no la confunde con una vigente (rompe-bucles E4-8).
            if (SesionVigenteHelper.SuperoDuracionMaxima(SesionHelper.InicioSesionUtc(sesion), DateTime.UtcNow))
            {
                LogSGV.Info("Sesión cerrada: llegó a su duración máxima (usuario {IdUsuario}, {Ruta})",
                    idUsuario.Value, req.FilePath);
                CerrarSesionActual(ctx);
                if (publica)
                    return;
                if (ajax)
                    ResponderJson(ctx, 401, "{\"Message\":\"" + SesionVigenteHelper.MensajeDuracionMaxima + "\",\"sesionCerrada\":true,\"codigoLogin\":\"" + SesionVigenteHelper.CodigoSesionVencida + "\"}");
                else
                    Redirigir(ctx, "~/Views/Login.aspx?error=" + SesionVigenteHelper.CodigoSesionVencida);
                return;
            }

            Guid? selloSesion = SesionVigenteHelper.SelloDeSesion(sesion[SesionVigenteHelper.ClaveSello]);
            EstadoSesionUsuario estado = SesionVigenteHelper.ObtenerEstado(idUsuario.Value, selloSesion);
            if (estado != null)
            {
                ResultadoSesion resultado = SesionVigenteHelper.EvaluarSesion(
                    selloSesion,
                    SesionHelper.Rol(sesion),
                    SesionHelper.IdConductor(sesion),
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

                // Site.Master decide el modal obligatorio con este valor: se refresca desde la BD (más
                // la marca de sesión de una contraseña fuera de la política, que no está en la BD).
                try { sesion["RequiereCambioContrasena"] = estado.RequiereCambioContrasena || SesionVigenteHelper.ContrasenaFueraDePolitica(sesion); }
                catch { /* sesión de solo lectura: basta con usar el valor de la BD abajo */ }
            }

            bool requiereCambio = (estado != null
                ? estado.RequiereCambioContrasena
                : sesion["RequiereCambioContrasena"] is bool b && b)
                || SesionVigenteHelper.ContrasenaFueraDePolitica(sesion);
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

                // Para el log: sin los valores del querystring (p. ej. el token de RestablecerContrasena).
                string ruta = Request.Path + WebSGV.Services.Operacion.AvisoErrores.QuerySinValores(Request.Url.Query);

                // Envío más grande que el maxRequestLength de la página (Web.config, tarea 14): se
                // vuelve a la misma página por GET con ?aviso=archivo_grande (Site.Master lo muestra),
                // en vez de la pantalla de error. El GET no lleva cuerpo, así que no hay bucle.
                // Las llamadas AJAX (PageMethods, .ashx) siguen al registro de error normal.
                if (ErrorAplicacionHelper.EsEnvioDemasiadoGrande(real)
                    && !SesionVigenteHelper.EsLlamadaAjax(Request, Context.Handler))
                {
                    bool haySesion = false;
                    try { haySesion = SesionHelper.HaySesion(Context.Session); }
                    catch { /* sin sesión en este punto del pipeline */ }

                    string destino = ErrorAplicacionHelper.DestinoConAviso(
                        Request.HttpMethod, haySesion, Request.AppRelativeCurrentExecutionFilePath, Request.Url.Query,
                        ErrorAplicacionHelper.AvisoArchivoGrande);

                    LogSGV.Advertencia("Envío demasiado grande ({Bytes} bytes) en {Ruta}; se envía a {Destino}.",
                        Request.ContentLength, ruta, WebSGV.Services.Operacion.AvisoErrores.UrlSinValores(destino));
                    Server.ClearError();
                    Redirigir(Context, destino);
                    return;
                }

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

                    LogSGV.Advertencia("ViewState inválido o vencido en {Ruta}; se envía a {Destino}. {Error}", ruta,
                        WebSGV.Services.Operacion.AvisoErrores.UrlSinValores(destino), real.Message);
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

                if (ErrorAplicacionHelper.EsValidacionDePeticion(real))
                    LogSGV.Advertencia("Petición rechazada por la validación de ASP.NET ({Tipo}) en {Ruta} (usuario {IdUsuario})",
                        real.GetType().Name, ruta, idUsuario); // sin real.Message: trae parte del valor enviado
                else
                    LogSGV.Error(real, "Error no controlado en {Ruta} (usuario {IdUsuario})", ruta, idUsuario);

                // E1-7: correo a AvisoErroresPara (Web.config), con límite y en segundo plano; no lanza
                // ni cambia la respuesta. Las exclusiones (bots, peticiones inválidas, negocio) van en AvisoErrores.
                WebSGV.Services.Operacion.AvisoErrores.Notificar(real, Context);
            }
            catch
            {
                // Application_Error nunca debe lanzar: dejaría al usuario con la pantalla de IIS.
            }
        }

        /// <summary>
        /// Respaldo de compresión (E5-7): gzip propio del HTML/JSON/texto si el interruptor Compresion.Respuesta
        /// está encendido (apagado por defecto; ver Helpers/CompresionRespuesta.cs). Aquí el handler ya fijó su
        /// Content-Type final y nada se envió todavía.
        /// </summary>
        void Application_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            CompresionRespuesta.AplicarSiCorresponde(Context);
        }

        /// <summary>
        /// Quita la cabecera "Server" (versión de IIS) de todas las respuestas y pone <c>X-SGV-Peticion</c> con el id de la
        /// petición (E6-10). Aquí y no en BeginRequest: las descargas de Excel/PDF limpian las cabeceras antes de escribir.
        /// </summary>
        void Application_PreSendRequestHeaders(object sender, EventArgs e)
        {
            try
            {
                Response.Headers.Remove("Server");
                if (Context?.Items[LogSGV.ClaveIdPeticion] is string id)
                    Response.Headers.Set("X-SGV-Peticion", id);
            }
            catch { /* modo clásico o cabeceras ya enviadas: no hay nada que hacer (el id sigue en el log) */ }
        }
    }
}