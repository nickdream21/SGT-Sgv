using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// M-102 (E6-20): todo archivo de <c>Database/StoredProcedures</c> está registrado como <c>&lt;Content&gt;</c> en
    /// WebSGV.csproj (se ve en Visual Studio y viaja con el proyecto), y nada registrado ahí falta en disco. No se
    /// publican: el paquete excluye <c>Database</c> (<c>ExcludeFromPackageFolders</c>) y publicar.ps1 lo comprueba.
    /// </summary>
    public class ProcedimientosEnCsprojTests
    {
        private static readonly Regex Registrado = new Regex(
            @"<Content\s+Include=""Database\\StoredProcedures\\(?<a>[^""]+)""\s*/>", RegexOptions.IgnoreCase);

        [Fact]
        public void CadaSp_EstaRegistradoEnElCsproj_YNadaRegistradoFalta()
        {
            string raiz = FuentesWeb.RaizWeb();
            string csproj = File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj"));
            var registrados = Registrado.Matches(csproj).Cast<Match>().Select(m => m.Groups["a"].Value)
                .ToList();
            var enDisco = Directory.GetFiles(Path.Combine(raiz, "Database", "StoredProcedures"), "*.sql")
                .Select(Path.GetFileName).ToList();

            var sinRegistrar = enDisco.Where(f => !registrados.Contains(f, StringComparer.OrdinalIgnoreCase))
                .Select(f => f + ": falta <Content Include=\"Database\\StoredProcedures\\" + f + "\" /> en WebSGV.csproj");
            var sinArchivo = registrados.Where(f => !enDisco.Contains(f, StringComparer.OrdinalIgnoreCase))
                .Select(f => f + ": registrado en WebSGV.csproj pero no está en Database/StoredProcedures");
            var repetidos = registrados.GroupBy(f => f, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
                .Select(g => g.Key + ": registrado " + g.Count() + " veces");

            var fallas = sinRegistrar.Concat(sinArchivo).Concat(repetidos).ToList();
            Assert.True(enDisco.Count > 100, "Se esperaban más de 100 SP en Database/StoredProcedures (hay " + enDisco.Count + ").");
            Assert.True(fallas.Count == 0, string.Join("\n", fallas));
        }

        [Fact]
        public void Database_NoSePublica()
        {
            string csproj = File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), "WebSGV.csproj"));
            Assert.Matches(new Regex(@"<ExcludeFromPackageFolders\s+Include=""[^""]*\bDatabase\b"), csproj);
        }
    }
}
