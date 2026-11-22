using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Accesibilidad y celular en el marcado (Etapa 5: E5-4 y las reglas de E5-5). Leen las páginas
    /// como texto, sin los comentarios <c>&lt;%-- --%&gt;</c> ni <c>&lt;!-- --&gt;</c>, para que un
    /// ejemplo comentado no cuente. Cada regla nueva de E5-5 es otro <c>[Fact]</c> sobre
    /// <see cref="Paginas"/>.
    /// </summary>
    public class AccesibilidadMarcadoTests
    {
        // ── Lector de archivos ───────────────────────────────────────────────────────────────

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Relativo(string raiz, string archivo) =>
            archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');

        private static readonly Regex Comentarios = new Regex(@"<%--.*?--%>|<!--.*?-->", RegexOptions.Singleline);

        /// <summary>Quita los comentarios de servidor y de HTML, dejando los saltos de línea (los números de línea siguen valiendo).</summary>
        internal static string SinComentarios(string texto) =>
            Comentarios.Replace(texto, m => new string('\n', m.Value.Count(c => c == '\n')));

        /// <summary>Página leída como texto sin comentarios.</summary>
        internal sealed class Pagina
        {
            public string Ruta { get; set; }   // relativa a WebSGV/, con '/'
            public string Texto { get; set; }

            public int Linea(int indice) => Texto.Take(indice).Count(c => c == '\n') + 1;
        }

        /// <summary>Todo .aspx, .ascx y .Master de WebSGV (sin obj/ ni bin/), sin comentarios.</summary>
        internal static List<Pagina> Paginas()
        {
            string raiz = RaizWeb();
            return new[] { "*.aspx", "*.ascx", "*.Master", "*.html", "*.htm" }
                .SelectMany(ext => Directory.GetFiles(raiz, ext, SearchOption.AllDirectories))
                .Where(f => !Relativo(raiz, f).StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                         && !Relativo(raiz, f).StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new Pagina { Ruta = Relativo(raiz, f), Texto = SinComentarios(File.ReadAllText(f)) })
                .ToList();
        }

        internal static Pagina PaginaDe(string ruta) =>
            Paginas().Single(p => string.Equals(p.Ruta, ruta, StringComparison.OrdinalIgnoreCase));

        private static readonly Regex EtiquetaHtml = new Regex(@"<html\b[^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex MetaViewport = new Regex(@"<meta\b[^>]*\bname\s*=\s*[""']viewport[""'][^>]*>", RegexOptions.IgnoreCase);
        private static readonly Regex Contenido = new Regex(@"\bcontent\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)')", RegexOptions.IgnoreCase);
        private static readonly Regex Canvas = new Regex(@"<canvas\b[^>]*>", RegexOptions.IgnoreCase);

        // ── Regla 4 (E5-4): idioma, zoom, estructura y firma ────────────────────────────────

        [Fact]
        public void Lector_QuitaComentariosDeServidorYDeHtml()
        {
            string texto = "a<%-- <html> --%>b<!-- <canvas>\n -->c";
            Assert.Equal("ab\nc", SinComentarios(texto));
        }

        [Fact]
        public void TodaEtiquetaHtml_TieneLangEs()
        {
            var paginas = Paginas();
            var conHtml = paginas.Where(p => EtiquetaHtml.IsMatch(p.Texto)).ToList();
            Assert.Contains(conHtml, p => p.Ruta == "Site.Master");
            Assert.Contains(conHtml, p => p.Ruta == "Views/Error.aspx");

            var malas = conHtml
                .SelectMany(p => EtiquetaHtml.Matches(p.Texto).Cast<Match>()
                    .Where(m => !Regex.IsMatch(m.Value, @"\blang\s*=\s*[""']es[""']", RegexOptions.IgnoreCase))
                    .Select(m => p.Ruta + ":" + p.Linea(m.Index) + " " + m.Value))
                .ToList();
            Assert.True(malas.Count == 0, "Sin lang=\"es\" (el lector de pantalla lee en otro idioma):\n" + string.Join("\n", malas));
        }

        [Fact]
        public void TodaPaginaConHtml_TieneViewportQueAdmiteZoom()
        {
            var malas = new List<string>();
            foreach (var p in Paginas().Where(x => EtiquetaHtml.IsMatch(x.Texto)))
            {
                var metas = MetaViewport.Matches(p.Texto).Cast<Match>().ToList();
                if (metas.Count == 0) { malas.Add(p.Ruta + ": sin <meta name=\"viewport\">"); continue; }
                foreach (Match m in metas)
                {
                    string contenido = Contenido.Match(m.Value).Groups["v"].Value.ToLowerInvariant().Replace(" ", "");
                    if (!contenido.Contains("width=device-width")
                        || contenido.Contains("user-scalable=no") || contenido.Contains("user-scalable=0")
                        || contenido.Contains("maximum-scale"))
                        malas.Add(p.Ruta + ":" + p.Linea(m.Index) + " " + m.Value);
                }
            }
            Assert.True(malas.Count == 0,
                "El viewport debe ser width=device-width, initial-scale=1, sin user-scalable=no ni maximum-scale (quien ve poco no puede ampliar):\n"
                + string.Join("\n", malas));
        }

        [Fact]
        public void SiteMaster_TieneUnSoloMainContenidoYEnlaceParaSaltarlo()
        {
            string t = PaginaDe("Site.Master").Texto;

            var mains = Regex.Matches(t, @"<main\b[^>]*>", RegexOptions.IgnoreCase).Cast<Match>().ToList();
            Assert.Single(mains);
            Assert.Matches(@"\bid\s*=\s*""contenido""", mains[0].Value);
            Assert.Matches(@"\btabindex\s*=\s*""-1""", mains[0].Value);

            // El contenido de cada página va dentro de <main>.
            int abre = mains[0].Index, cierra = t.IndexOf("</main>", abre, StringComparison.OrdinalIgnoreCase);
            int marcador = t.IndexOf("ContentPlaceHolderID=\"MainContent\"", StringComparison.OrdinalIgnoreCase);
            if (marcador < 0) marcador = t.IndexOf("ID=\"MainContent\"", StringComparison.OrdinalIgnoreCase);
            Assert.True(cierra > abre && marcador > abre && marcador < cierra, "MainContent debe quedar dentro de <main id=\"contenido\">.");

            // "Saltar al contenido" es el primer elemento enfocable del <body>.
            int body = t.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
            Assert.True(body >= 0);
            var primero = Regex.Match(t.Substring(body + 5), @"<(a|button|input|select|textarea|asp:\w+)\b[^>]*>", RegexOptions.IgnoreCase);
            Assert.True(primero.Success);
            Assert.Matches(@"^<a\b[^>]*\bhref\s*=\s*""#contenido""", primero.Value);

            // El submit deshabilitado sigue siendo el primer elemento del <form> (CLAUDE.md).
            var form = Regex.Match(t, @"<form\b[^>]*>", RegexOptions.IgnoreCase);
            var primeroDelForm = Regex.Match(t.Substring(form.Index + form.Length), @"<[a-z][\w:]*\b[^>]*>", RegexOptions.IgnoreCase);
            Assert.Matches(@"^<button\b", primeroDelForm.Value);
            Assert.Matches(@"\btype\s*=\s*[""']submit[""']", primeroDelForm.Value);
            Assert.Matches(@"\bdisabled\b", primeroDelForm.Value);
        }

        [Fact]
        public void PaginasConMaster_NoTienenOtroMain()
        {
            // Site.Master ya pone <main id="contenido">: otro <main> dentro de MainContent quedaría anidado.
            var malas = Paginas()
                .Where(p => Regex.IsMatch(p.Texto, @"\bMasterPageFile\s*=", RegexOptions.IgnoreCase))
                .SelectMany(p => Regex.Matches(p.Texto, @"<main\b", RegexOptions.IgnoreCase).Cast<Match>().Select(m => p.Ruta + ":" + p.Linea(m.Index)))
                .ToList();
            Assert.True(malas.Count == 0, "Página con master y su propio <main>: " + string.Join(", ", malas));
        }

        [Fact]
        public void BotonDelMenu_TieneEtiquetaEnEspanol()
        {
            string t = PaginaDe("Site.Master").Texto;
            var boton = Regex.Match(t, @"<button\b[^>]*\bnavbar-toggler\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            Assert.True(boton.Success);
            Assert.Matches(@"\baria-label\s*=\s*""Abrir menú""", boton.Value);
            Assert.Matches(@"\baria-expanded\s*=", boton.Value);
            Assert.Matches(@"\baria-controls\s*=\s*""navbarNav""", boton.Value);
        }

        [Fact]
        public void CanvasDeFirma_TienenRoleImgYAriaLabel()
        {
            var canvas = Paginas()
                .SelectMany(p => Canvas.Matches(p.Texto).Cast<Match>().Select(m => new { p.Ruta, Linea = p.Linea(m.Index), m.Value }))
                .ToList();
            Assert.Contains(canvas, c => c.Ruta == "Views/FirmarLiquidacion.aspx");
            Assert.Contains(canvas, c => c.Ruta == "Views/ConformarLiquidacion.aspx");

            var malos = canvas
                .Where(c => !Regex.IsMatch(c.Value, @"\brole\s*=\s*""img""", RegexOptions.IgnoreCase)
                         || !Regex.IsMatch(c.Value, @"\baria-label\s*=\s*""[^""]{10,}""", RegexOptions.IgnoreCase))
                .Select(c => c.Ruta + ":" + c.Linea + " " + c.Value)
                .ToList();
            Assert.True(malos.Count == 0, "Canvas sin role=\"img\" o sin aria-label:\n" + string.Join("\n", malos));
        }

        // ── Reglas de E5-5: etiquetas, ✕, modales, pestañas y <h1> ──────────────────────────
        //
        // Cada regla tiene un tope igual a lo que quedaba al cerrar E5-5. El tope solo baja: la prueba
        // falla si la cifra sube (algo nuevo sin etiqueta) y también si baja sin bajar el tope (así
        // queda ajustado y nadie gasta el margen sin darse cuenta). E5-6 los lleva a 0 (salvo <h1>,
        // E5-15).

        private const int TopeLabelSinForNiControl = 14;
        private const int TopeForConIdDeServidor = 0;
        private const int TopeCampoSinEtiqueta = 65;
        private const int TopeCerrarSinAriaLabel = 14;
        private const int TopeModalSinRoleNiTitulo = 17;
        private const int TopePestanaSinAriaSelected = 8;
        private const int TopePaginaSinUnSoloH1 = 34;

        /// <summary>Etiqueta de apertura: nombre, atributos y posición. Admite <c>&lt;% %&gt;</c> dentro y fuera de comillas.</summary>
        internal sealed class Elemento
        {
            public string Nombre { get; set; }      // en minúsculas: "label", "asp:textbox"
            public int Indice { get; set; }
            public int Fin { get; set; }            // índice tras el '>'
            public bool Cerrado { get; set; }       // termina en "/>"
            public Dictionary<string, string> Atributos { get; set; }

            public string Attr(string nombre) => Atributos.TryGetValue(nombre, out var v) ? v : null;
            public bool Tiene(string nombre) => !string.IsNullOrWhiteSpace(Attr(nombre));
            public bool EsServidor => Nombre.StartsWith("asp:") || string.Equals(Attr("runat"), "server", StringComparison.OrdinalIgnoreCase);
            public bool EsEstatico => string.Equals(Attr("ClientIDMode"), "Static", StringComparison.OrdinalIgnoreCase);
            public string Id => Attr("id");
            public string Clase => (Attr("class") ?? "") + " " + (Attr("CssClass") ?? "");
            public bool TieneClase(string c) => Regex.IsMatch(Clase, @"(^|\s)" + Regex.Escape(c) + @"(\s|$)");
        }

        private const string Bloque = @"<%.*?%>";
        private static readonly Regex EtiquetaApertura = new Regex(
            @"<(?<n>[a-zA-Z][\w:\.-]*)(?<a>(?:" + Bloque + @"|""(?:" + Bloque + @"|[^""])*""|'(?:" + Bloque + @"|[^'])*'|[^>""'])*)>",
            RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex Atributo = new Regex(
            @"(?<k>[a-zA-Z_:][\w:\.-]*)\s*=\s*(?:""(?<v>(?:" + Bloque + @"|[^""])*)""|'(?<v>(?:" + Bloque + @"|[^'])*)'|(?<v>[^\s>""']+))",
            RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex ReferenciaClientId = new Regex(@"^\s*<%[=:#]\s*(?<id>\w+)\.ClientID\s*%>\s*$");

        internal static List<Elemento> Elementos(string texto)
        {
            var lista = new List<Elemento>();
            foreach (Match m in EtiquetaApertura.Matches(texto))
            {
                // Los atributos se leen sin los bloques <% %> que van fuera de comillas (p. ej. <%# Eval() %> sueltos).
                string a = m.Groups["a"].Value;
                var atributos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match at in Atributo.Matches(Regex.Replace(a, @"(?<=\s)" + Bloque, " ", RegexOptions.Singleline)))
                    if (!atributos.ContainsKey(at.Groups["k"].Value)) atributos[at.Groups["k"].Value] = at.Groups["v"].Value;
                lista.Add(new Elemento
                {
                    Nombre = m.Groups["n"].Value.ToLowerInvariant(),
                    Indice = m.Index,
                    Fin = m.Index + m.Length,
                    Cerrado = a.TrimEnd().EndsWith("/"),
                    Atributos = atributos,
                });
            }
            return lista;
        }

        private static readonly HashSet<string> TiposSinEtiqueta = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "hidden", "submit", "button", "reset", "image" };

        private static readonly HashSet<string> CamposDeServidor = new HashSet<string>
            { "asp:textbox", "asp:dropdownlist", "asp:listbox", "asp:checkbox", "asp:radiobutton",
              "asp:checkboxlist", "asp:radiobuttonlist", "asp:fileupload" };

        /// <summary>¿Es un campo que el usuario llena y que necesita nombre accesible?</summary>
        internal static bool EsCampo(Elemento e)
        {
            // aria-hidden="true": el lector no lo ve (p. ej. el usuario oculto para el gestor de contraseñas de Site.Master).
            if (string.Equals(e.Attr("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase)) return false;
            if (CamposDeServidor.Contains(e.Nombre)) return true;
            if (e.Nombre == "select" || e.Nombre == "textarea") return true;
            if (e.Nombre == "input") return !TiposSinEtiqueta.Contains((e.Attr("type") ?? "text").Trim());
            return false;
        }

        /// <summary>Análisis de las etiquetas de una página (reglas 1 a 3).</summary>
        internal sealed class AnalisisEtiquetas
        {
            public List<string> LabelSinForNiControl = new List<string>();
            public List<string> ForConIdDeServidor = new List<string>();
            public List<string> CampoSinEtiqueta = new List<string>();
        }

        internal static AnalisisEtiquetas AnalizarEtiquetas(Pagina p)
        {
            var r = new AnalisisEtiquetas();
            var elementos = Elementos(p.Texto);
            var campos = elementos.Where(EsCampo).ToList();

            // IDs de servidor cuyo id en el cliente NO es el ID a secas (no tienen ClientIDMode="Static").
            var idsDeServidor = new HashSet<string>(elementos.Where(e => e.EsServidor && !e.EsEstatico && e.Tiene("id")).Select(e => e.Id), StringComparer.Ordinal);
            // IDs que el navegador verá tal cual: HTML sin runat o de servidor con ClientIDMode="Static".
            var idsLiterales = new HashSet<string>(elementos.Where(e => e.Tiene("id") && (!e.EsServidor || e.EsEstatico)).Select(e => e.Id), StringComparer.Ordinal);

            var asociadosServidor = new HashSet<string>(StringComparer.Ordinal);   // ID de servidor con etiqueta
            var asociadosLiteral = new HashSet<string>(StringComparer.Ordinal);    // id de cliente con etiqueta
            var rangosLabel = new List<Tuple<int, int>>();

            foreach (var e in elementos)
            {
                if (e.Nombre == "asp:label" && e.Tiene("AssociatedControlID"))
                    asociadosServidor.Add(e.Attr("AssociatedControlID"));
                if (e.Nombre != "label") continue;

                int cierre = p.Texto.IndexOf("</label", e.Fin, StringComparison.OrdinalIgnoreCase);
                if (cierre < 0) cierre = e.Fin;
                rangosLabel.Add(Tuple.Create(e.Indice, cierre));
                string donde = p.Ruta + ":" + p.Linea(e.Indice);

                string paraQue = e.Attr("for");
                if (string.IsNullOrWhiteSpace(paraQue))
                {
                    if (!campos.Any(c => c.Indice > e.Indice && c.Indice < cierre))
                        r.LabelSinForNiControl.Add(donde + " " + Recortar(p.Texto, e.Indice, cierre));
                    continue;
                }
                var refId = ReferenciaClientId.Match(paraQue);
                if (refId.Success) { asociadosServidor.Add(refId.Groups["id"].Value); continue; }
                if (idsDeServidor.Contains(paraQue) && !idsLiterales.Contains(paraQue))
                    r.ForConIdDeServidor.Add(donde + " for=\"" + paraQue + "\" (use for=\"<%= " + paraQue + ".ClientID %>\")");
                else
                    asociadosLiteral.Add(paraQue);
            }

            foreach (var c in campos)
            {
                bool tieneNombre =
                    c.Tiene("aria-label") || c.Tiene("aria-labelledby") || c.Tiene("title") || c.Tiene("ToolTip")
                    // asp:CheckBox / asp:RadioButton con Text generan su propio <label for>; las listas, uno por opción.
                    || ((c.Nombre == "asp:checkbox" || c.Nombre == "asp:radiobutton") && c.Tiene("Text"))
                    || c.Nombre == "asp:checkboxlist" || c.Nombre == "asp:radiobuttonlist"
                    || rangosLabel.Any(rg => c.Indice > rg.Item1 && c.Indice < rg.Item2)
                    || (c.Tiene("id") && (c.EsServidor && !c.EsEstatico ? asociadosServidor.Contains(c.Id) : asociadosLiteral.Contains(c.Id)))
                    || (c.Tiene("id") && c.EsServidor && c.EsEstatico && asociadosServidor.Contains(c.Id));
                if (!tieneNombre)
                    r.CampoSinEtiqueta.Add(p.Ruta + ":" + p.Linea(c.Indice) + " <" + c.Nombre + (c.Tiene("id") ? " id=" + c.Id : "") + ">");
            }
            return r;
        }

        private static string Recortar(string texto, int desde, int hasta)
        {
            string s = Regex.Replace(texto.Substring(desde, Math.Min(Math.Max(hasta - desde, 0) + 8, Math.Min(160, texto.Length - desde))), @"\s+", " ");
            return s.Length > 120 ? s.Substring(0, 120) + "…" : s;
        }

        private static readonly Regex SoloCruz = new Regex(@"^(×|&times;|✕|✖|x|X|&#215;|&#x2715;)?$");

        /// <summary>Regla 4: botones de cerrar (class="close", o data-dismiss sin más texto que la ✕) sin aria-label.</summary>
        internal static List<string> CerrarSinAriaLabel(Pagina p)
        {
            var malas = new List<string>();
            foreach (var e in Elementos(p.Texto))
            {
                bool esCruz = e.TieneClase("close");
                if (!esCruz && e.Tiene("data-dismiss"))
                {
                    string interior = e.Attr("Text");
                    if (interior == null && !e.Cerrado)
                    {
                        int cierre = p.Texto.IndexOf("</" + e.Nombre, e.Fin, StringComparison.OrdinalIgnoreCase);
                        interior = cierre < 0 ? "" : p.Texto.Substring(e.Fin, cierre - e.Fin);
                    }
                    interior = Regex.Replace(interior ?? "", @"<[^>]*>|\s+", "");
                    esCruz = SoloCruz.IsMatch(interior);
                }
                if (esCruz && !e.Tiene("aria-label") && !e.Tiene("aria-labelledby"))
                    malas.Add(p.Ruta + ":" + p.Linea(e.Indice) + " <" + e.Nombre + " class=\"" + e.Clase.Trim() + "\">");
            }
            return malas;
        }

        /// <summary>Regla 5: todo class="modal" con role="dialog" y aria-labelledby a un id que existe en la página.</summary>
        internal static List<string> ModalSinRoleNiTitulo(Pagina p)
        {
            var elementos = Elementos(p.Texto);
            var idsLiterales = new HashSet<string>(elementos.Where(e => e.Tiene("id") && (!e.EsServidor || e.EsEstatico)).Select(e => e.Id), StringComparer.Ordinal);
            var idsServidor = new HashSet<string>(elementos.Where(e => e.Tiene("id") && e.EsServidor).Select(e => e.Id), StringComparer.Ordinal);
            var malas = new List<string>();
            foreach (var e in elementos.Where(x => x.TieneClase("modal")))
            {
                var faltas = new List<string>();
                string rol = e.Attr("role") ?? "";
                if (rol != "dialog" && rol != "alertdialog") faltas.Add("sin role=\"dialog\"");
                string titulo = e.Attr("aria-labelledby");
                if (string.IsNullOrWhiteSpace(titulo)) faltas.Add("sin aria-labelledby");
                else
                {
                    var refId = ReferenciaClientId.Match(titulo);
                    if (refId.Success ? !idsServidor.Contains(refId.Groups["id"].Value) : !idsLiterales.Contains(titulo))
                        faltas.Add("aria-labelledby=\"" + titulo + "\" no existe");
                }
                if (faltas.Count > 0)
                    malas.Add(p.Ruta + ":" + p.Linea(e.Indice) + " " + (e.Tiene("id") ? "#" + e.Id + " " : "") + string.Join(", ", faltas));
            }
            return malas;
        }

        /// <summary>Regla 6: todo role="tab" con aria-selected.</summary>
        internal static List<string> PestanaSinAriaSelected(Pagina p) =>
            Elementos(p.Texto)
                .Where(e => string.Equals(e.Attr("role"), "tab", StringComparison.OrdinalIgnoreCase) && !e.Tiene("aria-selected"))
                .Select(e => p.Ruta + ":" + p.Linea(e.Indice) + " <" + e.Nombre + (e.Tiene("id") ? " id=" + e.Id : "") + " role=\"tab\">")
                .ToList();

        /// <summary>
        /// Regla 7: un solo &lt;h1&gt; en cada .aspx que muestra algo (con master o con &lt;body&gt;). Site.Master no
        /// pone &lt;h1&gt;: lo pone cada página.
        /// </summary>
        internal static List<string> PaginaSinUnSoloH1(Pagina p)
        {
            if (!p.Ruta.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)) return new List<string>();
            bool muestraAlgo = Regex.IsMatch(p.Texto, @"<asp:Content\b|<body\b", RegexOptions.IgnoreCase);
            if (!muestraAlgo) return new List<string>();
            var h1 = Regex.Matches(p.Texto, @"<h1\b", RegexOptions.IgnoreCase).Cast<Match>().ToList();
            if (h1.Count == 1) return new List<string>();
            return new List<string> { p.Ruta + ": " + h1.Count + " <h1>" + (h1.Count > 1 ? " (líneas " + string.Join(", ", h1.Select(m => p.Linea(m.Index))) + ")" : "") };
        }

        private static void CumpleTope(string regla, int tope, List<string> casos)
        {
            string lista = string.Join("\n", casos);
            Assert.True(casos.Count <= tope,
                regla + ": hay " + casos.Count + " y el tope es " + tope + ". Lo nuevo debe cumplir la regla:\n" + lista);
            Assert.True(casos.Count == tope,
                regla + ": bajó de " + tope + " a " + casos.Count + ". Baje el tope a " + casos.Count
                + " en AccesibilidadMarcadoTests (el tope solo baja).\n" + lista);
        }

        [Fact]
        public void Regla1_TodoLabel_TieneForOEnvuelveSuCampo()
        {
            var casos = Paginas().SelectMany(p => AnalizarEtiquetas(p).LabelSinForNiControl).ToList();
            CumpleTope("<label> sin for y sin campo adentro (el lector no sabe a qué campo nombra)", TopeLabelSinForNiControl, casos);
        }

        [Fact]
        public void Regla2_NingunFor_UsaElIdDeServidorASecas()
        {
            var casos = Paginas().SelectMany(p => AnalizarEtiquetas(p).ForConIdDeServidor).ToList();
            CumpleTope("for=\"X\" con el ID de servidor (en el navegador el id es MainContent_X)", TopeForConIdDeServidor, casos);
        }

        [Fact]
        public void Regla3_TodoCampo_TieneEtiquetaONombreAccesible()
        {
            var casos = Paginas().SelectMany(p => AnalizarEtiquetas(p).CampoSinEtiqueta).ToList();
            CumpleTope("Campo sin <label for>, aria-label, aria-labelledby, title ni ToolTip", TopeCampoSinEtiqueta, casos);
        }

        [Fact]
        public void Regla4_TodaCruzDeCerrar_TieneAriaLabel()
        {
            var casos = Paginas().SelectMany(CerrarSinAriaLabel).ToList();
            CumpleTope("Botón ✕ de cerrar sin aria-label", TopeCerrarSinAriaLabel, casos);
        }

        [Fact]
        public void Regla5_TodoModal_TieneRoleDialogYTituloQueExiste()
        {
            var casos = Paginas().SelectMany(ModalSinRoleNiTitulo).ToList();
            CumpleTope("Modal sin role=\"dialog\" o sin aria-labelledby válido", TopeModalSinRoleNiTitulo, casos);
        }

        [Fact]
        public void Regla6_TodaPestana_TieneAriaSelected()
        {
            var casos = Paginas().SelectMany(PestanaSinAriaSelected).ToList();
            CumpleTope("role=\"tab\" sin aria-selected", TopePestanaSinAriaSelected, casos);
        }

        [Fact]
        public void Regla7_CadaPagina_TieneUnSoloH1()
        {
            var casos = Paginas().SelectMany(PaginaSinUnSoloH1).ToList();
            CumpleTope("Página sin un <h1> o con más de uno", TopePaginaSinUnSoloH1, casos);
        }

        // ── El analizador mismo ──────────────────────────────────────────────────────────────

        private static Pagina De(string texto) => new Pagina { Ruta = "X.aspx", Texto = texto };

        [Fact]
        public void Analizador_ForConIdDeServidor_SeDetectaConArchivoYLinea()
        {
            var r = AnalizarEtiquetas(De("<div>\n<label for=\"txtA\">A</label>\n<asp:TextBox ID=\"txtA\" runat=\"server\" />\n</div>"));
            Assert.Equal(new[] { "X.aspx:2 for=\"txtA\" (use for=\"<%= txtA.ClientID %>\")" }, r.ForConIdDeServidor);
            Assert.Single(r.CampoSinEtiqueta);   // el for roto no cuenta como etiqueta
            Assert.StartsWith("X.aspx:3 ", r.CampoSinEtiqueta[0]);
        }

        [Fact]
        public void Analizador_ClientIdAssociatedControlYStatic_CuentanComoEtiqueta()
        {
            var r = AnalizarEtiquetas(De(
                "<label for=\"<%= txtA.ClientID %>\">A</label><asp:TextBox ID=\"txtA\" runat=\"server\" />" +
                "<asp:Label runat=\"server\" AssociatedControlID=\"ddlB\" Text=\"B\" /><asp:DropDownList ID=\"ddlB\" runat=\"server\" />" +
                "<label for=\"txtC\">C</label><asp:TextBox ID=\"txtC\" runat=\"server\" ClientIDMode=\"Static\" />" +
                "<label for=\"d\">D</label><input id=\"d\" type=\"text\" />" +
                "<label>E <input type=\"checkbox\" /></label>" +
                "<asp:CheckBox ID=\"chkF\" runat=\"server\" Text=\"F\" />" +
                "<input type=\"search\" aria-label=\"Buscar\" /><asp:TextBox ID=\"txtG\" runat=\"server\" ToolTip=\"G\" />" +
                "<input type=\"hidden\" id=\"h\" /><input type=\"submit\" value=\"Ir\" />" +
                "<input type=\"text\" autocomplete=\"username\" hidden aria-hidden=\"true\" />"));
            Assert.Empty(r.ForConIdDeServidor);
            Assert.Empty(r.LabelSinForNiControl);
            Assert.Empty(r.CampoSinEtiqueta);
        }

        [Fact]
        public void Analizador_LabelSueltoYCampoSinNada_SeDetectan()
        {
            var r = AnalizarEtiquetas(De("<label class=\"x\">Fecha</label>\n<input type=\"date\" id=\"f\" placeholder=\"dd/mm\" />\n<asp:TextBox ID=\"txtB\" runat=\"server\" Text='<%# Eval(\"A\") %>' />"));
            Assert.Equal(1, r.LabelSinForNiControl.Count);
            Assert.Equal(2, r.CampoSinEtiqueta.Count);   // el placeholder no es etiqueta
        }

        [Fact]
        public void Analizador_CruzModalYPestana()
        {
            var p = De("<div class=\"modal fade\" id=\"m\" tabindex=\"-1\">\n<h5 id=\"t\">T</h5>" +
                       "<button type=\"button\" class=\"close\" data-dismiss=\"modal\">&times;</button>" +
                       "<button type=\"button\" class=\"btn\" data-dismiss=\"modal\">Cancelar</button></div>" +
                       "<div class=\"modal\" role=\"dialog\" aria-labelledby=\"t\"></div>" +
                       "<a role=\"tab\" href=\"#a\">A</a><a role=\"tab\" aria-selected=\"true\" href=\"#b\">B</a>");
            Assert.Single(CerrarSinAriaLabel(p));
            Assert.Equal(new[] { "X.aspx:1 #m sin role=\"dialog\", sin aria-labelledby" }, ModalSinRoleNiTitulo(p));
            Assert.Single(PestanaSinAriaSelected(p));
        }

        [Fact]
        public void Analizador_H1_SoloCuentaPaginasQueMuestranAlgo()
        {
            Assert.Empty(PaginaSinUnSoloH1(De("<%@ Page %>")));
            Assert.Empty(PaginaSinUnSoloH1(De("<asp:Content runat=\"server\"><h1>A</h1></asp:Content>")));
            Assert.Single(PaginaSinUnSoloH1(De("<asp:Content runat=\"server\"><h2>A</h2></asp:Content>")));
            Assert.Single(PaginaSinUnSoloH1(De("<body><h1>A</h1><h1>B</h1></body>")));
        }
    }
}
