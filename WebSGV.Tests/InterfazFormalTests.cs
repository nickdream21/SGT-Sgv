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
    /// Interfaz formal (E5-24, DN-28). Por ahora solo las reglas de E5-24a: 0 emojis y símbolos
    /// decorativos, redacción sin exclamaciones ni "exitosamente" y botones en estilo oración. Los
    /// iconos Font Awesome y las mayúsculas de las tablas son de E5-24b. Revisa los .aspx, .ascx,
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
    }
}
