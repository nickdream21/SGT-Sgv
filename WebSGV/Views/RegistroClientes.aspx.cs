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
    public partial class RegistroClientes : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("REGISTRO");
            SecurityHelper.AgregarHeadersSeguridad();

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
            if (!Page.IsValid)
            {
                MostrarMensaje("Corrija los campos marcados del cliente.");
                return;
            }

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
                    MostrarMensaje("El RUC debe contener 11 d�gitos num�ricos.");
                    return;
                }
            }

            if (!Regex.IsMatch(txtNombre.Text.Trim(), "^[A-Za-zÁÉÍÓÚÑáéíóú0-9\\s\\-\\.&]{3,200}$"))
            {
                MostrarMensaje("Nombre/Razón Social inválido: use entre 3 y 200 caracteres válidos.");
                return;
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

                    string insertQuery = string.IsNullOrWhiteSpace(ruc)
                        ? "INSERT INTO Cliente (nombre, activo) VALUES (@nombre, 1)"
                        : "INSERT INTO Cliente (ruc, nombre, activo) VALUES (@ruc, @nombre, 1)";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        if (!string.IsNullOrWhiteSpace(ruc))
                            insertCmd.Parameters.AddWithValue("@ruc", ruc);
                        insertCmd.Parameters.AddWithValue("@nombre", txtNombre.Text.Trim());
                        insertCmd.ExecuteNonQuery();
                    }

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

        protected string AttrEncode(object val) =>
            System.Web.HttpUtility.HtmlAttributeEncode(val?.ToString() ?? "");

        protected void btnActualizarCliente_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfIdCliente.Value, out int idCliente) || idCliente <= 0)
            {
                MostrarMensaje("ID de cliente inválido.");
                return;
            }

            string ruc = txtEditarRUC.Text.Trim();
            string nombre = txtEditarNombre.Text.Trim();

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MostrarMensaje("El nombre del cliente es obligatorio.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(ruc) && (ruc.Length != 11 || !EsNumerico(ruc)))
            {
                MostrarMensaje("El RUC debe contener 11 dígitos numéricos.");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    if (!string.IsNullOrWhiteSpace(ruc))
                    {
                        string check = "SELECT COUNT(*) FROM Cliente WHERE ruc=@ruc AND idCliente<>@id";
                        using (SqlCommand cmd = new SqlCommand(check, conn))
                        {
                            cmd.Parameters.AddWithValue("@ruc", ruc);
                            cmd.Parameters.AddWithValue("@id", idCliente);
                            if ((int)cmd.ExecuteScalar() > 0)
                            {
                                MostrarMensaje("Ya existe otro cliente registrado con ese RUC.");
                                return;
                            }
                        }
                    }

                    string update = "UPDATE Cliente SET ruc=@ruc, nombre=@nombre WHERE idCliente=@id";
                    using (SqlCommand cmd = new SqlCommand(update, conn))
                    {
                        cmd.Parameters.AddWithValue("@ruc", string.IsNullOrWhiteSpace(ruc) ? (object)DBNull.Value : ruc);
                        cmd.Parameters.AddWithValue("@nombre", nombre);
                        cmd.Parameters.AddWithValue("@id", idCliente);
                        cmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("UPDATE", "Cliente", idCliente,
                    $"Cliente editado — Nombre:{nombre}, RUC:{(string.IsNullOrWhiteSpace(ruc) ? "Sin RUC" : ruc)}");

                hfIdCliente.Value = "";
                MostrarMensaje("Cliente actualizado correctamente.", true);
                CargarClientes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al actualizar el cliente: " + ex.Message);
            }
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
