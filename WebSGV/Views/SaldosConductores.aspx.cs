using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.Reportes;

namespace WebSGV.Views
{
    /// <summary>
    /// Descuentos y reintegros de la quincena (cuenta corriente del conductor): saldo de cada liquidación
    /// aprobada y registro de cómo se saldó. Lógica en <see cref="SaldosConductorService"/>.
    /// </summary>
    public partial class SaldosConductores : PaginaBase
    {
        private static readonly CultureInfo Pe = CultureInfo.GetCultureInfo("es-PE");

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
            SecurityHelper.AgregarHeadersSeguridad();
            if (!IsPostBack)
            {
                LlenarFiltros();
                Cargar();
            }
        }

        private void LlenarFiltros()
        {
            // Quincena actual y las 11 anteriores (6 meses).
            var actual = SaldosConductorService.Quincena(FechaHelper.Hoy());
            DateTime desde = actual.Item1;
            for (int i = 0; i < 12; i++)
            {
                var q = SaldosConductorService.Quincena(desde);
                ddlQuincena.Items.Add(new ListItem(
                    SaldosConductorService.NombreQuincena(q.Item1) + $" ({q.Item1:dd/MM} al {q.Item2:dd/MM})",
                    q.Item1.ToString("yyyy-MM-dd")));
                desde = q.Item1.AddDays(-1);
            }

            ddlConductor.Items.Add(new ListItem("Todos", "0"));
            foreach (DataRow r in SaldosConductorService.ConductoresConLiquidacionAprobada().Rows)
                ddlConductor.Items.Add(new ListItem(Convert.ToString(r["Nombre"]), Convert.ToString(r["idConductor"])));

            foreach (var m in SaldosConductorService.Medios)
                ddlMedio.Items.Add(new ListItem(m.Value, m.Key));
            txtFechaPago.Text = FechaHelper.Hoy().ToString("yyyy-MM-dd");
        }

        private Tuple<DateTime, DateTime> QuincenaElegida() =>
            SaldosConductorService.Quincena(DateTime.ParseExact(ddlQuincena.SelectedValue, "yyyy-MM-dd", CultureInfo.InvariantCulture));

        private DataTable Datos()
        {
            var q = QuincenaElegida();
            return SaldosConductorService.Listar(q.Item1, q.Item2, int.Parse(ddlConductor.SelectedValue),
                chkPendientesAnteriores.Checked, chkSoloPendientes.Checked);
        }

        private bool _enlazado;

        // El Repeater va sin ViewState: si ningún evento lo enlazó (un postback que no recarga), se enlaza aquí.
        protected override void OnPreRender(EventArgs e)
        {
            if (!_enlazado && !Response.IsRequestBeingRedirected)
                Cargar();
            base.OnPreRender(e);
        }

        private void Cargar()
        {
            _enlazado = true;
            DataTable dt = Datos();
            var filas = dt.Rows.Cast<DataRow>().ToList();

            var grupos = filas.GroupBy(r => Convert.ToInt32(r["idConductor"])).Select(g =>
            {
                // El neto del conductor cuenta solo lo pendiente: lo ya saldado no se vuelve a cobrar ni pagar.
                var pendientes = g.Where(r => r.IsNull("idPagoSaldo")).ToList();
                decimal netoS = pendientes.Sum(r => D(r, "SaldoSoles")), netoD = pendientes.Sum(r => D(r, "SaldoDolares"));
                return new
                {
                    IdConductor = g.Key,
                    Conductor = Convert.ToString(g.First()["Conductor"]),
                    Dni = Convert.ToString(g.First()["Dni"]),
                    HayPendientes = pendientes.Count > 0,
                    CssSentido = pendientes.Count == 0 ? EstadoUiHelper.ClaseOk : SaldosConductorService.ClaseSentido(netoS, netoD),
                    TextoTotal = pendientes.Count == 0 ? "Todo saldado"
                        : SaldosConductorService.TextoSaldo(netoS, netoD) + $" ({pendientes.Count} pend.)",
                    Filas = g.Select(r =>
                    {
                        decimal s = D(r, "SaldoSoles"), d = D(r, "SaldoDolares");
                        return new
                        {
                            IdOrdenViaje = Convert.ToInt32(r["idOrdenViaje"]),
                            Numero = Convert.ToString(r["numeroOrdenViaje"]),
                            Aprobada = r.IsNull("FechaAprobacion") ? "" : Convert.ToDateTime(r["FechaAprobacion"]).ToString("dd/MM/yyyy"),
                            Entregado = N(D(r, "EntregadoSoles")),
                            Gastos = N(D(r, "GastosSoles")),
                            Ajustes = N(D(r, "DescuentoSoles") - D(r, "ReintegroSoles")),
                            SaldoSoles = N(s),
                            SaldoDolares = N(d),
                            // Para el script (suma de lo marcado): con punto decimal y sin separador de miles.
                            SolesInvariante = s.ToString("0.00", CultureInfo.InvariantCulture),
                            DolaresInvariante = d.ToString("0.00", CultureInfo.InvariantCulture),
                            Sentido = Convert.ToString(r["Sentido"]),
                            CssSentido = SaldosConductorService.ClaseSentido(s, d),
                            Pendiente = r.IsNull("idPagoSaldo"),
                            IdPagoSaldo = r.IsNull("idPagoSaldo") ? 0 : Convert.ToInt32(r["idPagoSaldo"]),
                            Estado = Convert.ToString(r["Estado"]),
                        };
                    }).ToList(),
                };
            }).ToList();

            rptConductores.DataSource = grupos;
            rptConductores.DataBind();
            pnlVacio.Visible = grupos.Count == 0;

            // Misma cuenta que el Excel: solo pendientes, neto por conductor y por moneda.
            var tot = SaldosConductorService.Totales(dt);
            litResumen.Text = HtmlResumen(tot, grupos.Count);
        }

