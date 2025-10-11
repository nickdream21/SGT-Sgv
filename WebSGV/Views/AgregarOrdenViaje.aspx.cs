using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI.WebControls;

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
            public int IdViajeProgreso { get; set; }  // ✅ AGREGADO - CRÍTICO
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
            public string categoria { get; set; }
            public int id { get; set; }
            public string estacion { get; set; }
            public string lugar { get; set; }
            public string tipo { get; set; }
            public DateTime fecha { get; set; }
            public string comprobante { get; set; }
            public decimal soles { get; set; }
            public decimal dolares { get; set; }
            public string observaciones { get; set; }
        }

        /// <summary>
        /// Clases para ingresos y gastos adicionales dinámicos
        /// </summary>
        public class IngresoAdicionalData
        {
            public string categoria { get; set; }
            public string nombreCategoria { get; set; }
            public string descripcion { get; set; }
            public decimal? soles { get; set; }
            public decimal? dolares { get; set; }
        }

        public class GastoAdicionalData
        {
            public string categoria { get; set; }
            public string nombreCategoria { get; set; }
            public string descripcion { get; set; }
            public decimal? soles { get; set; }
            public decimal? dolares { get; set; }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                InicializarSistema();

                // Verificar origen
                if (Request.QueryString["origen"] == "viajeFinalizado")
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
                hfIdViajeProgreso.Value = datosTransferencia.IdViajeProgreso.ToString();  // ✅ CRÍTICO
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

                // 1. Validar número de orden
                string numeroOrdenViaje = txtNumeroOrdenViaje.Text?.Trim();
                System.Diagnostics.Debug.WriteLine($"Número de orden recibido: '{numeroOrdenViaje}'");

                string errorFormato = ValidarFormatoNumeroOrden(numeroOrdenViaje);
                if (!string.IsNullOrEmpty(errorFormato))
                {
                    System.Diagnostics.Debug.WriteLine($"Error de formato: {errorFormato}");
                    MostrarMensaje(errorFormato, "danger");
                    return;
                }

                // 2. Verificar existencia
                if (NumeroOrdenExiste(numeroOrdenViaje))
                {
                    System.Diagnostics.Debug.WriteLine($"Número ya existe: {numeroOrdenViaje}");
                    MostrarMensaje($"El número {numeroOrdenViaje} ya existe.", "danger");
                    return;
                }

                // 3. Validar datos generales
                System.Diagnostics.Debug.WriteLine("Validando datos generales...");

                DateTime fechaSalida = DateTime.TryParse(txtFechaSalida.Text, out DateTime fs) ? fs : DateTime.MinValue;
                DateTime fechaLlegada = DateTime.TryParse(txtFechaLlegada.Text, out DateTime fl) ? fl : DateTime.MinValue;
                string horaSalida = txtHoraSalida.Text;
                string horaLlegada = txtHoraLlegada.Text;
                string observaciones = txtObservaciones.Text;

                // Obtener IDs de HiddenFields
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

                // 4. Validar fechas SQL
                if (!EsFechaValidaSQL(fechaSalida))
                {
                    MostrarMensaje("La fecha de salida es inválida. Debe estar entre 1753 y 9999.", "danger");
                    return;
                }

                if (!EsFechaValidaSQL(fechaLlegada))
                {
                    MostrarMensaje("La fecha de llegada es inválida. Debe estar entre 1753 y 9999.", "danger");
                    return;
                }

                // 5. Procesar en base de datos
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

                            // 5.1 Insertar orden de viaje
                            System.Diagnostics.Debug.WriteLine("Insertando orden de viaje...");
                            int idOrdenViaje = InsertarOrdenViaje(conn, transaction, numeroOrdenViaje,
                                fechaSalida, fechaLlegada, horaSalida, horaLlegada,
                                idConductor, idTracto, idCarreta, observaciones);
                            System.Diagnostics.Debug.WriteLine($"Orden de viaje insertada: {idOrdenViaje}");

                            // 5.2 Insertar datos financieros completos
                            System.Diagnostics.Debug.WriteLine("Insertando datos financieros...");
                            InsertarDatosFinancierosCompletos(conn, transaction, numeroOrdenViaje);
                            System.Diagnostics.Debug.WriteLine("Datos financieros insertados");

                            // 5.3 Insertar descuentos y reintegros
                            System.Diagnostics.Debug.WriteLine("Insertando descuentos y reintegros...");
                            InsertarDescuentosReintegros(conn, transaction, numeroOrdenViaje);
                            System.Diagnostics.Debug.WriteLine("Descuentos y reintegros insertados");

                            // 5.4 Procesar datos detallados de modales
                            System.Diagnostics.Debug.WriteLine("Procesando datos detallados de modales...");
                            var gastosFinancieros = ObtenerGastosFinancierosDeSession();
                            if (gastosFinancieros.Count > 0)
                            {
                                InsertarGastosFinancierosDetallados(conn, transaction, numeroOrdenViaje, gastosFinancieros);
                            }

                            // ✅ 5.5 NUEVO: Si viene de viaje finalizado, cerrar el viaje DENTRO de esta transacción
                            if (hfOrigenViaje.Value == "viajeFinalizado")
                            {
                                int idViajeProgreso = int.TryParse(hfIdViajeProgreso.Value, out int ivp) ? ivp : 0;

                                if (idViajeProgreso > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"⚡ Cerrando viaje en progreso: {idViajeProgreso}");
                                    CerrarViajeProgreso(conn, transaction, idViajeProgreso, numeroOrdenViaje);
                                    System.Diagnostics.Debug.WriteLine("✅ Viaje cerrado y vinculado exitosamente");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("⚠️ Advertencia: No se encontró ID de viaje para cerrar");
                                }
                            }

                            // 5.6 Commit
                            System.Diagnostics.Debug.WriteLine("Haciendo commit de toda la transacción...");
                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine("✅ Commit exitoso - Todo guardado correctamente");

                            // 6. Mostrar resultado
                            MostrarResultadoExitoso(numeroOrdenViaje);

                            System.Diagnostics.Debug.WriteLine("=== GUARDADO COMPLETADO EXITOSAMENTE ===");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ ERROR EN TRANSACCIÓN: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine("⚠️ Rollback ejecutado - Ningún cambio aplicado");

                            MostrarMensaje($@"Error al guardar: {ex.Message}<br/>
                                <strong>Tipo:</strong> {ex.GetType().Name}<br/>
                                {(ex.InnerException != null ? $"<strong>Error interno:</strong> {ex.InnerException.Message}" : "")}",
                                "danger");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR GENERAL: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                MostrarMensaje($@"Error general del sistema: {ex.Message}<br/>
                    <strong>Tipo:</strong> {ex.GetType().Name}<br/>
                    {(ex.InnerException != null ? $"<strong>Error interno:</strong> {ex.InnerException.Message}" : "")}",
                    "danger");
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
                        fechaCierre = GETDATE()
                    WHERE idViajeProgreso = @idViaje 
                        AND estadoViaje = 'ABIERTO'";

                using (SqlCommand cmd = new SqlCommand(queryCerrarViaje, conn, transaction))
                {
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
                    fechaModificacion = GETDATE()
                    WHERE idViajeProgreso = @idViaje 
                        AND activo = 1";

                using (SqlCommand cmd = new SqlCommand(queryDespachos, conn, transaction))
                {
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

        private int InsertarOrdenViaje(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje,
                                     DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada,
                                     int idConductor, int idTracto, int idCarreta, string observaciones)
        {
            try
            {
                string queryOrdenViaje = @"
                    INSERT INTO OrdenViaje (
                        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
                        idConductor, idTracto, idCarreta, observaciones, estadoViaje, tipoViaje
                    ) 
                    VALUES (
                        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
                        @idConductor, @idTracto, @idCarreta, @observaciones, 'PENDIENTE', 'NACIONAL'
                    );
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(queryOrdenViaje, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(fechaSalida));
                    cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                    cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(fechaLlegada));
                    cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                    cmd.Parameters.AddWithValue("@idConductor", idConductor > 0 ? (object)idConductor : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idTracto", idTracto > 0 ? (object)idTracto : DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCarreta", idCarreta > 0 ? (object)idCarreta : DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);

                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        int idOrdenViaje = Convert.ToInt32(result);
                        System.Diagnostics.Debug.WriteLine($"OrdenViaje creada con ID: {idOrdenViaje}");
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
                System.Diagnostics.Debug.WriteLine($"Error insertando orden de viaje: {ex.Message}");
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
                string ingresosAdicionalesJson = Request.Form["hiddenIngresosAdicionales"] ?? "[]";

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
                            cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.categoria ?? ingreso.nombreCategoria ?? "");
                            cmd.Parameters.AddWithValue("@soles", ingreso.soles ?? 0);
                            cmd.Parameters.AddWithValue("@dolares", ingreso.dolares ?? 0);
                            cmd.Parameters.AddWithValue("@descripcion", ingreso.descripcion ?? "");
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
                string gastosAdicionalesJson = Request.Form["hiddenGastosAdicionales"] ?? "[]";

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
                            cmd.Parameters.AddWithValue("@nombreCategoria", gasto.categoria ?? gasto.nombreCategoria ?? "");
                            cmd.Parameters.AddWithValue("@soles", gasto.soles ?? 0);
                            cmd.Parameters.AddWithValue("@dolares", gasto.dolares ?? 0);
                            cmd.Parameters.AddWithValue("@descripcion", gasto.descripcion ?? "");
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
                decimal descuentoSoles = decimal.TryParse(Request.Form["descuentoSoles"], out decimal ds) ? ds : 0;
                decimal descuentoDolares = decimal.TryParse(Request.Form["descuentoDolares"], out decimal dd) ? dd : 0;
                decimal reintegroSoles = decimal.TryParse(Request.Form["reintegroSoles"], out decimal rs) ? rs : 0;
                decimal reintegroDolares = decimal.TryParse(Request.Form["reintegroDolares"], out decimal rd) ? rd : 0;

                if (descuentoSoles > 0 || descuentoDolares > 0 || reintegroSoles > 0 || reintegroDolares > 0)
                {
                    string query = @"
                        INSERT INTO DescuentosReintegros (
                            numeroOrdenViaje, descuentoSoles, descuentoDolares, 
                            reintegroSoles, reintegroDolares
                        )
                        VALUES (
                            @numeroOrdenViaje, @descuentoSoles, @descuentoDolares,
                            @reintegroSoles, @reintegroDolares
                        )";

                    using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                    {
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
                    string categoria = gasto.categoria?.ToLower() ?? "";

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
                cmd.Parameters.AddWithValue("@estacion", gasto.estacion ?? gasto.lugar ?? "");
                cmd.Parameters.AddWithValue("@fecha", FechaSeguraSQL(gasto.fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.observaciones ?? "");
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
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.observaciones ?? "");
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
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.observaciones ?? "");
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
                cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(gasto.fecha));
                cmd.Parameters.AddWithValue("@numeroComprobante", gasto.comprobante ?? "");
                cmd.Parameters.AddWithValue("@montoSoles", gasto.soles);
                cmd.Parameters.AddWithValue("@montoDolares", gasto.dolares);
                cmd.Parameters.AddWithValue("@observaciones", gasto.observaciones ?? "");
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

        private void MostrarResultadoExitoso(string numeroOrdenViaje)
        {
            try
            {
                string mensajeCompleto = $@"¡Orden de viaje guardada exitosamente!<br/>
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
                System.Diagnostics.Debug.WriteLine($"Orden {numeroOrdenViaje} guardada exitosamente");
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