using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroProductos : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            SecurityHelper.ExigirRolAdmin();

            if (!IsPostBack)
            {
                // Cargar la lista de clientes en el dropdown
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

                    // Crear el elemento "Seleccione un cliente"
                    ddlCliente.Items.Clear();
                    ddlCliente.Items.Add(new System.Web.UI.WebControls.ListItem("-- Seleccione un cliente --", "0"));

                    SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        int idCliente = reader.GetInt32(0);
                        string nombreCliente = reader.GetString(1);
                        ddlCliente.Items.Add(new System.Web.UI.WebControls.ListItem(nombreCliente, idCliente.ToString()));
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar la lista de clientes: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Error en CargarClientes: " + ex.ToString());
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            // Verificar que se haya seleccionado un cliente
            if (ddlCliente.SelectedValue == "0")
            {
                MostrarMensaje("Debe seleccionar un cliente.");
                return;
            }

            // Verificar que el nombre del producto no esté vacío
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MostrarMensaje("Debe ingresar el nombre del producto.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Verificar si el producto ya existe para este cliente
                    string checkQuery = "SELECT COUNT(*) FROM Producto WHERE nombre = @nombre AND idCliente = @idCliente";
                    SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                    checkCmd.Parameters.AddWithValue("@nombre", txtNombre.Text.Trim());
                    checkCmd.Parameters.AddWithValue("@idCliente", ddlCliente.SelectedValue);

                    conn.Open();
                    int productoExistente = (int)checkCmd.ExecuteScalar();

                    if (productoExistente > 0)
                    {
                        MostrarMensaje("Ya existe un producto con ese nombre para el cliente seleccionado.");
                        return;
                    }

                    // Insertar el nuevo producto asociado al cliente
                    string insertQuery = "INSERT INTO Producto (nombre, idCliente) VALUES (@nombre, @idCliente)";
                    SqlCommand insertCmd = new SqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@nombre", txtNombre.Text.Trim());
                    insertCmd.Parameters.AddWithValue("@idCliente", ddlCliente.SelectedValue);

                    insertCmd.ExecuteNonQuery();

                    AuditoriaHelper.Registrar("INSERT", "Producto",
                        descripcion: $"Producto registrado - Nombre: {txtNombre.Text.Trim()}, Cliente: {ddlCliente.SelectedItem?.Text}");

                    // Limpiar el formulario
                    LimpiarFormulario();

                    // Mostrar mensaje de éxito
                    MostrarMensaje("Producto registrado correctamente para el cliente seleccionado.", true);
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el producto: " + ex.Message);
                // Registrar el error para debugging
                System.Diagnostics.Debug.WriteLine("Error en RegistroProductos: " + ex.ToString());
            }
        }

        private void LimpiarFormulario()
        {
            ddlCliente.SelectedValue = "0";
            txtNombre.Text = "";
            txtDescripcion.Text = "";
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            string script = $"alert('{mensaje}');";

            // Si es un mensaje de éxito, podemos agregar redireccionamiento o acciones adicionales
            if (esExito)
            {
                // Opcional: redirigir a otra página o actualizar la lista de productos
                // script += "window.location = 'ListaProductos.aspx';";
            }

            ScriptManager.RegisterStartupScript(this, GetType(), "alertMessage", script, true);
        }
    }
}