        /// <summary>
        /// Cifras de la página en una línea (sgv-resumen), por moneda: "A descontar S/ 60.00 y US$ 25.00 · A reintegrar
        /// S/ 25.00 · 5 liquidaciones pendientes · 3 conductores". Una moneda en cero no se nombra (los soles sí, si
        /// tampoco hay dólares).
        /// </summary>
        public static string HtmlResumen(TotalesSaldos tot, int conductores)
        {
            string Parte(string rotulo, decimal soles, decimal dolares)
            {
                var montos = new List<string>();
                if (soles != 0 || dolares == 0) montos.Add("<strong>S/ " + N(soles) + "</strong>");
                if (dolares != 0) montos.Add("<strong>US$ " + N(dolares) + "</strong>");
                return "<span>" + rotulo + " " + string.Join(" y ", montos) + "</span>";
            }
            return "<p class=\"sgv-resumen\">" +
                   Parte("A descontar", tot.DescontarSoles, tot.DescontarDolares) +
                   Parte("A reintegrar", tot.ReintegrarSoles, tot.ReintegrarDolares) +
                   "<span><strong>" + tot.Pendientes + "</strong> " + (tot.Pendientes == 1 ? "liquidación pendiente" : "liquidaciones pendientes") + "</span>" +
                   "<span><strong>" + conductores + "</strong> " + (conductores == 1 ? "conductor" : "conductores") + "</span></p>";
        }

        protected void btnBuscar_Click(object sender, EventArgs e) => Cargar();

        protected void btnRegistrar_Click(object sender, EventArgs e)
        {
            try
            {
                int idConductor = int.Parse(hfConductor.Value);
                var ids = (hfOrdenes.Value ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                if (!DateTime.TryParseExact(txtFechaPago.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fecha))
                    throw new Services.Common.ErrorNegocioException("Indica la fecha en que se pagó o descontó.");
                SaldosConductorService.RegistrarPago(idConductor, ids, fecha, ddlMedio.SelectedValue, txtOperacion.Text, txtNota.Text,
                    SesionHelper.Nombre(Session));
                txtOperacion.Text = txtNota.Text = "";
                Mostrar($"Registrado: {ids.Count} liquidación(es) saldada(s).", "success");
            }
            catch (Exception ex)
            {
                Mostrar(MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
            Cargar();
        }

        protected void btnAnular_Click(object sender, EventArgs e)
        {
            try
            {
                SaldosConductorService.AnularPago(int.Parse(hfPagoAnular.Value), txtMotivoAnular.Text, SesionHelper.Nombre(Session));
                txtMotivoAnular.Text = "";
                Mostrar("Registro anulado: sus liquidaciones vuelven a estar pendientes.", "success");
            }
            catch (Exception ex)
            {
                Mostrar(MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
            Cargar();
        }

        protected void btnExcel_Click(object sender, EventArgs e) => Enviar(false);
        protected void btnImagen_Click(object sender, EventArgs e) => Enviar(true);

        private void Enviar(bool imagen)
        {
            _enlazado = true; // la respuesta es el archivo: OnPreRender no vuelve a consultar para un Repeater que no se envía
            var q = QuincenaElegida();
            var filtros = new List<string> { SaldosConductorService.NombreQuincena(q.Item1) + $" ({q.Item1:dd/MM/yyyy} al {q.Item2:dd/MM/yyyy})" };
            if (ddlConductor.SelectedValue != "0") filtros.Add("Conductor: " + ddlConductor.SelectedItem.Text);
            if (chkPendientesAnteriores.Checked) filtros.Add("Incluye pendientes de quincenas anteriores");
            if (chkSoloPendientes.Checked) filtros.Add("Solo pendientes");

            var reporte = SaldosConductorService.Reporte(Datos(), filtros, SesionHelper.Nombre(Session));
            var empresa = EmpresaConfigHelper.ObtenerEmpresa();
            string logo = Server.MapPath("~/Content/favicon.png");
            string nombre = $"DESCUENTOS_REINTEGROS_{q.Item1:yyyyMMdd}";
            byte[] archivo = imagen ? ReporteTabularExportador.GenerarImagen(reporte, empresa, logo) : ReporteTabularExportador.GenerarExcel(reporte, empresa, logo);

            var resp = HttpContext.Current.Response;
            resp.Clear();
            resp.ContentType = imagen ? "image/png" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            resp.AddHeader("Content-Disposition", "attachment; filename=" + nombre + (imagen ? ".png" : ".xlsx"));
            resp.AddHeader("Content-Length", archivo.Length.ToString());
            resp.BinaryWrite(archivo);
            resp.Flush();
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            resp.SuppressContent = true;
        }

        private void Mostrar(string texto, string tipo)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, lblMensaje, texto, tipo, "mb-0");
        }

        private static string N(decimal v) => v.ToString("N2", Pe);
        private static decimal D(DataRow r, string c) => r.IsNull(c) ? 0m : Convert.ToDecimal(r[c]);
    }
}
