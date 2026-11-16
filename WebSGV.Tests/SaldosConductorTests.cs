using System;
using System.Data;
using System.Linq;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.Reportes;
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

        /// <summary>Filas como las de <see cref="SaldosConductorService.Listar"/> (solo las columnas que usan los totales y el reporte).</summary>
        private static DataTable Filas(params (int conductor, decimal soles, decimal dolares, bool pagada)[] filas)
        {
            var dt = new DataTable();
            dt.Columns.Add("idOrdenViaje", typeof(int));
            dt.Columns.Add("numeroOrdenViaje", typeof(string));
            dt.Columns.Add("idConductor", typeof(int));
            dt.Columns.Add("Conductor", typeof(string));
            dt.Columns.Add("Dni", typeof(string));
            dt.Columns.Add("FechaAprobacion", typeof(DateTime));
            dt.Columns.Add("EntregadoSoles", typeof(decimal));
            dt.Columns.Add("GastosSoles", typeof(decimal));
            dt.Columns.Add("SaldoSoles", typeof(decimal));
            dt.Columns.Add("SaldoDolares", typeof(decimal));
            dt.Columns.Add("Sentido", typeof(string));
            dt.Columns.Add("Estado", typeof(string));
            dt.Columns.Add("idPagoSaldo", typeof(int));
            int id = 0;
            foreach (var f in filas)
            {
                id++;
                dt.Rows.Add(id, "LIQ-" + id, f.conductor, "Conductor " + f.conductor, "0000000" + f.conductor, new DateTime(2026, 11, 10),
                    0m, 0m, f.soles, f.dolares, SaldosConductorService.Sentido(f.soles, f.dolares),
                    f.pagada ? "Saldado" : "Pendiente", f.pagada ? (object)id : DBNull.Value);
            }
            return dt;
        }

        [Fact]
        public void Totales_SoloPendientes_NetoPorConductorYPorMoneda()
        {
            var dt = Filas(
                (1, 100m, 0m, false), (1, -40m, 0m, false),   // conductor 1: neto +60 → a descontar
                (1, 500m, 0m, true),                          // ya saldada: no cuenta
                (2, -25m, -10m, false),                       // conductor 2: solo negativo → a reintegrar
                (3, 0m, 30m, false), (3, 0m, -5m, false));    // conductor 3: solo dólares, neto +25

            var t = SaldosConductorService.Totales(dt);

            Assert.Equal(60m, t.DescontarSoles);
            Assert.Equal(25m, t.ReintegrarSoles);
            Assert.Equal(25m, t.DescontarDolares);
            Assert.Equal(10m, t.ReintegrarDolares);
            Assert.Equal(5, t.Pendientes);
            Assert.True(t.HayDolares);
        }

        [Fact]
        public void Reporte_UsaLosMismosTotalesQueLaPantalla_YSuFilaDeTotalesNoSumaLasSaldadas()
        {
            var dt = Filas((1, 100m, 0m, false), (1, -40m, 0m, false), (1, 500m, 0m, true), (2, -25m, 0m, false));
            var t = SaldosConductorService.Totales(dt);

            var r = SaldosConductorService.Reporte(dt, new string[0], "prueba");

            Assert.Equal(t.DescontarSoles, r.Resumen.Single(l => l.Etiqueta == "A descontar a conductores (S/)").Valor);
            Assert.Equal(t.ReintegrarSoles, r.Resumen.Single(l => l.Etiqueta == "A reintegrar a conductores (S/)").Valor);
            Assert.Equal(3m, r.Resumen.Single(l => l.Etiqueta == "Liquidaciones pendientes de saldar").Valor);
            Assert.DoesNotContain(r.Resumen, l => l.Tipo == TipoColumnaReporte.Dolares);   // sin dólares no hay líneas en US$

            // Fila de totales: 100 − 40 − 25 = 35 (la saldada de 500 no entra).
            var saldo = r.Columnas.Single(c => c.Titulo == "SALDO S/");
            Assert.Equal(35m, ReporteTabularExportador.TotalColumna(r, saldo));
            Assert.Equal(t.DescontarSoles - t.ReintegrarSoles, ReporteTabularExportador.TotalColumna(r, saldo));

            // El Excel se genera con el total ya calculado (sin fórmula SUBTOTAL sobre todas las filas).
            Assert.NotEmpty(ReporteTabularExportador.GenerarExcel(r, null));
        }

        [Fact]
        public void NombreQuincena()
        {
            Assert.Equal("1.ª quincena de noviembre 2026", SaldosConductorService.NombreQuincena(new DateTime(2026, 11, 1)));
            Assert.Equal("2.ª quincena de noviembre 2026", SaldosConductorService.NombreQuincena(new DateTime(2026, 11, 16)));
        }
    }
}
