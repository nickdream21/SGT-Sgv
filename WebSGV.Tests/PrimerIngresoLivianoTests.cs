using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E5-7, primer ingreso liviano: compresión (IIS y respaldo propio apagado), caché de 365 días para Content y
    /// Scripts con toda referencia versionada (?v=), sin jQuery UI y versión de RecursoHelper en caché.
    /// </summary>
    public class PrimerIngresoLivianoTests
    {
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

        private static XElement WebConfig() => XDocument.Load(Path.Combine(RaizWeb(), "Web.config")).Root;

        // --- Respaldo de compresión: decisión pura ---

        private const string Gzip = "gzip, deflate, br";

        [Theory]
        [InlineData("text/html", "/Views/Login.aspx")]
        [InlineData("text/html; charset=utf-8", "/Views/Inicio.aspx")]
        [InlineData("application/json; charset=utf-8", "/Views/DashboardConductor.aspx/ObtenerDatos")]
        [InlineData("text/plain", "/Views/ListaDespachos.aspx")]
        [InlineData("TEXT/HTML", "/Views/Login.aspx")]
        // Catálogo de destinos de descarga: es una página normal, no una descarga.
        [InlineData("text/html", "/Views/DestinoDescarga.aspx")]
        public void Compresion_ComprimePaginasJsonYTexto(string tipo, string ruta)
        {
            Assert.True(CompresionRespuesta.DebeComprimir(tipo, Gzip, null, null, ruta, false));
        }

        [Theory]
        // Tipos que no son de la lista: descargas, imágenes, scripts de ScriptResource.axd.
        [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "/Views/SaldosConductores.aspx")]
        [InlineData("application/pdf", "/Views/ReportesOrdenesViaje.aspx")]
        [InlineData("image/png", "/Views/Login.aspx")]
        [InlineData("application/x-javascript", "/ScriptResource.axd")]
        [InlineData("", "/Views/Login.aspx")]
        [InlineData(null, "/Views/Login.aspx")]
        // Páginas y manejadores que entregan archivos, aunque respondan con texto.
        [InlineData("text/html", "/Views/DescargarDocumento.aspx")]
        [InlineData("text/plain", "/Views/DescargarPdfOrdenViaje.aspx")]
        [InlineData("application/json", "/Salud.ashx")]
        [InlineData("text/plain", "/Views/Exportacion/GpsAutomatico.ashx")]
        [InlineData("text/html", "/WebResource.axd")]
        public void Compresion_NoComprimeOtrosTiposNiDescargas(string tipo, string ruta)
        {
            Assert.False(CompresionRespuesta.DebeComprimir(tipo, Gzip, null, null, ruta, false));
        }

        [Fact]
        public void Compresion_NoComprimeConDisposicionCodificacionOParcial()
        {
            const string ruta = "/Views/SaldosConductores.aspx";
            // Una descarga con tipo de texto (CSV, TXT) sale por Content-Disposition.
            Assert.False(CompresionRespuesta.DebeComprimir("text/plain", Gzip, null, "attachment; filename=a.txt", ruta, false));
            Assert.False(CompresionRespuesta.DebeComprimir("text/html", Gzip, null, "inline; filename=a.html", ruta, false));
            // Ya comprimida (IIS o el propio handler): no se comprime dos veces.
            Assert.False(CompresionRespuesta.DebeComprimir("text/html", Gzip, "gzip", null, ruta, false));
            // Parcial de UpdatePanel.
            Assert.False(CompresionRespuesta.DebeComprimir("text/plain", Gzip, null, null, ruta, true));
            // El cliente no acepta gzip.
            Assert.False(CompresionRespuesta.DebeComprimir("text/html", null, null, null, ruta, false));
            Assert.False(CompresionRespuesta.DebeComprimir("text/html", "deflate, br", null, null, ruta, false));
            Assert.False(CompresionRespuesta.DebeComprimir("text/html", "gzip;q=0", null, null, ruta, false));
            // Encabezados vacíos cuentan como ausentes.
            Assert.True(CompresionRespuesta.DebeComprimir("text/html", Gzip, "", " ", ruta, false));
        }

        [Theory]
        [InlineData("gzip", true)]
        [InlineData("GZIP", true)]
        [InlineData("deflate, gzip;q=0.5", true)]
        [InlineData("gzip; q=1.0, br", true)]
        [InlineData("*", true)]
        [InlineData("gzip;q=0", false)]
        [InlineData("gzip; q=0.0, deflate", false)]
        [InlineData("identity", false)]
        [InlineData("x-gzip-raro", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Compresion_AceptaGzip(string encabezado, bool esperado)
        {
            Assert.Equal(esperado, CompresionRespuesta.AceptaGzip(encabezado));
        }

        [Fact]
        public void Compresion_InterruptorApagadoPorDefecto()
        {
            XElement clave = WebConfig().Element("appSettings").Elements("add")
                .SingleOrDefault(a => (string)a.Attribute("key") == CompresionRespuesta.ClaveInterruptor);
            Assert.NotNull(clave);
            Assert.Equal("false", (string)clave.Attribute("value"));
        }

        [Fact]
        public void GlobalAsax_AplicaElRespaldoDeCompresion()
        {
            string global = File.ReadAllText(Path.Combine(RaizWeb(), "Global.asax.cs"));
            Assert.Contains("Application_PostRequestHandlerExecute", global);
            Assert.Contains("CompresionRespuesta.AplicarSiCorresponde", global);
        }

        // --- Web.config: compresión de IIS y caché de Content/Scripts ---

        [Fact]
        public void WebConfig_PideCompresionEstaticaYDinamica()
        {
            XElement servidor = WebConfig().Element("system.webServer");
            XElement compresion = servidor.Elements("urlCompression").SingleOrDefault();
            Assert.NotNull(compresion);
            Assert.Equal("true", (string)compresion.Attribute("doStaticCompression"));
            Assert.Equal("true", (string)compresion.Attribute("doDynamicCompression"));
            // Un <staticContent> repetido en el mismo nivel da un 500 en IIS.
            Assert.Single(servidor.Elements("staticContent"));
        }

        /// <summary>Ubicaciones con caché de navegador y su duración (las excepciones son una lista cerrada).</summary>
        private static readonly Dictionary<string, string> CacheEsperada = new Dictionary<string, string>
        {
            ["Content"] = "365.00:00:00",
            ["Scripts"] = "365.00:00:00",
            // Los pide el ScriptManager sin ?v= (App_Start/BundleConfig.cs y el paquete de WebForms).
            ["Scripts/jquery-si-falta.js"] = "7.00:00:00",
            ["Scripts/WebForms"] = "7.00:00:00",
            // Content/Templates salió con CargarExcel (E5-19a).
        };

        [Fact]
        public void WebConfig_CacheDeContentYScriptsPorUbicacion()
        {
            var conCache = WebConfig().Elements("location")
                .Select(l => new
                {
                    Ruta = (string)l.Attribute("path"),
                    Cache = l.Element("system.webServer")?.Element("staticContent")?.Element("clientCache")
                })
                .Where(x => x.Cache != null)
                .ToList();

            Assert.Equal(CacheEsperada.Keys.OrderBy(k => k), conCache.Select(x => x.Ruta).OrderBy(k => k));
            foreach (var x in conCache)
            {
                Assert.Equal("UseMaxAge", (string)x.Cache.Attribute("cacheControlMode"));
                Assert.Equal(CacheEsperada[x.Ruta], (string)x.Cache.Attribute("cacheControlMaxAge"));
                Assert.Single(x.Cache.Parent.Parent.Elements("staticContent"));
            }
            // Sin caché de navegador para todo el sitio (las páginas no deben guardarse).
            Assert.Null(WebConfig().Element("system.webServer").Element("staticContent").Element("clientCache"));
        }

        // --- Referencias versionadas ---

        /// <summary>
        /// Una URL a Content o Scripts dentro de una página o master: "~/Content/...", "/Scripts/...", "../Content/...".
        /// </summary>
        private static readonly Regex RutaDeRecurso = new Regex(@"(?<![\w.-])(?:~/|\.\./|/)(?:Content|Scripts)/[^\s""'<>)]*",
            RegexOptions.IgnoreCase);

        /// <summary>Lo que debe ir justo antes de la ruta: RecursoHelper.Url("</summary>
        private static readonly Regex AntesConVersion = new Regex(@"RecursoHelper\.Url\(\s*""$");

        /// <summary>Excepciones justificadas, archivo → ruta (hoy ninguna).</summary>
        private static readonly HashSet<string> ExcepcionesSinVersion = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static List<string> ReferenciasSinVersion(string archivo, string texto)
        {
            var hallazgos = new List<string>();
            foreach (Match m in RutaDeRecurso.Matches(texto))
            {
                string antes = texto.Substring(Math.Max(0, m.Index - 40), Math.Min(40, m.Index));
                if (AntesConVersion.IsMatch(antes)) continue;
                if (ExcepcionesSinVersion.Contains(archivo + " → " + m.Value)) continue;
                int linea = texto.Take(m.Index).Count(c => c == '\n') + 1;
                hallazgos.Add($"{archivo}:{linea} {m.Value}");
            }
            return hallazgos;
        }

        [Fact]
        public void Paginas_TodaReferenciaAContentYScriptsLlevaVersion()
        {
            // El detector, con casos conocidos.
            Assert.Empty(ReferenciasSinVersion("a", "<link href=\"<%= WebSGV.Helpers.RecursoHelper.Url(\"~/Content/App.css\") %>\" />"));
            Assert.Empty(ReferenciasSinVersion("a", "<img src=\"<%= RecursoHelper.Url(\"~/Content/logo-sgv.png\") %>\">"));
            Assert.Single(ReferenciasSinVersion("a", "<img src=\"/Content/favicon.png\" alt=\"Logo\">"));
            Assert.Single(ReferenciasSinVersion("a", "<img src=\"<%= ResolveUrl(\"~/Content/favicon.png\") %>\" />"));
            Assert.Single(ReferenciasSinVersion("a", "<script src=\"../Scripts/paginas/X.js\"></script>"));
            Assert.Single(ReferenciasSinVersion("a", "<asp:Image ImageUrl=\"~/Content/favicon.png\" runat=\"server\" />"));
            Assert.Empty(ReferenciasSinVersion("a", "<%-- estilos en Content/paginas/X.css --%>"));

            string raiz = RaizWeb();
            var archivos = Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories)
                .Where(f => new[] { ".aspx", ".master", ".ascx" }.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !Relativo(raiz, f).StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
                         && !Relativo(raiz, f).StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Contains(archivos, f => Relativo(raiz, f).Equals("Site.Master", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(archivos, f => Relativo(raiz, f).Equals("Views/Login.aspx", StringComparison.OrdinalIgnoreCase));

            var hallazgos = archivos.SelectMany(f => ReferenciasSinVersion(Relativo(raiz, f), File.ReadAllText(f))).ToList();
            Assert.True(hallazgos.Count == 0,
                "Content y Scripts se guardan 365 días en el navegador (Web.config): refiéralos con " +
                "<%= WebSGV.Helpers.RecursoHelper.Url(\"~/...\") %> para que lleven ?v=:" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }

        [Fact]
        public void LoginEInicio_UsanElLogoLiviano()
        {
            string raiz = RaizWeb();
            var logo = new FileInfo(Path.Combine(raiz, "Content", "logo-sgv.png"));
            Assert.True(logo.Exists, "Falta Content/logo-sgv.png");
            Assert.True(logo.Length < 40 * 1024, $"Content/logo-sgv.png pesa {logo.Length} bytes (máximo 40 KB).");
            foreach (string pagina in new[] { "Views/Login.aspx", "Views/Inicio.aspx" })
            {
                string texto = File.ReadAllText(Path.Combine(raiz, pagina));
                Assert.Contains("RecursoHelper.Url(\"~/Content/logo-sgv.png\")", texto);
                Assert.DoesNotContain("favicon.png", texto);
            }
        }

        [Fact]
        public void FontAwesome_MuestraElTextoMientrasCargaLaFuente()
        {
            string css = File.ReadAllText(Path.Combine(RaizWeb(), "Content", "lib", "fontawesome-5.15.4", "css", "all.min.css"));
            var reglas = Regex.Matches(css, @"@font-face\{[^}]*\}").Cast<Match>().Select(m => m.Value).ToList();
            Assert.Equal(3, reglas.Count);
            Assert.All(reglas, r => Assert.Contains("font-display:swap", r));
        }

        // --- Sin jQuery UI ---

        [Fact]
        public void SinJQueryUi_EnElArbolWeb()
        {
            string raiz = RaizWeb();
            string[] textos = { ".aspx", ".master", ".ascx", ".cs", ".js", ".css", ".config", ".csproj", ".asax", ".ashx" };
            var hallazgos = new List<string>();
            foreach (string f in Directory.GetFiles(raiz, "*.*", SearchOption.AllDirectories))
            {
                string r = Relativo(raiz, f);
                if (r.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) || r.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (r.IndexOf("jquery-ui", StringComparison.OrdinalIgnoreCase) >= 0) { hallazgos.Add(r); continue; }
                if (!textos.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                string[] lineas = File.ReadAllLines(f);
                for (int i = 0; i < lineas.Length; i++)
                    if (lineas[i].IndexOf("jquery-ui", StringComparison.OrdinalIgnoreCase) >= 0)
                        hallazgos.Add($"{r}:{i + 1}");
            }
            Assert.True(hallazgos.Count == 0,
                "jQuery UI se quitó en E5-7 (nada lo usaba y pesaba 100 KB en cada página):" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }

        // --- RecursoHelper: versión en caché con dependencia sobre el archivo ---

        [Fact]
        public void RecursoHelper_VersionSeGuardaYSeRenuevaAlCambiarElArchivo()
        {
            Assert.Null(RecursoHelper.Version(null));
            Assert.Null(RecursoHelper.Version(Path.Combine(Path.GetTempPath(), "sgv-no-existe-" + Guid.NewGuid() + ".css")));

            string dir = Path.Combine(Path.GetTempPath(), "sgv-recurso-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string archivo = Path.Combine(dir, "x.css");
            try
            {
                File.WriteAllText(archivo, "a{}");
                File.SetLastWriteTimeUtc(archivo, new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
                Assert.Equal("20260102030405", RecursoHelper.Version(archivo));
                // Guardada en la caché: las siguientes referencias no leen el disco.
                Assert.Equal("20260102030405", System.Web.HttpRuntime.Cache["RecursoHelper.Version:" + archivo.ToLowerInvariant()]);

                // Publicación: el archivo cambia y la entrada guardada se descarta (la dependencia avisa en segundo plano).
                File.WriteAllText(archivo, "b{}");
                File.SetLastWriteTimeUtc(archivo, new DateTime(2026, 6, 7, 8, 9, 10, DateTimeKind.Utc));
                string nueva = null;
                for (int i = 0; i < 100 && nueva != "20260607080910"; i++)
                {
                    Thread.Sleep(100);
                    nueva = RecursoHelper.Version(archivo);
                }
                Assert.Equal("20260607080910", nueva);
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { /* temporal */ }
            }
        }
    }
}
