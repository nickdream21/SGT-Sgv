using System;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Quincenas y sentido del saldo de la cuenta corriente del conductor.</summary>
    public class SaldosConductorTests
    {
        [Theory]
        [InlineData("2026-11-01", "2026-11-01", "2026-11-15")]
        [InlineData("2026-11-15", "2026-11-01", "2026-11-15")]
        [InlineData("2026-11-16", "2026-11-16", "2026-11-30")]
        [InlineData("2026-02-20", "2026-02-16", "2026-02-28")]
        [InlineData("2028-02-29", "2028-02-16", "2028-02-29")]
        [InlineData("2026-12-31", "2026-12-16", "2026-12-31")]
        public void Quincena_DelUnoAlQuinceYDelDieciseisAlFinDeMes(string fecha, string desde, string hasta)
        {
            var q = SaldosConductorService.Quincena(DateTime.Parse(fecha));
            Assert.Equal(DateTime.Parse(desde), q.Item1);
            Assert.Equal(DateTime.Parse(hasta), q.Item2);
        }

        [Theory]
        [InlineData(150, 0, "A descontar")]      // le sobró y no lo devolvió
        [InlineData(-80, 0, "A reintegrar")]     // puso de su plata
        [InlineData(0, 0, "Cuadrado")]
        [InlineData(0, -20, "A reintegrar")]     // solo en dólares
        public void Sentido_SegunElSaldo(double soles, double dolares, string esperado)
        {
            Assert.Equal(esperado, SaldosConductorService.Sentido((decimal)soles, (decimal)dolares));
        }

        [Fact]
        public void NombreQuincena()
        {
            Assert.Equal("1.ª quincena de noviembre 2026", SaldosConductorService.NombreQuincena(new DateTime(2026, 11, 1)));
            Assert.Equal("2.ª quincena de noviembre 2026", SaldosConductorService.NombreQuincena(new DateTime(2026, 11, 16)));
        }
    }
}
