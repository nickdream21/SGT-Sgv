using System;
using System.Collections.Generic;
using WebSGV.Helpers;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Dónde empieza el recorrido de la carga de un cliente tercero en el mapa del viaje.</summary>
    public class RutaInicioTerceroTests
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Punto [lat, lng, ms, velocidad] a la hora local de Perú.</summary>
        private static double[] P(double lat, double lng, DateTime local) =>
            new[] { lat, lng, (FechaHelper.ConvertirAUtc(local) - Epoch).TotalMilliseconds, 0 };

        private const double BaseLat = -4.95630, BaseLng = -80.69900;          // dentro del terreno de la base de Sullana
        private static readonly DateTime Llegada = new DateTime(2026, 10, 19, 6, 15, 0); // a Cartavio

        private static double Ancla => (FechaHelper.ConvertirAUtc(Llegada) - Epoch).TotalMilliseconds;

        [Fact]
        public void SalioDeBase_ElRecorridoEmpiezaEnLaSalida()
        {
            var puntos = new List<double[]>
            {
                P(-3.43, -79.96, new DateTime(2026, 10, 17, 18, 0, 0)),     // volviendo de Ecuador
                P(BaseLat, BaseLng, new DateTime(2026, 10, 17, 20, 48, 0)), // llegó a la base
                P(BaseLat, BaseLng, new DateTime(2026, 10, 18, 9, 0, 0)),
                P(-5.20, -80.62, new DateTime(2026, 10, 18, 10, 30, 0)),   // salió
                P(-7.88, -79.21, new DateTime(2026, 10, 19, 6, 15, 0)),    // Cartavio
            };
            var r = RutaViajeService.RecortarInicio(puntos, Ancla, "venía de descargar en Jave (Vitapro 4400133873, salió el 17/10 12:30)");
            Assert.Equal(3, r.Item1.Count);
            Assert.StartsWith("Salió de la base el 18/10 10:30 (venía de descargar en Jave", r.Item2);
        }

        [Fact]
        public void VueltaCorrida_EmpiezaDondeTerminoElViajeAnteriorYAvisaLaParadaLarga()
        {
            var puntos = new List<double[]>
            {
                P(-2.18, -79.80, new DateTime(2026, 10, 17, 15, 0, 0)),    // saliendo de Jave
                P(-4.87, -80.77, new DateTime(2026, 10, 18, 10, 21, 0)),   // frente a la base, sin entrar
                P(-6.754, -79.866, new DateTime(2026, 10, 18, 18, 16, 0)), // Pimentel
                P(-6.754, -79.866, new DateTime(2026, 10, 19, 0, 30, 0)),  // sigue ahí 6 h
                P(-7.88, -79.21, new DateTime(2026, 10, 19, 6, 15, 0)),
            };
            var r = RutaViajeService.RecortarInicio(puntos, Ancla, "venía de descargar en Jave (Vitapro 4400133873, salió el 17/10 15:00)");
            Assert.Equal(5, r.Item1.Count);
            Assert.StartsWith("Venía de descargar en Jave (Vitapro 4400133873, salió el 17/10 15:00) y no entró a la base antes de ir a cargar", r.Item2);
            Assert.Contains("parada de 6 h del 18/10 18:16 al 19/10 00:30", r.Item2);
        }

        [Fact]
        public void SinViajeAnteriorNiSalidaDeBase_LoDice()
        {
            var puntos = new List<double[]> { P(-6.0, -80.5, new DateTime(2026, 10, 18, 12, 0, 0)), P(-7.88, -79.21, Llegada) };
            var r = RutaViajeService.RecortarInicio(puntos, Ancla, null);
            Assert.Equal(2, r.Item1.Count);
            Assert.StartsWith("No salió de la base en los 4 días antes de llegar a cargar", r.Item2);
        }

        [Fact]
        public void UnaSalidaDeBaseDespuesDeCargar_NoCuenta()
        {
            var puntos = new List<double[]>
            {
                P(-6.0, -80.5, new DateTime(2026, 10, 18, 12, 0, 0)),
                P(-7.88, -79.21, Llegada),
                P(BaseLat, BaseLng, new DateTime(2026, 10, 20, 14, 0, 0)), // vuelve cargado
                P(-4.90, -80.60, new DateTime(2026, 10, 20, 16, 0, 0)),
            };
            var r = RutaViajeService.RecortarInicio(puntos, Ancla, null);
            Assert.Equal(4, r.Item1.Count);
        }
    }
}
