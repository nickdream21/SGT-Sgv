using System;
using System.Globalization;
using System.Web;
using System.Web.SessionState;
using WebSGV.Helpers;
using WebSGV.Services.Usuarios;

namespace WebSGV.Views
{
    /// <summary>
    /// "Mi seguridad" (E4-14): alta o cambio de celular del segundo factor y códigos de recuperación nuevos, con
    /// la contraseña de la cuenta (<see cref="PropositoReautenticacion.SegundoFactor"/>). Solo los roles de
    /// <see cref="RolesHelper.ExigeSegundoFactor"/>; un conductor vuelve a su inicio. Quitar el factor no se hace
    /// aquí: lo hace otro administrador de sistema en Gestión de usuarios (o el script de emergencia).
    /// </summary>
    public partial class MiSeguridad : PaginaBase
    {
        /// <summary>
        /// Clave de sesión (no de autenticación) con los códigos recién generados (<see cref="CodigosRecuperacionPorMostrar"/>):
        /// se muestran en cada carga hasta "Ya los guardé" o 10 minutos.
        /// </summary>
        public const string ClaveCodigosNuevos = "SegundoFactorCodigosNuevos";

        /// <summary>
        /// True si esta carga muestra códigos de recuperación: Site.Master no abre encima el cambio de contraseña
        /// obligatorio (los taparía); se abre al pulsar "Ya los guardé", que recarga la página sin ellos.
        /// </summary>
        public bool MostrandoCodigosRecuperacion { get; private set; }

        /// <summary>UniqueID de "Ya los guardé": Site.Master lo deja pasar aunque haya un cambio de contraseña pendiente.</summary>
        public string UniqueIdCodigosGuardados => btnCodigosGuardados.UniqueID;

        public static void GuardarCodigosPorMostrar(HttpSessionState sesion, string[] codigos, bool porActivacion)
        {
            if (sesion == null || codigos == null || codigos.Length == 0) return;
            sesion[ClaveCodigosNuevos] = new CodigosRecuperacionPorMostrar
            {
                Codigos = codigos,
                DesdeUtc = DateTime.UtcNow,
                PorActivacion = porActivacion
            };
        }

        private CodigosRecuperacionPorMostrar CodigosPorMostrar()
        {
            var c = Session[ClaveCodigosNuevos] as CodigosRecuperacionPorMostrar;
            if (c != null && c.Vigente(DateTime.UtcNow)) return c;
            Session.Remove(ClaveCodigosNuevos);
            return null;
        }

        private const string MensajeActivada =
            "Verificación en dos pasos activada. Desde ahora, al entrar se le pedirá el código de la app.";
        private const string MensajeCodigosNuevos =
            "Códigos de recuperación nuevos generados: los anteriores ya no sirven.";

        private int IdUsuario => SesionHelper.IdUsuarioActual ?? 0;

        private string NombreUsuarioLogin =>
            Convert.ToString(Session["NombreUsuario"] ?? Session["Usuario"]) ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirSesion();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!RolesHelper.ExigeSegundoFactor(SesionHelper.RolActual))
            {
                Response.Redirect(RolesHelper.UrlInicioSegunRol(SesionHelper.RolActual) ?? "~/Views/Login.aspx", true);
                return;
            }

            // En cada carga (también en postback, donde vive el botón "Ya los guardé").
            CodigosRecuperacionPorMostrar porMostrar = CodigosPorMostrar();
            if (porMostrar != null)
                MostrarCodigos(porMostrar.Codigos);

