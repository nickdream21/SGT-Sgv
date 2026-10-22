using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Unidades activas de Vitapro para el Reporte GPS por placas.</summary>
    public class UnidadesActivasTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 22, 15, 0, 0);

        private static ViajeResumen Viaje(string tracto1, string tracto2, string grupo, double horasDesdeUltimoHito,
            string tipoRuta = null, int id = 0)
        {
            var horas = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase)
            {
                ["fhSalidaBase1"] = Ahora.AddHours(-horasDesdeUltimoHito),
            };
            return new ViajeResumen
            {
                Id = id, Tracto1 = tracto1, Tracto2 = tracto2, TipoRuta = tipoRuta, Horas = horas,
                FhProgramacion = Ahora.AddDays(-10),
                Fase = new FaseCalculada { Nombre = grupo, Grupo = grupo },
            };
        }

        [Fact]
        public void CargaUsaElTractoDeCarga_DesdeLaBaseElDeDescarga()
        {
            Assert.Equal("BPS-816", UnidadesActivas.Tracto(Viaje("BPS-816", null, "carga", 1)));
            Assert.Equal("BPS-816", UnidadesActivas.Tracto(Viaje("BPS-816", "ARN-917", "carga", 1)));
            // En la base con descarga asignada: la lleva el de descarga aunque el GPS no marque su salida (BFK-879 del 22/10).
            Assert.Equal("BFK-879", UnidadesActivas.Tracto(Viaje("T6F-864", "BFK-879", "base", 1)));
            Assert.Equal("ANU-794", UnidadesActivas.Tracto(Viaje("ANU-794", null, "base", 1)));
            Assert.Equal("ARN-917", UnidadesActivas.Tracto(Viaje("BPS-816", "ARN-917", "retorno", 1)));
            Assert.Equal("CBU-945", UnidadesActivas.Tracto(Viaje("CBU-945", "CBU-945", "ecuador", 1)));
        }

        [Fact]
        public void SinCerradosNiTercerosNiViajesQueNoAvanzan_YSinRepetir()
        {
            var viajes = new List<ViajeResumen>
            {
                Viaje("AMQ-859", "AMQ-859", "retorno", 3),
                Viaje("ASU-748", "ASU-748", "cerrado", 1),
                Viaje("CBV-761", null, "carga", 2, tipoRuta: "CRISAR"),
                Viaje("BNG-862", "BFR-730", "base", 24 * 15),  // quedó abierto el 07/10
                Viaje("C4Z-745", "C4Z-745", "ecuador", 24 * 6), // el GPS le perdió el rastro
                Viaje("T6F-864", "BFK-879", "base", 20, id: 1),
                Viaje("T6F-864", null, "carga", 2, id: 2),      // el mismo tracto ya carga otro viaje
                Viaje("t6f864", null, "carga", 30, id: 3),
            };

            var activas = UnidadesActivas.DeVitapro(viajes, Ahora);

            Assert.Equal(new[] { "T6F-864", "BFK-879", "AMQ-859" }, activas.Select(u => u.Placa).ToArray());
            Assert.Equal(2, activas[0].Viaje.Id); // de los dos viajes de T6F-864 queda el más reciente
        }

        [Fact]
        public void ProgramadoSinHitos_CuentaPorSuProgramacion()
        {
            var nuevo = Viaje("CBV-854", null, "carga", 0);
            nuevo.Horas.Clear();
            nuevo.FhProgramacion = Ahora.AddDays(1);
            var viejo = Viaje("A7N-822", null, "carga", 0);
            viejo.Horas.Clear();
            viejo.FhProgramacion = Ahora.AddDays(-38);

            Assert.Equal(new[] { "CBV-854" }, UnidadesActivas.DeVitapro(new[] { nuevo, viejo }, Ahora).Select(u => u.Placa).ToArray());
        }
    }
}
