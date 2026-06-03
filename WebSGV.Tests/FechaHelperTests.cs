using System;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class FechaHelperTests
    {
        [Fact]
        public void Ahora_RetornaDateTimeNoDefault()
        {
            DateTime resultado = FechaHelper.Ahora();
            Assert.NotEqual(default(DateTime), resultado);
        }

        [Fact]
        public void Hoy_SoloTieneFecha_SinHora()
        {
            DateTime resultado = FechaHelper.Hoy();
            Assert.Equal(TimeSpan.Zero, resultado.TimeOfDay);
        }

        [Fact]
        public void Hoy_CoincideConFechaDeAhora()
        {
            Assert.Equal(FechaHelper.Ahora().Date, FechaHelper.Hoy());
        }

        [Fact]
        public void Ahora_EstaEnRangoRazonable()
        {
            DateTime resultado = FechaHelper.Ahora();
            DateTime utcNow   = DateTime.UtcNow;

            // Perú es UTC-5: resultado debe estar entre UTC-6 y UTC-4 (tolerancia de 1h)
            Assert.True(resultado >= utcNow.AddHours(-6));
            Assert.True(resultado <= utcNow.AddHours(-4));
        }

        [Fact]
        public void Ahora_DesfaseRespectoUtcEsCincoHoras()
        {
            DateTime utcNow   = DateTime.UtcNow;
            DateTime resultado = FechaHelper.Ahora();

            // El desfase debe ser exactamente -5h (±1 min por latencia de ejecución)
            double desfaseHoras = (resultado - utcNow).TotalHours;
            Assert.True(desfaseHoras > -5.02 && desfaseHoras < -4.98,
                $"Desfase esperado ~-5h, obtenido {desfaseHoras:F4}h");
        }
    }
}
