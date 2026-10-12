using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using WebSGV.Helpers;
using WebSGV.Services.Reportes;

namespace WebSGV.Views
{
    public partial class ReportesOrdenesViaje : PaginaBase
    {
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
                DateTime hoy = FechaHelper.Ahora();
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
                DataTable dt = ReportesOrdenesViajeService.ObtenerConductoresActivos();
                foreach (DataRow dr in dt.Rows)
                    ddlPersConductor.Items.Add(new System.Web.UI.WebControls.ListItem(
                        dr["NombreCompleto"].ToString().Trim(),
                        dr["idConductor"].ToString()));
            }
            catch { /* silencioso: el combo sigue con "Todos" */ }
        }

        private void CargarClientesPersonalizado()
        {
            try
            {
                DataTable dt = ReportesOrdenesViajeService.ObtenerClientes();
                foreach (DataRow dr in dt.Rows)
                    ddlPersCliente.Items.Add(new System.Web.UI.WebControls.ListItem(
                        dr["nombre"].ToString(), dr["idCliente"].ToString()));
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
                MostrarMensaje($"Error al cargar liquidaciones: {MensajeErrorHelper.ParaUsuario(ex)}", "danger");
            }
        }

        private static DataTable ObtenerLiquidaciones(DateTime fechaDesde, DateTime fechaHasta)
        {
            DataTable dt = ReportesOrdenesViajeService.ObtenerLiquidaciones(fechaDesde, fechaHasta);

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
                MostrarMensaje($"Error al cargar viajes activos: {MensajeErrorHelper.ParaUsuario(ex)}", "danger");
            }
        }

        private static DataTable ObtenerViajesActivos(string buscarConductor, string estadoViaje) =>
            ReportesOrdenesViajeService.ObtenerViajesActivos(buscarConductor, estadoViaje);

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

        #region EXPORTACIÓN (Excel y PDF con el formato común de ReporteTabular)

        private void ExportarLiquidacionesExcel() => ExportarLiquidaciones(pdf: false);

        private void GenerarPDFLiquidaciones() => ExportarLiquidaciones(pdf: true);

        private void ExportarLiquidaciones(bool pdf)
        {
            try
            {
                DateTime fechaDesde;
                DateTime fechaHasta;
                decimal factorConversion;
                string mensajeValidacion;
                if (!ValidarFiltrosLiquidaciones(Request.QueryString["fechaDesde"], Request.QueryString["fechaHasta"],
                    Request.QueryString["factor"], out fechaDesde, out fechaHasta, out factorConversion, out mensajeValidacion))
                {
                    ResponderSolicitudInvalida(mensajeValidacion);
                    return;
                }

                var reporte = ReporteLiquidaciones(fechaDesde, fechaHasta, factorConversion, UsuarioActual());
                if (pdf) EnviarPdf(reporte, "Liquidaciones");
                else EnviarExcel(reporte, "Liquidaciones");
            }
            catch (System.Threading.ThreadAbortException) { /* normal: Response.End() */ }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al exportar el reporte de liquidaciones en ReportesOrdenesViaje");
                throw;
            }
        }

        private static ReporteTabular ReporteLiquidaciones(DateTime fechaDesde, DateTime fechaHasta, decimal factor, string usuario)
        {
            DataTable dt = ObtenerLiquidaciones(fechaDesde, fechaHasta);
            var r = new ReporteTabular
            {
                Titulo = "Reporte de liquidaciones: descuentos y reintegros",
                NombreHoja = "Liquidaciones",
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario,
                Nota = "Neto = reintegro − descuento: en verde cuando predomina el reintegro y en rojo cuando predomina el descuento. " +
                       "El total general convierte los dólares a soles con el factor indicado."
            };
            r.Filtros.Add($"Período: {fechaDesde:dd/MM/yyyy} al {fechaHasta:dd/MM/yyyy}");

            r.Columnas.Add(Columna("DNI", TipoColumnaReporte.Codigo, 0.9f, d => d["DNI"]));
            r.Columnas.Add(Columna("Conductor", TipoColumnaReporte.Texto, 2.4f, d => d["Conductor"]));
            r.Columnas.Add(Columna("Fecha de salida", TipoColumnaReporte.Fecha, 1f, d => d["FechaSalida"]));
            r.Columnas.Add(Columna("N.° de liquidación", TipoColumnaReporte.Codigo, 1.2f, d => d["NumeroLiquidacion"]));
            r.Columnas.Add(Columna("Descuento S/", TipoColumnaReporte.Soles, 1f, d => d["DescuentoSoles"]));
            r.Columnas.Add(Columna("Reintegro S/", TipoColumnaReporte.Soles, 1f, d => d["ReintegroSoles"]));
            r.Columnas.Add(Columna("Neto S/", TipoColumnaReporte.Soles, 1f, d => d["MontoSoles"], verdeSiPositivo: true));
            r.Columnas.Add(Columna("Descuento US$", TipoColumnaReporte.Dolares, 1f, d => d["DescuentoDolares"]));
            r.Columnas.Add(Columna("Reintegro US$", TipoColumnaReporte.Dolares, 1f, d => d["ReintegroDolares"]));
            r.Columnas.Add(Columna("Neto US$", TipoColumnaReporte.Dolares, 1f, d => d["MontoDolares"], verdeSiPositivo: true));

            decimal netoS = SumaColumna(dt, "MontoSoles");
            decimal netoD = SumaColumna(dt, "MontoDolares");
            r.Resumen.Add(Linea("Liquidaciones", dt.Rows.Count, TipoColumnaReporte.Entero));
            r.Resumen.Add(Linea("Neto en soles", netoS, TipoColumnaReporte.Soles));
            r.Resumen.Add(Linea("Neto en dólares", netoD, TipoColumnaReporte.Dolares));
            r.Resumen.Add(Linea("Factor de conversión (US$ a S/)", factor, TipoColumnaReporte.Texto));
            r.Resumen.Add(Linea("Neto en dólares convertido a soles", netoD * factor, TipoColumnaReporte.Soles));
            r.Resumen.Add(Linea("Total general en soles", netoS + netoD * factor, TipoColumnaReporte.Soles, destacada: true));
            return r;
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

                var reporte = ReporteViajesActivos(buscarConductor, estadoViaje, UsuarioActual());
                if (Request.QueryString["formato"] == "pdf") EnviarPdf(reporte, "ViajesActivos");
                else EnviarExcel(reporte, "ViajesActivos");
            }
            catch (System.Threading.ThreadAbortException) { /* normal: Response.End() */ }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al exportar el reporte de viajes activos en ReportesOrdenesViaje");
                throw;
            }
        }

        private const int DiasViajeAlerta = 7;

        private static ReporteTabular ReporteViajesActivos(string buscarConductor, string estadoViaje, string usuario)
        {
            DataTable dt = ObtenerViajesActivos(buscarConductor, estadoViaje);
            Func<DataRow, int> dias = d => d["DiasEnViaje"] == DBNull.Value ? 0 : Convert.ToInt32(d["DiasEnViaje"]);

            var r = new ReporteTabular
            {
                Titulo = "Reporte de viajes activos sin liquidación",
                NombreHoja = "Viajes activos",
                IncluirTotales = false,
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario,
                Nota = $"Días en viaje en rojo: más de {DiasViajeAlerta} días desde el inicio del viaje."
            };
            r.Filtros.Add("Estado: " + Legible(estadoViaje) +
                (string.IsNullOrWhiteSpace(buscarConductor) ? "" : "  ·  Conductor: " + buscarConductor.Trim()));

            r.Columnas.Add(Columna("DNI", TipoColumnaReporte.Codigo, 0.9f, d => d["DNI"]));
            r.Columnas.Add(Columna("Conductor", TipoColumnaReporte.Texto, 2.2f, d => d["Conductor"]));
            r.Columnas.Add(Columna("Tracto", TipoColumnaReporte.Codigo, 0.9f, d => d["PlacaTracto"]));
            r.Columnas.Add(Columna("Carreta", TipoColumnaReporte.Codigo, 0.9f, d => d["PlacaCarreta"]));
            r.Columnas.Add(Columna("Cliente", TipoColumnaReporte.Texto, 2f, d => d["Cliente"]));
            r.Columnas.Add(Columna("Destino", TipoColumnaReporte.Texto, 2f, d => d["Destino"]));
            r.Columnas.Add(Columna("Despachos", TipoColumnaReporte.Entero, 0.8f, d => d["CantidadDespachos"]));
            r.Columnas.Add(Columna("Fecha de programación", TipoColumnaReporte.Fecha, 1.1f, d => d["FechaProgramacion"]));
            var colDias = Columna("Días en viaje", TipoColumnaReporte.Entero, 0.8f, d => d["DiasEnViaje"]);
            colDias.Alerta = d => dias(d) > DiasViajeAlerta;
            r.Columnas.Add(colDias);
            r.Columnas.Add(Columna("Estado", TipoColumnaReporte.Codigo, 0.9f, d => Legible(Convert.ToString(d["Estado"]))));

            r.Resumen.Add(Linea("Viajes sin liquidación", dt.Rows.Count, TipoColumnaReporte.Entero));
            r.Resumen.Add(Linea("Despachos en esos viajes", SumaColumna(dt, "CantidadDespachos"), TipoColumnaReporte.Entero));
            r.Resumen.Add(Linea($"Viajes con más de {DiasViajeAlerta} días", r.Filas.Count(d => dias(d) > DiasViajeAlerta), TipoColumnaReporte.Entero));
            return r;
        }

        // ------------------------------------------------------------------
        // Piezas comunes de las exportaciones
        // ------------------------------------------------------------------

        private static ColumnaReporte Columna(string titulo, TipoColumnaReporte tipo, float ancho,
            Func<DataRow, object> valor, bool verdeSiPositivo = false) =>
            new ColumnaReporte { Titulo = titulo, Tipo = tipo, Ancho = ancho, Valor = valor, VerdeSiPositivo = verdeSiPositivo };

        private static LineaResumen Linea(string etiqueta, decimal valor, TipoColumnaReporte tipo, bool destacada = false) =>
            new LineaResumen { Etiqueta = etiqueta, Valor = valor, Tipo = tipo, Destacada = destacada };

        /// <summary>"COMPLETADO" → "Completado" (los estados se guardan en mayúsculas).</summary>
        private static string Legible(string valor) =>
            string.IsNullOrWhiteSpace(valor) ? "" : char.ToUpperInvariant(valor.Trim()[0]) + valor.Trim().Substring(1).ToLowerInvariant();

        private string UsuarioActual() => Convert.ToString(Session["Nombre"]);

        private void EnviarExcel(ReporteTabular reporte, string prefijo) =>
            EnviarArchivo(ReporteTabularExportador.GenerarExcel(reporte, EmpresaConfigHelper.ObtenerEmpresa(),
                    Server.MapPath("~/Content/favicon.png")),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{prefijo}_{FechaHelper.Ahora():yyyyMMdd_HHmm}.xlsx");

        private void EnviarPdf(ReporteTabular reporte, string prefijo) =>
            EnviarArchivo(ReporteTabularExportador.CrearPdf(reporte, EmpresaConfigHelper.ObtenerEmpresa(),
                    Server.MapPath("~/Content/favicon.png")).GeneratePdf(),
                "application/pdf", $"{prefijo}_{FechaHelper.Ahora():yyyyMMdd_HHmm}.pdf");

        private static void EnviarArchivo(byte[] contenido, string tipo, string nombreArchivo)
        {
            var resp = HttpContext.Current.Response;
            resp.Clear();
            resp.ClearContent();
            resp.ClearHeaders();
            resp.Buffer = true;
            resp.Charset = "";
            resp.ContentType = tipo;
            resp.AddHeader("Content-Disposition", "attachment; filename=" + nombreArchivo);
            resp.AddHeader("Content-Length", contenido.Length.ToString());
            resp.Cache.SetCacheability(HttpCacheability.NoCache);
            resp.BinaryWrite(contenido);
            resp.Flush();
            resp.End();
        }

        #endregion

        #region DETALLE DE ORDEN (AJAX)

        /// <summary>HTML-encode de un valor de BD para armar el HTML del detalle.</summary>
        private static string Enc(object valor) => HttpUtility.HtmlEncode(Convert.ToString(valor));

        [WebMethod(EnableSession = true)]
        public static string ObtenerDetalleOrden(int idOrden)
        {
            // Los WebMethods no pasan por Page_Load: validar sesión y rol aquí.
            if (!SecurityHelper.TieneSesionActiva() || !SecurityHelper.EsAdminOSupervisor())
                throw new UnauthorizedAccessException("Acceso no autorizado.");

            try
            {
                StringBuilder html = new StringBuilder();

                // SQL movido a ReportesOrdenesViajeService; el armado del HTML queda aquí.
                DataTable dtGeneral = ReportesOrdenesViajeService.ObtenerCabeceraDetalle(idOrden);

                if (dtGeneral.Rows.Count > 0)
                {
                    DataRow reader = dtGeneral.Rows[0];
                    string numeroOrden = reader["numeroOrdenViaje"].ToString();

                    html.Append("<div class='orden-detalle'>");

                    // Información general
                    html.Append("<div class='card mb-3'>");
                    html.Append("<div class='card-header bg-primary text-white'><h6 class='mb-0'>Información General</h6></div>");
                    html.Append("<div class='card-body'>");
                    html.Append("<div class='row'>");
                    html.Append($"<div class='col-md-3'><strong>N° Orden:</strong> {Enc(reader["numeroOrdenViaje"])}</div>");
                    html.Append($"<div class='col-md-3'><strong>Conductor:</strong> {Enc(reader["Conductor"])}</div>");
                    html.Append($"<div class='col-md-3'><strong>Tracto:</strong> {Enc(reader["PlacaTracto"])}</div>");
                    html.Append($"<div class='col-md-3'><strong>Carreta:</strong> {Enc(reader["PlacaCarreta"])}</div>");
                    html.Append("</div>");
                    html.Append("<div class='row mt-2'>");
                    html.Append($"<div class='col-md-3'><strong>Fecha Salida:</strong> {Convert.ToDateTime(reader["fechaSalida"]):dd/MM/yyyy}</div>");
                    html.Append($"<div class='col-md-3'><strong>Fecha Llegada:</strong> {Convert.ToDateTime(reader["fechaLlegada"]):dd/MM/yyyy}</div>");
                    html.Append($"<div class='col-md-3'><strong>Hora Salida:</strong> {Enc(reader["horaSalida"])}</div>");
                    html.Append($"<div class='col-md-3'><strong>Hora Llegada:</strong> {Enc(reader["horaLlegada"])}</div>");
                    html.Append("</div>");
                    html.Append("</div>");
                    html.Append("</div>");

                    // ✅ INGRESOS
                    html.Append(ObtenerSeccionIngresos(numeroOrden));

                    // ✅ GASTOS
                    html.Append(ObtenerSeccionGastos(numeroOrden));

                    // ✅ BALANCE FINAL
                    html.Append(ObtenerBalanceFinal(numeroOrden));

                    html.Append("</div>");
                }
                else
                {
                    html.Append("<div class='alert alert-warning'>No se encontró información de la orden</div>");
                }

                return html.ToString();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener el detalle de la orden {IdOrden}", idOrden);
                return "<div class='alert alert-danger'>Error al cargar el detalle de la orden.</div>";
            }
        }

        private static string ObtenerSeccionIngresos(string numeroOrden)
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

            // Ingresos principales (SQL movido a ReportesOrdenesViajeService)
            DataTable dtPrincipales = ReportesOrdenesViajeService.ObtenerIngresosPrincipales(numeroOrden);
            if (dtPrincipales.Rows.Count > 0)
            {
                DataRow reader = dtPrincipales.Rows[0];

                decimal despachoS = Convert.ToDecimal(reader["despachoSoles"]);
                decimal despachoD = Convert.ToDecimal(reader["despachoDolares"]);
                if (despachoS > 0 || despachoD > 0)
                {
                    html.Append($"<tr><td>Despacho</td><td>{Enc(reader["descDespacho"])}</td><td>S/ {despachoS:N2}</td><td>$ {despachoD:N2}</td></tr>");
                    totalIngresosSoles += despachoS;
                    totalIngresosDolares += despachoD;
                }

                decimal prestamoS = Convert.ToDecimal(reader["prestamoSoles"]);
                decimal prestamoD = Convert.ToDecimal(reader["prestamosDolares"]);
                if (prestamoS > 0 || prestamoD > 0)
                {
                    html.Append($"<tr><td>Préstamo</td><td>{Enc(reader["descPrestamo"])}</td><td>S/ {prestamoS:N2}</td><td>$ {prestamoD:N2}</td></tr>");
                    totalIngresosSoles += prestamoS;
                    totalIngresosDolares += prestamoD;
                }

                decimal mensualidadS = Convert.ToDecimal(reader["mensualidadSoles"]);
                decimal mensualidadD = Convert.ToDecimal(reader["mensualidadDolares"]);
                if (mensualidadS > 0 || mensualidadD > 0)
                {
                    html.Append($"<tr><td>Mensualidad</td><td>{Enc(reader["descMensualidad"])}</td><td>S/ {mensualidadS:N2}</td><td>$ {mensualidadD:N2}</td></tr>");
                    totalIngresosSoles += mensualidadS;
                    totalIngresosDolares += mensualidadD;
                }

                decimal otrosS = Convert.ToDecimal(reader["otrosSoles"]);
                decimal otrosD = Convert.ToDecimal(reader["otrosDolares"]);
                if (otrosS > 0 || otrosD > 0)
                {
                    html.Append($"<tr><td>Otros Autorizados</td><td>{Enc(reader["descOtrosAutorizados"])}</td><td>S/ {otrosS:N2}</td><td>$ {otrosD:N2}</td></tr>");
                    totalIngresosSoles += otrosS;
                    totalIngresosDolares += otrosD;
                }
            }

            // Ingresos adicionales
            DataTable dtAdicionales = ReportesOrdenesViajeService.ObtenerIngresosAdicionales(numeroOrden);
            foreach (DataRow reader in dtAdicionales.Rows)
            {
                decimal soles = Convert.ToDecimal(reader["soles"]);
                decimal dolares = Convert.ToDecimal(reader["dolares"]);
                html.Append($"<tr><td>{Enc(reader["nombreCategoria"])}</td><td>{Enc(reader["descripcion"])}</td><td>S/ {soles:N2}</td><td>$ {dolares:N2}</td></tr>");
                totalIngresosSoles += soles;
                totalIngresosDolares += dolares;
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

        private static string ObtenerSeccionGastos(string numeroOrden)
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

            // Gastos principales (SQL movido a ReportesOrdenesViajeService)
            DataTable dtPrincipales = ReportesOrdenesViajeService.ObtenerGastosPrincipales(numeroOrden);
            if (dtPrincipales.Rows.Count > 0)
            {
                DataRow reader = dtPrincipales.Rows[0];

                // Peajes
                decimal peajesS = Convert.ToDecimal(reader["peajesSoles"]);
                decimal peajesD = Convert.ToDecimal(reader["peajesDolares"]);
                if (peajesS > 0 || peajesD > 0)
                {
                    html.Append($"<tr><td>Peajes</td><td>{Enc(reader["descPeajes"])}</td><td>S/ {peajesS:N2}</td><td>$ {peajesD:N2}</td></tr>");
                    totalGastosSoles += peajesS;
                    totalGastosDolares += peajesD;
                }

                // Alimentación
                decimal alimentacionS = Convert.ToDecimal(reader["alimentacionSoles"]);
                decimal alimentacionD = Convert.ToDecimal(reader["alimentacionDolares"]);
                if (alimentacionS > 0 || alimentacionD > 0)
                {
                    html.Append($"<tr><td>Alimentación</td><td>{Enc(reader["descAlimentacion"])}</td><td>S/ {alimentacionS:N2}</td><td>$ {alimentacionD:N2}</td></tr>");
                    totalGastosSoles += alimentacionS;
                    totalGastosDolares += alimentacionD;
                }

                // Apoyo Seguridad
                decimal apoyoS = Convert.ToDecimal(reader["apoyoseguridadSoles"]);
                decimal apoyoD = Convert.ToDecimal(reader["apoyoseguridadDolares"]);
                if (apoyoS > 0 || apoyoD > 0)
                {
                    html.Append($"<tr><td>Apoyo Seguridad</td><td>{Enc(reader["descApoyoSeguridad"])}</td><td>S/ {apoyoS:N2}</td><td>$ {apoyoD:N2}</td></tr>");
                    totalGastosSoles += apoyoS;
                    totalGastosDolares += apoyoD;
                }

                // Reparaciones
                decimal reparacionesS = Convert.ToDecimal(reader["reparacionesVariosSoles"]);
                decimal reparacionesD = Convert.ToDecimal(reader["repacionesVariosDolares"]);
                if (reparacionesS > 0 || reparacionesD > 0)
                {
                    html.Append($"<tr><td>Reparaciones</td><td>{Enc(reader["descReparacionesVarios"])}</td><td>S/ {reparacionesS:N2}</td><td>$ {reparacionesD:N2}</td></tr>");
                    totalGastosSoles += reparacionesS;
                    totalGastosDolares += reparacionesD;
                }

                // Movilidad
                decimal movilidadS = Convert.ToDecimal(reader["movilidadSoles"]);
                decimal movilidadD = Convert.ToDecimal(reader["movilidadDolares"]);
                if (movilidadS > 0 || movilidadD > 0)
                {
                    html.Append($"<tr><td>Movilidad</td><td>{Enc(reader["descMovilidad"])}</td><td>S/ {movilidadS:N2}</td><td>$ {movilidadD:N2}</td></tr>");
                    totalGastosSoles += movilidadS;
                    totalGastosDolares += movilidadD;
                }

                // Hospedaje
                decimal hospedajeS = Convert.ToDecimal(reader["hospedajeSoles"]);
                decimal hospedajeD = Convert.ToDecimal(reader["hospedajeDolares"]);
                if (hospedajeS > 0 || hospedajeD > 0)
                {
                    html.Append($"<tr><td>Hospedaje</td><td>{Enc(reader["descHospedaje"])}</td><td>S/ {hospedajeS:N2}</td><td>$ {hospedajeD:N2}</td></tr>");
                    totalGastosSoles += hospedajeS;
                    totalGastosDolares += hospedajeD;
                }

                // Combustible
                decimal combustibleS = Convert.ToDecimal(reader["combustibleSoles"]);
                decimal combustibleD = Convert.ToDecimal(reader["combustibleDolares"]);
                if (combustibleS > 0 || combustibleD > 0)
                {
                    html.Append($"<tr><td>Combustible</td><td>{Enc(reader["descCombustible"])}</td><td>S/ {combustibleS:N2}</td><td>$ {combustibleD:N2}</td></tr>");
                    totalGastosSoles += combustibleS;
                    totalGastosDolares += combustibleD;
                }

                // Encarpada
                decimal encapadaS = Convert.ToDecimal(reader["encarpada_desencarpadaSoles"]);
                decimal encapadaD = Convert.ToDecimal(reader["encarpada_desencarpadaDolares"]);
                if (encapadaS > 0 || encapadaD > 0)
                {
                    html.Append($"<tr><td>Encarpada/Desencarpada</td><td>{Enc(reader["descEncarpadaDesencarpada"])}</td><td>S/ {encapadaS:N2}</td><td>$ {encapadaD:N2}</td></tr>");
                    totalGastosSoles += encapadaS;
                    totalGastosDolares += encapadaD;
                }
            }

            // Gastos adicionales (Categorías Adicionales)
            DataTable dtAdicionales = ReportesOrdenesViajeService.ObtenerGastosAdicionales(numeroOrden);
            foreach (DataRow reader in dtAdicionales.Rows)
            {
                decimal soles = Convert.ToDecimal(reader["soles"]);
                decimal dolares = Convert.ToDecimal(reader["dolares"]);
                html.Append($"<tr><td>{Enc(reader["nombreCategoria"])}</td><td>{Enc(reader["descripcion"])}</td><td>S/ {soles:N2}</td><td>$ {dolares:N2}</td></tr>");
                totalGastosSoles += soles;
                totalGastosDolares += dolares;
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

        private static string ObtenerBalanceFinal(string numeroOrden)
        {
            StringBuilder html = new StringBuilder();

            // Calcular balance final
            decimal balanceSoles = 0;
            decimal balanceDolares = 0;

            // Obtener todos los valores (SQL movido a ReportesOrdenesViajeService)
            DataTable dtBalance = ReportesOrdenesViajeService.ObtenerBalanceDetalle(numeroOrden);
            if (dtBalance.Rows.Count > 0)
            {
                DataRow reader = dtBalance.Rows[0];

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
            Tuple.Create("fechaSalida",      "Fecha de salida",     "date"),
            Tuple.Create("fechaLlegada",     "Fecha de llegada",    "date"),
            Tuple.Create("horaSalida",       "Hora de salida",      "time"),
            Tuple.Create("horaLlegada",      "Hora de llegada",     "time"),
            Tuple.Create("tracto",           "Tracto",              "text"),
            Tuple.Create("carreta",          "Carreta",             "text"),
            Tuple.Create("cliente",          "Cliente",             "text"),
            Tuple.Create("destino",          "Destino",             "text"),
            Tuple.Create("cantDespachos",    "Despachos",           "text"),
            Tuple.Create("numero",           "N.° de liquidación",  "text"),
            Tuple.Create("estado",           "Estado",              "text"),
            Tuple.Create("ingresosSoles",    "Ingresos S/",         "money-sol"),
            Tuple.Create("ingresosDolares",  "Ingresos US$",          "money-dol"),
            Tuple.Create("gastosSoles",      "Gastos S/",           "money-sol"),
            Tuple.Create("gastosDolares",    "Gastos US$",            "money-dol"),
            Tuple.Create("descuentoSoles",   "Descuento S/",        "money-sol"),
            Tuple.Create("descuentoDolares", "Descuento US$",         "money-dol"),
            Tuple.Create("reintegroSoles",   "Reintegro S/",        "money-sol"),
            Tuple.Create("reintegroDolares", "Reintegro US$",         "money-dol"),
            Tuple.Create("balanceSoles",     "Balance neto S/",     "money-sol"),
            Tuple.Create("balanceDolares",   "Balance neto US$",    "money-dol"),
            // === INGRESOS DETALLADOS ===
            Tuple.Create("iDespachoS",       "Ing. despacho S/",    "money-sol"),
            Tuple.Create("iDespachoD",       "Ing. despacho US$",     "money-dol"),
            Tuple.Create("iPrestamoS",       "Ing. préstamo S/",    "money-sol"),
            Tuple.Create("iPrestamoD",       "Ing. préstamo US$",     "money-dol"),
            Tuple.Create("iMensualidadS",    "Ing. mensualidad S/", "money-sol"),
            Tuple.Create("iMensualidadD",    "Ing. mensualidad US$",  "money-dol"),
            Tuple.Create("iOtrosS",          "Ing. otros S/",       "money-sol"),
            Tuple.Create("iOtrosD",          "Ing. otros US$",        "money-dol"),
            Tuple.Create("iAdicionalesS",    "Ing. adicionales S/", "money-sol"),
            Tuple.Create("iAdicionalesD",    "Ing. adicionales US$",  "money-dol"),
            Tuple.Create("iAdicionalesDet",  "Detalle de ingresos adicionales", "text"),
            // === GASTOS DETALLADOS ===
            Tuple.Create("gPeajesS",         "Peajes S/",           "money-sol"),
            Tuple.Create("gPeajesD",         "Peajes US$",            "money-dol"),
            Tuple.Create("gAlimentacionS",   "Alimentación S/",     "money-sol"),
            Tuple.Create("gAlimentacionD",   "Alimentación US$",      "money-dol"),
            Tuple.Create("gApoyoSeguridadS", "Apoyo y seguridad S/",  "money-sol"),
            Tuple.Create("gApoyoSeguridadD", "Apoyo y seguridad US$",   "money-dol"),
            Tuple.Create("gReparacionesS",   "Reparaciones S/",     "money-sol"),
            Tuple.Create("gReparacionesD",   "Reparaciones US$",      "money-dol"),
            Tuple.Create("gMovilidadS",      "Movilidad S/",        "money-sol"),
            Tuple.Create("gMovilidadD",      "Movilidad US$",         "money-dol"),
            Tuple.Create("gHospedajeS",      "Hospedaje S/",        "money-sol"),
            Tuple.Create("gHospedajeD",      "Hospedaje US$",         "money-dol"),
            Tuple.Create("gCombustibleS",    "Combustible S/",      "money-sol"),
            Tuple.Create("gCombustibleD",    "Combustible US$",       "money-dol"),
            Tuple.Create("gEncarpadaS",      "Encarpada S/","money-sol"),
            Tuple.Create("gEncarpadaD",      "Encarpada US$", "money-dol"),
            Tuple.Create("gAdicionalesS",    "Otros gastos S/",     "money-sol"),
            Tuple.Create("gAdicionalesD",    "Otros gastos US$",      "money-dol"),
            Tuple.Create("gAdicionalesDet",  "Detalle de otros gastos", "text"),
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

        private static DataTable ObtenerReportePersonalizado(FiltrosPersonalizado f)
        {
            // SQL (incl. construcción dinámica de filtros/orden) movido al servicio.
            DataTable dt = ReportesOrdenesViajeService.ObtenerReportePersonalizado(
                f.FechaDesde, f.FechaHasta, f.Estado, f.IdConductor, f.IdCliente,
                f.PlacaTracto, f.Categoria, f.Orden);

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
                case "cantDespachos": return "CantidadDespachos";
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
                LogSGV.Error(ex, "Error al generar el reporte personalizado en ReportesOrdenesViaje");
                throw;
            }
        }

        private void ExportarPersonalizadoExcel(DataTable dt,
            List<Tuple<string, string, string>> cols, FiltrosPersonalizado f) =>
            EnviarExcel(ReportePersonalizado(dt, UsuarioActual(), cols, f), "ReportePersonalizado");

        private void ExportarPersonalizadoPDF(DataTable dt,
            List<Tuple<string, string, string>> cols, FiltrosPersonalizado f) =>
            EnviarPdf(ReportePersonalizado(dt, UsuarioActual(), cols, f), "ReportePersonalizado");

        private static readonly HashSet<string> ColumnasCodigo =
            new HashSet<string> { "dni", "numero", "estado", "tracto", "carreta" };
        private static readonly HashSet<string> ColumnasTextoLargo =
            new HashSet<string> { "observaciones", "iAdicionalesDet", "gAdicionalesDet" };

        private static ReporteTabular ReportePersonalizado(DataTable dt, string usuario,
            List<Tuple<string, string, string>> cols, FiltrosPersonalizado f)
        {
            var r = new ReporteTabular
            {
                Titulo = f.Titulo,
                NombreHoja = "Reporte personalizado",
                IncluirTotales = f.IncluirTotales,
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                GeneradoPor = usuario
            };
            r.Filtros.Add($"Período: {f.FechaDesde:dd/MM/yyyy} al {f.FechaHasta:dd/MM/yyyy}  ·  Estado: {Legible(f.Estado)}");

            var otros = new List<string>();
            if (f.IdConductor > 0) otros.Add("Conductor: " + NombreDeCatalogo(ReportesOrdenesViajeService.ObtenerConductoresActivos(), "idConductor", "NombreCompleto", f.IdConductor));
            if (f.IdCliente > 0) otros.Add("Cliente: " + NombreDeCatalogo(ReportesOrdenesViajeService.ObtenerClientes(), "idCliente", "nombre", f.IdCliente));
            if (!string.IsNullOrEmpty(f.PlacaTracto)) otros.Add("Tracto: " + f.PlacaTracto.ToUpperInvariant());
            if (!string.IsNullOrEmpty(f.Categoria)) otros.Add("Categoría de gasto: " + f.Categoria);
            if (otros.Count > 0) r.Filtros.Add(string.Join("  ·  ", otros));

            foreach (var c in cols)
            {
                string clave = ColumnaDataKey(c.Item1);
                TipoColumnaReporte tipo =
                    c.Item3 == "date" ? TipoColumnaReporte.Fecha :
                    c.Item3 == "time" ? TipoColumnaReporte.Hora :
                    c.Item3 == "money-sol" ? TipoColumnaReporte.Soles :
                    c.Item3 == "money-dol" ? TipoColumnaReporte.Dolares :
                    c.Item1 == "cantDespachos" ? TipoColumnaReporte.Entero :
                    ColumnasCodigo.Contains(c.Item1) ? TipoColumnaReporte.Codigo : TipoColumnaReporte.Texto;

                r.Columnas.Add(new ColumnaReporte
                {
                    Titulo = c.Item2,
                    Tipo = tipo,
                    Ancho = c.Item1 == "numero" ? 1.4f
                          : tipo != TipoColumnaReporte.Texto ? 1f
                          : ColumnasTextoLargo.Contains(c.Item1) ? 2.6f : 1.8f,
                    Valor = d =>
                    {
                        object v = clave != null && d.Table.Columns.Contains(clave) ? d[clave] : null;
                        return c.Item1 == "estado" && v != null && v != DBNull.Value ? Legible(v.ToString()) : v;
                    },
                    VerdeSiPositivo = c.Item1.StartsWith("balance") || c.Item1.StartsWith("reintegro")
                });
            }

            if (f.IncluirResumen)
            {
                decimal balS = SumaColumna(dt, "BalanceSoles");
                decimal balD = SumaColumna(dt, "BalanceDolares");
                r.TituloResumen = "Resumen ejecutivo";
                r.Resumen.Add(Linea("Liquidaciones", dt.Rows.Count, TipoColumnaReporte.Entero));
                r.Resumen.Add(Linea("Ingresos", SumaColumna(dt, "IngresosSoles"), TipoColumnaReporte.Soles));
                r.Resumen.Add(Linea("Gastos", SumaColumna(dt, "GastosSoles"), TipoColumnaReporte.Soles));
                r.Resumen.Add(Linea("Descuentos", SumaColumna(dt, "DescuentoSoles"), TipoColumnaReporte.Soles));
                r.Resumen.Add(Linea("Reintegros", SumaColumna(dt, "ReintegroSoles"), TipoColumnaReporte.Soles));
                r.Resumen.Add(Linea("Balance neto en soles", balS, TipoColumnaReporte.Soles));
                r.Resumen.Add(Linea("Ingresos", SumaColumna(dt, "IngresosDolares"), TipoColumnaReporte.Dolares));
                r.Resumen.Add(Linea("Gastos", SumaColumna(dt, "GastosDolares"), TipoColumnaReporte.Dolares));
                r.Resumen.Add(Linea("Descuentos", SumaColumna(dt, "DescuentoDolares"), TipoColumnaReporte.Dolares));
                r.Resumen.Add(Linea("Reintegros", SumaColumna(dt, "ReintegroDolares"), TipoColumnaReporte.Dolares));
                r.Resumen.Add(Linea("Balance neto en dólares", balD, TipoColumnaReporte.Dolares));
                r.Resumen.Add(Linea("Factor de conversión (US$ a S/)", f.Factor, TipoColumnaReporte.Texto));
                r.Resumen.Add(Linea("Balance general en soles", balS + balD * f.Factor, TipoColumnaReporte.Soles, destacada: true));
                r.Nota = "Balance neto = ingresos − gastos − descuentos + reintegros. El balance general convierte los dólares con el factor indicado.";
            }

            return r;
        }

        private static string NombreDeCatalogo(DataTable dt, string colId, string colNombre, int id)
        {
            var fila = dt.Rows.Cast<DataRow>().FirstOrDefault(d => Convert.ToInt32(d[colId]) == id);
            return fila == null ? "#" + id : Regex.Replace(fila[colNombre].ToString().Trim(), @"\s+", " ");
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
            DateTime max = FechaHelper.Hoy().AddDays(1);
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
