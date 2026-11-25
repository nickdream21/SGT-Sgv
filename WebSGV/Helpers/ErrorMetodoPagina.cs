using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Web;
using WebSGV.Services.Common;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Errores no capturados de un <c>[WebMethod]</c> de página (E6-11). El manejador de scripts de ASP.NET AJAX
    /// (<c>RestHandler</c>) atrapa la excepción, responde 500 con el JSON del error y la cabecera <c>jsonerror: true</c>,
    /// y no la pasa a <c>Application_Error</c> ni a <c>Server.GetLastError()</c>: hasta E6-10 no quedaba en ningún log.
    /// Cómo se detecta:
    /// <list type="number">
    /// <item><see cref="AlIniciar"/> (BeginRequest) marca la petición si es un PageMethod (<c>.aspx/Metodo</c>).</item>
    /// <item><see cref="Instalar"/> engancha <c>AppDomain.FirstChanceException</c>: en una petición marcada, guarda en
    /// <c>Items</c> la última excepción lanzada en el hilo de la petición (la del método llega envuelta en
    /// <c>TargetInvocationException</c>, que se desenvuelve). Las demás peticiones y los hilos de fondo no guardan nada.</item>
    /// <item><see cref="RegistrarSiFallo"/> (EndRequest): PageMethod + 500 + <c>jsonerror</c> + la excepción no se
    /// registró ya en esta petición (<see cref="LogSGV.ClaveExcepcionesRegistradas"/>, p. ej. un método que registró y
    /// relanzó) → si el método llegó a ejecutarse, una línea ERR "Error no controlado en {Ruta} (WebMethod)" con la
    /// excepción y el correo de <c>AvisoErrores</c> (INF si es un aviso de negocio); si ASP.NET la rechazó antes (JSON
    /// inválido, parámetro que falta), un WRN sin mensaje ni correo (<see cref="Decidir"/>).</item>
    /// </list>
    /// </summary>
    internal static class ErrorMetodoPagina
    {
        internal const string ClaveEsMetodoPagina = "SGV_EsMetodoPagina";
        internal const string ClaveExcepcion = "SGV_ExcepcionMetodoPagina";
        internal const string CabeceraJsonError = "jsonerror";

        private static int instalado;

        [ThreadStatic]
        private static bool dentro;

        internal enum Decision { Nada, Error, AvisoNegocio, LlamadaInvalida }

        /// <summary>Una vez por proceso (Application_Start); llamarla de nuevo no duplica el manejador.</summary>
        internal static void Instalar()
        {
            if (Interlocked.Exchange(ref instalado, 1) == 0)
                AppDomain.CurrentDomain.FirstChanceException += AlLanzarse;
        }

        internal static bool EsMetodoPagina(string rutaArchivo, string pathInfo) =>
            SesionVigenteHelper.EsLlamadaAjax(rutaArchivo, pathInfo, handlerEsPagina: true);

        internal static void AlIniciar(HttpContext ctx)
        {
            if (ctx != null && EsMetodoPagina(ctx.Request.FilePath, ctx.Request.PathInfo))
                ctx.Items[ClaveEsMetodoPagina] = true;
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "Un manejador de FirstChanceException nunca debe lanzar (correría en cada excepción del proceso).")]
        internal static void AlLanzarse(object sender, FirstChanceExceptionEventArgs e)
        {
            if (dentro || e == null)
                return;
            dentro = true; // una excepción aquí dentro volvería a llamar a este manejador
            try
            {
                HttpContext ctx = HttpContext.Current;
                if (ctx != null && ctx.Items[ClaveEsMetodoPagina] is bool marcada && marcada)
                    ctx.Items[ClaveExcepcion] = e.Exception;
            }
            catch { /* sin contexto utilizable */ }
            finally { dentro = false; }
        }

        /// <summary>La excepción del método sin las envolturas de la reflexión ni de ASP.NET.</summary>
        internal static Exception Desenvolver(Exception ex)
        {
            Exception real = ex;
            while (real is TargetInvocationException && real.InnerException != null)
                real = real.InnerException;
            return ErrorAplicacionHelper.Desenvolver(real);
        }

        /// <summary>
        /// Qué escribir. <paramref name="crudo"/> es la última excepción guardada, sin desenvolver: si es la
        /// <c>TargetInvocationException</c> de <c>MethodInfo.Invoke</c> o su pila pasa por código propio (WebSGV.* o
        /// ASP.*), el método llegó a ejecutarse → ERR (o INF si es de negocio). Si no, falló ASP.NET al leer la llamada
        /// (JSON inválido, parámetro que falta o de otro tipo: lo típico de un bot) → <see cref="Decision.LlamadaInvalida"/>,
        /// un WRN sin el mensaje (puede traer un trozo del cuerpo) y sin correo.
        /// </summary>
        internal static Decision Decidir(bool esMetodoPagina, int codigoEstado, bool cabeceraJsonError, bool yaRegistrada, Exception crudo)
        {
            if (!esMetodoPagina || codigoEstado != 500 || !cabeceraJsonError || yaRegistrada)
                return Decision.Nada;
            if (crudo == null)
                return Decision.Error;
            if (!LlegoAEjecutarse(crudo))
                return Decision.LlamadaInvalida;
            return MensajeError.ExtraerMensajeDeNegocio(Desenvolver(crudo)) != null ? Decision.AvisoNegocio : Decision.Error;
        }

        internal static bool LlegoAEjecutarse(Exception crudo)
        {
            if (crudo is TargetInvocationException)
                return true;
            string lugar = WebSGV.Services.Operacion.AvisoErrores.Lugar(crudo);
            return lugar.StartsWith("WebSGV.", StringComparison.Ordinal) || lugar.StartsWith("ASP.", StringComparison.Ordinal);
        }

        /// <summary>
        /// True si la petición ya registró como ERR esta misma excepción (p. ej. el método la registró y la relanzó o la
        /// envolvió): se comparan por referencia las dos cadenas de <c>InnerException</c>. Un ERR de otra excepción
        /// (una auditoría que se tragó su error) no silencia el del método.
        /// </summary>
        internal static bool YaRegistrada(IEnumerable<Exception> registradas, Exception lanzada)
        {
            if (registradas == null || lanzada == null)
                return false;
            foreach (Exception r in registradas)
                for (Exception a = r; a != null; a = a.InnerException)
                    for (Exception b = lanzada; b != null; b = b.InnerException)
                        if (ReferenceEquals(a, b))
                            return true;
            return false;
        }

        /// <summary>EndRequest. Nunca lanza.</summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types",
            Justification = "EndRequest no debe lanzar: la respuesta ya está escrita.")]
        internal static void RegistrarSiFallo(HttpContext ctx)
        {
            try
            {
                if (ctx == null || !(ctx.Items[ClaveEsMetodoPagina] is bool marcada) || !marcada)
                    return;
                HttpResponse resp = ctx.Response;
                Exception crudo = ctx.Items[ClaveExcepcion] as Exception;
                Decision decision = Decidir(true, resp.StatusCode, TieneCabeceraJsonError(resp),
                    YaRegistrada(ctx.Items[LogSGV.ClaveExcepcionesRegistradas] as IEnumerable<Exception>, crudo), crudo);
                if (decision == Decision.Nada)
                    return;

                // Como Application_Error: sin los valores del querystring.
                HttpRequest req = ctx.Request;
                string ruta = req.Path + WebSGV.Services.Operacion.AvisoErrores.QuerySinValores(req.Url.Query);
                Exception ex = Desenvolver(crudo);
                Escribir(decision, ruta, ex);
                if (decision == Decision.Error && ex != null)
                    WebSGV.Services.Operacion.AvisoErrores.Notificar(ex, ctx);
            }
            catch { /* el registro es secundario */ }
        }

        /// <summary>La línea del log (aparte para probarla sin pipeline).</summary>
        internal static void Escribir(Decision decision, string ruta, Exception ex)
        {
            if (decision == Decision.AvisoNegocio)
                LogSGV.Info("WebMethod {Ruta} respondió 500 con un aviso de negocio: {Aviso}", ruta, MensajeError.ExtraerMensajeDeNegocio(ex));
            else if (decision == Decision.LlamadaInvalida)
                LogSGV.Advertencia("Llamada inválida a {Ruta} (WebMethod; ASP.NET la rechazó antes de ejecutar el método: {Tipo})",
                    ruta, ex?.GetType().Name ?? "?");
            else if (decision == Decision.Error && ex != null)
                LogSGV.Error(ex, "Error no controlado en {Ruta} (WebMethod)", ruta);
            else if (decision == Decision.Error)
                LogSGV.Error("Error no controlado en {Ruta} (WebMethod; respondió 500 sin que se viera la excepción)", ruta);
        }
        /// <summary>La cabecera que pone <c>RestHandler</c> al responder un error; sin pipeline integrado no se puede leer y se supone.</summary>
        private static bool TieneCabeceraJsonError(HttpResponse resp)
        {
            try
            {
                return string.Equals(resp.Headers[CabeceraJsonError], "true", StringComparison.OrdinalIgnoreCase);
            }
            catch (PlatformNotSupportedException)
            {
                return true;
            }
        }
    }
}
