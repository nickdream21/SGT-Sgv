using System.Web.UI;
using System.Web.Services;
using System.Web;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Views
{
    public partial class DashboardConductor : PaginaBase
    {
        #region Clases de Datos

        // Los modelos GastoFinanciero, IngresoAdicionalData y GastoAdicionalData se
        // movieron a WebSGV.Models.Conductor (compartidos con LiquidacionConductorService).

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
                if (!VerificarSesion())
                {
                    return; // VerificarSesion ya emitió el redirect; no inicializar nada
                }
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

        private bool VerificarSesion()
        {
            try
            {
                // Sin sesión activa => al login
                if (!RolesHelper.TieneSesionActiva())
                {
                    Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                // Rol no autorizado para esta sección => redirige según su rol
                if (!RolesHelper.TienePermiso("DASHBOARD_CONDUCTOR"))
                {
                    RolesHelper.RedirigirSegunRol();
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    // Usuario CONDUCTOR sin idConductor vinculado en BD.
                    // Limpiamos la sesión para evitar loop con Login.aspx (que auto-redirige al
                    // dashboard si encuentra Session["UsuarioID"] con rol CONDUCTOR) y enviamos
                    // un código de error que el Login mostrará al usuario.
                    Log("❌ Usuario logueado sin idConductor vinculado — limpiando sesión y redirigiendo al login");
                    Session.Clear();
                    Session.Abandon();
                    Response.Redirect("~/Views/Login.aspx?error=sin_conductor", false);
                    Context.ApplicationInstance.CompleteRequest();
                    return false;
                }

                Log($"✅ Sesión válida: IdConductor={IdConductorActual}");
                return true;
            }
            catch (System.Threading.ThreadAbortException)
            {
                return false;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar la sesión en DashboardConductor");
                Response.Redirect("~/Views/Login.aspx?error=sesion", false);
                Context.ApplicationInstance.CompleteRequest();
                return false;
            }
        }

        private void InicializarDashboard()
        {
            try
            {
                Log("=== INICIALIZANDO DASHBOARD CONDUCTOR ===");

                CargarDatosConductor();
                List<ViajeActivo> viajesActivos = ObtenerViajesActivos();

                if (viajesActivos.Count > 0)
                {
                    ViajeActivo viajePrimario = viajesActivos[0]; // el más antiguo (ASC)
                    List<int> idsViajes = viajesActivos.Select(v => v.IdViajeProgreso).ToList();
                    int totalDespachos = viajesActivos.Sum(v => v.CantidadDespachos);

                    Log($"✅ {viajesActivos.Count} viaje(s) activo(s) encontrado(s): {string.Join(", ", viajesActivos.Select(v => v.NumeroViajeProgreso))}");
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
                    Log("ℹ️ No hay viajes activos");
                    MostrarSinViajes();
                }

                CargarHistorialLiquidaciones();

                Log("✅ Dashboard inicializado correctamente");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al inicializar el dashboard en DashboardConductor");
                MostrarMensaje("Error al cargar el dashboard. Por favor, recarga la página o contacta al administrador.", "danger");
            }
        }

        private void CargarDatosConductor()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
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

                                Log($"✅ Conductor cargado: {lblNombreConductor.Text}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos del conductor en DashboardConductor");
                throw;
            }
        }

        #endregion

        #region Verificación de Observaciones de Rechazo

        private void VerificarObservacionesRechazo(List<int> idsViajes)
        {
            try
            {
                Log($"Verificando observaciones de rechazo para {idsViajes.Count} viaje(s)");

                using (SqlConnection conn = new SqlConnection(connectionString))
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

                                Log($"⚠️ LIQUIDACIÓN RECHAZADA ENCONTRADA: {numeroOrden}");

                                MostrarAlertaRechazo(numeroOrden, observaciones, fechaRechazo, rechazadoPor);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar observaciones en DashboardConductor");
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
                string connStr = connectionString;
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

                    Log($"✅ Orden rechazada encontrada: {ordenNumero}");

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
                    Log($"✅ Formulario pre-cargado con datos de orden rechazada: {ordenNumero}");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar la orden rechazada en DashboardConductor");
            }
        }

        #endregion

        #region Métodos de Viaje Activo

        private List<ViajeActivo> ObtenerViajesActivos()
        {
            List<ViajeActivo> viajes = new List<ViajeActivo>();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
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
                LogSGV.Error(ex, "Error al obtener viajes activos en DashboardConductor");
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

                Log($"✅ {viajes.Count} viaje(s) activo(s) mostrado(s): {numerosViaje}");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar viajes activos en DashboardConductor");
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

                using (SqlConnection conn = new SqlConnection(connectionString))
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

                Log($"✅ {despachos.Count} despachos cargados de {idsViajes.Count} viaje(s)");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos en DashboardConductor");
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

                DateTime ahoraServidor = DateTime.Now;
                txtFechaSalida.Text = viajePrimario.FechaInicio.ToString("yyyy-MM-dd");
                txtFechaLlegada.Text = ahoraServidor.ToString("yyyy-MM-dd");
                txtHoraSalida.Text = "08:00";
                txtHoraLlegada.Text = ahoraServidor.ToString("HH:mm");

                Log("✅ Formulario de liquidación habilitado");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al habilitar la liquidación en DashboardConductor");
                throw;
            }
        }

        #endregion

        #region Evento Principal - Enviar Liquidación

        protected void btnEnviarLiquidacion_Click(object sender, EventArgs e)
        {
            try
            {
                Log("=== INICIANDO ENVÍO DE LIQUIDACIÓN (CONDUCTOR) ===");

                // Verificar sesión activa antes de proceder
                if (Session["IdConductor"] == null || IdConductorActual == 0)
                {
                    MostrarMensaje("Tu sesión ha expirado. Por favor, inicia sesión nuevamente.", "warning");
                    return;
                }

                int idViajeProgreso = int.TryParse(hfIdViajeActivo.Value, out int ivp) ? ivp : 0;
                Log($"idViajeProgreso del HiddenField: {idViajeProgreso} (raw: '{hfIdViajeActivo.Value}')");

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
                Log($"Viajes a cerrar: {string.Join(", ", idsViajesActivos)}");

                // Verificar que todos los IDs de viajes pertenecen al conductor actual (C-3)
                if (!ValidarOwnershipViajes(idsViajesActivos, IdConductorActual))
                {
                    Log($"❌ Ownership inválido: los viajes {string.Join(", ", idsViajesActivos)} no pertenecen al conductor {IdConductorActual}");
                    MostrarMensaje("Acceso no autorizado", "danger");
                    return;
                }

                // ✅ Detectar si es una re-liquidación de orden rechazada
                bool esReliquidacion = !string.IsNullOrEmpty(hfNumeroOrdenExistente.Value);
                string numeroOrdenViaje = esReliquidacion
                    ? hfNumeroOrdenExistente.Value
                    : GenerarNumeroOrden();

                Log(esReliquidacion
                    ? $"♻️ Re-liquidación sobre orden existente: {numeroOrdenViaje}"
                    : $"Número de orden generado: {numeroOrdenViaje}");

                // Verificar que la orden rechazada pertenece al conductor actual (C-4)
                if (esReliquidacion && !ValidarOwnershipOrden(numeroOrdenViaje, IdConductorActual))
                {
                    Log($"❌ Ownership inválido: la orden {numeroOrdenViaje} no pertenece al conductor {IdConductorActual}");
                    MostrarMensaje("Acceso no autorizado", "danger");
                    return;
                }

                DateTime fechaSalida = DateTime.TryParse(txtFechaSalida.Text, out DateTime fs) ? fs : DateTime.MinValue;
                DateTime fechaLlegada = FechaHelper.Hoy();
                string horaSalida = txtHoraSalida.Text;
                string horaLlegada = FechaHelper.Ahora().ToString("HH:mm");
                string observaciones = txtObservaciones.Text;

                // Reflejar en UI la hora oficial usada para registrar la liquidación
                txtFechaLlegada.Text = fechaLlegada.ToString("yyyy-MM-dd");
                txtHoraLlegada.Text = horaLlegada;

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

                Log($"Datos del viaje: Conductor={datosViaje.IdConductor}, Tracto={datosViaje.IdTracto}, Carreta={datosViaje.IdCarreta}");

                // Armar el DTO de entrada leyendo Request.Form/hidden fields (el parseo
                // queda en el code-behind; el Service sólo ejecuta el SQL/SPs).
                LiquidacionConductorInput input = ConstruirInputLiquidacion(
                    esReliquidacion, numeroOrdenViaje, fechaSalida, fechaLlegada,
                    horaSalida, horaLlegada, observaciones, datosViaje,
                    idViajeProgreso, idsViajesActivos);

                Log("Iniciando transacción de base de datos...");
                bool transaccionExitosa = false;
                int idOrdenGuardada = 0;

                try
                {
                    idOrdenGuardada = LiquidacionConductorService.EnviarLiquidacion(input);
                    transaccionExitosa = true;
                    Log("✅ Commit exitoso");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Error en la transacción de liquidación del conductor (orden {Numero})", numeroOrdenViaje);
                    MostrarMensaje($"Error al enviar la liquidación: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
                }

                // Post-commit: mostrar resultado y programar redirect (fuera del using de la transacción)
                if (transaccionExitosa)
                {
                    AuditoriaHelper.Registrar("LIQUIDAR", "OrdenViaje", numeroOrdenViaje,
                        $"Conductor envió liquidación - Orden: {numeroOrdenViaje}, Viajes: {string.Join(", ", idsViajesActivos)}");

                    string nombreConductorNotif = Session["Nombre"]?.ToString() ?? lblNombreConductor.Text;
                    Services.NotificacionService.NotificarLiquidacionPendiente(numeroOrdenViaje, nombreConductorNotif);

                    Log($"=== LIQUIDACIÓN ENVIADA EXITOSAMENTE (idOrdenViaje={idOrdenGuardada}) ===");

                    // ✅ Fase 3.A: redirigir al conductor a la vista de firma digital
                    if (idOrdenGuardada > 0)
                    {
                        Response.Redirect(
                            ResolveUrl($"~/Views/FirmarLiquidacion.aspx?id={idOrdenGuardada}"),
                            false);
                        Context.ApplicationInstance.CompleteRequest();
                        return;
                    }

                    // Fallback: si no obtuvimos id, mostrar el mensaje clasico y recargar
                    MostrarResultadoExitoso(numeroOrdenViaje);
                    string redirectScript = $@"
                        setTimeout(function() {{
                            window.location.href = '{ResolveUrl(Request.RawUrl)}';
                        }}, 2000);";
                    ScriptManager.RegisterStartupScript(this, GetType(), "RedirectDespuesExito", redirectScript, true);
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error general al enviar la liquidación del conductor");
                MostrarMensaje("Error al enviar la liquidación. Por favor, intente nuevamente o contacte al administrador.", "danger");
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

        /// <summary>
        /// Construye el DTO de liquidación leyendo Request.Form y los hidden fields.
        /// El parseo (montos, descripciones, JSON) permanece en el code-behind; el
        /// Service (<see cref="LiquidacionConductorService"/>) sólo recibe valores ya
        /// parseados/validados y ejecuta el SQL/SPs.
        /// </summary>
        private LiquidacionConductorInput ConstruirInputLiquidacion(
            bool esReliquidacion, string numeroOrdenViaje,
            DateTime fechaSalida, DateTime fechaLlegada,
            string horaSalida, string horaLlegada, string observaciones,
            DatosViajeParaLiquidacion datosViaje,
            int idViajeProgreso, List<int> idsViajesActivos)
        {
            // Peajes simplificados: Nacionales solo en Soles (Perú), Extranjeros solo en Dólares (Ecuador)
            decimal peajesSoles   = ParseMonto(Request.Form["peajesNacSoles"]);
            decimal peajesDolares = ParseMonto(Request.Form["peajesExtDolares"]);

            // Descripción de peajes: combinar nacionales (S/) y extranjeros ($)
            string descPeajesNac = Request.Form["descPeajesNacionales"] ?? "";
            string descPeajesExt = Request.Form["descPeajesExtranjeros"] ?? "";
            var partesPeajes = new List<string>();
            if (peajesSoles > 0)
                partesPeajes.Add("Nac: S/" + peajesSoles.ToString("F2") + (!string.IsNullOrEmpty(descPeajesNac) ? " - " + descPeajesNac : ""));
            if (peajesDolares > 0)
                partesPeajes.Add("Ext: $" + peajesDolares.ToString("F2") + (!string.IsNullOrEmpty(descPeajesExt) ? " - " + descPeajesExt : ""));
            string descPeajes = partesPeajes.Count > 0 ? string.Join(" | ", partesPeajes) : "";

            return new LiquidacionConductorInput
            {
                EsReliquidacion = esReliquidacion,
                NumeroOrdenViaje = numeroOrdenViaje,
                FechaSalida = fechaSalida,
                FechaLlegada = fechaLlegada,
                HoraSalida = horaSalida,
                HoraLlegada = horaLlegada,
                Observaciones = observaciones,
                IdConductor = datosViaje.IdConductor,
                IdTracto = datosViaje.IdTracto,
                IdCarreta = datosViaje.IdCarreta,
                IdViajeProgreso = idViajeProgreso,
                IdUsuarioRegistro = IdUsuarioActual,
                IdConductorActual = IdConductorActual,
                IdsViajesActivos = idsViajesActivos,

                // Ingresos principales
                DespachoSoles      = ParseMonto(Request.Form["despachoSoles"]),
                DespachoDolares    = ParseMonto(Request.Form["despachoDolares"]),
                PrestamoSoles      = ParseMonto(Request.Form["prestamoSoles"]),
                PrestamoDolares    = ParseMonto(Request.Form["prestamoDolares"]),
                MensualidadSoles   = ParseMonto(Request.Form["mensualidadSoles"]),
                MensualidadDolares = ParseMonto(Request.Form["mensualidadDolares"]),
                OtrosSoles         = ParseMonto(Request.Form["otrosSoles"]),
                OtrosDolares       = ParseMonto(Request.Form["otrosDolares"]),
                DescDespacho       = Request.Form["descDespacho"] ?? "",
                DescMensualidad    = Request.Form["descMensualidad"] ?? "",
                DescOtros          = Request.Form["descOtros"] ?? "",
                DescPrestamo       = Request.Form["descPrestamo"] ?? "",

                // Egresos principales
                PeajesSoles           = peajesSoles,
                PeajesDolares         = peajesDolares,
                DescPeajes            = descPeajes,
                AlimentacionSoles     = ParseMonto(Request.Form["alimentacionSoles"]),
                AlimentacionDolares   = ParseMonto(Request.Form["alimentacionDolares"]),
                DescAlimentacion      = Request.Form["descAlimentacion"] ?? "",
                ApoyoSeguridadSoles   = ParseMonto(Request.Form["apoyoSeguridadSoles"]),
                ApoyoSeguridadDolares = ParseMonto(Request.Form["apoyoSeguridadDolares"]),
                DescApoyoSeguridad    = Request.Form["descApoyoSeguridad"] ?? "",
                ReparacionesSoles     = ParseMonto(Request.Form["reparacionesSoles"]),
                ReparacionesDolares   = ParseMonto(Request.Form["reparacionesDolares"]),
                DescReparaciones      = Request.Form["descReparaciones"] ?? "",
                MovilidadSoles        = ParseMonto(Request.Form["movilidadSoles"]),
                MovilidadDolares      = ParseMonto(Request.Form["movilidadDolares"]),
                DescMovilidad         = Request.Form["descMovilidad"] ?? "",
                EncapadaSoles         = ParseMonto(Request.Form["encapadaSoles"]),
                EncapadaDolares       = ParseMonto(Request.Form["encapadaDolares"]),
                DescEncapada          = Request.Form["descEncapada"] ?? "",
                HospedajeSoles        = ParseMonto(Request.Form["hospedajeSoles"]),
                HospedajeDolares      = ParseMonto(Request.Form["hospedajeDolares"]),
                DescHospedaje         = Request.Form["descHospedaje"] ?? "",
                CombustibleSoles      = ParseMonto(Request.Form["combustibleSoles"]),
                CombustibleDolares    = ParseMonto(Request.Form["combustibleDolares"]),
                DescCombustible       = Request.Form["descCombustible"] ?? "",

                // Descuentos / reintegros
                DescuentoSoles   = ParseMonto(Request.Form["descuentoSoles"]),
                DescuentoDolares = ParseMonto(Request.Form["descuentoDolares"]),
                ReintegroSoles   = ParseMonto(Request.Form["reintegroSoles"]),
                ReintegroDolares = ParseMonto(Request.Form["reintegroDolares"]),

                // Listas dinámicas / detalladas
                IngresosAdicionales = DeserializarIngresosAdicionales(),
                GastosAdicionales   = DeserializarGastosAdicionales(),
                GastosFinancieros   = ObtenerGastosFinancierosDeSession()
            };
        }

        private List<IngresoAdicionalData> DeserializarIngresosAdicionales()
        {
            string ingresosAdicionalesJson = hfIngresosAdicionales.Value ?? "[]";
            if (string.IsNullOrEmpty(ingresosAdicionalesJson) || ingresosAdicionalesJson == "[]")
                return new List<IngresoAdicionalData>();

            return JsonConvert.DeserializeObject<List<IngresoAdicionalData>>(ingresosAdicionalesJson) ?? new List<IngresoAdicionalData>();
        }

        private List<GastoAdicionalData> DeserializarGastosAdicionales()
        {
            string gastosAdicionalesJson = hfGastosAdicionales.Value ?? "[]";
            if (string.IsNullOrEmpty(gastosAdicionalesJson) || gastosAdicionalesJson == "[]")
                return new List<GastoAdicionalData>();

            return JsonConvert.DeserializeObject<List<GastoAdicionalData>>(gastosAdicionalesJson) ?? new List<GastoAdicionalData>();
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
                LogSGV.Error(ex, "Error al obtener gastos financieros en DashboardConductor");
                return new List<GastoFinanciero>();
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
                using (SqlConnection conn = new SqlConnection(connectionString))
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
                LogSGV.Error(ex, "Error al cargar el historial en DashboardConductor");
                MostrarMensaje($"Error al cargar el historial: {System.Web.HttpUtility.HtmlEncode(ex.Message)}", "danger");
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

                DataTable dtEstaciones = DashboardConductorService.ObtenerEstacionesPeaje();
                foreach (DataRow row in dtEstaciones.Rows)
                {
                    estaciones.Add(new EstacionPeaje
                    {
                        Nombre = row["nombre"].ToString()
                    });
                }

                string json = JsonConvert.SerializeObject(estaciones);
                Log($"✅ JSON generado con {estaciones.Count} peajes: {json}");
                return json;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener estaciones en DashboardConductor");
                return "[]";
            }
        }

        private string GenerarNumeroOrden()
        {
            try
            {
                string nuevoNumero = DashboardConductorService.GenerarNumeroOrden();

                if (!string.IsNullOrEmpty(nuevoNumero))
                {
                    Log($"✅ Número de orden generado: {nuevoNumero}");
                    return nuevoNumero;
                }

                throw new Exception("El SP no retornó un número de orden");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al generar el número de orden en DashboardConductor");
                // Fallback: un solo snapshot de DateTime.Now garantiza consistencia del timestamp
                var ahora = DateTime.Now;
                string fallbackNumero = $"OV-{ahora:yyyyMMddHHmmss}-{IdConductorActual:D4}-{ahora.Ticks % 1000:D3}";
                Log($"⚠️ Usando número de orden fallback (SP falló): {fallbackNumero}");
                return fallbackNumero;
            }
        }

        private DatosViajeParaLiquidacion ObtenerDatosViajeParaLiquidacion(int idViajeProgreso)
        {
            try
            {
                Log($"Obteniendo datos del viaje para idViajeProgreso={idViajeProgreso}");

                DataTable dtViaje = DashboardConductorService.ObtenerDatosViajeParaLiquidacion(idViajeProgreso, IdConductorActual);
                if (dtViaje.Rows.Count > 0)
                {
                    DataRow reader = dtViaje.Rows[0];
                    int idConductor = reader["idConductor"] != DBNull.Value ? Convert.ToInt32(reader["idConductor"]) : IdConductorActual;
                    int idTracto = reader["idTracto"] != DBNull.Value ? Convert.ToInt32(reader["idTracto"]) : 0;
                    int idCarreta = reader["idCarreta"] != DBNull.Value ? Convert.ToInt32(reader["idCarreta"]) : 0;
                    string origen = reader["origen"]?.ToString() ?? "";

                    if (idConductor == 0 || idTracto == 0 || idCarreta == 0)
                    {
                        Log($"⚠️ Datos incompletos ({origen}): Conductor={idConductor}, Tracto={idTracto}, Carreta={idCarreta}");
                    }
                    else
                    {
                        Log($"✅ Datos obtenidos desde {origen}: Conductor={idConductor}, Tracto={idTracto}, Carreta={idCarreta}");
                    }

                    return new DatosViajeParaLiquidacion
                    {
                        IdConductor = idConductor > 0 ? idConductor : IdConductorActual,
                        IdTracto = idTracto,
                        IdCarreta = idCarreta
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener los datos del viaje en DashboardConductor");
                return null;
            }
        }

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada)
        {
            string mensajeError = "";

            // Validar fechas obligatorias
            if (fechaSalida == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Salida'.\n";

            if (fechaLlegada == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Llegada'.\n";

            // Validar orden lógico de fechas
            if (fechaSalida != DateTime.MinValue && fechaLlegada != DateTime.MinValue)
            {
                if (fechaSalida > fechaLlegada)
                    mensajeError += "La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.\n";

                // M-3: Detectar fechas absurdamente futuras (más de 1 año desde hoy)
                DateTime limiteMaximo = DateTime.Today.AddYears(1);
                if (fechaSalida > limiteMaximo || fechaLlegada > limiteMaximo)
                    mensajeError += "Las fechas no pueden ser superiores a un año desde hoy.\n";
            }

            // M-2: Validar formato de hora (HH:mm, rango 00:00-23:59)
            if (!string.IsNullOrEmpty(horaSalida) && !ValidarFormatoHora(horaSalida))
                mensajeError += "El formato de 'Hora de Salida' es incorrecto. Use HH:MM (ej. 08:30).\n";

            if (!string.IsNullOrEmpty(horaLlegada) && !ValidarFormatoHora(horaLlegada))
                mensajeError += "El formato de 'Hora de Llegada' es incorrecto. Use HH:MM (ej. 18:00).\n";

            return mensajeError;
        }

        /// <summary>
        /// Valida que una cadena represente una hora válida en formato H:mm o HH:mm (00:00–23:59).
        /// Acepta con y sin cero inicial para interoperabilidad con distintos dispositivos.
        /// Usa InvariantCulture para evitar dependencia del locale del servidor.
        /// </summary>
        private bool ValidarFormatoHora(string hora) =>
            OrdenViajeValidaciones.ValidarFormatoHora(hora);

        /// <summary>
        /// Logging condicional: solo activo en compilaciones DEBUG.
        /// En Release el compilador elimina todos los call-sites sin overhead.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        private static void Log(string mensaje)
            => System.Diagnostics.Debug.WriteLine(mensaje);

        /// <summary>
        /// Parsea un monto proveniente de Request.Form con cultura invariante.
        /// HTML type="number" siempre usa punto como separador decimal,
        /// independientemente de la cultura del servidor (es-PE usa coma).
        /// Valores negativos se clampean a 0 ya que los campos tienen min="0".
        /// </summary>
        private static decimal ParseMonto(string valor) => MontoHelper.ParseMonto(valor);

        private void MostrarMensaje(string mensaje, string tipo)
        {
            // M-4: whitelist de tipos Bootstrap válidos para evitar CSS injection
            var tiposPermitidos = new System.Collections.Generic.HashSet<string>
                { "success", "danger", "warning", "info", "primary", "secondary" };
            string tipoSeguro = tiposPermitidos.Contains(tipo) ? tipo : "info";

            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipoSeguro} alert-dismissible fade show";
            pnlMensajes.Visible = true;
        }

        private void MostrarResultadoExitoso(string numeroOrdenViaje)
        {
            try
            {
                string mensajeCompleto = $@"
                    ¡Liquidación enviada exitosamente!<br/>
                    <strong>Número de orden:</strong> {System.Web.HttpUtility.HtmlEncode(numeroOrdenViaje)}<br/>
                    <div class='alert alert-info mt-3'>
                        <i class='fas fa-info-circle mr-2'></i>
                        <strong>Estado:</strong> Pendiente de aprobación<br/>
                        Tu liquidación ha sido enviada a la administración para su revisión.
                    </div>";

                MostrarMensaje(mensajeCompleto, "success");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar el resultado en DashboardConductor");
            }
        }

        private bool ValidarOwnershipViajes(List<int> idsViajes, int idConductor)
        {
            try
            {
                if (idsViajes == null || idsViajes.Count == 0) return false;

                int count = DashboardConductorService.ContarViajesDelConductor(idsViajes, idConductor);
                return count == idsViajes.Count;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en ValidarOwnershipViajes en DashboardConductor");
                return false;
            }
        }

        private bool ValidarOwnershipOrden(string numeroOrden, int idConductor)
        {
            try
            {
                if (string.IsNullOrEmpty(numeroOrden)) return false;

                int count = DashboardConductorService.ContarOrdenDelConductor(numeroOrden, idConductor);
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en ValidarOwnershipOrden en DashboardConductor");
                return false;
            }
        }

        #endregion

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

        #region WebMethods

        [WebMethod(EnableSession = true)]
        public static object RetirarLiquidacion(int idOrdenViaje)
        {
            try
            {
                // Validar sesión activa
                var session = HttpContext.Current.Session;
                if (session["IdConductor"] == null)
                {
                    Log("❌ RetirarLiquidacion: sesión no válida (IdConductor nulo)");
                    return new { success = false, message = "Sesión no válida" };
                }

                // Validar rol: solo CONDUCTOR / CHOFER pueden retirar liquidaciones propias
                if (!RolesHelper.EsConductor())
                {
                    Log("❌ RetirarLiquidacion: rol no autorizado");
                    return new { success = false, message = "Sesión no válida" };
                }

                int idConductorActual = Convert.ToInt32(session["IdConductor"]);

                var r = DashboardConductorService.RetirarLiquidacion(idOrdenViaje, idConductorActual);
                int resultado = r.Resultado;
                string mensaje = r.Mensaje;
                string numeroOrdenViaje = r.NumeroOrdenViaje;
                int idViajeProgreso = r.IdViajeProgreso;

                if (resultado == 1)
                {
                    AuditoriaHelper.Registrar("RETIRAR", "OrdenViaje", idOrdenViaje,
                        $"Conductor retiró liquidación - OrdenViaje ID: {idOrdenViaje}, Número: {numeroOrdenViaje}, Viaje reabierto: {idViajeProgreso}");

                    Log($"✅ Liquidación retirada: OrdenViaje {idOrdenViaje}, Viaje {idViajeProgreso} reabierto");
                }
                else
                {
                    Log($"⚠️ RetirarLiquidacion falló: {mensaje}");
                }

                return new { success = resultado == 1, message = mensaje };
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al retirar la liquidación (orden {IdOrden})", idOrdenViaje);
                return new { success = false, message = "Error al retirar la liquidación. Intente nuevamente." };
            }
        }

        #endregion
    }
}