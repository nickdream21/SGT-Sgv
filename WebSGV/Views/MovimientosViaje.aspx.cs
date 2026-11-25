using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Common;
using WebSGV.Services.Liquidaciones;

namespace WebSGV.Views
{
    /// <summary>
    /// Dinero y pagos del viaje: la administración y monitoreo registran en el momento cada entrega
    /// de dinero al conductor (adelanto, segundo despacho, mensualidad…) y cada pago que
    /// la empresa hace directamente a un proveedor del viaje. Al aprobar la liquidación del conductor
    /// se unen a ella (LiquidacionesPendientes) y aparecen en su PDF y en su conformidad.
    /// </summary>
    public partial class MovimientosViaje : PaginaBase
    {
        /// <summary>Captura del Yape o comprobante: 5 MB (Web.config deja 6 MB por envío).</summary>
        public const int MaxBytesAdjunto = 5 * 1024 * 1024;
        public static readonly string[] ExtensionesAdjunto = ArchivoSubidoHelper.ExtensionesPdfOImagen;

        /// <summary>Error para el usuario ("Adjunto: …") o null si el archivo es un PDF/JPG/PNG de verdad y de hasta 5 MB.</summary>
        public static string ValidarAdjunto(string nombre, long largo, byte[] cabecera)
        {
            string error = ArchivoSubidoHelper.Validar(nombre, largo, cabecera, MaxBytesAdjunto, ExtensionesAdjunto);
            return error == null ? null : "Adjunto: " + error + ".";
        }

        private static string ValidarAdjunto(HttpPostedFile archivo) =>
            archivo == null || archivo.ContentLength <= 0
                ? "Adjunto: el archivo está vacío."
                : ValidarAdjunto(archivo.FileName, archivo.ContentLength, ArchivoSubidoHelper.LeerCabecera(archivo.InputStream));

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            if (!MovimientosViajeService.Habilitado)
                Response.Redirect(RolesHelper.UrlInicioSegunRol(RolesHelper.ObtenerRolActual()) ?? "~/Views/Login.aspx", true);
            SecurityHelper.AgregarHeadersSeguridad();
            if (IsPostBack) return;

            EnvioUnicoHelper.Emitir(ClaveEnvioUnico, hfEnvioUnico);
            CargarListas();
            txtFecha.Text = FechaHelper.Ahora().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
            if (int.TryParse(Request.QueryString["conductor"], out int idConductor) && ddlConductor.Items.FindByValue(idConductor.ToString()) != null)
                ddlConductor.SelectedValue = idConductor.ToString();
            CargarMovimientos();
        }

        private void CargarListas()
        {
            // Activos y, aunque estén dados de baja, los que aún tienen un viaje abierto o una
            // liquidación sin cerrar (puede haber dinero que registrarles).
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT c.idConductor, LTRIM(RTRIM(c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, ''))) AS nombre,
                       CAST(ISNULL(c.activo, 0) AS BIT) AS activo
                FROM Conductor c
                WHERE c.activo = 1
                   OR EXISTS (SELECT 1 FROM ViajesEnProgreso v WHERE v.idConductor = c.idConductor AND v.estadoViaje = 'ABIERTO')
                   OR EXISTS (SELECT 1 FROM OrdenViaje o WHERE o.idConductor = c.idConductor
                              AND o.estadoAprobacion IN ('PENDIENTE', 'REABIERTO', 'POR_CONFORMAR'))
                ORDER BY nombre");
            ddlConductor.Items.Clear();
            ddlConductor.Items.Add(new ListItem("Seleccione…", ""));
            foreach (DataRow r in dt.Rows)
            {
                string nombre = System.Text.RegularExpressions.Regex.Replace(r["nombre"].ToString(), @"\s+", " ");
                bool activo = Convert.ToBoolean(r["activo"]);
                ddlConductor.Items.Add(new ListItem(activo ? nombre : nombre + " (inactivo)", r["idConductor"].ToString()));
            }

