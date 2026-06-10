using System;
using WebSGV.Services.Facturas;
using Xunit;

namespace WebSGV.Tests
{
    public class FacturaValidacionesTests
    {
        [Theory]
        [InlineData("F222 - 00004267")]
        [InlineData("001-098-000326757")]
        public void ValidarYFormatearNumeroFactura_FormatoCanonico_SinCambios(string entrada)
        {
            Assert.Equal(entrada, FacturaValidaciones.ValidarYFormatearNumeroFactura(entrada));
        }

        [Fact]
        public void ValidarYFormatearNumeroFactura_DoceAlfanumericosConF_SeFormatea()
        {
            Assert.Equal("F222 - 00004267",
                FacturaValidaciones.ValidarYFormatearNumeroFactura("F22200004267"));
        }

        [Fact]
        public void ValidarYFormatearNumeroFactura_MinusculaYSeparadores_SeNormaliza()
        {
            Assert.Equal("F222 - 00004267",
                FacturaValidaciones.ValidarYFormatearNumeroFactura("f222/00004267"));
        }

        [Fact]
        public void ValidarYFormatearNumeroFactura_QuinceDigitos_SeFormatea()
        {
            Assert.Equal("001-098-000326757",
                FacturaValidaciones.ValidarYFormatearNumeroFactura("001098000326757"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidarYFormatearNumeroFactura_Vacio_Lanza(string entrada)
        {
            Assert.Throws<FormatException>(
                () => FacturaValidaciones.ValidarYFormatearNumeroFactura(entrada));
        }

        [Theory]
        [InlineData("xyz")]
        [InlineData("12345")]
        public void ValidarYFormatearNumeroFactura_Irreparable_Lanza(string entrada)
        {
            Assert.Throws<FormatException>(
                () => FacturaValidaciones.ValidarYFormatearNumeroFactura(entrada));
        }
    }
}
