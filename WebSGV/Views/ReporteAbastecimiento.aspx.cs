using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class ReporteAbastecimiento : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RolesHelper.TieneSesionActiva())
            {
                Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (!RolesHelper.EsAdminGrifo() && !RolesHelper.EsAdmin())
            {
                RolesHelper.RedirigirSegunRol();
                return;
            }

            if (!IsPostBack)
            {
                // Establecer rango por defecto: mes actual
                txtFechaDesde.Text = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");
                CargarReporte();
            }
        }

        #region Carga de Datos

        private DataTable ObtenerReporte(string fechaDesde, string fechaHasta, string conductor, string tipoAbast)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Detectar columnas disponibles (scripts de migración pueden no haberse ejecutado)
                bool tieneTipoAbast = ColumnaExisteEnTabla(conn, "AbastecimientoCombustible", "tipoAbastecimiento");
                bool tieneRutaDesc = ColumnaExisteEnTabla(conn, "AbastecimientoCombustible", "rutaDescripcion");
                bool tieneIdOrdenViaje = ColumnaExisteEnTabla(conn, "AbastecimientoCombustible", "idOrdenViaje");

                // Expresiones dinámicas según columnas disponibles
                string tipoAbastExpr;
                if (tieneIdOrdenViaje && tieneTipoAbast)
                    tipoAbastExpr = "CASE WHEN a.idOrdenViaje IS NOT NULL THEN 'VIAJE PROGRAMADO' ELSE ISNULL(a.tipoAbastecimiento, 'ABASTECIMIENTO') END";
                else if (tieneIdOrdenViaje)
                    tipoAbastExpr = "CASE WHEN a.idOrdenViaje IS NOT NULL THEN 'VIAJE PROGRAMADO' ELSE 'ABASTECIMIENTO' END";
                else if (tieneTipoAbast)
                    tipoAbastExpr = "ISNULL(a.tipoAbastecimiento, 'ABASTECIMIENTO')";
                else
                    tipoAbastExpr = "'ABASTECIMIENTO'";

                string rutaExpr = tieneRutaDesc
                    ? "ISNULL(a.rutaDescripcion, ISNULL(r.nombre, 'N/A'))"
                    : "ISNULL(r.nombre, 'N/A')";

                StringBuilder query = new StringBuilder();
                query.Append($@"
                    SELECT 
                        RTRIM(a.numeroAbastecimientoCombustible) AS NumeroFormato,
                        a.fechaHora AS Fecha,
                        {tipoAbastExpr} AS TipoAbastecimiento,
                        ISNULL(tc.nombre, 'N/A') AS TipoVehiculo,
                        ISNULL(CONCAT(c.nombre, ' ', c.apPaterno), 'N/A') AS Conductor,
                        ISNULL(t.placaTracto, 'N/A') AS PlacaTracto,
                        {rutaExpr} AS Ruta,
                        ISNULL(a.producto, 'N/A') AS Producto,
                        ISNULL(la.nombreAbastecimiento, 'N/A') AS LugarAbastecimiento,
                        FORMAT(a.fechaHora, 'hh:mmtt') AS Hora,
                        a.galonesRutaAsignada AS GLRutaAsignada,
                        a.galonesCompradosRuta AS GLCompradosRuta,
                        a.galonesTotalAbastecidos AS GLTotalAbastecidos,
                        a.galonesAlFinalizar AS GLAlFinalizar,
                        a.galonesTotalConsumidos AS GLTotalConsumidos,
                        a.distanciaRutaKM AS DistanciaKM,
                        a.consumoComputador AS ConsumoComputador,
                        ISNULL(a.observaciones, '') AS Observaciones
                    FROM AbastecimientoCombustible a
                    LEFT JOIN Tracto t ON a.idTracto = t.idTracto
                    LEFT JOIN Conductor c ON a.idConductor = c.idConductor
                    LEFT JOIN Ruta r ON a.idRuta = r.idRuta
                    LEFT JOIN LugarAbastecimiento la ON a.idLugarAbastecimiento = la.idLugarAbastecimiento
                    LEFT JOIN TipoCarro tc ON a.idTipoCarro = tc.idTipoCarro
                    WHERE 1=1");

                if (!string.IsNullOrEmpty(fechaDesde))
                {
                    query.Append(" AND CAST(a.fechaHora AS DATE) >= @FechaDesde");
                }

                if (!string.IsNullOrEmpty(fechaHasta))
                {
                    query.Append(" AND CAST(a.fechaHora AS DATE) <= @FechaHasta");
                }

                if (!string.IsNullOrEmpty(conductor))
                {
                    query.Append(" AND (c.nombre LIKE @Conductor OR c.apPaterno LIKE @Conductor OR c.DNI LIKE @Conductor)");
                }

                if (!string.IsNullOrEmpty(tipoAbast) && (tieneTipoAbast || tieneIdOrdenViaje))
                {
                    query.Append($" AND {tipoAbastExpr} = @TipoAbast");
                }

                query.Append(" ORDER BY a.fechaHora DESC");

                using (SqlCommand cmd = new SqlCommand(query.ToString(), conn))
                {
                    if (!string.IsNullOrEmpty(fechaDesde))
                        cmd.Parameters.AddWithValue("@FechaDesde", DateTime.Parse(fechaDesde));

                    if (!string.IsNullOrEmpty(fechaHasta))
                        cmd.Parameters.AddWithValue("@FechaHasta", DateTime.Parse(fechaHasta));

                    if (!string.IsNullOrEmpty(conductor))
                        cmd.Parameters.AddWithValue("@Conductor", "%" + conductor + "%");

                    if (!string.IsNullOrEmpty(tipoAbast) && (tieneTipoAbast || tieneIdOrdenViaje))
                        cmd.Parameters.AddWithValue("@TipoAbast", tipoAbast);

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }
            }

            return dt;
        }

        private bool ColumnaExisteEnTabla(SqlConnection conn, string tabla, string columna)
        {
            using (SqlCommand cmd = new SqlCommand(
                "SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@Tabla) AND name = @Columna", conn))
            {
                cmd.Parameters.AddWithValue("@Tabla", tabla);
                cmd.Parameters.AddWithValue("@Columna", columna);
                return cmd.ExecuteScalar() != null;
            }
        }

        private void CargarReporte()
        {
            try
            {
                string fechaDesde = txtFechaDesde.Text.Trim();
                string fechaHasta = txtFechaHasta.Text.Trim();
                string conductor = txtBuscarConductor.Text.Trim();
                string tipoAbast = ddlTipoAbastecimiento.SelectedValue;

                DataTable dt = ObtenerReporte(fechaDesde, fechaHasta, conductor, tipoAbast);

                lblTotalRegistros.Text = dt.Rows.Count.ToString();

                if (dt.Rows.Count > 0)
                {
                    gvReporte.DataSource = dt;
                    gvReporte.DataBind();

                    // Calcular totales
                    decimal totalGLAbastecidos = 0, totalGLConsumidos = 0, totalKM = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        totalGLAbastecidos += row["GLTotalAbastecidos"] != DBNull.Value ? Convert.ToDecimal(row["GLTotalAbastecidos"]) : 0;
                        totalGLConsumidos += row["GLTotalConsumidos"] != DBNull.Value ? Convert.ToDecimal(row["GLTotalConsumidos"]) : 0;
                        totalKM += row["DistanciaKM"] != DBNull.Value ? Convert.ToDecimal(row["DistanciaKM"]) : 0;
                    }

                    lblTotalGLAbastecidos.Text = totalGLAbastecidos.ToString("#,##0.##");
                    lblTotalGLConsumidos.Text = totalGLConsumidos.ToString("#,##0.##");
                    lblTotalKM.Text = totalKM.ToString("#,##0.#");

                    pnlReporte.Visible = true;
                    pnlResumen.Visible = true;
                    pnlSinResultados.Visible = false;
                }
                else
                {
                    pnlReporte.Visible = false;
                    pnlResumen.Visible = false;
                    pnlSinResultados.Visible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al cargar reporte: " + ex.Message);
                ScriptManager.RegisterStartupScript(this, GetType(), "errorReporte",
                    "alert('Error al cargar el reporte: " + ex.Message.Replace("'", "\\'" ).Replace("\r", "").Replace("\n", " ") + "');", true);
                pnlReporte.Visible = false;
                pnlResumen.Visible = false;
                pnlSinResultados.Visible = true;
            }
        }

        #endregion

        #region Eventos

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            CargarReporte();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            txtFechaDesde.Text = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
            txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtBuscarConductor.Text = "";
            ddlTipoAbastecimiento.SelectedIndex = 0;
            pnlReporte.Visible = false;
            pnlResumen.Visible = false;
            pnlSinResultados.Visible = true;
            lblTotalRegistros.Text = "0";
        }

        protected void btnExportarExcel_Click(object sender, EventArgs e)
        {
            try
            {
                string fechaDesde = txtFechaDesde.Text.Trim();
                string fechaHasta = txtFechaHasta.Text.Trim();
                string conductor = txtBuscarConductor.Text.Trim();
                string tipoAbast = ddlTipoAbastecimiento.SelectedValue;

                DataTable dt = ObtenerReporte(fechaDesde, fechaHasta, conductor, tipoAbast);

                if (dt.Rows.Count == 0)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "noData",
                        "alert('No hay datos para exportar.');", true);
                    return;
                }

                Response.Clear();
                Response.Buffer = true;
                Response.ClearHeaders();
                Response.ContentType = "application/vnd.ms-excel";
                string fileName = $"Reporte_Abastecimiento_{DateTime.Now:yyyyMMdd_HHmm}.xls";
                Response.AddHeader("content-disposition", "attachment;filename=" + fileName);
                Response.Charset = "";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Write("\uFEFF"); // UTF-8 BOM para Excel

                using (StringWriter sw = new StringWriter())
                {
                    using (HtmlTextWriter hw = new HtmlTextWriter(sw))
                    {
                        // Crear tabla HTML para Excel
                        hw.Write("<table border='1' cellpadding='4' cellspacing='0' style='border-collapse:collapse;font-family:Calibri,Arial;font-size:11px;'>");

                        // Encabezado principal
                        hw.Write("<tr>");
                        hw.Write("<td colspan='18' style='background-color:#0056b3;color:white;font-size:14px;font-weight:bold;text-align:center;padding:10px;'>");
                        hw.Write("REPORTE DE ABASTECIMIENTO DE COMBUSTIBLE");
                        hw.Write("</td></tr>");

                        // Rango de fechas
                        hw.Write("<tr>");
                        hw.Write("<td colspan='18' style='background-color:#f0f7ff;font-size:10px;text-align:center;padding:6px;'>");
                        string rangoFechas = "";
                        if (!string.IsNullOrEmpty(fechaDesde) && !string.IsNullOrEmpty(fechaHasta))
                            rangoFechas = $"Periodo: {DateTime.Parse(fechaDesde):dd/MM/yyyy} al {DateTime.Parse(fechaHasta):dd/MM/yyyy}";
                        else
                            rangoFechas = $"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}";
                        hw.Write(rangoFechas);
                        hw.Write("</td></tr>");

                        // Fila vacia
                        hw.Write("<tr><td colspan='18'></td></tr>");

                        // Headers grupo
                        hw.Write("<tr>");
                        string thStyle = "background-color:#0056b3;color:white;font-weight:bold;text-align:center;padding:6px;border:1px solid #004494;";
                        string thGroupStyle = "background-color:#003d82;color:white;font-weight:bold;text-align:center;padding:6px;border:1px solid #004494;";

                        hw.Write($"<td rowspan='2' style='{thStyle}'>NRO. FORMATO</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>FECHA</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>MOTIVO</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>TIPO VEHICULO</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>NOMBRE</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>PLACA TRACTO</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>RUTA</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>PRODUCTO</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>LUGAR ABASTECIMIENTO</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>HORA</td>");
                        hw.Write($"<td colspan='5' style='{thGroupStyle}'>GALONAJES</td>");
                        hw.Write($"<td colspan='2' style='{thGroupStyle}'>DISTANCIA</td>");
                        hw.Write($"<td rowspan='2' style='{thStyle}'>OBSERVACIONES</td>");
                        hw.Write("</tr>");

                        // Sub-headers
                        hw.Write("<tr>");
                        hw.Write($"<td style='{thStyle}'>GL RUTA ASIGNADA</td>");
                        hw.Write($"<td style='{thStyle}'>GL COMPRADOS EN RUTA</td>");
                        hw.Write($"<td style='{thStyle}'>GL TOTAL ABASTECIDOS</td>");
                        hw.Write($"<td style='{thStyle}'>GL AL FINALIZAR RUTA</td>");
                        hw.Write($"<td style='{thStyle}'>GL TOTAL CONSUMIDOS</td>");
                        hw.Write($"<td style='{thStyle}'>DISTANCIA KM</td>");
                        hw.Write($"<td style='{thStyle}'>CONSUMO COMPUTADOR</td>");
                        hw.Write("</tr>");

                        // Datos
                        string tdStyle = "border:1px solid #dee2e6;padding:4px;text-align:center;";
                        string tdLeftStyle = "border:1px solid #dee2e6;padding:4px;text-align:left;";

                        decimal sumGLAsignada = 0, sumGLComprados = 0, sumGLTotal = 0, sumGLFinal = 0, sumGLConsumidos = 0, sumKM = 0, sumConsumo = 0;

                        foreach (DataRow row in dt.Rows)
                        {
                            decimal glAsig = row["GLRutaAsignada"] != DBNull.Value ? Convert.ToDecimal(row["GLRutaAsignada"]) : 0;
                            decimal glComp = row["GLCompradosRuta"] != DBNull.Value ? Convert.ToDecimal(row["GLCompradosRuta"]) : 0;
                            decimal glTot = row["GLTotalAbastecidos"] != DBNull.Value ? Convert.ToDecimal(row["GLTotalAbastecidos"]) : 0;
                            decimal glFin = row["GLAlFinalizar"] != DBNull.Value ? Convert.ToDecimal(row["GLAlFinalizar"]) : 0;
                            decimal glCons = row["GLTotalConsumidos"] != DBNull.Value ? Convert.ToDecimal(row["GLTotalConsumidos"]) : 0;
                            decimal km = row["DistanciaKM"] != DBNull.Value ? Convert.ToDecimal(row["DistanciaKM"]) : 0;
                            decimal consumo = row["ConsumoComputador"] != DBNull.Value ? Convert.ToDecimal(row["ConsumoComputador"]) : 0;

                            sumGLAsignada += glAsig;
                            sumGLComprados += glComp;
                            sumGLTotal += glTot;
                            sumGLFinal += glFin;
                            sumGLConsumidos += glCons;
                            sumKM += km;
                            sumConsumo += consumo;

                            DateTime fecha = Convert.ToDateTime(row["Fecha"]);
                            string obs = row["Observaciones"].ToString().Replace("\"", "&quot;");

                            hw.Write("<tr>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["NumeroFormato"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{fecha:dd/MM/yyyy}</td>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["TipoAbastecimiento"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["TipoVehiculo"])}</td>");
                            hw.Write($"<td style='{tdLeftStyle}'>{HttpUtility.HtmlEncode(row["Conductor"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["PlacaTracto"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["Ruta"])}</td>");
                            hw.Write($"<td style='{tdLeftStyle}'>{HttpUtility.HtmlEncode(row["Producto"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["LugarAbastecimiento"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{HttpUtility.HtmlEncode(row["Hora"])}</td>");
                            hw.Write($"<td style='{tdStyle}'>{(glAsig != 0 ? glAsig.ToString("#,##0.##") : "")}</td>");
                            hw.Write($"<td style='{tdStyle}'>{(glComp != 0 ? glComp.ToString("#,##0.##") : "")}</td>");
                            hw.Write($"<td style='{tdStyle};font-weight:bold;'>{(glTot != 0 ? glTot.ToString("#,##0.##") : "")}</td>");
                            hw.Write($"<td style='{tdStyle}'>{(glFin != 0 ? glFin.ToString("#,##0.##") : "")}</td>");
                            hw.Write($"<td style='{tdStyle}'>{(glCons != 0 ? glCons.ToString("#,##0.##") : "")}</td>");
                            hw.Write($"<td style='{tdStyle}'>{(km != 0 ? km.ToString("#,##0.#") : "")}</td>");
                            hw.Write($"<td style='{tdStyle}'>{(consumo != 0 ? consumo.ToString("#,##0.#") : "")}</td>");
                            hw.Write($"<td style='{tdLeftStyle}font-size:9px;'>{HttpUtility.HtmlEncode(obs)}</td>");
                            hw.Write("</tr>");
                        }

                        // Fila de totales
                        string tfStyle = "background-color:#f0f7ff;border:1px solid #d1e7ff;padding:6px;text-align:center;font-weight:bold;color:#0056b3;";
                        hw.Write("<tr>");
                        hw.Write($"<td colspan='10' style='{tfStyle}text-align:right;'>TOTALES</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumGLAsignada:#,##0.##}</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumGLComprados:#,##0.##}</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumGLTotal:#,##0.##}</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumGLFinal:#,##0.##}</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumGLConsumidos:#,##0.##}</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumKM:#,##0.#}</td>");
                        hw.Write($"<td style='{tfStyle}'>{sumConsumo:#,##0.#}</td>");
                        hw.Write($"<td style='{tfStyle}'></td>");
                        hw.Write("</tr>");

                        hw.Write("</table>");
                    }

                    Response.Write(sw.ToString());
                }

                Response.Flush();
                Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Response.End() lanza ThreadAbortException - es comportamiento esperado
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al exportar Excel: " + ex.Message);
                ScriptManager.RegisterStartupScript(this, GetType(), "errorExport",
                    "alert('Error al exportar: " + ex.Message.Replace("'", "\\'" ).Replace("\r", "").Replace("\n", " ") + "');", true);
            }
        }

        #endregion

        #region GridView Events

        protected void gvReporte_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Columna Conductor (indice 4) - alineada a izquierda
                e.Row.Cells[4].CssClass = "td-left";
                // Columna Producto (indice 7)
                e.Row.Cells[7].CssClass = "td-left";
                // Columnas numericas
                for (int i = 10; i <= 16; i++)
                {
                    e.Row.Cells[i].CssClass = "td-num";
                }
                // Columna Observaciones (indice 17)
                if (e.Row.Cells.Count > 17)
                {
                    e.Row.Cells[17].CssClass = "td-obs";
                }
            }
        }

        #endregion

        #region Helpers de Formato

        protected string FormatTipoAbastecimiento(object tipo)
        {
            string val = tipo?.ToString() ?? "ABASTECIMIENTO";
            string cssClass = "motivo-abastecimiento";
            string icon = "🚛";

            switch (val.ToUpper())
            {
                case "VIAJE PROGRAMADO":
                    cssClass = "motivo-viaje";
                    icon = "📦";
                    break;
                case "MANTENIMIENTO":
                    cssClass = "motivo-mantenimiento";
                    icon = "🔧";
                    break;
                case "OTRO":
                    cssClass = "motivo-otro";
                    icon = "📋";
                    break;
            }

            return $"<span class=\"motivo-badge {cssClass}\">{icon} {HttpUtility.HtmlEncode(val)}</span>";
        }

        protected string FormatTipoVehiculo(object tipoVehiculo)
        {
            string val = tipoVehiculo?.ToString() ?? "N/A";
            string upper = val.ToUpper();
            string cssClass = "tipo-otro";

            if (upper.Contains("TRAILER"))
                cssClass = "tipo-trailer";
            else if (upper.Contains("CAMIONETA"))
                cssClass = "tipo-camioneta";
            else if (upper.Contains("CAMION") || upper.Contains("CAMION"))
                cssClass = "tipo-camion";

            return $"<span class=\"tipo-badge {cssClass}\">{HttpUtility.HtmlEncode(val)}</span>";
        }

        protected string FormatObservaciones(object observaciones)
        {
            string val = observaciones?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(val))
                return "<span style='color:#ccc;'>-</span>";

            string encoded = HttpUtility.HtmlEncode(val);
            // Truncar para la tabla, tooltip con texto completo
            if (encoded.Length > 80)
            {
                string truncated = encoded.Substring(0, 80) + "...";
                return $"<span title=\"{encoded}\" style=\"cursor:help;\">{truncated}</span>";
            }

            return encoded;
        }

        #endregion
    }
}
