using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Etapa 4, E4-1 (el repositorio de GitHub es público): ningún archivo versionado (o por
    /// versionar) contiene un DNI, licencia, carnet de extranjería o teléfono de un conductor real.
    /// Cruza los números de 8 a 9 cifras (con o sin letra inicial, forma de licencia peruana) de los
    /// archivos de texto con la tabla Conductor de la BD de pruebas. SOLO LECTURA. El mensaje de
    /// fallo da archivo:línea y la columna que coincide, NUNCA el valor.
    /// </summary>
    public class DatosPersonalesRepoTests
    {
        public DatosPersonalesRepoTests() => BaseDePruebas.Preparar();

        private static readonly HashSet<string> ExtensionesTexto = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".aspx", ".ascx", ".mjs", ".cjs", ".ts", ".master", ".asax", ".ashx", ".asmx", ".js", ".css", ".sql", ".ps1", ".psm1",
            ".md", ".config", ".json", ".txt", ".xml", ".csproj", ".sln", ".html", ".htm", ".yml", ".yaml",
            ".csv", ".cshtml", ".resx", ".bat", ".cmd", ".sh", ".py", ".props", ".targets", ".editorconfig",
            ".gitignore", ".gitattributes", ".example", ".tt", ".settings", ".svg"
        };

        // Letra opcional (licencia peruana: Q12345678) + 8 o 9 cifras, sin más cifras ni letras pegadas.
        private static readonly Regex Numero = new Regex(@"(?<![A-Za-z0-9])([A-Za-z]?)(\d{8,9})(?!\d)", RegexOptions.Compiled);

        [FactBD]
        public void Ningun_archivo_versionado_tiene_datos_de_un_conductor_real()
        {
            List<string> archivos = ArchivosVersionados(out string motivo);
            if (archivos == null)
            {
                // Sin git no se sabe qué está versionado. Falla a la vista en vez de pasar sin revisar nada
                // (xUnit 2.9 no permite omitir en tiempo de ejecución; las [FactBD] solo corren en PC con el repo).
                Assert.Fail("No se pudo listar los archivos versionados con git: " + motivo);
            }

            // valor normalizado -> columnas donde aparece (nunca se imprime el valor)
            var reales = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            DataTable dt = DbHelper.ConsultarTabla(
                "SELECT DNI, numeroLicencia, telefono, carnetExtranjeria FROM dbo.Conductor");
            foreach (DataRow r in dt.Rows)
            {
                Agregar(reales, Normalizar(r["DNI"]), "DNI");
                string lic = Normalizar(r["numeroLicencia"]);
                Agregar(reales, lic, "licencia");
                // Licencia guardada con letra: también su parte numérica (suele ser el DNI).
                if (lic != null && lic.Length > 1 && char.IsLetter(lic[0])) Agregar(reales, lic.Substring(1), "licencia");
                Agregar(reales, SoloUltimas9(r["telefono"]), "teléfono");
                Agregar(reales, Normalizar(r["carnetExtranjeria"]), "carnet");
            }
            Assert.True(reales.Count > 0, "La tabla Conductor de la BD de pruebas está vacía: la prueba no probaría nada.");

            var hallazgos = new List<string>();
            foreach (string relativo in archivos)
            {
                if (relativo.StartsWith("packages/", StringComparison.OrdinalIgnoreCase)) continue;
                string ext = Path.GetExtension(relativo);
                if (!ExtensionesTexto.Contains(ext) && !ExtensionesTexto.Contains(Path.GetFileName(relativo))) continue;
                string ruta = Path.Combine(BaseDePruebas.RaizRepo, relativo.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(ruta)) continue;   // borrado del disco y aún en el índice

                string[] lineas;
                try { lineas = File.ReadAllLines(ruta, Encoding.UTF8); }
                catch (IOException) { continue; }

                for (int i = 0; i < lineas.Length; i++)
                {
                    foreach (Match m in Numero.Matches(lineas[i]))
                    {
                        var columnas = new SortedSet<string>();
                        string letra = m.Groups[1].Value, cifras = m.Groups[2].Value;
                        if (letra.Length > 0 && reales.TryGetValue(letra + cifras, out var c1)) columnas.UnionWith(c1);
                        if (reales.TryGetValue(cifras, out var c2)) columnas.UnionWith(c2);
                        if (columnas.Count > 0)
                            hallazgos.Add($"{relativo}:{i + 1} ({string.Join(", ", columnas)})");
                    }
                }
            }

            Assert.True(hallazgos.Count == 0,
                $"{hallazgos.Count} coincidencia(s) con datos de conductores reales (cámbialos por ficticios; no se muestra el valor):"
                + Environment.NewLine + string.Join(Environment.NewLine, hallazgos));
        }

        private static void Agregar(Dictionary<string, HashSet<string>> d, string valor, string columna)
        {
            if (string.IsNullOrEmpty(valor) || valor.Length < 8) return;
            if (!d.TryGetValue(valor, out var cols)) d[valor] = cols = new HashSet<string>();
            cols.Add(columna);
        }

        private static string Normalizar(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            string s = Regex.Replace(v.ToString(), @"[\s\-]", "").ToUpperInvariant();
            return s.Length == 0 ? null : s;
        }

        private static string SoloUltimas9(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            string s = Regex.Replace(v.ToString(), @"\D", "");
            return s.Length >= 9 ? s.Substring(s.Length - 9) : null;
        }

        /// <summary>Rutas relativas (con "/") de lo versionado y de lo nuevo no ignorado; null si no hay git.</summary>
        private static List<string> ArchivosVersionados(out string motivo)
        {
            motivo = null;
            if (BaseDePruebas.RaizRepo == null) { motivo = "no se encontró la raíz del repositorio."; return null; }
            var psi = new ProcessStartInfo("git", "-c core.quotepath=off ls-files -z --cached --others --exclude-standard")
            {
                WorkingDirectory = BaseDePruebas.RaizRepo,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };
            try
            {
                using (var p = Process.Start(psi))
                {
                    var err = p.StandardError.ReadToEndAsync();
                    string salida = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0) { motivo = "git ls-files falló (" + p.ExitCode + ")."; return null; }
                    return salida.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                motivo = "git no está disponible en esta máquina.";
                return null;
            }
        }
    }
}
