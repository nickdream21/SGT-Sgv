using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Globalization;
using System.Web;
using System.Web.UI.WebControls;
using QuestPDF.Fluent;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using WebSGV.Services.Reportes;

namespace WebSGV.Views
{
    /// <summary>
    /// Reporte de cargas (menú Reportes): los despachos de CARGA con sus documentos, unidad y carga,
    /// con totales y exportación a Excel (con hoja de productos y lotes) o PDF. Solo lee.
    /// </summary>
    public partial class ReporteCargas : PaginaBase
    {
        private static readonly CultureInfo CulturaPe = CultureInfo.GetCultureInfo("es-PE");

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdminOSupervisor();
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarClientes();
                PonerMes(FechaHelper.Hoy());
                Buscar();
            }
        }

        private void CargarClientes()
        {
            ddlCliente.Items.Clear();
            ddlCliente.Items.Add(new ListItem("Todos", ""));
            foreach (DataRow r in RegistroDespachoService.ObtenerClientesActivos().Rows)
                ddlCliente.Items.Add(new ListItem(Convert.ToString(r["nombre"]).Trim(), Convert.ToString(r["idCliente"])));
        }

        private void PonerMes(DateTime dia)
        {
            var inicio = new DateTime(dia.Year, dia.Month, 1);
            txtDesde.Text = inicio.ToString("yyyy-MM-dd");
            txtHasta.Text = inicio.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
        }

        protected void btnMesActual_Click(object sender, EventArgs e)
        {
            PonerMes(FechaHelper.Hoy());
            Buscar();
        }

        protected void btnMesAnterior_Click(object sender, EventArgs e)
        {
            DateTime desde;
            DateTime referencia = DateTime.TryParse(txtDesde.Text, out desde) ? desde : FechaHelper.Hoy();
            PonerMes(referencia.AddMonths(-1));
            Buscar();
        }

        protected void btnBuscar_Click(object sender, EventArgs e) => Buscar();

        protected void btnExcel_Click(object sender, EventArgs e) => Exportar(pdf: false);

        protected void btnPdf_Click(object sender, EventArgs e) => Exportar(pdf: true);

        private bool LeerFiltros(out FiltroCargas filtro)
        {
            filtro = null;
            DateTime desde, hasta;
            if (!DateTime.TryParse(txtDesde.Text, out desde) || !DateTime.TryParse(txtHasta.Text, out hasta))
            {
                MostrarMensaje("Indique las fechas Desde y Hasta.");
                return false;
            }
            if (hasta < desde)
            {
                MostrarMensaje("La fecha Hasta debe ser igual o posterior a Desde.");
                return false;
            }
            if ((hasta - desde).TotalDays > ReporteCargasService.MaxDias)
            {
                MostrarMensaje($"El rango máximo es de {ReporteCargasService.MaxDias} días.");
                return false;
            }
            int idCliente;
            filtro = new FiltroCargas
            {
                Desde = desde,
                Hasta = hasta,
                IdCliente = int.TryParse(ddlCliente.SelectedValue, out idCliente) ? idCliente : (int?)null,
                NombreCliente = string.IsNullOrEmpty(ddlCliente.SelectedValue) ? null : ddlCliente.SelectedItem.Text,
                Pedido = txtPedido.Text,
                Texto = txtTexto.Text
            };
            pnlMensaje.Visible = false;
            return true;
        }

        private void Buscar()
        {
            FiltroCargas filtro;
            if (!LeerFiltros(out filtro)) return;
            try
            {
                DataTable dt = ReporteCargasService.Despachos(filtro);
                var tot = ReporteCargasService.Calcular(dt);

                litDespachos.Text = tot.Despachos.ToString("N0", CulturaPe);
                litPedidos.Text = tot.Pedidos.ToString("N0", CulturaPe);
                litBolsas.Text = tot.Bolsas.ToString("N0", CulturaPe);
                litKg.Text = tot.KgBruto.ToString("N0", CulturaPe);
                string facturado = "";
                if (tot.FacturadoUsd > 0) facturado = "US$ " + tot.FacturadoUsd.ToString("N2", CulturaPe);
                if (tot.FacturadoPen > 0) facturado += (facturado == "" ? "" : "  ·  ") + "S/ " + tot.FacturadoPen.ToString("N2", CulturaPe);
                litFacturado.Text = facturado == "" ? "—" : HttpUtility.HtmlEncode(facturado);

                litAvisos.Text = tot.SinDocumentos == 0 ? "" :
                    $"<div class=\"alert alert-warning\">{tot.SinDocumentos} despacho(s) todavía sin guía: programados con el Word, esperan el correo con los documentos.</div>";
                litPie.Text = $"{tot.SinViajeChofer} de {tot.Despachos} despacho(s) sin viaje del chofer (cargas registradas con los documentos del correo, " +
                              "fuera de la liquidación). Excel: incluye una hoja con cada producto y lote.";

                litGrupos.Text = HtmlGrupos(ReporteCargasService.AgruparPorPedido(dt));
                pnlResultados.Visible = true;
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al buscar en el reporte de cargas");
                MostrarMensaje("No se pudo obtener el reporte. " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        private void Exportar(bool pdf)
        {
            FiltroCargas filtro;
            if (!LeerFiltros(out filtro)) return;
            try
            {
                var reporte = ReporteCargasService.Reporte(filtro, Convert.ToString(Session["Nombre"]), conDetalle: !pdf);
                string logo = Server.MapPath("~/Content/favicon.png");
                var empresa = EmpresaConfigHelper.ObtenerEmpresa();
                byte[] contenido = pdf
                    ? ReporteTabularExportador.CrearPdf(reporte, empresa, logo).GeneratePdf()
                    : ReporteTabularExportador.GenerarExcel(reporte, empresa, logo);
                string nombre = $"Cargas_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}" + (pdf ? ".pdf" : ".xlsx");

                Response.Clear();
                Response.ClearContent();
                Response.ClearHeaders();
                Response.Buffer = true;
                Response.Charset = "";
                Response.ContentType = pdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                Response.AddHeader("Content-Disposition", "attachment; filename=" + nombre);
                Response.AddHeader("Content-Length", contenido.Length.ToString());
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.BinaryWrite(contenido);
                Response.Flush();
                Response.End();   // sin esto la página se escribe detrás del archivo
            }
            catch (System.Threading.ThreadAbortException) { /* normal: Response.End() */ }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Error al exportar el reporte de cargas");
                MostrarMensaje("No se pudo generar el archivo. " + MensajeErrorHelper.ParaUsuario(ex));
            }
        }

        /// <summary>
        /// Un &lt;details&gt; por pedido: la fila resumen (siempre visible) y, al abrirlo, la tabla de sus
        /// unidades. Así el mes entra en una pantalla (un pedido por fila en vez de una unidad por fila).
        /// </summary>
        private static string HtmlGrupos(List<ReporteCargasService.GrupoPedido> grupos)
        {
            if (grupos.Count == 0)
                return "<div class=\"text-center text-muted p-4\">No hay cargas con esos filtros.</div>";

            Func<object, string> h = v => HttpUtility.HtmlEncode(Convert.ToString(v));
            Func<object, string> n0 = v => v == null || v == DBNull.Value ? "" : Convert.ToDecimal(v).ToString("N0", CulturaPe);
            var sb = new StringBuilder();
            foreach (var g in grupos)
            {
                string fecha = g.Desde == g.Hasta ? g.Desde.ToString("dd/MM") : $"{g.Desde:dd/MM}–{g.Hasta:dd/MM}";
                string valor = g.ValorFactura.HasValue
                    ? (g.Moneda == "USD" ? "US$ " : "S/ ") + g.ValorFactura.Value.ToString("N2", CulturaPe) : "";
                sb.Append("<details class=\"rc-grupo\"><summary class=\"rc-resumen\">")
                  .Append($"<span class=\"rc-c-pedido\"><i class=\"fas fa-chevron-right rc-flecha\"></i>{h(g.Titulo)}</span>")
                  .Append($"<span class=\"rc-c-fecha\">{fecha}</span>")
                  .Append($"<span class=\"rc-c-cliente\">{h(g.Cliente)}</span>")
                  .Append($"<span class=\"rc-c-factura\">{h(g.Factura)}</span>")
                  .Append($"<span class=\"rc-c-num\"><span class=\"d-lg-none text-muted\">Unidades </span>{g.Filas.Count}</span>")
                  .Append($"<span class=\"rc-c-num\"><span class=\"d-lg-none text-muted\">Bolsas </span>{n0(g.Bolsas)}</span>")
                  .Append($"<span class=\"rc-c-num\"><span class=\"d-lg-none text-muted\">Kg </span>{n0(g.KgBruto)}</span>")
                  .Append($"<span class=\"rc-c-num rc-c-valor\">{h(valor)}</span>")
                  .Append($"<span class=\"rc-c-descarga\">{h(g.Descargas)}</span>")
                  .Append("</summary><div class=\"table-responsive\"><table class=\"table table-sm mb-0 rc-detalle\"><thead><tr>")
                  .Append("<th>Fecha</th><th>Gu&#xED;a</th><th>Conductor</th><th>Tracto</th><th>Carreta</th>")
                  .Append("<th class=\"text-right\">Bolsas</th><th class=\"text-right\">Kg bruto</th><th>Descarga</th><th>Viaje del chofer</th></tr></thead><tbody>");
                foreach (DataRow r in g.Filas)
                {
                    string viaje = h(ReporteCargasService.ViajeChofer(r));
                    if (r["ViajeChofer"] == DBNull.Value) viaje = $"<span class=\"rc-sin-viaje\">{viaje}</span>";
                    sb.Append("<tr>")
                      .Append($"<td>{Convert.ToDateTime(r["Fecha"]):dd/MM/yyyy}</td>")
                      .Append($"<td>{(r["Guia"] == DBNull.Value ? "<span class=\"text-muted\">sin guía</span>" : h(r["Guia"]))}</td>")
                      .Append($"<td>{h(r["Conductor"])}</td><td>{h(r["Tracto"])}</td><td>{h(r["Carreta"])}</td>")
                      .Append($"<td class=\"text-right\">{n0(r["Bolsas"])}</td>")
                      .Append($"<td class=\"text-right\">{(r["KgBruto"] == DBNull.Value ? "" : Convert.ToDecimal(r["KgBruto"]).ToString("N1", CulturaPe))}</td>")
                      .Append($"<td>{h(r["Descarga"])}</td><td>{viaje}</td></tr>");
                }
                sb.Append("</tbody></table></div></details>");
            }
            return sb.ToString();
        }

        private void MostrarMensaje(string mensaje)
        {
            pnlMensaje.Visible = true;
            lblMensaje.Text = $"<div class='alert alert-danger'>{HttpUtility.HtmlEncode(mensaje)}</div>";
        }
    }
}
