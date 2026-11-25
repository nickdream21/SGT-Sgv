using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WebSGV.Services.Ayuda;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Ayuda dentro de la app (E5-21, M-074), sobre el contenido real de <c>WebSGV/ContenidoAyuda</c>: el catálogo
    /// (temas.json ↔ fragmentos ↔ imágenes), cada pantalla con su "?" al tema que le corresponde, los roles de cada
    /// tema dentro de quienes entran a sus páginas (<see cref="GuardasAccesoTests.AccesoEsperado"/>), un HTML de
    /// lista blanca sin datos personales, el peso de las imágenes y el despliegue (csproj). Pruebas de fuente, sin BD.
    /// </summary>
    public class AyudaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Carpeta() => Path.Combine(RaizWeb(), "ContenidoAyuda");

        private static AyudaCatalogo Catalogo() => AyudaCatalogo.Cargar(Carpeta());

        private static string Relativo(string archivo) =>
            archivo.Substring(RaizWeb().Length + 1).Replace('\\', '/');

        private static void SinCasos(string regla, List<string> casos) =>
            Assert.True(casos.Count == 0, regla + " (" + casos.Count + "):\n" + string.Join("\n", casos));

        /// <summary>Pantallas sin la plantilla de encabezado que igual llevan su tema (portada y celular del conductor).</summary>
        private static readonly string[] SinPlantillaConTema =
        {
            "Views/Inicio.aspx", "Views/DashboardConductor.aspx", "Views/FirmarLiquidacion.aspx",
            "Views/ConformarLiquidacion.aspx", "Views/DetalleOrdenViaje.aspx",
        };

        /// <summary>Las pantallas que deben tener su tema: toda página con sgv-encabezado (salvo la propia Ayuda) y las de arriba.</summary>
        private static List<AccesibilidadMarcadoTests.Pagina> PaginasConTema() =>
            AccesibilidadMarcadoTests.Paginas()
                .Where(p => p.Ruta.StartsWith("Views/", StringComparison.OrdinalIgnoreCase) && p.Ruta.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase))
                .Where(p => (EncabezadoPaginaTests.Plantilla.IsMatch(p.Texto) && !p.Ruta.Equals("Views/Ayuda.aspx", StringComparison.OrdinalIgnoreCase))
                         || SinPlantillaConTema.Contains(p.Ruta, StringComparer.OrdinalIgnoreCase))
                .ToList();

        private static readonly Regex EnlaceAyuda = new Regex(@"<a\b(?:<%.*?%>|[^>])*\bdata-ayuda=""(?<t>[^""]*)""(?:<%.*?%>|[^>])*>", RegexOptions.Singleline);

        // ── 1. Catálogo ──────────────────────────────────────────────────────────────────────

        [Fact]
        public void Catalogo_ClavesValidasUnicasYConSuFragmento()
        {
            var cat = Catalogo();
            Assert.True(cat.Temas.Count >= 30, "temas.json con menos temas de lo esperado: " + cat.Temas.Count);
            var casos = new List<string>();
            foreach (var g in cat.Temas.GroupBy(t => t.Clave).Where(g => g.Count() > 1)) casos.Add("clave repetida: " + g.Key);
            foreach (var t in cat.Temas)
            {
                if (t.Clave == null || !AyudaCatalogo.ClaveValida.IsMatch(t.Clave)) { casos.Add("clave no válida: " + t.Clave); continue; }
                if (string.IsNullOrWhiteSpace(t.Titulo) || string.IsNullOrWhiteSpace(t.Seccion)) casos.Add(t.Clave + ": sin título o sección");
                bool tieneHtml = File.Exists(Path.Combine(Carpeta(), t.Clave + ".html"));
                if (!t.Borrador && !tieneHtml) casos.Add(t.Clave + ": no es borrador y no tiene " + t.Clave + ".html");
                if (!string.IsNullOrEmpty(t.Video) && !t.Video.StartsWith("https://", StringComparison.Ordinal)) casos.Add(t.Clave + ": el video debe ser https");
                foreach (var r in t.Capturas)
                    if (r.Archivo == null || !AyudaCatalogo.ImagenValida.IsMatch(r.Archivo) || (r.Ancho != 1366 && r.Ancho != 390)
                        || string.IsNullOrWhiteSpace(r.Selector) || r.Url == null || !r.Url.StartsWith("Views/", StringComparison.Ordinal))
                        casos.Add(t.Clave + ": receta de captura incompleta (" + r.Archivo + ")");
            }
            var secciones = cat.Temas.GroupBy(t => t.Seccion);
            foreach (var s in secciones)
                foreach (var o in s.GroupBy(t => t.Orden).Where(o => o.Count() > 1))
                    casos.Add("sección " + s.Key + ": orden " + o.Key + " repetido");
            SinCasos("Catálogo de la ayuda", casos);
        }

        [Fact]
        public void Catalogo_NingunArchivoSuelto()
        {
            var cat = Catalogo();
            var claves = new HashSet<string>(cat.Temas.Select(t => t.Clave), StringComparer.Ordinal);
            var casos = new List<string>();
            foreach (string f in Directory.GetFiles(Carpeta()))
            {
                string nombre = Path.GetFileName(f);
                if (nombre == AyudaCatalogo.ArchivoTemas) continue;
                if (Path.GetExtension(nombre) != ".html" || !claves.Contains(Path.GetFileNameWithoutExtension(nombre)))
                    casos.Add(nombre + ": archivo fuera del catálogo");
                else if (cat.Tema(Path.GetFileNameWithoutExtension(nombre)).Borrador)
                    casos.Add(nombre + ": el tema sigue en borrador (quite \"borrador\" de temas.json)");
            }
            foreach (string d in Directory.GetDirectories(Carpeta()))
                if (Path.GetFileName(d) != AyudaCatalogo.CarpetaImagenes) casos.Add(Path.GetFileName(d) + ": carpeta fuera del catálogo");
            string img = Path.Combine(Carpeta(), AyudaCatalogo.CarpetaImagenes);
            if (Directory.Exists(img))
            {
                foreach (string f in Directory.GetFiles(img)) casos.Add("img/" + Path.GetFileName(f) + ": va dentro de img/<tema>/");
                foreach (string d in Directory.GetDirectories(img))
                    if (!claves.Contains(Path.GetFileName(d))) casos.Add("img/" + Path.GetFileName(d) + ": no es un tema");
            }
            SinCasos("ContenidoAyuda con archivos sueltos", casos);
        }

        private static readonly Regex Src = new Regex(@"\bsrc=""(?<a>[^""]*)""");

        [Fact]
        public void Catalogo_CadaImagenReferidaExisteYNingunaHuerfana()
        {
            var casos = new List<string>();
            foreach (var t in Catalogo().Temas.Where(t => !t.Borrador))
            {
                string html = File.ReadAllText(Path.Combine(Carpeta(), t.Clave + ".html"));
                var usadas = Src.Matches(html).Cast<Match>().Select(m => m.Groups["a"].Value).ToList();
                string dir = Path.Combine(Carpeta(), AyudaCatalogo.CarpetaImagenes, t.Clave);
                var hay = Directory.Exists(dir) ? Directory.GetFiles(dir).Select(Path.GetFileName).ToList() : new List<string>();
                foreach (string u in usadas.Where(u => !hay.Contains(u))) casos.Add(t.Clave + ": falta img/" + t.Clave + "/" + u);
                foreach (string h in hay.Where(h => !usadas.Contains(h))) casos.Add(t.Clave + ": img/" + t.Clave + "/" + h + " no la usa el fragmento");
                foreach (var g in usadas.GroupBy(u => u).Where(g => g.Count() > 1)) casos.Add(t.Clave + ": " + g.Key + " repetida");
            }
            SinCasos("Imágenes de la ayuda", casos);
        }

        // ── 2. Cada pantalla ↔ su tema ───────────────────────────────────────────────────────

        [Fact]
        public void Paginas_CadaPantallaTieneSuInterrogacionAlTema()
        {
            var cat = Catalogo();
            var paginas = PaginasConTema();
            Assert.True(paginas.Count >= 30, "No encontré las pantallas con encabezado: " + paginas.Count);
            var casos = new List<string>();
            foreach (var p in paginas)
            {
                var temas = cat.Temas.Where(t => t.Paginas.Contains(p.Ruta, StringComparer.OrdinalIgnoreCase)).ToList();
                if (temas.Count != 1) { casos.Add(p.Ruta + ": está en " + temas.Count + " temas de temas.json (debe ser 1)"); continue; }
                var tema = temas[0];
                var enlaces = EnlaceAyuda.Matches(p.Texto).Cast<Match>().ToList();
                if (tema.Borrador) casos.Add(p.Ruta + ": su tema " + tema.Clave + " es borrador (no se puede abrir)");
                if (enlaces.Count != 1) { casos.Add(p.Ruta + ": " + enlaces.Count + " enlaces data-ayuda (debe ser 1)"); continue; }
                var e = enlaces[0];
                if (e.Groups["t"].Value != tema.Clave) casos.Add(p.Ruta + ": el \"?\" abre " + e.Groups["t"].Value + " y su tema es " + tema.Clave);
                if (!e.Value.Contains("ResolveUrl(\"~/Views/Ayuda.aspx?tema=" + tema.Clave + "\")")) casos.Add(p.Ruta + ": el href no es Ayuda.aspx?tema=" + tema.Clave);
                if (!e.Value.Contains("target=\"_blank\"") || !e.Value.Contains("rel=\"noopener\"")) casos.Add(p.Ruta + ": el \"?\" debe abrir otra pestaña sin JS (target y rel)");
                bool esPortada = p.Ruta.Equals("Views/Inicio.aspx", StringComparison.OrdinalIgnoreCase);
                if (!esPortada && (!e.Value.Contains("class=\"sgv-ayuda\"") || !e.Value.Contains("aria-label=")))
                    casos.Add(p.Ruta + ": el \"?\" lleva class=\"sgv-ayuda\" y aria-label");
            }
            foreach (string r in SinPlantillaConTema.Where(r => !paginas.Any(p => p.Ruta.Equals(r, StringComparison.OrdinalIgnoreCase))))
                casos.Add(r + ": está en SinPlantillaConTema pero no existe");
            SinCasos("Pantalla sin su \"?\" al tema", casos);
        }

        [Fact]
        public void Paginas_ElConductorTieneSuInterrogacionJuntoAlTitulo()
        {
            // E5-21b: en las 4 pantallas del celular del conductor el "?" va en la misma fila que el h1 (no junto al
            // nombre, que a 390 px quedaría tapado), con el estilo de la fila en su hoja de página.
            string raiz = RaizWeb();
            var esperadas = new Dictionary<string, (string Fila, string Tema)>
            {
                ["Views/DashboardConductor.aspx"] = ("dc-titulo-fila", "conductor-mi-viaje"),
                ["Views/DetalleOrdenViaje.aspx"] = ("dc-titulo-fila", "conductor-historial"),
                ["Views/FirmarLiquidacion.aspx"] = ("fl-titulo-fila", "conductor-liquidar-firmar"),
                ["Views/ConformarLiquidacion.aspx"] = ("fl-titulo-fila", "conductor-conformidad"),
            };
            var casos = new List<string>();
            foreach (var e in esperadas)
            {
                string texto = File.ReadAllText(Path.Combine(raiz, e.Key.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
                var fila = Regex.Match(texto, @"<div class=""" + e.Value.Fila + @""">\s*<h1\b.*?</h1>\s*(?<a><a class=""sgv-ayuda""(?:<%.*?%>|[^>])*>\?</a>)\s*</div>", RegexOptions.Singleline);
                if (!fila.Success) { casos.Add(e.Key + ": el \"?\" no está en " + e.Value.Fila + " junto al h1"); continue; }
                if (!fila.Groups["a"].Value.Contains("data-ayuda=\"" + e.Value.Tema + "\"")) casos.Add(e.Key + ": el \"?\" no abre " + e.Value.Tema);
            }
            string cssConductor = File.ReadAllText(Path.Combine(raiz, "Content", "paginas", "DashboardConductor.css"));
            string cssFirma = File.ReadAllText(Path.Combine(raiz, "Content", "paginas", "FirmarLiquidacion.css"));
            if (!cssConductor.Contains(".dc-titulo-fila .sgv-ayuda { flex-shrink: 0; }")) casos.Add("DashboardConductor.css sin .dc-titulo-fila");
            if (!cssFirma.Contains(".fl-titulo-fila .sgv-ayuda { flex-shrink: 0; }")) casos.Add("FirmarLiquidacion.css sin .fl-titulo-fila");
            // 2,75 rem en el celular (para el dedo): la regla común de sgv-base.css.
            string base_ = File.ReadAllText(Path.Combine(raiz, "Content", "sgv-base.css"));
            if (!base_.Contains(".sgv-ayuda { width: 2.75rem; height: 2.75rem; }")) casos.Add("sgv-base.css sin el \"?\" de 2,75 rem en el celular");
            SinCasos("\"?\" del conductor", casos);
        }

        [Fact]
        public void Paginas_DelCatalogoExistenYNingunaQuedaEnDosTemas()
        {
            string raiz = RaizWeb();
            var casos = new List<string>();
            var todas = Catalogo().Temas.SelectMany(t => t.Paginas.Select(p => (t.Clave, Pagina: p))).ToList();
            foreach (var (clave, pagina) in todas)
                if (!File.Exists(Path.Combine(raiz, pagina.Replace('/', Path.DirectorySeparatorChar)))) casos.Add(clave + ": " + pagina + " no existe");
            foreach (var g in todas.GroupBy(x => x.Pagina, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                casos.Add(g.Key + ": en " + string.Join(", ", g.Select(x => x.Clave)));
            SinCasos("Páginas del catálogo", casos);
        }

        [Fact]
        public void Paginas_YaNoQuedaElTextoProvisionalDelInterrogacion()
        {
            var casos = AccesibilidadMarcadoTests.Paginas()
                .Where(p => p.Texto.Contains("id=\"ayudaPagina\"") || p.Texto.Contains("data-target=\"#ayudaPagina\""))
                .Select(p => p.Ruta).ToList();
            SinCasos("#ayudaPagina (el texto pasó a ContenidoAyuda)", casos);
        }

        // ── 3. Roles ─────────────────────────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> GrupoDeGuardas = new Dictionary<string, string>
        {
            [AyudaCatalogo.GrupoAdmin] = GuardasAccesoTests.A,
            [AyudaCatalogo.GrupoConductor] = GuardasAccesoTests.C,
            [AyudaCatalogo.GrupoContabilidad] = GuardasAccesoTests.K,
            [AyudaCatalogo.GrupoSistema] = GuardasAccesoTests.Y,
        };

        [Fact]
        public void Roles_DeCadaTemaEntranASusPaginas()
        {
            var casos = new List<string>();
            foreach (var t in Catalogo().Temas)
            {
                if (t.Roles.Count == 0) casos.Add(t.Clave + ": sin roles");
                foreach (string r in t.Roles.Where(r => !AyudaCatalogo.Grupos.Contains(r))) casos.Add(t.Clave + ": grupo desconocido " + r);
                foreach (var g in t.Roles.GroupBy(r => r).Where(g => g.Count() > 1)) casos.Add(t.Clave + ": grupo repetido " + g.Key);
                foreach (string pagina in t.Paginas)
                {
                    if (!GuardasAccesoTests.AccesoEsperado.TryGetValue(pagina, out var entran)) { casos.Add(t.Clave + ": " + pagina + " no está en AccesoEsperado"); continue; }
                    foreach (string r in t.Roles.Where(GrupoDeGuardas.ContainsKey))
                        if (!entran.Contains(GrupoDeGuardas[r])) casos.Add(t.Clave + ": el grupo " + r + " no entra a " + pagina);
                }
            }
            SinCasos("Roles de los temas", casos);
        }

        [Fact]
        public void Roles_CadaRolVigenteTieneTemas()
        {
            // E5-21b: todo rol vigente tiene temas abiertos (ninguno queda en borrador).
            var cat = Catalogo();
            foreach (var g in GuardasAccesoTests.RolesDelGrupo)
                foreach (string rol in g.Value)
                    Assert.True(cat.Temas.Any(t => t.Roles.Contains(AyudaCatalogo.Grupo(rol))), rol + " no tiene ningún tema en temas.json");
            Assert.True(cat.TemasDelRol("ADMIN", k => false).Count >= 10);
            foreach (string rol in GuardasAccesoTests.RolesRetirados) Assert.Empty(cat.TemasDelRol(rol, k => true));
            foreach (var g in GuardasAccesoTests.RolesDelGrupo)
                foreach (string rol in g.Value)
                    Assert.True(cat.TemasDelRol(rol, k => false).Count > 0, rol + " no tiene temas abiertos");
            Assert.Empty(cat.Temas.Where(t => t.Borrador).Select(t => t.Clave));
        }

        [Fact]
        public void Interruptores_SonAppSettingsDelWebConfig()
        {
            var claves = XDocument.Load(Path.Combine(RaizWeb(), "Web.config")).Root.Element("appSettings").Elements("add")
                .Select(e => (string)e.Attribute("key")).ToList();
            var casos = Catalogo().Temas.Where(t => !string.IsNullOrEmpty(t.Interruptor) && !claves.Contains(t.Interruptor))
                .Select(t => t.Clave + ": " + t.Interruptor).ToList();
            SinCasos("Interruptor de tema que no es un appSetting", casos);
        }

        // ── 4. HTML de lista blanca y redacción formal (DN-28) ───────────────────────────────

        private static readonly HashSet<string> EtiquetasPermitidas = new HashSet<string>(StringComparer.Ordinal)
        {
            "h2", "h3", "p", "ul", "ol", "li", "strong", "em", "kbd", "br", "table", "thead", "tbody", "tr", "th", "td",
            "figure", "figcaption", "img", "a",
        };

        private static readonly HashSet<string> ClasesPermitidas = new HashSet<string>(StringComparer.Ordinal) { "ayuda-resumen", "ayuda-nota" };

        private static readonly Regex Etiqueta = new Regex(@"<(?<cierre>/?)(?<n>[a-zA-Z][a-zA-Z0-9]*)(?<a>[^<>]*)>");
        private static readonly Regex Atributo = new Regex(@"\s(?<n>[a-zA-Z-]+)=""(?<v>[^""<>]*)""");

        /// <summary>Lo que viola la lista blanca en un fragmento (vacío si cumple).</summary>
        internal static List<string> FallasDeHtml(string html, ISet<string> temasAbiertos)
        {
            var fallas = new List<string>();
            if (html.Contains("<!") || html.Contains("<?")) fallas.Add("comentario o declaración (<! o <?)");
            if (!html.TrimStart().StartsWith("<p class=\"ayuda-resumen\">", StringComparison.Ordinal)) fallas.Add("no empieza con <p class=\"ayuda-resumen\">");
            if (Regex.Matches(html, @"class=""ayuda-resumen""").Count != 1) fallas.Add("debe tener un solo ayuda-resumen");
            string resto = Etiqueta.Replace(html, " ");
            foreach (Match m in Etiqueta.Matches(html))
            {
                string n = m.Groups["n"].Value;
                if (!EtiquetasPermitidas.Contains(n)) { fallas.Add("etiqueta <" + n + ">"); continue; }
                string attrs = m.Groups["a"].Value;
                if (m.Groups["cierre"].Value == "/")
                {
                    if (attrs.Trim().Length > 0) fallas.Add("cierre con atributos </" + n + attrs + ">");
                    continue;
                }
                var leidos = Atributo.Matches(attrs).Cast<Match>().ToList();
                string sobrante = Atributo.Replace(attrs, "").Trim().TrimEnd('/').Trim();
                if (sobrante.Length > 0) fallas.Add("<" + n + ">: atributo no permitido o mal escrito: " + sobrante);
                foreach (Match a in leidos)
                {
                    string an = a.Groups["n"].Value, av = a.Groups["v"].Value;
                    if (an == "class" && !av.Split(' ').All(ClasesPermitidas.Contains)) fallas.Add("<" + n + ">: clase " + av);
                    else if (an == "alt" && n != "img") fallas.Add("<" + n + ">: alt fuera de img");
                    else if (an == "src" && (n != "img" || !AyudaCatalogo.ImagenValida.IsMatch(av))) fallas.Add("<" + n + ">: src " + av);
                    else if (an == "href")
                    {
                        Match h = Regex.Match(av, @"^\?tema=(?<c>[a-z0-9-]{1,40})$");
                        if (n != "a" || !h.Success) fallas.Add("<" + n + ">: href " + av + " (solo ?tema=clave)");
                        else if (!temasAbiertos.Contains(h.Groups["c"].Value)) fallas.Add("enlace a un tema borrador o inexistente: " + av);
                    }
                    else if (an != "class" && an != "alt" && an != "src" && an != "href") fallas.Add("<" + n + ">: atributo " + an);
                }
                if (n == "img" && !leidos.Any(a => a.Groups["n"].Value == "alt" && a.Groups["v"].Value.Trim().Length >= 5))
                    fallas.Add("<img> sin alt descriptivo");
                if (n == "a" && !leidos.Any(a => a.Groups["n"].Value == "href")) fallas.Add("<a> sin href");
            }
            if (resto.Contains("<") || resto.Contains(">")) fallas.Add("< o > sueltos (use &lt; y &gt;)");
            string texto = WebUtility.HtmlDecode(resto);
            if (texto.Contains("!") || texto.Contains("¡")) fallas.Add("exclamación (DN-28)");
            if (Regex.IsMatch(html, @"\bfa-[a-z]")) fallas.Add("icono fa- (DN-28)");
            if (texto.Any(c => char.IsSurrogate(c) || (c >= '☀' && c <= '➿') || (c >= '⬀' && c <= '⯿'))) fallas.Add("emoji o símbolo (DN-28)");
            if (Regex.IsMatch(html, @"(?i)javascript:|https?:|\bon[a-z]+\s*=|<script|<style|style\s*=")) fallas.Add("script, estilo o URL externa");
            foreach (Match ent in Regex.Matches(html, @"&[^;\s]*;?"))
                if (!Regex.IsMatch(ent.Value, @"^&(amp|lt|gt|quot|nbsp);$")) fallas.Add("entidad " + ent.Value + " (escriba el carácter en UTF-8)");
            return fallas;
        }

        [Fact]
        public void Html_ListaBlancaYRedaccionFormal()
        {
            var cat = Catalogo();
            var abiertos = new HashSet<string>(cat.Temas.Where(t => !t.Borrador).Select(t => t.Clave));
            var casos = new List<string>();
            foreach (var t in cat.Temas.Where(t => !t.Borrador))
                foreach (string f in FallasDeHtml(File.ReadAllText(Path.Combine(Carpeta(), t.Clave + ".html"), Encoding.UTF8), abiertos))
                    casos.Add(t.Clave + ".html: " + f);
            foreach (var t in cat.Temas)
                if ((t.Titulo + t.Seccion).IndexOfAny(new[] { '!', '¡' }) >= 0) casos.Add("temas.json " + t.Clave + ": exclamación en título o sección");
            SinCasos("Fragmento de ayuda fuera de la lista blanca", casos);
        }

        [Fact]
        public void Html_LaListaBlancaRechazaLoPeligroso()
        {
            var abiertos = new HashSet<string> { "tema-a" };
            string ok = "<p class=\"ayuda-resumen\">Resumen.</p><p>Vea <a href=\"?tema=tema-a\">el otro</a>.</p>" +
                        "<figure><img src=\"01.webp\" alt=\"La pantalla de alta\"><figcaption>Alta</figcaption></figure>";
            Assert.Empty(FallasDeHtml(ok, abiertos));
            string r = "<p class=\"ayuda-resumen\">R.</p>";
            Assert.NotEmpty(FallasDeHtml(r + "<script>x()</script>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p onclick=\"x()\">a</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p style=\"color:red\">a</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<a href=\"https://x.com\">a</a>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<a href=\"javascript:x()\">a</a>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<a href=\"?tema=borrador\">a</a>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<img src=\"../x.webp\" alt=\"Una imagen\">", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<img src=\"01.webp\">", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<h1>T</h1>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p class=\"otra\">a</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p>Listo!</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p>¡Bien</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p>Bien \U0001F600</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p><i class=\"fas fa-check\"></i></p>", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<!-- nota -->", abiertos));
            Assert.NotEmpty(FallasDeHtml(r + "<p>a &#x21; b</p>", abiertos));
            Assert.NotEmpty(FallasDeHtml("<p>Sin resumen.</p>", abiertos));
        }

        // ── 5. Sin datos personales ──────────────────────────────────────────────────────────

        private static readonly Regex Telefono = new Regex(@"(?<!\d)9\d{8}(?!\d)|\+51");
        private static readonly Regex Licencia = new Regex(@"\b[A-Z]-?\d{8}\b");

        [Fact]
        public void Contenido_SinNumerosDeDniTelefonoNiLicencia()
        {
            var casos = new List<string>();
            foreach (string f in Directory.GetFiles(Carpeta(), "*.*", SearchOption.AllDirectories)
                         .Where(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                string[] lineas = File.ReadAllLines(f);
                for (int i = 0; i < lineas.Length; i++)
                    foreach (var re in new[] { CodigoFuenteLimpiezaTests.NumeroConFormaDeDni, Telefono, Licencia })
                        foreach (Match m in re.Matches(lineas[i]))
                            // Sin el número en el mensaje: no repetir el dato personal en la salida.
                            casos.Add(Relativo(f) + ":" + (i + 1) + ", columna " + (m.Index + 1));
            }
            SinCasos("Número con forma de DNI, teléfono o licencia en la ayuda", casos);
        }

        [Fact]
        public void Contenido_LasExpresionesDetectanLoPersonal()
        {
            Assert.Matches(Telefono, "llame al 987654321");
            Assert.Matches(Telefono, "+51 1 234");
            Assert.DoesNotMatch(Telefono, "pedido 4400132772");
            Assert.Matches(Licencia, "licencia Q12345678");
            Assert.Matches(Licencia, "B-00000000");
            Assert.DoesNotMatch(Licencia, "guía T660-00016509");
        }

        // ── 6. Imágenes: formato y peso ──────────────────────────────────────────────────────

        [Fact]
        public void Imagenes_SoloWebpLivianas()
        {
            string img = Path.Combine(Carpeta(), AyudaCatalogo.CarpetaImagenes);
            if (!Directory.Exists(img)) return;   // la ayuda funciona sin imágenes
            var casos = new List<string>();
            var archivos = Directory.GetFiles(img, "*", SearchOption.AllDirectories);
            foreach (string f in archivos)
            {
                if (!AyudaCatalogo.ImagenValida.IsMatch(Path.GetFileName(f))) casos.Add(Relativo(f) + ": nombre NN.webp");
                if (new FileInfo(f).Length > 150 * 1024) casos.Add(Relativo(f) + ": más de 150 KB");
                byte[] cab = new byte[12];
                using (var s = File.OpenRead(f)) s.Read(cab, 0, 12);
                if (Encoding.ASCII.GetString(cab, 0, 4) != "RIFF" || Encoding.ASCII.GetString(cab, 8, 4) != "WEBP") casos.Add(Relativo(f) + ": no es WebP");
            }
            foreach (string d in Directory.GetDirectories(img))
                if (Directory.GetFiles(d).Length > 3) casos.Add(Relativo(d) + ": más de 3 imágenes");
            long total = archivos.Sum(f => new FileInfo(f).Length);
            if (total > 6L * 1024 * 1024) casos.Add("img/: " + total / 1024 + " KB (tope 6 MB)");
            SinCasos("Imágenes de la ayuda", casos);
        }

        // ── 7. Despliegue ────────────────────────────────────────────────────────────────────

        [Fact]
        public void Despliegue_TodoArchivoDeLaAyudaEsContentDelCsproj()
        {
            XDocument csproj = XDocument.Load(Path.Combine(RaizWeb(), "WebSGV.csproj"));
            XNamespace ns = csproj.Root.Name.Namespace;
            var content = csproj.Descendants(ns + "Content").Select(e => ((string)e.Attribute("Include") ?? "").Replace('\\', '/'))
                .Where(i => i.StartsWith("ContenidoAyuda/", StringComparison.OrdinalIgnoreCase)).ToList();
            var enDisco = Directory.GetFiles(Carpeta(), "*", SearchOption.AllDirectories).Select(Relativo).ToList();
            var casos = enDisco.Where(f => !content.Contains(f, StringComparer.OrdinalIgnoreCase)).Select(f => f + ": falta su <Content Include> en WebSGV.csproj")
                .Concat(content.Where(c => !enDisco.Contains(c, StringComparer.OrdinalIgnoreCase)).Select(c => c + ": está en el csproj y no existe"))
                .ToList();
            SinCasos("Despliegue de la ayuda", casos);
            foreach (string p in new[] { "Views/Ayuda.aspx", "Views/AyudaImagen.aspx" })
                Assert.Contains(csproj.Descendants(ns + "Content"), e => ((string)e.Attribute("Include") ?? "").Replace('\\', '/') == p);
        }

        // ── 8. Pantallas de la ayuda ─────────────────────────────────────────────────────────

        [Fact]
        public void Ayuda_AvisoYModal()
        {
            string raiz = RaizWeb();
            string aspx = File.ReadAllText(Path.Combine(raiz, "Views", "Ayuda.aspx"));
            string cs = File.ReadAllText(Path.Combine(raiz, "Views", "Ayuda.aspx.cs"));
            Assert.Equal("Ese tema no está disponible para su usuario.",
                WebUtility.HtmlDecode(Regex.Match(aspx, @"<span id=""ayudaAviso"">(?<t>.*?)</span>").Groups["t"].Value));
            Assert.Contains("id=\"ayudaContenido\"", aspx);
            Assert.Contains("PuedeVer(clave, rol, InterruptorEncendido)", cs);
            // El fragmento sin permiso no se lee: lo que no puede ver no llega ni al HTML.
            Assert.Matches(@"PuedeVer\(clave, rol, InterruptorEncendido\) \? catalogo\.LeerFragmento\(clave\) : null", cs);

            string imagen = File.ReadAllText(Path.Combine(raiz, "Views", "AyudaImagen.aspx.cs"));
            Assert.Contains("PuedeVer(tema, RolesHelper.ObtenerRolActual(), Ayuda.InterruptorEncendido)", imagen);
            Assert.Contains("TryRutaImagen(tema, archivo, out ruta)", imagen);
            Assert.Contains("Response.StatusCode = 404", imagen);

            string master = File.ReadAllText(Path.Combine(raiz, "Site.Master"));
            Assert.Contains("id=\"modalAyuda\"", master);
            Assert.Contains("id=\"modalAyudaCuerpo\"", master);
            string js = File.ReadAllText(Path.Combine(raiz, "Scripts", "paginas", "SiteMaster.js"));
            Assert.Contains("closest('a[data-ayuda]')", js);
            Assert.Contains("getElementById('ayudaContenido')", js);
            Assert.Contains("getElementById('ayudaAviso')", js);
            string css = File.ReadAllText(Path.Combine(raiz, "Content", "sgv-base.css"));
            Assert.Contains(".sgv-ayuda-cuerpo figure img { display: block; max-width: 100%;", css);
        }

        // ── 9. Textos legales y manual completo (E5-21b) ─────────────────────────────────────

        /// <summary>Texto visible de un fragmento: sin etiquetas, entidades decodificadas y espacios unidos.</summary>
        private static string TextoPlano(string html) =>
            Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

        private static string Unir(string s) => Regex.Replace(s, @"\s+", " ").Trim();

        [Theory]
        [InlineData("firma-electronica", "Declaracion,Revision,Conformidad,DatosPersonales,BaseLegal")]
        [InlineData("conductor-liquidar-firmar", "Declaracion,DatosPersonales,BaseLegal")]
        [InlineData("conductor-conformidad", "Conformidad,DatosPersonales,BaseLegal")]
        public void Firma_LaAyudaCitaLosTextosLegalesTalCual(string tema, string constantes)
        {
            // Lo que el firmante acepta vive solo en TextosFirma; la ayuda lo cita entero, sin parafrasear. Si cambia
            // un texto legal, esta prueba obliga a actualizar el tema.
            string texto = TextoPlano(File.ReadAllText(Path.Combine(Carpeta(), tema + ".html"), Encoding.UTF8));
            var textos = new Dictionary<string, string>
            {
                ["Declaracion"] = WebSGV.Services.Liquidaciones.TextosFirma.Declaracion,
                ["Revision"] = WebSGV.Services.Liquidaciones.TextosFirma.Revision,
                ["Conformidad"] = WebSGV.Services.Liquidaciones.TextosFirma.Conformidad,
                ["DatosPersonales"] = WebSGV.Services.Liquidaciones.TextosFirma.DatosPersonales,
                ["BaseLegal"] = WebSGV.Services.Liquidaciones.TextosFirma.BaseLegal,
            };
            var casos = constantes.Split(',').Where(c => !texto.Contains(Unir(textos[c]))).Select(c => tema + ": no cita TextosFirma." + c + " tal cual").ToList();
            SinCasos("Texto legal en la ayuda", casos);
        }

        [Fact]
        public void Manual_JuntaLosTemasDelRolConPortadaIndiceYAnclas()
        {
            var cat = Catalogo();
            foreach (string rol in new[] { "ADMINISTRADOR DE TRANSPORTE", "CONDUCTOR", "CONTABILIDAD", "ADMINISTRADOR DE SISTEMA" })
            {
                var temas = cat.TemasDelRol(rol, k => false);
                string html = cat.HtmlManual(rol, k => false, (t, a) => "/img?tema=" + t + "&img=" + a, c => "/Ayuda.aspx?tema=" + c,
                    new DateTime(2026, 11, 24), "Nota de portada");
                Assert.NotNull(html);
                Assert.Contains("manual-portada", html);
                Assert.Contains("24/11/2026", html);
                Assert.Contains("Usuario: " + WebUtility.HtmlEncode(AyudaCatalogo.NombreGrupo(AyudaCatalogo.Grupo(rol))), html);
                // Un tema por sección del manual, en el orden del índice; nada de otro rol ni apagado.
                var secciones = Regex.Matches(html, @"<section class=""manual-tema"" id=""tema-(?<c>[a-z0-9-]+)"">").Cast<Match>().Select(m => m.Groups["c"].Value).ToList();
                Assert.Equal(temas.Select(t => t.Clave), secciones);
                Assert.DoesNotContain("tema-movimientos-viaje\"", html);
                // Enlaces entre temas del manual: anclas; sin data-ayuda (no abre el modal); imágenes por la URL dada.
                Assert.DoesNotContain("data-ayuda", html);
                Assert.DoesNotContain("href=\"?tema=", html);
                foreach (Match a in Regex.Matches(html, @"href=""#tema-(?<c>[a-z0-9-]+)""")) Assert.Contains(a.Groups["c"].Value, secciones);
                foreach (Match s in Regex.Matches(html, @"\bsrc=""(?<u>[^""]*)""")) Assert.StartsWith("/img?tema=", WebUtility.HtmlDecode(s.Groups["u"].Value));
            }
            Assert.Null(cat.HtmlManual("ROL INVENTADO", k => true, (t, a) => a, c => c, DateTime.Today));
            // Con el interruptor encendido el tema de movimientos entra al manual de la administración.
            Assert.Contains("tema-movimientos-viaje\"", cat.HtmlManual("ADMIN", k => true, (t, a) => a, c => c, DateTime.Today));
        }

        [Fact]
        public void Manual_PaginaEnlazadaDesdeElIndiceYConSuHoja()
        {
            string raiz = RaizWeb();
            string aspx = File.ReadAllText(Path.Combine(raiz, "Views", "Ayuda.aspx"));
            string cs = File.ReadAllText(Path.Combine(raiz, "Views", "Ayuda.aspx.cs"));
            Assert.Contains("NavigateUrl=\"~/Views/Ayuda.aspx?manual=1\">Manual completo</asp:HyperLink>", aspx);
            Assert.Contains("RecursoHelper.Url(\"~/Content/paginas/AyudaManual.css\")", aspx);
            Assert.Contains("data-accion=\"imprimirPagina\"", aspx);
            Assert.Contains("Request.QueryString[\"manual\"] == \"1\"", cs);
            Assert.Contains("catalogo.HtmlManual(rol, InterruptorEncendido,", cs);
            // La hoja del manual se sirve desde Content (ContenidoAyuda está en hiddenSegments) y se despliega.
            Assert.True(File.Exists(Path.Combine(raiz, "Content", "paginas", "AyudaManual.css")));
            Assert.False(File.Exists(Path.Combine(Carpeta(), "manual.css")));
            Assert.Contains("<Content Include=\"Content\\paginas\\AyudaManual.css\" />", File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj")));
            Assert.Contains("imprimirPagina: function () { window.print(); return false; }",
                File.ReadAllText(Path.Combine(raiz, "Scripts", "paginas", "SiteMaster.js")));
            // El generador del PDF usa la misma hoja y marca la portada como borrador.
            string mjs = File.ReadAllText(Path.Combine(raiz, "..", "herramientas", "manual", "generar-manual.mjs"), Encoding.UTF8);
            Assert.Contains("Content/paginas/AyudaManual.css", mjs);
            Assert.Contains("Borrador hasta la revisión del usuario.", mjs);
        }
    }
}
