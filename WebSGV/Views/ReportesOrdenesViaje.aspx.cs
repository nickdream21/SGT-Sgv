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
using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using iTextSharp.text;
using iTextSharp.text.pdf;
using WebSGV.Helpers;

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
        private static readonly string[] EstadosViajePermitidos = { "TODOS", "ABIERTO", "CERRADO" };
        private static readonly string[] EstadosPersonalizadoPermitidos = { "TODOS", "COMPLETADO", "PENDIENTE", "RECHAZADO" };
        private static readonly string[] OrdenesPersonalizadoPermitidas = { "fecha_desc", "fecha_asc", "conductor", "cliente" };

        protected void Page_Load(object sender, EventArgs e)
        {
            // Verificar acceso antes de cualquier operación (incluidas exportaciones)
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

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
                    case "reportePersonalizado":
                        GenerarReportePersonalizado();
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

                // Fechas y combos para el reporte personalizado
                txtPersFechaDesde.Text = primerDia.ToString("yyyy-MM-dd");
                txtPersFechaHasta.Text = hoy.ToString("yyyy-MM-dd");
                txtPersFactor.Text = "3.75";
                CargarConductoresPersonalizado();
                CargarClientesPersonalizado();

                // Cargar datos iniciales
                CargarLiquidaciones();
                CargarViajesActivos();
            }
        }

        private void CargarConductoresPersonalizado()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT idConductor, 
                                        CONCAT(nombre, ' ', apPaterno, ' ', ISNULL(apMaterno,'')) AS NombreCompleto
                                     FROM Conductor
                                     WHERE activo = 1
                                     ORDER BY nombre, apPaterno";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ddlPersConductor.Items.Add(new System.Web.UI.WebControls.ListItem(
                                dr["NombreCompleto"].ToString().Trim(),
                                dr["idConductor"].ToString()));
                        }
                    }
                }
            }
            catch { /* silencioso: el combo sigue con "Todos" */ }
        }

        private void CargarClientesPersonalizado()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT idCliente, nombre FROM Cliente ORDER BY nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            ddlPersCliente.Items.Add(new System.Web.UI.WebControls.ListItem(
                                dr["nombre"].ToString(),
                                dr["idCliente"].ToString()));
                        }
                    }
                }
            }
            catch { /* silencioso */ }
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
                string mensajeValidacion;
                DateTime fechaDesde;
                DateTime fechaHasta;
                decimal factorConversion;
                if (!ValidarFiltrosLiquidaciones(txtFechaDesde.Text, txtFechaHasta.Text, txtFactorConversion.Text,
                    out fechaDesde, out fechaHasta, out factorConversion, out mensajeValidacion))
                {
                    MostrarMensaje(mensajeValidacion, "warning");
                    return;
                }

                DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);

                gvLiquidaciones.DataSource = dt;
                gvLiquidaciones.DataBind();

                // Actualizar contadores
                lblTotalRegistros.Text = dt.Rows.Count.ToString();
                lblTotalRegistrosTabla.Text = $"{dt.Rows.Count} registros";

                // Calcular y mostrar totales
                CalcularTotalesLiquidaciones(dt, factorConversion);
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

        private void CalcularTotalesLiquidaciones(DataTable dt, decimal factorConversion)
        {
            decimal totalSoles = 0;
            decimal totalDolares = 0;

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

                string mensajeValidacion;
                if (!ValidarFiltrosViajesActivos(buscarConductor, estadoViaje, out mensajeValidacion))
                {
                    MostrarMensaje(mensajeValidacion, "warning");
                    return;
                }

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
                    /*
                        Definición de 'Viaje activo sin liquidación':
                        Mismo criterio que la pantalla ListaDespachos (sp_LD_ObtenerViajesActivos):
                        - El viaje en progreso sigue ABIERTO (al liquidar pasa a CERRADO).
                        - El viaje está activo (no anulado).
                        - Tiene al menos un despacho activo asociado.

                        Salvaguarda: si por inconsistencia de datos quedó vinculada una OrdenViaje
                        ya COMPLETADA al viaje, igual lo excluimos. Las órdenes en otros estados
                        (borradores, rechazadas, etc.) NO se consideran liquidación válida.
                    */
                    WHERE vp.estadoViaje = 'ABIERTO'
                      AND vp.activo = 1
                      AND NOT EXISTS (
                          SELECT 1 FROM OrdenViaje ov
                          WHERE ov.idViajeProgreso = vp.idViajeProgreso
                            AND ov.estadoViaje = 'COMPLETADO'
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
                string mensajeValidacion;
                DateTime fechaDesde;
                DateTime fechaHasta;
                decimal factorConversion;
                if (!ValidarFiltrosLiquidaciones(Request.QueryString["fechaDesde"], Request.QueryString["fechaHasta"],
                    Request.QueryString["factor"], out fechaDesde, out fechaHasta, out factorConversion, out mensajeValidacion))
                {
                    ResponderSolicitudInvalida(mensajeValidacion);
                    return;
                }

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

                string mensajeValidacion;
                if (!ValidarFiltrosViajesActivos(buscarConductor, estadoViaje, out mensajeValidacion))
                {
                    ResponderSolicitudInvalida(mensajeValidacion);
                    return;
                }

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
                string mensajeValidacion;
                DateTime fechaDesde;
                DateTime fechaHasta;
                decimal factorConversion;
                if (!ValidarFiltrosLiquidaciones(Request.QueryString["fechaDesde"], Request.QueryString["fechaHasta"],
                    Request.QueryString["factor"], out fechaDesde, out fechaHasta, out factorConversion, out mensajeValidacion))
                {
                    ResponderSolicitudInvalida(mensajeValidacion);
                    return;
                }

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

        #region REPORTE PERSONALIZADO

        /// <summary>
        /// Definición de columnas disponibles (clave, encabezado, tipo).
        /// Tipo: "text" | "date" | "time" | "money-sol" | "money-dol" | "int"
        /// </summary>
        private static readonly Tuple<string, string, string>[] ColumnasPersonalizadas = new[]
        {
            Tuple.Create("dni",              "DNI",                 "text"),
            Tuple.Create("conductor",        "Conductor",           "text"),
            Tuple.Create("fechaSalida",      "Fecha Salida",        "date"),
            Tuple.Create("fechaLlegada",     "Fecha Llegada",       "date"),
            Tuple.Create("horaSalida",       "Hora Salida",         "time"),
            Tuple.Create("horaLlegada",      "Hora Llegada",        "time"),
            Tuple.Create("tracto",           "Placa Tracto",        "text"),
            Tuple.Create("carreta",          "Placa Carreta",       "text"),
            Tuple.Create("cliente",          "Cliente",             "text"),
            Tuple.Create("destino",          "Destino",             "text"),
            Tuple.Create("numero",           "N° Liquidación",      "text"),
            Tuple.Create("estado",           "Estado",              "text"),
            Tuple.Create("ingresosSoles",    "Ingresos S/",         "money-sol"),
            Tuple.Create("ingresosDolares",  "Ingresos $",          "money-dol"),
            Tuple.Create("gastosSoles",      "Gastos S/",           "money-sol"),
            Tuple.Create("gastosDolares",    "Gastos $",            "money-dol"),
            Tuple.Create("descuentoSoles",   "Descuento S/",        "money-sol"),
            Tuple.Create("descuentoDolares", "Descuento $",         "money-dol"),
            Tuple.Create("reintegroSoles",   "Reintegro S/",        "money-sol"),
            Tuple.Create("reintegroDolares", "Reintegro $",         "money-dol"),
            Tuple.Create("balanceSoles",     "Balance Neto S/",     "money-sol"),
            Tuple.Create("balanceDolares",   "Balance Neto $",      "money-dol"),
            // === INGRESOS DETALLADOS ===
            Tuple.Create("iDespachoS",       "Ing. Despacho S/",    "money-sol"),
            Tuple.Create("iDespachoD",       "Ing. Despacho $",     "money-dol"),
            Tuple.Create("iPrestamoS",       "Ing. Préstamo S/",    "money-sol"),
            Tuple.Create("iPrestamoD",       "Ing. Préstamo $",     "money-dol"),
            Tuple.Create("iMensualidadS",    "Ing. Mensualidad S/", "money-sol"),
            Tuple.Create("iMensualidadD",    "Ing. Mensualidad $",  "money-dol"),
            Tuple.Create("iOtrosS",          "Ing. Otros S/",       "money-sol"),
            Tuple.Create("iOtrosD",          "Ing. Otros $",        "money-dol"),
            Tuple.Create("iAdicionalesS",    "Ing. Adicionales S/", "money-sol"),
            Tuple.Create("iAdicionalesD",    "Ing. Adicionales $",  "money-dol"),
            Tuple.Create("iAdicionalesDet",  "Detalle Ing. Adicionales", "text"),
            // === GASTOS DETALLADOS ===
            Tuple.Create("gPeajesS",         "Peajes S/",           "money-sol"),
            Tuple.Create("gPeajesD",         "Peajes $",            "money-dol"),
            Tuple.Create("gAlimentacionS",   "Alimentación S/",     "money-sol"),
            Tuple.Create("gAlimentacionD",   "Alimentación $",      "money-dol"),
            Tuple.Create("gApoyoSeguridadS", "Apoyo Seguridad S/",  "money-sol"),
            Tuple.Create("gApoyoSeguridadD", "Apoyo Seguridad $",   "money-dol"),
            Tuple.Create("gReparacionesS",   "Reparaciones S/",     "money-sol"),
            Tuple.Create("gReparacionesD",   "Reparaciones $",      "money-dol"),
            Tuple.Create("gMovilidadS",      "Movilidad S/",        "money-sol"),
            Tuple.Create("gMovilidadD",      "Movilidad $",         "money-dol"),
            Tuple.Create("gHospedajeS",      "Hospedaje S/",        "money-sol"),
            Tuple.Create("gHospedajeD",      "Hospedaje $",         "money-dol"),
            Tuple.Create("gCombustibleS",    "Combustible S/",      "money-sol"),
            Tuple.Create("gCombustibleD",    "Combustible $",       "money-dol"),
            Tuple.Create("gEncarpadaS",      "Encarpada/Desenc. S/","money-sol"),
            Tuple.Create("gEncarpadaD",      "Encarpada/Desenc. $", "money-dol"),
            Tuple.Create("gAdicionalesS",    "Otros Gastos S/",     "money-sol"),
            Tuple.Create("gAdicionalesD",    "Otros Gastos $",      "money-dol"),
            Tuple.Create("gAdicionalesDet",  "Detalle Otros Gastos (Propina, etc.)", "text"),
            Tuple.Create("observaciones",    "Observaciones",       "text"),
        };

        private class FiltrosPersonalizado
        {
            public DateTime FechaDesde;
            public DateTime FechaHasta;
            public string Estado;
            public int IdConductor;
            public int IdCliente;
            public string PlacaTracto;
            public string Categoria;
            public string Orden;
            public decimal Factor;
            public string Titulo;
            public bool IncluirTotales;
            public bool IncluirResumen;
            public List<string> Columnas;
            public string Formato;
        }

        private FiltrosPersonalizado LeerFiltrosPersonalizado()
        {
            var q = Request.QueryString;

            DateTime fechaDesde;
            DateTime fechaHasta;
            string errorFecha;
            if (!TryParseFecha(q["fechaDesde"], "Fecha desde", out fechaDesde, out errorFecha))
                throw new ArgumentException(errorFecha);
            if (!TryParseFecha(q["fechaHasta"], "Fecha hasta", out fechaHasta, out errorFecha))
                throw new ArgumentException(errorFecha);
            if (fechaDesde > fechaHasta)
                throw new ArgumentException("El rango de fechas del reporte personalizado es inválido: la fecha final debe ser mayor o igual a la inicial.");
            if ((fechaHasta - fechaDesde).TotalDays > 3660)
                throw new ArgumentException("El rango de fechas del reporte personalizado no puede exceder 10 años.");

            decimal factor;
            if (!TryParseFactor(q["factor"], out factor, out errorFecha))
                throw new ArgumentException(errorFecha);

            int idConductor;
            if (!TryParseIdentificadorNoNegativo(q["idConductor"], "Conductor", out idConductor, out errorFecha))
                throw new ArgumentException(errorFecha);

            int idCliente;
            if (!TryParseIdentificadorNoNegativo(q["idCliente"], "Cliente", out idCliente, out errorFecha))
                throw new ArgumentException(errorFecha);

            string estado = (q["estado"] ?? "TODOS").Trim().ToUpperInvariant();
            if (!EstadosPersonalizadoPermitidos.Contains(estado))
                throw new ArgumentException("El estado del viaje en reporte personalizado no es válido.");

            string orden = (q["orden"] ?? "fecha_desc").Trim().ToLowerInvariant();
            if (!OrdenesPersonalizadoPermitidas.Contains(orden))
                throw new ArgumentException("El criterio de orden del reporte personalizado no es válido.");

            string placaTracto = (q["placaTracto"] ?? "").Trim();
            if (placaTracto.Length > 15 || !Regex.IsMatch(placaTracto, @"^[a-zA-Z0-9\-\s]*$"))
                throw new ArgumentException("La placa de tracto es inválida. Solo se permiten letras, números, espacio y guion (máx. 15). ");

            string categoria = (q["categoria"] ?? "").Trim();
            if (categoria.Length > 60 || !Regex.IsMatch(categoria, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s\-\.]*$"))
                throw new ArgumentException("La categoría de gasto adicional es inválida (máx. 60 caracteres). ");

            string titulo = string.IsNullOrWhiteSpace(q["titulo"]) ? "Reporte Personalizado" : q["titulo"].Trim();
            if (titulo.Length < 5 || titulo.Length > 120)
                throw new ArgumentException("El título del reporte debe tener entre 5 y 120 caracteres.");

            string formato = (q["formato"] ?? "excel").Trim().ToLowerInvariant();
            if (formato != "excel" && formato != "pdf")
                throw new ArgumentException("El formato de exportación personalizado no es válido.");

            var columnasSolicitadas = (q["cols"] ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var columnasValidas = new HashSet<string>(ColumnasPersonalizadas.Select(c => c.Item1), StringComparer.OrdinalIgnoreCase);
            var columnasInvalidas = columnasSolicitadas.Where(c => !columnasValidas.Contains(c)).ToList();
            if (columnasInvalidas.Any())
                throw new ArgumentException("Se enviaron columnas inválidas en el reporte personalizado.");

            return new FiltrosPersonalizado
            {
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta,
                Estado = estado,
                IdConductor = idConductor,
                IdCliente = idCliente,
                PlacaTracto = placaTracto,
                Categoria = categoria,
                Orden = orden,
                Factor = factor,
                Titulo = titulo,
                IncluirTotales = q["totales"] != "0",
                IncluirResumen = q["resumen"] != "0",
                Columnas = columnasSolicitadas,
                Formato = formato
            };
        }

        private DataTable ObtenerReportePersonalizado(FiltrosPersonalizado f)
        {
            DataTable dt = new DataTable();

            string orderBy;
            switch (f.Orden)
            {
                case "fecha_asc": orderBy = "ov.fechaSalida ASC"; break;
                case "conductor": orderBy = "Conductor ASC, ov.fechaSalida DESC"; break;
                case "cliente": orderBy = "Cliente ASC, ov.fechaSalida DESC"; break;
                default: orderBy = "ov.fechaSalida DESC"; break;
            }

            StringBuilder sb = new StringBuilder(@"
                SELECT
                    c.DNI AS DNI,
                    CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno,'')) AS Conductor,
                    ov.fechaSalida AS FechaSalida,
                    ov.fechaLlegada AS FechaLlegada,
                    ov.horaSalida AS HoraSalida,
                    ov.horaLlegada AS HoraLlegada,
                    ISNULL(t.placaTracto, 'N/A') AS PlacaTracto,
                    ISNULL(ca.placaCarreta, 'N/A') AS PlacaCarreta,
                    ISNULL((SELECT TOP 1 cl.nombre FROM Despachos d
                                INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                            WHERE d.idViajeProgreso = ov.idViajeProgreso AND d.activo = 1
                            ORDER BY d.idDespacho DESC), 'N/A') AS Cliente,
                    ISNULL((SELECT TOP 1 d.lugarOperacion FROM Despachos d
                            WHERE d.idViajeProgreso = ov.idViajeProgreso AND d.activo = 1
                            ORDER BY d.idDespacho DESC), 'N/A') AS Destino,
                    ov.numeroOrdenViaje AS NumeroLiquidacion,
                    ov.estadoViaje AS Estado,
                    ISNULL(ov.observaciones, '') AS Observaciones,

                    -- Ingresos por categoría
                    ISNULL((SELECT i.despachoSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IDespachoSoles,
                    ISNULL((SELECT i.despachoDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IDespachoDolares,
                    ISNULL((SELECT i.prestamoSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IPrestamoSoles,
                    ISNULL((SELECT i.prestamosDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IPrestamoDolares,
                    ISNULL((SELECT i.mensualidadSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IMensualidadSoles,
                    ISNULL((SELECT i.mensualidadDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IMensualidadDolares,
                    ISNULL((SELECT i.otrosSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IOtrosSoles,
                    ISNULL((SELECT i.otrosDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IOtrosDolares,
                    ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IAdicionalesSoles,
                    ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IAdicionalesDolares,
                    ISNULL(STUFF((SELECT '; ' + ia.nombreCategoria + ' S/' + CONVERT(varchar, CAST(ISNULL(ia.soles,0) AS decimal(18,2)))
                                        + CASE WHEN ISNULL(ia.dolares,0) > 0 THEN ' / $' + CONVERT(varchar, CAST(ia.dolares AS decimal(18,2))) ELSE '' END
                                 FROM IngresosAdicionales ia
                                 WHERE ia.numeroOrdenViaje = ov.numeroOrdenViaje
                                   AND (ISNULL(ia.soles,0) > 0 OR ISNULL(ia.dolares,0) > 0)
                                 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS IAdicionalesDetalle,

                    -- Gastos por categoría
                    ISNULL((SELECT e.peajesSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GPeajesSoles,
                    ISNULL((SELECT e.peajesDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GPeajesDolares,
                    ISNULL((SELECT e.alimentacionSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAlimentacionSoles,
                    ISNULL((SELECT e.alimentacionDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAlimentacionDolares,
                    ISNULL((SELECT e.apoyoseguridadSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GApoyoSeguridadSoles,
                    ISNULL((SELECT e.apoyoseguridadDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GApoyoSeguridadDolares,
                    ISNULL((SELECT e.reparacionesVariosSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GReparacionesSoles,
                    ISNULL((SELECT e.repacionesVariosDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GReparacionesDolares,
                    ISNULL((SELECT e.movilidadSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GMovilidadSoles,
                    ISNULL((SELECT e.movilidadDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GMovilidadDolares,
                    ISNULL((SELECT e.hospedajeSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GHospedajeSoles,
                    ISNULL((SELECT e.hospedajeDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GHospedajeDolares,
                    ISNULL((SELECT e.combustibleSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GCombustibleSoles,
                    ISNULL((SELECT e.combustibleDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GCombustibleDolares,
                    ISNULL((SELECT e.encarpada_desencarpadaSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GEncarpadaSoles,
                    ISNULL((SELECT e.encarpada_desencarpadaDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GEncarpadaDolares,
                    ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAdicionalesSoles,
                    ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAdicionalesDolares,
                    ISNULL(STUFF((SELECT '; ' + gca.nombreCategoria + ' S/' + CONVERT(varchar, CAST(ISNULL(gca.soles,0) AS decimal(18,2)))
                                        + CASE WHEN ISNULL(gca.dolares,0) > 0 THEN ' / $' + CONVERT(varchar, CAST(gca.dolares AS decimal(18,2))) ELSE '' END
                                 FROM CategoriasAdicionales gca
                                 WHERE gca.numeroOrdenViaje = ov.numeroOrdenViaje
                                   AND (ISNULL(gca.soles,0) > 0 OR ISNULL(gca.dolares,0) > 0)
                                 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS GAdicionalesDetalle,

                    -- Totales agregados (para columnas agregadas y balance)
                    ISNULL((SELECT ISNULL(i.despachoSoles,0) + ISNULL(i.prestamoSoles,0)
                                   + ISNULL(i.mensualidadSoles,0) + ISNULL(i.otrosSoles,0)
                            FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(soles) FROM IngresosAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosSoles,

                    ISNULL((SELECT ISNULL(i.despachoDolares,0) + ISNULL(i.prestamosDolares,0)
                                   + ISNULL(i.mensualidadDolares,0) + ISNULL(i.otrosDolares,0)
                            FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosDolares,

                    ISNULL((SELECT ISNULL(e.peajesSoles,0) + ISNULL(e.alimentacionSoles,0)
                                   + ISNULL(e.apoyoseguridadSoles,0) + ISNULL(e.reparacionesVariosSoles,0)
                                   + ISNULL(e.movilidadSoles,0) + ISNULL(e.hospedajeSoles,0)
                                   + ISNULL(e.combustibleSoles,0) + ISNULL(e.encarpada_desencarpadaSoles,0)
                            FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosSoles,

                    ISNULL((SELECT ISNULL(e.peajesDolares,0) + ISNULL(e.alimentacionDolares,0)
                                   + ISNULL(e.apoyoseguridadDolares,0) + ISNULL(e.repacionesVariosDolares,0)
                                   + ISNULL(e.movilidadDolares,0) + ISNULL(e.hospedajeDolares,0)
                                   + ISNULL(e.combustibleDolares,0) + ISNULL(e.encarpada_desencarpadaDolares,0)
                            FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosDolares,

                    ISNULL((SELECT dr.descuentoSoles FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoSoles,
                    ISNULL((SELECT dr.descuentoDolares FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoDolares,
                    ISNULL((SELECT dr.reintegroSoles FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroSoles,
                    ISNULL((SELECT dr.reintegroDolares FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroDolares

                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
                LEFT JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                WHERE ov.fechaSalida BETWEEN @FechaDesde AND @FechaHasta ");

            if (!string.IsNullOrEmpty(f.Estado) && f.Estado != "TODOS")
                sb.Append(" AND ov.estadoViaje = @Estado ");
            if (f.IdConductor > 0)
                sb.Append(" AND ov.idConductor = @IdConductor ");
            if (!string.IsNullOrEmpty(f.PlacaTracto))
                sb.Append(" AND t.placaTracto LIKE @PlacaTracto ");
            if (f.IdCliente > 0)
                sb.Append(@" AND EXISTS (SELECT 1 FROM Despachos d
                                         WHERE d.idViajeProgreso = ov.idViajeProgreso
                                           AND d.activo = 1 AND d.idCliente = @IdCliente) ");
            if (!string.IsNullOrEmpty(f.Categoria))
                sb.Append(@" AND EXISTS (SELECT 1 FROM CategoriasAdicionales gca
                                         WHERE gca.numeroOrdenViaje = ov.numeroOrdenViaje
                                           AND gca.nombreCategoria LIKE @Categoria
                                           AND (ISNULL(gca.soles,0) > 0 OR ISNULL(gca.dolares,0) > 0)) ");

            sb.Append(" ORDER BY ").Append(orderBy);

            using (SqlConnection conn = new SqlConnection(connectionString))
            using (SqlCommand cmd = new SqlCommand(sb.ToString(), conn))
            {
                cmd.Parameters.AddWithValue("@FechaDesde", f.FechaDesde);
                cmd.Parameters.AddWithValue("@FechaHasta", f.FechaHasta);
                if (!string.IsNullOrEmpty(f.Estado) && f.Estado != "TODOS")
                    cmd.Parameters.AddWithValue("@Estado", f.Estado);
                if (f.IdConductor > 0)
                    cmd.Parameters.AddWithValue("@IdConductor", f.IdConductor);
                if (!string.IsNullOrEmpty(f.PlacaTracto))
                    cmd.Parameters.AddWithValue("@PlacaTracto", "%" + f.PlacaTracto + "%");
                if (f.IdCliente > 0)
                    cmd.Parameters.AddWithValue("@IdCliente", f.IdCliente);
                if (!string.IsNullOrEmpty(f.Categoria))
                    cmd.Parameters.AddWithValue("@Categoria", "%" + f.Categoria + "%");

                new SqlDataAdapter(cmd).Fill(dt);
            }

            // Balance Neto = Ingresos - Gastos - Descuento + Reintegro
            dt.Columns.Add("BalanceSoles", typeof(decimal));
            dt.Columns.Add("BalanceDolares", typeof(decimal));
            foreach (DataRow r in dt.Rows)
            {
                decimal ingS = Convert.ToDecimal(r["IngresosSoles"]);
                decimal gasS = Convert.ToDecimal(r["GastosSoles"]);
                decimal desS = Convert.ToDecimal(r["DescuentoSoles"]);
                decimal reiS = Convert.ToDecimal(r["ReintegroSoles"]);
                decimal ingD = Convert.ToDecimal(r["IngresosDolares"]);
                decimal gasD = Convert.ToDecimal(r["GastosDolares"]);
                decimal desD = Convert.ToDecimal(r["DescuentoDolares"]);
                decimal reiD = Convert.ToDecimal(r["ReintegroDolares"]);
                r["BalanceSoles"] = ingS - gasS - desS + reiS;
                r["BalanceDolares"] = ingD - gasD - desD + reiD;
            }
            return dt;
        }

        /// <summary>Mapea la clave de columna al nombre de la columna del DataTable.</summary>
        private static string ColumnaDataKey(string key)
        {
            switch (key)
            {
                case "dni": return "DNI";
                case "conductor": return "Conductor";
                case "fechaSalida": return "FechaSalida";
                case "fechaLlegada": return "FechaLlegada";
                case "horaSalida": return "HoraSalida";
                case "horaLlegada": return "HoraLlegada";
                case "tracto": return "PlacaTracto";
                case "carreta": return "PlacaCarreta";
                case "cliente": return "Cliente";
                case "destino": return "Destino";
                case "numero": return "NumeroLiquidacion";
                case "estado": return "Estado";
                case "ingresosSoles": return "IngresosSoles";
                case "ingresosDolares": return "IngresosDolares";
                case "gastosSoles": return "GastosSoles";
                case "gastosDolares": return "GastosDolares";
                case "descuentoSoles": return "DescuentoSoles";
                case "descuentoDolares": return "DescuentoDolares";
                case "reintegroSoles": return "ReintegroSoles";
                case "reintegroDolares": return "ReintegroDolares";
                case "balanceSoles": return "BalanceSoles";
                case "balanceDolares": return "BalanceDolares";
                case "iDespachoS": return "IDespachoSoles";
                case "iDespachoD": return "IDespachoDolares";
                case "iPrestamoS": return "IPrestamoSoles";
                case "iPrestamoD": return "IPrestamoDolares";
                case "iMensualidadS": return "IMensualidadSoles";
                case "iMensualidadD": return "IMensualidadDolares";
                case "iOtrosS": return "IOtrosSoles";
                case "iOtrosD": return "IOtrosDolares";
                case "iAdicionalesS": return "IAdicionalesSoles";
                case "iAdicionalesD": return "IAdicionalesDolares";
                case "iAdicionalesDet": return "IAdicionalesDetalle";
                case "gPeajesS": return "GPeajesSoles";
                case "gPeajesD": return "GPeajesDolares";
                case "gAlimentacionS": return "GAlimentacionSoles";
                case "gAlimentacionD": return "GAlimentacionDolares";
                case "gApoyoSeguridadS": return "GApoyoSeguridadSoles";
                case "gApoyoSeguridadD": return "GApoyoSeguridadDolares";
                case "gReparacionesS": return "GReparacionesSoles";
                case "gReparacionesD": return "GReparacionesDolares";
                case "gMovilidadS": return "GMovilidadSoles";
                case "gMovilidadD": return "GMovilidadDolares";
                case "gHospedajeS": return "GHospedajeSoles";
                case "gHospedajeD": return "GHospedajeDolares";
                case "gCombustibleS": return "GCombustibleSoles";
                case "gCombustibleD": return "GCombustibleDolares";
                case "gEncarpadaS": return "GEncarpadaSoles";
                case "gEncarpadaD": return "GEncarpadaDolares";
                case "gAdicionalesS": return "GAdicionalesSoles";
                case "gAdicionalesD": return "GAdicionalesDolares";
                case "gAdicionalesDet": return "GAdicionalesDetalle";
                case "observaciones": return "Observaciones";
                default: return null;
            }
        }

        private void GenerarReportePersonalizado()
        {
            try
            {
                var f = LeerFiltrosPersonalizado();
                DataTable dt = ObtenerReportePersonalizado(f);

                // Preservar el orden declarado en ColumnasPersonalizadas para salida ordenada
                var colsOrdenadas = ColumnasPersonalizadas
                    .Where(c => f.Columnas.Contains(c.Item1))
                    .ToList();

                if (colsOrdenadas.Count == 0)
                {
                    // Al menos mostrar conductor + número
                    colsOrdenadas = ColumnasPersonalizadas
                        .Where(c => c.Item1 == "conductor" || c.Item1 == "numero").ToList();
                }

                if (f.Formato == "pdf")
                    ExportarPersonalizadoPDF(dt, colsOrdenadas, f);
                else
                    ExportarPersonalizadoExcel(dt, colsOrdenadas, f);
            }
            catch (System.Threading.ThreadAbortException) { /* normal */ }
            catch (ArgumentException ex)
            {
                ResponderSolicitudInvalida(ex.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error reporte personalizado: " + ex.Message);
                throw;
            }
        }

        private void ExportarPersonalizadoExcel(DataTable dt,
            List<Tuple<string, string, string>> cols, FiltrosPersonalizado f)
        {
            byte[] excelBytes;
            int colCount = cols.Count;

            using (XLWorkbook wb = new XLWorkbook())
            {
                var ws = wb.Worksheets.Add("Reporte Personalizado");

                // Encabezados del reporte
                ws.Cell(1, 1).Value = "SISTEMA SGV";
                ws.Range(1, 1, 1, colCount).Merge();
                ws.Cell(1, 1).Style.Font.Bold = true;
                ws.Cell(1, 1).Style.Font.FontSize = 14;
                ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;
                ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e40af");
                ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Row(1).Height = 30;

                ws.Cell(2, 1).Value = f.Titulo;
                ws.Range(2, 1, 2, colCount).Merge();
                ws.Cell(2, 1).Style.Font.Bold = true;
                ws.Cell(2, 1).Style.Font.FontSize = 12;
                ws.Cell(2, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#e2e8f0");
                ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(3, 1).Value = $"Período: {f.FechaDesde:dd/MM/yyyy} - {f.FechaHasta:dd/MM/yyyy}  |  Estado: {f.Estado}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
                ws.Range(3, 1, 3, colCount).Merge();
                ws.Cell(3, 1).Style.Font.Italic = true;
                ws.Cell(3, 1).Style.Font.FontSize = 10;
                ws.Cell(3, 1).Style.Font.FontColor = XLColor.FromHtml("#64748b");
                ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Encabezados de columnas
                int headerRow = 5;
                for (int i = 0; i < colCount; i++)
                    ws.Cell(headerRow, i + 1).Value = cols[i].Item2;

                var headerRange = ws.Range(headerRow, 1, headerRow, colCount);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Font.FontColor = XLColor.White;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#334155");
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Row(headerRow).Height = 25;

                // Totales por columna
                var totales = new decimal[colCount];
                bool[] esMoneda = new bool[colCount];
                for (int i = 0; i < colCount; i++)
                    esMoneda[i] = cols[i].Item3 == "money-sol" || cols[i].Item3 == "money-dol";

                int row = headerRow + 1;
                bool alt = false;
                foreach (DataRow dr in dt.Rows)
                {
                    for (int i = 0; i < colCount; i++)
                    {
                        var col = cols[i];
                        string dkey = ColumnaDataKey(col.Item1);
                        object v = dkey != null && dt.Columns.Contains(dkey) ? dr[dkey] : null;
                        var cell = ws.Cell(row, i + 1);

                        if (v == null || v == DBNull.Value) { cell.Value = ""; }
                        else
                        {
                            switch (col.Item3)
                            {
                                case "date":
                                    cell.Value = Convert.ToDateTime(v).ToString("dd/MM/yyyy");
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    break;
                                case "time":
                                    cell.Value = v.ToString();
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    break;
                                case "money-sol":
                                case "money-dol":
                                    decimal mv = Convert.ToDecimal(v);
                                    cell.Value = mv;
                                    cell.Style.NumberFormat.Format = "#,##0.00";
                                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                    if (mv < 0) cell.Style.Font.FontColor = XLColor.FromHtml("#dc2626");
                                    else if (mv > 0 && (col.Item1.StartsWith("reintegro") || col.Item1.StartsWith("balance")))
                                        cell.Style.Font.FontColor = XLColor.FromHtml("#2563eb");
                                    totales[i] += mv;
                                    break;
                                default:
                                    cell.Value = v.ToString();
                                    break;
                            }
                        }
                    }

                    if (alt) ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#f8fafc");
                    alt = !alt;
                    ws.Range(row, 1, row, colCount).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                    ws.Range(row, 1, row, colCount).Style.Border.BottomBorderColor = XLColor.FromHtml("#e2e8f0");
                    row++;
                }

                // Fila de totales
                if (f.IncluirTotales && dt.Rows.Count > 0)
                {
                    var totalRange = ws.Range(row, 1, row, colCount);
                    totalRange.Style.Font.Bold = true;
                    totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#f1f5f9");
                    totalRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
                    totalRange.Style.Border.TopBorderColor = XLColor.FromHtml("#334155");

                    // Colocar etiqueta en primera columna no monetaria, o en la primera
                    int labelIdx = 0;
                    for (int i = 0; i < colCount; i++) { if (!esMoneda[i]) { labelIdx = i; break; } }
                    ws.Cell(row, labelIdx + 1).Value = "TOTAL:";
                    ws.Cell(row, labelIdx + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    for (int i = 0; i < colCount; i++)
                    {
                        if (esMoneda[i])
                        {
                            ws.Cell(row, i + 1).Value = totales[i];
                            ws.Cell(row, i + 1).Style.NumberFormat.Format = "#,##0.00";
                            ws.Cell(row, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                    }
                    row++;
                }

                // Resumen ejecutivo
                if (f.IncluirResumen)
                {
                    row += 1;
                    ws.Cell(row, 1).Value = "RESUMEN EJECUTIVO";
                    ws.Range(row, 1, row, colCount).Merge();
                    ws.Cell(row, 1).Style.Font.Bold = true;
                    ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
                    ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#059669");
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    row++;

                    decimal totIngS = SumaColumna(dt, "IngresosSoles");
                    decimal totGasS = SumaColumna(dt, "GastosSoles");
                    decimal totDesS = SumaColumna(dt, "DescuentoSoles");
                    decimal totReiS = SumaColumna(dt, "ReintegroSoles");
                    decimal totBalS = SumaColumna(dt, "BalanceSoles");
                    decimal totIngD = SumaColumna(dt, "IngresosDolares");
                    decimal totGasD = SumaColumna(dt, "GastosDolares");
                    decimal totBalD = SumaColumna(dt, "BalanceDolares");

                    string[,] resumen = {
                        { "Total de liquidaciones:",        dt.Rows.Count.ToString() },
                        { "Total Ingresos (S/):",           $"S/ {totIngS:N2}" },
                        { "Total Gastos (S/):",             $"S/ {totGasS:N2}" },
                        { "Total Descuentos (S/):",         $"S/ {totDesS:N2}" },
                        { "Total Reintegros (S/):",         $"S/ {totReiS:N2}" },
                        { "Total Balance Neto (S/):",       $"S/ {totBalS:N2}" },
                        { "Total Ingresos ($):",            $"$ {totIngD:N2}" },
                        { "Total Gastos ($):",              $"$ {totGasD:N2}" },
                        { "Total Balance Neto ($):",        $"$ {totBalD:N2}" },
                        { "Factor de conversión:",          f.Factor.ToString("0.00") },
                        { "Balance General en Soles:",      $"S/ {(totBalS + totBalD * f.Factor):N2}" },
                    };

                    int halfA = Math.Max(1, colCount / 2);
                    for (int i = 0; i < resumen.GetLength(0); i++)
                    {
                        ws.Cell(row, 1).Value = resumen[i, 0];
                        ws.Cell(row, 1).Style.Font.Bold = true;
                        if (halfA > 1) ws.Range(row, 1, row, halfA).Merge();
                        ws.Cell(row, halfA + 1).Value = resumen[i, 1];
                        ws.Cell(row, halfA + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        if (halfA + 1 < colCount) ws.Range(row, halfA + 1, row, colCount).Merge();
                        ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor =
                            XLColor.FromHtml(i % 2 == 0 ? "#f0fdf4" : "#ecfdf5");
                        ws.Range(row, 1, row, colCount).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                        row++;
                    }
                }

                ws.Columns().AdjustToContents();
                foreach (var idx in Enumerable.Range(1, colCount))
                    if (ws.Column(idx).Width < 14) ws.Column(idx).Width = 14;

                using (MemoryStream ms = new MemoryStream())
                {
                    wb.SaveAs(ms);
                    excelBytes = ms.ToArray();
                }
            }

            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            HttpContext.Current.Response.AddHeader("Content-Disposition",
                $"attachment; filename=ReportePersonalizado_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            HttpContext.Current.Response.BinaryWrite(excelBytes);
            HttpContext.Current.Response.Flush();
            HttpContext.Current.Response.End();
        }

        private void ExportarPersonalizadoPDF(DataTable dt,
            List<Tuple<string, string, string>> cols, FiltrosPersonalizado f)
        {
            byte[] pdfBytes;
            int colCount = cols.Count;

            using (MemoryStream ms = new MemoryStream())
            {
                iTextDocument document = new iTextDocument(iTextPageSize.A4.Rotate(), 25, 25, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);
                writer.CloseStream = false;
                document.Open();

                BaseColor headerBg = new BaseColor(30, 41, 59);
                BaseColor accentBlue = new BaseColor(30, 64, 175);
                BaseColor accentGreen = new BaseColor(5, 150, 105);
                BaseColor altRow = new BaseColor(248, 250, 252);
                BaseColor borderColor = new BaseColor(226, 232, 240);
                BaseColor redColor = new BaseColor(220, 38, 38);
                BaseColor blueColor = new BaseColor(37, 99, 235);

                // Banner
                PdfPTable headerTable = new PdfPTable(1) { WidthPercentage = 100 };
                PdfPCell brandCell = new PdfPCell(new Phrase("SISTEMA SGV",
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.WHITE)))
                {
                    BackgroundColor = accentBlue,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    Padding = 12,
                    Border = 0
                };
                headerTable.AddCell(brandCell);
                document.Add(headerTable);

                iTextParagraph title = new iTextParagraph(f.Titulo,
                    FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(30, 41, 59)))
                { Alignment = Element.ALIGN_CENTER, SpacingBefore = 12 };
                document.Add(title);

                iTextParagraph subtitle = new iTextParagraph(
                    $"Período: {f.FechaDesde:dd/MM/yyyy} - {f.FechaHasta:dd/MM/yyyy}  |  Estado: {f.Estado}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm}",
                    FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(100, 116, 139)))
                { Alignment = Element.ALIGN_CENTER, SpacingAfter = 15 };
                document.Add(subtitle);

                // Tabla
                PdfPTable table = new PdfPTable(colCount) { WidthPercentage = 100 };
                float[] widths = new float[colCount];
                for (int i = 0; i < colCount; i++)
                {
                    string t = cols[i].Item3;
                    widths[i] = (t == "money-sol" || t == "money-dol") ? 14f :
                                t == "date" || t == "time" ? 12f :
                                cols[i].Item1 == "conductor" || cols[i].Item1 == "cliente" ||
                                cols[i].Item1 == "destino" || cols[i].Item1 == "observaciones" ? 22f : 12f;
                }
                table.SetWidths(widths);

                iTextFont headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, BaseColor.WHITE);
                foreach (var c in cols)
                {
                    PdfPCell h = new PdfPCell(new Phrase(c.Item2, headerFont))
                    {
                        BackgroundColor = headerBg,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        VerticalAlignment = Element.ALIGN_MIDDLE,
                        Padding = 6,
                        BorderColor = headerBg
                    };
                    table.AddCell(h);
                }

                iTextFont dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 8);
                var totales = new decimal[colCount];
                bool[] esMoneda = new bool[colCount];
                for (int i = 0; i < colCount; i++)
                    esMoneda[i] = cols[i].Item3 == "money-sol" || cols[i].Item3 == "money-dol";

                bool alt = false;
                foreach (DataRow dr in dt.Rows)
                {
                    BaseColor rowBg = alt ? altRow : BaseColor.WHITE;
                    for (int i = 0; i < colCount; i++)
                    {
                        var col = cols[i];
                        string dkey = ColumnaDataKey(col.Item1);
                        object v = dkey != null && dt.Columns.Contains(dkey) ? dr[dkey] : null;
                        string texto = "";
                        int align = Element.ALIGN_LEFT;
                        BaseColor fontColor = new BaseColor(30, 41, 59);
                        bool bold = false;

                        if (v != null && v != DBNull.Value)
                        {
                            switch (col.Item3)
                            {
                                case "date":
                                    texto = Convert.ToDateTime(v).ToString("dd/MM/yyyy");
                                    align = Element.ALIGN_CENTER; break;
                                case "time":
                                    texto = v.ToString(); align = Element.ALIGN_CENTER; break;
                                case "money-sol":
                                case "money-dol":
                                    decimal mv = Convert.ToDecimal(v);
                                    string sym = col.Item3 == "money-sol" ? "S/" : "$";
                                    texto = mv < 0 ? $"-{sym} {Math.Abs(mv):N2}" : $"{sym} {mv:N2}";
                                    align = Element.ALIGN_RIGHT;
                                    bold = true;
                                    if (mv < 0) fontColor = redColor;
                                    else if (mv > 0 && (col.Item1.StartsWith("reintegro") || col.Item1.StartsWith("balance")))
                                        fontColor = blueColor;
                                    totales[i] += mv;
                                    break;
                                default:
                                    texto = v.ToString();
                                    align = col.Item1 == "numero" || col.Item1 == "estado" ||
                                            col.Item1 == "dni" ? Element.ALIGN_CENTER : Element.ALIGN_LEFT;
                                    break;
                            }
                        }

                        iTextFont cellFont = bold
                            ? FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8, fontColor)
                            : FontFactory.GetFont(FontFactory.HELVETICA, 8, fontColor);

                        PdfPCell cell = new PdfPCell(new Phrase(texto, cellFont))
                        {
                            HorizontalAlignment = align,
                            VerticalAlignment = Element.ALIGN_MIDDLE,
                            Padding = 5,
                            BackgroundColor = rowBg,
                            BorderColor = borderColor
                        };
                        table.AddCell(cell);
                    }
                    alt = !alt;
                }

                // Totales
                if (f.IncluirTotales && dt.Rows.Count > 0)
                {
                    iTextFont totalFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, new BaseColor(30, 41, 59));
                    BaseColor totalBg = new BaseColor(241, 245, 249);
                    int labelIdx = 0;
                    for (int i = 0; i < colCount; i++) { if (!esMoneda[i]) { labelIdx = i; break; } }

                    for (int i = 0; i < colCount; i++)
                    {
                        string texto = "";
                        int align = Element.ALIGN_LEFT;
                        if (i == labelIdx) { texto = "TOTAL:"; align = Element.ALIGN_RIGHT; }
                        else if (esMoneda[i])
                        {
                            string sym = cols[i].Item3 == "money-sol" ? "S/" : "$";
                            decimal mv = totales[i];
                            texto = mv < 0 ? $"-{sym} {Math.Abs(mv):N2}" : $"{sym} {mv:N2}";
                            align = Element.ALIGN_RIGHT;
                        }
                        PdfPCell tc = new PdfPCell(new Phrase(texto, totalFont))
                        {
                            BackgroundColor = totalBg,
                            HorizontalAlignment = align,
                            Padding = 7,
                            BorderColor = headerBg,
                            BorderWidthTop = 2,
                            BorderWidthBottom = 2
                        };
                        table.AddCell(tc);
                    }
                }

                document.Add(table);

                // Resumen
                if (f.IncluirResumen)
                {
                    document.Add(new iTextParagraph(" "));
                    PdfPTable rh = new PdfPTable(1) { WidthPercentage = 70, HorizontalAlignment = Element.ALIGN_CENTER };
                    PdfPCell rhc = new PdfPCell(new Phrase("RESUMEN EJECUTIVO",
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.WHITE)))
                    {
                        BackgroundColor = accentGreen,
                        HorizontalAlignment = Element.ALIGN_CENTER,
                        Padding = 10,
                        Border = 0
                    };
                    rh.AddCell(rhc);
                    rh.SpacingBefore = 10;
                    rh.SpacingAfter = 5;
                    document.Add(rh);

                    decimal totIngS = SumaColumna(dt, "IngresosSoles");
                    decimal totGasS = SumaColumna(dt, "GastosSoles");
                    decimal totDesS = SumaColumna(dt, "DescuentoSoles");
                    decimal totReiS = SumaColumna(dt, "ReintegroSoles");
                    decimal totBalS = SumaColumna(dt, "BalanceSoles");
                    decimal totIngD = SumaColumna(dt, "IngresosDolares");
                    decimal totGasD = SumaColumna(dt, "GastosDolares");
                    decimal totBalD = SumaColumna(dt, "BalanceDolares");
                    decimal balanceGeneral = totBalS + totBalD * f.Factor;

                    string[,] resumen = {
                        { "Total de liquidaciones:",        dt.Rows.Count.ToString() },
                        { "Total Ingresos (S/):",           $"S/ {totIngS:N2}" },
                        { "Total Gastos (S/):",             $"S/ {totGasS:N2}" },
                        { "Total Descuentos (S/):",         $"S/ {totDesS:N2}" },
                        { "Total Reintegros (S/):",         $"S/ {totReiS:N2}" },
                        { "Total Balance Neto (S/):",       $"S/ {totBalS:N2}" },
                        { "Total Ingresos ($):",            $"$ {totIngD:N2}" },
                        { "Total Gastos ($):",              $"$ {totGasD:N2}" },
                        { "Total Balance Neto ($):",        $"$ {totBalD:N2}" },
                        { "Factor de conversión:",          f.Factor.ToString("0.00") },
                        { "BALANCE GENERAL EN SOLES:",      $"S/ {balanceGeneral:N2}" },
                    };

                    PdfPTable rt = new PdfPTable(2) { WidthPercentage = 70, HorizontalAlignment = Element.ALIGN_CENTER };
                    rt.SetWidths(new float[] { 60f, 40f });
                    iTextFont labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, new BaseColor(30, 41, 59));
                    iTextFont valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(30, 41, 59));
                    iTextFont totValFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, accentGreen);

                    for (int i = 0; i < resumen.GetLength(0); i++)
                    {
                        bool ultima = i == resumen.GetLength(0) - 1;
                        BaseColor bg = ultima ? new BaseColor(209, 250, 229) :
                            (i % 2 == 0 ? new BaseColor(240, 253, 244) : new BaseColor(236, 253, 245));

                        PdfPCell lc = new PdfPCell(new Phrase(resumen[i, 0], labelFont))
                        {
                            BackgroundColor = bg,
                            Padding = 7,
                            BorderColor = borderColor
                        };
                        PdfPCell vc = new PdfPCell(new Phrase(resumen[i, 1], ultima ? totValFont : valueFont))
                        {
                            BackgroundColor = bg,
                            HorizontalAlignment = Element.ALIGN_RIGHT,
                            Padding = 7,
                            BorderColor = borderColor
                        };
                        if (ultima)
                        {
                            lc.BorderColor = accentGreen;
                            vc.BorderColor = accentGreen;
                            lc.BorderWidthTop = 2;
                            vc.BorderWidthTop = 2;
                        }
                        rt.AddCell(lc);
                        rt.AddCell(vc);
                    }
                    document.Add(rt);
                }

                document.Close();
                writer.Close();
                pdfBytes = ms.ToArray();
            }

            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.ContentType = "application/pdf";
            HttpContext.Current.Response.AddHeader("Content-Disposition",
                $"attachment; filename=ReportePersonalizado_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            HttpContext.Current.Response.BinaryWrite(pdfBytes);
            HttpContext.Current.Response.Flush();
            HttpContext.Current.Response.End();
        }

        private static decimal SumaColumna(DataTable dt, string col)
        {
            if (!dt.Columns.Contains(col)) return 0m;
            decimal s = 0;
            foreach (DataRow r in dt.Rows)
                if (r[col] != DBNull.Value) s += Convert.ToDecimal(r[col]);
            return s;
        }

        #endregion

        #region UTILIDADES

        private bool TryParseFecha(string valor, string nombreCampo, out DateTime fecha, out string error)
        {
            error = null;
            fecha = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(valor))
            {
                error = $"{nombreCampo}: el valor es obligatorio.";
                return false;
            }

            if (!DateTime.TryParseExact(valor.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out fecha))
            {
                error = $"{nombreCampo}: formato inválido. Use aaaa-mm-dd.";
                return false;
            }

            DateTime min = new DateTime(2010, 1, 1);
            DateTime max = DateTime.Today.AddDays(1);
            if (fecha < min || fecha > max)
            {
                error = $"{nombreCampo}: fuera de rango permitido ({min:dd/MM/yyyy} - {max:dd/MM/yyyy}).";
                return false;
            }

            return true;
        }

        private bool TryParseFactor(string valor, out decimal factor, out string error)
        {
            error = null;
            factor = 3.75m;
            if (string.IsNullOrWhiteSpace(valor))
                return true;

            string normalizado = valor.Trim().Replace(',', '.');
            if (!decimal.TryParse(normalizado, NumberStyles.Number, CultureInfo.InvariantCulture, out factor))
            {
                error = "Factor de conversión: formato inválido.";
                return false;
            }

            if (factor < 0.01m || factor > 20m)
            {
                error = "Factor de conversión: debe estar entre 0.01 y 20.";
                return false;
            }

            return true;
        }

        private bool TryParseIdentificadorNoNegativo(string valor, string nombreCampo, out int id, out string error)
        {
            error = null;
            id = 0;
            if (string.IsNullOrWhiteSpace(valor))
                return true;

            if (!int.TryParse(valor.Trim(), out id) || id < 0)
            {
                error = $"{nombreCampo}: identificador inválido.";
                return false;
            }

            return true;
        }

        private bool ValidarFiltrosLiquidaciones(string fechaDesdeTexto, string fechaHastaTexto, string factorTexto,
            out DateTime fechaDesde, out DateTime fechaHasta, out decimal factorConversion, out string mensaje)
        {
            fechaDesde = DateTime.MinValue;
            fechaHasta = DateTime.MinValue;
            factorConversion = 3.75m;

            if (!TryParseFecha(fechaDesdeTexto, "Fecha desde", out fechaDesde, out mensaje))
                return false;

            if (!TryParseFecha(fechaHastaTexto, "Fecha hasta", out fechaHasta, out mensaje))
                return false;

            if (fechaDesde > fechaHasta)
            {
                mensaje = "Rango de fechas inválido: la fecha final debe ser mayor o igual a la fecha inicial.";
                return false;
            }

            if ((fechaHasta - fechaDesde).TotalDays > 3660)
            {
                mensaje = "El rango de fechas para liquidaciones no puede exceder 10 años.";
                return false;
            }

            if (!TryParseFactor(factorTexto, out factorConversion, out mensaje))
                return false;

            return true;
        }

        private bool ValidarFiltrosViajesActivos(string buscarConductor, string estadoViaje, out string mensaje)
        {
            mensaje = null;
            string texto = (buscarConductor ?? string.Empty).Trim();

            if (texto.Length > 100)
            {
                mensaje = "Buscar conductor: no debe superar 100 caracteres.";
                return false;
            }

            if (!Regex.IsMatch(texto, @"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s\-\.]*$"))
            {
                mensaje = "Buscar conductor: solo se permiten letras, números, espacios, punto y guion.";
                return false;
            }

            string estadoNormalizado = (estadoViaje ?? string.Empty).Trim().ToUpperInvariant();
            if (!EstadosViajePermitidos.Contains(estadoNormalizado))
            {
                mensaje = "Estado del viaje inválido. Seleccione Todos, Abierto o Cerrado.";
                return false;
            }

            return true;
        }

        private void ResponderSolicitudInvalida(string mensaje)
        {
            HttpContext.Current.Response.Clear();
            HttpContext.Current.Response.StatusCode = 400;
            HttpContext.Current.Response.ContentType = "text/plain";
            HttpContext.Current.Response.Write(mensaje);
            HttpContext.Current.Response.End();
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            pnlMensajes.Visible = true;
            lblMensaje.Text = $"<div class='alert alert-{tipo} alert-dismissible fade show' role='alert'>{mensaje}<button type='button' class='close' data-dismiss='alert'>&times;</button></div>";
        }

        #endregion
    }
}
