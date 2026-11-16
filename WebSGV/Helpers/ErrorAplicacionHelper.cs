using System;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Clasificación de los errores no controlados que llegan a Global.asax (Application_Error).
    /// Lógica pura, sin HttpContext, para poder probarla.
    /// </summary>
    public static class ErrorAplicacionHelper
    {
        /// <summary>
        /// Quita las capas de HttpUnhandledException (la página envuelve la excepción real).
        /// </summary>
        public static Exception Desenvolver(Exception ex)
        {
            Exception real = ex;
            while (real is HttpUnhandledException && real.InnerException != null)
                real = real.InnerException;
            return real;
        }

        /// <summary>
        /// Error de ViewState (MAC inválido, ViewState vencido o alterado): se manda al Login.
        /// Se reconoce por tipo (System.Web.UI.ViewStateException en la cadena de InnerException);
        /// el texto del mensaje queda de respaldo, en inglés y en español ("estado de vista"),
        /// porque .NET traduce el mensaje según el idioma del servidor.
        /// </summary>
        public static bool EsErrorViewState(Exception ex)
        {
            if (!(ex is HttpException))
                return false;

            for (Exception e = ex; e != null; e = e.InnerException)
            {
                if (e is System.Web.UI.ViewStateException)
                    return true;
            }

            string mensaje = ex.Message ?? "";
            return mensaje.IndexOf("viewstate", StringComparison.OrdinalIgnoreCase) >= 0
                || mensaje.IndexOf("estado de vista", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Recurso inexistente (HttpException con código 404).</summary>
        public static bool EsNoEncontrado(Exception ex)
        {
            return ex is HttpException http && http.GetHttpCode() == 404;
        }
    }
}
