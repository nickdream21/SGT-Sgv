using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
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
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

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
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del semiremolque.");
                return;
            }

            string placa = txtPlaca.Text.Trim().ToUpper();
            string marca = txtMarca.Text.Trim().ToUpper();
            string modelo = txtModelo.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(placa) || string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            {
                MostrarMensaje("Debe completar todos los campos requeridos.");
                return;
            }

            if (!Regex.IsMatch(placa, "^[A-Z0-9-]{6,10}$")) { MostrarMensaje("Placa inválida: use 6 a 10 caracteres (letras, números o guion)."); return; }
            if (!Regex.IsMatch(marca, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-.]{2,100}$")) { MostrarMensaje("Marca inválida: use entre 2 y 100 caracteres válidos."); return; }
            if (!Regex.IsMatch(modelo, "^[A-ZÁÉÍÓÚÑ0-9\\s\\-.]{2,100}$")) { MostrarMensaje("Modelo inválido: use entre 2 y 100 caracteres válidos."); return; }

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

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarSemiremolque_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdCarreta.Value, out int idCarreta) || idCarreta <= 0)
            {
                MostrarMensaje("ID de semiremolque inválido.");
                return;
            }

            string placa = txtEditarPlaca.Text.Trim().ToUpper();
            string marca = txtEditarMarca.Text.Trim().ToUpper();
            string modelo = txtEditarModelo.Text.Trim().ToUpper();

            if (string.IsNullOrWhiteSpace(placa) || string.IsNullOrWhiteSpace(marca) || string.IsNullOrWhiteSpace(modelo))
            {
                MostrarMensaje("Debe completar todos los campos del semiremolque.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string check = "SELECT COUNT(*) FROM Carreta WHERE UPPER(placaCarreta)=@placa AND idCarreta<>@id";
                    using (SqlCommand cmd = new SqlCommand(check, conn))
                    {
                        cmd.Parameters.AddWithValue("@placa", placa);
                        cmd.Parameters.AddWithValue("@id", idCarreta);
                        if ((int)cmd.ExecuteScalar() > 0)
                        {
                            MostrarMensaje("Ya existe otro semiremolque con esa placa.");
                            return;
                        }
                    }
                    string update = "UPDATE Carreta SET placaCarreta=@placa, marca=@marca, modelo=@modelo WHERE idCarreta=@id";
                    using (SqlCommand cmd = new SqlCommand(update, conn))
                    {
                        cmd.Parameters.AddWithValue("@placa", placa);
                        cmd.Parameters.AddWithValue("@marca", marca);
                        cmd.Parameters.AddWithValue("@modelo", modelo);
                        cmd.Parameters.AddWithValue("@id", idCarreta);
                        cmd.ExecuteNonQuery();
                    }
                }
                AuditoriaHelper.Registrar("UPDATE", "Carreta", idCarreta,
                    $"Semiremolque editado — Placa:{placa}, Marca:{marca}, Modelo:{modelo}");
                hfIdCarreta.Value = "";
                MostrarMensaje("Semiremolque actualizado correctamente.", true);
                CargarSemiremolques();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el semiremolque: " + ex.Message);
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
