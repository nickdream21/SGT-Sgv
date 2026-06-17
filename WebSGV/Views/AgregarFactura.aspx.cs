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
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using WebSGV.Services.Facturas;

namespace WebSGV.Views
{
    public partial class AgregarFactura : PaginaBase
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
                DataTable dt = FacturaConsultasService.ObtenerClientes();
                ddlCliente.DataSource = dt;
                ddlCliente.DataTextField = "nombreCompleto";
                ddlCliente.DataValueField = "idCliente";
                ddlCliente.DataBind();
                ddlCliente.Items.Insert(0, new ListItem("-- Seleccione un Cliente --", ""));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al cargar clientes en AgregarFactura");
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
                LogSGV.Error(ex, "Error al registrar la factura {Numero}", txtNumFactura.Text);
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
                return FacturaConsultasService.ContarPorNumero(numeroFactura) > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al verificar la existencia de la factura en AgregarFactura");
                return false;
            }
        }

        /// <summary>
        /// Inserta una nueva factura en la base de datos (con documentos)
        /// </summary>
        private bool InsertarFactura(string numeroFactura, string numeroPedido, decimal valorTotal, DateTime fechaEmision, int idCliente)
        {
            string connectionString = DbHelper.ConnectionString;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Usar transacción para garantizar la integridad de los datos
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Insertar la factura CON idCliente (SQL movido a FacturaEscrituraService)
                        int idFactura = FacturaEscrituraService.InsertarFactura(
                            connection, transaction, numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente);

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
                        LogSGV.Error(ex, "Error al insertar la factura {Numero} en BD", numeroFactura);
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
                LogSGV.Error(ex, "Error al procesar el archivo de la factura en AgregarFactura");
                throw new Exception("Error al guardar el archivo: " + ex.Message);
            }
        }

        // Guardar información del documento en BD (SQL movido a FacturaEscrituraService;
        // la fecha y el usuario se resuelven aquí porque dependen de la sesión)
        private void GuardarDocumentoEnBD(int idFactura, DocumentoInfoFactura docInfo, SqlConnection connection, SqlTransaction transaction)
        {
            FacturaEscrituraService.InsertarDocumento(connection, transaction, idFactura,
                docInfo.NombreOriginal, docInfo.NombreArchivo, docInfo.RutaCompleta, docInfo.TipoArchivo,
                docInfo.TamanoBytes, DateTime.Now, ObtenerUsuarioActual(), docInfo.Descripcion);
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
                LogSGV.Error(ex, "Error al crear directorios de archivos en AgregarFactura");
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
        private bool ValidarNumeroPedido(string numeroPedido) =>
            DespachoValidaciones.ValidarNumeroPedido(numeroPedido);

        /// <summary>
        /// Valida y ajusta el formato del número de factura.
        /// </summary>
        private string ValidarYFormatearNumeroFactura(string numeroFactura) =>
            FacturaValidaciones.ValidarYFormatearNumeroFactura(numeroFactura);
    }
}