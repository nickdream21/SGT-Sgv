using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Catálogos con una plantilla común (E5-16): sgv-panel, sgv-tabla, sgv-btn-icono, modal sin style=, grilla
    /// paginada de 25 filas sin ViewState con su buscador en el servidor, y la caja "Información" pasada a la
    /// ayuda del "?". Leen las páginas como texto sin comentarios, igual que <see cref="AccesibilidadMarcadoTests"/>.
    /// </summary>
    public class CatalogosPlantillaTests
    {
        /// <summary>
        /// Páginas con la plantilla. E5-16a: Tractos y Semirremolques; E5-16b: Choferes, Clientes, Plantas y Peajes;
        /// E5-16c: CatalogoCliente y DestinoDescarga.
        /// </summary>
        public static readonly string[] Paginas =
        {
            "RegistroTractos", "RegistroSemiremolques", "RegistroChoferes", "RegistroClientes",
            "RegistroPlantas", "RegistroPeajes", "CatalogoCliente", "DestinoDescarga"
        };

        /// <summary>Con Activar/Desactivar y modal de edición (DestinoDescarga solo quita destinos).</summary>
        public static readonly string[] PaginasConEstado = Paginas.Where(p => p != "DestinoDescarga").ToArray();

        /// <summary>
        /// Con buscador que filtra en el servidor con <c>ListaCatalogo.Filtrar</c> (las seis Registro*).
        /// CatalogoCliente no tiene buscador (pocas filas por pestaña) y DestinoDescarga busca por pedido exacto en SQL.
        /// </summary>
        public static readonly string[] PaginasConBuscador = Paginas.Where(p => p.StartsWith("Registro", StringComparison.Ordinal)).ToArray();

        public static IEnumerable<object[]> Casos() => Paginas.Select(p => new object[] { p });

        public static IEnumerable<object[]> CasosConEstado() => PaginasConEstado.Select(p => new object[] { p });

        public static IEnumerable<object[]> CasosConBuscador() => PaginasConBuscador.Select(p => new object[] { p });

        /// <summary>Páginas cuyo buscador va en un &lt;div data-enter&gt; (prueba D): las Registro* y DestinoDescarga.</summary>
        public static IEnumerable<object[]> CasosConCajaDeBusqueda() =>
            PaginasConBuscador.Concat(new[] { "DestinoDescarga" }).Select(p => new object[] { p });

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

        private static string Js(string pagina) => File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", pagina + ".js"));

        /// <summary>Script de la página, o vacío si no tiene (DestinoDescarga usa solo SiteMaster.js).</summary>
        private static string JsSiHay(string pagina)
        {
            string ruta = Path.Combine(RaizWeb(), "Scripts", "paginas", pagina + ".js");
            return File.Exists(ruta) ? File.ReadAllText(ruta) : string.Empty;
        }

        private static readonly Regex Grilla = new Regex(@"<asp:GridView\b(?<a>[^>]*)>(?<c>.*?)</asp:GridView\s*>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        private static Match LaGrilla(string aspx)
        {
            var grillas = Grilla.Matches(aspx).Cast<Match>().ToList();
            Assert.True(grillas.Count == 1, "Se esperaba una sola grilla y hay " + grillas.Count);
            return grillas[0];
        }

        // ── (a) Sin estilos en línea ─────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void A_SinStyleNiAnchosEnElMarcado(string pagina)
        {
            // Antes de E5-16 cada página tenía 3 style= en el .aspx (buscador, cabecera y ✕ del modal) y 4
            // ItemStyle-Width; servidas daban 417 (Tractos) y 549 (Semirremolques) style= con el border-collapse.
            string texto = Aspx(pagina) + "\n" + Cs(pagina);
            Assert.Empty(Regex.Matches(texto, @"\bstyle\s*=", RegexOptions.IgnoreCase).Cast<Match>().Select(m => m.Value));
            Assert.Empty(Regex.Matches(texto, @"\b(Item|Header)Style-(Width|Height|HorizontalAlign)\b").Cast<Match>().Select(m => m.Value));
        }

        // ── (b) Plantilla visual ─────────────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void B_PanelesTablaYBotonesDeIcono(string pagina)
        {
            string aspx = Aspx(pagina);
            Assert.Contains("class=\"sgv-panel\"", aspx);
            Assert.Contains("sgv-tabla", LaGrilla(aspx).Groups["a"].Value);
            Assert.DoesNotContain("class=\"card", aspx);
            Assert.DoesNotContain("card-header", aspx);
            Assert.DoesNotContain("thead-light", aspx);

            // Las pestañas de CatalogoCliente siguen siendo LinkButton (B5-9: su Text guarda el contenido en el
            // ViewState, CatalogoClientePestanasTests); fuera de ellas, ningún LinkButton.
            var linkButtons = Regex.Matches(aspx, @"<asp:LinkButton\b[^>]*\bID=""(?<id>\w+)""").Cast<Match>()
                .Select(m => m.Groups["id"].Value).ToList();
            var pestanas = new[] { "lnkProductos", "lnkPlantasCarga", "lnkPlantasDescarga", "lnkRutas" };
            Assert.Empty(pagina == "CatalogoCliente" ? linkButtons.Except(pestanas) : linkButtons);
            Assert.DoesNotMatch(new Regex(@"<asp:LinkButton\b", RegexOptions.IgnoreCase),
                Regex.Replace(aspx, @"<asp:LinkButton\b[^>]*\bID=""(lnkProductos|lnkPlantasCarga|lnkPlantasDescarga|lnkRutas)""", ""));

            if (!PaginasConEstado.Contains(pagina)) return;

            var editar = Regex.Matches(aspx, @"<button\b[^>]*\bbtn-editar\b[^>]*>", RegexOptions.Singleline).Cast<Match>().ToList();
            Assert.NotEmpty(editar);
            foreach (Match b in editar)
            {
                Assert.Contains("sgv-btn-icono", b.Value);
                Assert.Matches(@"\baria-label\s*=\s*['""][^'""]+", b.Value);
            }

            Assert.DoesNotMatch(new Regex(@"class=""modal-title""[^>]*>(?:(?!</h\d>).)*?<i\b", RegexOptions.Singleline), aspx);
            Assert.DoesNotMatch(new Regex(@"class=""sgv-panel-titulo""[^>]*>(?:(?!</h\d>).)*?<i\b", RegexOptions.Singleline), aspx);
        }

        // ── (c) Grilla paginada sin ViewState ────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void C_GrillaPaginadaSinViewStateNiControlesDeServidor(string pagina)
        {
            Match g = LaGrilla(Aspx(pagina));
            string a = g.Groups["a"].Value, c = g.Groups["c"].Value;
            Assert.Contains("EnableViewState=\"false\"", a);
            Assert.Contains("AllowPaging=\"true\"", a);
            Assert.Contains("PageSize=\"25\"", a);
            Assert.Contains("OnPageIndexChanging=", a);
            Assert.Contains("CellSpacing=\"-1\"", a);   // sin él, el GridView escribe style="border-collapse:collapse"
            Assert.DoesNotContain("OnRowCommand", a);
            Assert.Contains("CssClass=\"sgv-paginador\"", c);
            Assert.DoesNotMatch(new Regex(@"<asp:(LinkButton|Button|ImageButton|TextBox|DropDownList|CheckBox)\b", RegexOptions.IgnoreCase), c);
            Assert.Equal(25, WebSGV.Helpers.ListaCatalogo.FilasPorPagina);

            string cs = Cs(pagina);
            Assert.Contains("override void OnPreRender(", cs);
            Assert.Contains("IsRequestBeingRedirected", cs);
            if (PaginasConEstado.Contains(pagina))
                Assert.Contains("TryLeerArgumento(hfArgumentoFila.Value", cs);
            if (PaginasConBuscador.Contains(pagina))
                Assert.Contains("ListaCatalogo.Filtrar(", cs);
            Assert.Contains("ListaCatalogo.IndiceValido(", cs);
            Assert.DoesNotContain("_RowCommand", cs);
        }

        // ── (c2) Activar/Desactivar por el botón oculto ──────────────────────────────────────

        [Theory]
        [MemberData(nameof(CasosConEstado))]
        public void C2_ActivarDesactivarPorBotonOcultoConConfirmacion(string pagina)
        {
            string aspx = Aspx(pagina);
            string c = LaGrilla(aspx).Groups["c"].Value;
            // Los <%# %> de los atributos llevan ">" (el de "%>"): se saltan enteros, en grupo atómico y sin cruzar
            // un "<", para que la coincidencia no se alargue hasta otro botón (p. ej. Editar).
            var filas = Regex.Matches(c, @"<button\b(?:(?><%.*?%>)|[^<>])*?\bdata-accion=""enviarFila""(?:(?><%.*?%>)|[^<>])*>", RegexOptions.Singleline).Cast<Match>().ToList();
            Assert.Single(filas);
            Assert.Contains("data-argumento='<%#: ArgumentoEstado(", filas[0].Value);
            Assert.Contains("data-confirmar='<%#: PreguntaEstado(", filas[0].Value);
            Assert.Contains("EstadoUiHelper.PreguntaCambioEstado(", Cs(pagina));

            Match boton = Regex.Match(aspx, @"<asp:Button\b[^>]*\bID=""btnAccionFila""[^>]*>", RegexOptions.Singleline);
            Assert.True(boton.Success, "Falta <asp:Button ID=\"btnAccionFila\">");
            Assert.Contains("CausesValidation=\"false\"", boton.Value);
            Assert.Contains("OnClick=\"btnAccionFila_Click\"", boton.Value);
            Assert.True(boton.Index < LaGrilla(aspx).Index || boton.Index > LaGrilla(aspx).Index + LaGrilla(aspx).Length,
                "btnAccionFila va fuera de la grilla");
            Assert.Contains("ID=\"hfArgumentoFila\"", aspx);
            Assert.Contains("hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>'", aspx);
            Assert.Contains("btnAccionFila: '<%= btnAccionFila.ClientID %>'", aspx);
            Assert.Contains("enviarFila: function", File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", "SiteMaster.js")));
        }

        // ── (c3) El filtro es el de la última búsqueda ───────────────────────────────────────

        [Theory]
        [MemberData(nameof(CasosConBuscador))]
        public void C3_FiltraConLaUltimaBusquedaNoConLaCaja(string pagina)
        {
            Assert.Contains("ID=\"hfFiltroAplicado\"", Aspx(pagina));
            string cs = Cs(pagina);
            Assert.Contains("string texto = hfFiltroAplicado.Value;", cs);
            Assert.DoesNotContain("string texto = txtBuscar.Text", cs);
            Assert.Contains("ListaCatalogo.Filtrar(dt, texto,", cs);
            Assert.Contains("ListaCatalogo.RecortarBusqueda(", cs);
            // Solo Buscar escribe el filtro aplicado.
            var escrituras = Regex.Matches(cs, @"hfFiltroAplicado\.Value\s*=(?!=)").Cast<Match>().ToList();
            Assert.Single(escrituras);
            int buscar = cs.IndexOf("void btnBuscar_Click(", StringComparison.Ordinal);
            int siguiente = cs.IndexOf("protected void", buscar + 1, StringComparison.Ordinal);
            Assert.True(escrituras[0].Index > buscar && escrituras[0].Index < siguiente, "hfFiltroAplicado se escribe fuera de btnBuscar_Click");
        }

        // ── (d) Buscador en el servidor ──────────────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(CasosConCajaDeBusqueda))]
        public void D_BuscadorConEnterYSinFiltroEnElNavegador(string pagina)
        {
            string aspx = Aspx(pagina);
            Match m = Regex.Match(aspx, @"<div\b[^>]*data-enter=""<%= btnBuscar\.ClientID %>""[^>]*>(?<c>(?:(?!</div>).)*)</div>", RegexOptions.Singleline);
            Assert.True(m.Success, "Falta el <div data-enter> del buscador");
            Assert.Contains("ID=\"txtBuscar\"", m.Groups["c"].Value);
            Assert.Contains("ID=\"btnBuscar\"", m.Groups["c"].Value);
            Assert.DoesNotContain("filtrarTabla", JsSiHay(pagina));
            Assert.DoesNotContain("filtrarTabla", aspx);
        }

        // ── (e) La caja "Información" pasó a la ayuda ────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void E_AyudaEnElEncabezadoYSinCajaInformacion(string pagina)
        {
            string aspx = Aspx(pagina);
            Assert.DoesNotContain("Información:", WebUtility.HtmlDecode(aspx));
            Assert.DoesNotContain("Informaci&#xF3;n:", aspx);

            // E5-21: el "?" es un enlace al tema de la página en Ayuda.aspx (lo abre el modal de Site.Master); el
            // texto que antes estaba en #ayudaPagina está ahora en ContenidoAyuda (lo revisa AyudaTests).
            Match enlace = Regex.Match(aspx, @"<a\b[^>]*\bclass=""sgv-ayuda""(?:<%.*?%>|[^>])*>\?</a>", RegexOptions.Singleline);
            Assert.True(enlace.Success, "Falta el enlace sgv-ayuda");
            Match tema = Regex.Match(enlace.Value, @"data-ayuda=""(?<t>[a-z0-9-]+)""");
            Assert.True(tema.Success, "El enlace sgv-ayuda no tiene data-ayuda");
            Assert.Contains("~/Views/Ayuda.aspx?tema=" + tema.Groups["t"].Value + "\"", enlace.Value);
            Assert.DoesNotContain("id=\"ayudaPagina\"", aspx);
        }

        // ── (f) En el celular la lista va primero ────────────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
        public void F_LaListaSaleAntesQueElAltaEnElCelular(string pagina)
        {
            string aspx = Aspx(pagina);
            int alta = aspx.IndexOf("id=\"altaCatalogo\"", StringComparison.Ordinal);
            int grilla = aspx.IndexOf("<asp:GridView", StringComparison.Ordinal);
            Assert.True(alta > 0, "Falta la columna id=\"altaCatalogo\"");
            Assert.Contains("href=\"#altaCatalogo\"", aspx);

            var columnas = Regex.Matches(aspx, @"<div\b[^>]*\bclass=""[^""]*\border-first\b[^""]*""", RegexOptions.Singleline).Cast<Match>().ToList();
            Assert.Single(columnas);
            Assert.True(columnas[0].Index > alta && columnas[0].Index < grilla,
                "La columna de la lista (con order-first) debe ir después del alta y contener la grilla");
            // Las Registro* parten en md; CatalogoCliente y DestinoDescarga (alta más ancha), en lg.
            Assert.Matches(@"\border-(md|lg)-0\b", columnas[0].Value);
        }

        // ── (g) Propias de cada página (E5-16b) ──────────────────────────────────────────────

        [Fact]
        public void G_Choferes_cargaDeLaListaPlegableYCarnetConDNone()
        {
            string aspx = Aspx("RegistroChoferes");
            string cs = Cs("RegistroChoferes");
            string js = Js("RegistroChoferes");

            // La carga de la lista va plegada salvo con una comparación o un error (CargaAbierta).
            Assert.Contains("<details class=\"sgv-plegable\"<%= CargaAbierta ? \" open\" : \"\" %>>", aspx);
            Assert.Contains("<div class=\"sgv-plegable-cuerpo\">", aspx);
            Assert.Contains("protected bool CargaAbierta", cs);
            Assert.True(aspx.IndexOf("ID=\"btnCompararLista\"", StringComparison.Ordinal) > aspx.IndexOf("<details", StringComparison.Ordinal));

            // grupoCarnet se oculta con d-none (lleva !important): el script tiene que alternar la clase, no
            // style.display, o el campo del carnet no se podría mostrar nunca.
            Assert.Matches(@"<div\b[^>]*\bclass=""[^""]*\bd-none\b[^""]*""[^>]*\bid=""grupoCarnet""", aspx);
            Assert.Contains("classList.toggle('d-none'", js);
            Assert.DoesNotMatch(@"getElementById\('grupo(Carnet|DNI)'\)\.style", js);

            // El validador del tipo oculto no bloquea Registrar: se apaga en el navegador y en el servidor (antes de
            // la validación, que corre después de Page_Load), y cada mensaje va en la columna de su campo.
            Assert.Contains("ValidatorEnable(val, false)", js);
            Assert.Contains("activarValidador(SGV.revCarnet, esCarnet)", js);
            Assert.Contains("revCarnet: '<%= revCarnet.ClientID %>'", aspx);
            Assert.Contains("revCarnet.Enabled = esCarnet;", cs);
            Assert.Contains("revDni.Enabled = !esCarnet;", cs);
            int load = cs.IndexOf("void Page_Load(", StringComparison.Ordinal);
            Assert.True(cs.IndexOf("AjustarValidadoresDocumento();", load, StringComparison.Ordinal) <
                        cs.IndexOf("\n        }", load, StringComparison.Ordinal), "AjustarValidadoresDocumento va en Page_Load");
            foreach (var par in new[] { ("grupoDNI", "revDni"), ("grupoCarnet", "revCarnet") })
            {
                Match grupo = Regex.Match(aspx, @"<div\b[^>]*\bid=""" + par.Item1 + @"""[^>]*>(?<c>.*?)\n {28}</div>", RegexOptions.Singleline);
                Assert.True(grupo.Success, "No se encontró " + par.Item1);
                Assert.Contains("ID=\"" + par.Item2 + "\"", grupo.Groups["c"].Value);
            }

            // La grilla de la comparación (HTML armado en el servidor) sin anchos en línea.
            Assert.Contains("<th class=\\\"sgv-col-acciones\\\">", cs);
        }

        [Fact]
        public void G_Clientes_contactoYTelefonoEnBloquesSinPlaceHolder()
        {
            // M-084: con el contacto vacío el teléfono ya no baja una línea (antes iba tras un <br/>).
            string c = LaGrilla(Aspx("RegistroClientes")).Groups["c"].Value;
            Assert.Contains("<div><%#: Eval(\"contacto\") %></div>", c);
            Assert.Contains("<div class=\"small text-muted\"><%#: Eval(\"telefono\") %></div>", c);
            Assert.DoesNotContain("<asp:PlaceHolder", c);
            Assert.DoesNotMatch(new Regex(@"<br\s*/?>", RegexOptions.IgnoreCase), c);
        }

        // ── (h) Propias de cada página (E5-16c) ──────────────────────────────────────────────

        [Fact]
        public void H_CatalogoCliente_sinBuscadorYVuelveALaPrimeraPagina()
        {
            string aspx = Aspx("CatalogoCliente");
            string cs = Cs("CatalogoCliente");

            // Sin buscador: pocas filas por pestaña.
            Assert.DoesNotContain("txtBuscar", aspx);
            Assert.DoesNotContain("hfFiltroAplicado", aspx);

            // Cambiar de cliente o de pestaña vuelve a la primera página.
            foreach (string manejador in new[] { "void ddlCliente_SelectedIndexChanged(", "void CambiarTab_Command(" })
            {
                int i = cs.IndexOf(manejador, StringComparison.Ordinal);
                Assert.True(i > 0, "Falta " + manejador);
                int fin = cs.IndexOf("\n        }", i, StringComparison.Ordinal);
                Assert.Contains("gvCatalogo.PageIndex = 0;", cs.Substring(i, fin - i));
            }

            // La grilla solo se enlaza con un cliente elegido; el sello General sale sin un Literal por fila.
            Assert.Matches(@"!_grillaEnlazada && !Response\.IsRequestBeingRedirected && idCliente > 0", cs);
            Assert.DoesNotContain("litGeneral", aspx);
            Assert.Contains("<%# EsGeneral(Eval(\"esGeneral\")) ? ", aspx);
            // El aviso de las rutas generales sigue (B5-5) y el modal se reabre con su error (B5-7).
            Assert.Contains("ID=\"pnlNotaRutas\"", aspx);
            Assert.Contains("$('#modalEditar').modal('show')", Js("CatalogoCliente"));
        }

        [Fact]
        public void H_DestinoDescarga_quitarConConfirmacionYFiltroPorPedido()
        {
            string aspx = Aspx("DestinoDescarga");
            string cs = Cs("DestinoDescarga");
            string c = LaGrilla(aspx).Groups["c"].Value;

            var filas = Regex.Matches(c, @"<button\b(?:(?><%.*?%>)|[^<>])*?\bdata-accion=""enviarFila""(?:(?><%.*?%>)|[^<>])*>", RegexOptions.Singleline).Cast<Match>().ToList();
            Assert.Single(filas);
            Assert.Contains("data-argumento='<%#: Eval(\"idDestinoDescarga\") %>'", filas[0].Value);
            Assert.Contains("data-confirmar='<%#: PreguntaQuitar(", filas[0].Value);
            Assert.Contains("data-confirmar-peligro=\"true\"", filas[0].Value);
            Assert.Contains("aria-label='<%#: EtiquetaQuitar(", filas[0].Value);
            Assert.Contains("int.TryParse(hfArgumentoFila.Value, out int id) && id > 0", cs);
            Assert.Contains("hfArgumentoFila: '<%= hfArgumentoFila.ClientID %>'", aspx);
            Assert.Contains("btnAccionFila: '<%= btnAccionFila.ClientID %>'", aspx);
            Assert.DoesNotContain("DataKeyNames", aspx);
            Assert.DoesNotContain("txtBuscarPedido", aspx + cs);

            // Búsqueda exacta por pedido en SQL (no ListaCatalogo.Filtrar) con el filtro aplicado, que solo escriben
            // Buscar y, para mostrar lo recién guardado, Guardar.
            Assert.Contains("DestinoDescargaService.Listar(pedido)", cs);
            Assert.Contains("ListaCatalogo.RecortarBusqueda(hfFiltroAplicado.Value)", cs);
            Assert.DoesNotContain("ListaCatalogo.Filtrar(", cs);
            var escrituras = Regex.Matches(cs, @"hfFiltroAplicado\.Value\s*=(?!=)").Cast<Match>().ToList();
            Assert.Equal(2, escrituras.Count);
            foreach (Match m in escrituras)
            {
                bool dentro = new[] { "void btnBuscar_Click(", "void btnGuardarManual_Click(" }.Any(n =>
                {
                    int inicio = cs.IndexOf(n, StringComparison.Ordinal);
                    int siguiente = cs.IndexOf("protected void", inicio + 1, StringComparison.Ordinal);
                    return inicio > 0 && m.Index > inicio && (siguiente < 0 || m.Index < siguiente);
                });
                Assert.True(dentro, "hfFiltroAplicado se escribe fuera de btnBuscar_Click y btnGuardarManual_Click");
            }

            // La carga de la lista va plegada salvo con una vista previa o un error (CargaAbierta).
            Assert.Contains("<details class=\"sgv-plegable\"<%= CargaAbierta ? \" open\" : \"\" %>>", aspx);
            Assert.Contains("protected bool CargaAbierta", cs);
            Assert.True(aspx.IndexOf("ID=\"btnLeer\"", StringComparison.Ordinal) > aspx.IndexOf("<details", StringComparison.Ordinal));
        }
    }
}
