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
    public partial class DashboardConductor : System.Web.UI.Page
    {
        #region Clases de Datos

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

        /// <summary>
        /// Clase para datos del viaje activo
        /// </summary>
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

        /// <summary>
        /// Clase para despachos
        /// </summary>
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
                VerificarSesion();
                InicializarDashboard();
            }
        }

        #endregion

        #region Métodos de Inicialización

        private void VerificarSesion()
        {
            try
            {
                // Verificar que exista sesión de conductor
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

                // 1. Cargar información del conductor
                CargarDatosConductor();

                // 2. Cargar viaje activo (si existe)
                ViajeActivo viajeActivo = ObtenerViajeActivo();

                if (viajeActivo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Viaje activo encontrado: {viajeActivo.NumeroViajeProgreso}");
                    MostrarViajeActivo(viajeActivo);
                    CargarDespachosViajeActivo(viajeActivo.IdViajeProgreso);
                    HabilitarLiquidacion(viajeActivo);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay viajes activos");
                    MostrarSinViajes();
                }

                // 3. Cargar historial (siempre se muestra)
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
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT 
                            nombre + ' ' + apPaterno + ' ' + ISNULL(apMaterno, '') AS nombreCompleto,
                            DNI
                        FROM Conductor
                        WHERE idConductor = @idConductor";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
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

        #region Métodos de Viaje Activo

        private ViajeActivo ObtenerViajeActivo()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Consulta directa en lugar de SP
                    string query = @"
                        SELECT TOP 1
                            vp.idViajeProgreso,
                            vp.numeroViajeProgreso,
                            vp.idConductor,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                            vp.fechaInicio,
                            vp.fechaUltimaActividad,
                            vp.estadoViaje,
                            vp.descripcionViaje,
                            (SELECT COUNT(*) FROM Despachos WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1) AS cantidadDespachos,
                            CAST(0 AS BIT) AS esInternacional
                        FROM ViajesEnProgreso vp
                        INNER JOIN Conductor c ON vp.idConductor = c.idConductor
                        WHERE vp.idConductor = @idConductor 
                            AND vp.estadoViaje = 'ABIERTO'
                        ORDER BY vp.fechaInicio DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new ViajeActivo
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
                                };
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error obteniendo viaje activo: {ex.Message}");
                return null;
            }
        }

        private void MostrarViajeActivo(ViajeActivo viaje)
        {
            try
            {
                // Mostrar panel de viaje activo
                pnlViajeActivo.Visible = true;
                pnlSinViajes.Visible = false;
                pnlDespachos.Visible = true;

                // Llenar datos del viaje
                lblNumeroViaje.Text = viaje.NumeroViajeProgreso;
                lblFechaInicio.Text = viaje.FechaInicio.ToString("dd/MM/yyyy HH:mm");
                lblCantidadDespachos.Text = viaje.CantidadDespachos.ToString();

                // Calcular días en ruta
                TimeSpan diasRuta = DateTime.Now - viaje.FechaInicio;
                lblDiasRuta.Text = $"{diasRuta.Days} días";

                // Actualizar estado
                lblEstadoViaje.Text = $"Viaje Activo - {viaje.CantidadDespachos} despacho(s)";
                pnlEstadoViaje.CssClass = "badge-status badge badge-success";

                // Guardar ID del viaje en HiddenField
                hfIdViajeActivo.Value = viaje.IdViajeProgreso.ToString();

                // Mostrar badge de liquidación pendiente
                pnlBadgeLiquidar.Visible = true;
                pnlBadgeActivos.Visible = true;
                lblCantidadActivos.Text = viaje.CantidadDespachos.ToString();

                System.Diagnostics.Debug.WriteLine($"✅ Viaje activo mostrado: {viaje.NumeroViajeProgreso}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error mostrando viaje activo: {ex.Message}");
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

        private void CargarDespachosViajeActivo(int idViajeProgreso)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                List<DespachoInfo> despachos = new List<DespachoInfo>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // ✅ CONSULTA CORREGIDA CON NOMBRES REALES DE COLUMNAS
                    string query = @"
                SELECT 
                    d.idDespacho,
                    d.numeroDespacho,
                    d.fechaDespacho,
                    cl.nombre AS nombreCliente,
                    d.tipoOperacion,
                    d.lugarOperacion,
                    t.placaTracto,
                    ca.placaCarreta,
                    d.estadoDespacho,
                    ISNULL(d.guiaRemitente, '') AS guiaRemitente,
                    ISNULL(d.guiaTransportista, '') AS guiaTransportista,
                    ISNULL(CAST(d.idCPIC AS VARCHAR), '') AS numeroCPIC
                FROM Despachos d
                INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                INNER JOIN Tracto t ON d.idTracto = t.idTracto
                INNER JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                WHERE d.idViajeProgreso = @idViajeProgreso
                    AND d.activo = 1
                ORDER BY d.fechaDespacho DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
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

                System.Diagnostics.Debug.WriteLine($"✅ {despachos.Count} despachos cargados en GridView");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error cargando despachos: {ex.Message}");
                throw;
            }
        }

        private void HabilitarLiquidacion(ViajeActivo viaje)
        {
            try
            {
                // Habilitar formulario de liquidación
                pnlSinViajeParaLiquidar.Visible = false;
                pnlFormularioLiquidacion.Visible = true;

                // Llenar resumen del viaje
                lblNumeroViajeResumen.Text = viaje.NumeroViajeProgreso;
                lblFechaInicioResumen.Text = viaje.FechaInicio.ToString("dd/MM/yyyy");
                lblDespachosResumen.Text = viaje.CantidadDespachos.ToString();

                // Configurar fechas por defecto
                DateTime hoy = DateTime.Today;
                txtFechaSalida.Text = viaje.FechaInicio.ToString("yyyy-MM-dd");
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

                // 1. Obtener viaje activo
                int idViajeProgreso = int.TryParse(hfIdViajeActivo.Value, out int ivp) ? ivp : 0;

                if (idViajeProgreso == 0)
                {
                    MostrarMensaje("No se encontró un viaje activo para liquidar.", "warning");
                    return;
                }

                // 2. Generar número de orden automático
                string numeroOrdenViaje = GenerarNumeroOrden();
                System.Diagnostics.Debug.WriteLine($"Número de orden generado: {numeroOrdenViaje}");

                // 3. Validar datos generales
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

                // 4. Obtener datos del viaje para idConductor, idTracto, idCarreta
                var datosViaje = ObtenerDatosViajeParaLiquidacion(idViajeProgreso);
                if (datosViaje == null)
                {
                    MostrarMensaje("Error al obtener los datos del viaje.", "danger");
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

                            // 5.1 Insertar orden de viaje (ESTADO PENDIENTE)
                            System.Diagnostics.Debug.WriteLine("Insertando orden de viaje con estado PENDIENTE...");
                            int idOrdenViaje = InsertarOrdenViajeConductor(
                                conn, transaction, numeroOrdenViaje,
                                fechaSalida, fechaLlegada, horaSalida, horaLlegada,
                                datosViaje.IdConductor, datosViaje.IdTracto, datosViaje.IdCarreta,
                                observaciones, idViajeProgreso
                            );
                            System.Diagnostics.Debug.WriteLine($"Orden de viaje insertada: {idOrdenViaje}");

                            // 5.2 Insertar datos financieros completos
                            System.Diagnostics.Debug.WriteLine("Insertando datos financieros...");
                            InsertarDatosFinancierosCompletos(conn, transaction, numeroOrdenViaje);

                            // 5.3 Insertar descuentos y reintegros
                            System.Diagnostics.Debug.WriteLine("Insertando descuentos y reintegros...");
                            InsertarDescuentosReintegros(conn, transaction, numeroOrdenViaje);

                            // 5.4 Procesar datos detallados de modales
                            System.Diagnostics.Debug.WriteLine("Procesando datos detallados de modales...");
                            var gastosFinancieros = ObtenerGastosFinancierosDeSession();
                            if (gastosFinancieros.Count > 0)
                            {
                                InsertarGastosFinancierosDetallados(conn, transaction, numeroOrdenViaje, gastosFinancieros);
                            }

                            // 5.5 Cerrar el viaje en progreso
                            System.Diagnostics.Debug.WriteLine($"⚡ Cerrando viaje en progreso: {idViajeProgreso}");
                            CerrarViajeProgreso(conn, transaction, idViajeProgreso, numeroOrdenViaje);

                            // 5.6 Commit
                            System.Diagnostics.Debug.WriteLine("Haciendo commit de toda la transacción...");
                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine("✅ Commit exitoso");

                            // 6. Mostrar resultado exitoso
                            MostrarResultadoExitoso(numeroOrdenViaje);

                            // 7. Recargar el dashboard
                            System.Threading.Thread.Sleep(2000); // Esperar 2 segundos
                            Response.Redirect(Request.RawUrl); // Recargar la página

                            System.Diagnostics.Debug.WriteLine("=== LIQUIDACIÓN ENVIADA EXITOSAMENTE ===");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ ERROR EN TRANSACCIÓN: {ex.Message}");
                            transaction.Rollback();
                            System.Diagnostics.Debug.WriteLine("⚠️ Rollback ejecutado");

                            MostrarMensaje($"Error al enviar la liquidación: {ex.Message}", "danger");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR GENERAL: {ex.Message}");
                MostrarMensaje($"Error general: {ex.Message}", "danger");
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

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
                string queryOrdenViaje = @"
                    INSERT INTO OrdenViaje (
                        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
                        idConductor, idTracto, idCarreta, observaciones, 
                        estadoViaje, tipoViaje, idViajeProgreso,
                        registradoPor, idUsuarioRegistro, estadoAprobacion, fechaRegistro
                    ) 
                    VALUES (
                        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
                        @idConductor, @idTracto, @idCarreta, @observaciones, 
                        'PENDIENTE', 'NACIONAL', @idViajeProgreso,
                        'CONDUCTOR', @idUsuarioRegistro, 'PENDIENTE', GETDATE()
                    );
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(queryOrdenViaje, conn, transaction))
                {
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
                        System.Diagnostics.Debug.WriteLine($"✅ OrdenViaje creada por CONDUCTOR: ID={idOrdenViaje}, Estado=PENDIENTE, EstadoAprobacion=PENDIENTE");
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

        private void CerrarViajeProgreso(SqlConnection conn, SqlTransaction transaction, int idViajeProgreso, string numeroOrdenViaje)
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
                        throw new Exception($"No se pudo cerrar el viaje {idViajeProgreso}. Puede que ya esté cerrado o no exista.");
                    }

                    System.Diagnostics.Debug.WriteLine($"✓ Viaje {idViajeProgreso} marcado como CERRADO");
                }

                // 2. Actualizar estado de despachos asociados
                string queryDespachos = @"
                    UPDATE Despachos 
                    SET estadoDespacho = 'COMPLETADO',
                        fechaModificacion = GETDATE()
                    WHERE idViajeProgreso = @idViaje 
                        AND activo = 1";

                using (SqlCommand cmd = new SqlCommand(queryDespachos, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@idViaje", idViajeProgreso);
                    int despachosActualizados = cmd.ExecuteNonQuery();

                    System.Diagnostics.Debug.WriteLine($"✓ {despachosActualizados} despachos actualizados a COMPLETADO");
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

        #region Métodos de Historial

        protected void btnBuscarHistorial_Click(object sender, EventArgs e)
        {
            CargarHistorialLiquidaciones();
        }

        private void CargarHistorialLiquidaciones()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT 
                            ov.idOrdenViaje,
                            ov.numeroOrdenViaje,
                            ov.fechaSalida,
                            ov.fechaLlegada,
                            ov.estadoAprobacion,
                            ISNULL(ing.totalSoles, 0) AS totalIngresosSoles,
                            ISNULL(ing.totalDolares, 0) AS totalIngresosDolares,
                            (ISNULL(eg.peajesSoles, 0) + ISNULL(eg.alimentacionSoles, 0) + 
                             ISNULL(eg.apoyoseguridadSoles, 0) + ISNULL(eg.reparacionesVariosSoles, 0) + 
                             ISNULL(eg.movilidadSoles, 0) + ISNULL(eg.encarpada_desencarpadaSoles, 0) + 
                             ISNULL(eg.hospedajeSoles, 0) + ISNULL(eg.combustibleSoles, 0)) AS totalGastosSoles,
                            (ISNULL(eg.peajesDolares, 0) + ISNULL(eg.alimentacionDolares, 0) + 
                             ISNULL(eg.apoyoseguridadDolares, 0) + ISNULL(eg.repacionesVariosDolares, 0) + 
                             ISNULL(eg.movilidadDolares, 0) + ISNULL(eg.encarpada_desencarpadaDolares, 0) + 
                             ISNULL(eg.hospedajeDolares, 0) + ISNULL(eg.combustibleDolares, 0)) AS totalGastosDolares
                        FROM OrdenViaje ov
                        LEFT JOIN Ingresos ing ON ov.numeroOrdenViaje = ing.numeroOrdenViaje
                        LEFT JOIN Egresos eg ON ov.numeroOrdenViaje = eg.numeroOrdenViaje
                        WHERE ov.idConductor = @idConductor
                            AND ov.registradoPor = 'CONDUCTOR'
                        ORDER BY ov.fechaRegistro DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idConductor", IdConductorActual);
                        conn.Open();

                        DataTable dt = new DataTable();
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            da.Fill(dt);
                        }

                        // Mapear columnas para el GridView
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

                            string estado = "PENDIENTE";
                            if (row["estadoAprobacion"] != DBNull.Value)
                            {
                                estado = row["estadoAprobacion"].ToString();
                            }

                            DataRow newRow = dtMapeado.NewRow();
                            newRow["IdOrdenViaje"] = row["idOrdenViaje"];
                            newRow["NumeroViaje"] = row["numeroOrdenViaje"];
                            newRow["FechaSalida"] = row["fechaSalida"];
                            newRow["FechaLlegada"] = row["fechaLlegada"];
                            newRow["CantidadDespachos"] = 0; // Por calcular
                            newRow["BalanceSoles"] = balanceSoles;
                            newRow["BalanceDolares"] = balanceDolares;
                            newRow["Estado"] = estado;
                            dtMapeado.Rows.Add(newRow);
                        }

                        gvHistorial.DataSource = dtMapeado;
                        gvHistorial.DataBind();

                        lblTotalHistorial.Text = $"{dtMapeado.Rows.Count} registros";

                        System.Diagnostics.Debug.WriteLine($"✅ Historial cargado: {dtMapeado.Rows.Count} registros");
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
            // Implementar acciones si es necesario
        }

        #endregion

        #region Métodos Auxiliares

        /// <summary>
        /// Método para obtener estaciones de peaje en formato JSON (llamado desde JavaScript)
        /// </summary>
        public string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                List<EstacionPeaje> estaciones = new List<EstacionPeaje>();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT DISTINCT estacion AS nombre FROM DetallePeajes WHERE estacion IS NOT NULL AND estacion != '' ORDER BY estacion";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
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

                return JsonConvert.SerializeObject(estaciones);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo estaciones: {ex.Message}");
                return "[]";
            }
        }

        private string GenerarNumeroOrden()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string nuevoNumero = "";
                int intentos = 0;
                int maxIntentos = 5;

                while (intentos < maxIntentos)
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        using (SqlTransaction transaction = conn.BeginTransaction(System.Data.IsolationLevel.Serializable))
                        {
                            try
                            {
                                // Obtener el último número
                                string queryMax = @"
                            SELECT TOP 1 numeroOrdenViaje 
                            FROM OrdenViaje WITH (TABLOCKX)
                            WHERE numeroOrdenViaje LIKE 'OV-' + CAST(YEAR(GETDATE()) AS VARCHAR) + '-%'
                            ORDER BY numeroOrdenViaje DESC";

                                string ultimoNumero = null;
                                using (SqlCommand cmdMax = new SqlCommand(queryMax, conn, transaction))
                                {
                                    object result = cmdMax.ExecuteScalar();
                                    ultimoNumero = result?.ToString();
                                }

                                int siguienteSecuencial = 1;

                                if (!string.IsNullOrEmpty(ultimoNumero))
                                {
                                    // Formato esperado: OV-2025-000001
                                    string[] partes = ultimoNumero.Split('-');
                                    if (partes.Length == 3)
                                    {
                                        if (int.TryParse(partes[2], out int numeroActual))
                                        {
                                            siguienteSecuencial = numeroActual + 1;
                                        }
                                    }
                                }

                                // Generar nuevo número con formato OV-YYYY-NNNNNN
                                int anioActual = DateTime.Now.Year;
                                nuevoNumero = $"OV-{anioActual}-{siguienteSecuencial:D6}";

                                // Verificar que no exista (por seguridad)
                                string queryVerificar = "SELECT COUNT(*) FROM OrdenViaje WHERE numeroOrdenViaje = @numero";
                                using (SqlCommand cmdVerificar = new SqlCommand(queryVerificar, conn, transaction))
                                {
                                    cmdVerificar.Parameters.AddWithValue("@numero", nuevoNumero);
                                    int existe = Convert.ToInt32(cmdVerificar.ExecuteScalar());

                                    if (existe > 0)
                                    {
                                        // Si existe, incrementar y reintentar
                                        siguienteSecuencial++;
                                        nuevoNumero = $"OV-{anioActual}-{siguienteSecuencial:D6}";
                                    }
                                }

                                transaction.Commit();
                                System.Diagnostics.Debug.WriteLine($"✅ Número de orden generado: {nuevoNumero}");
                                return nuevoNumero;
                            }
                            catch (Exception ex)
                            {
                                transaction.Rollback();
                                System.Diagnostics.Debug.WriteLine($"⚠️ Intento {intentos + 1} fallido: {ex.Message}");
                                intentos++;

                                if (intentos >= maxIntentos)
                                {
                                    throw;
                                }

                                // Esperar un poco antes de reintentar
                                System.Threading.Thread.Sleep(100 * intentos);
                            }
                        }
                    }
                }

                // Si todos los intentos fallan, usar fallback
                throw new Exception("No se pudo generar número de orden después de varios intentos");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error generando número de orden: {ex.Message}");

                // Fallback: usar timestamp para garantizar unicidad
                string fallbackNumero = $"OV-{DateTime.Now:yyyyMMddHHmmss}-{IdConductorActual:D4}";
                System.Diagnostics.Debug.WriteLine($"⚠️ Usando número de orden fallback: {fallbackNumero}");
                return fallbackNumero;
            }
        }

        private dynamic ObtenerDatosViajeParaLiquidacion(int idViajeProgreso)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT TOP 1
                            d.idConductor,
                            d.idTracto,
                            d.idCarreta
                        FROM Despachos d
                        WHERE d.idViajeProgreso = @idViajeProgreso
                            AND d.activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idViajeProgreso", idViajeProgreso);
                        conn.Open();

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new
                                {
                                    IdConductor = Convert.ToInt32(reader["idConductor"]),
                                    IdTracto = Convert.ToInt32(reader["idTracto"]),
                                    IdCarreta = Convert.ToInt32(reader["idCarreta"])
                                };
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo datos del viaje: {ex.Message}");
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
                        Recibirás una notificación cuando sea aprobada o si requiere correcciones.
                    </div>";

                MostrarMensaje(mensajeCompleto, "success");
                System.Diagnostics.Debug.WriteLine($"Liquidación {numeroOrdenViaje} enviada por conductor");
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
    }
}