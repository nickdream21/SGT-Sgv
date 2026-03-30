using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroEquiposMaquinaria : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RolesHelper.EsAdminMaquinaria() && !RolesHelper.EsAdmin())
            {
                Response.Redirect("~/Views/Login.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                CargarEquipos();
            }
        }

        private void CargarEquipos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idEquipo, placa, ISNULL(descripcion,'') AS descripcion, 
                                            ISNULL(tipo,'') AS tipo, ISNULL(marca,'') AS marca, 
                                            ISNULL(modelo,'') AS modelo, anio, activo
                                     FROM EquiposMaquinaria ORDER BY placa";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvEquipos.DataSource = dt;
                        gvEquipos.DataBind();
                        lblTotalEquipos.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los equipos: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid) return;

                string placa = txtPlaca.Text.Trim().ToUpper();
                string tipo = ddlTipo.SelectedValue;
                string descripcion = txtDescripcion.Text.Trim();
                string marca = txtMarca.Text.Trim();
                string modelo = txtModelo.Text.Trim();
                int? anio = null;
                if (!string.IsNullOrEmpty(txtAnio.Text))
                    anio = Convert.ToInt32(txtAnio.Text.Trim());

                if (EquipoExiste(placa))
                {
                    MostrarMensaje("Ya existe un equipo registrado con esta placa.");
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO EquiposMaquinaria (placa, descripcion, tipo, marca, modelo, anio, activo)
                                     VALUES (@placa, @descripcion, @tipo, @marca, @modelo, @anio, 1)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@placa", placa);
                        cmd.Parameters.AddWithValue("@descripcion", string.IsNullOrEmpty(descripcion) ? (object)DBNull.Value : descripcion);
                        cmd.Parameters.AddWithValue("@tipo", string.IsNullOrEmpty(tipo) ? (object)DBNull.Value : tipo);
                        cmd.Parameters.AddWithValue("@marca", string.IsNullOrEmpty(marca) ? (object)DBNull.Value : marca);
                        cmd.Parameters.AddWithValue("@modelo", string.IsNullOrEmpty(modelo) ? (object)DBNull.Value : modelo);
                        cmd.Parameters.AddWithValue("@anio", anio.HasValue ? (object)anio.Value : DBNull.Value);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("INSERT", "EquiposMaquinaria",
                    descripcion: $"Equipo registrado - Placa: {placa}, Tipo: {tipo}");

                LimpiarFormulario();
                MostrarMensaje("Equipo registrado correctamente.", true);
                CargarEquipos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el equipo: " + ex.Message);
            }
        }

        protected void gvEquipos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idEquipo = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE EquiposMaquinaria 
                                         SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                                         WHERE idEquipo = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idEquipo);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "EquiposMaquinaria", idEquipo,
                        "Estado de equipo actualizado (activar/desactivar)");

                    CargarEquipos();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        private bool EquipoExiste(string placa)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT COUNT(*) FROM EquiposMaquinaria WHERE placa = @placa";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@placa", placa);
                    conn.Open();
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        private void LimpiarFormulario()
        {
            txtPlaca.Text = string.Empty;
            ddlTipo.SelectedIndex = 0;
            txtDescripcion.Text = string.Empty;
            txtMarca.Text = string.Empty;
            txtModelo.Text = string.Empty;
            txtAnio.Text = string.Empty;
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
