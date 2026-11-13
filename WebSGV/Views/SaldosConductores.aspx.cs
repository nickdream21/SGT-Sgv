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
            SecurityHelper.ExigirRolAdminOSupervisor();
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
            foreach (DataRow r in DbHelper.ConsultarTabla(@"
                SELECT DISTINCT c.idConductor,
                       LTRIM(RTRIM(c.nombre)) + ' ' + LTRIM(RTRIM(ISNULL(c.apPaterno, ''))) AS Nombre
                FROM Conductor c JOIN OrdenViaje ov ON ov.idConductor = c.idConductor
                WHERE ov.estadoAprobacion = 'APROBADO'
                ORDER BY Nombre").Rows)
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

        private void Cargar()
        {
            DataTable dt = Datos();
            var filas = dt.Rows.Cast<DataRow>().ToList();

            var grupos = filas.GroupBy(r => Convert.ToInt32(r["idConductor"])).Select(g =>
            {
                // El neto del conductor cuenta solo lo pendiente: lo ya saldado no se vuelve a cobrar ni pagar.
                var pendientes = g.Where(r => r.IsNull("idPagoSaldo")).ToList();
                decimal netoS = pendientes.Sum(r => D(r, "SaldoSoles")), netoD = pendientes.Sum(r => D(r, "SaldoDolares"));
                string sentido = SaldosConductorService.Sentido(netoS, netoD);
                return new
                {
                    IdConductor = g.Key,
                    Conductor = Convert.ToString(g.First()["Conductor"]),
                    Dni = Convert.ToString(g.First()["Dni"]),
                    HayPendientes = pendientes.Count > 0,
                    CssSentido = Css(sentido),
                    TextoTotal = pendientes.Count == 0 ? "Todo saldado"
                        : sentido + " " + Montos(Math.Abs(netoS), Math.Abs(netoD)) + $" ({pendientes.Count} pend.)",
                    Filas = g.Select(r =>
                    {
                        string s = Convert.ToString(r["Sentido"]);
                        return new
                        {
                            IdOrdenViaje = Convert.ToInt32(r["idOrdenViaje"]),
                            Numero = Convert.ToString(r["numeroOrdenViaje"]),
                            Aprobada = r.IsNull("FechaAprobacion") ? "" : Convert.ToDateTime(r["FechaAprobacion"]).ToString("dd/MM/yyyy"),
                            Entregado = N(D(r, "EntregadoSoles")),
                            Gastos = N(D(r, "GastosSoles")),
                            Ajustes = N(D(r, "DescuentoSoles") - D(r, "ReintegroSoles")),
                            SaldoSoles = N(D(r, "SaldoSoles")),
                            SaldoDolares = N(D(r, "SaldoDolares")),
                            Sentido = s,
                            CssSentido = Css(s),
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

            var pend = filas.Where(r => r.IsNull("idPagoSaldo")).ToList();
            litDescontar.Text = "S/ " + N(pend.Where(r => D(r, "SaldoSoles") > 0).Sum(r => D(r, "SaldoSoles")));
            litReintegrar.Text = "S/ " + N(-pend.Where(r => D(r, "SaldoSoles") < 0).Sum(r => D(r, "SaldoSoles")));
            litPendientes.Text = pend.Count.ToString();
            litConductores.Text = grupos.Count.ToString();
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
                    Convert.ToString(Session["Nombre"]));
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
                SaldosConductorService.AnularPago(int.Parse(hfPagoAnular.Value), txtMotivoAnular.Text, Convert.ToString(Session["Nombre"]));
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
            var q = QuincenaElegida();
            var filtros = new List<string> { SaldosConductorService.NombreQuincena(q.Item1) + $" ({q.Item1:dd/MM/yyyy} al {q.Item2:dd/MM/yyyy})" };
            if (ddlConductor.SelectedValue != "0") filtros.Add("Conductor: " + ddlConductor.SelectedItem.Text);
            if (chkPendientesAnteriores.Checked) filtros.Add("Incluye pendientes de quincenas anteriores");
            if (chkSoloPendientes.Checked) filtros.Add("Solo pendientes");

            var reporte = SaldosConductorService.Reporte(Datos(), filtros, Convert.ToString(Session["Nombre"]));
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
            pnlMensaje.Visible = true;
            pnlMensaje.CssClass = "alert alert-" + tipo + " mb-3";
            lblMensaje.Text = HttpUtility.HtmlEncode(texto);
        }

        private static string Css(string sentido) =>
            sentido == "A descontar" ? "sgv-estado-alerta" : sentido == "A reintegrar" ? "sgv-estado-info" : "sgv-estado-ok";

        private static string Montos(decimal s, decimal d) =>
            "S/ " + N(s) + (d != 0 ? " · US$ " + N(d) : "");

        private static string N(decimal v) => v.ToString("N2", Pe);
        private static decimal D(DataRow r, string c) => r.IsNull(c) ? 0m : Convert.ToDecimal(r[c]);
    }
}
