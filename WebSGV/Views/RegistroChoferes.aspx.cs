using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models;

namespace WebSGV.Views
{
    [ScriptService]
    public partial class RegistroChoferes : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                CargarConductores();
            }
        }

        private void CargarConductores()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idConductor, ISNULL(DNI, ISNULL(carnetExtranjeria, '')) AS DNI,
                        (nombre + ' ' + apPaterno + ' ' + apMaterno) AS nombreCompleto,
                        ISNULL(telefono, '') AS telefono, activo
                        FROM Conductor ORDER BY apPaterno, nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvConductores.DataSource = dt;
                        gvConductores.DataBind();
                        lblTotalConductores.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los conductores: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid)
                {
                    MostrarMensaje("Por favor, complete los campos requeridos correctamente.");
                    return;
                }

                string tipoDocumento = ddlTipoDocumento.SelectedValue;
                string numeroDocumento = null;
                string carnetExtranjeria = null;
                string pasaporte = null;

                switch (tipoDocumento)
                {
                    case "DNI":
                        if (string.IsNullOrEmpty(txtDNI.Text))
                        {
                            MostrarMensaje("Debe ingresar el número de DNI.");
                            return;
                        }
                        numeroDocumento = txtDNI.Text.Trim();
                        break;
                    case "Carnet de Extranjería":
                        if (string.IsNullOrEmpty(txtCarnetExtranjeria.Text))
                        {
                            MostrarMensaje("Debe ingresar el número de Carnet de Extranjería.");
                            return;
                        }
                        carnetExtranjeria = txtCarnetExtranjeria.Text.Trim();
                        break;
                    case "Pasaporte":
                        if (string.IsNullOrEmpty(txtPasaporte.Text))
                        {
                            MostrarMensaje("Debe ingresar el número de Pasaporte.");
                            return;
                        }
                        pasaporte = txtPasaporte.Text.Trim();
                        break;
                }

                if (tipoDocumento == "DNI" && ConductorExiste("DNI", numeroDocumento))
                {
                    MostrarMensaje("Ya existe un conductor registrado con este DNI.");
                    return;
                }
                else if (tipoDocumento == "Carnet de Extranjería" && ConductorExiste("carnetExtranjeria", carnetExtranjeria))
                {
                    MostrarMensaje("Ya existe un conductor registrado con este Carnet de Extranjería.");
                    return;
                }

                GuardarConductor(numeroDocumento, txtNombres.Text.Trim(), txtApellidoPaterno.Text.Trim(),
                                txtApellidoMaterno.Text.Trim(), txtTelefono.Text.Trim(), carnetExtranjeria);

                AuditoriaHelper.Registrar("INSERT", "Conductor",
                    descripcion: $"Conductor registrado - Nombre: {txtNombres.Text.Trim()} {txtApellidoPaterno.Text.Trim()}, Doc: {numeroDocumento ?? carnetExtranjeria ?? pasaporte}");

                LimpiarFormulario();
                MostrarMensaje("Conductor registrado correctamente.", true);
                CargarConductores();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error en btnRegistrar_Click: " + ex.Message);
                MostrarMensaje("Ocurrió un error al registrar el conductor: " + ex.Message);
            }
        }

        protected void gvConductores_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idConductor = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Conductor 
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idConductor = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idConductor);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Conductor", idConductor,
                        "Estado de conductor actualizado (activar/desactivar)");

                    CargarConductores();
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

        private bool ConductorExiste(string campo, string valor)
        {
            string query = $"SELECT COUNT(*) FROM Conductor WHERE {campo} = @valor";
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@valor", valor);
                    conn.Open();
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        private void GuardarConductor(string dni, string nombre, string apPaterno, string apMaterno,
                                    string telefono, string carnetExtranjeria)
        {
            string query = @"INSERT INTO Conductor (DNI, nombre, apPaterno, apMaterno, fechaNacimiento,
                            direccion, telefono, correo, carnetExtranjeria, activo)
                            VALUES (@DNI, @nombre, @apPaterno, @apMaterno, @fechaNacimiento,
                            @direccion, @telefono, @correo, @carnetExtranjeria, 1)";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@DNI", string.IsNullOrEmpty(dni) ? DBNull.Value : (object)dni);
                    cmd.Parameters.AddWithValue("@nombre", nombre);
                    cmd.Parameters.AddWithValue("@apPaterno", apPaterno);
                    cmd.Parameters.AddWithValue("@apMaterno", apMaterno);
                    cmd.Parameters.AddWithValue("@fechaNacimiento", DBNull.Value);
                    cmd.Parameters.AddWithValue("@direccion", DBNull.Value);
                    cmd.Parameters.AddWithValue("@telefono", string.IsNullOrEmpty(telefono) ? DBNull.Value : (object)telefono);
                    cmd.Parameters.AddWithValue("@correo", DBNull.Value);
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
            txtTelefono.Text = string.Empty;
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
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
