using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroRutas : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdmin();

            if (!IsPostBack)
            {
                CargarClientes();
            }
        }

        private void CargarClientes()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT idCliente, nombre FROM Cliente ORDER BY nombre";
                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    adapter.Fill(dt);

                    // Agregar un item predeterminado
                    DataRow dr = dt.NewRow();
                    dr["idCliente"] = 0;
                    dr["nombre"] = "-- Seleccione un Cliente --";
                    dt.Rows.InsertAt(dr, 0);

                    ddlCliente.DataSource = dt;
                    ddlCliente.DataTextField = "nombre";
                    ddlCliente.DataValueField = "idCliente";
                    ddlCliente.DataBind();
                }
            }
            catch (Exception ex)
            {
                // Manejar el error apropiadamente
                System.Diagnostics.Debug.WriteLine("Error al cargar clientes: " + ex.Message);
                MostrarMensaje("Error al cargar la lista de clientes. Por favor, intente nuevamente.");
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            // Validar campos obligatorios
            if (string.IsNullOrWhiteSpace(txtNombreRuta.Text))
            {
                MostrarMensaje("Debe ingresar el nombre de la ruta.");
                return;
            }

            // Verificar que se haya seleccionado un cliente válido
            if (ddlCliente.SelectedIndex == 0)
            {
                MostrarMensaje("Por favor, seleccione un cliente válido.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Verificar si la ruta ya existe para este cliente
                    string checkQuery = @"SELECT COUNT(*) FROM Ruta 
                                         WHERE nombre = @nombre AND idCliente = @idCliente";
                    SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                    checkCmd.Parameters.AddWithValue("@nombre", txtNombreRuta.Text.Trim());
                    checkCmd.Parameters.AddWithValue("@idCliente", Convert.ToInt32(ddlCliente.SelectedValue));

                    int rutaExistente = (int)checkCmd.ExecuteScalar();

                    if (rutaExistente > 0)
                    {
                        MostrarMensaje("Ya existe una ruta con ese nombre para el cliente seleccionado.");
                        return;
                    }

                    // Insertar la nueva ruta
                    string insertQuery = @"INSERT INTO Ruta (nombre, descripcion, idCliente) 
                                          VALUES (@nombre, @descripcion, @idCliente)";

                    SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@nombre", txtNombreRuta.Text.Trim());
                    insertCmd.Parameters.AddWithValue("@descripcion", txtDescripcion.Text.Trim());
                    insertCmd.Parameters.AddWithValue("@idCliente", Convert.ToInt32(ddlCliente.SelectedValue));

                    insertCmd.ExecuteNonQuery();

                    AuditoriaHelper.Registrar("INSERT", "Ruta",
                        descripcion: $"Ruta registrada - Nombre: {txtNombreRuta.Text.Trim()}, Cliente: {ddlCliente.SelectedItem.Text}");

                    // Limpiar el formulario
                    LimpiarFormulario();

                    // Mostrar mensaje de éxito
                    MostrarMensaje("Ruta registrada correctamente.", true);
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la ruta: " + ex.Message);
                // Registrar el error para debugging
                System.Diagnostics.Debug.WriteLine("Error en RegistroRutas: " + ex.ToString());
            }
        }

        private void LimpiarFormulario()
        {
            txtNombreRuta.Text = "";
            txtDescripcion.Text = "";
            ddlCliente.SelectedIndex = 0;
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            string script = $"alert('{mensaje}');";

            // Si es un mensaje de éxito, podemos agregar redireccionamiento o acciones adicionales
            if (esExito)
            {
                // Opcional: redirigir a otra página o actualizar la lista de rutas
                // script += "window.location = 'ListaRutas.aspx';";
            }

            ScriptManager.RegisterStartupScript(this, GetType(), "alertMessage", script, true);
        }
    }
}