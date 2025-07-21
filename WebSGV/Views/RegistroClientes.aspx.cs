using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;

namespace WebSGV.Views
{
    public partial class RegistroClientes : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                // Inicialización del formulario si fuera necesario
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            // Validar que el nombre no esté vacío
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MostrarMensaje("El nombre del cliente es obligatorio.");
                return;
            }

            // Validar formato de RUC (11 dígitos) si se ha ingresado
            string ruc = txtRUC.Text.Trim();
            if (!string.IsNullOrWhiteSpace(ruc))
            {
                if (ruc.Length != 11 || !EsNumerico(ruc))
                {
                    MostrarMensaje("El RUC debe contener 11 dígitos numéricos.");
                    return;
                }
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Verificar si el RUC ya existe (solo si se proporcionó un RUC)
                    if (!string.IsNullOrWhiteSpace(ruc))
                    {
                        string checkQuery = "SELECT COUNT(*) FROM Cliente WHERE ruc = @ruc";
                        SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                        checkCmd.Parameters.AddWithValue("@ruc", ruc);

                        int clienteExistente = (int)checkCmd.ExecuteScalar();

                        if (clienteExistente > 0)
                        {
                            MostrarMensaje("Ya existe un cliente registrado con ese RUC.");
                            return;
                        }
                    }

                    // Insertar el nuevo cliente
                    string insertQuery;
                    SqlCommand insertCmd;

                    if (string.IsNullOrWhiteSpace(ruc))
                    {
                        // Si no hay RUC, insertar solo el nombre
                        insertQuery = @"INSERT INTO Cliente (nombre) VALUES (@nombre)";
                        insertCmd = new SqlCommand(insertQuery, conn);
                    }
                    else
                    {
                        // Si hay RUC, insertar RUC y nombre
                        insertQuery = @"INSERT INTO Cliente (ruc, nombre) VALUES (@ruc, @nombre)";
                        insertCmd = new SqlCommand(insertQuery, conn);
                        insertCmd.Parameters.AddWithValue("@ruc", ruc);
                    }

                    insertCmd.Parameters.AddWithValue("@nombre", txtNombre.Text.Trim());
                    insertCmd.ExecuteNonQuery();

                    // Limpiar el formulario
                    LimpiarFormulario();

                    // Mostrar mensaje de éxito
                    MostrarMensaje("Cliente registrado correctamente.", true);
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el cliente: " + ex.Message);
                // Registrar el error para debugging
                System.Diagnostics.Debug.WriteLine("Error en RegistroClientes: " + ex.ToString());
            }
        }

        private bool EsNumerico(string texto)
        {
            foreach (char c in texto)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }

        private void LimpiarFormulario()
        {
            txtRUC.Text = "";
            txtNombre.Text = "";
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            string script = $"alert('{mensaje}');";

            // Si es un mensaje de éxito, podemos agregar redireccionamiento o acciones adicionales
            if (esExito)
            {
                // Opcional: redirigir a otra página o refrescar para seguir agregando clientes
                // script += "window.location = 'RegistroClientes.aspx';";
            }

            ScriptManager.RegisterStartupScript(this, GetType(), "alertMessage", script, true);
        }
    }
}