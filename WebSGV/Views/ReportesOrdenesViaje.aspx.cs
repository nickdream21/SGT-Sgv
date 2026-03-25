using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Text;
using System.IO;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;

// Alias para resolver ambigüedades
using iTextParagraph = iTextSharp.text.Paragraph;
using iTextDocument = iTextSharp.text.Document;
using iTextFont = iTextSharp.text.Font;
using iTextPageSize = iTextSharp.text.PageSize;

namespace WebSGV.Views
{
    public partial class ReportesOrdenesViaje : System.Web.UI.Page
    {
        // Cadena de conexión
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            // ✅ PROCESAR EXPORTACIONES ANTES DE CUALQUIER COSA
            string action = Request.QueryString["action"];

            if (!string.IsNullOrEmpty(action))
            {
                switch (action)
                {
                    case "exportarLiquidaciones":
                        ExportarLiquidacionesExcel();
                        return; // ✅ Salir inmediatamente
                    case "exportarViajesActivos":
                        ExportarViajesActivosExcel();
                        return; // ✅ Salir inmediatamente
                    case "generarPDF":
                        GenerarPDFLiquidaciones();
                        return; // ✅ Salir inmediatamente
                }
            }

            if (!IsPostBack)
            {
                // Establecer fechas por defecto
                DateTime hoy = DateTime.Now;
                DateTime primerDia = new DateTime(hoy.Year, hoy.Month, 1);

                txtFechaDesde.Text = primerDia.ToString("yyyy-MM-dd");
                txtFechaHasta.Text = hoy.ToString("yyyy-MM-dd");
                txtFactorConversion.Text = "3.75";

                // Cargar datos iniciales
                CargarLiquidaciones();
                CargarViajesActivos();
            }
        }

        #region LIQUIDACIONES

        protected void btnBuscarLiquidaciones_Click(object sender, EventArgs e)
        {
            CargarLiquidaciones();
        }

        private void CargarLiquidaciones()
        {
            try
            {
                DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
                DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);

                DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);

                gvLiquidaciones.DataSource = dt;
                gvLiquidaciones.DataBind();

                // Actualizar contadores
                lblTotalRegistros.Text = dt.Rows.Count.ToString();
                lblTotalRegistrosTabla.Text = $"{dt.Rows.Count} registros";

