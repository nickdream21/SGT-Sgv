using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroOperadores : System.Web.UI.Page
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!RolesHelper.EsAdminMaquinaria() && !RolesHelper.EsAdmin())
            {
                Response.Redirect("~/Views/Login.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            if (!IsPostBack)
            {
                CargarOperadores();
            }
        }

        private void CargarOperadores()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT idOperador, dni, nombre, ISNULL(telefono, '') AS telefono, activo
                                     FROM Operadores ORDER BY nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvOperadores.DataSource = dt;
                        gvOperadores.DataBind();
                        lblTotalOperadores.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los operadores: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid) return;

                string dni = txtDNI.Text.Trim();
                string nombre = txtNombre.Text.Trim();
                string telefono = txtTelefono.Text.Trim();

                if (OperadorExiste(dni))
                {
                    MostrarMensaje("Ya existe un operador registrado con este DNI.");
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO Operadores (nombre, dni, telefono, activo)
                                     VALUES (@nombre, @dni, @telefono, 1)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@nombre", nombre);
                        cmd.Parameters.AddWithValue("@dni", dni);
                        cmd.Parameters.AddWithValue("@telefono", string.IsNullOrEmpty(telefono) ? (object)DBNull.Value : telefono);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("INSERT", "Operadores",
                    descripcion: $"Operador registrado - Nombre: {nombre}, DNI: {dni}");

                LimpiarFormulario();
                MostrarMensaje("Operador registrado correctamente.", true);
                CargarOperadores();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el operador: " + ex.Message);
            }
        }

        protected void gvOperadores_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idOperador = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Operadores 
                                         SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END
                                         WHERE idOperador = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idOperador);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Operadores", idOperador,
                        "Estado de operador actualizado (activar/desactivar)");

                    CargarOperadores();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        private bool OperadorExiste(string dni)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT COUNT(*) FROM Operadores WHERE dni = @dni";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@dni", dni);
                    conn.Open();
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        private void LimpiarFormulario()
        {
            txtDNI.Text = string.Empty;
            txtNombre.Text = string.Empty;
            txtTelefono.Text = string.Empty;
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

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
