using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E3-2: reglas de fuente sobre todos los SP del repo (sin BD, corre en CI).
    /// - Ningún despacho vuelve a 'EN_PROCESO' (estado que ninguna pantalla reconoce; al rechazar
    ///   o retirar una liquidación los despachos vuelven a PROGRAMADO).
    /// - Ningún error técnico llega al usuario: sin ERROR_MESSAGE() ni RAISERROR(@variable); el
    ///   CATCH relanza con THROW y MensajeErrorHelper muestra el texto genérico.
    /// Los comentarios (-- y /* */) no cuentan.
    /// </summary>
    public class ErroresYEstadosEnSpTests
    {
        private static readonly string CarpetaSp =
            Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Database", "StoredProcedures");

        // Literales de texto se conservan (un "--" dentro de un texto no es comentario).
        private static readonly Regex TokensComentario =
            new Regex(@"'(?:[^']|'')*'|--[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);

        internal static string SinComentarios(string sql) =>
            TokensComentario.Replace(sql, m => m.Value.StartsWith("'") ? m.Value : " ");

        private static string Codigo(string archivo) =>
            SinComentarios(File.ReadAllText(Path.Combine(CarpetaSp, archivo)));

        private static string[] ArchivosQueContienen(string patron) =>
            Directory.GetFiles(CarpetaSp, "*.sql")
                .Where(f => Regex.IsMatch(SinComentarios(File.ReadAllText(f)), patron, RegexOptions.IgnoreCase))
                .Select(Path.GetFileName)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        [Fact]
        public void Hay_procedimientos_que_revisar()
        {
            Assert.True(Directory.GetFiles(CarpetaSp, "*.sql").Length > 50);
        }

        [Fact]
        public void Ningun_sp_usa_el_estado_EN_PROCESO()
        {
            Assert.Empty(ArchivosQueContienen(@"'EN_PROCESO'"));
        }

        [Fact]
        public void Ningun_sp_devuelve_ERROR_MESSAGE()
        {
            Assert.Empty(ArchivosQueContienen(@"ERROR_MESSAGE\s*\(\s*\)"));
        }

        [Fact]
        public void Ningun_sp_relanza_con_RAISERROR_de_una_variable()
        {
            Assert.Empty(ArchivosQueContienen(@"RAISERROR\s*\(\s*@"));
        }

        [Theory]
        [InlineData("sp_DC_RegistrarFirmaAdmin.sql")]
        [InlineData("sp_DC_CerrarViajesProgreso.sql")]
        public void El_CATCH_relanza_con_THROW(string archivo)
        {
            var catches = Regex.Matches(Codigo(archivo), @"BEGIN\s+CATCH([\s\S]*?)END\s+CATCH", RegexOptions.IgnoreCase)
                .Cast<Match>().ToList();
            Assert.NotEmpty(catches);
            Assert.All(catches, m => Assert.Matches(new Regex(@"\bTHROW\s*;", RegexOptions.IgnoreCase), m.Groups[1].Value));
        }

        [Fact]
        public void Rechazar_devuelve_los_despachos_a_PROGRAMADO()
        {
            Assert.Matches(@"estadoDespacho\s*=\s*'PROGRAMADO'", Codigo("sp_RechazarLiquidacion.sql"));
        }

        [Fact]
        public void SinComentarios_respeta_los_textos()
        {
            string sql = "SELECT '--no es comentario' -- sí lo es\r\n/* bloque\r\n ERROR_MESSAGE() */ SELECT 1";
            string limpio = SinComentarios(sql);
            Assert.Contains("'--no es comentario'", limpio);
            Assert.DoesNotContain("sí lo es", limpio);
            Assert.DoesNotContain("ERROR_MESSAGE", limpio);
            Assert.Contains("SELECT 1", limpio);
        }
    }
}
