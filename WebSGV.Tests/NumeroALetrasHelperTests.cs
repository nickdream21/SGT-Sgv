using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class NumeroALetrasHelperTests
    {
        // ── Casos base ──────────────────────────────────────────────────────────

        [Fact]
        public void Convertir_Cero()
        {
            Assert.Equal("CERO CON 00/100 SOLES", NumeroALetrasHelper.Convertir(0m, "SOLES"));
        }

        [Theory]
        [InlineData(1,    "UNO CON 00/100 SOLES")]
        [InlineData(2,    "DOS CON 00/100 SOLES")]
        [InlineData(10,   "DIEZ CON 00/100 SOLES")]
        [InlineData(11,   "ONCE CON 00/100 SOLES")]
        [InlineData(15,   "QUINCE CON 00/100 SOLES")]
        [InlineData(20,   "VEINTE CON 00/100 SOLES")]
        public void Convertir_Unidades(int numero, string esperado)
        {
            Assert.Equal(esperado, NumeroALetrasHelper.Convertir(numero, "SOLES"));
        }

        // ── Decenas y centenas ──────────────────────────────────────────────────

        [Theory]
        [InlineData(30,  "TREINTA CON 00/100 SOLES")]
        [InlineData(45,  "CUARENTA Y CINCO CON 00/100 SOLES")]
        [InlineData(99,  "NOVENTA Y NUEVE CON 00/100 SOLES")]
        [InlineData(100, "CIEN CON 00/100 SOLES")]
        [InlineData(101, "CIENTO UNO CON 00/100 SOLES")]
        [InlineData(500, "QUINIENTOS CON 00/100 SOLES")]
        [InlineData(999, "NOVECIENTOS NOVENTA Y NUEVE CON 00/100 SOLES")]
        public void Convertir_DecenasYCentenas(int numero, string esperado)
        {
            Assert.Equal(esperado, NumeroALetrasHelper.Convertir(numero, "SOLES"));
        }

        // ── Miles ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(1000,  "MIL CON 00/100 SOLES")]
        [InlineData(1001,  "MIL UNO CON 00/100 SOLES")]
        [InlineData(2000,  "DOS MIL CON 00/100 SOLES")]
        [InlineData(1500,  "MIL QUINIENTOS CON 00/100 SOLES")]
        [InlineData(10000, "DIEZ MIL CON 00/100 SOLES")]
        [InlineData(99999, "NOVENTA Y NUEVE MIL NOVECIENTOS NOVENTA Y NUEVE CON 00/100 SOLES")]
        public void Convertir_Miles(int numero, string esperado)
        {
            Assert.Equal(esperado, NumeroALetrasHelper.Convertir(numero, "SOLES"));
        }

        // ── Millones ────────────────────────────────────────────────────────────

        [Fact]
        public void Convertir_UnMillon()
        {
            Assert.Equal("UN MILLÓN CON 00/100 SOLES", NumeroALetrasHelper.Convertir(1000000m, "SOLES"));
        }

        [Fact]
        public void Convertir_DosMilones()
        {
            Assert.Equal("DOS MILLONES CON 00/100 SOLES", NumeroALetrasHelper.Convertir(2000000m, "SOLES"));
        }

        // ── Decimales ───────────────────────────────────────────────────────────

        [Theory]
        [InlineData("1234.56",  "MIL DOSCIENTOS TREINTA Y CUATRO CON 56/100 SOLES")]
        [InlineData("100.50",   "CIEN CON 50/100 SOLES")]
        [InlineData("0.99",     "CERO CON 99/100 SOLES")]
        [InlineData("0.01",     "CERO CON 01/100 SOLES")]
        public void Convertir_ConDecimales(string monto, string esperado)
        {
            Assert.Equal(esperado, NumeroALetrasHelper.Convertir(decimal.Parse(monto, System.Globalization.CultureInfo.InvariantCulture), "SOLES"));
        }

        // ── Negativos ───────────────────────────────────────────────────────────

        [Fact]
        public void Convertir_Negativo()
        {
            Assert.Equal("MENOS UNO CON 00/100 SOLES", NumeroALetrasHelper.Convertir(-1m, "SOLES"));
        }

        [Fact]
        public void Convertir_NegativoConDecimales()
        {
            Assert.Equal("MENOS MIL CON 50/100 SOLES", NumeroALetrasHelper.Convertir(-1000.50m, "SOLES"));
        }

        // ── Moneda ──────────────────────────────────────────────────────────────

        [Fact]
        public void Convertir_MonedaDolares()
        {
            Assert.Equal("CIEN CON 00/100 DÓLARES AMERICANOS",
                NumeroALetrasHelper.Convertir(100m, "DÓLARES AMERICANOS"));
        }

        [Fact]
        public void Convertir_MonedaVacia()
        {
            Assert.Equal("UNO CON 00/100", NumeroALetrasHelper.Convertir(1m, ""));
        }

        // ── Redondeo de decimales en borde ──────────────────────────────────────

        [Fact]
        public void Convertir_RedondeoAlSubir()
        {
            // 0.999 → decimales = round(0.999 * 100) = 100 → entero sube a 1, decimales = 0
            Assert.Equal("UNO CON 00/100 SOLES", NumeroALetrasHelper.Convertir(0.999m, "SOLES"));
        }
    }
}
