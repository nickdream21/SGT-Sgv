using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class GestionUsuarios : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminSistema();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
                CargarUsuarios();
        }

        private void CargarUsuarios()
        {
            string buscar = txtBuscar.Text.Trim();
            string rol    = ddlFiltroRol.SelectedValue;
            string estado = ddlFiltroEstado.SelectedValue;

            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();

                string query = @"SELECT idUsuario, nombreUsuario, nombre, rol, activo FROM Usuarios WHERE 1=1";
                if (!string.IsNullOrEmpty(buscar))  query += " AND (nombreUsuario LIKE @Buscar OR nombre LIKE @Buscar)";
                if (!string.IsNullOrEmpty(rol))     query += " AND rol = @Rol";
                if (!string.IsNullOrEmpty(estado))  query += " AND activo = @Estado";
                query += " ORDER BY activo DESC, rol, nombre";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    if (!string.IsNullOrEmpty(buscar))  cmd.Parameters.AddWithValue("@Buscar", "%" + buscar + "%");
                    if (!string.IsNullOrEmpty(rol))     cmd.Parameters.AddWithValue("@Rol", rol);
                    if (!string.IsNullOrEmpty(estado))  cmd.Parameters.AddWithValue("@Estado", Convert.ToInt32(estado));

                    DataTable dt = new DataTable();
                    new SqlDataAdapter(cmd).Fill(dt);
                    gvUsuarios.DataSource = dt;
                    gvUsuarios.DataBind();
                }
            }
        }

        protected void btnNuevoUsuario_Click(object sender, EventArgs e)
        {
            hfIdUsuario.Value       = "0";
            lblTituloModal.Text     = "Nuevo Usuario";
            txtNombreUsuario.Text   = "";
            txtNombreUsuario.Enabled = true;
            txtNombre.Text          = "";
            ddlRol.SelectedIndex    = 0;
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
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT idUsuario, nombreUsuario, nombre, rol FROM Usuarios WHERE idUsuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return;

                        hfIdUsuario.Value       = r["idUsuario"].ToString();
                        lblTituloModal.Text     = "Editar Usuario";
                        txtNombreUsuario.Text   = r["nombreUsuario"].ToString();
                        txtNombreUsuario.Enabled = false;
                        txtNombre.Text          = r["nombre"].ToString();
                        ddlRol.SelectedValue    = r["rol"].ToString();
                        pnlContrasena.Visible   = false;
                        pnlMensajeModal.Visible = false;
                    }
                }
            }
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

            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Usuarios SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END WHERE idUsuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);
                    cmd.ExecuteNonQuery();
                }
            }

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Cambio de estado activo/inactivo por Admin Sistema");
            MostrarMensaje("Estado del usuario actualizado correctamente.", "success");
            CargarUsuarios();
        }

        private void PrepararResetPassword(int idUsuario)
        {
            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "SELECT nombreUsuario FROM Usuarios WHERE idUsuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", idUsuario);
                    object result = cmd.ExecuteScalar();
                    lblUsuarioReset.Text = result?.ToString() ?? "";
                }
            }

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

            string cs = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            try
            {
                using (SqlConnection conn = new SqlConnection(cs))
                {
                    conn.Open();

                    if (idUsuario == 0)
                    {
                        using (SqlCommand check = new SqlCommand(
                            "SELECT COUNT(*) FROM Usuarios WHERE nombreUsuario = @user", conn))
                        {
                            check.Parameters.AddWithValue("@user", nombreUsuario);
                            if ((int)check.ExecuteScalar() > 0)
                            {
                                MostrarMensajeModal("El nombre de usuario ya existe. Elige otro.", "danger");
                                AbrirModal("modalUsuario");
                                return;
                            }
                        }

                        string hash = PasswordHelper.HashPassword(contrasena);
                        using (SqlCommand cmd = new SqlCommand(
                            "INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo) VALUES (@user, @nombre, @pass, @rol, 1)", conn))
                        {
                            cmd.Parameters.AddWithValue("@user",   nombreUsuario);
                            cmd.Parameters.AddWithValue("@nombre", nombre);
                            cmd.Parameters.AddWithValue("@pass",   hash);
                            cmd.Parameters.AddWithValue("@rol",    rol);
                            cmd.ExecuteNonQuery();
                        }
                        AuditoriaHelper.Registrar("INSERT", "Usuarios", 0, $"Nuevo usuario creado: {nombreUsuario}, rol: {rol}");
                        MostrarMensaje($"Usuario '{nombreUsuario}' creado exitosamente.", "success");
                    }
                    else
                    {
                        using (SqlCommand cmd = new SqlCommand(
                            "UPDATE Usuarios SET nombre = @nombre, rol = @rol WHERE idUsuario = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@nombre", nombre);
                            cmd.Parameters.AddWithValue("@rol",    rol);
                            cmd.Parameters.AddWithValue("@id",     idUsuario);
                            cmd.ExecuteNonQuery();
                        }
                        AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, $"Usuario actualizado: nombre={nombre}, rol={rol}");
                        MostrarMensaje("Usuario actualizado exitosamente.", "success");
                    }
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
            int    idUsuario    = Convert.ToInt32(hfIdUsuarioReset.Value);
            string nuevaPass    = txtNuevaPass.Text;
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

            string cs   = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;
            string hash = PasswordHelper.HashPassword(nuevaPass);

            using (SqlConnection conn = new SqlConnection(cs))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(
                    "UPDATE Usuarios SET contrasena = @hash WHERE idUsuario = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@hash", hash);
                    cmd.Parameters.AddWithValue("@id",   idUsuario);
                    cmd.ExecuteNonQuery();
                }
            }

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Contraseña restablecida por Admin Sistema");
            MostrarMensaje("Contraseña restablecida exitosamente.", "success");
            CargarUsuarios();
        }

        protected void btnBuscar_Click(object sender, EventArgs e) => CargarUsuarios();

        protected void btnLimpiar_Click(object sender, EventArgs e)
        {
            txtBuscar.Text           = "";
            ddlFiltroRol.SelectedIndex   = 0;
            ddlFiltroEstado.SelectedIndex = 0;
            pnlMensaje.Visible       = false;
            CargarUsuarios();
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text      = mensaje;
            lblMensaje.CssClass  = $"alert alert-{tipo} d-block mb-0";
            pnlMensaje.Visible   = true;
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
