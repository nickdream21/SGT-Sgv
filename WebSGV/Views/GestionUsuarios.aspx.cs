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

            txtContrasena.Attributes["placeholder"] = PasswordHelper.TextoMinimoNueva;
            txtNuevaPass.Attributes["placeholder"] = PasswordHelper.TextoMinimoNueva;

            if (!IsPostBack)
            {
                CargarRoles();
                CargarConductores();
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

        private void CargarConductores()
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT c.idConductor,
                       LTRIM(RTRIM(
                           COALESCE(NULLIF(c.DNI, ''), NULLIF(c.carnetExtranjeria, ''), 'Sin documento')
                           + ' - ' + ISNULL(c.nombre, '') + ' ' + ISNULL(c.apPaterno, '') + ' ' + ISNULL(c.apMaterno, '')
                       )) AS descripcion,
                       u.nombreUsuario
                FROM Conductor c
                LEFT JOIN Usuarios u ON u.idConductor = c.idConductor
                WHERE c.activo = 1
                ORDER BY c.apPaterno, c.apMaterno, c.nombre");

            ddlConductor.Items.Clear();
            ddlConductor.Items.Add(new ListItem("-- No aplica --", ""));

            foreach (DataRow row in dt.Rows)
            {
                string descripcion = row["descripcion"].ToString();
                if (row["nombreUsuario"] != DBNull.Value)
                    descripcion += " (usuario: " + row["nombreUsuario"] + ")";

                ddlConductor.Items.Add(new ListItem(descripcion, row["idConductor"].ToString()));
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
            lblTituloModal.Text     = "Nuevo usuario";
            txtNombreUsuario.Text   = "";
            txtNombreUsuario.Enabled = true;
            txtNombre.Text          = "";
            if (ddlRol.Items.Count > 0)
                ddlRol.SelectedIndex = 0;
            ddlConductor.SelectedIndex = 0;
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
                "SELECT idUsuario, nombreUsuario, nombre, rol, idConductor FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));

            if (dt.Rows.Count == 0) return;
            DataRow r = dt.Rows[0];

            hfIdUsuario.Value        = r["idUsuario"].ToString();
            lblTituloModal.Text      = "Editar Usuario";
            txtNombreUsuario.Text    = r["nombreUsuario"].ToString();
            txtNombreUsuario.Enabled = false;
            txtNombre.Text           = r["nombre"].ToString();
            // Los roles son un valor cerrado en MAYUSCULAS; se normaliza para que un valor antiguo
            // en minuscula no deje de coincidir con la lista ni se vuelva a guardar asi.
            string rolUsuario = r["rol"].ToString().Trim().ToUpperInvariant();
            ListItem itemRol = ddlRol.Items.FindByValue(rolUsuario);
            if (itemRol == null)
                ddlRol.Items.Add(new ListItem(rolUsuario, rolUsuario));
            ddlRol.SelectedValue     = rolUsuario;
            ddlConductor.SelectedIndex = 0;
            if (r["idConductor"] != DBNull.Value)
            {
                ListItem itemConductor = ddlConductor.Items.FindByValue(r["idConductor"].ToString());
                if (itemConductor != null)
                    ddlConductor.SelectedValue = itemConductor.Value;
            }
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
                "UPDATE Usuarios SET activo = CASE WHEN activo = 1 THEN 0 ELSE 1 END, selloSesion = NEWID() WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));
            // Nuevo sello: las sesiones abiertas del usuario se cierran en su próxima petición.
            SesionVigenteHelper.InvalidarCache(idUsuario);

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Cambio de estado activo/inactivo por Admin Sistema");
            MostrarMensaje("Estado del usuario actualizado correctamente.", "success");
            CargarUsuarios();
        }

        private void PrepararResetPassword(int idUsuario)
        {
            object result = DbHelper.EjecutarEscalar(
                "SELECT nombreUsuario FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));
            lblUsuarioReset.Text = System.Web.HttpUtility.HtmlEncode(result?.ToString() ?? "");

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
            string rol           = (ddlRol.SelectedValue ?? "").Trim().ToUpperInvariant();
            // Recortada, como en Restablecer contraseña (el celular agrega un espacio al final).
            string contrasena    = (txtContrasena.Text ?? "").Trim();
            int idConductor;
            bool tieneConductor = int.TryParse(ddlConductor.SelectedValue, out idConductor) && idConductor > 0;
            bool esRolConductor = string.Equals(rol?.Trim(), RolesHelper.ROL_CONDUCTOR, StringComparison.OrdinalIgnoreCase)
                || string.Equals(rol?.Trim(), "CHOFER", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(nombreUsuario) || string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(rol))
            {
                MostrarMensajeModal("Todos los campos obligatorios deben ser completados.", "danger");
                AbrirModal("modalUsuario");
                return;
            }

            if (esRolConductor && !tieneConductor)
            {
                MostrarMensajeModal("Debe asociar un conductor al usuario con rol Conductor.", "danger");
                AbrirModal("modalUsuario");
                return;
            }

            if (!esRolConductor)
                tieneConductor = false;

            if (idUsuario == 0)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(nombreUsuario, @"^[A-Za-z0-9_.]{3,50}$"))
                {
                    MostrarMensajeModal("El nombre de usuario debe tener entre 3 y 50 caracteres: solo letras, números, punto y guion bajo, sin espacios.", "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
                if (string.IsNullOrEmpty(contrasena))
                {
                    MostrarMensajeModal("Debe ingresar una contraseña para el nuevo usuario.", "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
                string errorContrasena = PasswordHelper.ValidarNueva(contrasena);
                if (errorContrasena != null)
                {
                    MostrarMensajeModal(errorContrasena, "danger");
                    AbrirModal("modalUsuario");
                    return;
                }
            }

            try
            {
                if (tieneConductor)
                {
                    int asociado = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                        SELECT COUNT(*)
                        FROM Usuarios
                        WHERE idConductor = @idConductor
                          AND idUsuario <> @idUsuario",
                        DbHelper.Param("@idConductor", idConductor),
                        DbHelper.Param("@idUsuario", idUsuario)));

                    if (asociado > 0)
                    {
                        MostrarMensajeModal("El conductor seleccionado ya está asociado a otro usuario.", "danger");
                        AbrirModal("modalUsuario");
                        return;
                    }
                }

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
                    int idNuevo = Convert.ToInt32(DbHelper.EjecutarEscalar(
                        @"INSERT INTO Usuarios
                            (nombreUsuario, nombre, contrasena, rol, activo, idConductor, requiereCambioContrasena)
                          OUTPUT INSERTED.idUsuario
                          VALUES
                            (@user, @nombre, @pass, @rol, 1, @idConductor, 1)",
                        DbHelper.Param("@user",   nombreUsuario),
                        DbHelper.Param("@nombre", nombre),
                        DbHelper.Param("@pass",   hash),
                        DbHelper.Param("@rol",    rol),
                        DbHelper.Param("@idConductor", tieneConductor ? (object)idConductor : DBNull.Value)));

                    AuditoriaHelper.Registrar("INSERT", "Usuarios", idNuevo, $"Nuevo usuario creado: {nombreUsuario}, rol: {rol}");
                    MostrarMensaje($"Usuario '{nombreUsuario}' creado exitosamente.", "success");
                }
                else
                {
                    DataTable previo = DbHelper.ConsultarTabla(
                        "SELECT nombre, rol, idConductor FROM Usuarios WHERE idUsuario = @id",
                        DbHelper.Param("@id", idUsuario));
                    if (previo.Rows.Count == 0)
                        throw new Services.Common.ErrorNegocioException("El usuario ya no existe. Actualice la lista.");
                    DataRow anterior = previo.Rows[0];
                    string rolAnterior = Convert.ToString(anterior["rol"]).Trim();

                    // Nadie se cambia su propio rol (ni se quita ni se da permisos): lo hace otro administrador de sistema.
                    if (idUsuario == SecurityHelper.ObtenerIdUsuario()
                        && !string.Equals(rolAnterior, rol.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new Services.Common.ErrorNegocioException("No puede cambiar su propio rol. Pídaselo a otro administrador de sistema.");

                    DbHelper.EjecutarNonQuery(
                        // Cambiar el rol o el conductor vinculado renueva el sello: cierra sus sesiones abiertas.
                        @"UPDATE Usuarios
                          SET selloSesion = CASE WHEN ISNULL(rol, '') <> @rol
                                                   OR ISNULL(idConductor, -1) <> ISNULL(@idConductor, -1)
                                                 THEN NEWID() ELSE selloSesion END,
                              nombre = @nombre, rol = @rol, idConductor = @idConductor
                          WHERE idUsuario = @id",
                        DbHelper.Param("@nombre", nombre),
                        DbHelper.Param("@rol",    rol),
                        DbHelper.Param("@idConductor", tieneConductor ? (object)idConductor : DBNull.Value),
                        DbHelper.Param("@id",     idUsuario));
                    SesionVigenteHelper.InvalidarCache(idUsuario);

                    string Json(string n, string r, object idC) => Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        nombre = n,
                        rol = r,
                        idConductor = idC == null || idC == DBNull.Value ? (int?)null : Convert.ToInt32(idC)
                    });
                    AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario,
                        string.Equals(rolAnterior, rol.Trim(), StringComparison.OrdinalIgnoreCase)
                            ? $"Usuario actualizado: nombre={nombre}, rol={rol}"
                            : $"Usuario actualizado: nombre={nombre}, rol {rolAnterior} -> {rol}",
                        Json(Convert.ToString(anterior["nombre"]), rolAnterior, anterior["idConductor"]),
                        Json(nombre, rol, tieneConductor ? (object)idConductor : null));
                    MostrarMensaje("Usuario actualizado exitosamente.", "success");
                }

                CargarUsuarios();
            }
            catch (Exception ex)
            {
                MostrarMensajeModal("Error al guardar: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
                AbrirModal("modalUsuario");
            }
        }

        protected void btnResetPass_Click(object sender, EventArgs e)
        {
            int    idUsuario     = Convert.ToInt32(hfIdUsuarioReset.Value);
            // Recortadas las dos (se guarda recortada): una de solo espacios queda vacía y se rechaza.
            string nuevaPass     = (txtNuevaPass.Text ?? "").Trim();
            string confirmarPass = (txtConfirmarPass.Text ?? "").Trim();

            string errorNueva = PasswordHelper.ValidarNueva(nuevaPass);
            if (errorNueva != null)
            {
                MostrarMensajeReset(errorNueva, "danger");
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
                "UPDATE Usuarios SET contrasena = @hash, requiereCambioContrasena = 1, resetToken = NULL, resetTokenExpira = NULL, selloSesion = NEWID() WHERE idUsuario = @id",
                DbHelper.Param("@hash", hash),
                DbHelper.Param("@id",   idUsuario));
            // Nuevo sello: las sesiones abiertas con la contraseña anterior se cierran.
            SesionVigenteHelper.InvalidarCache(idUsuario);

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
            lblMensaje.Text     = MensajeHtml.Texto(mensaje);
            lblMensaje.CssClass = $"alert alert-{tipo} d-block mb-0";
            pnlMensaje.Visible  = true;
        }

        /// <summary>
        /// OnClientClick del botón Activar/Desactivar: "return confirm('...');" con el nombre del usuario y el
        /// efecto del cambio. SGV.confirmar (SiteMaster.js) lo muestra como modal; el texto va codificado
        /// como literal de JavaScript porque el nombre de usuario viene de la base de datos.
        /// </summary>
        protected string ConfirmarCambioEstado(object activo, object nombreUsuario)
        {
            string usuario = Convert.ToString(nombreUsuario);
            string mensaje = Convert.ToBoolean(activo)
                ? $"¿Desactivar al usuario {usuario}?\n\nNo podrá iniciar sesión hasta que lo vuelva a activar."
                : $"¿Activar al usuario {usuario}?\n\nPodrá volver a iniciar sesión.";
            return "return confirm('" + System.Web.HttpUtility.JavaScriptStringEncode(mensaje) + "');";
        }

        private void MostrarMensajeModal(string mensaje, string tipo)
        {
            lblMensajeModal.Text     = MensajeHtml.Texto(mensaje);
            lblMensajeModal.CssClass = $"alert alert-{tipo} d-block mb-0";
            pnlMensajeModal.Visible  = true;
        }

        private void MostrarMensajeReset(string mensaje, string tipo)
        {
            lblMensajeReset.Text     = MensajeHtml.Texto(mensaje);
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
