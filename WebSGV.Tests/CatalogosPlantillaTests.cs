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
        /// Páginas con la plantilla. E5-16a: Tractos y Semirremolques; E5-16b la amplía a las 8 (Choferes, Clientes,
        /// Plantas, Peajes, CatalogoCliente y DestinoDescarga).
        /// </summary>
        public static readonly string[] Paginas = { "RegistroTractos", "RegistroSemiremolques" };

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

        private static string Js(string pagina) => File.ReadAllText(Path.Combine(RaizWeb(), "Scripts", "paginas", pagina + ".js"));

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
            Assert.Contains("TryLeerArgumento(hfArgumentoFila.Value", cs);
            Assert.Contains("ListaCatalogo.Filtrar(", cs);
            Assert.Contains("ListaCatalogo.IndiceValido(", cs);
            Assert.DoesNotContain("_RowCommand", cs);
        }

        // ── (c2) Activar/Desactivar por el botón oculto ──────────────────────────────────────

        [Theory]
        [MemberData(nameof(Casos))]
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
        [MemberData(nameof(Casos))]
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
        [MemberData(nameof(Casos))]
        public void D_BuscadorConEnterYSinFiltroEnElNavegador(string pagina)
        {
            string aspx = Aspx(pagina);
            Match m = Regex.Match(aspx, @"<div\b[^>]*data-enter=""<%= btnBuscar\.ClientID %>""[^>]*>(?<c>(?:(?!</div>).)*)</div>", RegexOptions.Singleline);
            Assert.True(m.Success, "Falta el <div data-enter> del buscador");
            Assert.Contains("ID=\"txtBuscar\"", m.Groups["c"].Value);
            Assert.Contains("ID=\"btnBuscar\"", m.Groups["c"].Value);
            Assert.DoesNotContain("filtrarTabla", Js(pagina));
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

            Match boton = Regex.Match(aspx, @"<button\b[^>]*\bclass=""sgv-ayuda""[^>]*>", RegexOptions.Singleline);
            Assert.True(boton.Success, "Falta el botón sgv-ayuda");
            Match destino = Regex.Match(boton.Value, @"aria-controls=""(?<id>[\w-]+)""");
            Assert.True(destino.Success, "El botón sgv-ayuda no tiene aria-controls");
            string id = destino.Groups["id"].Value;
            Assert.Contains("data-target=\"#" + id + "\"", boton.Value);

            Match bloque = Regex.Match(aspx, @"<div\b[^>]*\bid=""" + Regex.Escape(id) + @"""[^>]*>(?<c>.*?)</div>", RegexOptions.Singleline);
            Assert.True(bloque.Success, "No existe el elemento id=\"" + id + "\" de la ayuda");
            string texto = Regex.Replace(WebUtility.HtmlDecode(bloque.Groups["c"].Value), "<[^>]+>", " ").Trim();
            Assert.True(texto.Length >= 20, "La ayuda está vacía");
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
            Assert.Contains("order-md-0", columnas[0].Value);
        }
    }
}
