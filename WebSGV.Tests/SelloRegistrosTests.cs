using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E4-11: sello externo de FirmaDigital, AuditoriaLog y Auditoria (herramientas/respaldo/sellar-registros.ps1).
    /// - El script y sus pruebas existen y están en UTF-8 con BOM (llevan tildes; PowerShell 5.1).
    /// - La tarea diaria (programar-respaldo.ps1) lo llama después del respaldo de la BD.
    /// - Solo lee la BD: nada de ExecuteNonQuery ni sentencias que escriban.
    /// - probar-sellar.ps1 (funciones puras: canonización, hash, comparación, retención) en verde.
    /// </summary>
    public class SelloRegistrosTests
    {
        private static string RaizRepo()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        private static string Ruta(string archivo) => Path.Combine(RaizRepo(), "herramientas", "respaldo", archivo);

        [Theory]
        [InlineData("sellar-registros.ps1")]
        [InlineData("probar-sellar.ps1")]
        [InlineData("programar-respaldo.ps1")]
        public void Scripts_ExistenConBom(string archivo)
        {
            byte[] bytes = File.ReadAllBytes(Ruta(archivo));
            Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, archivo + " debe estar en UTF-8 con BOM.");
        }

        [Fact]
        public void TareaDiaria_LlamaAlSelloDespuesDelRespaldoDeLaBd()
        {
            string texto = File.ReadAllText(Ruta("programar-respaldo.ps1"));
            Assert.Contains("Join-Path $PSScriptRoot 'sellar-registros.ps1'", texto);
            int bd = texto.IndexOf("& $(ConvertTo-Literal $scriptBd)", StringComparison.Ordinal);
            int sello = texto.IndexOf("& $(ConvertTo-Literal $scriptSello)", StringComparison.Ordinal);
            int archivos = texto.IndexOf("& $(ConvertTo-Literal $scriptArchivos)", StringComparison.Ordinal);
            Assert.True(bd >= 0 && sello > bd && archivos > sello, "Orden esperado en la tarea: respaldo de la BD, sello, espejo de archivos.");
            // Un error del sello no corta el resto de la tarea.
            Assert.Contains("catch { `$_; `$cSe = 2 }", texto);
        }

        [Fact]
        public void Sello_SoloLeeLaBd()
        {
            string texto = File.ReadAllText(Ruta("sellar-registros.ps1"));
            Assert.DoesNotContain("ExecuteNonQuery", texto);
            Assert.DoesNotContain("ExecuteScalar", texto);
            foreach (string sentencia in new[] { "UPDATE ", "DELETE ", "INSERT ", "ALTER ", "DROP ", "DISABLE TRIGGER", "ENABLE TRIGGER", "TRUNCATE " })
            {
                // Solo como palabras sueltas en mensajes ("bloquee UPDATE"), nunca en un CommandText.
                var lineas = texto.Split('\n').Where(l => l.Contains(sentencia) && (l.Contains("CommandText") || l.Contains("Invoke-Lectura")));
                Assert.Empty(lineas);
            }
            Assert.Contains("-ConfirmoProduccion", texto);
        }

        [Fact]
        public async System.Threading.Tasks.Task ProbarSellar_EnVerde()
        {
            string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + Ruta("probar-sellar.ps1") + "\"")
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
                Assert.True(proceso.WaitForExit(120000), "probar-sellar.ps1 no terminó en 120 s.");
                string textoSalida = await salida;
                string textoErrores = await errores;
                Assert.True(proceso.ExitCode == 0, "probar-sellar.ps1 salió con " + proceso.ExitCode + ": " + textoErrores + textoSalida);
                Assert.Contains("Todos los casos OK.", textoSalida);
            }
        }
    }
}