            ddlConceptoEntrega.Items.Clear();
            foreach (var c in MovimientosViajeService.ConceptosEntrega) ddlConceptoEntrega.Items.Add(new ListItem(c.Value, c.Key));
            ddlConceptoPago.Items.Clear();
            foreach (var c in MovimientosViajeService.ConceptosPagoDirecto) ddlConceptoPago.Items.Add(new ListItem(c.Value, c.Key));
            ddlMedio.Items.Clear();
            foreach (var m in MovimientosViajeService.Medios) ddlMedio.Items.Add(new ListItem(m.Value, m.Key));
        }

        protected void ddlConductor_SelectedIndexChanged(object sender, EventArgs e) => CargarMovimientos();

        private int IdConductorSeleccionado => int.TryParse(ddlConductor.SelectedValue, out int id) ? id : 0;

        private void CargarMovimientos()
        {
            int idConductor = IdConductorSeleccionado;
            litViajeEnCurso.Text = "";
            if (idConductor <= 0)
            {
                rptMovimientos.DataSource = null;
                rptMovimientos.DataBind();
                pnlVacio.Visible = true;
                litTotales.Text = "";
                return;
            }

            object viaje = DbHelper.EjecutarEscalar(@"
                SELECT TOP 1 numeroViajeProgreso + ' (desde ' + CONVERT(VARCHAR(10), fechaInicio, 103) + ')'
                FROM ViajesEnProgreso WHERE idConductor = @id AND estadoViaje = 'ABIERTO' ORDER BY idViajeProgreso DESC",
                DbHelper.Param("@id", idConductor));
            litViajeEnCurso.Text = HttpUtility.HtmlEncode(viaje == null || viaje == DBNull.Value
                ? "Sin viaje abierto: el registro queda pendiente y se une a su próxima liquidación."
                : "Viaje en curso: " + viaje);

            var lista = MovimientosViajeService.ObtenerSinLiquidar(idConductor, incluirAnulados: true);
            rptMovimientos.DataSource = lista;
            rptMovimientos.DataBind();
            pnlVacio.Visible = lista.Count == 0;
            if (lista.Count == 0) pnlVacio.Controls.Add(new System.Web.UI.LiteralControl("No tiene entregas ni pagos sin liquidar."));

            var activos = lista.Where(m => m.Activo).ToList();
            decimal entS = activos.Where(m => m.Tipo == MovimientosViajeService.Entrega).Sum(m => m.MontoSoles);
            decimal entD = activos.Where(m => m.Tipo == MovimientosViajeService.Entrega).Sum(m => m.MontoDolares);
            decimal pagS = activos.Where(m => m.Tipo == MovimientosViajeService.PagoDirecto).Sum(m => m.MontoSoles);
            litTotales.Text = HttpUtility.HtmlEncode($"entregado S/ {entS:N2}" + (entD > 0 ? $" · US$ {entD:N2}" : "") +
                                                     (pagS > 0 ? $" · pagos directos S/ {pagS:N2}" : ""));
        }

        /// <summary>Clave del formulario de registro en <see cref="EnvioUnicoHelper"/> (E5-3).</summary>
        public const string ClaveEnvioUnico = "MovimientosViaje.Registrar";

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            // Envío único (E5-3): la respuesta lleva siempre un token nuevo (el siguiente registro legítimo
            // funciona); el recibido se consume justo antes de escribir.
            string token = hfEnvioUnico.Value;
            EnvioUnicoHelper.Emitir(ClaveEnvioUnico, hfEnvioUnico);
            // Validadores del grupo vgMovimiento (sin JS llegan aquí): el token recibido queda sin consumir.
            if (!Page.IsValid) return;
            try
            {
                if (!DateTime.TryParseExact(txtFecha.Text, new[] { "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
                {
                    MostrarMensaje("Indique la fecha y hora.", "warning");
                    return;
                }

                string tipo = rblTipo.SelectedValue == MovimientosViajeService.PagoDirecto
                    ? MovimientosViajeService.PagoDirecto : MovimientosViajeService.Entrega;
                var m = new MovimientoViaje
                {
                    Tipo = tipo,
                    IdConductor = IdConductorSeleccionado,
                    Fecha = fecha,
                    Concepto = tipo == MovimientosViajeService.PagoDirecto ? ddlConceptoPago.SelectedValue : ddlConceptoEntrega.SelectedValue,
                    MontoSoles = MontoHelper.LeerMonto(txtSoles.Text, "Soles (S/)"),
                    MontoDolares = MontoHelper.LeerMonto(txtDolares.Text, "Dólares (US$)"),
                    Medio = ddlMedio.SelectedValue,
                    NumeroOperacion = Texto(txtOperacion.Text, 50),
                    NombreProveedor = Texto(txtProveedor.Text, 150),
                    RucProveedor = Texto(txtRuc.Text, 11),
                    Comprobante = Texto(txtComprobante.Text, 30),
                    Descripcion = Texto(txtDescripcion.Text, 250)
                };

                string error = MovimientosViajeService.Validar(m, FechaHelper.Ahora());
                if (error != null) { MostrarMensaje(error, "warning"); return; }

                // Antes del adjunto y del INSERT: un POST repetido no guarda ni el archivo ni la fila.
                if (!EnvioUnicoHelper.Consumir(ClaveEnvioUnico, token))
                {
                    MostrarMensaje(EnvioUnicoHelper.MensajeYaRegistrado, "warning");
                    // Sin los montos reenviados: un clic más con el token nuevo no debe duplicar el registro.
                    txtSoles.Text = txtDolares.Text = txtOperacion.Text = txtProveedor.Text = txtRuc.Text = txtComprobante.Text = txtDescripcion.Text = "";
                    CargarMovimientos();
                    return;
                }

                if (fuAdjunto.HasFile)
                {
                    // Tipo real por la firma de los primeros bytes, no solo la extensión (B6-8).
                    string errorAdjunto = ValidarAdjunto(fuAdjunto.PostedFile);
                    if (errorAdjunto != null) { MostrarMensaje(errorAdjunto, "warning"); return; }
                    string ext = Path.GetExtension(fuAdjunto.FileName ?? "").ToLowerInvariant();
                    m.RutaAdjunto = GuardarAdjunto(fuAdjunto, ext, m);
                }

                var r = MovimientosViajeService.Registrar(m, SecurityHelper.ObtenerIdUsuario());
                if (!r.Exito)
                {
                    MostrarMensaje(r.Mensaje, "warning");
                    return;
                }

                AuditoriaHelper.Registrar("REGISTRAR_MOVIMIENTO_VIAJE", "MovimientoDineroViaje", r.IdMovimiento,
                    $"{(tipo == MovimientosViajeService.PagoDirecto ? "Pago directo" : "Entrega")} {m.Concepto} S/ {m.MontoSoles:0.00} US$ {m.MontoDolares:0.00} " +
                    $"({m.Medio}) al conductor {m.IdConductor}");

                MostrarMensaje("Registrado. Se unirá a la liquidación del conductor cuando la administración la revise.", "success", enPagina: true);
                txtSoles.Text = txtDolares.Text = txtOperacion.Text = txtProveedor.Text = txtRuc.Text = txtComprobante.Text = txtDescripcion.Text = "";
                CargarMovimientos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo registrar: " + MensajeErrorHelper.ParaUsuario(ex, "Error al registrar un movimiento de dinero del viaje"), "danger");
            }
        }

        // ---------- Validadores con condición (el cliente, en MovimientosViaje.js) ----------

        private bool EsPagoDirecto => rblTipo.SelectedValue == MovimientosViajeService.PagoDirecto;

        protected void cvProveedor_ServerValidate(object source, ServerValidateEventArgs args) =>
            args.IsValid = !EsPagoDirecto || !string.IsNullOrWhiteSpace(args.Value);

        protected void cvOperacion_ServerValidate(object source, ServerValidateEventArgs args) =>
            args.IsValid = !(ddlMedio.SelectedValue == "YAPE" || ddlMedio.SelectedValue == "TRANSFERENCIA")
                || !string.IsNullOrWhiteSpace(args.Value);

        /// <summary>
        /// Soles o dólares distinto de 0 (vacío = 0). Un monto ilegible pasa aquí: su formato lo rechaza
        /// MontoHelper.LeerMonto con el nombre del campo.
        /// </summary>
        protected void cvMonto_ServerValidate(object source, ServerValidateEventArgs args) =>
            args.IsValid = MontoNoCero(txtSoles.Text) != false || MontoNoCero(txtDolares.Text) != false;

        /// <summary>true si el monto es distinto de 0, false si es 0 o está vacío, null si no se puede leer.</summary>
        public static bool? MontoNoCero(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return false;
            if (!MontoHelper.IntentarLeerMonto(texto, out decimal monto)) return null;
            return monto != 0;
        }

        protected void btnAnular_Click(object sender, EventArgs e)
        {
            if (!Page.IsValid) return;
            try
            {
                if (!int.TryParse(hfIdAnular.Value, out int id) || id <= 0) return;
                string motivo = Texto(txtMotivoAnular.Text, 250);
                string antes = MovimientosViajeService.InstantaneaMovimiento(id);
                var r = MovimientosViajeService.Anular(id, SecurityHelper.ObtenerIdUsuario(), motivo);
                if (r.Exito)
                    AuditoriaHelper.Registrar("ANULAR_MOVIMIENTO_VIAJE", "MovimientoDineroViaje", id, "Anulado: " + motivo,
                        antes, MovimientosViajeService.InstantaneaMovimiento(id));
                MostrarMensaje(r.Mensaje, r.Exito ? "success" : "warning");
                txtMotivoAnular.Text = "";
                CargarMovimientos();
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo anular: " + MensajeErrorHelper.ParaUsuario(ex, "Error al anular un movimiento de dinero del viaje"), "danger");
            }
        }

        private string GuardarAdjunto(FileUpload fu, string ext, MovimientoViaje m)
        {
            DateTime ahora = FechaHelper.Ahora();
            string raiz = DocumentoHelper.RaizFisicaUploads(Server);
            string anio = ahora.ToString("yyyy", CultureInfo.InvariantCulture), mes = ahora.ToString("MM", CultureInfo.InvariantCulture);
            string dir = Path.Combine(raiz, "MovimientosViaje", anio, mes);
            string nombre = DocumentoHelper.NombreSeguro($"{m.Tipo}_{m.IdConductor}") + "_" + ahora.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ext;
            string fisica = Path.Combine(dir, nombre);
            if (!DocumentoHelper.EstaDentroDe(fisica, raiz)) throw new Services.Common.ErrorNegocioException("Ruta de archivo no permitida.");
            Directory.CreateDirectory(dir);
            fu.SaveAs(fisica);
            return $"~/Uploads/MovimientosViaje/{anio}/{mes}/{nombre}";
        }

        // ---------- Formato para la lista ----------

        protected string NombreConcepto(object tipo, object concepto) =>
            MovimientosViajeService.NombreConcepto(tipo as string, concepto as string);

        protected string NombreMedio(object medio) => MovimientosViajeService.NombreMedio(medio as string);

        protected string Monto(object v) => v is decimal d && d != 0 ? d.ToString("N2", CultureInfo.GetCultureInfo("es-PE")) : "—";

        protected string Detalle(object item)
        {
            var m = (MovimientoViaje)item;
            return string.Join(" · ", new[]
            {
                string.IsNullOrEmpty(m.NumeroOperacion) ? null : "op. " + m.NumeroOperacion,
                m.Tipo == MovimientosViajeService.PagoDirecto
                    ? string.Join(" ", new[] { m.NombreProveedor, string.IsNullOrEmpty(m.RucProveedor) ? null : "RUC " + m.RucProveedor, m.Comprobante }.Where(s => !string.IsNullOrEmpty(s)))
                    : null,
                m.Descripcion,
                string.IsNullOrEmpty(m.RegistradoPor) ? null : "registró " + m.RegistradoPor
            }.Where(s => !string.IsNullOrEmpty(s)));
        }

        protected string UrlAdjunto(object ruta) =>
            string.IsNullOrEmpty(ruta as string) ? null : DocumentoHelper.UrlDescarga((string)ruta);

        private static string Texto(string s, int max)
        {
            string t = (s ?? "").Trim();
            return t.Length == 0 ? null : (t.Length <= max ? t : t.Substring(0, max));
        }

        private void MostrarMensaje(string mensaje, string tipo, bool enPagina = false)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, mensaje, tipo, "mb-0", enPagina);
        }
    }
}
