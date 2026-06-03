using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class Auditoria : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RolesHelper.EsAdminSistema())
            {
                Response.Redirect("~/Views/Inicio.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                txtFechaDesde.Text = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                txtFechaHasta.Text = DateTime.Now.ToString("yyyy-MM-dd");

                CargarFiltroTablas();
                CargarEstadisticas();
                CargarDatos();
            }
        }

        #region Carga de Datos

        private void CargarFiltroTablas()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT DISTINCT TablaAfectada FROM AuditoriaLog ORDER BY TablaAfectada");

                ddlTabla.Items.Clear();
                ddlTabla.Items.Add(new ListItem("-- Todas --", ""));

                foreach (DataRow row in dt.Rows)
                    ddlTabla.Items.Add(new ListItem(row["TablaAfectada"].ToString()));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando filtro tablas: {ex.Message}");
            }
        }

        private void CargarEstadisticas()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(@"
                    SELECT
                        (SELECT COUNT(*) FROM AuditoriaLog) AS TotalRegistros,
                        (SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(GETDATE() AS DATE)) AS RegistrosHoy,
                        (SELECT COUNT(DISTINCT NombreUsuario) FROM AuditoriaLog WHERE FechaHora >= DATEADD(DAY, -7, GETDATE())) AS UsuariosActivos,
                        (SELECT COUNT(DISTINCT TablaAfectada) FROM AuditoriaLog) AS TablasAfectadas");

                if (dt.Rows.Count > 0)
                {
                    lblTotalRegistros.Text    = dt.Rows[0]["TotalRegistros"].ToString();
                    lblRegistrosHoy.Text      = dt.Rows[0]["RegistrosHoy"].ToString();
                    lblUsuariosActivos.Text   = dt.Rows[0]["UsuariosActivos"].ToString();
                    lblTablasAfectadas.Text   = dt.Rows[0]["TablasAfectadas"].ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando estadísticas: {ex.Message}");
            }
        }

        private void CargarDatos()
        {
            try
            {
                var parametros = new List<SqlParameter>();
                StringBuilder sb = new StringBuilder(@"
                    SELECT IdAuditoria, FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion,
                           TablaAfectada, IdRegistroAfectado, Descripcion,
                           ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador
                    FROM AuditoriaLog
                    WHERE 1=1");

                if (!string.IsNullOrEmpty(txtFechaDesde.Text))
                {
                    sb.Append(" AND FechaHora >= @FechaDesde");
                    parametros.Add(DbHelper.Param("@FechaDesde", DateTime.Parse(txtFechaDesde.Text)));
                }

                if (!string.IsNullOrEmpty(txtFechaHasta.Text))
                {
                    sb.Append(" AND FechaHora < DATEADD(DAY, 1, @FechaHasta)");
                    parametros.Add(DbHelper.Param("@FechaHasta", DateTime.Parse(txtFechaHasta.Text)));
                }

                if (!string.IsNullOrEmpty(ddlAccion.SelectedValue))
                {
                    sb.Append(" AND Accion = @Accion");
                    parametros.Add(DbHelper.Param("@Accion", ddlAccion.SelectedValue));
                }

                if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
                {
                    sb.Append(" AND TablaAfectada = @Tabla");
                    parametros.Add(DbHelper.Param("@Tabla", ddlTabla.SelectedValue));
                }

                if (!string.IsNullOrEmpty(txtUsuario.Text.Trim()))
                {
                    sb.Append(" AND NombreUsuario LIKE @Usuario");
                    parametros.Add(DbHelper.Param("@Usuario", "%" + txtUsuario.Text.Trim() + "%"));
                }

                sb.Append(" ORDER BY FechaHora DESC");

                DataTable dt = DbHelper.ConsultarTabla(sb.ToString(), parametros.ToArray());
                gvAuditoria.DataSource = dt;
                gvAuditoria.DataBind();
                lblCantidadResultados.Text = dt.Rows.Count + " registros";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando datos auditoría: {ex.Message}");
                lblCantidadResultados.Text = "Error al cargar datos";
            }
        }

        #endregion

        #region Eventos

        protected void btnBuscar_Click(object sender, EventArgs e)
        {
            gvAuditoria.PageIndex = 0;
            CargarEstadisticas();
            CargarDatos();
        }

        protected void gvAuditoria_PageIndexChanging(object sender, GridViewPageEventArgs e)
        {
            gvAuditoria.PageIndex = e.NewPageIndex;
            CargarDatos();
        }

        protected void btnExportarExcel_Click(object sender, EventArgs e)
        {
            try
            {
                gvAuditoria.AllowPaging = false;
                CargarDatos();

                Response.Clear();
                Response.Buffer = true;
                Response.AddHeader("content-disposition", $"attachment;filename=Auditoria_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
                Response.Charset = "";
                Response.ContentType = "application/vnd.ms-excel";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Write("﻿");

                using (StringWriter sw = new StringWriter())
                {
                    using (HtmlTextWriter htw = new HtmlTextWriter(sw))
                    {
                        gvAuditoria.RenderControl(htw);
                        Response.Write(sw.ToString());
                    }
                }

                Response.Flush();
                Response.End();
            }
            catch (System.Threading.ThreadAbortException)
            {
                // Normal en Response.End()
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exportando: {ex.Message}");
            }
            finally
            {
                gvAuditoria.AllowPaging = true;
                CargarDatos();
            }
        }

        public override void VerifyRenderingInServerForm(Control control)
        {
            // Requerido para exportar GridView a Excel
        }

        #endregion

        #region Helpers

        protected string ObtenerClaseAccion(object accion)
        {
            if (accion == null) return "default";
            switch (accion.ToString().ToUpper())
            {
                case "INSERT": return "insert";
                case "UPDATE": return "update";
                case "DELETE": return "delete";
                case "LOGIN": return "login";
                case "LOGIN_FALLIDO": return "login_fallido";
                case "LOGOUT": return "logout";
                case "APROBAR": return "aprobar";
                case "RECHAZAR": return "rechazar";
                case "LIQUIDAR": return "liquidar";
                case "RETIRAR": return "retirar";
                default: return "default";
            }
        }

        protected string ObtenerIconoAccion(object accion)
        {
            if (accion == null) return "fas fa-circle";
            switch (accion.ToString().ToUpper())
            {
                case "INSERT": return "fas fa-plus";
                case "UPDATE": return "fas fa-edit";
                case "DELETE": return "fas fa-trash";
                case "LOGIN": return "fas fa-sign-in-alt";
                case "LOGIN_FALLIDO": return "fas fa-exclamation-triangle";
                case "LOGOUT": return "fas fa-sign-out-alt";
                case "APROBAR": return "fas fa-check";
                case "RECHAZAR": return "fas fa-times";
                case "LIQUIDAR": return "fas fa-calculator";
                case "RETIRAR": return "fas fa-undo";
                default: return "fas fa-circle";
            }
        }

        protected string TruncateText(object text, int maxLength)
        {
            if (text == null) return "";
            string str = text.ToString();
            if (str.Length <= maxLength) return str;
            return str.Substring(0, maxLength) + "...";
        }

        #endregion
    }
}
