using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Models.Facturas;
using WebSGV.Services.Despachos;
using WebSGV.Services.Facturas;
using WebSGV.Services.Reportes;

namespace WebSGV.Views
{
    /// <summary>
    /// Facturas de flete (menú Reportes, solo administración): se suben los PDF de SUNAT de las
    /// facturas E001 y se concilian con lo cargado en el sistema (<see cref="FacturaFleteService"/>).
    /// </summary>
    public partial class FacturasFlete : PaginaBase
    {
        private static readonly CultureInfo Pe = CultureInfo.GetCultureInfo("es-PE");
        private const int DiasAvisoVencimiento = 15;

        protected void Page_Load(object sender, EventArgs e)
        {
            RolesHelper.ValidarAccesoSeccion("FACTURAS_FLETE");
            SecurityHelper.AgregarHeadersSeguridad();

            if (!IsPostBack)
            {
                CargarClientes();
                // ?mes=yyyy-MM: llega desde el expediente del pedido (Bandeja) con el mes de emisión de su factura.
                PonerMes(DateTime.TryParseExact(Request.QueryString["mes"], "yyyy-MM", CultureInfo.InvariantCulture,
                                                DateTimeStyles.None, out DateTime mes) ? mes : FechaHelper.Hoy());
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
            DateTime referencia = DateTime.TryParse(txtDesde.Text, out DateTime desde) ? desde : FechaHelper.Hoy();
            PonerMes(referencia.AddMonths(-1));
            Buscar();
        }

        protected void btnBuscar_Click(object sender, EventArgs e) => Buscar();

        // ── Subir facturas ───────────────────────────────────────────────────

        protected void btnSubir_Click(object sender, EventArgs e)
        {
            var archivos = fuFacturas.PostedFiles?.Where(f => f != null && f.ContentLength > 0).ToList() ?? new List<HttpPostedFile>();
            if (archivos.Count == 0)
            {
                MostrarMensaje("Elija uno o varios PDF de facturas.", "warning");
                Buscar();
                return;
            }

            var resultados = new List<ResultadoFacturaSubida>();
            string raiz = DocumentoHelper.RaizFisicaUploads(Server);
            string ruc = EmpresaConfigHelper.ObtenerEmpresa().Ruc;
            string usuario = SesionHelper.Nombre(Session);

            foreach (var a in archivos)
            {
                string nombre = Path.GetFileName(a.FileName);
                // Extensión, tamaño (10 MB) y que el contenido sea de verdad un PDF.
                string error = ArchivoSubidoHelper.Validar(a, ArchivoSubidoHelper.MaxDocumento, ".pdf");
                if (error != null)
                {
                    resultados.Add(new ResultadoFacturaSubida { Archivo = nombre, Estado = "No se registró", Detalle = char.ToUpper(error[0]) + error.Substring(1) + "." });
                    continue;
                }
                try
                {
                    byte[] pdf;
                    using (var ms = new MemoryStream())
                    {
                        a.InputStream.CopyTo(ms);
                        pdf = ms.ToArray();
                    }
                    resultados.Add(FacturaFleteService.Registrar(nombre, pdf, usuario, raiz, ruc));
                }
                catch (Exception ex)
                {
                    resultados.Add(new ResultadoFacturaSubida { Archivo = nombre, Estado = "No se registró", Detalle = MensajeErrorHelper.ParaUsuario(ex, "Error al registrar la factura de flete {Archivo}", nombre) });
                }
            }

            litResultadoCarga.Text = HtmlResultadoCarga(resultados);
            pnlResultadoCarga.Visible = true;
            int ok = resultados.Count(r => r.Ok);
            MostrarMensaje($"{ok} de {resultados.Count} factura(s) registradas o al día.", ok == resultados.Count ? "success" : "warning", enPagina: true);

            // Muestra el período de las facturas subidas si no entran en el actual.
            var fechas = resultados.Where(r => r.Ok && r.Factura?.FechaEmision != null).Select(r => r.Factura.FechaEmision.Value).ToList();
            if (fechas.Count > 0 && DateTime.TryParse(txtDesde.Text, out DateTime d) && DateTime.TryParse(txtHasta.Text, out DateTime h)
                && fechas.All(x => x < d || x > h))
                PonerMes(fechas.Min());
            Buscar();
        }

        private static string HtmlResultadoCarga(List<ResultadoFacturaSubida> resultados)
        {
            var sb = new StringBuilder("<table class=\"table table-sm ff-tabla sgv-tabla-apilada\"><thead><tr><th>Archivo</th><th>Factura</th><th>Estado</th>" +
                                       "<th>Emisi&#xF3;n</th><th>Carga (factura comercial o gu&#xED;a)</th><th class=\"ff-num\">Importe</th><th>Observaciones</th></tr></thead><tbody>");
            foreach (var r in resultados)
            {
                var f = r.Factura;
                var tono = !r.Ok ? TonoEstado.Peligro : r.Estado == "Sin cambios" ? TonoEstado.Neutro : TonoEstado.Ok;
                sb.Append("<tr>")
                  .Append($"<td data-label=\"Archivo\">{H(r.Archivo)}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Factura\">{H(f?.NumeroFactura)}</td>")
                  .Append($"<td data-label=\"Estado\">{EstadoUiHelper.Sello(r.Estado, tono)}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Emisi&#xF3;n\">{(f?.FechaEmision.HasValue == true ? f.FechaEmision.Value.ToString("dd/MM/yyyy") : "")}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Carga (factura comercial o gu&#xED;a)\">{(f == null ? "" : string.Join("<br>", f.Items.Select(i => i.Referencia).Where(x => x != null).Distinct().Select(H)))}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Importe\">{(f?.ImporteTotal.HasValue == true ? H(Monto(f.Moneda, f.ImporteTotal.Value)) : "")}</td>")
                  .Append($"<td class=\"small\" data-label=\"Observaciones\">{H(r.Detalle)}</td>")
                  .Append("</tr>");
            }
            return sb.Append("</tbody></table>").ToString();
        }

        // ── Conciliación ─────────────────────────────────────────────────────

        private bool LeerFiltros(out DateTime desde, out DateTime hasta, out int? idCliente)
        {
            idCliente = int.TryParse(ddlCliente.SelectedValue, out int id) ? id : (int?)null;
            hasta = DateTime.MinValue;
            if (!DateTime.TryParse(txtDesde.Text, out desde) || !DateTime.TryParse(txtHasta.Text, out hasta))
            {
                MostrarMensaje("Indique las fechas Desde y Hasta.", "danger");
                return false;
            }
            if (hasta < desde)
            {
                MostrarMensaje("La fecha Hasta debe ser igual o posterior a Desde.", "danger");
                return false;
            }
            if ((hasta - desde).TotalDays > 366)
            {
                MostrarMensaje("El rango máximo es de un año.", "danger");
                return false;
            }
            return true;
        }

        private sealed class Datos
        {
            public List<FilaConciliacion> Filas;
            public List<FacturaFleteRegistrada> Facturas;
            public Dictionary<string, int> ClientesSinFacturas;
        }

        private Datos Consultar(DateTime desde, DateTime hasta, int? idCliente)
        {
            var pedidos = FacturaFleteService.ObtenerPedidos(desde, hasta, idCliente, out var tarifas);
            var facturas = FacturaFleteService.ObtenerFacturas(desde, hasta, idCliente, out var cargaDeComercial);
            var filas = FacturaFleteService.Conciliar(pedidos, facturas, tarifas, cargaDeComercial, desde, hasta, out var sinFacturas);
            return new Datos { Filas = filas, Facturas = facturas, ClientesSinFacturas = sinFacturas };
        }

        private void Buscar()
        {
            if (!LeerFiltros(out DateTime desde, out DateTime hasta, out int? idCliente)) return;
            try
            {
                var datos = Consultar(desde, hasta, idCliente);
                var emitidas = datos.Facturas.Where(f => f.FechaEmision.Date >= desde.Date && f.FechaEmision.Date <= hasta.Date).ToList();
                DateTime hoy = FechaHelper.Hoy();
                _lotes = FacturaFleteService.LotesPorReferencia(
                    datos.Filas.SelectMany(x => new[] { x.Referencia, x.Pedido?.Referencia })
                         .Concat(emitidas.SelectMany(FacturaFleteService.Referencias)));

                litResumen.Text = HtmlResumen(datos.Filas, emitidas, hoy);
                litConciliacion.Text = HtmlConciliacion(datos.Filas.Where(x => x.Estado != EstadoConciliacion.PorFacturar
                                                                              && x.Estado != EstadoConciliacion.SinDocumentos).ToList());
                litPorFacturar.Text = HtmlPorFacturar(datos.Filas.Where(x => x.Estado == EstadoConciliacion.PorFacturar
                                                                            || x.Estado == EstadoConciliacion.SinDocumentos).ToList(),
                                                      datos.ClientesSinFacturas);
                litFacturas.Text = HtmlFacturas(emitidas, hoy);
                litPieConciliacion.Text = "Conforme: lo facturado coincide con lo cargado (peso neto de las gu&#xED;as, o unidades si se factura por viaje). " +
                                          "Diferencia: revise el pedido (unidad que falta registrar o facturada de m&#xE1;s). " +
                                          "Factura sin carga: falta registrar los documentos (ZIP) de ese pedido.";
                pnlResultados.Visible = true;
            }
            catch (Exception ex)
            {
                MostrarMensaje("No se pudo obtener la conciliación. " + MensajeErrorHelper.ParaUsuario(ex, "Error al conciliar las facturas de flete"), "danger");
            }
        }

        protected void btnExcel_Click(object sender, EventArgs e)
        {
            if (!LeerFiltros(out DateTime desde, out DateTime hasta, out int? idCliente)) return;
            try
            {
                var datos = Consultar(desde, hasta, idCliente);
                string cliente = string.IsNullOrEmpty(ddlCliente.SelectedValue) ? null : ddlCliente.SelectedItem.Text;
                var reporte = FacturaFleteService.Reporte(datos.Filas, datos.Facturas, desde, hasta, cliente,
                                                          SesionHelper.Nombre(Session), FechaHelper.Hoy());
                byte[] contenido = ReporteTabularExportador.GenerarExcel(reporte, EmpresaConfigHelper.ObtenerEmpresa(),
                                                                         Server.MapPath("~/Content/favicon.png"));
                string nombre = $"FacturasFlete_{desde:yyyyMMdd}_{hasta:yyyyMMdd}.xlsx";

                Response.Clear();
                Response.ClearContent();
                Response.ClearHeaders();
                Response.Buffer = true;
                Response.Charset = "";
                Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                Response.AddHeader("Content-Disposition", "attachment; filename=" + nombre);
                Response.AddHeader("Content-Length", contenido.Length.ToString());
                Response.Cache.SetCacheability(HttpCacheability.NoCache);
                Response.BinaryWrite(contenido);
                Response.Flush();
                Response.End();
            }
            catch (System.Threading.ThreadAbortException) { /* normal: Response.End() */ }
            catch (Exception ex)
            {
                string mensaje = MensajeErrorHelper.ParaUsuario(ex, "Error al exportar las facturas de flete");
                Buscar();
                MostrarMensaje("No se pudo generar el Excel. " + mensaje, "danger");
            }
        }

        // ── HTML ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Cifras del período en una línea (sgv-resumen, E5-17) en vez de siete tarjetas: facturado, conformes, con
        /// diferencia, por facturar, sin documentos, facturas sin carga y lo que vence pronto. Lo que pide revisar
        /// lleva su número en rojo (ff-mal) o ámbar (ff-aviso).
        /// </summary>
        public static string HtmlResumen(List<FilaConciliacion> filas, List<FacturaFleteRegistrada> emitidas, DateTime hoy)
        {
            var sb = new StringBuilder("<p class=\"sgv-resumen\">");
            void Parte(string html) => sb.Append("<span>").Append(html).Append("</span>");
            string Num(int n, string clase) => $"<strong{(clase == null ? "" : $" class=\"{clase}\"")}>{n.ToString("N0", Pe)}</strong>";
            string Montos(IEnumerable<IGrouping<string, decimal>> grupos, string prefijo) =>
                string.Join(" y ", grupos.OrderByDescending(g => g.Key).Select(g => "<strong>" + H(prefijo + Monto(g.Key, g.Sum())) + "</strong>"));

