using System;
using System.Collections.Generic;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    public class CheckpointMatchingServiceTests
    {
        private static OnwayHistoryPoint Punto(DateTime hora, double lat, double lng, string alertEn = null) =>
            new OnwayHistoryPoint
            {
                MessageTime = hora,
                Lat = lat,
                Lng = lng,
                AlertDescription = alertEn == null ? null : new OnwayTextoMultiIdioma { En = alertEn }
            };

        // ---- CalcularDistanciaMetros ----

        [Fact]
        public void CalcularDistanciaMetros_PuntosIguales_RetornaCero()
        {
            double d = CheckpointMatchingService.CalcularDistanciaMetros(-4.956195, -80.699310, -4.956195, -80.699310);
            Assert.Equal(0, d, 3);
        }

        [Fact]
        public void CalcularDistanciaMetros_UnGradoDeLatitud_RetornaAproximadamente111Km()
        {
            // 1 grado de latitud equivale a ~111.32 km en cualquier punto de la Tierra.
            double d = CheckpointMatchingService.CalcularDistanciaMetros(0, 0, 1, 0);
            Assert.InRange(d, 110500, 111500);
        }

        // ---- DetectarLlegada ----

        [Fact]
        public void DetectarLlegada_HistorialNull_RetornaNull()
        {
            var resultado = CheckpointMatchingService.DetectarLlegada(null, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarLlegada_HistorialVacio_RetornaNull()
        {
            var resultado = CheckpointMatchingService.DetectarLlegada(new List<OnwayHistoryPoint>(), 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarLlegada_NingunPuntoDentroDelRadio_RetornaNull()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 10, 0, 0), 10, 10) // muy lejos del checkpoint (0,0)
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);
            Assert.Null(resultado);
        }

        [Fact]
        public void DetectarLlegada_SinEventoApagado_RetornaPrimerPuntoDentroDelRadioPorHora()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 11, 0, 0), 10, 10),      // fuera de radio
                Punto(new DateTime(2026, 8, 25, 12, 30, 0), 0, 0),       // dentro del radio - más tarde
                Punto(new DateTime(2026, 8, 25, 12, 0, 0), 0.0005, 0.0005) // dentro del radio - más temprano
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 12, 0, 0), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_ConEventoIgnitionOffDentroDelRadio_PriorizaEseEventoSobreUnPuntoCrudoAnterior()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                // Punto crudo dentro del radio, más temprano que el evento de apagado.
                Punto(new DateTime(2026, 8, 25, 12, 0, 0), 0, 0),
                // Evento "Ignition Off" dentro del radio, más tarde.
                Punto(new DateTime(2026, 8, 25, 12, 30, 33), 0, 0, "Ignition Off, Vehicle AVM-877")
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 12, 30, 33), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_EventoIgnitionOffFueraDelRadio_NoLoPrioriza()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 12, 0, 0), 0, 0),                         // dentro del radio
                Punto(new DateTime(2026, 8, 25, 12, 30, 0), 10, 10, "Ignition Off, X")     // apagado, pero lejos
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 12, 0, 0), resultado.MessageTime);
        }

        [Fact]
        public void DetectarLlegada_HistorialDesordenado_LoOrdenaPorHoraAntesDeMatchear()
        {
            var historial = new List<OnwayHistoryPoint>
            {
                Punto(new DateTime(2026, 8, 25, 13, 0, 0), 0, 0),
                Punto(new DateTime(2026, 8, 25, 11, 0, 0), 0, 0)
            };

            var resultado = CheckpointMatchingService.DetectarLlegada(historial, 0, 0, 400);

            Assert.NotNull(resultado);
            Assert.Equal(new DateTime(2026, 8, 25, 11, 0, 0), resultado.MessageTime);
        }
    }
}
