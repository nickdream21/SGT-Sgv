using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroEquiposMaquinaria : PaginaBase
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
                CargarEquipos();
            }
        }

        private void CargarEquipos()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT idEquipo, placa, ISNULL(descripcion,'') AS descripcion,
                             ISNULL(tipo,'') AS tipo, ISNULL(marca,'') AS marca,
                             ISNULL(modelo,'') AS modelo, anio, activo
                      FROM EquiposMaquinaria ORDER BY placa");
                gvEquipos.DataSource = dt;
                gvEquipos.DataBind();
                lblTotalEquipos.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los equipos: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid) return;

                string placa = txtPlaca.Text.Trim().ToUpper();
                string tipo = ddlTipo.SelectedValue;
                string descripcion = txtDescripcion.Text.Trim();
                string marca = txtMarca.Text.Trim();
                string modelo = txtModelo.Text.Trim();
                int? anio = null;
                if (!string.IsNullOrEmpty(txtAnio.Text))
                    anio = Convert.ToInt32(txtAnio.Text.Trim());

                if (EquipoExiste(placa))
                {
                    MostrarMensaje("Ya existe un equipo registrado con esta placa.");
                    return;
                }

                DbHelper.EjecutarNonQuery(
                    @"INSERT INTO EquiposMaquinaria (placa, descripcion, tipo, marca, modelo, anio, activo)
                      VALUES (@placa, @descripcion, @tipo, @marca, @modelo, @anio, 1)",
                    DbHelper.Param("@placa", placa),
                    DbHelper.Param("@descripcion", string.IsNullOrEmpty(descripcion) ? null : (object)descripcion),
                    DbHelper.Param("@tipo", string.IsNullOrEmpty(tipo) ? null : (object)tipo),
                    DbHelper.Param("@marca", string.IsNullOrEmpty(marca) ? null : (object)marca),
                    DbHelper.Param("@modelo", string.IsNullOrEmpty(modelo) ? null : (object)modelo),
                    DbHelper.Param("@anio", anio.HasValue ? (object)anio.Value : null));

                AuditoriaHelper.Registrar("INSERT", "EquiposMaquinaria",
                    descripcion: $"Equipo registrado - Placa: {placa}, Tipo: {tipo}");

                LimpiarFormulario();
                MostrarMensaje("Equipo registrado correctamente.", true);
                CargarEquipos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el equipo: " + ex.Message);
            }
        }

        protected void gvEquipos_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idEquipo = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE EquiposMaquinaria SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idEquipo = @id",
                        DbHelper.Param("@id", idEquipo));

                    AuditoriaHelper.Registrar("UPDATE", "EquiposMaquinaria", idEquipo,
                        "Estado de equipo actualizado (activar/desactivar)");

                    CargarEquipos();
                }
                catch (Exception ex)
                {
                    MostrarMensaje("Error al actualizar el estado: " + ex.Message);
                }
            }
        }

        private bool EquipoExiste(string placa)
        {
            return Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM EquiposMaquinaria WHERE placa = @placa",
                DbHelper.Param("@placa", placa))) > 0;
        }

        private void LimpiarFormulario()
        {
            txtPlaca.Text = string.Empty;
            ddlTipo.SelectedIndex = 0;
            txtDescripcion.Text = string.Empty;
            txtMarca.Text = string.Empty;
            txtModelo.Text = string.Empty;
            txtAnio.Text = string.Empty;
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
