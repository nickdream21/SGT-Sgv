using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.OrdenViaje;
using WebSGV.Services.OrdenViaje;

namespace WebSGV.Views
{
    public partial class AgregarOrdenViaje : PaginaBase
    {
        #region Clases de Datos

        /// <summary>
        /// Clase para transferir datos de viajes finalizados
        /// </summary>
        [Serializable]
        public class DatosTransferencia
        {
            public int IdViajeProgreso { get; set; }  // ✅ CRÍTICO
            public string Conductor { get; set; }
            public string Cliente { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string Planta { get; set; }
            public string Operacion { get; set; }
            public int CantidadDespachos { get; set; }
            public bool TieneVariaciones { get; set; }
            public List<DespachoViaje> DespachosDetalle { get; set; }

            // IDs para bloquear dropdowns
            public int IdConductor { get; set; }
            public int IdTracto { get; set; }
            public int IdCarreta { get; set; }
            public int IdCliente { get; set; }

            // ✅ NUEVO: Datos para operaciones internacionales
            public bool EsInternacional { get; set; }
            public string NumeroCPIC { get; set; }
            public int? IdCPIC { get; set; }  // ID en la tabla CPIC

            public DatosTransferencia()
            {
                DespachosDetalle = new List<DespachoViaje>();
            }
        }

        /// <summary>
        /// Clase para despachos del viaje origen
        /// </summary>
        [Serializable]
        public class DespachoViaje
        {
            public int IdDespacho { get; set; }
            public string NumeroDespacho { get; set; }
            public DateTime FechaDespacho { get; set; }
            public string NombreCliente { get; set; }
            public string NombreConductor { get; set; }
            public string PlacaTracto { get; set; }
            public string PlacaCarreta { get; set; }
            public string TipoOperacion { get; set; }
            public string LugarOperacion { get; set; }
            public string EstadoDespacho { get; set; }
            public string GuiaRemitente { get; set; }
            public string GuiaTransportista { get; set; }
            public string NumeroViaje { get; set; }
        }

        // GastoFinanciero, IngresoAdicionalData y GastoAdicionalData se movieron a
        // WebSGV.Models.OrdenViaje (AgregarOrdenViajeModels.cs). Son tipos propios de esta
        // página: su GastoFinanciero deserializa la fecha como DateTime directo y por eso no
        // se fusiona con el de WebSGV.Models.Conductor.

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
            {
                InicializarSistema();

                // ✅ NUEVO: Detectar modo edición
                if (Request.QueryString["modo"] == "editar" && Request.QueryString["id"] != null)
                {
                    int idOrdenViaje = int.TryParse(Request.QueryString["id"], out int id) ? id : 0;

                    if (idOrdenViaje > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"=== MODO EDICIÓN: ID {idOrdenViaje} ===");
                        CargarDatosOrdenExistente(idOrdenViaje);
                    }
                    else
                    {
                        MostrarMensaje("ID de orden inválido para edición.", "warning");
                        CargarDatosNormales();
                    }
                }
                // Verificar origen viaje finalizado
                else if (Request.QueryString["origen"] == "viajeFinalizado")
                {
                    CargarDatosDesdeViajeFinalizados();
                }
                else
                {
                    CargarDatosNormales();
                }
            }
        }

        #endregion

        #region Métodos de Carga de Datos

        private void CargarDatosNormales()
        {
            try
            {
                OcultarPanelesViajeOrigen();

                // Configurar fechas por defecto
                DateTime hoy = DateTime.Today;
                txtFechaSalida.Text = hoy.ToString("yyyy-MM-dd");
                txtFechaLlegada.Text = hoy.AddDays(1).ToString("yyyy-MM-dd");
                txtHoraSalida.Text = "08:00";
                txtHoraLlegada.Text = "18:00";

                System.Diagnostics.Debug.WriteLine("Carga normal completada");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en la carga normal de AgregarOrdenViaje");
                MostrarMensaje("Error al cargar la página: " + ex.Message, "danger");
            }
        }

        private void CargarDatosOrdenExistente(int idOrdenViaje)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando datos de orden {idOrdenViaje} ---");

                // SQL movido a AgregarOrdenViajeService.ObtenerOrdenParaEdicion; el binding a
                // controles y el script de carga permanecen aquí.
                DataTable dtOrden = AgregarOrdenViajeService.ObtenerOrdenParaEdicion(idOrdenViaje);

