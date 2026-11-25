using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-14 (M-101, M-123): alarmas del respaldo que avisan de verdad.
    /// - herramientas/comun/enviar-aviso.ps1 (SGV_SCRIPT_AVISO): lee el SMTP de appSettings.Secrets.config,
    ///   nunca recibe la clave por parámetro ni la escribe; en modo carpeta deja exactamente un .eml.
    /// - herramientas/respaldo/avisar-tarea.ps1: un correo si un paso de la tarea falló, salvo la FALLA
    ///   del sello, que ya avisa el propio sello (una FALLA = un correo).
    /// - verificar-pdf-firmados.ps1: la firma anulada de una orden retirada sin PDF no es un faltante.
    /// Ninguna prueba envía correos: el modo carpeta usa SpecifiedPickupDirectory y avisar-tarea usa un
    /// script de aviso falso que solo cuenta las llamadas.
    /// </summary>
    public class AvisoCorreoTests : IDisposable
    {
        private const string ClaveFalsa = "clave-de-prueba-NO-real-7Hq2";
        private readonly string _temp = Path.Combine(Path.GetTempPath(), "sgv-aviso-" + Guid.NewGuid().ToString("N"));

        public AvisoCorreoTests() { Directory.CreateDirectory(_temp); }

        public void Dispose()
        {
            try { Directory.Delete(_temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private static string RaizRepo()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        private static string Ruta(params string[] partes) => Path.Combine(new[] { RaizRepo(), "herramientas" }.Concat(partes).ToArray());

        private static (int Codigo, string Salida) Correr(string script, string argumentos, params (string Nombre, string Valor)[] entorno)
        {
            string ps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe");
            var info = new ProcessStartInfo(ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" " + argumentos)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            // Que nada de la máquina se cuele: sin variables de aviso salvo las de la prueba.
            foreach (string v in new[] { "SGV_SCRIPT_AVISO", "SGV_AVISO_PARA", "SGV_AVISO_CARPETA_SALIDA" })
                info.EnvironmentVariables.Remove(v);
            foreach (var (nombre, valor) in entorno) info.EnvironmentVariables[nombre] = valor;
            using (var proceso = Process.Start(info))
            {
                var salida = proceso.StandardOutput.ReadToEndAsync();
                var errores = proceso.StandardError.ReadToEndAsync();
                Assert.True(proceso.WaitForExit(60000), Path.GetFileName(script) + " no terminó en 60 s.");
                return (proceso.ExitCode, salida.Result + errores.Result);
            }
        }

        private string SecretosFalsos()
        {
            string ruta = Path.Combine(_temp, "appSettings.Secrets.config");
            File.WriteAllText(ruta,
                "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n<appSettings>\r\n" +
                "  <add key=\"SmtpHost\" value=\"smtp.ejemplo.invalid\" />\r\n" +
                "  <add key=\"SmtpPort\" value=\"587\" />\r\n" +
                "  <add key=\"SmtpUser\" value=\"cuenta@ejemplo.invalid\" />\r\n" +
                "  <add key=\"SmtpPass\" value=\"" + ClaveFalsa + "\" />\r\n" +
                "  <add key=\"SmtpFrom\" value=\"avisos@ejemplo.invalid\" />\r\n" +
                "</appSettings>\r\n");
            return ruta;
        }

        [Theory]
        [InlineData("comun", "enviar-aviso.ps1")]
        [InlineData("respaldo", "avisar-tarea.ps1")]
        [InlineData("respaldo", "verificar-pdf-firmados.ps1")]
        [InlineData("respaldo", "programar-respaldo.ps1")]
        public void Scripts_ExistenConBom(string carpeta, string archivo)
        {
            byte[] bytes = File.ReadAllBytes(Ruta(carpeta, archivo));
            Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, archivo + " debe estar en UTF-8 con BOM.");
        }

        [Fact]
        public void EnviarAviso_NoRecibeLaClavePorParametroNiLaEscribe()
        {
            string texto = File.ReadAllText(Ruta("comun", "enviar-aviso.ps1"));
            var bloque = Regex.Match(texto, @"(?ms)^param\((.*?)^\)");
            Assert.True(bloque.Success, "No se encontró el bloque param( ... ) de enviar-aviso.ps1.");
            // Parámetros declarados: [tipo]$Nombre.
            var parametros = Regex.Matches(bloque.Groups[1].Value, @"\[\w+\]\s*\$(\w+)").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Equal(new[] { "Asunto", "Cuerpo", "Para", "CarpetaSalida", "ArchivoSecretos", "ArchivoWebConfig" }, parametros);
            Assert.DoesNotMatch(new Regex("(?i)pass|contrase|clave|credential|securestring"), bloque.Groups[1].Value);

            // La clave sale del archivo de secretos y solo llega a NetworkCredential.
            Assert.Contains("appSettings.Secrets.config", texto);
            string codigo = Regex.Replace(texto, @"(?s)<#.*?#>", "");
            var usos = codigo.Split('\n').Where(l => l.Contains("SmtpPass") && !l.TrimStart().StartsWith("#", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, usos.Count);
            Assert.All(usos, l => Assert.True(l.Contains("NetworkCredential") || l.Contains("IsNullOrEmpty"),
                "Uso de SmtpPass fuera de la credencial o la comprobación: " + l.Trim()));
        }

        [Fact]
        public void EnviarAviso_ModoCarpeta_DejaUnSoloEmlSinLaClave()
        {
            string buzon = Path.Combine(_temp, "buzon");
            var (codigo, salida) = Correr(Ruta("comun", "enviar-aviso.ps1"),
                "-Asunto \"SGV: prueba de aviso\" -Cuerpo \"FALLA de ensayo\" -Para destino@ejemplo.invalid -CarpetaSalida \"" + buzon +
                "\" -ArchivoSecretos \"" + SecretosFalsos() + "\"");

            Assert.True(codigo == 0, "enviar-aviso.ps1 salió con " + codigo + ": " + salida);
            string[] eml = Directory.GetFiles(buzon, "*.eml");
            Assert.Single(eml);
            string contenido = File.ReadAllText(eml[0]);
            Assert.Contains("destino@ejemplo.invalid", contenido);
            Assert.Contains("avisos@ejemplo.invalid", contenido);
            Assert.DoesNotContain(ClaveFalsa, contenido);
            Assert.DoesNotContain(ClaveFalsa, salida);
            Assert.DoesNotContain("destino@", salida); // en la salida, el destinatario va sin la parte local
        }

        [Fact]
        public void EnviarAviso_SinClaveEnElArchivo_FallaSinEnviar()
        {
            string secretos = Path.Combine(_temp, "sin-clave.config");
            File.WriteAllText(secretos, "<appSettings><add key=\"SmtpHost\" value=\"smtp.ejemplo.invalid\" /><add key=\"SmtpUser\" value=\"c@ejemplo.invalid\" /></appSettings>");
            string buzon = Path.Combine(_temp, "buzon-vacio");
            var (codigo, salida) = Correr(Ruta("comun", "enviar-aviso.ps1"),
                "-Asunto x -Para d@ejemplo.invalid -CarpetaSalida \"" + buzon + "\" -ArchivoSecretos \"" + secretos + "\"");

            Assert.NotEqual(0, codigo);
            Assert.Contains("SmtpPass", salida);
            Assert.False(Directory.Exists(buzon) && Directory.GetFiles(buzon, "*.eml").Length > 0, "No debe quedar ningún .eml.");
        }

        /// <summary>Script de aviso falso: cada llamada deja un archivo (no envía nada).</summary>
        private string AvisoFalso(string carpeta)
        {
            Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(_temp, "aviso-falso.ps1");
            File.WriteAllText(ruta,
                "param([string]$Asunto, [string]$Cuerpo)\r\n" +
                "Set-Content -LiteralPath (Join-Path '" + carpeta.Replace("'", "''") + "' ([guid]::NewGuid().ToString() + '.txt')) -Value ($Asunto + \"`n\" + $Cuerpo)\r\n");
            return ruta;
        }

        [Theory]
        [InlineData(0, 0, 0, 0)] // día limpio: nada
        [InlineData(0, 1, 0, 0)] // FALLA del sello: ya la avisó el sello (una FALLA = un correo)
        [InlineData(1, 0, 0, 1)] // falló el respaldo de la BD
        [InlineData(0, 2, 0, 1)] // no se pudo sellar
        [InlineData(0, 0, 2, 1)] // la verificación de los PDF firmados
        [InlineData(1, 1, 1, 1)] // varios pasos: un solo correo
        public void AvisarTarea_UnCorreoSoloSiFallaUnPasoNoAvisado(int bd, int sello, int archivos, int correos)
        {
            string buzon = Path.Combine(_temp, "llamadas");
            var (codigo, salida) = Correr(Ruta("respaldo", "avisar-tarea.ps1"),
                "-CodigoBd " + bd + " -CodigoSello " + sello + " -CodigoArchivos " + archivos,
                ("SGV_SCRIPT_AVISO", AvisoFalso(buzon)));

            Assert.True(codigo == 0, "avisar-tarea.ps1 debe salir siempre con 0; salió con " + codigo + ": " + salida);
            string[] llamadas = Directory.GetFiles(buzon, "*.txt");
            Assert.Equal(correos, llamadas.Length);
            if (correos == 1)
            {
                string aviso = File.ReadAllText(llamadas[0]);
                Assert.StartsWith("SGV: FALLA en el respaldo diario", aviso);
                if (bd != 0) Assert.Contains("respaldar-bd.ps1", aviso);
                if (archivos == 2) Assert.Contains("verificar-pdf-firmados.ps1", aviso);
            }
        }

        [Fact]
        public void AvisarTarea_SinScriptDeAviso_NoFalla()
        {
            var (codigo, salida) = Correr(Ruta("respaldo", "avisar-tarea.ps1"), "-CodigoBd 1");
            Assert.Equal(0, codigo);
            Assert.Contains("SGV_SCRIPT_AVISO", salida);
        }

        [Fact]
        public void TareaDiaria_AvisaAlFinalSinCambiarSuResultado()
        {
            string texto = File.ReadAllText(Ruta("respaldo", "programar-respaldo.ps1"));
            Assert.Contains("Join-Path $PSScriptRoot 'avisar-tarea.ps1'", texto);
            int detener = texto.IndexOf("\"Stop-Transcript | Out-Null\"", StringComparison.Ordinal);
            int avisar = texto.IndexOf("& $(ConvertTo-Literal $scriptAvisar) -CodigoBd `$cBd -CodigoSello `$cSe -CodigoArchivos `$cAr", StringComparison.Ordinal);
            int resultado = texto.IndexOf("exit (10 + `$cSe)", StringComparison.Ordinal);
            Assert.True(detener >= 0 && avisar > detener && resultado > avisar, "El aviso va después de Stop-Transcript y antes del resultado de la tarea.");
            Assert.Contains("} catch { }\"", texto.Substring(avisar, 400));
        }

        [Fact]
        public void Sello_AvisaConSgvScriptAviso()
        {
            string texto = File.ReadAllText(Ruta("respaldo", "sellar-registros.ps1"));
            Assert.Contains("[string]$ScriptAviso = $env:SGV_SCRIPT_AVISO", texto);
            Assert.Contains("& $ScriptAviso -Asunto", texto);
        }

        [Fact]
        public void VerificarPdf_FirmaAnuladaDeOrdenRetiradaNoEsFaltante()
        {
            string texto = File.ReadAllText(Ruta("respaldo", "verificar-pdf-firmados.ps1"));
            Assert.Contains("NOT EXISTS (SELECT 1 FROM dbo.OrdenViaje o WHERE CAST(o.idOrdenViaje AS varchar(50)) = f.idDocumento)", texto);
            Assert.Contains("Retirada = ($fila.origen -eq 'FirmaDigital' -and [bool]$fila.anulada -and [bool]$fila.ordenRetirada)", texto);
            Assert.Contains("if ($e.Retirada) { $retiradasSinPdf += $e } else { $faltantes += $e }", texto);
            // Solo los faltantes y distintos dan código 1: las retiradas sin PDF no.
            Assert.Contains("if ($faltantes.Count -gt 0 -or $distintos.Count -gt 0) { exit 1 }", texto);
        }
    }
}