            var facturado = Montos(emitidas.GroupBy(f => f.Moneda, f => f.ImporteTotal), "");
            Parte(emitidas.Count == 0 ? "Sin facturas emitidas en el per&#xED;odo"
                : $"Facturado {facturado} ({Plural(emitidas.Count, "factura", "facturas")})");

            int facturados = filas.Count(x => x.Estado == EstadoConciliacion.Conforme || x.Estado == EstadoConciliacion.Diferencia);
            int conformes = filas.Count(x => x.Estado == EstadoConciliacion.Conforme);
            Parte($"{Num(conformes, null)} de {Num(facturados, null)} {(facturados == 1 ? "pedido conforme" : "pedidos conformes")}");

            int dif = filas.Count(x => x.Estado == EstadoConciliacion.Diferencia);
            if (dif > 0) Parte($"{Num(dif, "ff-mal")} con diferencia");

            var porFacturar = filas.Where(x => x.Estado == EstadoConciliacion.PorFacturar).ToList();
            if (porFacturar.Count > 0)
            {
                string estimado = Montos(porFacturar.Where(x => x.ImporteEstimado.HasValue)
                    .GroupBy(x => x.Moneda, x => x.ImporteEstimado.Value), "≈ ");
                Parte($"{Num(porFacturar.Count, "ff-aviso")} {(porFacturar.Count == 1 ? "pedido" : "pedidos")} por facturar" +
                      (estimado.Length > 0 ? " (" + estimado + ")" : ""));
            }

