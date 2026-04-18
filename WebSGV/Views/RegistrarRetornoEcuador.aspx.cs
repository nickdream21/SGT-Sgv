using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistrarRetornoEcuador : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RolesHelper.TieneSesionActiva())
            {
                Response.Redirect("~/Views/Login.aspx");
                return;
            }

            if (!RolesHelper.EsAdminGrifo() && !RolesHelper.EsAdmin())
            {
                RolesHelper.RedirigirSegunRol();
                return;
            }

            if (!IsPostBack)
            {
                hfIdViaje.Value = Request.QueryString["idViaje"] ?? "";
                hfIdConductor.Value = Request.QueryString["idConductor"] ?? "";
                hfIdTracto.Value = Request.QueryString["idTracto"] ?? "";

                txtFecha.Text = DateTime.Now.ToString("yyyy-MM-ddTHH:mm");

                CargarDatosViaje();
            }
        }

        private void CargarDatosViaje()
        {
            if (string.IsNullOrEmpty(hfIdViaje.Value)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"
                        SELECT TOP 1
                            vp.numeroViajeProgreso AS NumViaje,
                            CONCAT(c.nombre, ' ', c.apPaterno) AS Conductor,
                            ISNULL(t.placaTracto, 'N/A') AS PlacaTracto
                        FROM ViajesEnProgreso vp
                        LEFT JOIN Conductor c ON c.idConductor = @idConductor
                        LEFT JOIN Tracto t ON t.idTracto = @idTracto
                        WHERE vp.idViajeProgreso = @idViaje";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idViaje", Convert.ToInt32(hfIdViaje.Value));
                        cmd.Parameters.AddWithValue("@idConductor", string.IsNullOrEmpty(hfIdConductor.Value) ? 0 : Convert.ToInt32(hfIdConductor.Value));
                        cmd.Parameters.AddWithValue("@idTracto", string.IsNullOrEmpty(hfIdTracto.Value) ? 0 : Convert.ToInt32(hfIdTracto.Value));

                        conn.Open();
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                litNumeroViaje.Text = r["NumViaje"].ToString();
                                litConductor.Text = r["Conductor"].ToString();
                                litTracto.Text = r["PlacaTracto"].ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando datos viaje: {ex.Message}");
            }
        }

        protected void btnGuardar_Click(object sender, EventArgs e)
        {
            string ticketsJson = hfTicketsJson.Value;
            if (string.IsNullOrWhiteSpace(ticketsJson) || ticketsJson == "[]")
            {
                MostrarError("Debe registrar al menos un ticket.");
                return;
            }

            int? idViaje = ParseNullableInt(hfIdViaje.Value);
            int? idConductor = ParseNullableInt(hfIdConductor.Value);
            int? idTracto = ParseNullableInt(hfIdTracto.Value);

            DateTime fecha;
            if (!DateTime.TryParse(txtFecha.Text, out fecha))
                fecha = DateTime.Now;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                using (SqlCommand cmd = new SqlCommand("sp_InsertarIngresoEcuador", conn))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@idViajeProgreso", (object)idViaje ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@idConductor", (object)idConductor ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@idTracto", (object)idTracto ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fechaRecepcion", fecha);
                    cmd.Parameters.AddWithValue("@observaciones",
                        string.IsNullOrWhiteSpace(txtObservaciones.Text) ? (object)DBNull.Value : txtObservaciones.Text.Trim());
                    cmd.Parameters.AddWithValue("@usuarioRegistro",
                        Session["Usuario"]?.ToString() ?? "grifo");
                    cmd.Parameters.AddWithValue("@ticketsJson", ticketsJson);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }

                try { AuditoriaHelper.Registrar("IngresoEcuador", "Registro retorno Ecuador viaje " + litNumeroViaje.Text); }
                catch { /* auditoria no crítica */ }

                Response.Redirect("DashboardGrifo.aspx?msg=retorno_ok", false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                MostrarError("Error al guardar: " + ex.Message);
            }
        }

        private static int? ParseNullableInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            int v;
            return int.TryParse(s, out v) && v > 0 ? (int?)v : null;
        }

        private void MostrarError(string msg)
        {
            pnlError.Visible = true;
            pnlError.Controls.Clear();
            pnlError.Controls.Add(new LiteralControl(System.Net.WebUtility.HtmlEncode(msg)));
        }
    }
}
