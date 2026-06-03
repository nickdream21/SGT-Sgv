using System;
using System.Data;
using System.Globalization;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardAdminSistema : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                lblFechaHora.Text = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy · HH:mm",
                    new CultureInfo("es-PE"));
                CargarEstadisticas();
                CargarRoles();
                CargarAuditoria();
            }
        }

        private void CargarEstadisticas()
        {
            DataTable dtStats = DbHelper.ConsultarTabla(@"
                SELECT
                    COUNT(*) AS total,
                    ISNULL(SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END), 0) AS activos,
                    ISNULL(SUM(CASE WHEN activo = 0 THEN 1 ELSE 0 END), 0) AS inactivos
                FROM Usuarios");

            if (dtStats.Rows.Count > 0)
            {
                DataRow r = dtStats.Rows[0];
                lblTotalUsuarios.Text     = r["total"].ToString();
                lblUsuariosActivos.Text   = r["activos"].ToString();
                lblUsuariosInactivos.Text = r["inactivos"].ToString();
            }

            try
            {
                object result = DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(GETDATE() AS DATE)");
                lblEventosHoy.Text = result != DBNull.Value ? result.ToString() : "0";
            }
            catch
            {
                lblEventosHoy.Text = "N/A";
            }
        }

        private void CargarRoles()
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT rol, COUNT(*) AS total FROM Usuarios GROUP BY rol ORDER BY total DESC");

            if (dt.Rows.Count > 0)
            {
                rptRoles.DataSource = dt;
                rptRoles.DataBind();
            }
            else
            {
                pnlSinRoles.Visible = true;
            }
        }

        private void CargarAuditoria()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    "SELECT TOP 8 NombreUsuario, Accion, TablaAfectada, FechaHora FROM AuditoriaLog ORDER BY FechaHora DESC");

                if (dt.Rows.Count > 0)
                {
                    rptAuditoria.DataSource = dt;
                    rptAuditoria.DataBind();
                }
                else
                {
                    pnlSinAuditoria.Visible = true;
                }
            }
            catch
            {
                pnlSinAuditoria.Visible = true;
            }
        }
    }
}
