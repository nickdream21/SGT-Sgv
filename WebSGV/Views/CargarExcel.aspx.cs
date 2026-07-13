using System;
using System.Collections.Generic;
using ClosedXML.Excel;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Linq;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class CargarExcel : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                // Establecer valores predeterminados
                int currentMonth = DateTime.Now.Month;
                int currentYear = DateTime.Now.Year;
                ddlMes.SelectedValue = currentMonth.ToString();
                ddlAnio.SelectedValue = currentYear.ToString();

                // Cargar historial
                LoadUploadHistory();
            }
        }

        protected void btnProcesar_Click(object sender, EventArgs e)
        {
            if (fileUpload.HasFile)
            {
                try
                {
                    // Validar extensión del archivo
                    string fileExtension = Path.GetExtension(fileUpload.FileName).ToLower();
                    if (fileExtension != ".xlsx" && fileExtension != ".xls")
                    {
                        ShowAlert("Por favor, seleccione un archivo Excel válido (.xlsx o .xls).", "warning");
                        return;
                    }

                    // Validar tamaño del archivo (máx 10MB)
                    if (fileUpload.PostedFile.ContentLength > 10485760)
                    {
                        ShowAlert("El archivo es demasiado grande. El tamaño máximo es de 10MB.", "danger");
                        return;
                    }

                    // Obtener mes y año de los dropdowns
                    int selectedMonth = Convert.ToInt32(ddlMes.SelectedValue);
                    int selectedYear = Convert.ToInt32(ddlAnio.SelectedValue);

                    // Guardar archivo temporalmente
                    string tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + fileExtension);
                    fileUpload.SaveAs(tempFilePath);

                    // Procesar el archivo Excel (usando ClosedXML)
                    int rowsProcessed = ProcessExcelWithClosedXML(tempFilePath, selectedMonth, selectedYear);

                    // Eliminar el archivo temporal
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }

                    ShowAlert($"Proceso completado exitosamente. Se procesaron {rowsProcessed} registros.", "success");

                    // Refrescar la grilla
                    LoadUploadHistory();
                }
                catch (Exception ex)
                {
                    ShowAlert("Error al procesar el archivo: " + ex.Message, "danger");
                }
            }
            else
            {
                ShowAlert("Por favor, seleccione un archivo para cargar.", "warning");
            }
        }

        private int ProcessExcelWithClosedXML(string filePath, int month, int year)
        {
            int rowsProcessed = 0;

            using (var workbook = new XLWorkbook(filePath))
            {
                // Verificar que el archivo tenga hojas
                if (!workbook.Worksheets.Any())
                {
                    throw new Exception("El archivo Excel no contiene hojas de trabajo.");
                }

                // Intentar obtener la hoja por nombre primero, luego la primera disponible
                IXLWorksheet worksheet = workbook.Worksheets.Contains("Hoja1")
                    ? workbook.Worksheet("Hoja1")
                    : workbook.Worksheets.First();

                // Verificar que la hoja tenga datos
                var ultimaCelda = worksheet.LastCellUsed();
                if (ultimaCelda == null)
                {
                    throw new Exception("La hoja del archivo Excel está vacía.");
                }

                // Determinar el número de filas con datos
                int rowCount = ultimaCelda.Address.RowNumber;
                int colCount = ultimaCelda.Address.ColumnNumber;

                // Verificar que tenga al menos encabezados
                if (rowCount < 1)
                {
                    throw new Exception("La hoja no contiene datos suficientes (al menos debe tener encabezados).");
                }

                // Obtener los encabezados (primera fila)
                var headers = new List<string>();
                for (int col = 1; col <= colCount; col++)
                {
                    try
                    {
                        string header = worksheet.Cell(1, col).GetString().Trim();
                        if (!string.IsNullOrEmpty(header))
                        {
                            headers.Add(header);
                        }
                        else
                        {
                            // Si encontramos una celda vacía, detener la lectura de encabezados
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error al leer encabezado en columna {col}: {ex.Message}");
                    }
                }

                // Verificar que se encontraron encabezados
                if (headers.Count == 0)
                {
                    throw new Exception("No se encontraron encabezados válidos en la primera fila.");
                }

                // Verificar que existe el campo obligatorio 'numeroPedido'
                if (!headers.Contains("numeroPedido"))
                {
                    throw new Exception("El archivo debe contener la columna 'numeroPedido' en los encabezados.");
                }

                // Conexión a la base de datos SQL
                using (SqlConnection sqlConn = new SqlConnection(ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString))
                {
                    sqlConn.Open();

                    // Comenzar transacción
                    using (SqlTransaction transaction = sqlConn.BeginTransaction())
                    {
                        try
                        {
                            // Registrar la carga
                            int uploadId = RecordUploadHistory(sqlConn, transaction, Path.GetFileName(filePath), month, year);

                            // Eliminar registros existentes
                            DeleteExistingRecords(sqlConn, transaction, month, year);

                            // Recorrer las filas e insertar registros
                            for (int row = 2; row <= rowCount; row++) // Empezar desde la segunda fila (omitir encabezados)
                            {
                                try
                                {
                                    // Verificar si la fila tiene el número de pedido (campo obligatorio)
                                    int numeroPedidoIndex = headers.IndexOf("numeroPedido");
                                    if (numeroPedidoIndex == -1)
                                    {
                                        continue; // Saltar si no se encuentra la columna
                                    }

                                    string numeroPedido = ValorCelda(worksheet.Cell(row, numeroPedidoIndex + 1))?.ToString() ?? "";

                                    if (!string.IsNullOrEmpty(numeroPedido))
                                    {
                                        // Crear un diccionario con los datos de la fila
                                        var rowData = new Dictionary<string, object>();
                                        for (int col = 0; col < headers.Count; col++)
                                        {
                                            try
                                            {
                                                var cellValue = ValorCelda(worksheet.Cell(row, col + 1));
                                                rowData[headers[col]] = cellValue;
                                            }
                                            catch (Exception)
                                            {
                                                // Si hay error leyendo una celda, asignar null
                                                rowData[headers[col]] = null;
                                            }
                                        }

                                        // Insertar registro
                                        InsertRecord(sqlConn, transaction, rowData, headers, uploadId);
                                        rowsProcessed++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Log del error pero continuar con la siguiente fila
                                    System.Diagnostics.Debug.WriteLine($"Error procesando fila {row}: {ex.Message}");
                                }
                            }

                            // Actualizar historial de carga
                            UpdateUploadHistory(sqlConn, transaction, uploadId, rowsProcessed, 0);

                            // Confirmar transacción
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw new Exception("Error al procesar los datos: " + ex.Message);
                        }
                    }
                }
            }

            return rowsProcessed;
        }

        // Convierte una celda ClosedXML al mismo tipo de objeto que devolvía EPPlus
        // (null, double, string, DateTime, TimeSpan o bool) para no alterar la lógica posterior.
        private static object ValorCelda(IXLCell cell)
        {
            var v = cell.Value;
            switch (v.Type)
            {
                case XLDataType.Boolean: return v.GetBoolean();
                case XLDataType.Number: return v.GetNumber();
                case XLDataType.Text: return v.GetText();
                case XLDataType.DateTime: return v.GetDateTime();
                case XLDataType.TimeSpan: return v.GetTimeSpan();
                default: return null; // Blank o Error
            }
        }

        private int RecordUploadHistory(SqlConnection conn, SqlTransaction transaction, string fileName, int month, int year)
        {
            string currentUser = HttpContext.Current.User.Identity.Name;
            if (string.IsNullOrEmpty(currentUser))
                currentUser = "Sistema";

            string sql = @"
                INSERT INTO UploadHistory (FileName, UploadDate, Month, Year, RowsProcessed, Status, UploadedBy)
                VALUES (@FileName, @UploadDate, @Month, @Year, 0, 'Procesando', @UploadedBy);
                SELECT SCOPE_IDENTITY();";

            using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@FileName", fileName);
                cmd.Parameters.AddWithValue("@UploadDate", DateTime.Now);
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@Year", year);
                cmd.Parameters.AddWithValue("@UploadedBy", currentUser);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private void DeleteExistingRecords(SqlConnection conn, SqlTransaction transaction, int month, int year)
        {
            // Primero, obtener los IDs de todas las cargas para este mes/año
            string getUploadsQuery = @"
                SELECT UploadID FROM UploadHistory 
                WHERE Month = @Month AND Year = @Year";

            List<int> uploadIds = new List<int>();

            using (SqlCommand cmd = new SqlCommand(getUploadsQuery, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@Month", month);
                cmd.Parameters.AddWithValue("@Year", year);

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        uploadIds.Add(reader.GetInt32(0));
                    }
                }
            }

            // Eliminar registros de Indicadores para este mes/año
            foreach (int uploadId in uploadIds)
            {
                string deleteQuery = "DELETE FROM Indicadores WHERE UploadID = @UploadID";

                using (SqlCommand cmd = new SqlCommand(deleteQuery, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@UploadID", uploadId);
                    cmd.ExecuteNonQuery();
                }

                // Actualizar estado del historial de carga a 'Reemplazado'
                string updateQuery = "UPDATE UploadHistory SET Status = 'Reemplazado' WHERE UploadID = @UploadID";

                using (SqlCommand cmd = new SqlCommand(updateQuery, conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@UploadID", uploadId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void InsertRecord(SqlConnection conn, SqlTransaction transaction, Dictionary<string, object> rowData, List<string> headers, int uploadId)
        {
            // Lista de campos a insertar
            string[] fields = {
        "numeroPedido", "conductorOrigen", "tracto1", "carreta", "conductorDestino", "tracto2",
        "fechaHoraSalidaBase", "fechaHoraLlegadaTrujillo", "fechaHoraRegistro", "fechaHoraProgramacion",
        "fechaHoraIngresoPlanta", "fechaHoraInicioCarga", "fechaHoraTerminoCarga", "fechaHoraSalidaPlanta",
        "fechaHoraLlegadaBase", "fechaHoraSalidaBaseDepsa", "fechaHoraLlegadaDepsa", "fechaHoraInicioDepsa",
        "fechaHoraSalidaDepsa", "bodega", "fechaHoraLlegadaCebafE", "fechaHoraCruceE",
        "fechaHoraAutorizacionNacionalizacion", "bodegaEcuatoriana", "fechaHoraLlegadaTCI",
        "fechaHoraSalidaTCI", "bodegaDescarga", "fechaHoraLlegadaPlantaDescarga", "fechaHoraLlegadaAlmacen",
        "fechaHoraIngreso", "fechaHoraInicioDescarga", "fechaHoraTerminoDescarga", "fechaHoraSalida"
    };

            // Construir la consulta SQL
            System.Text.StringBuilder sql = new System.Text.StringBuilder();
            sql.Append("INSERT INTO Indicadores (");

            // Añadir los nombres de columnas
            List<string> columns = new List<string>();
            List<string> paramNames = new List<string>();

            foreach (string field in fields)
            {
                // Solo incluir campos que existen en los headers
                if (headers.Contains(field))
                {
                    columns.Add(field);
                    paramNames.Add("@" + field);
                }
            }

            // Añadir campos adicionales
            columns.Add("UploadID");
            paramNames.Add("@UploadID");
            columns.Add("usuarioCreacion");
            paramNames.Add("@usuarioCreacion");

            sql.Append(string.Join(", ", columns));
            sql.Append(") VALUES (");
            sql.Append(string.Join(", ", paramNames));
            sql.Append(")");

            using (SqlCommand cmd = new SqlCommand(sql.ToString(), conn, transaction))
            {
                // Añadir los parámetros
                foreach (string field in fields)
                {
                    if (headers.Contains(field))
                    {
                        object value = rowData.ContainsKey(field) ? rowData[field] : DBNull.Value;

                        // CORRECCIÓN: Manejar el campo numeroPedido especialmente para corregir notación científica
                        if (field == "numeroPedido" && value != DBNull.Value)
                        {
                            string strValue = value.ToString();
                            // Verificar si está en formato científico
                            if (strValue.Contains("E+") || strValue.Contains("e+"))
                            {
                                try
                                {
                                    // Convertir notación científica a número entero
                                    double doubleValue = Convert.ToDouble(strValue);
                                    string formattedValue = doubleValue.ToString("0");
                                    cmd.Parameters.AddWithValue("@" + field, formattedValue);
                                }
                                catch
                                {
                                    // Si falla la conversión, usar el valor original
                                    cmd.Parameters.AddWithValue("@" + field, strValue);
                                }
                            }
                            else
                            {
                                // Si no es notación científica, usar el valor tal cual
                                cmd.Parameters.AddWithValue("@" + field, strValue);
                            }
                        }
                        // Manejar campos de fechas como antes
                        else if (field.StartsWith("fechaHora") && value != DBNull.Value)
                        {
                            // Intentar convertir a DateTime
                            DateTime dateValue;
                            if (value is DateTime)
                            {
                                dateValue = (DateTime)value;
                                cmd.Parameters.AddWithValue("@" + field, dateValue);
                            }
                            else if (DateTime.TryParse(value.ToString(), out dateValue))
                            {
                                cmd.Parameters.AddWithValue("@" + field, dateValue);
                            }
                            else
                            {
                                cmd.Parameters.AddWithValue("@" + field, DBNull.Value);
                            }
                        }
                        else
                        {
                            // Para cualquier otro campo
                            cmd.Parameters.AddWithValue("@" + field, value ?? DBNull.Value);
                        }
                    }
                }

                // Añadir los campos adicionales
                cmd.Parameters.AddWithValue("@UploadID", uploadId);
                cmd.Parameters.AddWithValue("@usuarioCreacion", HttpContext.Current.User.Identity.Name ?? "Sistema");

                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateUploadHistory(SqlConnection conn, SqlTransaction transaction, int uploadId, int rowsProcessed, int rowsWithErrors)
        {
            string status = rowsWithErrors > 0 ? "Completado con errores" : "Completado";

            string sql = @"
                UPDATE UploadHistory 
                SET RowsProcessed = @RowsProcessed, 
                    Status = @Status,
                    ErrorCount = @ErrorCount
                WHERE UploadID = @UploadID";

            using (SqlCommand cmd = new SqlCommand(sql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@RowsProcessed", rowsProcessed);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@ErrorCount", rowsWithErrors);
                cmd.Parameters.AddWithValue("@UploadID", uploadId);

                cmd.ExecuteNonQuery();
            }
        }

        protected void btnCancelar_Click(object sender, EventArgs e)
        {
            Response.Redirect(Request.RawUrl);
        }

        protected void btnDescargarPlantilla_Click(object sender, EventArgs e)
        {
            // Descargar la plantilla de Excel
            string templatePath = Server.MapPath("~/Content/Templates/PlantillaIndicadores.xlsx");

            if (File.Exists(templatePath))
            {
                Response.Clear();
                Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                Response.AddHeader("Content-Disposition", "attachment; filename=PlantillaIndicadores.xlsx");
                Response.TransmitFile(templatePath);
                Response.End();
            }
            else
            {
                ShowAlert("La plantilla no se encuentra disponible. Por favor contacte al administrador.", "danger");
            }
        }

        // Método para cargar el historial de cargas
        private void LoadUploadHistory()
        {
            int? monthFilter = ddlMesFilter.SelectedValue != "0" ? Convert.ToInt32(ddlMesFilter.SelectedValue) : (int?)null;
            int? yearFilter  = ddlAnioFilter.SelectedValue != "0" ? Convert.ToInt32(ddlAnioFilter.SelectedValue) : (int?)null;

            var parametros = new List<SqlParameter>();
            string query = "SELECT UploadID, FileName, UploadDate, Month, Year, RowsProcessed, Status FROM UploadHistory WHERE Status != 'Eliminado'";

            if (monthFilter.HasValue) { query += " AND Month = @Month"; parametros.Add(DbHelper.Param("@Month", monthFilter.Value)); }
            if (yearFilter.HasValue)  { query += " AND Year = @Year";   parametros.Add(DbHelper.Param("@Year",  yearFilter.Value));  }

            query += " ORDER BY UploadDate DESC";

            DataTable dt = DbHelper.ConsultarTabla(query, parametros.ToArray());
            gvHistorial.DataSource = dt;
            gvHistorial.DataBind();
        }

        protected void UpdateHistorial(object sender, EventArgs e)
        {
            LoadUploadHistory();
        }

        protected void gvHistorial_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvHistorial.PageIndex = e.NewPageIndex;
            LoadUploadHistory();
        }

        protected string GetStatusClass(string status)
        {
            switch (status)
            {
                case "Completado":
                    return "status-success";
                case "Completado con errores":
                    return "status-warning";
                case "Procesando":
                    return "status-warning";
                case "Reemplazado":
                    return "status-info";
                default:
                    return "status-error";
            }
        }

        protected void btnDeleteUpload_Command(object sender, CommandEventArgs e)
        {
            if (e.CommandName == "DeleteUpload")
            {
                int uploadId = Convert.ToInt32(e.CommandArgument);

                try
                {
                    DbHelper.EnTransaccion((conn, tx) => {
                        DbHelper.EjecutarNonQuery(conn, tx,
                            "DELETE FROM Indicadores WHERE UploadID = @UploadID",
                            DbHelper.Param("@UploadID", uploadId));
                        DbHelper.EjecutarNonQuery(conn, tx,
                            "UPDATE UploadHistory SET Status = 'Eliminado' WHERE UploadID = @UploadID",
                            DbHelper.Param("@UploadID", uploadId));
                    });
                    ShowAlert("La carga y sus registros asociados han sido eliminados exitosamente.", "success");
                }
                catch (Exception ex)
                {
                    ShowAlert("Error al eliminar la carga: " + ex.Message, "danger");
                }

                // Actualizar la grilla
                LoadUploadHistory();
            }
        }

        private void ShowAlert(string message, string type)
        {
            alertMessage.Text = message;
            alertPanel.Visible = true;

            switch (type.ToLower())
            {
                case "success":
                    alertPanel.CssClass = "alert alert-success";
                    break;
                case "warning":
                    alertPanel.CssClass = "alert alert-warning";
                    break;
                case "danger":
                    alertPanel.CssClass = "alert alert-danger";
                    break;
                default:
                    alertPanel.CssClass = "alert alert-info";
                    break;
            }
        }
    }
}