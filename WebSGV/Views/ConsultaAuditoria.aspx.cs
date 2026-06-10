using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class ConsultaAuditoria : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");
                txtFechaDesde.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                CargarDatosAuditoria();
            }
        }

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            CargarDatosAuditoria();
        }

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            ddlTabla.SelectedIndex = 0;
            ddlOperacion.SelectedIndex = 0;
            txtFechaDesde.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtUsuario.Text = string.Empty;
            txtIdRegistro.Text = string.Empty;
            txtCampo.Text = string.Empty;
            txtValor.Text = string.Empty;
            CargarDatosAuditoria();
        }

        protected void btnExportar_Click(object sender, EventArgs e)
        {
            try
            {
                DataTable dt = ObtenerDatosAuditoria(false);

                using (XLWorkbook workbook = new XLWorkbook())
                {
                    var worksheet = workbook.AddWorksheet("Auditoría");

                    worksheet.Cell(1, 1).Value = "ID";
                    worksheet.Cell(1, 2).Value = "Tabla";
                    worksheet.Cell(1, 3).Value = "Operación";
                    worksheet.Cell(1, 4).Value = "ID Registro";
                    worksheet.Cell(1, 5).Value = "Campo";
                    worksheet.Cell(1, 6).Value = "Valor Anterior";
                    worksheet.Cell(1, 7).Value = "Valor Nuevo";
                    worksheet.Cell(1, 8).Value = "Usuario";
                    worksheet.Cell(1, 9).Value = "Fecha y Hora";
                    worksheet.Cell(1, 10).Value = "Estación";
                    worksheet.Cell(1, 11).Value = "IP";

                    var range = worksheet.Range(1, 1, 1, 11);
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = XLFillPatternValues.Solid;
                    range.Style.Fill.BackgroundColor = XLColor.LightBlue;

                    int row = 2;
                    foreach (DataRow dr in dt.Rows)
                    {
                        worksheet.Cell(row, 1).Value = ValorExcel(dr["idAuditoria"]);
                        worksheet.Cell(row, 2).Value = ValorExcel(dr["TablaAfectada"]);
                        worksheet.Cell(row, 3).Value = ValorExcel(dr["TipoOperacion"]);
                        worksheet.Cell(row, 4).Value = ValorExcel(dr["IdRegistro"]);
                        worksheet.Cell(row, 5).Value = ValorExcel(dr["Campo"]);
                        worksheet.Cell(row, 6).Value = ValorExcel(dr["ValorAnterior"]);
                        worksheet.Cell(row, 7).Value = ValorExcel(dr["ValorNuevo"]);
                        worksheet.Cell(row, 8).Value = ValorExcel(dr["Usuario"]);

                        if (dr["FechaHora"] != DBNull.Value)
                        {
                            DateTime fecha = (DateTime)dr["FechaHora"];
                            worksheet.Cell(row, 9).Value = fecha.ToString("dd/MM/yyyy HH:mm:ss");
                        }

                        worksheet.Cell(row, 10).Value = ValorExcel(dr["Estacion"]);
                        worksheet.Cell(row, 11).Value = ValorExcel(dr["IP"]);

                        row++;
                    }

                    worksheet.Columns().AdjustToContents();

                    Response.Clear();
                    Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    Response.AddHeader("content-disposition", "attachment; filename=Auditoria_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx");

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        workbook.SaveAs(memoryStream);
                        memoryStream.WriteTo(Response.OutputStream);
                        Response.Flush();
                        Response.End();
                    }
                }
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "showalert",
                    "alert('Error al exportar a Excel: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        private static XLCellValue ValorExcel(object valor)
        {
            if (valor == null || valor == DBNull.Value) return Blank.Value;
            return XLCellValue.FromObject(valor);
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

                if (dt.Rows.Count > 0)
                {
                    litEstadisticas.Text = $"<strong>Total de registros encontrados:</strong> {dt.Rows.Count}";

                    if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
                        litEstadisticas.Text += $" | <strong>Tabla:</strong> {ddlTabla.SelectedValue}";

                    if (!string.IsNullOrEmpty(ddlOperacion.SelectedValue))
                        litEstadisticas.Text += $" | <strong>Operación:</strong> {ddlOperacion.SelectedValue}";
                }
                else
                {
                    litEstadisticas.Text = "<strong>No se encontraron registros</strong> con los criterios de búsqueda especificados.";
                }

                if (dt.Rows.Count > gvAuditoria.PageSize)
                    ConfigurarPaginacion(dt.Rows.Count, gvAuditoria.PageIndex);
            }
            catch (Exception ex)
            {
                ScriptManager.RegisterStartupScript(this, GetType(), "showalert",
                    "alert('Error al cargar datos de auditoría: " + ex.Message.Replace("'", "\\'") + "');", true);
            }
        }

        private DataTable ObtenerDatosAuditoria(bool paginar)
        {
            var parametros = new List<SqlParameter>();
            StringBuilder query = new StringBuilder();
            query.Append("SELECT * FROM Auditoria WHERE 1=1");

            if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
            {
                query.Append(" AND TablaAfectada = @TablaAfectada");
                parametros.Add(DbHelper.Param("@TablaAfectada", ddlTabla.SelectedValue));
            }

            if (!string.IsNullOrEmpty(ddlOperacion.SelectedValue))
            {
                query.Append(" AND TipoOperacion = @TipoOperacion");
                parametros.Add(DbHelper.Param("@TipoOperacion", ddlOperacion.SelectedValue));
            }

            if (!string.IsNullOrEmpty(txtFechaDesde.Text))
            {
                query.Append(" AND FechaHora >= @FechaDesde");
                parametros.Add(DbHelper.Param("@FechaDesde", Convert.ToDateTime(txtFechaDesde.Text).Date));
            }

            if (!string.IsNullOrEmpty(txtFechaHasta.Text))
            {
                query.Append(" AND FechaHora <= @FechaHasta");
                parametros.Add(DbHelper.Param("@FechaHasta", Convert.ToDateTime(txtFechaHasta.Text).Date.AddDays(1).AddSeconds(-1)));
            }

            if (!string.IsNullOrEmpty(txtUsuario.Text))
            {
                query.Append(" AND Usuario LIKE @Usuario");
                parametros.Add(DbHelper.Param("@Usuario", "%" + txtUsuario.Text + "%"));
            }

            if (!string.IsNullOrEmpty(txtIdRegistro.Text))
            {
                query.Append(" AND IdRegistro = @IdRegistro");
                parametros.Add(DbHelper.Param("@IdRegistro", Convert.ToInt32(txtIdRegistro.Text)));
            }

            if (!string.IsNullOrEmpty(txtCampo.Text))
            {
                query.Append(" AND Campo LIKE @Campo");
                parametros.Add(DbHelper.Param("@Campo", "%" + txtCampo.Text + "%"));
            }

            if (!string.IsNullOrEmpty(txtValor.Text))
            {
                query.Append(" AND (ValorAnterior LIKE @Valor OR ValorNuevo LIKE @Valor)");
                parametros.Add(DbHelper.Param("@Valor", "%" + txtValor.Text + "%"));
            }

            query.Append(" ORDER BY FechaHora DESC");
            return DbHelper.ConsultarTabla(query.ToString(), parametros.ToArray());
        }

        private void CargarDetallesAuditoria(int idAuditoria)
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT * FROM Auditoria WHERE idAuditoria = @idAuditoria",
                DbHelper.Param("@idAuditoria", idAuditoria));

            if (dt.Rows.Count == 0) return;
            DataRow reader = dt.Rows[0];

            litIdAuditoria.Text = reader["idAuditoria"].ToString();
            litTabla.Text = reader["TablaAfectada"].ToString();
            litOperacion.Text = reader["TipoOperacion"].ToString();
            litIdRegistro.Text = reader["IdRegistro"].ToString();
            litCampo.Text = reader["Campo"].ToString();
            litUsuario.Text = reader["Usuario"].ToString();
            litEstacion.Text = reader["Estacion"].ToString();
            litIP.Text = reader["IP"].ToString();

            if (reader["FechaHora"] != DBNull.Value)
            {
                DateTime fecha = (DateTime)reader["FechaHora"];
                litFechaHora.Text = fecha.ToString("dd/MM/yyyy HH:mm:ss");
            }

            litValorAnterior.Text = reader["ValorAnterior"] != DBNull.Value
                ? HttpUtility.HtmlEncode(reader["ValorAnterior"].ToString()).Replace("\n", "<br/>")
                : "<span class='text-muted'>No disponible</span>";

            litValorNuevo.Text = reader["ValorNuevo"] != DBNull.Value
                ? HttpUtility.HtmlEncode(reader["ValorNuevo"].ToString()).Replace("\n", "<br/>")
                : "<span class='text-muted'>No disponible</span>";
        }

        private void ConfigurarPaginacion(int totalRegistros, int paginaActual)
        {
            int paginasTotales = (int)Math.Ceiling((double)totalRegistros / gvAuditoria.PageSize);

            List<ListItem> pages = new List<ListItem>();

            if (paginaActual > 0)
                pages.Add(new ListItem("<<", "1", true));

            if (paginaActual > 0)
                pages.Add(new ListItem("<", (paginaActual).ToString(), true));

            int startIndex = Math.Max(0, paginaActual - 2);
            int endIndex = Math.Min(paginasTotales - 1, paginaActual + 2);

            if (endIndex - startIndex < 4 && paginasTotales > 5)
            {
                if (startIndex <= 2)
                    endIndex = Math.Min(startIndex + 4, paginasTotales - 1);
                else if (endIndex >= paginasTotales - 3)
                    startIndex = Math.Max(endIndex - 4, 0);
            }

            for (int i = startIndex; i <= endIndex; i++)
            {
                bool isActive = i == paginaActual;
                pages.Add(new ListItem((i + 1).ToString(), (i + 1).ToString(), true) { Selected = isActive });
            }

            if (paginaActual < paginasTotales - 1)
                pages.Add(new ListItem(">", (paginaActual + 2).ToString(), true));

            if (paginaActual < paginasTotales - 1)
                pages.Add(new ListItem(">>", paginasTotales.ToString(), true));

            rptPager.DataSource = pages;
            rptPager.DataBind();
        }
    }
}