                // Calcular y mostrar totales
                CalcularTotalesLiquidaciones(dt);
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar liquidaciones: {ex.Message}", "danger");
            }
        }

        private DataTable ObtenerLiquidaciones(DateTime fechaDesde, DateTime fechaHasta)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        c.DNI,
                        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
                        ov.fechaSalida AS FechaSalida,
                        ov.numeroOrdenViaje AS NumeroLiquidacion,
                        ov.idOrdenViaje AS IdOrdenViaje,
                        
                        -- ✅ DESCUENTOS Y REINTEGROS (lo que realmente importa ahora)
                        ISNULL((SELECT dr.descuentoSoles FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoSoles,
                        ISNULL((SELECT dr.descuentoDolares FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoDolares,
                        ISNULL((SELECT dr.reintegroSoles FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroSoles,
                        ISNULL((SELECT dr.reintegroDolares FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroDolares
                        
                    FROM OrdenViaje ov
                    INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                    WHERE ov.fechaSalida BETWEEN @FechaDesde AND @FechaHasta
                    AND ov.estadoViaje = 'COMPLETADO'
                    ORDER BY ov.fechaSalida DESC, c.nombre";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@FechaHasta", fechaHasta);

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }
            }

            // ✅ CALCULAR MONTOS FINALES (Reintegros - Descuentos)
            dt.Columns.Add("MontoSoles", typeof(decimal));
            dt.Columns.Add("MontoDolares", typeof(decimal));

            foreach (DataRow row in dt.Rows)
            {
                decimal descuentoSoles = Convert.ToDecimal(row["DescuentoSoles"]);
                decimal reintegroSoles = Convert.ToDecimal(row["ReintegroSoles"]);
                decimal descuentoDolares = Convert.ToDecimal(row["DescuentoDolares"]);
                decimal reintegroDolares = Convert.ToDecimal(row["ReintegroDolares"]);

                // Monto = Reintegro - Descuento
                row["MontoSoles"] = reintegroSoles - descuentoSoles;
                row["MontoDolares"] = reintegroDolares - descuentoDolares;
            }

            return dt;
        }

        private void CalcularTotalesLiquidaciones(DataTable dt)
        {
            decimal totalSoles = 0;
            decimal totalDolares = 0;
            decimal factorConversion = 3.75m;

            if (!string.IsNullOrEmpty(txtFactorConversion.Text))
            {
                decimal.TryParse(txtFactorConversion.Text, out factorConversion);
            }

            foreach (DataRow row in dt.Rows)
            {
                totalSoles += Convert.ToDecimal(row["MontoSoles"]);
                totalDolares += Convert.ToDecimal(row["MontoDolares"]);
            }

            decimal totalDolaresConvertido = totalDolares * factorConversion;
            decimal totalGeneral = totalSoles + totalDolaresConvertido;

            lblResumenTotalSoles.Text = $"S/ {totalSoles:N2}";
            lblResumenTotalDolares.Text = $"$ {totalDolares:N2}";
            lblResumenConversion.Text = $"S/ {totalDolaresConvertido:N2}";
            lblResumenTotal.Text = $"S/ {totalGeneral:N2}";
        }

        protected void gvLiquidaciones_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.Footer)
            {
                decimal totalSoles = 0;
                decimal totalDolares = 0;

                if (gvLiquidaciones.DataSource is DataTable dt)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        totalSoles += Convert.ToDecimal(row["MontoSoles"]);
                        totalDolares += Convert.ToDecimal(row["MontoDolares"]);
                    }
                }

                Label lblTotalSoles = (Label)e.Row.FindControl("lblTotalSoles");
                Label lblTotalDolares = (Label)e.Row.FindControl("lblTotalDolares");

                if (lblTotalSoles != null)
                    lblTotalSoles.Text = $"S/ {totalSoles:N2}";

                if (lblTotalDolares != null)
                    lblTotalDolares.Text = $"$ {totalDolares:N2}";
            }
        }

        // Funciones helper para formateo
        protected string ObtenerClaseMontoSoles(object monto)
        {
            if (monto == null || monto == DBNull.Value)
                return "monto-normal";

            decimal valor = Convert.ToDecimal(monto);

            if (valor < 0)
                return "monto-descuento"; // Rojo
            else if (valor > 0)
                return "monto-reintegro"; // Azul
            else
                return "monto-normal";
        }

        protected string FormatearMontoSoles(object monto)
        {
            if (monto == null || monto == DBNull.Value)
                return "S/ 0.00";

            decimal valor = Convert.ToDecimal(monto);

            if (valor < 0)
                return $"-S/ {Math.Abs(valor):N2}";
            else
                return $"S/ {valor:N2}";
        }

        protected string ObtenerClaseMontoDolares(object monto)
        {
            if (monto == null || monto == DBNull.Value)
                return "monto-normal";

            decimal valor = Convert.ToDecimal(monto);

            if (valor < 0)
                return "monto-descuento"; // Rojo
            else if (valor > 0)
                return "monto-reintegro"; // Azul
            else
                return "monto-normal";
        }

        protected string FormatearMontoDolares(object monto)
        {
            if (monto == null || monto == DBNull.Value)
                return "$ 0.00";

            decimal valor = Convert.ToDecimal(monto);

            if (valor < 0)
                return $"-$ {Math.Abs(valor):N2}";
            else
                return $"$ {valor:N2}";
        }

        #endregion

        #region VIAJES ACTIVOS

        protected void btnBuscarViajesActivos_Click(object sender, EventArgs e)
        {
            CargarViajesActivos();
        }

        private void CargarViajesActivos()
        {
            try
            {
                string buscarConductor = txtBuscarConductor.Text.Trim();
                string estadoViaje = ddlEstadoViaje.SelectedValue;

                DataTable dt = ObtenerViajesActivos(buscarConductor, estadoViaje);

                gvViajesActivos.DataSource = dt;
                gvViajesActivos.DataBind();

                // Actualizar contadores
                lblTotalViajesActivos.Text = $"{dt.Rows.Count} conductores";
                lblCountViajesActivos.Text = dt.Rows.Count.ToString();
                lblCountViajesActivosTabla.Text = $"{dt.Rows.Count} viajes";
            }
            catch (Exception ex)
            {
                MostrarMensaje($"Error al cargar viajes activos: {ex.Message}", "danger");
            }
        }

        private DataTable ObtenerViajesActivos(string buscarConductor, string estadoViaje)
        {
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                StringBuilder query = new StringBuilder(@"
                    SELECT 
                        c.DNI,
                        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
                        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
                        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
                        ISNULL(MAX(cl.nombre), 'N/A') AS Cliente,
                        MAX(d.fechaDespacho) AS FechaProgramacion,
                        ISNULL(MAX(d.lugarOperacion), 'N/A') AS Destino,
                        vp.fechaInicio AS FechaInicio,
                        DATEDIFF(DAY, vp.fechaInicio, GETDATE()) AS DiasEnViaje,
                        vp.estadoViaje AS Estado,
                        vp.idViajeProgreso AS IdViaje,
                        MAX(d.numeroDespacho) AS NumeroDespacho
                    FROM ViajesEnProgreso vp
                    INNER JOIN Conductor c ON c.idConductor = (
                        SELECT TOP 1 idConductor FROM Despachos
                        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
                        GROUP BY idConductor ORDER BY COUNT(*) DESC
                    )
                    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
                    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
                    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
                    WHERE vp.estadoViaje IN ('ABIERTO')
                    AND vp.activo = 1
                    AND NOT EXISTS (
                        SELECT 1 FROM OrdenViaje ov 
                        WHERE ov.idViajeProgreso = vp.idViajeProgreso
                    )");

                if (!string.IsNullOrEmpty(buscarConductor))
                {
                    query.Append(" AND (c.nombre LIKE @BuscarConductor OR c.apPaterno LIKE @BuscarConductor OR c.DNI LIKE @BuscarConductor)");
                }

                if (!string.IsNullOrEmpty(estadoViaje) && estadoViaje != "TODOS")
                {
                    query.Append(" AND vp.estadoViaje = @EstadoViaje");
                }

                query.Append(" GROUP BY c.DNI, c.nombre, c.apPaterno, c.apMaterno, vp.fechaInicio, vp.estadoViaje, vp.idViajeProgreso");
                query.Append(" ORDER BY vp.fechaInicio DESC");

                using (SqlCommand cmd = new SqlCommand(query.ToString(), conn))
                {
                    if (!string.IsNullOrEmpty(buscarConductor))
                    {
                        cmd.Parameters.AddWithValue("@BuscarConductor", $"%{buscarConductor}%");
                    }

                    if (!string.IsNullOrEmpty(estadoViaje) && estadoViaje != "TODOS")
                    {
                        cmd.Parameters.AddWithValue("@EstadoViaje", estadoViaje);
                    }

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }
            }

            return dt;
        }

        protected void gvViajesActivos_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            // Lógica adicional si es necesaria
        }

        protected string ObtenerClaseEstado(object estado)
        {
            if (estado == null || estado == DBNull.Value)
                return "badge-estado-activo";

            string estadoStr = estado.ToString();

            if (estadoStr == "ABIERTO")
                return "badge-estado-activo";
            else if (estadoStr == "CERRADO")
                return "badge-estado-cerrado";
            else
                return "badge-estado-activo";
        }

        #endregion

        #region EXPORTACIÓN EXCEL

        private void ExportarLiquidacionesExcel()
        {
            try
            {
                DateTime fechaDesde = DateTime.Parse(Request.QueryString["fechaDesde"]);
                DateTime fechaHasta = DateTime.Parse(Request.QueryString["fechaHasta"]);
                decimal factorConversion = 3.75m;
                if (!string.IsNullOrEmpty(Request.QueryString["factor"]))
                    decimal.TryParse(Request.QueryString["factor"], out factorConversion);

                DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);

                byte[] excelBytes;
                using (XLWorkbook wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Liquidaciones");

                    ws.Cell(1, 1).Value = "SISTEMA SGV";
                    ws.Range(1, 1, 1, 6).Merge();
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Font.FontSize = 14;
                    ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af");
                    ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Row(1).Height = 30;

                    ws.Cell(2, 1).Value = "REPORTE DE LIQUIDACIONES (Descuentos y Reintegros)";
                    ws.Range(2, 1, 2, 6).Merge();
                    ws.Cell(2, 1).Style.Font.Bold = true;
                    ws.Cell(2, 1).Style.Font.FontSize = 12;
                    ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#1e293b");
                    ws.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
                    ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(3, 1).Value = $"Período: {fechaDesde:dd/MM/yyyy} - {fechaHasta:dd/MM/yyyy}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    ws.Range(3, 1, 3, 6).Merge();
                    ws.Cell(3, 1).Style.Font.Italic = true;
                    ws.Cell(3, 1).Style.Font.FontSize = 10;
                    ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");
                    ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Encabezados
                    string[] headers = { "DNI", "CONDUCTOR", "FECHA", "N° DE LIQ", "MONTO S/ (Reintegro-Descuento)", "MONTO $ (Reintegro-Descuento)" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        ws.Cell(5, i + 1).Value = headers[i];
                    }

                    var headerRange = ws.Range(5, 1, 5, 6);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontSize = 10;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#334155");
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#1e293b");
                    ws.Row(5).Height = 25;

                    int row = 6;
                    decimal totalSoles = 0;
                    decimal totalDolares = 0;
                    bool isAlternate = false;

                    foreach (DataRow dr in dt.Rows)
                    {
                        ws.Cell(row, 1).Value = dr["DNI"].ToString();
                        ws.Cell(row, 2).Value = dr["Conductor"].ToString();
                        ws.Cell(row, 3).Value = Convert.ToDateTime(dr["FechaSalida"]).ToString("dd/MM/yyyy");
                        ws.Cell(row, 4).Value = dr["NumeroLiquidacion"].ToString();

                        decimal montoSoles = Convert.ToDecimal(dr["MontoSoles"]);
                        decimal montoDolares = Convert.ToDecimal(dr["MontoDolares"]);

                        ws.Cell(row, 5).Value = montoSoles;
                        ws.Cell(row, 6).Value = montoDolares;
                        ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                        if (montoSoles < 0)
                            ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                        else if (montoSoles > 0)
                            ws.Cell(row, 5).Style.Font.FontColor = XLColor.FromHtml("#2563eb");

                        if (montoDolares < 0)
                            ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                        else if (montoDolares > 0)
                            ws.Cell(row, 6).Style.Font.FontColor = XLColor.FromHtml("#2563eb");

                        // Filas alternadas
                        if (isAlternate)
                        {
                            ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                        }
                        isAlternate = !isAlternate;

                        // Bordes
                        ws.Range(row, 1, row, 6).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                        ws.Range(row, 1, row, 6).Style.Border.BottomBorderColor = XLColor.FromHtml("#e2e8f0");

                        totalSoles += montoSoles;
                        totalDolares += montoDolares;
                        row++;
                    }

                    // Fila de totales
                    var totalRange = ws.Range(row, 1, row, 6);
                    totalRange.Style.Font.Bold = true;
                    totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");
                    totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                    totalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#334155");
                    totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                    totalRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#334155");

                    ws.Cell(row, 4).Value = "TOTAL:";
                    ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(row, 5).Value = totalSoles;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(row, 6).Value = totalDolares;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    // Resumen
                    row += 2;
                    ws.Cell(row, 1).Value = "RESUMEN TOTAL EN SOLES";
                    ws.Range(row, 1, row, 6).Merge();
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 12;
                    ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Row(row).Height = 25;
                    row++;

                    // Resumen filas
                    string[][] resumenData = new string[][]
                    {
                        new[] { "Factor de Conversión ($ a S/):", factorConversion.ToString("0.00"), "#fef3c7" },
                        new[] { "Total en Soles (S/):", $"S/ {totalSoles:N2}", "#f0fdf4" },
                        new[] { "Total en Dólares ($):", $"$ {totalDolares:N2}", "#f0fdf4" },
                        new[] { "Conversión a Soles ($ × Factor):", $"S/ {(totalDolares * factorConversion):N2}", "#ecfeff" }
                    };

                    foreach (var item in resumenData)
                    {
                        ws.Cell(row, 1).Value = item[0];
                        ws.Cell(row, 1).Style.Font.Bold = true;
                        ws.Range(row, 1, row, 4).Merge();
                        ws.Cell(row, 5).Value = item[1];
                        ws.Cell(row, 5).Style.Font.Bold = true;
                        ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        ws.Range(row, 5, row, 6).Merge();
                        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml(item[2]);
                        ws.Range(row, 1, row, 6).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                        ws.Range(row, 1, row, 6).Style.Border.BottomBorderColor = XLColor.FromHtml("#cbd5e1");
                        row++;
                    }

                    decimal totalDolaresConvertido = totalDolares * factorConversion;
                    decimal totalGeneral = totalSoles + totalDolaresConvertido;
                    ws.Cell(row, 1).Value = "TOTAL GENERAL EN SOLES:";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 12;
                    ws.Range(row, 1, row, 4).Merge();
                    ws.Cell(row, 5).Value = $"S/ {totalGeneral:N2}";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 5).Style.Font.FontSize = 12;
                    ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Range(row, 5, row, 6).Merge();
                    ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#d1fae5");
                    ws.Range(row, 1, row, 6).Style.Border.TopBorder = XLBorderStyleValues.Medium;
                    ws.Range(row, 1, row, 6).Style.Border.TopBorderColor = XLColor.FromHtml("#059669");
                    ws.Range(row, 1, row, 6).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                    ws.Range(row, 1, row, 6).Style.Border.BottomBorderColor = XLColor.FromHtml("#059669");

                    // Leyenda de colores
                    row += 2;
                    ws.Cell(row, 1).Value = "Leyenda: Rojo = Descuento Neto  |  Azul = Reintegro Neto  |  Negro = Cero";
                    ws.Range(row, 1, row, 6).Merge();
                    ws.Cell(row, 1).Style.Font.Italic = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 9;
                    ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");

                    ws.Columns().AdjustToContents();
                    ws.Column(2).Width = 30;
                    ws.Column(5).Width = 22;
                    ws.Column(6).Width = 22;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        wb.SaveAs(ms);
                        excelBytes = ms.ToArray();
                    }
                }

                HttpContext.Current.Response.Clear();
                HttpContext.Current.Response.ClearContent();
                HttpContext.Current.Response.ClearHeaders();
                HttpContext.Current.Response.Buffer = true;
                HttpContext.Current.Response.Charset = "";
                HttpContext.Current.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                HttpContext.Current.Response.AddHeader("Content-Disposition", $"attachment; filename=Liquidaciones_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                HttpContext.Current.Response.AddHeader("Content-Length", excelBytes.Length.ToString());
                HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                HttpContext.Current.Response.BinaryWrite(excelBytes);
                HttpContext.Current.Response.Flush();
                HttpContext.Current.Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Normal - Response.End() causa esto
            }
            catch (Exception ex)
            {
                // NO tocar Response aquí - solo logear el error
                System.Diagnostics.Debug.WriteLine($"Error exportando Excel: {ex.Message}");
                throw; // Re-lanzar para que lo maneje ASP.NET
            }
        }

        private void ExportarViajesActivosExcel()
        {
            try
            {
                string buscarConductor = Request.QueryString["buscarConductor"] ?? "";
                string estadoViaje = Request.QueryString["estadoViaje"] ?? "";
                DataTable dt = ObtenerViajesActivos(buscarConductor, estadoViaje);

                byte[] excelBytes;
                using (XLWorkbook wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Viajes Activos");

                    ws.Cell(1, 1).Value = "SISTEMA SGV";
                    ws.Range(1, 1, 1, 9).Merge();
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Font.FontSize = 14;
                    ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#92400e");
                    ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Row(1).Height = 30;

                    ws.Cell(2, 1).Value = "REPORTE DE VIAJES ACTIVOS SIN LIQUIDACIÓN";
                    ws.Range(2, 1, 2, 9).Merge();
                    ws.Cell(2, 1).Style.Font.Bold = true;
                    ws.Cell(2, 1).Style.Font.FontSize = 12;
                    ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#1e293b");
                    ws.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#fef3c7");
                    ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(3, 1).Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    ws.Range(3, 1, 3, 9).Merge();
                    ws.Cell(3, 1).Style.Font.Italic = true;
                    ws.Cell(3, 1).Style.Font.FontSize = 10;
                    ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");
                    ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    string[] vHeaders = { "DNI", "CONDUCTOR", "TRACTO", "CARRETA", "CLIENTE", "DESTINO", "FECHA PROGRAMACIÓN", "DÍAS EN VIAJE", "ESTADO" };
                    for (int i = 0; i < vHeaders.Length; i++)
                    {
                        ws.Cell(5, i + 1).Value = vHeaders[i];
                    }

                    var headerRange = ws.Range(5, 1, 5, 9);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Font.FontSize = 10;
                    headerRange.Style.Font.FontColor = XLColor.White;
                    headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#334155");
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#1e293b");
                    ws.Row(5).Height = 25;

                    int row = 6;
                    bool isAlternate = false;
                    foreach (DataRow dr in dt.Rows)
                    {
                        ws.Cell(row, 1).Value = dr["DNI"] != DBNull.Value ? dr["DNI"].ToString() : "";
                        ws.Cell(row, 2).Value = dr["Conductor"] != DBNull.Value ? dr["Conductor"].ToString() : "";
                        ws.Cell(row, 3).Value = dr["PlacaTracto"] != DBNull.Value ? dr["PlacaTracto"].ToString() : "N/A";
                        ws.Cell(row, 4).Value = dr["PlacaCarreta"] != DBNull.Value ? dr["PlacaCarreta"].ToString() : "N/A";
                        ws.Cell(row, 5).Value = dr["Cliente"] != DBNull.Value ? dr["Cliente"].ToString() : "N/A";
                        ws.Cell(row, 6).Value = dr["Destino"] != DBNull.Value ? dr["Destino"].ToString() : "N/A";
                        ws.Cell(row, 7).Value = dr["FechaProgramacion"] != DBNull.Value ? Convert.ToDateTime(dr["FechaProgramacion"]).ToString("dd/MM/yyyy") : "N/A";
                        ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        int diasViaje = dr["DiasEnViaje"] != DBNull.Value ? Convert.ToInt32(dr["DiasEnViaje"]) : 0;
                        ws.Cell(row, 8).Value = diasViaje;
                        ws.Cell(row, 8).Style.NumberFormat.Format = "0";
                        ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        if (diasViaje > 7)
                        {
                            ws.Cell(row, 8).Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                            ws.Cell(row, 8).Style.Font.Bold = true;
                        }

                        ws.Cell(row, 9).Value = dr["Estado"] != DBNull.Value ? dr["Estado"].ToString() : "N/A";
                        ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        if (isAlternate)
                        {
                            ws.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                        }
                        isAlternate = !isAlternate;

                        ws.Range(row, 1, row, 9).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                        ws.Range(row, 1, row, 9).Style.Border.BottomBorderColor = XLColor.FromHtml("#e2e8f0");

                        row++;
                    }

                    // Total
                    var totalRange = ws.Range(row, 1, row, 9);
                    totalRange.Style.Font.Bold = true;
                    totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#fef3c7");
                    totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                    totalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#d97706");
                    totalRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                    totalRange.Style.Border.BottomBorderColor = XLColor.FromHtml("#d97706");

                    ws.Cell(row, 1).Value = "TOTAL DE VIAJES SIN LIQUIDACIÓN:";
                    ws.Range(row, 1, row, 7).Merge();
                    ws.Cell(row, 8).Value = dt.Rows.Count;
                    ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Range(row, 8, row, 9).Merge();

                    ws.Columns().AdjustToContents();
                    ws.Column(2).Width = 30;
                    ws.Column(5).Width = 20;
                    ws.Column(6).Width = 20;

                    using (MemoryStream ms = new MemoryStream())
                    {
                        wb.SaveAs(ms);
                        excelBytes = ms.ToArray();
                    }
                }

                HttpContext.Current.Response.Clear();
                HttpContext.Current.Response.ClearContent();
                HttpContext.Current.Response.ClearHeaders();
                HttpContext.Current.Response.Buffer = true;
                HttpContext.Current.Response.Charset = "";
                HttpContext.Current.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                HttpContext.Current.Response.AddHeader("Content-Disposition", $"attachment; filename=ViajesActivos_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                HttpContext.Current.Response.AddHeader("Content-Length", excelBytes.Length.ToString());
                HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                HttpContext.Current.Response.BinaryWrite(excelBytes);
                HttpContext.Current.Response.Flush();
                HttpContext.Current.Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Normal
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region EXPORTACIÓN PDF

        private void GenerarPDFLiquidaciones()
        {
            try
            {
                DateTime fechaDesde = DateTime.Parse(Request.QueryString["fechaDesde"]);
                DateTime fechaHasta = DateTime.Parse(Request.QueryString["fechaHasta"]);
                decimal factorConversion = 3.75m;
                if (!string.IsNullOrEmpty(Request.QueryString["factor"]))
                    decimal.TryParse(Request.QueryString["factor"], out factorConversion);

                DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);

                byte[] pdfBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    iTextDocument document = new iTextDocument(iTextPageSize.A4.Rotate(), 25, 25, 30, 30);
                    PdfWriter writer = PdfWriter.GetInstance(document, ms);
                    writer.CloseStream = false;

                    document.Open();

                    // Colores del tema
                    BaseColor headerBg = new BaseColor(30, 41, 59);   // slate-800
                    BaseColor headerText = BaseColor.WHITE;
                    BaseColor altRow = new BaseColor(248, 250, 252);   // slate-50
                    BaseColor borderColor = new BaseColor(226, 232, 240); // slate-200
                    BaseColor accentGreen = new BaseColor(5, 150, 105); // emerald-600
                    BaseColor accentBlue = new BaseColor(30, 64, 175);  // blue-800
                    BaseColor redColor = new BaseColor(220, 38, 38);    // red-600
                    BaseColor blueColor = new BaseColor(37, 99, 235);   // blue-600

                    // Header del documento
                    PdfPTable headerTable = new PdfPTable(1);
                    headerTable.WidthPercentage = 100;
                    PdfPCell brandCell = new PdfPCell(new Phrase("SISTEMA SGV", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, headerText)));
                    brandCell.BackgroundColor = accentBlue;
                    brandCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    brandCell.Padding = 12;
                    brandCell.Border = 0;
                    headerTable.AddCell(brandCell);
                    document.Add(headerTable);

                    iTextFont titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(30, 41, 59));
                    iTextParagraph title = new iTextParagraph("REPORTE DE LIQUIDACIONES (Descuentos y Reintegros)", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingBefore = 12;
                    document.Add(title);

                    iTextFont subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(100, 116, 139));
                    iTextParagraph subtitle = new iTextParagraph($"Período: {fechaDesde:dd/MM/yyyy} - {fechaHasta:dd/MM/yyyy}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm}", subtitleFont);
                    subtitle.Alignment = Element.ALIGN_CENTER;
                    subtitle.SpacingAfter = 15;
                    document.Add(subtitle);

                    // Línea separadora
                    PdfPTable lineTable = new PdfPTable(1);
                    lineTable.WidthPercentage = 100;
                    PdfPCell lineCell = new PdfPCell();
                    lineCell.Border = PdfPCell.BOTTOM_BORDER;
                    lineCell.BorderColor = borderColor;
                    lineCell.BorderWidth = 2;
                    lineCell.FixedHeight = 5;
                    lineTable.AddCell(lineCell);
                    lineTable.SpacingAfter = 15;
                    document.Add(lineTable);

                    // Tabla principal
                    PdfPTable table = new PdfPTable(6);
                    table.WidthPercentage = 100;
                    table.SetWidths(new float[] { 12f, 28f, 12f, 13f, 17.5f, 17.5f });

                    iTextFont headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, headerText);
                    string[] headers = { "DNI", "CONDUCTOR", "FECHA", "N° DE LIQ", "MONTO S/ (Reint.-Desc.)", "MONTO $ (Reint.-Desc.)" };
                    foreach (string header in headers)
                    {
                        PdfPCell hCell = new PdfPCell(new Phrase(header, headerFont));
                        hCell.BackgroundColor = headerBg;
                        hCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        hCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        hCell.Padding = 8;
                        hCell.BorderColor = headerBg;
                        table.AddCell(hCell);
                    }

                    iTextFont dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                    decimal totalSoles = 0;
                    decimal totalDolares = 0;
                    bool isAlt = false;

                    foreach (DataRow dr in dt.Rows)
                    {
                        BaseColor rowBg = isAlt ? altRow : BaseColor.WHITE;

                        PdfPCell c1 = new PdfPCell(new Phrase(dr["DNI"].ToString(), dataFont));
                        c1.HorizontalAlignment = Element.ALIGN_CENTER;
                        c1.Padding = 6;
                        c1.BackgroundColor = rowBg;
                        c1.BorderColor = borderColor;
                        table.AddCell(c1);

                        PdfPCell c2 = new PdfPCell(new Phrase(dr["Conductor"].ToString(), dataFont));
                        c2.Padding = 6;
                        c2.BackgroundColor = rowBg;
                        c2.BorderColor = borderColor;
                        table.AddCell(c2);

                        PdfPCell c3 = new PdfPCell(new Phrase(Convert.ToDateTime(dr["FechaSalida"]).ToString("dd/MM/yyyy"), dataFont));
                        c3.HorizontalAlignment = Element.ALIGN_CENTER;
                        c3.Padding = 6;
                        c3.BackgroundColor = rowBg;
                        c3.BorderColor = borderColor;
                        table.AddCell(c3);

                        PdfPCell c4 = new PdfPCell(new Phrase(dr["NumeroLiquidacion"].ToString(), dataFont));
                        c4.HorizontalAlignment = Element.ALIGN_CENTER;
                        c4.Padding = 6;
                        c4.BackgroundColor = rowBg;
                        c4.BorderColor = borderColor;
                        table.AddCell(c4);

                        decimal montoSoles = Convert.ToDecimal(dr["MontoSoles"]);
                        decimal montoDolares = Convert.ToDecimal(dr["MontoDolares"]);

                        // Color coding for amounts
                        BaseColor solesColor = montoSoles < 0 ? redColor : (montoSoles > 0 ? blueColor : new BaseColor(30, 41, 59));
                        BaseColor dolaresColor = montoDolares < 0 ? redColor : (montoDolares > 0 ? blueColor : new BaseColor(30, 41, 59));

                        iTextFont solesFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, solesColor);
                        iTextFont dolaresFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, dolaresColor);

                        string solesText = montoSoles < 0 ? $"-S/ {Math.Abs(montoSoles):N2}" : $"S/ {montoSoles:N2}";
                        string dolaresText = montoDolares < 0 ? $"-$ {Math.Abs(montoDolares):N2}" : $"$ {montoDolares:N2}";

                        PdfPCell c5 = new PdfPCell(new Phrase(solesText, solesFont));
                        c5.HorizontalAlignment = Element.ALIGN_RIGHT;
                        c5.Padding = 6;
                        c5.BackgroundColor = rowBg;
                        c5.BorderColor = borderColor;
                        table.AddCell(c5);

                        PdfPCell c6 = new PdfPCell(new Phrase(dolaresText, dolaresFont));
                        c6.HorizontalAlignment = Element.ALIGN_RIGHT;
                        c6.Padding = 6;
                        c6.BackgroundColor = rowBg;
                        c6.BorderColor = borderColor;
                        table.AddCell(c6);

                        totalSoles += montoSoles;
                        totalDolares += montoDolares;
                        isAlt = !isAlt;
                    }

                    // Fila de totales
                    iTextFont totalFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(30, 41, 59));
                    BaseColor totalBg = new BaseColor(241, 245, 249); // slate-100

                    PdfPCell totalLabelCell = new PdfPCell(new Phrase("TOTAL:", totalFont));
                    totalLabelCell.Colspan = 4;
                    totalLabelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    totalLabelCell.Padding = 8;
                    totalLabelCell.BackgroundColor = totalBg;
                    totalLabelCell.BorderColor = headerBg;
                    totalLabelCell.BorderWidthTop = 2;
                    totalLabelCell.BorderWidthBottom = 2;
                    table.AddCell(totalLabelCell);

                    PdfPCell totalSolesCell = new PdfPCell(new Phrase($"S/ {totalSoles:N2}", totalFont));
                    totalSolesCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    totalSolesCell.Padding = 8;
                    totalSolesCell.BackgroundColor = totalBg;
                    totalSolesCell.BorderColor = headerBg;
                    totalSolesCell.BorderWidthTop = 2;
                    totalSolesCell.BorderWidthBottom = 2;
                    table.AddCell(totalSolesCell);

                    PdfPCell totalDolaresCell = new PdfPCell(new Phrase($"$ {totalDolares:N2}", totalFont));
                    totalDolaresCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    totalDolaresCell.Padding = 8;
                    totalDolaresCell.BackgroundColor = totalBg;
                    totalDolaresCell.BorderColor = headerBg;
                    totalDolaresCell.BorderWidthTop = 2;
                    totalDolaresCell.BorderWidthBottom = 2;
                    table.AddCell(totalDolaresCell);

                    document.Add(table);
                    document.Add(new iTextParagraph("\n"));

                    // Resumen
                    iTextFont resumenTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE);
                    PdfPTable resumenHeaderTable = new PdfPTable(1);
                    resumenHeaderTable.WidthPercentage = 60;
                    resumenHeaderTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    PdfPCell resumenHeaderCell = new PdfPCell(new Phrase("RESUMEN TOTAL EN SOLES", resumenTitleFont));
                    resumenHeaderCell.BackgroundColor = accentGreen;
                    resumenHeaderCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    resumenHeaderCell.Padding = 10;
                    resumenHeaderCell.Border = 0;
                    resumenHeaderTable.AddCell(resumenHeaderCell);
                    resumenHeaderTable.SpacingAfter = 5;
                    document.Add(resumenHeaderTable);

                    PdfPTable resumenTable = new PdfPTable(2);
                    resumenTable.WidthPercentage = 60;
                    resumenTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    resumenTable.SetWidths(new float[] { 60f, 40f });

                    iTextFont resumenLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, new BaseColor(30, 41, 59));
                    iTextFont resumenValueFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(30, 41, 59));
                    iTextFont resumenTotalValueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, accentGreen);

                    BaseColor rowLight1 = new BaseColor(254, 243, 199); // amber-100
                    BaseColor rowLight2 = new BaseColor(240, 253, 244); // green-50
                    BaseColor rowLight3 = new BaseColor(236, 254, 255); // cyan-50
                    BaseColor rowTotal = new BaseColor(209, 250, 229);  // emerald-100

                    PdfPCell rlc1 = new PdfPCell(new Phrase("Factor de Conversión ($ a S/):", resumenLabelFont)) { BackgroundColor = rowLight1, Padding = 8, BorderColor = borderColor };
                    PdfPCell rvc1 = new PdfPCell(new Phrase(factorConversion.ToString("0.00"), resumenValueFont)) { BackgroundColor = rowLight1, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8, BorderColor = borderColor };
                    resumenTable.AddCell(rlc1);
                    resumenTable.AddCell(rvc1);

                    PdfPCell rlc2 = new PdfPCell(new Phrase("Total en Soles (S/):", resumenLabelFont)) { BackgroundColor = rowLight2, Padding = 8, BorderColor = borderColor };
                    PdfPCell rvc2 = new PdfPCell(new Phrase($"S/ {totalSoles:N2}", resumenValueFont)) { BackgroundColor = rowLight2, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8, BorderColor = borderColor };
                    resumenTable.AddCell(rlc2);
                    resumenTable.AddCell(rvc2);

                    PdfPCell rlc3 = new PdfPCell(new Phrase("Total en Dólares ($):", resumenLabelFont)) { BackgroundColor = rowLight2, Padding = 8, BorderColor = borderColor };
                    PdfPCell rvc3 = new PdfPCell(new Phrase($"$ {totalDolares:N2}", resumenValueFont)) { BackgroundColor = rowLight2, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8, BorderColor = borderColor };
                    resumenTable.AddCell(rlc3);
                    resumenTable.AddCell(rvc3);

                    decimal totalDolaresConvertido = totalDolares * factorConversion;
                    PdfPCell rlc4 = new PdfPCell(new Phrase("Conversión a Soles ($ × Factor):", resumenLabelFont)) { BackgroundColor = rowLight3, Padding = 8, BorderColor = borderColor };
                    PdfPCell rvc4 = new PdfPCell(new Phrase($"S/ {totalDolaresConvertido:N2}", resumenValueFont)) { BackgroundColor = rowLight3, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 8, BorderColor = borderColor };
                    resumenTable.AddCell(rlc4);
                    resumenTable.AddCell(rvc4);

                    decimal totalGeneral = totalSoles + totalDolaresConvertido;
                    PdfPCell rlc5 = new PdfPCell(new Phrase("TOTAL GENERAL EN SOLES:", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(30, 41, 59)))) { BackgroundColor = rowTotal, Padding = 10, BorderColor = accentGreen, BorderWidthTop = 2 };
                    PdfPCell rvc5 = new PdfPCell(new Phrase($"S/ {totalGeneral:N2}", resumenTotalValueFont)) { BackgroundColor = rowTotal, HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 10, BorderColor = accentGreen, BorderWidthTop = 2 };
                    resumenTable.AddCell(rlc5);
                    resumenTable.AddCell(rvc5);

                    document.Add(resumenTable);

                    // Leyenda
                    iTextFont legendFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 8, new BaseColor(100, 116, 139));
                    iTextParagraph legend = new iTextParagraph("Leyenda: Rojo = Descuento Neto  |  Azul = Reintegro Neto  |  Negro = Cero", legendFont);
                    legend.Alignment = Element.ALIGN_CENTER;
                    legend.SpacingBefore = 15;
                    document.Add(legend);

                    document.Close();
                    writer.Close();

                    pdfBytes = ms.ToArray();
                }

                HttpContext.Current.Response.Clear();
                HttpContext.Current.Response.ClearContent();
                HttpContext.Current.Response.ClearHeaders();
                HttpContext.Current.Response.Buffer = true;
                HttpContext.Current.Response.Charset = "";
                HttpContext.Current.Response.ContentType = "application/pdf";
                HttpContext.Current.Response.AddHeader("Content-Disposition", $"attachment; filename=Liquidaciones_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                HttpContext.Current.Response.AddHeader("Content-Length", pdfBytes.Length.ToString());
                HttpContext.Current.Response.Cache.SetCacheability(HttpCacheability.NoCache);
                HttpContext.Current.Response.BinaryWrite(pdfBytes);
                HttpContext.Current.Response.Flush();
                HttpContext.Current.Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Normal
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region DETALLE DE ORDEN (AJAX)

        [WebMethod]
        public static string ObtenerDetalleOrden(int idOrden)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                StringBuilder html = new StringBuilder();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // ✅ Obtener información general de la orden
                    string queryGeneral = @"
                        SELECT 
                            ov.numeroOrdenViaje,
                            ov.fechaSalida,
                            ov.fechaLlegada,
                            ov.horaSalida,
                            ov.horaLlegada,
                            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
                            t.placaTracto AS PlacaTracto,
                            ISNULL(ca.placaCarreta, 'N/A') AS PlacaCarreta,
                            ov.observaciones
                        FROM OrdenViaje ov
                        INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                        LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
                        LEFT JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                        WHERE ov.idOrdenViaje = @IdOrden";

                    SqlCommand cmdGeneral = new SqlCommand(queryGeneral, conn);
                    cmdGeneral.Parameters.AddWithValue("@IdOrden", idOrden);

                    SqlDataReader reader = cmdGeneral.ExecuteReader();

                    if (reader.Read())
                    {
                        string numeroOrden = reader["numeroOrdenViaje"].ToString();

                        html.Append("<div class='orden-detalle'>");

                        // Información general
                        html.Append("<div class='card mb-3'>");
                        html.Append("<div class='card-header bg-primary text-white'><h6 class='mb-0'>Información General</h6></div>");
                        html.Append("<div class='card-body'>");
                        html.Append("<div class='row'>");
                        html.Append($"<div class='col-md-3'><strong>N° Orden:</strong> {reader["numeroOrdenViaje"]}</div>");
                        html.Append($"<div class='col-md-3'><strong>Conductor:</strong> {reader["Conductor"]}</div>");
                        html.Append($"<div class='col-md-3'><strong>Tracto:</strong> {reader["PlacaTracto"]}</div>");
                        html.Append($"<div class='col-md-3'><strong>Carreta:</strong> {reader["PlacaCarreta"]}</div>");
                        html.Append("</div>");
                        html.Append("<div class='row mt-2'>");
                        html.Append($"<div class='col-md-3'><strong>Fecha Salida:</strong> {Convert.ToDateTime(reader["fechaSalida"]):dd/MM/yyyy}</div>");
                        html.Append($"<div class='col-md-3'><strong>Fecha Llegada:</strong> {Convert.ToDateTime(reader["fechaLlegada"]):dd/MM/yyyy}</div>");
                        html.Append($"<div class='col-md-3'><strong>Hora Salida:</strong> {reader["horaSalida"]}</div>");
                        html.Append($"<div class='col-md-3'><strong>Hora Llegada:</strong> {reader["horaLlegada"]}</div>");
                        html.Append("</div>");
                        html.Append("</div>");
                        html.Append("</div>");

                        reader.Close();

                        // ✅ INGRESOS
                        html.Append(ObtenerSeccionIngresos(conn, numeroOrden));

                        // ✅ GASTOS
                        html.Append(ObtenerSeccionGastos(conn, numeroOrden));

                        // ✅ BALANCE FINAL
                        html.Append(ObtenerBalanceFinal(conn, numeroOrden));

                        html.Append("</div>");
                    }
                    else
                    {
                        reader.Close();
                        html.Append("<div class='alert alert-warning'>No se encontró información de la orden</div>");
                    }
                }

                return html.ToString();
            }
            catch (Exception ex)
            {
                return $"<div class='alert alert-danger'>Error: {ex.Message}</div>";
            }
        }

        private static string ObtenerSeccionIngresos(SqlConnection conn, string numeroOrden)
        {
            StringBuilder html = new StringBuilder();
            decimal totalIngresosSoles = 0;
            decimal totalIngresosDolares = 0;

            html.Append("<div class='card mb-3'>");
            html.Append("<div class='card-header bg-success text-white'><h6 class='mb-0'>Ingresos</h6></div>");
            html.Append("<div class='card-body'>");
            html.Append("<table class='table table-sm'>");
            html.Append("<thead><tr><th>Concepto</th><th>Descripción</th><th>Soles</th><th>Dólares</th></tr></thead>");
            html.Append("<tbody>");

            // Ingresos principales
            string queryIngresosPrincipales = @"
                SELECT 
                    despachoSoles, despachoDolares, descDespacho,
                    prestamoSoles, prestamosDolares, descPrestamo,
                    mensualidadSoles, mensualidadDolares, descMensualidad,
                    otrosSoles, otrosDolares, descOtrosAutorizados
                FROM Ingresos 
                WHERE numeroOrdenViaje = @numeroOrden";

            using (SqlCommand cmd = new SqlCommand(queryIngresosPrincipales, conn))
            {
                cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        decimal despachoS = Convert.ToDecimal(reader["despachoSoles"]);
                        decimal despachoD = Convert.ToDecimal(reader["despachoDolares"]);
                        if (despachoS > 0 || despachoD > 0)
                        {
                            html.Append($"<tr><td>Despacho</td><td>{reader["descDespacho"]}</td><td>S/ {despachoS:N2}</td><td>$ {despachoD:N2}</td></tr>");
                            totalIngresosSoles += despachoS;
                            totalIngresosDolares += despachoD;
                        }

                        decimal prestamoS = Convert.ToDecimal(reader["prestamoSoles"]);
                        decimal prestamoD = Convert.ToDecimal(reader["prestamosDolares"]);
                        if (prestamoS > 0 || prestamoD > 0)
                        {
                            html.Append($"<tr><td>Préstamo</td><td>{reader["descPrestamo"]}</td><td>S/ {prestamoS:N2}</td><td>$ {prestamoD:N2}</td></tr>");
                            totalIngresosSoles += prestamoS;
                            totalIngresosDolares += prestamoD;
                        }

                        decimal mensualidadS = Convert.ToDecimal(reader["mensualidadSoles"]);
                        decimal mensualidadD = Convert.ToDecimal(reader["mensualidadDolares"]);
                        if (mensualidadS > 0 || mensualidadD > 0)
                        {
                            html.Append($"<tr><td>Mensualidad</td><td>{reader["descMensualidad"]}</td><td>S/ {mensualidadS:N2}</td><td>$ {mensualidadD:N2}</td></tr>");
                            totalIngresosSoles += mensualidadS;
                            totalIngresosDolares += mensualidadD;
                        }

                        decimal otrosS = Convert.ToDecimal(reader["otrosSoles"]);
                        decimal otrosD = Convert.ToDecimal(reader["otrosDolares"]);
                        if (otrosS > 0 || otrosD > 0)
                        {
                            html.Append($"<tr><td>Otros Autorizados</td><td>{reader["descOtrosAutorizados"]}</td><td>S/ {otrosS:N2}</td><td>$ {otrosD:N2}</td></tr>");
                            totalIngresosSoles += otrosS;
                            totalIngresosDolares += otrosD;
                        }
                    }
                }
            }

            // Ingresos adicionales
            string queryIngresosAdicionales = @"
                SELECT nombreCategoria, descripcion, soles, dolares
                FROM IngresosAdicionales
                WHERE numeroOrdenViaje = @numeroOrden";

            using (SqlCommand cmd = new SqlCommand(queryIngresosAdicionales, conn))
            {
                cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        decimal soles = Convert.ToDecimal(reader["soles"]);
                        decimal dolares = Convert.ToDecimal(reader["dolares"]);
                        html.Append($"<tr><td>{reader["nombreCategoria"]}</td><td>{reader["descripcion"]}</td><td>S/ {soles:N2}</td><td>$ {dolares:N2}</td></tr>");
                        totalIngresosSoles += soles;
                        totalIngresosDolares += dolares;
                    }
                }
            }

            html.Append("<tr class='font-weight-bold'>");
            html.Append("<td colspan='2'>TOTAL INGRESOS</td>");
            html.Append($"<td>S/ {totalIngresosSoles:N2}</td>");
            html.Append($"<td>$ {totalIngresosDolares:N2}</td>");
            html.Append("</tr>");
            html.Append("</tbody></table>");
            html.Append("</div>");
            html.Append("</div>");

            return html.ToString();
        }

        private static string ObtenerSeccionGastos(SqlConnection conn, string numeroOrden)
        {
            StringBuilder html = new StringBuilder();
            decimal totalGastosSoles = 0;
            decimal totalGastosDolares = 0;

            html.Append("<div class='card mb-3'>");
            html.Append("<div class='card-header bg-danger text-white'><h6 class='mb-0'>Gastos</h6></div>");
            html.Append("<div class='card-body'>");
            html.Append("<table class='table table-sm'>");
            html.Append("<thead><tr><th>Concepto</th><th>Descripción</th><th>Soles</th><th>Dólares</th></tr></thead>");
            html.Append("<tbody>");

            // Gastos principales
            string queryGastosPrincipales = @"
                SELECT 
                    peajesSoles, peajesDolares, descPeajes,
                    alimentacionSoles, alimentacionDolares, descAlimentacion,
                    apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                    reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                    movilidadSoles, movilidadDolares, descMovilidad,
                    hospedajeSoles, hospedajeDolares, descHospedaje,
                    combustibleSoles, combustibleDolares, descCombustible,
                    encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada
                FROM Egresos 
                WHERE numeroOrdenViaje = @numeroOrden";

            using (SqlCommand cmd = new SqlCommand(queryGastosPrincipales, conn))
            {
                cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        // Peajes
                        decimal peajesS = Convert.ToDecimal(reader["peajesSoles"]);
                        decimal peajesD = Convert.ToDecimal(reader["peajesDolares"]);
                        if (peajesS > 0 || peajesD > 0)
                        {
                            html.Append($"<tr><td>Peajes</td><td>{reader["descPeajes"]}</td><td>S/ {peajesS:N2}</td><td>$ {peajesD:N2}</td></tr>");
                            totalGastosSoles += peajesS;
                            totalGastosDolares += peajesD;
                        }

                        // Alimentación
                        decimal alimentacionS = Convert.ToDecimal(reader["alimentacionSoles"]);
                        decimal alimentacionD = Convert.ToDecimal(reader["alimentacionDolares"]);
                        if (alimentacionS > 0 || alimentacionD > 0)
                        {
                            html.Append($"<tr><td>Alimentación</td><td>{reader["descAlimentacion"]}</td><td>S/ {alimentacionS:N2}</td><td>$ {alimentacionD:N2}</td></tr>");
                            totalGastosSoles += alimentacionS;
                            totalGastosDolares += alimentacionD;
                        }

                        // Apoyo Seguridad
                        decimal apoyoS = Convert.ToDecimal(reader["apoyoseguridadSoles"]);
                        decimal apoyoD = Convert.ToDecimal(reader["apoyoseguridadDolares"]);
                        if (apoyoS > 0 || apoyoD > 0)
                        {
                            html.Append($"<tr><td>Apoyo Seguridad</td><td>{reader["descApoyoSeguridad"]}</td><td>S/ {apoyoS:N2}</td><td>$ {apoyoD:N2}</td></tr>");
                            totalGastosSoles += apoyoS;
                            totalGastosDolares += apoyoD;
                        }

                        // Reparaciones
                        decimal reparacionesS = Convert.ToDecimal(reader["reparacionesVariosSoles"]);
                        decimal reparacionesD = Convert.ToDecimal(reader["repacionesVariosDolares"]);
                        if (reparacionesS > 0 || reparacionesD > 0)
                        {
                            html.Append($"<tr><td>Reparaciones</td><td>{reader["descReparacionesVarios"]}</td><td>S/ {reparacionesS:N2}</td><td>$ {reparacionesD:N2}</td></tr>");
                            totalGastosSoles += reparacionesS;
                            totalGastosDolares += reparacionesD;
                        }

                        // Movilidad
                        decimal movilidadS = Convert.ToDecimal(reader["movilidadSoles"]);
                        decimal movilidadD = Convert.ToDecimal(reader["movilidadDolares"]);
                        if (movilidadS > 0 || movilidadD > 0)
                        {
                            html.Append($"<tr><td>Movilidad</td><td>{reader["descMovilidad"]}</td><td>S/ {movilidadS:N2}</td><td>$ {movilidadD:N2}</td></tr>");
                            totalGastosSoles += movilidadS;
                            totalGastosDolares += movilidadD;
                        }

                        // Hospedaje
                        decimal hospedajeS = Convert.ToDecimal(reader["hospedajeSoles"]);
                        decimal hospedajeD = Convert.ToDecimal(reader["hospedajeDolares"]);
                        if (hospedajeS > 0 || hospedajeD > 0)
                        {
                            html.Append($"<tr><td>Hospedaje</td><td>{reader["descHospedaje"]}</td><td>S/ {hospedajeS:N2}</td><td>$ {hospedajeD:N2}</td></tr>");
                            totalGastosSoles += hospedajeS;
                            totalGastosDolares += hospedajeD;
                        }

                        // Combustible
                        decimal combustibleS = Convert.ToDecimal(reader["combustibleSoles"]);
                        decimal combustibleD = Convert.ToDecimal(reader["combustibleDolares"]);
                        if (combustibleS > 0 || combustibleD > 0)
                        {
                            html.Append($"<tr><td>Combustible</td><td>{reader["descCombustible"]}</td><td>S/ {combustibleS:N2}</td><td>$ {combustibleD:N2}</td></tr>");
                            totalGastosSoles += combustibleS;
                            totalGastosDolares += combustibleD;
                        }

                        // Encarpada
                        decimal encapadaS = Convert.ToDecimal(reader["encarpada_desencarpadaSoles"]);
                        decimal encapadaD = Convert.ToDecimal(reader["encarpada_desencarpadaDolares"]);
                        if (encapadaS > 0 || encapadaD > 0)
                        {
                            html.Append($"<tr><td>Encarpada/Desencarpada</td><td>{reader["descEncarpadaDesencarpada"]}</td><td>S/ {encapadaS:N2}</td><td>$ {encapadaD:N2}</td></tr>");
                            totalGastosSoles += encapadaS;
                            totalGastosDolares += encapadaD;
                        }
                    }
                }
            }

            // Gastos adicionales (Categorías Adicionales)
            string queryGastosAdicionales = @"
                SELECT nombreCategoria, descripcion, soles, dolares
                FROM CategoriasAdicionales
                WHERE numeroOrdenViaje = @numeroOrden";

            using (SqlCommand cmd = new SqlCommand(queryGastosAdicionales, conn))
            {
                cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        decimal soles = Convert.ToDecimal(reader["soles"]);
                        decimal dolares = Convert.ToDecimal(reader["dolares"]);
                        html.Append($"<tr><td>{reader["nombreCategoria"]}</td><td>{reader["descripcion"]}</td><td>S/ {soles:N2}</td><td>$ {dolares:N2}</td></tr>");
                        totalGastosSoles += soles;
                        totalGastosDolares += dolares;
                    }
                }
            }

            html.Append("<tr class='font-weight-bold'>");
            html.Append("<td colspan='2'>TOTAL GASTOS</td>");
            html.Append($"<td>S/ {totalGastosSoles:N2}</td>");
            html.Append($"<td>$ {totalGastosDolares:N2}</td>");
            html.Append("</tr>");
            html.Append("</tbody></table>");
            html.Append("</div>");
            html.Append("</div>");

            return html.ToString();
        }

        private static string ObtenerBalanceFinal(SqlConnection conn, string numeroOrden)
        {
            StringBuilder html = new StringBuilder();

            // Calcular balance final
            decimal balanceSoles = 0;
            decimal balanceDolares = 0;

            // Obtener todos los valores
            string queryBalance = @"
                SELECT 
                    -- Ingresos
                    ISNULL((SELECT totalSoles FROM Ingresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalIngresosSoles,
                    
                    ISNULL((SELECT totalDolares FROM Ingresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalIngresosDolares,
                    
                    -- Gastos
                    ISNULL((SELECT 
                        ISNULL(peajesSoles, 0) + ISNULL(alimentacionSoles, 0) + ISNULL(apoyoseguridadSoles, 0) + 
                        ISNULL(reparacionesVariosSoles, 0) + ISNULL(movilidadSoles, 0) + ISNULL(hospedajeSoles, 0) + 
                        ISNULL(combustibleSoles, 0) + ISNULL(encarpada_desencarpadaSoles, 0)
                    FROM Egresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalGastosSoles,
                    
                    ISNULL((SELECT 
                        ISNULL(peajesDolares, 0) + ISNULL(alimentacionDolares, 0) + ISNULL(apoyoseguridadDolares, 0) + 
                        ISNULL(repacionesVariosDolares, 0) + ISNULL(movilidadDolares, 0) + ISNULL(hospedajeDolares, 0) + 
                        ISNULL(combustibleDolares, 0) + ISNULL(encarpada_desencarpadaDolares, 0)
                    FROM Egresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalGastosDolares,
                    
                    -- Descuentos y Reintegros
                    ISNULL((SELECT descuentoSoles FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS DescuentoSoles,
                    ISNULL((SELECT descuentoDolares FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS DescuentoDolares,
                    ISNULL((SELECT reintegroSoles FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS ReintegroSoles,
                    ISNULL((SELECT reintegroDolares FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS ReintegroDolares";

            using (SqlCommand cmd = new SqlCommand(queryBalance, conn))
            {
                cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        decimal ingresosSoles = Convert.ToDecimal(reader["TotalIngresosSoles"]);
                        decimal gastosSoles = Convert.ToDecimal(reader["TotalGastosSoles"]);
                        decimal descuentoSoles = Convert.ToDecimal(reader["DescuentoSoles"]);
                        decimal reintegroSoles = Convert.ToDecimal(reader["ReintegroSoles"]);

                        decimal ingresosDolares = Convert.ToDecimal(reader["TotalIngresosDolares"]);
                        decimal gastosDolares = Convert.ToDecimal(reader["TotalGastosDolares"]);
                        decimal descuentoDolares = Convert.ToDecimal(reader["DescuentoDolares"]);
                        decimal reintegroDolares = Convert.ToDecimal(reader["ReintegroDolares"]);

                        balanceSoles = ingresosSoles - gastosSoles - descuentoSoles + reintegroSoles;
                        balanceDolares = ingresosDolares - gastosDolares - descuentoDolares + reintegroDolares;
                    }
                }
            }

            html.Append("<div class='card'>");
            html.Append("<div class='card-header bg-info text-white'><h6 class='mb-0'>Balance Final</h6></div>");
            html.Append("<div class='card-body'>");
            html.Append("<div class='row'>");
            html.Append($"<div class='col-md-6 text-center'><h5>Soles: S/ {balanceSoles:N2}</h5></div>");
            html.Append($"<div class='col-md-6 text-center'><h5>Dólares: $ {balanceDolares:N2}</h5></div>");
            html.Append("</div>");
            html.Append("</div>");
            html.Append("</div>");

            return html.ToString();
        }

        #endregion

        #region UTILIDADES

        private void MostrarMensaje(string mensaje, string tipo)
        {
            pnlMensajes.Visible = true;
            lblMensaje.Text = $"<div class='alert alert-{tipo} alert-dismissible fade show' role='alert'>{mensaje}<button type='button' class='close' data-dismiss='alert'>&times;</button></div>";
        }

        #endregion
    }
}