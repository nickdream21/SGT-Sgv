using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroSemiremolques : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdmin();

            if (!IsPostBack)
            {
                CargarSemiremolques();
            }
        }

        private void CargarSemiremolques()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT idCarreta, placaCarreta, marca, modelo, activo FROM Carreta ORDER BY placaCarreta";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvSemiremolques.DataSource = dt;
                        gvSemiremolques.DataBind();
                        lblTotalSemiremolques.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los semiremolques: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            string placa = txtPlaca.Text.Trim().ToUpper();
            string marca = txtMarca.Text.Trim().ToUpper();
            string modelo = txtModelo.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(placa) || string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            {
                MostrarMensaje("Debe completar todos los campos requeridos.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM Carreta WHERE UPPER(placaCarreta) = @placa";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@placa", placa);
                        int existe = (int)checkCmd.ExecuteScalar();
                        if (existe > 0)
                        {
                            MostrarMensaje("Ya existe un semiremolque registrado con esa placa.");
                            return;
                        }
                    }

                    string insertQuery = "INSERT INTO Carreta (placaCarreta, marca, modelo, activo) VALUES (@placa, @marca, @modelo, 1)";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@placa", placa);
                        insertCmd.Parameters.AddWithValue("@marca", marca);
                        insertCmd.Parameters.AddWithValue("@modelo", modelo);
                        insertCmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("INSERT", "Carreta",
                    descripcion: $"Semiremolque registrado - Placa: {placa}, Marca: {marca}, Modelo: {modelo}");

                txtPlaca.Text = "";
                txtMarca.Text = "";
                txtModelo.Text = "";
                MostrarMensaje("Semiremolque registrado correctamente.", true);
                CargarSemiremolques();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el semiremolque: " + ex.Message);
            }
        }

        protected void gvSemiremolques_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idCarreta = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Carreta 
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idCarreta = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idCarreta);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Carreta", idCarreta,
                        "Estado de semiremolque actualizado (activar/desactivar)");

                    CargarSemiremolques();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
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