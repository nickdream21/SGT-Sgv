using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.UI;
using WebSGV.Helpers;
using WebSGV.Services;

namespace WebSGV.Views
{
    /// <summary>
    /// Handler de descarga del PDF oficial del Parte de Abastecimiento de
    /// Combustible (SGV-CDF-F-06). Reemplaza al talonario pre-impreso.
    /// Requiere sesión. Accesible para roles ADMIN / ADMINISTRADOR DE GRIFO /
    /// ADMINISTRADOR DE SISTEMA.
    ///
    /// Uso:  /Views/DescargarPdfAbastecimiento.aspx?id=[idAbastecimiento]
    ///       /Views/DescargarPdfAbastecimiento.aspx?num=[numeroAbastecimiento]
    ///       [&download=1]
    /// </summary>
    public partial class DescargarPdfAbastecimiento : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.Clear();
            Response.ContentEncoding = System.Text.Encoding.UTF8;

            // 1) Auth
            int idUsuario = 0;
            if (Session["IdUsuario"] != null)
                int.TryParse(Session["IdUsuario"].ToString(), out idUsuario);
            if (idUsuario == 0)
            {
                Response.Redirect(ResolveUrl("~/Views/Login.aspx?returnUrl=" +
                    Server.UrlEncode(Request.RawUrl)), true);
                return;
            }

            string rol = (Session["Rol"] as string ?? "").ToUpperInvariant();
            if (!(rol == "ADMIN" || rol == "ADMINISTRADOR" ||
                  rol == "ADMINISTRADOR DE GRIFO" || rol == "ADMINISTRADOR DE SISTEMA"))
            {
                EscribirError(403, "No tiene permisos para descargar este documento.");
                return;
            }

            // 2) Parámetros
            string idStr = Request.QueryString["id"];
            string numStr = Request.QueryString["num"];
            bool descargar = Request.QueryString["download"] == "1";

            if (string.IsNullOrWhiteSpace(idStr) && string.IsNullOrWhiteSpace(numStr))
            {
                EscribirError(400, "Parámetro 'id' o 'num' requerido.");
                return;
            }

