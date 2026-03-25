using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class Auditoria : System.Web.UI.Page
    {
        private string ConnectionString
        {
            get { return ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            // Verificar acceso: solo ADMINISTRADOR DE SISTEMA
            if (!RolesHelper.EsAdminSistema())
            {
                Response.Redirect("~/Views/Inicio.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                // Fechas por defecto: último mes
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
                string query = @"
                    SELECT DISTINCT TablaAfectada 
                    FROM AuditoriaLog 
                    ORDER BY TablaAfectada";

                DataTable dt = EjecutarConsulta(query);

                ddlTabla.Items.Clear();
                ddlTabla.Items.Add(new ListItem("-- Todas --", ""));

                foreach (DataRow row in dt.Rows)
                {
                    ddlTabla.Items.Add(new ListItem(row["TablaAfectada"].ToString()));
                }
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
                string query = @"
                    SELECT 
                        (SELECT COUNT(*) FROM AuditoriaLog) AS TotalRegistros,
                        (SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(GETDATE() AS DATE)) AS RegistrosHoy,
                        (SELECT COUNT(DISTINCT NombreUsuario) FROM AuditoriaLog WHERE FechaHora >= DATEADD(DAY, -7, GETDATE())) AS UsuariosActivos,
                        (SELECT COUNT(DISTINCT TablaAfectada) FROM AuditoriaLog) AS TablasAfectadas";

                DataTable dt = EjecutarConsulta(query);

                if (dt.Rows.Count > 0)
                {
                    lblTotalRegistros.Text = dt.Rows[0]["TotalRegistros"].ToString();
                    lblRegistrosHoy.Text = dt.Rows[0]["RegistrosHoy"].ToString();
                    lblUsuariosActivos.Text = dt.Rows[0]["UsuariosActivos"].ToString();
                    lblTablasAfectadas.Text = dt.Rows[0]["TablasAfectadas"].ToString();
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
                StringBuilder sb = new StringBuilder();
                sb.Append(@"
                    SELECT IdAuditoria, FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, 
                           TablaAfectada, IdRegistroAfectado, Descripcion, 
                           ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador
                    FROM AuditoriaLog
                    WHERE 1=1");

                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;

                        // Filtro fecha desde
                        if (!string.IsNullOrEmpty(txtFechaDesde.Text))
                        {
                            sb.Append(" AND FechaHora >= @FechaDesde");
                            cmd.Parameters.AddWithValue("@FechaDesde", DateTime.Parse(txtFechaDesde.Text));
                        }

                        // Filtro fecha hasta
                        if (!string.IsNullOrEmpty(txtFechaHasta.Text))
                        {
                            sb.Append(" AND FechaHora < DATEADD(DAY, 1, @FechaHasta)");
                            cmd.Parameters.AddWithValue("@FechaHasta", DateTime.Parse(txtFechaHasta.Text));
                        }

                        // Filtro acción
                        if (!string.IsNullOrEmpty(ddlAccion.SelectedValue))
                        {
                            sb.Append(" AND Accion = @Accion");
                            cmd.Parameters.AddWithValue("@Accion", ddlAccion.SelectedValue);
                        }

                        // Filtro tabla
                        if (!string.IsNullOrEmpty(ddlTabla.SelectedValue))
                        {
                            sb.Append(" AND TablaAfectada = @Tabla");
                            cmd.Parameters.AddWithValue("@Tabla", ddlTabla.SelectedValue);
                        }

                        // Filtro usuario
                        if (!string.IsNullOrEmpty(txtUsuario.Text.Trim()))
                        {
                            sb.Append(" AND NombreUsuario LIKE @Usuario");
                            cmd.Parameters.AddWithValue("@Usuario", "%" + txtUsuario.Text.Trim() + "%");
                        }

                        sb.Append(" ORDER BY FechaHora DESC");

                        cmd.CommandText = sb.ToString();
                        conn.Open();

                        SqlDataAdapter da = new SqlDataAdapter(cmd);
                        DataTable dt = new DataTable();
                        da.Fill(dt);

                        gvAuditoria.DataSource = dt;
                        gvAuditoria.DataBind();

                        lblCantidadResultados.Text = dt.Rows.Count + " registros";
                    }
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
                // Temporalmente desactivar paginación para exportar todo
                gvAuditoria.AllowPaging = false;
                CargarDatos();

                Response.Clear();
                Response.Buffer = true;
                Response.AddHeader("content-disposition", $"attachment;filename=Auditoria_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
                Response.Charset = "";
                Response.ContentType = "application/vnd.ms-excel";
                Response.ContentEncoding = Encoding.UTF8;
                Response.Write("\uFEFF"); // BOM para UTF-8

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

        private DataTable EjecutarConsulta(string query)
        {
            DataTable dt = new DataTable();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt);
                }
            }
            return dt;
        }

        #endregion
    }
}
