using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroPlantas : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdmin();

            if (!IsPostBack)
            {
                CargarPlantas();
            }
        }

        private void CargarPlantas()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT idPlanta, nombre, esInternacional, activo FROM Planta ORDER BY esInternacional, nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvPlantas.DataSource = dt;
                        gvPlantas.DataBind();
                        lblTotalPlantas.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar las plantas: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            string nombre = txtNombre.Text.Trim().ToUpper();
            bool esInternacional = ddlAmbito.SelectedValue == "1";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("Debe ingresar el nombre de la planta.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM Planta WHERE UPPER(nombre) = @nombre AND esInternacional = @esInternacional";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@nombre", nombre);
                        checkCmd.Parameters.AddWithValue("@esInternacional", esInternacional);
                        int existe = (int)checkCmd.ExecuteScalar();
                        if (existe > 0)
                        {
                            MostrarMensaje("Ya existe una planta con ese nombre en el mismo ámbito.");
                            return;
                        }
                    }

                    string insertQuery = "INSERT INTO Planta (nombre, esInternacional, activo) VALUES (@nombre, @esInternacional, 1)";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@nombre", nombre);
                        insertCmd.Parameters.AddWithValue("@esInternacional", esInternacional);
                        insertCmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("INSERT", "Planta",
                    descripcion: $"Planta registrada - Nombre: {nombre}, Ámbito: {(esInternacional ? "Internacional" : "Nacional")}");

                txtNombre.Text = "";
                ddlAmbito.SelectedIndex = 0;
                MostrarMensaje("Planta registrada correctamente.", true);
                CargarPlantas();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la planta: " + ex.Message);
            }
        }

        protected void gvPlantas_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idPlanta = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Planta 
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idPlanta = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idPlanta);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Planta", idPlanta,
                        "Estado de planta actualizado (activar/desactivar)");

                    CargarPlantas();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        protected string ObtenerClaseAmbito(object esInternacional)
        {
            bool esInt = esInternacional != null && esInternacional != DBNull.Value && Convert.ToBoolean(esInternacional);
            return esInt ? "badge-info" : "badge-primary";
        }

        protected string ObtenerTextoAmbito(object esInternacional)
        {
            bool esInt = esInternacional != null && esInternacional != DBNull.Value && Convert.ToBoolean(esInternacional);
            return esInt ? "Internacional" : "Nacional";
        }

        protected string ObtenerClaseEstado(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "badge-success" : "badge-secondary";
        }

        protected string ObtenerTextoEstado(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "Activo" : "Inactivo";
        }

        protected string ObtenerTextoBoton(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "Desactivar" : "Activar";
        }

        protected string ObtenerClaseBoton(object activo)
        {
            bool esActivo = activo != null && activo != DBNull.Value && Convert.ToBoolean(activo);
            return esActivo ? "btn btn-warning btn-sm" : "btn btn-success btn-sm";
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
