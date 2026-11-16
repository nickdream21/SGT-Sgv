using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// El código de la web registra con <c>LogSGV</c> (Serilog: ventana Output y archivo en
    /// App_Data/logs). <c>Debug.WriteLine</c>/<c>Trace</c>/<c>Console</c> no dejan rastro en producción
    /// y llevaban a catch que "registraban" un error que nadie podía ver (auditoría de seguridad, 8D-1).
    /// </summary>
    public class CodigoFuenteLimpiezaTests
    {
        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static readonly string[] Extensiones = { "*.cs", "*.aspx", "*.ascx", "*.Master", "*.ashx" };
        private static readonly string[] CarpetasExcluidas = { "obj", "bin", "Database" };

        /// <summary>Archivos (ruta relativa a WebSGV, con '/') que pueden usar estas APIs y por qué. Vacío.</summary>
        private static readonly Dictionary<string, string> Excepciones =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex LlamadaProhibida = new Regex(
            @"\b(System\.Diagnostics\.)?(Debug|Trace)\.(Write|WriteLine|WriteIf|WriteLineIf|Print|TraceError|TraceWarning|TraceInformation)\b" +
            @"|\bConsole\.(Write|WriteLine|Error)\b" +
            // using static System.Diagnostics.Debug; / using D = System.Diagnostics.Debug;
            // permitirían llamar a WriteLine(...) sin el prefijo que detecta la expresión anterior.
            @"|^using\s+(static\s+)?(\w+\s*=\s*)?System\.Diagnostics\.(Debug|Trace)\b");

        private static IEnumerable<string> ArchivosFuente(string raiz)
        {
            return Extensiones
                .SelectMany(ext => Directory.GetFiles(raiz, ext, SearchOption.AllDirectories))
                .Where(f =>
                {
                    string relativo = f.Substring(raiz.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string primera = relativo.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                    return !CarpetasExcluidas.Contains(primera, StringComparer.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> BuscarLlamadas(string raiz)
        {
            var hallazgos = new List<string>();
            foreach (string archivo in ArchivosFuente(raiz))
            {
                string relativo = archivo.Substring(raiz.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (Excepciones.ContainsKey(relativo)) continue;

                string[] lineas = File.ReadAllLines(archivo);
                for (int i = 0; i < lineas.Length; i++)
                {
                    string linea = lineas[i].TrimStart();
                    if (linea.StartsWith("//", StringComparison.Ordinal)) continue; // también ///
                    if (LlamadaProhibida.IsMatch(linea))
                        hallazgos.Add($"{relativo}:{i + 1}: {linea.Trim()}");
                }
            }
            return hallazgos;
        }

        [Fact]
        public void CodigoFuente_NoUsaDebugTraceNiConsole()
        {
            string raiz = RaizWeb();
            Assert.True(ArchivosFuente(raiz).Count() > 100, "No se encontraron los fuentes de WebSGV.");

            List<string> hallazgos = BuscarLlamadas(raiz);

            Assert.True(hallazgos.Count == 0,
                "Usa LogSGV.Info/Advertencia/Error en lugar de Debug/Trace/Console " +
                "(o MensajeErrorHelper.ParaUsuario(ex), que ya registra el error):" +
                Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }
    }
}
