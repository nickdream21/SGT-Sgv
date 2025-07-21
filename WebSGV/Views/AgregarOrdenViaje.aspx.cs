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
        #region Clases de Datos Actualizadas

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
            public List<ProductoOperacion> productos { get; set; } = new List<ProductoOperacion>();
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
        /// Clases para liquidación financiera (mantener compatibilidad)
        /// </summary>
        public class GastoAdicional
        {
            public string nombreCategoria { get; set; }
            public decimal soles { get; set; }
            public decimal dolares { get; set; }
            public string descripcion { get; set; }
        }

        public class IngresoAdicional
        {
            public string nombreCategoria { get; set; }
            public decimal soles { get; set; }
            public decimal dolares { get; set; }
            public string descripcion { get; set; }
        }

        #endregion

        #region Eventos de Página

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                CargarConductores();
                CargarPlacasTracto();
                CargarPlacasCarreta();
            }
        }

        #endregion

        #region Métodos de Carga de Datos

        private void CargarConductores()
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

        private void CargarPlacasTracto()
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

        private void CargarPlacasCarreta()
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
                    c.ruc
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
                                    ruc = reader["ruc"] != DBNull.Value ? reader["ruc"].ToString() : ""
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
            ruc,
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
                                    ruc = reader["ruc"] != DBNull.Value ? reader["ruc"].ToString() : "",
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
                    c.valorTotalFlete,
                    c.fechaEmision,
                    f.numeroFactura
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
                                if (reader["valorTotalFlete"] != DBNull.Value)
                                {
                                    decimal valor = Convert.ToDecimal(reader["valorTotalFlete"]);
                                    descripcion += $" - ${valor:N2}";
                                }

                                cpics.Add(new
                                {
                                    idCPIC = Convert.ToInt32(reader["idCPIC"]),
                                    numeroCPIC = reader["numeroCPIC"].ToString(),
                                    valorTotalFlete = reader["valorTotalFlete"] != DBNull.Value ?
                                                     Convert.ToDecimal(reader["valorTotalFlete"]) : 0,
                                    fechaEmision = reader["fechaEmision"] != DBNull.Value ?
                                                  Convert.ToDateTime(reader["fechaEmision"]).ToString("dd/MM/yyyy") : "",
                                    numeroFactura = reader["numeroFactura"] != DBNull.Value ?
                                                   reader["numeroFactura"].ToString() : "",
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
            f.numeroPedido,
            f.idCliente,
            c.nombre as nombreCliente,
            c.ruc
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
                                // Solo mostrar el número de factura
                                string numeroFactura = reader["numeroFactura"].ToString();
                                string descripcionCompleta = numeroFactura;

                                // Agregar información del pedido si existe (opcional)
                                if (reader["numeroPedido"] != DBNull.Value)
                                {
                                    descripcionCompleta += $" (Pedido: {reader["numeroPedido"]})";
                                }

                                facturas.Add(new
                                {
                                    idFactura = Convert.ToInt32(reader["idFactura"]),
                                    numeroFactura = numeroFactura,
                                    valorTotal = Convert.ToDecimal(reader["valorTotal"]),
                                    fechaEmision = Convert.ToDateTime(reader["fechaEmision"]).ToString("dd/MM/yyyy"),
                                    numeroPedido = reader["numeroPedido"] != DBNull.Value ?
                                                  reader["numeroPedido"].ToString() : "",
                                    idCliente = Convert.ToInt32(reader["idCliente"]),
                                    nombreCliente = reader["nombreCliente"].ToString(),
                                    ruc = reader["ruc"] != DBNull.Value ? reader["ruc"].ToString() : "",
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

        private string GenerarNumeroOrdenViaje()
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand("GenerarNumeroOrdenViaje", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    try
                    {
                        conn.Open();
                        object result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "";
                    }
                    catch (Exception ex)
                    {
                        lblErrores.Text = "Error al generar número de orden: " + ex.Message;
                        return "";
                    }
                }
            }
        }

        #endregion

        #region Validaciones Actualizadas

        private string ValidarDatosGenerales(DateTime fechaSalida, DateTime fechaLlegada, string horaSalida, string horaLlegada, string conductor, string placaTracto, string placaCarreta)
        {
            string mensajeError = "";

            // Validar fechas y horas
            DateTime fechaActual = DateTime.Now;
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
                if (fechaLlegada > fechaActual)
                {
                    mensajeError += "La 'Fecha de Llegada' no puede ser mayor a la fecha actual (" + fechaActual.ToString("dd/MM/yyyy") + ").\n";
                }
            }

            // Validar dropdowns
            if (string.IsNullOrEmpty(conductor))
            {
                mensajeError += "Por favor, seleccione un 'Conductor'.\n";
            }
            if (string.IsNullOrEmpty(placaTracto))
            {
                mensajeError += "Por favor, seleccione una 'Placa Tracto'.\n";
            }
            if (string.IsNullOrEmpty(placaCarreta))
            {
                mensajeError += "Por favor, seleccione una 'Placa Carreta'.\n";
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
                    // Solo validar que las guías estén presentes si son requeridas
                    break;
            }

            return errores;
        }

        private string ValidarOperacion(OperacionData operacion, string prefijo, string tipoOperacion)
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

            if (operacion.esInternacional && (!operacion.idCPIC.HasValue || operacion.idCPIC.Value <= 0))
            {
                errores += prefijo + $"Los clientes internacionales requieren CPIC para {tipoOperacion}.\n";
            }

            if (!operacion.esInternacional && (!operacion.idFactura.HasValue || operacion.idFactura.Value <= 0))
            {
                errores += prefijo + $"Los clientes nacionales requieren factura para {tipoOperacion}.\n";
            }

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
                }
            }

            return errores;
        }

        private string ValidarLiquidacion()
        {
            string errores = "";

            // Obtener datos de liquidación del form
            decimal peajesSoles = decimal.TryParse(Request.Form["txtPeajesSoles"], out var ps) ? ps : 0;
            decimal peajesDolares = decimal.TryParse(Request.Form["txtPeajesDolares"], out var pd) ? pd : 0;
            decimal alimentacionSoles = decimal.TryParse(Request.Form["txtAlimentacionSoles"], out var as_) ? as_ : 0;
            decimal alimentacionDolares = decimal.TryParse(Request.Form["txtAlimentacionDolares"], out var ad) ? ad : 0;
            decimal despachoSoles = decimal.TryParse(Request.Form["txtDespachoSoles"], out var ds) ? ds : 0;
            decimal despachoDolares = decimal.TryParse(Request.Form["txtDespachoDolares"], out var dd) ? dd : 0;

            // Validar que no sean negativos
            if (peajesSoles < 0 || peajesDolares < 0)
            {
                errores += "Los valores de 'Peajes' no pueden ser negativos.\n";
            }
            if (alimentacionSoles < 0 || alimentacionDolares < 0)
            {
                errores += "Los valores de 'Alimentación' no pueden ser negativos.\n";
            }
            if (despachoSoles < 0 || despachoDolares < 0)
            {
                errores += "Los valores de 'Despacho' no pueden ser negativos.\n";
            }

            return errores;
        }



        #region Métodos de Validación Adicionales para Relaciones Cliente-Producto

        /// <summary>
        /// Valida que el producto pertenezca al cliente especificado
        /// </summary>
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

        /// <summary>
        /// Valida que la factura pertenezca al cliente especificado
        /// </summary>
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

        #endregion




        #region Validación Mejorada de Operaciones con Relaciones Cliente-Producto

        /// <summary>
        /// Validación mejorada de operaciones con verificación de relaciones cliente-producto
        /// </summary>
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

            // NUEVA VALIDACIÓN: Verificar que la factura pertenezca al cliente
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

                    // NUEVA VALIDACIÓN: Verificar que el producto pertenezca al cliente
                    if (!ValidarProductoPertenecceACliente(producto.idProducto, operacion.idCliente))
                    {
                        errores += prefijo + $"El producto con ID {producto.idProducto} no pertenece al cliente seleccionado en {tipoOperacion}.\n";
                    }
                }
            }

            return errores;
        }

        #endregion



        // Métodos de validación de existencia actualizados para nueva estructura
        private bool GuiaTransportistaExiste(string guiaTransportista)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = "SELECT COUNT(*) FROM SubTramos WHERE guiaTransportista = @guiaTransportista AND activo = 1";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        cmd.Parameters.AddWithValue("@guiaTransportista", guiaTransportista);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                    catch (Exception ex)
                    {
                        lblErrores.Text = "Error al validar el 'N° Guía Transportista': " + ex.Message;
                        return false;
                    }
                }
            }
        }

        private bool GuiaClienteExiste(string guiaCliente)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = "SELECT COUNT(*) FROM SubTramos WHERE guiaCliente = @guiaCliente AND activo = 1";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        cmd.Parameters.AddWithValue("@guiaCliente", guiaCliente);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                    catch (Exception ex)
                    {
                        lblErrores.Text = "Error al validar el 'N° Guía Cliente': " + ex.Message;
                        return false;
                    }
                }
            }
        }

        private bool ManifiestoExiste(string manifiesto)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = "SELECT COUNT(*) FROM SubTramos WHERE manifiesto = @manifiesto AND activo = 1";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    try
                    {
                        cmd.Parameters.AddWithValue("@manifiesto", manifiesto);
                        conn.Open();
                        int count = Convert.ToInt32(cmd.ExecuteScalar());
                        return count > 0;
                    }
                    catch (Exception ex)
                    {
                        lblErrores.Text = "Error al validar el 'N° Manifiesto': " + ex.Message;
                        return false;
                    }
                }
            }
        }

        #endregion

        #region Evento Principal - Guardar Orden Viaje

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            try
            {
                // 1. Generar número de orden automáticamente
                string numeroOrdenViaje = GenerarNumeroOrdenViaje();
                if (string.IsNullOrEmpty(numeroOrdenViaje))
                {
                    lblErrores.Text = "Error al generar el número de orden de viaje.";
                    return;
                }

                // 2. Obtener datos generales del viaje
                DateTime fechaSalida = string.IsNullOrEmpty(txtFechaSalida.Value) ? DateTime.MinValue : DateTime.Parse(txtFechaSalida.Value);
                DateTime fechaLlegada = string.IsNullOrEmpty(txtFechaLlegada.Value) ? DateTime.MinValue : DateTime.Parse(txtFechaLlegada.Value);
                string horaSalida = txtHoraSalida.Value;
                string horaLlegada = txtHoraLlegada.Value;
                string conductor = ddlConductor.SelectedValue;
                string placaTracto = ddlPlacaTracto.SelectedValue;
                string placaCarreta = ddlPlacaCarreta.SelectedValue;
                string observaciones = txtObservaciones.Value;

                // 3. Obtener observaciones de liquidación
                string observacionesLiquidacion = Request.Form["txtObservacionesLiquidacion"] ?? "";

                // 4. Obtener datos de liquidaciones
                string liquidacionesJson = Request.Form["hiddenLiquidacionesData"] ?? "[]";
                List<LiquidacionData> liquidaciones = new List<LiquidacionData>();

                try
                {
                    liquidaciones = JsonConvert.DeserializeObject<List<LiquidacionData>>(liquidacionesJson) ?? new List<LiquidacionData>();
                }
                catch (JsonException ex)
                {
                    lblErrores.Text = $"Error al procesar los datos de liquidaciones: {ex.Message}";
                    return;
                }

                // 5. Validaciones
                string errores = ValidarDatosGenerales(fechaSalida, fechaLlegada, horaSalida, horaLlegada, conductor, placaTracto, placaCarreta);
                errores += ValidarLiquidaciones(liquidaciones);
                errores += ValidarLiquidacion();

                if (!string.IsNullOrEmpty(errores))
                {
                    lblErrores.Text = errores.Replace("\n", "<br/>");
                    return;
                }

                // 6. Guardar en la base de datos
                string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            // 6.1 Insertar orden de viaje principal (sin liquidaciones complejas)
                            string queryOrdenViaje = @"
                                INSERT INTO OrdenViaje (numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
                                                      idTracto, idCarreta, idConductor, observaciones, observacionesLiquidacion, 
                                                      idCPIC, estadoViaje) 
                                OUTPUT INSERTED.idOrdenViaje 
                                VALUES (@numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
                                        @idTracto, @idCarreta, @idConductor, @observaciones, @observacionesLiquidacion, 
                                        0, 'PENDIENTE')";

                            int idOrdenViaje;
                            using (SqlCommand cmd = new SqlCommand(queryOrdenViaje, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                                cmd.Parameters.AddWithValue("@fechaSalida", fechaSalida == DateTime.MinValue ? (object)DBNull.Value : fechaSalida);
                                cmd.Parameters.AddWithValue("@horaSalida", string.IsNullOrEmpty(horaSalida) ? (object)DBNull.Value : horaSalida);
                                cmd.Parameters.AddWithValue("@fechaLlegada", fechaLlegada == DateTime.MinValue ? (object)DBNull.Value : fechaLlegada);
                                cmd.Parameters.AddWithValue("@horaLlegada", string.IsNullOrEmpty(horaLlegada) ? (object)DBNull.Value : horaLlegada);
                                cmd.Parameters.AddWithValue("@idTracto", placaTracto);
                                cmd.Parameters.AddWithValue("@idCarreta", placaCarreta);
                                cmd.Parameters.AddWithValue("@idConductor", conductor);
                                cmd.Parameters.AddWithValue("@observaciones", observaciones ?? (object)DBNull.Value);
                                cmd.Parameters.AddWithValue("@observacionesLiquidacion", observacionesLiquidacion ?? (object)DBNull.Value);

                                idOrdenViaje = (int)cmd.ExecuteScalar();
                            }

                            // 6.2 Insertar liquidaciones usando stored procedure
                            foreach (var liquidacion in liquidaciones)
                            {
                                using (SqlCommand cmd = new SqlCommand("InsertarLiquidacionCompleta", conn, transaction))
                                {
                                    cmd.CommandType = CommandType.StoredProcedure;
                                    cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                                    cmd.Parameters.AddWithValue("@numeroLiquidacion", liquidacion.numeroLiquidacion);
                                    cmd.Parameters.AddWithValue("@tipo", liquidacion.tipo ?? "NACIONAL");
                                    cmd.Parameters.AddWithValue("@descripcion", liquidacion.descripcion ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@observaciones", liquidacion.observaciones ?? (object)DBNull.Value);
                                    cmd.Parameters.AddWithValue("@jsonSubTramos", JsonConvert.SerializeObject(liquidacion.subTramos));

                                    var result = cmd.ExecuteScalar();
                                    // El stored procedure retorna el ID de la liquidación creada
                                }
                            }

                            // 6.3 Actualizar tipo de viaje basado en liquidaciones
                            using (SqlCommand cmd = new SqlCommand("ActualizarTipoViajeAutomatico", conn, transaction))
                            {
                                cmd.CommandType = CommandType.StoredProcedure;
                                cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                                cmd.ExecuteNonQuery();
                            }

                            // 6.4 Insertar liquidación financiera (usando stored procedures existentes)
                            InsertarDatosLiquidacion(conn, transaction, numeroOrdenViaje);

                            transaction.Commit();

                            // Mostrar número generado y mensaje de éxito
                            lblNumeroOrdenViaje.Text = numeroOrdenViaje;
                            hfNumeroOrdenViaje.Value = numeroOrdenViaje;
                            lblErrores.Text = "✅ Orden de viaje guardada correctamente. Número generado: " + numeroOrdenViaje;
                            ClientScript.RegisterStartupScript(this.GetType(), "mensaje",
                                $"alert('Orden de viaje guardada correctamente.\\nNúmero generado: {numeroOrdenViaje}'); window.location = 'OrdenesViajes.aspx';", true);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            lblErrores.Text = "Error al guardar la orden de viaje: " + ex.Message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblErrores.Text = "Error general: " + ex.Message;
            }
        }

        #endregion

        #region Método de Liquidación Financiera (Mantener compatibilidad)

        private void InsertarDatosLiquidacion(SqlConnection conn, SqlTransaction transaction, string numeroOrdenViaje)
        {
            // Obtener datos de liquidación del formulario
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

            // Insertar ingresos
            using (SqlCommand cmd = new SqlCommand("InsertarIngresos", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@despachoSoles", despachoSoles);
                cmd.Parameters.AddWithValue("@despachoDolares", despachoDolares);
                cmd.Parameters.AddWithValue("@prestamoSoles", prestamoSoles);
                cmd.Parameters.AddWithValue("@prestamosDolares", prestamoDolares);
                cmd.Parameters.AddWithValue("@mensualidadSoles", mensualidadSoles);
                cmd.Parameters.AddWithValue("@mensualidadDolares", mensualidadDolares);
                cmd.Parameters.AddWithValue("@otrosSoles", otrosSoles);
                cmd.Parameters.AddWithValue("@otrosDolares", otrosDolares);
                cmd.Parameters.AddWithValue("@totalSoles", despachoSoles + prestamoSoles + mensualidadSoles + otrosSoles);
                cmd.Parameters.AddWithValue("@totalDolares", despachoDolares + prestamoDolares + mensualidadDolares + otrosDolares);
                cmd.Parameters.AddWithValue("@descDespacho", descDespacho);
                cmd.Parameters.AddWithValue("@descMensualidad", descMensualidad);
                cmd.Parameters.AddWithValue("@descOtrosAutorizados", descOtros);
                cmd.Parameters.AddWithValue("@descPrestamo", descPrestamo);
                cmd.ExecuteNonQuery();
            }

            // Obtener datos de gastos
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

            // Descripciones de gastos
            string descPeajes = Request.Form["txtDescPeajes"] ?? "";
            string descAlimentacion = Request.Form["txtDescAlimentacion"] ?? "";
            string descApoyoSeguridad = Request.Form["txtDescApoyoSeguridad"] ?? "";
            string descReparaciones = Request.Form["txtDescReparaciones"] ?? "";
            string descMovilidad = Request.Form["txtDescMovilidad"] ?? "";
            string descEncapada = Request.Form["txtDescEncapada"] ?? "";
            string descHospedaje = Request.Form["txtDescHospedaje"] ?? "";
            string descCombustible = Request.Form["txtDescCombustible"] ?? "";

            // Insertar egresos
            using (SqlCommand cmd = new SqlCommand("InsertarEgresos", conn, transaction))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                cmd.Parameters.AddWithValue("@peajesSoles", peajesSoles);
                cmd.Parameters.AddWithValue("@peajesDolares", peajesDolares);
                cmd.Parameters.AddWithValue("@descPeajes", descPeajes);
                cmd.Parameters.AddWithValue("@alimentacionSoles", alimentacionSoles);
                cmd.Parameters.AddWithValue("@alimentacionDolares", alimentacionDolares);
                cmd.Parameters.AddWithValue("@descAlimentacion", descAlimentacion);
                cmd.Parameters.AddWithValue("@apoyoseguridadSoles", apoyoSeguridadSoles);
                cmd.Parameters.AddWithValue("@apoyoseguridadDolares", apoyoSeguridadDolares);
                cmd.Parameters.AddWithValue("@descApoyoSeguridad", descApoyoSeguridad);
                cmd.Parameters.AddWithValue("@reparacionesVariosSoles", reparacionesSoles);
                cmd.Parameters.AddWithValue("@repacionesVariosDolares", reparacionesDolares);
                cmd.Parameters.AddWithValue("@descReparacionesVarios", descReparaciones);
                cmd.Parameters.AddWithValue("@movilidadSoles", movilidadSoles);
                cmd.Parameters.AddWithValue("@movilidadDolares", movilidadDolares);
                cmd.Parameters.AddWithValue("@descMovilidad", descMovilidad);
                cmd.Parameters.AddWithValue("@encarpada_desencarpadaSoles", encapadaSoles);
                cmd.Parameters.AddWithValue("@encarpada_desencarpadaDolares", encapadaDolares);
                cmd.Parameters.AddWithValue("@descEncarpadaDesencarpada", descEncapada);
                cmd.Parameters.AddWithValue("@hospedajeSoles", hospedajeSoles);
                cmd.Parameters.AddWithValue("@hospedajeDolares", hospedajeDolares);
                cmd.Parameters.AddWithValue("@descHospedaje", descHospedaje);
                cmd.Parameters.AddWithValue("@combustibleSoles", combustibleSoles);
                cmd.Parameters.AddWithValue("@combustibleDolares", combustibleDolares);
                cmd.Parameters.AddWithValue("@descCombustible", descCombustible);
                cmd.ExecuteNonQuery();
            }

            // Insertar gastos adicionales
            string gastosAdicionalesJson = Request.Form["hiddenGastosAdicionales"] ?? "[]";
            try
            {
                var gastosAdicionales = JsonConvert.DeserializeObject<List<GastoAdicional>>(gastosAdicionalesJson) ?? new List<GastoAdicional>();
                foreach (var gasto in gastosAdicionales)
                {
                    using (SqlCommand cmd = new SqlCommand("InsertarGastoAdicional", conn, transaction))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@nombreCategoria", gasto.nombreCategoria ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@soles", gasto.soles);
                        cmd.Parameters.AddWithValue("@dolares", gasto.dolares);
                        cmd.Parameters.AddWithValue("@descripcion", gasto.descripcion ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (JsonException) { /* Ignorar errores en gastos adicionales */ }

            // Insertar ingresos adicionales
            string ingresosAdicionalesJson = Request.Form["hiddenIngresosAdicionales"] ?? "[]";
            try
            {
                var ingresosAdicionales = JsonConvert.DeserializeObject<List<IngresoAdicional>>(ingresosAdicionalesJson) ?? new List<IngresoAdicional>();
                foreach (var ingreso in ingresosAdicionales)
                {
                    using (SqlCommand cmd = new SqlCommand("InsertarIngresoAdicional", conn, transaction))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@nombreCategoria", ingreso.nombreCategoria ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@soles", ingreso.soles);
                        cmd.Parameters.AddWithValue("@dolares", ingreso.dolares);
                        cmd.Parameters.AddWithValue("@descripcion", ingreso.descripcion ?? (object)DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (JsonException) { /* Ignorar errores en ingresos adicionales */ }
        }

        #endregion
    }
}