            int sinDocs = filas.Where(x => x.Estado == EstadoConciliacion.SinDocumentos).Sum(x => x.Pedido.Unidades);
            if (sinDocs > 0) Parte($"{Num(sinDocs, "ff-aviso")} {(sinDocs == 1 ? "unidad" : "unidades")} sin documentos");

            int sinCarga = filas.Count(x => x.Estado == EstadoConciliacion.SinCarga);
            if (sinCarga > 0) Parte($"{Num(sinCarga, "ff-mal")} {(sinCarga == 1 ? "factura" : "facturas")} sin carga");

            var porVencer = emitidas.Where(f => { int? d = FacturaFleteService.DiasParaVencer(f, hoy); return d.HasValue && d.Value >= 0 && d.Value <= DiasAvisoVencimiento; }).ToList();
            if (porVencer.Count > 0)
                Parte($"Vencen en {DiasAvisoVencimiento} d&#xED;as {Montos(porVencer.GroupBy(f => f.Moneda, f => f.ImporteTotal), "")} " +
                      $"({Plural(porVencer.Count, "factura", "facturas")})");
            return sb.Append("</p>").ToString();
        }

        private static string Plural(int n, string uno, string varios) => n.ToString("N0", Pe) + " " + (n == 1 ? uno : varios);

