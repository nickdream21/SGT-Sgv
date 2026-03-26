using System.Web.UI;
using System.Web.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardConductor : System.Web.UI.Page
    {
        #region Clases de Datos

        public class GastoFinanciero
        {
            [JsonProperty("categoria")]
            public string Categoria { get; set; }

            [JsonProperty("id")]
            public int Id { get; set; }

            [JsonProperty("estacion")]
            public string Estacion { get; set; }

            [JsonProperty("lugar")]
            public string Lugar { get; set; }

            [JsonProperty("tipo")]
            public string Tipo { get; set; }

            [JsonProperty("fecha")]
            public string FechaString { get; set; }

            public DateTime Fecha
            {
                get
                {
                    if (DateTime.TryParse(FechaString, out DateTime fecha))
                        return fecha;
                    return DateTime.Now;
                }
            }

            [JsonProperty("comprobante")]
            public string Comprobante { get; set; }

            [JsonProperty("soles")]
            public decimal Soles { get; set; }

            [JsonProperty("dolares")]
            public decimal Dolares { get; set; }

            [JsonProperty("observaciones")]
            public string Observaciones { get; set; }
        }

        public class IngresoAdicionalData
        {
            [JsonProperty("categoria")]
            public string Categoria { get; set; }

            [JsonProperty("nombreCategoria")]
            public string NombreCategoria { get; set; }

            [JsonProperty("descripcion")]
            public string Descripcion { get; set; }

            [JsonProperty("soles")]
            public decimal? Soles { get; set; }

            [JsonProperty("dolares")]
            public decimal? Dolares { get; set; }
        }

        public class GastoAdicionalData
        {
            [JsonProperty("categoria")]
            public string Categoria { get; set; }

            [JsonProperty("nombreCategoria")]
            public string NombreCategoria { get; set; }

            [JsonProperty("descripcion")]
            public string Descripcion { get; set; }

            [JsonProperty("soles")]
            public decimal? Soles { get; set; }

            [JsonProperty("dolares")]
            public decimal? Dolares { get; set; }
        }

        public class ViajeActivo
        {
            public int IdViajeProgreso { get; set; }
            public string NumeroViajeProgreso { get; set; }
            public int IdConductor { get; set; }
            public string NombreConductor { get; set; }
            public DateTime FechaInicio { get; set; }
            public DateTime FechaUltimaActividad { get; set; }
            public string EstadoViaje { get; set; }
            public string DescripcionViaje { get; set; }
            public int CantidadDespachos { get; set; }
            public bool EsInternacional { get; set; }
        }

        public class DespachoInfo
        {
            public int IdDespacho { get; set; }
            public string NumeroDespacho { get; set; }
            public DateTime FechaDespacho { get; set; }
            public string Cliente { get; set; }
            public string TipoOperacion { get; set; }
            public string LugarOperacion { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string EstadoDespacho { get; set; }
            public string GuiaRemitente { get; set; }
            public string GuiaTransportista { get; set; }
            public string NumeroCPIC { get; set; }
        }

        public class EstacionPeaje
        {
            [JsonProperty("nombre")]
            public string Nombre { get; set; }
        }

        public class DatosViajeParaLiquidacion
        {
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
        }

        #endregion

        #region Variables Globales

        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        private int IdConductorActual
        {
            get
            {
                if (Session["IdConductor"] != null)
                {
                    return Convert.ToInt32(Session["IdConductor"]);
                }
                return 0;
            }
        }

        private int IdUsuarioActual
        {
            get
            {
                if (Session["IdUsuario"] != null)
                {
                    return Convert.ToInt32(Session["IdUsuario"]);
                }
                return 0;
            }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                VerificarSesion();
                InicializarDashboard();
            }
            else
            {
                // En PostBack, verificar que la sesión siga activa
                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
            }
        }

        #endregion

        #region Métodos de Inicialización

        private void VerificarSesion()
        {
            try
            {
                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    System.Diagnostics.Debug.WriteLine("❌ No hay sesión de conductor");
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"✅ Sesión válida: IdConductor={IdConductorActual}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error verificando sesión: {ex.Message}");
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
            }
        }

        private void InicializarDashboard()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIALIZANDO DASHBOARD CONDUCTOR ===");

                CargarDatosConductor();
                List<ViajeActivo> viajesActivos = ObtenerViajesActivos();

                if (viajesActivos.Count > 0)
                {
                    ViajeActivo viajePrimario = viajesActivos[0]; // el más antiguo (ASC)
                    List<int> idsViajes = viajesActivos.Select(v => v.IdViajeProgreso).ToList();
                    int totalDespachos = viajesActivos.Sum(v => v.CantidadDespachos);

                    System.Diagnostics.Debug.WriteLine($"✅ {viajesActivos.Count} viaje(s) activo(s) encontrado(s): {string.Join(", ", viajesActivos.Select(v => v.NumeroViajeProgreso))}");
                    MostrarViajesActivos(viajesActivos, totalDespachos);
                    CargarDespachosViajesActivos(idsViajes);
                    HabilitarLiquidacion(viajesActivos, totalDespachos);

                    // ✅ VERIFICAR SI HAY OBSERVACIONES DE RECHAZO
                    VerificarObservacionesRechazo(idsViajes);

                    // ✅ PRE-CARGAR DATOS DE ORDEN RECHAZADA SI EXISTE
                    CargarDatosOrdenRechazada(idsViajes);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay viajes activos");
                    MostrarSinViajes();
                }

                CargarHistorialLiquidaciones();

                System.Diagnostics.Debug.WriteLine("✅ Dashboard inicializado correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error inicializando dashboard: {ex.Message}");
                MostrarMensaje($"Error al cargar el dashboard: {ex.Message}", "danger");
            }
        }

        private void CargarDatosConductor()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDatosConductor", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                lblNombreConductor.Text = reader["nombreCompleto"].ToString();
                                lblDNIConductor.Text = reader["DNI"].ToString();
                                hfIdConductor.Value = IdConductorActual.ToString();

                                System.Diagnostics.Debug.WriteLine($"✅ Conductor cargado: {lblNombreConductor.Text}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando datos del conductor: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Verificación de Observaciones de Rechazo

        private void VerificarObservacionesRechazo(List<int> idsViajes)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Verificando observaciones de rechazo para {idsViajes.Count} viaje(s)");

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_VerificarObservacionesRechazo", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string numeroOrden = reader["numeroOrdenViaje"].ToString();
                                string observaciones = reader["observacionesRechazo"].ToString();
                                DateTime fechaRechazo = reader["fechaRechazo"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["fechaRechazo"])
                                    : DateTime.Now;
                                string rechazadoPor = reader["rechazadoPor"]?.ToString() ?? "Administrador";

                                System.Diagnostics.Debug.WriteLine($"⚠️ LIQUIDACIÓN RECHAZADA ENCONTRADA: {numeroOrden}");

                                MostrarAlertaRechazo(numeroOrden, observaciones, fechaRechazo, rechazadoPor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error verificando observaciones: {ex.Message}");
            }
        }

        private void MostrarAlertaRechazo(string numeroOrden, string observaciones, DateTime fechaRechazo, string rechazadoPor)
        {
            // ✅ Codificar todas las variables para prevenir XSS
            string numeroOrdenSafe = System.Web.HttpUtility.HtmlEncode(numeroOrden);
            string observacionesSafe = System.Web.HttpUtility.HtmlEncode(observaciones);
            string rechazadoPorSafe = System.Web.HttpUtility.HtmlEncode(rechazadoPor);

            string mensajeHtml = $@"
                <div class='alert alert-warning alert-rechazo' role='alert'>
                    <div class='d-flex align-items-start'>
                        <div class='alert-icon'>
                            <i class='fas fa-exclamation-triangle fa-2x'></i>
                        </div>
                        <div class='alert-content ml-3' style='flex: 1;'>
                            <h5 class='alert-heading mb-2'>
                                <i class='fas fa-undo-alt mr-2'></i>
                                Liquidación Rechazada - Orden {numeroOrdenSafe}
                            </h5>
                            <p class='mb-2'>
                                <strong>Tu liquidación fue rechazada el {fechaRechazo:dd/MM/yyyy HH:mm} por {rechazadoPorSafe}.</strong>
                            </p>
                            <div class='rechazo-motivo'>
                                <label class='font-weight-bold text-danger'>Motivo del rechazo:</label>
                                <p class='mb-0'>{observacionesSafe}</p>
                            </div>
                            <hr class='my-2' />
                            <p class='mb-0 text-muted'>
                                <i class='fas fa-info-circle mr-1'></i>
                                Por favor, corrige los datos según las observaciones y vuelve a liquidar el viaje.
                            </p>
                        </div>
                    </div>
                </div>";

            // Mostrar mensaje en el panel
            lblMensaje.Text = mensajeHtml;
            lblMensaje.CssClass = ""; // Sin clase adicional
            pnlMensajes.Visible = true;

            // Script para hacer scroll automático
            string script = @"
                $(document).ready(function() {
                    $('#liquidarViaje-tab').tab('show');
                    $('html, body').animate({
                        scrollTop: $('.alert-rechazo').offset().top - 100
                    }, 500);
                });
            ";
            System.Web.UI.ScriptManager.RegisterStartupScript(this, GetType(), "ScrollToRechazo", script, true);
        }

        #endregion

        #region Pre-cargar Datos de Orden Rechazada

        private decimal DecimalOrZero(SqlDataReader r, string column)
        {
            try { return r[column] != DBNull.Value ? Convert.ToDecimal(r[column]) : 0m; }
            catch { return 0m; }
        }

        private string StringOrEmpty(SqlDataReader r, string column)
        {
            try { return r[column] != DBNull.Value ? r[column].ToString() : ""; }
            catch { return ""; }
        }

        private void CargarDatosOrdenRechazada(List<int> idsViajes)
        {
            try
            {
                string connStr = ConnectionString;
                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    conn.Open();

                    // 1. Find the REABIERTO order for these trips
                    string ordenNumero = null;
                    DateTime fechaSal = DateTime.MinValue, fechaLleg = DateTime.MinValue;
                    string horaSal = "", horaLleg = "", obs = "";

                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerOrdenRechazada", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            if (!r.Read()) return;
                            ordenNumero = r["numeroOrdenViaje"].ToString();
                            fechaSal = r["fechaSalida"] != DBNull.Value ? Convert.ToDateTime(r["fechaSalida"]) : DateTime.MinValue;
                            fechaLleg = r["fechaLlegada"] != DBNull.Value ? Convert.ToDateTime(r["fechaLlegada"]) : DateTime.MinValue;
                            horaSal = r["horaSalida"]?.ToString() ?? "";
                            horaLleg = r["horaLlegada"]?.ToString() ?? "";
                            obs = r["observaciones"]?.ToString() ?? "";
                        }
                    }

                    if (string.IsNullOrEmpty(ordenNumero)) return;

                    System.Diagnostics.Debug.WriteLine($"✅ Orden rechazada encontrada: {ordenNumero}");

                    // 2. Store existing order number and populate TextBox controls
                    hfNumeroOrdenExistente.Value = ordenNumero;
                    if (fechaSal != DateTime.MinValue) txtFechaSalida.Text = fechaSal.ToString("yyyy-MM-dd");
                    if (fechaLleg != DateTime.MinValue) txtFechaLlegada.Text = fechaLleg.ToString("yyyy-MM-dd");
                    txtHoraSalida.Text = horaSal;
                    txtHoraLlegada.Text = horaLleg;
                    txtObservaciones.Text = obs;

                    // 3-11. Read all financial data via single multi-resultset SP
                    decimal despachoS = 0, despachoD = 0, prestamoS = 0, prestamoD = 0;
                    decimal mensualS = 0, mensualD = 0, otrosS = 0, otrosD = 0;
                    string descDespacho = "", descPrestamo = "", descMensual = "", descOtros = "";
                    decimal alimentS = 0, alimentD = 0, apoyoS = 0, apoyoD = 0;
                    decimal movilS = 0, movilD = 0, encapS = 0, encapD = 0;
                    string descAliment = "", descApoyo = "", descMovil = "", descEncap = "";
                    var peajesList = new List<object>();
                    var repList = new List<object>();
                    var hospList = new List<object>();
                    var combList = new List<object>();
                    var ingAdList = new List<object>();
                    var gastAdList = new List<object>();
                    decimal descS = 0, descD = 0, reintS = 0, reintD = 0;

                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDatosFinancierosOrden", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", ordenNumero);
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            // ResultSet 1: Ingresos
                            if (r.Read())
                            {
                                despachoS = DecimalOrZero(r, "despachoSoles");
                                despachoD = DecimalOrZero(r, "despachoDolares");
                                prestamoS = DecimalOrZero(r, "prestamoSoles");
                                prestamoD = DecimalOrZero(r, "prestamosDolares");
                                mensualS = DecimalOrZero(r, "mensualidadSoles");
                                mensualD = DecimalOrZero(r, "mensualidadDolares");
                                otrosS = DecimalOrZero(r, "otrosSoles");
                                otrosD = DecimalOrZero(r, "otrosDolares");
                                descDespacho = StringOrEmpty(r, "descDespacho");
                                descPrestamo = StringOrEmpty(r, "descPrestamo");
                                descMensual = StringOrEmpty(r, "descMensualidad");
                                descOtros = StringOrEmpty(r, "descOtrosAutorizados");
                            }

                            // ResultSet 2: Egresos
                            r.NextResult();
                            if (r.Read())
                            {
                                alimentS = DecimalOrZero(r, "alimentacionSoles");
                                alimentD = DecimalOrZero(r, "alimentacionDolares");
                                apoyoS = DecimalOrZero(r, "apoyoseguridadSoles");
                                apoyoD = DecimalOrZero(r, "apoyoseguridadDolares");
                                movilS = DecimalOrZero(r, "movilidadSoles");
                                movilD = DecimalOrZero(r, "movilidadDolares");
                                encapS = DecimalOrZero(r, "encarpada_desencarpadaSoles");
                                encapD = DecimalOrZero(r, "encarpada_desencarpadaDolares");
                                descAliment = StringOrEmpty(r, "descAlimentacion");
                                descApoyo = StringOrEmpty(r, "descApoyoSeguridad");
                                descMovil = StringOrEmpty(r, "descMovilidad");
                                descEncap = StringOrEmpty(r, "descEncarpadaDesencarpada");
                            }

                            // ResultSet 3: DetallePeajes
                            r.NextResult();
                            int idx = 1;
                            while (r.Read())
                            {
                                peajesList.Add(new
                                {
                                    id = idx++,
                                    estacion = StringOrEmpty(r, "estacion"),
                                    fecha = r["fecha"] != DBNull.Value ? Convert.ToDateTime(r["fecha"]).ToString("yyyy-MM-dd") : "",
                                    comprobante = StringOrEmpty(r, "numeroComprobante"),
                                    soles = DecimalOrZero(r, "montoSoles"),
                                    dolares = DecimalOrZero(r, "montoDolares"),
                                    observaciones = StringOrEmpty(r, "observaciones")
                                });
                            }

                            // ResultSet 4: DetalleReparacionesVarios
                            r.NextResult();
                            idx = 1;
                            while (r.Read())
                            {
                                repList.Add(new
                                {
                                    id = idx++,
                                    tipo = StringOrEmpty(r, "observaciones"),
                                    fecha = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]).ToString("yyyy-MM-dd") : "",
                                    comprobante = StringOrEmpty(r, "numeroComprobante"),
                                    soles = DecimalOrZero(r, "montoSoles"),
                                    dolares = DecimalOrZero(r, "montoDolares"),
                                    observaciones = StringOrEmpty(r, "observaciones")
                                });
                            }

                            // ResultSet 5: DetalleHospedaje
                            r.NextResult();
                            idx = 1;
                            while (r.Read())
                            {
                                hospList.Add(new
                                {
                                    id = idx++,
                                    lugar = StringOrEmpty(r, "observaciones"),
                                    fecha = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]).ToString("yyyy-MM-dd") : "",
                                    comprobante = StringOrEmpty(r, "numeroComprobante"),
                                    soles = DecimalOrZero(r, "montoSoles"),
                                    dolares = DecimalOrZero(r, "montoDolares"),
                                    observaciones = StringOrEmpty(r, "observaciones")
                                });
                            }

                            // ResultSet 6: DetalleCombustible
                            r.NextResult();
                            idx = 1;
                            while (r.Read())
                            {
                                combList.Add(new
                                {
                                    id = idx++,
                                    lugar = StringOrEmpty(r, "observaciones"),
                                    fecha = r["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(r["fechaComprobante"]).ToString("yyyy-MM-dd") : "",
                                    comprobante = StringOrEmpty(r, "numeroComprobante"),
                                    soles = DecimalOrZero(r, "montoSoles"),
                                    dolares = DecimalOrZero(r, "montoDolares"),
                                    observaciones = StringOrEmpty(r, "observaciones")
                                });
                            }

                            // ResultSet 7: IngresosAdicionales
                            r.NextResult();
                            while (r.Read())
                            {
                                ingAdList.Add(new
                                {
                                    categoria = StringOrEmpty(r, "nombreCategoria"),
                                    descripcion = StringOrEmpty(r, "descripcion"),
                                    soles = DecimalOrZero(r, "soles"),
                                    dolares = DecimalOrZero(r, "dolares")
                                });
                            }

                            // ResultSet 8: CategoriasAdicionales
                            r.NextResult();
                            while (r.Read())
                            {
                                gastAdList.Add(new
                                {
                                    categoria = StringOrEmpty(r, "nombreCategoria"),
                                    descripcion = StringOrEmpty(r, "descripcion"),
                                    soles = DecimalOrZero(r, "soles"),
                                    dolares = DecimalOrZero(r, "dolares")
                                });
                            }

                            // ResultSet 9: DescuentosReintegros
                            r.NextResult();
                            if (r.Read())
                            {
                                descS = DecimalOrZero(r, "descuentoSoles");
                                descD = DecimalOrZero(r, "descuentoDolares");
                                reintS = DecimalOrZero(r, "reintegroSoles");
                                reintD = DecimalOrZero(r, "reintegroDolares");
                            }
                        }
                    }

                    // 12. Build and emit startup JS to restore all form state
                    string peajesJson = JsonConvert.SerializeObject(peajesList);
                    string repJson = JsonConvert.SerializeObject(repList);
                    string hospJson = JsonConvert.SerializeObject(hospList);
                    string combJson = JsonConvert.SerializeObject(combList);
                    string ingAdJson = JsonConvert.SerializeObject(ingAdList);
                    string gastAdJson = JsonConvert.SerializeObject(gastAdList);

                    var ic = System.Globalization.CultureInfo.InvariantCulture;
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("$(function() {");

                    // Restore modal detail arrays
                    sb.AppendLine($"  peajesData = {peajesJson};");
                    sb.AppendLine("  contadorPeajes = peajesData.length;");
                    sb.AppendLine("  actualizarTablaPeajes(); actualizarTotalesPeajes();");

                    sb.AppendLine($"  reparacionesData = {repJson};");
                    sb.AppendLine("  contadorReparaciones = reparacionesData.length;");
                    sb.AppendLine("  actualizarTablaReparaciones(); actualizarTotalesReparaciones();");

                    sb.AppendLine($"  hospedajesData = {hospJson};");
                    sb.AppendLine("  contadorHospedajes = hospedajesData.length;");
                    sb.AppendLine("  actualizarTablaHospedajes(); actualizarTotalesHospedajes();");

                    sb.AppendLine($"  combustiblesData = {combJson};");
                    sb.AppendLine("  contadorCombustibles = combustiblesData.length;");
                    sb.AppendLine("  actualizarTablaCombustibles(); actualizarTotalesCombustibles();");

                    // Restore fixed income fields
                    sb.AppendLine($"  $('input[name=\"despachoSoles\"]').val('{despachoS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"despachoDolares\"]').val('{despachoD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descDespacho\"]').val({JsonConvert.SerializeObject(descDespacho)});");
                    sb.AppendLine($"  $('input[name=\"prestamoSoles\"]').val('{prestamoS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"prestamoDolares\"]').val('{prestamoD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descPrestamo\"]').val({JsonConvert.SerializeObject(descPrestamo)});");
                    sb.AppendLine($"  $('input[name=\"mensualidadSoles\"]').val('{mensualS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"mensualidadDolares\"]').val('{mensualD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descMensualidad\"]').val({JsonConvert.SerializeObject(descMensual)});");
                    sb.AppendLine($"  $('input[name=\"otrosSoles\"]').val('{otrosS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"otrosDolares\"]').val('{otrosD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descOtros\"]').val({JsonConvert.SerializeObject(descOtros)});");

                    // Restore fixed expense fields (manual, non-modal)
                    sb.AppendLine($"  $('input[name=\"alimentacionSoles\"]').val('{alimentS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"alimentacionDolares\"]').val('{alimentD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descAlimentacion\"]').val({JsonConvert.SerializeObject(descAliment)});");
                    sb.AppendLine($"  $('input[name=\"apoyoSeguridadSoles\"]').val('{apoyoS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"apoyoSeguridadDolares\"]').val('{apoyoD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descApoyoSeguridad\"]').val({JsonConvert.SerializeObject(descApoyo)});");
                    sb.AppendLine($"  $('input[name=\"movilidadSoles\"]').val('{movilS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"movilidadDolares\"]').val('{movilD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descMovilidad\"]').val({JsonConvert.SerializeObject(descMovil)});");
                    sb.AppendLine($"  $('input[name=\"encapadaSoles\"]').val('{encapS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"encapadaDolares\"]').val('{encapD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descEncapada\"]').val({JsonConvert.SerializeObject(descEncap)});");

                    // Restore descuentos/reintegros if fields exist
                    sb.AppendLine($"  $('input[name=\"descuentoSoles\"]').val('{descS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"descuentoDolares\"]').val('{descD.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"reintegroSoles\"]').val('{reintS.ToString("F2", ic)}');");
                    sb.AppendLine($"  $('input[name=\"reintegroDolares\"]').val('{reintD.ToString("F2", ic)}');");

                    // Restore additional income rows
                    if (ingAdList.Count > 0)
                    {
                        sb.AppendLine($"  (function() {{ var items = {ingAdJson};");
                        sb.AppendLine("    items.forEach(function(item, i) {");
                        sb.AppendLine("      agregarIngreso();");
                        sb.AppendLine("      var idx = contadorIngresosAdicionales;");
                        sb.AppendLine("      $('input[name=\"conceptoIngreso_' + idx + '\"]').val(item.categoria);");
                        sb.AppendLine("      $('input[name=\"descIngreso_' + idx + '\"]').val(item.descripcion);");
                        sb.AppendLine("      if (item.soles > 0) $('input[name=\"ingresoSoles_' + idx + '\"]').val(item.soles.toFixed(2));");
                        sb.AppendLine("      if (item.dolares > 0) $('input[name=\"ingresoDolares_' + idx + '\"]').val(item.dolares.toFixed(2));");
                        sb.AppendLine("    });");
                        sb.AppendLine("  })();");
                    }

                    // Restore additional expense rows
                    if (gastAdList.Count > 0)
                    {
                        sb.AppendLine($"  (function() {{ var items = {gastAdJson};");
                        sb.AppendLine("    items.forEach(function(item, i) {");
                        sb.AppendLine("      agregarGasto();");
                        sb.AppendLine("      var idx = contadorGastosAdicionales;");
                        sb.AppendLine("      $('input[name=\"conceptoGasto_' + idx + '\"]').val(item.categoria);");
                        sb.AppendLine("      $('input[name=\"descGasto_' + idx + '\"]').val(item.descripcion);");
                        sb.AppendLine("      if (item.soles > 0) $('input[name=\"gastoSoles_' + idx + '\"]').val(item.soles.toFixed(2));");
                        sb.AppendLine("      if (item.dolares > 0) $('input[name=\"gastoDolares_' + idx + '\"]').val(item.dolares.toFixed(2));");
                        sb.AppendLine("    });");
                        sb.AppendLine("  })();");
                    }

                    sb.AppendLine("  calcularTotales();");
                    sb.AppendLine("});");

                    ScriptManager.RegisterStartupScript(this, GetType(), "RestaurarOrdenRechazada", sb.ToString(), true);
                    System.Diagnostics.Debug.WriteLine($"✅ Formulario pre-cargado con datos de orden rechazada: {ordenNumero}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando orden rechazada: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Viaje Activo

        private List<ViajeActivo> ObtenerViajesActivos()
        {
            List<ViajeActivo> viajes = new List<ViajeActivo>();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerViajesActivosConductor", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                viajes.Add(new ViajeActivo
                                {
                                    IdViajeProgreso = Convert.ToInt32(reader["idViajeProgreso"]),
                                    NumeroViajeProgreso = reader["numeroViajeProgreso"].ToString(),
                                    IdConductor = Convert.ToInt32(reader["idConductor"]),
                                    NombreConductor = reader["nombreConductor"].ToString(),
                                    FechaInicio = Convert.ToDateTime(reader["fechaInicio"]),
                                    FechaUltimaActividad = Convert.ToDateTime(reader["fechaUltimaActividad"]),
                                    EstadoViaje = reader["estadoViaje"].ToString(),
                                    DescripcionViaje = reader["descripcionViaje"]?.ToString() ?? "",
                                    CantidadDespachos = Convert.ToInt32(reader["cantidadDespachos"]),
                                    EsInternacional = reader["esInternacional"] != DBNull.Value && Convert.ToBoolean(reader["esInternacional"])
                                });
                            }
                        }
                    }
                }

                return viajes;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo viajes activos: {ex.Message}");
                return viajes;
            }
        }

        private void MostrarViajesActivos(List<ViajeActivo> viajes, int totalDespachos)
        {
            try
            {
                ViajeActivo viajePrimario = viajes[0];
                List<int> ids = viajes.Select(v => v.IdViajeProgreso).ToList();

                pnlViajeActivo.Visible = true;
                pnlSinViajes.Visible = false;
                pnlDespachos.Visible = true;

                string numerosViaje = string.Join(", ", viajes.Select(v => v.NumeroViajeProgreso));
                lblNumeroViaje.Text = numerosViaje;
                lblFechaInicio.Text = viajePrimario.FechaInicio.ToString("dd/MM/yyyy HH:mm");
                lblCantidadDespachos.Text = totalDespachos.ToString();

                TimeSpan diasRuta = DateTime.Now - viajePrimario.FechaInicio;
                lblDiasRuta.Text = $"{diasRuta.Days} días";

                lblEstadoViaje.Text = $"Viaje Activo - {totalDespachos} despacho(s)";
                pnlEstadoViaje.CssClass = "badge-status badge badge-success";

                hfIdViajeActivo.Value = viajePrimario.IdViajeProgreso.ToString();
                hfIdsViajesActivos.Value = string.Join(",", ids);

                pnlBadgeLiquidar.Visible = true;
                pnlBadgeActivos.Visible = true;
                lblCantidadActivos.Text = totalDespachos.ToString();

                System.Diagnostics.Debug.WriteLine($"✅ {viajes.Count} viaje(s) activo(s) mostrado(s): {numerosViaje}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error mostrando viajes activos: {ex.Message}");
                throw;
            }
        }

        private void MostrarSinViajes()
        {
            pnlViajeActivo.Visible = false;
            pnlSinViajes.Visible = true;
            pnlDespachos.Visible = false;

            lblEstadoViaje.Text = "Sin viajes activos";
            pnlEstadoViaje.CssClass = "badge-status badge badge-secondary";

            pnlBadgeLiquidar.Visible = false;
            pnlBadgeActivos.Visible = false;
        }

        private void CargarDespachosViajesActivos(List<int> idsViajes)
        {
            try
            {
                List<DespachoInfo> despachos = new List<DespachoInfo>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDespachosViajesActivos", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                despachos.Add(new DespachoInfo
                                {
                                    IdDespacho = Convert.ToInt32(reader["idDespacho"]),
                                    NumeroDespacho = reader["numeroDespacho"].ToString(),
                                    FechaDespacho = Convert.ToDateTime(reader["fechaDespacho"]),
                                    Cliente = reader["nombreCliente"].ToString(),
                                    TipoOperacion = reader["tipoOperacion"].ToString(),
                                    LugarOperacion = reader["lugarOperacion"].ToString(),
                                    PlacaTracto = reader["placaTracto"].ToString(),
                                    PlacaCarreta = reader["placaCarreta"].ToString(),
                                    EstadoDespacho = reader["estadoDespacho"].ToString(),
                                    GuiaRemitente = reader["guiaRemitente"].ToString(),
                                    GuiaTransportista = reader["guiaTransportista"].ToString(),
                                    NumeroCPIC = reader["numeroCPIC"].ToString()
                                });
                            }
                        }
                    }
                }

                gvDespachosActivos.DataSource = despachos;
                gvDespachosActivos.DataBind();

                System.Diagnostics.Debug.WriteLine($"✅ {despachos.Count} despachos cargados de {idsViajes.Count} viaje(s)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando despachos: {ex.Message}");
                throw;
            }
        }

        private void HabilitarLiquidacion(List<ViajeActivo> viajes, int totalDespachos)
        {
            try
            {
                ViajeActivo viajePrimario = viajes[0];

                pnlSinViajeParaLiquidar.Visible = false;
                pnlFormularioLiquidacion.Visible = true;

                string numerosViaje = string.Join(", ", viajes.Select(v => v.NumeroViajeProgreso));
                lblNumeroViajeResumen.Text = numerosViaje;
                lblFechaInicioResumen.Text = viajePrimario.FechaInicio.ToString("dd/MM/yyyy");
                lblDespachosResumen.Text = totalDespachos.ToString();

                DateTime hoy = DateTime.Today;
                txtFechaSalida.Text = viajePrimario.FechaInicio.ToString("yyyy-MM-dd");
                txtFechaLlegada.Text = hoy.ToString("yyyy-MM-dd");
                txtHoraSalida.Text = "08:00";
                txtHoraLlegada.Text = "18:00";

                System.Diagnostics.Debug.WriteLine("✅ Formulario de liquidación habilitado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error habilitando liquidación: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Evento Principal - Enviar Liquidación

        protected void btnEnviarLiquidacion_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIANDO ENVÍO DE LIQUIDACIÓN (CONDUCTOR) ===");

                // Verificar sesión activa antes de proceder
                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    MostrarMensaje("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", "warning");
                    return;
                }

                int idViajeProgreso = int.TryParse(hfIdViajeActivo.Value, out int ivp) ? ivp : 0;
                System.Diagnostics.Debug.WriteLine($"idViajeProgreso del HiddenField: {idViajeProgreso} (raw: '{hfIdViajeActivo.Value}')");

                if (idViajeProgreso == 0)
                {
                    MostrarMensaje("No se encontró un viaje activo para liquidar. Recarga la página e intenta nuevamente.", "warning");
                    return;
                }

                // Obtener todos los IDs de viajes activos para cerrarlos todos
                List<int> idsViajesActivos = new List<int>();
                string idsStr = hfIdsViajesActivos.Value;
                if (!string.IsNullOrEmpty(idsStr))
                {
                    foreach (string s in idsStr.Split(','))
                    {
                        if (int.TryParse(s.Trim(), out int id) && id > 0)
                            idsViajesActivos.Add(id);
                    }
                }
                if (idsViajesActivos.Count == 0)
                    idsViajesActivos.Add(idViajeProgreso);
                System.Diagnostics.Debug.WriteLine($"Viajes a cerrar: {string.Join(", ", idsViajesActivos)}");

                // ✅ Detectar si es una re-liquidación de orden rechazada
                bool esReliquidacion = !string.IsNullOrEmpty(hfNumeroOrdenExistente.Value);
                string numeroOrdenViaje = esReliquidacion
                    ? hfNumeroOrdenExistente.Value
                    : GenerarNumeroOrden();

                System.Diagnostics.Debug.WriteLine(esReliquidacion
                    ? $"♻️ Re-liquidación sobre orden existente: {numeroOrdenViaje}"
                    : $"Número de orden generado: {numeroOrdenViaje}");

                DateTime fechaSalida = DateTime.TryParse(txtFechaSalida.Text, out DateTime fs) ? fs : DateTime.MinValue;
                DateTime fechaLlegada = DateTime.TryParse(txtFechaLlegada.Text, out DateTime fl) ? fl : DateTime.MinValue;
                string horaSalida = txtHoraSalida.Text;
                string horaLlegada = txtHoraLlegada.Text;
                string observaciones = txtObservaciones.Text;

                string errores = ValidarDatosGenerales(fechaSalida, fechaLlegada, horaSalida, horaLlegada);
                if (!string.IsNullOrEmpty(errores))
                {
                    MostrarMensaje(errores.Replace("\n", "<br/>"), "danger");
                    return;
                }

                DatosViajeParaLiquidacion datosViaje = ObtenerDatosViajeParaLiquidacion(idViajeProgreso);
                if (datosViaje == null)
                {
                    MostrarMensaje("Error al obtener los datos del viaje. Verifica que el viaje tenga despachos activos y recarga la página.", "danger");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Datos del viaje: Conductor={datosViaje.IdConductor}, Tracto={datosViaje.IdTracto}, Carreta={datosViaje.IdCarreta}");

                System.Diagnostics.Debug.WriteLine("Iniciando transacción de base de datos...");
                bool transaccionExitosa = false;

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();
                    System.Diagnostics.Debug.WriteLine("Conexión abierta");

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("Transacción iniciada");

                            if (esReliquidacion)
                            {
                                System.Diagnostics.Debug.WriteLine("Eliminando datos financieros anteriores de la orden rechazada...");
                                EliminarDatosFinancierosOrdenExistente(conn, transaction, numeroOrdenViaje);

                                System.Diagnostics.Debug.WriteLine("Actualizando orden de viaje existente...");
                                ActualizarOrdenViajeConductor(conn, transaction, numeroOrdenViaje,
                                    fechaSalida, fechaLlegada, horaSalida, horaLlegada, observaciones);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("Insertando orden de viaje con estado PENDIENTE...");
                                int idOrdenViaje = InsertarOrdenViajeConductor(
                                    conn, transaction, numeroOrdenViaje,
                                    fechaSalida, fechaLlegada, horaSalida, horaLlegada,
                                    datosViaje.IdConductor, datosViaje.IdTracto, datosViaje.IdCarreta,
                                    observaciones, idViajeProgreso
                                );
                                System.Diagnostics.Debug.WriteLine($"Orden de viaje insertada: {idOrdenViaje}");
                            }

                            System.Diagnostics.Debug.WriteLine("Insertando datos financieros...");
                            InsertarDatosFinancierosCompletos(conn, transaction, numeroOrdenViaje);

                            System.Diagnostics.Debug.WriteLine("Insertando descuentos y reintegros...");
                            InsertarDescuentosReintegros(conn, transaction, numeroOrdenViaje);

                            System.Diagnostics.Debug.WriteLine("Procesando datos detallados de modales...");
                            var gastosFinancieros = ObtenerGastosFinancierosDeSession();
                            if (gastosFinancieros.Count > 0)
                            {
                                InsertarGastosFinancierosDetallados(conn, transaction, numeroOrdenViaje, gastosFinancieros);
                            }

                            System.Diagnostics.Debug.WriteLine($"⚡ Cerrando {idsViajesActivos.Count} viaje(s) en progreso: {string.Join(", ", idsViajesActivos)}");
                            CerrarViajesProgreso(conn, transaction, idsViajesActivos, numeroOrdenViaje);

                            System.Diagnostics.Debug.WriteLine("Haciendo commit de toda la transacción...");
                            transaction.Commit();
                            transaccionExitosa = true;
                            System.Diagnostics.Debug.WriteLine("✅ Commit exitoso");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ ERROR EN TRANSACCIÓN: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");

                            try
                            {
                                transaction.Rollback();
                                System.Diagnostics.Debug.WriteLine("⚠️ Rollback ejecutado");
                            }
                            catch (Exception rollbackEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ Error en Rollback (ignorado): {rollbackEx.Message}");
                            }

                            MostrarMensaje($"Error al enviar la liquidación: {ex.Message}", "danger");
                        }
                    }
                }

                // Post-commit: mostrar resultado y programar redirect (fuera del using de la transacción)
                if (transaccionExitosa)
                {
                    AuditoriaHelper.Registrar("LIQUIDAR", "OrdenViaje", numeroOrdenViaje,
                        $"Conductor envió liquidación - Orden: {numeroOrdenViaje}, Viajes: {string.Join(", ", idsViajesActivos)}");

                    MostrarResultadoExitoso(numeroOrdenViaje);

                    string redirectScript = $@"
                        setTimeout(function() {{
                            window.location.href = '{ResolveUrl(Request.RawUrl)}';
                        }}, 2000);";
                    ScriptManager.RegisterStartupScript(this, GetType(), "RedirectDespuesExito", redirectScript, true);

                    System.Diagnostics.Debug.WriteLine("=== LIQUIDACIÓN ENVIADA EXITOSAMENTE ===");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR GENERAL: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                MostrarMensaje($"Error general: {ex.Message}", "danger");
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

        private void EliminarDatosFinancierosOrdenExistente(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_EliminarDatosFinancierosOrden", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine($"✅ Datos financieros eliminados para re-liquidación: {numeroOrdenViaje}");
        }

        private void ActualizarOrdenViajeConductor(
            SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje,
            DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada,
            string observaciones)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_ActualizarOrdenViajeConductor", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(fechaSalida));
                cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(fechaLlegada));
                cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);
                cmd.Parameters.AddWithValue("@idUsuarioRegistro", IdUsuarioActual > 0 ? (object)IdUsuarioActual : DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            System.Diagnostics.Debug.WriteLine($"✅ OrdenViaje actualizada para re-liquidación: {numeroOrdenViaje}");
        }

        private int InsertarOrdenViajeConductor(
            SqlConnection conn,
            SqlTransaction transaction,
            string numeroOrdenViaje,
            DateTime fechaSalida,
            DateTime fechaLlegada,
            string horaSalida,
            string horaLlegada,
            int idConductor,
            int idTracto,
            int idCarreta,
            string observaciones,
            int idViajeProgreso
        )
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarOrdenViajeConductor", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(fechaSalida));
                    cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                    cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(fechaLlegada));
                    cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                    cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    cmd.Parameters.AddWithValue("@idTracto", idTracto);
                    cmd.Parameters.AddWithValue("@idCarreta", idCarreta);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    cmd.Parameters.AddWithValue("@idUsuarioRegistro", IdUsuarioActual > 0 ? (object)IdUsuarioActual : DBNull.Value);

                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        int idOrdenViaje = Convert.ToInt32(result);
                        System.Diagnostics.Debug.WriteLine($"✅ OrdenViaje creada por CONDUCTOR: ID={idOrdenViaje}");
                        return idOrdenViaje;
                    }
                    else
                    {
                        throw new Exception("No se pudo insertar la orden de viaje");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error insertando orden: {ex.Message}");
                throw;
            }
        }

        private void InsertarDatosFinancierosCompletos(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            try
            {
                InsertarIngresosPrincipales(conn, transaction, numeroOrdenViaje);
                InsertarGastosPrincipales(conn, transaction, numeroOrdenViaje);
                InsertarIngresosAdicionales(conn, transaction, numeroOrdenViaje);
                InsertarGastosAdicionales(conn, transaction, numeroOrdenViaje);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR en datos financieros: {ex.Message}");
                throw;
            }
        }

        private void InsertarIngresosPrincipales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            decimal despachoSoles = decimal.TryParse(Request.Form["despachoSoles"], out decimal ds) ? ds : 0;
            decimal despachoDolares = decimal.TryParse(Request.Form["despachoDolares"], out decimal dd) ? dd : 0;
            decimal prestamoSoles = decimal.TryParse(Request.Form["prestamoSoles"], out decimal ps) ? ps : 0;
            decimal prestamoDolares = decimal.TryParse(Request.Form["prestamoDolares"], out decimal pd) ? pd : 0;
            decimal mensualidadSoles = decimal.TryParse(Request.Form["mensualidadSoles"], out decimal ms) ? ms : 0;
            decimal mensualidadDolares = decimal.TryParse(Request.Form["mensualidadDolares"], out decimal md) ? md : 0;
            decimal otrosSoles = decimal.TryParse(Request.Form["otrosSoles"], out decimal os) ? os : 0;
            decimal otrosDolares = decimal.TryParse(Request.Form["otrosDolares"], out decimal od) ? od : 0;

            string descDespacho = Request.Form["descDespacho"] ?? "";
            string descMensualidad = Request.Form["descMensualidad"] ?? "";
            string descOtros = Request.Form["descOtros"] ?? "";
            string descPrestamo = Request.Form["descPrestamo"] ?? "";

            if (despachoSoles > 0 || despachoDolares > 0 || prestamoSoles > 0 || prestamoDolares > 0 ||
                mensualidadSoles > 0 || mensualidadDolares > 0 || otrosSoles > 0 || otrosDolares > 0)
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarIngresos", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@despachoSoles", despachoSoles);
                    cmd.Parameters.AddWithValue("@despachoDolares", despachoDolares);
                    cmd.Parameters.AddWithValue("@prestamoSoles", prestamoSoles);
                    cmd.Parameters.AddWithValue("@prestamoDolares", prestamoDolares);
                    cmd.Parameters.AddWithValue("@mensualidadSoles", mensualidadSoles);
                    cmd.Parameters.AddWithValue("@mensualidadDolares", mensualidadDolares);
                    cmd.Parameters.AddWithValue("@otrosSoles", otrosSoles);
                    cmd.Parameters.AddWithValue("@otrosDolares", otrosDolares);
                    cmd.Parameters.AddWithValue("@descDespacho", string.IsNullOrEmpty(descDespacho) ? (object)DBNull.Value : descDespacho);
                    cmd.Parameters.AddWithValue("@descMensualidad", string.IsNullOrEmpty(descMensualidad) ? (object)DBNull.Value : descMensualidad);
                    cmd.Parameters.AddWithValue("@descOtros", string.IsNullOrEmpty(descOtros) ? (object)DBNull.Value : descOtros);
                    cmd.Parameters.AddWithValue("@descPrestamo", string.IsNullOrEmpty(descPrestamo) ? (object)DBNull.Value : descPrestamo);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void InsertarGastosPrincipales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            decimal peajesSoles = decimal.TryParse(Request.Form["peajesSoles"], out decimal pjs) ? pjs : 0;
            decimal peajesDolares = decimal.TryParse(Request.Form["peajesDolares"], out decimal pjd) ? pjd : 0;
            decimal alimentacionSoles = decimal.TryParse(Request.Form["alimentacionSoles"], out decimal als) ? als : 0;
            decimal alimentacionDolares = decimal.TryParse(Request.Form["alimentacionDolares"], out decimal ald) ? ald : 0;
            decimal apoyoSeguridadSoles = decimal.TryParse(Request.Form["apoyoSeguridadSoles"], out decimal ass) ? ass : 0;
            decimal apoyoSeguridadDolares = decimal.TryParse(Request.Form["apoyoSeguridadDolares"], out decimal asd) ? asd : 0;
            decimal reparacionesSoles = decimal.TryParse(Request.Form["reparacionesSoles"], out decimal reps) ? reps : 0;
            decimal reparacionesDolares = decimal.TryParse(Request.Form["reparacionesDolares"], out decimal repd) ? repd : 0;
            decimal movilidadSoles = decimal.TryParse(Request.Form["movilidadSoles"], out decimal movs) ? movs : 0;
            decimal movilidadDolares = decimal.TryParse(Request.Form["movilidadDolares"], out decimal movd) ? movd : 0;
            decimal encapadaSoles = decimal.TryParse(Request.Form["encapadaSoles"], out decimal encs) ? encs : 0;
            decimal encapadaDolares = decimal.TryParse(Request.Form["encapadaDolares"], out decimal encd) ? encd : 0;
            decimal hospedajeSoles = decimal.TryParse(Request.Form["hospedajeSoles"], out decimal hoss) ? hoss : 0;
            decimal hospedajeDolares = decimal.TryParse(Request.Form["hospedajeDolares"], out decimal hosd) ? hosd : 0;
            decimal combustibleSoles = decimal.TryParse(Request.Form["combustibleSoles"], out decimal coms) ? coms : 0;
            decimal combustibleDolares = decimal.TryParse(Request.Form["combustibleDolares"], out decimal comd) ? comd : 0;

            string descPeajes = Request.Form["descPeajes"] ?? "";
            string descAlimentacion = Request.Form["descAlimentacion"] ?? "";
            string descApoyoSeguridad = Request.Form["descApoyoSeguridad"] ?? "";
            string descReparaciones = Request.Form["descReparaciones"] ?? "";
            string descMovilidad = Request.Form["descMovilidad"] ?? "";
            string descEncapada = Request.Form["descEncapada"] ?? "";
            string descHospedaje = Request.Form["descHospedaje"] ?? "";
            string descCombustible = Request.Form["descCombustible"] ?? "";

            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarEgresos", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@peajesSoles", peajesSoles);
                cmd.Parameters.AddWithValue("@peajesDolares", peajesDolares);
                cmd.Parameters.AddWithValue("@descPeajes", string.IsNullOrEmpty(descPeajes) ? (object)DBNull.Value : descPeajes);
                cmd.Parameters.AddWithValue("@alimentacionSoles", alimentacionSoles);
                cmd.Parameters.AddWithValue("@alimentacionDolares", alimentacionDolares);
                cmd.Parameters.AddWithValue("@descAlimentacion", string.IsNullOrEmpty(descAlimentacion) ? (object)DBNull.Value : descAlimentacion);
                cmd.Parameters.AddWithValue("@apoyoSeguridadSoles", apoyoSeguridadSoles);
                cmd.Parameters.AddWithValue("@apoyoSeguridadDolares", apoyoSeguridadDolares);
                cmd.Parameters.AddWithValue("@descApoyoSeguridad", string.IsNullOrEmpty(descApoyoSeguridad) ? (object)DBNull.Value : descApoyoSeguridad);
                cmd.Parameters.AddWithValue("@reparacionesSoles", reparacionesSoles);
                cmd.Parameters.AddWithValue("@reparacionesDolares", reparacionesDolares);
                cmd.Parameters.AddWithValue("@descReparaciones", string.IsNullOrEmpty(descReparaciones) ? (object)DBNull.Value : descReparaciones);
                cmd.Parameters.AddWithValue("@movilidadSoles", movilidadSoles);
                cmd.Parameters.AddWithValue("@movilidadDolares", movilidadDolares);
                cmd.Parameters.AddWithValue("@descMovilidad", string.IsNullOrEmpty(descMovilidad) ? (object)DBNull.Value : descMovilidad);
                cmd.Parameters.AddWithValue("@encapadaSoles", encapadaSoles);
                cmd.Parameters.AddWithValue("@encapadaDolares", encapadaDolares);
                cmd.Parameters.AddWithValue("@descEncapada", string.IsNullOrEmpty(descEncapada) ? (object)DBNull.Value : descEncapada);
                cmd.Parameters.AddWithValue("@hospedajeSoles", hospedajeSoles);
                cmd.Parameters.AddWithValue("@hospedajeDolares", hospedajeDolares);
                cmd.Parameters.AddWithValue("@descHospedaje", string.IsNullOrEmpty(descHospedaje) ? (object)DBNull.Value : descHospedaje);
                cmd.Parameters.AddWithValue("@combustibleSoles", combustibleSoles);
                cmd.Parameters.AddWithValue("@combustibleDolares", combustibleDolares);
                cmd.Parameters.AddWithValue("@descCombustible", string.IsNullOrEmpty(descCombustible) ? (object)DBNull.Value : descCombustible);
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertarIngresosAdicionales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            try
            {
                string ingresosAdicionalesJson = hfIngresosAdicionales.Value ?? "[]";

                if (string.IsNullOrEmpty(ingresosAdicionalesJson) || ingresosAdicionalesJson == "[]")
                    return;

                List<IngresoAdicionalData> ingresosAdicionales = JsonConvert.DeserializeObject<List<IngresoAdicionalData>>(ingresosAdicionalesJson) ?? new List<IngresoAdicionalData>();

                foreach (var ingreso in ingresosAdicionales)
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarIngresoAdicional", conn, transaction))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.Categoria ?? ingreso.NombreCategoria ?? "");
                        cmd.Parameters.AddWithValue("@soles", ingreso.Soles ?? 0);
                        cmd.Parameters.AddWithValue("@dolares", ingreso.Dolares ?? 0);
                        cmd.Parameters.AddWithValue("@descripcion", ingreso.Descripcion ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando ingresos adicionales: {ex.Message}");
                throw;
            }
        }

        private void InsertarGastosAdicionales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            try
            {
                string gastosAdicionalesJson = hfGastosAdicionales.Value ?? "[]";

                if (string.IsNullOrEmpty(gastosAdicionalesJson) || gastosAdicionalesJson == "[]")
                    return;

                List<GastoAdicionalData> gastosAdicionales = JsonConvert.DeserializeObject<List<GastoAdicionalData>>(gastosAdicionalesJson) ?? new List<GastoAdicionalData>();

                foreach (var gasto in gastosAdicionales)
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarGastoAdicional", conn, transaction))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@nombreCategoria", gasto.Categoria ?? gasto.NombreCategoria ?? "");
                        cmd.Parameters.AddWithValue("@soles", gasto.Soles ?? 0);
                        cmd.Parameters.AddWithValue("@dolares", gasto.Dolares ?? 0);
                        cmd.Parameters.AddWithValue("@descripcion", gasto.Descripcion ?? "");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando gastos adicionales: {ex.Message}");
                throw;
            }
        }

        private void InsertarDescuentosReintegros(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            try
            {
                decimal descuentoSoles = decimal.TryParse(Request.Form["descuentoSoles"], out decimal ds) ? ds : 0;
                decimal descuentoDolares = decimal.TryParse(Request.Form["descuentoDolares"], out decimal dd) ? dd : 0;
                decimal reintegroSoles = decimal.TryParse(Request.Form["reintegroSoles"], out decimal rs) ? rs : 0;
                decimal reintegroDolares = decimal.TryParse(Request.Form["reintegroDolares"], out decimal rd) ? rd : 0;

                if (descuentoSoles > 0 || descuentoDolares > 0 || reintegroSoles > 0 || reintegroDolares > 0)
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDescuentosReintegros", conn, transaction))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@descuentoSoles", descuentoSoles);
                        cmd.Parameters.AddWithValue("@descuentoDolares", descuentoDolares);
                        cmd.Parameters.AddWithValue("@reintegroSoles", reintegroSoles);
                        cmd.Parameters.AddWithValue("@reintegroDolares", reintegroDolares);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando descuentos/reintegros: {ex.Message}");
                throw;
            }
        }

        private List<GastoFinanciero> ObtenerGastosFinancierosDeSession()
        {
            try
            {
                string gastosJson = hfGastosFinancieros.Value ?? "[]";
                return JsonConvert.DeserializeObject<List<GastoFinanciero>>(gastosJson) ?? new List<GastoFinanciero>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo gastos financieros: {ex.Message}");
                return new List<GastoFinanciero>();
            }
        }

        private void InsertarGastosFinancierosDetallados(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<GastoFinanciero> gastos)
        {
            try
            {
                foreach (var gasto in gastos)
                {
                    string categoria = gasto.Categoria?.ToLower() ?? "";

                    switch (categoria)
                    {
                        case "peajes":
                            InsertarPeajeDetallado(conn, transaction, numeroOrdenViaje, gasto);
                            break;
                        case "reparaciones":
                            InsertarReparacionDetallada(conn, transaction, numeroOrdenViaje, gasto);
                            break;
                        case "hospedaje":
                            InsertarHospedajeDetallado(conn, transaction, numeroOrdenViaje, gasto);
                            break;
                        case "combustible":
                            InsertarCombustibleDetallado(conn, transaction, numeroOrdenViaje, gasto);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando gastos detallados: {ex.Message}");
                throw;
            }
        }

        private void InsertarPeajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetallePeaje", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@estacion", gasto.Estacion ?? gasto.Lugar ?? "");
                cmd.Parameters.AddWithValue("@fecha", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertarReparacionDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetalleReparacion", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertarHospedajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetalleHospedaje", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private void InsertarCombustibleDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, GastoFinanciero gasto)
        {
            using (SqlCommand cmd = new SqlCommand("sp_DC_InsertarDetalleCombustible", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private void CerrarViajesProgreso(SqlConnection conn, SqlTransaction transaction, List<int> idsViajes, string numeroOrdenViaje)
        {
            try
            {
                using (SqlCommand cmd = new SqlCommand("sp_DC_CerrarViajesProgreso", conn, transaction))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idsViajes", string.Join(",", idsViajes));

                    SqlParameter pViajesCerrados = new SqlParameter("@filasViajesCerrados", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    SqlParameter pDespachosCerrados = new SqlParameter("@filasDespachosCerrados", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(pViajesCerrados);
                    cmd.Parameters.Add(pDespachosCerrados);

                    cmd.ExecuteNonQuery();

                    int filasAfectadas = (int)pViajesCerrados.Value;
                    int despachosActualizados = (int)pDespachosCerrados.Value;

                    if (filasAfectadas == 0)
                    {
                        throw new Exception($"No se pudo cerrar ninguno de los viajes: {string.Join(", ", idsViajes)}");
                    }
                    System.Diagnostics.Debug.WriteLine($"✅ {filasAfectadas} viaje(s) cerrado(s)");
                    System.Diagnostics.Debug.WriteLine($"✅ {despachosActualizados} despacho(s) marcado(s) como COMPLETADO");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CerrarViajesProgreso: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Métodos de Historial

        protected void btnBuscarHistorial_Click(object sender, EventArgs e)
        {
            CargarHistorialLiquidaciones();
        }

        private void CargarHistorialLiquidaciones()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerHistorialLiquidaciones", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        DataTable dt = new DataTable();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }

                        DataTable dtMapeado = new DataTable();
                        dtMapeado.Columns.Add("IdOrdenViaje", typeof(int));
                        dtMapeado.Columns.Add("NumeroViaje", typeof(string));
                        dtMapeado.Columns.Add("FechaSalida", typeof(DateTime));
                        dtMapeado.Columns.Add("FechaLlegada", typeof(DateTime));
                        dtMapeado.Columns.Add("CantidadDespachos", typeof(int));
                        dtMapeado.Columns.Add("BalanceSoles", typeof(decimal));
                        dtMapeado.Columns.Add("BalanceDolares", typeof(decimal));
                        dtMapeado.Columns.Add("Estado", typeof(string));

                        foreach (DataRow row in dt.Rows)
                        {
                            decimal ingrSoles = row["totalIngresosSoles"] != DBNull.Value ? Convert.ToDecimal(row["totalIngresosSoles"]) : 0;
                            decimal ingrDolares = row["totalIngresosDolares"] != DBNull.Value ? Convert.ToDecimal(row["totalIngresosDolares"]) : 0;
                            decimal gastSoles = row["totalGastosSoles"] != DBNull.Value ? Convert.ToDecimal(row["totalGastosSoles"]) : 0;
                            decimal gastDolares = row["totalGastosDolares"] != DBNull.Value ? Convert.ToDecimal(row["totalGastosDolares"]) : 0;

                            decimal balanceSoles = ingrSoles - gastSoles;
                            decimal balanceDolares = ingrDolares - gastDolares;

                            string estado = row["estadoAprobacion"] != DBNull.Value
                                ? row["estadoAprobacion"].ToString()
                                : "PENDIENTE";

                            DataRow newRow = dtMapeado.NewRow();
                            newRow["IdOrdenViaje"] = row["idOrdenViaje"];
                            newRow["NumeroViaje"] = row["numeroOrdenViaje"];
                            newRow["FechaSalida"] = row["fechaSalida"];
                            newRow["FechaLlegada"] = row["fechaLlegada"];
                            newRow["CantidadDespachos"] = 0;
                            newRow["BalanceSoles"] = balanceSoles;
                            newRow["BalanceDolares"] = balanceDolares;
                            newRow["Estado"] = estado;
                            dtMapeado.Rows.Add(newRow);
                        }

                        gvHistorial.DataSource = dtMapeado;
                        gvHistorial.DataBind();

                        lblTotalHistorial.Text = $"{dtMapeado.Rows.Count} registros";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando historial: {ex.Message}");
                MostrarMensaje($"Error al cargar el historial: {ex.Message}", "danger");
            }
        }

        protected void gvHistorial_RowCommand(object sender, GridViewCommandEventArgs e)
        {
        }

        #endregion

        #region Métodos Auxiliares

        public string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                List<EstacionPeaje> estaciones = new List<EstacionPeaje>();

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerEstacionesPeaje", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                estaciones.Add(new EstacionPeaje
                                {
                                    Nombre = reader["nombre"].ToString()
                                });
                            }
                        }
                    }
                }

                string json = JsonConvert.SerializeObject(estaciones);
                System.Diagnostics.Debug.WriteLine($"✅ JSON generado con {estaciones.Count} peajes: {json}");
                return json;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo estaciones: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                return "[]";
            }
        }

        private string GenerarNumeroOrden()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_GenerarNumeroOrden", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        SqlParameter pNumeroGenerado = new SqlParameter("@numeroGenerado", SqlDbType.VarChar, 50)
                        {
                            Direction = ParameterDirection.Output
                        };
                        cmd.Parameters.Add(pNumeroGenerado);

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        string nuevoNumero = pNumeroGenerado.Value?.ToString();

                        if (!string.IsNullOrEmpty(nuevoNumero))
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ Número de orden generado: {nuevoNumero}");
                            return nuevoNumero;
                        }

                        throw new Exception("El SP no retornó un número de orden");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error generando número de orden: {ex.Message}");
                string fallbackNumero = $"OV-{DateTime.Now:yyyyMMddHHmmss}-{IdConductorActual:D4}";
                return fallbackNumero;
            }
        }

        private DatosViajeParaLiquidacion ObtenerDatosViajeParaLiquidacion(int idViajeProgreso)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Obteniendo datos del viaje para idViajeProgreso={idViajeProgreso}");

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_ObtenerDatosViajeParaLiquidacion", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                        cmd.Parameters.AddWithValue("@idConductorFallback", IdConductorActual);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int idConductor = reader["idConductor"] != DBNull.Value ? Convert.ToInt32(reader["idConductor"]) : IdConductorActual;
                                int idTracto = reader["idTracto"] != DBNull.Value ? Convert.ToInt32(reader["idTracto"]) : 0;
                                int idCarreta = reader["idCarreta"] != DBNull.Value ? Convert.ToInt32(reader["idCarreta"]) : 0;
                                string origen = reader["origen"]?.ToString() ?? "";

                                if (idConductor == 0 || idTracto == 0 || idCarreta == 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"⚠️ Datos incompletos ({origen}): Conductor={idConductor}, Tracto={idTracto}, Carreta={idCarreta}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"✅ Datos obtenidos desde {origen}: Conductor={idConductor}, Tracto={idTracto}, Carreta={idCarreta}");
                                }

                                return new DatosViajeParaLiquidacion
                                {
                                    IdConductor = idConductor > 0 ? idConductor : IdConductorActual,
                                    IdTracto = idTracto,
                                    IdCarreta = idCarreta
                                };
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo datos del viaje: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                return null;
            }
        }

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada)
        {
            string mensajeError = "";

            if (fechaSalida == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Salida'.\n";

            if (fechaLlegada == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Llegada'.\n";

            if (fechaSalida != DateTime.MinValue && fechaLlegada != DateTime.MinValue)
            {
                if (fechaSalida > fechaLlegada)
                    mensajeError += "La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.\n";
            }

            return mensajeError;
        }

        private bool EsFechaValidaSQL(DateTime fecha)
        {
            return fecha >= new DateTime(1753, 1, 1) && fecha <= new DateTime(9999, 12, 31) && fecha != DateTime.MinValue;
        }

        private object FechaSeguraSQL(DateTime fecha)
        {
            return EsFechaValidaSQL(fecha) ? (object)fecha : DBNull.Value;
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;
        }

        private void MostrarResultadoExitoso(string numeroOrdenViaje)
        {
            try
            {
                string mensajeCompleto = $@"
                    ¡Liquidación enviada exitosamente!<br/>
                    <strong>Número de orden:</strong> {numeroOrdenViaje}<br/>
                    <div class='alert alert-info mt-3'>
                        <i class='fas fa-info-circle mr-2'></i>
                        <strong>Estado:</strong> Pendiente de aprobación<br/>
                        Tu liquidación ha sido enviada a la administración para su revisión.
                    </div>";

                MostrarMensaje(mensajeCompleto, "success");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error mostrando resultado: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Utilidad para GridView

        protected string ObtenerClaseEstado(object estado)
        {
            string estadoStr = estado?.ToString()?.ToUpper() ?? "";

            switch (estadoStr)
            {
                case "PROGRAMADO":
                    return "programado";
                case "EN_PROCESO":
                    return "en_proceso";
                case "COMPLETADO":
                    return "completado";
                case "APROBADO":
                    return "completado";
                case "PENDIENTE":
                    return "programado";
                case "RECHAZADO":
                    return "cancelado";
                default:
                    return "programado";
            }
        }

        protected string ObtenerClaseBalance(object balance)
        {
            decimal balanceDecimal = balance != null ? Convert.ToDecimal(balance) : 0;

            if (balanceDecimal >= 0)
                return "text-success font-weight-bold";
            else
                return "text-danger font-weight-bold";
        }

        #endregion

        #region WebMethods

        [WebMethod]
        public static object RetirarLiquidacion(int idOrdenViaje)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("sp_DC_RetirarLiquidacion", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);

                        SqlParameter pResultado = new SqlParameter("@resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        SqlParameter pMensaje = new SqlParameter("@mensaje", SqlDbType.VarChar, 500) { Direction = ParameterDirection.Output };
                        SqlParameter pNumeroOrden = new SqlParameter("@numeroOrdenViajeSalida", SqlDbType.VarChar, 50) { Direction = ParameterDirection.Output };
                        SqlParameter pIdViaje = new SqlParameter("@idViajeProgresoSalida", SqlDbType.Int) { Direction = ParameterDirection.Output };

                        cmd.Parameters.Add(pResultado);
                        cmd.Parameters.Add(pMensaje);
                        cmd.Parameters.Add(pNumeroOrden);
                        cmd.Parameters.Add(pIdViaje);

                        conn.Open();
                        cmd.ExecuteNonQuery();

                        int resultado = (int)pResultado.Value;
                        string mensaje = pMensaje.Value?.ToString() ?? "";
                        string numeroOrdenViaje = pNumeroOrden.Value?.ToString() ?? "";
                        int idViajeProgreso = pIdViaje.Value != DBNull.Value ? (int)pIdViaje.Value : 0;

                        if (resultado == 1)
                        {
                            AuditoriaHelper.Registrar("RETIRAR", "OrdenViaje", idOrdenViaje,
                                $"Conductor retiró liquidación - OrdenViaje ID: {idOrdenViaje}, Número: {numeroOrdenViaje}, Viaje reabierto: {idViajeProgreso}");

                            System.Diagnostics.Debug.WriteLine($"✅ Liquidación retirada: OrdenViaje {idOrdenViaje}, Viaje {idViajeProgreso} reabierto");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ RetirarLiquidacion falló: {mensaje}");
                        }

                        return new { success = resultado == 1, message = mensaje };
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en RetirarLiquidacion: {ex.Message}");
                return new { success = false, message = "Error al retirar la liquidación: " + ex.Message };
            }
        }

        #endregion
    }
}