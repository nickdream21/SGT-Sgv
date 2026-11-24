using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-13 (M-117): el sitio carga Bootstrap 4.6.2, así que no quedan clases de Bootstrap 5 en el marcado, el
    /// código ni las hojas propias (lista cerrada), la capa de compatibilidad de sgv-base.css no vuelve, los
    /// !important de cada hoja no suben y los @media usan los puntos de corte de Bootstrap 4 (576, 768 y 992 px)
    /// salvo una lista cerrada de excepciones que cambiarían lo que se ve.
    /// </summary>
    public class BootstrapCuatroTests
    {
        /// <summary>
        /// Clases de Bootstrap 5 que no existen en Bootstrap 4.6 (lista cerrada). Su equivalente:
        /// form-select(-sm) → custom-select(-sm), form-label → sgv-etiqueta, text-end/start → text-right/left,
        /// fw-bold → font-weight-bold, me-/ms-/pe-/ps- → mr-/ml-/pr-/pl-, float-end/start → float-right/left,
        /// visually-hidden → sr-only, form-switch → custom-switch, btn-close → close, data-bs-* → data-*.
        /// gap-* y fs-* no tienen equivalente en Bootstrap 4: una clase de la página.
        /// </summary>
        private static readonly string[] ClasesBootstrap5 =
        {
            "form-select", "form-select-sm", "form-select-lg", "form-label", "form-floating", "form-switch", "form-range",
            "text-end", "text-start", "fw-bold", "fw-bolder", "fw-semibold", "fw-medium", "fw-normal", "fw-light", "fw-lighter",
            "fst-italic", "fst-normal", "visually-hidden", "visually-hidden-focusable", "btn-close",
            "float-end", "float-start", "border-end", "border-start", "rounded-end", "rounded-start", "d-grid",
        };

        /// <summary>Familias con número o tamaño (me-2, ms-auto, gap-3, fs-6, text-bg-primary, bg-opacity-50...).</summary>
        private const string FamiliasBootstrap5 =
            @"(?:m[es]|p[es])-(?:[0-5]|auto)|(?:[a-z]{2,3}-)?(?:me|ms|pe|ps)-(?:sm|md|lg|xl|xxl)-(?:[0-5]|auto)|gap-[0-5]|row-gap-[0-5]|column-gap-[0-5]|fs-[1-6]"
            + @"|text-bg-[a-z]+|bg-opacity-\d+|bg-body(?:-[a-z]+)?|bg-[a-z]+-subtle|text-[a-z]+-emphasis|border-[a-z]+-subtle|link-(?:primary|secondary|success|danger|warning|info|light|dark)";

        private static readonly Regex ClaseEnTexto = new Regex(
            @"(?<![\w-])(" + string.Join("|", ClasesBootstrap5.OrderByDescending(c => c.Length).Select(Regex.Escape)) + "|" + FamiliasBootstrap5 + @")(?![\w-])");

        private static readonly Regex ClaseEnSelector = new Regex(
            @"\.(" + string.Join("|", ClasesBootstrap5.OrderByDescending(c => c.Length).Select(Regex.Escape)) + "|" + FamiliasBootstrap5 + @")(?![\w-])");

        /// <summary>
        /// !important por hoja al cerrar E5-13 (sin contar comentarios). Solo pueden bajar: al quitar uno, baje
        /// también la cifra; una hoja nueva empieza en 0.
        /// </summary>
        private static readonly Dictionary<string, int> TopeImportant = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Content/App.css"] = 55,
            ["Content/sgv-base.css"] = 40,
            ["Content/paginas/DashboardConductor.css"] = 3,
            ["Content/paginas/Exportacion/DashboardExportacion.css"] = 2,
            ["Content/paginas/Exportacion/SeguimientoExportacion.css"] = 2,
            ["Content/paginas/FacturasFlete.css"] = 1,
            ["Content/paginas/LiquidacionesPendientes.css"] = 10,
            ["Content/paginas/ListaDespachos.css"] = 2,     // E5-18a: plantilla común (114 a 2: el borde y el relleno de la tabla por despacho en el celular)
            ["Content/paginas/RegistroDespacho.css"] = 10,
            ["Content/paginas/SiteMaster.css"] = 15,   // E5-24b: fuera .nav-link i y .dropdown-item i
        };

        /// <summary>Los puntos de corte de Bootstrap 4 que usa el sitio (sm, md y lg).</summary>
        private static readonly int[] PuntosDeCorte = { 576, 768, 992 };

        /// <summary>
        /// Excepciones (lista cerrada): moverlas al punto de corte más cercano cambiaría lo que se ve.
        /// </summary>
        private static readonly Dictionary<string, int[]> PuntosPropios = new Dictionary<string, int[]>(StringComparer.Ordinal)
        {
            // Barra fija del conductor centrada en 640 px cuando hay 672 px (640 + márgenes) de ancho.
            ["Content/paginas/DashboardConductor.css"] = new[] { 672 },
            // Paneles del tablero de exportación en dos columnas desde 1100 px (con 1200 se apilarían a 1100-1199,
            // p. ej. 1440 px al 125 %; con 992, a 1024 px los paneles quedarían angostos).
            ["Content/paginas/Exportacion/DashboardExportacion.css"] = new[] { 1100 },
            // Acompaña a las columnas col-xl-* de la página (el punto xl de Bootstrap 4).
            ["Content/paginas/FacturasFlete.css"] = new[] { 1200 },
            // Barra de menú compacta de 992 a 1599 px: incluye las pantallas de 1366 px de la oficina.
            ["Content/paginas/SiteMaster.css"] = new[] { 1600 },
        };

        // ------------------------------------------------------------------ lectura

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Relativo(string raiz, string archivo) => archivo.Substring(raiz.Length + 1).Replace('\\', '/');

        private static List<string> Hojas()
        {
            var raiz = RaizWeb();
            var content = Path.Combine(raiz, "Content");
            return Directory.GetFiles(content, "*.css")
                .Concat(Directory.GetFiles(Path.Combine(content, "paginas"), "*.css", SearchOption.AllDirectories))
                .Select(f => Relativo(raiz, f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
        }

        private static string Leer(string relativo) =>
            File.ReadAllText(Path.Combine(RaizWeb(), relativo.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

        /// <summary>aspx, master, ascx, cs y js propios (sin obj/, bin/, Scripts/lib ni minificados).</summary>
        private static List<string> ArchivosDeCodigo()
        {
            var raiz = RaizWeb();
            var ext = new[] { ".aspx", ".master", ".ascx", ".cs", ".js" };
            return Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories)
                .Where(a => ext.Contains(Path.GetExtension(a).ToLowerInvariant()))
                .Select(a => Relativo(raiz, a))
                .Where(r => !r.StartsWith("obj/") && !r.StartsWith("bin/") && !r.StartsWith("Scripts/lib/") && !r.Contains(".min."))
                .ToList();
        }

        private static int Linea(string texto, int indice) => texto.Take(indice).Count(c => c == '\n') + 1;

        // ------------------------------------------------------------------ clases

        [Fact]
        public void ClasesBootstrap5_NoEstanEnElMarcadoNiEnElCodigo()
        {
            var fallas = new List<string>();
            foreach (var a in ArchivosDeCodigo())
            {
                string t = Leer(a);
                foreach (Match m in ClaseEnTexto.Matches(t))
                    fallas.Add($"{a}:{Linea(t, m.Index)} {m.Value}");
                foreach (Match m in Regex.Matches(t, @"\bdata-bs-[a-z-]+"))
                    fallas.Add($"{a}:{Linea(t, m.Index)} {m.Value}");
            }
            Assert.True(fallas.Count == 0, "Clases de Bootstrap 5 (el sitio usa Bootstrap 4.6.2; ver el equivalente en ClasesBootstrap5):\n"
                + string.Join("\n", fallas.Take(40)));
        }

        [Fact]
        public void ClasesBootstrap5_SinSelectoresEnLasHojasPropias()
        {
            var fallas = new List<string>();
            foreach (var h in Hojas())
            {
                string m = EstilosTests.Enmascarar(Leer(h));
                // Solo los selectores: el texto antes de cada '{' (los valores van entre ':' y ';').
                foreach (Match sel in Regex.Matches(m, @"(?<=^|[{};])[^{};]*(?=\{)"))
                    foreach (Match c in ClaseEnSelector.Matches(sel.Value))
                        fallas.Add($"{h}:{Linea(m, sel.Index + c.Index)} {c.Value}");
            }
            Assert.True(fallas.Count == 0, "Selectores de clases de Bootstrap 5 en las hojas propias:\n" + string.Join("\n", fallas.Take(40)));
        }

        [Fact]
        public void CapaDeCompatibilidad_NoVuelve()
        {
            string baseCss = Leer("Content/sgv-base.css");
            Assert.DoesNotContain("Compatibilidad: clases de Bootstrap 5", baseCss);
            foreach (var retirada in new[] { "Content/responsive-mobile.css", "Content/paginas/ListaDespachos-2.css", "Content/paginas/RegistroDespacho-2.css" })
            {
                Assert.False(File.Exists(Path.Combine(RaizWeb(), retirada.Replace('/', Path.DirectorySeparatorChar))),
                    retirada + " se unió a su hoja principal en E5-13: no debe volver.");
                string nombre = Path.GetFileName(retirada);
                Assert.DoesNotContain(nombre, File.ReadAllText(Path.Combine(RaizWeb(), "WebSGV.csproj")));
                Assert.DoesNotContain(nombre, Leer("Site.Master"));
            }
        }

        [Fact]
        public void ListaDespachos_CargaSelect2AntesDeSuHoja()
        {
            // Las reglas de select2 de ListaDespachos.css (antes en ListaDespachos-2.css, que se cargaba al final)
            // tienen la misma especificidad que las de select2.min.css: deben ir después para ganarles.
            string aspx = Leer("Views/ListaDespachos.aspx");
            int select2 = aspx.IndexOf("select2-4.0.13/select2.min.css", StringComparison.Ordinal);
            int hoja = aspx.IndexOf("Content/paginas/ListaDespachos.css", StringComparison.Ordinal);
            Assert.True(select2 >= 0 && hoja >= 0 && select2 < hoja, "select2.min.css debe enlazarse antes de ListaDespachos.css");
            Assert.Contains(".select2-container--default .select2-selection--single", Leer("Content/paginas/ListaDespachos.css"));
        }

        // ------------------------------------------------------------------ !important

        [Fact]
        public void Important_PorHojaNoSube()
        {
            var fallas = new List<string>();
            var conteo = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var h in Hojas())
            {
                int n = Regex.Matches(EstilosTests.Enmascarar(Leer(h)), @"!\s*important", RegexOptions.IgnoreCase).Count;
                conteo[h] = n;
                int tope = TopeImportant.TryGetValue(h, out var t) ? t : 0;
                if (n > tope) fallas.Add($"{h}: {n} !important (tope {tope})");
            }
            Assert.True(fallas.Count == 0, "Subieron los !important (use especificidad o el orden de carga):\n" + string.Join("\n", fallas));
            foreach (var t in TopeImportant)
                Assert.True(conteo.ContainsKey(t.Key), "TopeImportant nombra una hoja que ya no existe: " + t.Key);
            Assert.True(conteo.Values.Sum() > 0, "El contador no encontró ningún !important: revise el lector.");
        }

        // ------------------------------------------------------------------ puntos de corte

        /// <summary>
        /// Punto de corte de cada min-width/max-width: min-width: 768px → 768; max-width: 767.98px → 768
        /// (la forma de Bootstrap 4). Un max-width redondo (768px) queda tal cual: no coincide con Bootstrap.
        /// </summary>
        private static IEnumerable<(string Hoja, int Linea, string Texto, decimal Punto)> Cortes()
        {
            foreach (var h in Hojas())
            {
                string m = EstilosTests.Enmascarar(Leer(h));
                foreach (Match media in Regex.Matches(m, @"@media[^{]*"))
                    foreach (Match w in Regex.Matches(media.Value, @"\((min|max)-width\s*:\s*([\d.]+)px\s*\)"))
                    {
                        decimal v = decimal.Parse(w.Groups[2].Value, CultureInfo.InvariantCulture);
                        decimal punto = w.Groups[1].Value == "max" && decimal.Remainder(v, 1m) == 0.98m ? v + 0.02m : v;
                        yield return (h, Linea(m, media.Index), w.Value, punto);
                    }
            }
        }

        [Fact]
        public void PuntosDeCorte_SoloLosDeBootstrap4()
        {
            var cortes = Cortes().ToList();
            Assert.True(cortes.Count >= 30, "El lector encontró solo " + cortes.Count + " @media con ancho: revise el lector.");
            var fallas = cortes
                .Where(c => !PuntosDeCorte.Contains((int)c.Punto) || c.Punto != decimal.Truncate(c.Punto))
                .Where(c => !(PuntosPropios.TryGetValue(c.Hoja, out var propios) && propios.Contains((int)c.Punto)))
                .Select(c => $"{c.Hoja}:{c.Linea} {c.Texto}")
                .ToList();
            Assert.True(fallas.Count == 0, "@media fuera de los puntos de corte de Bootstrap 4 (576, 768, 992; max-width con .98: 575.98px, 767.98px, 991.98px):\n"
                + string.Join("\n", fallas));

            // Como máximo 3 puntos de corte generales, y cada excepción sigue en uso (si se quita, se borra de la lista).
            Assert.True(cortes.Select(c => c.Punto).Where(p => PuntosDeCorte.Contains((int)p)).Distinct().Count() <= 3);
            foreach (var e in PuntosPropios)
                foreach (int p in e.Value)
                    Assert.True(cortes.Any(c => c.Hoja == e.Key && c.Punto == p), $"La excepción {e.Key} {p} px ya no se usa: quítela de PuntosPropios.");
        }

        [Fact]
        public void PuntosDeCorte_MaxWidthConLaFormaDeBootstrap4()
        {
            // max-width: 768px aplica también a 768 px, donde Bootstrap ya pasó a la columna md: usar 767.98px.
            var redondos = Cortes()
                .Where(c => c.Texto.StartsWith("(max", StringComparison.Ordinal) && PuntosDeCorte.Contains((int)c.Punto) && !c.Texto.Contains(".98"))
                .Select(c => $"{c.Hoja}:{c.Linea} {c.Texto}")
                .ToList();
            Assert.True(redondos.Count == 0, "max-width redondo (use 575.98px, 767.98px o 991.98px):\n" + string.Join("\n", redondos));
        }
    }
}
