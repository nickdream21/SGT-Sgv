using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class AgregarOrdenViaje : System.Web.UI.Page
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

        /// <summary>
        /// Clase para gastos financieros detallados (de modales)
        /// </summary>
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
            public DateTime Fecha { get; set; }

            [JsonProperty("comprobante")]
            public string Comprobante { get; set; }

            [JsonProperty("soles")]
            public decimal Soles { get; set; }

            [JsonProperty("dolares")]
            public decimal Dolares { get; set; }

            [JsonProperty("observaciones")]
            public string Observaciones { get; set; }
        }

        /// <summary>
        /// Clases para ingresos y gastos adicionales dinámicos
        /// </summary>
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
                System.Diagnostics.Debug.WriteLine($"Error en carga normal: {ex.Message}");
                MostrarMensaje("Error al cargar la página: " + ex.Message, "danger");
            }
        }

        private void CargarDatosOrdenExistente(int idOrdenViaje)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando datos de orden {idOrdenViaje} ---");

                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // ✅ CONSULTA CORREGIDA con nombres exactos de columnas
                    string query = @"
                SELECT 
                    ov.*,
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                    t.placaTracto,
                    ca.placaCarreta,
                    
                    -- Ingresos
                    i.despachoSoles, i.despachoDolares, i.descDespacho,
                    i.prestamoSoles, i.prestamosDolares, i.descPrestamo,
                    i.mensualidadSoles, i.mensualidadDolares, i.descMensualidad,
                    i.otrosSoles, i.otrosDolares, i.descOtrosAutorizados,
                    
                    -- Gastos
                    e.peajesSoles, e.peajesDolares, e.descPeajes,
                    e.alimentacionSoles, e.alimentacionDolares, e.descAlimentacion,
                    e.apoyoseguridadSoles, e.apoyoseguridadDolares, e.descApoyoSeguridad,
                    e.reparacionesVariosSoles, e.repacionesVariosDolares, e.descReparacionesVarios,
                    e.movilidadSoles, e.movilidadDolares, e.descMovilidad,
                    e.encarpada_desencarpadaSoles, e.encarpada_desencarpadaDolares, e.descEncarpadaDesencarpada,
                    e.hospedajeSoles, e.hospedajeDolares, e.descHospedaje,
                    e.combustibleSoles, e.combustibleDolares, e.descCombustible

                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                INNER JOIN Tracto t ON ov.idTracto = t.idTracto
                INNER JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.idOrdenViaje = @idOrdenViaje";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
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

                                reader.Close();

                                // === CARGAR INGRESOS ADICIONALES ===
                                CargarIngresosAdicionalesEdicion(conn, numeroOrdenViaje);

                                // === CARGAR GASTOS ADICIONALES ===
                                CargarGastosAdicionalesEdicion(conn, numeroOrdenViaje);

                                // === CARGAR DATOS DE MODALES ===
                                CargarDatosModalesEdicion(conn, numeroOrdenViaje);

                                System.Diagnostics.Debug.WriteLine($"✅ Datos de orden {idOrdenViaje} cargados completamente");
                                MostrarMensaje($"Orden <strong>{numeroOrdenViaje}</strong> cargada para edición. Modifica los datos necesarios y guarda los cambios.", "info");
                            }
                            else
                            {
                                MostrarMensaje($"No se encontró la orden con ID {idOrdenViaje}", "warning");
                                CargarDatosNormales();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando orden: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                MostrarMensaje($"Error al cargar la orden: {ex.Message}", "danger");
                CargarDatosNormales();
            }
        }



        private void CargarIngresosAdicionalesEdicion(SqlConnection conn, string numeroOrden)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando ingresos adicionales de orden {numeroOrden} ---");

                string query = @"
            SELECT 
                nombreCategoria,
                descripcion,
                soles,
                dolares
            FROM IngresosAdicionales
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY idIngresoAdicional";

                List<IngresoAdicionalData> ingresosAdicionales = new List<IngresoAdicionalData>();

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ingresosAdicionales.Add(new IngresoAdicionalData
                            {
                                Categoria = reader["nombreCategoria"]?.ToString() ?? "",
                                NombreCategoria = reader["nombreCategoria"]?.ToString() ?? "",
                                Descripcion = reader["descripcion"]?.ToString() ?? "",
                                Soles = reader["soles"] != DBNull.Value ? Convert.ToDecimal(reader["soles"]) : 0,
                                Dolares = reader["dolares"] != DBNull.Value ? Convert.ToDecimal(reader["dolares"]) : 0
                            });
                        }
                    }
                }

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
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando ingresos adicionales: {ex.Message}");
            }
        }

        private void CargarGastosAdicionalesEdicion(SqlConnection conn, string numeroOrden)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando gastos adicionales de orden {numeroOrden} ---");

                string query = @"
            SELECT 
                nombreCategoria,
                descripcion,
                soles,
                dolares
            FROM CategoriasAdicionales
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY idCategoriaAdicional";

                List<GastoAdicionalData> gastosAdicionales = new List<GastoAdicionalData>();

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            gastosAdicionales.Add(new GastoAdicionalData
                            {
                                Categoria = reader["nombreCategoria"]?.ToString() ?? "",
                                NombreCategoria = reader["nombreCategoria"]?.ToString() ?? "",
                                Descripcion = reader["descripcion"]?.ToString() ?? "",
                                Soles = reader["soles"] != DBNull.Value ? Convert.ToDecimal(reader["soles"]) : 0,
                                Dolares = reader["dolares"] != DBNull.Value ? Convert.ToDecimal(reader["dolares"]) : 0
                            });
                        }
                    }
                }

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
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando gastos adicionales: {ex.Message}");
            }
        }


        private void CargarDatosModalesEdicion(SqlConnection conn, string numeroOrden)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Cargando datos de modales de orden {numeroOrden} ---");

                List<GastoFinanciero> todosLosGastos = new List<GastoFinanciero>();

                // ========== PEAJES ==========
                string queryPeajes = @"
            SELECT 
                estacion,
                fecha,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetallePeajes
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fecha";

                using (SqlCommand cmd = new SqlCommand(queryPeajes, conn))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        int contador = 0;
                        while (reader.Read())
                        {
                            contador++;
                            todosLosGastos.Add(new GastoFinanciero
                            {
                                Categoria = "peajes",
                                Id = contador,
                                Estacion = reader["estacion"]?.ToString() ?? "",
                                Lugar = reader["estacion"]?.ToString() ?? "",
                                Fecha = reader["fecha"] != DBNull.Value ? Convert.ToDateTime(reader["fecha"]) : DateTime.Now,
                                Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                                Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                                Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                                Observaciones = reader["observaciones"]?.ToString() ?? ""
                            });
                        }
                        if (contador > 0)
                            System.Diagnostics.Debug.WriteLine($"✅ {contador} peajes cargados");
                    }
                }

                // ========== REPARACIONES ==========
                string queryReparaciones = @"
            SELECT 
                fechaComprobante,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetalleReparacionesVarios
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fechaComprobante";

                using (SqlCommand cmd = new SqlCommand(queryReparaciones, conn))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        int contador = 0;
                        while (reader.Read())
                        {
                            contador++;
                            todosLosGastos.Add(new GastoFinanciero
                            {
                                Categoria = "reparaciones",
                                Id = contador,
                                Tipo = reader["observaciones"]?.ToString()?.Split('-')[0]?.Trim() ?? "Reparación",
                                Fecha = reader["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(reader["fechaComprobante"]) : DateTime.Now,
                                Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                                Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                                Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                                Observaciones = reader["observaciones"]?.ToString() ?? ""
                            });
                        }
                        if (contador > 0)
                            System.Diagnostics.Debug.WriteLine($"✅ {contador} reparaciones cargadas");
                    }
                }

                // ========== HOSPEDAJE ==========
                string queryHospedaje = @"
            SELECT 
                fechaComprobante,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetalleHospedaje
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fechaComprobante";

                using (SqlCommand cmd = new SqlCommand(queryHospedaje, conn))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        int contador = 0;
                        while (reader.Read())
                        {
                            contador++;
                            todosLosGastos.Add(new GastoFinanciero
                            {
                                Categoria = "hospedaje",
                                Id = contador,
                                Lugar = reader["observaciones"]?.ToString()?.Split('-')[0]?.Trim() ?? "Hotel",
                                Fecha = reader["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(reader["fechaComprobante"]) : DateTime.Now,
                                Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                                Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                                Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                                Observaciones = reader["observaciones"]?.ToString() ?? ""
                            });
                        }
                        if (contador > 0)
                            System.Diagnostics.Debug.WriteLine($"✅ {contador} hospedajes cargados");
                    }
                }

                // ========== COMBUSTIBLE ==========
                string queryCombustible = @"
            SELECT 
                fechaComprobante,
                numeroComprobante,
                montoSoles,
                montoDolares,
                observaciones
            FROM DetalleCombustible
            WHERE numeroOrdenViaje = @numeroOrden
            ORDER BY fechaComprobante";

                using (SqlCommand cmd = new SqlCommand(queryCombustible, conn))
                {
                    cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        int contador = 0;
                        while (reader.Read())
                        {
                            contador++;
                            todosLosGastos.Add(new GastoFinanciero
                            {
                                Categoria = "combustible",
                                Id = contador,
                                Lugar = reader["observaciones"]?.ToString()?.Split('-')[0]?.Trim() ?? "Grifo",
                                Fecha = reader["fechaComprobante"] != DBNull.Value ? Convert.ToDateTime(reader["fechaComprobante"]) : DateTime.Now,
                                Comprobante = reader["numeroComprobante"]?.ToString() ?? "",
                                Soles = reader["montoSoles"] != DBNull.Value ? Convert.ToDecimal(reader["montoSoles"]) : 0,
                                Dolares = reader["montoDolares"] != DBNull.Value ? Convert.ToDecimal(reader["montoDolares"]) : 0,
                                Observaciones = reader["observaciones"]?.ToString() ?? ""
                            });
                        }
                        if (contador > 0)
                            System.Diagnostics.Debug.WriteLine($"✅ {contador} combustibles cargados");
                    }
                }

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
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando datos de modales: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Error: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Error precargando campos: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando despachos en GridView: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine("Iniciando transacción de base de datos...");
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    System.Diagnostics.Debug.WriteLine("Conexión abierta");

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("Transacción iniciada");

                            if (esEdicion)
                            {
                                // ✅ MODO EDICIÓN
                                System.Diagnostics.Debug.WriteLine($"Actualizando orden {idOrdenExistente} con número {numeroOrdenViaje}...");

                                ActualizarOrdenViaje(conn, transaction, idOrdenExistente, numeroOrdenViaje,
                                    fechaSalida, fechaLlegada, horaSalida, horaLlegada,
                                    idConductor, idTracto, idCarreta, observaciones);

                                // Eliminar datos financieros anteriores
                                System.Diagnostics.Debug.WriteLine($"Eliminando datos financieros anteriores de {numeroOrdenViaje}...");
                                EliminarDatosFinancierosAnteriores(conn, transaction, numeroOrdenViaje);
                            }
                            else
                            {
                                // ✅ MODO CREACIÓN
                                System.Diagnostics.Debug.WriteLine("Insertando nueva orden...");
                                int idOrdenViaje = InsertarOrdenViaje(conn, transaction, numeroOrdenViaje,
                                    fechaSalida, fechaLlegada, horaSalida, horaLlegada,
                                    idConductor, idTracto, idCarreta, observaciones);
                                System.Diagnostics.Debug.WriteLine($"Orden creada: {idOrdenViaje}");
                            }

                            // 5. Insertar datos financieros (con el número COMPLETO)
                            System.Diagnostics.Debug.WriteLine($"Insertando datos financieros con número: {numeroOrdenViaje}");
                            InsertarDatosFinancierosCompletos(conn, transaction, numeroOrdenViaje);
                            InsertarDescuentosReintegros(conn, transaction, numeroOrdenViaje);

                            var gastosFinancieros = ObtenerGastosFinancierosDeSession();
                            if (gastosFinancieros.Count > 0)
                            {
                                InsertarGastosFinancierosDetallados(conn, transaction, numeroOrdenViaje, gastosFinancieros);
                            }

                            // 6. Si viene de viaje finalizado, cerrar el viaje (solo en creación)
                            if (!esEdicion && hfOrigenViaje.Value == "viajeFinalizado")
                            {
                                int idViajeProgreso = int.TryParse(hfIdViajeProgreso.Value, out int ivp) ? ivp : 0;
                                if (idViajeProgreso > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Cerrando viaje en progreso: {idViajeProgreso}");
                                    CerrarViajeProgreso(conn, transaction, idViajeProgreso, numeroOrdenViaje);
                                }
                            }

                            // 7. Commit
                            System.Diagnostics.Debug.WriteLine("Haciendo commit...");
                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine("✅ Commit exitoso");

                            // 8. Registrar auditoría
                            string accion = esEdicion ? "actualizada" : "guardada";
                            AuditoriaHelper.Registrar(
                                esEdicion ? "UPDATE" : "INSERT", "OrdenViaje", numeroOrdenViaje,
                                $"Orden de viaje {accion} - Número: {numeroOrdenViaje}, Conductor ID: {idConductor}, Tracto ID: {idTracto}");

                            // 9. Mostrar resultado
                            MostrarResultadoExitoso(numeroOrdenViaje, accion);

                            // Limpiar HiddenField
                            hfIdOrdenViaje.Value = "0";

                            System.Diagnostics.Debug.WriteLine($"=== ORDEN {accion.ToUpper()} EXITOSAMENTE ===");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ ERROR: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                            transaction.Rollback();
                            MostrarMensaje($"Error al guardar: {ex.Message}", "danger");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR GENERAL: {ex.Message}");
                MostrarMensaje($"Error del sistema: {ex.Message}", "danger");
            }
        }


        private void ActualizarOrdenViaje(
            SqlConnection conn,
            SqlTransaction transaction,
            int idOrdenViaje,
            string numeroOrdenViaje,
            DateTime fechaSalida,
            DateTime fechaLlegada,
            string horaSalida,
            string horaLlegada,
            int idConductor,
            int idTracto,
            int idCarreta,
            string observaciones)
        {
            try
            {
                int? idCPIC = null;
                if (int.TryParse(hfIdCPIC.Value, out int cpicId) && cpicId > 0)
                {
                    idCPIC = cpicId;
                }

                bool esInternacional = hfEsInternacional.Value == "true";
                string tipoViaje = esInternacional ? "INTERNACIONAL" : "NACIONAL";

                // ✅ OBTENER ID DEL USUARIO ACTUAL
                int idUsuarioAprobacion = ObtenerIdUsuarioActual();

                // ✅ QUERY CORREGIDO: Actualiza estado y aprobación automáticamente
                string query = @"
                    UPDATE OrdenViaje 
                    SET fechaSalida = @fechaSalida,
                        horaSalida = @horaSalida,
                        fechaLlegada = @fechaLlegada,
                        horaLlegada = @horaLlegada,
                        idConductor = @idConductor,
                        idTracto = @idTracto,
                        idCarreta = @idCarreta,
                        idCPIC = @idCPIC,
                        observaciones = @observaciones,
                        tipoViaje = @tipoViaje,
                        esInternacional = @esInternacional,
                        estadoViaje = 'COMPLETADO',
                        estadoAprobacion = 'APROBADO',
                        fechaAprobacion = @fechaActual,
                        idUsuarioAprobacion = @idUsuarioAprobacion
                    WHERE idOrdenViaje = @idOrdenViaje";

                using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                    cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(fechaSalida));
                    cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                    cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(fechaLlegada));
                    cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                    cmd.Parameters.AddWithValue("@idConductor", idConductor > 0 ? (object)idConductor : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idTracto", idTracto > 0 ? (object)idTracto : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCarreta", idCarreta > 0 ? (object)idCarreta : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCPIC", idCPIC.HasValue ? (object)idCPIC.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);
                    cmd.Parameters.AddWithValue("@tipoViaje", tipoViaje);
                    cmd.Parameters.AddWithValue("@esInternacional", esInternacional);
                    cmd.Parameters.AddWithValue("@idUsuarioAprobacion", idUsuarioAprobacion > 0 ? (object)idUsuarioAprobacion : DBNull.Value);

                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas == 0)
                    {
                        throw new Exception($"No se pudo actualizar la orden {idOrdenViaje}");
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ Orden {idOrdenViaje} actualizada correctamente");
                    System.Diagnostics.Debug.WriteLine($"✅ Estado: COMPLETADO | Aprobación: APROBADO | Usuario: {idUsuarioAprobacion}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error actualizando orden: {ex.Message}");
                throw;
            }
        }

        private void EliminarDatosFinancierosAnteriores(SqlConnection conn, SqlTransaction transaction, string numeroOrden)
        {
            try
            {
                string[] tablas = {
                    "Ingresos",
                    "Egresos",
                    "IngresosAdicionales",
                    "CategoriasAdicionales",
                    "DescuentosReintegros",
                    "DetallePeajes",
                    "DetalleReparacionesVarios",
                    "DetalleHospedaje",
                    "DetalleCombustible"
                };

                foreach (string tabla in tablas)
                {
                    string query = $"DELETE FROM {tabla} WHERE numeroOrdenViaje = @numeroOrden";
                    using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                        int deleted = cmd.ExecuteNonQuery();
                        if (deleted > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"✓ Eliminados {deleted} registros de {tabla}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error eliminando datos anteriores: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Cierra un viaje en progreso y lo vincula con la orden de viaje creada.
        /// DEBE ejecutarse dentro de una transacción activa.
        /// </summary>
        private void CerrarViajeProgreso(SqlConnection conn, SqlTransaction transaction,
                                          int idViajeProgreso, string numeroOrdenViaje)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"--- Iniciando cierre de viaje {idViajeProgreso} ---");

                // 1. Cerrar el viaje en progreso
                string queryCerrarViaje = @"
                    UPDATE ViajesEnProgreso 
                    SET estadoViaje = 'CERRADO',
                        fechaCierre = @fechaActual
                    WHERE idViajeProgreso = @idViaje 
                        AND estadoViaje = 'ABIERTO'";

                using (SqlCommand cmd = new SqlCommand(queryCerrarViaje, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@idViaje", idViajeProgreso);
                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas == 0)
                    {
                        throw new Exception($"No se pudo cerrar el viaje {idViajeProgreso}. " +
                            "Puede que ya esté cerrado o no exista.");
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Viaje {idViajeProgreso} marcado como CERRADO");
                }

                // 2. Vincular la orden con el viaje
                string queryVincular = @"
                    UPDATE OrdenViaje 
                    SET idViajeProgreso = @idViajeProgreso
                    WHERE numeroOrdenViaje = @numeroOrdenViaje";

                using (SqlCommand cmd = new SqlCommand(queryVincular, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas == 0)
                    {
                        throw new Exception($"No se pudo vincular la orden {numeroOrdenViaje} con el viaje");
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Orden {numeroOrdenViaje} vinculada al viaje {idViajeProgreso}");
                }

                // 3. Actualizar estado de despachos asociados
                string queryDespachos = @"
                    UPDATE Despachos 
                    SET estadoDespacho = CASE 
                        WHEN estadoDespacho = 'PROGRAMADO' THEN 'EN_PROCESO'
                        ELSE estadoDespacho 
                    END,
                    fechaModificacion = @fechaActual
                    WHERE idViajeProgreso = @idViaje 
                        AND activo = 1";

                using (SqlCommand cmd = new SqlCommand(queryDespachos, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@idViaje", idViajeProgreso);
                    int despachosActualizados = cmd.ExecuteNonQuery();

                    System.Diagnostics.Debug.WriteLine($"✓ {despachosActualizados} despachos actualizados a EN_PROCESO");
                }

                System.Diagnostics.Debug.WriteLine($"--- Cierre de viaje {idViajeProgreso} completado ---");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error en CerrarViajeProgreso: {ex.Message}");
                throw new Exception($"Error al cerrar el viaje en progreso: {ex.Message}", ex);
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

        private int InsertarOrdenViaje(
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
            string observaciones
        )
        {
            try
            {
                // ✅ Obtener el IdCPIC que ya viene del despacho
                int? idCPIC = null;
                if (int.TryParse(hfIdCPIC.Value, out int cpicId) && cpicId > 0)
                {
                    idCPIC = cpicId;
                }

                // ✅ Determinar tipo de viaje desde el HiddenField
                bool esInternacional = hfEsInternacional.Value == "true";
                string tipoViaje = esInternacional ? "INTERNACIONAL" : "NACIONAL";

                // ✅ OBTENER ID DEL USUARIO ACTUAL
                int idUsuarioAprobacion = ObtenerIdUsuarioActual();

                // ✅ QUERY CORREGIDO: Incluye campos de aprobación al crear
                string queryOrdenViaje = @"
                    INSERT INTO OrdenViaje (
                        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
                        idConductor, idTracto, idCarreta, idCPIC, observaciones, 
                        estadoViaje, tipoViaje, esInternacional,
                        estadoAprobacion, fechaAprobacion, idUsuarioAprobacion
                    ) 
                    VALUES (
                        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
                        @idConductor, @idTracto, @idCarreta, @idCPIC, @observaciones, 
                        'COMPLETADO', @tipoViaje, @esInternacional,
                        'APROBADO', @fechaActual, @idUsuarioAprobacion
                    );
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(queryOrdenViaje, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@fechaActual", FechaHelper.Ahora());
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(fechaSalida));
                    cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                    cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(fechaLlegada));
                    cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                    cmd.Parameters.AddWithValue("@idConductor", idConductor > 0 ? (object)idConductor : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idTracto", idTracto > 0 ? (object)idTracto : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCarreta", idCarreta > 0 ? (object)idCarreta : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCPIC", idCPIC.HasValue ? (object)idCPIC.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);
                    cmd.Parameters.AddWithValue("@tipoViaje", tipoViaje);
                    cmd.Parameters.AddWithValue("@esInternacional", esInternacional);
                    cmd.Parameters.AddWithValue("@idUsuarioAprobacion", idUsuarioAprobacion > 0 ? (object)idUsuarioAprobacion : DBNull.Value);

                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        int idOrdenViaje = Convert.ToInt32(result);
                        System.Diagnostics.Debug.WriteLine($"✅ OrdenViaje creada: ID={idOrdenViaje}, Estado=COMPLETADO, Aprobación=APROBADO, Tipo={tipoViaje}");
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
            System.Diagnostics.Debug.WriteLine("=== INICIO DATOS FINANCIEROS ===");

            try
            {
                InsertarIngresosPrincipales(conn, transaction, numeroOrdenViaje);
                InsertarGastosPrincipales(conn, transaction, numeroOrdenViaje);
                InsertarIngresosAdicionales(conn, transaction, numeroOrdenViaje);
                InsertarGastosAdicionales(conn, transaction, numeroOrdenViaje);

                System.Diagnostics.Debug.WriteLine("✅ DATOS FINANCIEROS COMPLETADOS");
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
                string queryIngresos = @"
                    INSERT INTO Ingresos (
                        numeroOrdenViaje, despachoSoles, despachoDolares, prestamoSoles, prestamosDolares,
                        mensualidadSoles, mensualidadDolares, otrosSoles, otrosDolares, 
                        totalSoles, totalDolares, descDespacho, descMensualidad, descOtrosAutorizados, descPrestamo
                    )
                    VALUES (
                        @numeroOrdenViaje, @despachoSoles, @despachoDolares, @prestamoSoles, @prestamoDolares,
                        @mensualidadSoles, @mensualidadDolares, @otrosSoles, @otrosDolares,
                        @totalSoles, @totalDolares, @descDespacho, @descMensualidad, @descOtros, @descPrestamo
                    )";

                using (SqlCommand cmd = new SqlCommand(queryIngresos, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@despachoSoles", despachoSoles);
                    cmd.Parameters.AddWithValue("@despachoDolares", despachoDolares);
                    cmd.Parameters.AddWithValue("@prestamoSoles", prestamoSoles);
                    cmd.Parameters.AddWithValue("@prestamoDolares", prestamoDolares);
                    cmd.Parameters.AddWithValue("@mensualidadSoles", mensualidadSoles);
                    cmd.Parameters.AddWithValue("@mensualidadDolares", mensualidadDolares);
                    cmd.Parameters.AddWithValue("@otrosSoles", otrosSoles);
                    cmd.Parameters.AddWithValue("@otrosDolares", otrosDolares);
                    cmd.Parameters.AddWithValue("@totalSoles", despachoSoles + prestamoSoles + mensualidadSoles + otrosSoles);
                    cmd.Parameters.AddWithValue("@totalDolares", despachoDolares + prestamoDolares + mensualidadDolares + otrosDolares);
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

            string queryEgresos = @"
                INSERT INTO Egresos (
                    numeroOrdenViaje, peajesSoles, peajesDolares, descPeajes,
                    alimentacionSoles, alimentacionDolares, descAlimentacion,
                    apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                    reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                    movilidadSoles, movilidadDolares, descMovilidad,
                    encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada,
                    hospedajeSoles, hospedajeDolares, descHospedaje,
                    combustibleSoles, combustibleDolares, descCombustible
                )
                VALUES (
                    @numeroOrdenViaje, @peajesSoles, @peajesDolares, @descPeajes,
                    @alimentacionSoles, @alimentacionDolares, @descAlimentacion,
                    @apoyoSeguridadSoles, @apoyoSeguridadDolares, @descApoyoSeguridad,
                    @reparacionesSoles, @reparacionesDolares, @descReparaciones,
                    @movilidadSoles, @movilidadDolares, @descMovilidad,
                    @encapadaSoles, @encapadaDolares, @descEncapada,
                    @hospedajeSoles, @hospedajeDolares, @descHospedaje,
                    @combustibleSoles, @combustibleDolares, @descCombustible
                )";

            using (SqlCommand cmd = new SqlCommand(queryEgresos, conn, transaction))
            {
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
                // ✅ Leer desde el HiddenField protegido por ViewState MAC
                string ingresosAdicionalesJson = hfIngresosAdicionales.Value ?? "[]";

                if (string.IsNullOrEmpty(ingresosAdicionalesJson) || ingresosAdicionalesJson == "[]")
                    return;

                List<IngresoAdicionalData> ingresosAdicionales = JsonConvert.DeserializeObject<List<IngresoAdicionalData>>(ingresosAdicionalesJson) ?? new List<IngresoAdicionalData>();

                if (ingresosAdicionales.Count > 0)
                {
                    string queryInsert = @"
                INSERT INTO IngresosAdicionales (
                    numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
                ) VALUES (
                    @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
                )";

                    foreach (var ingreso in ingresosAdicionales)
                    {
                        using (SqlCommand cmd = new SqlCommand(queryInsert, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.Categoria ?? ingreso.NombreCategoria ?? "");
                            cmd.Parameters.AddWithValue("@soles", ingreso.Soles ?? 0);
                            cmd.Parameters.AddWithValue("@dolares", ingreso.Dolares ?? 0);
                            cmd.Parameters.AddWithValue("@descripcion", ingreso.Descripcion ?? "");
                            cmd.ExecuteNonQuery();
                        }
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
                // ✅ Leer desde el HiddenField protegido por ViewState MAC
                string gastosAdicionalesJson = hfGastosAdicionales.Value ?? "[]";

                if (string.IsNullOrEmpty(gastosAdicionalesJson) || gastosAdicionalesJson == "[]")
                    return;

                List<GastoAdicionalData> gastosAdicionales = JsonConvert.DeserializeObject<List<GastoAdicionalData>>(gastosAdicionalesJson) ?? new List<GastoAdicionalData>();

                if (gastosAdicionales.Count > 0)
                {
                    string queryInsert = @"
                INSERT INTO CategoriasAdicionales (
                    numeroOrdenViaje, nombreCategoria, soles, dolares, descripcion
                ) VALUES (
                    @numeroOrdenViaje, @nombreCategoria, @soles, @dolares, @descripcion
                )";

                    foreach (var gasto in gastosAdicionales)
                    {
                        using (SqlCommand cmd = new SqlCommand(queryInsert, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@nombreCategoria", gasto.Categoria ?? gasto.NombreCategoria ?? "");
                            cmd.Parameters.AddWithValue("@soles", gasto.Soles ?? 0);
                            cmd.Parameters.AddWithValue("@dolares", gasto.Dolares ?? 0);
                            cmd.Parameters.AddWithValue("@descripcion", gasto.Descripcion ?? "");
                            cmd.ExecuteNonQuery();
                        }
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
                System.Diagnostics.Debug.WriteLine("--- Insertando Descuentos y Reintegros ---");

                // Obtener valores del formulario
                decimal descuentoSoles = decimal.TryParse(Request.Form["descuentoSoles"], out decimal ds) ? ds : 0;
                decimal descuentoDolares = decimal.TryParse(Request.Form["descuentoDolares"], out decimal dd) ? dd : 0;
                decimal reintegroSoles = decimal.TryParse(Request.Form["reintegroSoles"], out decimal rs) ? rs : 0;
                decimal reintegroDolares = decimal.TryParse(Request.Form["reintegroDolares"], out decimal rd) ? rd : 0;

                System.Diagnostics.Debug.WriteLine($"Descuento: S/ {descuentoSoles} | $ {descuentoDolares}");
                System.Diagnostics.Debug.WriteLine($"Reintegro: S/ {reintegroSoles} | $ {reintegroDolares}");

                // ✅ Solo insertar si al menos uno de los valores es diferente de cero
                if (descuentoSoles != 0 || descuentoDolares != 0 || reintegroSoles != 0 || reintegroDolares != 0)
                {
                    string query = @"
                INSERT INTO DescuentosReintegros (
                    numeroOrdenViaje, 
                    descuentoSoles, 
                    descuentoDolares, 
                    reintegroSoles, 
                    reintegroDolares,
                    activo
                )
                VALUES (
                    @numeroOrdenViaje, 
                    @descuentoSoles, 
                    @descuentoDolares,
                    @reintegroSoles, 
                    @reintegroDolares,
                    1
                )";

                    using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@descuentoSoles", descuentoSoles);
                        cmd.Parameters.AddWithValue("@descuentoDolares", descuentoDolares);
                        cmd.Parameters.AddWithValue("@reintegroSoles", reintegroSoles);
                        cmd.Parameters.AddWithValue("@reintegroDolares", reintegroDolares);

                        int filasAfectadas = cmd.ExecuteNonQuery();

                        if (filasAfectadas > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"✅ Descuentos/Reintegros insertados correctamente");
                            System.Diagnostics.Debug.WriteLine($"   Descuento: S/ {descuentoSoles} | $ {descuentoDolares}");
                            System.Diagnostics.Debug.WriteLine($"   Reintegro: S/ {reintegroSoles} | $ {reintegroDolares}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("⚠️ No se insertaron descuentos/reintegros");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No se insertaron descuentos/reintegros (todos los valores son 0)");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error insertando descuentos/reintegros: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                throw new Exception($"Error al insertar descuentos y reintegros: {ex.Message}", ex);
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
                    string categoria = gasto.Categoria?.ToLower() ?? "";  // ✅ Mayúscula

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
            string query = @"
        INSERT INTO DetallePeajes (
            numeroOrdenViaje, estacion, fecha, numeroComprobante, 
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @estacion, @fecha, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
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
            string query = @"
        INSERT INTO DetalleReparacionesVarios (
            numeroOrdenViaje, fechaComprobante, numeroComprobante, 
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
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
            string query = @"
        INSERT INTO DetalleHospedaje (
            numeroOrdenViaje, fechaComprobante, numeroComprobante, 
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
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
            string query = @"
        INSERT INTO DetalleCombustible (
            numeroOrdenViaje, fechaComprobante, numeroComprobante, 
            montoSoles, montoDolares, observaciones
        )
        VALUES (
            @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
            @montoSoles, @montoDolares, @observaciones
        )";

            using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.Fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.Comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.Soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.Dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.Observaciones ?? "");
                cmd.ExecuteNonQuery();
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

        private string ValidarFormatoNumeroOrden(string numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
                return "El número de orden es obligatorio.";

            if (numero.Length != 6)
                return $"El número de orden debe tener exactamente 6 dígitos. Ingresado: {numero.Length} caracteres.";

            if (!numero.All(char.IsDigit))
                return "El número de orden debe contener solo números (0-9).";

            if (numero == "000000")
                return "El número de orden no puede ser '000000'.";

            return string.Empty;
        }

        private bool NumeroOrdenExiste(string numeroOrden)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @numeroOrden";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrden", numeroOrden);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando número de orden: {ex.Message}");
                return true;
            }
        }

        #endregion

        #region Métodos Auxiliares

        private bool EsFechaValidaSQL(DateTime fecha)
        {
            return fecha >= new DateTime(1753, 1, 1) && fecha <= new DateTime(9999, 12, 31) && fecha != DateTime.MinValue;
        }

        private object FechaSeguraSQL(DateTime fecha)
        {
            return EsFechaValidaSQL(fecha) ? (object)fecha : DBNull.Value;
        }

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
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo ID usuario: {ex.Message}");
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
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Ajusta esta query según tu estructura de tabla de usuarios
                    string query = @"
                        SELECT idUsuario 
                        FROM Usuarios 
                        WHERE nombreUsuario = @nombreUsuario 
                           OR usuario = @nombreUsuario
                           OR email = @nombreUsuario";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@nombreUsuario", nombreUsuario);
                        conn.Open();

                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error buscando usuario: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error mostrando resultado: {ex.Message}");
            }
        }

        #endregion

        #region Métodos JSON para JavaScript

        protected string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string queryVerificar = @"
                        SELECT COUNT(*) 
                        FROM INFORMATION_SCHEMA.TABLES 
                        WHERE TABLE_NAME = 'EstacionesPeaje'";

                    using (SqlCommand cmdVerificar = new SqlCommand(queryVerificar, conn))
                    {
                        conn.Open();
                        int tablaExiste = Convert.ToInt32(cmdVerificar.ExecuteScalar());

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
                    }

                    string query = @"
                        SELECT idEstacion, nombre
                        FROM EstacionesPeaje 
                        WHERE activo = 1
                        ORDER BY nombre";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var estaciones = new List<object>();

                            while (reader.Read())
                            {
                                estaciones.Add(new
                                {
                                    idEstacion = Convert.ToInt32(reader["idEstacion"]),
                                    nombre = reader["nombre"].ToString()
                                });
                            }

                            return JsonConvert.SerializeObject(estaciones);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al obtener estaciones de peaje: {ex.Message}");

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
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    MostrarMensaje("Error: No se encontró la cadena de conexión 'ConexionSGV'", "danger");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Sistema inicializado correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inicializando sistema: {ex.Message}");
                MostrarMensaje("Error al inicializar el sistema: " + ex.Message, "danger");
            }
        }

        #endregion
    }
}