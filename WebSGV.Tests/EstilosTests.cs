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
    /// el :root de sgv-base.css (paleta exacta + semánticas) y las hojas solo usan var(). Lo genera y lo comprueba
    /// herramientas/estilos/colores.js (aplicar / verificar). Aquí se sostienen las reglas para lo que venga después.
    /// </summary>
    public class EstilosTests
    {
        private const string Base = "Content/sgv-base.css";

        // Variables de otras hojas que se quedan con su literal o su valor propio (lista cerrada).
        // --de-f-*: colores de las fases que lee DashboardExportacion.js con getComputedStyle.
        // --dc-radio: radio de las tarjetas del conductor (no es un color).
        private static readonly Regex RaizPermitida = new Regex(@"^--de-f-|^--dc-radio$");

        // var() que se definen fuera de las hojas (lista cerrada): --fase, --tam y --planta los pone
        // DashboardExportacion.js en style=; --dark-gray no está definida en ninguna parte (M-141).
        private static readonly HashSet<string> VariablesExternas = new HashSet<string>(StringComparer.Ordinal)
        {
            "--fase", "--tam", "--planta", "--dark-gray"
        };

        // Páginas sin Site.Master que no enlazan hojas (Error.aspx guarda su CSS en línea: debe verse aunque
        // fallen los archivos estáticos).
        private static readonly HashSet<string> SinMasterSinHojas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Views/Error.aspx", "Views/DescargarDocumento.aspx", "Views/DescargarPdfOrdenViaje.aspx"
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

        /// <summary>Las 31 hojas propias: Content/*.css y Content/paginas/**/*.css (Content/lib es de terceros).</summary>
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
        public void Hojas_SonLasTreintaYUnaPropias()
        {
            var hojas = Hojas();
            Assert.Contains(Base, hojas);
            Assert.True(hojas.Count == 31, "Hay " + hojas.Count + " hojas en Content; si agregaste o quitaste una, actualiza esta cifra (sus colores deben usar var(--sgv-*)).");
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
            Assert.True(paleta.Count > 100, "La paleta de sgv-base.css debería tener más de 100 colores; tiene " + paleta.Count);
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

            // Las 10 que existían antes de E5-11 siguen con su nombre y su valor.
            var raiz = defs.ToDictionary(d => d.Propiedad, d => d.Valor);
            string Resolver(string nombre)
            {
                var v = raiz[nombre];
                for (int i = 0; i < 5 && v.StartsWith("var(", StringComparison.Ordinal); i++)
                    v = raiz[Regex.Match(v, @"var\((--[\w-]+)\)").Groups[1].Value];
                return v;
            }
            var originales = new Dictionary<string, string>
            {
                ["--sgv-texto"] = "#1f2937", ["--sgv-texto-suave"] = "#6b7280", ["--sgv-borde"] = "#e5e7eb",
                ["--sgv-borde-fuerte"] = "#d1d5db", ["--sgv-fondo"] = "#f9fafb", ["--sgv-primario"] = "#1d4ed8",
                ["--sgv-ok"] = "#15803d", ["--sgv-alerta"] = "#b45309", ["--sgv-peligro"] = "#b91c1c", ["--sgv-radio"] = "6px"
            };
            foreach (var o in originales)
            {
                Assert.True(raiz.ContainsKey(o.Key), "Falta " + o.Key + " en sgv-base.css");
                Assert.Equal(o.Value, Resolver(o.Key));
            }
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
