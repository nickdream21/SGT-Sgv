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

namespace WebSGV.Views
{
    public partial class AgregarCPIC : System.Web.UI.Page
    {
        // Configuración para archivos
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private readonly string[] ALLOWED_EXTENSIONS = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                txtFechaEmision.Text = DateTime.Now.ToString("yyyy-MM-dd");
                CrearDirectoriosUpload();
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

                // Validar peso neto
                decimal pesoNeto;
                if (!decimal.TryParse(txtPesoNeto.Text, out pesoNeto) || pesoNeto <= 0)
                {
                    MostrarMensaje("Debe ingresar un peso neto válido.", "error");
                    return;
                }

                // Validar peso bruto
                decimal pesoBruto;
                if (!decimal.TryParse(txtPesoBruto.Text, out pesoBruto) || pesoBruto <= 0)
                {
                    MostrarMensaje("Debe ingresar un peso bruto válido.", "error");
                    return;
                }

                // Validar que el peso bruto sea mayor al peso neto
                if (pesoBruto <= pesoNeto)
                {
                    MostrarMensaje("El peso bruto debe ser mayor al peso neto.", "error");
                    return;
                }

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

                // Procesar productos desde el campo oculto
                if (string.IsNullOrEmpty(hiddenProductos.Value))
                {
                    MostrarMensaje("Debe agregar al menos un producto.", "error");
                    return;
                }

                List<ProductoCPIC> productos = ObtenerProductosDesdeJSON(hiddenProductos.Value);
                if (productos.Count == 0)
                {
                    MostrarMensaje("Debe agregar al menos un producto válido.", "error");
                    return;
                }

                // Guardar el CPIC y sus productos
                GuardarCPICEnBaseDeDatos(numeroCPIC, idFactura.Value, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto, productos);

                // Mostrar mensaje de éxito
                MostrarMensaje("CPIC registrado correctamente.", "success");

                // Evitar que aparezcan mensajes de alerta
                ScriptManager.RegisterStartupScript(this, GetType(), "AvoidAlerts",
                    "window.onbeforeunload = null; window.skipRowValidation = true;", true);

                // Limpiar el formulario
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error: " + ex.Message, "error");
            }
        }

        // Clase para manejar los productos (sin peso individual)
        private class ProductoCPIC
        {
            public int IdProducto { get; set; }
            public int Cantidad { get; set; }
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

        // Guardar en base de datos (actualizado con peso neto, bruto y documentos)
        private void GuardarCPICEnBaseDeDatos(string numeroCPIC, int idFactura, decimal valorTotalFlete,
                                           DateTime fechaEmision, decimal pesoNeto, decimal pesoBruto, List<ProductoCPIC> productos)
        {
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Insertar el CPIC con los nuevos campos
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

                        // 2. Insertar los productos
                        string queryInsertProducto = @"
                            INSERT INTO CPIC_Productos (idCPIC, idProducto, cantidadBolsasProducto, pesoKg)
                            VALUES (@idCPIC, @idProducto, @cantidadBolsas, @pesoKg)";

                        foreach (var producto in productos)
                        {
                            using (SqlCommand cmd = new SqlCommand(queryInsertProducto, connection, transaction))
                            {
                                cmd.Parameters.AddWithValue("@idCPIC", idCPIC);
                                cmd.Parameters.AddWithValue("@idProducto", producto.IdProducto);
                                cmd.Parameters.AddWithValue("@cantidadBolsas", producto.Cantidad);
                                cmd.Parameters.AddWithValue("@pesoKg", 0); // Peso 0 ya que se maneja a nivel CPIC

                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 3. Procesar archivo si existe
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

        // Procesar JSON de productos (actualizado sin peso)
        private List<ProductoCPIC> ObtenerProductosDesdeJSON(string json)
        {
            List<ProductoCPIC> productos = new List<ProductoCPIC>();

            if (string.IsNullOrEmpty(json))
                return productos;

            try
            {
                productos = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ProductoCPIC>>(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al deserializar productos: " + ex.Message);
                throw new Exception("Error al procesar los productos: " + ex.Message);
            }

            return productos ?? new List<ProductoCPIC>();
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

        // Limpiar formulario (actualizado con nuevos campos)
        private void LimpiarFormulario()
        {
            txtNumCPIC.Text = string.Empty;
            txtNumFactura.Text = string.Empty;
            txtFechaEmision.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtTotalFlete.Text = string.Empty;
            txtPesoNeto.Text = string.Empty;
            txtPesoBruto.Text = string.Empty;
            txtDescripcionDoc.Text = string.Empty; // Nuevo campo
            lblErrorFactura.Text = string.Empty;
            hiddenProductos.Value = string.Empty;

            // Limpiar archivo
            // Nota: fileUploadCPIC se limpia automáticamente en postback

            // Limpiar tabla sin alertas
            ScriptManager.RegisterStartupScript(this, GetType(), "resetProductos",
                "window.skipRowValidation = true; document.querySelector('#tablaProductos tbody').innerHTML = ''; agregarFila(); clearFileSelection();", true);
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

        // Obtener JSON de productos para el dropdown
        protected string ObtenerProductosJSON()
        {
            try
            {
                DataTable dt = new DataTable();
                using (SqlConnection connection = new SqlConnection(System.Configuration.ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString))
                {
                    connection.Open();
                    string query = "SELECT idProducto, nombre FROM Producto";
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(dt);
                        }
                    }
                }
                return Newtonsoft.Json.JsonConvert.SerializeObject(dt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener productos: " + ex.Message);
                return "[]"; // Retornar array vacío en caso de error
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