using System;
using WebSGV.Services.OrdenViaje;
using Xunit;

namespace WebSGV.Tests
{
    public class OrdenViajeValidacionesTests
    {
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

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_HoraLlegadaDeclaradaVacia_NoEsError()
        {
            DateTime hoy = DateTime.Today;
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                hoy, hoy, "08:30", "18:00", horaLlegadaDeclarada: "");
            Assert.Equal(string.Empty, r);
        }

        [Fact]
        public void ValidarDatosGeneralesLiquidacion_HoraLlegadaDeclaradaConFormatoInvalido_Error()
        {
            DateTime hoy = DateTime.Today;
            string r = OrdenViajeValidaciones.ValidarDatosGeneralesLiquidacion(
                hoy, hoy, "08:30", "18:00", horaLlegadaDeclarada: "25:99");
            Assert.Contains("'Hora Real de Llegada' es incorrecto", r);
        }
    }
}
