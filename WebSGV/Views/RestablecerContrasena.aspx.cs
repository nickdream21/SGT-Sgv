using System;
using WebSGV.Helpers;
using WebSGV.Services.Usuarios;

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

            // Vigencia del enlace, política con los datos de esa cuenta, UPDATE que consume el enlace (uso
            // único y atómico), sello nuevo y auditoría: ContrasenaService (E6-16). Los errores de BD llegan
            // ya registrados como resultado.
            var r = ContrasenaService.RestablecerConToken(HashTokenActual, nuevaContrasena);
            switch (r.Resultado)
            {
                case ContrasenaService.ResultadoEnlace.Restablecida:
                    MostrarMensaje("Contraseña restablecida. Ahora puede iniciar sesión.", true);
                    pnlFormulario.Visible = false;
                    HashTokenActual = "";
                    break;
                case ContrasenaService.ResultadoEnlace.EnlaceNoVigente:
                    MostrarError("El enlace ha expirado o no es válido. Solicite uno nuevo desde la página de recuperación.");
                    break;
                case ContrasenaService.ResultadoEnlace.ErrorAlVerificar:
                    MostrarMensaje("No se pudo verificar el enlace. Intente nuevamente en unos minutos.", false);
                    break;
                case ContrasenaService.ResultadoEnlace.NoCumplePolitica:
                    MostrarMensaje(r.Mensaje, false);
                    break;
                default:
                    MostrarError("Error al restablecer la contraseña. El enlace puede haber expirado.");
                    break;
            }
        }

        protected void btnVolverLogin_Click(object sender, EventArgs e)
        {
            Response.Redirect("~/Views/Login.aspx");
        }

        /// <summary>true: vigente; false: no existe o venció; null: error de BD.</summary>
        private bool? ValidarToken(string hashToken)
        {
            try
            {
                return ContrasenaService.IdUsuarioDelToken(hashToken).HasValue;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Restablecer contraseña: error al validar el token");
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
