using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroProductos : PaginaBase
    {
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
                DataTable dt = DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente ORDER BY nombre");

                // Crear el elemento "Seleccione un cliente"
                ddlCliente.Items.Clear();
                ddlCliente.Items.Add(new System.Web.UI.WebControls.ListItem("-- Seleccione un cliente --", "0"));

                foreach (DataRow row in dt.Rows)
                {
                    ddlCliente.Items.Add(new System.Web.UI.WebControls.ListItem(
                        row["nombre"].ToString(), row["idCliente"].ToString()));
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
                // Verificar si el producto ya existe para este cliente
                int productoExistente = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Producto WHERE nombre = @nombre AND idCliente = @idCliente",
                    DbHelper.Param("@nombre", txtNombre.Text.Trim()),
                    DbHelper.Param("@idCliente", ddlCliente.SelectedValue)));

                if (productoExistente > 0)
                {
                    MostrarMensaje("Ya existe un producto con ese nombre para el cliente seleccionado.");
                    return;
                }

                // Insertar el nuevo producto asociado al cliente
                DbHelper.EjecutarNonQuery(
                    "INSERT INTO Producto (nombre, idCliente) VALUES (@nombre, @idCliente)",
                    DbHelper.Param("@nombre", txtNombre.Text.Trim()),
                    DbHelper.Param("@idCliente", ddlCliente.SelectedValue));

                AuditoriaHelper.Registrar("INSERT", "Producto",
                    descripcion: $"Producto registrado - Nombre: {txtNombre.Text.Trim()}, Cliente: {ddlCliente.SelectedItem?.Text}");

                // Limpiar el formulario
                LimpiarFormulario();

                // Mostrar mensaje de éxito
                MostrarMensaje("Producto registrado correctamente para el cliente seleccionado.", true);
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