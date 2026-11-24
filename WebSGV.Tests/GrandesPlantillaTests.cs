using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Pantallas grandes con la plantilla común (E5-18, fase 4 de M-073): sgv-panel, sgv-tabs, sgv-resumen en vez de
    /// tarjetas, sgv-tabla con sgv-paginador y grillas sin ViewState, filtros en details.sgv-filtros y sellos
    /// sgv-estado. La lista crece con cada parte de E5-18 (a: ListaDespachos). Leen las páginas como texto sin
    /// comentarios, igual que <see cref="BuscarEditarPlantillaTests"/>. Las pruebas LD* fijan ListaDespachos (M-082:
    /// las pestañas y el resumen solo en las dos listas).
    /// </summary>
    public class GrandesPlantillaTests
    {
        public static readonly string[] Paginas = { "ListaDespachos" };

        /// <summary>Clases y nombres retirados en E5-18a: no vuelven en el marcado, el código, el script ni la hoja.</summary>
        private static readonly string[] ClasesRetiradas =
        {
            "ld-stat", "ld-pager", "summary-card", "section-card", "section-header", "estado-abierto",
            "tipo-internacional", "tipo-nacional", "ld-chip", "ld-estado-", "active-nav", "btn-nav", "empty-data",
            "doc-panel-header", "GetEstadoDespachoClass", "GetManifiestoBadgeClass", "fa-info-circle"
        };

        public static IEnumerable<object[]> Casos() => Paginas.Select(p => new object[] { p });

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Aspx(string pagina) => PaginaDe("Views/" + pagina + ".aspx").Texto;

        private static string Cs(string pagina) => File.ReadAllText(Path.Combine(RaizWeb(), "Views", pagina + ".aspx.cs"));

        private static string SiHay(params string[] ruta)
        {
            string f = Path.Combine(RaizWeb(), Path.Combine(ruta));
            return File.Exists(f) ? File.ReadAllText(f) : string.Empty;
        }

        private static string Js(string pagina) => SiHay("Scripts", "paginas", pagina + ".js");

        private static string Css(string pagina) => SiHay("Content", "paginas", pagina + ".css");

        private static readonly Regex Grilla = new Regex(@"<asp:GridView\b(?<a>[^>]*)>(?<c>.*?)</asp:GridView\s*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static Match GrillaDe(string aspx, string id) =>
            Grilla.Matches(aspx).Cast<Match>().FirstOrDefault(m => m.Groups["a"].Value.Contains("ID=\"" + id + "\""));

        /// <summary>Cuerpo de un método del code-behind (desde su firma hasta la llave que lo cierra).</summary>
        private static string Metodo(string cs, string nombre)
        {
            Match firma = Regex.Match(cs, @"(protected|private|public|internal)[^\n(=]*\b" + nombre + @"\(");
            Assert.True(firma.Success, "No se encontró el método " + nombre);
            int i = firma.Index;
            int inicio = cs.IndexOf('{', i);
            int nivel = 0;
            for (int k = inicio; k < cs.Length; k++)
            {
                if (cs[k] == '{') nivel++;
                else if (cs[k] == '}' && --nivel == 0) return cs.Substring(inicio, k - inicio + 1);
            }
            return cs.Substring(inicio);
        }

        // ── (a) Sin estilos en línea ─────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void A_SinStyleEnMarcadoNiCodigo(string pagina)
        {
            foreach (var (archivo, texto) in new[] { (pagina + ".aspx", Aspx(pagina)), (pagina + ".aspx.cs", Cs(pagina)) })
            {
                Assert.True(!Regex.IsMatch(texto, @"\bstyle\s*=", RegexOptions.IgnoreCase), archivo + " tiene style=");
                Assert.DoesNotMatch(new Regex(@"<style\b", RegexOptions.IgnoreCase), texto);
            }
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void A3_TodaGrillaConCellSpacingMenosUno(string pagina)
        {
            // Sin CellSpacing="-1" el GridView escribe style="border-collapse:collapse" en la tabla.
            foreach (Match g in Grilla.Matches(Aspx(pagina)))
                Assert.Contains("CellSpacing=\"-1\"", g.Groups["a"].Value);
        }

        // ── (b) Plantilla visual ─────────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void B_PanelesSinTarjetasNiContenedorFluido(string pagina)
        {
            string aspx = Aspx(pagina);
            Assert.Matches(@"class=""sgv-panel[\s""]", aspx);
            Assert.DoesNotContain("container-fluid", aspx);
            // Por clase, no por texto: ld-doc-card es una clase propia de la página.
            var clases = Regex.Matches(aspx, @"class=""(?<c>[^""]*)""").Cast<Match>()
                .SelectMany(m => m.Groups["c"].Value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.DoesNotContain(clases, c => c == "card" || c == "card-body" || c == "card-header");
            Assert.False(InterfazFormalTests.TieneTarjetasKpi(aspx), "La página volvió a tener tarjetas KPI");
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B2_DetailsBalanceados(string pagina)
        {
            string aspx = Aspx(pagina);
            int abren = Regex.Matches(aspx, @"<details\b", RegexOptions.IgnoreCase).Count;
            int cierran = Regex.Matches(aspx, @"</details\s*>", RegexOptions.IgnoreCase).Count;
            Assert.True(abren == cierran, $"{pagina}: {abren} <details> y {cierran} </details>");
            Assert.Matches(@"<details class=""sgv-filtros"" open data-plegar-movil>", aspx);
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B3_ClasesRetiradasNoVuelven(string pagina)
        {
            foreach (var (archivo, texto) in new[]
            {
                (pagina + ".aspx", Aspx(pagina)), (pagina + ".aspx.cs", Cs(pagina)),
                (pagina + ".js", Js(pagina)), (pagina + ".css", Css(pagina))
            })
                foreach (string clase in ClasesRetiradas)
                    Assert.True(texto.IndexOf(clase, StringComparison.Ordinal) < 0, $"{archivo} todavía usa «{clase}»");
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B4_HojaSinLetraMenorDeUnPuntoSetentaYCinco(string pagina)
        {
            foreach (Match m in Regex.Matches(Css(pagina), @"font-size:\s*(?<n>[0-9.]+)(?<u>rem|px)"))
            {
                double n = double.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture);
                double rem = m.Groups["u"].Value == "px" ? n / 16 : n;
                Assert.True(rem >= .75 - 1e-9, $"{pagina}.css: {m.Value} es menor de .75rem");
            }
        }

        [Theory]
        [MemberData(nameof(Casos))]
        public void B5_HojaSinMayusculasForzadas(string pagina) =>
            Assert.DoesNotMatch(new Regex(@"text-transform:\s*uppercase", RegexOptions.IgnoreCase), Css(pagina));

        // ── LD: ListaDespachos ───────────────────────────────────────────────────────────────

        [Fact]
        public void LD1_PestanasYResumenSoloEnLasListas()
        {
            Assert.True(ListaDespachosReglas.MuestraNavegacion(VistaListaDespachos.Viajes));
            Assert.True(ListaDespachosReglas.MuestraNavegacion(VistaListaDespachos.Lotes));
            foreach (var vista in new[] { VistaListaDespachos.DetalleViaje, VistaListaDespachos.EdicionLote,
                VistaListaDespachos.DetalleLote, VistaListaDespachos.RegistroManual })
                Assert.False(ListaDespachosReglas.MuestraNavegacion(vista), vista + " no lleva las pestañas (M-082)");

            string cs = Cs("ListaDespachos");
            Assert.Matches(@"pnlNavegacionListas\.Visible = (ListaDespachosReglas\.)?MuestraNavegacion\(vista\)", Metodo(cs, "MostrarVista"));
            // Toda pantalla pasa por MostrarVista: ningún otro método enciende un panel a mano.
            Assert.Equal(1, Regex.Matches(cs, @"pnlListaLotes\.Visible\s*=").Count);
            Assert.Equal(1, Regex.Matches(cs, @"pnlManifiestosLote\.Visible\s*=").Count);
        }

        [Fact]
        public void LD2_SinViajesActivosAbreEnLotes()
        {
            Assert.Equal(VistaListaDespachos.Lotes, ListaDespachosReglas.VistaInicial(0));
            Assert.Equal(VistaListaDespachos.Viajes, ListaDespachosReglas.VistaInicial(3));
            Assert.Contains("VistaInicial(", Cs("ListaDespachos"));
        }

        [Fact]
        public void LD3_PestanasConRolesAria()
        {
            string aspx = Aspx("ListaDespachos");
            Assert.Matches(@"<div class=""sgv-tabs"" role=""tablist""", aspx);
            foreach (string id in new[] { "btnMostrarViajes", "btnMostrarLotes" })
                Assert.Matches(@"<asp:LinkButton ID=""" + id + @"""[^>]*role=""tab""", aspx);
            string vista = Metodo(Cs("ListaDespachos"), "MostrarVista");
            Assert.Contains("btnMostrarViajes.Attributes[\"aria-selected\"]", vista);
            Assert.Contains("btnMostrarLotes.Attributes[\"aria-selected\"]", vista);
            Assert.Equal("Lotes registrados <span class=\"sgv-tab-contador\">108</span>",
                ListaDespachosReglas.TextoPestana("Lotes registrados", 108));
        }

        [Fact]
        public void LD4_ListasSinViewStateConPaginadorYBotonesDeFila()
        {
            string aspx = Aspx("ListaDespachos");
            foreach (string id in new[] { "gvViajesActivos", "gvLotesRegistrados" })
            {
                Match g = GrillaDe(aspx, id);
                Assert.NotNull(g);
                string a = g.Groups["a"].Value, c = g.Groups["c"].Value;
                Assert.Contains("EnableViewState=\"false\"", a);
                Assert.Contains("PageSize=\"" + ListaCatalogo.FilasPorPagina + "\"", a);
                Assert.Contains("CellSpacing=\"-1\"", a);
                Assert.DoesNotContain("OnRowCommand", a);
                Assert.Contains("CssClass=\"sgv-paginador\"", c);
                Assert.DoesNotMatch(@"<asp:(Button|LinkButton)\b", c);
                Assert.Contains("data-accion=\"enviarFila\"", c);
            }
            foreach (string id in new[] { "gvDespachosViaje", "gvDespachosLote" })
                Assert.Contains("EnableViewState=\"false\"", GrillaDe(aspx, id).Groups["a"].Value);

            // El botón oculto y su campo van dentro del UpdatePanel y fuera de toda grilla.
            string contenido = Regex.Match(aspx, @"<ContentTemplate>(?<c>.*)</ContentTemplate>", RegexOptions.Singleline).Groups["c"].Value;
            string sinGrillas = Grilla.Replace(contenido, "");
            Assert.Contains("ID=\"btnAccionFila\"", sinGrillas);
            Assert.Contains("ID=\"hfArgumentoFila\"", sinGrillas);
            Assert.Contains("hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>'", aspx);
            Assert.Contains("btnAccionFila: '<%= btnAccionFila.ClientID %>'", aspx);
            Assert.Contains("ControlID=\"btnAccionFila\"", aspx);
        }

        [Fact]
        public void LD5_AccionDeFilaRevalidaEnElServidor()
        {
            string cuerpo = Metodo(Cs("ListaDespachos"), "btnAccionFila_Click");
            Assert.Contains("hfArgumentoFila.Value = \"\";", cuerpo);
            Assert.Contains("TryLeerAccionFila(", cuerpo);
            Assert.Contains("ObtenerLotePorId(", cuerpo);
            Assert.Contains("ObtenerInfoViajeDetalle(", cuerpo);
        }

        [Theory]
        [InlineData("VER|1_4400134786_2026-11-11_CARGA_0_2", AccionFilaLista.VerLote, "1_4400134786_2026-11-11_CARGA_0_2", 0)]
        [InlineData("EDITAR|12_NOPEDIDO_2026-01-05_DESCARGA_1_7", AccionFilaLista.EditarLote, "12_NOPEDIDO_2026-01-05_DESCARGA_1_7", 0)]
        [InlineData("VERANULADO|3_123-A_2025-12-31_CARGA_1_10", AccionFilaLista.VerLoteAnulado, "3_123-A_2025-12-31_CARGA_1_10", 0)]
        [InlineData("VIAJE|42", AccionFilaLista.VerViaje, null, 42)]
        // Pedido vacío: el SP arma el id con ISNULL(numeroPedido, 'NOPEDIDO') y un '' guardado deja "1__fecha" (hay lotes activos así).
        [InlineData("VER|1__2026-08-26_CARGA_0_2", AccionFilaLista.VerLote, "1__2026-08-26_CARGA_0_2", 0)]
        [InlineData("EDITAR|1__2026-08-26_CARGA_0_2", AccionFilaLista.EditarLote, "1__2026-08-26_CARGA_0_2", 0)]
        public void LD6_ArgumentoDeFilaValido(string argumento, AccionFilaLista accion, string lote, int viaje)
        {
            Assert.True(ListaDespachosReglas.TryLeerAccionFila(argumento, out var a, out string l, out int v));
            Assert.Equal(accion, a);
            Assert.Equal(lote, l);
            Assert.Equal(viaje, v);
            // Ida y vuelta con el que pinta la grilla.
            Assert.Equal(argumento, ListaDespachosReglas.ArgumentoFila(accion, (object)lote ?? viaje));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("VER")]
        [InlineData("VER|")]
        [InlineData("BORRAR|1_4400134786_2026-11-11_CARGA_0_2")]
        [InlineData("ver|1_4400134786_2026-11-11_CARGA_0_2")]
        [InlineData("VER|1_4400134786_2026-11-11_CARGA_0_2|x")]
        [InlineData("VER|1_4400134786_2026-11-11_OTRA_0_2")]
        [InlineData("VER|1_4400134786_2026-11-11_CARGA_2_2")]
        [InlineData("VER|1_44001'34786_2026-11-11_CARGA_0_2")]
        [InlineData("VER|<script>")]
        [InlineData("VIAJE|0")]
        [InlineData("VIAJE|-3")]
        [InlineData("VIAJE| 5")]
        [InlineData("VIAJE|5a")]
        [InlineData("VIAJE|99999999999")]
        [InlineData("EDITAR|42")]
        public void LD6_ArgumentoDeFilaInvalido(string argumento) =>
            Assert.False(ListaDespachosReglas.TryLeerAccionFila(argumento, out _, out _, out _));

        [Fact]
        public void LD6_ArgumentoDemasiadoLargo() =>
            Assert.False(ListaDespachosReglas.TryLeerAccionFila("VER|1_" + new string('9', 200) + "_2026-11-11_CARGA_0_2", out _, out _, out _));

        [Fact]
        public void LD7_LineasDeResumen()
        {
            Assert.Equal(
                "<span><strong>3</strong> lotes con documentos pendientes</span>" +
                "<span><strong>1</strong> con más de 3 días</span>" +
                "<span><strong>2</strong> lotes con manifiestos pendientes</span>",
                ListaDespachosReglas.HtmlResumenListas(2, 3, 1));
            Assert.Equal(
                "<span><strong>1</strong> lote con documentos pendientes</span>" +
                "<span><strong>0</strong> lotes con manifiestos pendientes</span>",
                ListaDespachosReglas.HtmlResumenListas(0, 1, 0));
            Assert.Equal(
                "<p class=\"sgv-resumen\"><span><strong>8</strong> despachos</span><span>Carga</span><span>A&amp;B</span>" +
                "<span>Creado el 05/11/2026</span></p>",
                ListaDespachosReglas.HtmlResumenLote(8, "CARGA", "A&B", new DateTime(2026, 11, 5, 14, 30, 0)));
            Assert.DoesNotContain("<span></span>", ListaDespachosReglas.HtmlResumenLote(1, "DESCARGA", " ", DateTime.Today));
            Assert.Contains("<span>Última actividad: 05/11/2026 14:30</span>",
                ListaDespachosReglas.HtmlResumenViaje(2, "Nacional", "ABIERTO", new DateTime(2026, 11, 5, 14, 30, 0)));
        }

        [Fact]
        public void LD8_EstadosConSelloSinIconos()
        {
            string aspx = Aspx("ListaDespachos");
            Assert.Contains("ListaDespachosReglas.SellosDocumentos(", aspx);
            Assert.Contains("ListaDespachosReglas.SelloSinAdjuntar(", aspx);
            Assert.Contains("ListaDespachosReglas.SelloEstadoDespacho(", aspx);
            Assert.DoesNotContain("ld-sin-adjuntar", aspx);
            Assert.Equal("", ListaDespachosReglas.SelloSinAdjuntar(5));
            Assert.Contains(">Sin adjuntar<", ListaDespachosReglas.SelloSinAdjuntar(null));
            Assert.Equal("", ListaDespachosReglas.SelloLoteAnulado("ACTIVO"));
            Assert.Contains(EstadoUiHelper.ClasePeligro, ListaDespachosReglas.SelloLoteAnulado("ANULADO"));
            string celda = ListaDespachosReglas.SellosDocumentos("Falta <factura>", TonoEstado.Alerta, true, "Pendiente");
            Assert.Contains("Falta &lt;factura&gt;", celda);
            Assert.Contains("Manifiesto: Pendiente", celda);
            Assert.DoesNotContain("Manifiesto", ListaDespachosReglas.SellosDocumentos("Completo", TonoEstado.Ok, false, "N/A"));
            string insignia = Metodo(Cs("ListaDespachos"), "InsigniaDocumento");
            Assert.DoesNotContain("<i class", insignia);

            Assert.Equal(TonoEstado.Ok, ListaDespachosReglas.TonoManifiesto("Completo"));
            Assert.Equal(TonoEstado.Peligro, ListaDespachosReglas.TonoManifiesto("Pendiente"));
            Assert.Equal(TonoEstado.Alerta, ListaDespachosReglas.TonoManifiesto("Parcial (1/2)"));
            Assert.Equal(TonoEstado.Neutro, ListaDespachosReglas.TonoManifiesto("N/A"));
            Assert.Equal("En progreso", ListaDespachosReglas.TextoOracion("EN_PROGRESO"));
            Assert.Contains(">Abierto<", ListaDespachosReglas.SelloEstadoViaje("ABIERTO"));
        }

        [Fact]
        public void LD10_FechasLasPoneElServidorNoElScript()
        {
            // "?documentos=pendientes" vacía Desde y Hasta a propósito; si el script las rellenaba, la pantalla mostraba
            // un rango que no se había aplicado y Buscar bajaba de 6 lotes a 1.
            string js = Js("ListaDespachos");
            Assert.DoesNotContain("establecerFechasPorDefecto", js);
            Assert.DoesNotMatch(new Regex(@"txtFecha(Desde|Hasta)"), js);
            string cs = Cs("ListaDespachos");
            Assert.Contains("ConfigurarFechasPorDefecto();", Metodo(cs, "Page_Load"));
            string pendientes = Metodo(cs, "lnkStatDocumentosPendientes_Click");
            Assert.Contains("txtFechaDesde.Text = string.Empty;", pendientes);
            Assert.Contains("txtFechaHasta.Text = string.Empty;", pendientes);
        }

        [Fact]
        public void LD11_DocumentosDelDetalleEsUnEnlaceYLasPlacasNoSeParten()
        {
            string aspx = Aspx("ListaDespachos"), cs = Cs("ListaDespachos");
            Assert.Contains("<asp:HyperLink ID=\"hlDocumentosLote\"", aspx);
            Assert.DoesNotContain("btnGestionarManifiestos", aspx + cs);
            Assert.DoesNotContain("IrAlExpediente", cs);
            Assert.Contains("hlDocumentosLote.NavigateUrl = UrlExpediente(", cs);
            Assert.Equal(5, Regex.Matches(aspx, @"DataField=""Placa(Tracto|Carreta)""[^>]*ItemStyle-CssClass=""ld-placa""").Count);
            Assert.Matches(@"\.ld-placa\s*\{\s*white-space:\s*nowrap", Css("ListaDespachos"));
        }

        [Fact]
        public void LD12_FiltroDeConductorGuardaSuEstadoYLosContadoresSeActualizan()
        {
            string aspx = Aspx("ListaDespachos"), cs = Cs("ListaDespachos");
            Assert.DoesNotMatch(@"ID=""ddlFiltroConductorViajes""[^>]*EnableViewState=""false""", aspx);
            foreach (string m in new[] { "ddlFiltroConductorViajes_SelectedIndexChanged", "ddlFiltroTipoViajes_SelectedIndexChanged", "btnBuscarViaje_Click" })
                Assert.Contains("CargarViajesDesdeInicio();", Metodo(cs, m));
            foreach (string m in new[] { "btnGuardarCambios_Click", "btnAnularLote_Click", "btnRefrescarViajes_Click", "btnRefrescarLotes_Click" })
                Assert.Contains("EstablecerContadores();", Metodo(cs, m));
        }

        [Fact]
        public void LD9_NuevoDespachoEsUnEnlaceEnElEncabezado()
        {
            string aspx = Aspx("ListaDespachos");
            string acciones = Regex.Match(aspx, @"<div class=""sgv-encabezado-acciones"">(?<c>.*?)</div>", RegexOptions.Singleline).Groups["c"].Value;
            Assert.Contains("<asp:HyperLink ID=\"hlNuevoDespacho\"", acciones);
            Assert.Contains("RegistroDespacho.aspx", acciones);
            Assert.DoesNotContain("ID=\"btnVolver\"", aspx);
            Assert.DoesNotContain("btnVolver_Click", Cs("ListaDespachos"));
        }
    }
}
