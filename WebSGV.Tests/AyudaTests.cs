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

        /// <summary>
        /// Páginas cuyo "?" llega en E5-21b (lista cerrada temporal): su tema sigue en borrador y la página todavía
        /// no tiene el enlace. Al escribir el tema y poner el "?", se saca de aquí; al cerrar 21b queda vacía.
        /// </summary>
        internal static readonly HashSet<string> PendientesDe21b = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Views/Auditoria.aspx",
            "Views/DashboardAdminSistema.aspx",
            "Views/Exportacion/DashboardExportacion.aspx",
            "Views/Exportacion/ImportarSeguimiento.aspx",
            "Views/Exportacion/RegistroSeguimiento.aspx",
            "Views/Exportacion/SeguimientoViaje.aspx",
            "Views/FacturasFlete.aspx",
            "Views/GestionUsuarios.aspx",
            "Views/LiquidacionesAprobadasContabilidad.aspx",
            "Views/MiSeguridad.aspx",
            "Views/MovimientosViaje.aspx",
            "Views/RegistroDespacho.aspx",
            "Views/ReporteGps.aspx",
            "Views/ReportesOrdenesViaje.aspx",
            "Views/SaldosConductores.aspx",
            "Views/DashboardConductor.aspx",
            "Views/FirmarLiquidacion.aspx",
            "Views/ConformarLiquidacion.aspx",
            "Views/DetalleOrdenViaje.aspx",
        };

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
                if (nombre == AyudaCatalogo.ArchivoTemas || nombre == "manual.css") continue;
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
                if (PendientesDe21b.Contains(p.Ruta))
                {
                    if (!tema.Borrador) casos.Add(p.Ruta + ": su tema " + tema.Clave + " ya no es borrador: ponga el \"?\" y sáquela de PendientesDe21b");
                    if (enlaces.Count > 0) casos.Add(p.Ruta + ": ya tiene el \"?\": sáquela de PendientesDe21b");
                    continue;
                }
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
            foreach (string r in PendientesDe21b.Where(r => !paginas.Any(p => p.Ruta.Equals(r, StringComparison.OrdinalIgnoreCase))))
                casos.Add(r + ": está en PendientesDe21b pero no es una pantalla con encabezado");
            SinCasos("Pantalla sin su \"?\" al tema", casos);
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
            // Al cerrar 21b todo rol vigente tiene al menos su tema; en 21a solo la administración tiene temas abiertos.
            var cat = Catalogo();
            foreach (var g in GuardasAccesoTests.RolesDelGrupo)
                foreach (string rol in g.Value)
                    Assert.True(cat.Temas.Any(t => t.Roles.Contains(AyudaCatalogo.Grupo(rol))), rol + " no tiene ningún tema en temas.json");
            Assert.True(cat.TemasDelRol("ADMIN", k => false).Count >= 10);
            foreach (string rol in GuardasAccesoTests.RolesRetirados) Assert.Empty(cat.TemasDelRol(rol, k => true));
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
    }
}