            if (!IsPostBack)
            {
                if (porMostrar != null)
                    MostrarMensaje(porMostrar.PorActivacion ? MensajeActivada : MensajeCodigosNuevos, "success");
                CargarEstado();
            }
        }

        protected void btnCodigosGuardados_Click(object sender, EventArgs e)
        {
            Session.Remove(ClaveCodigosNuevos);
            Response.Redirect("~/Views/MiSeguridad.aspx", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected void btnEmpezarAlta_Click(object sender, EventArgs e)
        {
            if (!ConfirmarContrasena())
                return;
            if (!SegundoFactorService.ClaveConfigurada())
            {
                MostrarMensaje(SegundoFactorService.MensajeNoConfigurado, "danger");
                CargarEstado();
                return;
            }

            try
            {
                // Pulsado otra vez con un alta a medias: el mismo QR (el que quizá ya escaneó), no uno nuevo.
                MostrarAlta(SegundoFactorService.AltaPendiente(IdUsuario, NombreUsuarioLogin)
                            ?? SegundoFactorService.IniciarAlta(IdUsuario, NombreUsuarioLogin));
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
            CargarEstado();
        }

        protected void btnConfirmarAlta_Click(object sender, EventArgs e)
        {
            string codigo = txtCodigoAlta.Text;
            txtCodigoAlta.Text = "";
            ResultadoSegundoFactor r = SegundoFactorService.ConfirmarAlta(IdUsuario, NombreUsuarioLogin, codigo);
            if (r.Ok)
            {
                pnlAlta.Visible = false;
                GuardarCodigosPorMostrar(Session, r.CodigosNuevos, porActivacion: true);
                MostrarCodigos(r.CodigosNuevos);
                MostrarMensaje(MensajeActivada, "success");
            }
            else
            {
                MostrarMensaje(r.Mensaje, "danger");
                AltaSegundoFactor alta = r.Reiniciar ? null : SegundoFactorService.AltaPendiente(IdUsuario, NombreUsuarioLogin);
                if (alta != null)
                    MostrarAlta(alta);
                else
                    pnlAlta.Visible = false;
            }
            CargarEstado();
        }

        protected void btnCancelarAlta_Click(object sender, EventArgs e)
        {
            pnlAlta.Visible = false;
            CargarEstado();
        }

        protected void btnRegenerar_Click(object sender, EventArgs e)
        {
            if (!ConfirmarContrasena())
                return;
            try
            {
                string[] codigos = SegundoFactorService.RegenerarCodigos(IdUsuario, NombreUsuarioLogin);
                if (codigos == null)
                    MostrarMensaje("Primero active la verificación en dos pasos.", "warning");
                else
                {
                    GuardarCodigosPorMostrar(Session, codigos, porActivacion: false);
                    MostrarCodigos(codigos);
                    MostrarMensaje(MensajeCodigosNuevos, "success");
                }
            }
            catch (Exception ex)
            {
                MostrarMensaje(MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
            CargarEstado();
        }

        /// <summary>Contraseña de la cuenta (mismo contador que la firma y el cambio de contraseña, 5 → 15 min).</summary>
        private bool ConfirmarContrasena()
        {
            string error = ReautenticacionHelper.Verificar(IdUsuario, txtContrasena.Text, PropositoReautenticacion.SegundoFactor);
            txtContrasena.Text = "";
            if (error == null)
                return true;
            MostrarMensaje(error, "danger");
            CargarEstado();
            return false;
        }

        private void CargarEstado()
        {
            EstadoSegundoFactor estado;
            try
            {
                estado = SegundoFactorService.Obtener(IdUsuario);
            }
            catch (Exception ex)
            {
                lblEstado.Text = MensajeHtml.Texto(MensajeErrorHelper.ParaUsuario(ex));
                pnlAcciones.Visible = false;
                return;
            }

            bool activo = estado?.Activo == true;
            bool obligatorio = SegundoFactorReglas.ExigirSegundoFactor;
            if (activo)
            {
                string desde = estado.ActivadoUtc.HasValue
                    ? FechaHelper.ConvertirDeUtc(estado.ActivadoUtc.Value).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-PE"))
                    : "";
                lblEstado.Text = "<span class=\"badge badge-success mr-1\">Activa</span> "
                    + HttpUtility.HtmlEncode("Desde el " + desde + ". Códigos de recuperación sin usar: " + estado.CodigosRestantes + ".")
                    + (estado.CodigosRestantes <= 3
                        ? "<br /><span class=\"text-danger\">" + HttpUtility.HtmlEncode("Le quedan pocos códigos: genere unos nuevos.") + "</span>"
                        : "");
            }
            else
            {
                lblEstado.Text = "<span class=\"badge badge-secondary mr-1\">No activada</span> "
                    + HttpUtility.HtmlEncode(obligatorio
                        ? "Su rol debe usarla: actívela ahora."
                        : "Es opcional por ahora. Si la activa, al entrar se le pedirá el código de la app.");
            }

            if (!SegundoFactorService.ClaveConfigurada())
                lblEstado.Text += "<br /><span class=\"text-danger\">" + HttpUtility.HtmlEncode(SegundoFactorService.MensajeNoConfigurado) + "</span>";

            btnEmpezarAlta.Text = activo ? "Cambiar de celular" : "Activar la verificación en dos pasos";
            btnRegenerar.Visible = activo;
            pnlAcciones.Visible = !pnlAlta.Visible;
        }

        private void MostrarAlta(AltaSegundoFactor alta)
        {
            pnlAlta.Visible = true;
            litQr.Text = alta.QrSvg ?? "";
            lblSecreto.Text = HttpUtility.HtmlEncode(alta.SecretoAgrupado);
            lnkOtpAuth.NavigateUrl = alta.UriOtpAuth;
        }

        private void MostrarCodigos(string[] codigos)
        {
            if (codigos == null || codigos.Length == 0) return;
            rptCodigos.DataSource = codigos;
            rptCodigos.DataBind();
            pnlCodigosNuevos.Visible = true;
            MostrandoCodigosRecuperacion = true;
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            // En la página también los éxitos: hablan de los códigos de recuperación que se ven debajo.
            AvisoHelper.Pintar(lblMensaje, mensaje, tipo, "mb-0");
            pnlMensaje.Visible = true;
        }
    }
}
