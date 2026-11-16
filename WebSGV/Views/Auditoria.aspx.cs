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
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                txtFechaDesde.Text = FechaHelper.Ahora().AddDays(-30).ToString("yyyy-MM-dd");
                txtFechaHasta.Text = FechaHelper.Ahora().ToString("yyyy-MM-dd");

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
                        (SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(dbo.fn_AhoraPeru() AS DATE)) AS RegistrosHoy,
                        (SELECT COUNT(DISTINCT NombreUsuario) FROM AuditoriaLog WHERE FechaHora >= DATEADD(DAY, -7, dbo.fn_AhoraPeru())) AS UsuariosActivos,
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

        /// <summary>Máximo de registros que se cargan para la grilla.</summary>
        internal const int MaximoEnPantalla = 1000;
        /// <summary>Máximo de registros que se exportan a Excel.</summary>
        internal const int MaximoAlExportar = 10000;

        /// <summary>
        /// Consulta de AuditoriaLog con los filtros dados, limitada a los <paramref name="maximo"/> registros
        /// más recientes (TOP (@max), FechaHora descendente). Agrega los parámetros a <paramref name="parametros"/>.
        /// </summary>
        internal static string ConstruirConsulta(DateTime? fechaDesde, DateTime? fechaHasta, string accion,
            string tabla, string usuario, int maximo, List<SqlParameter> parametros)
        {
            StringBuilder sb = new StringBuilder(@"
                    SELECT TOP (@max) IdAuditoria, FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion,
                           TablaAfectada, IdRegistroAfectado, Descripcion,
                           ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador
                    FROM AuditoriaLog
                    WHERE 1=1");
            parametros.Add(DbHelper.Param("@max", maximo));

            if (fechaDesde.HasValue)
            {
                sb.Append(" AND FechaHora >= @FechaDesde");
                parametros.Add(DbHelper.Param("@FechaDesde", fechaDesde.Value));
            }

            if (fechaHasta.HasValue)
            {
                sb.Append(" AND FechaHora < DATEADD(DAY, 1, @FechaHasta)");
                parametros.Add(DbHelper.Param("@FechaHasta", fechaHasta.Value));
            }

            if (!string.IsNullOrEmpty(accion))
            {
                sb.Append(" AND Accion = @Accion");
                parametros.Add(DbHelper.Param("@Accion", accion));
            }

            if (!string.IsNullOrEmpty(tabla))
            {
                sb.Append(" AND TablaAfectada = @Tabla");
                parametros.Add(DbHelper.Param("@Tabla", tabla));
            }

            if (!string.IsNullOrEmpty(usuario))
            {
                sb.Append(" AND NombreUsuario LIKE @Usuario");
                parametros.Add(DbHelper.Param("@Usuario", "%" + usuario + "%"));
            }

            sb.Append(" ORDER BY FechaHora DESC, IdAuditoria DESC");
            return sb.ToString();
        }

        /// <summary>Aviso cuando la consulta llegó al tope; null si no llegó.</summary>
        internal static string AvisoTope(int filas, int maximo) =>
            filas >= maximo ? $"Se muestran los {maximo} registros más recientes; acote las fechas." : null;

        private void CargarDatos(int maximo = MaximoEnPantalla)
        {
            lblAvisoTope.Visible = false;
            try
            {
                var parametros = new List<SqlParameter>();
                string sql = ConstruirConsulta(
                    string.IsNullOrEmpty(txtFechaDesde.Text) ? (DateTime?)null : DateTime.Parse(txtFechaDesde.Text),
                    string.IsNullOrEmpty(txtFechaHasta.Text) ? (DateTime?)null : DateTime.Parse(txtFechaHasta.Text),
                    ddlAccion.SelectedValue, ddlTabla.SelectedValue, txtUsuario.Text.Trim(), maximo, parametros);

                DataTable dt = DbHelper.ConsultarTabla(sql, parametros.ToArray());
                gvAuditoria.DataSource = dt;
                gvAuditoria.DataBind();
                lblCantidadResultados.Text = dt.Rows.Count + " registros";

                string aviso = AvisoTope(dt.Rows.Count, maximo);
                if (aviso != null)
                {
                    lblAvisoTope.Text = MensajeHtml.Texto(aviso);
                    lblAvisoTope.Visible = true;
                }
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
                CargarDatos(MaximoAlExportar);

                Response.Clear();
                Response.Buffer = true;
                Response.AddHeader("content-disposition", $"attachment;filename=Auditoria_{FechaHelper.Ahora():yyyyMMdd_HHmmss}.xls");
                Response.Charset = "";
                Response.ContentType = "application/vnd.ms-excel";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Write("﻿");

                // Sin esto, un rango con más filas que el tope se exportaría cortado sin avisar.
                string avisoTope = AvisoTope(gvAuditoria.Rows.Count, MaximoAlExportar);
                if (avisoTope != null)
                    Response.Write("<p><b>" + System.Web.HttpUtility.HtmlEncode(avisoTope) + "</b></p>");

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
