using System;
using System.Web;
using System.Web.UI;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Views
{
    /// <summary>
    /// Descarga del PDF oficial de Orden de Viaje (SGV-CDF-F-05). Sirve el archivo guardado
    /// solo si su SHA-256 coincide con el registrado; un documento firmado nunca se regenera
    /// (ver <see cref="ArchivoOrdenViajeService"/>). Requiere sesión autenticada.
    ///
    /// Uso:  /Views/DescargarPdfOrdenViaje.aspx?id=[idOrdenViaje][&amp;firma=[idFirma]][&amp;download=1]
    ///       firma = una versión anterior de la misma orden (p. ej. la declaración del conductor).
    ///
    /// Permisos:
    ///   - Roles que consultan liquidaciones: cualquier orden.
    ///   - CONDUCTOR: sólo las suyas.
    /// </summary>
    public partial class DescargarPdfOrdenViaje : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentEncoding = System.Text.Encoding.UTF8;

            // 1) Autenticación
            int idUsuario = SecurityHelper.ObtenerIdUsuario();
            if (!SecurityHelper.TieneSesionActiva() || idUsuario == 0)
            {
                Response.Redirect(ResolveUrl("~/Views/Login.aspx?returnUrl=" +
                    Server.UrlEncode(Request.RawUrl)), true);
                return;
            }

            // 2) Parámetros
            if (!int.TryParse(Request.QueryString["id"], out int idOrdenViaje) || idOrdenViaje <= 0)
            {
                EscribirError(400, "Parámetro 'id' requerido.");
                return;
            }
            int? idFirma = int.TryParse(Request.QueryString["firma"], out int f) && f > 0 ? f : (int?)null;
            bool descargar = Request.QueryString["download"] == "1";

            try
            {
                // 3) Autorización por rol
                if (!UsuarioPuedeVerOrden(idUsuario, idOrdenViaje))
                {
                    EscribirError(403, "No tiene permisos para descargar este documento.");
                    return;
                }

                // 4) Archivo verificado
                var pdf = ArchivoOrdenViajeService.Obtener(idOrdenViaje, idFirma);
                if (!pdf.Ok)
                {
                    EscribirError(409, pdf.Error);
                    return;
                }

                // 5) Auditoría
                AuditoriaHelper.Registrar("DESCARGAR_PDF", "OrdenViaje", idOrdenViaje,
                    "Descarga PDF SGV-CDF-F-05" + (idFirma.HasValue ? " (firma " + idFirma.Value + ")" : "") +
                    " por usuario " + idUsuario + " · SHA-256 " + pdf.Hash);

                // 6) Stream
                object numero = LiquidacionesPendientesService.ObtenerNumeroOrden(idOrdenViaje);
                string nombre = "OV-" + (numero == null || numero == DBNull.Value ? idOrdenViaje.ToString() : numero.ToString())
                              + (idFirma.HasValue ? "-firma" + idFirma.Value : "") + ".pdf";
                string disposicion = (descargar ? "attachment" : "inline") + "; filename=\"" + nombre + "\"";

                Response.ContentType = "application/pdf";
                Response.AddHeader("Content-Disposition", disposicion);
                Response.AddHeader("Content-Length", pdf.Bytes.Length.ToString());
                Response.AddHeader("X-PDF-SHA256", pdf.Hash);
                Response.AddHeader("X-Content-Type-Options", "nosniff");
                Response.Cache.SetCacheability(HttpCacheability.Private);

                Response.BinaryWrite(pdf.Bytes);
                Response.Flush();
                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (System.Threading.ThreadAbortException) { throw; }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al entregar el PDF de la orden {IdOrden}", idOrdenViaje);
                EscribirError(500, "No fue posible entregar el PDF. Consulte el registro del sistema.");
            }
        }

        private static bool UsuarioPuedeVerOrden(int idUsuario, int idOrdenViaje)
        {
            if (SecurityHelper.PuedeConsultarLiquidacionesGlobales())
                return true;

            if (!SecurityHelper.EsConductor())
                return false;

            return LiquidacionesPendientesService.OrdenPerteneceAUsuarioConductor(idOrdenViaje, idUsuario);
        }

        private void EscribirError(int statusCode, string mensaje)
        {
            Response.Clear();
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Write(mensaje);
            Response.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}
