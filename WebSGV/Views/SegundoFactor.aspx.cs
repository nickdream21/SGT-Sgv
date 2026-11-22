using System;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Usuarios;

namespace WebSGV.Views
{
    /// <summary>
    /// Paso intermedio del login con segundo factor (E4-14). Página pública (SesionVigenteHelper.EsRutaPublica):
    /// aquí todavía NO hay sesión, solo el estado pendiente que dejó Login (<see cref="SegundoFactorPendiente"/>:
    /// id sin rol, 5 min). Con el código correcto se vuelven a leer los datos del usuario en la BD y se emite un
    /// ticket nuevo (<see cref="InicioSesionHelper.EmitirTicket"/>): la sesión se abandona y Login la inicia con
    /// otro id. Si el interruptor exige el factor y el usuario no lo tiene, aquí mismo lo da de alta: no puede
    /// usar nada más hasta hacerlo (no hay sesión que usar).
    /// </summary>
    public partial class SegundoFactor : PaginaBase
    {
        private SegundoFactorPendiente _pendiente;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();

            // Con una sesión ya iniciada no hay nada que verificar.
            if (SesionHelper.HaySesion(Session))
            {
                Response.Redirect(RolesHelper.UrlInicioSegunRol(SesionHelper.Rol(Session)) ?? "~/Views/Login.aspx", true);
                return;
            }

            _pendiente = SesionHelper.ObtenerSegundoFactorPendiente(Session, DateTime.UtcNow);
            if (_pendiente == null)
            {
                Response.Redirect("~/Views/Login.aspx?error=" + WebForm1.CodigoSegundoFactorVencido, true);
                return;
            }

            if (!IsPostBack)
            {
                if (_pendiente.ExigirAlta)
                    MostrarAlta(nueva: false);
                else
                {
                    pnlCodigo.Visible = true;
                    if (!SegundoFactorService.ClaveConfigurada())
                        MostrarMensaje(SegundoFactorService.MensajeNoConfigurado, "danger");
                }
            }
        }

        protected void btnVerificar_Click(object sender, EventArgs e)
        {
            if (_pendiente.ExigirAlta)
                return;

            string codigo = txtCodigo.Text;
            txtCodigo.Text = "";
            // Antes de gastar el código (paso TOTP o código de recuperación): si la cuenta ya no sirve o la BD no
            // responde, el código sigue sin usar.
            DatosLogin datos;
            if (!LeerDatos(out datos))
                return;

            ResultadoSegundoFactor r = SegundoFactorService.VerificarEntrada(_pendiente.IdUsuario, _pendiente.NombreUsuario, codigo);
            if (!r.Ok)
            {
                if (r.Reiniciar)
                {
                    SalirAlLogin();
                    return;
                }
                MostrarMensaje(r.Mensaje, "danger");
                return;
            }

            Completar(datos, null, r.PorCodigoRecuperacion ? r.CodigosRestantes : null);
        }

        protected void btnConfirmarAlta_Click(object sender, EventArgs e)
        {
            if (!_pendiente.ExigirAlta)
                return;

            string codigo = txtCodigoAlta.Text;
            txtCodigoAlta.Text = "";
            DatosLogin datos;
            if (!LeerDatos(out datos))
                return;

            ResultadoSegundoFactor r = SegundoFactorService.ConfirmarAlta(_pendiente.IdUsuario, _pendiente.NombreUsuario, codigo);
            if (!r.Ok)
            {
                MostrarMensaje(r.Mensaje, "danger");
                MostrarAlta(nueva: r.Reiniciar);
                return;
            }

            Completar(datos, r.CodigosNuevos, null);
        }

        protected void btnCancelar_Click(object sender, EventArgs e) => SalirAlLogin();

        /// <summary>
        /// Datos frescos de la BD (rol, nombre, conductor, sello), leídos antes de verificar el código. False si la
        /// BD falla (mensaje en pantalla) o la cuenta ya no está activa (vuelve al login).
        /// </summary>
        private bool LeerDatos(out DatosLogin datos)
        {
            try
            {
                datos = InicioSesionHelper.CargarDatosLogin(_pendiente.IdUsuario);
            }
            catch (Exception ex)
            {
                datos = null;
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "danger");
                return false;
            }
            if (datos == null)
            {
                SalirAlLogin();
                return false;
            }
            return true;
        }

        /// <summary>Código aceptado: ticket nuevo con los datos leídos antes de verificarlo.</summary>
        private void Completar(DatosLogin datos, string[] codigosNuevos, int? codigosRestantes)
        {
            datos.ContrasenaFueraDePolitica = _pendiente.ContrasenaFueraDePolitica;
            datos.SegundoFactorVerificado = true;
            datos.CodigosRecuperacionNuevos = codigosNuevos;
            if (codigosRestantes.HasValue && codigosRestantes.Value <= 3)
                LogSGV.Info("El usuario {IdUsuario} entró con un código de recuperación; le quedan {Restantes}",
                    _pendiente.IdUsuario, codigosRestantes.Value);

            SesionHelper.QuitarSegundoFactorPendiente(Session);
            InicioSesionHelper.EmitirTicket(Context, datos);
        }

        /// <summary>Muestra el alta: una nueva (secreto nuevo) o la pendiente (código mal escrito).</summary>
        private void MostrarAlta(bool nueva)
        {
            pnlAlta.Visible = true;
            pnlCodigo.Visible = false;
            if (!SegundoFactorService.ClaveConfigurada())
            {
                MostrarMensaje(SegundoFactorService.MensajeNoConfigurado, "danger");
                btnConfirmarAlta.Enabled = false;
                return;
            }

            AltaSegundoFactor alta = null;
            try
            {
                if (!nueva)
                    alta = SegundoFactorService.AltaPendiente(_pendiente.IdUsuario, _pendiente.NombreUsuario);
                if (alta == null)
                    alta = SegundoFactorService.IniciarAlta(_pendiente.IdUsuario, _pendiente.NombreUsuario);
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "danger");
                btnConfirmarAlta.Enabled = false;
                return;
            }

            litQr.Text = alta.QrSvg ?? "";
            lblSecreto.Text = HttpUtility.HtmlEncode(alta.SecretoAgrupado);
            lnkOtpAuth.NavigateUrl = alta.UriOtpAuth;
        }

        private void SalirAlLogin()
        {
            Session.Clear();
            Session.Abandon();
            Response.Cookies.Add(new HttpCookie("SGV_SessionId", "") { Expires = DateTime.Now.AddDays(-1), HttpOnly = true });
            Response.Redirect("~/Views/Login.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            lblMensaje.Text = MensajeHtml.Texto(mensaje);
            lblMensaje.CssClass = "alert alert-" + tipo + " d-block mb-0";
            pnlMensaje.Visible = true;
        }
    }
}
