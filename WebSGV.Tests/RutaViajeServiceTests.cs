using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    public class RutaViajeServiceTests
    {
        private static readonly DateTime Base = new DateTime(2026, 10, 17, 10, 0, 0, DateTimeKind.Utc);
        private static readonly double MsBase = (Base - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        // ~111 m por cada 0.001° de latitud.
        private static OnwayHistoryPoint Punto(int minuto, double lat, double lng = -79.0, double vel = 50) =>
            new OnwayHistoryPoint { MessageTime = Base.AddMinutes(minuto), Lat = lat, Lng = lng, Speed = vel };

        [Fact]
        public void Simplificar_OrdenaPorHoraYDescartaSinCoordenadas()
        {
            var r = RutaViajeService.Simplificar(new[]
            {
                Punto(2, -8.002), Punto(0, -8.000), Punto(1, 0, 0), Punto(1, -8.001),
            });
            Assert.Equal(new[] { -8.000, -8.001, -8.002 }, r.Select(p => p[0]));
            Assert.Equal(MsBase, r[0][2]);
        }

        [Fact]
        public void Simplificar_UnidadParada_UnSoloPuntoConLaHoraDeSalida()
        {
            // Llega, espera 3 h en el mismo sitio (puntos a menos de 40 m) y sigue.
            var crudos = new List<OnwayHistoryPoint> { Punto(0, -8.000) };
            for (int m = 10; m <= 180; m += 10) crudos.Add(Punto(m, -8.0001, vel: 0));
            crudos.Add(Punto(190, -8.010));

            var r = RutaViajeService.Simplificar(crudos);

            Assert.Equal(3, r.Count);
            // El punto de la espera conserva la última hora allí: la animación se detiene en el sitio.
            Assert.Equal(MsBase + TimeSpan.FromMinutes(180).TotalMilliseconds, r[1][2]);
            Assert.Equal(0, r[1][3]);
            Assert.Equal(-8.010, r[2][0]);
        }

        [Fact]
        public void Simplificar_MuchosPuntos_SeReduceConservandoElPrimeroYElUltimo()
        {
            var crudos = Enumerable.Range(0, 6000).Select(i => Punto(i, -5.0 - i * 0.001)).ToList();

            var r = RutaViajeService.Simplificar(crudos);

            Assert.InRange(r.Count, 2000, 2501);
            Assert.Equal(-5.0, r.First()[0]);
            Assert.Equal(Math.Round(-5.0 - 5999 * 0.001, 5), r.Last()[0]);
            Assert.True(r.Zip(r.Skip(1), (a, b) => a[2] < b[2]).All(x => x), "Las horas deben quedar en orden");
        }
    }
}
