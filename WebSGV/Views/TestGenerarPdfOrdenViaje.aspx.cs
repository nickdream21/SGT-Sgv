using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.UI;
using WebSGV.Services;

namespace WebSGV.Views
{
    /// <summary>
    /// Página de prueba aislada para validar la generación del PDF de Orden de Viaje
    /// (formato SGV-CDF-F-05). Uso:
    ///
    ///   /Views/TestGenerarPdfOrdenViaje.aspx?id=123
    ///
    /// Opcionales:
    ///   &amp;archivar=1  -> guarda el PDF en ~/App_Data/OrdenesViaje/AAAA/MM/
    ///   &amp;download=1  -> fuerza descarga (attachment) en vez de inline
    ///
    /// IMPORTANTE: Esta página es SOLO para desarrollo/QA. No debe publicarse en producción
    /// sin autenticación. Se recomienda eliminarla o bloquearla tras validar Fase 2.B.
    /// </summary>
    public partial class TestGenerarPdfOrdenViaje : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentEncoding = System.Text.Encoding.UTF8;

            // 1) Leer parámetros
            string idStr = Request.QueryString["id"];
            int idOrdenViaje;
            if (string.IsNullOrWhiteSpace(idStr) || !int.TryParse(idStr, out idOrdenViaje))
            {
                EscribirError(400, "Parámetro 'id' (idOrdenViaje) es requerido y debe ser entero. " +
                                   "Ejemplo: TestGenerarPdfOrdenViaje.aspx?id=123");
                return;
            }

            bool archivar = Request.QueryString["archivar"] == "1";
            bool descargar = Request.QueryString["download"] == "1";

            try
            {
                // 2) Obtener el DTO reutilizando el WebMethod existente
                var detalle = LiquidacionesPendientes.ObtenerDetalleLiquidacion(idOrdenViaje);
                if (detalle == null)
                {
                    EscribirError(404, "No se encontró la Orden de Viaje con id = " + idOrdenViaje);
                    return;
                }

                // 2.b) Buscar si la orden ya tiene firma registrada en BD
                string rutaPdfFirmadoRel;
                string hashPdfFirmado;
                byte[] imagenTrazoPng;
                CargarEstadoFirma(idOrdenViaje, out rutaPdfFirmadoRel, out hashPdfFirmado, out imagenTrazoPng);

                byte[] pdfBytes;
                string pdfHash;
                string pdfRutaRel = null;

                // 2.c) Si existe el archivo ya firmado en disco, servirlo tal cual
                //      (preserva hash e integridad de la firma original)
                string rutaFisica = ResolverRutaFisica(rutaPdfFirmadoRel);
                if (!string.IsNullOrEmpty(rutaFisica) && File.Exists(rutaFisica))
                {
                    pdfBytes = File.ReadAllBytes(rutaFisica);
                    pdfHash = hashPdfFirmado ?? "";
                    pdfRutaRel = rutaPdfFirmadoRel;
                }
                else
                {
                    // 3) Re-generar PDF. Si tenemos PNG del trazo en BD lo embebemos;
                    //    de lo contrario queda solo con la constancia (preview).
                    var svc = new PdfOrdenViajeService();
                    var resultado = svc.GenerarYArchivar(
                        detalle: detalle,
                        firmaConductorPng: imagenTrazoPng,
                        archivar: archivar);
                    pdfBytes = resultado.Bytes;
                    pdfHash = resultado.Hash;
                    pdfRutaRel = resultado.RutaRelativa;
                }

                // 4) Stream del PDF
                string nombre = "OV-" + (detalle.NumeroOrdenViaje ?? idOrdenViaje.ToString()) + ".pdf";
                string disposicion = (descargar ? "attachment" : "inline") + "; filename=\"" + nombre + "\"";

                Response.ContentType = "application/pdf";
                Response.AddHeader("Content-Disposition", disposicion);
                Response.AddHeader("Content-Length", pdfBytes.Length.ToString());
                if (!string.IsNullOrEmpty(pdfHash))
                    Response.AddHeader("X-PDF-SHA256", pdfHash);
                if (!string.IsNullOrEmpty(pdfRutaRel))
                    Response.AddHeader("X-PDF-Archivo", pdfRutaRel);
                Response.BinaryWrite(pdfBytes);
                Response.Flush();
                Response.SuppressContent = true;
                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Response.End / Redirect: propagar
                throw;
            }
            catch (Exception ex)
            {
                EscribirError(500, "Error generando PDF: " + ex.Message +
                                   Environment.NewLine + Environment.NewLine +
                                   "StackTrace: " + ex.StackTrace);
            }
        }

        /// <summary>
        /// Consulta en <c>OrdenViaje</c> la ruta del PDF firmado y su hash, y
        /// recupera el PNG del trazo de la firma del conductor desde
        /// <c>FirmaDigital</c> (si existe). El PNG permite re-generar un PDF
        /// con la firma embebida si el archivo original ya no estuviese en disco.
        /// </summary>
        private static void CargarEstadoFirma(
            int idOrdenViaje,
            out string rutaPdfFirmado,
            out string hashPdfFirmado,
            out byte[] imagenTrazoPng)
        {
            rutaPdfFirmado = null;
            hashPdfFirmado = null;
            imagenTrazoPng = null;

            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            const string sql = @"
                SELECT TOP 1
                    ov.rutaPdfFirmado,
                    ov.hashPdfFirmado,
                    fd.imagenTrazoPng
                FROM OrdenViaje ov
                LEFT JOIN FirmaDigital fd
                       ON fd.idFirma = ov.idFirmaConductor
                      AND fd.estadoFirma = 'V'
                WHERE ov.idOrdenViaje = @id;";

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                conn.Open();
                using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                {
                    if (rd.Read())
                    {
                        if (!rd.IsDBNull(0)) rutaPdfFirmado = rd.GetString(0);
                        if (!rd.IsDBNull(1)) hashPdfFirmado = rd.GetString(1);
                        if (!rd.IsDBNull(2)) imagenTrazoPng = (byte[])rd[2];
                    }
                }
            }
        }

        /// <summary>
        /// Convierte una ruta relativa del tipo <c>~/App_Data/OrdenesViaje/...</c>
        /// a una ruta física del servidor. Devuelve null si no se puede resolver.
        /// </summary>
        private static string ResolverRutaFisica(string rutaRelativa)
        {
            if (string.IsNullOrWhiteSpace(rutaRelativa)) return null;
            try
            {
                if (rutaRelativa.StartsWith("~", StringComparison.Ordinal))
                    return HostingEnvironment.MapPath(rutaRelativa);
                if (Path.IsPathRooted(rutaRelativa))
                    return rutaRelativa;
                return HostingEnvironment.MapPath("~/" + rutaRelativa.TrimStart('/', '\\'));
            }
            catch
            {
                return null;
            }
        }

        private void EscribirError(int statusCode, string mensaje)
        {
            Response.Clear();
            Response.StatusCode = statusCode;
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Write(mensaje);
            Response.Flush();
            Response.SuppressContent = true;
            HttpContext.Current.ApplicationInstance.CompleteRequest();
        }
    }
}
