using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Seguimiento de exportación con la plantilla común (E5-18c, M-072): el tablero (RegistroSeguimiento) con los
    /// filtros en details.sgv-filtros y los reportes plegables en el celular; el dashboard sin tarjetas KPI (línea
    /// sgv-resumen), con sellos sgv-estado, chips de 2,75 rem en el celular y el detalle en details.sgv-plegable; el
    /// detalle y la importación sin estilos en línea. Leen las páginas como texto sin comentarios, igual que
    /// <see cref="GrandesPlantillaTests"/>.
    /// </summary>
    public class ExportacionPlantillaTests
    {
        public static readonly string[] Paginas =
        {
            "Exportacion/RegistroSeguimiento", "Exportacion/DashboardExportacion",
            "Exportacion/SeguimientoViaje", "Exportacion/ImportarSeguimiento"
        };

        private static readonly string[] Hojas = { "Exportacion/SeguimientoExportacion", "Exportacion/DashboardExportacion" };

        public static System.Collections.Generic.IEnumerable<object[]> CasosPaginas() => Paginas.Select(p => new object[] { p });

        public static System.Collections.Generic.IEnumerable<object[]> CasosHojas() => Hojas.Select(h => new object[] { h });

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Aspx(string pagina) => PaginaDe("Views/" + pagina + ".aspx").Texto;

        private static string Cs(string pagina) => File.ReadAllText(Path.Combine(RaizWeb(), "Views", pagina.Replace('/', Path.DirectorySeparatorChar) + ".aspx.cs"));

        private static string Css(string hoja) => File.ReadAllText(Path.Combine(RaizWeb(), "Content", "paginas", hoja.Replace('/', Path.DirectorySeparatorChar) + ".css"));

        private static string Js(string pagina) => File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", pagina.Replace('/', Path.DirectorySeparatorChar) + ".js"));

        /// <summary>Bloque &lt;details …&gt;…&lt;/details&gt; que empieza en la etiqueta que cumple <paramref name="apertura"/>.</summary>
        private static string Details(string aspx, string apertura)
        {
            Match m = Regex.Match(aspx, apertura);
            Assert.True(m.Success, "No se encontró " + apertura);
            int fin = aspx.IndexOf("</details>", m.Index, StringComparison.OrdinalIgnoreCase);
            Assert.True(fin > m.Index, "El details no se cierra");
            return aspx.Substring(m.Index, fin - m.Index);
        }

        /// <summary>Cuerpo de una función del script (desde «function nombre(» hasta la llave que la cierra).</summary>
        private static string Metodo(string js, string nombre)
        {
            int i = js.IndexOf("function " + nombre + "(", StringComparison.Ordinal);
            Assert.True(i >= 0, "No se encontró la función " + nombre);
            int inicio = js.IndexOf('{', i), nivel = 0;
            for (int k = inicio; k < js.Length; k++)
            {
                if (js[k] == '{') nivel++;
                else if (js[k] == '}' && --nivel == 0) return js.Substring(inicio, k - inicio + 1);
            }
            return js.Substring(inicio);
        }

        // ── Sin estilos en línea ─────────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(CasosPaginas))]
        public void A_SinStyleEnMarcadoNiCodigo(string pagina)
        {
            foreach (var (archivo, texto) in new[] { (pagina + ".aspx", Aspx(pagina)), (pagina + ".aspx.cs", Cs(pagina)) })
            {
                Assert.True(!Regex.IsMatch(texto, @"\bstyle\s*=", RegexOptions.IgnoreCase), archivo + " tiene style=");
                Assert.True(!Regex.IsMatch(texto, @"<style\b", RegexOptions.IgnoreCase), archivo + " tiene <style>");
                Assert.True(!Regex.IsMatch(texto, @"style=\\""", RegexOptions.IgnoreCase), archivo + " arma un style en el código");
            }
        }

        [Theory]
        [MemberData(nameof(CasosPaginas))]
        public void A2_DetailsBalanceados(string pagina)
        {
            string aspx = Aspx(pagina);
            int abren = Regex.Matches(aspx, @"<details\b", RegexOptions.IgnoreCase).Count;
            int cierran = Regex.Matches(aspx, @"</details\s*>", RegexOptions.IgnoreCase).Count;
            Assert.True(abren == cierran, $"{pagina}: {abren} <details> y {cierran} </details>");
        }

        // ── Tablero (RegistroSeguimiento) ────────────────────────────────────────────────────

        [Fact]
        public void RS1_FiltrosEnDetailsComunConBuscarYFiltrar()
        {
            string aspx = Aspx("Exportacion/RegistroSeguimiento");
            string filtros = Details(aspx, @"<details class=""sgv-filtros"" open data-plegar-movil>");
            Assert.Contains(@"<summary>Filtros <span class=""sgv-filtros-cuenta""></span></summary>", filtros);
            Assert.Matches(@"class=""sgv-filtros-cuerpo sx-filtros"" data-enter=""<%= btnFiltrar.ClientID %>""", filtros);
            int buscar = filtros.IndexOf("ID=\"txtBuscar\"", StringComparison.Ordinal);
            int filtrar = filtros.IndexOf("ID=\"btnFiltrar\"", StringComparison.Ordinal);
            Assert.True(buscar > 0 && filtrar > buscar, "txtBuscar y btnFiltrar van dentro de los filtros, en ese orden");
            foreach (string id in new[] { "txtDesde", "txtHasta", "ddlCliente", "ddlPlanta", "chkHistorial" })
                Assert.Contains("ID=\"" + id + "\"", filtros);
            // El "n de m viajes" del buscador queda a la vista aunque los filtros estén plegados.
            Assert.DoesNotContain("sxFiltroCuenta", filtros);
            Assert.Contains("id=\"sxFiltroCuenta\"", aspx);
        }

        [Fact]
        public void RS2_ReportesPlegablesEnElCelularYLoDemasFuera()
        {
            string aspx = Aspx("Exportacion/RegistroSeguimiento");
            string reportes = Details(aspx, @"<details class=""sgv-plegable sx-reportes"" open data-plegar-movil>");
            Assert.Contains("<summary>Reportes</summary>", reportes);
            foreach (string id in new[] { "btnExportarStatus", "btnImagenStatus", "btnExportar", "btnExportarStatusExtra",
                "btnImagenStatusExtra", "btnExportarGrifo", "btnImagenGrifo", "btnExportarLlegadas", "btnImagenLlegadas" })
                Assert.Contains("ID=\"" + id + "\"", reportes);
            foreach (string fuera in new[] { "btnActualizarGps", "dropdown-toggle", "sxFiltroCuenta", "litUltimaConsulta", "<textarea" })
                Assert.DoesNotContain(fuera, reportes);
            Assert.Contains("ID=\"btnActualizarGps\"", aspx);

            // En escritorio, sin caja y en fila (se ve como antes); el rótulo se oculta solo abierto.
            string css = Css("Exportacion/SeguimientoExportacion");
            Assert.Matches(@"@media \(min-width: 768px\) \{\s*details\.sgv-plegable\.sx-reportes \{[^}]*border: 0", css);
            Assert.Contains("details.sgv-plegable.sx-reportes[open] > summary { display: none; }", css);
        }

        [Fact]
        public void RS3_BarrasDeAvanceConProgress()
        {
            string aspx = Aspx("Exportacion/RegistroSeguimiento");
            Assert.Contains(@"<progress class=""sx-avance-barra sx-avance-carga"" max=""100"" value='<%#: Eval(""CargaPct"") %>' aria-hidden=""true"">", aspx);
            Assert.Contains(@"<progress class=""sx-avance-barra sx-avance-descarga"" max=""100"" value='<%#: Eval(""DescargaPct"") %>' aria-hidden=""true"">", aspx);
            string css = Css("Exportacion/SeguimientoExportacion");
            Assert.Contains("appearance: none", css);
            Assert.Contains(".sx-avance-carga::-webkit-progress-value", css);
            Assert.Contains(".sx-avance-carga::-moz-progress-bar", css);
            Assert.DoesNotContain(".sx-avance-barra > span", css);
        }

        // ── Dashboard ────────────────────────────────────────────────────────────────────────

        [Fact]
        public void DE1_SinTarjetasKpiNiAdornos()
        {
            string aspx = Aspx("Exportacion/DashboardExportacion");
            Assert.False(InterfazFormalTests.TieneTarjetasKpi(aspx), "El dashboard volvió a tener tarjetas KPI");
            Assert.Contains(@"<p class=""sgv-resumen"" id=""resumenPeriodo""></p>", aspx);

            string js = Js("Exportacion/DashboardExportacion");
            Assert.Contains("resumenPeriodo", js);
            Assert.Contains("function renderResumen(", js);
            // Lo que decían las tarjetas sigue a la vista: los de vuelta en base, los hitos del GPS y con qué período se compara.
            foreach (string campo in new[] { "k.cerrados", "k.hitosGps", "k.hitosRegistrados", "p.periodoAnterior" })
                Assert.Contains(campo, Metodo(js, "renderResumen"));
            Assert.Contains("de vuelta en base", js);
            Assert.Contains("class=\"de-vs\"", js);
            string css = Css("Exportacion/DashboardExportacion");
            foreach (var (archivo, texto) in new[] { ("aspx", aspx), ("js", js), ("css", css), ("aspx.cs", Cs("Exportacion/DashboardExportacion")) })
                foreach (string retirada in new[] { "de-kpi", "data-contar", "de-anillo", "de-meter", "de-spark", "de-pill",
                    "de-alert-", "de-plegable", "btnDetalle", "renderKpis", "animarNumeros", "sparkline" })
                    Assert.True(texto.IndexOf(retirada, StringComparison.Ordinal) < 0, $"DashboardExportacion.{archivo} todavía usa «{retirada}»");
        }

        [Fact]
        public void DE2_SellosComunesYAvisoDeError()
        {
            string js = Js("Exportacion/DashboardExportacion");
            Assert.Contains("sgv-estado sgv-estado-alerta", js);
            Assert.Contains("sgv-estado sgv-estado-peligro", js);
            Assert.Contains("sgv-estado sgv-estado-ok", js);
            Assert.Contains("AvisoHelper.Pintar(litAlert", Cs("Exportacion/DashboardExportacion"));
            Assert.Matches(@"<asp:Panel ID=""pnlAlert"" runat=""server"" Visible=""false"">", Aspx("Exportacion/DashboardExportacion"));
        }

        [Fact]
        public void DE3_DetallePorTramoEnDetails()
        {
            string aspx = Aspx("Exportacion/DashboardExportacion");
            string detalle = Details(aspx, @"<details class=""sgv-plegable de-detalle"">");
            Assert.Contains("<summary>Detalle por tramo", detalle);
            Assert.Contains("id=\"tablaTramos\"", detalle);
            Assert.DoesNotContain("fa-chevron-down", aspx);
        }

        [Fact]
        public void DE4_ChipsYFlechasParaElDedo()
        {
            string css = Css("Exportacion/DashboardExportacion");
            Assert.Matches(@"\.de-chip \{[^}]*min-height: 2\.25rem[^}]*font-size: \.8125rem", css);
            Match movil = Regex.Match(css, @"@media \(max-width: 767\.98px\) \{(?<c>(?:[^{}]*\{[^}]*\})*)\s*\}");
            Assert.True(movil.Success, "Falta la media del celular");
            Match regla = Regex.Match(movil.Groups["c"].Value, @"(?<s>[^{}]*\.de-chip[^{}]*)\{(?<d>[^}]*)\}");
            Assert.True(regla.Success && regla.Groups["s"].Value.Contains(".de-btn-icon"), "La media del celular agranda .de-chip y .de-btn-icon");
            Match alto = Regex.Match(regla.Groups["d"].Value, @"min-height:\s*(?<n>[0-9.]+)rem");
            Assert.True(alto.Success && double.Parse(alto.Groups["n"].Value, CultureInfo.InvariantCulture) >= 2.75,
                ".de-chip en el celular necesita min-height de 2,75 rem o más");
            Assert.Contains("aria-pressed=", Js("Exportacion/DashboardExportacion"));
        }

        [Fact]
        public void DE5_GraficosSinLetraDeOnce()
        {
            string js = Js("Exportacion/DashboardExportacion");
            Assert.DoesNotMatch(@"size:\s*11(\.\d+)?\b", js);
        }

        // ── Hojas ────────────────────────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(CasosHojas))]
        public void H1_HojaSinLetraMenorDeUnPuntoSetentaYCinco(string hoja)
        {
            foreach (Match m in Regex.Matches(Css(hoja), @"font-size:\s*(?<n>[0-9.]+)(?<u>rem|px)"))
            {
                double n = double.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
                double rem = m.Groups["u"].Value == "px" ? n / 16 : n;
                Assert.True(rem >= .75 - 1e-9, $"{hoja}.css: {m.Value} es menor de .75rem");
            }
        }

        [Fact]
        public void H2_DashboardSinMayusculasForzadas() =>
            Assert.DoesNotMatch(new Regex(@"text-transform:\s*uppercase", RegexOptions.IgnoreCase), Css("Exportacion/DashboardExportacion"));

        [Fact]
        public void H3_MarcadoresDelMapaDelTamanoDeSuHoja()
        {
            // .sx-mk mide 24 px en la hoja: el icono de Leaflet tiene que medir lo mismo para quedar centrado.
            Assert.Matches(@"\.sx-mk \{[^}]*width: 24px; height: 24px", Css("Exportacion/SeguimientoExportacion"));
            Assert.Contains("iconSize: [24, 24], iconAnchor: [12, 12]", Js("Exportacion/SeguimientoViajeMapa"));
        }
    }
}
