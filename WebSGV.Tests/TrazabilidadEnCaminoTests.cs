using System;
using System.Collections.Generic;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Salida de base mientras la unidad va en camino, antes de su primera parada (planta de Trujillo
    /// o Complex): casos BFH-852 (descarga) y CBU-945 (carga) del 20/10/2026, que seguían "en base".
    /// </summary>
    public class TrazabilidadEnCaminoTests
    {
        // 20/10/2026 09:00 hora de Perú.
        private static readonly DateTime T0 = new DateTime(2026, 10, 20, 14, 0, 0, DateTimeKind.Utc);
        private const double BaseLat = -4.9562, BaseLng = -80.6995;   // dentro de la base de Sullana
        private const double PuertaLat = -4.9520, PuertaLng = -80.6950; // ~600 m afuera, en la Panamericana

        private sealed class Recorrido
        {
            public readonly List<OnwayHistoryPoint> Puntos = new List<OnwayHistoryPoint>();
            private int _minuto;

            public Recorrido En(double lat, double lng, int veces = 1, double vel = 40)
            {
                for (int i = 0; i < veces; i++, _minuto += 5)
                    Puntos.Add(new OnwayHistoryPoint { MessageTime = T0.AddMinutes(_minuto), Lat = lat, Lng = lng, Speed = vel });
                return this;
            }

            public DateTime HoraDelPunto(int indice) => Puntos[indice].MessageTime;
        }

        [Fact]
        public void Descarga_SalioYVaHaciaTalara_RegistraLaSalida()
        {
            var r = new Recorrido()
                .En(BaseLat, BaseLng, 24, 0)         // 2 h en la base
                .En(PuertaLat, PuertaLng)             // índice 24: primer punto afuera
                .En(-4.92, -80.70).En(-4.88, -80.73)
                .En(-4.8488, -80.7908);               // Marcavelica/Mallares, 15 km

            var d = TrazabilidadDescargaService.DetectarTramoDescarga(r.Puntos, T0.AddHours(-1));

            Assert.NotNull(d.SalidaBase);
            Assert.Equal(r.HoraDelPunto(24), d.SalidaBase.HoraUtc);
            Assert.Null(d.LlegadaComplex);
        }

        [Fact]
        public void Descarga_SalioPeroAunCercaDeLaBase_NoRegistraNada()
        {
            var r = new Recorrido()
                .En(BaseLat, BaseLng, 12, 0)
                .En(PuertaLat, PuertaLng).En(-4.93, -80.70).En(-4.90, -80.71); // ~6 km: puede ser el grifo

            Assert.Null(TrazabilidadDescargaService.DetectarTramoDescarga(r.Puntos, T0.AddHours(-1)).SalidaBase);
        }

        [Fact]
        public void Descarga_VueltaDeRutinaYSigueEnLaBase_NoRegistraNada()
        {
            var r = new Recorrido()
                .En(BaseLat, BaseLng, 12, 0)
                .En(PuertaLat, PuertaLng).En(-4.86, -80.75)   // salió al norte...
                .En(PuertaLat, PuertaLng).En(BaseLat, BaseLng, 12, 0); // ...y volvió: sigue adentro

            Assert.Null(TrazabilidadDescargaService.DetectarTramoDescarga(r.Puntos, T0.AddHours(-1)).SalidaBase);
        }

        [Fact]
        public void Descarga_VueltaDeRutinaYLuegoSaleDeVerdad_TomaLaUltimaSalida()
        {
            var r = new Recorrido()
                .En(BaseLat, BaseLng, 12, 0)
                .En(PuertaLat, PuertaLng).En(-4.93, -80.70)            // grifo
                .En(BaseLat, BaseLng, 12, 0)                            // vuelve a la base
                .En(PuertaLat, PuertaLng)                               // índice 26: sale con la carreta
                .En(-4.88, -80.73).En(-4.80, -80.85);

            var d = TrazabilidadDescargaService.DetectarTramoDescarga(r.Puntos, T0.AddHours(-1));

            Assert.Equal(r.HoraDelPunto(26), d.SalidaBase.HoraUtc);
        }

        [Fact]
        public void Carga_SalioYPasoPiuraHaciaElSur_RegistraLaSalida()
        {
            var r = new Recorrido()
                .En(BaseLat, BaseLng, 12, 0)
                .En(PuertaLat, PuertaLng)                 // índice 12
                .En(-5.10, -80.65).En(-5.19, -80.63)      // Piura
                .En(-5.60, -80.45).En(-6.20, -80.00);     // rumbo a Chiclayo

            var c = TrazabilidadCargaService.DetectarTramoCarga(r.Puntos, T0.AddHours(-1));

            Assert.Equal(r.HoraDelPunto(12), c.SalidaBase.HoraUtc);
            Assert.Null(c.IngresoPlanta);
        }

        [Fact]
        public void Carga_SoloHastaPiura_NoRegistraNada()
        {
            var r = new Recorrido()
                .En(BaseLat, BaseLng, 12, 0)
                .En(PuertaLat, PuertaLng).En(-5.10, -80.65).En(-5.19, -80.63, 6, 0); // taller en Piura

            Assert.Null(TrazabilidadCargaService.DetectarTramoCarga(r.Puntos, T0.AddHours(-1)).SalidaBase);
        }
    }
}
