using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Fija lo esencial del CI (E4-9): token de solo lectura, acciones fijadas por SHA,
    /// auditoría de NuGet y de JS de terceros, corrida semanal y Dependabot.
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
