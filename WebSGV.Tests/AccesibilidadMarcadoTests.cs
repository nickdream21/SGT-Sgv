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
    }
}
