using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Exportacion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Parada en el grifo Santa Rosa (El Retiro, Machala) de vuelta de Ecuador y su cuadro.</summary>
    public class GrifoSantaRosaTests
    {
        private static readonly string Carpeta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datos", "Trazabilidad");
        private static readonly DateTime Ahora = new DateTime(2026, 10, 22, 12, 0, 0);

        /// <summary>
        /// En los casos reales de descarga que vuelven a la base, el GPS marca el grifo después de salir de la
        /// planta y antes de llegar a la base (111 de 112 viajes del 15/09 al 20/10/2026 paran ahí).
        /// </summary>
        [Fact]
        public void CasosReales_MarcanElGrifoEntreLaPlantaYLaBase()
        {
            int conRegreso = 0;
            foreach (var caso in JArray.Parse(File.ReadAllText(Path.Combine(Carpeta, "casos.json"))).Where(c => (string)c["tramo"] == "descarga"))
            {
                var puntos = JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(File.ReadAllText(Path.Combine(Carpeta, (string)caso["historial"])));
                DateTime desde = FechaHelper.ConvertirAUtc(DateTime.Parse((string)caso["desde"]));
                var r = TrazabilidadDescargaService.DetectarTramoDescarga(puntos, desde);
                if (r.SalidaPlanta == null || r.LlegadaBase == null) continue;
                conRegreso++;
                Assert.True(r.LlegadaGrifo != null, $"{caso["caso"]}: no se marcó el grifo");
                Assert.True(r.LlegadaGrifo.HoraUtc > r.SalidaPlanta.HoraUtc && r.LlegadaGrifo.HoraUtc < r.LlegadaBase.HoraUtc, (string)caso["caso"]);
                Assert.NotNull(r.SalidaGrifo);
                Assert.True(r.SalidaGrifo.HoraUtc > r.LlegadaGrifo.HoraUtc);
            }
            Assert.True(conRegreso >= 2, $"Solo {conRegreso} casos con regreso a base");
        }

        [Fact]
        public void PasarDeLargoPorLaCarretera_NoEsParadaEnElGrifo()
        {
            var g = GeocercasTrazabilidad.GrifoSantaRosa;
            Assert.True(g.Contiene(-3.42855, -79.96242));  // estación (OpenStreetMap)
            Assert.True(g.Contiene(-3.4285, -79.9630));    // patio donde se estacionan
            Assert.False(g.Contiene(-3.4250, -79.9600));   // ~500 m al norte por la carretera
        }

        // ---------- STATUS ----------

        private static Dictionary<string, DateTime?> Horas(params string[] columnas) =>
            columnas.ToDictionary(c => c, c => (DateTime?)Ahora.AddHours(-2), StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void Status_EnElGrifoYDespues()
        {
            Assert.Equal("UNIDAD DESCARGADA EN GRIFO SANTA ROSA",
                StatusUnidad.Texto(Horas("fhIngreso", "fhSalida", "fhLlegadaGrifo"), "EN_CURSO", "JAVE", "MACHALA", Ahora, Ahora));
            Assert.Equal("UNIDAD DESCARGADA CAMINO A BASE - ARENILLAS",
                StatusUnidad.Texto(Horas("fhIngreso", "fhSalida", "fhLlegadaGrifo", "fhSalidaGrifo"), "EN_CURSO", "JAVE", "ARENILLAS", Ahora, Ahora));
            Assert.Equal("UNIDAD DESCARGADA CAMINO A BASE - NARANJAL",
                StatusUnidad.Texto(Horas("fhIngreso", "fhSalida"), "EN_CURSO", "JAVE", "NARANJAL", Ahora, Ahora));
        }

        // ---------- Cuadro ----------

        private static ViajeResumen Viaje(int id, string estado, params string[] columnas) =>
            new ViajeResumen { Id = id, Estado = estado, Horas = Horas(columnas) };

        [Fact]
        public void Retornando_SoloLasQueSalieronDeLaPlantaYNoLleganABase()
        {
            var viajes = new List<ViajeResumen>
            {
                Viaje(1, "EN_CURSO", "fhSalida"),
                Viaje(2, "EN_CURSO", "fhSalida", "fhLlegadaBaseFinal"),
                Viaje(3, "EN_CURSO", "fhIngreso"),
                Viaje(4, "CANCELADO", "fhSalida"),
                new ViajeResumen { Id = 5, Estado = "EN_CURSO", TipoRuta = "CRISAR", Horas = Horas("fhSalida") },
            };
            Assert.Equal(new[] { 1 }, ExportacionGrifoSantaRosa.Retornando(viajes).Select(v => v.Id));
        }

        [Fact]
        public void SituacionYTiempoEnElGrifo()
        {
            var v = new ViajeResumen
            {
                Horas = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fhSalida"] = Ahora.AddHours(-6), ["fhLlegadaGrifo"] = Ahora.AddHours(-2).AddMinutes(-15),
                },
            };
            Assert.Equal("En el grifo", ExportacionGrifoSantaRosa.Situacion(v));
            Assert.Equal("2 h 15 min (sigue)", ExportacionGrifoSantaRosa.TiempoEnGrifo(v, Ahora));
            v.Horas["fhSalidaGrifo"] = Ahora.AddMinutes(-80);
            Assert.Equal("Pasó por el grifo, camino a base", ExportacionGrifoSantaRosa.Situacion(v));
            Assert.Equal("55 min", ExportacionGrifoSantaRosa.TiempoEnGrifo(v, Ahora));
            Assert.Equal("Todavía no llega al grifo", ExportacionGrifoSantaRosa.Situacion(new ViajeResumen { Horas = Horas("fhSalida") }));
        }

        // ---------- Llegada a la base ----------

        [Fact]
        public void Retorno_SeEstimaLaLlegadaALaBaseYNoVaEnElReporteDePlanta()
        {
            // Pasando por Arenillas de vuelta, con el tracto de descarga.
            var e = LlegadaEstimada.ParaViaje(Horas("fhSalidaBase2", "fhSalida", "fhLlegadaGrifo", "fhSalidaGrifo"), "EN_CURSO", "JAVE",
                "T7J-861", "TBM-898", "T7J-861", -3.5500, -80.0600, Ahora);
            Assert.NotNull(e);
            Assert.Equal(LlegadaEstimada.DestinoBase, e.Destino);
            Assert.InRange((e.Llegada - Ahora).TotalHours, 3, 20);
            var viajes = new List<ViajeResumen> { new ViajeResumen { Tracto2 = "T7J-861", Carreta = "T0L-981", Llegada = e } };
            Assert.Equal("", LlegadaEstimada.Reporte(viajes, Ahora));
        }
    }
}
