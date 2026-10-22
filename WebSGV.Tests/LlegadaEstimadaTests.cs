using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Hora aproximada de llegada a la planta desde la última posición del GPS.</summary>
    public class LlegadaEstimadaTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 22, 10, 0, 0);

        /// <summary>
        /// Camino recto de 100 km hacia el sur (1 km ≈ 0,009°). De día los viajes tardan 1 min por km;
        /// de noche (22:00–05:00) duermen: desde cualquier punto tardan 8 h más.
        /// </summary>
        private static LlegadaEstimada.Ruta Recta()
        {
            var r = new LlegadaEstimada.Ruta { Codigo = "PRUEBA", Destino = "TRUJILLO", LargoKm = 100, Viajes = 40 };
            for (int km = 0; km <= 100; km++) r.Polilinea.Add(new[] { -5.0 - km * 0.008993, -80.0 });
            for (int viaje = 0; viaje < 40; viaje++)
                for (int km = 0; km < 100; km += 5)
                {
                    double hora = (viaje * 0.6) % 24;
                    bool noche = hora >= 22 || hora < 5;
                    r.Muestras.Add(new double[] { km, hora, (100 - km) + (noche ? 480 : 0) + viaje % 5 });
                }
            return r;
        }

        private static double Lat(double km) => -5.0 - km * 0.008993;

        [Fact]
        public void DeDia_LlegaPorLoQueTardaronLosViajesDeDia()
        {
            var e = LlegadaEstimada.Estimar(Recta(), Lat(40), -80.0, Ahora);
            Assert.NotNull(e);
            Assert.Equal(60, e.KmRestantes, 0);
            Assert.InRange((e.Llegada - Ahora).TotalMinutes, 50, 75);
            Assert.True(e.Desde <= e.Llegada && e.Llegada <= e.Hasta);
            Assert.Equal("TRUJILLO", e.Destino);
        }

        [Fact]
        public void DeNoche_CuentaLaParadaParaDormir()
        {
            var e = LlegadaEstimada.Estimar(Recta(), Lat(40), -80.0, Ahora.Date.AddHours(23));
            Assert.InRange((e.Llegada - Ahora.Date.AddHours(23)).TotalHours, 8, 10);
        }

        [Fact]
        public void FueraDelCamino_NoSeEstima()
        {
            Assert.Null(LlegadaEstimada.Estimar(Recta(), Lat(40), -79.8, Ahora)); // ~22 km al este
            Assert.Null(LlegadaEstimada.Estimar((LlegadaEstimada.Ruta)null, Lat(40), -80.0, Ahora));
        }

        [Fact]
        public void LaHoraSeRedondeaADiezMinutos()
        {
            var e = LlegadaEstimada.Estimar(Recta(), Lat(40), -80.0, Ahora.AddMinutes(3));
            Assert.Equal(0, e.Llegada.Minute % 10);
            Assert.Equal(0, e.Llegada.Second);
        }

        [Theory]
        [InlineData(23, 1, 2)]
        [InlineData(1, 23, 2)]
        [InlineData(10, 14, 4)]
        [InlineData(0, 12, 12)]
        public void DiferenciaHoras_DaLaVueltaAMedianoche(double a, double b, double esperado) =>
            Assert.Equal(esperado, LlegadaEstimada.DiferenciaHoras(a, b), 3);

        [Fact]
        public void Percentil_Interpola()
        {
            var l = new List<double> { 10, 20, 30, 40 };
            Assert.Equal(25, LlegadaEstimada.Percentil(l, 0.5), 3);
            Assert.Equal(10, LlegadaEstimada.Percentil(l, 0), 3);
            Assert.Equal(40, LlegadaEstimada.Percentil(l, 1), 3);
        }

        // ---------- Qué viajes van en camino ----------

        private static Dictionary<string, DateTime?> Horas(params string[] columnas) =>
            columnas.ToDictionary(c => c, c => (DateTime?)Ahora.AddHours(-5), StringComparer.OrdinalIgnoreCase);

        // Chiclayo, por la Panamericana: en el camino de la carga a Trujillo del perfil real.
        private const double ChiclayoLat = -6.7700, ChiclayoLng = -79.8600;

        [Fact]
        public void Carga_SalioDeBaseYNoLlego_SeEstimaATrujillo()
        {
            var e = LlegadaEstimada.ParaViaje(Horas("fhSalidaBase1"), "EN_CURSO", null, "ANU794", "ANU-794", null, ChiclayoLat, ChiclayoLng, Ahora);
            Assert.NotNull(e);
            Assert.Equal("TRUJILLO", e.Destino);
        }

        [Fact]
        public void Carga_NoSeEstimaSiYaLlegoOSiLaPosicionEsDeOtroTracto()
        {
            Assert.Null(LlegadaEstimada.ParaViaje(Horas("fhSalidaBase1", "fhLlegadaTrujillo"), "EN_CURSO", null, "ANU-794", "ANU-794", null, ChiclayoLat, ChiclayoLng, Ahora));
            Assert.Null(LlegadaEstimada.ParaViaje(Horas("fhSalidaBase1"), "EN_CURSO", null, "BNH-712", "ANU-794", "BNH-712", ChiclayoLat, ChiclayoLng, Ahora));
            Assert.Null(LlegadaEstimada.ParaViaje(Horas(), "EN_CURSO", null, "ANU-794", "ANU-794", null, ChiclayoLat, ChiclayoLng, Ahora)); // en base
            Assert.Null(LlegadaEstimada.ParaViaje(Horas("fhSalidaBase1"), "FINALIZADO", null, "ANU-794", "ANU-794", null, ChiclayoLat, ChiclayoLng, Ahora));
            Assert.Null(LlegadaEstimada.ParaViaje(Horas("fhSalidaBase1"), "EN_CURSO", null, "ANU-794", "ANU-794", null, null, null, null));
        }

        [Fact]
        public void Descarga_SoloDespuesDeSalirDeTci()
        {
            string[] enTci = { "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta", "fhSalidaBase2", "fhCruceEcuador", "fhLlegadaTCI" };
            // Naranjal, en el camino de Machala a Guayaquil.
            Assert.Null(LlegadaEstimada.ParaViaje(Horas(enTci), "EN_CURSO", "JAVE", "T7J-861", "TBM-898", "T7J-861", -2.7730, -79.6950, Ahora));
            var e = LlegadaEstimada.ParaViaje(Horas(enTci.Concat(new[] { "fhSalidaTCI" }).ToArray()), "EN_CURSO", "JAVE", "T7J-861", "TBM-898", "T7J-861", -2.7730, -79.6950, Ahora);
            Assert.NotNull(e);
            Assert.Equal("JAVE", e.Destino);
            Assert.Null(LlegadaEstimada.ParaViaje(Horas(enTci.Concat(new[] { "fhSalidaTCI", "fhLlegadaPlantaEcuador" }).ToArray()), "EN_CURSO", "JAVE", "T7J-861", "TBM-898", "T7J-861", -2.7730, -79.6950, Ahora));
        }

        [Fact]
        public void PerfilReal_DeChiclayoATrujilloEsDeHorasNoDeDias()
        {
            var perfil = LlegadaEstimada.Perfil(LlegadaEstimada.CargaTrujillo);
            Assert.NotNull(perfil);
            Assert.True(perfil.Viajes >= 50, $"El perfil tiene {perfil.Viajes} viajes");
            var e = LlegadaEstimada.Estimar(perfil, ChiclayoLat, ChiclayoLng, Ahora);
            Assert.NotNull(e);
            Assert.InRange(e.KmRestantes, 150, 260);
            Assert.InRange((e.Llegada - Ahora).TotalHours, 2.5, 12);
        }

        [Fact]
        public void PerfilesReales_DeDescargaAJaveEInbalnor()
        {
            Assert.NotNull(LlegadaEstimada.Perfil("DESCARGA_JAVE"));
            Assert.NotNull(LlegadaEstimada.Perfil("DESCARGA_INBALNOR"));
        }

        // ---------- Reporte para enviar ----------

        [Fact]
        public void Reporte_AgrupaPorPlantaYOrdenaPorLlegada()
        {
            LlegadaEstimada.Estimado E(string destino, int horas, int km, double minutosPosicion = -10) => new LlegadaEstimada.Estimado
            {
                Destino = destino, KmRestantes = km, Llegada = Ahora.AddHours(horas), Desde = Ahora.AddHours(horas - 1), Hasta = Ahora.AddHours(horas + 1),
                Muestras = 10, HoraPosicion = Ahora.AddMinutes(minutosPosicion),
            };
            var viajes = new List<ViajeResumen>
            {
                new ViajeResumen { Tracto1 = "ANU-794", Carreta = "TJI-983", Chofer1 = "JUSTO CESPEDES", Lugar = "CHICLAYO", Velocidad = 0, Llegada = E("TRUJILLO", 5, 210) },
                new ViajeResumen { Tracto1 = "BPS-816", Carreta = "AVA-979", Chofer1 = "ALFREDO ANDRADE", Lugar = "PAIJAN", Velocidad = 70, Llegada = E("TRUJILLO", 1, 60) },
                new ViajeResumen { Tracto1 = "TBM-898", Tracto2 = "T7J-861", Carreta = "T0L-981", Chofer2 = "DANILO PIEDRA", Lugar = "NARANJAL", Llegada = E("JAVE", 3, 90, -180) },
                new ViajeResumen { Tracto1 = "X", Carreta = "Y", Llegada = null },
            };
            string r = LlegadaEstimada.Reporte(viajes, Ahora);
            var lineas = r.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int trujillo = Array.IndexOf(lineas, "CARGA EN TRUJILLO"), jave = Array.IndexOf(lineas, "DESCARGA EN JAVE");
            Assert.True(trujillo > 0 && jave > trujillo);
            Assert.StartsWith("- BPS-816 / AVA-979 - ALFREDO ANDRADE: PAIJAN, faltan 60 km (en marcha a las 09:50). Llega aprox. 22/10 11:00", lineas[trujillo + 1]);
            Assert.Contains("ANU-794", lineas[trujillo + 2]);
            Assert.Contains("detenida a las 09:50", lineas[trujillo + 2]);
            Assert.Contains("T7J-861 / T0L-981 - DANILO PIEDRA", lineas[jave + 1]);
            Assert.Contains("última señal 22/10 07:00", lineas[jave + 1]);
            Assert.Equal("", LlegadaEstimada.Reporte(viajes.Where(v => v.Llegada == null), Ahora));
        }
    }
}
