using System;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    public class LiquidacionCalculosTests
    {
        // ---- ValidarMontosAjuste ----

        [Fact]
        public void ValidarMontosAjuste_MontosValidos_RetornaNull()
        {
            Assert.Null(LiquidacionCalculos.ValidarMontosAjuste(100m, 50m, 0m, 25m));
        }

        [Fact]
        public void ValidarMontosAjuste_TodosCero_RetornaNull()
        {
            Assert.Null(LiquidacionCalculos.ValidarMontosAjuste(0m, 0m, 0m, 0m));
        }

        [Theory]
        [InlineData(-1, 0, 0, 0)]
        [InlineData(0, -1, 0, 0)]
        [InlineData(0, 0, -1, 0)]
        [InlineData(0, 0, 0, -1)]
        public void ValidarMontosAjuste_AlgunNegativo_RetornaErrorNegativos(
            decimal descS, decimal descD, decimal reintS, decimal reintD)
        {
            string error = LiquidacionCalculos.ValidarMontosAjuste(descS, descD, reintS, reintD);
            Assert.Equal("Los montos de descuento y reintegro no pueden ser negativos.", error);
        }

        [Fact]
        public void ValidarMontosAjuste_EnElLimiteMaximo_RetornaNull()
        {
            Assert.Null(LiquidacionCalculos.ValidarMontosAjuste(
                LiquidacionCalculos.MontoMaximoAjuste, LiquidacionCalculos.MontoMaximoAjuste,
                LiquidacionCalculos.MontoMaximoAjuste, LiquidacionCalculos.MontoMaximoAjuste));
        }

        [Fact]
        public void ValidarMontosAjuste_SuperaMaximo_RetornaErrorMaximo()
        {
            string error = LiquidacionCalculos.ValidarMontosAjuste(
                LiquidacionCalculos.MontoMaximoAjuste + 1m, 0m, 0m, 0m);
            Assert.Equal("Los montos no pueden superar S/ 9,999,999.", error);
        }

        // ---- ClaseBalance ----

        [Theory]
        [InlineData(0, "balance-positivo")]
        [InlineData(1.5, "balance-positivo")]
        [InlineData(-0.01, "balance-negativo")]
        [InlineData(-100, "balance-negativo")]
        public void ClaseBalance_SegunSigno(decimal balance, string esperado)
        {
            Assert.Equal(esperado, LiquidacionCalculos.ClaseBalance(balance));
        }

        // ---- Prioridad ----

        [Theory]
        [InlineData(0, "normal")]
        [InlineData(11, "normal")]
        [InlineData(12, "alta")]
        [InlineData(24, "alta")]
        [InlineData(25, "urgente")]
        [InlineData(100, "urgente")]
        public void Prioridad_SegunHoras(int horas, string esperado)
        {
            Assert.Equal(esperado, LiquidacionCalculos.Prioridad(horas));
        }

        // ---- FormatearTiempo ----

        [Theory]
        [InlineData(0, "0h")]
        [InlineData(5, "5h")]
        [InlineData(23, "23h")]
        [InlineData(24, "1d")]
        [InlineData(25, "1d 1h")]
        [InlineData(49, "2d 1h")]
        [InlineData(48, "2d")]
        public void FormatearTiempo_SegunHoras(int horas, string esperado)
        {
            Assert.Equal(esperado, LiquidacionCalculos.FormatearTiempo(horas));
        }

        // ---- NormalizarTexto ----

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizarTexto_VacioONulo_RetornaCadenaVacia(string entrada)
        {
            Assert.Equal(string.Empty, LiquidacionCalculos.NormalizarTexto(entrada, 100));
        }

        [Fact]
        public void NormalizarTexto_RecortaEspacios()
        {
            Assert.Equal("hola", LiquidacionCalculos.NormalizarTexto("  hola  ", 100));
        }

        [Fact]
        public void NormalizarTexto_TruncaAlMaximo()
        {
            Assert.Equal("abcde", LiquidacionCalculos.NormalizarTexto("abcdefghij", 5));
        }

        [Fact]
        public void NormalizarTexto_LongitudIgualAlMaximo_NoTrunca()
        {
            Assert.Equal("abcde", LiquidacionCalculos.NormalizarTexto("abcde", 5));
        }

        // ---- DecodificarPngBase64 ----

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void DecodificarPngBase64_VacioONulo_RetornaNull(string entrada)
        {
            Assert.Null(LiquidacionCalculos.DecodificarPngBase64(entrada));
        }

        [Fact]
        public void DecodificarPngBase64_Base64Invalido_RetornaNull()
        {
            Assert.Null(LiquidacionCalculos.DecodificarPngBase64("no-es-base64!!!"));
        }

        [Fact]
        public void DecodificarPngBase64_Base64Simple_Decodifica()
        {
            string b64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
            byte[] resultado = LiquidacionCalculos.DecodificarPngBase64(b64);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, resultado);
        }

        [Fact]
        public void DecodificarPngBase64_ConPrefijoDataUri_DecodificaCuerpo()
        {
            string b64 = Convert.ToBase64String(new byte[] { 9, 8, 7 });
            byte[] resultado = LiquidacionCalculos.DecodificarPngBase64("data:image/png;base64," + b64);
            Assert.Equal(new byte[] { 9, 8, 7 }, resultado);
        }

        // ---- TryParseFechaFiltro ----

        private static readonly DateTime Hoy = new DateTime(2026, 6, 10);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void TryParseFechaFiltro_Vacia_EsValidaYNull(string entrada)
        {
            bool ok = LiquidacionCalculos.TryParseFechaFiltro(entrada, Hoy, out DateTime? fecha);
            Assert.True(ok);
            Assert.Null(fecha);
        }

        [Fact]
        public void TryParseFechaFiltro_FechaIsoValida_RetornaFecha()
        {
            bool ok = LiquidacionCalculos.TryParseFechaFiltro("2026-03-15", Hoy, out DateTime? fecha);
            Assert.True(ok);
            Assert.Equal(new DateTime(2026, 3, 15), fecha);
        }

        [Theory]
        [InlineData("15/03/2026")]
        [InlineData("2026-13-01")]
        [InlineData("no es fecha")]
        public void TryParseFechaFiltro_FormatoInvalido_RetornaFalse(string entrada)
        {
            bool ok = LiquidacionCalculos.TryParseFechaFiltro(entrada, Hoy, out DateTime? fecha);
            Assert.False(ok);
            Assert.Null(fecha);
        }

        [Fact]
        public void TryParseFechaFiltro_AnteriorAlMinimo_RetornaFalse()
        {
            bool ok = LiquidacionCalculos.TryParseFechaFiltro("1999-12-31", Hoy, out DateTime? fecha);
            Assert.False(ok);
            Assert.Null(fecha);
        }

        [Fact]
        public void TryParseFechaFiltro_MasDeUnAnioEnFuturo_RetornaFalse()
        {
            // Hoy + 1 año + 1 día
            bool ok = LiquidacionCalculos.TryParseFechaFiltro("2027-06-11", Hoy, out DateTime? fecha);
            Assert.False(ok);
            Assert.Null(fecha);
        }

        [Fact]
        public void TryParseFechaFiltro_ExactamenteUnAnioEnFuturo_EsValida()
        {
            bool ok = LiquidacionCalculos.TryParseFechaFiltro("2027-06-10", Hoy, out DateTime? fecha);
            Assert.True(ok);
            Assert.Equal(new DateTime(2027, 6, 10), fecha);
        }
    }
}