                if (dtOrden.Rows.Count > 0)
                {
                    DataRow reader = dtOrden.Rows[0];

                                string numeroOrdenViaje = reader["numeroOrdenViaje"].ToString();

                                // === DATOS GENERALES ===
                                txtNumeroOrdenViaje.Text = numeroOrdenViaje;
                                txtNumeroOrdenViaje.ReadOnly = true; // ✅ Bloquear edición del número

                                hfIdOrdenViaje.Value = idOrdenViaje.ToString();

                                txtFechaSalida.Text = Convert.ToDateTime(reader["fechaSalida"]).ToString("yyyy-MM-dd");
                                txtFechaLlegada.Text = Convert.ToDateTime(reader["fechaLlegada"]).ToString("yyyy-MM-dd");
                                txtHoraSalida.Text = reader["horaSalida"]?.ToString() ?? "";
                                txtHoraLlegada.Text = reader["horaLlegada"]?.ToString() ?? "";
                                txtObservaciones.Text = reader["observaciones"]?.ToString() ?? "";

                                // === CONDUCTOR Y VEHÍCULOS ===
                                txtConductor.Text = reader["nombreConductor"].ToString();
                                txtPlacaTracto.Text = reader["placaTracto"].ToString();
                                txtPlacaCarreta.Text = reader["placaCarreta"].ToString();

                                hfIdConductor.Value = reader["idConductor"].ToString();
                                hfIdTracto.Value = reader["idTracto"].ToString();
                                hfIdCarreta.Value = reader["idCarreta"].ToString();

                                // === CARGAR INGRESOS Y GASTOS PRINCIPALES VÍA JAVASCRIPT ===
                                string scriptDatos = $@"
                            <script>
                                $(document).ready(function() {{
                                    // INGRESOS
                                    $('input[name=""despachoSoles""]').val('{reader["despachoSoles"] ?? "0"}');
                                    $('input[name=""despachoDolares""]').val('{reader["despachoDolares"] ?? "0"}');
                                    $('input[name=""descDespacho""]').val('{reader["descDespacho"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""prestamoSoles""]').val('{reader["prestamoSoles"] ?? "0"}');
                                    $('input[name=""prestamoDolares""]').val('{reader["prestamosDolares"] ?? "0"}');
                                    $('input[name=""descPrestamo""]').val('{reader["descPrestamo"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""mensualidadSoles""]').val('{reader["mensualidadSoles"] ?? "0"}');
                                    $('input[name=""mensualidadDolares""]').val('{reader["mensualidadDolares"] ?? "0"}');
                                    $('input[name=""descMensualidad""]').val('{reader["descMensualidad"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""otrosSoles""]').val('{reader["otrosSoles"] ?? "0"}');
                                    $('input[name=""otrosDolares""]').val('{reader["otrosDolares"] ?? "0"}');
                                    $('input[name=""descOtros""]').val('{reader["descOtrosAutorizados"]?.ToString().Replace("'", "\\'")}');
                                    
                                    // GASTOS
                                    $('#peajesSoles').val('{reader["peajesSoles"] ?? "0"}');
                                    $('#peajesDolares').val('{reader["peajesDolares"] ?? "0"}');
                                    $('#descPeajes').val('{reader["descPeajes"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""alimentacionSoles""]').val('{reader["alimentacionSoles"] ?? "0"}');
                                    $('input[name=""alimentacionDolares""]').val('{reader["alimentacionDolares"] ?? "0"}');
                                    $('input[name=""descAlimentacion""]').val('{reader["descAlimentacion"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""apoyoSeguridadSoles""]').val('{reader["apoyoseguridadSoles"] ?? "0"}');
                                    $('input[name=""apoyoSeguridadDolares""]').val('{reader["apoyoseguridadDolares"] ?? "0"}');
                                    $('input[name=""descApoyoSeguridad""]').val('{reader["descApoyoSeguridad"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('#reparacionesSoles').val('{reader["reparacionesVariosSoles"] ?? "0"}');
                                    $('#reparacionesDolares').val('{reader["repacionesVariosDolares"] ?? "0"}');
                                    $('#descReparaciones').val('{reader["descReparacionesVarios"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""movilidadSoles""]').val('{reader["movilidadSoles"] ?? "0"}');
                                    $('input[name=""movilidadDolares""]').val('{reader["movilidadDolares"] ?? "0"}');
                                    $('input[name=""descMovilidad""]').val('{reader["descMovilidad"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('input[name=""encapadaSoles""]').val('{reader["encarpada_desencarpadaSoles"] ?? "0"}');
                                    $('input[name=""encapadaDolares""]').val('{reader["encarpada_desencarpadaDolares"] ?? "0"}');
                                    $('input[name=""descEncapada""]').val('{reader["descEncarpadaDesencarpada"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('#hospedajeSoles').val('{reader["hospedajeSoles"] ?? "0"}');
                                    $('#hospedajeDolares').val('{reader["hospedajeDolares"] ?? "0"}');
                                    $('#descHospedaje').val('{reader["descHospedaje"]?.ToString().Replace("'", "\\'")}');
                                    
                                    $('#combustibleSoles').val('{reader["combustibleSoles"] ?? "0"}');
                                    $('#combustibleDolares').val('{reader["combustibleDolares"] ?? "0"}');
                                    $('#descCombustible').val('{reader["descCombustible"]?.ToString().Replace("'", "\\'")}');
                                    
                                    console.log('✅ Ingresos y gastos principales cargados');
                                    calcularTotales();
                                }});
                            </script>
                        ";

                                ScriptManager.RegisterStartupScript(this, GetType(), "cargarDatosPrincipales", scriptDatos, false);

                    // === CARGAR INGRESOS ADICIONALES ===
                    CargarIngresosAdicionalesEdicion(numeroOrdenViaje);

                    // === CARGAR GASTOS ADICIONALES ===
                    CargarGastosAdicionalesEdicion(numeroOrdenViaje);

                    // === CARGAR DATOS DE MODALES ===
                    CargarDatosModalesEdicion(numeroOrdenViaje);

                    System.Diagnostics.Debug.WriteLine($"✅ Datos de orden {idOrdenViaje} cargados completamente");
                    MostrarMensaje($"Orden <strong>{numeroOrdenViaje}</strong> cargada para edición. Modifica los datos necesarios y guarda los cambios.", "info");
                }
                else
                {
                    MostrarMensaje($"No se encontró la orden con ID {idOrdenViaje}", "warning");
                    CargarDatosNormales();
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar la orden para edición en AgregarOrdenViaje");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                MostrarMensaje($"Error al cargar la orden: {ex.Message}", "danger");
                CargarDatosNormales();
            }
        }



        private void CargarIngresosAdicionalesEdicion(string numeroOrden)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando ingresos adicionales de orden {numeroOrden} ---");

                List<IngresoAdicionalData> ingresosAdicionales =
                    AgregarOrdenViajeService.ObtenerIngresosAdicionalesParaEdicion(numeroOrden);

                if (ingresosAdicionales.Count > 0)
                {
                    string json = JsonConvert.SerializeObject(ingresosAdicionales);

                    // ✅ Escapar comillas para JavaScript
                    json = json.Replace("\"", "\\\"");

                    // ✅ Inyectar JavaScript para cargar los datos
                    string script = $@"
                <script>
                    $(document).ready(function() {{
                        var ingresosJSON = '{json}';
                        $('#hiddenIngresosAdicionales').val(ingresosJSON);
                        
                        console.log('✅ Ingresos adicionales cargados:', JSON.parse(ingresosJSON));
                        
                        // Reconstruir la tabla visualmente
                        var ingresos = JSON.parse(ingresosJSON);
                        ingresos.forEach(function(ingreso, index) {{
                            contadorIngresosAdicionales++;
                            var numeroFila = 4 + contadorIngresosAdicionales;
                            
                            $('#ingresosAdicionalesBody').append(`
                                <tr id='ingresoAdicional_${{contadorIngresosAdicionales}}'>
                                    <td class='text-center'>${{numeroFila}}</td>
                                    <td>
                                        <input type='text' class='form-control form-control-sm' 
                                               name='conceptoIngreso_${{contadorIngresosAdicionales}}' 
                                               value='${{ingreso.nombreCategoria || ingreso.categoria}}' required>
                                    </td>
                                    <td>
                                        <input type='text' class='form-control form-control-sm' 
                                               name='descIngreso_${{contadorIngresosAdicionales}}' 
                                               value='${{ingreso.descripcion}}'>
                                    </td>
                                    <td>
                                        <input type='number' class='form-control form-control-sm ingreso-soles' 
                                               name='ingresoSoles_${{contadorIngresosAdicionales}}' 
                                               value='${{ingreso.soles}}' step='0.01' onchange='calcularTotales()'>
                                    </td>
                                    <td>
                                        <input type='number' class='form-control form-control-sm ingreso-dolares' 
                                               name='ingresoDolares_${{contadorIngresosAdicionales}}' 
                                               value='${{ingreso.dolares}}' step='0.01' onchange='calcularTotales()'>
                                    </td>
                                    <td class='text-center'>
                                        <button type='button' class='btn btn-danger btn-sm' 
                                                onclick='eliminarIngreso(${{contadorIngresosAdicionales}})'>
                                            <i class='fas fa-trash'></i>
                                        </button>
                                    </td>
                                </tr>
                            `);
                        }});
                        
                        calcularTotales();
                    }});
                </script>
            ";

                    ScriptManager.RegisterStartupScript(this, GetType(), "cargarIngresosAdicionales", script, false);
                    System.Diagnostics.Debug.WriteLine($"✅ {ingresosAdicionales.Count} ingresos adicionales cargados");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay ingresos adicionales para esta orden");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar ingresos adicionales en AgregarOrdenViaje");
            }
        }

