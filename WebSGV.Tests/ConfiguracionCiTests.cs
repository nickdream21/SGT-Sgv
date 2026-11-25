using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Fija lo esencial del CI (E4-9): token de solo lectura, acciones fijadas por SHA,
    /// auditoría de NuGet y de JS de terceros, corrida semanal y Dependabot; y la cobertura con
    /// su tope (E6-1); los avisos como error en los dos proyectos (E6-2); y los analizadores con sus
    /// reglas en error y el trinquete en CI (E6-3).
    /// Lee los YAML como texto para no añadir dependencias.
    /// </summary>
    public class ConfiguracionCiTests
    {
        private static string Leer(params string[] ruta)
        {
            string raiz = BaseDePruebas.RaizRepo;
            Assert.NotNull(raiz);
            string archivo = Path.Combine(raiz, Path.Combine(ruta));
            Assert.True(File.Exists(archivo), "No existe " + archivo);
            return File.ReadAllText(archivo);
        }

        private static string Workflow() => Leer(".github", "workflows", "build.yml");

        [Fact]
        public void Workflow_TokenDeSoloLectura()
        {
            // A nivel de workflow (sin sangría), no solo en un job.
            Assert.Matches(new Regex(@"^permissions:\s*\r?\n\s+contents:\s*read\s*$", RegexOptions.Multiline), Workflow());
            Assert.DoesNotMatch(new Regex(@":\s*write\b"), Workflow());
        }

        [Fact]
        public void Workflow_AccionesFijadasPorSha()
        {
            var usos = Regex.Matches(Workflow(), @"^\s*-?\s*uses:\s*(\S+)", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(usos);
            var sinSha = usos.Where(u => !Regex.IsMatch(u, @"^[\w.-]+/[\w./-]+@[0-9a-f]{40}$")).ToList();
            Assert.True(sinSha.Count == 0, "Acciones sin SHA completo: " + string.Join(", ", sinSha));
        }

        [Fact]
        public void Workflow_AuditaNuGetYFallaEnAltaYCritica()
        {
            string w = Workflow();
            var paso = Regex.Match(w, @"msbuild\s+WebSGV\.sln\s+/t:Restore[^\r\n]*");
            Assert.True(paso.Success, "Falta el paso msbuild /t:Restore");
            foreach (string parte in new[] { "/p:RestorePackagesConfig=true", "/p:NuGetAudit=true",
                "/p:NuGetAuditMode=all", "/p:NuGetAuditLevel=low", "/warnaserror:NU1903,NU1904" })
                Assert.Contains(parte, paso.Value);
        }

        [Fact]
        public void Workflow_AuditaJsDeTercerosConExcepcionesVersionadas()
        {
            string w = Workflow();
            // Versión fija de retire y una corrida por carpeta (retire solo usa el último --path).
            Assert.Matches(new Regex(@"npx --yes retire@\d+\.\d+\.\d+ --path WebSGV/Scripts/lib --ignorefile \.retireignore\.json"), w);
            Assert.Matches(new Regex(@"npx --yes retire@\d+\.\d+\.\d+ --path WebSGV/Content/lib --ignorefile \.retireignore\.json"), w);

            string ignorar = Leer(".retireignore.json");
            Assert.Contains("\"justification\"", ignorar);
            Assert.Contains("Revisar antes del", ignorar);
        }

        [Fact]
        public void Workflow_CorridaSemanalYManual()
        {
            string w = Workflow();
            Assert.Matches(new Regex(@"^\s+schedule:\s*\r?\n\s+-\s*cron:\s*'[^']+'", RegexOptions.Multiline), w);
            Assert.Matches(new Regex(@"^\s+workflow_dispatch:", RegexOptions.Multiline), w);
        }

        [Fact]
        public void Workflow_CoberturaConTopeYArtefacto()
        {
            string w = Workflow();

            // Las pruebas recolectan cobertura con el runsettings y dejan el XML en una carpeta fija.
            var pruebas = Regex.Match(w, @"dotnet test WebSGV\.Tests/WebSGV\.Tests\.csproj[^\r\n]*");
            Assert.True(pruebas.Success, "Falta el paso dotnet test");
            Assert.Contains("--settings WebSGV.Tests/cobertura.runsettings", pruebas.Value);
            Assert.Contains("--collect \"Code Coverage;Format=cobertura\"", pruebas.Value);
            Assert.Contains("--results-directory TestResults/cobertura", pruebas.Value);

            // Comparación con el mínimo (falla el CI si baja).
            Assert.Matches(new Regex(@"medir-cobertura\.ps1 -Directorio TestResults/cobertura -Comparar"), w);

            // Artefacto con la acción fijada por SHA, subido aunque falle el tope.
            Assert.Matches(new Regex(
                @"-\s*name:\s*Subir cobertura\s*\r?\n\s+if:\s*always\(\)\s*\r?\n\s+uses:\s*actions/upload-artifact@[0-9a-f]{40}\s+#\s*v\d+\.\d+\.\d+\s*\r?\n\s+with:\s*\r?\n(\s+\S[^\r\n]*\r?\n)*?\s+path:\s*TestResults/cobertura/"), w);

            // Los archivos que usa existen y el runsettings mide solo WebSGV.dll.
            string runsettings = Leer("WebSGV.Tests", "cobertura.runsettings");
            Assert.Contains("friendlyName=\"Code Coverage\"", runsettings);
            Assert.Contains(@"WebSGV\.dll$", runsettings);
            Assert.Contains(@".*\.designer\.cs$", runsettings);
            Leer("herramientas", "calidad", "medir-cobertura.ps1");
            string minimos = Leer("herramientas", "calidad", "cobertura-minima.json");
            foreach (string clave in new[] { "\"total\"", "\"Services\"", "\"Helpers\"" })
                Assert.Contains(clave, minimos);
        }

        [Theory]
        [InlineData("WebSGV", "WebSGV.csproj")]
        [InlineData("WebSGV.Tests", "WebSGV.Tests.csproj")]
        public void Proyectos_AvisosComoError(string carpeta, string archivo)
        {
            // E6-2: en un PropertyGroup sin condición (vale para Debug y Release), sin silenciar avisos
            // y con la auditoría NuGet fuera (la decide el CI con /warnaserror:NU1903,NU1904).
            var xml = XDocument.Parse(Leer(carpeta, archivo));
            var comunes = xml.Descendants().Where(e => e.Name.LocalName == "PropertyGroup"
                && e.Attribute("Condition") == null).SelectMany(g => g.Elements()).ToList();
            Assert.Contains(comunes, e => e.Name.LocalName == "TreatWarningsAsErrors" && e.Value.Trim() == "true");
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "TreatWarningsAsErrors" && e.Value.Trim() != "true");
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "NoWarn");
            var noComoError = comunes.Where(e => e.Name.LocalName == "WarningsNotAsErrors")
                .SelectMany(e => e.Value.Split(';')).Select(s => s.Trim()).ToList();
            foreach (string codigo in new[] { "NU1901", "NU1902" })
                Assert.Contains(codigo, noComoError);
            Assert.All(noComoError, c => Assert.StartsWith("NU19", c));
        }

        [Fact]
        public void Analizadores_ReglasEnErrorYTrinqueteEnCi()
        {
            // E6-3: el paquete de analizadores (solo de desarrollo) y sus dos DLL en el csproj.
            string paquetes = Leer("WebSGV", "packages.config");
            Assert.Matches(new Regex(@"<package id=""Microsoft\.CodeAnalysis\.NetAnalyzers"" version=""[\d.]+""[^>]*developmentDependency=""true"""), paquetes);
            var csproj = XDocument.Parse(Leer("WebSGV", "WebSGV.csproj"));
            var analizadores = csproj.Descendants().Where(e => e.Name.LocalName == "Analyzer")
                .Select(e => (string)e.Attribute("Include")).ToList();
            Assert.Contains(analizadores, a => a.EndsWith(@"analyzers\dotnet\Microsoft.CodeAnalysis.NetAnalyzers.dll"));
            Assert.Contains(analizadores, a => a.EndsWith(@"analyzers\dotnet\cs\Microsoft.CodeAnalysis.CSharp.NetAnalyzers.dll"));
            // Si falta cualquiera de las dos, el build lo dice claro (EnsureNuGetPackageBuildImports).
            var faltaPaquete = csproj.Descendants().Where(e => e.Name.LocalName == "Target"
                    && (string)e.Attribute("Name") == "EnsureNuGetPackageBuildImports")
                .SelectMany(t => t.Elements()).Where(e => e.Name.LocalName == "Error")
                .Select(e => (string)e.Attribute("Condition") ?? "").ToList();
            foreach (string dll in analizadores)
                Assert.Contains(faltaPaquete, c => c.Contains("!Exists('" + dll + "')"));

            // SGV.globalconfig siempre; el del trinquete solo al medir. Ninguno se publica.
            var configs = csproj.Descendants().Where(e => e.Name.LocalName == "GlobalAnalyzerConfigFiles").ToList();
            Assert.Contains(configs, e => (string)e.Attribute("Include") == "SGV.globalconfig" && e.Attribute("Condition") == null);
            Assert.Contains(configs, e => (string)e.Attribute("Include") == "SGV.trinquete.globalconfig"
                && ((string)e.Attribute("Condition") ?? "").Contains("'$(SgvContarAvisos)' == 'true'"));
            Assert.DoesNotContain(csproj.Descendants(), e => e.Name.LocalName == "Content"
                && ((string)e.Attribute("Include") ?? "").EndsWith(".globalconfig"));

            // Todo apagado salvo las 6 de seguridad, en error.
            string global = Leer("WebSGV", "SGV.globalconfig");
            Assert.Matches(new Regex(@"^is_global\s*=\s*true\s*$", RegexOptions.Multiline), global);
            Assert.Matches(new Regex(@"^dotnet_analyzer_diagnostic\.severity\s*=\s*none\s*$", RegexOptions.Multiline), global);
            var enError = new[] { "CA2100", "CA3007", "CA5350", "CA5368", "CA2213", "CA2025" };
            foreach (string regla in enError)
                Assert.Matches(new Regex(@"^dotnet_diagnostic\." + regla + @"\.severity\s*=\s*error\s*$", RegexOptions.Multiline), global);
            var conSeveridad = Regex.Matches(global, @"^dotnet_diagnostic\.(\w+)\.severity", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(enError.OrderBy(r => r), conSeveridad.OrderBy(r => r));

            // Las 5 del trinquete: aviso al medir y con su cifra en la base.
            var trinquete = new[] { "CA1031", "CA2000", "CA3002", "CA3003", "CA1062" };
            string medir = Leer("WebSGV", "SGV.trinquete.globalconfig");
            string baseAvisos = Leer("herramientas", "calidad", "avisos-base.json");
            foreach (string regla in trinquete)
            {
                Assert.Matches(new Regex(@"^dotnet_diagnostic\." + regla + @"\.severity\s*=\s*warning\s*$", RegexOptions.Multiline), medir);
                Assert.Matches(new Regex("\"" + regla + @"""\s*:\s*\d+"), baseAvisos);
            }

            // El CI corre el script tras compilar.
            string w = Workflow();
            // Trinquete estricto: también falla si una regla baja sin bajar la base.
            string script = Leer("herramientas", "calidad", "contar-avisos.ps1");
            Assert.Matches(new Regex(@"if \(\$bajan\.Count -gt 0\) \{\s*\r?\n(?:[^\r\n]*\r?\n){0,3}?[^\r\n]*Corre -Actualizar[^\r\n]*\r?\n\s*exit 1", RegexOptions.Singleline), script);
            int compilar = w.IndexOf("msbuild WebSGV.sln /t:Build");
            var paso = Regex.Match(w, @"-\s*name:\s*Contar avisos del trinquete\s*\r?\n\s+shell:\s*pwsh\s*\r?\n\s+run:\s*\./herramientas/calidad/contar-avisos\.ps1\s*$", RegexOptions.Multiline);
            Assert.True(paso.Success, "Falta el paso que corre contar-avisos.ps1");
            Assert.True(compilar >= 0 && paso.Index > compilar, "contar-avisos.ps1 debe correr después de compilar");
        }

        [Fact]
        public void Dependabot_NuGetYGitHubActions()
        {
            string d = Leer(".github", "dependabot.yml");
            Assert.Matches(new Regex(@"package-ecosystem:\s*nuget\b"), d);
            Assert.Matches(new Regex(@"package-ecosystem:\s*github-actions\b"), d);
            Assert.Contains("version-update:semver-major", d);
            Assert.Contains("dependency-name: QuestPDF", d);
            Assert.Contains("dependency-name: ClosedXML", d);
        }
    }
}
