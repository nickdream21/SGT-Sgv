using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroOperadores : PaginaBase
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
                CargarOperadores();
            }
        }

        private void CargarOperadores()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT idOperador, dni, nombre, ISNULL(telefono, '') AS telefono, activo
                      FROM Operadores ORDER BY nombre");
                gvOperadores.DataSource = dt;
                gvOperadores.DataBind();
                lblTotalOperadores.Text = dt.Rows.Count + " registro(s)";
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

                DbHelper.EjecutarNonQuery(
                    @"INSERT INTO Operadores (nombre, dni, telefono, activo) VALUES (@nombre, @dni, @telefono, 1)",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@dni", dni),
                    DbHelper.Param("@telefono", string.IsNullOrEmpty(telefono) ? null : (object)telefono));

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
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE Operadores SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idOperador = @id",
                        DbHelper.Param("@id", idOperador));

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
            return Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM Operadores WHERE dni = @dni",
                DbHelper.Param("@dni", dni))) > 0;
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
