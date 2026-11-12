using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
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
        private const int MaxBytesAdjunto = 5 * 1024 * 1024;
        private static readonly string[] ExtensionesAdjunto = { ".pdf", ".jpg", ".jpeg", ".png" };

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();
            if (IsPostBack) return;

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

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
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
                    MontoSoles = Decimal(txtSoles.Text),
                    MontoDolares = Decimal(txtDolares.Text),
                    Medio = ddlMedio.SelectedValue,
                    NumeroOperacion = Texto(txtOperacion.Text, 50),
                    NombreProveedor = Texto(txtProveedor.Text, 150),
                    RucProveedor = Texto(txtRuc.Text, 11),
                    Comprobante = Texto(txtComprobante.Text, 30),
                    Descripcion = Texto(txtDescripcion.Text, 250)
                };

                string error = MovimientosViajeService.Validar(m, FechaHelper.Ahora());
                if (error != null) { MostrarMensaje(error, "warning"); return; }

                if (fuAdjunto.HasFile)
                {
                    string ext = Path.GetExtension(fuAdjunto.FileName ?? "").ToLowerInvariant();
                    if (!ExtensionesAdjunto.Contains(ext)) { MostrarMensaje("El adjunto debe ser PDF, JPG o PNG.", "warning"); return; }
                    if (fuAdjunto.PostedFile.ContentLength > MaxBytesAdjunto) { MostrarMensaje("El adjunto no puede superar 5 MB.", "warning"); return; }
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

                MostrarMensaje("Registrado. Se unirá a la liquidación del conductor cuando la administración la revise.", "success");
                txtSoles.Text = txtDolares.Text = txtOperacion.Text = txtProveedor.Text = txtRuc.Text = txtComprobante.Text = txtDescripcion.Text = "";
                CargarMovimientos();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al registrar un movimiento de dinero del viaje");
                MostrarMensaje("No se pudo registrar: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        protected void btnAnular_Click(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(hfIdAnular.Value, out int id) || id <= 0) return;
                string motivo = Texto(txtMotivoAnular.Text, 250);
                var r = MovimientosViajeService.Anular(id, SecurityHelper.ObtenerIdUsuario(), motivo);
                if (r.Exito)
                    AuditoriaHelper.Registrar("ANULAR_MOVIMIENTO_VIAJE", "MovimientoDineroViaje", id, "Anulado: " + motivo);
                MostrarMensaje(r.Mensaje, r.Exito ? "success" : "warning");
                txtMotivoAnular.Text = "";
                CargarMovimientos();
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al anular un movimiento de dinero del viaje");
                MostrarMensaje("No se pudo anular: " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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

        private static decimal Decimal(string s) =>
            decimal.TryParse((s ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? Math.Round(d, 2) : 0m;

        private static string Texto(string s, int max)
        {
            string t = (s ?? "").Trim();
            return t.Length == 0 ? null : (t.Length <= max ? t : t.Substring(0, max));
        }

        private void MostrarMensaje(string mensaje, string tipo)
        {
            pnlMensaje.Visible = true;
            lblMensaje.Text = "<div class=\"alert alert-" + tipo + " mb-0\">" + HttpUtility.HtmlEncode(mensaje) + "</div>";
        }
    }
}
