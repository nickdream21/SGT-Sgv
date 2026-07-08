using System;
using System.Data;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.UI;
using WebSGV.Helpers;
using WebSGV.Services;

namespace WebSGV.Views
{
    /// <summary>
    /// Handler de descarga del PDF oficial de Orden de Viaje (SGV-CDF-F-05).
    /// Reemplaza al talonario pre-impreso: sirve el PDF archivado en disco o
    /// lo regenera bajo demanda si faltara. Requiere sesión autenticada.
    ///
    /// Uso:  /Views/DescargarPdfOrdenViaje.aspx?id=[idOrdenViaje][&download=1]
    ///
    /// Permisos:
    ///   - ADMIN / ADMINISTRADOR / ADMINISTRADOR DE SISTEMA: cualquier orden.
    ///   - CONDUCTOR: sólo las suyas.
    /// </summary>
    public partial class DescargarPdfOrdenViaje : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentEncoding = System.Text.Encoding.UTF8;

            // 1) Autenticación
            int idUsuario = 0;
            if (Session["IdUsuario"] != null)
                int.TryParse(Session["IdUsuario"].ToString(), out idUsuario);

            if (Session["UsuarioID"] == null || idUsuario == 0)
            {
                Response.Redirect(ResolveUrl("~/Views/Login.aspx?returnUrl=" +
                    Server.UrlEncode(Request.RawUrl)), true);
                return;
            }

            // 2) Parámetros
            int idOrdenViaje;
            if (!int.TryParse(Request.QueryString["id"], out idOrdenViaje) || idOrdenViaje <= 0)
            {
                EscribirError(400, "Parámetro 'id' requerido.");
                return;
            }
            bool descargar = Request.QueryString["download"] == "1";

            try
            {
                // 3) Autorización por rol
                if (!UsuarioPuedeVerOrden(idUsuario, idOrdenViaje))
                {
                    EscribirError(403, "No tiene permisos para descargar este documento.");
                    return;
                }

                // 4) Obtener DTO y estado de firma
                var detalle = LiquidacionesPendientes.ObtenerDetalleLiquidacion(idOrdenViaje);
                if (detalle == null)
                {
                    EscribirError(404, "Orden de Viaje no encontrada.");
                    return;
                }

                string rutaPdfFirmadoRel;
                string hashPdfFirmado;
                byte[] imagenTrazoPng;
                string nombreAdmin;
                string fechaAprobacionAdmin;
                CargarEstadoFirma(idOrdenViaje, out rutaPdfFirmadoRel, out hashPdfFirmado, out imagenTrazoPng, out nombreAdmin, out fechaAprobacionAdmin);

                byte[] pdfBytes;
                string pdfHash;

                // 5) Servir desde disco si el archivo archivado existe
                string rutaFisica = ResolverRutaFisica(rutaPdfFirmadoRel);
                if (!string.IsNullOrEmpty(rutaFisica) && File.Exists(rutaFisica))
                {
                    pdfBytes = File.ReadAllBytes(rutaFisica);
                    pdfHash = hashPdfFirmado ?? "";
                }
                else
                {
                    // 6) Regenerar y archivar
                    var svc = new PdfOrdenViajeService();
                    var resultado = svc.GenerarYArchivar(
                        detalle: detalle,
                        firmaConductorPng: imagenTrazoPng,
                        archivar: true,
                        nombreAdminAprobador: nombreAdmin,
                        fechaAprobacionAdmin: fechaAprobacionAdmin);
                    pdfBytes = resultado.Bytes;
                    pdfHash = resultado.Hash;

                    PersistirRutaHash(idOrdenViaje, resultado.RutaRelativa, resultado.Hash);
                }

                // 7) Auditoría
                try
                {
                    AuditoriaHelper.Registrar("DESCARGAR_PDF", "OrdenViaje", idOrdenViaje,
                        "Descarga PDF SGV-CDF-F-05 por usuario " + idUsuario);
                }
                catch (Exception exAud)
                {
                    // La auditoría nunca debe romper la descarga; se registra el fallo.
                    LogSGV.Advertencia("No se pudo auditar la descarga del PDF de OrdenViaje {Id}: {Error}",
                        idOrdenViaje, exAud.Message);
                }

                // 8) Stream
                string nombre = "OV-" + (detalle.NumeroOrdenViaje ?? idOrdenViaje.ToString()) + ".pdf";
                string disposicion = (descargar ? "attachment" : "inline") +
                                     "; filename=\"" + nombre + "\"";

                Response.ContentType = "application/pdf";
                Response.AddHeader("Content-Disposition", disposicion);
                Response.AddHeader("Content-Length", pdfBytes.Length.ToString());
                if (!string.IsNullOrEmpty(pdfHash))
                    Response.AddHeader("X-PDF-SHA256", pdfHash);

                Response.BinaryWrite(pdfBytes);
                Response.Flush();
                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (System.Threading.ThreadAbortException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DescargarPdfOrdenViaje error: " + ex);
                EscribirError(500, "Error generando PDF.");
            }
        }

