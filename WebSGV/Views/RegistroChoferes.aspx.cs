using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using WebSGV.Models;

namespace WebSGV.Views
{
    [ScriptService]
    public partial class RegistroChoferes : System.Web.UI.Page
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

                // Determinar tipo de documento y valor
                string tipoDocumento = ddlTipoDocumento.SelectedValue;
                string numeroDocumento = null;
                string carnetExtranjeria = null;
                string pasaporte = null;

                switch (tipoDocumento)
                {
                    case "DNI":
                        if (string.IsNullOrEmpty(txtDNI.Text))
                        {
                            mostrarError("Debe ingresar el número de DNI.");
                            return;
                        }
                        numeroDocumento = txtDNI.Text.Trim();
                        break;
                    case "Carnet de Extranjería":
                        if (string.IsNullOrEmpty(txtCarnetExtranjeria.Text))
                        {
                            mostrarError("Debe ingresar el número de Carnet de Extranjería.");
                            return;
                        }
                        carnetExtranjeria = txtCarnetExtranjeria.Text.Trim();
                        break;
                    case "Pasaporte":
                        if (string.IsNullOrEmpty(txtPasaporte.Text))
                        {
                            mostrarError("Debe ingresar el número de Pasaporte.");
                            return;
                        }
                        pasaporte = txtPasaporte.Text.Trim();
                        break;
                }

                // Verificar si ya existe un conductor con ese documento
                if (tipoDocumento == "DNI" && ConductorExiste("DNI", numeroDocumento))
                {
                    mostrarError("Ya existe un conductor registrado con este DNI.");
                    return;
                }
                else if (tipoDocumento == "Carnet de Extranjería" && ConductorExiste("carnetExtranjeria", carnetExtranjeria))
                {
                    mostrarError("Ya existe un conductor registrado con este Carnet de Extranjería.");
                    return;
                }

                // Guardar el conductor
                GuardarConductor(numeroDocumento, txtNombres.Text.Trim(), txtApellidoPaterno.Text.Trim(),
                                txtApellidoMaterno.Text.Trim(), txtFechaNacimiento.Text,
                                txtDireccion.Text.Trim(), txtTelefono.Text.Trim(),
                                txtCorreo.Text.Trim(), carnetExtranjeria);

                // Mostrar mensaje de éxito
                mensajeExito.Visible = true;
                LimpiarFormulario();
            }
            catch (Exception ex)
            {
                // Registrar el error y mostrar mensaje
                System.Diagnostics.Debug.WriteLine("Error en btnRegistrar_Click: " + ex.Message);
                mostrarError("Ocurrió un error al registrar el conductor: " + ex.Message);
            }
        }

        private bool ConductorExiste(string campo, string valor)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = $"SELECT COUNT(*) FROM Conductor WHERE {campo} = @valor";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@valor", valor);
                    conn.Open();
                    int count = (int)cmd.ExecuteScalar();
                    return count > 0;
                }
            }
        }

        private void GuardarConductor(string dni, string nombre, string apPaterno, string apMaterno,
                                    string fechaNacimiento, string direccion, string telefono,
                                    string correo, string carnetExtranjeria)
        {
            string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string query = @"INSERT INTO Conductor (DNI, nombre, apPaterno, apMaterno, fechaNacimiento, 
                            direccion, telefono, correo, carnetExtranjeria)
                            VALUES (@DNI, @nombre, @apPaterno, @apMaterno, @fechaNacimiento,
                            @direccion, @telefono, @correo, @carnetExtranjeria)";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    // Configurar parámetros
                    cmd.Parameters.AddWithValue("@DNI", string.IsNullOrEmpty(dni) ? DBNull.Value : (object)dni);
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    cmd.Parameters.AddWithValue("@apPaterno", apPaterno);
                    cmd.Parameters.AddWithValue("@apMaterno", apMaterno);
                    cmd.Parameters.AddWithValue("@fechaNacimiento", DateTime.Parse(fechaNacimiento));
                    cmd.Parameters.AddWithValue("@direccion", string.IsNullOrEmpty(direccion) ? DBNull.Value : (object)direccion);
                    cmd.Parameters.AddWithValue("@telefono", string.IsNullOrEmpty(telefono) ? DBNull.Value : (object)telefono);
                    cmd.Parameters.AddWithValue("@correo", string.IsNullOrEmpty(correo) ? DBNull.Value : (object)correo);
                    cmd.Parameters.AddWithValue("@carnetExtranjeria", string.IsNullOrEmpty(carnetExtranjeria) ? DBNull.Value : (object)carnetExtranjeria);

                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private void LimpiarFormulario()
        {
            txtDNI.Text = string.Empty;
            txtCarnetExtranjeria.Text = string.Empty;
            txtPasaporte.Text = string.Empty;
            txtNombres.Text = string.Empty;
            txtApellidoPaterno.Text = string.Empty;
            txtApellidoMaterno.Text = string.Empty;
            txtFechaNacimiento.Text = string.Empty;
            txtDireccion.Text = string.Empty;
            txtTelefono.Text = string.Empty;
            txtCorreo.Text = string.Empty;
        }

        private void mostrarError(string mensaje)
        {
            mensajeError.Visible = true;
            litError.Text = mensaje;
        }

        [WebMethod]
        public static DniResponse BuscarPorDNI(string numero)
        {
            try
            {
                return DniService.ConsultarPorDNI(numero);
            }
            catch (Exception ex)
            {
                return new DniResponse
                {
                    error = true,
                    message = "Error interno: " + ex.Message
                };
            }
        }
    }
}