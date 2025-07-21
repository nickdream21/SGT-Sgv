using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class RegistroTractos : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Inicializar formulario
                mensajeExito.Visible = false;
                mensajeError.Visible = false;
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                // Ocultar mensajes previos
                mensajeExito.Visible = false;
                mensajeError.Visible = false;

                // Validar el formulario
                if (!Page.IsValid)
                {
                    mostrarError("Por favor, complete los campos requeridos correctamente.");
                    return;
                }

                // Obtener los valores del formulario
                string placa = txtPlaca.Text.Trim();
                string modelo = txtModelo.Text.Trim();
                string marca = txtMarca.Text.Trim();

                // Verificar si ya existe un tracto con esa placa
                if (TractoExiste(placa))
                {
                    mostrarError("Ya existe un tracto registrado con esta placa.");
                    return;
                }

                // Guardar el tracto
                GuardarTracto(placa, modelo, marca);

                // Mostrar mensaje de éxito
                mensajeExito.Visible = true;
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                // Registrar el error y mostrar mensaje
                System.Diagnostics.Debug.WriteLine("Error en btnRegistrar_Click: " + ex.Message);
                mostrarError("Ocurrió un error al registrar el tracto: " + ex.Message);
            }
        }

        private bool TractoExiste(string placa)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = "SELECT COUNT(*) FROM Tracto WHERE placaTracto = @placaTracto";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@placaTracto", placa);
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        private void GuardarTracto(string placa, string modelo, string marca)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = @"INSERT INTO Tracto (placaTracto, modelo, marca)
                           VALUES (@placaTracto, @modelo, @marca)";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Configurar parámetros
                    cmd.Parameters.AddWithValue("@placaTracto", placa);
                    cmd.Parameters.AddWithValue("@modelo", modelo);
                    cmd.Parameters.AddWithValue("@marca", marca);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LimpiarFormulario()
        {
            txtPlaca.Text = string.Empty;
            txtModelo.Text = string.Empty;
            txtMarca.Text = string.Empty;
        }

        private void mostrarError(string mensaje)
        {
            mensajeError.Visible = true;
            litError.Text = mensaje;
        }
    }
}