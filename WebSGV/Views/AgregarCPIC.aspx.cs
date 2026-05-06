using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class AgregarCPIC : System.Web.UI.Page
    {
        // Configuración para archivos
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private readonly string[] ALLOWED_EXTENSIONS = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
            {
                txtFechaEmision.Text = DateTime.Now.ToString("yyyy-MM-dd");
                CrearDirectoriosUpload();

                // NUEVO: Cargar datos desde query string si vienen de BuscarFactura
                CargarDatosDesdeQueryString();
            }
        }

        private void CargarDatosDesdeQueryString()
        {
            try
            {
                // Leer parámetros de la URL
                string numeroFactura = Request.QueryString["numeroFactura"];
                string valorTotalStr = Request.QueryString["valorTotal"];

                if (!string.IsNullOrEmpty(numeroFactura) && !string.IsNullOrEmpty(valorTotalStr))
                {
                    // Decodificar el número de factura (por si tiene caracteres especiales)
                    numeroFactura = HttpUtility.UrlDecode(numeroFactura);

                    // Asignar número de factura
                    txtNumFactura.Text = numeroFactura;

                    // Validar y asignar valor total
                    if (decimal.TryParse(valorTotalStr, out decimal valorTotal))
                    {
                        txtTotalFlete.Text = valorTotal.ToString("0.00");

                        // Validar que la factura realmente existe y no está ya asociada a un CPIC
                        ValidarFacturaParaCPIC(numeroFactura);

                        // Mostrar mensaje informativo
                        MostrarMensaje($"Datos cargados automáticamente para la factura: {numeroFactura}", "info");
                    }
                    else
                    {
                        MostrarMensaje("Error al cargar el valor total desde la factura seleccionada.", "warning");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cargar datos desde query string: " + ex.Message);
                MostrarMensaje("Error al cargar datos automáticamente: " + ex.Message, "warning");
            }
        }
        private void ValidarFacturaParaCPIC(string numeroFactura)
        {
            try
            {
                // Verificar si la factura ya tiene CPIC asociado
                if (ExisteFactura(numeroFactura))
                {
                    lblErrorFactura.Text = "⚠️ ADVERTENCIA: Esta factura ya tiene un CPIC asociado.";
                    lblErrorFactura.CssClass = "text-warning";
                }
                else
                {
                    // Verificar que la factura existe en el sistema
                    int? idFactura = ObtenerIdFactura(numeroFactura);
                    if (idFactura.HasValue)
                    {
                        lblErrorFactura.Text = "✅ Factura válida para crear CPIC.";
                        lblErrorFactura.CssClass = "text-success";
                    }
                    else
                    {
                        lblErrorFactura.Text = "❌ El número de factura no existe en el sistema.";
                        lblErrorFactura.CssClass = "text-danger";
                        txtTotalFlete.Text = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al validar factura: " + ex.Message);
                lblErrorFactura.Text = "Error al validar la factura.";
                lblErrorFactura.CssClass = "text-danger";
            }
        }
        protected void GuardarCPIC(object sender, EventArgs e)
        {
            try
            {
                // Validar datos del formulario
                string numeroFactura = txtNumFactura.Text.Trim();
                string numeroCPIC = txtNumCPIC.Text.Trim();

                // Validar CPIC y Factura
                if (string.IsNullOrEmpty(numeroCPIC))
                {
                    MostrarMensaje("Debe ingresar un número de CPIC.", "error");
                    return;
                }

                if (numeroCPIC.Length != 7)
                {
                    MostrarMensaje("El número de CPIC debe tener exactamente 7 caracteres.", "error");
                    return;
                }

                if (string.IsNullOrEmpty(numeroFactura))
                {
                    MostrarMensaje("Debe ingresar un número de factura.", "error");
                    return;
                }

                // Verificar si el CPIC ya existe
                if (ExisteCPIC(numeroCPIC))
                {
                    MostrarMensaje("El número de CPIC ya existe. Por favor, ingrese un número único.", "error");
                    return;
                }

                // Verificar si la factura ya está asociada a otro CPIC
                if (ExisteFactura(numeroFactura))
                {
                    MostrarMensaje("El número de factura ya está asociado a otro CPIC. Por favor, ingrese una factura única.", "error");
                    return;
                }

                // Validar fecha y valor de flete
                DateTime fechaEmision;
                if (!DateTime.TryParse(txtFechaEmision.Text, out fechaEmision))
                {
                    MostrarMensaje("La fecha de emisión no es válida.", "error");
                    return;
                }

                decimal valorTotalFlete;
                if (!decimal.TryParse(txtTotalFlete.Text, out valorTotalFlete) || valorTotalFlete <= 0)
                {
                    MostrarMensaje("El valor total del flete no es válido.", "error");
                    return;
                }

                // Valores por defecto para peso (ya que los campos fueron removidos)
                decimal pesoNeto = 0;   // Valor por defecto
                decimal pesoBruto = 0;  // Valor por defecto

                // Validar archivo si se seleccionó uno
                string resultadoValidacion = ValidarArchivo();
                if (!string.IsNullOrEmpty(resultadoValidacion))
                {
                    MostrarMensaje(resultadoValidacion, "error");
                    return;
                }

                // Obtener el idFactura
                int? idFactura = ObtenerIdFactura(numeroFactura);
                if (!idFactura.HasValue)
                {
                    MostrarMensaje("El número de factura no existe en el sistema.", "error");
                    return;
                }

                // Guardar el CPIC (SIN productos)
                GuardarCPICEnBaseDeDatos(numeroCPIC, idFactura.Value, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto);

                AuditoriaHelper.Registrar("INSERT", "CPIC", numeroCPIC,
                    $"CPIC registrado - Numero: {numeroCPIC}, Factura: {numeroFactura}, Flete: {valorTotalFlete}");

                // Mostrar mensaje de éxito
                MostrarMensaje("CPIC registrado correctamente.", "success");

                // Limpiar el formulario
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error: " + ex.Message, "error");
            }
        }

        // Clase para información del documento
        private class DocumentoInfo
        {
            public string NombreOriginal { get; set; }
            public string NombreArchivo { get; set; }
            public string RutaCompleta { get; set; }
            public string TipoArchivo { get; set; }
            public long TamanoBytes { get; set; }
            public string Descripcion { get; set; }
        }

        // Guardar en base de datos (SIN productos)
        private void GuardarCPICEnBaseDeDatos(string numeroCPIC, int idFactura, decimal valorTotalFlete,
                                           DateTime fechaEmision, decimal pesoNeto, decimal pesoBruto)
        {
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Insertar el CPIC
                        string queryInsertCPIC = @"
                            INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto)
                            VALUES (@numeroCPIC, @idFactura, @valorTotalFlete, @fechaEmision, @pesoNeto, @pesoBruto);
                            SELECT SCOPE_IDENTITY();";

                        int idCPIC;
                        using (SqlCommand cmd = new SqlCommand(queryInsertCPIC, connection, transaction))
                        {
                            cmd.Parameters.AddWithValue("@numeroCPIC", numeroCPIC);
                            cmd.Parameters.AddWithValue("@idFactura", idFactura);
                            cmd.Parameters.AddWithValue("@valorTotalFlete", valorTotalFlete);
                            cmd.Parameters.AddWithValue("@fechaEmision", fechaEmision);
                            cmd.Parameters.AddWithValue("@pesoNeto", pesoNeto);
                            cmd.Parameters.AddWithValue("@pesoBruto", pesoBruto);

                            idCPIC = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // 2. Procesar archivo si existe
                        if (fileUploadCPIC.HasFile)
                        {
                            DocumentoInfo docInfo = ProcesarArchivo(idCPIC, numeroCPIC);
                            if (docInfo != null)
                            {
                                GuardarDocumentoEnBD(idCPIC, docInfo, connection, transaction);
                            }
                        }

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        throw new Exception("Error al guardar el CPIC: " + ex.Message);
                    }
                }
            }
        }

        // Validar archivo subido
        private string ValidarArchivo()
        {
            if (!fileUploadCPIC.HasFile)
            {
                return ""; // No es obligatorio
            }

            var archivo = fileUploadCPIC.PostedFile;

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
        private DocumentoInfo ProcesarArchivo(int idCPIC, string numeroCPIC)
        {
            try
            {
                var archivo = fileUploadCPIC.PostedFile;
                string nombreOriginal = Path.GetFileName(archivo.FileName);
                string extension = Path.GetExtension(nombreOriginal).ToLower();

                // Generar nombre único para el archivo
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string nombreArchivo = $"CPIC_{numeroCPIC}_{timestamp}{extension}";

                // Crear ruta de destino
                string carpetaAno = DateTime.Now.Year.ToString();
                string carpetaMes = DateTime.Now.ToString("MM");
                string rutaCarpeta = Server.MapPath($"~/Uploads/CPIC/{carpetaAno}/{carpetaMes}/");

                // Crear directorios si no existen
                if (!Directory.Exists(rutaCarpeta))
                {
                    Directory.CreateDirectory(rutaCarpeta);
                }

                string rutaCompleta = Path.Combine(rutaCarpeta, nombreArchivo);

                // Guardar archivo
                archivo.SaveAs(rutaCompleta);

                // Crear info del documento
                return new DocumentoInfo
                {
                    NombreOriginal = nombreOriginal,
                    NombreArchivo = nombreArchivo,
                    RutaCompleta = $"~/Uploads/CPIC/{carpetaAno}/{carpetaMes}/{nombreArchivo}",
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
        private void GuardarDocumentoEnBD(int idCPIC, DocumentoInfo docInfo, SqlConnection connection, SqlTransaction transaction)
        {
            string queryInsertDoc = @"
                INSERT INTO DocumentosCPIC 
                (idCPIC, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida, descripcion)
                VALUES 
                (@idCPIC, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida, @descripcion)";

            using (SqlCommand cmd = new SqlCommand(queryInsertDoc, connection, transaction))
            {
                cmd.Parameters.AddWithValue("@idCPIC", idCPIC);
                cmd.Parameters.AddWithValue("@nombreOriginal", docInfo.NombreOriginal);
                cmd.Parameters.AddWithValue("@nombreArchivo", docInfo.NombreArchivo);
                cmd.Parameters.AddWithValue("@rutaArchivo", docInfo.RutaCompleta);
                cmd.Parameters.AddWithValue("@tipoArchivo", docInfo.TipoArchivo);
                cmd.Parameters.AddWithValue("@tamanoBytes", docInfo.TamanoBytes);
                cmd.Parameters.AddWithValue("@fechaSubida", FechaHelper.Ahora());
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
                string carpetaBase = Server.MapPath("~/Uploads/CPIC/");
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

        // Obtener usuario actual (puedes ajustar según tu sistema de autenticación)
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

        // Obtener ID de factura
        private int? ObtenerIdFactura(string numeroFactura)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT idFactura FROM Factura WHERE numeroFactura = @numeroFactura";

                    using (SqlCommand cmd = new SqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                        object result = cmd.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }

                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener ID de factura: " + ex.Message);
                return null;
            }
        }

        // Limpiar formulario (SIN campos de peso)
        private void LimpiarFormulario()
        {
            txtNumCPIC.Text = string.Empty;
            txtNumFactura.Text = string.Empty;
            txtFechaEmision.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtTotalFlete.Text = string.Empty;
            txtDescripcionDoc.Text = string.Empty;
            lblErrorFactura.Text = string.Empty;
        }

        // Verificar si el CPIC existe
        private bool ExisteCPIC(string numeroCPIC)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string query = "SELECT COUNT(*) FROM CPIC WHERE numeroCPIC = @numeroCPIC";

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@numeroCPIC", numeroCPIC);
                        int count = (int)command.ExecuteScalar();
                        return count > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al verificar CPIC: " + ex.Message);
                return false;
            }
        }

        // Verificar si la factura está asociada
        private bool ExisteFactura(string numeroFactura)
        {
            try
            {
                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string queryBuscarFactura = "SELECT idFactura FROM Factura WHERE numeroFactura = @numeroFactura";

                    using (SqlCommand cmdBuscarFactura = new SqlCommand(queryBuscarFactura, connection))
                    {
                        cmdBuscarFactura.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                        object resultadoFactura = cmdBuscarFactura.ExecuteScalar();

                        if (resultadoFactura != null && resultadoFactura != DBNull.Value)
                        {
                            int idFactura = Convert.ToInt32(resultadoFactura);
                            string queryBuscarCPIC = "SELECT COUNT(*) FROM CPIC WHERE idFactura = @idFactura";

                            using (SqlCommand cmdBuscarCPIC = new SqlCommand(queryBuscarCPIC, connection))
                            {
                                cmdBuscarCPIC.Parameters.AddWithValue("@idFactura", idFactura);
                                int count = (int)cmdBuscarCPIC.ExecuteScalar();
                                return count > 0;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al verificar factura: " + ex.Message);
                return false;
            }
        }

        // Obtener valor de factura al cambiar el texto
        protected void TxtNumFactura_TextChanged(object sender, EventArgs e)
        {
            try
            {
                string numeroFactura = txtNumFactura.Text.Trim();
                lblErrorFactura.Text = string.Empty;

                if (string.IsNullOrWhiteSpace(numeroFactura))
                {
                    lblErrorFactura.Text = "Debe ingresar un número de factura.";
                    txtTotalFlete.Text = string.Empty;
                    return;
                }

                string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    try
                    {
                        connection.Open();
                        string query = "SELECT valorTotal FROM Factura WHERE numeroFactura = @numeroFactura";

                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@numeroFactura", numeroFactura);
                            object result = command.ExecuteScalar();

                            if (result != null)
                            {
                                decimal valorTotal = Convert.ToDecimal(result);
                                txtTotalFlete.Text = valorTotal.ToString("0.00");

                                // Verificar si la factura ya está asociada
                                if (ExisteFactura(numeroFactura))
                                {
                                    lblErrorFactura.Text = "Esta factura ya está asociada a un CPIC.";
                                }
                            }
                            else
                            {
                                lblErrorFactura.Text = "El número de factura no existe.";
                                txtTotalFlete.Text = string.Empty;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Si ocurre un error en la consulta, intentamos obtener directamente el valor
                        try
                        {
                            string queryDirecto = @"
                                SELECT valorTotal 
                                FROM Factura 
                                WHERE numeroFactura = '" + numeroFactura.Replace("'", "''") + "'";

                            using (SqlCommand cmd = new SqlCommand(queryDirecto, connection))
                            {
                                if (connection.State != ConnectionState.Open)
                                    connection.Open();

                                object result = cmd.ExecuteScalar();
                                if (result != null)
                                {
                                    decimal valorTotal = Convert.ToDecimal(result);
                                    txtTotalFlete.Text = valorTotal.ToString("0.00");
                                }
                                else
                                {
                                    lblErrorFactura.Text = "No se encontró la factura.";
                                    txtTotalFlete.Text = string.Empty;
                                }
                            }
                        }
                        catch
                        {
                            // Si aún falla, solo mostramos un mensaje genérico
                            lblErrorFactura.Text = "Error al obtener el valor. Continúe con el valor manualmente.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lblErrorFactura.Text = "Error: " + ex.Message;
                txtTotalFlete.Text = string.Empty;
            }
        }

        // Mostrar mensaje
        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = tipo == "success" ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Visible = true;
        }
    }
}