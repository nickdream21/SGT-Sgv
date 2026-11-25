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
    /// Encabezado y títulos de página (E5-15, M-073). Toda página de oficina abre con la plantilla de
    /// sgv-base.css (<c>div.sgv-encabezado &gt; div.sgv-encabezado-titulo &gt; h1.h2</c>), su Title es la
    /// etiqueta del menú sin el paréntesis final y el &lt;h1&gt; dice lo mismo. Leen las páginas como texto
    /// sin comentarios, igual que <see cref="AccesibilidadMarcadoTests"/>.
    /// </summary>
    public class EncabezadoPaginaTests
    {
        /// <summary>Páginas con master que no usan la plantilla (igual tienen un solo &lt;h1&gt;), con su motivo.</summary>
        internal static readonly Dictionary<string, string> ListaCerrada = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/Inicio.aspx"] = "Portada con saludo y accesos, sin acciones de página.",
            ["Views/DashboardConductor.aspx"] = "Pantalla del conductor en el celular: cabecera propia con nombre y viaje.",
            ["Views/DetalleOrdenViaje.aspx"] = "Liquidación del conductor en el celular: cabecera propia (dc-cab).",
            ["Views/Login.aspx"] = "Formulario de cuenta centrado, sin menú.",
            ["Views/RecuperarContrasena.aspx"] = "Formulario de cuenta centrado, sin menú.",
            ["Views/RestablecerContrasena.aspx"] = "Formulario de cuenta centrado, sin menú.",
            ["Views/SegundoFactor.aspx"] = "Formulario de cuenta centrado, sin menú.",
            ["Views/Error404.aspx"] = "Página de error: debe verse aunque fallen las hojas.",
        };

        /// <summary>Palabras que van con mayúscula aunque no sean la primera del título.</summary>
        internal static readonly HashSet<string> NombresPropios = new HashSet<string>(StringComparer.Ordinal)
        {
            "Excel", "SGV", "Vitapro", "Onway", "SUNAT", "Perú", "Ecuador",
            // Siglas y formatos que aparecen en los botones (InterfazFormalTests).
            "PDF", "GPS", "RUC", "CPIC", "DNI", "OV", "ZIP", "Word",
            "Santa", "Rosa",   // grifo Santa Rosa (Seguimiento de exportación)
        };

        internal static readonly string[] ClasesRetiradas =
        {
            "aud-page-header", "aud-page-title", "bd-cabecera", "bd-titulo", "ff-encabezado", "ff-titulo",
            "de-header", "de-title", "ld-page-header", "rd-page-header", "page-header", "page-title",
            "admin-titulo", "rc-titulo", "ms-titulo", "registro-header-title",
        };

        // ── Expresiones ──────────────────────────────────────────────────────────────────────

        internal static readonly Regex Plantilla = new Regex(@"\bsgv-encabezado\b(?!-)");
        internal static readonly Regex PlantillaBienFormada = new Regex(
            @"sgv-encabezado[^""]*"">\s*<div class=""sgv-encabezado-titulo"">(?:(?!</div>).)*?<h1 class=""h2""", RegexOptions.Singleline);
        internal static readonly Regex DirectivaPage = new Regex(@"<%@\s*Page\b[^%]*%>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        internal static readonly Regex TitleAttr = new Regex(@"\bTitle\s*=\s*""(?<t>[^""]*)""", RegexOptions.IgnoreCase);
        // El href lleva <%= ResolveUrl(...) %>: su '>' no cierra la etiqueta (sin el icono, E5-24b, el texto sigue al href).
        internal static readonly Regex EnlaceMenu = new Regex(
            @"<a\b[^>]*\bclass\s*=\s*""(?:dropdown-item|nav-link)\b[^""]*""(?:<%.*?%>|[^>])*>(?<t>.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        internal static readonly Regex UrlMenu = new Regex(@"ResolveUrl\(""~/(?<u>Views/[\w/]+\.aspx)""\)");
        internal static readonly Regex H1 = new Regex(@"<h1\b[^>]*>(?<t>.*?)</h1>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        internal static readonly Regex IconoEnH1 = new Regex(
            @"<h1\b[^>]*>(?:(?!</h1>).)*?<i\b[^>]*\bclass\s*=\s*[""'][^""']*\bfa[srb]?\b", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        internal static readonly Regex Contador = new Regex(
            @"<span class=""sgv-encabezado-contador"">.*?</span>|<asp:Label\b[^>]*CssClass=""sgv-encabezado-contador""[^>]*?(?:/>|>.*?</asp:Label>)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        internal static readonly Regex EtiquetaGridView = new Regex(@"<asp:GridView\b(?<a>[^>]*)>|</asp:GridView\s*>", RegexOptions.IgnoreCase);

        /// <summary>
        /// Índice de cada &lt;asp:GridView&gt; sin EmptyDataText en su apertura ni &lt;EmptyDataTemplate&gt; propio. El cierre se
        /// busca contando la profundidad, y el contenido de una grilla anidada no cuenta para la de afuera.
        /// </summary>
        internal static List<int> GrillasSinTextoVacio(string texto)
        {
            var etiquetas = EtiquetaGridView.Matches(texto).Cast<Match>().ToList();
            var malas = new List<int>();
            for (int i = 0; i < etiquetas.Count; i++)
            {
                var ap = etiquetas[i];
                if (ap.Value.StartsWith("</")) continue;
                if (Regex.IsMatch(ap.Groups["a"].Value, @"\bEmptyDataText\s*=", RegexOptions.IgnoreCase)) continue;
                if (ap.Value.EndsWith("/>")) { malas.Add(ap.Index); continue; }
                // Contenido propio: lo que queda a profundidad 1 hasta su cierre.
                var propio = new System.Text.StringBuilder();
                int prof = 1, desde = ap.Index + ap.Length;
                for (int k = i + 1; k < etiquetas.Count && prof > 0; k++)
                {
                    var e = etiquetas[k];
                    if (prof == 1) propio.Append(texto, desde, e.Index - desde);
                    if (e.Value.StartsWith("</")) prof--;
                    else if (!e.Value.EndsWith("/>")) prof++;
                    desde = e.Index + e.Length;
                }
                if (prof > 0) propio.Append(texto.Substring(desde));   // sin cierre: hasta el final
                if (propio.ToString().IndexOf("<EmptyDataTemplate", StringComparison.OrdinalIgnoreCase) < 0) malas.Add(ap.Index);
            }
            return malas;
        }

        private static string Normalizar(string s) => Regex.Replace(WebUtility.HtmlDecode(s ?? ""), @"\s+", " ").Trim();

        /// <summary>Title del <c>&lt;%@ Page %&gt;</c>, o null.</summary>
        internal static string TituloDe(string texto)
        {
            var d = DirectivaPage.Match(texto);
            if (!d.Success) return null;
            var t = TitleAttr.Match(d.Value);
            return t.Success ? Normalizar(t.Groups["t"].Value) : null;
        }

        /// <summary>Palabras (salvo la primera) que empiezan como nombre propio y no están en <see cref="NombresPropios"/>.</summary>
        internal static List<string> PalabrasConMayuscula(string titulo)
        {
            var palabras = Normalizar(titulo).Split(' ').Select(w => w.Trim('(', ')', '¿', '?', '¡', '!', '"', '\'', '.', ',', ':', ';', '·', '-')).ToList();
            return palabras.Skip(1)
                .Where(w => Regex.IsMatch(w, @"^\p{Lu}\p{Ll}") && !NombresPropios.Contains(w))
                .ToList();
        }

        /// <summary>Texto visible de un enlace del menú: lo que sigue al icono, sin etiquetas.</summary>
        internal static string TextoDeEnlace(string interior)
        {
            int i = interior.LastIndexOf("</i>", StringComparison.OrdinalIgnoreCase);
            string t = i >= 0 ? interior.Substring(i + 4) : interior;
            return Normalizar(Regex.Replace(t, @"<[^>]*>", ""));
        }

        /// <summary>Etiqueta del menú sin el paréntesis final: "Facturas de flete (conciliación)" → "Facturas de flete".</summary>
        internal static string SinParentesisFinal(string etiqueta) => Normalizar(Regex.Replace(etiqueta, @"\s*\(.*\)$", ""));

        /// <summary>Texto del &lt;h1&gt; sin el contador ni etiquetas; null si lo pone el servidor (&lt;asp:...&gt;).</summary>
        internal static string TextoDeH1(string interior)
        {
            string t = Contador.Replace(interior, "");
            if (t.IndexOf("<asp:", StringComparison.OrdinalIgnoreCase) >= 0 || t.Contains("<%")) return null;
            return Normalizar(Regex.Replace(t, @"<[^>]*>", ""));
        }

        /// <summary>
        /// Páginas .aspx con master. Las reglas 1, 2 y 7 (plantilla, lista cerrada, h1 igual al Title) son solo de
        /// páginas: un control .ascx no tiene Title ni encabezado propio (lo pone la página que lo contiene; la regla
        /// 11 lo exige). Las reglas 3, 4, 8 y 9 sí leen los .ascx (<see cref="Paginas"/> los incluye).
        /// </summary>
        private static IEnumerable<Pagina> ConMaster() =>
            Paginas().Where(p => p.Ruta.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)
                              && Regex.IsMatch(p.Texto, @"\bMasterPageFile\s*=", RegexOptions.IgnoreCase));

        private static IEnumerable<Pagina> DeOficina() => ConMaster().Where(p => !ListaCerrada.ContainsKey(p.Ruta));

        private static Pagina Master() => PaginaDe("Site.Master");

        private static void SinCasos(string regla, List<string> casos) =>
            Assert.True(casos.Count == 0, regla + " (" + casos.Count + "):\n" + string.Join("\n", casos));

        // ── Reglas ───────────────────────────────────────────────────────────────────────────

        [Fact]
        public void Regla1_PaginaDeOficina_UsaLaPlantillaUnaVezYBienFormada()
        {
            var casos = new List<string>();
            foreach (var p in DeOficina())
            {
                int n = Plantilla.Matches(p.Texto).Count;
                if (n != 1) casos.Add(p.Ruta + ": " + n + " sgv-encabezado");
                else if (!PlantillaBienFormada.IsMatch(p.Texto)) casos.Add(p.Ruta + ": sgv-encabezado sin -titulo ni <h1 class=\"h2\"> adentro");
            }
            SinCasos("Página de oficina sin la plantilla sgv-encabezado", casos);
        }

        [Fact]
        public void Regla2_ListaCerrada_EsExactamenteLoQueNoUsaLaPlantilla()
        {
            var conMaster = ConMaster().ToList();
            foreach (var r in ListaCerrada.Keys)
            {
                var p = conMaster.SingleOrDefault(x => string.Equals(x.Ruta, r, StringComparison.OrdinalIgnoreCase));
                Assert.True(p != null, "ListaCerrada nombra una página con master que no existe: " + r);
                Assert.False(Plantilla.IsMatch(p.Texto), r + " está en ListaCerrada pero usa sgv-encabezado: sáquela de la lista.");
            }
            var sinPlantilla = new HashSet<string>(conMaster.Where(p => !Plantilla.IsMatch(p.Texto)).Select(p => p.Ruta), StringComparer.OrdinalIgnoreCase);
            var lista = new HashSet<string>(ListaCerrada.Keys, StringComparer.OrdinalIgnoreCase);
            Assert.True(sinPlantilla.SetEquals(lista),
                "Sin la plantilla y fuera de ListaCerrada (use sgv-encabezado o agréguela con su motivo): "
                + string.Join(", ", sinPlantilla.Except(lista, StringComparer.OrdinalIgnoreCase))
                + "\nEn ListaCerrada pero con la plantilla: " + string.Join(", ", lista.Except(sinPlantilla, StringComparer.OrdinalIgnoreCase)));
        }

        [Fact]
        public void Regla3_NingunH1_TieneIcono()
        {
            var casos = Paginas().SelectMany(p => IconoEnH1.Matches(p.Texto).Cast<Match>().Select(m => p.Ruta + ":" + p.Linea(m.Index))).ToList();
            SinCasos("<h1> con icono <i class=\"fa...\">", casos);
        }

        [Fact]
        public void Regla4_TodoTitle_EnEstiloOracion()
        {
            var casos = Paginas()
                .Select(p => new { p.Ruta, T = TituloDe(p.Texto) })
                .Where(x => x.T != null && PalabrasConMayuscula(x.T).Any())
                .Select(x => x.Ruta + ": \"" + x.T + "\" (" + string.Join(", ", PalabrasConMayuscula(x.T)) + ")")
                .ToList();
            SinCasos("Title con mayúscula en medio (va en estilo oración)", casos);
        }

        /// <summary>
        /// Textos del menú: los de MenuSitio (E5-19c, única fuente de los menús por rol) y los enlaces que siguen
        /// escritos en Site.Master (Configuración).
        /// </summary>
        private static List<(string Donde, string Texto)> TextosDelMenu()
        {
            var textos = new List<(string, string)>();
            foreach (var g in WebSGV.Helpers.MenuSitio.Completo)
            {
                textos.Add(("MenuSitio: menú", g.Texto));
                foreach (var e in g.Entradas) textos.Add(("MenuSitio: " + g.Texto, e.Texto));
            }
            foreach (var e in WebSGV.Helpers.MenuSitio.Configuracion) textos.Add(("MenuSitio: Configuración", e.Texto));
            var m = Master();
            foreach (Match e in EnlaceMenu.Matches(m.Texto))
            {
                string t = TextoDeEnlace(e.Groups["t"].Value);
                if (t.Length > 0 && !t.Contains("<%")) textos.Add(("Site.Master:" + m.Linea(e.Index), t));
            }
            return textos;
        }

        [Fact]
        public void Regla5_TextosDelMenu_EnEstiloOracion()
        {
            var textos = TextosDelMenu();
            Assert.True(textos.Count > 30, "No encontré los textos del menú (MenuSitio y Site.Master): " + textos.Count);
            Assert.Contains(textos, t => t.Texto == "Cambiar contraseña");   // Configuración, en el marcado
            var casos = textos
                .Where(x => PalabrasConMayuscula(x.Texto).Any())
                .Select(x => x.Donde + " \"" + x.Texto + "\"")
                .ToList();
            SinCasos("Texto del menú con mayúscula en medio", casos);
        }

        [Fact]
        public void Regla6_TitleDeCadaPagina_EsLaEtiquetaDelMenu()
        {
            var paginas = Paginas();
            var casos = new List<string>();
            var opciones = WebSGV.Helpers.MenuSitio.Completo.SelectMany(g => g.Entradas).Concat(WebSGV.Helpers.MenuSitio.Configuracion)
                .Select(e => (e.Texto, e.Url))
                .Concat(new[] { ("Mi seguridad", WebSGV.SiteMaster.UrlMiSeguridad) })
                .ToList();
            foreach (var (texto, url) in opciones)
            {
                string ruta = url.Substring(2);   // "~/Views/X.aspx" → "Views/X.aspx"
                string etiqueta = SinParentesisFinal(texto);
                var p = paginas.SingleOrDefault(x => string.Equals(x.Ruta, ruta, StringComparison.OrdinalIgnoreCase));
                if (p == null) { casos.Add("El menú enlaza a " + ruta + ", que no existe"); continue; }
                string titulo = TituloDe(p.Texto);
                if (!string.Equals(titulo, etiqueta, StringComparison.Ordinal))
                    casos.Add(p.Ruta + ": Title \"" + titulo + "\" y el menú dice \"" + etiqueta + "\"");
            }
            Assert.True(opciones.Count > 20, "No encontré las opciones del menú en MenuSitio.");
            SinCasos("Title distinto de la etiqueta del menú", casos);
        }

        [Fact]
        public void Regla7_H1DeOficina_DiceLoMismoQueElTitle()
        {
            var casos = new List<string>();
            foreach (var p in DeOficina())
            {
                var h = H1.Match(p.Texto);
                if (!h.Success) continue;   // Regla 1 y AccesibilidadMarcadoTests.Regla7 lo cubren
                string texto = TextoDeH1(h.Groups["t"].Value);
                if (texto == null) continue;
                string titulo = TituloDe(p.Texto);
                if (!string.Equals(texto, titulo, StringComparison.Ordinal))
                    casos.Add(p.Ruta + ":" + p.Linea(h.Index) + " <h1> \"" + texto + "\" y Title \"" + titulo + "\"");
            }
            SinCasos("<h1> distinto del Title", casos);
        }

        [Fact]
        public void Regla8_NingunUpdateProgress_ElVeloGlobalYaCumple()
        {
            var casos = Paginas().SelectMany(p => Regex.Matches(p.Texto, @"<asp:UpdateProgress\b", RegexOptions.IgnoreCase)
                .Cast<Match>().Select(m => p.Ruta + ":" + p.Linea(m.Index))).ToList();
            SinCasos("<asp:UpdateProgress> (el velo de espera de E5-2 ya lo hace)", casos);
        }

        [Fact]
        public void Regla9_TodaGrilla_DiceQueNoHayDatos()
        {
            var casos = Paginas().SelectMany(p => GrillasSinTextoVacio(p.Texto).Select(i => p.Ruta + ":" + p.Linea(i))).ToList();
            Assert.True(Paginas().Sum(p => EtiquetaGridView.Matches(p.Texto).Count) > 20, "No encontré las <asp:GridView>: revise EtiquetaGridView.");
            SinCasos("<asp:GridView> sin EmptyDataText ni EmptyDataTemplate", casos);
        }

        [Fact]
        public void Regla10_ClasesDeEncabezadoRetiradas_NoVuelven()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views"))) dir = dir.Parent;
            Assert.NotNull(dir);
            string web = Path.Combine(dir.FullName, "WebSGV");
            var clase = new Regex(@"(?<![\w-])(" + string.Join("|", ClasesRetiradas.OrderByDescending(c => c.Length).Select(Regex.Escape)) + @")(?![\w-])");
            var archivos = new[]
            {
                new { Carpeta = "Views", Patrones = new[] { "*.aspx", "*.ascx", "*.cs" } },
                new { Carpeta = "Content", Patrones = new[] { "*.css" } },
                new { Carpeta = Path.Combine("Scripts", "paginas"), Patrones = new[] { "*.js" } },
            }
            .SelectMany(c => c.Patrones.SelectMany(pat => Directory.GetFiles(Path.Combine(web, c.Carpeta), pat, SearchOption.AllDirectories)))
            .Where(f => f.IndexOf(Path.DirectorySeparatorChar + "lib" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
            .Concat(new[] { Path.Combine(web, "Site.Master") });

            var casos = new List<string>();
            foreach (var f in archivos)
            {
                string[] lineas = File.ReadAllLines(f);
                for (int i = 0; i < lineas.Length; i++)
                    foreach (Match m in clase.Matches(lineas[i]))
                        casos.Add(f.Substring(web.Length + 1).Replace('\\', '/') + ":" + (i + 1) + " " + m.Value);
            }
            SinCasos("Clase de encabezado retirada en E5-15 (use sgv-encabezado)", casos);
        }

        /// <summary>
        /// Un control de usuario (.ascx) no trae encabezado propio ni &lt;h1&gt;: la página que lo contiene ya tiene el
        /// suyo (uno solo por página) y un segundo encabezado partiría la plantilla (E6-4: RegistroDespacho y
        /// ListaDespachos se parten en controles).
        /// </summary>
        [Fact]
        public void Regla11_ControlDeUsuario_SinEncabezadoNiH1()
        {
            var casos = Paginas().Where(p => FuentesWeb.EsControl(p.Ruta))
                .SelectMany(p => Plantilla.Matches(p.Texto).Cast<Match>().Concat(H1.Matches(p.Texto).Cast<Match>())
                    .Select(m => p.Ruta + ":" + p.Linea(m.Index)))
                .ToList();
            SinCasos("Control .ascx con sgv-encabezado o <h1> (el encabezado es de la página)", casos);
        }

        // ── Las expresiones mismas ───────────────────────────────────────────────────────────

        [Fact]
        public void Expresion_Plantilla_NoCuentaLasPiezasInternas()
        {
            Assert.Single(Plantilla.Matches("<div class=\"sgv-encabezado\"><div class=\"sgv-encabezado-titulo\"><div class=\"sgv-encabezado-acciones\">").Cast<Match>());
            Assert.Empty(Plantilla.Matches("<span class=\"sgv-encabezado-contador\">").Cast<Match>());
        }

        [Fact]
        public void Expresion_PlantillaBienFormada_AceptaVolverYRechazaH2()
        {
            Assert.Matches(PlantillaBienFormada, "<div class=\"sgv-encabezado\">\n <div class=\"sgv-encabezado-titulo\">\n  <a class=\"sgv-volver\" href=\"x\">Volver</a>\n  <h1 class=\"h2\">A</h1>");
            Assert.Matches(PlantillaBienFormada, "<div class=\"x sgv-encabezado\"><div class=\"sgv-encabezado-titulo\"><h1 class=\"h2\">A</h1>");
            Assert.DoesNotMatch(PlantillaBienFormada, "<div class=\"sgv-encabezado\"><div class=\"sgv-encabezado-titulo\"><h2>A</h2>");
            Assert.DoesNotMatch(PlantillaBienFormada, "<div class=\"sgv-encabezado\"><div><h1 class=\"h2\">A</h1>");
            Assert.DoesNotMatch(PlantillaBienFormada, "<div class=\"sgv-encabezado\"><div class=\"sgv-encabezado-titulo\"><div>x</div></div><h1 class=\"h2\">A</h1>");
        }

        [Fact]
        public void Expresion_TituloDe_LeeSoloLaDirectivaPage()
        {
            Assert.Equal("Catálogo del cliente", TituloDe("<%@ Page Title=\"Cat&#xE1;logo del cliente\" Language=\"C#\" %>\n<x Title=\"Otro\" />"));
            Assert.Equal("A b", TituloDe("<%@ Page Language=\"C#\"\n    Title=\"A  b\" %>"));
            Assert.Null(TituloDe("<%@ Control Language=\"C#\" %>"));
            Assert.Null(TituloDe("<%@ Page Language=\"C#\" %>"));
        }

        [Fact]
        public void Expresion_PalabrasConMayuscula_PermiteSiglasYNombresPropios()
        {
            Assert.Empty(PalabrasConMayuscula("Reporte GPS por placas"));
            Assert.Empty(PalabrasConMayuscula("Carga de indicadores vía Excel"));
            Assert.Empty(PalabrasConMayuscula("Cargas (pedidos, guías y carga)"));
            Assert.Empty(PalabrasConMayuscula("Exportación a Ecuador"));
            Assert.Equal(new[] { "Sistema" }, PalabrasConMayuscula("Auditoría del Sistema"));
            Assert.Equal(new[] { "Usuarios" }, PalabrasConMayuscula("Gestión de (Usuarios)"));
        }

        [Fact]
        public void Expresion_TextoDeEnlace_TomaLoQueSigueAlIcono()
        {
            Assert.Equal("Dashboard de exportación", TextoDeEnlace("\n  <i class=\"fas fa-chart-line\"></i>Dashboard de exportaci&#xF3;n\n"));
            Assert.Equal("Sin icono", TextoDeEnlace(" Sin <b>icono</b> "));
            Assert.Equal(2, EnlaceMenu.Matches("<a class=\"dropdown-item\" href='x'>A</a><a class=\"navbar-brand\">B</a><a class=\"nav-link dropdown-toggle\" href=\"#\">C</a>").Count);
            // Sin icono el texto sigue al href con <%= %>, cuyo '>' no corta la etiqueta.
            Assert.Equal("Lista de despachos", TextoDeEnlace(EnlaceMenu.Match(
                "<a class=\"dropdown-item\" href='<%= ResolveUrl(\"~/Views/ListaDespachos.aspx\") %>'>\n  Lista de despachos\n</a>").Groups["t"].Value));
        }

        [Fact]
        public void Expresion_SinParentesisFinal()
        {
            Assert.Equal("Facturas de flete", SinParentesisFinal("Facturas de flete (conciliación)"));
            Assert.Equal("Reporte de cargas", SinParentesisFinal("Reporte de cargas (pedidos, guías y carga)"));
            Assert.Equal("Lista de despachos", SinParentesisFinal("Lista de despachos"));
        }

        [Fact]
        public void Expresion_TextoDeH1_QuitaElContadorYSaltaLoDelServidor()
        {
            Assert.Equal("Clientes", TextoDeH1("Clientes <asp:Label ID=\"lblTotal\" runat=\"server\" CssClass=\"sgv-encabezado-contador\" />"));
            Assert.Equal("Clientes", TextoDeH1("Clientes <asp:Label ID=\"l\" runat=\"server\" CssClass=\"sgv-encabezado-contador\">0</asp:Label>"));
            Assert.Equal("Lotes", TextoDeH1("Lotes <span class=\"sgv-encabezado-contador\">12</span>"));
            Assert.Equal("Catálogo del cliente", TextoDeH1("Cat&#xE1;logo\n del cliente"));
            Assert.Null(TextoDeH1("<asp:Literal ID=\"litTitulo\" runat=\"server\" />"));
            Assert.Null(TextoDeH1("<%: Titulo %>"));
        }

        [Fact]
        public void Expresion_IconoEnH1_SoloDentroDelH1()
        {
            Assert.Matches(IconoEnH1, "<h1 class=\"h2\"><i class=\"fas fa-x mr-2\"></i>A</h1>");
            Assert.DoesNotMatch(IconoEnH1, "<h1 class=\"h2\">A</h1><button><i class=\"fas fa-x\"></i>B</button>");
            Assert.Matches(IconoEnH1, "<h1><i class='fa fa-x'></i>A</h1>");
            Assert.Matches(IconoEnH1, "<h1>A <i aria-hidden=\"true\" class=\"mr-2 fab fa-x\"></i></h1>");
            Assert.Matches(IconoEnH1, "<h1>A <i class=\"text-primary fas fa-x\"></i></h1>");
            Assert.DoesNotMatch(IconoEnH1, "<h1>A <i class=\"fancy\">b</i></h1>");
            Assert.DoesNotMatch(IconoEnH1, "<h1>A <img class=\"fa-x\" /></h1>");
        }

        [Fact]
        public void Expresion_GrillasSinTextoVacio_RevisaCadaGrillaConSuPropioCierre()
        {
            Assert.Empty(GrillasSinTextoVacio("<asp:GridView ID=\"g\" runat=\"server\"\n EmptyDataText=\"Nada\">\n<Columns></Columns>\n</asp:GridView>"));
            Assert.Empty(GrillasSinTextoVacio("<asp:GridView ID=\"g\" runat=\"server\"><EmptyDataTemplate>Nada</EmptyDataTemplate></asp:GridView>"));
            Assert.Single(GrillasSinTextoVacio("<asp:GridView ID=\"g\" runat=\"server\"><Columns></Columns></asp:GridView>"));

            // Anidada sin texto dentro de una con texto: falla la de adentro.
            string anidadaMala = "<asp:GridView ID=\"a\" runat=\"server\" EmptyDataText=\"Nada\"><Columns><asp:TemplateField><ItemTemplate>"
                               + "<asp:GridView ID=\"b\" runat=\"server\"><Columns></Columns></asp:GridView>"
                               + "</ItemTemplate></asp:TemplateField></Columns></asp:GridView>";
            Assert.Equal(new[] { anidadaMala.IndexOf("<asp:GridView ID=\"b\"") }, GrillasSinTextoVacio(anidadaMala));

            // La plantilla de la grilla de adentro no le sirve a la de afuera.
            string afueraMala = "<asp:GridView ID=\"a\" runat=\"server\"><Columns><asp:TemplateField><ItemTemplate>"
                              + "<asp:GridView ID=\"b\" runat=\"server\"><EmptyDataTemplate>Nada</EmptyDataTemplate></asp:GridView>"
                              + "</ItemTemplate></asp:TemplateField></Columns></asp:GridView>";
            Assert.Equal(new[] { 0 }, GrillasSinTextoVacio(afueraMala));

            // Plantilla propia de la de afuera después de la anidada: las dos cumplen.
            string ambasBien = "<asp:GridView ID=\"a\" runat=\"server\"><Columns><asp:TemplateField><ItemTemplate>"
                             + "<asp:GridView ID=\"b\" runat=\"server\" EmptyDataText=\"x\"></asp:GridView>"
                             + "</ItemTemplate></asp:TemplateField></Columns><EmptyDataTemplate>Nada</EmptyDataTemplate></asp:GridView>";
            Assert.Empty(GrillasSinTextoVacio(ambasBien));
        }
    }
}
