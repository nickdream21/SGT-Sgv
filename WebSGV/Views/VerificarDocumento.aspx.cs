using System;
using System.Collections.Generic;
using System.Web;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Views
{
    /// <summary>
    /// Verificación PÚBLICA (sin sesión) de un documento firmado con el código impreso en su pie
    /// de página: muestra qué documento es, quién lo firmó y cuándo, si sigue vigente y su huella
    /// SHA-256. El navegador calcula la huella del PDF que se cargue y la compara con la registrada
    /// (el archivo no se envía al servidor). Pensada para auditorías: el auditor no necesita usuario.
    /// Uso: /Views/VerificarDocumento.aspx[?c=XXXX-XXXX-XXXX]
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5368:Set ViewStateUserKey For Classes Derived From Page",
        Justification = "Página pública sin sesión: como en Site.Master, ViewStateUserKey se omite porque el SessionID puede cambiar entre GET y POST.")]
    public partial class VerificarDocumento : System.Web.UI.Page
    {
        /// <summary>Límite de consultas por IP (la página es pública): evita barrer códigos. 30 consultas
        /// en 10 min dejan la IP 10 min sin consultar; los contadores van en la tabla IntentoAcceso
        /// (sobreviven al reciclaje), clave <see cref="ClaveConsulta"/>. Cada consulta cuenta y no se devuelve.</summary>
        private static readonly ControlIntentosLogin _limite = new ControlIntentosLogin(
            AlmacenBdIntentos.Sitio, () => DateTime.UtcNow, porIp: null,
            porCuenta: new ControlIntentosLogin.Limite(30, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));

        internal static string ClaveConsulta(string ip) =>
            "VERIF_IP_" + (string.IsNullOrWhiteSpace(ip) ? IpClienteHelper.Desconocida : ip.Trim());

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.AgregarHeadersSeguridad();
            if (IsPostBack) return;

            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            lblEmpresa.Text = HttpUtility.HtmlEncode(empresa.RazonSocial ?? "");
            lblRuc.Text = HttpUtility.HtmlEncode(empresa.Ruc ?? "");

            string c = Request.QueryString["c"];
            if (!string.IsNullOrWhiteSpace(c))
            {
                txtCodigo.Text = c.Trim();
                Verificar(c);
            }
        }

        protected void btnVerificar_Click(object sender, EventArgs e) => Verificar(txtCodigo.Text);

        private void Verificar(string codigoIngresado)
        {
            pnlResultado.Visible = false;
            pnlNoEncontrado.Visible = false;

            string codigo = CodigoVerificacion.Normalizar(codigoIngresado);
            if (codigo == null)
            {
                MostrarNoEncontrado("El código debe tener 12 letras y números (por ejemplo, ABCD-EFGH-2345), tal como aparece en el pie de página del documento.");
                return;
            }

            if (_limite.Reservar(null, ClaveConsulta(IpClienteHelper.Obtener(Request))).Rechazado)
            {
                MostrarNoEncontrado("Se hicieron demasiadas consultas desde su conexión. Intente nuevamente en unos minutos.");
                return;
            }

            DocumentoVerificado d;
            try
            {
                d = VerificacionDocumentoService.Consultar(codigo);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en la verificación pública del documento {Codigo}", codigo);
                MostrarNoEncontrado("El servicio de verificación no está disponible en este momento. Intente más tarde.");
                return;
            }

            if (d == null)
            {
                MostrarNoEncontrado("No existe ningún documento con el código " + codigo + ". Revise que lo haya escrito tal como aparece en el documento.");
                return;
            }

            lblCodigo.Text = HttpUtility.HtmlEncode(codigo);
            // Sin número: la orden ya no existe. Pasa cuando el conductor la retira
            // (sp_DC_RetirarLiquidacion, desde 2026-11-15) o cuando la administración retiró
            // liquidaciones de prueba (script del 2026-11-16); en ambos casos su firma quedó
            // anulada antes y el motivo se ve en la lista de firmas. Las firmas huérfanas
            // anteriores (sin anulación ni código de verificación) no llegan a esta página.
            string orden = d.NumeroOrdenViaje == null
                ? "Orden de viaje retirada (ver el motivo en las firmas)"
                : "Orden de viaje N.° " + d.NumeroOrdenViaje;
            lblDocumento.Text = HttpUtility.HtmlEncode(orden + " — " + EtapaFirma.Nombre(d.Etapa));
            lblFirmante.Text = HttpUtility.HtmlEncode(System.Text.RegularExpressions.Regex.Replace(d.NombreFirmante.Trim(), @"\s+", " ") +
                (d.FirmoAdministracion ? " (administración)" : " (conductor)"));
            lblFecha.Text = HttpUtility.HtmlEncode(FechaHelper.ConvertirDeUtc(d.FechaHoraFirmaUtc)
                .ToString("dd/MM/yyyy HH:mm:ss") + " (hora de Perú)");
            string hash = d.HashDocumento.Trim();
            lblHash.Text = HttpUtility.HtmlEncode(hash);
            hfHash.Value = hash;

            if (d.Anulada)
            {
                lblEstado.Text = "Documento auténtico, pero ANULADO: la firma fue anulada (ver el motivo en las firmas de la orden) y este documento ya no tiene efecto.";
                pnlEstado.CssClass = "vd-estado vd-estado-alerta";
            }
            else if (d.Reemplazado)
            {
                lblEstado.Text = "Documento auténtico. Es una versión anterior: la orden tiene un documento firmado posterior (ver las firmas de la orden).";
                pnlEstado.CssClass = "vd-estado vd-estado-info";
            }
            else
            {
                lblEstado.Text = "Documento auténtico y vigente: es el último documento firmado de la orden.";
                pnlEstado.CssClass = "vd-estado vd-estado-ok";
            }

            var firmas = new List<object>();
            foreach (FirmaVerificada f in d.Firmas)
            {
                firmas.Add(new
                {
                    Etapa = EtapaFirma.Nombre(f.EsAnulacion ? EtapaFirma.Anulacion : f.Etapa),
                    Firmante = System.Text.RegularExpressions.Regex.Replace(f.NombreFirmante.Trim(), @"\s+", " "),
                    Fecha = FechaHelper.ConvertirDeUtc(f.FechaHoraFirmaUtc).ToString("dd/MM/yyyy HH:mm"),
                    Estado = f.EsAnulacion ? "Motivo: " + f.MotivoAnulacion
                           : f.Anulada ? "Anulada" : "Vigente",
                    Codigo = f.CodigoVerificacion
                });
            }
            rptFirmas.DataSource = firmas;
            rptFirmas.DataBind();

            pnlResultado.Visible = true;
        }

        private void MostrarNoEncontrado(string mensaje)
        {
            lblNoEncontrado.Text = HttpUtility.HtmlEncode(mensaje);
            pnlNoEncontrado.Visible = true;
        }
    }
}
