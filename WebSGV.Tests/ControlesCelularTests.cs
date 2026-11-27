using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Controles y textos vistos en el celular (M-252, M-253) y el Login en escritorio bajo (M-254):
    /// selectores de archivo, temas de Ayuda, radios/casillas de EditarLote, "Sin pedido" en ListaDespachos
    /// y la media de alto de Login.css.
    /// </summary>
    public class ControlesCelularTests
    {
        private static string Leer(string relativo) =>
            File.ReadAllText(Path.Combine(Integracion.BaseDePruebas.RaizRepo, "WebSGV", relativo.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

        /// <summary>Bloque de un @media (sin comentarios) que contiene el texto dado.</summary>
        private static string BloqueMedia(string css, string condicion, string contiene)
        {
            string limpio = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            int i = 0;
            while ((i = limpio.IndexOf("@media " + condicion, i, StringComparison.Ordinal)) >= 0)
            {
                int a = limpio.IndexOf('{', i), nivel = 0, k = a;
                for (; k < limpio.Length; k++)
                {
                    if (limpio[k] == '{') nivel++;
                    else if (limpio[k] == '}' && --nivel == 0) break;
                }
                string bloque = limpio.Substring(a, k - a + 1);
                if (bloque.Contains(contiene)) return bloque;
                i = k;
            }
            return null;
        }

        private const string Celular = "(max-width: 767.98px)";

        // ---- M-252 ----

        [Fact]
        public void SelectorDeArchivos_EnElCelularCon16pxY44pxDeAlto()
        {
            string bloque = BloqueMedia(Leer("Content/paginas/SiteMaster.css"), Celular, "input[type=file]");
            Assert.NotNull(bloque);
            Assert.Contains("input[type=file] { font-size: 1rem; min-height: 2.75rem; }", bloque);
            Assert.Contains("input[type=file]::file-selector-button { min-height: 2.75rem; padding: .5rem 1rem; }", bloque);
            Assert.DoesNotContain("!important", bloque);
            // En escritorio sigue el tamaño de antes.
            Assert.Contains("input[type=file] { font-size: .875rem;", Leer("Content/paginas/SiteMaster.css"));
        }

        [Fact]
        public void FacturasFlete_SuSelectorTambienA16pxEnElCelular()
        {
            string css = Leer("Content/paginas/FacturasFlete.css");
            // La regla general (.85rem) gana a la de SiteMaster por especificidad: la del celular la corrige.
            Assert.Contains(".ff-pagina input[type=file] { font-size: .85rem;", css);
            string bloque = BloqueMedia(css, Celular, ".ff-pagina input[type=file]");
            Assert.NotNull(bloque);
            Assert.Contains(".ff-pagina input[type=file] { font-size: 1rem; }", bloque);
        }

        [Fact]
        public void Ayuda_TemasDelIndiceConAreaDeToqueEnElCelular()
        {
            string bloque = BloqueMedia(Leer("Content/sgv-base.css"), Celular, ".sgv-ayuda-indice");
            Assert.NotNull(bloque);
            // block (no inline-block): en un tema de dos líneas la viñeta queda en la primera.
            Assert.Contains(".sgv-ayuda-indice li > a { display: block; padding: .625rem 0; }", bloque);
            // El índice se arma con <li><a href=...> (Ayuda.aspx.cs).
            Assert.Contains("sb.Append(\"<li><a href=\\\"\")", Leer("Views/Ayuda.aspx.cs"));
        }

        [Fact]
        public void EditarLote_RadiosYCasillasConEtiquetaDe44pxEnElCelular()
        {
            string aspx = Leer("Views/EditarLote.aspx");
            Assert.Matches(@"ID=""rblAmbitoEdit""[^>]*CssClass=""rbl-inline""", aspx);
            Assert.Matches(@"RepeatLayout=""Flow""", aspx);
            Assert.Equal(2, Regex.Matches(aspx, @"<asp:CheckBox[^>]*CssClass=""ld-quitar-doc""").Count);

            string bloque = BloqueMedia(Leer("Content/paginas/EditarLote.css"), Celular, ".rbl-inline");
            Assert.NotNull(bloque);
            Assert.Matches(@"\.rbl-inline input\[type=radio\],\s*\.ld-quitar-doc input\[type=checkbox\] \{ width: 1\.25rem; height: 1\.25rem;", bloque);
            Assert.Matches(@"\.rbl-inline label,\s*\.ld-quitar-doc label \{ display: inline-flex; align-items: center; min-height: 2\.75rem;", bloque);
            Assert.DoesNotContain("!important", bloque);
        }

        [Fact]
        public void RegistroManualLote_SuSelectorOcultoNoTomaElAltoDelCelular()
        {
            // .ld-doc-input (0,1,0) perdía ante input[type=file] (0,1,1) de SiteMaster.css: quedaba en 1x44.
            Assert.Matches(@"input\[type=file\]\.ld-doc-input \{[^}]*height: 1px;[^}]*min-height: 0;",
                Leer("Content/paginas/RegistroManualLote.css"));
        }

        // ---- M-253 ----

        [Fact]
        public void PedidoMostrado_CargaSinPedidoDiceSinPedido()
        {
            var lote = new LoteRegistrado { TipoOperacion = "CARGA" };
            Assert.Equal("Sin pedido", lote.PedidoMostrado);
            Assert.Equal("Sin pedido", LoteRegistrado.TextoSinPedido);

            Assert.Equal("4500123", new LoteRegistrado { TipoOperacion = "CARGA", NumeroPedido = "4500123" }.PedidoMostrado);
            // Con factura (o de un tercero) no se marca: se muestra el pedido tal cual (vacío → "—" en la grilla).
            Assert.True(string.IsNullOrEmpty(new LoteRegistrado { TipoOperacion = "CARGA", NumeroFactura = "F001-1" }.PedidoMostrado));
            Assert.True(string.IsNullOrEmpty(new LoteRegistrado { TipoOperacion = "CARGA", PerfilDocumentos = "TERCERO" }.PedidoMostrado));
        }

        [Fact]
        public void NombreLote_ConPedido()
        {
            Assert.Equal("pedido 4500123", ListaDespachosReglas.NombreLote("4500123", "Cliente SA", new DateTime(2026, 11, 3)));
            Assert.Equal("pedido 4500123", ListaDespachosReglas.NombreLote(" 4500123 ", null, null));
        }

        [Fact]
        public void NombreLote_SinPedidoConClienteYFecha()
        {
            var fecha = new DateTime(2026, 11, 3);
            Assert.Equal("lote sin pedido de Cliente SA del 03/11/2026", ListaDespachosReglas.NombreLote("—", "Cliente SA", fecha));
            Assert.Equal("lote sin pedido de Cliente SA del 03/11/2026", ListaDespachosReglas.NombreLote("", "Cliente SA", fecha));
            Assert.Equal("lote sin pedido de Cliente SA del 03/11/2026", ListaDespachosReglas.NombreLote("Sin pedido", "Cliente SA", fecha));
            Assert.Equal("lote sin pedido de Cliente SA del 03/11/2026", ListaDespachosReglas.NombreLote(DBNull.Value, "Cliente SA", (DateTime?)fecha));
        }

        [Fact]
        public void NombreLote_SinPedidoOmiteLasPartesQueFaltan()
        {
            Assert.Equal("lote sin pedido", ListaDespachosReglas.NombreLote(null, null, null));
            Assert.Equal("lote sin pedido", ListaDespachosReglas.NombreLote(DBNull.Value, DBNull.Value, DBNull.Value));
            Assert.Equal("lote sin pedido de Cliente SA", ListaDespachosReglas.NombreLote(null, "Cliente SA", null));
            Assert.Equal("lote sin pedido del 03/11/2026", ListaDespachosReglas.NombreLote("—", " ", new DateTime(2026, 11, 3)));
        }

        [Fact]
        public void ListaDespachos_AriaLabelsConElNombreDelLote()
        {
            string aspx = Leer("Views/ListaDespachos.aspx");
            Assert.Equal(4, Regex.Matches(aspx, Regex.Escape("ListaDespachosReglas.NombreLote(Eval(\"NumeroPedido\"), Eval(\"NombreCliente\"), Eval(\"FechaProgramacion\"))")).Count);
            Assert.DoesNotMatch(@"aria-label='[^']*PedidoMostrado", aspx);
            Assert.DoesNotContain("del pedido \" + Eval(", aspx);
        }

        [Fact]
        public void NingunaPaginaNiServicioUsaFaltanDocumentosComoPedido()
        {
            string raiz = Path.Combine(Integracion.BaseDePruebas.RaizRepo, "WebSGV");
            var conTexto = new[] { "Views", "Services", "Models" }
                .SelectMany(d => Directory.EnumerateFiles(Path.Combine(raiz, d), "*.*", SearchOption.AllDirectories))
                .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                .Where(f => File.ReadAllText(f, Encoding.UTF8).Contains("\"Faltan documentos\""))
                .Select(f => f.Substring(raiz.Length))
                .ToList();
            Assert.True(conTexto.Count == 0, "\"Faltan documentos\" como texto en: " + string.Join(", ", conTexto));
        }

        // ---- M-254 ----

        [Fact]
        public void Login_EscritorioBajoConLogoChicoSinTocarCelularNiPantallaAlta()
        {
            string css = Leer("Content/paginas/Login.css");
            string bloque = BloqueMedia(css, "(min-width: 576px) and (max-height: 800px)", ".logo");
            Assert.NotNull(bloque);
            var logo = Regex.Match(bloque, @"\.logo\s*\{[^}]*max-width:\s*(\d+)px");
            Assert.True(logo.Success, "La media de alto no fija el ancho del logo.");
            Assert.True(int.Parse(logo.Groups[1].Value) <= 120, "Logo de " + logo.Groups[1].Value + " px en escritorio bajo.");
            foreach (string selector in new[] { ".main-container", ".login-container", ".form-group", ".login-error" })
                Assert.Contains(selector + " {", bloque);
            Assert.DoesNotContain("!important", bloque);
            // Más bajo aún (1280x600): logo todavía más chico.
            string muyBajo = BloqueMedia(css, "(min-width: 576px) and (max-height: 640px)", ".logo");
            Assert.NotNull(muyBajo);
            var logoChico = Regex.Match(muyBajo, @"\.logo\s*\{[^}]*max-width:\s*(\d+)px");
            Assert.True(logoChico.Success && int.Parse(logoChico.Groups[1].Value) <= int.Parse(logo.Groups[1].Value));
            Assert.DoesNotContain("!important", muyBajo);
            // El logo general (1920x1080) sigue igual.
            Assert.Matches(@"^\.logo \{\s*max-width: 200px;", Regex.Replace(css, @"\r", "").Split(new[] { "\n\n" }, StringSplitOptions.None)
                .First(p => p.StartsWith(".logo", StringComparison.Ordinal)));
        }
    }
}
