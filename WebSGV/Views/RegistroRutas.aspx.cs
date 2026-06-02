using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroRutas : PaginaBase
    {
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
                DataTable dt = DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente ORDER BY nombre");

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
                // Verificar si la ruta ya existe para este cliente
                int rutaExistente = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    @"SELECT COUNT(*) FROM Ruta
                      WHERE nombre = @nombre AND idCliente = @idCliente",
                    DbHelper.Param("@nombre", txtNombreRuta.Text.Trim()),
                    DbHelper.Param("@idCliente", Convert.ToInt32(ddlCliente.SelectedValue))));

                if (rutaExistente > 0)
                {
                    MostrarMensaje("Ya existe una ruta con ese nombre para el cliente seleccionado.");
                    return;
                }

                // Insertar la nueva ruta
                DbHelper.EjecutarNonQuery(
                    @"INSERT INTO Ruta (nombre, descripcion, idCliente)
                      VALUES (@nombre, @descripcion, @idCliente)",
                    DbHelper.Param("@nombre", txtNombreRuta.Text.Trim()),
                    DbHelper.Param("@descripcion", txtDescripcion.Text.Trim()),
                    DbHelper.Param("@idCliente", Convert.ToInt32(ddlCliente.SelectedValue)));

                AuditoriaHelper.Registrar("INSERT", "Ruta",
                    descripcion: $"Ruta registrada - Nombre: {txtNombreRuta.Text.Trim()}, Cliente: {ddlCliente.SelectedItem.Text}");

                // Limpiar el formulario
                LimpiarFormulario();

                // Mostrar mensaje de éxito
                MostrarMensaje("Ruta registrada correctamente.", true);
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