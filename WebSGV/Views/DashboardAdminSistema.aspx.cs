using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class DashboardAdminSistema : System.Web.UI.Page
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
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();

                string queryUsuarios = @"
                    SELECT
                        COUNT(*) AS total,
                        ISNULL(SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END), 0) AS activos,
                        ISNULL(SUM(CASE WHEN activo = 0 THEN 1 ELSE 0 END), 0) AS inactivos
                    FROM Usuarios";

                using (SqlCommand cmd = new SqlCommand(queryUsuarios, conn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        lblTotalUsuarios.Text    = r["total"].ToString();
                        lblUsuariosActivos.Text  = r["activos"].ToString();
                        lblUsuariosInactivos.Text = r["inactivos"].ToString();
                    }
                }

                try
                {
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(GETDATE() AS DATE)", conn))
                    {
                        object result = cmd.ExecuteScalar();
                        lblEventosHoy.Text = result != DBNull.Value ? result.ToString() : "0";
                    }
                }
                catch
                {
                    lblEventosHoy.Text = "N/A";
                }
            }
        }

        private void CargarRoles()
        {
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT rol, COUNT(*) AS total FROM Usuarios GROUP BY rol ORDER BY total DESC", conn))
                {
                    DataTable dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);

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
            }
        }

        private void CargarAuditoria()
        {
            try
            {
                string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(cs))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT TOP 8 NombreUsuario, Accion, TablaAfectada, FechaHora FROM AuditoriaLog ORDER BY FechaHora DESC", conn))
                    {
                        DataTable dt = new DataTable();
                        new SqlDataAdapter(cmd).Fill(dt);

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
                }
            }
            catch
            {
                pnlSinAuditoria.Visible = true;
            }
        }
    }
}
