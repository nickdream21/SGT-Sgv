using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroObras : System.Web.UI.Page
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
                CargarClientes();
                CargarObras();
            }
        }

        private void CargarClientes()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT idClienteObra, nombre FROM ClientesObra WHERE activo = 1 ORDER BY nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());

                        ddlCliente.DataSource = dt;
                        ddlCliente.DataTextField = "nombre";
                        ddlCliente.DataValueField = "idClienteObra";
                        ddlCliente.DataBind();
                        ddlCliente.Items.Insert(0, new ListItem("-- Seleccione Cliente --", ""));
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los clientes: " + ex.Message);
            }
        }

        private void CargarObras()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT o.idObra, o.nombre, c.nombre AS clienteNombre, 
                                            ISNULL(o.ubicacion,'') AS ubicacion, o.estado
                                     FROM Obras o
                                     INNER JOIN ClientesObra c ON o.idClienteObra = c.idClienteObra
                                     ORDER BY o.estado, o.nombre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvObras.DataSource = dt;
                        gvObras.DataBind();
                        lblTotalObras.Text = dt.Rows.Count + " registro(s)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar las obras: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid) return;

                string nombre = txtNombre.Text.Trim();
                int idClienteObra = Convert.ToInt32(ddlCliente.SelectedValue);
                string ubicacion = txtUbicacion.Text.Trim();

                DateTime? fechaInicio = null;
                DateTime? fechaFin = null;
                if (!string.IsNullOrEmpty(txtFechaInicio.Text))
                    fechaInicio = DateTime.Parse(txtFechaInicio.Text);
                if (!string.IsNullOrEmpty(txtFechaFin.Text))
                    fechaFin = DateTime.Parse(txtFechaFin.Text);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO Obras (nombre, idClienteObra, ubicacion, estado, fechaInicio, fechaFin)
                                     VALUES (@nombre, @idClienteObra, @ubicacion, 'ACTIVA', @fechaInicio, @fechaFin)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@nombre", nombre);
                        cmd.Parameters.AddWithValue("@idClienteObra", idClienteObra);
                        cmd.Parameters.AddWithValue("@ubicacion", string.IsNullOrEmpty(ubicacion) ? (object)DBNull.Value : ubicacion);
                        cmd.Parameters.AddWithValue("@fechaInicio", fechaInicio.HasValue ? (object)fechaInicio.Value : DBNull.Value);
                        cmd.Parameters.AddWithValue("@fechaFin", fechaFin.HasValue ? (object)fechaFin.Value : DBNull.Value);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                AuditoriaHelper.Registrar("INSERT", "Obras",
                    descripcion: $"Obra registrada - Nombre: {nombre}, Cliente ID: {idClienteObra}");

                LimpiarFormulario();
                MostrarMensaje("Obra registrada correctamente.", true);
                CargarObras();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar la obra: " + ex.Message);
            }
        }

        protected void gvObras_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "CambiarEstado")
            {
                int idObra = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE Obras 
                                         SET estado = CASE 
                                            WHEN estado = 'ACTIVA' THEN 'SUSPENDIDA'
                                            WHEN estado = 'SUSPENDIDA' THEN 'ACTIVA'
                                            ELSE estado END
                                         WHERE idObra = @id";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idObra);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "Obras", idObra,
                        "Estado de obra actualizado");

                    CargarObras();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        private void LimpiarFormulario()
        {
            txtNombre.Text = string.Empty;
            ddlCliente.SelectedIndex = 0;
            txtUbicacion.Text = string.Empty;
            txtFechaInicio.Text = string.Empty;
            txtFechaFin.Text = string.Empty;
        }

        protected string ObtenerClaseEstadoObra(object estado)
        {
            if (estado == null) return "badge-secondary";
            switch (estado.ToString())
            {
                case "ACTIVA": return "badge-success";
                case "SUSPENDIDA": return "badge-warning";
                case "FINALIZADA": return "badge-secondary";
                default: return "badge-secondary";
            }
        }

        protected string ObtenerTextoBotonObra(object estado)
        {
            if (estado == null) return "";
            switch (estado.ToString())
            {
                case "ACTIVA": return "Suspender";
                case "SUSPENDIDA": return "Reactivar";
                default: return "";
            }
        }

        protected string ObtenerClaseBotonObra(object estado)
        {
            if (estado == null) return "btn btn-sm btn-secondary";
            switch (estado.ToString())
            {
                case "ACTIVA": return "btn btn-warning btn-sm";
                case "SUSPENDIDA": return "btn btn-success btn-sm";
                default: return "btn btn-sm btn-secondary";
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito = false)
        {
            pnlMensaje.Visible = true;
            string css = esExito ? "alert alert-success" : "alert alert-danger";
            lblMensaje.Text = $"<div class='{css}'>{mensaje}</div>";
        }
    }
}