            try
            {
                DetalleAbastecimientoPdf dto;
                string rutaPdfRel;
                string hashPdf;

                CargarDto(idStr, numStr, out dto, out rutaPdfRel, out hashPdf);
                if (dto == null)
                {
                    EscribirError(404, "Abastecimiento no encontrado.");
                    return;
                }

                byte[] pdfBytes;
                string pdfHash;

                // 3) Servir desde disco si existe
                string rutaFisica = ResolverRutaFisica(rutaPdfRel);
                if (!string.IsNullOrEmpty(rutaFisica) && File.Exists(rutaFisica))
                {
                    pdfBytes = File.ReadAllBytes(rutaFisica);
                    pdfHash = hashPdf ?? "";
                }
                else
                {
                    // 4) Regenerar y archivar
                    dto.UsuarioRegistra = (Session["Nombre"] as string) ?? dto.UsuarioRegistra;
                    var svc = new PdfAbastecimientoService();
                    var resultado = svc.GenerarYArchivar(dto, archivar: true);
                    pdfBytes = resultado.Bytes;
                    pdfHash = resultado.Hash;
                    PersistirRutaHash(dto.IdAbastecimientoCombustible, resultado.RutaRelativa, resultado.Hash);
                }

                try
                {
                    AuditoriaHelper.Registrar("DESCARGAR_PDF", "AbastecimientoCombustible",
                        dto.IdAbastecimientoCombustible,
                        "Descarga PDF SGV-CDF-F-06 por usuario " + idUsuario);
                }
                catch (Exception exAud)
                {
                    // La auditoría nunca debe romper la descarga; se registra el fallo.
                    LogSGV.Advertencia("No se pudo auditar la descarga del PDF de Abastecimiento {Id}: {Error}",
                        dto.IdAbastecimientoCombustible, exAud.Message);
                }

                string nombre = "ABT-" + (dto.NumeroAbastecimiento ?? dto.IdAbastecimientoCombustible.ToString()) + ".pdf";
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
                LogError(ex);
                string detalle = HttpContext.Current.IsDebuggingEnabled
                    ? ("Error generando PDF: " + ex.Message +
                       (ex.InnerException != null ? " | Inner: " + ex.InnerException.Message : ""))
                    : "Error generando PDF.";
                EscribirError(500, detalle);
            }
        }

        private static void LogError(Exception ex)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("DescargarPdfAbastecimiento: " + ex);
                string dir = HostingEnvironment.MapPath("~/App_Data/logs");
                if (string.IsNullOrEmpty(dir)) return;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string linea = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex + Environment.NewLine +
                               new string('-', 80) + Environment.NewLine;
                File.AppendAllText(Path.Combine(dir, "pdf-abastecimiento.log"), linea);
            }
            catch
            {
                // Logger de último recurso: si la escritura del propio log falla, se traga
                // a propósito (no hay dónde registrarlo sin recursión).
            }
        }

        private static void CargarDto(
            string idStr, string numStr,
            out DetalleAbastecimientoPdf dto,
            out string rutaPdfRel,
            out string hashPdf)
        {
            dto = null;
            rutaPdfRel = null;
            hashPdf = null;

            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();

                bool tieneRuta = ColumnaExiste(conn, "rutaPdfGenerado");
                bool tieneHash = ColumnaExiste(conn, "hashPdfGenerado");
                bool tieneIdVolquete   = ColumnaExiste(conn, "idVolquete");
                bool tieneIdCamioneta  = ColumnaExiste(conn, "idCamioneta");

                string colExtra = "";
                if (tieneRuta) colExtra += ", a.rutaPdfGenerado";
                if (tieneHash) colExtra += ", a.hashPdfGenerado";

                string placaExpr =
                    "ISNULL(t.placaTracto, " +
                    (tieneIdVolquete  ? "ISNULL(v.placa, "   : "ISNULL(NULL, ") +
                    (tieneIdCamioneta ? "ISNULL(cam.placa, ''))" : "ISNULL(NULL, ''))") +
                    ")";

                string joinVolquete  = tieneIdVolquete  ? "LEFT JOIN volquetes v    ON a.idVolquete  = v.id" : "";
                string joinCamioneta = tieneIdCamioneta ? "LEFT JOIN camionetas cam ON a.idCamioneta = cam.id" : "";

                string where = !string.IsNullOrWhiteSpace(idStr)
                    ? "a.idAbastecimientoCombustible = @id"
                    : "RTRIM(a.numeroAbastecimientoCombustible) = @num";

                string sql = @"
                    SELECT TOP 1
                        a.idAbastecimientoCombustible,
                        a.numeroAbastecimientoCombustible,
                        a.fechaHora, a.horaRetorno,
                        a.galonesRutaAsignada, a.galonesCompradosRuta,
                        a.galonesTotalAbastecidos, a.galonesAlFinalizar, a.galonesTotalConsumidos,
                        a.precioDolar, a.montoTotalGalonesComprados,
                        a.distanciaRutaKM, a.consumoComputador, a.rendimientoPromedio,
                        a.producto, a.observaciones,
                        ISNULL(tc.nombre, '') AS tipoUnidad,
                        " + placaExpr + @" AS placa,
                        ISNULL(cr.placaCarreta, '') AS placaCarreta,
                        LTRIM(RTRIM(ISNULL(c.nombre,'') + ' ' + ISNULL(c.apPaterno,'') + ' ' + ISNULL(c.apMaterno,''))) AS conductor,
                        ISNULL(la.nombreAbastecimiento, '') AS lugar,
                        ISNULL(a.tipoAbastecimiento, '') AS tipoAbast,
                        ISNULL(a.rutaDescripcion, ISNULL(r.nombre, '')) AS ruta
                        " + colExtra + @"
                    FROM AbastecimientoCombustible a
                    LEFT JOIN TipoCarro tc ON a.idTipoCarro = tc.idTipoCarro
                    LEFT JOIN Tracto t     ON a.idTracto    = t.idTracto
                    " + joinVolquete + @"
                    " + joinCamioneta + @"
                    LEFT JOIN Carreta cr   ON a.idCarreta   = cr.idCarreta
                    LEFT JOIN Conductor c  ON a.idConductor = c.idConductor
                    LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                    LEFT JOIN Ruta r       ON a.idRuta      = r.idRuta
                    WHERE " + where;

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (!string.IsNullOrWhiteSpace(idStr))
                        cmd.Parameters.AddWithValue("@id", Convert.ToInt32(idStr));
                    else
                        cmd.Parameters.AddWithValue("@num", numStr.Trim());

                    using (var rd = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if (!rd.Read()) return;

                        dto = new DetalleAbastecimientoPdf
                        {
                            IdAbastecimientoCombustible = Convert.ToInt32(rd["idAbastecimientoCombustible"]),
                            NumeroAbastecimiento = rd["numeroAbastecimientoCombustible"].ToString().Trim(),
                            FechaHora = Convert.ToDateTime(rd["fechaHora"]),
                            HoraRetorno = rd["horaRetorno"] != DBNull.Value ? (TimeSpan?)(TimeSpan)rd["horaRetorno"] : null,
                            GalonesRutaAsignada = ToDec(rd, "galonesRutaAsignada"),
                            GalonesCompradosRuta = ToDec(rd, "galonesCompradosRuta"),
                            GalonesTotalAbastecidos = ToDec(rd, "galonesTotalAbastecidos"),
                            GalonesAlFinalizar = ToDec(rd, "galonesAlFinalizar"),
                            GalonesTotalConsumidos = ToDec(rd, "galonesTotalConsumidos"),
                            PrecioDolar = ToDec(rd, "precioDolar"),
                            MontoTotal = ToDec(rd, "montoTotalGalonesComprados"),
                            DistanciaKm = ToDec(rd, "distanciaRutaKM"),
                            ConsumoComputador = ToDec(rd, "consumoComputador"),
                            RendimientoPromedio = ToDec(rd, "rendimientoPromedio"),
                            Producto = rd["producto"] as string,
                            Observaciones = rd["observaciones"] as string,
                            TipoUnidad = rd["tipoUnidad"] as string,
                            Placa = rd["placa"] as string,
                            PlacaCarreta = rd["placaCarreta"] as string,
                            Conductor = rd["conductor"] as string,
                            LugarAbastecimiento = rd["lugar"] as string,
                            TipoAbastecimiento = rd["tipoAbast"] as string,
                            Ruta = rd["ruta"] as string
                        };

                        if (tieneRuta && rd["rutaPdfGenerado"] != DBNull.Value)
                            rutaPdfRel = rd["rutaPdfGenerado"].ToString();
                        if (tieneHash && rd["hashPdfGenerado"] != DBNull.Value)
                            hashPdf = rd["hashPdfGenerado"].ToString();
                    }
                }
            }
        }

        private static decimal ToDec(IDataRecord rd, string col)
        {
            int i = rd.GetOrdinal(col);
            return rd.IsDBNull(i) ? 0m : Convert.ToDecimal(rd[i]);
        }

        private static bool ColumnaExiste(SqlConnection conn, string columna)
        {
            const string q = @"SELECT COUNT(*) FROM sys.columns
                               WHERE object_id = OBJECT_ID('AbastecimientoCombustible')
                                 AND name = @c";
            using (var cmd = new SqlCommand(q, conn))
            {
                cmd.Parameters.AddWithValue("@c", columna);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private static void PersistirRutaHash(int idAbast, string rutaRelativa, string hash)
        {
            if (string.IsNullOrEmpty(rutaRelativa)) return;
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                bool tieneRuta  = ColumnaExiste(conn, "rutaPdfGenerado");
                bool tieneHash  = ColumnaExiste(conn, "hashPdfGenerado");
                bool tieneFecha = ColumnaExiste(conn, "fechaGeneracionPdf");
                if (!tieneRuta && !tieneHash && !tieneFecha) return;

                var sets = new System.Collections.Generic.List<string>();
                if (tieneRuta)  sets.Add("rutaPdfGenerado = @ruta");
                if (tieneHash)  sets.Add("hashPdfGenerado = @hash");
                if (tieneFecha) sets.Add("fechaGeneracionPdf = @fecha");

                string sql = "UPDATE AbastecimientoCombustible SET " +
                             string.Join(", ", sets) +
                             " WHERE idAbastecimientoCombustible = @id";
                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (tieneRuta)  cmd.Parameters.AddWithValue("@ruta",  (object)rutaRelativa ?? DBNull.Value);
                    if (tieneHash)  cmd.Parameters.AddWithValue("@hash",  (object)hash ?? DBNull.Value);
                    if (tieneFecha) cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmd.Parameters.AddWithValue("@id", idAbast);
                    cmd.ExecuteNonQuery();
                }
            }
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
