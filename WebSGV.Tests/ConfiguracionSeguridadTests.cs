using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Configuración de seguridad (auditoría de seguridad, tarea 6):
    /// - Las cabeceras de seguridad se envían desde Web.config, en un solo lugar (no desde C#).
    /// - Sin cabecera de versión de ASP.NET ni caracteres de ruta peligrosos permitidos.
    /// - Sin el manejador ChartImg.axd (DataVisualization no se usa).
    /// - Release: errores remotos a Error.aspx y HSTS añadido sin reemplazar las cabeceras base.
    /// - Las páginas de prueba / duplicadas ya no existen.
    /// </summary>
    public class ConfiguracionSeguridadTests
    {
        private static readonly XNamespace Xdt = "http://schemas.microsoft.com/XML-Document-Transform";

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static XDocument Cargar(string archivo) => XDocument.Load(Path.Combine(RaizWeb(), archivo));

        [Theory]
        [InlineData("X-Frame-Options", "SAMEORIGIN")]
        [InlineData("X-Content-Type-Options", "nosniff")]
        // No "same-origin": las teselas de OpenStreetMap exigen Referer (si no, 403 "Access blocked").
        [InlineData("Referrer-Policy", "strict-origin-when-cross-origin")]
        public void WebConfig_EnviaCabeceraDeSeguridadUnaVez(string nombre, string valor)
        {
            XElement cabeceras = Cargar("Web.config").Root
                .Element("system.webServer").Element("httpProtocol").Element("customHeaders");
            Assert.NotNull(cabeceras);

            var agregadas = cabeceras.Elements("add").Where(a => (string)a.Attribute("name") == nombre).ToList();
            Assert.Single(agregadas);
            Assert.Equal(valor, (string)agregadas[0].Attribute("value"));

            // Su <remove> va antes del <add>, para no duplicarla si IIS ya la trae.
            XElement quitar = cabeceras.Elements("remove").FirstOrDefault(r => (string)r.Attribute("name") == nombre);
            Assert.NotNull(quitar);
            Assert.True(quitar.IsBefore(agregadas[0]));
        }

        [Fact]
        public void WebConfig_SinVersionDeAspNetNiCaracteresDeRutaPermitidos()
        {
            XElement httpRuntime = Cargar("Web.config").Root.Element("system.web").Element("httpRuntime");
            Assert.NotNull(httpRuntime);
            Assert.Equal("false", (string)httpRuntime.Attribute("enableVersionHeader"));
            Assert.Null(httpRuntime.Attribute("requestPathInvalidCharacters"));
        }

        [Fact]
        public void WebConfig_SinChartImgNiDataVisualization()
        {
            string texto = File.ReadAllText(Path.Combine(RaizWeb(), "Web.config"));
            Assert.DoesNotContain("ChartImg", texto);
            Assert.DoesNotContain("DataVisualization", texto);

            string csproj = File.ReadAllText(Path.Combine(RaizWeb(), "WebSGV.csproj"));
            Assert.DoesNotContain("System.Web.DataVisualization", csproj);
        }

        [Fact]
        public void WebConfigRelease_ErroresRemotosVanAErrorAspx()
        {
            XElement customErrors = Cargar("Web.Release.config").Root.Element("system.web").Element("customErrors");
            Assert.NotNull(customErrors);
            Assert.Equal("RemoteOnly", (string)customErrors.Attribute("mode"));
            Assert.Equal("~/Views/Error.aspx", (string)customErrors.Attribute("defaultRedirect"));

            string transformacion = (string)customErrors.Attribute(Xdt + "Transform");
            Assert.NotNull(transformacion);
            Assert.Contains("defaultRedirect", transformacion);
            Assert.Contains("mode", transformacion);
        }

        [Fact]
        public void WebConfigRelease_AgregaHstsSinReemplazarLasCabecerasBase()
        {
            XElement httpProtocol = Cargar("Web.Release.config").Root.Element("system.webServer").Element("httpProtocol");
            Assert.NotNull(httpProtocol);
            Assert.Null(httpProtocol.Attribute(Xdt + "Transform"));

            XElement hsts = httpProtocol.Element("customHeaders").Elements("add")
                .SingleOrDefault(a => (string)a.Attribute("name") == "Strict-Transport-Security");
            Assert.NotNull(hsts);
            Assert.Equal("Insert", (string)hsts.Attribute(Xdt + "Transform"));
        }

        /// <summary>
        /// Tarea 7: la BD de pruebas la comparte el sitio publicado (DLL que solo lee el formato de 3
        /// partes), así que local/Debug NO convierte hashes al iniciar sesión; Release sí.
        /// </summary>
        [Fact]
        public void ConversionDeHashAlIngresar_FalseEnWebConfig_TrueEnRelease()
        {
            XElement baseAdd = Cargar("Web.config").Root.Element("appSettings").Elements("add")
                .Single(a => (string)a.Attribute("key") == "Seguridad.ConvertirHashAlIngresar");
            Assert.Equal("false", (string)baseAdd.Attribute("value"));

            XElement releaseAdd = Cargar("Web.Release.config").Root.Element("appSettings").Elements("add")
                .Single(a => (string)a.Attribute("key") == "Seguridad.ConvertirHashAlIngresar");
            Assert.Equal("true", (string)releaseAdd.Attribute("value"));
            Assert.Equal("SetAttributes(value)", (string)releaseAdd.Attribute(Xdt + "Transform"));
            Assert.Equal("Match(key)", (string)releaseAdd.Attribute(Xdt + "Locator"));

            XElement iteraciones = Cargar("Web.config").Root.Element("appSettings").Elements("add")
                .Single(a => (string)a.Attribute("key") == "Seguridad.IteracionesContrasena");
            Assert.Equal("310000", (string)iteraciones.Attribute("value"));
        }

        [Theory]
        [InlineData("TestGenerarPdfOrdenViaje")]
        [InlineData("ConsultaAuditoria")]
        public void PaginaRetirada_NoExisteNiEstaEnElProyecto(string pagina)
        {
            string raiz = RaizWeb();
            Assert.False(File.Exists(Path.Combine(raiz, "Views", pagina + ".aspx")));
            Assert.False(File.Exists(Path.Combine(raiz, "Views", pagina + ".aspx.cs")));

            string csproj = File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj"));
            Assert.DoesNotContain(pagina, csproj);
        }

        [Fact]
        public void CodigoCSharp_NoAgregaXFrameOptions()
        {
            string raiz = RaizWeb();
            var conCabecera = new[] { "Views", "Helpers" }
                .SelectMany(carpeta => Directory.GetFiles(Path.Combine(raiz, carpeta), "*.cs", SearchOption.AllDirectories))
                .Where(f => File.ReadAllText(f).Contains("X-Frame-Options"))
                .Select(f => f.Substring(raiz.Length + 1))
                .ToList();
            Assert.Empty(conCabecera);
        }

        // --- Límites de envío (tarea 14) ---

        private static XElement Ubicacion(string ruta) =>
            Cargar("Web.config").Root.Elements("location").SingleOrDefault(l => (string)l.Attribute("path") == ruta);

        [Theory]
        [InlineData("Views/RegistroDespacho.aspx", 25600)]
        [InlineData("Views/ListaDespachos.aspx", 25600)]
        [InlineData("Views/BandejaDocumentos.aspx", 51200)]
        [InlineData("Views/FacturasFlete.aspx", 51200)]
        [InlineData("Views/Exportacion/ImportarSeguimiento.aspx", 12288)]
        [InlineData("Views/MovimientosViaje.aspx", 6144)]
        public void WebConfig_LimiteDeEnvio_IisPorEncimaDeAspNet(string ruta, int kbEsperados)
        {
            XElement ubicacion = Ubicacion(ruta);
            Assert.NotNull(ubicacion);

            int kbAspNet = (int)ubicacion.Element("system.web").Element("httpRuntime").Attribute("maxRequestLength");
            long bytesIis = (long)ubicacion.Element("system.webServer").Element("security").Element("requestFiltering")
                .Element("requestLimits").Attribute("maxAllowedContentLength");

            Assert.Equal(kbEsperados, kbAspNet);
            // IIS debe dejar pasar más que ASP.NET: así el envío demasiado grande lo corta ASP.NET
            // (Global.asax lo devuelve a la página con ?aviso=archivo_grande) y no IIS con un 404.13.
            Assert.True(bytesIis > kbAspNet * 1024L, $"{ruta}: IIS ({bytesIis} bytes) debe ser mayor que ASP.NET ({kbAspNet} KB)");
        }
    }
}
