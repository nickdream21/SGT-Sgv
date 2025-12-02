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

                // Actualizar contador
                lblTotalRegistros.Text = $"{dt.Rows.Count} registros";

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
                        
                        -- ✅ CALCULAR INGRESOS TOTALES
                        (
                            -- Ingresos principales en Soles
                            ISNULL((SELECT i.totalSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                            +
                            -- Ingresos adicionales en Soles
                            ISNULL((SELECT SUM(ia.soles) FROM IngresosAdicionales ia WHERE ia.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                        ) AS TotalIngresosSoles,
                        
                        (
                            -- Ingresos principales en Dólares
                            ISNULL((SELECT i.totalDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                            +
                            -- Ingresos adicionales en Dólares
                            ISNULL((SELECT SUM(ia.dolares) FROM IngresosAdicionales ia WHERE ia.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                        ) AS TotalIngresosDolares,
                        
                        -- ✅ CALCULAR GASTOS TOTALES
                        (
                            -- Gastos principales en Soles
                            ISNULL((SELECT 
                                ISNULL(e.peajesSoles, 0) + 
                                ISNULL(e.alimentacionSoles, 0) + 
                                ISNULL(e.apoyoseguridadSoles, 0) + 
                                ISNULL(e.reparacionesVariosSoles, 0) + 
                                ISNULL(e.movilidadSoles, 0) + 
                                ISNULL(e.hospedajeSoles, 0) + 
                                ISNULL(e.combustibleSoles, 0) + 
                                ISNULL(e.encarpada_desencarpadaSoles, 0)
                            FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                            +
                            -- Gastos adicionales en Soles
                            ISNULL((SELECT SUM(ca.soles) FROM CategoriasAdicionales ca WHERE ca.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                        ) AS TotalGastosSoles,
                        
                        (
                            -- Gastos principales en Dólares
                            ISNULL((SELECT 
                                ISNULL(e.peajesDolares, 0) + 
                                ISNULL(e.alimentacionDolares, 0) + 
                                ISNULL(e.apoyoseguridadDolares, 0) + 
                                ISNULL(e.repacionesVariosDolares, 0) + 
                                ISNULL(e.movilidadDolares, 0) + 
                                ISNULL(e.hospedajeDolares, 0) + 
                                ISNULL(e.combustibleDolares, 0) + 
                                ISNULL(e.encarpada_desencarpadaDolares, 0)
                            FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                            +
                            -- Gastos adicionales en Dólares
                            ISNULL((SELECT SUM(ca.dolares) FROM CategoriasAdicionales ca WHERE ca.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                        ) AS TotalGastosDolares,
                        
                        -- ✅ DESCUENTOS Y REINTEGROS
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

            // ✅ CALCULAR MONTOS FINALES (Ingresos - Gastos - Descuentos + Reintegros)
            dt.Columns.Add("MontoSoles", typeof(decimal));
            dt.Columns.Add("MontoDolares", typeof(decimal));

            foreach (DataRow row in dt.Rows)
            {
                decimal ingresosSoles = Convert.ToDecimal(row["TotalIngresosSoles"]);
                decimal gastosSoles = Convert.ToDecimal(row["TotalGastosSoles"]);
                decimal descuentoSoles = Convert.ToDecimal(row["DescuentoSoles"]);
                decimal reintegroSoles = Convert.ToDecimal(row["ReintegroSoles"]);

                decimal ingresosDolares = Convert.ToDecimal(row["TotalIngresosDolares"]);
                decimal gastosDolares = Convert.ToDecimal(row["TotalGastosDolares"]);
                decimal descuentoDolares = Convert.ToDecimal(row["DescuentoDolares"]);
                decimal reintegroDolares = Convert.ToDecimal(row["ReintegroDolares"]);

                row["MontoSoles"] = ingresosSoles - gastosSoles - descuentoSoles + reintegroSoles;
                row["MontoDolares"] = ingresosDolares - gastosDolares - descuentoDolares + reintegroDolares;
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
                // Calcular totales para el footer
                decimal totalSoles = 0;
                decimal totalDolares = 0;

                foreach (GridViewRow row in gvLiquidaciones.Rows)
                {
                    if (row.Cells.Count > 5)
                    {
                        // Obtener valores de las celdas
                        Label lblSoles = row.Cells[4].Controls.OfType<Label>().FirstOrDefault();
                        Label lblDolares = row.Cells[5].Controls.OfType<Label>().FirstOrDefault();

                        if (lblSoles != null)
                        {
                            string solesText = lblSoles.Text.Replace("S/", "").Replace("-", "").Replace("$", "").Trim();
                            decimal soles;
                            if (decimal.TryParse(solesText, out soles))
                                totalSoles += soles;
                        }

                        if (lblDolares != null)
                        {
                            string dolaresText = lblDolares.Text.Replace("S/", "").Replace("-", "").Replace("$", "").Trim();
                            decimal dolares;
                            if (decimal.TryParse(dolaresText, out dolares))
                                totalDolares += dolares;
                        }
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
                return "monto-descuento";
            else if (valor > 0)
                return "monto-reintegro";
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
                return "monto-descuento";
            else if (valor > 0)
                return "monto-reintegro";
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
                lblCountViajesActivos.Text = $"{dt.Rows.Count} viajes";
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
                        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
                        t.placaTracto AS PlacaTracto,
                        ISNULL(ca.placaCarreta, 'N/A') AS PlacaCarreta,
                        ISNULL(cl.Nombre, 'N/A') AS Cliente,
                        vp.fechaInicio AS FechaInicio,
                        DATEDIFF(DAY, vp.fechaInicio, GETDATE()) AS DiasEnViaje,
                        vp.estadoViaje AS Estado,
                        vp.idViajeProgreso AS IdViaje
                    FROM ViajesEnProgreso vp
                    INNER JOIN Conductor c ON vp.idConductor = c.idConductor
                    LEFT JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso
                    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
                    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                    LEFT JOIN (
                        SELECT DISTINCT 
                            d.idViajeProgreso,
                            cl.nombre AS Nombre
                        FROM Despachos d
                        INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                        WHERE d.idViajeProgreso IS NOT NULL
                    ) cl ON vp.idViajeProgreso = cl.idViajeProgreso
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

                // ✅ OBTENER FACTOR DE CONVERSIÓN
                decimal factorConversion = 3.75m;
                if (!string.IsNullOrEmpty(Request.QueryString["factor"]))
                {
                    decimal.TryParse(Request.QueryString["factor"], out factorConversion);
                }

                DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);

                // ✅ LIMPIAR RESPONSE COMPLETAMENTE
                Response.Clear();
                Response.ClearHeaders();
                Response.ClearContent();
                Response.Buffer = true;
                Response.Charset = "";

                using (XLWorkbook wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Liquidaciones");

                    // Título
                    ws.Cell(1, 1).Value = "REPORTE DE LIQUIDACIONES";
                    ws.Range(1, 1, 1, 6).Merge();
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Font.FontSize = 16;
                    ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Fechas
                    ws.Cell(2, 1).Value = $"Período: {fechaDesde:dd/MM/yyyy} - {fechaHasta:dd/MM/yyyy}";
                    ws.Range(2, 1, 2, 6).Merge();
                    ws.Cell(2, 1).Style.Font.Bold = true;
                    ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Encabezados
                    ws.Cell(4, 1).Value = "DNI";
                    ws.Cell(4, 2).Value = "CONDUCTOR";
                    ws.Cell(4, 3).Value = "FECHA";
                    ws.Cell(4, 4).Value = "N° DE LIQ";
                    ws.Cell(4, 5).Value = "MONTO S/";
                    ws.Cell(4, 6).Value = "MONTO $";

                    var headerRange = ws.Range(4, 1, 4, 6);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Datos
                    int row = 5;
                    decimal totalSoles = 0;
                    decimal totalDolares = 0;

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

                        // Aplicar color si es descuento o reintegro
                        if (montoSoles < 0)
                        {
                            ws.Cell(row, 5).Style.Font.FontColor = XLColor.Red;
                        }
                        else if (montoSoles > 0)
                        {
                            ws.Cell(row, 5).Style.Font.FontColor = XLColor.Blue;
                        }

                        if (montoDolares < 0)
                        {
                            ws.Cell(row, 6).Style.Font.FontColor = XLColor.Red;
                        }
                        else if (montoDolares > 0)
                        {
                            ws.Cell(row, 6).Style.Font.FontColor = XLColor.Blue;
                        }

                        totalSoles += montoSoles;
                        totalDolares += montoDolares;

                        row++;
                    }

                    // ✅ FILA DE TOTALES
                    ws.Cell(row, 4).Value = "TOTAL:";
                    ws.Cell(row, 4).Style.Font.Bold = true;
                    ws.Cell(row, 5).Value = totalSoles;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 6).Value = totalDolares;
                    ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 6).Style.Font.Bold = true;

                    // ✅ SECCIÓN DE RESUMEN CON CONVERSIÓN
                    row += 2;

                    // Título del resumen
                    ws.Cell(row, 1).Value = "RESUMEN TOTAL EN SOLES";
                    ws.Range(row, 1, row, 6).Merge();
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 14;
                    ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;

                    // Factor de conversión
                    ws.Cell(row, 1).Value = "Factor de Conversión ($ a S/):";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Range(row, 1, row, 4).Merge();
                    ws.Cell(row, 5).Value = factorConversion;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    ws.Range(row, 5, row, 6).Merge();
                    row++;

                    // Total en Soles
                    ws.Cell(row, 1).Value = "Total en Soles (S/):";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Range(row, 1, row, 4).Merge();
                    ws.Cell(row, 5).Value = totalSoles;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Range(row, 5, row, 6).Merge();
                    row++;

                    // Total en Dólares
                    ws.Cell(row, 1).Value = "Total en Dólares ($):";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Range(row, 1, row, 4).Merge();
                    ws.Cell(row, 5).Value = totalDolares;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Range(row, 5, row, 6).Merge();
                    row++;

                    // Conversión a Soles
                    decimal totalDolaresConvertido = totalDolares * factorConversion;
                    ws.Cell(row, 1).Value = "Conversión a Soles ($ × Factor):";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Range(row, 1, row, 4).Merge();
                    ws.Cell(row, 5).Value = totalDolaresConvertido;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightCyan;
                    ws.Range(row, 5, row, 6).Merge();
                    row++;

                    // TOTAL GENERAL
                    decimal totalGeneral = totalSoles + totalDolaresConvertido;
                    ws.Cell(row, 1).Value = "TOTAL GENERAL EN SOLES:";
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontSize = 12;
                    ws.Range(row, 1, row, 4).Merge();
                    ws.Cell(row, 5).Value = totalGeneral;
                    ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                    ws.Cell(row, 5).Style.Font.Bold = true;
                    ws.Cell(row, 5).Style.Font.FontSize = 12;
                    ws.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightGreen;
                    ws.Range(row, 5, row, 6).Merge();

                    // Ajustar columnas
                    ws.Columns().AdjustToContents();

                    // ✅ GUARDAR EN MEMORYSTREAM Y ENVIAR
                    using (MemoryStream ms = new MemoryStream())
                    {
                        wb.SaveAs(ms);
                        byte[] fileBytes = ms.ToArray();

                        Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        Response.AddHeader("content-disposition", $"attachment;filename=Liquidaciones_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                        Response.BinaryWrite(fileBytes);
                        Response.Flush();
                    }
                }

                // ✅ USAR CompleteRequest EN LUGAR DE End()
                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                Response.Clear();
                Response.ContentType = "text/html";
                Response.Write($"<h3>Error al exportar:</h3><p>{ex.Message}</p><pre>{ex.StackTrace}</pre>");
                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
        }

        private void ExportarViajesActivosExcel()
        {
            try
            {
                string buscarConductor = Request.QueryString["buscarConductor"] ?? "";
                string estadoViaje = Request.QueryString["estadoViaje"] ?? "";

                DataTable dt = ObtenerViajesActivos(buscarConductor, estadoViaje);

                // ✅ LIMPIAR RESPONSE COMPLETAMENTE
                Response.Clear();
                Response.ClearHeaders();
                Response.ClearContent();
                Response.Buffer = true;
                Response.Charset = "";

                using (XLWorkbook wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Viajes Activos");

                    // Título
                    ws.Cell(1, 1).Value = "REPORTE DE VIAJES ACTIVOS";
                    ws.Range(1, 1, 1, 8).Merge();
                    ws.Cell(1, 1).Style.Font.Bold = true;
                    ws.Cell(1, 1).Style.Font.FontSize = 16;
                    ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Fecha
                    ws.Cell(2, 1).Value = $"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}";
                    ws.Range(2, 1, 2, 8).Merge();
                    ws.Cell(2, 1).Style.Font.Bold = true;
                    ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Encabezados
                    ws.Cell(4, 1).Value = "DNI";
                    ws.Cell(4, 2).Value = "CONDUCTOR";
                    ws.Cell(4, 3).Value = "TRACTO";
                    ws.Cell(4, 4).Value = "CARRETA";
                    ws.Cell(4, 5).Value = "CLIENTE";
                    ws.Cell(4, 6).Value = "FECHA INICIO";
                    ws.Cell(4, 7).Value = "DÍAS EN VIAJE";
                    ws.Cell(4, 8).Value = "ESTADO";

                    var headerRange = ws.Range(4, 1, 4, 8);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.Orange;
                    headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Datos
                    int row = 5;
                    foreach (DataRow dr in dt.Rows)
                    {
                        ws.Cell(row, 1).Value = dr["DNI"] != DBNull.Value ? dr["DNI"].ToString() : "";
                        ws.Cell(row, 2).Value = dr["Conductor"] != DBNull.Value ? dr["Conductor"].ToString() : "";
                        ws.Cell(row, 3).Value = dr["PlacaTracto"] != DBNull.Value ? dr["PlacaTracto"].ToString() : "N/A";
                        ws.Cell(row, 4).Value = dr["PlacaCarreta"] != DBNull.Value ? dr["PlacaCarreta"].ToString() : "N/A";
                        ws.Cell(row, 5).Value = dr["Cliente"] != DBNull.Value ? dr["Cliente"].ToString() : "N/A";

                        if (dr["FechaInicio"] != DBNull.Value)
                        {
                            ws.Cell(row, 6).Value = Convert.ToDateTime(dr["FechaInicio"]).ToString("dd/MM/yyyy");
                        }
                        else
                        {
                            ws.Cell(row, 6).Value = "N/A";
                        }

                        if (dr["DiasEnViaje"] != DBNull.Value)
                        {
                            ws.Cell(row, 7).Value = Convert.ToInt32(dr["DiasEnViaje"]);
                            ws.Cell(row, 7).Style.NumberFormat.Format = "0";
                        }
                        else
                        {
                            ws.Cell(row, 7).Value = 0;
                        }

                        ws.Cell(row, 8).Value = dr["Estado"] != DBNull.Value ? dr["Estado"].ToString() : "N/A";

                        row++;
                    }

                    // Resumen
                    ws.Cell(row + 1, 1).Value = "TOTAL DE VIAJES:";
                    ws.Cell(row + 1, 1).Style.Font.Bold = true;
                    ws.Range(row + 1, 1, row + 1, 6).Merge();
                    ws.Cell(row + 1, 7).Value = dt.Rows.Count;
                    ws.Cell(row + 1, 7).Style.Font.Bold = true;
                    ws.Cell(row + 1, 7).Style.Fill.BackgroundColor = XLColor.LightGray;
                    ws.Range(row + 1, 7, row + 1, 8).Merge();

                    // Ajustar columnas
                    ws.Columns().AdjustToContents();

                    // ✅ GUARDAR Y ENVIAR
                    using (MemoryStream ms = new MemoryStream())
                    {
                        wb.SaveAs(ms);
                        byte[] fileBytes = ms.ToArray();

                        Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        Response.AddHeader("content-disposition", $"attachment;filename=ViajesActivos_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
                        Response.BinaryWrite(fileBytes);
                        Response.Flush();
                    }
                }

                HttpContext.Current.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                Response.Clear();
                Response.ContentType = "text/html";
                Response.Write($"<h3>Error al exportar:</h3><p>{ex.Message}</p><pre>{ex.StackTrace}</pre>");
                HttpContext.Current.ApplicationInstance.CompleteRequest();
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

                // ✅ OBTENER FACTOR DE CONVERSIÓN
                decimal factorConversion = 3.75m;
                if (!string.IsNullOrEmpty(Request.QueryString["factor"]))
                {
                    decimal.TryParse(Request.QueryString["factor"], out factorConversion);
                }

                DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);

                iTextDocument document = new iTextDocument(iTextPageSize.A4.Rotate(), 25, 25, 30, 30);
                MemoryStream ms = new MemoryStream();
                PdfWriter writer = PdfWriter.GetInstance(document, ms);

                document.Open();

                // Título
                iTextFont titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                iTextParagraph title = new iTextParagraph("REPORTE DE LIQUIDACIONES", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                document.Add(title);

                // Fechas
                iTextFont subtitleFont = FontFactory.GetFont(FontFactory.HELVETICA, 12);
                iTextParagraph subtitle = new iTextParagraph($"Período: {fechaDesde:dd/MM/yyyy} - {fechaHasta:dd/MM/yyyy}", subtitleFont);
                subtitle.Alignment = Element.ALIGN_CENTER;
                subtitle.SpacingAfter = 20;
                document.Add(subtitle);

                // Tabla de datos
                PdfPTable table = new PdfPTable(6);
                table.WidthPercentage = 100;
                table.SetWidths(new float[] { 15f, 30f, 15f, 15f, 12.5f, 12.5f });

                // Encabezados
                iTextFont headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                BaseColor headerColor = new BaseColor(173, 216, 230);

                PdfPCell headerCell;
                string[] headers = { "DNI", "CONDUCTOR", "FECHA", "N° DE LIQ", "MONTO S/", "MONTO $" };

                foreach (string header in headers)
                {
                    headerCell = new PdfPCell(new Phrase(header, headerFont));
                    headerCell.BackgroundColor = headerColor;
                    headerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    headerCell.Padding = 5;
                    table.AddCell(headerCell);
                }

                // Datos
                iTextFont dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                decimal totalSoles = 0;
                decimal totalDolares = 0;

                foreach (DataRow dr in dt.Rows)
                {
                    table.AddCell(new PdfPCell(new Phrase(dr["DNI"].ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER, Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(dr["Conductor"].ToString(), dataFont)) { Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(Convert.ToDateTime(dr["FechaSalida"]).ToString("dd/MM/yyyy"), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER, Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase(dr["NumeroLiquidacion"].ToString(), dataFont)) { HorizontalAlignment = Element.ALIGN_CENTER, Padding = 5 });

                    decimal montoSoles = Convert.ToDecimal(dr["MontoSoles"]);
                    decimal montoDolares = Convert.ToDecimal(dr["MontoDolares"]);

                    table.AddCell(new PdfPCell(new Phrase($"S/ {montoSoles:N2}", dataFont)) { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 5 });
                    table.AddCell(new PdfPCell(new Phrase($"$ {montoDolares:N2}", dataFont)) { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 5 });

                    totalSoles += montoSoles;
                    totalDolares += montoDolares;
                }

                // Fila de totales
                iTextFont totalFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                PdfPCell totalLabelCell = new PdfPCell(new Phrase("TOTAL:", totalFont));
                totalLabelCell.Colspan = 4;
                totalLabelCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                totalLabelCell.Padding = 5;
                totalLabelCell.BackgroundColor = new BaseColor(240, 240, 240);
                table.AddCell(totalLabelCell);

                table.AddCell(new PdfPCell(new Phrase($"S/ {totalSoles:N2}", totalFont)) { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 5, BackgroundColor = new BaseColor(240, 240, 240) });
                table.AddCell(new PdfPCell(new Phrase($"$ {totalDolares:N2}", totalFont)) { HorizontalAlignment = Element.ALIGN_RIGHT, Padding = 5, BackgroundColor = new BaseColor(240, 240, 240) });

                document.Add(table);

                // ✅ SECCIÓN DE RESUMEN CON CONVERSIÓN
                document.Add(new iTextParagraph("\n"));

                // Título del resumen
                iTextFont resumenTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                iTextParagraph resumenTitle = new iTextParagraph("RESUMEN TOTAL EN SOLES", resumenTitleFont);
                resumenTitle.Alignment = Element.ALIGN_CENTER;
                resumenTitle.SpacingBefore = 10;
                resumenTitle.SpacingAfter = 10;
                document.Add(resumenTitle);

                // Tabla de resumen
                PdfPTable resumenTable = new PdfPTable(2);
                resumenTable.WidthPercentage = 60;
                resumenTable.HorizontalAlignment = Element.ALIGN_CENTER;
                resumenTable.SetWidths(new float[] { 60f, 40f });

                iTextFont resumenLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11);
                iTextFont resumenValueFont = FontFactory.GetFont(FontFactory.HELVETICA, 11);
                iTextFont resumenTotalFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);

                BaseColor resumenBgColor = new BaseColor(240, 255, 240);
                BaseColor totalBgColor = new BaseColor(144, 238, 144);

                // Factor de conversión
                PdfPCell factorLabelCell = new PdfPCell(new Phrase("Factor de Conversión ($ a S/):", resumenLabelFont));
                factorLabelCell.BackgroundColor = new BaseColor(255, 255, 224);
                factorLabelCell.Padding = 8;
                resumenTable.AddCell(factorLabelCell);

                PdfPCell factorValueCell = new PdfPCell(new Phrase(factorConversion.ToString("0.00"), resumenValueFont));
                factorValueCell.BackgroundColor = new BaseColor(255, 255, 224);
                factorValueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                factorValueCell.Padding = 8;
                resumenTable.AddCell(factorValueCell);

                // Total en Soles
                PdfPCell solesLabelCell = new PdfPCell(new Phrase("Total en Soles (S/):", resumenLabelFont));
                solesLabelCell.BackgroundColor = resumenBgColor;
                solesLabelCell.Padding = 8;
                resumenTable.AddCell(solesLabelCell);

                PdfPCell solesValueCell = new PdfPCell(new Phrase($"S/ {totalSoles:N2}", resumenValueFont));
                solesValueCell.BackgroundColor = resumenBgColor;
                solesValueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                solesValueCell.Padding = 8;
                resumenTable.AddCell(solesValueCell);

                // Total en Dólares
                PdfPCell dolaresLabelCell = new PdfPCell(new Phrase("Total en Dólares ($):", resumenLabelFont));
                dolaresLabelCell.BackgroundColor = resumenBgColor;
                dolaresLabelCell.Padding = 8;
                resumenTable.AddCell(dolaresLabelCell);

                PdfPCell dolaresValueCell = new PdfPCell(new Phrase($"$ {totalDolares:N2}", resumenValueFont));
                dolaresValueCell.BackgroundColor = resumenBgColor;
                dolaresValueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                dolaresValueCell.Padding = 8;
                resumenTable.AddCell(dolaresValueCell);

                // Conversión a Soles
                decimal totalDolaresConvertido = totalDolares * factorConversion;
                PdfPCell conversionLabelCell = new PdfPCell(new Phrase("Conversión a Soles ($ × Factor):", resumenLabelFont));
                conversionLabelCell.BackgroundColor = new BaseColor(224, 255, 255);
                conversionLabelCell.Padding = 8;
                resumenTable.AddCell(conversionLabelCell);

                PdfPCell conversionValueCell = new PdfPCell(new Phrase($"S/ {totalDolaresConvertido:N2}", resumenValueFont));
                conversionValueCell.BackgroundColor = new BaseColor(224, 255, 255);
                conversionValueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                conversionValueCell.Padding = 8;
                resumenTable.AddCell(conversionValueCell);

                // TOTAL GENERAL
                decimal totalGeneral = totalSoles + totalDolaresConvertido;
                PdfPCell totalGeneralLabelCell = new PdfPCell(new Phrase("TOTAL GENERAL EN SOLES:", resumenTotalFont));
                totalGeneralLabelCell.BackgroundColor = totalBgColor;
                totalGeneralLabelCell.Padding = 10;
                resumenTable.AddCell(totalGeneralLabelCell);

                PdfPCell totalGeneralValueCell = new PdfPCell(new Phrase($"S/ {totalGeneral:N2}", resumenTotalFont));
                totalGeneralValueCell.BackgroundColor = totalBgColor;
                totalGeneralValueCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                totalGeneralValueCell.Padding = 10;
                resumenTable.AddCell(totalGeneralValueCell);

                document.Add(resumenTable);

                document.Close();
                writer.Close();

                // Enviar PDF
                Response.Clear();
                Response.ContentType = "application/pdf";
                Response.AddHeader("content-disposition", $"attachment;filename=Liquidaciones_{DateTime.Now:yyyyMMddHHmmss}.pdf");
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.BinaryWrite(ms.ToArray());
                Response.End();
            }
            catch (Exception ex)
            {
                Response.Write($"Error al generar PDF: {ex.Message}");
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