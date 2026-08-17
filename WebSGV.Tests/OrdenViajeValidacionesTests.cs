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

        // ---- ValidarDatosGeneralesOrdenViaje ----

        [Fact]
        public void ValidarDatosGeneralesOrdenViaje_TodoValido_RetornaVacio()
        {
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesOrdenViaje(
                new DateTime(2026, 6, 10), new DateTime(2026, 6, 11), "08:30", "18:00");
            Assert.Equal(string.Empty, r);
        }

        [Fact]
        public void ValidarDatosGeneralesOrdenViaje_FaltanCampos_AcumulaCuatroErrores()
        {
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesOrdenViaje(
                DateTime.MinValue, DateTime.MinValue, "", "");
            Assert.Contains("'Fecha de Salida'", r);
            Assert.Contains("'Hora de Salida'", r);
            Assert.Contains("'Fecha de Llegada'", r);
            Assert.Contains("'Hora de Llegada'", r);
        }

        [Fact]
        public void ValidarDatosGeneralesOrdenViaje_SalidaPosteriorALlegada_Error()
        {
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesOrdenViaje(
                new DateTime(2026, 6, 12), new DateTime(2026, 6, 10), "08:30", "18:00");
            Assert.Contains("no puede ser mayor a la 'Fecha de Llegada'", r);
        }

        [Fact]
        public void ValidarDatosGeneralesOrdenViaje_NoValidaFormatoHora()
        {
            // A diferencia de la variante de liquidación, esta NO valida el formato de hora.
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesOrdenViaje(
                new DateTime(2026, 6, 10), new DateTime(2026, 6, 11), "25:99", "xx:yy");
            Assert.Equal(string.Empty, r);
        }

        // ---- ValidarDatosGeneralesLiquidacion ----

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_TodoValido_RetornaVacio()
        {
            DateTime salida = DateTime.Today;
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                salida, salida, "08:30", "18:00");
            Assert.Equal(string.Empty, r);
        }

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_FaltanFechas_AcumulaErrores()
        {
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                DateTime.MinValue, DateTime.MinValue, "08:30", "18:00");
            Assert.Contains("'Fecha de Salida'", r);
            Assert.Contains("'Fecha de Llegada'", r);
        }

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_FechasMayoresAUnAnio_Error()
        {
            DateTime lejana = DateTime.Today.AddYears(2);
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                lejana, lejana, "08:30", "18:00");
            Assert.Contains("superiores a un año desde hoy", r);
        }

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_HoraConFormatoInvalido_Error()
        {
            DateTime hoy = DateTime.Today;
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                hoy, hoy, "25:99", "18:00");
            Assert.Contains("'Hora de Salida' es incorrecto", r);
        }

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_HoraSalidaVacia_Error()
        {
            DateTime hoy = DateTime.Today;
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                hoy, hoy, "", "18:00");
            Assert.Contains("ingrese la 'Hora de Salida'", r);
        }

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_SalidaPosteriorALlegada_Error()
        {
            DateTime salida = DateTime.Today;
            DateTime llegada = DateTime.Today.AddDays(-1);
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                salida, llegada, "08:30", "18:00");
            Assert.Contains("no puede ser mayor a la 'Fecha de Llegada'", r);
        }
    }
}
