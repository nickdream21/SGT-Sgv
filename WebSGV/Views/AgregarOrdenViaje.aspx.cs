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
        /// Clase principal para liquidaciones completas
        /// </summary>
        public class LiquidacionData
        {
            public int numeroLiquidacion { get; set; }
            public string tipo { get; set; } // NACIONAL, INTERNACIONAL, MIXTO
            public string descripcion { get; set; }
            public string observaciones { get; set; }
            public List<SubTramoData> subTramos { get; set; } = new List<SubTramoData>();
        }

        /// <summary>
        /// Clase para sub-tramos individuales
        /// </summary>
        public class SubTramoData
        {
            public string numeroSubTramo { get; set; }
            public string origen { get; set; }
            public string destino { get; set; }
            public string tipoOperacion { get; set; }
            public string observaciones { get; set; }

            // Información de guías por sub-tramo
            public string guiaTransportista { get; set; }
            public string guiaCliente { get; set; }
            public bool cruzaFrontera { get; set; }
            public string manifiesto { get; set; }

            // Para paradas operativas
            public ParadaData parada { get; set; }

            // Operaciones específicas
            public OperacionData operacionCarga { get; set; }
            public OperacionData operacionDescarga { get; set; }
        }

        /// <summary>
        /// Clase para operaciones de carga/descarga
        /// </summary>
        public class OperacionData
        {
            public bool activa { get; set; }
            public int idCliente { get; set; }
            public int? idCPIC { get; set; }
            public int? idFactura { get; set; }
            public bool esInternacional { get; set; }
            public string observaciones { get; set; }

            // Campos para plantas
            public int? idPlantaCarga { get; set; }
            public int? idPlantaDescarga { get; set; }

            public List<ProductoOperacion> productos { get; set; } = new List<ProductoOperacion>();
        }


        public class GastoAdicionalData
        {
            public string categoria { get; set; }
            public string nombreCategoria { get; set; }
            public string descripcion { get; set; }
            public decimal? soles { get; set; }
            public decimal? dolares { get; set; }
        }


        public class IngresoAdicionalData
        {
            public string categoria { get; set; }
            public string nombreCategoria { get; set; }
            public string descripcion { get; set; }
            public decimal? soles { get; set; }
            public decimal? dolares { get; set; }

            // ✅ ALTERNATIVA: Si tu JavaScript usa nombres diferentes
            public string concepto { get; set; } // Mapea a categoria
        }


        /// <summary>
        /// Clase para productos de operaciones
        /// </summary>
        public class ProductoOperacion
        {
            public int idProducto { get; set; }
            public int cantidad { get; set; }
            public decimal peso { get; set; } = 0;
        }

        /// <summary>
        /// Clase para paradas operativas
        /// </summary>
        public class ParadaData
        {
            public string motivo { get; set; }
            public int duracion { get; set; }
        }

        /// <summary>
        /// Clases para liquidación financiera
        /// </summary>
        public class GastoAdicional
        {
            public string categoria { get; set; }
            public string nombreCategoria { get; set; }
            public decimal soles { get; set; }
            public decimal dolares { get; set; }
            public string descripcion { get; set; }
        }

        public class IngresoAdicional
        {
            public string categoria { get; set; }
            public string nombreCategoria { get; set; }
            public decimal soles { get; set; }
            public decimal dolares { get; set; }
            public string descripcion { get; set; }
        }

        /// <summary>
        /// Clase para peajes detallados
        /// </summary>
        public class PeajeDetallado
        {
            public int id { get; set; }
            public string estacion { get; set; }
            public DateTime fecha { get; set; }
            public string comprobante { get; set; }
            public decimal soles { get; set; }
            public decimal dolares { get; set; }
            public string observaciones { get; set; }
            public DateTime fechaCreacion { get; set; }
        }

        /// <summary>
        /// Clase base para categorías de gastos detallados
        /// </summary>
        public class CategoriaGastoDetallado
        {
            public int id { get; set; }
            public DateTime fechaComprobante { get; set; }
            public string numeroComprobante { get; set; }
            public decimal montoSoles { get; set; }
            public decimal montoDolares { get; set; }
            public string observaciones { get; set; }
            public DateTime fechaCreacion { get; set; }
        }

        /// <summary>
        /// Clases específicas para cada categoría de gastos
        /// </summary>
        public class AlimentacionDetallada : CategoriaGastoDetallado { }
        public class ApoyoSeguridadDetallada : CategoriaGastoDetallado { }
        public class ReparacionesDetalladas : CategoriaGastoDetallado { }
        public class MovilidadDetallada : CategoriaGastoDetallado { }
        public class EncapadaDetallada : CategoriaGastoDetallado { }
        public class HospedajeDetallado : CategoriaGastoDetallado { }
        public class CombustibleDetallado : CategoriaGastoDetallado { }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                InicializarSistema();
                CargarConductores();
                CargarPlacasTracto();
                CargarPlacasCarreta();

                // NUEVA FUNCIONALIDAD: Verificar si viene desde despacho
                if (Request.QueryString["origen"] == "despacho")
                {
                    PreLlenarDesdeDespacho();
                }
            }
            else
            {
                RestaurarDatosPreservados();
            }
        }

        #endregion


        private void PreLlenarDesdeDespacho()
        {
            try
            {
                int idDespacho = Convert.ToInt32(Request.QueryString["idDespacho"] ?? "0");
                int idConductor = Convert.ToInt32(Request.QueryString["idConductor"] ?? "0");
                int idTracto = Convert.ToInt32(Request.QueryString["idTracto"] ?? "0");
                int idCarreta = Convert.ToInt32(Request.QueryString["idCarreta"] ?? "0");

                string conductor = Request.QueryString["conductor"] ?? "";
                string tracto = Request.QueryString["tracto"] ?? "";
                string carreta = Request.QueryString["carreta"] ?? "";

                if (idDespacho > 0 && idConductor > 0 && idTracto > 0 && idCarreta > 0)
                {
                    // Verificar que el despacho sigue en estado PROGRAMADO
                    if (!VerificarDespachoDisponible(idDespacho))
                    {
                        lblErrores.Text = @"<div class='alert alert-warning'>
                    <strong>Advertencia:</strong> El despacho ya no está disponible o cambió de estado.
                    <br/>Los datos se cargarán pero no se actualizará el despacho al guardar.
                </div>";
                        return;
                    }

                    // Pre-seleccionar valores en los dropdowns
                    if (ddlConductor.Items.FindByValue(idConductor.ToString()) != null)
                    {
                        ddlConductor.SelectedValue = idConductor.ToString();
                        ddlConductor.Enabled = false; // Bloquear para evitar cambios
                    }

                    if (ddlPlacaTracto.Items.FindByValue(idTracto.ToString()) != null)
                    {
                        ddlPlacaTracto.SelectedValue = idTracto.ToString();
                        ddlPlacaTracto.Enabled = false;
                    }

                    if (ddlPlacaCarreta.Items.FindByValue(idCarreta.ToString()) != null)
                    {
                        ddlPlacaCarreta.SelectedValue = idCarreta.ToString();
                        ddlPlacaCarreta.Enabled = false;
                    }

                    // Guardar referencia al despacho en ViewState para usar al guardar
                    ViewState["IdDespachoOrigen"] = idDespacho;
                    ViewState["VieneDesdeDespaco"] = true;

                    // Mostrar mensaje informativo
                    lblErrores.Text = $@"<div class='alert alert-info'>
                <i class='fas fa-info-circle'></i>
                <strong>Datos cargados desde despacho:</strong>
                <br/>• Conductor: {conductor}
                <br/>• Tracto: {tracto}
                <br/>• Carreta: {carreta}
                <br/><small class='text-muted'>Los campos están bloqueados para mantener consistencia.</small>
            </div>";

                    System.Diagnostics.Debug.WriteLine($"Datos pre-llenados desde despacho {idDespacho}");
                }
                else
                {
                    lblErrores.Text = @"<div class='alert alert-danger'>
                Error: Faltan parámetros del despacho. Verifique el enlace.
            </div>";
                }
            }
            catch (Exception ex)
            {
                lblErrores.Text = "Error al cargar datos del despacho: " + ex.Message;
                System.Diagnostics.Debug.WriteLine($"Error en PreLlenarDesdeDespacho: {ex.Message}");
            }
        }

        private bool VerificarDespachoDisponible(int idDespacho)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT estadoDespacho FROM Despachos WHERE idDespacho = @idDespacho AND activo = 1";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idDespacho", idDespacho);
                        conn.Open();

                        object resultado = cmd.ExecuteScalar();
                        string estado = resultado?.ToString();

                        return estado == "PROGRAMADO";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando despacho: {ex.Message}");
                return false;
            }
        }

        #region Métodos de Carga de Datos

        private void CargarConductores()
        {
            try
            {
                string query = "SELECT idConductor, CONCAT(nombre, ' ', apPaterno, ' ', apMaterno) AS nombreCompleto FROM Conductor ORDER BY nombreCompleto";
                DataTable dt = ObtenerDatosDeBD(query);

                if (dt.Rows.Count > 0)
                {
                    ddlConductor.DataSource = dt;
                    ddlConductor.DataTextField = "nombreCompleto";
                    ddlConductor.DataValueField = "idConductor";
                    ddlConductor.DataBind();
                }

                ddlConductor.Items.Insert(0, new ListItem("Seleccione un conductor", ""));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando conductores: {ex.Message}");
                lblErrores.Text = "Error al cargar conductores: " + ex.Message;
            }
        }

        private void CargarPlacasTracto()
        {
            try
            {
                string query = "SELECT idTracto, placaTracto FROM Tracto ORDER BY placaTracto";
                DataTable dt = ObtenerDatosDeBD(query);

                if (dt.Rows.Count > 0)
                {
                    ddlPlacaTracto.DataSource = dt;
                    ddlPlacaTracto.DataTextField = "placaTracto";
                    ddlPlacaTracto.DataValueField = "idTracto";
                    ddlPlacaTracto.DataBind();
                }

                ddlPlacaTracto.Items.Insert(0, new ListItem("Seleccione una placa", ""));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando placas tracto: {ex.Message}");
                lblErrores.Text = "Error al cargar placas de tracto: " + ex.Message;
            }
        }

        private void CargarPlacasCarreta()
        {
            try
            {
                string query = "SELECT idCarreta, placaCarreta FROM Carreta ORDER BY placaCarreta";
                DataTable dt = ObtenerDatosDeBD(query);

                if (dt.Rows.Count > 0)
                {
                    ddlPlacaCarreta.DataSource = dt;
                    ddlPlacaCarreta.DataTextField = "placaCarreta";
                    ddlPlacaCarreta.DataValueField = "idCarreta";
                    ddlPlacaCarreta.DataBind();
                }

                ddlPlacaCarreta.Items.Insert(0, new ListItem("Seleccione una placa", ""));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando placas carreta: {ex.Message}");
                lblErrores.Text = "Error al cargar placas de carreta: " + ex.Message;
            }
        }

        #endregion

        #region Métodos JSON para JavaScript

        protected string ObtenerProductosJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT 
                    p.idProducto,
                    p.nombre,
                    p.idCliente,
                    c.nombre as nombreCliente,
                    ISNULL(c.ruc, '') as ruc
                FROM Producto p
                INNER JOIN Cliente c ON p.idCliente = c.idCliente
                ORDER BY c.nombre, p.nombre";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var productos = new List<object>();

                            while (reader.Read())
                            {
                                productos.Add(new
                                {
                                    idProducto = Convert.ToInt32(reader["idProducto"]),
                                    nombre = reader["nombre"].ToString(),
                                    idCliente = Convert.ToInt32(reader["idCliente"]),
                                    nombreCliente = reader["nombreCliente"].ToString(),
                                    ruc = reader["ruc"].ToString()
                                });
                            }

                            return JsonConvert.SerializeObject(productos);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener productos: " + ex.Message);
                lblErrores.Text = "Error al cargar productos: " + ex.Message;
                return "[]";
            }
        }

        protected string ObtenerClientesJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT 
                    idCliente,
                    nombre,
                    ISNULL(ruc, '') as ruc,
                    nombre as nombreCompleto
                FROM Cliente 
                ORDER BY nombre";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var clientes = new List<object>();

                            while (reader.Read())
                            {
                                clientes.Add(new
                                {
                                    idCliente = Convert.ToInt32(reader["idCliente"]),
                                    nombre = reader["nombre"].ToString(),
                                    ruc = reader["ruc"].ToString(),
                                    nombreCompleto = reader["nombreCompleto"].ToString()
                                });
                            }

                            return JsonConvert.SerializeObject(clientes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener clientes: " + ex.Message);
                lblErrores.Text = "Error al cargar clientes: " + ex.Message;
                return "[]";
            }
        }

        protected string ObtenerCPICsJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT 
                    c.idCPIC,
                    c.numeroCPIC,
                    ISNULL(c.valorTotalFlete, 0) as valorTotalFlete,
                    c.fechaEmision,
                    ISNULL(f.numeroFactura, '') as numeroFactura
                FROM CPIC c
                LEFT JOIN Factura f ON c.idFactura = f.idFactura
                ORDER BY c.fechaEmision DESC, c.numeroCPIC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var cpics = new List<object>();

                            while (reader.Read())
                            {
                                string descripcion = reader["numeroCPIC"].ToString();
                                decimal valor = Convert.ToDecimal(reader["valorTotalFlete"]);
                                if (valor > 0)
                                {
                                    descripcion += $" - ${valor:N2}";
                                }

                                cpics.Add(new
                                {
                                    idCPIC = Convert.ToInt32(reader["idCPIC"]),
                                    numeroCPIC = reader["numeroCPIC"].ToString(),
                                    valorTotalFlete = valor,
                                    fechaEmision = reader["fechaEmision"] != DBNull.Value ?
                                                  Convert.ToDateTime(reader["fechaEmision"]).ToString("dd/MM/yyyy") : "",
                                    numeroFactura = reader["numeroFactura"].ToString(),
                                    descripcionCompleta = descripcion
                                });
                            }

                            return JsonConvert.SerializeObject(cpics);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener CPICs: " + ex.Message);
                lblErrores.Text = "Error al cargar CPICs: " + ex.Message;
                return "[]";
            }
        }

        protected string ObtenerFacturasJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT 
                    f.idFactura,
                    f.numeroFactura,
                    f.valorTotal,
                    f.fechaEmision,
                    ISNULL(f.numeroPedido, '') as numeroPedido,
                    f.idCliente,
                    c.nombre as nombreCliente,
                    ISNULL(c.ruc, '') as ruc
                FROM Factura f
                INNER JOIN Cliente c ON f.idCliente = c.idCliente
                ORDER BY f.fechaEmision DESC, f.numeroFactura";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var facturas = new List<object>();

                            while (reader.Read())
                            {
                                string numeroFactura = reader["numeroFactura"].ToString();
                                string descripcionCompleta = numeroFactura;
                                string numeroPedido = reader["numeroPedido"].ToString();

                                if (!string.IsNullOrEmpty(numeroPedido))
                                {
                                    descripcionCompleta += $" (Pedido: {numeroPedido})";
                                }

                                facturas.Add(new
                                {
                                    idFactura = Convert.ToInt32(reader["idFactura"]),
                                    numeroFactura = numeroFactura,
                                    valorTotal = Convert.ToDecimal(reader["valorTotal"]),
                                    fechaEmision = Convert.ToDateTime(reader["fechaEmision"]).ToString("dd/MM/yyyy"),
                                    numeroPedido = numeroPedido,
                                    idCliente = Convert.ToInt32(reader["idCliente"]),
                                    nombreCliente = reader["nombreCliente"].ToString(),
                                    ruc = reader["ruc"].ToString(),
                                    descripcionCompleta = descripcionCompleta
                                });
                            }

                            return JsonConvert.SerializeObject(facturas);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener facturas: " + ex.Message);
                lblErrores.Text = "Error al cargar facturas: " + ex.Message;
                return "[]";
            }
        }

        protected string ObtenerPlantasCargaJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT 
                    pc.idPlantaCarga,
                    pc.nombre,
                    ISNULL(pc.direccion, '') as direccion,
                    pc.idCliente,
                    c.nombre as nombreCliente,
                    ISNULL(c.ruc, '') as ruc
                FROM PlantaCarga pc
                INNER JOIN Cliente c ON pc.idCliente = c.idCliente
                WHERE pc.activa = 1
                ORDER BY c.nombre, pc.nombre";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var plantas = new List<object>();

                            while (reader.Read())
                            {
                                plantas.Add(new
                                {
                                    idPlantaCarga = Convert.ToInt32(reader["idPlantaCarga"]),
                                    nombre = reader["nombre"].ToString(),
                                    direccion = reader["direccion"].ToString(),
                                    idCliente = Convert.ToInt32(reader["idCliente"]),
                                    nombreCliente = reader["nombreCliente"].ToString(),
                                    ruc = reader["ruc"].ToString(),
                                    descripcionCompleta = $"{reader["nombre"]} - {reader["nombreCliente"]}"
                                });
                            }

                            return JsonConvert.SerializeObject(plantas);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener plantas de carga: " + ex.Message);
                return "[]";
            }
        }

        protected string ObtenerPlantasDescargaJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                SELECT 
                    pd.idPlanta as idPlantaDescarga,
                    pd.nombre,
                    ISNULL(pd.direccion, '') as direccion,
                    pd.idCliente,
                    c.nombre as nombreCliente,
                    ISNULL(c.ruc, '') as ruc
                FROM PlantaDescarga pd
                INNER JOIN Cliente c ON pd.idCliente = c.idCliente
                WHERE pd.activa = 1
                ORDER BY c.nombre, pd.nombre";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            var plantas = new List<object>();

                            while (reader.Read())
                            {
                                plantas.Add(new
                                {
                                    idPlantaDescarga = Convert.ToInt32(reader["idPlantaDescarga"]),
                                    nombre = reader["nombre"].ToString(),
                                    direccion = reader["direccion"].ToString(),
                                    idCliente = Convert.ToInt32(reader["idCliente"]),
                                    nombreCliente = reader["nombreCliente"].ToString(),
                                    ruc = reader["ruc"].ToString(),
                                    descripcionCompleta = $"{reader["nombre"]} - {reader["nombreCliente"]}"
                                });
                            }

                            return JsonConvert.SerializeObject(plantas);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al obtener plantas de descarga: " + ex.Message);
                return "[]";
            }
        }

        protected string ObtenerEstacionesPeajeJSON()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Verificar si la tabla existe
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
                            // Datos de prueba si la tabla no existe
                            var estacionesPrueba = new List<object>
                            {
                                new { nombre = "Peaje Sullana" },
                                new { nombre = "Peaje Piura" },
                                new { nombre = "Peaje Paita" },
                                new { nombre = "Peaje Lambayeque" },
                                new { nombre = "Peaje Chiclayo" },
                                new { nombre = "Peaje Trujillo" },
                                new { nombre = "Peaje Lima Norte" },
                                new { nombre = "Peaje Lima Sur" },
                                new { nombre = "Peaje Huacho" },
                                new { nombre = "Peaje Pucusana" }
                            };

                            return JsonConvert.SerializeObject(estacionesPrueba);
                        }
                    }

                    // Consultar datos reales si la tabla existe
                    string query = @"
                SELECT 
                    idEstacion,
                    nombre
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

                // Datos de emergencia si hay error
                var estacionesEmergencia = new List<object>
                {
                    new { nombre = "Peaje Sullana" },
                    new { nombre = "Peaje Piura" },
                    new { nombre = "Peaje Paita" }
                };

                return JsonConvert.SerializeObject(estacionesEmergencia);
            }
        }

        #endregion

        #region Métodos de Base de Datos

        private DataTable ObtenerDatosDeBD(string query)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        conn.Open();
                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);
                        return dt;
                    }
                    catch (Exception ex)
                    {
                        lblErrores.Text = "Error al obtener datos de la base de datos: " + ex.Message;
                        return new DataTable();
                    }
                }
            }
        }

        #endregion

        #region Validaciones

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada, string conductor, string placaTracto, string placaCarreta)
        {
            string mensajeError = "";

            // Validar fechas y horas
            if (fechaSalida == DateTime.MinValue)
            {
                mensajeError += "Por favor, seleccione una 'Fecha de Salida'.\n";
            }
            if (string.IsNullOrEmpty(horaSalida))
            {
                mensajeError += "Por favor, seleccione una 'Hora de Salida'.\n";
            }
            if (fechaLlegada == DateTime.MinValue)
            {
                mensajeError += "Por favor, seleccione una 'Fecha de Llegada'.\n";
            }
            if (string.IsNullOrEmpty(horaLlegada))
            {
                mensajeError += "Por favor, seleccione una 'Hora de Llegada'.\n";
            }

            if (fechaSalida != DateTime.MinValue && fechaLlegada != DateTime.MinValue)
            {
                if (fechaSalida > fechaLlegada)
                {
                    mensajeError += "La 'Fecha de Salida' no puede ser mayor a la 'Fecha de Llegada'.\n";
                }
            }

            // Validar dropdowns
            if (string.IsNullOrEmpty(conductor))
            {
                mensajeError += "Por favor, seleccione un 'Conductor'.\n";
            }
            else if (!ConductorExiste(Convert.ToInt32(conductor)))
            {
                mensajeError += "El conductor seleccionado no existe.\n";
            }

            if (string.IsNullOrEmpty(placaTracto))
            {
                mensajeError += "Por favor, seleccione una 'Placa Tracto'.\n";
            }
            else if (!TractoExiste(Convert.ToInt32(placaTracto)))
            {
                mensajeError += "El tracto seleccionado no existe.\n";
            }

            if (string.IsNullOrEmpty(placaCarreta))
            {
                mensajeError += "Por favor, seleccione una 'Placa Carreta'.\n";
            }
            else if (!CarretaExiste(Convert.ToInt32(placaCarreta)))
            {
                mensajeError += "La carreta seleccionada no existe.\n";
            }

            return mensajeError;
        }

        private string ValidarLiquidaciones(List<LiquidacionData> liquidaciones)
        {
            string errores = "";

            if (liquidaciones.Count == 0)
            {
                errores += "Debe agregar al menos una liquidación.\n";
                return errores;
            }

            // Lista para verificar duplicidad de guías
            var guiasTransportista = new List<string>();
            var guiasCliente = new List<string>();
            var manifiestos = new List<string>();

            for (int i = 0; i < liquidaciones.Count; i++)
            {
                var liquidacion = liquidaciones[i];
                string prefijoLiq = $"Liquidación {i + 1}: ";

                // Validar que tenga sub-tramos
                if (liquidacion.subTramos.Count == 0)
                {
                    errores += prefijoLiq + "Debe agregar al menos un sub-tramo.\n";
                    continue;
                }

                // Validar cada sub-tramo
                for (int j = 0; j < liquidacion.subTramos.Count; j++)
                {
                    var subTramo = liquidacion.subTramos[j];
                    string prefijo = $"{prefijoLiq}Sub-Tramo {j + 1}: ";

                    // Validar campos obligatorios básicos
                    if (string.IsNullOrEmpty(subTramo.origen))
                    {
                        errores += prefijo + "El origen es obligatorio.\n";
                    }

                    if (string.IsNullOrEmpty(subTramo.destino))
                    {
                        errores += prefijo + "El destino es obligatorio.\n";
                    }

                    if (string.IsNullOrEmpty(subTramo.tipoOperacion))
                    {
                        errores += prefijo + "Debe seleccionar un tipo de operación.\n";
                    }

                    // Validar tipos de operación permitidos
                    if (!string.IsNullOrEmpty(subTramo.tipoOperacion))
                    {
                        var tiposPermitidos = new[] { "TRANSITO_VACIO", "TRANSITO_CARGA", "SOLO_CARGA", "SOLO_DESCARGA", "DESCARGA_Y_CARGA", "PARADA_OPERATIVA" };
                        if (!tiposPermitidos.Contains(subTramo.tipoOperacion))
                        {
                            errores += prefijo + $"Tipo de operación '{subTramo.tipoOperacion}' no es válido.\n";
                        }
                    }

                    // Validar guías si están presentes
                    if (!string.IsNullOrEmpty(subTramo.guiaTransportista))
                    {
                        if (guiasTransportista.Contains(subTramo.guiaTransportista))
                        {
                            errores += prefijo + $"El N° Guía Transportista '{subTramo.guiaTransportista}' ya está siendo usado en otro sub-tramo.\n";
                        }
                        else
                        {
                            guiasTransportista.Add(subTramo.guiaTransportista);

                            // Verificar si ya existe en la base de datos
                            if (GuiaTransportistaExiste(subTramo.guiaTransportista))
                            {
                                errores += prefijo + $"El N° Guía Transportista '{subTramo.guiaTransportista}' ya está registrado en el sistema.\n";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(subTramo.guiaCliente))
                    {
                        if (guiasCliente.Contains(subTramo.guiaCliente))
                        {
                            errores += prefijo + $"El N° Guía Cliente '{subTramo.guiaCliente}' ya está siendo usado en otro sub-tramo.\n";
                        }
                        else
                        {
                            guiasCliente.Add(subTramo.guiaCliente);

                            // Verificar si ya existe en la base de datos
                            if (GuiaClienteExiste(subTramo.guiaCliente))
                            {
                                errores += prefijo + $"El N° Guía Cliente '{subTramo.guiaCliente}' ya está registrado en el sistema.\n";
                            }
                        }
                    }

                    // Validar manifiesto si cruza frontera
                    if (subTramo.cruzaFrontera)
                    {
                        if (string.IsNullOrEmpty(subTramo.manifiesto))
                        {
                            errores += prefijo + "Debe ingresar el N° Manifiesto cuando cruza frontera internacional.\n";
                        }
                        else
                        {
                            if (manifiestos.Contains(subTramo.manifiesto))
                            {
                                errores += prefijo + $"El N° Manifiesto '{subTramo.manifiesto}' ya está siendo usado en otro sub-tramo.\n";
                            }
                            else
                            {
                                manifiestos.Add(subTramo.manifiesto);

                                // Verificar si ya existe en la base de datos
                                if (ManifiestoExiste(subTramo.manifiesto))
                                {
                                    errores += prefijo + $"El N° Manifiesto '{subTramo.manifiesto}' ya está registrado en el sistema.\n";
                                }
                            }
                        }
                    }

                    // Validar operaciones específicas
                    errores += ValidarOperacionesPorTipo(subTramo, prefijo);
                }
            }

            return errores;
        }

        private string ValidarOperacionesPorTipo(SubTramoData subTramo, string prefijo)
        {
            string errores = "";

            switch (subTramo.tipoOperacion)
            {
                case "SOLO_CARGA":
                    errores += ValidarOperacionMejorada(subTramo.operacionCarga, prefijo, "Carga");
                    break;

                case "SOLO_DESCARGA":
                    errores += ValidarOperacionMejorada(subTramo.operacionDescarga, prefijo, "Descarga");
                    break;

                case "DESCARGA_Y_CARGA":
                    errores += ValidarOperacionMejorada(subTramo.operacionCarga, prefijo, "Carga");
                    errores += ValidarOperacionMejorada(subTramo.operacionDescarga, prefijo, "Descarga");
                    break;

                case "PARADA_OPERATIVA":
                    if (subTramo.parada == null || string.IsNullOrEmpty(subTramo.parada.motivo))
                    {
                        errores += prefijo + "Debe especificar el motivo de la parada operativa.\n";
                    }
                    break;

                case "TRANSITO_VACIO":
                case "TRANSITO_CARGA":
                    // Para tránsitos, no se requieren operaciones específicas de carga/descarga
                    break;
            }

            return errores;
        }

        private string ValidarOperacionMejorada(OperacionData operacion, string prefijo, string tipoOperacion)
        {
            string errores = "";

            if (operacion == null || !operacion.activa)
            {
                errores += prefijo + $"Debe configurar la operación de {tipoOperacion}.\n";
                return errores;
            }

            if (operacion.idCliente <= 0)
            {
                errores += prefijo + $"Debe seleccionar un cliente para la operación de {tipoOperacion}.\n";
            }

            // Validar documentos según tipo de transporte
            if (operacion.esInternacional && (!operacion.idCPIC.HasValue || operacion.idCPIC.Value <= 0))
            {
                errores += prefijo + $"Los clientes internacionales requieren CPIC para {tipoOperacion}.\n";
            }

            if (!operacion.esInternacional && (!operacion.idFactura.HasValue || operacion.idFactura.Value <= 0))
            {
                errores += prefijo + $"Los clientes nacionales requieren factura para {tipoOperacion}.\n";
            }

            // Validaciones para plantas
            if (tipoOperacion.ToLower() == "carga")
            {
                if (!operacion.idPlantaCarga.HasValue || operacion.idPlantaCarga.Value <= 0)
                {
                    errores += prefijo + $"Debe seleccionar una planta de carga.\n";
                }
                else if (!ValidarPlantaCargaPertenecceACliente(operacion.idPlantaCarga.Value, operacion.idCliente))
                {
                    errores += prefijo + $"La planta de carga seleccionada no pertenece al cliente especificado.\n";
                }
            }

            if (tipoOperacion.ToLower() == "descarga")
            {
                if (!operacion.idPlantaDescarga.HasValue || operacion.idPlantaDescarga.Value <= 0)
                {
                    errores += prefijo + $"Debe seleccionar una planta de descarga.\n";
                }
                else if (!ValidarPlantaDescargaPertenecceACliente(operacion.idPlantaDescarga.Value, operacion.idCliente))
                {
                    errores += prefijo + $"La planta de descarga seleccionada no pertenece al cliente especificado.\n";
                }
            }

            // Validar factura pertenece al cliente
            if (!operacion.esInternacional && operacion.idFactura.HasValue && operacion.idFactura.Value > 0)
            {
                if (!ValidarFacturaPertenecceACliente(operacion.idFactura.Value, operacion.idCliente))
                {
                    errores += prefijo + $"La factura seleccionada no pertenece al cliente especificado en {tipoOperacion}.\n";
                }
            }

            // Validar productos
            if (operacion.productos == null || operacion.productos.Count == 0)
            {
                errores += prefijo + $"Debe agregar al menos un producto para {tipoOperacion}.\n";
            }
            else
            {
                foreach (var producto in operacion.productos)
                {
                    if (producto.idProducto <= 0)
                    {
                        errores += prefijo + $"Todos los productos de {tipoOperacion} deben ser válidos.\n";
                        break;
                    }
                    if (producto.cantidad <= 0)
                    {
                        errores += prefijo + $"La cantidad de productos de {tipoOperacion} debe ser mayor a 0.\n";
                        break;
                    }

                    // Verificar que el producto pertenezca al cliente
                    if (!ValidarProductoPertenecceACliente(producto.idProducto, operacion.idCliente))
                    {
                        errores += prefijo + $"El producto con ID {producto.idProducto} no pertenece al cliente seleccionado en {tipoOperacion}.\n";
                    }
                }
            }

            return errores;
        }

        /// <summary>
        /// Valida que el número de orden tenga el formato correcto: exactamente 6 dígitos numéricos
        /// </summary>
        private string ValidarFormatoNumeroOrden(string numero)
        {
            // Verificar que no sea null o vacío
            if (string.IsNullOrWhiteSpace(numero))
            {
                return "El número de orden es obligatorio.";
            }

            // Verificar que tenga exactamente 6 caracteres
            if (numero.Length != 6)
            {
                return $"El número de orden debe tener exactamente 6 dígitos. Ingresado: {numero.Length} caracteres.";
            }

            // Verificar que todos los caracteres sean números
            if (!numero.All(char.IsDigit))
            {
                return "El número de orden debe contener solo números (0-9).";
            }

            // Verificar que no sea "000000"
            if (numero == "000000")
            {
                return "El número de orden no puede ser '000000'.";
            }

            return string.Empty;
        }

        #endregion

        #region Métodos de Validación Adicionales

        private bool ValidarProductoPertenecceACliente(int idProducto, int idCliente)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Producto WHERE idProducto = @idProducto AND idCliente = @idCliente";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idProducto", idProducto);
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);

                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al validar producto-cliente: " + ex.Message);
                return false;
            }
        }

        private bool ValidarFacturaPertenecceACliente(int idFactura, int idCliente)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Factura WHERE idFactura = @idFactura AND idCliente = @idCliente";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idFactura", idFactura);
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);

                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al validar factura-cliente: " + ex.Message);
                return false;
            }
        }

        private bool ValidarPlantaCargaPertenecceACliente(int idPlantaCarga, int idCliente)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM PlantaCarga WHERE idPlantaCarga = @idPlantaCarga AND idCliente = @idCliente AND activa = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idPlantaCarga", idPlantaCarga);
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);

                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al validar planta carga-cliente: " + ex.Message);
                return false;
            }
        }

        private bool ValidarPlantaDescargaPertenecceACliente(int idPlantaDescarga, int idCliente)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM PlantaDescarga WHERE idPlanta = @idPlantaDescarga AND idCliente = @idCliente AND activa = 1";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idPlantaDescarga", idPlantaDescarga);
                        cmd.Parameters.AddWithValue("@idCliente", idCliente);

                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error al validar planta descarga-cliente: " + ex.Message);
                return false;
            }
        }

        private bool ConductorExiste(int idConductor)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Conductor WHERE idConductor = @idConductor";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idConductor", idConductor);
                        conn.Open();
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validando conductor: " + ex.Message);
                return false;
            }
        }

        private bool TractoExiste(int idTracto)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Tracto WHERE idTracto = @idTracto";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idTracto", idTracto);
                        conn.Open();
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validando tracto: " + ex.Message);
                return false;
            }
        }

        private bool CarretaExiste(int idCarreta)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Carreta WHERE idCarreta = @idCarreta";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idCarreta", idCarreta);
                        conn.Open();
                        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validando carreta: " + ex.Message);
                return false;
            }
        }

        private bool GuiaTransportistaExiste(string guiaTransportista)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string query = "SELECT COUNT(*) FROM SubTramos WHERE guiaTransportista = @guiaTransportista AND activo = 1";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@guiaTransportista", guiaTransportista);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validando guía transportista: " + ex.Message);
                return false;
            }
        }

        private bool GuiaClienteExiste(string guiaCliente)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string query = "SELECT COUNT(*) FROM SubTramos WHERE guiaCliente = @guiaCliente AND activo = 1";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@guiaCliente", guiaCliente);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validando guía cliente: " + ex.Message);
                return false;
            }
        }

        private bool ManifiestoExiste(string manifiesto)
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                string query = "SELECT COUNT(*) FROM SubTramos WHERE manifiesto = @manifiesto AND activo = 1";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@manifiesto", manifiesto);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error validando manifiesto: " + ex.Message);
                return false;
            }
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

                        bool existe = count > 0;
                        System.Diagnostics.Debug.WriteLine($"Verificación número '{numeroOrden}': {(existe ? "YA EXISTE" : "Disponible")}");

                        return existe;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando número de orden: {ex.Message}");
                lblErrores.Text = "Error al verificar el número de orden: " + ex.Message;
                return true; // En caso de error, asumimos que existe para evitar duplicados
            }
        }

        #endregion

        #region Evento Principal - Guardar Orden Viaje

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== DIAGNÓSTICO DE FECHAS ===");

                // Verificar fechas principales
                string fechaSalidaStr = txtFechaSalida.Value;
                string fechaLlegadaStr = txtFechaLlegada.Value;

                System.Diagnostics.Debug.WriteLine($"Fecha Salida (string): '{fechaSalidaStr}'");
                System.Diagnostics.Debug.WriteLine($"Fecha Llegada (string): '{fechaLlegadaStr}'");

                DateTime fechaSalida = DateTime.MinValue;
                DateTime fechaLlegada = DateTime.MinValue;

                if (!string.IsNullOrEmpty(fechaSalidaStr))
                {
                    if (DateTime.TryParse(fechaSalidaStr, out fechaSalida))
                    {
                        System.Diagnostics.Debug.WriteLine($"Fecha Salida (parsed): {fechaSalida}");
                        if (fechaSalida.Year < 1753)
                        {
                            System.Diagnostics.Debug.WriteLine("⚠️ PROBLEMA: Fecha Salida inválida");
                            lblErrores.Text = "Error: La fecha de salida es inválida. Debe ser posterior al año 1753.";
                            return;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(fechaLlegadaStr))
                {
                    if (DateTime.TryParse(fechaLlegadaStr, out fechaLlegada))
                    {
                        System.Diagnostics.Debug.WriteLine($"Fecha Llegada (parsed): {fechaLlegada}");
                        if (fechaLlegada.Year < 1753)
                        {
                            System.Diagnostics.Debug.WriteLine("⚠️ PROBLEMA: Fecha Llegada inválida");
                            lblErrores.Text = "Error: La fecha de llegada es inválida. Debe ser posterior al año 1753.";
                            return;
                        }
                    }
                }

                // Verificar datos detallados
                VerificarFechasDetalladas();

                System.Diagnostics.Debug.WriteLine("=== INICIO GUARDAR ===");

                // 1. Validar número de orden
                string numeroOrdenViaje = txtNumeroOrdenViaje.Value?.Trim();
                System.Diagnostics.Debug.WriteLine($"Número de orden recibido: '{numeroOrdenViaje}'");

                string errorFormato = ValidarFormatoNumeroOrden(numeroOrdenViaje);
                if (!string.IsNullOrEmpty(errorFormato))
                {
                    System.Diagnostics.Debug.WriteLine($"Error de formato: {errorFormato}");
                    lblErrores.Text = $"<div class='alert alert-danger'>{errorFormato}</div>";
                    return;
                }

                // 2. Verificar existencia
                if (NumeroOrdenExiste(numeroOrdenViaje))
                {
                    System.Diagnostics.Debug.WriteLine($"Número ya existe: {numeroOrdenViaje}");
                    lblErrores.Text = $"<div class='alert alert-danger'>El número {numeroOrdenViaje} ya existe.</div>";
                    return;
                }

                // 3. Validar datos generales
                System.Diagnostics.Debug.WriteLine("Validando datos generales...");

                // CORREGIDO: No re-declarar las variables, solo asignar valores
                if (string.IsNullOrEmpty(fechaSalidaStr))
                    fechaSalida = DateTime.MinValue;
                else
                    fechaSalida = DateTime.Parse(fechaSalidaStr);

                if (string.IsNullOrEmpty(fechaLlegadaStr))
                    fechaLlegada = DateTime.MinValue;
                else
                    fechaLlegada = DateTime.Parse(fechaLlegadaStr);



                // Validar fechas principales
                if (!EsFechaValidaSQL(fechaSalida))
                {
                    lblErrores.Text = "<div class='alert alert-danger'>La fecha de salida es inválida. Debe estar entre 1753 y 9999.</div>";
                    return;
                }

                if (!EsFechaValidaSQL(fechaLlegada))
                {
                    lblErrores.Text = "<div class='alert alert-danger'>La fecha de llegada es inválida. Debe estar entre 1753 y 9999.</div>";
                    return;
                }


                string horaSalida = this.txtHoraSalida.Value;
                string horaLlegada = this.txtHoraLlegada.Value;



                string conductor = ddlConductor.SelectedValue;
                string placaTracto = ddlPlacaTracto.SelectedValue;
                string placaCarreta = ddlPlacaCarreta.SelectedValue;
                string observaciones = txtObservaciones.Value;

                string errores = ValidarDatosGenerales(fechaSalida, fechaLlegada, horaSalida, horaLlegada, conductor, placaTracto, placaCarreta);
                if (!string.IsNullOrEmpty(errores))
                {
                    System.Diagnostics.Debug.WriteLine($"Errores en datos generales: {errores}");
                    lblErrores.Text = $"<div class='alert alert-danger'>{errores.Replace("\n", "<br/>")}</div>";
                    return;
                }

                // 4. Procesar liquidaciones
                System.Diagnostics.Debug.WriteLine("Procesando liquidaciones...");
                List<LiquidacionData> liquidaciones = new List<LiquidacionData>();
                string liquidacionesJson = Request.Form["hiddenLiquidacionesData"] ?? "[]";
                System.Diagnostics.Debug.WriteLine($"JSON liquidaciones: {liquidacionesJson.Substring(0, Math.Min(500, liquidacionesJson.Length))}...");

                if (!string.IsNullOrEmpty(liquidacionesJson) && liquidacionesJson != "[]")
                {
                    try
                    {
                        liquidaciones = JsonConvert.DeserializeObject<List<LiquidacionData>>(liquidacionesJson) ?? new List<LiquidacionData>();
                        System.Diagnostics.Debug.WriteLine($"Liquidaciones deserializadas: {liquidaciones.Count}");

                        errores = ValidarLiquidaciones(liquidaciones);
                        if (!string.IsNullOrEmpty(errores))
                        {
                            System.Diagnostics.Debug.WriteLine($"Errores en liquidaciones: {errores}");
                            lblErrores.Text = $"<div class='alert alert-danger'>{errores.Replace("\n", "<br/>")}</div>";
                            return;
                        }
                    }
                    catch (JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error deserializando JSON: {ex.Message}");
                        lblErrores.Text = $"<div class='alert alert-danger'>Error en formato de datos: {ex.Message}</div>";
                        return;
                    }
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

                            // 5.1 Crear CPIC temporal
                            System.Diagnostics.Debug.WriteLine("Creando CPIC temporal...");
                            int idCPICTemporal = CrearCPICTemporal(conn, transaction, numeroOrdenViaje);
                            System.Diagnostics.Debug.WriteLine($"CPIC temporal creado: {idCPICTemporal}");

                            // 5.2 Insertar orden de viaje
                            System.Diagnostics.Debug.WriteLine("Insertando orden de viaje...");
                            int idOrdenViaje = InsertarOrdenViaje(conn, transaction, numeroOrdenViaje, fechaSalida, fechaLlegada,
                                                                horaSalida, horaLlegada, placaTracto, placaCarreta, conductor,
                                                                observaciones, idCPICTemporal, liquidaciones);
                            System.Diagnostics.Debug.WriteLine($"Orden de viaje insertada: {idOrdenViaje}");

                            // 5.3 Insertar liquidaciones
                            if (liquidaciones.Count > 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Insertando {liquidaciones.Count} liquidaciones...");
                                InsertarLiquidacionesCompletas(conn, transaction, idOrdenViaje, liquidaciones);
                                System.Diagnostics.Debug.WriteLine("Liquidaciones insertadas");
                            }

                            // 5.4 Insertar datos financieros
                            System.Diagnostics.Debug.WriteLine("Insertando datos financieros...");
                            InsertarDatosFinancierosCompletos(conn, transaction, numeroOrdenViaje);
                            System.Diagnostics.Debug.WriteLine("Datos financieros insertados");

                            // 5.5 Insertar descuentos y reintegros
                            System.Diagnostics.Debug.WriteLine("Insertando descuentos y reintegros...");
                            InsertarDescuentosReintegros(conn, transaction, numeroOrdenViaje);
                            System.Diagnostics.Debug.WriteLine("Descuentos y reintegros insertados");

                            // 5.6 Procesar datos detallados
                            System.Diagnostics.Debug.WriteLine("Procesando datos detallados...");
                            var peajes = ProcesarDatosDetallados<PeajeDetallado>("hiddenPeajesDetalle");
                            var alimentacion = ProcesarDatosDetallados<AlimentacionDetallada>("hiddenAlimentacionDetalle");
                            var apoyoSeguridad = ProcesarDatosDetallados<ApoyoSeguridadDetallada>("hiddenApoyoSeguridadDetalle");
                            var reparaciones = ProcesarDatosDetallados<ReparacionesDetalladas>("hiddenReparacionesDetalle");
                            var movilidad = ProcesarDatosDetallados<MovilidadDetallada>("hiddenMovilidadDetalle");
                            var encapada = ProcesarDatosDetallados<EncapadaDetallada>("hiddenEncapadaDetalle");
                            var hospedaje = ProcesarDatosDetallados<HospedajeDetallado>("hiddenHospedajeDetalle");
                            var combustible = ProcesarDatosDetallados<CombustibleDetallado>("hiddenCombustibleDetalle");

                            System.Diagnostics.Debug.WriteLine($"Datos detallados: P:{peajes.Count}, A:{alimentacion.Count}, AS:{apoyoSeguridad.Count}, R:{reparaciones.Count}, M:{movilidad.Count}, E:{encapada.Count}, H:{hospedaje.Count}, C:{combustible.Count}");

                            InsertarTodosLosDatosDetallados(conn, transaction, numeroOrdenViaje,
                                peajes, alimentacion, apoyoSeguridad, reparaciones,
                                movilidad, encapada, hospedaje, combustible);
                            System.Diagnostics.Debug.WriteLine("Datos detallados insertados");

                            // 5.7 Manejar despacho si viene desde ahí
                            bool despachoActualizado = false;
                            if (ViewState["VieneDesdeDespaco"] != null && (bool)ViewState["VieneDesdeDespaco"])
                            {
                                int idDespacho = Convert.ToInt32(ViewState["IdDespachoOrigen"]);
                                System.Diagnostics.Debug.WriteLine($"Actualizando despacho: {idDespacho}");
                                despachoActualizado = ActualizarEstadoDespacho(conn, transaction, idDespacho, "COMPLETADO", idOrdenViaje);
                            }

                            // 5.8 Auditoría
                            System.Diagnostics.Debug.WriteLine("Registrando auditoría...");
                            RegistrarAuditoria(conn, transaction, "OrdenViaje", "INSERT", idOrdenViaje,
                                             null, "CREADO", ObtenerUsuarioActual());

                            // 5.9 Commit
                            System.Diagnostics.Debug.WriteLine("Haciendo commit...");
                            transaction.Commit();
                            System.Diagnostics.Debug.WriteLine("Commit exitoso");

                            // 6. Mostrar resultado
                            MostrarResultadoExitosoConDespacho(numeroOrdenViaje, liquidaciones.Count,
                                peajes.Count + alimentacion.Count + apoyoSeguridad.Count + reparaciones.Count +
                                movilidad.Count + encapada.Count + hospedaje.Count + combustible.Count,
                                despachoActualizado);

                            System.Diagnostics.Debug.WriteLine("=== GUARDADO EXITOSO ===");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"ERROR EN TRANSACCIÓN: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                            transaction.Rollback();

                            lblErrores.Text = $@"<div class='alert alert-danger'>
                        <h5>Error al guardar:</h5>
                        <strong>Mensaje:</strong> {ex.Message}<br/>
                        <strong>Tipo:</strong> {ex.GetType().Name}<br/>
                        {(ex.InnerException != null ? $"<strong>Error interno:</strong> {ex.InnerException.Message}" : "")}
                    </div>";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR GENERAL: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                lblErrores.Text = $@"<div class='alert alert-danger'>
            <h5>Error general del sistema:</h5>
            <strong>Mensaje:</strong> {ex.Message}<br/>
            <strong>Tipo:</strong> {ex.GetType().Name}<br/>
            {(ex.InnerException != null ? $"<strong>Error interno:</strong> {ex.InnerException.Message}" : "")}
        </div>";
            }
        }


        // Método para actualizar estado del despacho
        private bool ActualizarEstadoDespacho(SqlConnection conn, SqlTransaction transaction, int idDespacho, string nuevoEstado, int idOrdenViaje)
        {
            try
            {
                // Verificar que el despacho aún esté en estado PROGRAMADO
                string queryVerificar = "SELECT estadoDespacho FROM Despachos WHERE idDespacho = @idDespacho AND activo = 1";
                using (SqlCommand cmdVerificar = new SqlCommand(queryVerificar, conn, transaction))
                {
                    cmdVerificar.Parameters.AddWithValue("@idDespacho", idDespacho);
                    object estadoActual = cmdVerificar.ExecuteScalar();

                    if (estadoActual == null || estadoActual.ToString() != "PROGRAMADO")
                    {
                        System.Diagnostics.Debug.WriteLine($"Despacho {idDespacho} ya no está en estado PROGRAMADO");
                        return false;
                    }
                }

                // Actualizar estado del despacho
                string queryActualizar = @"
            UPDATE Despachos 
            SET estadoDespacho = @nuevoEstado,
                fechaModificacion = GETDATE(),
                usuarioModificacion = @usuario,
                idOrdenViaje = @idOrdenViaje
            WHERE idDespacho = @idDespacho AND activo = 1";

                using (SqlCommand cmd = new SqlCommand(queryActualizar, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@nuevoEstado", nuevoEstado);
                    cmd.Parameters.AddWithValue("@idDespacho", idDespacho);
                    cmd.Parameters.AddWithValue("@usuario", ObtenerUsuarioActual());
                    cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);

                    int filasAfectadas = cmd.ExecuteNonQuery();

                    if (filasAfectadas > 0)
                    {
                        // Registrar auditoría del cambio de estado
                        RegistrarAuditoria(conn, transaction, "Despachos", "UPDATE_ESTADO", idDespacho,
                                         "PROGRAMADO", nuevoEstado, ObtenerUsuarioActual());

                        System.Diagnostics.Debug.WriteLine($"Despacho {idDespacho} actualizado a estado: {nuevoEstado}");
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error actualizando estado despacho: {ex.Message}");
                return false;
            }
        }


        private void VerificarFechasDetalladas()
        {
            try
            {
                // Verificar fechas en peajes
                string peajesJson = Request.Form["hiddenPeajesDetalle"] ?? "[]";
                if (peajesJson != "[]")
                {
                    var peajes = JsonConvert.DeserializeObject<List<PeajeDetallado>>(peajesJson);
                    foreach (var peaje in peajes)
                    {
                        if (!EsFechaValidaSQL(peaje.fecha))
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ PROBLEMA: Fecha peaje inválida: {peaje.fecha}");
                            throw new Exception($"Fecha de peaje inválida en estación '{peaje.estacion}': {peaje.fecha:yyyy-MM-dd}");
                        }
                    }
                }

                // Verificar fechas en otras categorías
                var categoriasConFechas = new Dictionary<string, string>
        {
            {"hiddenReparacionesDetalle", "Reparaciones"},
            {"hiddenHospedajeDetalle", "Hospedaje"},
            {"hiddenCombustibleDetalle", "Combustible"}
        };

                foreach (var categoria in categoriasConFechas)
                {
                    string jsonData = Request.Form[categoria.Key] ?? "[]";
                    if (jsonData != "[]")
                    {
                        var registros = JsonConvert.DeserializeObject<List<CategoriaGastoDetallado>>(jsonData);
                        foreach (var registro in registros)
                        {
                            if (!EsFechaValidaSQL(registro.fechaComprobante))
                            {
                                throw new Exception($"Fecha inválida en {categoria.Value}: {registro.fechaComprobante:yyyy-MM-dd}");
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("✅ Todas las fechas validadas correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando fechas: {ex.Message}");
                throw;
            }
        }

        // Método para registrar auditoría de cambio de estado
        private void RegistrarAuditoria(SqlConnection conn, SqlTransaction transaction, string tabla, string operacion,
                                       int idRegistro, string valorAnterior, string valorNuevo, string usuario)
        {
            try
            {
                string queryAuditoria = @"
            INSERT INTO Auditoria (TablaAfectada, TipoOperacion, IdRegistro, Campo, 
                                 ValorAnterior, ValorNuevo, Usuario, FechaHora)
            VALUES (@tabla, @operacion, @idRegistro, 'estadoDespacho', 
                    @valorAnterior, @valorNuevo, @usuario, GETDATE())";

                using (SqlCommand cmd = new SqlCommand(queryAuditoria, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@tabla", tabla);
                    cmd.Parameters.AddWithValue("@operacion", operacion);
                    cmd.Parameters.AddWithValue("@idRegistro", idRegistro);
                    cmd.Parameters.AddWithValue("@valorAnterior", valorAnterior ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@valorNuevo", valorNuevo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@usuario", usuario);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registrando auditoría: {ex.Message}");
                // No lanzar excepción para no interrumpir el proceso principal
            }
        }

        // Método para obtener usuario actual
        private string ObtenerUsuarioActual()
        {
            if (Session["Usuario"] != null)
                return Session["Usuario"].ToString();
            else if (User.Identity.IsAuthenticated)
                return User.Identity.Name;
            else
                return "Sistema";
        }

        // Método mejorado para mostrar resultado exitoso
        private void MostrarResultadoExitosoConDespacho(string numeroOrdenViaje, int cantidadLiquidaciones,
                                                       int totalRegistrosDetallados, bool despachoActualizado)
        {
            try
            {
                hfNumeroOrdenViaje.Value = numeroOrdenViaje;

                string mensajeDespacho = despachoActualizado
                    ? "<br/>✅ <strong>Despacho actualizado:</strong> Estado cambiado a 'EN_PROCESO'"
                    : "<br/>ℹ️ <em>No se actualizó ningún despacho (normal si no viene desde despacho)</em>";

                lblErrores.Text = $@"<div class='alert alert-success'>
            <h5><i class='fas fa-check-circle'></i> ¡Orden de viaje guardada exitosamente!</h5>
            <hr/>
            <strong>Número de orden:</strong> {numeroOrdenViaje}<br/>
            <strong>Liquidaciones procesadas:</strong> {cantidadLiquidaciones}<br/>
            <strong>Total registros detallados:</strong> {totalRegistrosDetallados}
            {mensajeDespacho}
            <hr/>
            <div class='mt-3'>
                <a href='ListaDespachos.aspx' class='btn btn-primary btn-sm me-2'>
                    <i class='fas fa-list'></i> Ver Lista de Despachos
                </a>
                <button type='button' class='btn btn-secondary btn-sm' onclick='location.reload()'>
                    <i class='fas fa-plus'></i> Nueva Orden de Viaje
                </button>
            </div>
        </div>";

                // Limpiar ViewState
                ViewState["VieneDesdeDespaco"] = null;
                ViewState["IdDespachoOrigen"] = null;

                System.Diagnostics.Debug.WriteLine($"Orden {numeroOrdenViaje} guardada. Despacho actualizado: {despachoActualizado}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error mostrando resultado: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Inserción en Base de Datos

        private int CrearCPICTemporal(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            try
            {
                string numeroCPICTemporal = $"TEMP-{numeroOrdenViaje}-{DateTime.Now:yyyyMMddHHmmss}";
                string queryCPIC = "INSERT INTO CPIC (numeroCPIC, valorTotalFlete, fechaEmision) VALUES (@numeroCPIC, 0.00, GETDATE()); SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(queryCPIC, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroCPIC", numeroCPICTemporal);
                    object result = cmd.ExecuteScalar();

                    if (result != null && result != DBNull.Value)
                    {
                        int idCPIC = Convert.ToInt32(result);
                        System.Diagnostics.Debug.WriteLine($"CPIC temporal creado con ID: {idCPIC}");
                        return idCPIC;
                    }
                    else
                    {
                        throw new Exception("No se pudo crear el CPIC temporal");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creando CPIC temporal: {ex.Message}");
                throw;
            }
        }

        private int InsertarOrdenViaje(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje,
                                     DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada,
                                     string placaTracto, string placaCarreta, string conductor, string observaciones,
                                     int idCPICTemporal, List<LiquidacionData> liquidaciones)
        {
            try
            {
                string tipoViaje = DeterminarTipoViaje(liquidaciones);

                string queryOrdenViaje = @"
                    INSERT INTO OrdenViaje (
                        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
                        idTracto, idCarreta, idConductor, observaciones, idCPIC, estadoViaje, tipoViaje
                    ) 
                    VALUES (
                        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
                        @idTracto, @idCarreta, @idConductor, @observaciones, @idCPIC, 'PENDIENTE', @tipoViaje
                    );
                    SELECT SCOPE_IDENTITY();";

                using (SqlCommand cmd = new SqlCommand(queryOrdenViaje, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@fechaSalida", FechaSeguraSQL(fechaSalida));
                    cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                    cmd.Parameters.AddWithValue("@fechaLlegada", FechaSeguraSQL(fechaLlegada));
                    cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                    cmd.Parameters.AddWithValue("@idTracto", placaTracto);
                    cmd.Parameters.AddWithValue("@idCarreta", placaCarreta);
                    cmd.Parameters.AddWithValue("@idConductor", conductor);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);
                    cmd.Parameters.AddWithValue("@idCPIC", idCPICTemporal);
                    cmd.Parameters.AddWithValue("@tipoViaje", tipoViaje);

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

        private void InsertarLiquidacionesCompletas(SqlConnection conn, SqlTransaction transaction, int idOrdenViaje, List<LiquidacionData> liquidaciones)
        {
            try
            {
                foreach (var liquidacion in liquidaciones)
                {
                    // Insertar liquidación principal
                    string tipoLiquidacion = DeterminarTipoLiquidacion(liquidacion);

                    string queryLiquidacion = @"
                        INSERT INTO Liquidaciones (idOrdenViaje, numeroLiquidacion, tipo, descripcion, observaciones)
                        VALUES (@idOrdenViaje, @numeroLiquidacion, @tipo, @descripcion, @observaciones);
                        SELECT SCOPE_IDENTITY();";

                    int idLiquidacion = 0;
                    using (SqlCommand cmd = new SqlCommand(queryLiquidacion, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                        cmd.Parameters.AddWithValue("@numeroLiquidacion", liquidacion.numeroLiquidacion);
                        cmd.Parameters.AddWithValue("@tipo", tipoLiquidacion);
                        cmd.Parameters.AddWithValue("@descripcion", string.IsNullOrEmpty(liquidacion.descripcion) ? (object)DBNull.Value : liquidacion.descripcion);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(liquidacion.observaciones) ? (object)DBNull.Value : liquidacion.observaciones);

                        object result = cmd.ExecuteScalar();
                        idLiquidacion = Convert.ToInt32(result);
                    }

                    // Insertar sub-tramos de esta liquidación
                    foreach (var subTramo in liquidacion.subTramos)
                    {
                        InsertarSubTramo(conn, transaction, idLiquidacion, subTramo);
                    }

                    System.Diagnostics.Debug.WriteLine($"Liquidación {liquidacion.numeroLiquidacion} insertada con {liquidacion.subTramos.Count} sub-tramos");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando liquidaciones: {ex.Message}");
                throw;
            }
        }

        private void InsertarSubTramo(SqlConnection conn, SqlTransaction transaction, int idLiquidacion, SubTramoData subTramo)
        {
            try
            {
                string querySubTramo = @"
                    INSERT INTO SubTramos (
                        idLiquidacion, numeroSubTramo, origen, destino, tipoOperacion, observaciones,
                        guiaTransportista, guiaCliente, cruzaFrontera, manifiesto, motivoParada, duracionHoras
                    )
                    VALUES (
                        @idLiquidacion, @numeroSubTramo, @origen, @destino, @tipoOperacion, @observaciones,
                        @guiaTransportista, @guiaCliente, @cruzaFrontera, @manifiesto, @motivoParada, @duracionHoras
                    );
                    SELECT SCOPE_IDENTITY();";

                int idSubTramo = 0;
                using (SqlCommand cmd = new SqlCommand(querySubTramo, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@idLiquidacion", idLiquidacion);
                    cmd.Parameters.AddWithValue("@numeroSubTramo", Convert.ToInt32(subTramo.numeroSubTramo));
                    cmd.Parameters.AddWithValue("@origen", subTramo.origen ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@destino", subTramo.destino ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@tipoOperacion", subTramo.tipoOperacion ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(subTramo.observaciones) ? (object)DBNull.Value : subTramo.observaciones);
                    cmd.Parameters.AddWithValue("@guiaTransportista", string.IsNullOrEmpty(subTramo.guiaTransportista) ? (object)DBNull.Value : subTramo.guiaTransportista);
                    cmd.Parameters.AddWithValue("@guiaCliente", string.IsNullOrEmpty(subTramo.guiaCliente) ? (object)DBNull.Value : subTramo.guiaCliente);
                    cmd.Parameters.AddWithValue("@cruzaFrontera", subTramo.cruzaFrontera);
                    cmd.Parameters.AddWithValue("@manifiesto", string.IsNullOrEmpty(subTramo.manifiesto) ? (object)DBNull.Value : subTramo.manifiesto);
                    cmd.Parameters.AddWithValue("@motivoParada", subTramo.parada?.motivo ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@duracionHoras", subTramo.parada?.duracion ?? (object)DBNull.Value);

                    object result = cmd.ExecuteScalar();
                    idSubTramo = Convert.ToInt32(result);
                }

                // Insertar operaciones del sub-tramo
                if (subTramo.operacionCarga != null && subTramo.operacionCarga.activa)
                {
                    InsertarOperacion(conn, transaction, idSubTramo, subTramo.operacionCarga, "CARGA");
                }

                if (subTramo.operacionDescarga != null && subTramo.operacionDescarga.activa)
                {
                    InsertarOperacion(conn, transaction, idSubTramo, subTramo.operacionDescarga, "DESCARGA");
                }

                System.Diagnostics.Debug.WriteLine($"Sub-tramo {subTramo.numeroSubTramo} insertado con ID: {idSubTramo}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando sub-tramo: {ex.Message}");
                throw;
            }
        }

        private void InsertarOperacion(SqlConnection conn, SqlTransaction transaction, int idSubTramo, OperacionData operacion, string tipoOperacion)
        {
            try
            {
                string queryOperacion = @"
                    INSERT INTO OperacionesSubTramo (
                        idSubTramo, tipoOperacion, idCliente, idFactura, idCPIC, 
                        esInternacional, observaciones, idPlantaCarga, idPlantaDescarga
                    )
                    VALUES (
                        @idSubTramo, @tipoOperacion, @idCliente, @idFactura, @idCPIC,
                        @esInternacional, @observaciones, @idPlantaCarga, @idPlantaDescarga
                    );
                    SELECT SCOPE_IDENTITY();";

                int idOperacion = 0;
                using (SqlCommand cmd = new SqlCommand(queryOperacion, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@idSubTramo", idSubTramo);
                    cmd.Parameters.AddWithValue("@tipoOperacion", tipoOperacion);
                    cmd.Parameters.AddWithValue("@idCliente", operacion.idCliente);
                    cmd.Parameters.AddWithValue("@idFactura", operacion.idFactura ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@idCPIC", operacion.idCPIC ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@esInternacional", operacion.esInternacional);
                    cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(operacion.observaciones) ? (object)DBNull.Value : operacion.observaciones);
                    cmd.Parameters.AddWithValue("@idPlantaCarga", operacion.idPlantaCarga ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@idPlantaDescarga", operacion.idPlantaDescarga ?? (object)DBNull.Value);

                    object result = cmd.ExecuteScalar();
                    idOperacion = Convert.ToInt32(result);
                }

                // Insertar productos de la operación
                foreach (var producto in operacion.productos)
                {
                    InsertarProductoOperacion(conn, transaction, idOperacion, producto);
                }

                System.Diagnostics.Debug.WriteLine($"Operación {tipoOperacion} insertada con {operacion.productos.Count} productos");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando operación {tipoOperacion}: {ex.Message}");
                throw;
            }
        }

        private void InsertarProductoOperacion(SqlConnection conn, SqlTransaction transaction, int idOperacion, ProductoOperacion producto)
        {
            try
            {
                string queryProducto = @"
                    INSERT INTO ProductosOperacion (idOperacion, idProducto, cantidadBolsas, pesoKg)
                    VALUES (@idOperacion, @idProducto, @cantidadBolsas, @pesoKg)";

                using (SqlCommand cmd = new SqlCommand(queryProducto, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@idOperacion", idOperacion);
                    cmd.Parameters.AddWithValue("@idProducto", producto.idProducto);
                    cmd.Parameters.AddWithValue("@cantidadBolsas", producto.cantidad);
                    cmd.Parameters.AddWithValue("@pesoKg", producto.peso);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando producto de operación: {ex.Message}");
                throw;
            }
        }

        private void InsertarDatosFinancierosCompletos(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            System.Diagnostics.Debug.WriteLine("=== INICIO DATOS FINANCIEROS ===");

            try
            {
                // Insertar ingresos principales
                System.Diagnostics.Debug.WriteLine("Insertando ingresos principales...");
                InsertarIngresosPrincipales(conn, transaction, numeroOrdenViaje);
                System.Diagnostics.Debug.WriteLine("✅ Ingresos principales insertados");

                // Insertar gastos principales  
                System.Diagnostics.Debug.WriteLine("Insertando gastos principales...");
                InsertarGastosPrincipales(conn, transaction, numeroOrdenViaje);
                System.Diagnostics.Debug.WriteLine("✅ Gastos principales insertados");

                // ✅ CORREGIDO: Insertar gastos adicionales con esquema correcto
                System.Diagnostics.Debug.WriteLine("Insertando gastos adicionales...");
                InsertarGastosAdicionales(conn, transaction, numeroOrdenViaje);
                System.Diagnostics.Debug.WriteLine("✅ Gastos adicionales insertados");

                // ✅ CORREGIDO: Insertar ingresos adicionales con esquema correcto
                System.Diagnostics.Debug.WriteLine("Insertando ingresos adicionales...");
                InsertarIngresosAdicionales(conn, transaction, numeroOrdenViaje);
                System.Diagnostics.Debug.WriteLine("✅ Ingresos adicionales insertados");

                System.Diagnostics.Debug.WriteLine("✅ DATOS FINANCIEROS COMPLETADOS");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR GENERAL en datos financieros: {ex.Message}");
                throw; // RE-LANZAR para que la transacción haga rollback
            }
        }

        private void InsertarIngresosPrincipales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            // Obtener datos del formulario
            decimal despachoSoles = decimal.TryParse(Request.Form["txtDespachoSoles"], out var ds) ? ds : 0;
            decimal despachoDolares = decimal.TryParse(Request.Form["txtDespachoDolares"], out var dd) ? dd : 0;
            decimal prestamoSoles = decimal.TryParse(Request.Form["txtPrestamoSoles"], out var ps) ? ps : 0;
            decimal prestamoDolares = decimal.TryParse(Request.Form["txtPrestamoDolares"], out var pd) ? pd : 0;
            decimal mensualidadSoles = decimal.TryParse(Request.Form["txtMensualidadSoles"], out var ms) ? ms : 0;
            decimal mensualidadDolares = decimal.TryParse(Request.Form["txtMensualidadDolares"], out var md) ? md : 0;
            decimal otrosSoles = decimal.TryParse(Request.Form["txtOtrosSoles"], out var os) ? os : 0;
            decimal otrosDolares = decimal.TryParse(Request.Form["txtOtrosDolares"], out var od) ? od : 0;

            string descDespacho = Request.Form["txtDescDespacho"] ?? "";
            string descMensualidad = Request.Form["txtDescMensualidad"] ?? "";
            string descOtros = Request.Form["txtDescOtros"] ?? "";
            string descPrestamo = Request.Form["txtDescPrestamo"] ?? "";

            // Solo insertar si hay datos
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
            // Obtener datos del formulario
            decimal peajesSoles = decimal.TryParse(Request.Form["txtPeajesSoles"], out var pjs) ? pjs : 0;
            decimal peajesDolares = decimal.TryParse(Request.Form["txtPeajesDolares"], out var pjd) ? pjd : 0;
            decimal alimentacionSoles = decimal.TryParse(Request.Form["txtAlimentacionSoles"], out var als) ? als : 0;
            decimal alimentacionDolares = decimal.TryParse(Request.Form["txtAlimentacionDolares"], out var ald) ? ald : 0;
            decimal apoyoSeguridadSoles = decimal.TryParse(Request.Form["txtApoyoSeguridadSoles"], out var ass) ? ass : 0;
            decimal apoyoSeguridadDolares = decimal.TryParse(Request.Form["txtApoyoSeguridadDolares"], out var asd) ? asd : 0;
            decimal reparacionesSoles = decimal.TryParse(Request.Form["txtReparacionesSoles"], out var reps) ? reps : 0;
            decimal reparacionesDolares = decimal.TryParse(Request.Form["txtReparacionesDolares"], out var repd) ? repd : 0;
            decimal movilidadSoles = decimal.TryParse(Request.Form["txtMovilidadSoles"], out var movs) ? movs : 0;
            decimal movilidadDolares = decimal.TryParse(Request.Form["txtMovilidadDolares"], out var movd) ? movd : 0;
            decimal encapadaSoles = decimal.TryParse(Request.Form["txtEncapadaSoles"], out var encs) ? encs : 0;
            decimal encapadaDolares = decimal.TryParse(Request.Form["txtEncapadaDolares"], out var encd) ? encd : 0;
            decimal hospedajeSoles = decimal.TryParse(Request.Form["txtHospedajeSoles"], out var hoss) ? hoss : 0;
            decimal hospedajeDolares = decimal.TryParse(Request.Form["txtHospedajeDolares"], out var hosd) ? hosd : 0;
            decimal combustibleSoles = decimal.TryParse(Request.Form["txtCombustibleSoles"], out var coms) ? coms : 0;
            decimal combustibleDolares = decimal.TryParse(Request.Form["txtCombustibleDolares"], out var comd) ? comd : 0;

            // Descripciones
            string descPeajes = Request.Form["txtDescPeajes"] ?? "";
            string descAlimentacion = Request.Form["txtDescAlimentacion"] ?? "";
            string descApoyoSeguridad = Request.Form["txtDescApoyoSeguridad"] ?? "";
            string descReparaciones = Request.Form["txtDescReparaciones"] ?? "";
            string descMovilidad = Request.Form["txtDescMovilidad"] ?? "";
            string descEncapada = Request.Form["txtDescEncapada"] ?? "";
            string descHospedaje = Request.Form["txtDescHospedaje"] ?? "";
            string descCombustible = Request.Form["txtDescCombustible"] ?? "";

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
            @reparacionesSoles, @repacionesVariosDolares, @descReparaciones,
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
                // *** CORRECCIÓN CRÍTICA: Usar nombre correcto del parámetro ***
                cmd.Parameters.AddWithValue("@repacionesVariosDolares", reparacionesDolares);
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

                // Debug para verificar
                System.Diagnostics.Debug.WriteLine($"Reparaciones insertadas: S/{reparacionesSoles}, $/{reparacionesDolares}");
            }
        }

        // MÉTODOS CORREGIDOS CON NOMBRES CONSISTENTES

        private void InsertarGastosAdicionales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            System.Diagnostics.Debug.WriteLine("--- Procesando gastos adicionales ---");

            try
            {
                // ✅ CORRECTO: Usar el nombre del campo oculto
                string gastosAdicionalesJson = Request.Form["hiddenGastosAdicionales"] ?? "[]";
                System.Diagnostics.Debug.WriteLine($"JSON gastos adicionales recibido: '{gastosAdicionalesJson}'");

                if (string.IsNullOrEmpty(gastosAdicionalesJson) || gastosAdicionalesJson == "[]")
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay gastos adicionales para insertar");
                    return;
                }

                // Deserializar gastos adicionales
                List<GastoAdicionalData> gastosAdicionales;
                try
                {
                    gastosAdicionales = JsonConvert.DeserializeObject<List<GastoAdicionalData>>(gastosAdicionalesJson) ?? new List<GastoAdicionalData>();
                    System.Diagnostics.Debug.WriteLine($"Gastos adicionales deserializados: {gastosAdicionales.Count}");
                }
                catch (JsonException ex)
                {
                    throw new Exception($"Error deserializando gastos adicionales: {ex.Message}", ex);
                }

                // Insertar cada gasto adicional usando el ESQUEMA REAL
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
                        System.Diagnostics.Debug.WriteLine($"Insertando gasto: {gasto.categoria} - S/{gasto.soles} - $/{gasto.dolares}");

                        using (SqlCommand cmd = new SqlCommand(queryInsert, conn, transaction))
                        {
                            // ✅ USAR COLUMNAS REALES DE LA BASE DE DATOS
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@nombreCategoria", gasto.categoria ?? "");
                            cmd.Parameters.AddWithValue("@soles", gasto.soles ?? 0);
                            cmd.Parameters.AddWithValue("@dolares", gasto.dolares ?? 0);
                            cmd.Parameters.AddWithValue("@descripcion", gasto.descripcion ?? "");

                            int rowsAffected = cmd.ExecuteNonQuery();
                            System.Diagnostics.Debug.WriteLine($"Gasto insertado, filas afectadas: {rowsAffected}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ {gastosAdicionales.Count} gastos adicionales insertados correctamente");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR en InsertarGastosAdicionales: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        private void InsertarIngresosAdicionales(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            System.Diagnostics.Debug.WriteLine("--- Procesando ingresos adicionales ---");

            try
            {
                // ✅ CORRECTO: Usar el nombre del campo oculto
                string ingresosAdicionalesJson = Request.Form["hiddenIngresosAdicionales"] ?? "[]";
                System.Diagnostics.Debug.WriteLine($"JSON ingresos adicionales recibido: '{ingresosAdicionalesJson}'");

                if (string.IsNullOrEmpty(ingresosAdicionalesJson) || ingresosAdicionalesJson == "[]")
                {
                    System.Diagnostics.Debug.WriteLine("ℹ️ No hay ingresos adicionales para insertar");
                    return;
                }

                // Deserializar ingresos adicionales
                List<IngresoAdicionalData> ingresosAdicionales;
                try
                {
                    ingresosAdicionales = JsonConvert.DeserializeObject<List<IngresoAdicionalData>>(ingresosAdicionalesJson) ?? new List<IngresoAdicionalData>();
                    System.Diagnostics.Debug.WriteLine($"Ingresos adicionales deserializados: {ingresosAdicionales.Count}");
                }
                catch (JsonException ex)
                {
                    throw new Exception($"Error deserializando ingresos adicionales: {ex.Message}", ex);
                }

                // Insertar cada ingreso adicional usando el ESQUEMA REAL
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
                        System.Diagnostics.Debug.WriteLine($"Insertando ingreso: {ingreso.categoria} - S/{ingreso.soles} - $/{ingreso.dolares}");

                        using (SqlCommand cmd = new SqlCommand(queryInsert, conn, transaction))
                        {
                            // ✅ USAR COLUMNAS REALES DE LA BASE DE DATOS
                            cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                            cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.categoria ?? "");
                            cmd.Parameters.AddWithValue("@soles", ingreso.soles ?? 0);
                            cmd.Parameters.AddWithValue("@dolares", ingreso.dolares ?? 0);
                            cmd.Parameters.AddWithValue("@descripcion", ingreso.descripcion ?? "");

                            int rowsAffected = cmd.ExecuteNonQuery();
                            System.Diagnostics.Debug.WriteLine($"Ingreso insertado, filas afectadas: {rowsAffected}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ {ingresosAdicionales.Count} ingresos adicionales insertados correctamente");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR en InsertarIngresosAdicionales: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        #endregion

        #region Métodos para Insertar Datos Detallados

        private List<T> ProcesarDatosDetallados<T>(string hiddenFieldName)
        {
            string jsonData = Request.Form[hiddenFieldName] ?? "[]";
            try
            {
                return JsonConvert.DeserializeObject<List<T>>(jsonData) ?? new List<T>();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error procesando {hiddenFieldName}: {ex.Message}");
                return new List<T>();
            }
        }

        private void InsertarTodosLosDatosDetallados(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje,
     List<PeajeDetallado> peajes, List<AlimentacionDetallada> alimentacion, List<ApoyoSeguridadDetallada> apoyoSeguridad,
     List<ReparacionesDetalladas> reparaciones, List<MovilidadDetallada> movilidad, List<EncapadaDetallada> encapada,
     List<HospedajeDetallado> hospedaje, List<CombustibleDetallado> combustible)
        {
            try
            {
                // SOLO insertar en tablas de detalle para categorías que tienen modal
                if (peajes.Count > 0)
                    InsertarPeajesDetallados(conn, transaction, numeroOrdenViaje, peajes);

                if (reparaciones.Count > 0)
                    InsertarReparacionesDetalladas(conn, transaction, numeroOrdenViaje, reparaciones);

                if (hospedaje.Count > 0)
                    InsertarHospedajeDetallado(conn, transaction, numeroOrdenViaje, hospedaje);

                if (combustible.Count > 0)
                    InsertarCombustibleDetallado(conn, transaction, numeroOrdenViaje, combustible);

                // NO intentar insertar alimentación, apoyo-seguridad, movilidad, encapada en tablas de detalle
                // Estas categorías se guardan SOLO en la tabla Egresos a través de InsertarGastosPrincipales()

                System.Diagnostics.Debug.WriteLine("Datos detallados insertados correctamente (solo categorías con modal)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando datos detallados: {ex.Message}");
                throw;
            }
        }



        private void InsertarDescuentosReintegros(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            try
            {
                // Obtener datos del formulario
                decimal descuentoSoles = decimal.TryParse(Request.Form["txtDescuentoSoles"], out var ds) ? ds : 0;
                decimal descuentoDolares = decimal.TryParse(Request.Form["txtDescuentoDolares"], out var dd) ? dd : 0;
                decimal reintegroSoles = decimal.TryParse(Request.Form["txtReintegroSoles"], out var rs) ? rs : 0;
                decimal reintegroDolares = decimal.TryParse(Request.Form["txtReintegroDolares"], out var rd) ? rd : 0;

                string obsDescuento = Request.Form["txtObservacionesDescuento"] ?? "";
                string obsReintegro = Request.Form["txtObservacionesReintegro"] ?? "";

                // Solo insertar si hay algún dato
                if (descuentoSoles > 0 || descuentoDolares > 0 || reintegroSoles > 0 || reintegroDolares > 0 ||
                    !string.IsNullOrEmpty(obsDescuento) || !string.IsNullOrEmpty(obsReintegro))
                {
                    string query = @"
                INSERT INTO DescuentosReintegros (
                    numeroOrdenViaje, descuentoSoles, descuentoDolares, 
                    reintegroSoles, reintegroDolares, observacionesDescuento, observacionesReintegro
                )
                VALUES (
                    @numeroOrdenViaje, @descuentoSoles, @descuentoDolares,
                    @reintegroSoles, @reintegroDolares, @obsDescuento, @obsReintegro
                )";

                    using (SqlCommand cmd = new SqlCommand(query, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@descuentoSoles", descuentoSoles);
                        cmd.Parameters.AddWithValue("@descuentoDolares", descuentoDolares);
                        cmd.Parameters.AddWithValue("@reintegroSoles", reintegroSoles);
                        cmd.Parameters.AddWithValue("@reintegroDolares", reintegroDolares);
                        cmd.Parameters.AddWithValue("@obsDescuento", string.IsNullOrEmpty(obsDescuento) ? (object)DBNull.Value : obsDescuento);
                        cmd.Parameters.AddWithValue("@obsReintegro", string.IsNullOrEmpty(obsReintegro) ? (object)DBNull.Value : obsReintegro);

                        cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine("Descuentos y reintegros insertados");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando descuentos/reintegros: {ex.Message}");
                throw;
            }
        }
        private void InsertarPeajesDetallados(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<PeajeDetallado> peajes)
        {
            try
            {
                foreach (var peaje in peajes)
                {
                    string queryPeaje = @"
                        INSERT INTO DetallePeajes (
                            numeroOrdenViaje, estacion, fecha, numeroComprobante, 
                            montoSoles, montoDolares, observaciones
                        )
                        VALUES (
                            @numeroOrdenViaje, @estacion, @fecha, @numeroComprobante,
                            @montoSoles, @montoDolares, @observaciones
                        )";

                    using (SqlCommand cmd = new SqlCommand(queryPeaje, conn, transaction))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@estacion", peaje.estacion);
                        cmd.Parameters.AddWithValue("@fecha", FechaSeguraSQL(peaje.fecha));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(peaje.comprobante) ? (object)DBNull.Value : peaje.comprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", peaje.soles);
                        cmd.Parameters.AddWithValue("@montoDolares", peaje.dolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(peaje.observaciones) ? (object)DBNull.Value : peaje.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Insertados {peajes.Count} peajes detallados");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando peajes detallados: {ex.Message}");
                throw;
            }
        }

        private void InsertarAlimentacionDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<AlimentacionDetallada> registros)
        {
            try
            {
                foreach (var registro in registros)
                {
                    string query = @"
                        INSERT INTO DetalleAlimentacion (
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(registro.fechaComprobante));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de alimentación detallada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando alimentación detallada: {ex.Message}");
                throw;
            }
        }

        private void InsertarApoyoSeguridadDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<ApoyoSeguridadDetallada> registros)
        {
            try
            {
                foreach (var registro in registros)
                {
                    string query = @"
                        INSERT INTO DetalleApoyoSeguridad (
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", registro.fechaComprobante);
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de apoyo-seguridad detallada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando apoyo-seguridad detallada: {ex.Message}");
                throw;
            }
        }

        private void InsertarReparacionesDetalladas(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<ReparacionesDetalladas> registros)
        {
            try
            {
                foreach (var registro in registros)
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(registro.fechaComprobante));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de reparaciones detalladas");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando reparaciones detalladas: {ex.Message}");
                throw;
            }
        }

        private void InsertarMovilidadDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<MovilidadDetallada> registros)
        {
            try
            {
                foreach (var registro in registros)
                {
                    string query = @"
                        INSERT INTO DetalleMovilidad (
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(registro.fechaComprobante));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de movilidad detallada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando movilidad detallada: {ex.Message}");
                throw;
            }
        }

        private void InsertarEncapadaDetallada(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<EncapadaDetallada> registros)
        {
            try
            {
                foreach (var registro in registros)
                {
                    string query = @"
                        INSERT INTO DetalleEncapada (
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(registro.fechaComprobante));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de encapada detallada");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando encapada detallada: {ex.Message}");
                throw;
            }
        }

        private void InsertarHospedajeDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<HospedajeDetallado> registros)
        {
            try
            {
                foreach (var registro in registros)
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(registro.fechaComprobante));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de hospedaje detallado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando hospedaje detallado: {ex.Message}");
                throw;
            }
        }

        private void InsertarCombustibleDetallado(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje, List<CombustibleDetallado> registros)
        {
            try
            {
                foreach (var registro in registros)
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
                        cmd.Parameters.AddWithValue("@fechaComprobante", FechaSeguraSQL(registro.fechaComprobante));
                        cmd.Parameters.AddWithValue("@numeroComprobante", string.IsNullOrEmpty(registro.numeroComprobante) ? (object)DBNull.Value : registro.numeroComprobante);
                        cmd.Parameters.AddWithValue("@montoSoles", registro.montoSoles);
                        cmd.Parameters.AddWithValue("@montoDolares", registro.montoDolares);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(registro.observaciones) ? (object)DBNull.Value : registro.observaciones);

                        cmd.ExecuteNonQuery();
                    }
                }
                System.Diagnostics.Debug.WriteLine($"Insertados {registros.Count} registros de combustible detallado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error insertando combustible detallado: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Métodos Auxiliares

        private string DeterminarTipoViaje(List<LiquidacionData> liquidaciones)
        {
            bool tieneNacional = false;
            bool tieneInternacional = false;

            foreach (var liquidacion in liquidaciones)
            {
                foreach (var subTramo in liquidacion.subTramos)
                {
                    // Revisar operaciones de carga
                    if (subTramo.operacionCarga != null && subTramo.operacionCarga.activa)
                    {
                        if (subTramo.operacionCarga.esInternacional)
                            tieneInternacional = true;
                        else
                            tieneNacional = true;
                    }

                    // Revisar operaciones de descarga
                    if (subTramo.operacionDescarga != null && subTramo.operacionDescarga.activa)
                    {
                        if (subTramo.operacionDescarga.esInternacional)
                            tieneInternacional = true;
                        else
                            tieneNacional = true;
                    }
                }
            }

            if (tieneNacional && tieneInternacional)
                return "MIXTO";
            else if (tieneInternacional)
                return "INTERNACIONAL";
            else
                return "NACIONAL";
        }

        private string DeterminarTipoLiquidacion(LiquidacionData liquidacion)
        {
            bool tieneNacional = false;
            bool tieneInternacional = false;

            foreach (var subTramo in liquidacion.subTramos)
            {
                if (subTramo.operacionCarga != null && subTramo.operacionCarga.activa)
                {
                    if (subTramo.operacionCarga.esInternacional)
                        tieneInternacional = true;
                    else
                        tieneNacional = true;
                }

                if (subTramo.operacionDescarga != null && subTramo.operacionDescarga.activa)
                {
                    if (subTramo.operacionDescarga.esInternacional)
                        tieneInternacional = true;
                    else
                        tieneNacional = true;
                }
            }

            if (tieneNacional && tieneInternacional)
                return "MIXTO";
            else if (tieneInternacional)
                return "INTERNACIONAL";
            else
                return "NACIONAL";
        }

        

        private string GetClientIPAddress()
        {
            try
            {
                string ip = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
                if (string.IsNullOrEmpty(ip))
                {
                    ip = Request.ServerVariables["REMOTE_ADDR"];
                }
                return ip;
            }
            catch
            {
                return "Unknown";
            }
        }

        private void MostrarResultadoExitoso(string numeroOrdenViaje, int cantidadLiquidaciones, int totalRegistrosDetallados)
        {
            try
            {
                hfNumeroOrdenViaje.Value = numeroOrdenViaje;

                string detalleRegistros = $"Liquidaciones: {cantidadLiquidaciones}, Total registros detallados: {totalRegistrosDetallados}";

                lblErrores.Text = $@"<div class='alert alert-success'>
                    ✅ Orden de viaje guardada correctamente.<br/>
                    <strong>Número ingresado:</strong> {numeroOrdenViaje}<br/>
                    <strong>Liquidaciones procesadas:</strong> {cantidadLiquidaciones}<br/>
                    <strong>Total registros detallados:</strong> {totalRegistrosDetallados}<br/>
                </div>";

                ClientScript.RegisterStartupScript(this.GetType(), "mensaje",
                    $@"alert('Orden de viaje guardada correctamente.\n' +
                       'Número: {numeroOrdenViaje}\n' +
                       'Liquidaciones: {cantidadLiquidaciones}\n' +
                       'Total registros detallados: {totalRegistrosDetallados}');", true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error mostrando resultado: {ex.Message}");
            }
        }

        #endregion
        #region Métodos de Preservación y Restauración de Datos

        private void PreservarDatosDinamicos()
        {
            try
            {
                // Preservar datos de liquidaciones
                string liquidacionesData = Request.Form["hiddenLiquidacionesData"] ?? "[]";
                ViewState["LiquidacionesData"] = liquidacionesData;

                // Preservar datos de gastos detallados
                ViewState["PeajesDetalle"] = Request.Form["hiddenPeajesDetalle"] ?? "[]";
                ViewState["AlimentacionDetalle"] = Request.Form["hiddenAlimentacionDetalle"] ?? "[]";
                ViewState["ApoyoSeguridadDetalle"] = Request.Form["hiddenApoyoSeguridadDetalle"] ?? "[]";
                ViewState["ReparacionesDetalle"] = Request.Form["hiddenReparacionesDetalle"] ?? "[]";
                ViewState["MovilidadDetalle"] = Request.Form["hiddenMovilidadDetalle"] ?? "[]";
                ViewState["EncapadaDetalle"] = Request.Form["hiddenEncapadaDetalle"] ?? "[]";
                ViewState["HospedajeDetalle"] = Request.Form["hiddenHospedajeDetalle"] ?? "[]";
                ViewState["CombustibleDetalle"] = Request.Form["hiddenCombustibleDetalle"] ?? "[]";

                // Preservar datos financieros
                ViewState["GastosAdicionales"] = Request.Form["hiddenGastosAdicionales"] ?? "[]";
                ViewState["IngresosAdicionales"] = Request.Form["hiddenIngresosAdicionales"] ?? "[]";

                // Preservar datos del formulario principal
                PreservarDatosFormularioPrincipal();

                System.Diagnostics.Debug.WriteLine("Datos dinámicos preservados correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error preservando datos: {ex.Message}");
            }
        }

        private void PreservarDatosFormularioPrincipal()
        {
            ViewState["NumeroOrdenViaje"] = txtNumeroOrdenViaje.Value;
            ViewState["FechaSalida"] = txtFechaSalida.Value;
            ViewState["HoraSalida"] = txtHoraSalida.Value;
            ViewState["FechaLlegada"] = txtFechaLlegada.Value;
            ViewState["HoraLlegada"] = txtHoraLlegada.Value;
            ViewState["Conductor"] = ddlConductor.SelectedValue;
            ViewState["PlacaTracto"] = ddlPlacaTracto.SelectedValue;
            ViewState["PlacaCarreta"] = ddlPlacaCarreta.SelectedValue;
            ViewState["Observaciones"] = txtObservaciones.Value;

            // Preservar datos financieros de la tercera pestaña
            PreservarDatosFinancieros();
        }

        private void PreservarDatosFinancieros()
        {
            var datosFinancieros = new Dictionary<string, string>();

            // Ingresos
            datosFinancieros["txtDespachoSoles"] = Request.Form["txtDespachoSoles"] ?? "";
            datosFinancieros["txtDespachoDolares"] = Request.Form["txtDespachoDolares"] ?? "";
            datosFinancieros["txtMensualidadSoles"] = Request.Form["txtMensualidadSoles"] ?? "";
            datosFinancieros["txtMensualidadDolares"] = Request.Form["txtMensualidadDolares"] ?? "";
            datosFinancieros["txtOtrosSoles"] = Request.Form["txtOtrosSoles"] ?? "";
            datosFinancieros["txtOtrosDolares"] = Request.Form["txtOtrosDolares"] ?? "";
            datosFinancieros["txtPrestamoSoles"] = Request.Form["txtPrestamoSoles"] ?? "";
            datosFinancieros["txtPrestamoDolares"] = Request.Form["txtPrestamoDolares"] ?? "";

            // Descripciones
            datosFinancieros["txtDescDespacho"] = Request.Form["txtDescDespacho"] ?? "";
            datosFinancieros["txtDescMensualidad"] = Request.Form["txtDescMensualidad"] ?? "";
            datosFinancieros["txtDescOtros"] = Request.Form["txtDescOtros"] ?? "";
            datosFinancieros["txtDescPrestamo"] = Request.Form["txtDescPrestamo"] ?? "";

            // Gastos principales
            datosFinancieros["txtAlimentacionSoles"] = Request.Form["txtAlimentacionSoles"] ?? "";
            datosFinancieros["txtAlimentacionDolares"] = Request.Form["txtAlimentacionDolares"] ?? "";
            datosFinancieros["txtApoyoSeguridadSoles"] = Request.Form["txtApoyoSeguridadSoles"] ?? "";
            datosFinancieros["txtApoyoSeguridadDolares"] = Request.Form["txtApoyoSeguridadDolares"] ?? "";
            datosFinancieros["txtMovilidadSoles"] = Request.Form["txtMovilidadSoles"] ?? "";
            datosFinancieros["txtMovilidadDolares"] = Request.Form["txtMovilidadDolares"] ?? "";
            datosFinancieros["txtEncapadaSoles"] = Request.Form["txtEncapadaSoles"] ?? "";
            datosFinancieros["txtEncapadaDolares"] = Request.Form["txtEncapadaDolares"] ?? "";

            // Observaciones
            datosFinancieros["txtObservacionesLiquidacion"] = Request.Form["txtObservacionesLiquidacion"] ?? "";

            ViewState["DatosFinancieros"] = JsonConvert.SerializeObject(datosFinancieros);
        }

        private void RestaurarDatosPreservados()
        {
            try
            {
                // Restaurar datos del formulario principal
                if (ViewState["NumeroOrdenViaje"] != null)
                    txtNumeroOrdenViaje.Value = ViewState["NumeroOrdenViaje"].ToString();
                if (ViewState["FechaSalida"] != null)
                    txtFechaSalida.Value = ViewState["FechaSalida"].ToString();
                if (ViewState["HoraSalida"] != null)
                    txtHoraSalida.Value = ViewState["HoraSalida"].ToString();
                if (ViewState["FechaLlegada"] != null)
                    txtFechaLlegada.Value = ViewState["FechaLlegada"].ToString();
                if (ViewState["HoraLlegada"] != null)
                    txtHoraLlegada.Value = ViewState["HoraLlegada"].ToString();
                if (ViewState["Conductor"] != null)
                    ddlConductor.SelectedValue = ViewState["Conductor"].ToString();
                if (ViewState["PlacaTracto"] != null)
                    ddlPlacaTracto.SelectedValue = ViewState["PlacaTracto"].ToString();
                if (ViewState["PlacaCarreta"] != null)
                    ddlPlacaCarreta.SelectedValue = ViewState["PlacaCarreta"].ToString();
                if (ViewState["Observaciones"] != null)
                    txtObservaciones.Value = ViewState["Observaciones"].ToString();

                // Generar JavaScript para restaurar datos dinámicos
                GenerarScriptRestauracionDatos();

                System.Diagnostics.Debug.WriteLine("Datos preservados restaurados correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restaurando datos: {ex.Message}");
            }
        }

        private void GenerarScriptRestauracionDatos()
        {
            string script = @"
        <script type='text/javascript'>
            $(document).ready(function() {
                console.log('🔄 Restaurando datos preservados...');
                
                // Restaurar datos de liquidaciones
                " + GenerarRestauracionLiquidaciones() + @"
                
                // Restaurar datos de gastos detallados
                " + GenerarRestauracionGastosDetallados() + @"
                
                // Restaurar datos financieros
                " + GenerarRestauracionDatosFinancieros() + @"
                
                // Recalcular totales
                if (typeof calcularTotales === 'function') {
                    calcularTotales();
                }
                
                console.log('✅ Datos restaurados completamente');
            });
        </script>";

            ClientScript.RegisterStartupScript(this.GetType(), "RestaurarDatos", script, false);
        }

        private string GenerarRestauracionLiquidaciones()
        {
            string liquidacionesData = ViewState["LiquidacionesData"]?.ToString() ?? "[]";
            if (liquidacionesData != "[]")
            {
                return $@"
            try {{
                console.log('Restaurando liquidaciones...');
                $('#hiddenLiquidacionesData').val('{liquidacionesData.Replace("'", "\\'")}');
                
                // Si hay funciones específicas para restaurar liquidaciones
                if (typeof restaurarLiquidacionesDesdeJSON === 'function') {{
                    restaurarLiquidacionesDesdeJSON();
                }}
            }} catch (e) {{
                console.error('Error restaurando liquidaciones:', e);
            }}";
            }
            return "console.log('No hay liquidaciones para restaurar');";
        }

        private string GenerarRestauracionGastosDetallados()
        {
            var restauraciones = new List<string>();

            // Lista de categorías de gastos detallados
            var categorias = new Dictionary<string, string>
            {
                {"PeajesDetalle", "hiddenPeajesDetalle"},
                {"AlimentacionDetalle", "hiddenAlimentacionDetalle"},
                {"ApoyoSeguridadDetalle", "hiddenApoyoSeguridadDetalle"},
                {"ReparacionesDetalle", "hiddenReparacionesDetalle"},
                {"MovilidadDetalle", "hiddenMovilidadDetalle"},
                {"EncapadaDetalle", "hiddenEncapadaDetalle"},
                {"HospedajeDetalle", "hiddenHospedajeDetalle"},
                {"CombustibleDetalle", "hiddenCombustibleDetalle"}
            };

            foreach (var categoria in categorias)
            {
                string datos = ViewState[categoria.Key]?.ToString() ?? "[]";
                if (datos != "[]")
                {
                    restauraciones.Add($@"
                $('#{categoria.Value}').val('{datos.Replace("'", "\\'")}');
                console.log('Restaurado {categoria.Key}');");
                }
            }

            // Restaurar funciones específicas de carga
            restauraciones.Add(@"
        try {
            if (typeof cargarPeajesEnModal === 'function') cargarPeajesEnModal();
            if (typeof cargarAlimentacionEnModal === 'function') cargarAlimentacionEnModal();
            if (typeof cargarApoyoSeguridadEnModal === 'function') cargarApoyoSeguridadEnModal();
            if (typeof cargarReparacionesEnModal === 'function') cargarReparacionesEnModal();
            if (typeof cargarMovilidadEnModal === 'function') cargarMovilidadEnModal();
            if (typeof cargarEncapadaEnModal === 'function') cargarEncapadaEnModal();
            if (typeof cargarHospedajeEnModal === 'function') cargarHospedajeEnModal();
            if (typeof cargarCombustibleEnModal === 'function') cargarCombustibleEnModal();
        } catch (e) {
            console.error('Error cargando datos en modales:', e);
        }");

            return string.Join("\n", restauraciones);
        }

        private string GenerarRestauracionDatosFinancieros()
        {
            string datosFinancierosJson = ViewState["DatosFinancieros"]?.ToString();
            if (!string.IsNullOrEmpty(datosFinancierosJson))
            {
                try
                {
                    var datosFinancieros = JsonConvert.DeserializeObject<Dictionary<string, string>>(datosFinancierosJson);
                    var restauraciones = new List<string>();

                    foreach (var dato in datosFinancieros)
                    {
                        if (!string.IsNullOrEmpty(dato.Value))
                        {
                            restauraciones.Add($"$('input[name=\"{dato.Key}\"], textarea[name=\"{dato.Key}\"]').val('{dato.Value}');");
                        }
                    }

                    return string.Join("\n", restauraciones);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deserializando datos financieros: {ex.Message}");
                }
            }

            return "console.log('No hay datos financieros para restaurar');";
        }

        #endregion
        #region Método de Inicialización

        private void InicializarSistema()
        {
            try
            {
                // Verificar conexión a base de datos
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    lblErrores.Text = "Error: No se encontró la cadena de conexión 'ConexionSGV'";
                    return;
                }

                // Verificar que las tablas principales existan
                VerificarTablasPrincipales();

                System.Diagnostics.Debug.WriteLine("Sistema inicializado correctamente");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inicializando sistema: {ex.Message}");
                lblErrores.Text = "Error al inicializar el sistema: " + ex.Message;
            }
        }

        private void VerificarTablasPrincipales()
        {
            try
            {
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string[] tablasRequeridas = {
                        "OrdenViaje", "Conductor", "Tracto", "Carreta", "Cliente",
                        "Producto", "CPIC", "Factura", "PlantaCarga", "PlantaDescarga"
                    };

                    foreach (string tabla in tablasRequeridas)
                    {
                        string query = $@"
                            SELECT COUNT(*) 
                            FROM INFORMATION_SCHEMA.TABLES 
                            WHERE TABLE_NAME = '{tabla}'";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            int existe = Convert.ToInt32(cmd.ExecuteScalar());
                            if (existe == 0)
                            {
                                System.Diagnostics.Debug.WriteLine($"Advertencia: La tabla '{tabla}' no existe");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando tablas: {ex.Message}");
            }
        }

        #endregion




        #region Validación de Fechas - Solución Rápida

        /// <summary>
        /// Valida si una fecha está en rango válido para SQL Server DateTime
        /// </summary>
        private bool EsFechaValidaSQL(DateTime fecha)
        {
            return fecha >= new DateTime(1753, 1, 1) && fecha <= new DateTime(9999, 12, 31) && fecha != DateTime.MinValue;
        }

        /// <summary>
        /// Convierte DateTime a object para SQL de forma segura
        /// </summary>
        private object FechaSeguraSQL(DateTime fecha)
        {
            return EsFechaValidaSQL(fecha) ? (object)fecha : DBNull.Value;
        }

        #endregion
    }
}