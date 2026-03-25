using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroClientes : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
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
                    string query = "SELECT idCliente, ISNULL(ruc, '') AS ruc, nombre, activo FROM Cliente ORDER BY nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvClientes.DataSource = dt;
                        gvClientes.DataBind();
                        lblTotalClientes.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los clientes: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNombre.Text))
            {
                MostrarMensaje("El nombre del cliente es obligatorio.");
                return;
            }

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

                    if (!string.IsNullOrWhiteSpace(ruc))
                    {
                        string checkQuery = "SELECT COUNT(*) FROM Cliente WHERE ruc = @ruc";
                        using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@ruc", ruc);
                            if ((int)checkCmd.ExecuteScalar() > 0)
                            {
                                MostrarMensaje("Ya existe un cliente registrado con ese RUC.");
                                return;
                            }
                        }
                    }

                    string insertQuery;
                    SqlCommand insertCmd;

                    if (string.IsNullOrWhiteSpace(ruc))
                    {
                        insertQuery = "INSERT INTO Cliente (nombre, activo) VALUES (@nombre, 1)";
                        insertCmd = new SqlCommand(insertQuery, conn);
                    }
                    else
                    {
                        insertQuery = "INSERT INTO Cliente (ruc, nombre, activo) VALUES (@ruc, @nombre, 1)";
                        insertCmd = new SqlCommand(insertQuery, conn);
                        insertCmd.Parameters.AddWithValue("@ruc", ruc);
                    }

                    insertCmd.Parameters.AddWithValue("@nombre", txtNombre.Text.Trim());
                    insertCmd.ExecuteNonQuery();

                    AuditoriaHelper.Registrar("INSERT", "Cliente",
                        descripcion: $"Cliente registrado - Nombre: {txtNombre.Text.Trim()}, RUC: {(string.IsNullOrWhiteSpace(ruc) ? "Sin RUC" : ruc)}");

                    LimpiarFormulario();
                    MostrarMensaje("Cliente registrado correctamente.", true);
                    CargarClientes();
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el cliente: " + ex.Message);
                System.Diagnostics.Debug.WriteLine("Error en RegistroClientes: " + ex.ToString());
            }
        }

        protected void gvClientes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idCliente = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Cliente 
                            SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                            WHERE idCliente = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idCliente);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Cliente", idCliente,
                        "Estado de cliente actualizado (activar/desactivar)");

                    CargarClientes();
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

        private bool EsNumerico(string texto)
        {
            foreach (char c in texto)
            {
                if (!char.IsDigit(c)) return false;
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
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
