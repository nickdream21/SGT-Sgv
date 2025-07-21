using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;

namespace WebSGV.Views
{
    public partial class AgregarFactura : System.Web.UI.Page
    {
        // Configuración para archivos
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private readonly string[] ALLOWED_EXTENSIONS = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Establecer la fecha actual como predeterminada
                txtFechaEmision.Text = DateTime.Now.ToString("yyyy-MM-dd");
                CrearDirectoriosUpload();

                // NUEVO: Cargar lista de clientes
                CargarClientes();
            }
        }

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
                MostrarError("Error al cargar la lista de clientes.");
            }
        }

        protected void GuardarFactura(object sender, EventArgs e)
        {
            try
            {
                // NUEVO: Validar que se haya seleccionado un cliente
                if (string.IsNullOrEmpty(ddlCliente.SelectedValue))
                {
                    MostrarError("Debe seleccionar un cliente.");
                    return;
                }

                // Validar que los campos no estén vacíos
                if (string.IsNullOrEmpty(txtNumFactura.Text.Trim()))
                {
                    MostrarError("El número de factura es obligatorio.");
                    return;
                }

                if (string.IsNullOrEmpty(txtImporteTotal.Text.Trim()))
                {
                    MostrarError("El importe total es obligatorio.");
                    return;
                }

                if (string.IsNullOrEmpty(txtFechaEmision.Text.Trim()))
                {
                    MostrarError("La fecha de emisión es obligatoria.");
                    return;
                }

                // NUEVO: Obtener el ID del cliente seleccionado
                int idCliente = Convert.ToInt32(ddlCliente.SelectedValue);

                // Validar y formatear el número de factura
                string numeroFactura;
                try
                {
                    numeroFactura = ValidarYFormatearNumeroFactura(txtNumFactura.Text);
                }
                catch (FormatException ex)
                {
                    MostrarError(ex.Message);
                    return;
                }

                // Validar el número de pedido (si no está vacío)
                string numeroPedido = txtNumPedido.Text.Trim();
                if (!string.IsNullOrEmpty(numeroPedido) && !ValidarNumeroPedido(numeroPedido))
                {
                    MostrarError("El número de pedido debe tener exactamente 10 dígitos numéricos.");
                    return;
                }

                // Validar el importe total
                if (!decimal.TryParse(txtImporteTotal.Text, out decimal valorTotal) || valorTotal <= 0)
                {
                    MostrarError("El importe total debe ser un número válido y mayor a 0.");
                    return;
                }

                // Validar la fecha de emisión
                if (!DateTime.TryParse(txtFechaEmision.Text, out DateTime fechaEmision))
                {
                    MostrarError("La fecha de emisión no es válida.");
                    return;
                }

                // Validar que la fecha no sea futura
                if (fechaEmision > DateTime.Now)
                {
                    MostrarError("La fecha de emisión no puede ser mayor que la fecha actual.");
                    return;
                }

                // Validar archivo si se seleccionó uno
                string resultadoValidacion = ValidarArchivo();
                if (!string.IsNullOrEmpty(resultadoValidacion))
                {
                    MostrarError(resultadoValidacion);
                    return;
                }

                // Validar que la factura no exista previamente
                if (FacturaExiste(numeroFactura))
                {
                    MostrarError("Ya existe una factura con ese número en el sistema.");
                    return;
                }

                // MODIFICADO: Insertar factura con cliente
                if (InsertarFactura(numeroFactura, numeroPedido, valorTotal, fechaEmision, idCliente))
                {
                    // Limpiar campos después de guardar correctamente
                    LimpiarFormulario();

                    // Mostrar mensaje de éxito
                    MostrarExito("Factura registrada correctamente.");
                }
            }
            catch (Exception ex)
            {
                MostrarError("Error al registrar la factura: " + ex.Message);
                Debug.WriteLine("Error en GuardarFactura: " + ex.Message);
            }
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

        /// <summary>
        /// Verifica si ya existe una factura con el número especificado
        /// </summary>
        private bool FacturaExiste(string numeroFactura)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "SELECT COUNT(*) FROM Factura WHERE numeroFactura = @numeroFactura";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@numeroFactura", numeroFactura);

                        connection.Open();
                        int count = (int)cmd.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al verificar factura: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Inserta una nueva factura en la base de datos (con documentos)
        /// </summary>
        private bool InsertarFactura(string numeroFactura, string numeroPedido, decimal valorTotal, DateTime fechaEmision, int idCliente)
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
                        int idFactura;

                        // 1. MODIFICADO: Insertar la factura CON idCliente
                        using (SqlCommand command = new SqlCommand(@"
                    INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente) 
                    VALUES (@numeroFactura, @valorTotal, @fechaEmision, @numeroPedido, @idCliente);
                    SELECT SCOPE_IDENTITY();", connection, transaction))
                        {
                            command.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                            command.Parameters.AddWithValue("@valorTotal", valorTotal);
                            command.Parameters.AddWithValue("@fechaEmision", fechaEmision);
                            command.Parameters.AddWithValue("@idCliente", idCliente); // NUEVO PARÁMETRO

                            // Si numeroPedido está vacío, usar DBNull
                            if (string.IsNullOrEmpty(numeroPedido))
                                command.Parameters.AddWithValue("@numeroPedido", DBNull.Value);
                            else
                                command.Parameters.AddWithValue("@numeroPedido", numeroPedido);

                            idFactura = Convert.ToInt32(command.ExecuteScalar());
                        }

                        // 2. Procesar archivo si existe
                        if (fileUploadFactura.HasFile)
                        {
                            DocumentoInfoFactura docInfo = ProcesarArchivo(idFactura, numeroFactura);
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
                        Debug.WriteLine("Error al insertar factura: " + ex.Message);
                        throw new Exception("Error al guardar la factura: " + ex.Message);
                    }
                }
            }
        }

        // Validar archivo subido
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

        // Procesar y guardar archivo físicamente
        private DocumentoInfoFactura ProcesarArchivo(int idFactura, string numeroFactura)
        {
            try
            {
                var archivo = fileUploadFactura.PostedFile;
                string nombreOriginal = Path.GetFileName(archivo.FileName);
                string extension = Path.GetExtension(nombreOriginal).ToLower();

                // Generar nombre único para el archivo
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string numeroLimpio = numeroFactura.Replace(" - ", "_").Replace(" ", "_");
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

        // Guardar información del documento en BD
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

        // Crear directorios de upload si no existen
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

        // Obtener usuario actual (ajustar según tu sistema de autenticación)
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
        /// Muestra un mensaje de error
        /// </summary>
        private void MostrarError(string mensaje)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = "text-danger";
        }

        /// <summary>
        /// Muestra un mensaje de éxito
        /// </summary>
        private void MostrarExito(string mensaje)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = "text-success";
        }

        /// <summary>
        /// Limpia los campos del formulario después de guardar.
        /// </summary>
        private void LimpiarFormulario()
        {
            ddlCliente.SelectedIndex = 0; // NUEVO: Resetear al primer item (vacío)
            txtNumFactura.Text = string.Empty;
            txtNumPedido.Text = string.Empty;
            txtImporteTotal.Text = string.Empty;
            txtDescripcionDoc.Text = string.Empty;
            // Restaurar la fecha actual
            txtFechaEmision.Text = DateTime.Now.ToString("yyyy-MM-dd");

            // Limpiar archivo
            // Nota: fileUploadFactura se limpia automáticamente en postback

            // Script para limpiar preview de archivo
            ScriptManager.RegisterStartupScript(this, GetType(), "clearFilePreview",
                "clearFileSelection();", true);
        }

        /// <summary>
        /// Valida que el número de pedido tenga exactamente 10 dígitos.
        /// </summary>
        private bool ValidarNumeroPedido(string numeroPedido)
        {
            string pattern = @"^\d{10}$"; // Exactamente 10 dígitos
            return Regex.IsMatch(numeroPedido, pattern);
        }

        /// <summary>
        /// Valida y ajusta el formato del número de factura.
        /// </summary>
        private string ValidarYFormatearNumeroFactura(string numeroFactura)
        {
            if (string.IsNullOrWhiteSpace(numeroFactura))
                throw new FormatException("El número de factura no puede estar vacío.");

            // Expresión regular para el formato correcto
            string pattern = @"^F\d{3} - \d{8}$";

            if (Regex.IsMatch(numeroFactura, pattern))
            {
                // Si ya cumple el formato, retornarlo sin cambios
                return numeroFactura;
            }

            // Intentar corregir el formato
            string soloNumerosYLetras = Regex.Replace(numeroFactura, @"[^A-Za-z0-9]", "");

            if (soloNumerosYLetras.Length == 12 && soloNumerosYLetras.StartsWith("F", StringComparison.OrdinalIgnoreCase))
            {
                string codigo = soloNumerosYLetras.Substring(0, 4).ToUpper(); // Ejemplo: "F222"
                string secuencia = soloNumerosYLetras.Substring(4); // Ejemplo: "00004267"                     
                return $"{codigo} - {secuencia}";
            }

            // Si no es posible corregir, lanzar excepción
            throw new FormatException("El número de factura debe tener el formato 'F222 - 00004267'.");
        }
    }
}