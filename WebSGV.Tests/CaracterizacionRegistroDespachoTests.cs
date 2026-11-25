using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-21: herramientas/sitio-local/caracterizar-registro-despacho.ps1 (la red de seguridad para partir
    /// RegistroDespacho en E6-22 a E6-26).
    /// - UTF-8 con BOM (lleva tildes; PowerShell 5.1).
    /// - La lista blanca de POST existe, tiene exactamente los tres botones de lectura y cada uno sigue en
    ///   RegistroDespacho.aspx con el mismo texto; ningún otro botón de la página aparece en el script.
    /// - Todo POST pasa por un único punto que la comprueba (sin eventos de AutoPostBack, un solo botón).
    /// - Un juego fallido no se guarda y al comparar cuenta como diferencia (no hay "0 diferencias" falso).
    /// - La contraseña no va en los argumentos de curl (también en probar-importacion.ps1).
    /// - Solo contra el sitio local, nunca con la conexión a producción, y la salida va a privado/ (gitignored).
    /// </summary>
    public class CaracterizacionRegistroDespachoTests
    {
        private static readonly string[] BotonesDeLectura =
            { "btnLeerDocumentos", "btnVerCargasPendientes", "btnLeerProgramacionDescarga" };

        private static string RaizRepo()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        private static string RutaScript() =>
            Path.Combine(RaizRepo(), "herramientas", "sitio-local", "caracterizar-registro-despacho.ps1");

        private static string Script() => File.ReadAllText(RutaScript());

        private static string ListaBlanca()
        {
            var m = Regex.Match(Script(), @"\$PostsPermitidos\s*=\s*\[ordered\]@\{(.*?)\r?\n\}", RegexOptions.Singleline);
            Assert.True(m.Success, "Falta la lista blanca $PostsPermitidos.");
            return m.Groups[1].Value;
        }

        [Fact]
        public void Script_EnUtf8ConBom()
        {
            byte[] bytes = File.ReadAllBytes(RutaScript());
            Assert.True(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                "caracterizar-registro-despacho.ps1 debe estar en UTF-8 con BOM.");
        }

        [Fact]
        public void ListaBlanca_SoloLosTresBotonesDeLectura_YSiguenEnLaPagina()
        {
            var claves = Regex.Matches(ListaBlanca(), @"'(\w+)'\s*=\s*'([^']+)'")
                .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            Assert.Equal(BotonesDeLectura.OrderBy(b => b), claves.Keys.OrderBy(b => b));

            string aspx = File.ReadAllText(Path.Combine(RaizRepo(), "WebSGV", "Views", "RegistroDespacho.aspx"));
            foreach (var par in claves)
            {
                var boton = Regex.Match(aspx, "<asp:Button ID=\"" + par.Key + "\"[^>]*?Text=\"([^\"]+)\"", RegexOptions.Singleline);
                Assert.True(boton.Success, par.Key + " ya no está en RegistroDespacho.aspx.");
                Assert.Equal(boton.Groups[1].Value, par.Value);
            }
        }

        [Fact]
        public void Script_NoNombraNingunBotonDeEscrituraDeLaPagina()
        {
            string aspx = File.ReadAllText(Path.Combine(RaizRepo(), "WebSGV", "Views", "RegistroDespacho.aspx"));
            var otros = Regex.Matches(aspx, "<asp:(?:Button|LinkButton|ImageButton)\\b[^>]*?ID=\"(\\w+)\"", RegexOptions.Singleline)
                .Cast<Match>().Select(m => m.Groups[1].Value).Except(BotonesDeLectura).ToList();
            Assert.Contains("btnAplicarImportacion", otros);
            Assert.Contains("btnProgramarDespachos", otros);
            Assert.Contains("btnFinalizarLote", otros);

            string script = Script();
            foreach (string boton in otros)
                Assert.False(Regex.IsMatch(script, @"\b" + boton + @"\b"), "El script no debe nombrar " + boton + ".");
            // Tampoco los eventos de AutoPostBack (la sección del Word y demás listas).
            Assert.DoesNotContain("ddlSeccionProg", script);
        }

        [Fact]
        public void Post_PasaSiemprePorLaComprobacion()
        {
            string script = Script();
            // Dos salidas a curl: el GET de Obtener y el POST de CurlConEntrada, al que solo llama Publicar tras comprobar.
            Assert.Single(Regex.Matches(script, @"&\s*curl\.exe").Cast<Match>());
            Assert.Single(Regex.Matches(script, @"ProcessStartInfo 'curl\.exe'").Cast<Match>());
            Assert.Equal(2, Regex.Matches(script, @"\bCurlConEntrada\b").Count); // la definición y la llamada de Publicar
            var publicar = Regex.Match(script, @"function Publicar\(.*?\r?\n\}", RegexOptions.Singleline).Value;
            Assert.Contains("__EVENTTARGET", publicar);
            Assert.Contains("$botones.Count -ne 1", publicar);
            Assert.Contains("$PostsPermitidos.Keys", publicar);
            Assert.Contains("'/Views/Login.aspx') { $permitidos = @($PostIngreso) }", publicar);
            Assert.True(publicar.LastIndexOf("POST cortado", StringComparison.Ordinal) < publicar.IndexOf("CurlConEntrada", StringComparison.Ordinal),
                "La comprobación debe ir antes del envío.");
            Assert.Contains("$PostIngreso = 'btnLogin'", script);
            // Un POST cortado aborta todo el script, no solo el juego.
            Assert.Contains("$m = $_.Exception.Message", script);
            Assert.Contains("if ($m -like 'POST cortado*') { throw }", script);
        }

        [Fact]
        public void JuegoFallido_NoSeGuarda_YAlCompararCuentaComoDiferencia()
        {
            string script = Script();
            // CorrerJuego marca el fallo: pedido ya no pendiente y página sin el recuadro; el catch, cualquier error.
            Assert.Contains("[pscustomobject]@{ Texto = ($partes -join \"`n`n\") + \"`n\"; Fallo = $fallo }", script);
            Assert.Contains("$fallo = \"el pedido $($p.Pedido) ya no está", script);
            Assert.Contains("if ($vista.StartsWith('(la página no muestra')) { $fallo =", script);
            Assert.Contains("$r = [pscustomobject]@{ Texto = \"(error al correr el juego: $m)`n\"; Fallo = $m }", script);

            // Al guardar: el fallido no se escribe (el aviso y el continue van antes del WriteAllText) y sale con 1.
            var guardar = Regex.Match(script, @"if \(-not \$Comparar\) \{\s*\r?\n\s*if \(\$r\.Fallo\) \{(.*?)\}\s*\r?\n\s*\[IO\.File\]::WriteAllText\(\$archivo", RegexOptions.Singleline);
            Assert.True(guardar.Success, "El fallido debe saltarse antes de escribir el guardado.");
            Assert.Contains("$fallidos++", guardar.Groups[1].Value);
            Assert.Contains("continue", guardar.Groups[1].Value);
            Assert.Contains("if ($fallidos -gt 0) {", script);
            Assert.Matches(new Regex(@"if \(\$fallidos -gt 0\) \{[^}]*exit 1 \}"), script);

            // Al comparar: el fallido y el guardado de una corrida fallida cuentan antes de mirar si el texto coincide.
            int igual = script.IndexOf("if ($viejo -ceq $texto)", StringComparison.Ordinal);
            int fallo = script.IndexOf("if ($r.Fallo) { \"$($j.Nombre): FALLÓ", StringComparison.Ordinal);
            int marca = script.IndexOf("if ($viejo -match $MarcaFallo)", StringComparison.Ordinal);
            Assert.True(fallo > 0 && marca > 0 && fallo < igual && marca < igual,
                "El juego fallido y el guardado de error deben contar como diferencia antes de comparar el texto.");
            Assert.Contains("(error al correr el juego|la página no muestra el recuadro|el pedido \\d+ ya no está en la lista)", script);

            // Sitio caído: su propio mensaje y exit 1, no "POST cortado".
            Assert.Contains("$NoResponde = \"El sitio no responde en $Sitio (¿iniciar-sitio.ps1?)\"", script);
            Assert.Contains("if ($m -eq $NoResponde) { Write-Output $NoResponde; exit 1 }", script);
        }

        [Theory]
        [InlineData("caracterizar-registro-despacho.ps1")]
        [InlineData("probar-importacion.ps1")]
        public void Contrasena_NoVaEnLosArgumentosDeCurl(string archivo)
        {
            string script = File.ReadAllText(Path.Combine(RaizRepo(), "herramientas", "sitio-local", archivo));
            // Los campos (contraseña incluida) van por la entrada estándar como configuración de curl.
            Assert.DoesNotContain("--form-string", script);
            Assert.DoesNotContain("-F', \"$k=", script);
            Assert.Contains("'-K', '-'", script);
            Assert.Contains("'form-string = ' + (Citar \"$k=$($campos[$k])\")", script);
            Assert.Contains("RedirectStandardInput = $true", script);
            Assert.Contains("StandardInput.BaseStream.Write($bytes", script);
            // Ni en un archivo temporal.
            Assert.DoesNotContain("sgv-caracterizacion-campo", script);
        }

        [Fact]
        public void Salida_EnPrivado_YSoloContraElSitioLocalDePruebas()
        {
            string script = Script();
            Assert.Contains("Join-Path $repo 'privado\\caracterizacion'", script);
            Assert.Contains("^https?://(localhost|127\\.0\\.0\\.1)(:\\d+)?$", script);
            Assert.Contains("sgvTransporte", script);
            Assert.Contains("privado\\CREDENCIALES_USUARIOS.txt", script);

            string gitignore = File.ReadAllText(Path.Combine(RaizRepo(), ".gitignore"));
            Assert.Matches(new Regex(@"^/privado/\s*$", RegexOptions.Multiline), gitignore);
        }
    }
}
