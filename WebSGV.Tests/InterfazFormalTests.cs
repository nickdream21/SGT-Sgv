using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using static WebSGV.Tests.AccesibilidadMarcadoTests;
using static WebSGV.Tests.EncabezadoPaginaTests;

namespace WebSGV.Tests
{
    /// <summary>
    /// Interfaz formal (E5-24, DN-28). E5-24a: 0 emojis y símbolos decorativos (regla 1), redacción
    /// sin exclamaciones ni "exitosamente" (8) y botones en estilo oración (8b). E5-24b: iconos Font
    /// Awesome solo en los botones compactos de acción (con aria-label), en los avisos y en los
    /// funcionales (✕, menú del celular, desplegable, ojo, giratorios, chevron, play/pausa, quitar):
    /// fuera de títulos (2), etiquetas (3), menú (4), pestañas (5) y botones con texto (6); botón
    /// solo icono con aria-label (7); tarjetas KPI solo en la lista cerrada de E5-17/E5-18 (9);
    /// encabezados de tabla sin mayúsculas (10) y topes de iconos (11). Revisa los .aspx, .ascx,
    /// .Master, .cs (sin designer) y .js de <c>Views</c>, <c>Scripts/paginas</c>, <c>Helpers</c>,
    /// <c>Services</c> y <c>Site.Master(.cs)</c>.
    /// </summary>
    public class InterfazFormalTests
    {
        // ── Lector de archivos ───────────────────────────────────────────────────────────────

        internal sealed class Archivo
        {
            public string Ruta { get; set; }    // relativa a WebSGV/, con '/'
            public string Texto { get; set; }   // bruto, con comentarios

            public bool EsCodigo => Ruta.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                                 || Ruta.EndsWith(".js", StringComparison.OrdinalIgnoreCase);
            public bool EsJs => Ruta.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

            /// <summary>Sin comentarios: de código en .cs/.js, de servidor y HTML en el marcado.</summary>
            public string SinComentarios => EsCodigo ? SinComentariosDeCodigo(Texto) : AccesibilidadMarcadoTests.SinComentarios(Texto);
        }

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static readonly string[] Carpetas = { "Views", "Scripts/paginas", "Helpers", "Services" };
        private static readonly string[] Extensiones = { ".aspx", ".ascx", ".master", ".cs", ".js" };

