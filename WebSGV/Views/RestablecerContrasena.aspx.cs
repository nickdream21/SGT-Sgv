using System;
using System.Web.UI;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    /// <summary>
    /// Destino del enlace del correo de recuperación (<c>?token=</c> en claro). En la BD solo está
    /// el SHA-256 del token (<see cref="TokenRecuperacion"/>): se busca y se consume por el hash.
    /// Los tokens inválidos por IP tienen límite (<see cref="SolicitudRecuperacion.ControlTokens"/>).
    /// </summary>
    public partial class RestablecerContrasena : PaginaBase
    {
        /// <summary>Hash del token ya validado (no el token en claro).</summary>
        private string HashTokenActual
        {
            get { return ViewState["HashTokenActual"]?.ToString() ?? ""; }
            set { ViewState["HashTokenActual"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string token = Request.QueryString["token"];

                if (string.IsNullOrEmpty(token))
                {
                    MostrarError("Enlace inválido. No se proporcionó un token de recuperación.");
                    return;
                }

                // Se reserva antes de validar (bajo candado: peticiones en paralelo no se saltan el
                // límite) y se devuelve si el token era válido o si la BD falló.
                string ip = IpClienteHelper.Obtener(Request);
                var control = SolicitudRecuperacion.ControlTokens;
                var reserva = control.Reservar(null, SolicitudRecuperacion.ClaveTokenInvalido(ip));
                if (reserva.Rechazado)
                {
                    LogSGV.Advertencia("Restablecer contraseña: IP {Ip} bloqueada por tokens inválidos", ip);
                    int minutos = (int)Math.Ceiling(reserva.SegundosRestantes / 60.0);
                    MostrarError("Demasiados enlaces no válidos. Intente nuevamente en " + minutos + " minuto(s).");
                    return;
                }

                string hash = TokenRecuperacion.Hash(token);
                bool? valido = ValidarToken(hash);
                if (valido != false)
                    control.ErrorAlValidar(reserva); // devuelve la reserva: no fue un token inválido

                if (valido == null)
                {
                    MostrarError("No se pudo verificar el enlace. Intente nuevamente en unos minutos.");
                    return;
                }

                if (valido == false)
                {
                    MostrarError("El enlace ha expirado o no es válido. Solicite uno nuevo desde la página de recuperación.");
                    return;
                }

                HashTokenActual = hash;
            }
        }

        protected void btnRestablecer_Click(object sender, EventArgs e)
        {
            string nuevaContrasena = txtNuevaContrasena.Text.Trim();
            string confirmarContrasena = txtConfirmarContrasena.Text.Trim();

            if (string.IsNullOrEmpty(nuevaContrasena) || string.IsNullOrEmpty(confirmarContrasena))
            {
                MostrarMensaje("Por favor, complete ambos campos.", false);
                return;
            }

            if (nuevaContrasena != confirmarContrasena)
            {
                MostrarMensaje("Las contraseñas no coinciden.", false);
                return;
            }

            if (string.IsNullOrEmpty(HashTokenActual))
            {
                MostrarError("Sesión expirada. Solicite un nuevo enlace de recuperación.");
                return;
            }

            // Las reglas miran el usuario, el nombre y el DNI de la cuenta del enlace.
            string errorNueva;
            try
            {
                int? idDelEnlace = IdUsuarioDelToken(HashTokenActual);
                if (!idDelEnlace.HasValue)
                {
                    MostrarError("El enlace ha expirado o no es válido. Solicite uno nuevo desde la página de recuperación.");
                    return;
                }
                errorNueva = ContrasenaNuevaHelper.ValidarParaUsuario(nuevaContrasena, idDelEnlace.Value);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Restablecer contraseña: error al revisar la política de contraseña");
                MostrarMensaje("No se pudo verificar el enlace. Intente nuevamente en unos minutos.", false);
                return;
            }
            if (errorNueva != null)
            {
                MostrarMensaje(errorNueva, false);
                return;
            }

            int? idUsuario = ActualizarContrasena(HashTokenActual, nuevaContrasena);
            if (idUsuario.HasValue)
            {
                SesionVigenteHelper.InvalidarCache(idUsuario.Value);
                AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario.Value,
                    "Contraseña restablecida mediante enlace de recuperación");
                MostrarMensaje("Contraseña restablecida. Ahora puede iniciar sesión.", true);
                pnlFormulario.Visible = false;
                HashTokenActual = "";
            }
            else
            {
                MostrarError("Error al restablecer la contraseña. El enlace puede haber expirado.");
            }
        }

        protected void btnVolverLogin_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/Login.aspx");
        }

        /// <summary>idUsuario del enlace vigente, o null si no existe o venció. Los errores de BD se propagan.</summary>
        private static int? IdUsuarioDelToken(string hashToken)
        {
            object id = DbHelper.EjecutarEscalar(
                @"SELECT TOP 1 idUsuario FROM Usuarios
                  WHERE resetToken = @Hash
                  AND resetTokenExpira > @FechaActual
                  AND activo = 1",
                DbHelper.Param("@Hash", hashToken),
                DbHelper.Param("@FechaActual", FechaHelper.Ahora()));
            if (id == null || id == DBNull.Value) return null;
            return Convert.ToInt32(id);
        }

        /// <summary>true: vigente; false: no existe o venció; null: error de BD.</summary>
        private bool? ValidarToken(string hashToken)
        {
            try
            {
                int count = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    @"SELECT COUNT(*) FROM Usuarios
                      WHERE resetToken = @Hash
                      AND resetTokenExpira > @FechaActual
                      AND activo = 1",
                    DbHelper.Param("@Hash", hashToken),
                    DbHelper.Param("@FechaActual", FechaHelper.Ahora())));
                return count > 0;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Restablecer contraseña: error al validar el token");
                return null;
            }
        }

        /// <summary>
        /// Cambia la contraseña y consume el token en un único UPDATE (uso único y atómico: dos
        /// envíos con el mismo enlace no pueden ganar ambos). Devuelve el idUsuario o null.
        /// </summary>
        private int? ActualizarContrasena(string hashToken, string nuevaContrasena)
        {
            try
            {
                string hashedPassword = PasswordHelper.HashPassword(nuevaContrasena);

                // OUTPUT ... INTO una variable de tabla: funciona aunque la tabla tenga triggers.
                object id = DbHelper.EjecutarEscalar(
                    @"DECLARE @Actualizado TABLE (idUsuario INT);
                      UPDATE Usuarios
                      SET contrasena = @NuevaContrasena,
                          resetToken = NULL,
                          resetTokenExpira = NULL,
                          -- Quien recupera por correo eligió su contraseña: ya no debe cambiarla.
                          requiereCambioContrasena = 0,
                          -- Nuevo sello: se cierran las sesiones abiertas con la contraseña anterior.
                          selloSesion = NEWID()
                      OUTPUT inserted.idUsuario INTO @Actualizado
                      WHERE resetToken = @Hash
                      AND resetTokenExpira > @FechaActual
                      AND activo = 1;
                      SELECT TOP 1 idUsuario FROM @Actualizado;",
                    DbHelper.Param("@NuevaContrasena", hashedPassword),
                    DbHelper.Param("@Hash", hashToken),
                    DbHelper.Param("@FechaActual", FechaHelper.Ahora()));

                if (id == null || id == DBNull.Value) return null;
                return Convert.ToInt32(id);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Restablecer contraseña: error al actualizar la contraseña");
                return null;
            }
        }

        private void MostrarMensaje(string mensaje, bool esExito)
        {
            // En la página también el éxito: es la indicación de qué hacer después (acceso sin sesión).
            AvisoHelper.Pintar(lblMensaje, mensaje, esExito ? AvisoHelper.Exito : AvisoHelper.Peligro);
            pnlMensaje.Visible = true;
        }

        private void MostrarError(string mensaje)
        {
            MostrarMensaje(mensaje, false);
            pnlFormulario.Visible = false;
        }

        protected global::System.Web.UI.WebControls.TextBox txtConfirmarContrasena;
        protected global::System.Web.UI.WebControls.Panel pnlMensaje;
        protected global::System.Web.UI.WebControls.Label lblMensaje;
        protected global::System.Web.UI.WebControls.Panel pnlFormulario;
        protected global::System.Web.UI.WebControls.TextBox txtNuevaContrasena;
    }
}
