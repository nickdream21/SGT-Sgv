using ClosedXML.Excel;
using DocumentFormat.OpenXml.Math;
using DocumentFormat.OpenXml.Spreadsheet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebGrease.Activities;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class Reportes : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
            {
                // Establecer fechas predeterminadas (último mes)
                txtFechaDesde.Text = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
                txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");

                // Cargar datos iniciales en los dropdown
                CargarConductores();
                CargarVehiculos();
                //CargarPedidos();
                CargarProductos();
                CargarLugaresAbastecimiento();
                CargarTiposReporteDetalle("conductor"); // Por defecto carga los tipos de reporte para conductor

                // Registrar el script para sincronizar los filtros
                string script = @"
            function sincronizarFiltros() {
                // Obtener los elementos
                var tipoReporte = document.getElementById('" + ddlTipoReporteDetalle.ClientID + @"');
                var tipoTransaccion = document.getElementById('" + ddlTipoTransaccion.ClientID + @"');
                
                if (!tipoReporte || !tipoTransaccion) return;
                
                // Cuando cambia el tipo de reporte
                tipoReporte.addEventListener('change', function() {
                    var valorReporte = this.value;
                    
                    // Ajustar el tipo de transacción según el reporte seleccionado
                    if (valorReporte === 'ingresos_detalle') {
                        tipoTransaccion.value = 'Ingreso';
                    } 
                    else if (valorReporte === 'egresos_detalle') {
                        tipoTransaccion.value = 'Egreso';
                    }
                    // Para otros tipos de reporte, mantener el valor actual
                });
                
                // Opcionalmente, también puedes sincronizar en la dirección opuesta
                tipoTransaccion.addEventListener('change', function() {
                    var valorTransaccion = this.value;
                    var valorReporte = tipoReporte.value;
                    
                    // Si hay una incompatibilidad clara, ajustar
                    if (valorTransaccion === 'Ingreso' && valorReporte === 'egresos_detalle') {
                        tipoReporte.value = 'ingresos_detalle';
                    }
                    else if (valorTransaccion === 'Egreso' && valorReporte === 'ingresos_detalle') {
                        tipoReporte.value = 'egresos_detalle';
                    }
                });
            }
            
            // Ejecutar cuando la página esté completamente cargada
            window.addEventListener('load', sincronizarFiltros);
        ";

                // Registrar el script en la página
                ScriptManager.RegisterStartupScript(this, GetType(), "SincronizarFiltros", script, true);

            }
        }

        #region Carga de Datos Iniciales

        private void CargarConductores()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idConductor, 
                                    CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) as NombreCompleto 
                                    FROM Conductor 
                                    ORDER BY apPaterno, apMaterno, nombre";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    ddlConductor.DataSource = dt;
                    ddlConductor.DataBind();

                    // Agregar el item por defecto
                    ddlConductor.Items.Insert(0, new ListItem("Todos los conductores", ""));
                }
            }
            catch (Exception ex)
            {
                // En caso de error, simplemente asegurarse de que el dropdown tenga al menos un item
                if (ddlConductor.Items.Count == 0)
                    ddlConductor.Items.Add(new ListItem("Todos los conductores", ""));

                System.Diagnostics.Debug.WriteLine("Error al cargar conductores: " + ex.Message);
            }
        }

        private void CargarVehiculos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idTracto, placaTracto, marca, modelo 
                                  FROM Tracto 
                                  ORDER BY placaTracto";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    ddlVehiculo.DataSource = dt;
                    ddlVehiculo.DataBind();

                    // Agregar el item por defecto
                    ddlVehiculo.Items.Insert(0, new ListItem("Todos los vehículos", ""));
                }
            }
            catch (Exception ex)
            {
                // En caso de error, simplemente asegurarse de que el dropdown tenga al menos un item
                if (ddlVehiculo.Items.Count == 0)
                    ddlVehiculo.Items.Add(new ListItem("Todos los vehículos", ""));

                System.Diagnostics.Debug.WriteLine("Error al cargar vehículos: " + ex.Message);
            }
        }



        private void CargarProductos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idProducto, nombre 
                                  FROM Producto 
                                  ORDER BY nombre";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    ddlProducto.DataSource = dt;
                    ddlProducto.DataBind();

                    // Agregar el item por defecto
                    ddlProducto.Items.Insert(0, new ListItem("Todos los productos", ""));
                }
            }
            catch (Exception ex)
            {
                // En caso de error, simplemente asegurarse de que el dropdown tenga al menos un item
                if (ddlProducto.Items.Count == 0)
                    ddlProducto.Items.Add(new ListItem("Todos los productos", ""));

                System.Diagnostics.Debug.WriteLine("Error al cargar productos: " + ex.Message);
            }
        }

        private void CargarLugaresAbastecimiento()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idLugarAbastecimiento, nombreAbastecimiento 
                                  FROM LugarAbastecimiento 
                                  ORDER BY nombreAbastecimiento";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    ddlLugarAbastecimiento.DataSource = dt;
                    ddlLugarAbastecimiento.DataBind();

                    // Agregar el item por defecto
                    ddlLugarAbastecimiento.Items.Insert(0, new ListItem("Todos los lugares", ""));
                }
            }
            catch (Exception ex)
            {
                // En caso de error, simplemente asegurarse de que el dropdown tenga al menos un item
                if (ddlLugarAbastecimiento.Items.Count == 0)
                    ddlLugarAbastecimiento.Items.Add(new ListItem("Todos los lugares", ""));

                System.Diagnostics.Debug.WriteLine("Error al cargar lugares de abastecimiento: " + ex.Message);
            }
        }

        private void CargarTiposReporteDetalle(string tipoReporte)
        {
            ddlTipoReporteDetalle.Items.Clear();

            switch (tipoReporte)
            {
                case "conductor":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Viajes realizados", "viajes_conductor"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Productos transportados", "productos_conductor"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Ingresos y gastos", "financiero_conductor"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Rendimiento de combustible", "combustible_conductor"));
                    break;

                case "vehiculo":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Historial de viajes", "viajes_vehiculo"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Consumo de combustible", "combustible_vehiculo"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Rendimiento por ruta", "rendimiento_vehiculo"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Gastos de mantenimiento", "mantenimiento_vehiculo"));
                    break;

                case "pedido":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Detalle de pedido", "detalle_pedido"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Vehículos asignados", "vehiculos_pedido"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Conductores asignados", "conductores_pedido"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Balance financiero", "financiero_pedido"));
                    break;

                case "financiero":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Balance general", "balance_financiero"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Ingresos detallados", "ingresos_detalle"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Egresos detallados", "egresos_detalle"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Análisis comparativo", "comparativo_financiero"));
                    break;

                case "combustible":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Consumo general", "consumo_combustible"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Rendimiento por vehículo", "rendimiento_combustible"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Rendimiento por ruta", "ruta_combustible"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Análisis de sobrantes", "sobrante_combustible"));
                    break;

                case "producto":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Productos más transportados", "ranking_producto"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Productos por cliente", "cliente_producto"));
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Productos por destino", "destino_producto"));
                    break;

                case "personalizado":
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Reporte dinámico", "dinamico_personalizado"));
                    pnlFiltrosPersonalizados.Visible = true;
                    break;

                default:
                    ddlTipoReporteDetalle.Items.Add(new ListItem("Listado general", "listado_general"));
                    break;
            }
        }

        #endregion

        #region Eventos de Navegación y UI


        protected void lnkTipoReporte_Click(object sender, EventArgs e)
        {
            // Obtener el tipo de reporte seleccionado desde el CommandArgument
            LinkButton lnkButton = (LinkButton)sender;
            string tipoReporte = lnkButton.CommandArgument;

            // Actualizar título del reporte
            switch (tipoReporte)
            {
                case "conductor":
                    litTituloReporte.Text = "Reportes por Conductor";
                    break;
                case "vehiculo":
                    litTituloReporte.Text = "Reportes por Vehículo";
                    break;
                case "pedido":
                    litTituloReporte.Text = "Reportes por Pedido";
                    break;
                case "financiero":
                    litTituloReporte.Text = "Reportes Financieros";
                    break;
                case "combustible":
                    litTituloReporte.Text = "Reportes de Combustible";
                    break;
                case "producto":
                    litTituloReporte.Text = "Reportes por Producto";
                    break;
                case "personalizado":
                    litTituloReporte.Text = "Reporte Personalizado";
                    break;
            }

            // Cambiar clase active en los botones de navegación
            lnkConductor.CssClass = lnkConductor.CssClass.Replace(" active", "");
            lnkVehiculo.CssClass = lnkVehiculo.CssClass.Replace(" active", "");
            lnkPedido.CssClass = lnkPedido.CssClass.Replace(" active", "");
            lnkFinanciero.CssClass = lnkFinanciero.CssClass.Replace(" active", "");
            lnkCombustible.CssClass = lnkCombustible.CssClass.Replace(" active", "");
            lnkProducto.CssClass = lnkProducto.CssClass.Replace(" active", "");
            //lnkPersonalizado.CssClass = lnkPersonalizado.CssClass.Replace(" active", "");

            lnkButton.CssClass += " active";

            // Mostrar/Ocultar los filtros específicos según el tipo de reporte
            pnlFiltroConductor.Visible = (tipoReporte == "conductor");
            pnlFiltroVehiculo.Visible = (tipoReporte == "vehiculo");
            pnlFiltroPedido.Visible = (tipoReporte == "pedido");
            pnlFiltroFinanciero.Visible = (tipoReporte == "financiero");
            pnlFiltroCombustible.Visible = (tipoReporte == "combustible");
            pnlFiltroProducto.Visible = (tipoReporte == "producto");
            pnlFiltrosPersonalizados.Visible = (tipoReporte == "personalizado");

            // Mostrar/Ocultar los paneles de filtros avanzados correspondientes
            pnlFiltrosAvanzadosConductor.Visible = (tipoReporte == "conductor");
            pnlFiltrosAvanzadosVehiculo.Visible = (tipoReporte == "vehiculo");
            pnlFiltrosAvanzadosPedido.Visible = (tipoReporte == "pedido");
            pnlFiltrosAvanzadosFinanciero.Visible = (tipoReporte == "financiero");
            pnlFiltrosAvanzadosCombustible.Visible = (tipoReporte == "combustible");
            pnlFiltrosAvanzadosProducto.Visible = (tipoReporte == "producto");
            pnlFiltrosAvanzadosPersonalizado.Visible = (tipoReporte == "personalizado");

            // Cargar los tipos de reportes detallados
            CargarTiposReporteDetalle(tipoReporte);

            // Resetear los resultados
            pnlResultados.Visible = false;
        }

        protected void ddlTipoReporteDetalle_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Actualizar UI según el tipo de reporte detallado seleccionado
        }

        protected void btnLimpiarFiltros_Click(object sender, EventArgs e)
        {
            // Limpiar todos los filtros
            txtFechaDesde.Text = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
            txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");

            // Restablecer dropdowns a su valor inicial
            if (ddlConductor.Items.Count > 0) ddlConductor.SelectedIndex = 0;
            if (ddlVehiculo.Items.Count > 0) ddlVehiculo.SelectedIndex = 0;
            if (ddlTipoTransaccion.Items.Count > 0) ddlTipoTransaccion.SelectedIndex = 0;
            if (ddlLugarAbastecimiento.Items.Count > 0) ddlLugarAbastecimiento.SelectedIndex = 0;
            if (ddlProducto.Items.Count > 0) ddlProducto.SelectedIndex = 0;

            // Limpiar filtros avanzados
            txtDNIConductor.Text = "";
            txtNombreConductor.Text = "";
            txtPlacaVehiculo.Text = "";

            if (ddlMarcaVehiculo.Items.Count > 0) ddlMarcaVehiculo.SelectedIndex = 0;
            if (ddlModeloVehiculo.Items.Count > 0) ddlModeloVehiculo.SelectedIndex = 0;

            txtNumeroFactura.Text = "";
            txtValorMinimo.Text = "";
            txtValorMaximo.Text = "";

            if (ddlMoneda.Items.Count > 0) ddlMoneda.SelectedIndex = 0;

            txtMontoMinimo.Text = "";
            txtMontoMaximo.Text = "";
            txtProductoCombustible.Text = "";
            txtNumeroAbastecimiento.Text = "";
            txtGalonesMinimos.Text = "";
            txtRendimientoMinimo.Text = "";

            // Limpiar filtros personalizados
            if (pnlFiltrosPersonalizados.Visible)
            {
                chkConductorInfo.Checked = false;
                chkVehiculoInfo.Checked = false;
                chkRutaInfo.Checked = false;
                chkProductoInfo.Checked = false;
                chkIngresoInfo.Checked = false;
                chkEgresoInfo.Checked = false;
                chkCombustibleInfo.Checked = false;
                chkClienteInfo.Checked = false;
                chkFacturaInfo.Checked = false;

                if (ddlAgrupamiento.Items.Count > 0) ddlAgrupamiento.SelectedIndex = 0;
                if (ddlOrdenamiento.Items.Count > 0) ddlOrdenamiento.SelectedIndex = 0;
            }

            // Ocultar resultados
            pnlResultados.Visible = false;
        }

        protected void btnAplicarFiltrosAvanzados_Click(object sender, EventArgs e)
        {
            // Cierra el modal de filtros avanzados; los filtros se aplican al generar el reporte
        }

        protected void gvReporte_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvReporte.PageIndex = e.NewPageIndex;
            GenerarReporte(); // Volver a cargar los datos con la página actualizada
        }

        protected void gvReporte_Sorting(object sender, GridViewSortEventArgs e)
        {
            ViewState["SortExpression"] = e.SortExpression;
            ViewState["SortDirection"] =
                ViewState["SortExpression"] != null && ViewState["SortExpression"].ToString() == e.SortExpression &&
                ViewState["SortDirection"].ToString() == "ASC" ? "DESC" : "ASC";

            GenerarReporte(); // Volver a cargar los datos con el ordenamiento actualizado
        }

        // Modificar el método btnExportarExcel_Click para arreglar el problema de espacios y usar el nombre correcto
        protected void btnExportarExcel_Click(object sender, EventArgs e)
        {
            try
            {
                // Guardar el estado original de la paginación y otros estados
                bool paginacionOriginal = gvReporte.AllowPaging;
                int paginaOriginal = gvReporte.PageIndex;
                bool ordenamientoOriginal = gvReporte.AllowSorting;

                // 1. Ejecutar GenerarReporte() normalmente como está configurado en la UI
                GenerarReporte();

                // 2. Guardar la fuente de datos original COMPLETA antes de desactivar paginación
                object fuenteDatosCompleta = gvReporte.DataSource;
                DataTable datosCompletos = null;

                // Convertir la fuente de datos a DataTable independientemente de su tipo original
                if (fuenteDatosCompleta is DataTable)
                {
                    datosCompletos = (DataTable)fuenteDatosCompleta;
                }
                else if (fuenteDatosCompleta is DataView)
                {
                    datosCompletos = ((DataView)fuenteDatosCompleta).ToTable();
                }
                else if (fuenteDatosCompleta is IEnumerable)
                {
                    // En caso de otras colecciones, convertirlas a una tabla
                    DataTable dt = new DataTable();
                    PropertyInfo[] properties = null;

                    foreach (var item in (IEnumerable)fuenteDatosCompleta)
                    {
                        if (properties == null)
                        {
                            properties = item.GetType().GetProperties();
                            foreach (var prop in properties)
                            {
                                dt.Columns.Add(prop.Name, prop.PropertyType);
                            }
                        }

                        DataRow row = dt.NewRow();
                        foreach (var prop in properties)
                        {
                            row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                        }
                        dt.Rows.Add(row);
                    }

                    datosCompletos = dt;
                }

                // 3. Ahora desactivar paginación y reordenar para el GridView
                gvReporte.AllowPaging = false;
                gvReporte.AllowSorting = false;

                // 4. Reasignar la fuente de datos completa sin paginación
                gvReporte.DataSource = datosCompletos;
                gvReporte.DataBind();

                // 5. Continuar con la exportación a Excel
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Reporte");

                    string numeroPedido = txtNumeroPedido.Text.Trim();
                    string tipoReporte = ObtenerTipoReporteSeleccionado();

                    // Modificar el título si estamos filtrando por número de pedido
                    string tituloReporte = litTituloResultados.Text;
                    if (!string.IsNullOrEmpty(numeroPedido) && tipoReporte == "pedido")
                    {
                        tituloReporte = $"Reporte de Pedido: {numeroPedido}";
                    }

                    // Título del reporte
                    worksheet.Cell(1, 1).Value = tituloReporte;
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Font.FontSize = 14;
                    worksheet.Range(1, 1, 1, 15).Merge();
                    worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Añadir información del período
                    worksheet.Cell(2, 1).Value = "Período: " + txtFechaDesde.Text + " al " + txtFechaHasta.Text;
                    worksheet.Range(2, 1, 2, 15).Merge();
                    worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Añadir información adicional si es necesario
                    if (!string.IsNullOrEmpty(numeroPedido) && tipoReporte == "pedido")
                    {
                        worksheet.Cell(3, 1).Value = $"Número de Pedido: {numeroPedido}";
                        worksheet.Cell(3, 1).Style.Font.Bold = true;
                        worksheet.Range(3, 1, 3, 15).Merge();
                        worksheet.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    // Calcular la fila donde empiezan los encabezados
                    int headerRow = (!string.IsNullOrEmpty(numeroPedido) && tipoReporte == "pedido") ? 5 : 4;

                    // Obtener columnas visibles (excluyendo botones)
                    List<string> columnHeaders = new List<string>();
                    List<int> columnIndexes = new List<int>();

                    for (int i = 0; i < gvReporte.Columns.Count; i++)
                    {
                        if (!(gvReporte.Columns[i] is ButtonField))
                        {
                            columnHeaders.Add(gvReporte.Columns[i].HeaderText);
                            columnIndexes.Add(i);
                        }
                    }

                    // Encabezados
                    for (int i = 0; i < columnHeaders.Count; i++)
                    {
                        worksheet.Cell(headerRow, i + 1).Value = columnHeaders[i];
                        worksheet.Cell(headerRow, i + 1).Style.Font.Bold = true;
                        worksheet.Cell(headerRow, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                        worksheet.Cell(headerRow, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    // Datos - IMPORTANTE: ahora estamos usando el GridView sin paginación
                    for (int rowIndex = 0; rowIndex < gvReporte.Rows.Count; rowIndex++)
                    {
                        GridViewRow row = gvReporte.Rows[rowIndex];

                        for (int colIdx = 0; colIdx < columnIndexes.Count; colIdx++)
                        {
                            int originalColIndex = columnIndexes[colIdx];
                            string cellValue = "";

                            if (originalColIndex < row.Cells.Count)
                            {
                                if (row.Cells[originalColIndex].Controls.Count > 0)
                                {
                                    foreach (System.Web.UI.Control control in row.Cells[originalColIndex].Controls)
                                    {
                                        if (control is Label)
                                            cellValue = ((Label)control).Text;
                                        else if (control is LinkButton)
                                            cellValue = ((LinkButton)control).Text;
                                        else if (control is HyperLink)
                                            cellValue = ((HyperLink)control).Text;
                                    }
                                }
                                else
                                {
                                    cellValue = row.Cells[originalColIndex].Text;
                                }

                                // Solo decodificar HTML y eliminar &nbsp;, preservando espacios normales
                                cellValue = HttpUtility.HtmlDecode(cellValue).Replace("&nbsp;", "").Trim();
                            }

                            worksheet.Cell(rowIndex + headerRow + 1, colIdx + 1).Value = cellValue;
                            worksheet.Cell(rowIndex + headerRow + 1, colIdx + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                            // Destacar celdas según condiciones específicas
                            if (columnHeaders[colIdx] == "Nº Pedido" && cellValue == numeroPedido)
                            {
                                worksheet.Cell(rowIndex + headerRow + 1, colIdx + 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
                                worksheet.Cell(rowIndex + headerRow + 1, colIdx + 1).Style.Font.Bold = true;
                            }

                            // Alternar colores de fila
                            if (rowIndex % 2 == 1)
                            {
                                worksheet.Cell(rowIndex + headerRow + 1, colIdx + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F9F9F9");
                            }
                        }
                    }

                    // Resumen
                    int summaryRow = (gvReporte.Rows.Count > 0 ? gvReporte.Rows.Count : 0) + headerRow + 3;
                    worksheet.Cell(summaryRow, 1).Value = "Resumen";
                    worksheet.Cell(summaryRow, 1).Style.Font.Bold = true;
                    summaryRow++;

                    worksheet.Cell(summaryRow, 1).Value = "Total Ingresos:";
                    worksheet.Cell(summaryRow, 2).Value = litTotalIngresos.Text;
                    worksheet.Cell(summaryRow, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(summaryRow, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    summaryRow++;

                    worksheet.Cell(summaryRow, 1).Value = "Total Egresos:";
                    worksheet.Cell(summaryRow, 2).Value = litTotalEgresos.Text;
                    worksheet.Cell(summaryRow, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(summaryRow, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    summaryRow++;

                    worksheet.Cell(summaryRow, 1).Value = "Balance:";
                    // Necesitamos eliminar las etiquetas HTML del balance
                    string balanceText = litBalance.Text;
                    if (balanceText.Contains("<span"))
                    {
                        // Extraer el texto entre >texto</span>
                        int startIndex = balanceText.IndexOf('>') + 1;
                        int endIndex = balanceText.IndexOf("</span>");
                        if (startIndex > 0 && endIndex > startIndex)
                        {
                            balanceText = balanceText.Substring(startIndex, endIndex - startIndex);
                        }
                    }
                    worksheet.Cell(summaryRow, 2).Value = balanceText;
                    worksheet.Cell(summaryRow, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(summaryRow, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    summaryRow++;

                    worksheet.Cell(summaryRow, 1).Value = litIndicadorAdicionalTitulo.Text + ":";
                    worksheet.Cell(summaryRow, 2).Value = litIndicadorAdicional.Text;
                    worksheet.Cell(summaryRow, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Cell(summaryRow, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    // Ajustar ancho de columnas
                    worksheet.Columns().AdjustToContents();

                    // Determinar el tipo de reporte para el nombre del archivo
                    string prefijo = "Reporte_";

                    // Seleccionar el prefijo según el tipo de reporte
                    switch (tipoReporte)
                    {
                        case "conductor":
                            prefijo += "Viajes_Conductor_";
                            break;
                        case "vehiculo":
                            prefijo += "Viajes_Vehiculo_";
                            break;
                        case "pedido":
                            if (!string.IsNullOrEmpty(numeroPedido))
                                prefijo += "Pedido_" + numeroPedido + "_";
                            else
                                prefijo += "Pedidos_";
                            break;
                        case "financiero":
                            prefijo += "Financiero_";
                            break;
                        case "combustible":
                            prefijo += "Combustible_";
                            break;
                        case "producto":
                            prefijo += "Producto_";
                            break;
                        case "personalizado":
                            prefijo += "Personalizado_";
                            break;
                        default:
                            prefijo += "General_";
                            break;
                    }

                    // Enviar al navegador
                    string fileName = prefijo + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
                    Response.Clear();
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    Response.AddHeader("content-disposition", "attachment;filename=" + fileName);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        workbook.SaveAs(ms);
                        ms.Position = 0;
                        Response.BinaryWrite(ms.ToArray());
                        Response.End();
                    }
                }

                // Restaurar la paginación original
                gvReporte.AllowPaging = paginacionOriginal;
                gvReporte.AllowSorting = ordenamientoOriginal;

                if (paginacionOriginal)
                {
                    gvReporte.PageIndex = paginaOriginal;
                    GenerarReporte(); // Volver a cargar el reporte con la paginación original
                }
            }
            catch (ThreadAbortException)
            {
                // Esperado con Response.End()
            }
            catch (Exception ex)
            {
                // Registrar error y mostrar error amigable
                System.Diagnostics.Debug.WriteLine("Error al exportar a Excel: " + ex.Message);

                ScriptManager.RegisterStartupScript(this, GetType(), "errorExport",
                    "alert('Ocurrió un error al exportar a Excel. Por favor, intente nuevamente.\\n" +
                    "Si el problema persiste, contacte al administrador del sistema.\\n\\n" +
                    "Detalle: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }
        protected void btnExportarPDF_Click(object sender, EventArgs e)
        {
            Response.Write("<script>alert('Función de exportación a PDF en desarrollo.');</script>");
        }

        protected void btnExportarDetalleExcel_Click(object sender, EventArgs e)
        {
            Response.Write("<script>alert('Función de exportación de detalle a Excel en desarrollo.');</script>");
        }

        #endregion

        #region Generación de Reportes

        protected void btnGenerarReporte_Click(object sender, EventArgs e)
        {
            try
            {
                // Asegurarnos que el panel de resultados esté visible
                pnlResultados.Visible = true;

                // Generar el reporte - esto llenará el GridView y los indicadores
                GenerarReporte();

                // Actualizar el título del reporte en la modal
                litTituloResultados.Text = litTituloReporte.Text + " - " + ddlTipoReporteDetalle.SelectedItem.Text;


                // Establecer el texto del contador de registros USANDO _totalRegistros
                if (_totalRegistros > 0)
                {
                    lblTotalRegistros.Text = "Total registros: " + _totalRegistros;
                }
                else
                {
                    lblTotalRegistros.Text = "No se encontraron registros";
                }

                // Establecer el indicador adicional según el tipo de reporte
                string tipoReporte = ObtenerTipoReporteSeleccionado();
                if (tipoReporte == "combustible")
                {
                    litIndicadorAdicionalTitulo.Text = "Total Combustible";
                    litIndicadorAdicional.Text = FormatDecimal(CalcularTotalCombustible()) + " Gal.";
                }
                else if (tipoReporte == "producto")
                {
                    litIndicadorAdicionalTitulo.Text = "Total Producto";
                    litIndicadorAdicional.Text = FormatDecimal(CalcularTotalProducto()) + " Kg.";
                }
                else
                {
                    litIndicadorAdicionalTitulo.Text = "Total Viajes";
                    // Usar _totalRegistros en lugar de gvReporte.Rows.Count
                    litIndicadorAdicional.Text = _totalRegistros.ToString();
                }

                // Actualizar el UpdatePanel para que refleje los cambios
                upResultados.Update();

                // Aplicar estilos correctos con jQuery
                string fixStylesScript = @"
    $(document).ready(function() {
        // Fix para encabezados de tabla transparentes
        $('.table thead th').css({
            'background-color': '#0275d8',
            'color': 'white',
            'opacity': '1'
        });
        
        // Fix para tarjetas de indicadores
        $('.card.shadow-sm, .card-body, .card-title, .card-body p, .card-body h4').css('opacity', '1');
        
        // Fix para gradientes
        $('.bg-gradient-primary').css({
            'background': 'linear-gradient(to right, #0062cc, #0275d8)',
            'opacity': '1'
        });
        $('.bg-gradient-danger').css({
            'background': 'linear-gradient(to right, #c82333, #dc3545)',
            'opacity': '1'
        });
        $('.bg-gradient-success').css({
            'background': 'linear-gradient(to right, #218838, #28a745)',
            'opacity': '1'
        });
        $('.bg-gradient-info').css({
            'background': 'linear-gradient(to right, #138496, #17a2b8)',
            'opacity': '1'
        });
    });";

                // Registrar script para aplicar estilos
                ScriptManager.RegisterStartupScript(this, GetType(), "FixStyles",
                    fixStylesScript, true);

                // Mostrar la modal después de un breve retraso para que los estilos se apliquen
                ScriptManager.RegisterStartupScript(this, GetType(), "MostrarModal",
                    "setTimeout(function() { $('#modalResultados').modal('show'); }, 100);", true);
            }
            catch (Exception ex)
            {
                // En caso de error, mostrar mensaje al usuario
                ScriptManager.RegisterStartupScript(this, GetType(), "errorModal",
                    "alert('Error al generar el reporte: " + System.Web.HttpUtility.JavaScriptStringEncode(ex.Message) + "');", true);
            }
        }

        // Método auxiliar para formatear valores decimales
        private string FormatDecimal(decimal value)
        {
            return value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
        }

        // Métodos de cálculo para los indicadores (implementa estas funciones según tu lógica de negocio)
        private decimal CalcularTotalIngresos()
        {
            // Implementa tu lógica para calcular ingresos aquí
            // Por ahora retornamos un valor de ejemplo
            return 12500.00m;
        }

        private decimal CalcularTotalEgresos()
        {
            // Implementa tu lógica para calcular egresos aquí
            // Por ahora retornamos un valor de ejemplo
            return 5250.00m;
        }

        private decimal CalcularTotalCombustible()
        {
            // Implementa tu lógica para calcular combustible aquí
            // Por ahora retornamos un valor de ejemplo
            return 320.00m;
        }

        private decimal CalcularTotalProducto()
        {
            // Implementa tu lógica para calcular producto aquí
            // Por ahora retornamos un valor de ejemplo
            return 15000.00m;
        }








        // Función para renderizar el Panel en string
        private string RenderizarPanel(System.Web.UI.Control control)
        {
            // DESACTIVA VALIDACIÓN DE EVENTOS
            this.EnableEventValidation = false;

            using (System.IO.StringWriter stringWriter = new System.IO.StringWriter())
            {
                using (HtmlTextWriter htmlWriter = new HtmlTextWriter(stringWriter))
                {
                    control.RenderControl(htmlWriter);
                    return stringWriter.ToString();
                }
            }
        }


        public override void VerifyRenderingInServerForm(System.Web.UI.Control control)
        {
            // Evitar error de GridView fuera de <form runat="server">
        }


        // Variable a nivel de clase para guardar el recuento total
        private int _totalRegistros = 0;
        private void GenerarReporte()
        {
            string tipoReporte = ObtenerTipoReporteSeleccionado();
            string tipoReporteDetalle = ddlTipoReporteDetalle.SelectedValue;

            if (tipoReporte == "conductor")
            {
                // Reportes por conductor
                if (tipoReporteDetalle == "viajes_conductor")
                {
                    GenerarReporteViajesConductor();
                }
                else if (tipoReporteDetalle == "productos_conductor")
                {
                    GenerarReporteProductosConductor();
                }
                else if (tipoReporteDetalle == "financiero_conductor")
                {
                    // Implementar en el futuro
                    GenerarReporteFinancieroConductor();
                }
                else if (tipoReporteDetalle == "combustible_conductor")
                {
                    // Implementar en el futuro
                    GenerarReporteCombustibleConductor();
                }
                else
                {
                    GenerarReporteDePrueba();
                }
            }
            else if (tipoReporte == "pedido")
            {
                // Reportes por pedido
                if (tipoReporteDetalle == "vehiculos_pedido")
                {
                    GenerarReporteVehiculosAsignados();
                }
                else if (tipoReporteDetalle == "conductores_pedido")
                {
                    GenerarReporteConductoresAsignados();
                }
                else if (tipoReporteDetalle == "financiero_pedido")
                {
                    GenerarReporteBalanceFinanciero();
                }
                else
                {
                    // Para los demás tipos de reporte de pedido, usar el reporte general de pedido
                    GenerarReportePedido();
                }
            }
            else if (tipoReporte == "financiero")
            {
                // Reportes financieros
                if (tipoReporteDetalle == "balance_financiero" || tipoReporteDetalle == "balance_general")
                {
                    // Usamos el nuevo método para Reportes Financieros - Balance General
                    GenerarReporteFinanciero_BalanceGeneral();
                }
                else if (tipoReporteDetalle == "ingresos_detalle")
                {
                    // Nueva implementación para ingresos detallados en sección financiera
                    GenerarReporteFinanciero_IngresosDetallados();
                }
                else if (tipoReporteDetalle == "egresos_detalle")
                {
                    // Nueva implementación para egresos detallados en sección financiera
                    GenerarReporteFinanciero_EgresosDetallados();
                }
                else if (tipoReporteDetalle == "comparativo_financiero")
                {
                    // Nueva implementación para análisis comparativo en sección financiera
                    GenerarReporteFinanciero_Comparativo();
                }
                else
                {
                    // Por defecto, usamos balance general
                    GenerarReporteFinanciero_BalanceGeneral();
                }
            }
            else if (tipoReporte == "vehiculo")
            {
                // Reportes por vehículo
                if (tipoReporteDetalle == "viajes_vehiculo")
                {
                    GenerarReporteViajesVehiculo();
                }
                else if (tipoReporteDetalle == "combustible_vehiculo")
                {
                    // Implementar en el futuro
                    GenerarReporteConsumoCombustibleVehiculo();
                }
                else if (tipoReporteDetalle == "rendimiento_vehiculo")
                {
                    // Nueva implementación para rendimiento por ruta
                    GenerarReporteRendimientoPorRuta();
                }
                else if (tipoReporteDetalle == "mantenimiento_vehiculo")
                {
                    // Nueva implementación para gastos de mantenimiento
                    GenerarReporteMantenimientoVehiculo();
                }
                else
                {
                    // Por defecto
                    GenerarReporteDePrueba();
                }
            }
            else if (tipoReporte == "combustible")
            {
                // Reportes de combustible
                if (tipoReporteDetalle == "consumo_combustible")
                {
                    // Implementar específico para consumo
                    GenerarReporteConsumoGeneralCombustible();
                }
                else if (tipoReporteDetalle == "rendimiento_combustible")
                {
                    // Implementar específico para rendimiento por vehículo
                    GenerarReporteRendimientoPorVehiculo();
                }
                else if (tipoReporteDetalle == "ruta_combustible")
                {
                    // Implementar específico para ruta
                    GenerarReporteRendimientoPorRutaCombustible();
                }
                else if (tipoReporteDetalle == "sobrante_combustible")
                {
                    // Implementar específico para sobrantes
                    GenerarReporteDePrueba();
                }
                else
                {
                    GenerarReporteDePrueba();
                }
            }
            else if (tipoReporte == "producto")
            {
                // Reportes por producto
                if (tipoReporteDetalle == "ranking_producto")
                {
                    // Implementar específico para ranking
                    GenerarReporteProductosMasTransportados();
                }
                else if (tipoReporteDetalle == "cliente_producto")
                {
                    // Implementar específico para cliente
                    GenerarReporteProductosPorCliente();
                }
                else if (tipoReporteDetalle == "destino_producto")
                {
                    // Implementar específico para destino
                    GenerarReporteProductosPorDestino();
                }
                else
                {
                    GenerarReporteDePrueba();
                }
            }
            else if (tipoReporte == "personalizado")
            {
                // Reportes personalizados - basado en las selecciones del usuario
                if (tipoReporteDetalle == "dinamico_personalizado")
                {
                    // Implementar generador de reportes dinámico
                    GenerarReporteDePrueba();
                }
                else
                {
                    GenerarReporteDePrueba();
                }
            }
            else
            {
                // Reporte por defecto
                GenerarReporteDePrueba();
            }

            if (gvReporte.DataSource != null)
            {
                if (gvReporte.DataSource is DataTable)
                {
                    _totalRegistros = ((DataTable)gvReporte.DataSource).Rows.Count;
                }
                else if (gvReporte.DataSource is DataView)
                {
                    _totalRegistros = ((DataView)gvReporte.DataSource).Count;
                }
                else if (gvReporte.DataSource is ICollection)
                {
                    _totalRegistros = ((ICollection)gvReporte.DataSource).Count;
                }
            }
            pnlResultados.Visible = true;
        }



        // Implementamos el método para generar reportes de pedido
        private void GenerarReportePedido()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string numeroPedido = txtNumeroPedido.Text.Trim();
            string idCliente = ddlClientePedido.SelectedValue;
            string numeroFactura = txtNumeroFactura.Text.Trim();
            string valorMinimo = txtValorMinimo.Text.Trim();
            string valorMaximo = txtValorMaximo.Text.Trim();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReportePedido", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(numeroPedido))
                        cmd.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                    else
                        cmd.Parameters.AddWithValue("@numeroPedido", DBNull.Value);

                    if (!string.IsNullOrEmpty(idCliente))
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);
                    else
                        cmd.Parameters.AddWithValue("@idCliente", DBNull.Value);

                    if (!string.IsNullOrEmpty(numeroFactura))
                        cmd.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                    else
                        cmd.Parameters.AddWithValue("@numeroFactura", DBNull.Value);

                    if (!string.IsNullOrEmpty(valorMinimo))
                        cmd.Parameters.AddWithValue("@valorMinimo", decimal.Parse(valorMinimo));
                    else
                        cmd.Parameters.AddWithValue("@valorMinimo", DBNull.Value);

                    if (!string.IsNullOrEmpty(valorMaximo))
                        cmd.Parameters.AddWithValue("@valorMaximo", decimal.Parse(valorMaximo));
                    else
                        cmd.Parameters.AddWithValue("@valorMaximo", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de pedidos
                    DataTable dtPedidos = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewPedido(dtPedidos);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        decimal totalIngresos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalIngresos"]);
                        decimal totalEgresos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalEgresos"]);
                        int totalHorasViaje = Convert.ToInt32(dtIndicadores.Rows[0]["TotalHorasViaje"]);
                        int totalPedidos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalPedidos"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"S/ {totalIngresos:N2}";
                        litTotalEgresos.Text = $"S/ {totalEgresos:N2}";
                        litBalance.Text = $"S/ {(totalIngresos - totalEgresos):N2}";
                        litIndicadorAdicionalTitulo.Text = "Total Horas de Viaje";
                        litIndicadorAdicional.Text = $"{totalHorasViaje} hrs";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "S/ 0.00";
                        litTotalEgresos.Text = "S/ 0.00";
                        litBalance.Text = "S/ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Total Horas de Viaje";
                        litIndicadorAdicional.Text = "0 hrs";
                    }

                    // Actualizar la interfaz
                    string titulo = !string.IsNullOrEmpty(numeroPedido)
                        ? $"Reporte de Pedido: {numeroPedido}"
                        : "Reporte de Pedidos";

                    litTituloResultados.Text = titulo;
                    lblTotalRegistros.Text = $"Total registros: {dtPedidos.Rows.Count}";
                    gvReporte.DataSource = dtPedidos;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores (similar al código original)
                DataTable dt = new DataTable();
                dt.Columns.Add("idOrdenViaje", typeof(int));
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("NumeroPedido", typeof(string));
                dt.Columns.Add("numeroCPIC", typeof(string));
                dt.Columns.Add("NombreConductor", typeof(string));
                dt.Columns.Add("placaTracto", typeof(string));
                dt.Columns.Add("placaCarreta", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("Producto", typeof(string));
                dt.Columns.Add("fechaSalida", typeof(DateTime));
                dt.Columns.Add("horaSalida", typeof(TimeSpan));
                dt.Columns.Add("fechaLlegada", typeof(DateTime));
                dt.Columns.Add("horaLlegada", typeof(TimeSpan));
                dt.Columns.Add("HorasViaje", typeof(int));
                dt.Columns.Add("PlantaDescarga", typeof(string));

                ConfigurarGridViewPedido(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "S/ 0.00";
                litTotalEgresos.Text = "S/ 0.00";
                litBalance.Text = "S/ 0.00";
                litIndicadorAdicionalTitulo.Text = "Total Horas de Viaje";
                litIndicadorAdicional.Text = "0 hrs";

                string titulo = !string.IsNullOrEmpty(numeroPedido)
                    ? $"Reporte de Pedido: {numeroPedido}"
                    : "Reporte de Pedidos";

                litTituloResultados.Text = titulo;
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de pedido: " + ex.Message);
            }
        }

        private void ConfigurarGridViewPedido(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "NumeroPedido", HeaderText = "Número de Pedido", SortExpression = "NumeroPedido" });
            gvReporte.Columns.Add(new BoundField { DataField = "NombreConductor", HeaderText = "Conductor", SortExpression = "NombreConductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaTracto", HeaderText = "Tracto", SortExpression = "placaTracto" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaCarreta", HeaderText = "Carreta", SortExpression = "placaCarreta" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "Producto", HeaderText = "Producto", SortExpression = "Producto" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaSalida", HeaderText = "Fecha Salida", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "horaSalida", HeaderText = "Hora Salida", SortExpression = "horaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaLlegada", HeaderText = "Fecha Llegada", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "horaLlegada", HeaderText = "Hora Llegada", SortExpression = "horaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "HorasViaje", HeaderText = "Horas Viaje", SortExpression = "HorasViaje", Visible = false });
            gvReporte.Columns.Add(new BoundField { DataField = "PlantaDescarga", HeaderText = "Planta Descarga", SortExpression = "PlantaDescarga" });
        }


        //reporte vehivulkos asignados por pedido

        private void GenerarReporteVehiculosAsignados()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string numeroPedido = txtNumeroPedido.Text.Trim();
            string idCliente = ddlClientePedido.SelectedValue;
            string placaVehiculo = txtPlacaVehiculo.Text.Trim();
            string marcaVehiculo = ddlMarcaVehiculo.SelectedValue;
            string modeloVehiculo = ddlModeloVehiculo.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteVehiculosAsignados", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(numeroPedido))
                        cmd.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                    else
                        cmd.Parameters.AddWithValue("@numeroPedido", DBNull.Value);

                    if (!string.IsNullOrEmpty(idCliente))
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);
                    else
                        cmd.Parameters.AddWithValue("@idCliente", DBNull.Value);

                    if (!string.IsNullOrEmpty(placaVehiculo))
                        cmd.Parameters.AddWithValue("@placaVehiculo", placaVehiculo);
                    else
                        cmd.Parameters.AddWithValue("@placaVehiculo", DBNull.Value);

                    if (!string.IsNullOrEmpty(marcaVehiculo))
                        cmd.Parameters.AddWithValue("@marcaVehiculo", marcaVehiculo);
                    else
                        cmd.Parameters.AddWithValue("@marcaVehiculo", DBNull.Value);

                    if (!string.IsNullOrEmpty(modeloVehiculo))
                        cmd.Parameters.AddWithValue("@modeloVehiculo", modeloVehiculo);
                    else
                        cmd.Parameters.AddWithValue("@modeloVehiculo", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos principales
                    DataTable dtVehiculos = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewVehiculosAsignados(dtVehiculos);

                    // Como no tenemos el código de CalcularIndicadoresVehiculosAsignados,
                    // vamos a manejar los indicadores directamente desde el resultado del SP
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalTractos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalTractos"]);
                        int totalCarretas = Convert.ToInt32(dtIndicadores.Rows[0]["TotalCarretas"]);
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        decimal promedioHoras = Convert.ToDecimal(dtIndicadores.Rows[0]["PromedioHorasViaje"]);
                        int maximoHoras = dtIndicadores.Rows[0]["MaximoHorasViaje"] == DBNull.Value ? 0 : Convert.ToInt32(dtIndicadores.Rows[0]["MaximoHorasViaje"]);

                        // Actualizar los indicadores en la interfaz
                        // Nota: Ajustar estos según la estructura real de los indicadores en su interfaz
                        litTotalIngresos.Text = $"{totalTractos} tractos";
                        litTotalEgresos.Text = $"{totalCarretas} carretas";
                        litBalance.Text = $"{totalViajes} viajes";
                        litIndicadorAdicionalTitulo.Text = "Tiempo Promedio";
                        litIndicadorAdicional.Text = $"{promedioHoras:N1} horas";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 tractos";
                        litTotalEgresos.Text = "0 carretas";
                        litBalance.Text = "0 viajes";
                        litIndicadorAdicionalTitulo.Text = "Tiempo Promedio";
                        litIndicadorAdicional.Text = "0 horas";
                    }

                    // Actualizar la interfaz
                    string titulo = !string.IsNullOrEmpty(numeroPedido)
                        ? $"Vehículos Asignados - Pedido: {numeroPedido}"
                        : "Vehículos Asignados por Período";

                    litTituloResultados.Text = titulo;
                    lblTotalRegistros.Text = $"Total registros: {dtVehiculos.Rows.Count}";
                    gvReporte.DataSource = dtVehiculos;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("idOrdenViaje", typeof(int));
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("NumeroPedido", typeof(string));
                dt.Columns.Add("numeroCPIC", typeof(string));
                dt.Columns.Add("placaTracto", typeof(string));
                dt.Columns.Add("MarcaTracto", typeof(string));
                dt.Columns.Add("ModeloTracto", typeof(string));
                dt.Columns.Add("placaCarreta", typeof(string));
                dt.Columns.Add("MarcaCarreta", typeof(string));
                dt.Columns.Add("ModeloCarreta", typeof(string));
                dt.Columns.Add("Conductor", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("fechaSalida", typeof(DateTime));
                dt.Columns.Add("fechaLlegada", typeof(DateTime));
                dt.Columns.Add("HorasViaje", typeof(int));
                dt.Columns.Add("PlantaDescarga", typeof(string));

                ConfigurarGridViewVehiculosAsignados(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 tractos";
                litTotalEgresos.Text = "0 carretas";
                litBalance.Text = "0 viajes";
                litIndicadorAdicionalTitulo.Text = "Tiempo Promedio";
                litIndicadorAdicional.Text = "0 horas";

                string titulo = !string.IsNullOrEmpty(numeroPedido)
                    ? $"Vehículos Asignados - Pedido: {numeroPedido}"
                    : "Vehículos Asignados por Período";

                litTituloResultados.Text = titulo;
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de vehículos asignados: " + ex.Message);
            }
        }

        private void ConfigurarGridViewVehiculosAsignados(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "NumeroPedido", HeaderText = "Número de Pedido", SortExpression = "NumeroPedido" });
            gvReporte.Columns.Add(new BoundField { DataField = "numeroCPIC", HeaderText = "CPIC", SortExpression = "numeroCPIC" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaTracto", HeaderText = "Placa Tracto", SortExpression = "placaTracto" });
            gvReporte.Columns.Add(new BoundField { DataField = "MarcaTracto", HeaderText = "Marca", SortExpression = "MarcaTracto" });
            gvReporte.Columns.Add(new BoundField { DataField = "ModeloTracto", HeaderText = "Modelo", SortExpression = "ModeloTracto" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaCarreta", HeaderText = "Placa Carreta", SortExpression = "placaCarreta" });
            gvReporte.Columns.Add(new BoundField { DataField = "Conductor", HeaderText = "Conductor", SortExpression = "Conductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaSalida", HeaderText = "Fecha Salida", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaLlegada", HeaderText = "Fecha Llegada", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "HorasViaje", HeaderText = "Horas Viaje", SortExpression = "HorasViaje", Visible = false });
            gvReporte.Columns.Add(new BoundField { DataField = "PlantaDescarga", HeaderText = "Planta Descarga", SortExpression = "PlantaDescarga" });
        }


        //reporte conductores asignados por pedido

        private void GenerarReporteConductoresAsignados()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string numeroPedido = txtNumeroPedido.Text.Trim();
            string idCliente = ddlClientePedido.SelectedValue;
            string nombreConductor = txtNombreConductor.Text.Trim();
            string dniConductor = txtDNIConductor.Text.Trim();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteConductoresAsignados", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(numeroPedido))
                        cmd.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                    else
                        cmd.Parameters.AddWithValue("@numeroPedido", DBNull.Value);

                    if (!string.IsNullOrEmpty(idCliente))
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);
                    else
                        cmd.Parameters.AddWithValue("@idCliente", DBNull.Value);

                    if (!string.IsNullOrEmpty(nombreConductor))
                        cmd.Parameters.AddWithValue("@nombreConductor", nombreConductor);
                    else
                        cmd.Parameters.AddWithValue("@nombreConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(dniConductor))
                        cmd.Parameters.AddWithValue("@dniConductor", dniConductor);
                    else
                        cmd.Parameters.AddWithValue("@dniConductor", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de conductores
                    DataTable dtConductores = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewConductoresAsignados(dtConductores);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalConductores = Convert.ToInt32(dtIndicadores.Rows[0]["TotalConductores"]);
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        double promedioHorasPorViaje = Convert.ToDouble(dtIndicadores.Rows[0]["PromedioHorasPorViaje"]);
                        string conductorMasViajes = dtIndicadores.Rows[0]["ConductorMasViajes"].ToString();
                        int numViajesConductorMasViajes = Convert.ToInt32(dtIndicadores.Rows[0]["NumViajesConductorMasViajes"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalConductores} conductores";
                        litTotalEgresos.Text = $"{totalViajes} viajes";
                        litBalance.Text = $"{promedioHorasPorViaje:F1} hrs/viaje";
                        litIndicadorAdicionalTitulo.Text = "Conductor con más viajes";
                        litIndicadorAdicional.Text = $"{conductorMasViajes} ({numViajesConductorMasViajes} viajes)";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 conductores";
                        litTotalEgresos.Text = "0 viajes";
                        litBalance.Text = "0.0 hrs/viaje";
                        litIndicadorAdicionalTitulo.Text = "Conductor con más viajes";
                        litIndicadorAdicional.Text = "Ninguno";
                    }

                    // Actualizar la interfaz
                    string titulo = !string.IsNullOrEmpty(numeroPedido)
                        ? $"Conductores Asignados - Pedido: {numeroPedido}"
                        : "Conductores Asignados por Período";

                    litTituloResultados.Text = titulo;
                    lblTotalRegistros.Text = $"Total registros: {dtConductores.Rows.Count}";
                    gvReporte.DataSource = dtConductores;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores (similar al código original)
                DataTable dt = new DataTable();
                dt.Columns.Add("idOrdenViaje", typeof(int));
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("NumeroPedido", typeof(string));
                dt.Columns.Add("numeroCPIC", typeof(string));
                dt.Columns.Add("DNI", typeof(string));
                dt.Columns.Add("NombreConductor", typeof(string));
                dt.Columns.Add("telefono", typeof(string));
                dt.Columns.Add("placaTracto", typeof(string));
                dt.Columns.Add("placaCarreta", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("Producto", typeof(string));
                dt.Columns.Add("fechaSalida", typeof(DateTime));
                dt.Columns.Add("fechaLlegada", typeof(DateTime));
                dt.Columns.Add("HorasViaje", typeof(int));
                dt.Columns.Add("Ruta", typeof(string));
                dt.Columns.Add("PlantaDescarga", typeof(string));

                ConfigurarGridViewConductoresAsignados(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 conductores";
                litTotalEgresos.Text = "0 viajes";
                litBalance.Text = "0.0 hrs/viaje";
                litIndicadorAdicionalTitulo.Text = "Conductor con más viajes";
                litIndicadorAdicional.Text = "Ninguno";

                string titulo = !string.IsNullOrEmpty(numeroPedido)
                    ? $"Conductores Asignados - Pedido: {numeroPedido}"
                    : "Conductores Asignados por Período";

                litTituloResultados.Text = titulo;
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de conductores asignados: " + ex.Message);
            }
        }


        private void ConfigurarGridViewConductoresAsignados(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "NumeroPedido", HeaderText = "Número de Pedido", SortExpression = "NumeroPedido" });
            gvReporte.Columns.Add(new BoundField { DataField = "NombreConductor", HeaderText = "Conductor", SortExpression = "NombreConductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "DNI", HeaderText = "DNI", SortExpression = "DNI" });
            gvReporte.Columns.Add(new BoundField { DataField = "telefono", HeaderText = "Teléfono", SortExpression = "telefono" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaTracto", HeaderText = "Placa Tracto", SortExpression = "placaTracto" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaCarreta", HeaderText = "Placa Carreta", SortExpression = "placaCarreta" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "Producto", HeaderText = "Producto", SortExpression = "Producto" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaSalida", HeaderText = "Fecha Salida", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "horaSalida", HeaderText = "Hora Salida", SortExpression = "horaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaLlegada", HeaderText = "Fecha Llegada", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "horaLlegada", HeaderText = "Hora Llegada", SortExpression = "horaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "HorasViaje", HeaderText = "Horas Viaje", SortExpression = "HorasViaje", Visible = false });
            gvReporte.Columns.Add(new BoundField { DataField = "Ruta", HeaderText = "Ruta", SortExpression = "Ruta" });
            gvReporte.Columns.Add(new BoundField { DataField = "PlantaDescarga", HeaderText = "Planta Descarga", SortExpression = "PlantaDescarga" });
        }

        private string ObtenerTipoReporteSeleccionado()
        {
            if (lnkConductor.CssClass.Contains("active")) return "conductor";
            if (lnkVehiculo.CssClass.Contains("active")) return "vehiculo";
            if (lnkPedido.CssClass.Contains("active")) return "pedido";
            if (lnkFinanciero.CssClass.Contains("active")) return "financiero";
            if (lnkCombustible.CssClass.Contains("active")) return "combustible";
            if (lnkProducto.CssClass.Contains("active")) return "producto";
            // if (lnkPersonalizado.CssClass.Contains("active")) return "personalizado";
            return "conductor"; // Por defecto
        }

        //REPORTE BALANCEFINANCIERO - POR PEDIDO

        private void GenerarReporteBalanceFinanciero()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string numeroPedido = txtNumeroPedido.Text.Trim();
            string idCliente = ddlClientePedido.SelectedValue;
            string tipoTransaccion = ddlTipoTransaccion.SelectedValue;
            string montoMinimo = txtMontoMinimo.Text.Trim();
            string montoMaximo = txtMontoMaximo.Text.Trim();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteBalanceFinanciero", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(numeroPedido))
                        cmd.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                    else
                        cmd.Parameters.AddWithValue("@numeroPedido", DBNull.Value);

                    if (!string.IsNullOrEmpty(idCliente))
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);
                    else
                        cmd.Parameters.AddWithValue("@idCliente", DBNull.Value);

                    if (!string.IsNullOrEmpty(tipoTransaccion))
                        cmd.Parameters.AddWithValue("@tipoTransaccion", tipoTransaccion);
                    else
                        cmd.Parameters.AddWithValue("@tipoTransaccion", DBNull.Value);

                    if (!string.IsNullOrEmpty(montoMinimo))
                        cmd.Parameters.AddWithValue("@montoMinimo", decimal.Parse(montoMinimo));
                    else
                        cmd.Parameters.AddWithValue("@montoMinimo", DBNull.Value);

                    if (!string.IsNullOrEmpty(montoMaximo))
                        cmd.Parameters.AddWithValue("@montoMaximo", decimal.Parse(montoMaximo));
                    else
                        cmd.Parameters.AddWithValue("@montoMaximo", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos financieros
                    DataTable dtFinanzas = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewBalanceFinanciero(dtFinanzas);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        decimal totalIngresosSoles = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalIngresosSoles"]);
                        decimal totalIngresosDolares = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalIngresosDolares"]);
                        decimal totalEgresosSoles = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalEgresosSoles"]);
                        decimal totalEgresosDolares = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalEgresosDolares"]);
                        int totalTransacciones = Convert.ToInt32(dtIndicadores.Rows[0]["TotalTransacciones"]);

                        decimal balanceSoles = totalIngresosSoles - totalEgresosSoles;
                        decimal balanceDolares = totalIngresosDolares - totalEgresosDolares;

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"S/ {totalIngresosSoles:N2} | $ {totalIngresosDolares:N2}";
                        litTotalEgresos.Text = $"S/ {totalEgresosSoles:N2} | $ {totalEgresosDolares:N2}";
                        litBalance.Text = $"S/ {balanceSoles:N2} | $ {balanceDolares:N2}";
                        litIndicadorAdicionalTitulo.Text = "Transacciones";
                        litIndicadorAdicional.Text = $"{totalTransacciones}";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "S/ 0.00 | $ 0.00";
                        litTotalEgresos.Text = "S/ 0.00 | $ 0.00";
                        litBalance.Text = "S/ 0.00 | $ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Transacciones";
                        litIndicadorAdicional.Text = "0";
                    }

                    // Actualizar la interfaz
                    string titulo = !string.IsNullOrEmpty(numeroPedido)
                        ? $"Balance Financiero - Pedido: {numeroPedido}"
                        : "Balance Financiero";

                    litTituloResultados.Text = titulo;
                    lblTotalRegistros.Text = $"Total registros: {dtFinanzas.Rows.Count}";
                    gvReporte.DataSource = dtFinanzas;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("NumeroPedido", typeof(string));
                dt.Columns.Add("FechaTransaccion", typeof(DateTime));
                dt.Columns.Add("TipoTransaccion", typeof(string));
                dt.Columns.Add("Concepto", typeof(string));
                dt.Columns.Add("Conductor", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("IngresoSoles", typeof(decimal));
                dt.Columns.Add("IngresoDolares", typeof(decimal));
                dt.Columns.Add("EgresoSoles", typeof(decimal));
                dt.Columns.Add("EgresoDolares", typeof(decimal));

                ConfigurarGridViewBalanceFinanciero(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "S/ 0.00 | $ 0.00";
                litTotalEgresos.Text = "S/ 0.00 | $ 0.00";
                litBalance.Text = "S/ 0.00 | $ 0.00";
                litIndicadorAdicionalTitulo.Text = "Transacciones";
                litIndicadorAdicional.Text = "0";

                string titulo = !string.IsNullOrEmpty(numeroPedido)
                    ? $"Balance Financiero - Pedido: {numeroPedido}"
                    : "Balance Financiero";

                litTituloResultados.Text = titulo;
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error y mostrar mensaje
                System.Diagnostics.Debug.WriteLine("Error al generar balance financiero: " + ex.Message);
                ScriptManager.RegisterStartupScript(this, GetType(), "errorReporte",
                    "alert('Error al generar el balance financiero: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        private void ConfigurarGridViewBalanceFinanciero(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "NumeroPedido", HeaderText = "Número de Pedido", SortExpression = "NumeroPedido" });
            gvReporte.Columns.Add(new BoundField { DataField = "FechaTransaccion", HeaderText = "Fecha", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "FechaTransaccion" });
            gvReporte.Columns.Add(new BoundField { DataField = "Concepto", HeaderText = "Concepto", SortExpression = "Concepto" });
            gvReporte.Columns.Add(new BoundField { DataField = "TipoTransaccion", HeaderText = "Tipo", SortExpression = "TipoTransaccion" });
            gvReporte.Columns.Add(new BoundField { DataField = "Conductor", HeaderText = "Conductor", SortExpression = "Conductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "IngresoSoles", HeaderText = "Ingreso S/", DataFormatString = "{0:N2}", SortExpression = "IngresoSoles", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "IngresoDolares", HeaderText = "Ingreso $", DataFormatString = "{0:N2}", SortExpression = "IngresoDolares", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "EgresoSoles", HeaderText = "Egreso S/", DataFormatString = "{0:N2}", SortExpression = "EgresoSoles", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "EgresoDolares", HeaderText = "Egreso $", DataFormatString = "{0:N2}", SortExpression = "EgresoDolares", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
        }


        //REPORTE VIAJE POR CONDUCTOR
        private void GenerarReporteViajesConductor()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idConductor = ddlConductor.SelectedValue;
            string dniConductor = txtDNIConductor.Text;
            string nombreConductor = txtNombreConductor.Text;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteViajesConductor", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(idConductor))
                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    else
                        cmd.Parameters.AddWithValue("@idConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(dniConductor))
                        cmd.Parameters.AddWithValue("@dniConductor", dniConductor);
                    else
                        cmd.Parameters.AddWithValue("@dniConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(nombreConductor))
                        cmd.Parameters.AddWithValue("@nombreConductor", nombreConductor);
                    else
                        cmd.Parameters.AddWithValue("@nombreConductor", DBNull.Value);

                    // Obtener datos principales para el GridView
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos principales
                    DataTable dtViajes = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores financieros
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewViajesConductor(dtViajes);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        decimal totalIngresos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalIngresos"]);
                        decimal totalEgresos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalEgresos"]);
                        decimal totalGalones = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalGalones"]);

                        litTotalIngresos.Text = $"S/ {totalIngresos:N2}";
                        litTotalEgresos.Text = $"S/ {totalEgresos:N2}";
                        litBalance.Text = $"S/ {(totalIngresos - totalEgresos):N2}";
                        litIndicadorAdicionalTitulo.Text = "Total Combustible";
                        litIndicadorAdicional.Text = $"{totalGalones:N2} gal";
                    }
                    else
                    {
                        // Si no hay datos, inicializar como cero
                        litTotalIngresos.Text = "S/ 0.00";
                        litTotalEgresos.Text = "S/ 0.00";
                        litBalance.Text = "S/ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Total Combustible";
                        litIndicadorAdicional.Text = "0.00 gal";
                    }

                    // Mostrar resultados
                    litTituloResultados.Text = "Reporte de Viajes por Conductor";
                    lblTotalRegistros.Text = $"Total registros: {dtViajes.Rows.Count}";
                    gvReporte.DataSource = dtViajes;
                    gvReporte.DataBind();
                }
            }
            catch (Exception )
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("NombreConductor", typeof(string));
                dt.Columns.Add("placaTracto", typeof(string));
                dt.Columns.Add("placaCarreta", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("Producto", typeof(string));
                dt.Columns.Add("fechaSalida", typeof(DateTime));
                dt.Columns.Add("horaSalida", typeof(TimeSpan));
                dt.Columns.Add("fechaLlegada", typeof(DateTime));
                dt.Columns.Add("horaLlegada", typeof(TimeSpan));
                dt.Columns.Add("PlantaDescarga", typeof(string));

                ConfigurarGridViewViajesConductor(dt);
                litTituloResultados.Text = "Reporte de Viajes por Conductor";
                lblTotalRegistros.Text = "Total registros: 0";

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "S/ 0.00";
                litTotalEgresos.Text = "S/ 0.00";
                litBalance.Text = "S/ 0.00";
                litIndicadorAdicionalTitulo.Text = "Total Combustible";
                litIndicadorAdicional.Text = "0.00 gal";

                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Considere registrar el error
                // Log.Error($"Error en GenerarReporteViajesConductor: {ex.Message}");
            }
        }

        private void ConfigurarGridViewViajesConductor(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "NombreConductor", HeaderText = "Conductor", SortExpression = "NombreConductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaTracto", HeaderText = "Tracto", SortExpression = "placaTracto" });
            gvReporte.Columns.Add(new BoundField { DataField = "placaCarreta", HeaderText = "Carreta", SortExpression = "placaCarreta" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "Producto", HeaderText = "Producto", SortExpression = "Producto" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaSalida", HeaderText = "Fecha Salida", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "horaSalida", HeaderText = "Hora Salida", SortExpression = "horaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "fechaLlegada", HeaderText = "Fecha Llegada", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "fechaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "horaLlegada", HeaderText = "Hora Llegada", SortExpression = "horaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "PlantaDescarga", HeaderText = "Planta Descarga", SortExpression = "PlantaDescarga" });
        }

        //PRODUCTOR POR CONDUCTOR - REPORTE POR CONDUCTOR

        private void GenerarReporteProductosConductor()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idConductor = ddlConductor.SelectedValue;
            string dniConductor = txtDNIConductor.Text;
            string nombreConductor = txtNombreConductor.Text;
            string idProducto = ddlProducto.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteProductosConductor", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(idConductor))
                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    else
                        cmd.Parameters.AddWithValue("@idConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(dniConductor))
                        cmd.Parameters.AddWithValue("@dniConductor", dniConductor);
                    else
                        cmd.Parameters.AddWithValue("@dniConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(nombreConductor))
                        cmd.Parameters.AddWithValue("@nombreConductor", nombreConductor);
                    else
                        cmd.Parameters.AddWithValue("@nombreConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(idProducto))
                        cmd.Parameters.AddWithValue("@idProducto", idProducto);
                    else
                        cmd.Parameters.AddWithValue("@idProducto", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos principales
                    DataTable dtProductos = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewProductosConductor(dtProductos);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalProductos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalProductos"]);
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        int totalBolsas = Convert.ToInt32(dtIndicadores.Rows[0]["TotalBolsas"]);
                        decimal totalPesoKg = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalPesoKg"]);
                        string productoMasTransportado = dtIndicadores.Rows[0]["ProductoMasTransportado"].ToString();

                        // Actualizar la interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalProductos} productos";
                        litTotalEgresos.Text = $"{totalViajes} viajes";
                        litBalance.Text = $"{totalBolsas:N0} bolsas";
                        litIndicadorAdicionalTitulo.Text = "Peso Total";
                        litIndicadorAdicional.Text = $"{totalPesoKg:N2} Kg";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 productos";
                        litTotalEgresos.Text = "0 viajes";
                        litBalance.Text = "0 bolsas";
                        litIndicadorAdicionalTitulo.Text = "Peso Total";
                        litIndicadorAdicional.Text = "0.00 Kg";
                    }

                    // Mostrar resultados
                    litTituloResultados.Text = "Reporte de Productos Transportados por Conductor";
                    lblTotalRegistros.Text = $"Total registros: {dtProductos.Rows.Count}";
                    gvReporte.DataSource = dtProductos;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("NombreConductor", typeof(string));
                dt.Columns.Add("Producto", typeof(string));
                dt.Columns.Add("CantidadBolsas", typeof(int));
                dt.Columns.Add("PesoKg", typeof(decimal));
                dt.Columns.Add("GuiaTransportista", typeof(string));
                dt.Columns.Add("GuiaCliente", typeof(string));
                dt.Columns.Add("FechaSalida", typeof(DateTime));
                dt.Columns.Add("FechaLlegada", typeof(DateTime));
                dt.Columns.Add("PlantaDescarga", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));

                ConfigurarGridViewProductosConductor(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 productos";
                litTotalEgresos.Text = "0 viajes";
                litBalance.Text = "0 bolsas";
                litIndicadorAdicionalTitulo.Text = "Peso Total";
                litIndicadorAdicional.Text = "0.00 Kg";

                litTituloResultados.Text = "Reporte de Productos Transportados por Conductor";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Considere registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de productos transportados: " + ex.Message);
            }
        }

        private void ConfigurarGridViewProductosConductor(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "NombreConductor", HeaderText = "Conductor", SortExpression = "NombreConductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "Producto", HeaderText = "Producto", SortExpression = "Producto" });
            gvReporte.Columns.Add(new BoundField { DataField = "CantidadBolsas", HeaderText = "Cant. Bolsas", SortExpression = "CantidadBolsas", DataFormatString = "{0:N0}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "PesoKg", HeaderText = "Peso (Kg)", SortExpression = "PesoKg", DataFormatString = "{0:N2}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, Visible = false });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "GuiaTransportista", HeaderText = "Guía Transportista", SortExpression = "GuiaTransportista" });
            gvReporte.Columns.Add(new BoundField { DataField = "GuiaCliente", HeaderText = "Guía Cliente", SortExpression = "GuiaCliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "FechaSalida", HeaderText = "Fecha Salida", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "FechaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "FechaLlegada", HeaderText = "Fecha Llegada", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "FechaLlegada" });
            gvReporte.Columns.Add(new BoundField { DataField = "PlantaDescarga", HeaderText = "Planta Descarga", SortExpression = "PlantaDescarga" });
        }


        //INGRESOS Y GASTOS - REPORTE POR CONDUCTOR

        private void GenerarReporteFinancieroConductor()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idConductor = ddlConductor.SelectedValue;
            string dniConductor = txtDNIConductor.Text;
            string nombreConductor = txtNombreConductor.Text;
            string tipoTransaccion = ddlTipoTransaccion.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteFinancieroConductor", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales de conductor
                    if (!string.IsNullOrEmpty(idConductor))
                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    else
                        cmd.Parameters.AddWithValue("@idConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(dniConductor))
                        cmd.Parameters.AddWithValue("@dniConductor", dniConductor);
                    else
                        cmd.Parameters.AddWithValue("@dniConductor", DBNull.Value);

                    if (!string.IsNullOrEmpty(nombreConductor))
                        cmd.Parameters.AddWithValue("@nombreConductor", nombreConductor);
                    else
                        cmd.Parameters.AddWithValue("@nombreConductor", DBNull.Value);

                    // Parámetros adicionales de filtrado
                    if (!string.IsNullOrEmpty(tipoTransaccion))
                        cmd.Parameters.AddWithValue("@tipoTransaccion", tipoTransaccion);
                    else
                        cmd.Parameters.AddWithValue("@tipoTransaccion", DBNull.Value);

                    if (!string.IsNullOrEmpty(txtMontoMinimo.Text))
                        cmd.Parameters.AddWithValue("@montoMinimo", decimal.Parse(txtMontoMinimo.Text));
                    else
                        cmd.Parameters.AddWithValue("@montoMinimo", DBNull.Value);

                    if (!string.IsNullOrEmpty(txtMontoMaximo.Text))
                        cmd.Parameters.AddWithValue("@montoMaximo", decimal.Parse(txtMontoMaximo.Text));
                    else
                        cmd.Parameters.AddWithValue("@montoMaximo", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de transacciones
                    DataTable dtTransacciones = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewFinancieroConductor(dtTransacciones);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        decimal totalIngresosSoles = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalIngresosSoles"]);
                        decimal totalIngresosDolares = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalIngresosDolares"]);
                        decimal totalEgresosSoles = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalEgresosSoles"]);
                        decimal totalEgresosDolares = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalEgresosDolares"]);
                        int contadorIngresos = Convert.ToInt32(dtIndicadores.Rows[0]["ContadorIngresos"]);
                        int contadorEgresos = Convert.ToInt32(dtIndicadores.Rows[0]["ContadorEgresos"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"S/ {totalIngresosSoles:N2} | $ {totalIngresosDolares:N2}";
                        litTotalEgresos.Text = $"S/ {totalEgresosSoles:N2} | $ {totalEgresosDolares:N2}";

                        decimal balanceSoles = totalIngresosSoles - totalEgresosSoles;
                        decimal balanceDolares = totalIngresosDolares - totalEgresosDolares;
                        litBalance.Text = $"S/ {balanceSoles:N2} | $ {balanceDolares:N2}";

                        litIndicadorAdicionalTitulo.Text = "Transacciones";
                        litIndicadorAdicional.Text = $"{contadorIngresos} ingresos, {contadorEgresos} egresos";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "S/ 0.00 | $ 0.00";
                        litTotalEgresos.Text = "S/ 0.00 | $ 0.00";
                        litBalance.Text = "S/ 0.00 | $ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Transacciones";
                        litIndicadorAdicional.Text = "0 ingresos, 0 egresos";
                    }

                    // Actualizar título según el conductor seleccionado
                    string tituloReporte = "Reporte de Ingresos y Gastos por Conductor";
                    if (!string.IsNullOrEmpty(idConductor))
                    {
                        string nombreConductorCompleto = "";
                        if (dtTransacciones.Rows.Count > 0 && dtTransacciones.Rows[0]["Conductor"] != DBNull.Value)
                        {
                            nombreConductorCompleto = dtTransacciones.Rows[0]["Conductor"].ToString();
                        }
                        else
                        {
                            foreach (ListItem item in ddlConductor.Items)
                            {
                                if (item.Value == idConductor)
                                {
                                    nombreConductorCompleto = item.Text;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(nombreConductorCompleto))
                        {
                            tituloReporte += ": " + nombreConductorCompleto;
                        }
                    }

                    // Mostrar resultados
                    litTituloResultados.Text = tituloReporte;
                    lblTotalRegistros.Text = $"Total registros: {dtTransacciones.Rows.Count}";
                    gvReporte.DataSource = dtTransacciones;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("FechaTransaccion", typeof(DateTime));
                dt.Columns.Add("TipoTransaccion", typeof(string));
                dt.Columns.Add("Concepto", typeof(string));
                dt.Columns.Add("Conductor", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("MontoSoles", typeof(decimal));
                dt.Columns.Add("MontoDolares", typeof(decimal));
                dt.Columns.Add("Observaciones", typeof(string));

                ConfigurarGridViewFinancieroConductor(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "S/ 0.00 | $ 0.00";
                litTotalEgresos.Text = "S/ 0.00 | $ 0.00";
                litBalance.Text = "S/ 0.00 | $ 0.00";
                litIndicadorAdicionalTitulo.Text = "Transacciones";
                litIndicadorAdicional.Text = "0 ingresos, 0 egresos";

                litTituloResultados.Text = "Reporte de Ingresos y Gastos por Conductor";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte financiero por conductor: " + ex.Message);
            }
        }

        private void ConfigurarGridViewFinancieroConductor(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "FechaTransaccion", HeaderText = "Fecha", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "FechaTransaccion" });
            gvReporte.Columns.Add(new BoundField { DataField = "TipoTransaccion", HeaderText = "Tipo", SortExpression = "TipoTransaccion" });
            gvReporte.Columns.Add(new BoundField { DataField = "Concepto", HeaderText = "Concepto", SortExpression = "Concepto" });
            gvReporte.Columns.Add(new BoundField { DataField = "Conductor", HeaderText = "Conductor", SortExpression = "Conductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "MontoSoles", HeaderText = "Monto S/", DataFormatString = "{0:N2}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "MontoSoles" });
            gvReporte.Columns.Add(new BoundField { DataField = "MontoDolares", HeaderText = "Monto $", DataFormatString = "{0:N2}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "MontoDolares" });
            gvReporte.Columns.Add(new BoundField { DataField = "Observaciones", HeaderText = "Observaciones", SortExpression = "Observaciones" });
        }


        //rendimiento de combustible - reporte por conductor
        private void GenerarReporteCombustibleConductor()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idConductor = ddlConductor.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteCombustibleConductor", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetro opcional del conductor
                    if (!string.IsNullOrEmpty(idConductor) && idConductor != "0")
                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    else
                        cmd.Parameters.AddWithValue("@idConductor", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de abastecimiento
                    DataTable dtAbastecimiento = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewCombustibleConductor(dtAbastecimiento);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int contadorVehiculos = Convert.ToInt32(dtIndicadores.Rows[0]["ContadorVehiculos"]);
                        decimal totalKilometros = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalKilometros"]);
                        decimal totalGalones = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalGalones"]);
                        decimal totalDolares = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalDolares"]);
                        decimal rendimientoGeneral = Convert.ToDecimal(dtIndicadores.Rows[0]["RendimientoGeneral"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalKilometros:N0} Km";
                        litTotalEgresos.Text = $"{totalGalones:N2} Gal";
                        litBalance.Text = $"$ {totalDolares:N2}";
                        litIndicadorAdicionalTitulo.Text = "Rendimiento";
                        litIndicadorAdicional.Text = $"{rendimientoGeneral:N2} Km/Gal";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 Km";
                        litTotalEgresos.Text = "0 Gal";
                        litBalance.Text = "$ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Rendimiento";
                        litIndicadorAdicional.Text = "0.00 Km/Gal";
                    }

                    // Actualizar título según el conductor seleccionado
                    string tituloReporte = "Reporte de Rendimiento de Combustible";
                    if (!string.IsNullOrEmpty(idConductor) && idConductor != "0")
                    {
                        string nombreConductor = "";
                        if (dtAbastecimiento.Rows.Count > 0)
                        {
                            nombreConductor = dtAbastecimiento.Rows[0]["Conductor"].ToString();
                        }
                        else
                        {
                            foreach (ListItem item in ddlConductor.Items)
                            {
                                if (item.Value == idConductor)
                                {
                                    nombreConductor = item.Text;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(nombreConductor))
                        {
                            tituloReporte += " - Conductor: " + nombreConductor;
                        }
                    }

                    // Mostrar resultados
                    litTituloResultados.Text = tituloReporte;
                    lblTotalRegistros.Text = $"Total registros: {dtAbastecimiento.Rows.Count}";
                    gvReporte.DataSource = dtAbastecimiento;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("FechaSalida", typeof(DateTime));
                dt.Columns.Add("FechaLlegada", typeof(DateTime));
                dt.Columns.Add("Conductor", typeof(string));
                dt.Columns.Add("Placa", typeof(string));
                dt.Columns.Add("Marca", typeof(string));
                dt.Columns.Add("Modelo", typeof(string));
                dt.Columns.Add("EstacionServicio", typeof(string));
                dt.Columns.Add("FechaAbastecimiento", typeof(DateTime));
                dt.Columns.Add("DistanciaKm", typeof(decimal));
                dt.Columns.Add("Galones", typeof(decimal));
                dt.Columns.Add("PrecioUnitario", typeof(decimal));
                dt.Columns.Add("Total", typeof(decimal));
                dt.Columns.Add("RendimientoKmGalon", typeof(decimal));
                dt.Columns.Add("Observaciones", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));

                ConfigurarGridViewCombustibleConductor(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 Km";
                litTotalEgresos.Text = "0 Gal";
                litBalance.Text = "$ 0.00";
                litIndicadorAdicionalTitulo.Text = "Rendimiento";
                litIndicadorAdicional.Text = "0.00 Km/Gal";

                litTituloResultados.Text = "Reporte de Rendimiento de Combustible";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de combustible: " + ex.Message);
            }
        }

        private void ConfigurarGridViewCombustibleConductor(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NroOrdenViaje", HeaderText = "Nº Orden", SortExpression = "NroOrdenViaje" });
            gvReporte.Columns.Add(new BoundField { DataField = "FechaSalida", HeaderText = "Fecha Salida", DataFormatString = "{0:dd/MM/yyyy}", SortExpression = "FechaSalida" });
            gvReporte.Columns.Add(new BoundField { DataField = "Conductor", HeaderText = "Conductor", SortExpression = "Conductor" });
            gvReporte.Columns.Add(new BoundField { DataField = "Placa", HeaderText = "Placa", SortExpression = "Placa" });
            gvReporte.Columns.Add(new BoundField { DataField = "EstacionServicio", HeaderText = "Est. Servicio", SortExpression = "EstacionServicio" });
            gvReporte.Columns.Add(new BoundField { DataField = "DistanciaKm", HeaderText = "Dist. (Km)", DataFormatString = "{0:N0}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "DistanciaKm" });
            gvReporte.Columns.Add(new BoundField { DataField = "Galones", HeaderText = "Galones", DataFormatString = "{0:N2}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "Galones" });
            gvReporte.Columns.Add(new BoundField { DataField = "PrecioUnitario", HeaderText = "Precio Unit.", DataFormatString = "{0:N3}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "PrecioUnitario" });
            gvReporte.Columns.Add(new BoundField { DataField = "Total", HeaderText = "Total $", DataFormatString = "{0:N2}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "Total" });
            gvReporte.Columns.Add(new BoundField { DataField = "RendimientoKmGalon", HeaderText = "Km/Galón", DataFormatString = "{0:N2}", ItemStyle = { HorizontalAlign = HorizontalAlign.Right }, SortExpression = "RendimientoKmGalon" });
            gvReporte.Columns.Add(new BoundField { DataField = "Cliente", HeaderText = "Cliente", SortExpression = "Cliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "Observaciones", HeaderText = "Observaciones", SortExpression = "Observaciones" });
        }


        // REPORTES POR VEHICULO
        //HISTORIAL DE VIAJES - REPORTE POR VEHICULO

        private void GenerarReporteViajesVehiculo()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idVehiculo = ddlVehiculo.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteViajesVehiculo", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(idVehiculo))
                        cmd.Parameters.AddWithValue("@idTracto", idVehiculo);
                    else
                        cmd.Parameters.AddWithValue("@idTracto", DBNull.Value);

                    if (!string.IsNullOrEmpty(txtPlacaVehiculo.Text))
                        cmd.Parameters.AddWithValue("@placaTracto", txtPlacaVehiculo.Text);
                    else
                        cmd.Parameters.AddWithValue("@placaTracto", DBNull.Value);

                    if (!string.IsNullOrEmpty(ddlMarcaVehiculo.SelectedValue))
                        cmd.Parameters.AddWithValue("@marcaVehiculo", ddlMarcaVehiculo.SelectedValue);
                    else
                        cmd.Parameters.AddWithValue("@marcaVehiculo", DBNull.Value);

                    if (!string.IsNullOrEmpty(ddlModeloVehiculo.SelectedValue))
                        cmd.Parameters.AddWithValue("@modeloVehiculo", ddlModeloVehiculo.SelectedValue);
                    else
                        cmd.Parameters.AddWithValue("@modeloVehiculo", DBNull.Value);

                    // Ejecutar y obtener resultados en un DataSet
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de viajes
                    DataTable dtViajes = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewViajesVehiculo(dtViajes);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        int totalClientesDistintos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalClientesDistintos"]);
                        int totalConductoresDistintos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalConductoresDistintos"]);
                        int totalHoras = Convert.ToInt32(dtIndicadores.Rows[0]["TotalHoras"]);
                        double promedioHoras = Convert.ToDouble(dtIndicadores.Rows[0]["PromedioHoras"]);
                        int viajesCompletados = Convert.ToInt32(dtIndicadores.Rows[0]["ViajesCompletados"]);

                        // Calcular porcentaje de viajes completados
                        double porcentajeCompletados = totalViajes > 0 ? (double)viajesCompletados / totalViajes * 100 : 0;

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalViajes} viajes";
                        litTotalEgresos.Text = $"{totalConductoresDistintos} conductores";
                        litBalance.Text = $"{totalHoras} horas";
                        litIndicadorAdicionalTitulo.Text = "Clientes";
                        litIndicadorAdicional.Text = $"{totalClientesDistintos} clientes";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 viajes";
                        litTotalEgresos.Text = "0 conductores";
                        litBalance.Text = "0 horas";
                        litIndicadorAdicionalTitulo.Text = "Clientes";
                        litIndicadorAdicional.Text = "0 clientes";
                    }

                    // Actualizar la interfaz
                    string tituloReporte = "Historial de Viajes por Vehículo";
                    if (!string.IsNullOrEmpty(idVehiculo))
                    {
                        if (dtViajes.Rows.Count > 0)
                        {
                            tituloReporte += ": " + dtViajes.Rows[0]["placaTracto"].ToString();
                        }
                        else
                        {
                            foreach (ListItem item in ddlVehiculo.Items)
                            {
                                if (item.Value == idVehiculo)
                                {
                                    tituloReporte += ": " + item.Text;
                                    break;
                                }
                            }
                        }
                    }

                    litTituloResultados.Text = tituloReporte;
                    lblTotalRegistros.Text = $"Total registros: {dtViajes.Rows.Count}";
                    gvReporte.DataSource = dtViajes;
                    gvReporte.DataBind();

                    // Mostrar el modal con los resultados
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowResultsModal",
                        "$('#modalResultados').modal('show');", true);
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NroOrdenViaje", typeof(string));
                dt.Columns.Add("placaTracto", typeof(string));
                dt.Columns.Add("placaCarreta", typeof(string));
                dt.Columns.Add("NombreConductor", typeof(string));
                dt.Columns.Add("Cliente", typeof(string));
                dt.Columns.Add("Producto", typeof(string));
                dt.Columns.Add("fechaSalida", typeof(DateTime));
                dt.Columns.Add("horaSalida", typeof(TimeSpan));
                dt.Columns.Add("fechaLlegada", typeof(DateTime));
                dt.Columns.Add("horaLlegada", typeof(TimeSpan));
                dt.Columns.Add("HorasViaje", typeof(int));
                dt.Columns.Add("NombreRuta", typeof(string));
                dt.Columns.Add("PlantaDescarga", typeof(string));

                ConfigurarGridViewViajesVehiculo(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 viajes";
                litTotalEgresos.Text = "0 conductores";
                litBalance.Text = "0 horas";
                litIndicadorAdicionalTitulo.Text = "Clientes";
                litIndicadorAdicional.Text = "0 clientes";

                litTituloResultados.Text = "Historial de Viajes por Vehículo";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                    $"alert('Error al generar reporte: {ex.Message.Replace("'", "\\'")}');", true);

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de viajes por vehículo: " + ex.Message);
            }
        }

        private void ConfigurarGridViewViajesVehiculo(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "NroOrdenViaje",
                HeaderText = "Nº Orden",
                SortExpression = "NroOrdenViaje"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "placaTracto",
                HeaderText = "Placa Tracto",
                SortExpression = "placaTracto"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "placaCarreta",
                HeaderText = "Placa Carreta",
                SortExpression = "placaCarreta"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "NombreConductor",
                HeaderText = "Conductor",
                SortExpression = "NombreConductor"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "Cliente",
                HeaderText = "Cliente",
                SortExpression = "Cliente"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "Producto",
                HeaderText = "Producto",
                SortExpression = "Producto"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "fechaSalida",
                HeaderText = "Fecha Salida",
                DataFormatString = "{0:dd/MM/yyyy}",
                SortExpression = "fechaSalida"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "horaSalida",
                HeaderText = "Hora Salida",
                SortExpression = "horaSalida"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "fechaLlegada",
                HeaderText = "Fecha Llegada",
                DataFormatString = "{0:dd/MM/yyyy}",
                SortExpression = "fechaLlegada"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "horaLlegada",
                HeaderText = "Hora Llegada",
                SortExpression = "horaLlegada"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "HorasViaje",
                HeaderText = "Horas",
                SortExpression = "HorasViaje",
                ItemStyle = { HorizontalAlign = HorizontalAlign.Right }
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "NombreRuta",
                HeaderText = "Ruta",
                SortExpression = "NombreRuta"
            });

            gvReporte.Columns.Add(new BoundField
            {
                DataField = "PlantaDescarga",
                HeaderText = "Planta Descarga",
                SortExpression = "PlantaDescarga"
            });
        }


        //CONSUMO DE COMBUSTIBLE - REPORTE POR VEHICULO

        private void GenerarReporteConsumoCombustibleVehiculo()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idVehiculo = ddlVehiculo.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteConsumoCombustibleVehiculo", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros obligatorios
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales - Corrección de manejo de nulos
                    cmd.Parameters.Add("@idTracto", SqlDbType.VarChar, 10).Value =
                        !string.IsNullOrEmpty(idVehiculo) ? idVehiculo : (object)DBNull.Value;

                    cmd.Parameters.Add("@placaTracto", SqlDbType.VarChar, 10).Value =
                        !string.IsNullOrEmpty(txtPlacaVehiculo.Text) ? txtPlacaVehiculo.Text : (object)DBNull.Value;

                    cmd.Parameters.Add("@numeroAbastecimiento", SqlDbType.VarChar, 10).Value =
                        !string.IsNullOrEmpty(txtNumeroAbastecimiento.Text) ? txtNumeroAbastecimiento.Text : (object)DBNull.Value;

                    cmd.Parameters.Add("@productoCombustible", SqlDbType.VarChar, 100).Value =
                        !string.IsNullOrEmpty(txtProductoCombustible.Text) ? txtProductoCombustible.Text : (object)DBNull.Value;

                    // Para el lugar de abastecimiento
                    if (!string.IsNullOrEmpty(ddlLugarAbastecimiento.SelectedValue))
                    {
                        cmd.Parameters.Add("@idLugarAbastecimiento", SqlDbType.Int).Value =
                            Convert.ToInt32(ddlLugarAbastecimiento.SelectedValue);
                    }
                    else
                    {
                        cmd.Parameters.Add("@idLugarAbastecimiento", SqlDbType.Int).Value = DBNull.Value;
                    }

                    // Para los valores decimales, usamos TryParse para evitar excepciones
                    decimal galonesMinimos;
                    if (!string.IsNullOrEmpty(txtGalonesMinimos.Text) && decimal.TryParse(txtGalonesMinimos.Text, out galonesMinimos))
                    {
                        cmd.Parameters.Add("@galonesMinimos", SqlDbType.Decimal).Value = galonesMinimos;
                    }
                    else
                    {
                        cmd.Parameters.Add("@galonesMinimos", SqlDbType.Decimal).Value = DBNull.Value;
                    }

                    decimal rendimientoMinimo;
                    if (!string.IsNullOrEmpty(txtRendimientoMinimo.Text) && decimal.TryParse(txtRendimientoMinimo.Text, out rendimientoMinimo))
                    {
                        cmd.Parameters.Add("@rendimientoMinimo", SqlDbType.Decimal).Value = rendimientoMinimo;
                    }
                    else
                    {
                        cmd.Parameters.Add("@rendimientoMinimo", SqlDbType.Decimal).Value = DBNull.Value;
                    }

                    cmd.Parameters.Add("@tipoReporte", SqlDbType.VarChar, 20).Value =
                        !string.IsNullOrEmpty(ddlTipoReporteCombustible.SelectedValue) ? ddlTipoReporteCombustible.SelectedValue : (object)DBNull.Value;

                    // Ejecutar y obtener resultados en un DataSet
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de abastecimientos
                    DataTable dtAbastecimientos = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables.Count > 1 ? ds.Tables[1] : null;

                    // Configurar el GridView
                    ConfigurarGridViewConsumoCombustibleVehiculo(dtAbastecimientos);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores != null && dtIndicadores.Rows.Count > 0)
                    {
                        decimal totalGalonesConsumidos = dtIndicadores.Rows[0]["TotalGalonesConsumidos"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalGalonesConsumidos"]) : 0;

                        decimal totalDistanciaKm = dtIndicadores.Rows[0]["TotalDistanciaKm"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalDistanciaKm"]) : 0;

                        decimal totalMontoGastado = dtIndicadores.Rows[0]["TotalMontoGastado"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalMontoGastado"]) : 0;

                        decimal rendimientoGlobal = dtIndicadores.Rows[0]["RendimientoGlobal"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["RendimientoGlobal"]) : 0;

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalGalonesConsumidos:N2} gal";
                        litTotalEgresos.Text = $"{totalDistanciaKm:N0} km";
                        litBalance.Text = $"$ {totalMontoGastado:N2}";
                        litIndicadorAdicionalTitulo.Text = "Rendimiento";
                        litIndicadorAdicional.Text = $"{rendimientoGlobal:N2} km/gal";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0.00 gal";
                        litTotalEgresos.Text = "0 km";
                        litBalance.Text = "$ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Rendimiento";
                        litIndicadorAdicional.Text = "0.00 km/gal";
                    }

                    // Actualizar la interfaz
                    string tituloReporte = "Consumo de Combustible por Vehículo";
                    if (!string.IsNullOrEmpty(idVehiculo))
                    {
                        string placaVehiculo = "";
                        if (dtAbastecimientos.Rows.Count > 0)
                        {
                            placaVehiculo = dtAbastecimientos.Rows[0]["PlacaTracto"] != DBNull.Value ?
                                dtAbastecimientos.Rows[0]["PlacaTracto"].ToString() : "";
                        }
                        else
                        {
                            foreach (ListItem item in ddlVehiculo.Items)
                            {
                                if (item.Value == idVehiculo)
                                {
                                    placaVehiculo = item.Text;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(placaVehiculo))
                        {
                            tituloReporte += ": " + placaVehiculo;
                        }
                    }

                    litTituloResultados.Text = tituloReporte;
                    lblTotalRegistros.Text = $"Total registros: {dtAbastecimientos.Rows.Count}";
                    gvReporte.DataSource = dtAbastecimientos;
                    gvReporte.DataBind();

                    // Mostrar el modal con los resultados
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowResultsModal",
                        "$('#modalResultados').modal('show');", true);
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NumeroAbastecimiento", typeof(string));
                dt.Columns.Add("PlacaTracto", typeof(string));
                dt.Columns.Add("PlacaCarreta", typeof(string));
                dt.Columns.Add("Conductor", typeof(string));
                dt.Columns.Add("ProductoCombustible", typeof(string));
                dt.Columns.Add("LugarAbastecimiento", typeof(string));
                dt.Columns.Add("FechaAbastecimiento", typeof(DateTime));
                dt.Columns.Add("GalonesAsignados", typeof(decimal));
                dt.Columns.Add("GalonesComprados", typeof(decimal));
                dt.Columns.Add("TotalGalones", typeof(decimal));
                dt.Columns.Add("GalonesSobrantes", typeof(decimal));
                dt.Columns.Add("GalonesConsumidos", typeof(decimal));
                dt.Columns.Add("PrecioDolar", typeof(decimal));
                dt.Columns.Add("MontoTotal", typeof(decimal));
                dt.Columns.Add("DistanciaKM", typeof(decimal));
                dt.Columns.Add("RendimientoKmGalon", typeof(decimal));
                dt.Columns.Add("NombreRuta", typeof(string));
                dt.Columns.Add("NumeroOrdenViaje", typeof(string));

                ConfigurarGridViewConsumoCombustibleVehiculo(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0.00 gal";
                litTotalEgresos.Text = "0 km";
                litBalance.Text = "$ 0.00";
                litIndicadorAdicionalTitulo.Text = "Rendimiento";
                litIndicadorAdicional.Text = "0.00 km/gal";

                litTituloResultados.Text = "Consumo de Combustible por Vehículo";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                    $"alert('Error al generar reporte: {ex.Message.Replace("'", "\\'")}');", true);

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de consumo de combustible: " + ex.Message);
            }
        }

        private void ConfigurarGridViewConsumoCombustibleVehiculo(DataTable dt)
        {
            gvReporte.Columns.Clear();

            // Desactivar ordenación para evitar enlaces en columnas
            gvReporte.AllowSorting = false;

            // Columna de Número de Abastecimiento
            BoundField colNumeroAbastecimiento = new BoundField();
            colNumeroAbastecimiento.DataField = "NumeroAbastecimiento";
            colNumeroAbastecimiento.HeaderText = "Nº Abast.";
            gvReporte.Columns.Add(colNumeroAbastecimiento);

            // Columna de Número de Orden
            BoundField colNumeroOrden = new BoundField();
            colNumeroOrden.DataField = "NumeroOrdenViaje";
            colNumeroOrden.HeaderText = "Nº Orden";
            gvReporte.Columns.Add(colNumeroOrden);

            // Columna de Placa Tracto
            BoundField colPlacaTracto = new BoundField();
            colPlacaTracto.DataField = "PlacaTracto";
            colPlacaTracto.HeaderText = "Tracto";
            gvReporte.Columns.Add(colPlacaTracto);

            // Columna de Conductor
            BoundField colConductor = new BoundField();
            colConductor.DataField = "Conductor";
            colConductor.HeaderText = "Conductor";
            gvReporte.Columns.Add(colConductor);

            // Columna de Producto de Combustible
            BoundField colProducto = new BoundField();
            colProducto.DataField = "ProductoCombustible";
            colProducto.HeaderText = "Combustible";
            gvReporte.Columns.Add(colProducto);

            // Columna de Lugar de Abastecimiento
            BoundField colLugar = new BoundField();
            colLugar.DataField = "LugarAbastecimiento";
            colLugar.HeaderText = "Lugar";
            gvReporte.Columns.Add(colLugar);

            // Columna de Fecha
            BoundField colFecha = new BoundField();
            colFecha.DataField = "FechaAbastecimiento";
            colFecha.HeaderText = "Fecha";
            colFecha.DataFormatString = "{0:dd/MM/yyyy HH:mm}";
            gvReporte.Columns.Add(colFecha);

            // Columna de Galones Asignados
            BoundField colGalAsignados = new BoundField();
            colGalAsignados.DataField = "GalonesAsignados";
            colGalAsignados.HeaderText = "Gal. Asignados";
            colGalAsignados.DataFormatString = "{0:N2}";
            colGalAsignados.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colGalAsignados);

            // Columna de Galones Comprados
            BoundField colGalComprados = new BoundField();
            colGalComprados.DataField = "GalonesComprados";
            colGalComprados.HeaderText = "Gal. Comprados";
            colGalComprados.DataFormatString = "{0:N2}";
            colGalComprados.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colGalComprados);

            // Columna de Galones Consumidos
            BoundField colGalConsumidos = new BoundField();
            colGalConsumidos.DataField = "GalonesConsumidos";
            colGalConsumidos.HeaderText = "Gal. Consumidos";
            colGalConsumidos.DataFormatString = "{0:N2}";
            colGalConsumidos.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colGalConsumidos);

            // Columna de Distancia
            BoundField colDistancia = new BoundField();
            colDistancia.DataField = "DistanciaKM";
            colDistancia.HeaderText = "Distancia (Km)";
            colDistancia.DataFormatString = "{0:N0}";
            colDistancia.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colDistancia);

            // Columna de Rendimiento
            BoundField colRendimiento = new BoundField();
            colRendimiento.DataField = "RendimientoKmGalon";
            colRendimiento.HeaderText = "Km/Galón";
            colRendimiento.DataFormatString = "{0:N2}";
            colRendimiento.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colRendimiento);

            // Columna de Precio Dólar
            BoundField colPrecioDolar = new BoundField();
            colPrecioDolar.DataField = "PrecioDolar";
            colPrecioDolar.HeaderText = "Precio $";
            colPrecioDolar.DataFormatString = "{0:N2}";
            colPrecioDolar.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colPrecioDolar);

            // Columna de Monto Total
            BoundField colMontoTotal = new BoundField();
            colMontoTotal.DataField = "MontoTotal";
            colMontoTotal.HeaderText = "Total $";
            colMontoTotal.DataFormatString = "{0:N2}";
            colMontoTotal.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colMontoTotal);

            // Columna de Ruta
            BoundField colRuta = new BoundField();
            colRuta.DataField = "NombreRuta";
            colRuta.HeaderText = "Ruta";
            gvReporte.Columns.Add(colRuta);

            // Configuración general del GridView
            gvReporte.AllowPaging = true;
            gvReporte.PageSize = 20;
            gvReporte.AutoGenerateColumns = false;
        }

        //RENDIMIENTO POR RUTA - REPORTE PO VEHICULO

        private void GenerarReporteRendimientoPorRuta()
        {
            try
            {
                DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
                DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
                string idVehiculo = ddlVehiculo.SelectedValue;
                string placaVehiculo = txtPlacaVehiculo.Text;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("sp_GenerarReporteRendimientoPorRuta", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Agregar parámetros
                        cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                        cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                        // Parámetros opcionales
                        if (!string.IsNullOrEmpty(idVehiculo))
                        {
                            cmd.Parameters.AddWithValue("@idTracto", idVehiculo);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@idTracto", DBNull.Value);
                        }

                        if (!string.IsNullOrEmpty(placaVehiculo))
                        {
                            cmd.Parameters.AddWithValue("@placaTracto", placaVehiculo);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@placaTracto", DBNull.Value);
                        }

                        // Crear conjunto de datos para almacenar resultados múltiples
                        DataSet ds = new DataSet();
                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        adapter.Fill(ds);

                        // Procesar los resultados principales (primer resultado)
                        if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        {
                            ConfigurarGridViewRendimientoPorRuta(ds.Tables[0]);

                            gvReporte.DataSource = ds.Tables[0];
                            gvReporte.DataBind();

                            lblTotalRegistros.Text = $"Total registros: {ds.Tables[0].Rows.Count}";
                        }
                        else
                        {
                            // Crear una tabla vacía con las columnas correctas para el grid
                            DataTable dtEmpty = new DataTable();
                            dtEmpty.Columns.Add("NombreRuta", typeof(string));
                            dtEmpty.Columns.Add("PlacaTracto", typeof(string));
                            dtEmpty.Columns.Add("CantidadViajes", typeof(int));
                            dtEmpty.Columns.Add("DistanciaTotal", typeof(decimal));
                            dtEmpty.Columns.Add("GalonesTotal", typeof(decimal));
                            dtEmpty.Columns.Add("RendimientoPromedio", typeof(decimal));
                            dtEmpty.Columns.Add("RendimientoMinimo", typeof(decimal));
                            dtEmpty.Columns.Add("RendimientoMaximo", typeof(decimal));
                            dtEmpty.Columns.Add("RendimientoAverage", typeof(decimal));
                            dtEmpty.Columns.Add("CostoTotal", typeof(decimal));

                            ConfigurarGridViewRendimientoPorRuta(dtEmpty);
                            gvReporte.DataSource = dtEmpty;
                            gvReporte.DataBind();

                            lblTotalRegistros.Text = "Total registros: 0";
                        }

                        // Procesar los indicadores calculados (segundo resultado)
                        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                        {
                            DataRow indicadoresRow = ds.Tables[1].Rows[0];

                            // Actualizar los indicadores
                            litTotalIngresos.Text = $"{indicadoresRow["TotalDistancia"]:N0} Km";
                            litTotalEgresos.Text = $"{indicadoresRow["TotalGalones"]:N2} Gal";
                            litBalance.Text = $"{indicadoresRow["RendimientoGeneral"]:N2} Km/Gal";
                            litIndicadorAdicionalTitulo.Text = "Mejor Ruta";

                            string rutaMejorRendimiento = Convert.ToString(indicadoresRow["RutaMejorRendimiento"]);
                            decimal valorMejorRendimiento = Convert.ToDecimal(indicadoresRow["ValorMejorRendimiento"]);

                            litIndicadorAdicional.Text = $"{rutaMejorRendimiento} ({valorMejorRendimiento:N2} Km/Gal)";
                        }
                        else
                        {
                            // Valores por defecto cuando no hay datos
                            litTotalIngresos.Text = "0 Km";
                            litTotalEgresos.Text = "0 Gal";
                            litBalance.Text = "0 Km/Gal";
                            litIndicadorAdicionalTitulo.Text = "Costo Total";
                            litIndicadorAdicional.Text = "$ 0.00";
                        }

                        // Actualizar título del reporte
                        string tituloReporte = "Rendimiento por Ruta";
                        if (!string.IsNullOrEmpty(idVehiculo))
                        {
                            string placaVehiculoTexto = "";
                            foreach (ListItem item in ddlVehiculo.Items)
                            {
                                if (item.Value == idVehiculo)
                                {
                                    placaVehiculoTexto = item.Text;
                                    break;
                                }
                            }

                            if (!string.IsNullOrEmpty(placaVehiculoTexto))
                            {
                                tituloReporte += " - Vehículo: " + placaVehiculoTexto;
                            }
                        }

                        litTituloResultados.Text = tituloReporte;
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NombreRuta", typeof(string));
                dt.Columns.Add("PlacaTracto", typeof(string));
                dt.Columns.Add("CantidadViajes", typeof(int));
                dt.Columns.Add("DistanciaTotal", typeof(decimal));
                dt.Columns.Add("GalonesTotal", typeof(decimal));
                dt.Columns.Add("RendimientoPromedio", typeof(decimal));
                dt.Columns.Add("RendimientoMinimo", typeof(decimal));
                dt.Columns.Add("RendimientoMaximo", typeof(decimal));
                dt.Columns.Add("RendimientoAverage", typeof(decimal));
                dt.Columns.Add("CostoTotal", typeof(decimal));

                ConfigurarGridViewRendimientoPorRuta(dt);

                litTotalIngresos.Text = "0 Km";
                litTotalEgresos.Text = "0 Gal";
                litBalance.Text = "0 Km/Gal";
                litIndicadorAdicionalTitulo.Text = "Costo Total";
                litIndicadorAdicional.Text = "$ 0.00";

                litTituloResultados.Text = "Rendimiento por Ruta";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                System.Diagnostics.Debug.WriteLine("Error al generar reporte de rendimiento por ruta: " + ex.Message);
                ScriptManager.RegisterStartupScript(this, GetType(), "errorReporte",
                    "alert('Error al generar el reporte: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        private void ConfigurarGridViewRendimientoPorRuta(DataTable dt)
        {
            gvReporte.Columns.Clear();

            // Desactivar la ordenación para evitar que las columnas aparezcan como enlaces
            gvReporte.AllowSorting = false;

            // Columna de Ruta
            BoundField colRuta = new BoundField();
            colRuta.DataField = "NombreRuta";
            colRuta.HeaderText = "Ruta";
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colRuta);

            // Columna de Placa Tracto
            BoundField colPlaca = new BoundField();
            colPlaca.DataField = "PlacaTracto";
            colPlaca.HeaderText = "Placa Tracto";
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colPlaca);

            // Columna de Cantidad de Viajes
            BoundField colViajes = new BoundField();
            colViajes.DataField = "CantidadViajes";
            colViajes.HeaderText = "Viajes";
            // Eliminamos SortExpression
            colViajes.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colViajes);

            // Columna de Distancia Total
            BoundField colDistancia = new BoundField();
            colDistancia.DataField = "DistanciaTotal";
            colDistancia.HeaderText = "Distancia Total (Km)";
            colDistancia.DataFormatString = "{0:N0}";
            colDistancia.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colDistancia);

            // Columna de Galones Total
            BoundField colGalones = new BoundField();
            colGalones.DataField = "GalonesTotal";
            colGalones.HeaderText = "Combustible (Gal)";
            colGalones.DataFormatString = "{0:N2}";
            colGalones.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colGalones);

            // Columna de Rendimiento Promedio
            BoundField colRendimientoPromedio = new BoundField();
            colRendimientoPromedio.DataField = "RendimientoPromedio";
            colRendimientoPromedio.HeaderText = "Rendimiento (Km/Gal)";
            colRendimientoPromedio.DataFormatString = "{0:N2}";
            colRendimientoPromedio.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colRendimientoPromedio);

            // Columna de Rendimiento Mínimo
            BoundField colRendimientoMinimo = new BoundField();
            colRendimientoMinimo.DataField = "RendimientoMinimo";
            colRendimientoMinimo.HeaderText = "Rend. Mínimo";
            colRendimientoMinimo.DataFormatString = "{0:N2}";
            colRendimientoMinimo.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colRendimientoMinimo);

            // Columna de Rendimiento Máximo
            BoundField colRendimientoMaximo = new BoundField();
            colRendimientoMaximo.DataField = "RendimientoMaximo";
            colRendimientoMaximo.HeaderText = "Rend. Máximo";
            colRendimientoMaximo.DataFormatString = "{0:N2}";
            colRendimientoMaximo.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colRendimientoMaximo);

            // Columna de Costo Total
            BoundField colCostoTotal = new BoundField();
            colCostoTotal.DataField = "CostoTotal";
            colCostoTotal.HeaderText = "Costo Total ($)";
            colCostoTotal.DataFormatString = "{0:N2}";
            colCostoTotal.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            // Eliminamos SortExpression
            gvReporte.Columns.Add(colCostoTotal);

            // Configuración general del GridView
            gvReporte.AllowPaging = true;
            gvReporte.PageSize = 20;
            gvReporte.AutoGenerateColumns = false;
        }


        //gastos de mantenimiento - reporte por vehiculo

        private void GenerarReporteMantenimientoVehiculo()
        {
            try
            {
                DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
                DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
                string idVehiculo = ddlVehiculo.SelectedValue;
                string placaVehiculo = txtPlacaVehiculo.Text;
                string marcaVehiculo = ddlMarcaVehiculo.SelectedValue;
                string modeloVehiculo = ddlModeloVehiculo.SelectedValue;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("sp_GenerarReporteMantenimientoVehiculo", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Agregar parámetros
                        cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                        cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                        // Parámetros opcionales
                        if (!string.IsNullOrEmpty(idVehiculo))
                        {
                            cmd.Parameters.AddWithValue("@idTracto", idVehiculo);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@idTracto", DBNull.Value);
                        }

                        if (!string.IsNullOrEmpty(placaVehiculo))
                        {
                            cmd.Parameters.AddWithValue("@placaTracto", placaVehiculo);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@placaTracto", DBNull.Value);
                        }

                        if (!string.IsNullOrEmpty(marcaVehiculo))
                        {
                            cmd.Parameters.AddWithValue("@marcaVehiculo", marcaVehiculo);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@marcaVehiculo", DBNull.Value);
                        }

                        if (!string.IsNullOrEmpty(modeloVehiculo))
                        {
                            cmd.Parameters.AddWithValue("@modeloVehiculo", modeloVehiculo);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@modeloVehiculo", DBNull.Value);
                        }

                        // Crear conjunto de datos para almacenar resultados múltiples
                        DataSet ds = new DataSet();
                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        adapter.Fill(ds);

                        // Procesar los resultados principales (primer resultado)
                        if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        {
                            ConfigurarGridViewMantenimientoVehiculo(ds.Tables[0]);

                            gvReporte.DataSource = ds.Tables[0];
                            gvReporte.DataBind();

                            lblTotalRegistros.Text = $"Total registros: {ds.Tables[0].Rows.Count}";
                        }
                        else
                        {
                            // Crear una tabla vacía con las columnas correctas para el grid
                            DataTable dtEmpty = new DataTable();
                            dtEmpty.Columns.Add("NumeroOrden", typeof(string));
                            dtEmpty.Columns.Add("PlacaTracto", typeof(string));
                            dtEmpty.Columns.Add("MarcaVehiculo", typeof(string));
                            dtEmpty.Columns.Add("ModeloVehiculo", typeof(string));
                            dtEmpty.Columns.Add("Conductor", typeof(string));
                            dtEmpty.Columns.Add("Concepto", typeof(string));
                            dtEmpty.Columns.Add("Fecha", typeof(DateTime));
                            dtEmpty.Columns.Add("MontoSoles", typeof(decimal));
                            dtEmpty.Columns.Add("MontoDolares", typeof(decimal));
                            dtEmpty.Columns.Add("Descripcion", typeof(string));
                            dtEmpty.Columns.Add("TipoGasto", typeof(string));

                            ConfigurarGridViewMantenimientoVehiculo(dtEmpty);
                            gvReporte.DataSource = dtEmpty;
                            gvReporte.DataBind();

                            lblTotalRegistros.Text = "Total registros: 0";
                        }

                        // Procesar los indicadores calculados (segundo resultado)
                        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                        {
                            DataRow indicadoresRow = ds.Tables[1].Rows[0];

                            // Actualizar los indicadores con datos del procedimiento almacenado
                            decimal totalSoles = Convert.ToDecimal(indicadoresRow["TotalSoles"]);
                            decimal totalDolares = Convert.ToDecimal(indicadoresRow["TotalDolares"]);
                            int totalVehiculos = Convert.ToInt32(indicadoresRow["TotalVehiculos"]);
                            string vehiculoMayorGasto = Convert.ToString(indicadoresRow["VehiculoMayorGasto"]);
                            decimal valorMayorGasto = Convert.ToDecimal(indicadoresRow["ValorMayorGasto"]);

                            litTotalIngresos.Text = $"S/ {totalSoles:N2}";
                            litTotalEgresos.Text = $"$ {totalDolares:N2}";
                            litBalance.Text = $"{totalVehiculos} vehículos";
                            litIndicadorAdicionalTitulo.Text = "Mayor Gasto";
                            litIndicadorAdicional.Text = !string.IsNullOrEmpty(vehiculoMayorGasto) ?
                                $"{vehiculoMayorGasto} (S/ {valorMayorGasto:N2})" : "N/A";
                        }
                        else
                        {
                            // Valores por defecto cuando no hay datos
                            litTotalIngresos.Text = "S/ 0.00";
                            litTotalEgresos.Text = "$ 0.00";
                            litBalance.Text = "0 vehículos";
                            litIndicadorAdicionalTitulo.Text = "Mayor Gasto";
                            litIndicadorAdicional.Text = "N/A";
                        }

                        // Actualizar título del reporte
                        string tituloReporte = "Gastos de Mantenimiento por Vehículo";
                        if (!string.IsNullOrEmpty(idVehiculo))
                        {
                            string placaVehiculoTexto = "";
                            if (ds.Tables[0].Rows.Count > 0)
                            {
                                placaVehiculoTexto = ds.Tables[0].Rows[0]["PlacaTracto"].ToString();
                            }
                            else
                            {
                                foreach (ListItem item in ddlVehiculo.Items)
                                {
                                    if (item.Value == idVehiculo)
                                    {
                                        placaVehiculoTexto = item.Text;
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(placaVehiculoTexto))
                            {
                                tituloReporte += ": " + placaVehiculoTexto;
                            }
                        }

                        litTituloResultados.Text = tituloReporte;
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("NumeroOrden", typeof(string));
                dt.Columns.Add("PlacaTracto", typeof(string));
                dt.Columns.Add("MarcaVehiculo", typeof(string));
                dt.Columns.Add("ModeloVehiculo", typeof(string));
                dt.Columns.Add("Conductor", typeof(string));
                dt.Columns.Add("Concepto", typeof(string));
                dt.Columns.Add("Fecha", typeof(DateTime));
                dt.Columns.Add("MontoSoles", typeof(decimal));
                dt.Columns.Add("MontoDolares", typeof(decimal));
                dt.Columns.Add("Descripcion", typeof(string));
                dt.Columns.Add("TipoGasto", typeof(string));

                ConfigurarGridViewMantenimientoVehiculo(dt);

                litTotalIngresos.Text = "S/ 0.00";
                litTotalEgresos.Text = "$ 0.00";
                litBalance.Text = "0 vehículos";
                litIndicadorAdicionalTitulo.Text = "Mayor Gasto";
                litIndicadorAdicional.Text = "N/A";

                litTituloResultados.Text = "Gastos de Mantenimiento por Vehículo";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                System.Diagnostics.Debug.WriteLine("Error al generar reporte de mantenimiento: " + ex.Message);
                ScriptManager.RegisterStartupScript(this, GetType(), "errorReporte",
                    "alert('Error al generar el reporte: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        private void ConfigurarGridViewMantenimientoVehiculo(DataTable dt)
        {
            gvReporte.Columns.Clear();

            // Desactivar ordenación para evitar enlaces en columnas
            gvReporte.AllowSorting = false;

            // Columna de Número de Orden
            BoundField colNumeroOrden = new BoundField();
            colNumeroOrden.DataField = "NumeroOrden";
            colNumeroOrden.HeaderText = "Nº Orden";
            gvReporte.Columns.Add(colNumeroOrden);

            // Columna de Fecha
            BoundField colFecha = new BoundField();
            colFecha.DataField = "Fecha";
            colFecha.HeaderText = "Fecha";
            colFecha.DataFormatString = "{0:dd/MM/yyyy}";
            gvReporte.Columns.Add(colFecha);

            // Columna de Placa
            BoundField colPlaca = new BoundField();
            colPlaca.DataField = "PlacaTracto";
            colPlaca.HeaderText = "Placa";
            gvReporte.Columns.Add(colPlaca);

            // Columna de Marca
            BoundField colMarca = new BoundField();
            colMarca.DataField = "MarcaVehiculo";
            colMarca.HeaderText = "Marca";
            gvReporte.Columns.Add(colMarca);

            // Columna de Modelo
            BoundField colModelo = new BoundField();
            colModelo.DataField = "ModeloVehiculo";
            colModelo.HeaderText = "Modelo";
            gvReporte.Columns.Add(colModelo);

            // Columna de Concepto
            BoundField colConcepto = new BoundField();
            colConcepto.DataField = "Concepto";
            colConcepto.HeaderText = "Concepto";
            gvReporte.Columns.Add(colConcepto);

            // Columna de Conductor
            BoundField colConductor = new BoundField();
            colConductor.DataField = "Conductor";
            colConductor.HeaderText = "Conductor";
            gvReporte.Columns.Add(colConductor);

            // Columna de Monto en Soles
            BoundField colMontoSoles = new BoundField();
            colMontoSoles.DataField = "MontoSoles";
            colMontoSoles.HeaderText = "Monto S/";
            colMontoSoles.DataFormatString = "{0:N2}";
            colMontoSoles.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colMontoSoles);

            // Columna de Monto en Dólares
            BoundField colMontoDolares = new BoundField();
            colMontoDolares.DataField = "MontoDolares";
            colMontoDolares.HeaderText = "Monto $";
            colMontoDolares.DataFormatString = "{0:N2}";
            colMontoDolares.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colMontoDolares);

            // Columna de Descripción
            BoundField colDescripcion = new BoundField();
            colDescripcion.DataField = "Descripcion";
            colDescripcion.HeaderText = "Descripción";
            gvReporte.Columns.Add(colDescripcion);

            // Columna de Tipo de Gasto
            BoundField colTipoGasto = new BoundField();
            colTipoGasto.DataField = "TipoGasto";
            colTipoGasto.HeaderText = "Tipo";
            gvReporte.Columns.Add(colTipoGasto);

            // Configuración general del GridView
            gvReporte.AllowPaging = true;
            gvReporte.PageSize = 20;
            gvReporte.AutoGenerateColumns = false;
        }

        //balance generak - pedido por reporte


        // Métodos para implementaciones futuras de reportes financieros
        private void GenerarReporteFinanciero_IngresosDetallados()
        {
            // Aquí implementaremos el reporte de ingresos detallados
            GenerarReporteDePrueba();
        }

        private void GenerarReporteFinanciero_EgresosDetallados()
        {
            // Aquí implementaremos el reporte de egresos detallados 
            GenerarReporteDePrueba();
        }

        private void GenerarReporteFinanciero_Comparativo()
        {
            // Aquí implementaremos el reporte de análisis comparativo
            GenerarReporteDePrueba();
        }



        private void GenerarReporteDePrueba()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("Descripción", typeof(string));
            dt.Columns.Add("Valor", typeof(decimal));

            for (int i = 1; i <= 10; i++)
            {
                dt.Rows.Add(i, $"Item de prueba {i}", i * 100.50m);
            }

            // Configurar GridView
            gvReporte.Columns.Clear();
            BoundField bfId = new BoundField { DataField = "ID", HeaderText = "ID", SortExpression = "ID" };
            BoundField bfDesc = new BoundField { DataField = "Descripción", HeaderText = "Descripción", SortExpression = "Descripción" };
            BoundField bfValor = new BoundField { DataField = "Valor", HeaderText = "Valor", SortExpression = "Valor", DataFormatString = "{0:N2}" };
            gvReporte.Columns.Add(bfId);
            gvReporte.Columns.Add(bfDesc);
            gvReporte.Columns.Add(bfValor);

            // Actualizar indicadores
            decimal totalValor = dt.AsEnumerable().Sum(row => row.Field<decimal>("Valor"));
            litTotalIngresos.Text = $"S/ {totalValor:N2}";
            litTotalEgresos.Text = "S/ 0.00";
            litBalance.Text = $"S/ {totalValor:N2}";
            litIndicadorAdicionalTitulo.Text = "Registros";
            litIndicadorAdicional.Text = dt.Rows.Count.ToString();

            // Actualizar UI
            litTituloResultados.Text = "Reporte de Prueba";
            lblTotalRegistros.Text = $"Total registros: {dt.Rows.Count}";
            gvReporte.DataSource = dt;
            gvReporte.DataBind();
        }

        //reportes por producto
        //reportes por producto - productos mas transportados

        private void GenerarReporteProductosMasTransportados()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idProducto = ddlProducto.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteProductosMasTransportados", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetro opcional del producto
                    if (!string.IsNullOrEmpty(idProducto) && idProducto != "0")
                        cmd.Parameters.AddWithValue("@idProducto", idProducto);
                    else
                        cmd.Parameters.AddWithValue("@idProducto", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de productos
                    DataTable dtProductos = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewProductosMasTransportados(dtProductos);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalProductos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalProductos"]);
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        int totalBolsas = Convert.ToInt32(dtIndicadores.Rows[0]["TotalBolsasGlobal"]);
                        decimal totalKilos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalKilosGlobal"]);
                        int totalClientes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalClientesGlobal"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalProductos} productos";
                        litTotalEgresos.Text = $"{totalViajes} viajes";
                        litBalance.Text = $"{totalBolsas:N0} bolsas";
                        litIndicadorAdicionalTitulo.Text = "Peso Total";
                        litIndicadorAdicional.Text = $"{totalKilos:N2} Kg";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 productos";
                        litTotalEgresos.Text = "0 viajes";
                        litBalance.Text = "0 bolsas";
                        litIndicadorAdicionalTitulo.Text = "Peso Total";
                        litIndicadorAdicional.Text = "0.00 Kg";
                    }

                    // Actualizar la interfaz
                    litTituloResultados.Text = "Ranking de Productos Más Transportados";
                    lblTotalRegistros.Text = $"Total registros: {dtProductos.Rows.Count}";
                    gvReporte.DataSource = dtProductos;
                    gvReporte.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("idProducto", typeof(int));
                dt.Columns.Add("NombreProducto", typeof(string));
                dt.Columns.Add("TotalViajes", typeof(int));
                dt.Columns.Add("TotalBolsas", typeof(int));
                dt.Columns.Add("TotalKilos", typeof(decimal));
                dt.Columns.Add("TotalConductores", typeof(int));
                dt.Columns.Add("TotalClientes", typeof(int));
                dt.Columns.Add("TotalDestinos", typeof(int));

                ConfigurarGridViewProductosMasTransportados(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 productos";
                litTotalEgresos.Text = "0 viajes";
                litBalance.Text = "0 bolsas";
                litIndicadorAdicionalTitulo.Text = "Peso Total";
                litIndicadorAdicional.Text = "0.00 Kg";

                litTituloResultados.Text = "Ranking de Productos Más Transportados";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de productos más transportados: " + ex.Message);
            }
        }

        private void ConfigurarGridViewProductosMasTransportados(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NombreProducto", HeaderText = "Producto", SortExpression = "NombreProducto" });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalViajes", HeaderText = "Viajes", SortExpression = "TotalViajes", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalBolsas", HeaderText = "Bolsas", DataFormatString = "{0:N0}", SortExpression = "TotalBolsas", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalKilos", HeaderText = "Peso (Kg)", DataFormatString = "{0:N2}", SortExpression = "TotalKilos", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalConductores", HeaderText = "Conductores", SortExpression = "TotalConductores", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalClientes", HeaderText = "Clientes", SortExpression = "TotalClientes", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalDestinos", HeaderText = "Destinos", SortExpression = "TotalDestinos", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
        }

        ////reportes por producto - GenerarReporteProductosPorCliente
        private void GenerarReporteProductosPorCliente()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idCliente = ddlClienteProducto.SelectedValue; // Corregido: usar ddlClienteProducto en lugar de ddlCliente
            string idProducto = ddlProducto.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteProductosPorCliente", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(idCliente) && idCliente != "0")
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);
                    else
                        cmd.Parameters.AddWithValue("@idCliente", DBNull.Value);

                    if (!string.IsNullOrEmpty(idProducto) && idProducto != "0")
                        cmd.Parameters.AddWithValue("@idProducto", idProducto);
                    else
                        cmd.Parameters.AddWithValue("@idProducto", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de clientes y sus productos
                    DataTable dtClientes = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewProductosPorCliente(dtClientes);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalClientes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalClientes"]);
                        int totalProductos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalProductos"]);
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        int totalBolsas = Convert.ToInt32(dtIndicadores.Rows[0]["TotalBolsasGlobal"]);
                        decimal totalKilos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalKilosGlobal"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalClientes} clientes";
                        litTotalEgresos.Text = $"{totalProductos} productos";
                        litBalance.Text = $"{totalViajes} viajes";
                        litIndicadorAdicionalTitulo.Text = "Volumen Total";
                        litIndicadorAdicional.Text = $"{totalBolsas:N0} bolsas / {totalKilos:N2} Kg";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 clientes";
                        litTotalEgresos.Text = "0 productos";
                        litBalance.Text = "0 viajes";
                        litIndicadorAdicionalTitulo.Text = "Volumen Total";
                        litIndicadorAdicional.Text = "0 bolsas / 0.00 Kg";
                    }

                    // Actualizar la interfaz
                    litTituloResultados.Text = "Reporte de Productos por Cliente";
                    lblTotalRegistros.Text = $"Total registros: {dtClientes.Rows.Count}";
                    gvReporte.DataSource = dtClientes;
                    gvReporte.DataBind();

                    // Mostrar el modal con los resultados
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowResultsModal",
                        "$('#modalResultados').modal('show');", true);
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("idCliente", typeof(int));
                dt.Columns.Add("NombreCliente", typeof(string));
                dt.Columns.Add("RUC", typeof(string));
                dt.Columns.Add("TotalProductos", typeof(int));
                dt.Columns.Add("TotalViajes", typeof(int));
                dt.Columns.Add("TotalBolsas", typeof(int));
                dt.Columns.Add("TotalKilos", typeof(decimal));
                dt.Columns.Add("Productos", typeof(string));
                dt.Columns.Add("Destinos", typeof(string));

                ConfigurarGridViewProductosPorCliente(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 clientes";
                litTotalEgresos.Text = "0 productos";
                litBalance.Text = "0 viajes";
                litIndicadorAdicionalTitulo.Text = "Volumen Total";
                litIndicadorAdicional.Text = "0 bolsas / 0.00 Kg";

                litTituloResultados.Text = "Reporte de Productos por Cliente";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                    $"alert('Error al generar reporte: {ex.Message.Replace("'", "\\'")}');", true);

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de productos por cliente: " + ex.Message);
            }
        }

        private void ConfigurarGridViewProductosPorCliente(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NombreCliente", HeaderText = "Cliente", SortExpression = "NombreCliente" });
            gvReporte.Columns.Add(new BoundField { DataField = "RUC", HeaderText = "RUC", SortExpression = "RUC" });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalProductos", HeaderText = "Productos", SortExpression = "TotalProductos", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalViajes", HeaderText = "Viajes", SortExpression = "TotalViajes", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalBolsas", HeaderText = "Bolsas", DataFormatString = "{0:N0}", SortExpression = "TotalBolsas", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalKilos", HeaderText = "Peso (Kg)", DataFormatString = "{0:N2}", SortExpression = "TotalKilos", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "Productos", HeaderText = "Lista de Productos", SortExpression = "Productos" });
            gvReporte.Columns.Add(new BoundField { DataField = "Destinos", HeaderText = "Destinos", SortExpression = "Destinos" });
        }


        ////reportes por producto - GenerarReporteProductosPorDestino

        private void GenerarReporteProductosPorDestino()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
            string idProducto = ddlProducto.SelectedValue;
            string idPlanta = ddlPlantaDescarga.SelectedValue;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteProductosPorDestino", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(idProducto) && idProducto != "0")
                        cmd.Parameters.AddWithValue("@idProducto", idProducto);
                    else
                        cmd.Parameters.AddWithValue("@idProducto", DBNull.Value);

                    if (!string.IsNullOrEmpty(idPlanta) && idPlanta != "0")
                        cmd.Parameters.AddWithValue("@idPlanta", idPlanta);
                    else
                        cmd.Parameters.AddWithValue("@idPlanta", DBNull.Value);

                    // Obtener datos e indicadores
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // El primer DataTable contiene los datos de destinos y sus productos
                    DataTable dtDestinos = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // Configurar el GridView
                    ConfigurarGridViewProductosPorDestino(dtDestinos);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores.Rows.Count > 0)
                    {
                        int totalDestinos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalDestinos"]);
                        int totalProductos = Convert.ToInt32(dtIndicadores.Rows[0]["TotalProductos"]);
                        int totalViajes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalViajes"]);
                        int totalBolsas = Convert.ToInt32(dtIndicadores.Rows[0]["TotalBolsasGlobal"]);
                        decimal totalKilos = Convert.ToDecimal(dtIndicadores.Rows[0]["TotalKilosGlobal"]);
                        int totalClientes = Convert.ToInt32(dtIndicadores.Rows[0]["TotalClientesGlobal"]);

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalDestinos} destinos";
                        litTotalEgresos.Text = $"{totalProductos} productos";
                        litBalance.Text = $"{totalViajes} viajes";
                        litIndicadorAdicionalTitulo.Text = "Volumen Total";
                        litIndicadorAdicional.Text = $"{totalBolsas:N0} bolsas / {totalKilos:N2} Kg";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0 destinos";
                        litTotalEgresos.Text = "0 productos";
                        litBalance.Text = "0 viajes";
                        litIndicadorAdicionalTitulo.Text = "Volumen Total";
                        litIndicadorAdicional.Text = "0 bolsas / 0.00 Kg";
                    }

                    // Actualizar la interfaz
                    litTituloResultados.Text = "Reporte de Productos por Destino";
                    lblTotalRegistros.Text = $"Total registros: {dtDestinos.Rows.Count}";
                    gvReporte.DataSource = dtDestinos;
                    gvReporte.DataBind();

                    // Mostrar el modal con los resultados
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowResultsModal",
                        "$('#modalResultados').modal('show');", true);
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("idPlanta", typeof(int));
                dt.Columns.Add("NombreDestino", typeof(string));
                dt.Columns.Add("TotalProductos", typeof(int));
                dt.Columns.Add("TotalViajes", typeof(int));
                dt.Columns.Add("TotalBolsas", typeof(int));
                dt.Columns.Add("TotalKilos", typeof(decimal));
                dt.Columns.Add("Productos", typeof(string));
                dt.Columns.Add("Clientes", typeof(string));

                ConfigurarGridViewProductosPorDestino(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0 destinos";
                litTotalEgresos.Text = "0 productos";
                litBalance.Text = "0 viajes";
                litIndicadorAdicionalTitulo.Text = "Volumen Total";
                litIndicadorAdicional.Text = "0 bolsas / 0.00 Kg";

                litTituloResultados.Text = "Reporte de Productos por Destino";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                    $"alert('Error al generar reporte: {ex.Message.Replace("'", "\\'")}');", true);

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de productos por destino: " + ex.Message);
            }
        }

        private void ConfigurarGridViewProductosPorDestino(DataTable dt)
        {
            gvReporte.Columns.Clear();

            gvReporte.Columns.Add(new BoundField { DataField = "NombreDestino", HeaderText = "Destino", SortExpression = "NombreDestino" });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalProductos", HeaderText = "Productos", SortExpression = "TotalProductos", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalViajes", HeaderText = "Viajes", SortExpression = "TotalViajes", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalBolsas", HeaderText = "Bolsas", DataFormatString = "{0:N0}", SortExpression = "TotalBolsas", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "TotalKilos", HeaderText = "Peso (Kg)", DataFormatString = "{0:N2}", SortExpression = "TotalKilos", ItemStyle = { HorizontalAlign = HorizontalAlign.Right } });
            gvReporte.Columns.Add(new BoundField { DataField = "Productos", HeaderText = "Lista de Productos", SortExpression = "Productos" });
            gvReporte.Columns.Add(new BoundField { DataField = "Clientes", HeaderText = "Clientes", SortExpression = "Clientes" });
        }


        private void GenerarReporteConsumoGeneralCombustible()
        {
            DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
            DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Crear comando para el procedimiento almacenado
                    SqlCommand cmd = new SqlCommand("sp_ReporteConsumoGeneralCombustible", conn);
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Añadir parámetros obligatorios
                    cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                    cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                    // Parámetros opcionales
                    if (!string.IsNullOrEmpty(ddlLugarAbastecimiento.SelectedValue) && ddlLugarAbastecimiento.SelectedValue != "0")
                    {
                        cmd.Parameters.Add("@idLugarAbastecimiento", SqlDbType.Int).Value =
                            Convert.ToInt32(ddlLugarAbastecimiento.SelectedValue);
                    }
                    else
                    {
                        cmd.Parameters.Add("@idLugarAbastecimiento", SqlDbType.Int).Value = DBNull.Value;
                    }

                    // Ejecutar y obtener resultados en un DataSet
                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataSet ds = new DataSet();
                    adapter.Fill(ds);

                    // Asegurarse de que tenemos todas las tablas de resultados
                    if (ds.Tables.Count < 3)
                    {
                        throw new Exception("El procedimiento no devolvió todos los datos esperados.");
                    }

                    // El primer DataTable contiene los datos de consumo general por fecha y lugar
                    DataTable dtConsumoGeneral = ds.Tables[0];

                    // El segundo DataTable contiene los indicadores calculados
                    DataTable dtIndicadores = ds.Tables[1];

                    // El tercer DataTable contiene datos por lugar de abastecimiento
                    DataTable dtLugares = ds.Tables[2];

                    // Configurar el GridView para mostrar los datos generales
                    ConfigurarGridViewConsumoGeneralCombustible(dtConsumoGeneral);

                    // Mostrar indicadores si hay datos
                    if (dtIndicadores != null && dtIndicadores.Rows.Count > 0)
                    {
                        decimal totalGalonesConsumidos = dtIndicadores.Rows[0]["TotalGalonesConsumidos"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalGalonesConsumidos"]) : 0;

                        decimal totalGalonesComprados = dtIndicadores.Rows[0]["TotalGalonesComprados"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalGalonesComprados"]) : 0;

                        decimal totalDistanciaRecorrida = dtIndicadores.Rows[0]["TotalDistanciaRecorrida"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalDistanciaRecorrida"]) : 0;

                        decimal totalGastoMonetario = dtIndicadores.Rows[0]["TotalGastoMonetario"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["TotalGastoMonetario"]) : 0;

                        decimal rendimientoGlobal = dtIndicadores.Rows[0]["RendimientoGlobalKmGal"] != DBNull.Value ?
                            Convert.ToDecimal(dtIndicadores.Rows[0]["RendimientoGlobalKmGal"]) : 0;

                        // Actualizar interfaz con los indicadores
                        litTotalIngresos.Text = $"{totalGalonesConsumidos:N2} gal";
                        litTotalEgresos.Text = $"{totalDistanciaRecorrida:N0} km";
                        litBalance.Text = $"$ {totalGastoMonetario:N2}";
                        litIndicadorAdicionalTitulo.Text = "Gal. Comprados";
                        litIndicadorAdicional.Text = $"{totalGalonesComprados:N2} gal";
                    }
                    else
                    {
                        // Si no hay datos, inicializar a cero
                        litTotalIngresos.Text = "0.00 gal";
                        litTotalEgresos.Text = "0 km";
                        litBalance.Text = "$ 0.00";
                        litIndicadorAdicionalTitulo.Text = "Gal. Comprados";
                        litIndicadorAdicional.Text = "0.00 gal";
                    }

                    // Actualizar la interfaz
                    string tituloReporte = "Consumo General de Combustible";

                    if (!string.IsNullOrEmpty(ddlLugarAbastecimiento.SelectedValue) &&
                        ddlLugarAbastecimiento.SelectedValue != "0" &&
                        ddlLugarAbastecimiento.SelectedItem != null)
                    {
                        tituloReporte += " - Lugar: " + ddlLugarAbastecimiento.SelectedItem.Text;
                    }

                    litTituloResultados.Text = tituloReporte;
                    lblTotalRegistros.Text = $"Total registros: {dtConsumoGeneral.Rows.Count}";
                    gvReporte.DataSource = dtConsumoGeneral;
                    gvReporte.DataBind();

                    // Mostrar el modal con los resultados
                    ScriptManager.RegisterStartupScript(this, GetType(), "ShowResultsModal",
                        "$('#modalResultados').modal('show');", true);
                }
            }
            catch (Exception ex)
            {
                // Manejo de errores
                DataTable dt = new DataTable();
                dt.Columns.Add("Fecha", typeof(DateTime));
                dt.Columns.Add("LugarAbastecimiento", typeof(string));
                dt.Columns.Add("CantidadAbastecimientos", typeof(int));
                dt.Columns.Add("TotalGalonesComprados", typeof(decimal));
                dt.Columns.Add("TotalGalonesConsumidos", typeof(decimal));
                dt.Columns.Add("TotalMontoPagado", typeof(decimal));
                dt.Columns.Add("RendimientoPromedio", typeof(decimal));
                dt.Columns.Add("CantidadVehiculos", typeof(int));
                dt.Columns.Add("CantidadConductores", typeof(int));

                ConfigurarGridViewConsumoGeneralCombustible(dt);

                // Inicializar indicadores en cero
                litTotalIngresos.Text = "0.00 gal";
                litTotalEgresos.Text = "0 km";
                litBalance.Text = "$ 0.00";
                litIndicadorAdicionalTitulo.Text = "Gal. Comprados";
                litIndicadorAdicional.Text = "0.00 gal";

                litTituloResultados.Text = "Consumo General de Combustible";
                lblTotalRegistros.Text = "Total registros: 0";
                gvReporte.DataSource = dt;
                gvReporte.DataBind();

                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                    $"alert('Error al generar reporte: {ex.Message.Replace("'", "\\'")}');", true);

                // Registrar el error
                System.Diagnostics.Debug.WriteLine("Error al generar reporte de consumo general: " + ex.Message);
            }
        }

        private void ConfigurarGridViewConsumoGeneralCombustible(DataTable dt)
        {
            gvReporte.Columns.Clear();

            // Desactivar ordenación para evitar enlaces en columnas
            gvReporte.AllowSorting = false;

            // Columna de Fecha
            BoundField colFecha = new BoundField();
            colFecha.DataField = "Fecha";
            colFecha.HeaderText = "Fecha";
            colFecha.DataFormatString = "{0:dd/MM/yyyy}";
            gvReporte.Columns.Add(colFecha);

            // Columna de Lugar de Abastecimiento
            BoundField colLugar = new BoundField();
            colLugar.DataField = "LugarAbastecimiento";
            colLugar.HeaderText = "Lugar";
            gvReporte.Columns.Add(colLugar);

            // Columna de Cantidad de Abastecimientos
            BoundField colCantidadAbastecimientos = new BoundField();
            colCantidadAbastecimientos.DataField = "CantidadAbastecimientos";
            colCantidadAbastecimientos.HeaderText = "Cant. Abastec.";
            colCantidadAbastecimientos.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colCantidadAbastecimientos);

            // Columna de Total Galones Comprados
            BoundField colGalComprados = new BoundField();
            colGalComprados.DataField = "TotalGalonesComprados";
            colGalComprados.HeaderText = "Gal. Comprados";
            colGalComprados.DataFormatString = "{0:N2}";
            colGalComprados.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colGalComprados);

            // Columna de Total Galones Consumidos
            BoundField colGalConsumidos = new BoundField();
            colGalConsumidos.DataField = "TotalGalonesConsumidos";
            colGalConsumidos.HeaderText = "Gal. Consumidos";
            colGalConsumidos.DataFormatString = "{0:N2}";
            colGalConsumidos.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colGalConsumidos);

            // Columna de Total Monto Pagado
            BoundField colMontoTotal = new BoundField();
            colMontoTotal.DataField = "TotalMontoPagado";
            colMontoTotal.HeaderText = "Total $";
            colMontoTotal.DataFormatString = "{0:N2}";
            colMontoTotal.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colMontoTotal);

            // Columna de Rendimiento Promedio
            BoundField colRendimiento = new BoundField();
            colRendimiento.DataField = "RendimientoPromedio";
            colRendimiento.HeaderText = "Km/Galón";
            colRendimiento.DataFormatString = "{0:N2}";
            colRendimiento.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colRendimiento);

            // Columna de Cantidad de Vehículos
            BoundField colCantVehiculos = new BoundField();
            colCantVehiculos.DataField = "CantidadVehiculos";
            colCantVehiculos.HeaderText = "Vehículos";
            colCantVehiculos.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colCantVehiculos);

            // Columna de Cantidad de Conductores
            BoundField colCantConductores = new BoundField();
            colCantConductores.DataField = "CantidadConductores";
            colCantConductores.HeaderText = "Conductores";
            colCantConductores.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            gvReporte.Columns.Add(colCantConductores);

            // Configuración general del GridView
            gvReporte.AllowPaging = true;
            gvReporte.PageSize = 20;
            gvReporte.AutoGenerateColumns = false;
        }

        // Modify the GenerarReporteFinanciero_BalanceGeneral method to ensure consistent column naming
        private void GenerarReporteFinanciero_BalanceGeneral()
        {
            try
            {
                DateTime fechaDesde = DateTime.Parse(txtFechaDesde.Text);
                DateTime fechaHasta = DateTime.Parse(txtFechaHasta.Text);
                string tipoTransaccion = ddlTipoTransaccion.SelectedValue;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand("sp_ReporteFinanciero_BalanceGeneral", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Add parameters
                        cmd.Parameters.AddWithValue("@fechaDesde", fechaDesde);
                        cmd.Parameters.AddWithValue("@fechaHasta", fechaHasta);

                        // Handle the transaction type parameter
                        if (!string.IsNullOrEmpty(tipoTransaccion) && tipoTransaccion != "Todas")
                        {
                            cmd.Parameters.AddWithValue("@tipoTransaccion", tipoTransaccion);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue("@tipoTransaccion", DBNull.Value);
                        }

                        // Debug: Print the SQL command
                        System.Diagnostics.Debug.WriteLine("SQL Command: " + cmd.CommandText);
                        foreach (SqlParameter param in cmd.Parameters)
                        {
                            System.Diagnostics.Debug.WriteLine($"Parameter: {param.ParameterName} = {param.Value}");
                        }

                        // Get multiple result sets
                        DataSet ds = new DataSet();
                        SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                        adapter.Fill(ds);

                        // Process main results (first result)
                        if (ds.Tables.Count > 0)
                        {
                            DataTable tablaResultados = ds.Tables[0];

                            // Debug: Print column names
                            System.Diagnostics.Debug.WriteLine("Columns in result set:");
                            foreach (DataColumn col in tablaResultados.Columns)
                            {
                                System.Diagnostics.Debug.WriteLine($"Column: {col.ColumnName}");
                            }

                            // Check if there are rows in the result
                            if (tablaResultados.Rows.Count > 0)
                            {
                                ConfigurarGridViewFinanciero_BalanceGeneral(tablaResultados);

                                gvReporte.DataSource = tablaResultados;
                                gvReporte.DataBind();

                                lblTotalRegistros.Text = $"Total registros: {tablaResultados.Rows.Count}";
                            }
                            else
                            {
                                gvReporte.DataSource = null;
                                gvReporte.DataBind();
                                lblTotalRegistros.Text = "Total registros: 0";
                            }
                        }

                        // Process indicators (second result)
                        if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                        {
                            DataRow indicadoresRow = ds.Tables[1].Rows[0];

                            decimal totalIngresosSoles = Convert.ToDecimal(indicadoresRow["TotalIngresosSoles"]);
                            decimal totalIngresosDolares = Convert.ToDecimal(indicadoresRow["TotalIngresosDolares"]);
                            decimal totalEgresosSoles = Convert.ToDecimal(indicadoresRow["TotalEgresosSoles"]);
                            decimal totalEgresosDolares = Convert.ToDecimal(indicadoresRow["TotalEgresosDolares"]);
                            decimal balanceSoles = Convert.ToDecimal(indicadoresRow["BalanceSoles"]);
                            decimal balanceDolares = Convert.ToDecimal(indicadoresRow["BalanceDolares"]);

                            // Update interface with indicators
                            litTotalIngresos.Text = $"S/ {totalIngresosSoles:N2} | $ {totalIngresosDolares:N2}";
                            litTotalEgresos.Text = $"S/ {totalEgresosSoles:N2} | $ {totalEgresosDolares:N2}";

                            string colorClase = balanceSoles >= 0 ? "text-success" : "text-danger";
                            litBalance.Text = $"<span class='{colorClase}'>S/ {balanceSoles:N2} | $ {balanceDolares:N2}</span>";

                            litIndicadorAdicionalTitulo.Text = "Transacciones";
                            litIndicadorAdicional.Text = $"{indicadoresRow["TotalRegistros"]}";
                        }

                        // Update report title
                        string tituloReporte = "Balance Financiero General";
                        if (!string.IsNullOrEmpty(tipoTransaccion) && tipoTransaccion != "Todas")
                        {
                            tituloReporte = tipoTransaccion;
                        }

                        litTituloResultados.Text = $"{tituloReporte} ({fechaDesde:dd/MM/yyyy} - {fechaHasta:dd/MM/yyyy})";

                        // Show results modal
                        ScriptManager.RegisterStartupScript(this, GetType(), "ShowResultsModal",
                            "$('#modalResultados').modal('show');", true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Error handling with detailed message for debugging
                string errorMessage = $"Error al generar balance financiero: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" | Inner exception: {ex.InnerException.Message}";
                }
                System.Diagnostics.Debug.WriteLine(errorMessage);
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                    $"alert('{errorMessage.Replace("'", "\\'")}');", true);
            }
        }

        // Método auxiliar para mostrar mensajes de error
        private void MostrarMensaje(string mensaje)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert",
                $"alert('{mensaje.Replace("'", "\\'")}');", true);
        }


        private void ConfigurarGridViewFinanciero_BalanceGeneral(DataTable dt)
        {
            // Limpiar configuración previa
            gvReporte.Columns.Clear();

            // Configuración general del GridView
            gvReporte.AllowSorting = false;  // Desactivar ordenación para evitar enlaces en columnas
            gvReporte.AllowPaging = true;
            gvReporte.PageSize = 20;
            gvReporte.AutoGenerateColumns = false;
            gvReporte.CssClass = "table table-striped table-bordered table-hover";
            gvReporte.GridLines = GridLines.None;

            // Columna de Número de Orden
            BoundField colNroOrden = new BoundField();
            colNroOrden.DataField = "NroOrdenViaje";
            colNroOrden.HeaderText = "Nº Orden";
            colNroOrden.HeaderStyle.CssClass = "text-center";
            colNroOrden.ItemStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colNroOrden);

            // Columna de Número de Pedido
            BoundField colNroPedido = new BoundField();
            colNroPedido.DataField = "NumeroPedido";
            colNroPedido.HeaderText = "Nº Pedido";
            colNroPedido.HeaderStyle.CssClass = "text-center";
            colNroPedido.ItemStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colNroPedido);

            // Columna de Fecha
            BoundField colFecha = new BoundField();
            colFecha.DataField = "FechaTransaccion";
            colFecha.HeaderText = "Fecha";
            colFecha.DataFormatString = "{0:dd/MM/yyyy}";
            colFecha.HeaderStyle.CssClass = "text-center";
            colFecha.ItemStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colFecha);

            // Columna de Tipo de Transacción
            BoundField colTipoTransaccion = new BoundField();
            colTipoTransaccion.DataField = "TipoTransaccion";
            colTipoTransaccion.HeaderText = "Tipo";
            colTipoTransaccion.HeaderStyle.CssClass = "text-center";
            colTipoTransaccion.ItemStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colTipoTransaccion);

            // Columna de Concepto
            BoundField colConcepto = new BoundField();
            colConcepto.DataField = "Concepto";
            colConcepto.HeaderText = "Concepto";
            colConcepto.HeaderStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colConcepto);

            // Columna de Conductor
            BoundField colConductor = new BoundField();
            colConductor.DataField = "Conductor";
            colConductor.HeaderText = "Conductor";
            colConductor.HeaderStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colConductor);

            // Columna de Cliente
            BoundField colCliente = new BoundField();
            colCliente.DataField = "Cliente";
            colCliente.HeaderText = "Cliente";
            colCliente.HeaderStyle.CssClass = "text-center";
            gvReporte.Columns.Add(colCliente);

            // Columna de Ingreso en Soles
            BoundField colIngresoSoles = new BoundField();
            colIngresoSoles.DataField = "IngresoSoles";
            colIngresoSoles.HeaderText = "Ingreso S/";
            colIngresoSoles.DataFormatString = "{0:N2}";
            colIngresoSoles.HeaderStyle.CssClass = "text-center text-success";
            colIngresoSoles.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            colIngresoSoles.ItemStyle.CssClass = "text-success";
            gvReporte.Columns.Add(colIngresoSoles);

            // Columna de Ingreso en Dólares
            BoundField colIngresoDolares = new BoundField();
            colIngresoDolares.DataField = "IngresoDolares";
            colIngresoDolares.HeaderText = "Ingreso $";
            colIngresoDolares.DataFormatString = "{0:N2}";
            colIngresoDolares.HeaderStyle.CssClass = "text-center text-success";
            colIngresoDolares.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            colIngresoDolares.ItemStyle.CssClass = "text-success";
            gvReporte.Columns.Add(colIngresoDolares);

            // Columna de Egreso en Soles
            BoundField colEgresoSoles = new BoundField();
            colEgresoSoles.DataField = "EgresoSoles";
            colEgresoSoles.HeaderText = "Egreso S/";
            colEgresoSoles.DataFormatString = "{0:N2}";
            colEgresoSoles.HeaderStyle.CssClass = "text-center text-danger";
            colEgresoSoles.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            colEgresoSoles.ItemStyle.CssClass = "text-danger";
            gvReporte.Columns.Add(colEgresoSoles);

            // Columna de Egreso en Dólares
            BoundField colEgresoDolares = new BoundField();
            colEgresoDolares.DataField = "EgresoDolares";
            colEgresoDolares.HeaderText = "Egreso $";
            colEgresoDolares.DataFormatString = "{0:N2}";
            colEgresoDolares.HeaderStyle.CssClass = "text-center text-danger";
            colEgresoDolares.ItemStyle.HorizontalAlign = HorizontalAlign.Right;
            colEgresoDolares.ItemStyle.CssClass = "text-danger";
            gvReporte.Columns.Add(colEgresoDolares);

            // Columna de Observaciones
            BoundField colObservaciones = new BoundField();
            colObservaciones.DataField = "Observaciones";
            colObservaciones.HeaderText = "Observaciones";
            colObservaciones.HeaderStyle.CssClass = "text-center";
            colObservaciones.ItemStyle.CssClass = "text-wrap";
            gvReporte.Columns.Add(colObservaciones);

            // Registrar RowDataBound evento para aplicar formato condicional
            gvReporte.RowDataBound += GvReporte_RowDataBound;

            // Configuración de estilos para la paginación
            gvReporte.PagerStyle.CssClass = "pagination-ys";
        }

        // Método para manejar el evento RowDataBound
        protected void GvReporte_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Aplicar estilos condicionales según el tipo de transacción
                string tipoTransaccion = DataBinder.Eval(e.Row.DataItem, "TipoTransaccion")?.ToString();

                if (tipoTransaccion == "Ingreso")
                {
                    // Podríamos añadir más estilos o clases aquí
                    e.Row.Cells[3].CssClass = "bg-success-subtle"; // Celda del tipo de transacción
                }
                else if (tipoTransaccion == "Egreso")
                {
                    e.Row.Cells[3].CssClass = "bg-danger-subtle"; // Celda del tipo de transacción
                }

                // Destacar filas con valores significativos
                decimal ingresoSoles = Convert.ToDecimal(DataBinder.Eval(e.Row.DataItem, "IngresoSoles"));
                decimal ingresoDolares = Convert.ToDecimal(DataBinder.Eval(e.Row.DataItem, "IngresoDolares"));
                decimal egresoSoles = Convert.ToDecimal(DataBinder.Eval(e.Row.DataItem, "EgresoSoles"));
                decimal egresoDolares = Convert.ToDecimal(DataBinder.Eval(e.Row.DataItem, "EgresoDolares"));

                // Mostrar con negrita los valores altos
                if (ingresoSoles > 1000)
                {
                    e.Row.Cells[7].CssClass = "text-success fw-bold"; // Celda de Ingreso Soles
                }

                if (ingresoDolares > 300)
                {
                    e.Row.Cells[8].CssClass = "text-success fw-bold"; // Celda de Ingreso Dólares
                }

                if (egresoSoles > 1000)
                {
                    e.Row.Cells[9].CssClass = "text-danger fw-bold"; // Celda de Egreso Soles
                }

                if (egresoDolares > 300)
                {
                    e.Row.Cells[10].CssClass = "text-danger fw-bold"; // Celda de Egreso Dólares
                }
            }
        }



        protected void GenerarReporteRendimientoPorVehiculo()
        {
            try
            {
                // Obtener los valores de los filtros desde la interfaz
                DateTime fechaDesde = Convert.ToDateTime(txtFechaDesde.Text);
                DateTime fechaHasta = Convert.ToDateTime(txtFechaHasta.Text);

                // Valor del desplegable de lugar de abastecimiento
                int? idLugarAbastecimiento = null;
                if (ddlLugarAbastecimiento.SelectedValue != "" && ddlLugarAbastecimiento.SelectedValue != "0")
                {
                    idLugarAbastecimiento = Convert.ToInt32(ddlLugarAbastecimiento.SelectedValue);
                }

                // Titular el reporte
                litTituloResultados.Text = "Reporte de Rendimiento por Vehículo";

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ReporteRendimientoPorVehiculo", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Agregar los parámetros
                        cmd.Parameters.Add("@FechaDesde", SqlDbType.DateTime).Value = fechaDesde;
                        cmd.Parameters.Add("@FechaHasta", SqlDbType.DateTime).Value = fechaHasta;
                        cmd.Parameters.Add("@IdLugarAbastecimiento", SqlDbType.Int).Value =
                            (object)idLugarAbastecimiento ?? DBNull.Value;

                        // Abrir la conexión
                        con.Open();

                        // Crear conjuntos de datos para los resultados del procedimiento
                        DataSet ds = new DataSet();

                        // Configurar adaptador para llenar el DataSet
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            // Llenar el DataSet con los resultados
                            adapter.Fill(ds);
                        }

                        // Verificar si hay datos - El primer resultado contiene la verificación
                        bool hayDatos = false;
                        if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        {
                            hayDatos = Convert.ToBoolean(ds.Tables[0].Rows[0]["ExistenRegistros"]);
                        }

                        // Mostrar o configurar el panel de resultados según corresponda
                        pnlResultados.Visible = true;

                        if (hayDatos)
                        {
                            // Hay datos - Configurar los indicadores principales
                            if (ds.Tables.Count > 2 && ds.Tables[2].Rows.Count > 0)
                            {
                                DataRow statsRow = ds.Tables[2].Rows[0];

                                // Configurar los indicadores en el panel de resultados
                                litTotalIngresos.Text = string.Format("{0}", statsRow["totalVehiculos"]);
                                litIndicadorAdicionalTitulo.Text = "Total Combustible";
                                litIndicadorAdicional.Text = string.Format("{0:N2} Gal.", Convert.ToDecimal(statsRow["totalGalonesFlota"]));
                                litTotalEgresos.Text = string.Format("{0:N2} km", Convert.ToDecimal(statsRow["totalKilometrosFlota"]));
                                litBalance.Text = string.Format("{0:N2} km/gal", Convert.ToDecimal(statsRow["rendimientoPromedioFlota"]));
                            }

                            // Mostrar cantidad total de registros
                            if (ds.Tables.Count > 1)
                            {
                                lblTotalRegistros.Text = string.Format("Total registros: {0}", ds.Tables[1].Rows.Count);
                            }

                            // Configurar el GridView principal si hay datos en la tabla 1
                            if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                            {
                                gvReporte.DataSource = ds.Tables[1];
                                ConfigurarGridViewRendimientoPorVehiculo(gvReporte);
                                gvReporte.DataBind();
                            }
                            else
                            {
                                // No hay datos en la tabla principal
                                gvReporte.DataSource = null;
                                gvReporte.DataBind();
                            }

                            // NOTA: Se ha eliminado la sección que creaba la tabla de "Top 10 Mejores Rendimientos"
                        }
                        else
                        {
                            // No hay datos - Configurar mensaje y sugerencias
                            litTotalIngresos.Text = "0";
                            litIndicadorAdicional.Text = "0.00 Gal.";
                            litTotalEgresos.Text = "0.00 km";
                            litBalance.Text = "0.00 km/gal";

                            // Mensaje detallado
                            lblTotalRegistros.Text = string.Format(
                                "No se encontraron registros para el período {0} - {1}. Por favor, ajuste los filtros.",
                                fechaDesde.ToString("dd/MM/yyyy"),
                                fechaHasta.ToString("dd/MM/yyyy"));

                            // Sugerencias
                            Panel pnlSugerencias = new Panel();
                            pnlSugerencias.CssClass = "alert alert-info mt-3";
                            pnlSugerencias.Controls.Add(new LiteralControl(
                                "<h6><i class='fas fa-info-circle mr-2'></i>Sugerencias:</h6>" +
                                "<ul class='mb-0'>" +
                                "<li>Amplíe el rango de fechas</li>" +
                                "<li>Verifique que la fecha inicial no sea posterior a la fecha final</li>" +
                                "<li>Seleccione 'Todos los lugares' en el filtro de Lugar de Abastecimiento</li>" +
                                "</ul>"));

                            pnlResultados.Controls.Add(pnlSugerencias);

                            // GridView vacío
                            gvReporte.DataSource = null;
                            gvReporte.DataBind();
                        }

                        // Mostrar el modal con los resultados
                        ScriptManager.RegisterStartupScript(this, this.GetType(), "showResultModal",
                            "$('#modalResultados').modal('show');", true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar errores
                litTituloResultados.Text = "Error al Generar Reporte";
                lblTotalRegistros.Text = "Se produjo un error: " + ex.Message;

                // Configurar indicadores con mensaje de error
                litTotalIngresos.Text = "Error";
                litIndicadorAdicional.Text = "Error";
                litTotalEgresos.Text = "Error";
                litBalance.Text = "Error";

                // Panel de error con detalles
                Panel pnlError = new Panel();
                pnlError.CssClass = "alert alert-danger mt-3";
                pnlError.Controls.Add(new LiteralControl(
                    "<h6><i class='fas fa-exclamation-circle mr-2'></i>Detalles del error:</h6>" +
                    "<p>" + ex.Message + "</p>"));

                pnlResultados.Controls.Add(pnlError);
                pnlResultados.Visible = true;
                 
                // Registrar el error para debugging
                System.Diagnostics.Debug.WriteLine("Error en GenerarReporteRendimientoPorVehiculo: " + ex.ToString());

                // Mostrar el modal con el error
                ScriptManager.RegisterStartupScript(this, this.GetType(), "showResultModal",
                    "$('#modalResultados').modal('show');", true);
            }
        }

        private void ConfigurarGridViewRendimientoPorVehiculo(GridView gv)
        {
            // Configurar propiedades básicas
            gv.AutoGenerateColumns = false;
            gv.ShowHeaderWhenEmpty = true;
            gv.EmptyDataText = "No hay datos para mostrar.";

            // Limpiar columnas existentes
            gv.Columns.Clear();

            // Columna de Placa
            BoundField bfPlaca = new BoundField();
            bfPlaca.HeaderText = "Placa";
            bfPlaca.DataField = "placaTracto";
            gv.Columns.Add(bfPlaca);

            // Columna de Marca
            BoundField bfMarca = new BoundField();
            bfMarca.HeaderText = "Marca";
            bfMarca.DataField = "marca";
            gv.Columns.Add(bfMarca);

            // Columna de Modelo
            BoundField bfModelo = new BoundField();
            bfModelo.HeaderText = "Modelo";
            bfModelo.DataField = "modelo";
            gv.Columns.Add(bfModelo);

            // Columna de Cantidad de Abastecimientos
            BoundField bfCantidadAbastecimientos = new BoundField();
            bfCantidadAbastecimientos.HeaderText = "# Abast.";
            bfCantidadAbastecimientos.DataField = "cantidadAbastecimientos";
            gv.Columns.Add(bfCantidadAbastecimientos);

            // Columna de Galones Totales
            BoundField bfGalones = new BoundField();
            bfGalones.HeaderText = "Galones";
            bfGalones.DataField = "totalGalonesAbastecidos";
            bfGalones.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfGalones);

            // Columna de Kilómetros Recorridos
            BoundField bfKilometros = new BoundField();
            bfKilometros.HeaderText = "Kilómetros";
            bfKilometros.DataField = "totalKilometrosRecorridos";
            bfKilometros.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfKilometros);

            // Columna de Rendimiento Promedio
            BoundField bfRendimiento = new BoundField();
            bfRendimiento.HeaderText = "Rendimiento (km/gal)";
            bfRendimiento.DataField = "rendimientoPromedio";
            bfRendimiento.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfRendimiento);

            // Columna de Costo Total
            BoundField bfCosto = new BoundField();
            bfCosto.HeaderText = "Costo ($)";
            bfCosto.DataField = "costoTotalCombustible";
            bfCosto.DataFormatString = "${0:N2}";
            gv.Columns.Add(bfCosto);

            // Configuración básica de la tabla (mínima)
            gv.GridLines = GridLines.Both;
            gv.CellPadding = 3;
            gv.CellSpacing = 1;
        }



        protected void GenerarReporteRendimientoPorRutaCombustible()
        {
            try
            {
                // Obtener los valores de los filtros desde la interfaz
                DateTime fechaDesde = Convert.ToDateTime(txtFechaDesde.Text);
                DateTime fechaHasta = Convert.ToDateTime(txtFechaHasta.Text);

                // Valor del desplegable de lugar de abastecimiento
                int? idLugarAbastecimiento = null;
                if (ddlLugarAbastecimiento.SelectedValue != "" && ddlLugarAbastecimiento.SelectedValue != "0")
                {
                    idLugarAbastecimiento = Convert.ToInt32(ddlLugarAbastecimiento.SelectedValue);
                }

                // Titular el reporte
                litTituloResultados.Text = "Reporte de Rendimiento por Ruta";

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_ReporteRendimientoPorRutaCombustible", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        // Agregar los parámetros
                        cmd.Parameters.Add("@FechaDesde", SqlDbType.DateTime).Value = fechaDesde;
                        cmd.Parameters.Add("@FechaHasta", SqlDbType.DateTime).Value = fechaHasta;
                        cmd.Parameters.Add("@IdLugarAbastecimiento", SqlDbType.Int).Value =
                            (object)idLugarAbastecimiento ?? DBNull.Value;

                        // Abrir la conexión
                        con.Open();

                        // Crear conjuntos de datos para los resultados
                        DataSet ds = new DataSet();

                        // Configurar adaptador para llenar el DataSet
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            // Llenar el DataSet con los resultados
                            adapter.Fill(ds);
                        }

                        // Verificar si hay datos - El primer resultado contiene la verificación
                        bool hayDatos = false;
                        if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                        {
                            hayDatos = Convert.ToBoolean(ds.Tables[0].Rows[0]["ExistenRegistros"]);
                        }

                        // Mostrar o configurar el panel de resultados según corresponda
                        pnlResultados.Visible = true;

                        if (hayDatos)
                        {
                            // Hay datos - Configurar los indicadores principales
                            if (ds.Tables.Count > 2 && ds.Tables[2].Rows.Count > 0)
                            {
                                DataRow statsRow = ds.Tables[2].Rows[0];

                                // Configurar los indicadores en el panel de resultados
                                litTotalIngresos.Text = string.Format("{0}", statsRow["totalRutas"]);
                                litIndicadorAdicionalTitulo.Text = "Total Combustible";
                                litIndicadorAdicional.Text = string.Format("{0:N2} Gal.", Convert.ToDecimal(statsRow["totalGalonesFlota"]));
                                litTotalEgresos.Text = string.Format("{0:N2} km", Convert.ToDecimal(statsRow["totalKilometrosFlota"]));
                                litBalance.Text = string.Format("{0:N2} km/gal", Convert.ToDecimal(statsRow["rendimientoPromedioFlota"]));
                            }

                            // Mostrar cantidad total de registros
                            if (ds.Tables.Count > 1)
                            {
                                lblTotalRegistros.Text = string.Format("Total registros: {0}", ds.Tables[1].Rows.Count);
                            }

                            // Configurar el GridView principal si hay datos en la tabla 1
                            if (ds.Tables.Count > 1 && ds.Tables[1].Rows.Count > 0)
                            {
                                gvReporte.DataSource = ds.Tables[1];
                                ConfigurarGridViewRendimientoPorRutaCombustible(gvReporte);
                                gvReporte.DataBind();
                            }
                            else
                            {
                                // No hay datos en la tabla principal
                                gvReporte.DataSource = null;
                                gvReporte.DataBind();
                            }
                        }
                        else
                        {
                            // No hay datos - Configurar mensaje y sugerencias
                            litTotalIngresos.Text = "0";
                            litIndicadorAdicional.Text = "0.00 Gal.";
                            litTotalEgresos.Text = "0.00 km";
                            litBalance.Text = "0.00 km/gal";

                            // Mensaje detallado
                            lblTotalRegistros.Text = string.Format(
                                "No se encontraron registros para el período {0} - {1}. Por favor, ajuste los filtros.",
                                fechaDesde.ToString("dd/MM/yyyy"),
                                fechaHasta.ToString("dd/MM/yyyy"));

                            // Sugerencias
                            Panel pnlSugerencias = new Panel();
                            pnlSugerencias.CssClass = "alert alert-info mt-3";
                            pnlSugerencias.Controls.Add(new LiteralControl(
                                "<h6><i class='fas fa-info-circle mr-2'></i>Sugerencias:</h6>" +
                                "<ul class='mb-0'>" +
                                "<li>Verifique que existan rutas asignadas a los abastecimientos</li>" +
                                "<li>Amplíe el rango de fechas</li>" +
                                "<li>Seleccione 'Todos los lugares' en el filtro de Lugar de Abastecimiento</li>" +
                                "</ul>"));

                            pnlResultados.Controls.Add(pnlSugerencias);

                            // GridView vacío
                            gvReporte.DataSource = null;
                            gvReporte.DataBind();
                        }

                        // Mostrar el modal con los resultados
                        ScriptManager.RegisterStartupScript(this, this.GetType(), "showResultModal",
                            "$('#modalResultados').modal('show');", true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar errores
                litTituloResultados.Text = "Error al Generar Reporte";
                lblTotalRegistros.Text = "Se produjo un error: " + ex.Message;

                // Configurar indicadores con mensaje de error
                litTotalIngresos.Text = "Error";
                litIndicadorAdicional.Text = "Error";
                litTotalEgresos.Text = "Error";
                litBalance.Text = "Error";

                // Panel de error con detalles
                Panel pnlError = new Panel();
                pnlError.CssClass = "alert alert-danger mt-3";
                pnlError.Controls.Add(new LiteralControl(
                    "<h6><i class='fas fa-exclamation-circle mr-2'></i>Detalles del error:</h6>" +
                    "<p>" + ex.Message + "</p>"));

                pnlResultados.Controls.Add(pnlError);
                pnlResultados.Visible = true;

                // Registrar el error para debugging
                System.Diagnostics.Debug.WriteLine("Error en GenerarReporteRendimientoPorRutaCombustible: " + ex.ToString());

                // Mostrar el modal con el error
                ScriptManager.RegisterStartupScript(this, this.GetType(), "showResultModal",
                    "$('#modalResultados').modal('show');", true);
            }
        }

        private void ConfigurarGridViewRendimientoPorRutaCombustible(GridView gv)
        {
            // Configurar propiedades básicas
            gv.AutoGenerateColumns = false;
            gv.ShowHeaderWhenEmpty = true;
            gv.EmptyDataText = "No hay datos para mostrar.";

            // Limpiar columnas existentes
            gv.Columns.Clear();

            // Columna de Nombre de Ruta
            BoundField bfRuta = new BoundField();
            bfRuta.HeaderText = "Ruta";
            bfRuta.DataField = "nombreRuta";
            gv.Columns.Add(bfRuta);

            // Columna de Cantidad de Vehículos
            BoundField bfVehiculos = new BoundField();
            bfVehiculos.HeaderText = "# Vehículos";
            bfVehiculos.DataField = "cantidadVehiculos";
            gv.Columns.Add(bfVehiculos);

            // Columna de Cantidad de Conductores
            BoundField bfConductores = new BoundField();
            bfConductores.HeaderText = "# Conductores";
            bfConductores.DataField = "cantidadConductores";
            gv.Columns.Add(bfConductores);

            // Columna de Cantidad de Abastecimientos
            BoundField bfAbastecimientos = new BoundField();
            bfAbastecimientos.HeaderText = "# Abast.";
            bfAbastecimientos.DataField = "cantidadAbastecimientos";
            gv.Columns.Add(bfAbastecimientos);

            // Columna de Galones Totales
            BoundField bfGalones = new BoundField();
            bfGalones.HeaderText = "Galones";
            bfGalones.DataField = "totalGalonesAbastecidos";
            bfGalones.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfGalones);

            // Columna de Kilómetros Recorridos
            BoundField bfKilometros = new BoundField();
            bfKilometros.HeaderText = "Kilómetros";
            bfKilometros.DataField = "totalKilometrosRecorridos";
            bfKilometros.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfKilometros);

            // Columna de Rendimiento Promedio
            BoundField bfRendimiento = new BoundField();
            bfRendimiento.HeaderText = "Rendimiento (km/gal)";
            bfRendimiento.DataField = "rendimientoPromedio";
            bfRendimiento.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfRendimiento);

            // Columna de Rendimiento Mínimo
            BoundField bfRendMin = new BoundField();
            bfRendMin.HeaderText = "Rend. Mínimo";
            bfRendMin.DataField = "rendimientoMinimo";
            bfRendMin.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfRendMin);

            // Columna de Rendimiento Máximo
            BoundField bfRendMax = new BoundField();
            bfRendMax.HeaderText = "Rend. Máximo";
            bfRendMax.DataField = "rendimientoMaximo";
            bfRendMax.DataFormatString = "{0:N2}";
            gv.Columns.Add(bfRendMax);

            // Columna de Costo Total
            BoundField bfCosto = new BoundField();
            bfCosto.HeaderText = "Costo ($)";
            bfCosto.DataField = "costoTotalCombustible";
            bfCosto.DataFormatString = "${0:N2}";
            gv.Columns.Add(bfCosto);

            // Configuración básica de la tabla (mínima)
            gv.GridLines = GridLines.Both;
            gv.CellPadding = 3;
            gv.CellSpacing = 1;
        }



        #endregion
    }
}