        private void CargarGastosAdicionalesEdicion(string numeroOrden)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando gastos adicionales de orden {numeroOrden} ---");

                List<GastoAdicionalData> gastosAdicionales =
                    AgregarOrdenViajeService.ObtenerGastosAdicionalesParaEdicion(numeroOrden);

                if (gastosAdicionales.Count > 0)
                {
                    string json = JsonConvert.SerializeObject(gastosAdicionales);

                    // ✅ Escapar comillas para JavaScript
                    json = json.Replace("\"", "\\\"");

                    // ✅ Inyectar JavaScript para cargar los datos
                    string script = $@"
                <script>
                    $(document).ready(function() {{
                        var gastosJSON = '{json}';
                        $('#hiddenGastosAdicionales').val(gastosJSON);
                        
                        console.log('✅ Gastos adicionales cargados:', JSON.parse(gastosJSON));
                        
                        // Reconstruir la tabla visualmente
                        var gastos = JSON.parse(gastosJSON);
                        gastos.forEach(function(gasto, index) {{
                            contadorGastosAdicionales++;
                            var numeroFila = 8 + contadorGastosAdicionales;
                            
                            $('#gastosAdicionalesBody').append(`
                                <tr id='gastoAdicional_${{contadorGastosAdicionales}}'>
                                    <td class='text-center'>${{numeroFila}}</td>
                                    <td>
                                        <input type='text' class='form-control form-control-sm' 
                                               name='conceptoGasto_${{contadorGastosAdicionales}}' 
                                               value='${{gasto.nombreCategoria || gasto.categoria}}' required>
                                    </td>
                                    <td>
                                        <input type='text' class='form-control form-control-sm' 
                                               name='descGasto_${{contadorGastosAdicionales}}' 
                                               value='${{gasto.descripcion}}'>
                                    </td>
                                    <td>
                                        <input type='number' class='form-control form-control-sm gasto-soles' 
                                               name='gastoSoles_${{contadorGastosAdicionales}}' 
                                               value='${{gasto.soles}}' step='0.01' onchange='calcularTotales()'>
                                    </td>
                                    <td>
                                        <input type='number' class='form-control form-control-sm gasto-dolares' 
                                               name='gastoDolares_${{contadorGastosAdicionales}}' 
                                               value='${{gasto.dolares}}' step='0.01' onchange='calcularTotales()'>
                                    </td>
                                    <td class='text-center'>
                                        <button type='button' class='btn btn-danger btn-sm' 
                                                onclick='eliminarGasto(${{contadorGastosAdicionales}})'>
                                            <i class='fas fa-trash'></i>
                                        </button>
                                    </td>
                                </tr>
                            `);
                        }});
                        
                        calcularTotales();
                    }});
                </script>
            ";

                    ScriptManager.RegisterStartupScript(this, GetType(), "cargarGastosAdicionales", script, false);
                    System.Diagnostics.Debug.WriteLine($"✅ {gastosAdicionales.Count} gastos adicionales cargados");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay gastos adicionales para esta orden");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar gastos adicionales en AgregarOrdenViaje");
            }
        }


        private void CargarDatosModalesEdicion(string numeroOrden)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando datos de modales de orden {numeroOrden} ---");

                List<GastoFinanciero> todosLosGastos =
                    AgregarOrdenViajeService.ObtenerGastosDetalladosParaEdicion(numeroOrden);

                // ========== SERIALIZAR TODO A JSON ==========
                if (todosLosGastos.Count > 0)
                {
                    string json = JsonConvert.SerializeObject(todosLosGastos);

                    // ✅ Guardar en HiddenField
                    hfGastosFinancieros.Value = json;

                    // ✅ Escapar comillas para JavaScript
                    json = json.Replace("\"", "\\\"");

                    // ✅ Inyectar JavaScript para reconstruir los modales
                    string script = $@"
                <script>
                    $(document).ready(function() {{
                        var gastosJSON = '{json}';
                        $('#hfGastosFinancieros').val(gastosJSON);
                        
                        console.log('✅ Datos de modales cargados:', JSON.parse(gastosJSON));
                        
                        var gastosDetallados = JSON.parse(gastosJSON);
                        
                        // Separar por categoría
                        peajesData = gastosDetallados.filter(g => g.categoria === 'peajes');
                        reparacionesData = gastosDetallados.filter(g => g.categoria === 'reparaciones');
                        hospedajesData = gastosDetallados.filter(g => g.categoria === 'hospedaje');
                        combustiblesData = gastosDetallados.filter(g => g.categoria === 'combustible');
                        
                        // Actualizar contadores
                        contadorPeajes = peajesData.length > 0 ? Math.max(...peajesData.map(p => p.id)) : 0;
                        contadorReparaciones = reparacionesData.length > 0 ? Math.max(...reparacionesData.map(r => r.id)) : 0;
                        contadorHospedajes = hospedajesData.length > 0 ? Math.max(...hospedajesData.map(h => h.id)) : 0;
                        contadorCombustibles = combustiblesData.length > 0 ? Math.max(...combustiblesData.map(c => c.id)) : 0;
                        
                        // Actualizar totales de modales
                        actualizarTotalesPeajes();
                        actualizarTotalesReparaciones();
                        actualizarTotalesHospedajes();
                        actualizarTotalesCombustibles();
                        
                        console.log('📊 Peajes:', peajesData.length);
                        console.log('📊 Reparaciones:', reparacionesData.length);
                        console.log('📊 Hospedajes:', hospedajesData.length);
                        console.log('📊 Combustibles:', combustiblesData.length);
                        
                        calcularTotales();
                    }});
                </script>
            ";

                    ScriptManager.RegisterStartupScript(this, GetType(), "cargarDatosModales", script, false);
                    System.Diagnostics.Debug.WriteLine($"✅ Total de gastos detallados cargados: {todosLosGastos.Count}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay datos de modales para esta orden");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos de modales en AgregarOrdenViaje");
            }
        }


        private void CargarDatosDesdeViajeFinalizados()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== CARGANDO DATOS DESDE VIAJE FINALIZADO ===");

                string datosEncoded = Request.QueryString["datos"];

                if (string.IsNullOrEmpty(datosEncoded))
                {
                    System.Diagnostics.Debug.WriteLine("❌ No se encontraron datos en URL");
                    MostrarMensaje("No se encontraron datos del viaje finalizado.", "warning");
                    CargarDatosNormales();
                    return;
                }

                string datosJson = Server.UrlDecode(datosEncoded);
                var datosTransferencia = JsonConvert.DeserializeObject<DatosTransferencia>(datosJson);

                if (datosTransferencia == null)
                {
                    System.Diagnostics.Debug.WriteLine("❌ Error al deserializar datos");
                    MostrarMensaje("Error al procesar datos del viaje finalizado.", "danger");
                    CargarDatosNormales();
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"✅ Datos cargados: IdViajeProgreso={datosTransferencia.IdViajeProgreso}");

                // Guardar IDs en HiddenFields
                hfIdConductor.Value = datosTransferencia.IdConductor.ToString();
                hfIdTracto.Value = datosTransferencia.IdTracto.ToString();
                hfIdCarreta.Value = datosTransferencia.IdCarreta.ToString();
                hfIdCliente.Value = datosTransferencia.IdCliente.ToString();
                hfIdViajeProgreso.Value = datosTransferencia.IdViajeProgreso.ToString();

                // ✅ NUEVO: Guardar datos internacionales
                hfEsInternacional.Value = datosTransferencia.EsInternacional.ToString().ToLower();
                hfIdCPIC.Value = datosTransferencia.IdCPIC?.ToString() ?? "0";

                hfOrigenViaje.Value = "viajeFinalizado";

                MostrarPanelesViajeOrigen(datosTransferencia);
                PrecargarCamposDesdeViaje(datosTransferencia);

                if (datosTransferencia.DespachosDetalle != null && datosTransferencia.DespachosDetalle.Count > 0)
                {
                    CargarDespachosViajeOrigen(datosTransferencia.DespachosDetalle);
                }

                System.Diagnostics.Debug.WriteLine("✅ Carga desde viaje finalizado completada");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar datos del viaje finalizado en AgregarOrdenViaje");
                MostrarMensaje("Error al cargar datos del viaje: " + ex.Message, "danger");
                CargarDatosNormales();
            }
        }

        private void MostrarPanelesViajeOrigen(DatosTransferencia datos)
        {
            pnlInfoViajeOrigen.Visible = true;
            lblCantidadDespachosOrigen.Text = datos.CantidadDespachos.ToString();
            lblConductorOrigen.Text = datos.Conductor;

            pnlDetallesViajeOrigen.Visible = true;

            if (datos.TieneVariaciones)
            {
                pnlVariaciones.Visible = true;
                System.Diagnostics.Debug.WriteLine("⚠️ Se detectaron variaciones entre despachos");
            }
        }

        private void OcultarPanelesViajeOrigen()
        {
            pnlInfoViajeOrigen.Visible = false;
            pnlDetallesViajeOrigen.Visible = false;
            pnlVariaciones.Visible = false;
        }

        private void PrecargarCamposDesdeViaje(DatosTransferencia datos)
        {
            try
            {
                txtConductor.Text = datos.Conductor ?? "";
                txtPlacaTracto.Text = datos.PlacaTracto ?? "";
                txtPlacaCarreta.Text = datos.PlacaCarreta ?? "";
                txtClientePrincipal.Text = datos.Cliente ?? "";
                txtTipoOperacion.Text = datos.Operacion ?? "";
                txtPlantaPrincipal.Text = datos.Planta ?? "";

                // ✅ NUEVO: Configurar tipo de viaje
                //txtTipoViaje.Text = datos.EsInternacional ? "INTERNACIONAL" : "NACIONAL";

                // ✅ NUEVO: Si es internacional, mostrar CPIC
                if (datos.EsInternacional && !string.IsNullOrEmpty(datos.NumeroCPIC))
                {
                    pnlMostrarCPIC.Visible = true;
                    txtCPICMostrar.Text = datos.NumeroCPIC;
                    System.Diagnostics.Debug.WriteLine($"✅ CPIC mostrado: {datos.NumeroCPIC}");
                }

                DateTime hoy = DateTime.Today;
                txtFechaSalida.Text = hoy.ToString("yyyy-MM-dd");
                txtFechaLlegada.Text = hoy.AddDays(1).ToString("yyyy-MM-dd");
                txtHoraSalida.Text = "08:00";
                txtHoraLlegada.Text = "18:00";

                System.Diagnostics.Debug.WriteLine("✅ Campos precargados correctamente");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al precargar campos desde el viaje en AgregarOrdenViaje");
                throw;
            }
        }

        private void CargarDespachosViajeOrigen(List<DespachoViaje> despachos)
        {
            try
            {
                if (despachos != null && despachos.Count > 0)
                {
                    gvDespachosViajeOrigen.DataSource = despachos;
                    gvDespachosViajeOrigen.DataBind();
                    System.Diagnostics.Debug.WriteLine($"✅ {despachos.Count} despachos cargados en GridView");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar despachos en GridView en AgregarOrdenViaje");
                throw;
            }
        }

        #endregion

        #region Evento Principal - Guardar Orden Viaje

        protected void btnGuardarOrden_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== INICIANDO GUARDADO DE ORDEN DE VIAJE ===");

                // ✅ DETECTAR MODO EDICIÓN
                int idOrdenExistente = int.TryParse(hfIdOrdenViaje.Value, out int idOrd) ? idOrd : 0;
                bool esEdicion = idOrdenExistente > 0;

                System.Diagnostics.Debug.WriteLine($"Modo: {(esEdicion ? "EDICIÓN" : "CREACIÓN")} | ID: {idOrdenExistente}");

                // 1. Obtener número de orden
                string numeroOrdenViaje = txtNumeroOrdenViaje.Text?.Trim();
                string numeroOrdenViajeOriginal = numeroOrdenViaje; // Guardar formato completo

                // 2. Validar y procesar según modo
                if (!esEdicion)
                {
                    // MODO CREACIÓN: Validar formato de 6 dígitos
                    System.Diagnostics.Debug.WriteLine($"Número recibido (creación): '{numeroOrdenViaje}'");

                    string errorFormato = ValidarFormatoNumeroOrden(numeroOrdenViaje);
                    if (!string.IsNullOrEmpty(errorFormato))
                    {
                        System.Diagnostics.Debug.WriteLine($"Error de formato: {errorFormato}");
                        MostrarMensaje(errorFormato, "danger");
                        return;
                    }

                    if (NumeroOrdenExiste(numeroOrdenViaje))
                    {
                        System.Diagnostics.Debug.WriteLine($"Número ya existe: {numeroOrdenViaje}");
                        MostrarMensaje($"El número {numeroOrdenViaje} ya existe.", "danger");
                        return;
                    }
                }
                else
                {
                    // MODO EDICIÓN: Usar el número completo tal como está
                    System.Diagnostics.Debug.WriteLine($"Editando orden con número: {numeroOrdenViaje}");
                    // NO normalizar - usar el formato completo "OV-2025-000005"
                }

                // 3. Validar datos generales
                System.Diagnostics.Debug.WriteLine("Validando datos generales...");

                DateTime fechaSalida = DateTime.TryParse(txtFechaSalida.Text, out DateTime fs) ? fs : DateTime.MinValue;
                DateTime fechaLlegada = DateTime.TryParse(txtFechaLlegada.Text, out DateTime fl) ? fl : DateTime.MinValue;
                string horaSalida = txtHoraSalida.Text;
                string horaLlegada = txtHoraLlegada.Text;
                string observaciones = txtObservaciones.Text;

                int idConductor = int.TryParse(hfIdConductor.Value, out int ic) ? ic : 0;
                int idTracto = int.TryParse(hfIdTracto.Value, out int it) ? it : 0;
                int idCarreta = int.TryParse(hfIdCarreta.Value, out int icar) ? icar : 0;

                string errores = ValidarDatosGenerales(fechaSalida, fechaLlegada, horaSalida, horaLlegada);
                if (!string.IsNullOrEmpty(errores))
                {
                    System.Diagnostics.Debug.WriteLine($"Errores en datos generales: {errores}");
                    MostrarMensaje(errores.Replace("\n", "<br/>"), "danger");
                    return;
                }

                if (!EsFechaValidaSQL(fechaSalida) || !EsFechaValidaSQL(fechaLlegada))
                {
                    MostrarMensaje("Las fechas son inválidas. Deben estar entre 1753 y 9999.", "danger");
                    return;
                }

                // 4. Procesar en base de datos
                // El code-behind arma el DTO leyendo Request.Form, hidden fields (JSON) y la
                // sesión; el servicio ejecuta la transacción (SQL movido verbatim).
                AgregarOrdenViajeInput input = ConstruirInputGuardado(
                    esEdicion, idOrdenExistente, numeroOrdenViaje,
                    fechaSalida, fechaLlegada, horaSalida, horaLlegada,
                    idConductor, idTracto, idCarreta, observaciones);

                try
                {
                    AgregarOrdenViajeService.GuardarOrden(input);

                    // Registrar auditoría
                    string accion = esEdicion ? "actualizada" : "guardada";
                    AuditoriaHelper.Registrar(
                        esEdicion ? "UPDATE" : "INSERT", "OrdenViaje", numeroOrdenViaje,
                        $"Orden de viaje {accion} - Número: {numeroOrdenViaje}, Conductor ID: {idConductor}, Tracto ID: {idTracto}");

                    // Mostrar resultado
                    MostrarResultadoExitoso(numeroOrdenViaje, accion);

                    // Limpiar HiddenField
                    hfIdOrdenViaje.Value = "0";

                    System.Diagnostics.Debug.WriteLine($"=== ORDEN {accion.ToUpper()} EXITOSAMENTE ===");
                }
                catch (Exception ex)
                {
                    LogSGV.Error(ex, "Error al guardar orden de viaje {Numero} (edicion={EsEdicion})", numeroOrdenViaje, esEdicion);
                    MostrarMensaje($"Error al guardar: {ex.Message}", "danger");
                }
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error del sistema al guardar orden de viaje");
                MostrarMensaje($"Error del sistema: {ex.Message}", "danger");
            }
        }

        /// <summary>
        /// Arma el DTO de guardado leyendo los hidden fields (idCPIC, esInternacional,
        /// origen/viaje), <c>Request.Form</c> (ingresos/egresos/descuentos), los hidden fields
        /// con JSON (adicionales/gastos detallados) y la sesión (id de usuario). El servicio
        /// ejecuta la transacción a partir de este objeto.
        /// </summary>
        private AgregarOrdenViajeInput ConstruirInputGuardado(
            bool esEdicion, int idOrdenExistente, string numeroOrdenViaje,
            DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada,
            int idConductor, int idTracto, int idCarreta, string observaciones)
        {
            int? idCPIC = null;
            if (int.TryParse(hfIdCPIC.Value, out int cpicId) && cpicId > 0)
                idCPIC = cpicId;

            var input = new AgregarOrdenViajeInput
            {
                EsEdicion = esEdicion,
                IdOrdenExistente = idOrdenExistente,
                NumeroOrdenViaje = numeroOrdenViaje,

                FechaSalida = fechaSalida,
                FechaLlegada = fechaLlegada,
                HoraSalida = horaSalida,
                HoraLlegada = horaLlegada,
                IdConductor = idConductor,
                IdTracto = idTracto,
                IdCarreta = idCarreta,
                Observaciones = observaciones,
                IdCPIC = idCPIC,
                EsInternacional = hfEsInternacional.Value == "true",
                IdUsuarioAprobacion = ObtenerIdUsuarioActual(),

                // Ingresos principales (Request.Form)
                DespachoSoles = ParseFormDecimal("despachoSoles"),
                DespachoDolares = ParseFormDecimal("despachoDolares"),
                PrestamoSoles = ParseFormDecimal("prestamoSoles"),
                PrestamoDolares = ParseFormDecimal("prestamoDolares"),
                MensualidadSoles = ParseFormDecimal("mensualidadSoles"),
                MensualidadDolares = ParseFormDecimal("mensualidadDolares"),
                OtrosSoles = ParseFormDecimal("otrosSoles"),
                OtrosDolares = ParseFormDecimal("otrosDolares"),
                DescDespacho = Request.Form["descDespacho"] ?? "",
                DescMensualidad = Request.Form["descMensualidad"] ?? "",
                DescOtros = Request.Form["descOtros"] ?? "",
                DescPrestamo = Request.Form["descPrestamo"] ?? "",

                // Egresos principales (Request.Form)
                PeajesSoles = ParseFormDecimal("peajesSoles"),
                PeajesDolares = ParseFormDecimal("peajesDolares"),
                AlimentacionSoles = ParseFormDecimal("alimentacionSoles"),
                AlimentacionDolares = ParseFormDecimal("alimentacionDolares"),
                ApoyoSeguridadSoles = ParseFormDecimal("apoyoSeguridadSoles"),
                ApoyoSeguridadDolares = ParseFormDecimal("apoyoSeguridadDolares"),
                ReparacionesSoles = ParseFormDecimal("reparacionesSoles"),
                ReparacionesDolares = ParseFormDecimal("reparacionesDolares"),
                MovilidadSoles = ParseFormDecimal("movilidadSoles"),
                MovilidadDolares = ParseFormDecimal("movilidadDolares"),
                EncapadaSoles = ParseFormDecimal("encapadaSoles"),
                EncapadaDolares = ParseFormDecimal("encapadaDolares"),
                HospedajeSoles = ParseFormDecimal("hospedajeSoles"),
                HospedajeDolares = ParseFormDecimal("hospedajeDolares"),
                CombustibleSoles = ParseFormDecimal("combustibleSoles"),
                CombustibleDolares = ParseFormDecimal("combustibleDolares"),
                DescPeajes = Request.Form["descPeajes"] ?? "",
                DescAlimentacion = Request.Form["descAlimentacion"] ?? "",
                DescApoyoSeguridad = Request.Form["descApoyoSeguridad"] ?? "",
                DescReparaciones = Request.Form["descReparaciones"] ?? "",
                DescMovilidad = Request.Form["descMovilidad"] ?? "",
                DescEncapada = Request.Form["descEncapada"] ?? "",
                DescHospedaje = Request.Form["descHospedaje"] ?? "",
                DescCombustible = Request.Form["descCombustible"] ?? "",

                // Descuentos / reintegros (Request.Form)
                DescuentoSoles = ParseFormDecimal("descuentoSoles"),
                DescuentoDolares = ParseFormDecimal("descuentoDolares"),
                ReintegroSoles = ParseFormDecimal("reintegroSoles"),
                ReintegroDolares = ParseFormDecimal("reintegroDolares"),

                // Listas dinámicas (hidden fields con JSON)
                IngresosAdicionales = DeserializarLista<IngresoAdicionalData>(hfIngresosAdicionales.Value),
                GastosAdicionales = DeserializarLista<GastoAdicionalData>(hfGastosAdicionales.Value),
                GastosFinancieros = ObtenerGastosFinancierosDeSession(),

                // Cierre de viaje en progreso (sólo creación desde viaje finalizado)
                OrigenViajeFinalizado = hfOrigenViaje.Value == "viajeFinalizado",
                IdViajeProgreso = int.TryParse(hfIdViajeProgreso.Value, out int ivp) ? ivp : 0
            };

            return input;
        }

        private decimal ParseFormDecimal(string key) =>
            decimal.TryParse(Request.Form[key], out decimal v) ? v : 0;

        private List<T> DeserializarLista<T>(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "[]")
                return new List<T>();
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
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
                LogSGV.Error(ex, "Error al obtener gastos financieros en AgregarOrdenViaje");
                return new List<GastoFinanciero>();
            }
        }


        #endregion

        #region Métodos de Validación

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada)
        {
            string mensajeError = "";

            if (fechaSalida == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Salida'.\n";

            if (string.IsNullOrEmpty(horaSalida))
                mensajeError += "Por favor, seleccione una 'Hora de Salida'.\n";

            if (fechaLlegada == DateTime.MinValue)
                mensajeError += "Por favor, seleccione una 'Fecha de Llegada'.\n";

            if (string.IsNullOrEmpty(horaLlegada))
                mensajeError += "Por favor, seleccione una 'Hora de Llegada'.\n";

            if (fechaSalida != DateTime.MinValue && fechaLlegada != DateTime.MinValue)
            {
                if (fechaSalida > fechaLlegada)
                    mensajeError += "La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.\n";
            }

            return mensajeError;
        }

        private string ValidarFormatoNumeroOrden(string numero) =>
            OrdenViajeValidaciones.ValidarFormatoNumeroOrden(numero);

        private bool NumeroOrdenExiste(string numeroOrden)
        {
            try
            {
                int count = AgregarOrdenViajeService.ContarPorNumero(numeroOrden);
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar el número de orden en AgregarOrdenViaje");
                return true;
            }
        }

        #endregion

        #region Métodos Auxiliares

        private bool EsFechaValidaSQL(DateTime fecha) =>
            OrdenViajeValidaciones.EsFechaValidaSQL(fecha);

        /// <summary>
        /// ✅ NUEVO MÉTODO: Obtiene el ID del usuario actual desde la sesión
        /// </summary>
        private int ObtenerIdUsuarioActual()
        {
            try
            {
                // Primero intenta obtener desde Session["IdUsuario"]
                if (Session["IdUsuario"] != null)
                {
                    if (int.TryParse(Session["IdUsuario"].ToString(), out int idUsuario))
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ ID Usuario obtenido de sesión: {idUsuario}");
                        return idUsuario;
                    }
                }

                // Si no existe en sesión, intenta buscar por nombre de usuario
                string nombreUsuario = ObtenerUsuarioActual();
                if (!string.IsNullOrEmpty(nombreUsuario) && nombreUsuario != "Sistema")
                {
                    int idUsuario = BuscarIdUsuarioPorNombre(nombreUsuario);
                    if (idUsuario > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ ID Usuario obtenido por nombre: {idUsuario}");
                        return idUsuario;
                    }
                }

                // Si no se encuentra, devuelve 0 (se guardará como NULL en DB)
                System.Diagnostics.Debug.WriteLine("⚠️ No se pudo obtener ID de usuario, se usará NULL");
                return 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener el ID de usuario en AgregarOrdenViaje");
                return 0;
            }
        }

        /// <summary>
        /// ✅ NUEVO MÉTODO: Busca el ID del usuario en la base de datos por su nombre
        /// </summary>
        private int BuscarIdUsuarioPorNombre(string nombreUsuario)
        {
            try
            {
                    object result = AgregarOrdenViajeService.BuscarIdUsuarioPorNombre(nombreUsuario);
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }

                return 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al buscar el usuario en AgregarOrdenViaje");
                return 0;
            }
        }

        private string ObtenerUsuarioActual()
        {
            if (Session["Usuario"] != null)
                return Session["Usuario"].ToString();
            else if (User.Identity.IsAuthenticated)
                return User.Identity.Name;
            else
                return "Sistema";
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo} alert-dismissible fade show";
            pnlMensajes.Visible = true;
        }

        private void MostrarResultadoExitoso(string numeroOrdenViaje, string accion = "guardada")
        {
            try
            {
                string mensajeCompleto = $@"¡Orden de viaje {accion} exitosamente!<br/>
            <strong>Número de orden:</strong> {numeroOrdenViaje}<br/>
            <div class='mt-3'>
                <a href='ListaDespachos.aspx' class='btn btn-primary btn-sm me-2'>
                    <i class='fas fa-list'></i> Ver Lista de Despachos
                </a>
                <button type='button' class='btn btn-secondary btn-sm' onclick='location.reload()'>
                    <i class='fas fa-plus'></i> Nueva Orden de Viaje
                </button>
            </div>";

                MostrarMensaje(mensajeCompleto, "success");
                System.Diagnostics.Debug.WriteLine($"Orden {numeroOrdenViaje} {accion} exitosamente");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al mostrar el resultado en AgregarOrdenViaje");
            }
        }

        #endregion

        #region Métodos JSON para JavaScript

        protected string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                    int tablaExiste = AgregarOrdenViajeService.ContarTablaEstacionesPeaje();

                    if (tablaExiste == 0)
                        {
                            var estacionesPrueba = new List<object>
                            {
                                new { idEstacion = 1, nombre = "Peaje Sullana" },
                                new { idEstacion = 2, nombre = "Peaje Piura" },
                                new { idEstacion = 3, nombre = "Peaje Paita" },
                                new { idEstacion = 4, nombre = "Peaje Lambayeque" },
                                new { idEstacion = 5, nombre = "Peaje Chiclayo" },
                                new { idEstacion = 6, nombre = "Peaje Trujillo" },
                                new { idEstacion = 7, nombre = "Peaje Lima Norte" },
                                new { idEstacion = 8, nombre = "Peaje Lima Sur" },
                                new { idEstacion = 9, nombre = "Peaje Huacho" },
                                new { idEstacion = 10, nombre = "Peaje Pucusana" }
                            };
                            return JsonConvert.SerializeObject(estacionesPrueba);
                        }

                    DataTable dt = AgregarOrdenViajeService.ObtenerEstacionesPeaje();
                    var estaciones = new List<object>();

                    foreach (DataRow reader in dt.Rows)
                    {
                        estaciones.Add(new
                        {
                            idEstacion = Convert.ToInt32(reader["idEstacion"]),
                            nombre = reader["nombre"].ToString()
                        });
                    }

                    return JsonConvert.SerializeObject(estaciones);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al obtener estaciones de peaje en AgregarOrdenViaje");

                var estacionesEmergencia = new List<object>
                {
                    new { idEstacion = 1, nombre = "Peaje Sullana" },
                    new { idEstacion = 2, nombre = "Peaje Piura" },
                    new { idEstacion = 3, nombre = "Peaje Paita" }
                };

                return JsonConvert.SerializeObject(estacionesEmergencia);
            }
        }

        #endregion

        #region Método de Inicialización

        private void InicializarSistema()
        {
            try
            {
                string connectionString = DbHelper.ConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    MostrarMensaje("Error: No se encontró la cadena de conexión 'ConexionSGV'", "danger");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Sistema inicializado correctamente");
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al inicializar el sistema en AgregarOrdenViaje");
                MostrarMensaje("Error al inicializar el sistema: " + ex.Message, "danger");
            }
        }

        #endregion
    }
}
