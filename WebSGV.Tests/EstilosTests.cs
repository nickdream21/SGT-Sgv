using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-11, variables de color: los colores de las hojas propias (Content/*.css y Content/paginas/**) viven en
    /// el :root de sgv-base.css y las hojas solo usan var(). E5-12, dirección "Expediente" (DN-28): el :root es
    /// una escala corta (azul tinta, gris único, verde, ámbar y rojo) + semánticas + alias heredados; fuente
    /// IBM Plex Sans servida desde Content/fonts; contraste WCAG de los pares texto/fondo; foco común.
    /// herramientas/estilos/colores.js verificar [rev] compara el resultado con otra revisión.
    /// </summary>
    public class EstilosTests
    {
        private const string Base = "Content/sgv-base.css";

        // Variables de otras hojas que se quedan con su literal o su valor propio (lista cerrada).
        // --de-f-*: colores de las fases que lee DashboardExportacion.js con getComputedStyle.
        // --dc-radio: radio de las tarjetas del conductor (no es un color).
        private static readonly Regex RaizPermitida = new Regex(@"^--de-f-|^--dc-radio$");

        // var() que se definen fuera de las hojas (lista cerrada): --fase, --tam y --planta los pone
        // DashboardExportacion.js en style=.
        private static readonly HashSet<string> VariablesExternas = new HashSet<string>(StringComparer.Ordinal)
        {
            "--fase", "--tam", "--planta"
        };

        // Páginas sin Site.Master que no enlazan hojas (Error.aspx guarda su CSS en línea: debe verse aunque
        // fallen los archivos estáticos).
        private static readonly HashSet<string> SinMasterSinHojas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Views/Error.aspx", "Views/DescargarDocumento.aspx", "Views/DescargarPdfOrdenViaje.aspx",
            "Views/AyudaImagen.aspx",   // solo devuelve la imagen de un tema de ayuda
        };

        private static readonly Regex Literal = new Regex(
            @"#[0-9a-fA-F]+(?![\w-])|(?<![\w-])rgba?\([^()]*\)|(?<![\w#-])(?:white|black|red|green|blue|gray|grey|silver|orange|yellow|purple|navy|maroon|teal|olive|lime|aqua|fuchsia|pink|brown|gold|darkgray|lightgray|whitesmoke|gainsboro)(?![\w-])",
            RegexOptions.IgnoreCase);

        private static readonly Regex UsoVar = new Regex(@"var\(\s*(--[\w-]+)");

        // ------------------------------------------------------------------ lectura

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Relativo(string raiz, string archivo) =>
            archivo.Substring(raiz.Length + 1).Replace('\\', '/');

        /// <summary>Las 27 hojas propias: Content/*.css y Content/paginas/**/*.css (Content/lib es de terceros). E5-13 unió 3 y E5-19a quitó CargarExcel.css.</summary>
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

        /// <summary>Copia con comentarios, contenido de cadenas y de url() en blanco (misma longitud y líneas).</summary>
        internal static string Enmascarar(string t)
        {
            var o = t.ToCharArray();
            int n = t.Length, i = 0;
            void Blanquear(int a, int b) { for (int k = a; k < b && k < n; k++) if (o[k] != '\n' && o[k] != '\r') o[k] = ' '; }
            while (i < n)
            {
                char c = t[i];
                if (c == '/' && i + 1 < n && t[i + 1] == '*')
                {
                    int f = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    int e = f < 0 ? n : f + 2;
                    Blanquear(i, e);
                    i = e;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    int k = i + 1;
                    while (k < n && t[k] != c) { if (t[k] == '\\') k++; k++; }
                    Blanquear(i + 1, k);
                    i = k + 1;
                    continue;
                }
                if ((c == 'u' || c == 'U') && i + 4 <= n && string.Compare(t, i, "url(", 0, 4, StringComparison.OrdinalIgnoreCase) == 0
                    && (i == 0 || !(char.IsLetterOrDigit(t[i - 1]) || t[i - 1] == '-' || t[i - 1] == '_')))
                {
                    int k = i + 4;
                    while (k < n && char.IsWhiteSpace(t[k])) k++;
                    if (k < n && (t[k] == '"' || t[k] == '\''))
                    {
                        char q = t[k++];
                        while (k < n && t[k] != q) { if (t[k] == '\\') k++; k++; }
                        k++;
                    }
                    while (k < n && t[k] != ')') k++;
                    Blanquear(i + 4, k);
                    i = k + 1;
                    continue;
                }
                i++;
            }
            return new string(o);
        }

        internal sealed class Declaracion
        {
            public string Hoja;
            public string Propiedad;
            public string Valor;        // enmascarado (sin comentarios ni url())
            public string Selector;
            public int Linea;
            public bool EnRaiz => Selector == ":root";
        }

        internal static List<Declaracion> Declaraciones(string hoja, string texto)
        {
            var m = Enmascarar(texto);
            var lineaDe = new int[m.Length + 1];
            for (int k = 0, l = 1; k <= m.Length; k++) { lineaDe[k] = l; if (k < m.Length && m[k] == '\n') l++; }
            var res = new List<Declaracion>();
            var pila = new Stack<string>();
            int ini = 0;
            for (int i = 0; i < m.Length; i++)
            {
                char c = m[i];
                if (c == '{')
                {
                    pila.Push(m.Substring(ini, i - ini).Replace("﻿", "").Trim());
                    ini = i + 1;
                }
                else if (c == ';' || c == '}')
                {
                    if (pila.Count > 0)
                    {
                        var seg = m.Substring(ini, i - ini);
                        var r = Regex.Match(seg, @"^\s*(-?-?[A-Za-z_][\w-]*)\s*:");
                        if (r.Success)
                        {
                            int inicioValor = ini + r.Length;
                            res.Add(new Declaracion
                            {
                                Hoja = hoja,
                                Propiedad = r.Groups[1].Value,
                                Valor = m.Substring(inicioValor, i - inicioValor).Trim(),
                                Selector = pila.Peek(),
                                Linea = lineaDe[inicioValor]
                            });
                        }
                    }
                    if (c == '}' && pila.Count > 0) pila.Pop();
                    ini = i + 1;
                }
            }
            return res;
        }

        private static List<Declaracion> TodasLasDeclaraciones() =>
            Hojas().SelectMany(h => Declaraciones(h, Leer(h))).ToList();

        private static IEnumerable<string> Literales(string valor) =>
            Literal.Matches(valor).Cast<Match>()
                .Where(x => !(x.Value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) && x.Value.IndexOf("var(", StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(x => x.Value);

        private static string Lista(IEnumerable<string> fallas) => string.Join("\n", fallas.Take(40));

        // ------------------------------------------------------------------ reglas

        [Fact]
        public void Hojas_SonLasVeintisietePropias()
        {
            var hojas = Hojas();
            Assert.Contains(Base, hojas);
            Assert.True(hojas.Count == 27, "Hay " + hojas.Count + " hojas en Content; si agregaste o quitaste una, actualiza esta cifra (sus colores deben usar var(--sgv-*)).");
        }

        /// <summary>(a) 0 colores literales fuera de un :root (los data-URI dentro de url() no cuentan).</summary>
        [Fact]
        public void A_SinColoresLiteralesFueraDeRoot()
        {
            var fallas = TodasLasDeclaraciones()
                .Where(d => !d.EnRaiz)
                .SelectMany(d => Literales(d.Valor).Select(l => $"{d.Hoja}:{d.Linea} {d.Propiedad}: {l}"))
                .ToList();
            Assert.True(fallas.Count == 0,
                "Use una variable de sgv-base.css (var(--sgv-...)) en vez del color literal:\n" + Lista(fallas));
        }

        /// <summary>(b) Los literales solo están en el :root de sgv-base.css, un valor por variable (salvo --de-f-*).</summary>
        [Fact]
        public void B_LiteralesSoloEnElRootDeSgvBase_UnoPorValor()
        {
            var decls = TodasLasDeclaraciones().Where(d => d.EnRaiz).ToList();
            var fuera = decls
                .Where(d => d.Hoja != Base && !RaizPermitida.IsMatch(d.Propiedad) && Literales(d.Valor).Any())
                .Select(d => $"{d.Hoja}:{d.Linea} {d.Propiedad}: {d.Valor}")
                .ToList();
            Assert.True(fuera.Count == 0, "Color literal en un :root que no es el de sgv-base.css:\n" + Lista(fuera));

            var paleta = decls.Where(d => d.Hoja == Base && Literal.IsMatch(d.Valor) && !d.Valor.StartsWith("var(", StringComparison.Ordinal)).ToList();
            // E5-12: una escala corta (DN-28). Un color nuevo entra a la escala solo si ningún tono existente sirve.
            Assert.True(paleta.Count >= 15 && paleta.Count <= 30,
                "La escala de sgv-base.css debería tener entre 15 y 30 colores literales; tiene " + paleta.Count);
            var repetidos = paleta
                .GroupBy(d => d.Valor.ToLowerInvariant())
                .Where(g => g.Count() > 1)
                .Select(g => g.Key + ": " + string.Join(", ", g.Select(d => d.Propiedad)))
                .ToList();
            Assert.True(repetidos.Count == 0, "Dos variables de la paleta con el mismo valor:\n" + Lista(repetidos));

            var noHex = paleta.Where(d => !Regex.IsMatch(d.Valor, "^#[0-9a-f]{6}$")).Select(d => $"{d.Propiedad}: {d.Valor}").ToList();
            Assert.True(noHex.Count == 0, "La paleta va en #rrggbb en minúsculas:\n" + Lista(noHex));
        }

        /// <summary>(c) Los :root de las otras hojas son solo alias de una variable --sgv-* (salvo --de-f-* y --dc-radio).</summary>
        [Fact]
        public void C_OtrosRootSonSoloAlias()
        {
            var fallas = TodasLasDeclaraciones()
                .Where(d => d.EnRaiz && d.Hoja != Base && !RaizPermitida.IsMatch(d.Propiedad))
                .Where(d => !Regex.IsMatch(d.Valor, @"^var\(--sgv-[\w-]+\)$"))
                .Select(d => $"{d.Hoja}:{d.Linea} {d.Propiedad}: {d.Valor}")
                .ToList();
            Assert.True(fallas.Count == 0, "En un :root de página solo van alias (--x: var(--sgv-...)):\n" + Lista(fallas));
        }

        /// <summary>(d) Ninguna --sgv-* se define fuera del :root de sgv-base.css ni dos veces.</summary>
        [Fact]
        public void D_NingunaSgvRedefinida()
        {
            var defs = TodasLasDeclaraciones().Where(d => d.Propiedad.StartsWith("--sgv-", StringComparison.Ordinal)).ToList();
            var fuera = defs.Where(d => d.Hoja != Base || !d.EnRaiz).Select(d => $"{d.Hoja}:{d.Linea} {d.Propiedad}").ToList();
            Assert.True(fuera.Count == 0, "Variable --sgv-* definida fuera del :root de sgv-base.css:\n" + Lista(fuera));
            var dobles = defs.GroupBy(d => d.Propiedad).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(dobles.Count == 0, "Variable --sgv-* definida dos veces: " + string.Join(", ", dobles));

            // E5-12: las semánticas toman los valores de la dirección "Expediente" (DN-28).
            var raiz = RaizBase();
            var direccion = new Dictionary<string, string>
            {
                ["--sgv-primario"] = "#1f3a6e", ["--sgv-foco"] = "#1f3a6e", ["--sgv-info-texto"] = "#1f3a6e",
                ["--sgv-texto"] = "#1a2230", ["--sgv-texto-suave"] = "#566173", ["--sgv-borde"] = "#d5dae2",
                ["--sgv-fondo-pagina"] = "#eceef1", ["--sgv-superficie"] = "#ffffff",
                ["--sgv-ok"] = "#17603a", ["--sgv-ok-texto"] = "#17603a",
                ["--sgv-alerta"] = "#7a5200", ["--sgv-alerta-texto"] = "#7a5200",
                ["--sgv-peligro"] = "#9a1c1c", ["--sgv-peligro-texto"] = "#9a1c1c",
                ["--sgv-radio"] = "3px"
            };
            foreach (var o in direccion)
            {
                Assert.True(raiz.ContainsKey(o.Key), "Falta " + o.Key + " en sgv-base.css");
                Assert.Equal(o.Value, Resolver(raiz, o.Key));
            }

            // Las familias que E5-12 retiró no vuelven: sus usos apuntan a las semánticas.
            var retiradas = defs.Where(d => Regex.IsMatch(d.Propiedad, @"^--sgv-(pizarra|bs|suelto)-")).Select(d => d.Propiedad).ToList();
            Assert.True(retiradas.Count == 0, "--sgv-pizarra-*, --sgv-bs-* y --sgv-suelto-* se retiraron en E5-12 (use una semántica): " + string.Join(", ", retiradas));
        }

        /// <summary>Definiciones del :root de sgv-base.css (nombre → valor enmascarado).</summary>
        private static Dictionary<string, string> RaizBase() =>
            Declaraciones(Base, Leer(Base)).Where(d => d.EnRaiz && d.Propiedad.StartsWith("--", StringComparison.Ordinal))
                .ToDictionary(d => d.Propiedad, d => d.Valor);

        /// <summary>Valor final de una variable siguiendo sus var() (falla si queda una sin definir).</summary>
        private static string Resolver(Dictionary<string, string> raiz, string nombre)
        {
            Assert.True(raiz.ContainsKey(nombre), "Falta " + nombre + " en el :root de sgv-base.css");
            var v = raiz[nombre];
            for (int i = 0; i < 6 && v.StartsWith("var(", StringComparison.Ordinal); i++)
            {
                var siguiente = Regex.Match(v, @"var\((--[\w-]+)\)").Groups[1].Value;
                Assert.True(raiz.ContainsKey(siguiente), nombre + " apunta a " + siguiente + ", que no está en sgv-base.css");
                v = raiz[siguiente];
            }
            return v.ToLowerInvariant();
        }

        /// <summary>(e) Toda var() usada está definida en alguna hoja (lista cerrada de las que pone el JS).</summary>
        [Fact]
        public void E_TodaVarEstaDefinida()
        {
            var decls = TodasLasDeclaraciones();
            var definidas = new HashSet<string>(decls.Where(d => d.Propiedad.StartsWith("--", StringComparison.Ordinal)).Select(d => d.Propiedad));
            var fallas = decls
                .SelectMany(d => UsoVar.Matches(d.Valor).Cast<Match>().Select(m => new { d, nombre = m.Groups[1].Value }))
                .Where(x => !definidas.Contains(x.nombre) && !VariablesExternas.Contains(x.nombre))
                .Select(x => $"{x.d.Hoja}:{x.d.Linea} {x.nombre}")
                .ToList();
            Assert.True(fallas.Count == 0, "var() sin definir (un color que no se pinta):\n" + Lista(fallas));

            // La lista cerrada no guarda nombres que ya no se usan.
            var usadas = new HashSet<string>(decls.SelectMany(d => UsoVar.Matches(d.Valor).Cast<Match>().Select(m => m.Groups[1].Value)));
            foreach (var externa in VariablesExternas)
                Assert.True(usadas.Contains(externa), externa + " ya no se usa: quítela de VariablesExternas");
        }

        /// <summary>(f) Las páginas sin Site.Master que enlazan hojas de Content enlazan sgv-base.css antes que ellas.</summary>
        [Fact]
        public void F_PaginasSinMasterEnlazanSgvBase()
        {
            var raiz = RaizWeb();
            var paginas = Directory.GetFiles(Path.Combine(raiz, "Views"), "*.aspx", SearchOption.AllDirectories)
                .Where(f => !File.ReadAllText(f).Contains("MasterPageFile"))
                .Select(f => Relativo(raiz, f))
                .ToList();
            Assert.Contains("Views/FirmarLiquidacion.aspx", paginas);
            Assert.Contains("Views/ConformarLiquidacion.aspx", paginas);
            Assert.Contains("Views/VerificarDocumento.aspx", paginas);

            var fallas = new List<string>();
            foreach (var p in paginas)
            {
                var texto = Leer(p);
                int pagina = texto.IndexOf("~/Content/paginas/", StringComparison.Ordinal);
                int baseCss = texto.IndexOf("RecursoHelper.Url(\"~/Content/sgv-base.css\")", StringComparison.Ordinal);
                if (pagina < 0)
                {
                    if (!SinMasterSinHojas.Contains(p)) fallas.Add(p + ": sin hoja de página; agréguela a SinMasterSinHojas si es a propósito");
                    continue;
                }
                if (baseCss < 0 || baseCss > pagina) fallas.Add(p + ": falta enlazar ~/Content/sgv-base.css (con RecursoHelper.Url) antes de su hoja");
            }
            Assert.True(fallas.Count == 0, string.Join("\n", fallas));
        }

        // ------------------------------------------------------------------ E5-12: dirección "Expediente"

        // Colores de la plantilla anterior que no deben volver como color de texto propio. Lista cerrada de
        // excepciones vacía: los style= de las páginas y los colores que pinta el JS son M-143 (fuera de las hojas).
        private static readonly string[] ColoresRetirados = { "#007bff", "#94a3b8", "#9ca3af", "#16a34a" };
        private static readonly HashSet<string> ColoresRetiradosPermitidos = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>(g) Ningún color: de las hojas (con las var() resueltas) ni de los &lt;style&gt; de las páginas usa un color retirado.</summary>
        [Fact]
        public void G_SinColoresRetiradosEnTexto()
        {
            var decls = TodasLasDeclaraciones();
            var tabla = decls.Where(d => d.EnRaiz && d.Propiedad.StartsWith("--", StringComparison.Ordinal))
                .GroupBy(d => d.Propiedad).ToDictionary(g => g.Key, g => g.First().Valor);
            string Expandir(string v)
            {
                for (int i = 0; i < 8; i++)
                {
                    var nuevo = UsoVarCompleto.Replace(v, m => tabla.TryGetValue(m.Groups[1].Value, out var x) ? x : m.Value);
                    if (nuevo == v) break;
                    v = nuevo;
                }
                return v.ToLowerInvariant();
            }
            var fallas = decls
                .Where(d => d.Propiedad == "color")
                .SelectMany(d => ColoresRetirados.Where(c => Expandir(d.Valor).Contains(c)).Select(c => $"{d.Hoja}:{d.Linea} color: {d.Valor} → {c}"))
                .Where(f => !ColoresRetiradosPermitidos.Contains(f))
                .ToList();

            // Bloques <style> de las páginas (Error.aspx, Error404.aspx...): literales a mano.
            var raiz = RaizWeb();
            foreach (var f in PaginasYMaster())
            {
                var texto = File.ReadAllText(f);
                foreach (Match bloque in Regex.Matches(texto, @"<style[^>]*>([\s\S]*?)</style>", RegexOptions.IgnoreCase))
                    foreach (var d in Declaraciones(Relativo(raiz, f), bloque.Groups[1].Value).Where(d => d.Propiedad == "color"))
                        foreach (var c in ColoresRetirados.Where(c => d.Valor.ToLowerInvariant().Contains(c)))
                            fallas.Add($"{d.Hoja} <style> color: {d.Valor}");
            }
            Assert.True(fallas.Count == 0, "Color de la plantilla anterior en un color: (use var(--sgv-texto-suave), --sgv-primario u --sgv-ok):\n" + Lista(fallas));
        }

        private static readonly Regex UsoVarCompleto = new Regex(@"var\(\s*(--[\w-]+)\s*\)");

        private static List<string> PaginasYMaster()
        {
            var raiz = RaizWeb();
            return Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories)
                .Where(f => new[] { ".aspx", ".master", ".ascx" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !Relativo(raiz, f).StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                         && !Relativo(raiz, f).StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static readonly string[] PesosFuente = { "400", "500", "600" };

        /// <summary>(h) IBM Plex Sans se sirve del propio sitio: 3 pesos woff2 en Content/fonts, con licencia, swap y respaldo.</summary>
        [Fact]
        public void H_FuentePropiaServidaDesdeElSitio()
        {
            var raiz = RaizWeb();
            var css = Leer(Base);
            var caras = Regex.Matches(css, @"@font-face\s*\{([^}]*)\}").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(PesosFuente.Length, caras.Count);
            var csproj = File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj"));
            foreach (var peso in PesosFuente)
            {
                var cara = caras.SingleOrDefault(c => Regex.IsMatch(c, @"font-weight:\s*" + peso + @"\s*;"));
                Assert.True(cara != null, "Falta la @font-face de peso " + peso);
                Assert.Contains("font-family: \"IBM Plex Sans\"", cara);
                Assert.Contains("font-display: swap", cara);
                var url = Regex.Match(cara, @"url\(""(fonts/[\w.-]+\.woff2)""\)\s*format\(""woff2""\)");
                Assert.True(url.Success, "La @font-face de " + peso + " debe apuntar a fonts/*.woff2 (relativa, sin otro origen)");
                var archivo = Path.Combine(raiz, "Content", url.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(archivo), "No existe " + archivo);
                var bytes = File.ReadAllBytes(archivo);
                Assert.True(bytes.Length > 1000 && bytes.Length < 40 * 1024, url.Groups[1].Value + " pesa " + bytes.Length + " bytes (subconjunto latino: < 40 KB)");
                Assert.Equal("wOF2", Encoding.ASCII.GetString(bytes, 0, 4));
                Assert.Contains("<Content Include=\"Content\\" + url.Groups[1].Value.Replace('/', '\\') + "\" />", csproj);
            }
            Assert.True(File.Exists(Path.Combine(raiz, "Content", "fonts", "OFL.txt")), "Falta la licencia OFL junto a las fuentes");
            Assert.Contains("SIL Open Font License", File.ReadAllText(Path.Combine(raiz, "Content", "fonts", "OFL.txt")));
            Assert.Contains("<Content Include=\"Content\\fonts\\OFL.txt\" />", csproj);

            // La familia y su respaldo están en una sola variable, y la usa el body.
            var fuente = Regex.Match(css, @"--sgv-fuente:\s*([^;]+);").Groups[1].Value.Trim();
            Assert.Equal("\"IBM Plex Sans\", \"Segoe UI\", Roboto, sans-serif", fuente);
            Assert.Matches(new Regex(@"(^|\})\s*body\s*\{[^}]*font-family:\s*var\(--sgv-fuente\)"), Enmascarar(css).Replace("\r", ""));

            // IIS sirve .woff2 (algunos no lo traen) y la CSP admite fuentes del propio sitio.
            var config = File.ReadAllText(Path.Combine(raiz, "Web.config"));
            Assert.Contains("<mimeMap fileExtension=\".woff2\" mimeType=\"font/woff2\" />", config);
            Assert.Matches(new Regex(@"font-src 'self'"), config);
        }

        /// <summary>(i) 0 orígenes externos: ni las hojas ni las páginas piden fuentes, hojas o scripts a otro sitio.</summary>
        [Fact]
        public void I_SinOrigenesExternos()
        {
            var fallas = new List<string>();
            foreach (var h in Hojas())
            {
                var t = Leer(h);
                if (Regex.IsMatch(t, @"@import", RegexOptions.IgnoreCase)) fallas.Add(h + ": @import");
                foreach (Match m in Regex.Matches(t, @"url\(\s*['""]?((?:https?:)?//[^'"")]+)", RegexOptions.IgnoreCase))
                    fallas.Add(h + ": " + m.Groups[1].Value);
            }
            var raiz = RaizWeb();
            var externo = new Regex(@"(?:href|src)\s*=\s*[""'](?:https?:)?//|fonts\.googleapis|fonts\.gstatic|use\.typekit|fonts\.bunny", RegexOptions.IgnoreCase);
            foreach (var f in PaginasYMaster())
                foreach (Match m in externo.Matches(File.ReadAllText(f)))
                    fallas.Add(Relativo(raiz, f) + ": " + m.Value);
            Assert.True(fallas.Count == 0, "Recurso pedido a otro origen (sírvalo desde Content/ o Scripts/):\n" + Lista(fallas));
        }

        /// <summary>Contraste WCAG 2.x entre dos colores #rrggbb.</summary>
        internal static double Contraste(string a, string b)
        {
            double Luminancia(string hex)
            {
                double Canal(int i)
                {
                    double c = Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0;
                    return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
                }
                return 0.2126 * Canal(1) + 0.7152 * Canal(3) + 0.0722 * Canal(5);
            }
            double la = Luminancia(a), lb = Luminancia(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>Pares texto/fondo de las semánticas que deben leerse (≥ 4,5:1). El foco se mide contra los dos fondos.</summary>
        public static IEnumerable<object[]> ParesDeContraste() => new[]
        {
            new object[] { "--sgv-texto", "--sgv-superficie" },
            new object[] { "--sgv-texto", "--sgv-fondo-pagina" },
            new object[] { "--sgv-texto", "--sgv-fondo" },
            new object[] { "--sgv-texto-medio", "--sgv-superficie" },
            new object[] { "--sgv-texto-suave", "--sgv-superficie" },
            new object[] { "--sgv-texto-suave", "--sgv-fondo" },
            new object[] { "--sgv-texto-suave", "--sgv-fondo-pagina" },
            new object[] { "--sgv-primario", "--sgv-superficie" },
            new object[] { "--sgv-primario", "--sgv-fondo-pagina" },
            new object[] { "--sgv-ok-texto", "--sgv-ok-fondo" },
            new object[] { "--sgv-alerta-texto", "--sgv-alerta-fondo" },
            new object[] { "--sgv-peligro-texto", "--sgv-peligro-fondo" },
            new object[] { "--sgv-info-texto", "--sgv-info-fondo" },
            new object[] { "--sgv-ok", "--sgv-superficie" },
            new object[] { "--sgv-alerta", "--sgv-superficie" },
            new object[] { "--sgv-peligro", "--sgv-superficie" },
            new object[] { "--sgv-blanco", "--sgv-primario" },
            new object[] { "--sgv-blanco", "--sgv-primario-osc" },
            new object[] { "--sgv-blanco", "--sgv-ok" },
            new object[] { "--sgv-blanco", "--sgv-alerta" },
            new object[] { "--sgv-blanco", "--sgv-peligro" },
            new object[] { "--sgv-blanco", "--sgv-texto-suave" },
            new object[] { "--sgv-foco", "--sgv-superficie" },
            new object[] { "--sgv-foco", "--sgv-fondo-pagina" },
        };

        /// <summary>(j) Contraste de al menos 4,5:1 (fórmula WCAG) en cada par texto/fondo de las semánticas.</summary>
        [Theory]
        [MemberData(nameof(ParesDeContraste))]
        public void J_ContrasteDeLasSemanticas(string texto, string fondo)
        {
            var raiz = RaizBase();
            string a = Resolver(raiz, texto), b = Resolver(raiz, fondo);
            Assert.Matches("^#[0-9a-f]{6}$", a);
            Assert.Matches("^#[0-9a-f]{6}$", b);
            double c = Contraste(a, b);
            Assert.True(c >= 4.5, $"{texto} ({a}) sobre {fondo} ({b}): {c:0.00}:1, mínimo 4,5:1");
        }

        [Fact]
        public void J_ContrasteCalculaComoWcag()
        {
            Assert.Equal(21.0, Contraste("#000000", "#ffffff"), 2);
            Assert.Equal(1.0, Contraste("#1f3a6e", "#1f3a6e"), 2);
            Assert.Equal(4.54, Contraste("#767676", "#ffffff"), 2);   // el gris más claro que pasa AA sobre blanco
            Assert.True(Contraste("#9ca3af", "#ffffff") < 4.5);       // el gris claro que E5-12 retira del texto
        }

        /// <summary>(k) Foco común con --sgv-foco en enlaces, botones y campos; cifras tabulares en tablas y montos.</summary>
        [Fact]
        public void K_FocoComunYCifrasTabulares()
        {
            var css = Enmascarar(Leer(Base)).Replace("\r", "");
            var regla = Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}").Cast<Match>()
                .FirstOrDefault(m => m.Groups[1].Value.Contains(":focus-visible") && m.Groups[2].Value.Contains("var(--sgv-foco)"));
            Assert.True(regla != null, "Falta la regla común de :focus-visible con var(--sgv-foco) en sgv-base.css");
            var selectores = regla.Groups[1].Value.Split(',').Select(s => s.Trim()).ToList();
            foreach (var s in new[] { "a:focus-visible", "button:focus-visible", ".btn:focus-visible", "input:focus-visible", "select:focus-visible", "textarea:focus-visible" })
                Assert.Contains(s, selectores);
            Assert.Matches(new Regex(@"outline:\s*2px solid var\(--sgv-foco\)"), regla.Groups[2].Value);

            var tabulares = Regex.Matches(css, @"([^{}]+)\{[^{}]*font-variant-numeric:\s*tabular-nums[^{}]*\}").Cast<Match>()
                .SelectMany(m => m.Groups[1].Value.Split(',').Select(s => s.Trim())).ToList();
            foreach (var s in new[] { "table", ".table", ".sgv-num", "input[data-monto]" })
                Assert.Contains(s, tabulares);
        }

        /// <summary>(l) Error.aspx guarda su CSS en línea: colores de la dirección y fuente de respaldo, sin la fuente web.</summary>
        [Fact]
        public void L_ErrorAspxConValoresDeLaDireccionSinFuenteWeb()
        {
            var texto = Leer("Views/Error.aspx");
            Assert.DoesNotContain("IBM Plex", texto);
            Assert.DoesNotContain("font-face", texto);
            Assert.Contains("font-family: \"Segoe UI\", Roboto, sans-serif", texto);
            foreach (var c in new[] { "#1a2230", "#566173", "#eceef1", "#1f3a6e" })
                Assert.Contains(c, texto);
        }

        private static readonly string[] FondosOscuros = { "primary", "info", "success", "danger", "secondary", "dark" };

        /// <summary>
        /// (m) .badge de Bootstrap 4 no define color: un "badge bg-primary" heredaba el texto oscuro (1,44:1). Todo badge
        /// con fondo oscuro en aspx, cs y js lleva texto blanco, por la regla de sgv-base.css o por text-white.
        /// </summary>
        [Fact]
        public void M_BadgesConFondoOscuroLlevanTextoBlanco()
        {
            var css = Enmascarar(Leer(Base)).Replace("\r", "");
            var conBlanco = new HashSet<string>(Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}").Cast<Match>()
                .Where(m => Regex.IsMatch(m.Groups[2].Value, @"(^|;)\s*color:\s*var\(--sgv-blanco\)"))
                .SelectMany(m => m.Groups[1].Value.Split(',').Select(s => s.Trim()))
                .Select(s => Regex.Match(s, @"^\.badge\.bg-([a-z]+)$").Groups[1].Value)
                .Where(s => s.Length > 0));
            foreach (var f in FondosOscuros)
                Assert.True(conBlanco.Contains(f), ".badge.bg-" + f + " sin color: var(--sgv-blanco) en sgv-base.css");

            // Cada uso en el código: o lo cubre la regla, o lleva text-white.
            var raiz = RaizWeb();
            var fallas = new List<string>();
            var clase = new Regex(@"[""']([^""'<>]*\bbadge\b[^""'<>]*)[""']");
            var archivos = Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories)
                .Where(a => new[] { ".aspx", ".master", ".ascx", ".cs", ".js" }.Contains(Path.GetExtension(a).ToLowerInvariant()))
                .Where(a => { var r = Relativo(raiz, a); return !r.StartsWith("obj/") && !r.StartsWith("bin/") && !r.StartsWith("Scripts/lib/") && !r.Contains(".min."); });
            int usos = 0;
            foreach (var a in archivos)
                foreach (Match m in clase.Matches(File.ReadAllText(a)))
                    foreach (Match bg in Regex.Matches(m.Groups[1].Value, @"\bbg-(primary|info|success|danger|secondary|dark)\b"))
                    {
                        usos++;
                        if (!conBlanco.Contains(bg.Groups[1].Value) && !Regex.IsMatch(m.Groups[1].Value, @"\btext-white\b"))
                            fallas.Add(Relativo(raiz, a) + ": " + m.Groups[1].Value);
                    }
            // El detector se prueba con una muestra fija: los badge bg-* reales se van con la plantilla de E5-18
            // (ListaDespachos ya no tiene; quedan los de RegistroDespacho), así que no se exige un mínimo de usos.
            Assert.Equal(2, clase.Matches("x = \"badge bg-info\"; y = '<span class=\"badge bg-dark text-white\">'").Cast<Match>()
                .Sum(m => Regex.Matches(m.Groups[1].Value, @"\bbg-(primary|info|success|danger|secondary|dark)\b").Count));
            Assert.True(fallas.Count == 0, "badge con fondo oscuro sin texto blanco:\n" + Lista(fallas));
        }

        // ------------------------------------------------------------------ letra mínima y style= (E5-18c)

        /// <summary>Letra mínima del sitio: 12 px = 0,75 rem (E5-4). Sin excepciones.</summary>
        private const double LetraMinimaPx = 12;

        // Tamaños con unidad de font-size y de font: (no los de line-height, que van tras la barra en font:).
        private static readonly Regex TamanoLetra = new Regex(@"(?<![\w./-])(?<n>\d*\.?\d+)(?<u>px|rem|em)\b", RegexOptions.IgnoreCase);

        internal static IEnumerable<string> LetrasChicas(string hoja, string texto) =>
            Declaraciones(hoja, texto)
                .Where(d => d.Propiedad.Equals("font-size", StringComparison.OrdinalIgnoreCase)
                         || d.Propiedad.Equals("font", StringComparison.OrdinalIgnoreCase))
                .SelectMany(d => TamanoLetra.Matches(d.Valor).Cast<Match>()
                    .Where(m => double.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture)
                                * (m.Groups["u"].Value.Equals("px", StringComparison.OrdinalIgnoreCase) ? 1 : 16) < LetraMinimaPx)
                    .Select(m => $"{d.Hoja}:{d.Linea} {d.Selector} {{ {d.Propiedad}: {d.Valor} }}"));

        /// <summary>(n) Ninguna hoja propia escribe letra de menos de 12 px / 0,75 rem (font-size ni font:).</summary>
        [Fact]
        public void N_SinLetraMenorDeDoceEnLasHojas()
        {
            var fallas = Hojas().SelectMany(h => LetrasChicas(h, Leer(h))).ToList();
            Assert.True(fallas.Count == 0, "Letra menor de 12 px (0,75 rem):\n" + Lista(fallas));
        }

        [Fact]
        public void N_LectorDeLetraDistingueTamanoDeInterlineado()
        {
            const string css = ".a { font-size: .72rem; } .b { font: 600 11px/1.4 var(--sgv-fuente); } .c { font: 12px/10px sans-serif; }\n"
                             + ".d { font-size: 0.75rem; line-height: 8px; } .e { font-size: 12px; } .f { font-size: .7em; } .g { width: 2px; }";
            var chicas = LetrasChicas("x.css", css).ToList();
            Assert.Equal(3, chicas.Count);
            Assert.Contains(chicas, c => c.Contains(".a {"));
            Assert.Contains(chicas, c => c.Contains(".b {"));
            Assert.Contains(chicas, c => c.Contains(".f {"));
        }

        /// <summary>
        /// style= que quedan en el marcado (lista cerrada, en igualdad exacta; el total solo baja). Fuera de
        /// Error.aspx (se pinta sin hojas) son los display:none de partida que el script alterna con
        /// style.display o .show(): un d-none (con !important) no se dejaría mostrar.
        /// </summary>
        private static readonly Dictionary<string, int> StyleEnMarcado = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Views/LiquidacionesPendientes.aspx"] = 15,   // .show()/.hide() de secciones, paneles y avisos
            ["Views/ConformarLiquidacion.aspx"] = 6,       // ConformarLiquidacion.js: style.display
            ["Views/MovimientosViaje.aspx"] = 3,           // MovimientosViaje.js mostrar(): style.display
            ["Views/FirmarLiquidacion.aspx"] = 1,          // vGastosDolaresFila
            ["Views/VerificarDocumento.aspx"] = 1,         // resultadoArchivo
            ["Views/Error.aspx"] = 3,                      // debe verse aunque fallen los archivos estáticos
        };

        private const int StyleEnMarcadoTotal = 29;

        private static readonly Regex AtributoStyle = new Regex(@"\bstyle\s*=", RegexOptions.IgnoreCase);
        private static readonly Regex StyleOcultoFijo = new Regex(@"\bstyle\s*=\s*""display:none;?""", RegexOptions.IgnoreCase);

        /// <summary>(o) style= del marcado solo en la lista cerrada, con total exacto, y fuera de Error.aspx solo style="display:none".</summary>
        [Fact]
        public void O_StyleEnElMarcadoSoloLaListaCerrada()
        {
            var raiz = RaizWeb();
            var fallas = new List<string>();
            int total = 0;
            foreach (var f in PaginasYMaster())
            {
                string rel = Relativo(raiz, f), texto = File.ReadAllText(f, Encoding.UTF8);
                int n = AtributoStyle.Matches(texto).Count;
                total += n;
                int esperado = StyleEnMarcado.TryGetValue(rel, out int e) ? e : 0;
                if (n != esperado) fallas.Add($"{rel}: {n} style= (se esperaban {esperado})");
                if (rel.Equals("Views/Error.aspx", StringComparison.OrdinalIgnoreCase)) continue;
                int fijos = StyleOcultoFijo.Matches(texto).Count;
                if (fijos != n) fallas.Add($"{rel}: {n - fijos} style= que no son literalmente style=\"display:none\" (use una clase en la hoja de la página)");
            }
            Assert.True(fallas.Count == 0, "style= en el marcado fuera de la lista cerrada:\n" + Lista(fallas));
            Assert.Equal(StyleEnMarcadoTotal, StyleEnMarcado.Values.Sum());
            Assert.Equal(StyleEnMarcadoTotal, total);
        }

        // ------------------------------------------------------------------ el lector mismo

        [Fact]
        public void Lector_IgnoraComentariosCadenasYDataUri()
        {
            const string css = "/* #123456 */ .a { color: #fff; background: url(\"data:image/svg+xml,%3csvg fill='%23343a40'/%3e\") no-repeat; }\n"
                             + ".b::before { content: \"#abc\"; border-color: rgba(0, 0, 0, .1); }\n"
                             + ":root { --x: var(--sgv-blanco); }\n"
                             + "@media (max-width: 500px) { .c:hover { box-shadow: 0 0 0 1px rgba(var(--sgv-negro-rgb), .2); color: white; } }";
            var d = Declaraciones("x.css", css);
            var lits = d.SelectMany(x => Literales(x.Valor).Select(l => x.Propiedad + "=" + l)).ToList();
            Assert.Equal(new[] { "color=#fff", "border-color=rgba(0, 0, 0, .1)", "color=white" }, lits);
            Assert.True(d.Single(x => x.Propiedad == "--x").EnRaiz);
            Assert.Equal(4, d.Single(x => x.Propiedad == "box-shadow").Linea);
        }
    }
}
