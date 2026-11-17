using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Conductor;
using WebSGV.Services.Conductor;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Orden de las instrucciones del lote de la liquidación del conductor (2026-11-17, carrera entre
    /// "Anular lote" y liquidar): lo primero es bloquear los viajes; en una orden nueva sin número, el
    /// número se genera después del bloqueo y antes de insertar la orden, dentro de la misma transacción.
    /// </summary>
    public class LoteLiquidacionConductorTests
    {
        private static LiquidacionConductorInput Input(bool reliquidacion, string numero) => new LiquidacionConductorInput
        {
            EsReliquidacion = reliquidacion,
            NumeroOrdenViaje = numero,
            FechaSalida = new DateTime(2026, 11, 16),
            FechaLlegada = new DateTime(2026, 11, 17),
            HoraSalida = "08:00",
            HoraLlegada = "18:00",
            HoraLlegadaDeclarada = "17:45",
            IdConductor = 7,
            IdConductorActual = 7,
            IdViajeProgreso = 11,
            IdsViajesActivos = new List<int> { 11, 12 },
            AlimentacionSoles = 20m,
            GastosAdicionales = { new GastoAdicionalData { Categoria = "Lavado", Soles = 10m, Dolares = 0m } }
        };

        private static int Posicion(IReadOnlyList<string> instrucciones, string sp) =>
            instrucciones.ToList().FindIndex(i => i.Contains(sp));

        [Theory]
        [InlineData(false, null)]
        [InlineData(false, "OV-TEST-1")]
        [InlineData(true, "OV-2026-000001")]
        public void LaPrimeraInstruccionBloqueaLosViajes(bool reliquidacion, string numero)
        {
            var instrucciones = LiquidacionConductorService.InstruccionesDelLote(Input(reliquidacion, numero));

            Assert.StartsWith("EXEC sp_DC_BloquearViajesParaLiquidar ", instrucciones[0]);
            Assert.Contains("@idsViajes", instrucciones[0]);
            Assert.Contains("@idConductor", instrucciones[0]);
            Assert.Equal(1, instrucciones.Count(i => i.Contains("sp_DC_BloquearViajesParaLiquidar")));
            // El cierre de los viajes sigue al final.
            Assert.Contains("sp_DC_CerrarViajesProgreso", instrucciones[instrucciones.Count - 1]);
        }

        [Fact]
        public void OrdenNuevaSinNumero_LoGeneraTrasElBloqueoYAntesDeInsertar()
        {
            var instrucciones = LiquidacionConductorService.InstruccionesDelLote(Input(false, null));

            Assert.StartsWith("EXEC sp_DC_GenerarNumeroOrden ", instrucciones[1]);
            Assert.True(Posicion(instrucciones, "sp_DC_InsertarOrdenViajeConductor") > 1);
        }

        [Theory]
        [InlineData(false, "OV-TEST-1")]
        [InlineData(true, "OV-2026-000001")]
        public void ConNumero_NoGeneraOtro(bool reliquidacion, string numero)
        {
            var instrucciones = LiquidacionConductorService.InstruccionesDelLote(Input(reliquidacion, numero));

            Assert.Equal(-1, Posicion(instrucciones, "sp_DC_GenerarNumeroOrden"));
        }
    }
}
