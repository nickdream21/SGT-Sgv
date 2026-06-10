using System;
using WebSGV.Services.OrdenViaje;
using Xunit;

namespace WebSGV.Tests
{
    public class OrdenViajeValidacionesTests
    {
        // ---- ValidarFormatoNumeroOrden ----

        [Fact]
        public void ValidarFormatoNumeroOrden_Valido_RetornaVacio()
        {
            Assert.Equal(string.Empty, OrdenViajeValidaciones.ValidarFormatoNumeroOrden("123456"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ValidarFormatoNumeroOrden_VacioONulo_Obligatorio(string entrada)
        {
            Assert.Equal("El número de orden es obligatorio.",
                OrdenViajeValidaciones.ValidarFormatoNumeroOrden(entrada));
        }

        [Theory]
        [InlineData("123")]
        [InlineData("1234567")]
        public void ValidarFormatoNumeroOrden_LongitudIncorrecta_MensajeLongitud(string entrada)
        {
            Assert.Contains("exactamente 6 dígitos",
                OrdenViajeValidaciones.ValidarFormatoNumeroOrden(entrada));
        }

        [Fact]
        public void ValidarFormatoNumeroOrden_ConLetras_MensajeSoloNumeros()
        {
            Assert.Equal("El número de orden debe contener solo números (0-9).",
                OrdenViajeValidaciones.ValidarFormatoNumeroOrden("12345a"));
        }

        [Fact]
        public void ValidarFormatoNumeroOrden_TodoCeros_NoPermitido()
        {
            Assert.Equal("El número de orden no puede ser '000000'.",
                OrdenViajeValidaciones.ValidarFormatoNumeroOrden("000000"));
        }

        // ---- ValidarFormatoHora ----

        [Theory]
        [InlineData("08:30")]
        [InlineData("8:30")]
        [InlineData("00:00")]
        [InlineData("23:59")]
        public void ValidarFormatoHora_Validas(string hora)
        {
            Assert.True(OrdenViajeValidaciones.ValidarFormatoHora(hora));
        }

        [Theory]
        [InlineData("24:00")]
        [InlineData("12:60")]
        [InlineData("08:30:00")]
        [InlineData("8.30")]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData(null)]
        public void ValidarFormatoHora_Invalidas(string hora)
        {
            Assert.False(OrdenViajeValidaciones.ValidarFormatoHora(hora));
        }

        // ---- EsFechaValidaSQL ----

        [Fact]
        public void EsFechaValidaSQL_FechaNormal_Valida()
        {
            Assert.True(OrdenViajeValidaciones.EsFechaValidaSQL(new DateTime(2026, 6, 10)));
        }

        [Fact]
        public void EsFechaValidaSQL_LimiteInferior_Valida()
        {
            Assert.True(OrdenViajeValidaciones.EsFechaValidaSQL(new DateTime(1753, 1, 1)));
        }

        [Fact]
        public void EsFechaValidaSQL_MinValue_NoValida()
        {
            Assert.False(OrdenViajeValidaciones.EsFechaValidaSQL(DateTime.MinValue));
        }

        [Fact]
        public void EsFechaValidaSQL_AntesDe1753_NoValida()
        {
            Assert.False(OrdenViajeValidaciones.EsFechaValidaSQL(new DateTime(1752, 12, 31)));
        }
    }
}
