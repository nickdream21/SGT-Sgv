using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
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
    public partial class VerificarDocumento : System.Web.UI.Page
    {
        private const int MaxConsultas = 30;
        private static readonly TimeSpan VentanaConsultas = TimeSpan.FromMinutes(10);
        private static readonly ConcurrentDictionary<string, Queue<DateTime>> _consultasPorIp =
            new ConcurrentDictionary<string, Queue<DateTime>>();

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

            if (!PermitirConsulta(Request.UserHostAddress ?? ""))
            {
                MostrarNoEncontrado("Se hicieron demasiadas consultas desde su conexión. Intente nuevamente en unos minutos.");
                return;
            }

            DataSet ds;
            try
            {
                ds = ConsultarSp(codigo);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error en la verificación pública del documento {Codigo}", codigo);
                MostrarNoEncontrado("El servicio de verificación no está disponible en este momento. Intente más tarde.");
                return;
            }

            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            {
                MostrarNoEncontrado("No existe ningún documento con el código " + codigo + ". Revise que lo haya escrito tal como aparece en el documento.");
                return;
            }

            DataRow r = ds.Tables[0].Rows[0];
            bool anulada = Convert.ToBoolean(r["anulada"]);
            bool reemplazado = Convert.ToBoolean(r["reemplazado"]);
            string etapa = r["etapa"] as string;

            lblCodigo.Text = HttpUtility.HtmlEncode(codigo);
            lblDocumento.Text = HttpUtility.HtmlEncode("Orden de viaje N.° " + r["numeroOrdenViaje"] + " — " + EtapaFirma.Nombre(etapa));
            lblFirmante.Text = HttpUtility.HtmlEncode(Convert.ToString(r["nombreFirmante"]) +
                (Convert.ToString(r["rolFirmante"]) == "ADMIN" ? " (administración)" : " (conductor)"));
            lblFecha.Text = HttpUtility.HtmlEncode(FechaHelper.ConvertirDeUtc(Convert.ToDateTime(r["fechaHoraFirma"]))
                .ToString("dd/MM/yyyy HH:mm:ss") + " (hora de Perú)");
            string hash = Convert.ToString(r["hashDocumento"]).Trim();
            lblHash.Text = HttpUtility.HtmlEncode(hash);
            hfHash.Value = hash;

            if (anulada)
            {
                lblEstado.Text = "Documento auténtico, pero ANULADO: la firma fue anulada en una revisión posterior y este documento ya no tiene efecto.";
                pnlEstado.CssClass = "vd-estado vd-estado-alerta";
            }
            else if (reemplazado)
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
            if (ds.Tables.Count > 1)
            {
                foreach (DataRow f in ds.Tables[1].Rows)
                {
                    string etapaFirma = f["etapa"] as string;
                    bool esAnulacion = Convert.ToString(f["estadoFirma"]) == "A";
                    firmas.Add(new
                    {
                        Etapa = EtapaFirma.Nombre(esAnulacion ? EtapaFirma.Anulacion : etapaFirma),
                        Firmante = Convert.ToString(f["nombreFirmante"]),
                        Fecha = FechaHelper.ConvertirDeUtc(Convert.ToDateTime(f["fechaHoraFirma"])).ToString("dd/MM/yyyy HH:mm"),
                        Estado = esAnulacion ? "Motivo: " + Convert.ToString(f["motivoAnulacion"])
                               : Convert.ToBoolean(f["anulada"]) ? "Anulada" : "Vigente",
                        Codigo = f["codigoVerificacion"] == DBNull.Value ? "" : Convert.ToString(f["codigoVerificacion"])
                    });
                }
            }
            rptFirmas.DataSource = firmas;
            rptFirmas.DataBind();

            pnlResultado.Visible = true;
        }

        private static DataSet ConsultarSp(string codigo)
        {
            using (var conn = new System.Data.SqlClient.SqlConnection(DbHelper.ConnectionString))
            using (var cmd = new System.Data.SqlClient.SqlCommand("sp_LD_VerificarDocumento", conn) { CommandType = CommandType.StoredProcedure })
            using (var da = new System.Data.SqlClient.SqlDataAdapter(cmd))
            {
                cmd.Parameters.Add(DbHelper.Param("@codigoVerificacion", codigo));
                var ds = new DataSet();
                da.Fill(ds);
                return ds;
            }
        }

        private void MostrarNoEncontrado(string mensaje)
        {
            lblNoEncontrado.Text = HttpUtility.HtmlEncode(mensaje);
            pnlNoEncontrado.Visible = true;
        }

        /// <summary>Límite de consultas por IP (la página es pública): evita barrer códigos.</summary>
        private static bool PermitirConsulta(string ip)
        {
            var ahora = DateTime.UtcNow;
            var cola = _consultasPorIp.GetOrAdd(ip, _ => new Queue<DateTime>());
            lock (cola)
            {
                while (cola.Count > 0 && ahora - cola.Peek() > VentanaConsultas) cola.Dequeue();
                if (cola.Count >= MaxConsultas) return false;
                cola.Enqueue(ahora);
                return true;
            }
        }
    }
}
