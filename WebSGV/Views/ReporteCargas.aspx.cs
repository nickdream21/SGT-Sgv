using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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

                DateTime antDesde, antHasta;
                ReporteCargasService.PeriodoAnterior(filtro.Desde, filtro.Hasta, FechaHelper.Hoy(), out antDesde, out antHasta);
                var filtroAnterior = new FiltroCargas
                {
                    Desde = antDesde, Hasta = antHasta, IdCliente = filtro.IdCliente,
                    Pedido = filtro.Pedido, Texto = filtro.Texto
                };
                var ant = ReporteCargasService.Calcular(ReporteCargasService.Despachos(filtroAnterior));
                var docs = ReporteCargasService.ResumenDocumental(dt, FechaHelper.Hoy());
                var periodos = ReporteCargasService.ResumenPorPeriodo(dt, filtro.Desde, filtro.Hasta);
                var clientes = ReporteCargasService.ResumenPorCliente(dt);
                var productos = ReporteCargasService.ResumenPorProducto(ReporteCargasService.LineasCarga(filtro));

                bool comparar = ant.Despachos >= ReporteCargasService.MinViajesParaComparar;
                litTarjetas.Text = HtmlTarjetas(tot, comparar ? ant : null, docs);
                string nombreAnterior = antDesde.Day == 1 && antHasta == antDesde.AddMonths(1).AddDays(-1)
                    ? antDesde.ToString("MMMM yyyy", CulturaPe).ToLower(CulturaPe)
                    : $"el {antDesde:dd/MM/yyyy} al {antHasta:dd/MM/yyyy}";
                string resumenAnterior = $"{ant.Despachos.ToString("N0", CulturaPe)} viaje(s) y {(ant.KgBruto / 1000m).ToString("N1", CulturaPe)} t";
                litComparacion.Text = HttpUtility.HtmlEncode(comparar
                    ? $"Las variaciones comparan con {nombreAnterior}: {resumenAnterior}."
                    : $"Sin comparación: {nombreAnterior} tiene solo {resumenAnterior} " +
                      $"(se compara desde {ReporteCargasService.MinViajesParaComparar} viajes).");

                bool porMes = (filtro.Hasta.Date - filtro.Desde.Date).TotalDays + 1 > ReporteCargasService.MaxDiasGraficoDiario;
                litTituloPeriodo.Text = porMes ? "Toneladas cargadas por mes" : "Toneladas cargadas por d&#xED;a";
                litDocumentacion.Text = HtmlDocumentacion(docs);
                litClientes.Text = HtmlClientes(clientes, tot);
                litProductos.Text = HtmlProductos(productos);
                litDatosGraficos.Text = DatosGraficos(periodos, clientes);

                litPie.Text = $"{tot.SinViajeChofer} de {tot.Despachos} despacho(s) sin viaje del chofer (cargas registradas con los documentos del correo, " +
                              "fuera de la liquidación). Excel: incluye hojas por cliente, por producto y con cada lote.";

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
                Buscar();   // los resultados no viajan en el ViewState: se vuelven a armar
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

        // ================= Pestaña Resumen =================

        private string HtmlTarjetas(ReporteCargasService.Totales tot, ReporteCargasService.Totales ant,
                                    ReporteCargasService.ResumenDocumentos docs)
        {
            var sb = new StringBuilder();
            sb.Append(Tarjeta("Viajes cargados", tot.Despachos.ToString("N0", CulturaPe), Variacion(tot.Despachos, ant?.Despachos)));
            sb.Append(Tarjeta("Pedidos", tot.Pedidos.ToString("N0", CulturaPe), Variacion(tot.Pedidos, ant?.Pedidos)));
            sb.Append(Tarjeta("Clientes", tot.Clientes.ToString("N0", CulturaPe), ""));
            sb.Append(Tarjeta("Bolsas", tot.Bolsas.ToString("N0", CulturaPe), Variacion(tot.Bolsas, ant?.Bolsas)));
            sb.Append(Tarjeta("Toneladas", (tot.KgBruto / 1000m).ToString("N1", CulturaPe), Variacion(tot.KgBruto, ant?.KgBruto)));

            var facturado = new List<string>();
            if (tot.FacturadoUsd > 0) facturado.Add("US$ " + tot.FacturadoUsd.ToString("N2", CulturaPe));
            if (tot.FacturadoPen > 0) facturado.Add("S/ " + tot.FacturadoPen.ToString("N2", CulturaPe));
            sb.Append(Tarjeta("Facturado", facturado.Count == 0 ? "—" : string.Join("<br>", facturado.Select(s => HttpUtility.HtmlEncode(s))),
                              "", "rc-tarjeta-destacada", esHtml: true));

            string detalleDocs = docs.Pendientes == 0
                ? "<small class=\"rc-var\">Todo completo</small>"
                : docs.Atrasados > 0
                    ? $"<small class=\"rc-var rc-var-baja\">{docs.Atrasados} con m&#xE1;s de {ReporteCargasService.DiasToleranciaDocumentos} d&#xED;as</small>"
                    : "<small class=\"rc-var\">Dentro del plazo</small>";
            sb.Append(Tarjeta("Documentos pendientes", docs.Pendientes.ToString("N0", CulturaPe), detalleDocs,
                              docs.Pendientes == 0 ? "rc-tarjeta-ok" : "rc-tarjeta-aviso"));
            return sb.ToString();
        }

        private static string Tarjeta(string etiqueta, string valor, string pie, string clase = "", bool esHtml = false) =>
            $"<div class=\"col-6 col-md-4 col-xl mb-3\"><div class=\"rc-tarjeta {clase}\"><span>{HttpUtility.HtmlEncode(etiqueta)}</span>" +
            $"<strong>{(esHtml ? valor : HttpUtility.HtmlEncode(valor))}</strong>{pie}</div></div>";

        /// <summary>"▲ 12 %" / "▼ 5 %" frente al período anterior; nada si no se compara (ant null).</summary>
        private static string Variacion(decimal actual, decimal? anterior)
        {
            if (!anterior.HasValue) return "";
            decimal? v = ReporteCargasService.Variacion(actual, anterior.Value);
            if (!v.HasValue) return "<small class=\"rc-var\">Sin carga en el per&#xED;odo anterior</small>";
            if (v.Value == 0) return "<small class=\"rc-var\">Igual que el per&#xED;odo anterior</small>";
            string clase = v.Value > 0 ? "rc-var-sube" : "rc-var-baja";
            string flecha = v.Value > 0 ? "&#x25B2;" : "&#x25BC;";
            return $"<small class=\"rc-var {clase}\">{flecha} {Math.Abs(v.Value).ToString("N0", CulturaPe)} % vs. anterior</small>";
        }

        private string HtmlDocumentacion(ReporteCargasService.ResumenDocumentos docs)
        {
            if (docs.Estado.Despachos == 0)
                return "<div class=\"text-muted\">No hay despachos vigentes en el per&#xED;odo.</div>";

            var sb = new StringBuilder("<div class=\"d-flex flex-wrap rc-docs\">");
            sb.Append($"<div class=\"rc-doc rc-doc-ok\"><strong>{docs.Completos:N0}</strong><span>Completos</span></div>");
            sb.Append($"<div class=\"rc-doc {(docs.Pendientes > 0 ? "rc-doc-aviso" : "")}\"><strong>{docs.Pendientes:N0}</strong><span>Con documentos pendientes</span></div>");
            sb.Append($"<div class=\"rc-doc {(docs.Atrasados > 0 ? "rc-doc-mal" : "")}\"><strong>{docs.Atrasados:N0}</strong>" +
                      $"<span>Pendientes hace m&#xE1;s de {ReporteCargasService.DiasToleranciaDocumentos} d&#xED;as</span></div>");
            sb.Append("</div>");

            if (docs.Pendientes > 0)
            {
                sb.Append("<div class=\"mt-3\"><span class=\"text-muted small d-block mb-1\">Lo que falta:</span>");
                foreach (string f in docs.Estado.Faltantes)
                    sb.Append($"<span class=\"badge rc-faltante\">{HttpUtility.HtmlEncode(f)}</span> ");
                sb.Append("</div>");
                sb.Append($"<a class=\"btn btn-outline-primary btn-sm mt-3\" href=\"{ResolveUrl("~/Views/ListaDespachos.aspx?documentos=pendientes")}\">" +
                          "<i class=\"fas fa-external-link-alt mr-1\"></i>Completar en la lista de despachos</a>");
            }
            return sb.ToString();
        }

        private static string HtmlClientes(List<ReporteCargasService.CargaCliente> clientes, ReporteCargasService.Totales tot)
        {
            if (clientes.Count == 0) return "<div class=\"text-center text-muted p-4\">No hay cargas con esos filtros.</div>";
            Func<decimal, string> n0 = v => v.ToString("N0", CulturaPe);
            Func<decimal, decimal, string> fact = (usd, pen) =>
            {
                var partes = new List<string>();
                if (usd > 0) partes.Add("US$ " + usd.ToString("N2", CulturaPe));
                if (pen > 0) partes.Add("S/ " + pen.ToString("N2", CulturaPe));
                return HttpUtility.HtmlEncode(string.Join(" · ", partes));
            };
            var sb = new StringBuilder("<table class=\"table table-sm table-hover mb-0 rc-tabla\"><thead><tr>" +
                "<th>Cliente</th><th class=\"text-right\">Viajes</th><th class=\"text-right\">Pedidos</th><th class=\"text-right\">Bolsas</th>" +
                "<th class=\"text-right\">Toneladas</th><th class=\"rc-col-barra\">% del peso</th><th class=\"text-right\">Facturado</th></tr></thead><tbody>");
            int sinDetalle = 0;
            foreach (var c in clientes)
            {
                // Sin líneas de producto (cargas de terceros sin documentos de carga): no hay bolsas ni kg que mostrar.
                bool conDetalle = c.KgBruto > 0 || c.Bolsas > 0;
                if (!conDetalle) sinDetalle++;
                string sinDato = "<span class=\"rc-sin-viaje\">sin detalle*</span>";
                sb.Append("<tr>")
                  .Append($"<td>{HttpUtility.HtmlEncode(c.Cliente)}</td>")
                  .Append($"<td class=\"text-right\">{c.Viajes:N0}</td><td class=\"text-right\">{c.Pedidos:N0}</td>")
                  .Append($"<td class=\"text-right\">{(conDetalle ? n0(c.Bolsas) : sinDato)}</td>")
                  .Append($"<td class=\"text-right\">{(conDetalle ? c.Toneladas.ToString("N1", CulturaPe) : sinDato)}</td>")
                  .Append($"<td>{(conDetalle ? Barra(c.Porcentaje) : "")}</td><td class=\"text-right text-nowrap\">{fact(c.FacturadoUsd, c.FacturadoPen)}</td></tr>");
            }
            sb.Append("</tbody><tfoot><tr>")
              .Append("<th>Total</th>")
              .Append($"<th class=\"text-right\">{tot.Despachos:N0}</th><th class=\"text-right\">{tot.Pedidos:N0}</th>")
              .Append($"<th class=\"text-right\">{n0(tot.Bolsas)}</th><th class=\"text-right\">{(tot.KgBruto / 1000m).ToString("N1", CulturaPe)}</th>")
              .Append($"<th></th><th class=\"text-right text-nowrap\">{fact(tot.FacturadoUsd, tot.FacturadoPen)}</th></tr></tfoot></table>");
            if (sinDetalle > 0)
                sb.Append("<div class=\"small text-muted px-3 py-2\">* Cargas registradas sin el detalle de productos (bolsas y kg), " +
                          "por eso no suman al peso ni entran al gr&#xE1;fico.</div>");
            return sb.ToString();
        }

        private static string HtmlProductos(List<ReporteCargasService.CargaProducto> productos)
        {
            if (productos.Count == 0)
                return "<div class=\"text-center text-muted p-4\">Sin detalle de productos (las cargas no tienen l&#xED;neas de producto).</div>";
            var sb = new StringBuilder("<table class=\"table table-sm table-hover mb-0 rc-tabla\"><thead><tr>" +
                "<th>C&#xF3;digo</th><th>Producto</th><th class=\"text-right\">Viajes</th><th class=\"text-right\">Lotes</th>" +
                "<th class=\"text-right\">Bolsas</th><th class=\"text-right\">Toneladas</th><th class=\"rc-col-barra\">% del peso</th></tr></thead><tbody>");
            foreach (var p in productos)
            {
                sb.Append("<tr>")
                  .Append($"<td class=\"text-nowrap\">{HttpUtility.HtmlEncode(p.Codigo)}</td><td>{HttpUtility.HtmlEncode(p.Producto)}</td>")
                  .Append($"<td class=\"text-right\">{p.Viajes:N0}</td><td class=\"text-right\">{p.Lotes:N0}</td>")
                  .Append($"<td class=\"text-right\">{p.Bolsas.ToString("N0", CulturaPe)}</td><td class=\"text-right\">{p.Toneladas.ToString("N1", CulturaPe)}</td>")
                  .Append($"<td>{Barra(p.Porcentaje)}</td></tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }

        private static string Barra(decimal porcentaje) =>
            $"<div class=\"rc-barra\"><div class=\"rc-barra-relleno\" style=\"width:{Math.Min(100m, porcentaje).ToString("0.#", CultureInfo.InvariantCulture)}%\"></div>" +
            $"<span>{HttpUtility.HtmlEncode(ReporteCargasService.TextoPorcentaje(porcentaje))}</span></div>";

        /// <summary>Datos de los gráficos (JSON para SGV.carga): toneladas por fecha y por cliente (8 + "Otros").</summary>
        private static string DatosGraficos(List<ReporteCargasService.CargaPeriodo> periodos, List<ReporteCargasService.CargaCliente> clientes)
        {
            const int maxClientes = 8;
            var porCliente = clientes.Take(maxClientes)
                .Select(c => new { c = c.Cliente, t = c.Toneladas, v = c.Viajes }).ToList();
            if (clientes.Count > maxClientes)
            {
                var resto = clientes.Skip(maxClientes).ToList();
                porCliente.Add(new { c = $"Otros ({resto.Count})", t = Math.Round(resto.Sum(x => x.KgBruto) / 1000m, 1), v = resto.Sum(x => x.Viajes) });
            }
            var datos = new
            {
                periodo = periodos.Select(p => new { e = p.Etiqueta, t = p.Toneladas, v = p.Viajes }),
                clientes = porCliente
            };
            // JavaScriptSerializer escapa < y > (<), así que es seguro dentro de <script>.
            return new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(datos);
        }

        private void MostrarMensaje(string mensaje)
        {
            pnlResultados.Visible = pnlResultados.Visible && litTarjetas.Text != "";
            pnlMensaje.Visible = true;
            lblMensaje.Text = $"<div class='alert alert-danger'>{HttpUtility.HtmlEncode(mensaje)}</div>";
        }
    }
}
