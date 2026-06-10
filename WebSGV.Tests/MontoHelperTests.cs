using System;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    public class MontoHelperTests
    {
        // ---- ParseMonto (cultura invariante, clamp a 0) ----

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ParseMonto_VacioONulo_RetornaCero(string entrada)
        {
            Assert.Equal(0m, MontoHelper.ParseMonto(entrada));
        }

        [Fact]
        public void ParseMonto_DecimalConPunto_Parsea()
        {
            Assert.Equal(10.50m, MontoHelper.ParseMonto("10.50"));
        }

        [Fact]
        public void ParseMonto_Negativo_ClampeaACero()
        {
            Assert.Equal(0m, MontoHelper.ParseMonto("-5"));
        }

        [Fact]
        public void ParseMonto_FormatoInvalido_RetornaCero()
        {
            Assert.Equal(0m, MontoHelper.ParseMonto("abc"));
        }

        // ---- ValidarMonto ----

        [Fact]
        public void ValidarMonto_Nulo_RetornaCero()
        {
            Assert.Equal(0m, MontoHelper.ValidarMonto((decimal?)null, "campo"));
        }

        [Fact]
        public void ValidarMonto_Valido_RetornaElValor()
        {
            Assert.Equal(1500m, MontoHelper.ValidarMonto(1500m, "peajes"));
        }

        [Fact]
        public void ValidarMonto_EnElLimite_RetornaElValor()
        {
            Assert.Equal(MontoHelper.MontoMaximo, MontoHelper.ValidarMonto(MontoHelper.MontoMaximo, "campo"));
        }

        [Fact]
        public void ValidarMonto_Negativo_Lanza()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => MontoHelper.ValidarMonto(-1m, "descuento"));
            Assert.Contains("descuento", ex.Message);
        }

        [Fact]
        public void ValidarMonto_ExcedeMaximo_Lanza()
        {
            Assert.Throws<InvalidOperationException>(
                () => MontoHelper.ValidarMonto(MontoHelper.MontoMaximo + 1m, "reintegro"));
        }

        // ---- EsMontoNoNegativo (cultura actual) ----

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("100", true)]
        [InlineData("0", true)]
        [InlineData("-5", false)]
        [InlineData("abc", false)]
        public void EsMontoNoNegativo_Casos(string entrada, bool esperado)
        {
            Assert.Equal(esperado, MontoHelper.EsMontoNoNegativo(entrada));
        }

        // ---- ConvertToDecimal ----

        [Theory]
        [InlineData("123", 123)]
        [InlineData("0", 0)]
        [InlineData("abc", 0)]
        [InlineData("", 0)]
        public void ConvertToDecimal_Casos(string entrada, decimal esperado)
        {
            Assert.Equal(esperado, MontoHelper.ConvertToDecimal(entrada));
        }
    }
}
