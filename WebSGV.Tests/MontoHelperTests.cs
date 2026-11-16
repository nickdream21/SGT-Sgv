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
        public void ValidarMonto_Negativo_LanzaErrorDeNegocioConElCampo()
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.ValidarMonto(-1m, "descuento"));
            Assert.Contains("descuento", ex.Message);
            // Mensaje pensado para el usuario: MensajeErrorHelper lo muestra tal cual.
            Assert.Equal(ex.Message, MensajeError.ExtraerMensajeDeNegocio(ex));
        }

        [Fact]
        public void ValidarMonto_ExcedeMaximo_LanzaErrorDeNegocioConElCampo()
        {
            var ex = Assert.Throws<ErrorNegocioException>(
                () => MontoHelper.ValidarMonto(MontoHelper.MontoMaximo + 1m, "reintegro"));
            Assert.Contains("reintegro", ex.Message);
            Assert.Contains("máximo", ex.Message);
        }

        [Fact]
        public void ValidarMonto_SigueSiendoInvalidOperation_ParaLosCatchExistentes()
        {
            Assert.IsAssignableFrom<InvalidOperationException>(
                Assert.ThrowsAny<Exception>(() => MontoHelper.ValidarMonto(-1m, "x")));
        }

        // ---- LeerMonto (campos del formulario con tope) ----

        [Theory]
        [InlineData(null, "0")]
        [InlineData("", "0")]
        [InlineData("  ", "0")]
        [InlineData("10.50", "10.50")]
        [InlineData(" 250 ", "250")]
        [InlineData("1e3", "1000")]
        [InlineData("9999999", "9999999")]
        public void LeerMonto_Validos(string entrada, string esperado)
        {
            Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture),
                MontoHelper.LeerMonto(entrada, "Alimentación (S/)"));
        }

        [Theory]
        [InlineData("2000000000")]
        [InlineData("1e15")]
        [InlineData("10000000")]
        public void LeerMonto_SobreElTope_LanzaConElNombreDelCampo(string entrada)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(entrada, "Combustible (S/)"));
            Assert.Contains("Combustible (S/)", ex.Message);
            Assert.Contains("máximo", ex.Message);
        }

        [Fact]
        public void LeerMonto_Negativo_Lanza()
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto("-5", "Movilidad (S/)"));
            Assert.Contains("negativo", ex.Message);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("1e400")]
        [InlineData("12,50")]
        public void LeerMonto_NoNumerico_Lanza(string entrada)
        {
            var ex = Assert.Throws<ErrorNegocioException>(() => MontoHelper.LeerMonto(entrada, "Hospedaje ($)"));
            Assert.Contains("Hospedaje ($)", ex.Message);
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
