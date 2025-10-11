using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class BusquedaFactura : System.Web.UI.Page
    {
        // Configuración para archivos
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private readonly string[] ALLOWED_EXTENSIONS = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Cargar lista de clientes
                CargarClientes();
                // Crear directorios de upload
                CrearDirectoriosUpload();
                // Cargar lista de todas las facturas
                CargarListaFacturas();
            }
        }

        /// <summary>
        /// Carga la lista completa de facturas en el GridView
        /// </summary>
        private void CargarListaFacturas()
        {
            try
            {
                DataTable dt = ObtenerTodasLasFacturas();
                gvFacturas.DataSource = dt;
                gvFacturas.DataBind();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cargar lista de facturas: " + ex.Message);
                MostrarMensaje("Error al cargar la lista de facturas: " + ex.Message, "danger");
            }
        }

        /// <summary>
        /// Obtiene todas las facturas con información del cliente y CPIC
        /// </summary>
        private DataTable ObtenerTodasLasFacturas()
        {
            DataTable dt = new DataTable();

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"
                        SELECT 
                            f.idFactura,
                            f.numeroFactura,
                            f.numeroPedido,
                            f.valorTotal,
                            f.fechaEmision,
                            f.idCliente,
                            c.nombre as nombreCliente,
                            c.ruc as rucCliente,
                            cp.numeroCPIC
                        FROM Factura f
                        INNER JOIN Cliente c ON f.idCliente = c.idCliente
                        LEFT JOIN CPIC cp ON f.idFactura = cp.idFactura
                        ORDER BY f.fechaEmision DESC, f.numeroFactura DESC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener todas las facturas: " + ex.Message);
                throw new Exception("Error al obtener las facturas: " + ex.Message);
            }

            return dt;
        }

        /// <summary>
        /// Maneja los comandos del GridView de facturas
        /// </summary>
        protected void gvFacturas_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                if (e.CommandName == "CrearCPIC")
                {
                    int idFactura = Convert.ToInt32(e.CommandArgument);
                    RedirectToCrearCPIC(idFactura);
                }
                else if (e.CommandName == "EditarFactura")
                {
                    string numeroFactura = e.CommandArgument.ToString();
                    CargarFacturaParaEdicion(numeroFactura);
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al procesar la acción: " + ex.Message, "danger");
                Debug.WriteLine("Error en gvFacturas_RowCommand: " + ex.Message);
            }
        }

        /// <summary>
        /// Redirige a la página de crear CPIC con datos precargados
        /// </summary>
        private void RedirectToCrearCPIC(int idFactura)
        {
            try
            {
                // Obtener datos de la factura
                FacturaModel factura = ObtenerFacturaPorId(idFactura);
                if (factura != null)
                {
                    // Redirigir con parámetros en query string
                    string url = $"AgregarCPIC.aspx?numeroFactura={HttpUtility.UrlEncode(factura.NumeroFactura)}&valorTotal={factura.ValorTotal}";
                    Response.Redirect(url);
                }
                else
                {
                    MostrarMensaje("No se encontró la factura seleccionada.", "warning");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al redirigir a crear CPIC: " + ex.Message);
                MostrarMensaje("Error al acceder a la creación de CPIC: " + ex.Message, "danger");
            }
        }

        /// <summary>
        /// Obtiene una factura por su ID
        /// </summary>
        private FacturaModel ObtenerFacturaPorId(int idFactura)
        {
            FacturaModel factura = null;

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    string query = @"SELECT f.idFactura, f.numeroFactura, f.numeroPedido, f.valorTotal, f.fechaEmision, 
                                          f.idCliente, c.nombre as nombreCliente, c.ruc
                                    FROM Factura f
                                    INNER JOIN Cliente c ON f.idCliente = c.idCliente
                                    WHERE f.idFactura = @idFactura";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@idFactura", idFactura);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                factura = new FacturaModel
                                {
                                    IdFactura = Convert.ToInt32(reader["idFactura"]),
                                    NumeroFactura = reader["numeroFactura"].ToString(),
                                    NumeroPedido = reader["numeroPedido"] != DBNull.Value ? reader["numeroPedido"].ToString() : "",
                                    ValorTotal = Convert.ToDecimal(reader["valorTotal"]),
                                    FechaEmision = Convert.ToDateTime(reader["fechaEmision"]),
                                    IdCliente = Convert.ToInt32(reader["idCliente"]),
                                    NombreCliente = reader["nombreCliente"].ToString(),
                                    RucCliente = reader["ruc"] != DBNull.Value ? reader["ruc"].ToString() : ""
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener factura por ID: " + ex.Message);
                throw new Exception("Error al obtener la factura: " + ex.Message);
            }

            return factura;
        }

        /// <summary>
        /// Carga una factura específica en el panel de edición
        /// </summary>
        private void CargarFacturaParaEdicion(string numeroFactura)
        {
            try
            {
                txtBuscarFactura.Text = numeroFactura;
                BuscarFacturaClick(null, null); // Reutilizar el método existente
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cargar factura para edición: " + ex.Message);
                MostrarMensaje("Error al cargar la factura: " + ex.Message, "danger");
            }
        }

        /// <summary>
        /// Maneja la paginación del GridView de facturas
        /// </summary>
        protected void gvFacturas_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            try
            {
                gvFacturas.PageIndex = e.NewPageIndex;
                CargarListaFacturas();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cambiar página: " + ex.Message);
                MostrarMensaje("Error al cambiar de página: " + ex.Message, "danger");
            }
        }

        /// <summary>
        /// Refresca la lista de facturas
        /// </summary>
        protected void RefrescarListaFacturas(object sender, EventArgs e)
        {
            try
            {
                CargarListaFacturas();
                MostrarMensaje("Lista de facturas actualizada correctamente.", "success");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al refrescar lista: " + ex.Message);
                MostrarMensaje("Error al actualizar la lista: " + ex.Message, "danger");
            }
        }

        /// <summary>
        /// Carga la lista de clientes en el DropDownList
        /// </summary>
        private void CargarClientes()
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idCliente, 
                                           CASE 
                                               WHEN ruc IS NOT NULL AND ruc != '' THEN ruc + ' - ' + nombre
                                               ELSE nombre 
                                           END as nombreCompleto
                                    FROM Cliente 
                                    ORDER BY nombre";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        connection.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            ddlCliente.Items.Clear();
                            ddlCliente.Items.Add(new ListItem("-- Seleccione un Cliente --", ""));

                            while (reader.Read())
                            {
                                string idCliente = reader["idCliente"].ToString();
                                string nombreCompleto = reader["nombreCompleto"].ToString();
                                ddlCliente.Items.Add(new ListItem(nombreCompleto, idCliente));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cargar clientes: " + ex.Message);
                MostrarMensaje("Error al cargar la lista de clientes.", "danger");
            }
        }

        protected void BuscarFacturaClick(object sender, EventArgs e)
        {
            string numeroFactura = txtBuscarFactura.Text.Trim();

            if (string.IsNullOrEmpty(numeroFactura))
            {
                MostrarMensaje("Por favor, ingrese un número de factura para buscar.", "danger");
                return;
            }

            try
            {
                // Buscar la factura en la base de datos
                FacturaModel factura = ObtenerFactura(numeroFactura);

                if (factura != null)
                {
                    // Mostrar datos de la factura
                    MostrarDatosFactura(factura);
                    // Cargar documentos asociados
                    CargarDocumentosFactura(factura.IdFactura);
                    pnlResultados.Visible = true;
                    pnlNoResultados.Visible = false;
                    MostrarMensaje($"Factura {numeroFactura} encontrada correctamente.", "success");
                }
                else
                {
                    // No se encontró la factura
                    pnlResultados.Visible = false;
                    pnlNoResultados.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al buscar la factura: " + ex.Message, "danger");
                Debug.WriteLine("Error en BuscarFacturaClick: " + ex.Message);
            }
        }

        private FacturaModel ObtenerFactura(string numeroFactura)
        {
            FacturaModel factura = null;

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Consulta SQL para obtener la factura CON cliente
                    string queryFactura = @"SELECT f.idFactura, f.numeroFactura, f.numeroPedido, f.valorTotal, f.fechaEmision, 
                                                  f.idCliente, c.nombre as nombreCliente, c.ruc
                                          FROM Factura f
                                          INNER JOIN Cliente c ON f.idCliente = c.idCliente
                                          WHERE f.numeroFactura = @numeroFactura";

                    using (SqlCommand command = new SqlCommand(queryFactura, connection))
                    {
                        command.Parameters.AddWithValue("@numeroFactura", numeroFactura);

                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                factura = new FacturaModel
                                {
                                    IdFactura = Convert.ToInt32(reader["idFactura"]),
                                    NumeroFactura = reader["numeroFactura"].ToString(),
                                    NumeroPedido = reader["numeroPedido"] != DBNull.Value ? reader["numeroPedido"].ToString() : "",
                                    ValorTotal = Convert.ToDecimal(reader["valorTotal"]),
                                    FechaEmision = Convert.ToDateTime(reader["fechaEmision"]),
                                    // Campos del cliente
                                    IdCliente = Convert.ToInt32(reader["idCliente"]),
                                    NombreCliente = reader["nombreCliente"].ToString(),
                                    RucCliente = reader["ruc"] != DBNull.Value ? reader["ruc"].ToString() : ""
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener factura: " + ex.Message);
                throw new Exception("Error al obtener la factura: " + ex.Message);
            }

            return factura;
        }

        private void CargarDocumentosFactura(int idFactura)
        {
            try
            {
                DataTable dt = ObtenerDocumentosFactura(idFactura);
                gvDocumentos.DataSource = dt;
                gvDocumentos.DataBind();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cargar documentos: " + ex.Message);
                MostrarMensaje("Error al cargar documentos: " + ex.Message, "warning");
            }
        }

        private DataTable ObtenerDocumentosFactura(int idFactura)
        {
            DataTable dt = new DataTable();

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT 
                            idDocumento,
                            nombreOriginal,
                            tipoArchivo,
                            tamanoBytes,
                            fechaSubida,
                            usuarioSubida,
                            descripcion,
                            rutaArchivo
                        FROM DocumentosFactura 
                        WHERE idFactura = @idFactura AND activo = 1
                        ORDER BY fechaSubida DESC";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@idFactura", idFactura);

                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener documentos: " + ex.Message);
            }

            return dt;
        }

        private void MostrarDatosFactura(FacturaModel factura)
        {
            // Llenar los campos con la información de la factura
            txtNumFactura.Text = factura.NumeroFactura;
            txtNumPedido.Text = factura.NumeroPedido;
            txtFechaEmision.Text = factura.FechaEmision.ToString("yyyy-MM-dd");
            txtValorTotal.Text = factura.ValorTotal.ToString("N2");

            // Seleccionar el cliente correspondiente
            ddlCliente.SelectedValue = factura.IdCliente.ToString();

            // NUEVO: Guardar el número original para validaciones
            ViewState["NumeroFacturaOriginal"] = factura.NumeroFactura;
        }

        protected void HabilitarEdicion(object sender, EventArgs e)
        {
            // Habilitar la edición de los campos
            ddlCliente.Enabled = true;
            txtNumFactura.ReadOnly = false;  // NUEVO: Habilitar edición del número de factura
            txtNumPedido.ReadOnly = false;
            txtFechaEmision.ReadOnly = false;
            txtValorTotal.ReadOnly = false;

            // Habilitar upload de documentos
            fileUploadFactura.Enabled = true;
            txtDescripcionDoc.ReadOnly = false;

            // Habilitar validador del cliente
            rfvCliente.Enabled = true;

            // Mostrar el botón de guardar cambios
            btnHabilitarEdicion.Visible = false;
            btnGuardarCambios.Visible = true;

            MostrarMensaje("Modo de edición activado. Realice los cambios necesarios y presione 'Guardar Cambios'.", "info");
        }

        protected void GuardarCambios(object sender, EventArgs e)
        {
            try
            {
                // NUEVA VALIDACIÓN: Verificar que el número de factura no esté vacío
                string numeroFacturaNuevo = txtNumFactura.Text.Trim();
                if (string.IsNullOrEmpty(numeroFacturaNuevo))
                {
                    MostrarMensaje("El número de factura es requerido.", "danger");
                    return;
                }

                // NUEVA VALIDACIÓN: Verificar unicidad del número de factura
                string numeroFacturaOriginal = ViewState["NumeroFacturaOriginal"] as string;
                if (numeroFacturaNuevo != numeroFacturaOriginal && ExisteNumeroFactura(numeroFacturaNuevo))
                {
                    MostrarMensaje("El número de factura ya existe en el sistema.", "danger");
                    return;
                }

                // Validar que se haya seleccionado un cliente
                if (string.IsNullOrEmpty(ddlCliente.SelectedValue))
                {
                    MostrarMensaje("Debe seleccionar un cliente.", "danger");
                    return;
                }

                // Recolectar datos actualizados
                string numeroPedido = txtNumPedido.Text.Trim();
                int idCliente = Convert.ToInt32(ddlCliente.SelectedValue);

                // Validar número de pedido
                if (!string.IsNullOrEmpty(numeroPedido) && !ValidarNumeroPedido(numeroPedido))
                {
                    MostrarMensaje("El número de pedido debe tener exactamente 10 dígitos numéricos.", "danger");
                    return;
                }

                // Validar importe total
                if (!decimal.TryParse(txtValorTotal.Text, out decimal valorTotal) || valorTotal <= 0)
                {
                    MostrarMensaje("El valor total debe ser un número válido y mayor a 0.", "danger");
                    return;
                }

                // Validar fecha de emisión
                if (!DateTime.TryParse(txtFechaEmision.Text, out DateTime fechaEmision))
                {
                    MostrarMensaje("Formato de fecha inválido.", "danger");
                    return;
                }

                // Validar que la fecha no sea futura
                if (fechaEmision > DateTime.Now)
                {
                    MostrarMensaje("La fecha de emisión no puede ser mayor que la fecha actual.", "danger");
                    return;
                }

                // Validar archivo si se seleccionó uno
                string resultadoValidacion = ValidarArchivo();
                if (!string.IsNullOrEmpty(resultadoValidacion))
                {
                    MostrarMensaje(resultadoValidacion, "danger");
                    return;
                }

                // Actualizar la factura incluyendo el cliente Y procesar archivo
                bool actualizado = ActualizarFacturaConDocumento(numeroFacturaOriginal, numeroFacturaNuevo, numeroPedido, valorTotal, fechaEmision, idCliente);

                if (actualizado)
                {
                    // Volver al modo de sólo lectura
                    ddlCliente.Enabled = false;
                    txtNumFactura.ReadOnly = true;  // NUEVO: Deshabilitar edición del número de factura
                    txtNumPedido.ReadOnly = true;
                    txtFechaEmision.ReadOnly = true;
                    txtValorTotal.ReadOnly = true;

                    // Deshabilitar upload de documentos
                    fileUploadFactura.Enabled = false;
                    txtDescripcionDoc.ReadOnly = true;

                    // Deshabilitar validador del cliente
                    rfvCliente.Enabled = false;

                    btnHabilitarEdicion.Visible = true;
                    btnGuardarCambios.Visible = false;

                    // Limpiar campos de archivo y recargar documentos
                    LimpiarCamposArchivo();

                    // Obtener el ID de la factura para recargar documentos
                    FacturaModel factura = ObtenerFactura(numeroFacturaNuevo); // Usar el nuevo número
                    if (factura != null)
                    {
                        CargarDocumentosFactura(factura.IdFactura);
                        // Actualizar ViewState con el nuevo número
                        ViewState["NumeroFacturaOriginal"] = numeroFacturaNuevo;
                    }

                    // Actualizar la lista de facturas después de editar
                    CargarListaFacturas();

                    MostrarMensaje("Factura actualizada correctamente.", "success");
                }
                else
                {
                    MostrarMensaje("No se pudo actualizar la factura.", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar los cambios: " + ex.Message, "danger");
                Debug.WriteLine("Error en GuardarCambios: " + ex.Message);
            }
        }

        /// <summary>
        /// NUEVO: Verifica si existe un número de factura en la base de datos
        /// </summary>
        private bool ExisteNumeroFactura(string numeroFactura)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM Factura WHERE numeroFactura = @numeroFactura";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al verificar número de factura: " + ex.Message);
                return true; // Por seguridad, asumir que existe
            }
        }

        /// <summary>
        /// MODIFICADO: Actualiza la factura y procesa documento si existe - Ahora maneja cambio de número de factura
        /// </summary>
        private bool ActualizarFacturaConDocumento(string numeroFacturaOriginal, string numeroFacturaNuevo, string numeroPedido, decimal valorTotal, DateTime fechaEmision, int idCliente)
        {
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Usar transacción para garantizar la integridad de los datos
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Verificar si el número de pedido ya está asociado a otra factura
                        if (!string.IsNullOrEmpty(numeroPedido))
                        {
                            string queryVerificarPedido = @"SELECT COUNT(*) 
                                                          FROM Factura 
                                                          WHERE numeroPedido = @numeroPedido 
                                                          AND numeroFactura <> @numeroFacturaOriginal";

                            using (SqlCommand command = new SqlCommand(queryVerificarPedido, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                                command.Parameters.AddWithValue("@numeroFacturaOriginal", numeroFacturaOriginal);

                                int count = (int)command.ExecuteScalar();
                                if (count > 0)
                                {
                                    throw new Exception("El número de pedido ya está asociado a otra factura.");
                                }
                            }
                        }

                        // 2. Actualizar factura con el nuevo número
                        string queryActualizar = @"UPDATE Factura 
                                                 SET numeroFactura = @numeroFacturaNuevo,
                                                     numeroPedido = @numeroPedido, 
                                                     valorTotal = @valorTotal, 
                                                     fechaEmision = @fechaEmision,
                                                     idCliente = @idCliente
                                                 WHERE numeroFactura = @numeroFacturaOriginal";

                        using (SqlCommand command = new SqlCommand(queryActualizar, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@numeroFacturaOriginal", numeroFacturaOriginal);
                            command.Parameters.AddWithValue("@numeroFacturaNuevo", numeroFacturaNuevo);
                            command.Parameters.AddWithValue("@numeroPedido", string.IsNullOrEmpty(numeroPedido) ? (object)DBNull.Value : numeroPedido);
                            command.Parameters.AddWithValue("@valorTotal", valorTotal);
                            command.Parameters.AddWithValue("@fechaEmision", fechaEmision);
                            command.Parameters.AddWithValue("@idCliente", idCliente);

                            int rowsAffected = command.ExecuteNonQuery();
                            if (rowsAffected == 0)
                            {
                                throw new Exception("No se pudo actualizar la factura.");
                            }
                        }

                        // 3. Procesar archivo si existe
                        if (fileUploadFactura.HasFile)
                        {
                            // Obtener ID de la factura usando el número original
                            int idFactura = ObtenerIdFactura(numeroFacturaOriginal, connection, transaction);

                            DocumentoInfoFactura docInfo = ProcesarArchivo(idFactura, numeroFacturaNuevo); // Usar el nuevo número para el archivo
                            if (docInfo != null)
                            {
                                GuardarDocumentoEnBD(idFactura, docInfo, connection, transaction);
                            }
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Debug.WriteLine("Error al actualizar factura con documento: " + ex.Message);
                        throw new Exception("Error al actualizar la factura: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene el ID de una factura por su número
        /// </summary>
        private int ObtenerIdFactura(string numeroFactura, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT idFactura FROM Factura WHERE numeroFactura = @numeroFactura";

            using (SqlCommand cmd = new SqlCommand(query, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                object result = cmd.ExecuteScalar();

                if (result != null && result != DBNull.Value)
                {
                    return Convert.ToInt32(result);
                }

                throw new Exception("No se encontró la factura especificada.");
            }
        }

        protected void Cancelar(object sender, EventArgs e)
        {
            // Volver al modo de solo lectura
            ddlCliente.Enabled = false;
            txtNumFactura.ReadOnly = true;  // NUEVO: Deshabilitar edición del número de factura
            txtNumPedido.ReadOnly = true;
            txtFechaEmision.ReadOnly = true;
            txtValorTotal.ReadOnly = true;

            // Deshabilitar upload de documentos
            fileUploadFactura.Enabled = false;
            txtDescripcionDoc.ReadOnly = true;

            // Deshabilitar validador del cliente
            rfvCliente.Enabled = false;

            btnHabilitarEdicion.Visible = true;
            btnGuardarCambios.Visible = false;
            lblMensaje.Text = "";

            // Limpiar campos de archivo
            LimpiarCamposArchivo();

            // Recargar datos originales para resetear cambios
            if (!string.IsNullOrEmpty(txtNumFactura.Text))
            {
                try
                {
                    string numeroOriginal = ViewState["NumeroFacturaOriginal"] as string;
                    if (!string.IsNullOrEmpty(numeroOriginal))
                    {
                        FacturaModel factura = ObtenerFactura(numeroOriginal);
                        if (factura != null)
                        {
                            MostrarDatosFactura(factura);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Error al cancelar: " + ex.Message);
                }
            }

            MostrarMensaje("Edición cancelada.", "info");
        }

        protected void NuevaBusqueda(object sender, EventArgs e)
        {
            // Limpiar los campos y ocultar paneles de resultados
            txtBuscarFactura.Text = "";
            pnlResultados.Visible = false;
            pnlNoResultados.Visible = false;
            lblMensaje.Text = "";

            // Resetear el dropdown del cliente
            ddlCliente.SelectedIndex = 0;
            ddlCliente.Enabled = false;
            rfvCliente.Enabled = false;

            // Limpiar campos de archivo
            LimpiarCamposArchivo();
            fileUploadFactura.Enabled = false;
            txtDescripcionDoc.ReadOnly = true;

            // Limpiar ViewState
            ViewState["NumeroFacturaOriginal"] = null;

            // Recargar lista de facturas por si hay cambios
            CargarListaFacturas();
        }

        // FUNCIONALIDAD DE DOCUMENTOS
        protected void gvDocumentos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            try
            {
                int idDocumento = Convert.ToInt32(e.CommandArgument);

                switch (e.CommandName)
                {
                    case "Descargar":
                        DescargarDocumento(idDocumento);
                        break;
                    case "Ver":
                        VerDocumento(idDocumento);
                        break;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al procesar documento: " + ex.Message, "danger");
                Debug.WriteLine("Error en gvDocumentos_RowCommand: " + ex.Message);
            }
        }

        private void DescargarDocumento(int idDocumento)
        {
            try
            {
                var docInfo = ObtenerInfoDocumento(idDocumento);
                if (docInfo != null)
                {
                    string rutaCompleta = Server.MapPath(docInfo["rutaArchivo"].ToString());

                    if (File.Exists(rutaCompleta))
                    {
                        Response.Clear();
                        Response.ContentType = ObtenerContentType(docInfo["tipoArchivo"].ToString());
                        Response.AddHeader("Content-Disposition", $"attachment; filename=\"{docInfo["nombreOriginal"]}\"");
                        Response.WriteFile(rutaCompleta);
                        Response.End();
                    }
                    else
                    {
                        MostrarMensaje("El archivo no existe en el servidor.", "warning");
                    }
                }
                else
                {
                    MostrarMensaje("Documento no encontrado.", "warning");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al descargar documento: " + ex.Message);
                MostrarMensaje("Error al descargar el documento.", "danger");
            }
        }

        private void VerDocumento(int idDocumento)
        {
            try
            {
                var docInfo = ObtenerInfoDocumento(idDocumento);
                if (docInfo != null)
                {
                    string rutaCompleta = Server.MapPath(docInfo["rutaArchivo"].ToString());

                    if (File.Exists(rutaCompleta))
                    {
                        string urlArchivo = ResolveUrl(docInfo["rutaArchivo"].ToString());
                        string script = $"window.open('{urlArchivo}', '_blank');";
                        ScriptManager.RegisterStartupScript(this, GetType(), "VerDocumento", script, true);
                    }
                    else
                    {
                        MostrarMensaje("El archivo no existe en el servidor.", "warning");
                    }
                }
                else
                {
                    MostrarMensaje("Documento no encontrado.", "warning");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al ver documento: " + ex.Message);
                MostrarMensaje("Error al abrir el documento.", "danger");
            }
        }

        private DataRow ObtenerInfoDocumento(int idDocumento)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = @"
                        SELECT nombreOriginal, rutaArchivo, tipoArchivo
                        FROM DocumentosFactura 
                        WHERE idDocumento = @idDocumento AND activo = 1";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@idDocumento", idDocumento);

                        DataTable dt = new DataTable();
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            adapter.Fill(dt);
                        }

                        if (dt.Rows.Count > 0)
                            return dt.Rows[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener info documento: " + ex.Message);
            }

            return null;
        }

        private string ObtenerContentType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                default: return "application/octet-stream";
            }
        }

        // FUNCIONES AUXILIARES PARA LA VISTA
        protected string ObtenerIconoArchivo(string tipoArchivo)
        {
            switch (tipoArchivo.ToLower())
            {
                case ".pdf": return "fa-file-pdf-o";
                case ".doc":
                case ".docx": return "fa-file-word-o";
                case ".jpg":
                case ".jpeg":
                case ".png": return "fa-file-image-o";
                default: return "fa-file-o";
            }
        }

        protected string FormatearTamano(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }

        /// <summary>
        /// Valida que el número de pedido tenga exactamente 10 dígitos.
        /// </summary>
        private bool ValidarNumeroPedido(string numeroPedido)
        {
            string pattern = @"^\d{10}$"; // Exactamente 10 dígitos
            return Regex.IsMatch(numeroPedido, pattern);
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo}";
            lblMensaje.Visible = true;
        }

        // MÉTODOS PARA MANEJO DE DOCUMENTOS

        /// <summary>
        /// Valida el archivo subido
        /// </summary>
        private string ValidarArchivo()
        {
            if (!fileUploadFactura.HasFile)
            {
                return ""; // No es obligatorio
            }

            var archivo = fileUploadFactura.PostedFile;

            // Validar tamaño
            if (archivo.ContentLength > MAX_FILE_SIZE)
            {
                return "El archivo es demasiado grande. El tamaño máximo permitido es 50MB.";
            }

            if (archivo.ContentLength == 0)
            {
                return "El archivo está vacío.";
            }

            // Validar extensión
            string extension = Path.GetExtension(archivo.FileName).ToLower();
            if (!Array.Exists(ALLOWED_EXTENSIONS, ext => ext == extension))
            {
                return "Tipo de archivo no permitido. Use: PDF, DOC, DOCX, JPG, PNG.";
            }

            return ""; // Todo OK
        }

        /// <summary>
        /// Procesa y guarda archivo físicamente
        /// </summary>
        private DocumentoInfoFactura ProcesarArchivo(int idFactura, string numeroFactura)
        {
            try
            {
                var archivo = fileUploadFactura.PostedFile;
                string nombreOriginal = Path.GetFileName(archivo.FileName);
                string extension = Path.GetExtension(nombreOriginal).ToLower();

                // Generar nombre único para el archivo
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string numeroLimpio = numeroFactura.Replace(" - ", "_").Replace(" ", "_").Replace("-", "_");
                string nombreArchivo = $"FACTURA_{numeroLimpio}_{timestamp}{extension}";

                // Crear ruta de destino
                string carpetaAno = DateTime.Now.Year.ToString();
                string carpetaMes = DateTime.Now.ToString("MM");
                string rutaCarpeta = Server.MapPath($"~/Uploads/Factura/{carpetaAno}/{carpetaMes}/");

                // Crear directorios si no existen
                if (!Directory.Exists(rutaCarpeta))
                {
                    Directory.CreateDirectory(rutaCarpeta);
                }

                string rutaCompleta = Path.Combine(rutaCarpeta, nombreArchivo);

                // Guardar archivo
                archivo.SaveAs(rutaCompleta);

                // Crear info del documento
                return new DocumentoInfoFactura
                {
                    NombreOriginal = nombreOriginal,
                    NombreArchivo = nombreArchivo,
                    RutaCompleta = $"~/Uploads/Factura/{carpetaAno}/{carpetaMes}/{nombreArchivo}",
                    TipoArchivo = extension,
                    TamanoBytes = archivo.ContentLength,
                    Descripcion = txtDescripcionDoc.Text.Trim()
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al procesar archivo: " + ex.Message);
                throw new Exception("Error al guardar el archivo: " + ex.Message);
            }
        }

        /// <summary>
        /// Guarda información del documento en BD
        /// </summary>
        private void GuardarDocumentoEnBD(int idFactura, DocumentoInfoFactura docInfo, SqlConnection connection, SqlTransaction transaction)
        {
            string queryInsertDoc = @"
                INSERT INTO DocumentosFactura 
                (idFactura, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida, descripcion)
                VALUES 
                (@idFactura, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida, @descripcion)";

            using (SqlCommand cmd = new SqlCommand(queryInsertDoc, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@idFactura", idFactura);
                cmd.Parameters.AddWithValue("@nombreOriginal", docInfo.NombreOriginal);
                cmd.Parameters.AddWithValue("@nombreArchivo", docInfo.NombreArchivo);
                cmd.Parameters.AddWithValue("@rutaArchivo", docInfo.RutaCompleta);
                cmd.Parameters.AddWithValue("@tipoArchivo", docInfo.TipoArchivo);
                cmd.Parameters.AddWithValue("@tamanoBytes", docInfo.TamanoBytes);
                cmd.Parameters.AddWithValue("@fechaSubida", DateTime.Now);
                cmd.Parameters.AddWithValue("@usuarioSubida", ObtenerUsuarioActual());
                cmd.Parameters.AddWithValue("@descripcion", string.IsNullOrEmpty(docInfo.Descripcion) ? DBNull.Value : (object)docInfo.Descripcion);

                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Crear directorios de upload si no existen
        /// </summary>
        private void CrearDirectoriosUpload()
        {
            try
            {
                string carpetaBase = Server.MapPath("~/Uploads/Factura/");
                if (!Directory.Exists(carpetaBase))
                {
                    Directory.CreateDirectory(carpetaBase);
                }

                // Crear carpetas del año y mes actual
                string carpetaAno = Path.Combine(carpetaBase, DateTime.Now.Year.ToString());
                if (!Directory.Exists(carpetaAno))
                {
                    Directory.CreateDirectory(carpetaAno);
                }

                string carpetaMes = Path.Combine(carpetaAno, DateTime.Now.ToString("MM"));
                if (!Directory.Exists(carpetaMes))
                {
                    Directory.CreateDirectory(carpetaMes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al crear directorios: " + ex.Message);
            }
        }

        /// <summary>
        /// Obtener usuario actual
        /// </summary>
        private string ObtenerUsuarioActual()
        {
            try
            {
                // Si usas Session para almacenar el usuario
                if (Session["Usuario"] != null)
                {
                    return Session["Usuario"].ToString();
                }

                // Si usas autenticación de Windows
                if (HttpContext.Current.User.Identity.IsAuthenticated)
                {
                    return HttpContext.Current.User.Identity.Name;
                }

                // Default si no hay usuario
                return "Sistema";
            }
            catch
            {
                return "Usuario";
            }
        }

        /// <summary>
        /// Limpiar campos de archivo
        /// </summary>
        private void LimpiarCamposArchivo()
        {
            txtDescripcionDoc.Text = string.Empty;

            // Script para limpiar preview de archivo
            ScriptManager.RegisterStartupScript(this, GetType(), "clearFilePreview",
                "if(typeof clearFileSelection === 'function') { clearFileSelection(); }", true);
        }

        // Clase para información del documento
        private class DocumentoInfoFactura
        {
            public string NombreOriginal { get; set; }
            public string NombreArchivo { get; set; }
            public string RutaCompleta { get; set; }
            public string TipoArchivo { get; set; }
            public long TamanoBytes { get; set; }
            public string Descripcion { get; set; }
        }
    }

    // Clase modelo para la factura con información del cliente
    public class FacturaModel
    {
        public int IdFactura { get; set; }
        public string NumeroFactura { get; set; }
        public string NumeroPedido { get; set; }
        public decimal ValorTotal { get; set; }
        public DateTime FechaEmision { get; set; }

        // Campos del cliente
        public int IdCliente { get; set; }
        public string NombreCliente { get; set; }
        public string RucCliente { get; set; }
    }
}