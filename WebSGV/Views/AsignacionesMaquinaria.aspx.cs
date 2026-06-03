using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class AsignacionesMaquinariaPage : PaginaBase
    {
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
                DataTable dtOp = DbHelper.ConsultarTabla(
                    "SELECT idOperador, nombre + ' (' + dni + ')' AS display FROM Operadores WHERE activo = 1 ORDER BY nombre");
                ddlOperador.DataSource = dtOp;
                ddlOperador.DataTextField = "display";
                ddlOperador.DataValueField = "idOperador";
                ddlOperador.DataBind();
                ddlOperador.Items.Insert(0, new ListItem("-- Seleccione Operador --", ""));

                DataTable dtEq = DbHelper.ConsultarTabla(
                    "SELECT idEquipo, placa + ISNULL(' - ' + tipo, '') AS display FROM EquiposMaquinaria WHERE activo = 1 ORDER BY placa");
                ddlEquipo.DataSource = dtEq;
                ddlEquipo.DataTextField = "display";
                ddlEquipo.DataValueField = "idEquipo";
                ddlEquipo.DataBind();
                ddlEquipo.Items.Insert(0, new ListItem("-- Seleccione Equipo --", ""));

                DataTable dtOb = DbHelper.ConsultarTabla(
                    @"SELECT o.idObra, o.nombre + ' (' + c.nombre + ')' AS display
                      FROM Obras o INNER JOIN ClientesObra c ON o.idClienteObra = c.idClienteObra
                      WHERE o.estado = 'ACTIVA' ORDER BY o.nombre");
                ddlObra.DataSource = dtOb;
                ddlObra.DataTextField = "display";
                ddlObra.DataValueField = "idObra";
                ddlObra.DataBind();
                ddlObra.Items.Insert(0, new ListItem("-- Seleccione Obra --", ""));
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
                var parametros = new List<SqlParameter>();

                string query = @"SELECT a.idAsignacion, op.nombre AS operadorNombre,
                                        eq.placa AS equipoPlaca, ob.nombre AS obraNombre,
                                        co.nombre AS clienteNombre, a.fechaAsignacion, a.estado
                                 FROM AsignacionesMaquinaria a
                                 INNER JOIN Operadores op ON a.idOperador = op.idOperador
                                 INNER JOIN EquiposMaquinaria eq ON a.idEquipo = eq.idEquipo
                                 INNER JOIN Obras ob ON a.idObra = ob.idObra
                                 INNER JOIN ClientesObra co ON ob.idClienteObra = co.idClienteObra";

                if (filtroEstado != "TODAS")
                {
                    query += " WHERE a.estado = @estado";
                    parametros.Add(DbHelper.Param("@estado", filtroEstado));
                }

                query += " ORDER BY CASE a.estado WHEN 'ACTIVA' THEN 0 ELSE 1 END, a.fechaAsignacion DESC";

                DataTable dt = DbHelper.ConsultarTabla(query, parametros.ToArray());
                gvAsignaciones.DataSource = dt;
                gvAsignaciones.DataBind();
                lblTotalAsignaciones.Text = dt.Rows.Count + " asignación(es)";
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

                if (OperadorTieneAsignacionActiva(idOperador))
                {
                    MostrarMensaje("Este operador ya tiene una asignación activa. Debe finalizarla antes de crear una nueva.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    @"INSERT INTO AsignacionesMaquinaria (idOperador, idEquipo, idObra, fechaAsignacion, estado, observaciones)
                      VALUES (@idOperador, @idEquipo, @idObra, @fechaAsignacion, 'ACTIVA', @observaciones)",
                    DbHelper.Param("@idOperador", idOperador),
                    DbHelper.Param("@idEquipo", idEquipo),
                    DbHelper.Param("@idObra", idObra),
                    DbHelper.Param("@fechaAsignacion", fechaAsignacion),
                    DbHelper.Param("@observaciones", string.IsNullOrEmpty(observaciones) ? null : (object)observaciones));

                AuditoriaHelper.Registrar("INSERT", "AsignacionesMaquinaria",
                    descripcion: $"Asignación creada - Operador: {ddlOperador.SelectedItem.Text}, Equipo: {ddlEquipo.SelectedItem.Text}, Obra: {ddlObra.SelectedItem.Text}");

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
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE AsignacionesMaquinaria SET estado = 'FINALIZADA', fechaFinAsignacion = GETDATE()
                          WHERE idAsignacion = @id AND estado = 'ACTIVA'",
                        DbHelper.Param("@id", idAsignacion));

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
            return Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM AsignacionesMaquinaria WHERE idOperador = @idOperador AND estado = 'ACTIVA'",
                DbHelper.Param("@idOperador", idOperador))) > 0;
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
