using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroTractos : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdmin();

            if (!IsPostBack)
            {
                CargarTractos();
            }
        }

        private void CargarTractos()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT idTracto, placaTracto, marca, modelo, activo FROM Tracto ORDER BY placaTracto";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvTractos.DataSource = dt;
                        gvTractos.DataBind();
                        lblTotalTractos.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los tractos: " + ex.Message);
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

                    string checkQuery = "SELECT COUNT(*) FROM Tracto WHERE UPPER(placaTracto) = @placa";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@placa", placa);
                        int existe = (int)checkCmd.ExecuteScalar();
                        if (existe > 0)
                        {
                            MostrarMensaje("Ya existe un tracto registrado con esa placa.");
                            return;
                        }
                    }

                    string insertQuery = "INSERT INTO Tracto (placaTracto, marca, modelo, activo) VALUES (@placa, @marca, @modelo, 1)";
                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@placa", placa);
                        insertCmd.Parameters.AddWithValue("@marca", marca);
                        insertCmd.Parameters.AddWithValue("@modelo", modelo);
                        insertCmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("INSERT", "Tracto",
                    descripcion: $"Tracto registrado - Placa: {placa}, Marca: {marca}, Modelo: {modelo}");

                txtPlaca.Text = "";
                txtMarca.Text = "";
                txtModelo.Text = "";
                MostrarMensaje("Tracto registrado correctamente.", true);
                CargarTractos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el tracto: " + ex.Message);
            }
        }

        protected void gvTractos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idTracto = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Tracto 
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idTracto = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idTracto);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Tracto", idTracto,
                        "Estado de tracto actualizado (activar/desactivar)");

                    CargarTractos();
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