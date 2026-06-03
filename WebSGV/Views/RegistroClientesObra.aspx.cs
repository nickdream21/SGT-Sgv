using System;
using System.Data;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class RegistroClientesObra : PaginaBase
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
                CargarClientes();
            }
        }

        private void CargarClientes()
        {
            try
            {
                DataTable dt = DbHelper.ConsultarTabla(
                    @"SELECT idClienteObra, nombre, ISNULL(ruc,'') AS ruc,
                             ISNULL(contacto,'') AS contacto, ISNULL(telefono,'') AS telefono, activo
                      FROM ClientesObra ORDER BY nombre");
                gvClientes.DataSource = dt;
                gvClientes.DataBind();
                lblTotalClientes.Text = dt.Rows.Count + " registro(s)";
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al cargar los clientes: " + ex.Message);
            }
        }

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Page.IsValid) return;

                string nombre = txtNombre.Text.Trim();
                string ruc = txtRuc.Text.Trim();
                string contacto = txtContacto.Text.Trim();
                string telefono = txtTelefono.Text.Trim();

                DbHelper.EjecutarNonQuery(
                    @"INSERT INTO ClientesObra (nombre, ruc, contacto, telefono, activo)
                      VALUES (@nombre, @ruc, @contacto, @telefono, 1)",
                    DbHelper.Param("@nombre", nombre),
                    DbHelper.Param("@ruc", string.IsNullOrEmpty(ruc) ? null : (object)ruc),
                    DbHelper.Param("@contacto", string.IsNullOrEmpty(contacto) ? null : (object)contacto),
                    DbHelper.Param("@telefono", string.IsNullOrEmpty(telefono) ? null : (object)telefono));

                AuditoriaHelper.Registrar("INSERT", "ClientesObra",
                    descripcion: $"Cliente de obra registrado - Nombre: {nombre}");

                LimpiarFormulario();
                MostrarMensaje("Cliente registrado correctamente.", true);
                CargarClientes();
            }
            catch (Exception ex)
            {
                MostrarMensaje("Error al registrar el cliente: " + ex.Message);
            }
        }

        protected void gvClientes_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "ToggleActivo")
            {
                int idCliente = Convert.ToInt32(e.CommandArgument);
                try
                {
                    DbHelper.EjecutarNonQuery(
                        @"UPDATE ClientesObra SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idClienteObra = @id",
                        DbHelper.Param("@id", idCliente));

                    AuditoriaHelper.Registrar("UPDATE", "ClientesObra", idCliente,
                        "Estado de cliente actualizado (activar/desactivar)");

                    CargarClientes();
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
            txtRuc.Text = string.Empty;
            txtContacto.Text = string.Empty;
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