        private static bool UsuarioPuedeVerOrden(int idUsuario, int idOrdenViaje)
        {
            if (RolesHelper.EsAdmin() || RolesHelper.EsContabilidad())
                return true;

            if (!RolesHelper.EsConductor())
                return false;

            object r = DbHelper.EjecutarEscalar(
                @"SELECT TOP 1 c.idUsuario
                  FROM OrdenViaje ov
                  INNER JOIN Conductor c ON c.idConductor = ov.idConductor
                  WHERE ov.idOrdenViaje = @id",
                DbHelper.Param("@id", idOrdenViaje));
            if (r == null || r == DBNull.Value) return false;
            return Convert.ToInt32(r) == idUsuario;
        }

        private static void CargarEstadoFirma(
            int idOrdenViaje,
            out string rutaPdfFirmado,
            out string hashPdfFirmado,
            out byte[] imagenTrazoPng,
            out string nombreAdmin,
            out string fechaAprobacionAdmin)
        {
            rutaPdfFirmado = null;
            hashPdfFirmado = null;
            imagenTrazoPng = null;
            nombreAdmin = null;
            fechaAprobacionAdmin = null;

            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT TOP 1
                    ov.rutaPdfFirmado,
                    ov.hashPdfFirmado,
                    fd.imagenTrazoPng,
                    fda.nombreFirmante,
                    CONVERT(VARCHAR(19), ov.fechaAprobacionFirmada, 103) + ' ' +
                        CONVERT(VARCHAR(8), ov.fechaAprobacionFirmada, 108) AS fechaAprobAdmin
                FROM OrdenViaje ov
                LEFT JOIN FirmaDigital fd
                       ON fd.idFirma = ov.idFirmaConductor AND fd.estadoFirma = 'V'
                LEFT JOIN FirmaDigital fda
                       ON fda.idFirma = ov.idFirmaAdmin AND fda.estadoFirma = 'V'
                WHERE ov.idOrdenViaje = @id",
                DbHelper.Param("@id", idOrdenViaje));

            if (dt.Rows.Count > 0)
            {
                DataRow rd = dt.Rows[0];
                if (rd["rutaPdfFirmado"] != DBNull.Value) rutaPdfFirmado = rd["rutaPdfFirmado"].ToString();
                if (rd["hashPdfFirmado"] != DBNull.Value) hashPdfFirmado = rd["hashPdfFirmado"].ToString();
                if (rd["imagenTrazoPng"] != DBNull.Value) imagenTrazoPng = (byte[])rd["imagenTrazoPng"];
                if (rd["nombreFirmante"]  != DBNull.Value) nombreAdmin = rd["nombreFirmante"].ToString();
                if (rd["fechaAprobAdmin"]  != DBNull.Value) fechaAprobacionAdmin = rd["fechaAprobAdmin"].ToString();
            }
        }

        private static void PersistirRutaHash(int idOrdenViaje, string rutaRelativa, string hash)
        {
            if (string.IsNullOrEmpty(rutaRelativa)) return;
            DbHelper.EjecutarNonQuery(@"
                UPDATE OrdenViaje
                   SET rutaPdfFirmado = @ruta,
                       hashPdfFirmado = @hash
                 WHERE idOrdenViaje = @id",
                DbHelper.Param("@ruta", rutaRelativa),
                DbHelper.Param("@hash", hash),
                DbHelper.Param("@id", idOrdenViaje));
        }

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
            catch { return null; }
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
