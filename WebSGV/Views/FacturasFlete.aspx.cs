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
        private const int MaximoBytesPdf = 10 * 1024 * 1024;
        private const int DiasAvisoVencimiento = 15;

        protected void Page_Load(object sender, EventArgs e)
        {
            SecurityHelper.ExigirRolAdmin();
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
            string usuario = Convert.ToString(Session["Nombre"]);

            foreach (var a in archivos)
            {
                string nombre = Path.GetFileName(a.FileName);
                if (!nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    resultados.Add(new ResultadoFacturaSubida { Archivo = nombre, Estado = "No se registró", Detalle = "No es un PDF." });
                    continue;
                }
                if (a.ContentLength > MaximoBytesPdf)
                {
                    resultados.Add(new ResultadoFacturaSubida { Archivo = nombre, Estado = "No se registró", Detalle = "El PDF pesa más de 10 MB." });
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
                    LogSGV.Error(ex, "Error al registrar la factura de flete {Archivo}", nombre);
                    resultados.Add(new ResultadoFacturaSubida { Archivo = nombre, Estado = "No se registró", Detalle = MensajeErrorHelper.ParaUsuario(ex) });
                }
            }

            litResultadoCarga.Text = HtmlResultadoCarga(resultados);
            pnlResultadoCarga.Visible = true;
            int ok = resultados.Count(r => r.Ok);
            MostrarMensaje($"{ok} de {resultados.Count} factura(s) registradas o al día.", ok == resultados.Count ? "success" : "warning");

            // Muestra el período de las facturas subidas si no entran en el actual.
            var fechas = resultados.Where(r => r.Ok && r.Factura?.FechaEmision != null).Select(r => r.Factura.FechaEmision.Value).ToList();
            if (fechas.Count > 0 && DateTime.TryParse(txtDesde.Text, out DateTime d) && DateTime.TryParse(txtHasta.Text, out DateTime h)
                && fechas.All(x => x < d || x > h))
                PonerMes(fechas.Min());
            Buscar();
        }

        private static string HtmlResultadoCarga(List<ResultadoFacturaSubida> resultados)
        {
            var sb = new StringBuilder("<table class=\"table table-sm ff-tabla\"><thead><tr><th>Archivo</th><th>Factura</th><th>Estado</th>" +
                                       "<th>Emisi&#xF3;n</th><th>Factura comercial</th><th class=\"ff-num\">Importe</th><th>Observaciones</th></tr></thead><tbody>");
            foreach (var r in resultados)
            {
                var f = r.Factura;
                string chip = !r.Ok ? "ff-chip-mal" : r.Estado == "Sin cambios" ? "ff-chip-gris" : "ff-chip-ok";
                sb.Append("<tr>")
                  .Append($"<td>{H(r.Archivo)}</td>")
                  .Append($"<td class=\"ff-cod\">{H(f?.NumeroFactura)}</td>")
                  .Append($"<td><span class=\"ff-chip {chip}\">{H(r.Estado)}</span></td>")
                  .Append($"<td class=\"ff-cod\">{(f?.FechaEmision.HasValue == true ? f.FechaEmision.Value.ToString("dd/MM/yyyy") : "")}</td>")
                  .Append($"<td class=\"ff-cod\">{H(f?.NumeroFacturaComercial)}</td>")
                  .Append($"<td class=\"ff-num\">{(f?.ImporteTotal.HasValue == true ? H(Monto(f.Moneda, f.ImporteTotal.Value)) : "")}</td>")
                  .Append($"<td class=\"small\">{H(r.Detalle)}</td>")
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

                litTarjetas.Text = HtmlTarjetas(datos.Filas, emitidas, hoy);
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
                LogSGV.Error(ex, "Error al conciliar las facturas de flete");
                MostrarMensaje("No se pudo obtener la conciliación. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
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
                                                          Convert.ToString(Session["Nombre"]), FechaHelper.Hoy());
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
                LogSGV.Error(ex, "Error al exportar las facturas de flete");
                Buscar();
                MostrarMensaje("No se pudo generar el Excel. " + MensajeErrorHelper.ParaUsuario(ex), "danger");
            }
        }

        // ── HTML ─────────────────────────────────────────────────────────────

        private static string HtmlTarjetas(List<FilaConciliacion> filas, List<FacturaFleteRegistrada> emitidas, DateTime hoy)
        {
            var sb = new StringBuilder();
            var facturado = emitidas.GroupBy(f => f.Moneda).OrderByDescending(g => g.Key)
                .Select(g => H(Monto(g.Key, g.Sum(x => x.ImporteTotal)))).ToList();
            sb.Append(Tarjeta("Facturado en el per&#xED;odo", facturado.Count == 0 ? "—" : string.Join("<br>", facturado),
                              $"<small>{emitidas.Count} factura(s)</small>", ""));

            int facturados = filas.Count(x => x.Estado == EstadoConciliacion.Conforme || x.Estado == EstadoConciliacion.Diferencia);
            int conformes = filas.Count(x => x.Estado == EstadoConciliacion.Conforme);
            sb.Append(Tarjeta("Pedidos conformes", $"{conformes} de {facturados}", "<small>Facturado = cargado</small>",
                              conformes == facturados ? "ff-tarjeta-ok" : ""));

            int dif = filas.Count(x => x.Estado == EstadoConciliacion.Diferencia);
            sb.Append(Tarjeta("Con diferencia", dif.ToString(), "<small>Facturado distinto de lo cargado</small>", dif > 0 ? "ff-tarjeta-mal" : "ff-tarjeta-ok"));

            var porFacturar = filas.Where(x => x.Estado == EstadoConciliacion.PorFacturar).ToList();
            var estimado = porFacturar.Where(x => x.ImporteEstimado.HasValue).GroupBy(x => x.Moneda)
                .Select(g => H("≈ " + Monto(g.Key, g.Sum(x => x.ImporteEstimado.Value)))).ToList();
            sb.Append(Tarjeta("Por facturar", porFacturar.Count + " pedido(s)",
                              estimado.Count == 0 ? "<small>—</small>" : $"<small>{string.Join(" · ", estimado)}</small>",
                              porFacturar.Count > 0 ? "ff-tarjeta-aviso" : "ff-tarjeta-ok"));

            int sinDocs = filas.Where(x => x.Estado == EstadoConciliacion.SinDocumentos).Sum(x => x.Pedido.Unidades);
            sb.Append(Tarjeta("Cargas sin documentos", sinDocs + " unidad(es)", "<small>Esperan el ZIP del correo</small>", sinDocs > 0 ? "ff-tarjeta-aviso" : "ff-tarjeta-ok"));

            int sinCarga = filas.Count(x => x.Estado == EstadoConciliacion.SinCarga);
            sb.Append(Tarjeta("Facturas sin carga", sinCarga.ToString(), "<small>Su pedido no est&#xE1; en el sistema</small>", sinCarga > 0 ? "ff-tarjeta-mal" : "ff-tarjeta-ok"));

            var porVencer = emitidas.Where(f => { int? d = FacturaFleteService.DiasParaVencer(f, hoy); return d.HasValue && d.Value >= 0 && d.Value <= DiasAvisoVencimiento; }).ToList();
            var montoVencer = porVencer.GroupBy(f => f.Moneda).Select(g => H(Monto(g.Key, g.Sum(x => x.ImporteTotal)))).ToList();
            sb.Append(Tarjeta($"Vencen en {DiasAvisoVencimiento} d&#xED;as", montoVencer.Count == 0 ? "—" : string.Join("<br>", montoVencer),
                              $"<small>{porVencer.Count} factura(s) del per&#xED;odo</small>", porVencer.Count > 0 ? "ff-tarjeta-aviso" : ""));
            return sb.ToString();
        }

        private static string Tarjeta(string etiqueta, string valorHtml, string pieHtml, string clase) =>
            $"<div class=\"col-6 col-md-4 col-xl mb-3\"><div class=\"ff-tarjeta {clase}\"><span>{etiqueta}</span>" +
            $"<strong>{valorHtml}</strong>{pieHtml}</div></div>";

        private string HtmlConciliacion(List<FilaConciliacion> filas)
        {
            if (filas.Count == 0)
                return "<div class=\"ff-vacio\">No hay pedidos facturados ni facturas en el per&#xED;odo.</div>";
            var sb = new StringBuilder("<table class=\"table table-sm ff-tabla\"><thead><tr><th>Estado</th><th>Carga</th><th>Cliente</th>" +
                "<th>Pedido</th><th>Factura comercial</th><th class=\"ff-num\">Unid.</th><th>Factura de flete</th>" +
                "<th class=\"ff-num\">Facturado</th><th class=\"ff-num\">Cargado</th><th class=\"ff-num\">Dif.</th>" +
                "<th class=\"ff-num\">Importe</th><th>Avisos</th></tr></thead><tbody>");
            foreach (var x in filas)
            {
                string clase = x.Estado == EstadoConciliacion.Conforme ? (x.Avisos.Count > 0 ? "ff-fila-aviso" : "") : "ff-fila-mal";
                sb.Append($"<tr class=\"{clase}\">")
                  .Append($"<td>{Chip(x.Estado)}</td>")
                  .Append($"<td class=\"ff-cod\">{(x.Pedido != null ? x.Pedido.FechaCarga.ToString("dd/MM/yyyy") : "—")}</td>")
                  .Append($"<td>{H(x.Pedido?.Cliente ?? x.Facturas.FirstOrDefault()?.Cliente)}</td>")
                  .Append($"<td class=\"ff-cod\">{H(x.Pedido?.NumeroPedido ?? "—")}</td>")
                  .Append($"<td class=\"ff-cod\">{H(x.Pedido?.FacturaComercial ?? x.Facturas.FirstOrDefault()?.FacturaComercial ?? "—")}</td>")
                  .Append($"<td class=\"ff-num\">{(x.Pedido != null ? x.Pedido.Unidades.ToString() : "")}</td>")
                  .Append($"<td class=\"ff-cod\">{string.Join("<br>", x.Facturas.Select(EnlaceFactura))}</td>")
                  .Append($"<td class=\"ff-num\">{Cantidad(x.CantidadFacturada, x.Unidad)}</td>")
                  .Append($"<td class=\"ff-num\">{Cantidad(x.CantidadCargada, x.Unidad)}</td>")
                  .Append($"<td class=\"ff-num\">{Diferencia(x.Diferencia)}</td>")
                  .Append($"<td class=\"ff-num\">{(x.ImporteFacturado.HasValue ? H(Monto(x.Moneda, x.ImporteFacturado.Value)) : "")}</td>")
                  .Append($"<td>{Avisos(x.Avisos)}</td>")
                  .Append("</tr>");
            }
            return sb.Append("</tbody></table>").ToString();
        }

        private static string HtmlPorFacturar(List<FilaConciliacion> filas, Dictionary<string, int> clientesSinFacturas)
        {
            var sb = new StringBuilder();
            if (filas.Count == 0)
                sb.Append("<div class=\"ff-vacio\">Todo lo cargado en el per&#xED;odo tiene su factura de flete.</div>");
            else
            {
                sb.Append("<table class=\"table table-sm ff-tabla\"><thead><tr><th>Estado</th><th>Carga</th><th>Cliente</th><th>Pedido</th>" +
                          "<th>Factura comercial</th><th class=\"ff-num\">Unid.</th><th class=\"ff-num\">Viajes</th><th class=\"ff-num\">Cargado</th>" +
                          "<th class=\"ff-num\">Estimado</th><th>Avisos</th></tr></thead><tbody>");
                foreach (var x in filas)
                {
                    sb.Append("<tr>")
                      .Append($"<td>{Chip(x.Estado)}</td>")
                      .Append($"<td class=\"ff-cod\">{x.Pedido.FechaCarga:dd/MM/yyyy}</td>")
                      .Append($"<td>{H(x.Pedido.Cliente)}</td>")
                      .Append($"<td class=\"ff-cod\">{H(x.Pedido.NumeroPedido ?? "—")}</td>")
                      .Append($"<td class=\"ff-cod\">{H(x.Pedido.FacturaComercial ?? "—")}</td>")
                      .Append($"<td class=\"ff-num\">{x.Pedido.Unidades}</td>")
                      .Append($"<td class=\"ff-num\">{x.Pedido.ViajesSeguimiento}</td>")
                      .Append($"<td class=\"ff-num\">{Cantidad(x.CantidadCargada, x.Unidad)}</td>")
                      .Append($"<td class=\"ff-num\">{(x.ImporteEstimado.HasValue ? H("≈ " + Monto(x.Moneda, x.ImporteEstimado.Value)) : "")}</td>")
                      .Append($"<td>{Avisos(x.Avisos)}</td>")
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
            var sb = new StringBuilder("<table class=\"table table-sm ff-tabla\"><thead><tr><th>Factura</th><th>Emisi&#xF3;n</th><th>Cliente</th>" +
                "<th>OS</th><th>Factura comercial</th><th>CPIC</th><th class=\"ff-num\">Cantidad</th><th class=\"ff-num\">Importe</th>" +
                "<th class=\"ff-num\">Neto pendiente</th><th>Vence</th><th class=\"ff-num\">D&#xED;as</th></tr></thead><tbody>");
            foreach (var f in facturas)
            {
                int? dias = FacturaFleteService.DiasParaVencer(f, hoy);
                string claseDias = !dias.HasValue ? "" : dias.Value < 0 ? "ff-vencida" : dias.Value <= DiasAvisoVencimiento ? "ff-por-vencer" : "";
                bool netoDistinto = f.MontoNetoPendiente.HasValue && f.MontoNetoPendiente.Value != f.ImporteTotal;
                sb.Append(netoDistinto ? "<tr class=\"ff-fila-aviso\">" : "<tr>")
                  .Append($"<td class=\"ff-cod\">{EnlaceFactura(f)}</td>")
                  .Append($"<td class=\"ff-cod\">{f.FechaEmision:dd/MM/yyyy}</td>")
                  .Append($"<td>{H(f.Cliente)}</td>")
                  .Append($"<td class=\"ff-cod\">{H(f.OrdenServicio)}</td>")
                  .Append($"<td class=\"ff-cod\">{H(f.FacturaComercial)}</td>")
                  .Append($"<td class=\"ff-cod\">{H(f.NumeroCPIC)}</td>")
                  .Append($"<td class=\"ff-num\">{(f.Cantidad.HasValue ? H(f.Cantidad.Value.ToString("N2", Pe) + " " + Abreviar(f.UnidadMedida)) : "")}</td>")
                  .Append($"<td class=\"ff-num\">{H(Monto(f.Moneda, f.ImporteTotal))}</td>")
                  .Append($"<td class=\"ff-num\">{(f.MontoNetoPendiente.HasValue ? H(Monto(f.Moneda, f.MontoNetoPendiente.Value)) : "")}" +
                          $"{(netoDistinto ? " <i class=\"fas fa-exclamation-triangle text-warning\" title=\"No coincide con el importe total\"></i>" : "")}</td>")
                  .Append($"<td class=\"ff-cod\">{(f.FechaVencimiento.HasValue ? f.FechaVencimiento.Value.ToString("dd/MM/yyyy") : "Contado")}</td>")
                  .Append($"<td class=\"ff-num {claseDias}\">{(dias.HasValue ? (dias.Value < 0 ? $"vencida hace {-dias.Value}" : dias.Value.ToString()) : "")}</td>")
                  .Append("</tr>");
            }
            return sb.Append("</tbody></table>").ToString();
        }

        private string EnlaceFactura(FacturaFleteRegistrada f)
        {
            if (string.IsNullOrEmpty(f.RutaArchivo)) return H(f.NumeroFactura);
            string url = DocumentoHelper.UrlDescarga(f.RutaArchivo);
            return $"<a href=\"{H(url)}\" target=\"_blank\" rel=\"noopener\" title=\"Ver el PDF\"><i class=\"far fa-file-pdf mr-1\"></i>{H(f.NumeroFactura)}</a>";
        }

        private static string Chip(EstadoConciliacion e)
        {
            string clase;
            switch (e)
            {
                case EstadoConciliacion.Conforme: clase = "ff-chip-ok"; break;
                case EstadoConciliacion.PorFacturar: clase = "ff-chip-aviso"; break;
                case EstadoConciliacion.SinDocumentos: clase = "ff-chip-espera"; break;
                default: clase = "ff-chip-mal"; break;
            }
            return $"<span class=\"ff-chip {clase}\">{H(FacturaFleteService.TextoEstado(e))}</span>";
        }

        private static string Avisos(List<string> avisos) =>
            avisos.Count == 0 ? "" : "<ul class=\"ff-avisos\">" + string.Concat(avisos.Select(a => $"<li>{H(a)}</li>")) + "</ul>";

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

        private void MostrarMensaje(string mensaje, string tipo)
        {
            pnlMensaje.Visible = true;
            litMensaje.Text = $"<div class='alert alert-{tipo}'>{H(mensaje)}</div>";
        }
    }
}
