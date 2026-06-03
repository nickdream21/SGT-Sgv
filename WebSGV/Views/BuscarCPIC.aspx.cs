using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class BusquedaCPIC : PaginaBase
    {
        // Configuración para archivos
        private const long MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private readonly string[] ALLOWED_EXTENSIONS = { ".pdf", ".doc", ".docx", ".jpg", ".jpeg", ".png" };

        // Propiedad para controlar el modo de edición
        protected bool ModoEdicionActivo
        {
            get
            {
                return ViewState["ModoEdicionActivo"] != null ? (bool)ViewState["ModoEdicionActivo"] : false;
            }
            set
            {
                ViewState["ModoEdicionActivo"] = value;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdminOSupervisor();

            if (!IsPostBack)
            {
                CrearDirectoriosUpload();
            }
        }

        protected void BuscarCPICClick(object sender, EventArgs e)
        {
            string numeroCPIC = txtBuscarCPIC.Text.Trim();

            if (string.IsNullOrEmpty(numeroCPIC))
            {
                MostrarMensaje("Por favor, ingrese un número de CPIC para buscar.", "danger");
                return;
            }

            if (numeroCPIC.Length != 7)
            {
                MostrarMensaje("El número de CPIC debe tener exactamente 7 caracteres.", "warning");
                return;
            }

            try
            {
                // Buscar el CPIC en la base de datos
                CPIC_Actualizado cpic = ObtenerCPIC(numeroCPIC);

                if (cpic != null)
                {
                    // Mostrar datos del CPIC
                    MostrarDatosCPIC(cpic);
                    CargarDocumentosCPIC(cpic.IdCPIC);
                    pnlResultados.Visible = true;
                    pnlNoResultados.Visible = false;

                    // Asegurar que el modo edición esté desactivado al buscar
                    ModoEdicionActivo = false;
                    pnlUploadDocumentos.Visible = false;

                    MostrarMensaje($"CPIC {numeroCPIC} encontrado correctamente.", "success");
                }
                else
                {
                    // No se encontró el CPIC
                    pnlResultados.Visible = false;
                    pnlNoResultados.Visible = true;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al buscar el CPIC: " + ex.Message, "danger");
                Debug.WriteLine("Error en BuscarCPICClick: " + ex.Message);
            }
        }

        private CPIC_Actualizado ObtenerCPIC(string numeroCPIC)
        {
            try
            {
                DataTable dtCPIC = DbHelper.ConsultarTabla(@"
                    SELECT c.idCPIC, c.numeroCPIC, c.idFactura, c.valorTotalFlete, c.fechaEmision,
                           c.pesoNeto, c.pesoBruto, f.numeroFactura
                    FROM CPIC c
                    LEFT JOIN Factura f ON c.idFactura = f.idFactura
                    WHERE c.numeroCPIC = @numeroCPIC",
                    DbHelper.Param("@numeroCPIC", numeroCPIC));

                if (dtCPIC.Rows.Count == 0) return null;

                DataRow row = dtCPIC.Rows[0];
                var cpic = new CPIC_Actualizado
                {
                    IdCPIC         = Convert.ToInt32(row["idCPIC"]),
                    NumeroCPIC     = row["numeroCPIC"].ToString(),
                    IdFactura      = row["idFactura"]    != DBNull.Value ? Convert.ToInt32(row["idFactura"])    : 0,
                    NumeroFactura  = row["numeroFactura"] != DBNull.Value ? row["numeroFactura"].ToString()     : "",
                    FechaEmision   = Convert.ToDateTime(row["fechaEmision"]),
                    ValorTotalFlete= Convert.ToDecimal(row["valorTotalFlete"]),
                    PesoNeto       = row["pesoNeto"]  != DBNull.Value ? Convert.ToDecimal(row["pesoNeto"])  : 0,
                    PesoBruto      = row["pesoBruto"] != DBNull.Value ? Convert.ToDecimal(row["pesoBruto"]) : 0,
                    Productos      = new List<ProductoCPIC_Actualizado>()
                };

                DataTable dtProd = DbHelper.ConsultarTabla(@"
                    SELECT cp.idCPIC, cp.idProducto, cp.cantidadBolsasProducto, p.nombre as nombreProducto
                    FROM CPIC_Productos cp
                    JOIN Producto p ON cp.idProducto = p.idProducto
                    WHERE cp.idCPIC = @idCPIC",
                    DbHelper.Param("@idCPIC", cpic.IdCPIC));

                int id = 1;
                foreach (DataRow r in dtProd.Rows)
                {
                    cpic.Productos.Add(new ProductoCPIC_Actualizado
                    {
                        ID            = id++,
                        IdCPIC        = cpic.IdCPIC,
                        IdProducto    = Convert.ToInt32(r["idProducto"]),
                        NombreProducto= r["nombreProducto"].ToString(),
                        CantidadBolsas= Convert.ToInt32(r["cantidadBolsasProducto"])
                    });
                }

                return cpic;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener CPIC: " + ex.Message);
                throw new Exception("Error al obtener el CPIC: " + ex.Message);
            }
        }

        private void CargarDocumentosCPIC(int idCPIC)
        {
            try
            {
                DataTable dt = ObtenerDocumentosCPIC(idCPIC);
                gvDocumentos.DataSource = dt;
                gvDocumentos.DataBind();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al cargar documentos: " + ex.Message);
                MostrarMensaje("Error al cargar documentos: " + ex.Message, "warning");
            }
        }

        private DataTable ObtenerDocumentosCPIC(int idCPIC)
        {
            try
            {
                return DbHelper.ConsultarTabla(@"
                    SELECT idDocumento, nombreOriginal, tipoArchivo, tamanoBytes,
                           fechaSubida, usuarioSubida, descripcion, rutaArchivo
                    FROM DocumentosCPIC
                    WHERE idCPIC = @idCPIC AND activo = 1
                    ORDER BY fechaSubida DESC",
                    DbHelper.Param("@idCPIC", idCPIC));
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener documentos: " + ex.Message);
                return new DataTable();
            }
        }

        private void MostrarDatosCPIC(CPIC_Actualizado cpic)
        {
            // Llenar los campos con la información del CPIC
            txtNumCPIC.Text = cpic.NumeroCPIC;
            txtNumFactura.Text = cpic.NumeroFactura;
            txtFechaEmision.Text = cpic.FechaEmision.ToString("yyyy-MM-dd");
            txtTotalFlete.Text = cpic.ValorTotalFlete.ToString("N2");

            // NUEVOS CAMPOS DE PESO
            txtPesoNeto.Text = cpic.PesoNeto.ToString("N2");
            txtPesoBruto.Text = cpic.PesoBruto.ToString("N2");

            // Cargar la tabla de productos (sin peso)
            DataTable dt = new DataTable();
            dt.Columns.Add("ID", typeof(int));
            dt.Columns.Add("IdProducto", typeof(int));
            dt.Columns.Add("NombreProducto", typeof(string));
            dt.Columns.Add("Cantidad", typeof(int));
            // Se elimina la columna Peso

            foreach (var producto in cpic.Productos)
            {
                dt.Rows.Add(producto.ID, producto.IdProducto, producto.NombreProducto, producto.CantidadBolsas);
            }

            gvProductos.DataSource = dt;
            gvProductos.DataBind();
        }

        protected void HabilitarEdicion(object sender, EventArgs e)
        {
            // Habilitar la edición de los campos
            txtNumFactura.ReadOnly = false;
            txtFechaEmision.ReadOnly = false;
            txtPesoNeto.ReadOnly = false;    // NUEVO
            txtPesoBruto.ReadOnly = false;   // NUEVO

            // Activar modo de edición
            ModoEdicionActivo = true;
            pnlUploadDocumentos.Visible = true;

            // Mostrar el botón de guardar cambios
            btnHabilitarEdicion.Visible = false;
            btnGuardarCambios.Visible = true;

            // Rebind del GridView de documentos para mostrar botones de eliminar
            if (!string.IsNullOrEmpty(txtNumCPIC.Text))
            {
                CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
                if (cpic != null)
                {
                    CargarDocumentosCPIC(cpic.IdCPIC);
                }
            }

            MostrarMensaje("Modo de edición activado. Puede editar los campos, agregar o eliminar documentos.", "info");
        }

        protected void GuardarCambios(object sender, EventArgs e)
        {
            try
            {
                // Recolectar datos actualizados
                string numeroCPIC = txtNumCPIC.Text;
                string numeroFactura = txtNumFactura.Text;
                DateTime fechaEmision;
                decimal pesoNeto, pesoBruto;

                if (!DateTime.TryParse(txtFechaEmision.Text, out fechaEmision))
                {
                    MostrarMensaje("Formato de fecha inválido.", "danger");
                    return;
                }

                if (!decimal.TryParse(txtPesoNeto.Text, out pesoNeto) || pesoNeto <= 0)
                {
                    MostrarMensaje("El peso neto debe ser un valor válido mayor a 0.", "danger");
                    return;
                }

                if (!decimal.TryParse(txtPesoBruto.Text, out pesoBruto) || pesoBruto <= 0)
                {
                    MostrarMensaje("El peso bruto debe ser un valor válido mayor a 0.", "danger");
                    return;
                }

                if (pesoBruto <= pesoNeto)
                {
                    MostrarMensaje("El peso bruto debe ser mayor al peso neto.", "danger");
                    return;
                }

                // Actualizar el CPIC en la base de datos
                bool actualizado = ActualizarCPIC(numeroCPIC, numeroFactura, fechaEmision, pesoNeto, pesoBruto);

                if (actualizado)
                {
                    // Volver al modo de sólo lectura
                    DesactivarModoEdicion();
                    MostrarMensaje("CPIC actualizado correctamente.", "success");
                }
                else
                {
                    MostrarMensaje("No se pudo actualizar el CPIC.", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al guardar los cambios: " + ex.Message, "danger");
                Debug.WriteLine("Error en GuardarCambios: " + ex.Message);
            }
        }

        private void DesactivarModoEdicion()
        {
            txtNumFactura.ReadOnly = true;
            txtFechaEmision.ReadOnly = true;
            txtPesoNeto.ReadOnly = true;
            txtPesoBruto.ReadOnly = true;

            ModoEdicionActivo = false;
            pnlUploadDocumentos.Visible = false;

            btnHabilitarEdicion.Visible = true;
            btnGuardarCambios.Visible = false;

            // Limpiar campos de upload
            txtDescripcionDoc.Text = "";

            // Rebind del GridView de documentos para ocultar botones de eliminar
            if (!string.IsNullOrEmpty(txtNumCPIC.Text))
            {
                CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
                if (cpic != null)
                {
                    CargarDocumentosCPIC(cpic.IdCPIC);
                }
            }
        }

        private bool ActualizarCPIC(string numeroCPIC, string numeroFactura, DateTime fechaEmision, decimal pesoNeto, decimal pesoBruto)
        {
            try
            {
                object resFactura = DbHelper.EjecutarEscalar(
                    "SELECT idFactura FROM Factura WHERE numeroFactura = @numeroFactura",
                    DbHelper.Param("@numeroFactura", numeroFactura));
                int idFactura = (resFactura != null && resFactura != DBNull.Value) ? Convert.ToInt32(resFactura) : 0;

                var parametros = new List<SqlParameter>
                {
                    DbHelper.Param("@fechaEmision", fechaEmision),
                    DbHelper.Param("@pesoNeto",     pesoNeto),
                    DbHelper.Param("@pesoBruto",    pesoBruto),
                    DbHelper.Param("@numeroCPIC",   numeroCPIC)
                };

                string query = @"UPDATE CPIC SET fechaEmision=@fechaEmision, pesoNeto=@pesoNeto, pesoBruto=@pesoBruto";
                if (idFactura > 0)
                {
                    query += ", idFactura=@idFactura";
                    parametros.Add(DbHelper.Param("@idFactura", idFactura));
                }
                query += " WHERE numeroCPIC=@numeroCPIC";

                return DbHelper.EjecutarNonQuery(query, parametros.ToArray()) > 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al actualizar CPIC: " + ex.Message);
                throw new Exception("Error al actualizar el CPIC: " + ex.Message);
            }
        }

        protected void Cancelar(object sender, EventArgs e)
        {
            // Volver al modo de solo lectura
            DesactivarModoEdicion();
            lblMensaje.Text = "";
            MostrarMensaje("Edición cancelada.", "info");
        }

        protected void NuevaBusqueda(object sender, EventArgs e)
        {
            // Limpiar los campos y ocultar paneles de resultados
            txtBuscarCPIC.Text = "";
            pnlResultados.Visible = false;
            pnlNoResultados.Visible = false;
            pnlUploadDocumentos.Visible = false;
            ModoEdicionActivo = false;
            lblMensaje.Text = "";
        }

        // ============ FUNCIONALIDAD DE DOCUMENTOS ============

        protected void SubirDocumento(object sender, EventArgs e)
        {
            try
            {
                if (!fileUploadCPIC.HasFile)
                {
                    MostrarMensaje("Debe seleccionar un archivo para subir.", "warning");
                    return;
                }

                // Validar archivo
                string resultadoValidacion = ValidarArchivo();
                if (!string.IsNullOrEmpty(resultadoValidacion))
                {
                    MostrarMensaje(resultadoValidacion, "danger");
                    return;
                }

                // Obtener ID del CPIC actual
                CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
                if (cpic == null)
                {
                    MostrarMensaje("No se puede obtener el CPIC actual.", "danger");
                    return;
                }

                // Procesar y guardar archivo
                DocumentoInfo docInfo = ProcesarArchivo(cpic.IdCPIC, cpic.NumeroCPIC);
                if (docInfo != null)
                {
                    GuardarDocumentoEnBD(cpic.IdCPIC, docInfo);
                    MostrarMensaje("Documento subido correctamente.", "success");
                    txtDescripcionDoc.Text = "";
                    CargarDocumentosCPIC(cpic.IdCPIC);
                    ScriptManager.RegisterStartupScript(this, GetType(), "clearFile", "clearFileSelection();", true);
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al subir documento: " + ex.Message, "danger");
                Debug.WriteLine("Error en SubirDocumento: " + ex.Message);
            }
        }

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
                    case "Eliminar":
                        EliminarDocumento(idDocumento);
                        break;
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al procesar documento: " + ex.Message, "danger");
                Debug.WriteLine("Error en gvDocumentos_RowCommand: " + ex.Message);
            }
        }

        private void EliminarDocumento(int idDocumento)
        {
            try
            {
                int rows = DbHelper.EjecutarNonQuery(@"
                    UPDATE DocumentosCPIC
                    SET activo=0, fechaEliminacion=@fechaEliminacion, usuarioEliminacion=@usuarioEliminacion
                    WHERE idDocumento=@idDocumento",
                    DbHelper.Param("@idDocumento",         idDocumento),
                    DbHelper.Param("@fechaEliminacion",    DateTime.Now),
                    DbHelper.Param("@usuarioEliminacion",  ObtenerUsuarioActual()));

                if (rows > 0)
                {
                    MostrarMensaje("Documento eliminado correctamente.", "success");
                    CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
                    if (cpic != null) CargarDocumentosCPIC(cpic.IdCPIC);
                }
                else
                {
                    MostrarMensaje("No se pudo eliminar el documento.", "danger");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al eliminar documento: " + ex.Message);
                MostrarMensaje("Error al eliminar documento: " + ex.Message, "danger");
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
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT nombreOriginal, rutaArchivo, tipoArchivo FROM DocumentosCPIC WHERE idDocumento=@idDocumento AND activo=1",
                    DbHelper.Param("@idDocumento", idDocumento));
                return dt.Rows.Count > 0 ? dt.Rows[0] : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al obtener info documento: " + ex.Message);
                return null;
            }
        }

        // ============ MÉTODOS DE ARCHIVOS (Reutilizados de AgregarCPIC) ============

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

        private void GuardarDocumentoEnBD(int idCPIC, DocumentoInfo docInfo)
        {
            DbHelper.EjecutarNonQuery(@"
                INSERT INTO DocumentosCPIC
                (idCPIC, nombreOriginal, nombreArchivo, rutaArchivo, tipoArchivo, tamanoBytes, fechaSubida, usuarioSubida, descripcion, activo)
                VALUES
                (@idCPIC, @nombreOriginal, @nombreArchivo, @rutaArchivo, @tipoArchivo, @tamanoBytes, @fechaSubida, @usuarioSubida, @descripcion, 1)",
                DbHelper.Param("@idCPIC",          idCPIC),
                DbHelper.Param("@nombreOriginal",  docInfo.NombreOriginal),
                DbHelper.Param("@nombreArchivo",   docInfo.NombreArchivo),
                DbHelper.Param("@rutaArchivo",     docInfo.RutaCompleta),
                DbHelper.Param("@tipoArchivo",     docInfo.TipoArchivo),
                DbHelper.Param("@tamanoBytes",     docInfo.TamanoBytes),
                DbHelper.Param("@fechaSubida",     DateTime.Now),
                DbHelper.Param("@usuarioSubida",   ObtenerUsuarioActual()),
                DbHelper.Param("@descripcion",     (object)docInfo.Descripcion ?? DBNull.Value));
        }

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

        // ============ FUNCIONES AUXILIARES PARA LA VISTA ============

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

        // ============ FUNCIONALIDAD DE PRODUCTOS ============

        protected void gvProductos_RowEditing(object sender, GridViewEditEventArgs e)
        {
            gvProductos.EditIndex = e.NewEditIndex;

            CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
            if (cpic != null)
            {
                MostrarDatosCPIC(cpic);

                DropDownList ddlProductos = (DropDownList)gvProductos.Rows[e.NewEditIndex].FindControl("ddlProductos");
                if (ddlProductos != null)
                {
                    CargarProductos(ddlProductos);
                    DataRowView dr = gvProductos.Rows[e.NewEditIndex].DataItem as DataRowView;
                    if (dr != null)
                    {
                        ddlProductos.SelectedValue = dr["IdProducto"].ToString();
                    }
                }
            }
        }

        private void CargarProductos(DropDownList ddl)
        {
            try
            {
                ddl.DataSource     = DbHelper.ConsultarTabla("SELECT idProducto, nombre FROM Producto ORDER BY nombre");
                ddl.DataTextField  = "nombre";
                ddl.DataValueField = "idProducto";
                ddl.DataBind();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los productos: " + ex.Message, "danger");
            }
        }

        protected void gvProductos_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            gvProductos.EditIndex = -1;
            CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
            if (cpic != null)
            {
                MostrarDatosCPIC(cpic);
            }
        }

        protected void gvProductos_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            try
            {
                GridViewRow row = gvProductos.Rows[e.RowIndex];
                int id = Convert.ToInt32(gvProductos.DataKeys[e.RowIndex].Value);

                DropDownList ddlProductos = (DropDownList)row.FindControl("ddlProductos");
                TextBox txtCantidad = (TextBox)row.FindControl("txtCantidad");

                int idProducto = Convert.ToInt32(ddlProductos.SelectedValue);
                string nombreProducto = ddlProductos.SelectedItem.Text;
                int cantidad = Convert.ToInt32(txtCantidad.Text);

                if (idProducto <= 0 || cantidad <= 0)
                {
                    MostrarMensaje("Por favor, complete todos los campos correctamente.", "danger");
                    return;
                }

                bool actualizado = ActualizarProductoCPIC(id, idProducto, nombreProducto, cantidad);

                if (actualizado)
                {
                    gvProductos.EditIndex = -1;
                    CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
                    if (cpic != null)
                    {
                        MostrarDatosCPIC(cpic);
                        MostrarMensaje("Producto actualizado correctamente.", "success");
                    }
                }
                else
                {
                    MostrarMensaje("No se pudo actualizar el producto.", "danger");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el producto: " + ex.Message, "danger");
                Debug.WriteLine("Error en gvProductos_RowUpdating: " + ex.Message);
            }
        }

        private bool ActualizarProductoCPIC(int id, int idProducto, string nombreProducto, int cantidad)
        {
            try
            {
                CPIC_Actualizado cpic = ObtenerCPIC(txtNumCPIC.Text);
                if (cpic == null) return false;

                int idCPIC = cpic.IdCPIC;
                ProductoCPIC_Actualizado productoOriginal = cpic.Productos.FirstOrDefault(p => p.ID == id);
                int idProductoOriginal = productoOriginal != null ? productoOriginal.IdProducto : idProducto;

                int existeRegistro = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM CPIC_Productos WHERE idCPIC=@idCPIC AND idProducto=@idProductoOriginal",
                    DbHelper.Param("@idCPIC",             idCPIC),
                    DbHelper.Param("@idProductoOriginal", idProductoOriginal)));

                if (existeRegistro > 0 && idProductoOriginal != idProducto)
                {
                    DbHelper.EjecutarNonQuery(
                        "DELETE FROM CPIC_Productos WHERE idCPIC=@idCPIC AND idProducto=@idProductoOriginal",
                        DbHelper.Param("@idCPIC",             idCPIC),
                        DbHelper.Param("@idProductoOriginal", idProductoOriginal));
                    existeRegistro = 0;
                }

                if (existeRegistro > 0)
                {
                    return DbHelper.EjecutarNonQuery(
                        "UPDATE CPIC_Productos SET cantidadBolsasProducto=@cantidad WHERE idCPIC=@idCPIC AND idProducto=@idProducto",
                        DbHelper.Param("@idCPIC",    idCPIC),
                        DbHelper.Param("@idProducto",idProducto),
                        DbHelper.Param("@cantidad",  cantidad)) > 0;
                }
                else
                {
                    return DbHelper.EjecutarNonQuery(
                        "INSERT INTO CPIC_Productos (idCPIC, idProducto, cantidadBolsasProducto, pesoKg) VALUES (@idCPIC, @idProducto, @cantidad, 0)",
                        DbHelper.Param("@idCPIC",    idCPIC),
                        DbHelper.Param("@idProducto",idProducto),
                        DbHelper.Param("@cantidad",  cantidad)) > 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Error al actualizar producto: " + ex.Message);
                throw new Exception("Error al actualizar el producto: " + ex.Message);
            }
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo}";
            lblMensaje.Visible = true;
        }

        // ============ CLASES AUXILIARES ============

        private class DocumentoInfo
        {
            public string NombreOriginal { get; set; }
            public string NombreArchivo { get; set; }
            public string RutaCompleta { get; set; }
            public string TipoArchivo { get; set; }
            public long TamanoBytes { get; set; }
            public string Descripcion { get; set; }
        }
    }

    // ============ CLASES DE DATOS ACTUALIZADAS ============

    public class CPIC_Actualizado
    {
        public int IdCPIC { get; set; }
        public string NumeroCPIC { get; set; }
        public int IdFactura { get; set; }
        public string NumeroFactura { get; set; }
        public DateTime FechaEmision { get; set; }
        public decimal ValorTotalFlete { get; set; }
        public decimal PesoNeto { get; set; }      // NUEVO CAMPO
        public decimal PesoBruto { get; set; }     // NUEVO CAMPO
        public List<ProductoCPIC_Actualizado> Productos { get; set; }
    }

    public class ProductoCPIC_Actualizado
    {
        public int ID { get; set; }
        public int IdCPIC { get; set; }
        public int IdProducto { get; set; }
        public string NombreProducto { get; set; }
        public int CantidadBolsas { get; set; }
        // Se elimina el campo PesoKg
    }
}