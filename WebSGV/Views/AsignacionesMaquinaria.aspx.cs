using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class AsignacionesMaquinariaPage : System.Web.UI.Page
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
                CargarDropdowns();
                txtFechaAsignacion.Text = DateTime.Now.ToString("yyyy-MM-dd");
                CargarAsignaciones();
            }
        }

        private void CargarDropdowns()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Operadores activos
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT idOperador, nombre + ' (' + dni + ')' AS display FROM Operadores WHERE activo = 1 ORDER BY nombre", conn))
                    {
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        ddlOperador.DataSource = dt;
                        ddlOperador.DataTextField = "display";
                        ddlOperador.DataValueField = "idOperador";
                        ddlOperador.DataBind();
                        ddlOperador.Items.Insert(0, new ListItem("-- Seleccione Operador --", ""));
                    }

                    // Equipos activos
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT idEquipo, placa + ISNULL(' - ' + tipo, '') AS display FROM EquiposMaquinaria WHERE activo = 1 ORDER BY placa", conn))
                    {
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        ddlEquipo.DataSource = dt;
                        ddlEquipo.DataTextField = "display";
                        ddlEquipo.DataValueField = "idEquipo";
                        ddlEquipo.DataBind();
                        ddlEquipo.Items.Insert(0, new ListItem("-- Seleccione Equipo --", ""));
                    }

                    // Obras activas (con nombre del cliente)
                    using (SqlCommand cmd = new SqlCommand(
                        @"SELECT o.idObra, o.nombre + ' (' + c.nombre + ')' AS display 
                          FROM Obras o INNER JOIN ClientesObra c ON o.idClienteObra = c.idClienteObra
                          WHERE o.estado = 'ACTIVA' ORDER BY o.nombre", conn))
                    {
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        ddlObra.DataSource = dt;
                        ddlObra.DataTextField = "display";
                        ddlObra.DataValueField = "idObra";
                        ddlObra.DataBind();
                        ddlObra.Items.Insert(0, new ListItem("-- Seleccione Obra --", ""));
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los datos: " + ex.Message);
            }
        }

        private void CargarAsignaciones()
        {
            try
            {
                string filtroEstado = ddlFiltroEstado.SelectedValue;

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"SELECT a.idAsignacion, op.nombre AS operadorNombre, 
                                            eq.placa AS equipoPlaca, ob.nombre AS obraNombre,
                                            co.nombre AS clienteNombre, a.fechaAsignacion, a.estado
                                     FROM AsignacionesMaquinaria a
                                     INNER JOIN Operadores op ON a.idOperador = op.idOperador
                                     INNER JOIN EquiposMaquinaria eq ON a.idEquipo = eq.idEquipo
                                     INNER JOIN Obras ob ON a.idObra = ob.idObra
                                     INNER JOIN ClientesObra co ON ob.idClienteObra = co.idClienteObra";

                    if (filtroEstado != "TODAS")
                        query += " WHERE a.estado = @estado";

                    query += " ORDER BY CASE a.estado WHEN 'ACTIVA' THEN 0 ELSE 1 END, a.fechaAsignacion DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (filtroEstado != "TODAS")
                            cmd.Parameters.AddWithValue("@estado", filtroEstado);

                        conn.Open();
                        DataTable dt = new DataTable();
                        dt.Load(cmd.ExecuteReader());
                        gvAsignaciones.DataSource = dt;
                        gvAsignaciones.DataBind();
                        lblTotalAsignaciones.Text = dt.Rows.Count + " asignación(es)";
                    }
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar las asignaciones: " + ex.Message);
            }
        }

        protected void btnAsignar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid) return;

                int idOperador = Convert.ToInt32(ddlOperador.SelectedValue);
                int idEquipo = Convert.ToInt32(ddlEquipo.SelectedValue);
                int idObra = Convert.ToInt32(ddlObra.SelectedValue);
                DateTime fechaAsignacion = DateTime.Parse(txtFechaAsignacion.Text);
                string observaciones = txtObservaciones.Text.Trim();

                // Verificar que el operador no tenga asignacion activa
                if (OperadorTieneAsignacionActiva(idOperador))
                {
                    MostrarMensaje("Este operador ya tiene una asignación activa. Debe finalizarla antes de crear una nueva.");
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = @"INSERT INTO AsignacionesMaquinaria (idOperador, idEquipo, idObra, fechaAsignacion, estado, observaciones)
                                     VALUES (@idOperador, @idEquipo, @idObra, @fechaAsignacion, 'ACTIVA', @observaciones)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@idOperador", idOperador);
                        cmd.Parameters.AddWithValue("@idEquipo", idEquipo);
                        cmd.Parameters.AddWithValue("@idObra", idObra);
                        cmd.Parameters.AddWithValue("@fechaAsignacion", fechaAsignacion);
                        cmd.Parameters.AddWithValue("@observaciones", string.IsNullOrEmpty(observaciones) ? (object)DBNull.Value : observaciones);
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }

                string opNombre = ddlOperador.SelectedItem.Text;
                string eqPlaca = ddlEquipo.SelectedItem.Text;
                string obNombre = ddlObra.SelectedItem.Text;

                AuditoriaHelper.Registrar("INSERT", "AsignacionesMaquinaria",
                    descripcion: $"Asignación creada - Operador: {opNombre}, Equipo: {eqPlaca}, Obra: {obNombre}");

                LimpiarFormulario();
                MostrarMensaje("Asignación creada correctamente.", true);
                CargarAsignaciones();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al crear la asignación: " + ex.Message);
            }
        }

        protected void gvAsignaciones_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "FinalizarAsignacion")
            {
                int idAsignacion = Convert.ToInt32(e.CommandArgument);
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"UPDATE AsignacionesMaquinaria 
                                         SET estado = 'FINALIZADA', fechaFinAsignacion = GETDATE()
                                         WHERE idAsignacion = @id AND estado = 'ACTIVA'";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@id", idAsignacion);
                            conn.Open();
                            cmd.ExecuteNonQuery();
                        }
                    }

                    AuditoriaHelper.Registrar("UPDATE", "AsignacionesMaquinaria", idAsignacion,
                        "Asignación finalizada");

                    MostrarMensaje("Asignación finalizada correctamente.", true);
                    CargarAsignaciones();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al finalizar la asignación: " + ex.Message);
                }
            }
        }

        protected void ddlFiltroEstado_SelectedIndexChanged(object sender, EventArgs e)
        {
            CargarAsignaciones();
        }

        private bool OperadorTieneAsignacionActiva(int idOperador)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                string query = "SELECT COUNT(*) FROM AsignacionesMaquinaria WHERE idOperador = @idOperador AND estado = 'ACTIVA'";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@idOperador", idOperador);
                    conn.Open();
                    return (int)cmd.ExecuteScalar() > 0;
                }
            }
        }

        private void LimpiarFormulario()
        {
            ddlOperador.SelectedIndex = 0;
            ddlEquipo.SelectedIndex = 0;
            ddlObra.SelectedIndex = 0;
            txtFechaAsignacion.Text = DateTime.Now.ToString("yyyy-MM-dd");
            txtObservaciones.Text = string.Empty;
        }

        protected string ObtenerClaseEstado(object estado)
        {
            if (estado == null) return "badge-secondary";
            switch (estado.ToString())
            {
                case "ACTIVA": return "badge-success";
                case "FINALIZADA": return "badge-secondary";
                case "CANCELADA": return "badge-danger";
                default: return "badge-secondary";
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
