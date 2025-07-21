using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace WebSGV.Views
{
    public partial class ConsultaAuditoria : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Establecer fecha hasta como hoy por defecto
                txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");

                // Establecer fecha desde como 30 días atrás por defecto
                txtFechaDesde.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");

                // Cargar datos iniciales
                CargarDatosAuditoria();
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            CargarDatosAuditoria();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            // Limpiar todos los filtros
            ddlTabla.SelectedIndex = 0;
            ddlOperacion.SelectedIndex = 0;
            txtFechaDesde.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtUsuario.Text = string.Empty;
            txtIdRegistro.Text = string.Empty;
            txtCampo.Text = string.Empty;
            txtValor.Text = string.Empty;

            // Recargar datos
            CargarDatosAuditoria();
        }

        protected void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                // Obtener datos para exportar (sin paginación)
                DataTable dt = ObtenerDatosAuditoria(false);

                // Configurar EPPlus para que no use licencia comercial
                //ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                using (ExcelPackage package = new ExcelPackage())
                {
                    // Añadir una hoja de trabajo
                    var worksheet = package.Workbook.Worksheets.Add("Auditoría");

                    // Añadir encabezados
                    worksheet.Cells[1, 1].Value = "ID";
                    worksheet.Cells[1, 2].Value = "Tabla";
                    worksheet.Cells[1, 3].Value = "Operación";
                    worksheet.Cells[1, 4].Value = "ID Registro";
                    worksheet.Cells[1, 5].Value = "Campo";
                    worksheet.Cells[1, 6].Value = "Valor Anterior";
                    worksheet.Cells[1, 7].Value = "Valor Nuevo";
                    worksheet.Cells[1, 8].Value = "Usuario";
                    worksheet.Cells[1, 9].Value = "Fecha y Hora";
                    worksheet.Cells[1, 10].Value = "Estación";
                    worksheet.Cells[1, 11].Value = "IP";

                    // Dar formato a los encabezados
                    using (var range = worksheet.Cells[1, 1, 1, 11])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    }

                    // Añadir datos
                    int row = 2;
                    foreach (DataRow dr in dt.Rows)
                    {
                        worksheet.Cells[row, 1].Value = dr["idAuditoria"];
                        worksheet.Cells[row, 2].Value = dr["TablaAfectada"];
                        worksheet.Cells[row, 3].Value = dr["TipoOperacion"];
                        worksheet.Cells[row, 4].Value = dr["IdRegistro"];
                        worksheet.Cells[row, 5].Value = dr["Campo"];
                        worksheet.Cells[row, 6].Value = dr["ValorAnterior"];
                        worksheet.Cells[row, 7].Value = dr["ValorNuevo"];
                        worksheet.Cells[row, 8].Value = dr["Usuario"];

                        // Formatear fecha
                        if (dr["FechaHora"] != DBNull.Value)
                        {
                            DateTime fecha = (DateTime)dr["FechaHora"];
                            worksheet.Cells[row, 9].Value = fecha.ToString("dd/MM/yyyy HH:mm:ss");
                        }

                        worksheet.Cells[row, 10].Value = dr["Estacion"];
                        worksheet.Cells[row, 11].Value = dr["IP"];

                        row++;
                    }

                    // Autoajustar columnas
                    worksheet.Cells.AutoFitColumns();

                    // Preparar para descarga
                    Response.Clear();
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    Response.AddHeader("content-disposition", "attachment; filename=Auditoria_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        package.SaveAs(memoryStream);
                        memoryStream.WriteTo(Response.OutputStream);
                        Response.Flush();
                        Response.End();
                    }
                }
            }
            catch (Exception ex)
            {
                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "showalert",
                    "alert('Error al exportar a Excel: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        protected void gvAuditoria_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvAuditoria.PageIndex = e.NewPageIndex;
            CargarDatosAuditoria();
        }

        protected void rptPager_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            int pageIndex = Convert.ToInt32(e.CommandArgument) - 1;
            gvAuditoria.PageIndex = pageIndex;
            CargarDatosAuditoria();
        }

        protected void gvAuditoria_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "VerDetalles")
            {
                int idAuditoria = Convert.ToInt32(e.CommandArgument);
                CargarDetallesAuditoria(idAuditoria);

                // Mostrar el modal usando JavaScript
                ScriptManager.RegisterStartupScript(this, GetType(), "ShowDetallesModal", "showDetallesModal();", true);
            }
        }

        private void CargarDatosAuditoria()
        {
            try
            {
                DataTable dt = ObtenerDatosAuditoria(true);
                gvAuditoria.DataSource = dt;
                gvAuditoria.DataBind();

                // Actualizar estadísticas
                if (dt.Rows.Count > 0)
                {
                    litEstadisticas.Text = $"<strong>Total de registros encontrados:</strong> {dt.Rows.Count}";

                    // Añadir información adicional basada en los filtros
                    if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
                    {
                        litEstadisticas.Text += $" | <strong>Tabla:</strong> {ddlTabla.SelectedValue}";
                    }

                    if (!string.IsNullOrEmpty(ddlOperacion.SelectedValue))
                    {
                        litEstadisticas.Text += $" | <strong>Operación:</strong> {ddlOperacion.SelectedValue}";
                    }
                }
                else
                {
                    litEstadisticas.Text = "<strong>No se encontraron registros</strong> con los criterios de búsqueda especificados.";
                }

                // Configurar paginador si hay muchos resultados
                if (dt.Rows.Count > gvAuditoria.PageSize)
                {
                    ConfigurarPaginacion(dt.Rows.Count, gvAuditoria.PageIndex);
                }
            }
            catch (Exception ex)
            {
                // Mostrar mensaje de error
                ScriptManager.RegisterStartupScript(this, GetType(), "showalert",
                    "alert('Error al cargar datos de auditoría: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        private DataTable ObtenerDatosAuditoria(bool paginar)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            DataTable dt = new DataTable();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                StringBuilder query = new StringBuilder();
                query.Append("SELECT * FROM Auditoria WHERE 1=1");

                // Aplicar filtros
                if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
                {
                    query.Append(" AND TablaAfectada = @TablaAfectada");
                }

                if (!string.IsNullOrEmpty(ddlOperacion.SelectedValue))
                {
                    query.Append(" AND TipoOperacion = @TipoOperacion");
                }

                if (!string.IsNullOrEmpty(txtFechaDesde.Text))
                {
                    query.Append(" AND FechaHora >= @FechaDesde");
                }

                if (!string.IsNullOrEmpty(txtFechaHasta.Text))
                {
                    query.Append(" AND FechaHora <= @FechaHasta");
                }

                if (!string.IsNullOrEmpty(txtUsuario.Text))
                {
                    query.Append(" AND Usuario LIKE @Usuario");
                }

                if (!string.IsNullOrEmpty(txtIdRegistro.Text))
                {
                    query.Append(" AND IdRegistro = @IdRegistro");
                }

                if (!string.IsNullOrEmpty(txtCampo.Text))
                {
                    query.Append(" AND Campo LIKE @Campo");
                }

                if (!string.IsNullOrEmpty(txtValor.Text))
                {
                    query.Append(" AND (ValorAnterior LIKE @Valor OR ValorNuevo LIKE @Valor)");
                }

                // Ordenar por fecha, más reciente primero
                query.Append(" ORDER BY FechaHora DESC");

                using (SqlCommand cmd = new SqlCommand(query.ToString(), conn))
                {
                    // Añadir parámetros
                    if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
                    {
                        cmd.Parameters.AddWithValue("@TablaAfectada", ddlTabla.SelectedValue);
                    }

                    if (!string.IsNullOrEmpty(ddlOperacion.SelectedValue))
                    {
                        cmd.Parameters.AddWithValue("@TipoOperacion", ddlOperacion.SelectedValue);
                    }

                    if (!string.IsNullOrEmpty(txtFechaDesde.Text))
                    {
                        cmd.Parameters.AddWithValue("@FechaDesde", Convert.ToDateTime(txtFechaDesde.Text).Date);
                    }

                    if (!string.IsNullOrEmpty(txtFechaHasta.Text))
                    {
                        // Añadir tiempo de 23:59:59 para que incluya todo el día
                        cmd.Parameters.AddWithValue("@FechaHasta", Convert.ToDateTime(txtFechaHasta.Text).Date.AddDays(1).AddSeconds(-1));
                    }

                    if (!string.IsNullOrEmpty(txtUsuario.Text))
                    {
                        cmd.Parameters.AddWithValue("@Usuario", "%" + txtUsuario.Text + "%");
                    }

                    if (!string.IsNullOrEmpty(txtIdRegistro.Text))
                    {
                        cmd.Parameters.AddWithValue("@IdRegistro", Convert.ToInt32(txtIdRegistro.Text));
                    }

                    if (!string.IsNullOrEmpty(txtCampo.Text))
                    {
                        cmd.Parameters.AddWithValue("@Campo", "%" + txtCampo.Text + "%");
                    }

                    if (!string.IsNullOrEmpty(txtValor.Text))
                    {
                        cmd.Parameters.AddWithValue("@Valor", "%" + txtValor.Text + "%");
                    }

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    adapter.Fill(dt);
                }
            }

            return dt;
        }

        private void CargarDetallesAuditoria(int idAuditoria)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Auditoria WHERE idAuditoria = @idAuditoria";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@idAuditoria", idAuditoria);

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        // Llenar los literales con la información
                        litIdAuditoria.Text = reader["idAuditoria"].ToString();
                        litTabla.Text = reader["TablaAfectada"].ToString();
                        litOperacion.Text = reader["TipoOperacion"].ToString();
                        litIdRegistro.Text = reader["IdRegistro"].ToString();
                        litCampo.Text = reader["Campo"].ToString();
                        litUsuario.Text = reader["Usuario"].ToString();
                        litEstacion.Text = reader["Estacion"].ToString();
                        litIP.Text = reader["IP"].ToString();

                        // Formatear fecha
                        if (reader["FechaHora"] != DBNull.Value)
                        {
                            DateTime fecha = (DateTime)reader["FechaHora"];
                            litFechaHora.Text = fecha.ToString("dd/MM/yyyy HH:mm:ss");
                        }

                        // Valores anterior y nuevo (podrían ser null)
                        litValorAnterior.Text = reader["ValorAnterior"] != DBNull.Value ?
                            HttpUtility.HtmlEncode(reader["ValorAnterior"].ToString()).Replace("\n", "<br/>") :
                            "<span class='text-muted'>No disponible</span>";

                        litValorNuevo.Text = reader["ValorNuevo"] != DBNull.Value ?
                            HttpUtility.HtmlEncode(reader["ValorNuevo"].ToString()).Replace("\n", "<br/>") :
                            "<span class='text-muted'>No disponible</span>";
                    }

                    reader.Close();
                }
            }
        }

        private void ConfigurarPaginacion(int totalRegistros, int paginaActual)
        {
            int paginasTotales = (int)Math.Ceiling((double)totalRegistros / gvAuditoria.PageSize);

            List<ListItem> pages = new List<ListItem>();

            // Añadir botón "Primera" si no estamos en la primera página
            if (paginaActual > 0)
            {
                pages.Add(new ListItem("<<", "1", true));
            }

            // Añadir botón "Anterior" si no estamos en la primera página
            if (paginaActual > 0)
            {
                pages.Add(new ListItem("<", (paginaActual).ToString(), true));
            }

            // Determinar rango de páginas a mostrar
            int startIndex = Math.Max(0, paginaActual - 2);
            int endIndex = Math.Min(paginasTotales - 1, paginaActual + 2);

            // Asegurar que siempre mostramos 5 enlaces si hay suficientes páginas
            if (endIndex - startIndex < 4 && paginasTotales > 5)
            {
                if (startIndex <= 2)
                {
                    endIndex = Math.Min(startIndex + 4, paginasTotales - 1);
                }
                else if (endIndex >= paginasTotales - 3)
                {
                    startIndex = Math.Max(endIndex - 4, 0);
                }
            }

            // Añadir enlaces de página
            for (int i = startIndex; i <= endIndex; i++)
            {
                bool isActive = i == paginaActual;
                pages.Add(new ListItem((i + 1).ToString(), (i + 1).ToString(), true) { Selected = isActive });
            }

            // Añadir botón "Siguiente" si no estamos en la última página
            if (paginaActual < paginasTotales - 1)
            {
                pages.Add(new ListItem(">", (paginaActual + 2).ToString(), true));
            }

            // Añadir botón "Última" si no estamos en la última página
            if (paginaActual < paginasTotales - 1)
            {
                pages.Add(new ListItem(">>", paginasTotales.ToString(), true));
            }

            // Asignar datos al repeater de paginación
            rptPager.DataSource = pages;
            rptPager.DataBind();
        }
    }
}