        private string HtmlConciliacion(List<FilaConciliacion> filas)
        {
            if (filas.Count == 0)
                return "<div class=\"ff-vacio\">No hay pedidos facturados ni facturas en el per&#xED;odo.</div>";
            var sb = new StringBuilder("<table class=\"table table-sm ff-tabla sgv-tabla-apilada\"><thead><tr><th>Estado</th><th>Carga</th><th>Cliente</th>" +
                "<th>Pedido</th><th>Factura comercial / gu&#xED;a</th><th class=\"ff-num\">Unid.</th><th>Factura de flete</th>" +
                "<th class=\"ff-num\">Facturado</th><th class=\"ff-num\">Cargado</th><th class=\"ff-num\">Dif.</th>" +
                "<th class=\"ff-num\">Importe</th><th class=\"ff-col-avisos\">Avisos</th></tr></thead><tbody>");
            foreach (var x in filas)
            {
                bool conAvisos = x.Avisos.Any(a => !EsInformativo(a));
                string clase = x.Estado == EstadoConciliacion.Conforme ? (conAvisos ? "ff-fila-aviso" : "") : "ff-fila-mal";
                string estado = x.Estado == EstadoConciliacion.Conforme && !conAvisos ? "ok" : "revisar";
                sb.Append($"<tr class=\"{clase}\" data-estado=\"{estado}\">")
                  .Append($"<td data-label=\"Estado\">{Chip(x.Estado)}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Carga\">{(x.Pedido != null ? x.Pedido.FechaCarga.ToString("dd/MM/yyyy") : "—")}</td>")
                  .Append(Cliente(x.Pedido?.Cliente ?? x.Facturas.FirstOrDefault()?.Cliente))
                  .Append($"<td class=\"ff-cod\" data-label=\"Pedido\">{EnlaceExpediente(x.Pedido?.NumeroPedido ?? "—", x.Referencia ?? x.Pedido?.Referencia)}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Factura comercial / gu&#xED;a\">{H(x.Referencia ?? "—")}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Unid.\">{(x.Pedido != null ? x.Pedido.Unidades.ToString() : "")}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Factura de flete\">{string.Join("<br>", x.Facturas.Select(EnlaceFactura))}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Facturado\">{Cantidad(x.CantidadFacturada, x.Unidad)}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Cargado\">{Cantidad(x.CantidadCargada, x.Unidad)}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Dif.\">{Diferencia(x.Diferencia)}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Importe\">{(x.ImporteFacturado.HasValue ? H(Monto(x.Moneda, x.ImporteFacturado.Value)) : "")}</td>")
                  .Append($"<td data-label=\"Avisos\">{Avisos(x.Avisos)}</td>")
                  .Append("</tr>");
            }
            return sb.Append("</tbody></table>").ToString();
        }

