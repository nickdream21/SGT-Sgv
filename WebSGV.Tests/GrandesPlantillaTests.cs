using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Pantallas grandes con la plantilla común (E5-18, fase 4 de M-073): sgv-panel, sgv-tabs, sgv-resumen en vez de
    /// tarjetas, sgv-tabla con sgv-paginador y grillas sin ViewState, filtros en details.sgv-filtros y sellos
    /// sgv-estado. La lista crece con cada parte de E5-18 (a: ListaDespachos; b: RegistroDespacho). Leen las páginas
    /// como texto sin comentarios, igual que <see cref="BuscarEditarPlantillaTests"/>. Las pruebas LD* fijan
    /// ListaDespachos (M-082: las pestañas y el resumen solo en las dos listas) y las RD*, RegistroDespacho (M-081:
    /// columna Estado en las vistas previas; M-086: tira de pasos del día).
    /// </summary>
    public class GrandesPlantillaTests
    {
        public static readonly string[] Paginas = { "ListaDespachos", "RegistroDespacho", "RegistroManualLote", "EditarLote" };

        /// <summary>Páginas con filtros de búsqueda: llevan el details.sgv-filtros común (RegistroDespacho no filtra).</summary>
        private static readonly string[] PaginasConFiltros = { "ListaDespachos" };

        /// <summary>Clases y nombres retirados en E5-18a y E5-18b: no vuelven en el marcado, el código, el script ni la hoja.</summary>
        private static readonly string[] ClasesRetiradas =
        {
            "ld-stat", "ld-pager", "summary-card", "section-card", "section-header", "estado-abierto",
            "tipo-internacional", "tipo-nacional", "ld-chip", "ld-estado-", "active-nav", "btn-nav", "empty-data",
            "doc-panel-header", "GetEstadoDespachoClass", "GetManifiestoBadgeClass", "fa-info-circle",
            // E5-18b (RegistroDespacho)
            "rd-chip", "rd-stepper", "rd-fila-programacion", "rd-casos", "rd-step-header", "rd-summary-header",
            "rd-viaje-header", "rd-list-header", "rd-subtitle", "litProgramacionImp", "litProgramacionDesc",
            "litYaRegistrado", "badge bg-", "ChipManifiesto", "ChipEstadoViaje"
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

        /// <summary>
        /// Controles .ascx que registra la página (<c>&lt;%@ Register Src %&gt;</c>). E6-21 a E6-28 parten RegistroDespacho y
        /// ListaDespachos en controles: lo que se mueva a un control sigue bajo estas reglas porque <see cref="Aspx"/> y
        /// <see cref="Cs"/> leen la página junto con sus controles.
        /// </summary>
        private static List<string> Controles(string pagina) =>
            FuentesWeb.ControlesRegistrados(RaizWeb(), Path.Combine(RaizWeb(), "Views", pagina + ".aspx"));

        /// <summary>Marcado de la página y de sus controles, sin comentarios.</summary>
        private static string Aspx(string pagina) =>
            string.Join("\n", new[] { PaginaDe("Views/" + pagina + ".aspx").Texto }
                .Concat(Controles(pagina).Select(c => SinComentarios(File.ReadAllText(c)))));

        /// <summary>Code-behind de la página y de sus controles.</summary>
        private static string Cs(string pagina) =>
            string.Join("\n", new[] { Path.Combine(RaizWeb(), "Views", pagina + ".aspx.cs") }
                .Concat(Controles(pagina).Select(c => c + ".cs").Where(File.Exists))
                .Select(File.ReadAllText));

        [Fact]
        public void Lector_JuntaLaPaginaConLosControlesQueRegistra()
        {
            string dir = Path.Combine(Path.GetTempPath(), "sgv-e6-4-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "Views", "Controles"));
                string pagina = Path.Combine(dir, "Views", "P.aspx");
                File.WriteAllText(pagina,
                    "<%@ Page Language=\"C#\" %>\n" +
                    "<%@ Register Src=\"~/Views/Controles/A.ascx\" TagPrefix=\"sgv\" TagName=\"A\" %>\n" +
                    "<%@ Register TagPrefix=\"sgv\" TagName=\"B\" Src=\"Controles/B.ascx\" %>\n" +
                    "<%@ Register Assembly=\"X\" Namespace=\"Y\" TagPrefix=\"z\" %>\n");
                var controles = FuentesWeb.ControlesRegistrados(dir, pagina);
                Assert.Equal(new[]
                {
                    Path.Combine(dir, "Views", "Controles", "A.ascx"),
                    Path.Combine(dir, "Views", "Controles", "B.ascx"),
                }, controles);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

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
            if (PaginasConFiltros.Contains(pagina))
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
            foreach (var vista in new[] { VistaListaDespachos.DetalleViaje, VistaListaDespachos.DetalleLote })
                Assert.False(ListaDespachosReglas.MuestraNavegacion(vista), vista + " no lleva las pestañas (M-082)");

            string cs = Cs("ListaDespachos");
            Assert.Matches(@"pnlNavegacionListas\.Visible = (ListaDespachosReglas\.)?MuestraNavegacion\(vista\)", Metodo(cs, "MostrarVista"));
            // Toda pantalla pasa por MostrarVista: ningún otro método enciende un panel a mano.
            Assert.Single(Regex.Matches(cs, @"pnlListaLotes\.Visible\s*="));
            // El registro manual es RegistroManualLote.aspx desde E6-27: ya no es una vista de esta página.
            Assert.DoesNotContain("pnlManifiestosLote", cs);
            Assert.DoesNotContain("RegistroManual", Enum.GetNames(typeof(VistaListaDespachos)));
            // La edición de un lote es EditarLote.aspx desde E6-28.
            Assert.DoesNotContain("EdicionLote", Enum.GetNames(typeof(VistaListaDespachos)));
            Assert.DoesNotContain("pnlEdicionLote", cs);
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
        [InlineData("VER|12_NOPEDIDO_2026-01-05_DESCARGA_1_7", AccionFilaLista.VerLote, "12_NOPEDIDO_2026-01-05_DESCARGA_1_7", 0)]
        [InlineData("VERANULADO|3_123-A_2025-12-31_CARGA_1_10", AccionFilaLista.VerLoteAnulado, "3_123-A_2025-12-31_CARGA_1_10", 0)]
        [InlineData("VIAJE|42", AccionFilaLista.VerViaje, null, 42)]
        // Pedido vacío: el SP arma el id con ISNULL(numeroPedido, 'NOPEDIDO') y un '' guardado deja "1__fecha" (hay lotes activos así).
        [InlineData("VER|1__2026-08-26_CARGA_0_2", AccionFilaLista.VerLote, "1__2026-08-26_CARGA_0_2", 0)]
        [InlineData("VERANULADO|1__2026-08-26_CARGA_0_2", AccionFilaLista.VerLoteAnulado, "1__2026-08-26_CARGA_0_2", 0)]
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
        // "Editar" es un enlace a EditarLote.aspx desde E6-28: un "EDITAR|lote" de una pestaña vieja ya no se lee.
        [InlineData("EDITAR|12_NOPEDIDO_2026-01-05_DESCARGA_1_7")]
        [InlineData("EDITAR|1__2026-08-26_CARGA_0_2")]
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
            // Los manifiestos (con su sello "Sin adjuntar") están en el registro manual desde E6-27.
            Assert.Contains("ListaDespachosReglas.SelloSinAdjuntar(", Aspx("RegistroManualLote"));
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
            // InsigniaDocumento pasó a RegistroManualLoteReglas (E6-27): un sello con texto, sin icono.
            Assert.DoesNotContain("<i class", Metodo(SiHay("Services", "Despachos", "RegistroManualLoteReglas.cs"), "InsigniaDocumento"));
            Assert.Equal(EstadoUiHelper.Sello("sin PDF", TonoEstado.Alerta), RegistroManualLoteReglas.InsigniaDocumento(false, "sin PDF"));
            Assert.Equal(EstadoUiHelper.Sello("PDF", TonoEstado.Ok), RegistroManualLoteReglas.InsigniaDocumento(true, "PDF"));

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
            // 4 en esta página y 1 en las guías del registro manual (E6-27), cada hoja con su .ld-placa.
            const string placa = @"DataField=""Placa(Tracto|Carreta)""[^>]*ItemStyle-CssClass=""ld-placa""";
            Assert.Equal(4, Regex.Matches(aspx, placa).Count);
            Assert.Single(Regex.Matches(Aspx("RegistroManualLote"), placa));
            foreach (string hoja in new[] { "ListaDespachos", "RegistroManualLote" })
                Assert.Matches(@"\.ld-placa\s*\{\s*white-space:\s*nowrap", Css(hoja));
        }

        [Fact]
        public void LD12_FiltroDeConductorGuardaSuEstadoYLosContadoresSeActualizan()
        {
            string aspx = Aspx("ListaDespachos"), cs = Cs("ListaDespachos");
            Assert.DoesNotMatch(@"ID=""ddlFiltroConductorViajes""[^>]*EnableViewState=""false""", aspx);
            foreach (string m in new[] { "ddlFiltroConductorViajes_SelectedIndexChanged", "ddlFiltroTipoViajes_SelectedIndexChanged", "btnBuscarViaje_Click" })
                Assert.Contains("CargarViajesDesdeInicio();", Metodo(cs, m));
            // Guardar y anular un lote son de EditarLote.aspx desde E6-28 (Guardar vuelve por GET y recalcula al abrir).
            foreach (string m in new[] { "btnRefrescarViajes_Click", "btnRefrescarLotes_Click" })
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

        // ── RD: RegistroDespacho ─────────────────────────────────────────────────────────────

        /// <summary>Tabla del marcado que contiene el Repeater <paramref name="repeater"/> (desde su table hasta el /table).</summary>
        private static string TablaDe(string aspx, string repeater)
        {
            int r = aspx.IndexOf("<asp:Repeater ID=\"" + repeater + "\"", StringComparison.Ordinal);
            Assert.True(r > 0, "No se encontró el Repeater " + repeater);
            int ini = aspx.LastIndexOf("<table", r, StringComparison.Ordinal);
            int fin = aspx.IndexOf("</table>", r, StringComparison.Ordinal);
            return aspx.Substring(ini, fin - ini);
        }

        [Theory]
        [InlineData("rptTractosImportados", "litEstadoImp", 7)]
        [InlineData("rptDescargas", "litEstadoDesc", 8)]
        public void RD1_VistasPreviasConColumnaEstado(string repeater, string literal, int columnas)
        {
            string tabla = TablaDe(Aspx("RegistroDespacho"), repeater);
            var th = Regex.Matches(tabla, @"<th\b[^>]*>(?<t>.*?)</th>", RegexOptions.Singleline).Cast<Match>().ToList();
            Assert.Equal(columnas, th.Count);
            Assert.Equal("Estado", th.Last().Groups["t"].Value.Trim());

            string fila = Regex.Match(tabla, @"<ItemTemplate>(?<c>.*?)</ItemTemplate>", RegexOptions.Singleline).Groups["c"].Value;
            // Una sola fila por tracto (sin la fila extra de antes) y tantas celdas como encabezados: la de la descarga
            // tenía 7 columnas y su fila extra sumaba 6.
            Assert.Single(Regex.Matches(fila, @"<tr\b"));
            Assert.Equal(columnas, Regex.Matches(fila, @"<td\b").Count);
            Assert.Contains("ID=\"" + literal + "\"", fila);
            Assert.Contains("FindControl(\"" + literal + "\")", Cs("RegistroDespacho"));
        }

        [Fact]
        public void RD2_EstadoDeLaFilaDelZip()
        {
            var programado = new PropuestaCarga { IdDespachoProgramado = 5, ChoferProgramado = "A", TractoProgramado = "T1", CarretaProgramada = "C1" };
            string html = RegistroDespachoReglas.EstadoFilaImportacionHtml(programado, false);
            Assert.Contains("sgv-estado-info\">Completa el despacho programado<", html);
            Assert.Contains("<strong>A</strong>", html);
            Assert.Contains("<strong>C1</strong>", html);

            var conAviso = new PropuestaCarga { EnProgramacion = true, ChoferProgramado = "A", TractoProgramado = "T1", Avisos = { "x<y" } };
            html = RegistroDespachoReglas.EstadoFilaImportacionHtml(conAviso, false);
            Assert.Contains("rd-notice-warn", html);
            Assert.Contains("x&lt;y", html);
            Assert.DoesNotContain(">Listo<", html);

            var sinAvisos = new PropuestaCarga { EnProgramacion = true, ChoferProgramado = "<b>", TractoProgramado = "T1", Horario = "06:00" };
            html = RegistroDespachoReglas.EstadoFilaImportacionHtml(sinAvisos, false);
            Assert.Contains("sgv-estado-ok\">Listo<", html);
            Assert.Contains("&lt;b&gt;", html);
            Assert.Contains("06:00", html);

            Assert.Contains("sgv-estado-alerta\">Ya registrado<", RegistroDespachoReglas.EstadoFilaImportacionHtml(programado, true));
            Assert.Contains("sgv-estado-alerta\">Ya registrado<", RegistroDespachoReglas.EstadoFilaImportacionHtml(null, true));
            Assert.Equal("<span class=\"rd-sin-datos\">—</span>", RegistroDespachoReglas.EstadoFilaImportacionHtml(null, false));
            Assert.DoesNotContain("<tr", RegistroDespachoReglas.EstadoFilaImportacionHtml(programado, false));
        }

        [Fact]
        public void RD2_EstadoDeLaFilaDeLaDescarga()
        {
            var p = new PropuestaDescarga { EnProgramacion = true, ChoferProgramado = "A", TractoProgramado = "T1" };
            string html = RegistroDespachoReglas.EstadoFilaDescargaHtml(p);
            Assert.Contains("sgv-estado-ok\">Listo<", html);
            Assert.Contains("<strong>A</strong>", html);
            Assert.Contains("sin cambio de chofer", html);

            p.Cambio = "B&C";
            p.Avisos.Add("x<y");
            html = RegistroDespachoReglas.EstadoFilaDescargaHtml(p);
            Assert.Contains("sale <strong>B&amp;C</strong>", html);
            Assert.Contains("rd-notice-warn", html);
            Assert.Contains("x&lt;y", html);
            Assert.DoesNotContain(">Listo<", html);

            Assert.Equal(EstadoUiHelper.Sello("No está en la programación", TonoEstado.Neutro), RegistroDespachoReglas.EstadoFilaDescargaHtml(new PropuestaDescarga()));
            Assert.Equal("<span class=\"rd-sin-datos\">—</span>", RegistroDespachoReglas.EstadoFilaDescargaHtml(null));
            Assert.DoesNotContain("<tr", html);
        }

        [Fact]
        public void RD3_PistasDePlacaConDataDato()
        {
            string aspx = Aspx("RegistroDespacho");
            var celdas = Regex.Matches(aspx, @"<td class=""rd-col-placa"">(?<c>.*?)</td>", RegexOptions.Singleline).Cast<Match>().ToList();
            Assert.True(celdas.Count >= 7, "Se esperaban las celdas de placa del Word, del ZIP y de la descarga");
            foreach (Match celda in celdas)
                foreach (Match pista in Regex.Matches(celda.Groups["c"].Value, @"<small class=""rd-doc-dato""[^>]*>"))
                    Assert.Contains("data-dato=", pista.Value);

            string js = Js("RegistroDespacho");
            Assert.Contains("getAttribute('data-dato')", js);
            Assert.DoesNotContain(@"/^\s*Word:", js);
            Assert.DoesNotContain(".style.", js);
            // Entregas del Word: se cuentan las filas registradas por su atributo, no por el texto de un chip.
            Assert.Contains("tr[data-registrada=\"1\"]", js);
            Assert.Contains("data-registrada='<%#: (bool)Eval(\"YaRegistrada\") ? \"1\" : \"0\" %>'", aspx);

            var t = new TractoImportado { IdCarreta = 3, PlacaCarreta = "ABC-123" };
            Assert.Equal("ABC-123", RegistroDespachoReglas.DatoPistaCarreta(t));
            t.CarretaSugerida = true;
            Assert.Equal("", RegistroDespachoReglas.DatoPistaCarreta(t));
            t.Programacion = new PropuestaCarga { EnProgramacion = true, CarretaProgramada = "XYZ-999" };
            Assert.Equal("XYZ-999", RegistroDespachoReglas.DatoPistaCarreta(t));
            Assert.Equal("", RegistroDespachoReglas.DatoPistaCarreta(new TractoImportado { PlacaCarreta = "ABC-123" }));
        }

        [Fact]
        public void RD4_TiraDePasosYEnlaceALosPendientes()
        {
            string aspx = Aspx("RegistroDespacho"), cs = Cs("RegistroDespacho");
            string tira = Regex.Match(aspx, @"<ol class=""rd-pasos""[^>]*>(?<c>.*?)</ol>", RegexOptions.Singleline).Groups["c"].Value;
            Assert.Equal(3, Regex.Matches(tira, @"<li\b").Count);
            Assert.Contains("href=\"ListaDespachos.aspx?documentos=pendientes\"", tira);
            for (int n = 1; n <= 3; n++) Assert.Contains("<li<%= AtributosPaso(" + n + ") %>>", tira);
            // La tira va dentro de UpdatePanelMain (se repinta con cada vista previa).
            int panel = aspx.IndexOf("ID=\"UpdatePanelMain\"", StringComparison.Ordinal);
            Assert.True(panel > 0 && aspx.IndexOf("<ol class=\"rd-pasos\"", StringComparison.Ordinal) > panel);

            Assert.Equal(1, RegistroDespachoReglas.PasoActivo(true, false));
            Assert.Equal(2, RegistroDespachoReglas.PasoActivo(false, true));
            Assert.Equal(1, RegistroDespachoReglas.PasoActivo(false, false));
            Assert.Equal(" class=\"activo\" aria-current=\"step\"", RegistroDespachoReglas.AtributosPaso(2, 2));
            Assert.Equal("", RegistroDespachoReglas.AtributosPaso(1, 2));

            Match enlace = Regex.Match(aspx, @"<asp:HyperLink ID=""hlVerLotesPendientes""[^>]*/>", RegexOptions.Singleline);
            Assert.True(enlace.Success);
            Assert.Contains("EnableViewState=\"false\"", enlace.Value);
            Assert.Contains("Visible=\"false\"", enlace.Value);
            Assert.Contains("NavigateUrl=\"~/Views/ListaDespachos.aspx?documentos=pendientes\"", enlace.Value);
            // Solo si programar salió bien: después del return de "No se programó ningún despacho".
            string programar = Metodo(cs, "btnProgramarDespachos_Click");
            int sinCreados = programar.IndexOf("if (r.Creados.Count == 0)", StringComparison.Ordinal);
            int visible = programar.IndexOf("hlVerLotesPendientes.Visible = true;", StringComparison.Ordinal);
            Assert.True(sinCreados > 0 && visible > programar.IndexOf("return;", sinCreados, StringComparison.Ordinal));
            Assert.Single(Regex.Matches(cs, @"hlVerLotesPendientes\.Visible\s*="));

            // Los pasos del lote a mano: ocultos con d-none (sin style), y el script los muestra con classList.
            Assert.Matches(@"<ol class=""rd-pasos rd-pasos-manual d-none"" id=""rdStepper""", aspx);
            string js = Js("RegistroDespacho");
            Assert.Contains("classList.toggle('d-none'", js);
            Assert.DoesNotContain("style.display", js);
        }

        [Fact]
        public void RD5_SelloDelLoteActivo()
        {
            Assert.Equal("Lote activo · 1 conductor", RegistroDespachoReglas.TextoEstadoLote(1));
            Assert.Equal("Lote activo · 3 conductores", RegistroDespachoReglas.TextoEstadoLote(3));
            Assert.Equal("Lote activo · 0 conductores", RegistroDespachoReglas.TextoEstadoLote(0));
            Assert.Matches(@"<asp:Label ID=""lblEstadoLote"" runat=""server"" CssClass=""sgv-estado sgv-estado-ok""", Aspx("RegistroDespacho"));
            Assert.Equal(2, Regex.Matches(Cs("RegistroDespacho"), @"lblEstadoLote\.Text = RegistroDespachoReglas\.TextoEstadoLote\(").Count);

            // Los sellos de la tabla de conductores y de las entregas, con texto y tono (sin iconos).
            Assert.Contains("sgv-estado-ok\">Completo<", RegistroDespachoReglas.SelloManifiesto("Completo"));
            Assert.Contains("sgv-estado-alerta\">Parcial<", RegistroDespachoReglas.SelloManifiesto("Parcial"));
            Assert.Contains("rd-sin-datos", RegistroDespachoReglas.SelloManifiesto("N/A"));
            Assert.Equal("<span class=\"sgv-estado\">Por asignar</span>", RegistroDespachoReglas.SelloEstadoViaje("Por asignar"));
            Assert.Contains("VP-1 &lt;x&gt;", RegistroDespachoReglas.SelloEstadoViaje("VP-1 <x>"));
            Assert.Contains("sgv-estado-info\">Lugar de entrega<", RegistroDespachoReglas.SelloEntrega("Lugar de entrega", TonoEstado.Info));
        }

        [Fact]
        public void RD6_ValidacionSinColoresEnLinea()
        {
            // E5-20a: el is-invalid de los validadores lo pone SiteMaster.js (un solo envoltorio de
            // ValidatorUpdateDisplay); RegistroDespacho-2.js ya no existe ni se enlaza.
            Assert.Equal("", SiHay("Scripts", "paginas", "RegistroDespacho-2.js"));
            Assert.DoesNotContain("RegistroDespacho-2.js", Aspx("RegistroDespacho"));
            // La limpieza que hacía RegistroDespacho-2.js (los combos que marca el servidor sin validador, p. ej.
            // conductor y tracto al leer documentos, se desmarcan al elegir, también en el select2) vive en
            // SiteMaster.js, por jQuery para recibir el .trigger('change') de select2.
            string comun = SiHay("Scripts", "paginas", "SiteMaster.js");
            Assert.Contains("window.jQuery(document).on('input change', alCorregir)", comun);
            Assert.Matches(@"if \(String\(el\.value \|\| ''\)\.trim\(\) !== ''\) \{\s*el\.classList\.remove\('is-invalid'\);\s*el\.removeAttribute\('aria-invalid'\);", comun);
            // El select2 inválido lo pinta sgv-base.css (M-143, sin !important); RegistroDespacho.css le da la
            // altura de un botón en el celular (M-145).
            string css = Css("RegistroDespacho");
            Assert.DoesNotContain("select.is-invalid + .select2-container", css);
            Assert.Contains("select.is-invalid + .select2-container.select2-container--default .select2-selection--single", SiHay("Content", "sgv-base.css"));
            Assert.Matches(@"@media \(max-width: 767\.98px\) \{[^@]*select2-selection--single \{ height: 2\.75rem; \}", css);
        }
    }
}
