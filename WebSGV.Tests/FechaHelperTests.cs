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

        // ---- ConvertirDeUtc / ConvertirAUtc ----
        // Ambas sostienen el cursor de búsqueda de la integración GPS: la hora que devuelve
        // Onway viene en UTC y se guarda en BD en hora de Perú, así que para retomar la
        // búsqueda desde un valor ya confirmado hay que poder volver a UTC sin desfase.

        [Fact]
        public void ConvertirDeUtc_RestaCincoHoras()
        {
            DateTime utc = new DateTime(2026, 8, 27, 13, 24, 21, DateTimeKind.Utc);
            Assert.Equal(new DateTime(2026, 8, 27, 8, 24, 21), FechaHelper.ConvertirDeUtc(utc));
        }

        [Fact]
        public void ConvertirAUtc_SumaCincoHoras()
        {
            DateTime local = new DateTime(2026, 8, 27, 8, 24, 21);
            Assert.Equal(new DateTime(2026, 8, 27, 13, 24, 21), FechaHelper.ConvertirAUtc(local));
        }

        [Fact]
        public void ConvertirAUtc_EsElInversoDeConvertirDeUtc()
        {
            // Incluye una hora nocturna: es justo el caso donde la fecha local y la UTC no
            // coinciden (19:00-23:59 en Perú ya es el día siguiente en UTC).
            foreach (var utc in new[]
            {
                new DateTime(2026, 8, 26, 21, 13, 54, DateTimeKind.Utc),
                new DateTime(2026, 8, 27, 3, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 8, 28, 6, 35, 42, DateTimeKind.Utc),
            })
            {
                DateTime local = FechaHelper.ConvertirDeUtc(utc);
                Assert.Equal(utc, DateTime.SpecifyKind(FechaHelper.ConvertirAUtc(local), DateTimeKind.Utc));
            }
        }

        [Fact]
        public void ConvertirDeUtc_HoraNocturnaCambiaDeFecha()
        {
            // 03:30 UTC del 27 es todavía 22:30 del 26 en Perú. Comparar fechas entre zonas
            // (en vez de timestamps) es lo que rompía el filtro del cursor GPS.
            DateTime local = FechaHelper.ConvertirDeUtc(new DateTime(2026, 8, 27, 3, 30, 0, DateTimeKind.Utc));
            Assert.Equal(new DateTime(2026, 8, 26), local.Date);
        }
    }
}