        private string HtmlPorFacturar(List<FilaConciliacion> filas, Dictionary<string, int> clientesSinFacturas)
        {
            var sb = new StringBuilder();
            if (filas.Count == 0)
                sb.Append("<div class=\"ff-vacio\">Todo lo cargado en el per&#xED;odo tiene su factura de flete.</div>");
            else
            {
                sb.Append("<table class=\"table table-sm ff-tabla sgv-tabla-apilada\"><thead><tr><th>Estado</th><th>Carga</th><th>Cliente</th><th>Pedido</th>" +
                          "<th>Factura comercial / gu&#xED;a</th><th class=\"ff-num\">Unid.</th><th class=\"ff-num\">Viajes</th><th class=\"ff-num\">Cargado</th>" +
                          "<th class=\"ff-num\">Estimado</th><th class=\"ff-col-avisos\">Avisos</th></tr></thead><tbody>");
                foreach (var x in filas)
                {
                    sb.Append("<tr data-estado=\"revisar\">")
                      .Append($"<td data-label=\"Estado\">{Chip(x.Estado)}</td>")
                      .Append($"<td class=\"ff-cod\" data-label=\"Carga\">{x.Pedido.FechaCarga:dd/MM/yyyy}</td>")
                      .Append(Cliente(x.Pedido.Cliente))
                      .Append($"<td class=\"ff-cod\" data-label=\"Pedido\">{EnlaceExpediente(x.Pedido.NumeroPedido ?? "—", x.Pedido.Referencia)}</td>")
                      .Append($"<td class=\"ff-cod\" data-label=\"Factura comercial / gu&#xED;a\">{H(x.Pedido.Referencia ?? "—")}</td>")
                      .Append($"<td class=\"ff-num\" data-label=\"Unid.\">{x.Pedido.Unidades}</td>")
                      .Append($"<td class=\"ff-num\" data-label=\"Viajes\">{x.Pedido.ViajesSeguimiento}</td>")
                      .Append($"<td class=\"ff-num\" data-label=\"Cargado\">{Cantidad(x.CantidadCargada, x.Unidad)}</td>")
                      .Append($"<td class=\"ff-num\" data-label=\"Estimado\">{(x.ImporteEstimado.HasValue ? H("≈ " + Monto(x.Moneda, x.ImporteEstimado.Value)) : "")}</td>")
                      .Append($"<td data-label=\"Avisos\">{Avisos(x.Avisos)}</td>")
                      .Append("</tr>");
                }
                sb.Append("</tbody>");
                var totales = filas.Where(x => x.ImporteEstimado.HasValue).GroupBy(x => x.Moneda).ToList();
                if (totales.Count > 0)
                    sb.Append("<tfoot><tr><th colspan=\"8\">Total estimado por facturar</th>" +
                              $"<th class=\"ff-num\">{string.Join("<br>", totales.Select(g => H("≈ " + Monto(g.Key, g.Sum(x => x.ImporteEstimado.Value)))))}</th><th></th></tr></tfoot>");
                sb.Append("</table>");
            }
            if (clientesSinFacturas.Count > 0)
                sb.Append("<div class=\"ff-vacio small\">Sin comparar (el cliente no tiene facturas de flete registradas): " +
                          H(string.Join(", ", clientesSinFacturas.Select(kv => $"{kv.Key} ({kv.Value} unidad(es))"))) + ".</div>");
            return sb.ToString();
        }

