using System;
using System.Configuration;
using System.IO.Compression;
using System.Linq;
using System.Web;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Respaldo de compresión (E5-7): gzip propio de las páginas, para cuando el servidor no comprime lo dinámico
    /// (somee sin el módulo de compresión dinámica de IIS; Web.config ya pide &lt;urlCompression&gt;). Apagado por
    /// defecto: se enciende con &lt;add key="Compresion.Respuesta" value="true" /&gt; en Web.config solo si, tras
    /// publicar, el HTML de Login llega sin Content-Encoding (paso "medir gzip tras publicar" de
    /// herramientas/publicar/README.md). Lo llama Global.asax en PostRequestHandlerExecute.
    /// Solo comprime HTML, JSON y texto; nunca descargas ni respuestas parciales de UpdatePanel. Los PageMethods
    /// ([WebMethod] de página) no pasan por aquí: ScriptModule los termina antes de PostRequestHandlerExecute.
    /// </summary>
    public static class CompresionRespuesta
    {
        public const string ClaveInterruptor = "Compresion.Respuesta";

        /// <summary>Tipos que se comprimen (sin parámetros como charset).</summary>
        public static readonly string[] TiposComprimibles = { "text/html", "application/json", "text/plain" };

        /// <summary>Páginas que entregan archivos aunque su tipo sea de texto: nunca se comprimen.</summary>
        public static readonly string[] PaginasDeDescarga =
        {
            "/Views/DescargarDocumento.aspx",
            "/Views/DescargarPdfOrdenViaje.aspx"
        };

        /// <summary>Valor del interruptor (false si falta o no es "true").</summary>
        public static bool Encendido
        {
            get
            {
                bool valor;
                return bool.TryParse(ConfigurationManager.AppSettings[ClaveInterruptor], out valor) && valor;
            }
        }

        /// <summary>
        /// Decide si la respuesta se comprime con gzip. Función pura: la prueba PrimerIngresoLivianoTests.
        /// </summary>
        /// <param name="tipoContenido">Content-Type final de la respuesta (puede traer "; charset=...").</param>
        /// <param name="aceptaCodificacion">Encabezado Accept-Encoding de la petición.</param>
        /// <param name="codificacionActual">Content-Encoding que ya tenga la respuesta (vacío si no).</param>
        /// <param name="disposicion">Content-Disposition de la respuesta (vacío si no): con él es una descarga.</param>
        /// <param name="ruta">Ruta de la petición (Request.Path, sin querystring).</param>
        /// <param name="esParcialUpdatePanel">True si la petición es un postback asíncrono de UpdatePanel.</param>
        public static bool DebeComprimir(string tipoContenido, string aceptaCodificacion, string codificacionActual,
            string disposicion, string ruta, bool esParcialUpdatePanel)
        {
            if (esParcialUpdatePanel) return false;
            if (!string.IsNullOrWhiteSpace(codificacionActual)) return false;
            if (!string.IsNullOrWhiteSpace(disposicion)) return false;
            if (!AceptaGzip(aceptaCodificacion)) return false;

            string r = (ruta ?? "").Trim();
            if (r.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase)) return false;
            if (r.EndsWith(".axd", StringComparison.OrdinalIgnoreCase)) return false;
            if (PaginasDeDescarga.Any(p => r.EndsWith(p, StringComparison.OrdinalIgnoreCase))) return false;

            string tipo = (tipoContenido ?? "").Split(';')[0].Trim();
            return TiposComprimibles.Any(t => t.Equals(tipo, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True si Accept-Encoding trae gzip (o *) sin q=0.</summary>
        public static bool AceptaGzip(string aceptaCodificacion)
        {
            if (string.IsNullOrWhiteSpace(aceptaCodificacion)) return false;
            foreach (string parte in aceptaCodificacion.Split(','))
            {
                string[] trozos = parte.Split(';');
                string nombre = trozos[0].Trim();
                if (!nombre.Equals("gzip", StringComparison.OrdinalIgnoreCase) && nombre != "*") continue;
                bool rechazado = trozos.Skip(1).Select(t => t.Replace(" ", ""))
                    .Any(t => t.StartsWith("q=", StringComparison.OrdinalIgnoreCase)
                              && double.TryParse(t.Substring(2), System.Globalization.NumberStyles.Float,
                                                 System.Globalization.CultureInfo.InvariantCulture, out double q)
                              && q <= 0);
                if (!rechazado) return true;
            }
            return false;
        }

        /// <summary>Postback asíncrono de UpdatePanel (MS Ajax manda X-MicrosoftAjax: Delta=true).</summary>
        public static bool EsParcialUpdatePanel(HttpRequest request)
        {
            string cabecera = request.Headers["X-MicrosoftAjax"];
            return cabecera != null && cabecera.IndexOf("Delta=true", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Si el interruptor está encendido y la respuesta califica, la comprime con gzip. Nunca lanza: ante
        /// cualquier duda la respuesta sale como siempre.
        /// </summary>
        public static void AplicarSiCorresponde(HttpContext contexto)
        {
            if (contexto == null || !Encendido) return;
            HttpResponse respuesta = contexto.Response;
            bool cabeceraPuesta = false;
            try
            {
                // Ya enviada (Flush, TransmitFile) o redirigida: no se puede poner filtro ni cabeceras.
                if (respuesta.HeadersWritten || respuesta.IsRequestBeingRedirected) return;
                // Un Content-Length puesto a mano dejaría de coincidir con el cuerpo comprimido.
                if (!string.IsNullOrEmpty(respuesta.Headers["Content-Length"])) return;
                if (!DebeComprimir(respuesta.ContentType, contexto.Request.Headers["Accept-Encoding"],
                        respuesta.Headers["Content-Encoding"], respuesta.Headers["Content-Disposition"],
                        contexto.Request.Path, EsParcialUpdatePanel(contexto.Request)))
                    return;

                // Cabeceras primero y el filtro al final: si el filtro falla, se quita la cabecera y sale sin comprimir.
                respuesta.AppendHeader("Content-Encoding", "gzip");
                cabeceraPuesta = true;
                respuesta.AppendHeader("Vary", "Accept-Encoding");
                respuesta.Filter = new GZipStream(respuesta.Filter, CompressionMode.Compress);
            }
            catch (Exception ex)
            {
                if (cabeceraPuesta)
                {
                    try { respuesta.Headers.Remove("Content-Encoding"); } catch { /* sin cabeceras que quitar */ }
                }
                LogSGV.Error(ex, "No se pudo comprimir la respuesta");
            }
        }
    }
}