        /// <summary>Para la regla 1 además: los .cs de <c>Models</c>, los .css de <c>Content/paginas</c> y <c>Web.config</c>.</summary>
        internal static List<Archivo> ArchivosParaEmojis()
        {
            string raiz = RaizWeb();
            var extra = Directory.GetFiles(Path.Combine(raiz, "Models"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(Path.Combine(raiz, "Content", "paginas"), "*.css", SearchOption.AllDirectories))
                .Concat(new[] { Path.Combine(raiz, "Web.config") })
                .Where(f => !f.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new Archivo
                {
                    Ruta = f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/'),
                    Texto = File.ReadAllText(f),
                });
            return Archivos().Concat(extra).ToList();
        }

        internal static List<Archivo> Archivos()
        {
            string raiz = RaizWeb();
            var rutas = Carpetas
                .SelectMany(c => Directory.GetFiles(Path.Combine(raiz, c.Replace('/', Path.DirectorySeparatorChar)), "*.*", SearchOption.AllDirectories))
                .Concat(new[] { Path.Combine(raiz, "Site.Master"), Path.Combine(raiz, "Site.Master.cs") })
                .Where(f => Extensiones.Contains(Path.GetExtension(f).ToLowerInvariant())
                         && !f.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            return rutas.Select(f => new Archivo
            {
                Ruta = f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/'),
                Texto = File.ReadAllText(f),
            }).ToList();
        }

        private static int Linea(string texto, int indice) => texto.Take(indice).Count(c => c == '\n') + 1;

        /// <summary>
        /// Quita los comentarios <c>//</c> y <c>/* */</c> de C# o JS respetando las cadenas ('…', "…",
        /// @"…" y `…`); deja los saltos de línea para que los números de línea sigan valiendo. Una
        /// cadena normal termina en el salto de línea, así una expresión regular de JS con comillas
        /// solo enreda su propia línea.
        /// </summary>
        internal static string SinComentariosDeCodigo(string t)
        {
            var sb = new StringBuilder(t.Length);
            int i = 0, n = t.Length;
            while (i < n)
            {
                char c = t[i];
                if (c == '/' && i + 1 < n && t[i + 1] == '/')
                {
                    while (i < n && t[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && i + 1 < n && t[i + 1] == '*')
                {
                    int f = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    int fin = f < 0 ? n : f + 2;
                    for (int k = i; k < fin; k++) if (t[k] == '\n') sb.Append('\n');
                    i = fin;
                    continue;
                }
                if (c == '"' || c == '\'' || c == '`')
                {
                    bool literal = c == '"' && i > 0 && (t[i - 1] == '@' || (t[i - 1] == '$' && i > 1 && t[i - 2] == '@'));
                    sb.Append(c);
                    i++;
                    while (i < n)
                    {
                        char d = t[i];
                        if (literal)
                        {
                            if (d == '"' && i + 1 < n && t[i + 1] == '"') { sb.Append("\"\""); i += 2; continue; }
                        }
                        else if (d == '\\' && i + 1 < n) { sb.Append(d).Append(t[i + 1]); i += 2; continue; }
                        else if (d == '\n' && c != '`') break;
                        sb.Append(d);
                        i++;
                        if (d == c) break;
                    }
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // ── Regla 1: 0 emojis ni símbolos decorativos ────────────────────────────────────────

        /// <summary>Lista cerrada de símbolos de los rangos de pictogramas que sí se usan: flechas de
        /// navegación y de orden (← → ↔) y los triángulos de subida y bajada de los gráficos y tablas (▲ ▼).</summary>
        internal static readonly HashSet<int> SimbolosPermitidos = new HashSet<int> { 0x2190, 0x2192, 0x2194, 0x25B2, 0x25BC };

        /// <summary>Flechas, técnicos, figuras geométricas, misceláneos, dingbats, flechas y figuras
        /// suplementarias, el plano de emojis, el selector de variación emoji y el unidor de ancho cero;
        /// además los sueltos que se ven como emoji (ℹ ‼ ⁉ ⤴ ⤵ 〰) y el marco de tecla (U+20E3).
        /// Quedan fuera ─ • · − ≈ ≤ ≥ ×.</summary>
        internal static bool EsPictograma(int cp) =>
            (cp >= 0x2190 && cp <= 0x21FF) || (cp >= 0x2300 && cp <= 0x23FF) || (cp >= 0x25A0 && cp <= 0x25FF)
            || (cp >= 0x2600 && cp <= 0x27BF) || (cp >= 0x2B00 && cp <= 0x2BFF) || (cp >= 0x1F000 && cp <= 0x1FBFF)
            || cp == 0xFE0F || cp == 0x200D
            || cp == 0x2139 || cp == 0x203C || cp == 0x2049 || cp == 0x2934 || cp == 0x2935 || cp == 0x3030 || cp == 0x20E3;

        private static bool Prohibido(int cp) => EsPictograma(cp) && !SimbolosPermitidos.Contains(cp);

        private static readonly Regex EscapeU = new Regex(@"\\u(?<h>[0-9A-Fa-f]{4})|\\u\{(?<h>[0-9A-Fa-f]{1,6})\}|\\U(?<h>[0-9A-Fa-f]{8})");
        private static readonly Regex Entidad = new Regex(@"&#(?:(?<d>\d{1,7})|[xX](?<h>[0-9A-Fa-f]{1,6}));");
        private static readonly Regex EntidadConNombre = new Regex(@"&(?<n>[a-zA-Z][a-zA-Z0-9]{1,31});");

        /// <summary>Entidades con nombre de HTML5 que <see cref="WebUtility.HtmlDecode"/> (HTML 4) no conoce
        /// y que dan pictogramas.</summary>
        private static readonly Dictionary<string, string> EntidadesHtml5 = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["check"] = "✓", ["checkmark"] = "✓", ["cross"] = "✗", ["star"] = "☆", ["starf"] = "★",
            ["bigstar"] = "★", ["phone"] = "☎", ["female"] = "♀", ["male"] = "♂", ["sung"] = "♪",
            ["flat"] = "♭", ["natural"] = "♮", ["sharp"] = "♯", ["malt"] = "✠", ["maltese"] = "✠",
            ["sext"] = "✶", ["squ"] = "□", ["square"] = "□", ["Square"] = "□", ["squf"] = "▪",
            ["blacksquare"] = "▪", ["rect"] = "▭", ["cir"] = "○", ["xcirc"] = "◯", ["utrif"] = "▴",
            ["dtrif"] = "▾", ["ltrif"] = "◂", ["rtrif"] = "▸", ["utri"] = "▵", ["dtri"] = "▿",
            ["ltri"] = "◃", ["rtri"] = "▹", ["xutri"] = "△", ["xdtri"] = "▽", ["uarr"] = "↑",
            ["darr"] = "↓", ["crarr"] = "↵", ["lArr"] = "⇐", ["rArr"] = "⇒", ["hArr"] = "⇔",
            ["uArr"] = "⇑", ["dArr"] = "⇓", ["incare"] = "℅", ["hearts"] = "♥", ["spades"] = "♠",
            ["clubs"] = "♣", ["diams"] = "♦", ["loz"] = "◊",
        };

        private static string DecodificarEntidad(string nombre)
        {
            if (EntidadesHtml5.TryGetValue(nombre, out var v)) return v;
            string d = WebUtility.HtmlDecode("&" + nombre + ";");
            return d == "&" + nombre + ";" ? null : d;
        }

        /// <summary>Cada emoji o símbolo decorativo del texto bruto (carácter, escape <c>\u…</c> o
        /// <c>\u{…}</c>, entidad <c>&amp;#…;</c> o con nombre), con su posición.</summary>
        internal static List<(int Indice, string Texto)> Emojis(string texto)
        {
            var hallados = new List<(int, string)>();
            for (int i = 0; i < texto.Length; i++)
            {
                int cp = texto[i];
                if (char.IsHighSurrogate(texto[i]) && i + 1 < texto.Length && char.IsLowSurrogate(texto[i + 1]))
                {
                    cp = char.ConvertToUtf32(texto[i], texto[i + 1]);
                    if (Prohibido(cp)) hallados.Add((i, texto.Substring(i, 2)));
                    i++;
                    continue;
                }
                if (Prohibido(cp)) hallados.Add((i, texto[i].ToString()));
            }
            foreach (Match m in EscapeU.Matches(texto))
            {
                int cp = int.Parse(m.Groups["h"].Value, NumberStyles.HexNumber);
                if ((cp >= 0xD83C && cp <= 0xD83E) || Prohibido(cp)) hallados.Add((m.Index, m.Value));
            }
            foreach (Match m in Entidad.Matches(texto))
            {
                int cp = m.Groups["d"].Success
                    ? int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture)
                    : int.Parse(m.Groups["h"].Value, NumberStyles.HexNumber);
                if (Prohibido(cp)) hallados.Add((m.Index, m.Value));
            }
            foreach (Match m in EntidadConNombre.Matches(texto))
            {
                string d = DecodificarEntidad(m.Groups["n"].Value);
                if (d == null) continue;
                for (int i = 0; i < d.Length; i++)
                {
                    int cp = char.IsHighSurrogate(d[i]) && i + 1 < d.Length ? char.ConvertToUtf32(d[i], d[++i]) : d[i];
                    if (Prohibido(cp)) { hallados.Add((m.Index, m.Value)); break; }
                }
            }
            return hallados.OrderBy(h => h.Item1).ToList();
        }

        [Fact]
        public void CeroEmojisNiSimbolosDecorativos()
        {
            var casos = ArchivosParaEmojis()
                .SelectMany(a => Emojis(a.Texto).Select(e => $"{a.Ruta}:{Linea(a.Texto, e.Indice)} {e.Texto}"))
                .ToList();
            Assert.True(casos.Count == 0, "Emojis o símbolos decorativos (DN-28; los permitidos son ← → ↔ ▲ ▼):\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_Emojis_CaracteresEscapesYEntidades()
        {
            Assert.Single(Emojis("SGV.avisar('\u2705 Listo');"));
            Assert.Single(Emojis("aviso '\u26A0\uFE0F'").Where(e => e.Texto == "\u26A0"));
            Assert.Equal(2, Emojis("'\u26A0\uFE0F'").Count);                     // el símbolo y su selector
            Assert.Single(Emojis("// \U0001F680 cohete"));                        // par sustituto
            Assert.Single(Emojis(@"SGV.avisar('\u274c ' + m);"));                // escape
            Assert.Single(Emojis(@"'\u2705'"));
            Assert.Single(Emojis(@"'\uD83D\uDE80'"));                            // escape de par sustituto
            Assert.Single(Emojis("<div>&#9888;</div>"));                         // entidad decimal
            Assert.Single(Emojis("<span>&#x2714;</span>"));                      // entidad hex
            Assert.Single(Emojis("<span>\u25CF Rojo</span>"));                   // figura geométrica
            Assert.Single(Emojis("\u2713 Completo"));
            // Sueltos fuera de los rangos que igual se ven como emoji.
            Assert.Single(Emojis("\u2139 Info"));
            Assert.Single(Emojis("Ojo\u203c"));
            Assert.Single(Emojis("\u00bfQu\u00e9\u2049"));
            Assert.Single(Emojis("\u2934 sube"));
            Assert.Single(Emojis("\u2935 baja"));
            Assert.Single(Emojis("\u3030"));
            Assert.Single(Emojis("1\u20e3 paso"));                                // marco de tecla
            // Escape ES6 con llaves.
            Assert.Single(Emojis(@"SGV.avisar('\u{1F680} Listo');"));
            Assert.Single(Emojis(@"'\u{2705}'"));
            Assert.Empty(Emojis(@"'\u{2190}' '\u{F3}'"));
            // Entidades con nombre (HTML 4 y HTML 5).
            Assert.Single(Emojis("<span>&check;</span>"));
            Assert.Single(Emojis("<span>&star;</span>"));
            Assert.Single(Emojis("<span>&hearts;</span>"));
            Assert.Single(Emojis("<span>&uarr;</span>"));
            Assert.Empty(Emojis("&larr; &rarr; &harr; &times; &nbsp; &oacute; &amp; &middot; &bull; &desconocida;"));
        }

        [Fact]
        public void Detector_Emojis_PermiteLaListaCerradaYLosSimbolosTipograficos()
        {
            Assert.Empty(Emojis("\u2190 Volver \u2192 Siguiente \u2194 \u25B2 sube \u25BC baja"));
            Assert.Empty(Emojis("&#x25B2; &#x25BC; &#8592; &#8594;"));
            Assert.Empty(Emojis(@"'\u2190' '\u25b2'"));
            Assert.Empty(Emojis("\u2500\u2500 Sección \u2022 a \u00B7 b \u2212 c \u2248 d \u2264 e \u2265 f \u00D7 g"));
            Assert.Empty(Emojis(@"'liquidaci\u00f3n' '\u00bfSeguro?' &#243; &times; &larr;"));
        }

        // ── Regla 8: redacción sin exclamaciones ni "exitosamente" ───────────────────────────

        private static readonly Regex Exitosamente = new Regex(@"exitosamente", RegexOptions.IgnoreCase);
        private static readonly Regex Exclamacion = new Regex(@"\p{L}[.)]?!(?![=\\\]])");   // ni != / !==, ni un ! dentro de una clase de regex ([A-Za-z!\/?])

        /// <summary>Faltas de redacción en el texto ya sin comentarios: <c>¡</c>, "exitosamente" y una
        /// palabra que cierra con <c>!</c> (sin contar <c>!=</c> ni <c>!==</c>).</summary>
        internal static List<(int Indice, string Texto)> FaltasDeRedaccion(string sinComentarios)
        {
            var faltas = new List<(int, string)>();
            for (int i = sinComentarios.IndexOf('¡'); i >= 0; i = sinComentarios.IndexOf('¡', i + 1))
                faltas.Add((i, "¡"));
            foreach (Match m in Exitosamente.Matches(sinComentarios)) faltas.Add((m.Index, m.Value));
            foreach (Match m in Exclamacion.Matches(sinComentarios)) faltas.Add((m.Index, m.Value));
            return faltas.OrderBy(f => f.Item1).ToList();
        }

        [Fact]
        public void RedaccionSinExclamacionesNiExitosamente()
        {
            var casos = Archivos()
                .SelectMany(a =>
                {
                    string t = a.SinComentarios;
                    return FaltasDeRedaccion(t).Select(f => $"{a.Ruta}:{Linea(t, f.Indice)} {f.Texto}");
                })
                .ToList();
            Assert.True(casos.Count == 0, "El mensaje dice lo que pasó, sin ¡, sin \"exitosamente\" y sin exclamaciones (DN-28):\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_FaltasDeRedaccion()
        {
            Assert.Single(FaltasDeRedaccion("MostrarMensaje(\"¡Listo\");"));
            Assert.Single(FaltasDeRedaccion("MostrarMensaje(\"Usuario creado EXITOSAMENTE.\");"));
            Assert.Single(FaltasDeRedaccion("MostrarMensaje(\"Hecho!\");"));
            Assert.Single(FaltasDeRedaccion("SGV.avisar('Guardado.!');"));
            Assert.Single(FaltasDeRedaccion("<p>Bienvenido!</p>"));
            Assert.Single(FaltasDeRedaccion("x = \"(listo)! \";"));
            Assert.Single(FaltasDeRedaccion("MostrarMensaje(\"Listo! Ahora puede seguir.\");"));
            Assert.Empty(FaltasDeRedaccion("if (a!=b || c!==d) { }"));
            Assert.Empty(FaltasDeRedaccion("v.replace(/<(?=[A-Za-z!\\/?])/g, '< ');"));
            Assert.Empty(FaltasDeRedaccion("/[a-z!]/.test(x);"));
            Assert.Empty(FaltasDeRedaccion("if (a != \"b\") { }"));
            Assert.Empty(FaltasDeRedaccion("if (!valido) return; x = !y;"));
            Assert.Empty(FaltasDeRedaccion("if (tipo !== 'danger') cerrar();"));
            Assert.Empty(FaltasDeRedaccion("MostrarMensaje(\"Datos guardados correctamente.\");"));
        }

        [Fact]
        public void Detector_SinComentariosDeCodigo_RespetaLasCadenas()
        {
            Assert.Equal("a = 1; \nb", SinComentariosDeCodigo("a = 1; // ¡hola!\nb"));
            Assert.Equal("a\n\n\nb", SinComentariosDeCodigo("a/* ✅\n uno\n */\nb"));
            Assert.Equal("x = \"http://sitio\";", SinComentariosDeCodigo("x = \"http://sitio\";"));
            Assert.Equal("x = 'a // b';", SinComentariosDeCodigo("x = 'a // b';"));
            Assert.Equal("x = @\"c:\\ \"\"// no\"\"\";", SinComentariosDeCodigo("x = @\"c:\\ \"\"// no\"\"\";"));
            Assert.Equal("x = `a\n// b`;", SinComentariosDeCodigo("x = `a\n// b`;"));
            Assert.Equal("x = \"a \\\" // b\";", SinComentariosDeCodigo("x = \"a \\\" // b\";"));
            Assert.Equal("x = 'it\\'s'; ", SinComentariosDeCodigo("x = 'it\\'s'; // fin"));
        }

        // ── Regla 8b: botones en estilo oración ──────────────────────────────────────────────

        private static readonly Regex CierreBoton = new Regex(@"</button\s*>", RegexOptions.IgnoreCase);
        private static readonly Regex CierreEnlace = new Regex(@"</a\s*>", RegexOptions.IgnoreCase);
        private static readonly Regex HtmlDeBotonJs = new Regex(
            @"(?<obj>(?:[\w$]+|\$\([^)]*\))(?:\s*\.\s*\w+\([^()]*\))*)\s*\.\s*(?:html\(\s*|text\(\s*|(?:innerHTML|textContent)\s*=\s*)(?:'(?<t>(?:[^'\\\n]|\\.)*)'|""(?<t>(?:[^""\\\n]|\\.)*)"")\s*[);]",
            RegexOptions.IgnoreCase);
        private static readonly Regex EscapeJs = new Regex(@"\\u(?<h>[0-9A-Fa-f]{4})");

        private static readonly Regex CorteDeBloque = new Regex(@"</?(?:h[1-6]|div|p|br|li|small)\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex FinDeFrase = new Regex(@"(?<=[.?!:])\s+");

        /// <summary>Frases visibles de un botón: corta en los bloques (&lt;h5&gt;, &lt;div&gt;,
        /// &lt;small&gt;…) y al final de cada oración, y quita lo que va antes de la primera letra
        /// ("+ Nuevo despacho", el número de paso), para que cada frase se mida desde su primera palabra.</summary>
        internal static List<string> FrasesVisibles(string html)
        {
            string t = Regex.Replace(html, @"<%.*?%>", " ", RegexOptions.Singleline);
            t = EscapeJs.Replace(t, m => ((char)int.Parse(m.Groups["h"].Value, NumberStyles.HexNumber)).ToString());
            return CorteDeBloque.Split(t)
                .SelectMany(b => FinDeFrase.Split(Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(b, @"<[^>]*>", " ")), @"\s+", " ")))
                .Select(f => Regex.Replace(f, @"^[^\p{L}]+", "").Trim())
                .Where(f => f.Length > 0)
                .ToList();
        }

        /// <summary>Frases de botón: <c>Text=</c> de asp:Button y asp:LinkButton y el contenido de
        /// &lt;button&gt; y de &lt;a class="btn"&gt; en el marcado; <c>.html('…')</c>, <c>.text('…')</c>,
        /// <c>innerHTML = '…'</c> y <c>textContent = '…'</c> sobre un objeto
        /// cuyo nombre dice btn o boton (en .js y en los &lt;script&gt; del marcado).</summary>
        internal static List<(int Indice, string Texto)> TextosDeBoton(string sinComentarios, bool esJs)
        {
            var textos = new List<(int, string)>();
            if (!esJs)
            {
                foreach (var e in Elementos(sinComentarios))
                {
                    if ((e.Nombre == "asp:button" || e.Nombre == "asp:linkbutton") && e.Tiene("Text") && !e.Attr("Text").Contains("<%"))
                        textos.AddRange(FrasesVisibles(e.Attr("Text")).Select(f => (e.Indice, f)));
                    else if ((e.Nombre == "button" || (e.Nombre == "a" && e.TieneClase("btn"))) && !e.Cerrado)
                    {
                        var cierre = (e.Nombre == "a" ? CierreEnlace : CierreBoton).Match(sinComentarios, e.Fin);
                        if (cierre.Success)
                            textos.AddRange(FrasesVisibles(sinComentarios.Substring(e.Fin, cierre.Index - e.Fin)).Select(f => (e.Indice, f)));
                    }
                }
            }
            foreach (Match m in HtmlDeBotonJs.Matches(sinComentarios))
            {
                if (!Regex.IsMatch(m.Groups["obj"].Value, "btn|boton|botón", RegexOptions.IgnoreCase)) continue;
                textos.AddRange(FrasesVisibles(m.Groups["t"].Value).Select(f => (m.Index, f)));
            }
            return textos;
        }

        [Fact]
        public void BotonesEnEstiloOracion()
        {
            var casos = Archivos()
                .Where(a => a.EsJs || !a.EsCodigo)
                .SelectMany(a =>
                {
                    string t = a.SinComentarios;
                    return TextosDeBoton(t, a.EsJs)
                        .Where(b => PalabrasConMayuscula(b.Texto).Count > 0)
                        .Select(b => $"{a.Ruta}:{Linea(t, b.Indice)} \"{b.Texto}\" ({string.Join(", ", PalabrasConMayuscula(b.Texto))})");
                })
                .ToList();
            Assert.True(casos.Count == 0, "Botones en estilo oración: solo la primera palabra y los nombres propios con mayúscula (DN-28):\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_TextosDeBoton_MarcadoYJs()
        {
            var marcado = TextosDeBoton(
                "<asp:Button ID=\"b\" runat=\"server\" Text=\"Firmar y Enviar\" />"
                + "<asp:LinkButton ID=\"l\" runat=\"server\" Text='<%# Eval(\"x\") %>' />"
                + "<button type=\"button\" class=\"btn\"><i class=\"fas fa-undo mr-1\"></i>Confirmar Reversi&oacute;n</button>"
                + "<button type=\"button\" class=\"close\"><span aria-hidden=\"true\">&times;</span></button>"
                + "<button class=\"btn\"><i class=\"fas fa-trash\"></i></button>"
                + "<a class=\"btn\">+ Nuevo despacho</a><button class=\"btn\">+ Nuevo despacho</button>"
                + "<button type=\"button\" class=\"x<%= Abierto ? \"\" : \" collapsed\" %>\" aria-expanded=\"<%= Abierto ? \"true\" : \"false\" %>\">"
                + "<h5><span>1</span> Registrar un lote a mano</h5><div>Para despachos sin Word. Los de exportación van arriba.</div></button>"
                + "<a href=\"/x\" class=\"btn btn-primary\"><i class=\"fas fa-home\"></i>Ir al Inicio</a>"
                + "<a href=\"/y\" class=\"btnx\">Enlace Suelto</a><a href=\"/z\">Otro Enlace</a>",
                esJs: false);
            Assert.Equal(new[] { "Firmar y Enviar", "Confirmar Reversión", "Nuevo despacho", "Nuevo despacho",
                                 "Registrar un lote a mano", "Para despachos sin Word.", "Los de exportación van arriba.", "Ir al Inicio" },
                         marcado.Select(b => b.Texto).ToArray());

            var js = TextosDeBoton(
                "$btn.prop('disabled', false).html('<i class=\"fas fa-undo mr-1\"></i>Confirmar Reversi\\u00f3n');\n"
                + "btnFirmar.innerHTML = '<i class=\"fas fa-check-circle\"></i>Firmar y Enviar';\n"
                + "$('#btnGuardar').html(\"Guardar\");\n"
                + "$('#resultado').html('<b>Total Neto</b>');\n"
                + "$btn.html('<i class=\"fas fa-spinner fa-spin\"></i>');\n"
                + "$btn.prop('disabled', false).text('S\\u00ed, Retirar');\n"
                + "boton.textContent = \"Guardar Cambios\";\n"
                + "$('#lblTotal').text('Total General');", esJs: true);
            Assert.Equal(new[] { "Confirmar Reversión", "Firmar y Enviar", "Guardar", "Sí, Retirar", "Guardar Cambios" },
                         js.Select(b => b.Texto).ToArray());
        }

        [Fact]
        public void Detector_PalabrasConMayuscula_EnBotones()
        {
            Assert.Equal(new[] { "Enviar" }, PalabrasConMayuscula("Firmar y Enviar"));
            Assert.Equal(new[] { "Inicio" }, PalabrasConMayuscula("Ir al Inicio"));
            Assert.Empty(PalabrasConMayuscula("Exportar a Excel"));
            Assert.Empty(PalabrasConMayuscula("Descargar PDF"));
            Assert.Empty(PalabrasConMayuscula("Cargar documentos Word y ZIP"));
            Assert.Empty(PalabrasConMayuscula("Consultar RUC en SUNAT"));
            Assert.Empty(PalabrasConMayuscula("Ver OV-000123"));
            Assert.Empty(PalabrasConMayuscula("Firmar y enviar"));
        }

        // ── Iconos (E5-24b) ──────────────────────────────────────────────────────────────────

        /// <summary>Icono Font Awesome del marcado: &lt;i&gt; o &lt;span&gt; cuya clase tiene fa/fas/far/fab/fal/fad
        /// y una fa-… (en cualquier orden). El grupo <c>c</c> es la clase.</summary>
        internal static readonly Regex IconoFa = new Regex(
            @"<(?:i|span)\b[^>]*?\bclass\s*=\s*[""'](?<c>(?=[^""']*\bfa[srlbd]?\b(?!-))(?=[^""']*\bfa-[\w-]+)[^""']*)[""'][^>]*>",
            RegexOptions.IgnoreCase);

        /// <summary>Lista cerrada de iconos funcionales que pueden acompañar un texto (DN-28): giratorios de
        /// espera, flecha o chevron de desplegar y play/pausa del mapa. Solo se achica.</summary>
        internal static readonly Regex IconoFuncional = new Regex(@"\bfa-(?:spin|spinner|chevron-(?:down|up|left|right)|play|pause)\b");

        private static IEnumerable<Archivo> Marcado() => Archivos().Where(a => !a.EsCodigo);
        private static IEnumerable<Archivo> Codigo() => Archivos().Where(a => a.EsCodigo);

        private static readonly Dictionary<string, Regex> Cierres = new Dictionary<string, Regex>(StringComparer.Ordinal);

        /// <summary>Lo que hay entre la etiqueta de apertura y su cierre (<c>&lt;/a&gt;</c> no confunde
        /// <c>&lt;/asp:…&gt;</c>); null si no cierra.</summary>
        internal static string Interior(string texto, Elemento e)
        {
            if (e.Cerrado) return null;
            if (!Cierres.TryGetValue(e.Nombre, out var re))
                Cierres[e.Nombre] = re = new Regex("</" + Regex.Escape(e.Nombre) + @"\s*>", RegexOptions.IgnoreCase);
            var m = re.Match(texto, e.Fin);
            return m.Success ? texto.Substring(e.Fin, m.Index - e.Fin) : null;
        }

        private static readonly Regex SrOnly = new Regex(@"<span\b[^>]*\bclass\s*=\s*[""'][^""']*\bsr-only\b[^""']*[""'][^>]*>.*?</span\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>Texto que se ve dentro de un botón: sin etiquetas ni <c>sr-only</c>; <c>&lt;%= %&gt;</c>,
        /// <c>&lt;%: %&gt;</c>, <c>&lt;%# %&gt;</c> y asp:Label/Literal cuentan como texto (lo pone el servidor).</summary>
        internal static string TextoVisible(string interior)
        {
            string t = SrOnly.Replace(interior, "");
            t = Regex.Replace(t, @"<%[=:#].*?%>", "T", RegexOptions.Singleline);
            t = Regex.Replace(t, @"<%.*?%>", "", RegexOptions.Singleline);
            t = Regex.Replace(t, @"<asp:(Label|Literal|Localize)\b", "T<", RegexOptions.IgnoreCase);
            return Regex.Replace(t, @"<[^>]*>|&nbsp;|&#160;|\s+", "");
        }

        private static readonly HashSet<string> Pulsables = new HashSet<string> { "button", "a", "asp:linkbutton", "asp:hyperlink" };

        private static string Donde(Archivo a, string t, int indice, string que) =>
            $"{a.Ruta}:{Linea(t, indice)} {Regex.Replace(que, @"\s+", " ")}";

        // ── Regla 2: sin iconos en los títulos h1-h6; regla 3: ni en las etiquetas ──────────

        private const string IconoEnAtributo = @"<(?:i|span)\b[^>]*?\bclass\s*=\s*[""'](?=[^""']*\bfa[srlbd]?\b(?!-))(?=[^""']*\bfa-[\w-]+)[^""']*[""']";
        private static readonly Regex IconoEnTitulo = new Regex(
            @"<h(?<n>[1-6])\b[^>]*>(?:(?!</h\k<n>\s*>).)*?(?<i>" + IconoEnAtributo + ")", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        private static readonly Regex IconoEnEtiqueta = new Regex(
            @"<label\b[^>]*>(?:(?!</label\s*>).)*?(?<i>" + IconoEnAtributo + ")", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        // En .cs/.js el HTML va en cadenas de una línea, con las comillas escapadas o simples.
        private static readonly Regex IconoEnTituloCodigo = new Regex(
            @"<h(?<n>[1-6])\b[^>\n]*>(?:(?!</h\k<n>)[^\n])*?(?<i><i\s+class\s*=\s*\\?[""']fa[srlbd]?\s)", RegexOptions.IgnoreCase);
        private static readonly Regex IconoEnEtiquetaCodigo = new Regex(
            @"<label\b[^>\n]*>(?:(?!</label)[^\n])*?(?<i><i\s+class\s*=\s*\\?[""']fa[srlbd]?\s)", RegexOptions.IgnoreCase);

        internal static List<int> IconosEnTitulos(string sinComentarios, bool esCodigo) =>
            (esCodigo ? IconoEnTituloCodigo : IconoEnTitulo).Matches(sinComentarios).Cast<Match>().Select(m => m.Groups["i"].Index).ToList();

        internal static List<int> IconosEnEtiquetas(string sinComentarios, bool esCodigo) =>
            (esCodigo ? IconoEnEtiquetaCodigo : IconoEnEtiqueta).Matches(sinComentarios).Cast<Match>().Select(m => m.Groups["i"].Index).ToList();

        [Fact]
        public void TitulosSinIconos()
        {
            var casos = Archivos().SelectMany(a =>
            {
                string t = a.SinComentarios;
                return IconosEnTitulos(t, a.EsCodigo).Select(i => $"{a.Ruta}:{Linea(t, i)}");
            }).ToList();
            Assert.True(casos.Count == 0, "Los títulos h1-h6 van sin icono (DN-28); un chevron de desplegar va fuera del <hN>:\n" + string.Join("\n", casos));
        }

        [Fact]
        public void EtiquetasSinIconos()
        {
            var casos = Archivos().SelectMany(a =>
            {
                string t = a.SinComentarios;
                return IconosEnEtiquetas(t, a.EsCodigo).Select(i => $"{a.Ruta}:{Linea(t, i)}");
            }).ToList();
            Assert.True(casos.Count == 0, "Las etiquetas (<label>) van sin icono (DN-28); una ayuda va en title o en un texto aparte:\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_IconosEnTitulosYEtiquetas()
        {
            Assert.Single(IconosEnTitulos("<h5 class=\"mb-0\"><i class=\"fas fa-filter mr-2\"></i>Filtros</h5>", false));
            Assert.Single(IconosEnTitulos("<h1 class=\"h2\">Lista <span class='far fa-list'></span></h1>", false));
            Assert.Single(IconosEnTitulos("<h6>\n  <i aria-hidden=\"true\" class=\"text-muted fas fa-truck\"></i>\n  Viajes\n</h6>", false));
            Assert.Single(IconosEnTitulos("<h3><i class=\"fa-solid fas fa-x\"></i>A</h3>", false));
            Assert.Empty(IconosEnTitulos("<h5>Filtros</h5><button><i class=\"fas fa-x\"></i>B</button>", false));
            Assert.Empty(IconosEnTitulos("<div class=\"d-flex\"><h5>A</h5><i class=\"fas fa-chevron-down\"></i></div>", false));
            Assert.Empty(IconosEnTitulos("<h5>A <i class=\"fancy\">b</i> <span class=\"fa-x\"></span></h5>", false));
            Assert.Empty(IconosEnTitulos("<header><h1>A</h1></header><i class=\"fas fa-x\"></i>", false));
            Assert.Single(IconosEnTitulos("sb.Append(\"<h6 class='x'><i class='fas fa-truck mr-1'></i>Por camión</h6>\");", true));
            Assert.Single(IconosEnTitulos("sb.Append($\"<h5 class=\\\"mb-1\\\"><i class=\\\"fas fa-folder-open mr-2\\\"></i>{H(c)}</h5>\");", true));
            Assert.Single(IconosEnTitulos("html += '<h4>Título <i class=\"far fa-x\"></i></h4>';", true));
            Assert.Empty(IconosEnTitulos("sb.Append(\"<h6>Por camión</h6><i class='fas fa-x'></i>\");", true));

            Assert.Single(IconosEnEtiquetas("<label for=\"x\"><i class=\"fas fa-lock mr-1\"></i>Contraseña</label>", false));
            Assert.Single(IconosEnEtiquetas("<label class=\"bd-zona\">\n<i class=\"fas fa-cloud-upload-alt bd-zona-icono\"></i>\n<span>Arrastre</span></label>", false));
            Assert.Empty(IconosEnEtiquetas("<label for=\"x\" title=\"Ayuda\">Categoría</label><i class=\"fas fa-x\"></i>", false));
            Assert.Single(IconosEnEtiquetas("html += '<label><i class=\"fas fa-x\"></i>A</label>';", true));
        }

        // ── Regla 4: el menú de Site.Master sin iconos ───────────────────────────────────────

        [Fact]
        public void MenuSinIconos()
        {
            var sm = Archivos().Single(a => a.Ruta == "Site.Master");
            string t = sm.SinComentarios;
            int ini = t.IndexOf("<nav", StringComparison.OrdinalIgnoreCase);
            int fin = ini < 0 ? -1 : t.IndexOf("</nav>", ini, StringComparison.OrdinalIgnoreCase);
            Assert.True(ini >= 0 && fin > ini, "Site.Master: no se encontró el <nav> del menú.");
            var casos = IconoFa.Matches(t.Substring(ini, fin - ini)).Cast<Match>()
                .Select(m => Donde(sm, t, ini + m.Index, m.Value)).ToList();
            Assert.True(casos.Count == 0, "El menú (marca, menús, opciones, usuario y Cerrar sesión) va sin iconos (DN-28):\n" + string.Join("\n", casos));
        }

        // ── Regla 5: pestañas sin iconos ─────────────────────────────────────────────────────

        /// <summary>Iconos dentro de una pestaña: role="tab", class nav-link o dc-tab.</summary>
        internal static List<(int Indice, string Texto)> IconosEnPestanas(string sinComentarios)
        {
            var r = new List<(int, string)>();
            foreach (var e in Elementos(sinComentarios))
            {
                bool pestana = string.Equals(e.Attr("role"), "tab", StringComparison.OrdinalIgnoreCase) || e.TieneClase("nav-link") || e.TieneClase("dc-tab");
                if (!pestana) continue;
                string interior = Interior(sinComentarios, e);
                if (interior == null) continue;
                foreach (Match m in IconoFa.Matches(interior)) r.Add((e.Fin + m.Index, m.Value));
            }
            return r.Distinct().ToList();
        }

        [Fact]
        public void PestanasSinIconos()
        {
            var casos = Marcado().SelectMany(a =>
            {
                string t = a.SinComentarios;
                return IconosEnPestanas(t).Select(i => Donde(a, t, i.Indice, i.Texto));
            }).ToList();
            foreach (WebSGV.Services.Clientes.TipoCatalogo tipo in Enum.GetValues(typeof(WebSGV.Services.Clientes.TipoCatalogo)))
                if (WebSGV.Services.Clientes.CatalogoClienteService.TextoPestana(tipo, 1).Contains("<i"))
                    casos.Add("CatalogoClienteService.TextoPestana(" + tipo + ") arma un <i>");
            Assert.True(casos.Count == 0, "Las pestañas van solo con su nombre (y el contador), sin icono (DN-28):\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_IconosEnPestanas()
        {
            Assert.Single(IconosEnPestanas("<a class=\"dc-tab active\" role=\"tab\" href=\"#a\"><i class=\"fas fa-truck\"></i><span>Mi viaje</span></a>"));
            Assert.Single(IconosEnPestanas("<a class=\"nav-link\" href=\"#b\"><i class=\"fas fa-chart-bar mr-1\"></i>Resumen</a>"));
            Assert.Single(IconosEnPestanas("<asp:LinkButton ID=\"l\" runat=\"server\" CssClass=\"nav-link\"><i class=\"fas fa-x\"></i>Uno</asp:LinkButton>"));
            Assert.Single(IconosEnPestanas("<button type=\"button\" role=\"tab\" aria-selected=\"true\"><span class=\"fas fa-x\"></span>Dos</button>"));
            Assert.Empty(IconosEnPestanas("<a class=\"nav-link\" href=\"#c\">Resumen <span class=\"badge\">3</span></a><i class=\"fas fa-x\"></i>"));
            Assert.Empty(IconosEnPestanas("<a class=\"navbar-link\" href=\"#\"><i class=\"fas fa-x\"></i>No es pestaña</a>"));
        }

        // ── Regla 6: botón con texto sin icono (salvo los funcionales) ───────────────────────

        /// <summary>Iconos no funcionales dentro de un botón o enlace (button, a, asp:LinkButton,
        /// asp:HyperLink) que ya tiene texto visible. Las tarjetas KPI son de la regla 9.</summary>
        internal static List<(int Indice, string Texto)> IconosEnBotonesConTexto(string sinComentarios)
        {
            var r = new List<(int, string)>();
            foreach (var e in Elementos(sinComentarios).Where(x => Pulsables.Contains(x.Nombre)))
            {
                string interior = Interior(sinComentarios, e);
                if (interior == null) continue;
                if (EsClaseKpi(e.Clase) || ClasesDe(interior).Any(EsClaseKpi)) continue;
                var iconos = IconoFa.Matches(interior).Cast<Match>().Where(m => !IconoFuncional.IsMatch(m.Groups["c"].Value)).ToList();
                if (iconos.Count == 0) continue;
                if (!e.Tiene("Text") && TextoVisible(interior).Length == 0) continue;   // solo icono: regla 7
                r.AddRange(iconos.Select(m => (e.Fin + m.Index, m.Value)));
            }
            return r.Distinct().ToList();
        }

        private const string NoFuncional = @"(?![^>]*\bfa-(?:spin|spinner|chevron-(?:down|up|left|right)|play|pause)\b)";
        private static readonly Regex HtmlConIconoYTexto = new Regex(
            @"(?:\.html\(|innerHTML\s*=)\s*['""]<i\b" + NoFuncional + @"[^>]*></i>\s*[\p{L}{]");
        private static readonly Regex BotonConIconoYTextoCodigo = new Regex(
            @"<(?:button|a)\b[^>\n]*>\s*<i\b" + NoFuncional + @"[^>\n]*></i>\s*[^<\s'""+\\]");

        internal static List<int> IconosEnBotonesConTextoCodigo(string sinComentarios) =>
            HtmlConIconoYTexto.Matches(sinComentarios).Cast<Match>()
                .Concat(BotonConIconoYTextoCodigo.Matches(sinComentarios).Cast<Match>())
                .Select(m => m.Index).Distinct().OrderBy(i => i).ToList();

        [Fact]
        public void BotonesConTextoSinIcono()
        {
            var casos = Archivos().SelectMany(a =>
            {
                string t = a.SinComentarios;
                return a.EsCodigo
                    ? IconosEnBotonesConTextoCodigo(t).Select(i => $"{a.Ruta}:{Linea(t, i)}")
                    : IconosEnBotonesConTexto(t).Select(i => Donde(a, t, i.Indice, i.Texto));
            }).ToList();
            Assert.True(casos.Count == 0,
                "Un botón o enlace que ya tiene texto va sin icono (DN-28); solo quedan los giratorios, el chevron de desplegar y play/pausa:\n"
                + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_IconosEnBotonesConTexto_Marcado()
        {
            Assert.Single(IconosEnBotonesConTexto("<button type=\"button\" class=\"btn\"><i class=\"fas fa-undo mr-1\"></i>Retirar envío</button>"));
            Assert.Single(IconosEnBotonesConTexto("<a href=\"x\" class=\"btn\"><i class=\"fas fa-home\"></i> Ir al inicio</a>"));
            Assert.Single(IconosEnBotonesConTexto("<asp:LinkButton ID=\"b\" runat=\"server\"><i class=\"fas fa-file-excel mr-1\"></i>Excel</asp:LinkButton>"));
            Assert.Single(IconosEnBotonesConTexto("<asp:HyperLink ID=\"h\" runat=\"server\" NavigateUrl=\"~/x\"><i class=\"fas fa-x\"></i><asp:Label ID=\"l\" runat=\"server\" /></asp:HyperLink>"));
            Assert.Single(IconosEnBotonesConTexto("<a href=\"x\"><i class=\"fas fa-x\"></i><%#: Eval(\"Nombre\") %></a>"));
            Assert.Single(IconosEnBotonesConTexto("<button class=\"btn\"><i class=\"fas fa-table\"></i> Ver tabla <i class=\"fas fa-chevron-down\"></i></button>"));
            // Funcionales, solo icono (regla 7), sr-only y tarjetas KPI (regla 9) no cuentan.
            Assert.Empty(IconosEnBotonesConTexto("<button class=\"btn\">Ver tabla <i class=\"fas fa-chevron-down chev\"></i></button>"));
            Assert.Empty(IconosEnBotonesConTexto("<button id=\"rutaPlay\"><i class=\"fas fa-play\"></i> <span>Reproducir</span></button>"));
            Assert.Empty(IconosEnBotonesConTexto("<button class=\"btn\"><i class=\"fas fa-spinner fa-spin\"></i> Guardando...</button>"));
            Assert.Empty(IconosEnBotonesConTexto("<button class=\"sgv-btn-icono\" aria-label=\"Editar\"><i class=\"fas fa-edit\"></i></button>"));
            Assert.Empty(IconosEnBotonesConTexto("<a class=\"sx-btn\" title=\"t\"><i class=\"fas fa-image\"></i><span class=\"sr-only\">Imagen</span></a>"));
            Assert.Empty(IconosEnBotonesConTexto("<asp:LinkButton ID=\"k\" runat=\"server\" CssClass=\"ld-stat-tile\"><span class=\"ld-stat-icon\"><i class=\"fas fa-route\"></i></span>Viajes</asp:LinkButton>"));
            Assert.Empty(IconosEnBotonesConTexto("<a href=\"x\" class=\"btn\">Volver</a><i class=\"fas fa-x\"></i>"));
            Assert.Empty(IconosEnBotonesConTexto("<button type=\"button\" class=\"close\" aria-label=\"Cerrar\"><span aria-hidden=\"true\">&times;</span></button>"));
        }

        [Fact]
        public void Detector_IconosEnBotonesConTexto_Codigo()
        {
            Assert.Single(IconosEnBotonesConTextoCodigo("$btn.prop('disabled', false).html('<i class=\"fas fa-undo mr-1\"></i>Retirar env\\u00edo');"));
            Assert.Single(IconosEnBotonesConTextoCodigo("btnFirmar.innerHTML = '<i class=\"fas fa-check-circle\"></i>Firmar y enviar';"));
            Assert.Single(IconosEnBotonesConTextoCodigo("'<button type=\"button\" class=\"btn\" data-id=\"' + id + '\"><i class=\"fas fa-eye\"></i> Ver</button>'"));
            Assert.Single(IconosEnBotonesConTextoCodigo("$\"<a href=\\\"{H(url)}\\\" title=\\\"Ver el PDF\\\"><i class=\\\"far fa-file-pdf mr-1\\\"></i>{H(f.Numero)}</a>\""));
            Assert.Single(IconosEnBotonesConTextoCodigo("sb.Append(\"<a class='btn' href='x'><i class='fas fa-truck mr-1'></i>Ver el lote</a>\");"));
            Assert.Empty(IconosEnBotonesConTextoCodigo("$btn.prop('disabled', true).html('<i class=\"fas fa-spinner fa-spin mr-1\"></i>Guardando...');"));
            Assert.Empty(IconosEnBotonesConTextoCodigo("btnFirmar.innerHTML = '<i class=\"fas fa-spinner\"></i>Registrando firma...';"));
            Assert.Empty(IconosEnBotonesConTextoCodigo("$btn.html('Verificar GPS');"));
            Assert.Empty(IconosEnBotonesConTextoCodigo("html += '<button type=\"button\" class=\"sgv-btn-icono\" aria-label=\"Ver\"><i class=\"fas fa-eye\"></i></button>';"));
            Assert.Empty(IconosEnBotonesConTextoCodigo("'<button class=\"btn btn-expand\" aria-label=\"Ver detalle\">' + '<i class=\"fas fa-chevron-down\"></i></button>'"));
        }

        // ── Regla 7: botón solo icono con aria-label ─────────────────────────────────────────

        /// <summary>Botones o enlaces cuyo único contenido visible es un icono y que no tienen aria-label,
        /// aria-labelledby ni un texto <c>sr-only</c> (el title no lo leen todos los lectores ni se ve en el celular).</summary>
        internal static List<(int Indice, string Texto)> BotonesSoloIconoSinAriaLabel(string sinComentarios)
        {
            var r = new List<(int, string)>();
            foreach (var e in Elementos(sinComentarios).Where(x => Pulsables.Contains(x.Nombre)))
            {
                if (e.Tiene("Text") || e.Tiene("aria-label") || e.Tiene("aria-labelledby")) continue;
                if (string.Equals(e.Attr("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase)) continue;
                string interior = Interior(sinComentarios, e);
                if (interior == null || !IconoFa.IsMatch(interior)) continue;
                if (TextoVisible(interior).Length > 0 || SrOnly.IsMatch(interior)) continue;
                r.Add((e.Indice, "<" + e.Nombre + (e.Tiene("id") ? " id=" + e.Id : "") + " class=\"" + e.Clase.Trim() + "\">"));
            }
            return r;
        }

        private static readonly Regex SoloIconoSinAriaLabelCodigo = new Regex(
            @"<(?<n>button|a)\b(?![^>]*\baria-label(?:ledby)?\s*=)[^>]*>\s*<i\b[^>]*></i>\s*</\k<n>\s*>", RegexOptions.IgnoreCase);

        internal static List<int> BotonesSoloIconoSinAriaLabelCodigo(string sinComentarios) =>
            SoloIconoSinAriaLabelCodigo.Matches(sinComentarios).Cast<Match>().Select(m => m.Index).ToList();

        [Fact]
        public void BotonesSoloIconoConAriaLabel()
        {
            var casos = Archivos().SelectMany(a =>
            {
                string t = a.SinComentarios;
                return a.EsCodigo
                    ? BotonesSoloIconoSinAriaLabelCodigo(t).Select(i => $"{a.Ruta}:{Linea(t, i)}")
                    : BotonesSoloIconoSinAriaLabel(t).Select(i => Donde(a, t, i.Indice, i.Texto));
            }).ToList();
            Assert.True(casos.Count == 0, "Un botón que solo tiene un icono necesita aria-label (deje también el title para el mouse):\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_BotonesSoloIconoSinAriaLabel()
        {
            Assert.Single(BotonesSoloIconoSinAriaLabel("<button type=\"button\" class=\"sgv-btn-icono\" title=\"Ver detalle\"><i class=\"fas fa-eye\"></i></button>"));
            Assert.Single(BotonesSoloIconoSinAriaLabel("<asp:HyperLink runat=\"server\" CssClass=\"sgv-btn-icono\" ToolTip=\"Ver adjunto\"><i class=\"fas fa-paperclip\"></i></asp:HyperLink>"));
            Assert.Single(BotonesSoloIconoSinAriaLabel("<a href=\"#\">\n  <i class=\"fas fa-x\"></i>\n</a>"));
            Assert.Empty(BotonesSoloIconoSinAriaLabel("<button class=\"sgv-btn-icono\" title=\"Ver\" aria-label=\"Ver\"><i class=\"fas fa-eye\"></i></button>"));
            Assert.Empty(BotonesSoloIconoSinAriaLabel("<a class=\"sx-btn\"><i class=\"fas fa-image\"></i><span class=\"sr-only\">STATUS como imagen</span></a>"));
            Assert.Empty(BotonesSoloIconoSinAriaLabel("<button aria-labelledby=\"t\"><i class=\"fas fa-x\"></i></button>"));
            Assert.Empty(BotonesSoloIconoSinAriaLabel("<button><i class=\"fas fa-x\"></i> Quitar</button>"));
            Assert.Empty(BotonesSoloIconoSinAriaLabel("<a href=\"#\" aria-hidden=\"true\" tabindex=\"-1\"><i class=\"fas fa-x\"></i></a>"));

            Assert.Single(BotonesSoloIconoSinAriaLabelCodigo("html += '<button type=\"button\" class=\"sgv-btn-icono\" title=\"Ver detalle\"><i class=\"fas fa-eye\"></i></button>';"));
            Assert.Single(BotonesSoloIconoSinAriaLabelCodigo("sb.Append(\"<a href='x' title='PDF'><i class='fas fa-file-pdf'></i></a>\");"));
            Assert.Empty(BotonesSoloIconoSinAriaLabelCodigo("html += '<button class=\"sgv-btn-icono\" title=\"Ver\" aria-label=\"Ver\"><i class=\"fas fa-eye\"></i></button>';"));
            Assert.Empty(BotonesSoloIconoSinAriaLabelCodigo("html += '<button class=\"btn\"><i class=\"fas fa-eye\"></i> Ver</button>';"));
        }

        // ── Regla 9: tarjetas KPI solo en la lista cerrada (E5-17 ya rehízo las suyas; quedan las de E5-18) ────────

        /// <summary>Páginas que todavía tienen tarjetas KPI (con sus iconos), y la tarea que las rehace.
        /// Lista cerrada: solo se achica (al rehacer una página, sáquela de aquí). E5-18c: vacía (DashboardExportacion
        /// pasó a la línea sgv-resumen); queda para que la prueba siga fallando si vuelve una tarjeta.</summary>
        internal static readonly Dictionary<string, string> PaginasConTarjetasKpi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
        };

        private static readonly Regex ClaseKpi = new Regex(
            @"^(?:[\w-]*stat-card|summary-card[\w-]*|[\w-]*stats-row|ld-stat[\w-]*|kpi-[\w-]+|[\w-]*kpis|[\w-]+-tarjetas)$");
        private static readonly Regex AtributoClase = new Regex(@"\b(?:class|CssClass)\s*=\s*[""'](?<c>[^""']*)[""']", RegexOptions.IgnoreCase);

        internal static bool EsClaseKpi(string clases) =>
            Regex.Split(clases ?? "", @"\s+").Any(c => c.Length > 0 && ClaseKpi.IsMatch(c));

        private static IEnumerable<string> ClasesDe(string marcado) =>
            AtributoClase.Matches(marcado).Cast<Match>().Select(m => m.Groups["c"].Value);

        internal static bool TieneTarjetasKpi(string marcado) => ClasesDe(marcado).Any(EsClaseKpi);

        [Fact]
        public void TarjetasKpiSoloEnLaListaCerrada()
        {
            var conKpi = Marcado().Where(a => TieneTarjetasKpi(a.SinComentarios)).Select(a => a.Ruta).ToList();
            var nuevas = conKpi.Where(r => !PaginasConTarjetasKpi.ContainsKey(r)).ToList();
            var yaNo = PaginasConTarjetasKpi.Keys.Where(k => !conKpi.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
            Assert.True(nuevas.Count == 0, "Tarjetas KPI nuevas (DN-28: no se agregan; E5-17/E5-18 rehacen las que hay):\n" + string.Join("\n", nuevas));
            Assert.True(yaNo.Count == 0, "Estas páginas ya no tienen tarjetas KPI: sáquelas de PaginasConTarjetasKpi (la lista solo se achica):\n" + string.Join("\n", yaNo));
        }

        [Fact]
        public void Detector_TarjetasKpi()
        {
            Assert.True(TieneTarjetasKpi("<div class=\"card aud-stat-card aud-stat-primary\">"));
            Assert.True(TieneTarjetasKpi("<div class=\"stat-card\"><div class=\"kpi-valor\">3</div></div>"));
            Assert.True(TieneTarjetasKpi("<div class=\"summary-card summary-card-blue\">"));
            Assert.True(TieneTarjetasKpi("<div class=\"ld-stats-row\">"));
            Assert.True(TieneTarjetasKpi("<asp:LinkButton runat=\"server\" CssClass=\"ld-stat-tile\" />"));
            Assert.True(TieneTarjetasKpi("<div class=\"de-kpis\" id=\"kpis\"></div>"));
            Assert.True(TieneTarjetasKpi("<div class=\"rc-tarjetas\">"));
            Assert.False(TieneTarjetasKpi("<div class=\"card status-pill statistic\">"));
            Assert.False(TieneTarjetasKpi("<div class=\"sgv-panel\" id=\"stat-card\">"));
            Assert.False(TieneTarjetasKpi("<div class=\"tarjetas-lista\">"));
        }

        // ── Regla 10: encabezados de tabla sin mayúsculas ────────────────────────────────────

        /// <summary>Siglas que pueden ir en mayúsculas en un encabezado. Lista cerrada.</summary>
        internal static readonly HashSet<string> Siglas = new HashSet<string>(StringComparer.Ordinal)
            { "DNI", "RUC", "CPIC", "MCI", "GRE", "PDF", "GPS", "SUNAT", "IGV", "ZIP", "USD", "SGV", "ETA" };

        /// <summary>¿Todo en mayúsculas, con al menos una palabra de 3 o más letras que no es sigla?</summary>
        internal static bool EnMayusculas(string texto)
        {
            string t = WebUtility.HtmlDecode(texto ?? "");
            if (t != t.ToUpper(CultureInfo.GetCultureInfo("es-PE"))) return false;
            return Regex.Matches(t, @"\p{L}{3,}").Cast<Match>().Any(m => !Siglas.Contains(m.Value));
        }

        private static readonly Regex HeaderText = new Regex(@"\bHeaderText\s*=\s*""(?<t>[^""<]*)""", RegexOptions.IgnoreCase);
        private static readonly Regex Th = new Regex(@"<th\b[^>]*>(?<t>[^<]*)</th\s*>", RegexOptions.IgnoreCase);
        private static readonly Regex ThEnMayusculasPorClase = new Regex(
            @"<th\b[^>]*\bclass\s*=\s*\\?[""'][^""']*\btext-uppercase\b|\bHeaderStyle-CssClass\s*=\s*""[^""]*\btext-uppercase\b", RegexOptions.IgnoreCase);

        internal static List<(int Indice, string Texto)> EncabezadosEnMayusculas(string sinComentarios)
        {
            var r = new List<(int, string)>();
            foreach (Match m in HeaderText.Matches(sinComentarios))
                if (EnMayusculas(m.Groups["t"].Value)) r.Add((m.Index, m.Groups["t"].Value));
            foreach (Match m in Th.Matches(sinComentarios))
                if (EnMayusculas(m.Groups["t"].Value)) r.Add((m.Index, m.Groups["t"].Value));
            foreach (Match m in ThEnMayusculasPorClase.Matches(sinComentarios))
                r.Add((m.Index, "text-uppercase"));
            return r.OrderBy(x => x.Item1).ToList();
        }

        private static readonly Regex SelectorDeTabla = new Regex(@"(?<![\w-])(?:th|thead)(?![\w-])|data-label");

        /// <summary>Reglas propias con <c>text-transform: uppercase</c> sobre encabezados de tabla
        /// (th, thead o el rótulo data-label de la tabla del celular).</summary>
        internal static List<string> MayusculasPorCssEnTablas(string hoja, string css) =>
            EstilosTests.Declaraciones(hoja, css)
                .Where(d => d.Propiedad.Equals("text-transform", StringComparison.OrdinalIgnoreCase)
                         && d.Valor.IndexOf("uppercase", StringComparison.OrdinalIgnoreCase) >= 0
                         && SelectorDeTabla.IsMatch(d.Selector))
                .Select(d => $"{hoja}:{d.Linea} {d.Selector}")
                .ToList();

        [Fact]
        public void EncabezadosDeTablaSinMayusculas()
        {
            var casos = Archivos().SelectMany(a =>
            {
                string t = a.SinComentarios;
                return EncabezadosEnMayusculas(t).Select(e => $"{a.Ruta}:{Linea(t, e.Indice)} \"{e.Texto}\"");
            }).ToList();
            string raiz = RaizWeb();
            string content = Path.Combine(raiz, "Content");
            foreach (var f in Directory.GetFiles(content, "*.css")
                         .Concat(Directory.GetFiles(Path.Combine(content, "paginas"), "*.css", SearchOption.AllDirectories))
                         .OrderBy(f => f, StringComparer.Ordinal))
            {
                string hoja = f.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                casos.AddRange(MayusculasPorCssEnTablas(hoja, File.ReadAllText(f, Encoding.UTF8)));
            }
            Assert.True(casos.Count == 0, "Encabezados de tabla en estilo oración, sin mayúsculas (salvo siglas como DNI o RUC) ni text-transform (DN-28):\n" + string.Join("\n", casos));
        }

        [Fact]
        public void Detector_EncabezadosEnMayusculas()
        {
            Assert.Single(EncabezadosEnMayusculas("<asp:BoundField DataField=\"x\" HeaderText=\"CONDUCTOR\" />"));
            Assert.Single(EncabezadosEnMayusculas("<asp:BoundField HeaderText=\"FECHA / HORA\" />"));
            Assert.Single(EncabezadosEnMayusculas("<asp:BoundField HeaderText=\"ACCI&#xD3;N\" />"));
            Assert.Single(EncabezadosEnMayusculas("sb.Append(\"<thead><tr><th>CAMBIO</th><th>DNI</th></tr></thead>\");"));
            Assert.Single(EncabezadosEnMayusculas("<th class=\"text-uppercase small\">Estado</th>"));
            Assert.Single(EncabezadosEnMayusculas("<asp:TemplateField HeaderText=\"Estado\" HeaderStyle-CssClass=\"text-uppercase\">"));
            Assert.Single(EncabezadosEnMayusculas("<th>N° DESP.</th>"));
            Assert.Empty(EncabezadosEnMayusculas("<asp:BoundField HeaderText=\"DNI\" /><asp:BoundField HeaderText=\"ID\" /><th>S/</th><th>US$</th><th>CPIC</th><th>P90</th><th>#</th>"));
            Assert.Empty(EncabezadosEnMayusculas("<asp:BoundField HeaderText=\"Fecha de programación\" /><th>Gu&#xED;a GRE</th><th class=\"num\">RUC del cliente</th>"));
            Assert.Empty(EncabezadosEnMayusculas("html += '<th>' + titulo + '</th>';"));

            Assert.Single(MayusculasPorCssEnTablas("x.css", ".bd-tabla th { font-size: .78rem; text-transform: uppercase; }"));
            Assert.Single(MayusculasPorCssEnTablas("x.css", "@media (max-width: 575.98px) {\n  .t td[data-label]::before { content: attr(data-label); text-transform: uppercase; }\n}"));
            Assert.Single(MayusculasPorCssEnTablas("x.css", ".t thead tr { text-transform:UPPERCASE }"));
            Assert.Empty(MayusculasPorCssEnTablas("x.css", ".bd-exp-doc-titulo { text-transform: uppercase; }\n.t tr:nth-child(2) td { text-transform: uppercase; }\n.t th { text-transform: none; }"));
            Assert.Empty(MayusculasPorCssEnTablas("x.css", "/* .t th { text-transform: uppercase; } */"));
        }

        // ── Regla 11: topes de iconos ────────────────────────────────────────────────────────

        /// <summary>Iconos del marcado al cerrar E5-24b: avisos, giratorios, funcionales, botones compactos
        /// de las tablas y lo que queda para E5-17/E5-18 (tarjetas KPI y sueltos de ListaDespachos,
        /// RegistroDespacho y FacturasFlete). E5-17a: 93 a 84 (fuera las tarjetas y los iconos de
        /// Auditoria y el de DashboardAdminSistema; queda la lupa del detalle de Auditoria, botón compacto con aria-label).
        /// E5-17b: 84 a 77 (fuera las cuatro tarjetas, el aviso y el fa-info-circle de ReportesOrdenesViaje y el
        /// giratorio fa-spinner de LiquidacionesAprobadasContabilidad; queda el ojo del detalle, botón compacto con aria-label).
        /// E5-17c: 77 a 76 (fuera la lupa decorativa de la búsqueda de FacturasFlete).
        /// E5-18a: 76 a 52 (ListaDespachos sin tarjetas, chips, títulos con icono ni fa-info-circle; quedan sus dos
        /// avisos de advertencia, el de Editar lote y el del lote que espera el correo).
        /// E5-18b: 52 a 30 (RegistroDespacho sin iconos en títulos, rótulos, avisos informativos ni chips; quedan sus
        /// tres avisos de advertencia y la flecha que pliega el lote a mano, que es funcional).
        /// E5-18c: 30 a 29 (el detalle por tramo de DashboardExportacion es un details.sgv-plegable: fuera su chevron;
        /// quedan las flechas de mes anterior y siguiente, botones compactos con aria-label).
        /// Igualdad exacta: al quitar iconos, baje la cifra.</summary>
        private const int TopeIconosMarcado = 29;
        /// <summary>Iconos armados en .cs/.js. E5-17c: 55 a 39 (fuera los 16 de BandejaDocumentos que eran estado o
        /// adorno, pasados a sellos sgv-estado con texto; quedan su aviso de observaciones y la flecha de plegar, que es
        /// funcional). E5-18a: 39 a 38 (InsigniaDocumento de ListaDespachos es un sello con texto). E5-18b: 38 a 25
        /// (RegistroDespacho: fuera los iconos de los resúmenes de la programación, de la fila extra y de la sesión
        /// recuperada; quedan el aviso de nombre truncado y RegistroDespachoReglas.AvisosHtml, el aviso ámbar común de
        /// las tres vistas previas). Lo que queda son avisos y botones compactos con aria-label.</summary>
        private const int TopeIconosCodigo = 25;

        private static readonly Regex IconoDinamico = new Regex(@"<i\s+class\s*=\s*[""']<%", RegexOptions.IgnoreCase);
        private static readonly Regex IconoEnCodigo = new Regex(@"<i class=[\\""']+fa[srlbd]? ");

        internal static int IconosDelMarcado(string sinComentarios) =>
            IconoFa.Matches(sinComentarios).Count + IconoDinamico.Matches(sinComentarios).Count;

        internal static int IconosDelCodigo(string sinComentarios) => IconoEnCodigo.Matches(sinComentarios).Count;

        private static string Reparto(IEnumerable<(string Ruta, int N)> porArchivo)
        {
            var lista = porArchivo.Where(x => x.N > 0).ToList();
            string Grupo(string tarea) => string.Join(", ", lista
                .Where(x => PaginasConTarjetasKpi.TryGetValue(Regex.Replace(x.Ruta, @"\.(cs|js)$", ""), out var t) && t.StartsWith(tarea))
                .Select(x => $"{x.Ruta} {x.N}"));
            string otros = string.Join(", ", lista
                .Where(x => !PaginasConTarjetasKpi.ContainsKey(Regex.Replace(x.Ruta, @"\.(cs|js)$", "")))
                .Select(x => $"{x.Ruta} {x.N}"));
            return $"\n  E5-17: {Grupo("E5-17")}\n  E5-18: {Grupo("E5-18")}\n  Resto: {otros}";
        }

        [Fact]
        public void TopeIconosDelMarcado()
        {
            var porArchivo = Marcado().Select(a => (a.Ruta, IconosDelMarcado(a.SinComentarios))).ToList();
            int total = porArchivo.Sum(x => x.Item2);
            Assert.True(total <= TopeIconosMarcado,
                $"Subieron los iconos del marcado: {total} (tope {TopeIconosMarcado}). DN-28: solo botones compactos con aria-label, avisos y funcionales." + Reparto(porArchivo));
            Assert.True(total == TopeIconosMarcado,
                $"Bajaron los iconos del marcado: {total}. Baje TopeIconosMarcado de {TopeIconosMarcado} a {total}." + Reparto(porArchivo));
        }

        [Fact]
        public void TopeIconosDelCodigo()
        {
            var porArchivo = Codigo().Select(a => (a.Ruta, IconosDelCodigo(a.SinComentarios))).ToList();
            int total = porArchivo.Sum(x => x.Item2);
            Assert.True(total <= TopeIconosCodigo,
                $"Subieron los iconos armados en .cs/.js: {total} (tope {TopeIconosCodigo}). DN-28: solo botones compactos con aria-label, avisos y funcionales." + Reparto(porArchivo));
            Assert.True(total == TopeIconosCodigo,
                $"Bajaron los iconos armados en .cs/.js: {total}. Baje TopeIconosCodigo de {TopeIconosCodigo} a {total}." + Reparto(porArchivo));
        }

        [Fact]
        public void Detector_ContadoresDeIconos()
        {
            Assert.Equal(3, IconosDelMarcado("<i class=\"fas fa-x\"></i><span class='far fa-y mr-1'></span><i class=\"fa-z fab\"></i><i class=\"fancy\"></i><span class=\"fa-x\"></span>"));
            Assert.Equal(1, IconosDelMarcado("<span class='aud-badge'><i class='<%#: Clase(Eval(\"Accion\")) %>'></i></span>"));
            Assert.Equal(3, IconosDelCodigo("a = \"<i class=\\\"fas fa-x\\\"></i>\"; b = \"<i class='fas {icono} mr-1'></i>\"; c = '<i class=\"far fa-y\"></i>';"));
            Assert.Equal(0, IconosDelCodigo("x = '<i>cursiva</i>'; y = \"<i class=\\\"fancy\\\">\";"));
        }
    }
}