        private string HtmlFacturas(List<FacturaFleteRegistrada> facturas, DateTime hoy)
        {
            if (facturas.Count == 0)
                return "<div class=\"ff-vacio\">No hay facturas de flete emitidas en el per&#xED;odo. S&#xFA;balas con \"Leer y registrar\".</div>";
            var sb = new StringBuilder("<table class=\"table table-sm ff-tabla sgv-tabla-apilada\"><thead><tr><th>Factura</th><th>Emisi&#xF3;n</th><th>Cliente</th>" +
                "<th>OS</th><th>Factura comercial / gu&#xED;a</th><th>CPIC</th><th class=\"ff-num\">Cantidad</th><th class=\"ff-num\">Importe</th>" +
                "<th class=\"ff-num\">Neto pendiente</th><th>Vence</th><th class=\"ff-num\">D&#xED;as</th></tr></thead><tbody>");
            foreach (var f in facturas)
            {
                int? dias = FacturaFleteService.DiasParaVencer(f, hoy);
                string claseDias = !dias.HasValue ? "" : dias.Value < 0 ? "ff-vencida" : dias.Value <= DiasAvisoVencimiento ? "ff-por-vencer" : "";
                bool netoDistinto = FacturaFleteService.NetoNoCuadra(f);
                bool revisar = netoDistinto || (dias.HasValue && dias.Value <= DiasAvisoVencimiento);
                sb.Append($"<tr class=\"{(netoDistinto ? "ff-fila-aviso" : "")}\" data-estado=\"{(revisar ? "revisar" : "ok")}\">")
                  .Append($"<td class=\"ff-cod\" data-label=\"Factura\">{EnlaceFactura(f)}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Emisi&#xF3;n\">{f.FechaEmision:dd/MM/yyyy}</td>")
                  .Append(Cliente(f.Cliente))
                  .Append($"<td class=\"ff-cod\" data-label=\"OS\">{H(f.OrdenServicio)}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Factura comercial / gu&#xED;a\">{string.Join("<br>", FacturaFleteService.Referencias(f).Select(r => EnlaceExpediente(r, r)))}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"CPIC\">{string.Join("<br>", FacturaFleteService.Cpics(f).Select(H))}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Cantidad\">{(f.Cantidad.HasValue ? H(f.Cantidad.Value.ToString("N2", Pe) + " " + Abreviar(f.UnidadMedida)) : "")}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Importe\">{H(Monto(f.Moneda, f.ImporteTotal))}</td>")
                  .Append($"<td class=\"ff-num\" data-label=\"Neto pendiente\">{(f.MontoNetoPendiente.HasValue ? H(Monto(f.Moneda, f.MontoNetoPendiente.Value)) : "")}" +
                          $"{(netoDistinto ? " <i class=\"fas fa-exclamation-triangle text-warning\" title=\"No coincide con el importe total\"></i>" : "")}</td>")
                  .Append($"<td class=\"ff-cod\" data-label=\"Vence\">{(f.FechaVencimiento.HasValue ? f.FechaVencimiento.Value.ToString("dd/MM/yyyy") : "Contado")}</td>")
                  .Append($"<td class=\"ff-num {claseDias}\" data-label=\"D&#xED;as\">{(dias.HasValue ? (dias.Value < 0 ? $"vencida hace {-dias.Value}" : dias.Value.ToString()) : "")}</td>")
                  .Append("</tr>");
            }
            return sb.Append("</tbody></table>").ToString();
        }

        /// <summary>Lote de la primera carga de cada referencia (factura comercial o guía), para abrir su expediente.</summary>
        private Dictionary<string, string> _lotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// El texto como enlace al expediente del pedido en la Bandeja (todos sus documentos: guías, GRE, CPIC, MCI).
        /// Sin carga en el sistema queda como texto.
        /// </summary>
        private string EnlaceExpediente(string texto, string referencia)
        {
            if (string.IsNullOrWhiteSpace(referencia) || !_lotes.TryGetValue(referencia.Trim(), out string lote))
                return H(texto);
            if (texto == "—") texto = "Expediente";   // carga sin número de pedido (cliente que factura por guía)
            string url = ResolveUrl("~/Views/BandejaDocumentos.aspx") + "?lote=" + HttpUtility.UrlEncode(lote);
            return $"<a href=\"{H(url)}\" title=\"Ver el expediente: todos los documentos del pedido\">" +
                   $"{H(texto)}</a>";
        }

        private string EnlaceFactura(FacturaFleteRegistrada f)
        {
            if (string.IsNullOrEmpty(f.RutaArchivo)) return H(f.NumeroFactura);
            string url = DocumentoHelper.UrlDescarga(f.RutaArchivo);
            return $"<a href=\"{H(url)}\" target=\"_blank\" rel=\"noopener\" title=\"Ver el PDF\">{H(f.NumeroFactura)}</a>";
        }

        private static string Chip(EstadoConciliacion e)
        {
            TonoEstado tono;
            switch (e)
            {
                case EstadoConciliacion.Conforme: tono = TonoEstado.Ok; break;
                case EstadoConciliacion.PorFacturar: tono = TonoEstado.Alerta; break;
                case EstadoConciliacion.SinDocumentos: tono = TonoEstado.Info; break;
                default: tono = TonoEstado.Peligro; break;
            }
            return EstadoUiHelper.Sello(FacturaFleteService.TextoEstado(e), tono);
        }

        /// <summary>Celda del cliente en una línea (el nombre completo en el título).</summary>
        private static string Cliente(string nombre) => $"<td class=\"ff-cliente\" data-label=\"Cliente\" title=\"{H(nombre)}\">{H(nombre)}</td>";

        /// <summary>Nota que no pide revisar nada (la carga se cobró por tramos a dos clientes, como debe ser).</summary>
        private static bool EsInformativo(string aviso) => aviso != null && aviso.StartsWith("Facturado por tramos", StringComparison.Ordinal);

        private static string Avisos(List<string> avisos) =>
            avisos.Count == 0 ? "" : "<ul class=\"ff-avisos\">" +
                string.Concat(avisos.Select(a => EsInformativo(a) ? $"<li class=\"ff-aviso-info\">{H(a)}</li>" : $"<li>{H(a)}</li>")) + "</ul>";

        private static string Cantidad(decimal? v, string unidad) =>
            v.HasValue ? H(v.Value.ToString(unidad == "t" ? "N2" : "N0", Pe) + " " + unidad) : "";

        private static string Diferencia(decimal? v)
        {
            if (!v.HasValue || v.Value == 0) return v.HasValue ? "0" : "";
            string clase = v.Value > 0 ? "ff-dif-mas" : "ff-dif-menos";
            return $"<span class=\"{clase}\">{(v.Value > 0 ? "+" : "")}{H(v.Value.ToString("N2", Pe))}</span>";
        }

        private static string Abreviar(string unidad) =>
            FacturaFleteService.FacturaPorToneladas(unidad) == true ? "t" : (unidad ?? "").ToLowerInvariant();

        private static string Monto(string moneda, decimal v) => (moneda == "PEN" ? "S/ " : "US$ ") + v.ToString("N2", Pe);

        private static string H(string s) => HttpUtility.HtmlEncode(s ?? "");

        private void MostrarMensaje(string mensaje, string tipo, bool enPagina = false)
        {
            AvisoHelper.Mostrar(Page, pnlMensaje, litMensaje, mensaje, tipo, enPagina: enPagina);
        }
    }
}
