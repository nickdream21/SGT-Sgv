using System;
using System.Data.SqlClient;
using System.Diagnostics;
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
        // E4-5
        [InlineData("Content-Security-Policy", "frame-ancestors 'self'; object-src 'none'; base-uri 'self'; form-action 'self'")]
        [InlineData("Content-Security-Policy-Report-Only", "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob: https://tile.openstreetmap.org; font-src 'self' data:; connect-src 'self'; frame-src 'self'; worker-src 'self' blob:; report-uri /CspInforme.ashx")]
        [InlineData("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=(), usb=(), clipboard-write=(self)")]
        [InlineData("Cross-Origin-Opener-Policy", "same-origin")]
        [InlineData("Cross-Origin-Resource-Policy", "same-origin")]
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

        /// <summary>
        /// E4-5: solo report-uri. Con report-to (y Reporting-Endpoints) Chrome ignora report-uri y la Reporting API
        /// entrega solo por https y en lotes: los informes dejarían de llegar al momento.
        /// </summary>
        [Fact]
        public void WebConfig_CspSinReportToNiReportingEndpoints()
        {
            XElement cabeceras = Cargar("Web.config").Root
                .Element("system.webServer").Element("httpProtocol").Element("customHeaders");
            Assert.DoesNotContain(cabeceras.Elements("add"), a => (string)a.Attribute("name") == "Reporting-Endpoints");
            Assert.DoesNotContain(cabeceras.Elements("add"), a => ((string)a.Attribute("value") ?? "").Contains("report-to"));
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

        /// <summary>E4-8: trace apagado y SameSite=Lax explícitos en la cookie de sesión y en las demás.</summary>
        [Fact]
        public void WebConfig_TraceApagadoYSameSiteLaxExplicitos()
        {
            XElement web = Cargar("Web.config").Root.Element("system.web");
            Assert.Equal("false", (string)web.Element("trace")?.Attribute("enabled"));
            Assert.Equal("Lax", (string)web.Element("sessionState")?.Attribute("cookieSameSite"));
            Assert.Equal("Lax", (string)web.Element("httpCookies")?.Attribute("sameSite"));
            Assert.Equal("true", (string)web.Element("httpCookies")?.Attribute("httpOnlyCookies"));
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
        [InlineData("Dashboard")]
        [InlineData("AgregarIndicadores")]
        [InlineData("DashboardGrifo")]
        [InlineData("AgregarAbastecimiento")]
        [InlineData("BuscarAbastecimiento")]
        [InlineData("ReporteAbastecimiento")]
        [InlineData("RegistrarDespachoObra")]
        [InlineData("RegistrarRetornoEcuador")]
        [InlineData("DescargarPdfAbastecimiento")]
        [InlineData("AsignacionesMaquinaria")]
        [InlineData("DashboardOperador")]
        [InlineData("RegistroOperadores")]
        [InlineData("RegistroEquiposMaquinaria")]
        [InlineData("RegistroObras")]
        [InlineData("RegistroClientesObra")]
        public void PaginaRetirada_NoExisteNiEstaEnElProyecto(string pagina)
        {
            string raiz = RaizWeb();
            Assert.False(File.Exists(Path.Combine(raiz, "Views", pagina + ".aspx")));
            Assert.False(File.Exists(Path.Combine(raiz, "Views", pagina + ".aspx.cs")));

            // Nombre exacto: "Dashboard" es prefijo de DashboardConductor, DashboardExportacion, etc., que siguen.
            string csproj = File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj"));
            Assert.DoesNotContain("Views\\" + pagina + ".aspx\"", csproj);
            Assert.DoesNotContain("Views\\" + pagina + ".aspx.cs\"", csproj);
            Assert.DoesNotContain("paginas\\" + pagina + ".css\"", csproj);
            Assert.DoesNotContain("paginas\\" + pagina + ".js\"", csproj);
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

        /// <summary>
        /// La publicación (carpeta, FTP, Web Deploy) nunca sube los datos locales (App_Data: PDFs
        /// firmados; Uploads: adjuntos) ni los .sql de Database\, aunque un .pubxml local diga
        /// ExcludeApp_Data=false: el candado vive en el csproj versionado.
        /// </summary>
        [Fact]
        public void PublicacionExcluyeAppDataUploadsYDatabase()
        {
            XDocument csproj = Cargar("WebSGV.csproj");
            XNamespace ns = csproj.Root.Name.Namespace;

            var excluidas = csproj.Descendants(ns + "ExcludeFromPackageFolders")
                .SelectMany(e => ((string)e.Attribute("Include") ?? "").Split(';'))
                .Select(c => c.Trim().TrimEnd('\\'))
                .ToList();
            foreach (string carpeta in new[] { "App_Data", "Uploads", "Database" })
                Assert.Contains(carpeta, excluidas, StringComparer.OrdinalIgnoreCase);

            var contenidoDeDatos = csproj.Descendants(ns + "Content")
                .Select(e => (string)e.Attribute("Include") ?? "")
                .Where(i => i.StartsWith("App_Data\\", StringComparison.OrdinalIgnoreCase)
                         || i.StartsWith("Uploads\\", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Empty(contenidoDeDatos);
        }

        // ---- E4-10: conexión cifrada a la BD -----------------------------------------------------

        private static string RaizRepo() => Path.GetDirectoryName(RaizWeb());

        [Fact]
        public void PlantillaDeConexion_LlevaEncryptTrue()
        {
            string cs = Cargar("connectionStrings.config.example").Descendants("add")
                .Where(e => (string)e.Attribute("name") == "ConexionSGV")
                .Select(e => (string)e.Attribute("connectionString"))
                .Single();
            var b = new SqlConnectionStringBuilder(cs.Replace("<", "").Replace(">", ""));
            Assert.True(b.Encrypt, "La plantilla debe llevar Encrypt=True.");
            // El certificado de somee no coincide con el nombre del servidor: sin esto no conecta.
            Assert.True(b.TrustServerCertificate);
        }

        [Fact]
        public void RespaldarBd_ExportaConConexionCifrada()
        {
            string texto = File.ReadAllText(Path.Combine(RaizRepo(), "herramientas", "respaldo", "respaldar-bd.ps1"));
            Assert.Contains("'/SourceEncryptConnection:True'", texto);
            Assert.Contains("'/SourceTrustServerCertificate:True'", texto);
        }

        /// <summary>
        /// Ejecuta conexion-bd.ps1 de verdad (Windows PowerShell) con un connectionStrings.config
        /// falso en una carpeta temporal: no se conecta a nada. Pruebas y producción salen con
        /// Encrypt=True (también si el archivo decía False o no decía nada), TrustServerCertificate
        /// como venía y -N en sqlcmd; ensayo (SQL local) no cambia.
        /// </summary>
        [Theory]
        [InlineData("Encrypt=False;TrustServerCertificate=True;", true)]
        [InlineData("TrustServerCertificate=True;", false)]
        [InlineData("Encrypt=True;", false)]
        public void ConexionBd_FuerzaEncryptTrueEnSomee(string extra, bool avisa)
        {
            string dir = Path.Combine(Path.GetTempPath(), "sgv-e410-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "connectionStrings.config"),
                    "<connectionStrings><add name=\"ConexionSGV\" connectionString=\"Data Source=ejemplo.mssql.somee.com;" +
                    "Initial Catalog=sgvActualizada;User ID=u;Password=p;" + extra + "\" /></connectionStrings>");
                string script = Path.Combine(RaizWeb(), "Database", "conexion-bd.ps1").Replace("'", "''");
                string comando =
                    "$ErrorActionPreference='Stop'; . '" + script + "'; " +
                    "$c = Get-ConexionSgv -Entorno pruebas -DirWeb '" + dir.Replace("'", "''") + "' 3>&1 | ForEach-Object { if ($_ -is [System.Management.Automation.WarningRecord]) { 'AVISO ' + $_.Message } else { $_ } }; " +
                    "$c | Where-Object { $_ -is [string] }; $con = $c | Where-Object { $_ -isnot [string] }; " +
                    "'CADENA ' + $con.Cadena; 'SQLCMD ' + ((Get-ArgumentosSqlcmd $con) -join ' '); " +
                    "$e = Get-ConexionSgv -Entorno ensayo -BaseDeDatos sgvEnsayo_x -DirWeb '" + dir.Replace("'", "''") + "'; " +
                    "'ENSAYO ' + $e.Cadena; 'SQLCMDENSAYO ' + ((Get-ArgumentosSqlcmd $e) -join ' ')";
                string salida = EjecutarPowerShell(comando);
                string[] lineas = salida.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                string Linea(string prefijo) => lineas.Single(l => l.StartsWith(prefijo + " ", StringComparison.Ordinal)).Substring(prefijo.Length + 1);

                var b = new SqlConnectionStringBuilder(Linea("CADENA"));
                Assert.True(b.Encrypt, salida);
                Assert.True(b.TrustServerCertificate == extra.Contains("TrustServerCertificate=True"), salida);
                Assert.Equal("sgvActualizada", b.InitialCatalog);
                Assert.Contains(" -N", " " + Linea("SQLCMD"));
                Assert.Equal(avisa, lineas.Any(l => l.StartsWith("AVISO ", StringComparison.Ordinal) && l.Contains("Encrypt=False")));

                var e = new SqlConnectionStringBuilder(Linea("ENSAYO"));
                Assert.False(e.Encrypt, salida);
                Assert.DoesNotContain(" -N", " " + Linea("SQLCMDENSAYO"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static string EjecutarPowerShell(string comando)
        {
            string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " +
                    Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(comando)))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var proceso = Process.Start(info))
            {
                var salida = proceso.StandardOutput.ReadToEndAsync();
                var errores = proceso.StandardError.ReadToEndAsync();
                Assert.True(proceso.WaitForExit(60000), "powershell.exe no terminó en 60 s.");
                Assert.True(proceso.ExitCode == 0, "powershell.exe salió con " + proceso.ExitCode + ": " + errores.Result + salida.Result);
                return salida.Result;
            }
        }
    }
}
