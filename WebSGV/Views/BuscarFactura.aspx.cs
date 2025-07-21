using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace WebSGV.Views
{
    public partial class BusquedaFactura : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // NUEVO: Cargar lista de clientes
                CargarClientes();
            }
        }

        // NUEVO MÉTODO: Cargar lista de clientes
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

        // MODIFICADO: Incluir información del cliente
        private FacturaModel ObtenerFactura(string numeroFactura)
        {
            FacturaModel factura = null;

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // MODIFICADO: Consulta SQL para obtener la factura CON cliente
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
                                    // NUEVO: Campos del cliente
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

        // MODIFICADO: Incluir mostrar cliente
        private void MostrarDatosFactura(FacturaModel factura)
        {
            // Llenar los campos con la información de la factura
            txtNumFactura.Text = factura.NumeroFactura;
            txtNumPedido.Text = factura.NumeroPedido;
            txtFechaEmision.Text = factura.FechaEmision.ToString("yyyy-MM-dd");
            txtValorTotal.Text = factura.ValorTotal.ToString("N2");

            // NUEVO: Seleccionar el cliente correspondiente
            ddlCliente.SelectedValue = factura.IdCliente.ToString();
        }

        // MODIFICADO: Incluir habilitación del cliente
        protected void HabilitarEdicion(object sender, EventArgs e)
        {
            // Habilitar la edición de los campos
            ddlCliente.Enabled = true; // NUEVO: Habilitar edición del cliente
            txtNumPedido.ReadOnly = false;
            txtFechaEmision.ReadOnly = false;
            txtValorTotal.ReadOnly = false;

            // NUEVO: Habilitar validador del cliente
            rfvCliente.Enabled = true;

            // Mostrar el botón de guardar cambios
            btnHabilitarEdicion.Visible = false;
            btnGuardarCambios.Visible = true;

            MostrarMensaje("Modo de edición activado. Realice los cambios necesarios y presione 'Guardar Cambios'.", "info");
        }

        // MODIFICADO: Incluir validación y actualización del cliente
        protected void GuardarCambios(object sender, EventArgs e)
        {
            try
            {
                // NUEVO: Validar que se haya seleccionado un cliente
                if (string.IsNullOrEmpty(ddlCliente.SelectedValue))
                {
                    MostrarMensaje("Debe seleccionar un cliente.", "danger");
                    return;
                }

                // Recolectar datos actualizados
                string numeroFactura = txtNumFactura.Text;
                string numeroPedido = txtNumPedido.Text.Trim();
                int idCliente = Convert.ToInt32(ddlCliente.SelectedValue); // NUEVO

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

                // MODIFICADO: Actualizar la factura incluyendo el cliente
                bool actualizado = ActualizarFactura(numeroFactura, numeroPedido, valorTotal, fechaEmision, idCliente);

                if (actualizado)
                {
                    // Volver al modo de sólo lectura
                    ddlCliente.Enabled = false; // NUEVO
                    txtNumPedido.ReadOnly = true;
                    txtFechaEmision.ReadOnly = true;
                    txtValorTotal.ReadOnly = true;

                    // NUEVO: Deshabilitar validador del cliente
                    rfvCliente.Enabled = false;

                    btnHabilitarEdicion.Visible = true;
                    btnGuardarCambios.Visible = false;

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

        // MODIFICADO: Incluir actualización del cliente
        private bool ActualizarFactura(string numeroFactura, string numeroPedido, decimal valorTotal, DateTime fechaEmision, int idCliente)
        {
            bool actualizado = false;

            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Verificar si el número de pedido ya está asociado a otra factura
                    if (!string.IsNullOrEmpty(numeroPedido))
                    {
                        string queryVerificarPedido = @"SELECT COUNT(*) 
                                                      FROM Factura 
                                                      WHERE numeroPedido = @numeroPedido 
                                                      AND numeroFactura <> @numeroFactura";

                        using (SqlCommand command = new SqlCommand(queryVerificarPedido, connection))
                        {
                            command.Parameters.AddWithValue("@numeroPedido", numeroPedido);
                            command.Parameters.AddWithValue("@numeroFactura", numeroFactura);

                            int count = (int)command.ExecuteScalar();
                            if (count > 0)
                            {
                                throw new Exception("El número de pedido ya está asociado a otra factura.");
                            }
                        }
                    }

                    // MODIFICADO: Actualizar factura incluyendo idCliente
                    string queryActualizar = @"UPDATE Factura 
                                             SET numeroPedido = @numeroPedido, 
                                                 valorTotal = @valorTotal, 
                                                 fechaEmision = @fechaEmision,
                                                 idCliente = @idCliente
                                             WHERE numeroFactura = @numeroFactura";

                    using (SqlCommand command = new SqlCommand(queryActualizar, connection))
                    {
                        command.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                        command.Parameters.AddWithValue("@numeroPedido", string.IsNullOrEmpty(numeroPedido) ? (object)DBNull.Value : numeroPedido);
                        command.Parameters.AddWithValue("@valorTotal", valorTotal);
                        command.Parameters.AddWithValue("@fechaEmision", fechaEmision);
                        command.Parameters.AddWithValue("@idCliente", idCliente); // NUEVO

                        int rowsAffected = command.ExecuteNonQuery();
                        actualizado = rowsAffected > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al actualizar factura: " + ex.Message);
                throw new Exception("Error al actualizar la factura: " + ex.Message);
            }

            return actualizado;
        }

        // MODIFICADO: Incluir reset del cliente
        protected void Cancelar(object sender, EventArgs e)
        {
            // Volver al modo de solo lectura
            ddlCliente.Enabled = false; // NUEVO
            txtNumPedido.ReadOnly = true;
            txtFechaEmision.ReadOnly = true;
            txtValorTotal.ReadOnly = true;

            // NUEVO: Deshabilitar validador del cliente
            rfvCliente.Enabled = false;

            btnHabilitarEdicion.Visible = true;
            btnGuardarCambios.Visible = false;
            lblMensaje.Text = "";

            // NUEVO: Recargar datos originales para resetear cambios
            if (!string.IsNullOrEmpty(txtNumFactura.Text))
            {
                try
                {
                    FacturaModel factura = ObtenerFactura(txtNumFactura.Text);
                    if (factura != null)
                    {
                        MostrarDatosFactura(factura);
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

            // NUEVO: Resetear el dropdown del cliente
            ddlCliente.SelectedIndex = 0;
            ddlCliente.Enabled = false;
            rfvCliente.Enabled = false;
        }

        // FUNCIONALIDAD DE DOCUMENTOS (sin cambios)
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

        // FUNCIONES AUXILIARES PARA LA VISTA (sin cambios)
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
    }

    // MODIFICADA: Clase modelo para la factura con información del cliente
    public class FacturaModel
    {
        public int IdFactura { get; set; }
        public string NumeroFactura { get; set; }
        public string NumeroPedido { get; set; }
        public decimal ValorTotal { get; set; }
        public DateTime FechaEmision { get; set; }

        // NUEVOS: Campos del cliente
        public int IdCliente { get; set; }
        public string NombreCliente { get; set; }
        public string RucCliente { get; set; }
    }
}