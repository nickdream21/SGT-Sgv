using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class GestionUsuarios : PaginaBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarRoles();
                CargarUsuarios();
            }
        }

        private void CargarRoles()
        {
            var rolesDisponibles = RolesHelper.ObtenerRolesDisponibles();

            ddlFiltroRol.Items.Clear();
            ddlFiltroRol.Items.Add(new ListItem("-- Todos los roles --", ""));

            ddlRol.Items.Clear();

            foreach (var rol in rolesDisponibles)
            {
                ddlFiltroRol.Items.Add(new ListItem(rol.Texto, rol.Valor));
                ddlRol.Items.Add(new ListItem(rol.Texto, rol.Valor));
            }
        }

        private void CargarUsuarios()
        {
            string buscar = txtBuscar.Text.Trim();
            string rol    = ddlFiltroRol.SelectedValue;
            string estado = ddlFiltroEstado.SelectedValue;

            var parametros = new List<SqlParameter>();
            string query = "SELECT idUsuario, nombreUsuario, nombre, rol, activo FROM Usuarios WHERE 1=1";

            if (!string.IsNullOrEmpty(buscar))
            {
                query += " AND (nombreUsuario LIKE @Buscar OR nombre LIKE @Buscar)";
                parametros.Add(DbHelper.Param("@Buscar", "%" + buscar + "%"));
            }
            if (!string.IsNullOrEmpty(rol))
            {
                query += " AND rol = @Rol";
                parametros.Add(DbHelper.Param("@Rol", rol));
            }
            if (!string.IsNullOrEmpty(estado))
            {
                query += " AND activo = @Estado";
                parametros.Add(DbHelper.Param("@Estado", Convert.ToInt32(estado)));
            }
            query += " ORDER BY activo DESC, rol, nombre";

            DataTable dt = DbHelper.ConsultarTabla(query, parametros.ToArray());
            gvUsuarios.DataSource = dt;
            gvUsuarios.DataBind();
        }

        protected void btnNuevoUsuario_Click(object sender, EventArgs e)
        {
            hfIdUsuario.Value       = "0";
            lblTituloModal.Text     = "Nuevo Usuario";
            txtNombreUsuario.Text   = "";
            txtNombreUsuario.Enabled = true;
            txtNombre.Text          = "";
            if (ddlRol.Items.Count > 0)
                ddlRol.SelectedIndex = 0;
            txtContrasena.Text      = "";
            pnlContrasena.Visible   = true;
            pnlMensajeModal.Visible = false;

            AbrirModal("modalUsuario");
        }

        protected void gvUsuarios_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            int idUsuario = Convert.ToInt32(e.CommandArgument);

            switch (e.CommandName)
            {
                case "EditarUsuario":
                    CargarUsuarioEnModal(idUsuario);
                    break;
                case "ToggleActivo":
                    ToggleEstadoUsuario(idUsuario);
                    break;
                case "ResetPassword":
                    PrepararResetPassword(idUsuario);
                    break;
            }
        }

        private void CargarUsuarioEnModal(int idUsuario)
        {
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT idUsuario, nombreUsuario, nombre, rol FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));

            if (dt.Rows.Count == 0) return;
            DataRow r = dt.Rows[0];

            hfIdUsuario.Value        = r["idUsuario"].ToString();
            lblTituloModal.Text      = "Editar Usuario";
            txtNombreUsuario.Text    = r["nombreUsuario"].ToString();
            txtNombreUsuario.Enabled = false;
            txtNombre.Text           = r["nombre"].ToString();
            string rolUsuario = r["rol"].ToString();
            ListItem itemRol = ddlRol.Items.FindByValue(rolUsuario);
            if (itemRol == null)
                ddlRol.Items.Add(new ListItem(rolUsuario, rolUsuario));
            ddlRol.SelectedValue     = rolUsuario;
            pnlContrasena.Visible    = false;
            pnlMensajeModal.Visible  = false;

            AbrirModal("modalUsuario");
        }

        private void ToggleEstadoUsuario(int idUsuario)
        {
            if (idUsuario == SecurityHelper.ObtenerIdUsuario())
            {
                MostrarMensaje("No puedes desactivar tu propia cuenta.", "warning");
                CargarUsuarios();
                return;
            }

            DbHelper.EjecutarNonQuery(
                "UPDATE Usuarios SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Cambio de estado activo/inactivo por Admin Sistema");
            MostrarMensaje("Estado del usuario actualizado correctamente.", "success");
            CargarUsuarios();
        }

        private void PrepararResetPassword(int idUsuario)
        {
            object result = DbHelper.EjecutarEscalar(
                "SELECT nombreUsuario FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));
            lblUsuarioReset.Text = result?.ToString() ?? "";

            hfIdUsuarioReset.Value   = idUsuario.ToString();
            txtNuevaPass.Text        = "";
            txtConfirmarPass.Text    = "";
            pnlMensajeReset.Visible  = false;

            AbrirModal("modalResetPass");
        }

        protected void btnGuardarUsuario_Click(object sender, EventArgs e)
        {
            int    idUsuario     = Convert.ToInt32(hfIdUsuario.Value);
            string nombreUsuario = txtNombreUsuario.Text.Trim();
            string nombre        = txtNombre.Text.Trim();
            string rol           = ddlRol.SelectedValue;
            string contrasena    = txtContrasena.Text;

            if (string.IsNullOrEmpty(nombreUsuario) || string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(rol))
            {
                MostrarMensajeModal("Todos los campos obligatorios deben ser completados.", "danger");
                AbrirModal("modalUsuario");
                return;
            }

            if (idUsuario == 0)
            {
                if (string.IsNullOrEmpty(contrasena))
                {
                    MostrarMensajeModal("Debe ingresar una contraseña para el nuevo usuario.", "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
                if (contrasena.Length < 6)
                {
                    MostrarMensajeModal("La contraseña debe tener al menos 6 caracteres.", "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
            }

            try
            {
                if (idUsuario == 0)
                {
                    int existe = Convert.ToInt32(DbHelper.EjecutarEscalar(
                        "SELECT COUNT(*) FROM Usuarios WHERE nombreUsuario = @user",
                        DbHelper.Param("@user", nombreUsuario)));

                    if (existe > 0)
                    {
                        MostrarMensajeModal("El nombre de usuario ya existe. Elige otro.", "danger");
                        AbrirModal("modalUsuario");
                        return;
                    }

                    string hash = PasswordHelper.HashPassword(contrasena);
                    DbHelper.EjecutarNonQuery(
                        "INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo) VALUES (@user, @nombre, @pass, @rol, 1)",
                        DbHelper.Param("@user",   nombreUsuario),
                        DbHelper.Param("@nombre", nombre),
                        DbHelper.Param("@pass",   hash),
                        DbHelper.Param("@rol",    rol));

                    AuditoriaHelper.Registrar("INSERT", "Usuarios", 0, $"Nuevo usuario creado: {nombreUsuario}, rol: {rol}");
                    MostrarMensaje($"Usuario '{nombreUsuario}' creado exitosamente.", "success");
                }
                else
                {
                    DbHelper.EjecutarNonQuery(
                        "UPDATE Usuarios SET nombre = @nombre, rol = @rol WHERE idUsuario = @id",
                        DbHelper.Param("@nombre", nombre),
                        DbHelper.Param("@rol",    rol),
                        DbHelper.Param("@id",     idUsuario));

                    AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, $"Usuario actualizado: nombre={nombre}, rol={rol}");
                    MostrarMensaje("Usuario actualizado exitosamente.", "success");
                }

                CargarUsuarios();
            }
            catch (Exception ex)
            {
                MostrarMensajeModal("Error al guardar: " + ex.Message, "danger");
                AbrirModal("modalUsuario");
            }
        }

        protected void btnResetPass_Click(object sender, EventArgs e)
        {
            int    idUsuario     = Convert.ToInt32(hfIdUsuarioReset.Value);
            string nuevaPass     = txtNuevaPass.Text;
            string confirmarPass = txtConfirmarPass.Text;

            if (string.IsNullOrEmpty(nuevaPass) || nuevaPass.Length < 6)
            {
                MostrarMensajeReset("La contraseña debe tener al menos 6 caracteres.", "danger");
                AbrirModal("modalResetPass");
                return;
            }

            if (nuevaPass != confirmarPass)
            {
                MostrarMensajeReset("Las contraseñas no coinciden.", "danger");
                AbrirModal("modalResetPass");
                return;
            }

            string hash = PasswordHelper.HashPassword(nuevaPass);
            DbHelper.EjecutarNonQuery(
                "UPDATE Usuarios SET contrasena = @hash WHERE idUsuario = @id",
                DbHelper.Param("@hash", hash),
                DbHelper.Param("@id",   idUsuario));

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Contraseña restablecida por Admin Sistema");
            MostrarMensaje("Contraseña restablecida exitosamente.", "success");
            CargarUsuarios();
        }

        protected void btnBuscar_Click(object sender, EventArgs e) => CargarUsuarios();

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            txtBuscar.Text                = "";
            ddlFiltroRol.SelectedIndex    = 0;
            ddlFiltroEstado.SelectedIndex = 0;
            pnlMensaje.Visible            = false;
            CargarUsuarios();
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text     = mensaje;
            lblMensaje.CssClass = $"alert alert-{tipo} d-block mb-0";
            pnlMensaje.Visible  = true;
        }

        private void MostrarMensajeModal(string mensaje, string tipo)
        {
            lblMensajeModal.Text     = mensaje;
            lblMensajeModal.CssClass = $"alert alert-{tipo} d-block mb-0";
            pnlMensajeModal.Visible  = true;
        }

        private void MostrarMensajeReset(string mensaje, string tipo)
        {
            lblMensajeReset.Text     = mensaje;
            lblMensajeReset.CssClass = $"alert alert-{tipo} d-block mb-0";
            pnlMensajeReset.Visible  = true;
        }

        private void AbrirModal(string idModal)
        {
            ScriptManager.RegisterStartupScript(this, GetType(), "AbrirModal" + idModal,
                $"$(document).ready(function(){{$('#{idModal}').modal('show');}});", true);
        }
    }
}
