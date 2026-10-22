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

        // Naranjal, por la Troncal de la Costa entre Guayaquil y Machala: antes del grifo.
        private const double NaranjalLat = -2.7730, NaranjalLng = -79.6950;

        private static ViajeResumen Viaje(int id, string tipoRuta, params string[] columnas) =>
            new ViajeResumen { Id = id, Estado = "EN_CURSO", TipoRuta = tipoRuta, Tracto2 = "T" + id, Carreta = "C" + id, Planta = "JAVE", Horas = Horas(columnas) };

        private static LlegadaEstimada.Estimado AlGrifo(int horas) => new LlegadaEstimada.Estimado
        {
            Destino = LlegadaEstimada.DestinoGrifo, KmRestantes = 90, Llegada = Ahora.AddHours(horas), Desde = Ahora.AddHours(horas - 1),
            Hasta = Ahora.AddHours(horas + 1), HoraPosicion = Ahora.AddMinutes(-10), Muestras = 10,
        };

        [Fact]
        public void Unidades_LasQueVienenAlGrifo_YNoLasQueYaCargaron()
        {
            var enCaminoTarde = Viaje(1, null, "fhSalida"); enCaminoTarde.Llegada = AlGrifo(5);
            var enCaminoPronto = Viaje(2, "CRISAR", "fhSalida"); enCaminoPronto.Llegada = AlGrifo(2);
            var enGrifo = Viaje(3, null, "fhSalida", "fhLlegadaGrifo");
            var yaCargo = Viaje(4, null, "fhSalida", "fhLlegadaGrifo", "fhSalidaGrifo");
            var descargando = Viaje(5, null, "fhLlegadaPlantaEcuador", "fhIngreso");
            var enBase = Viaje(6, null, "fhSalida", "fhLlegadaBaseFinal");
            var novopan = Viaje(7, "NOVOPAN", "fhSalida"); novopan.Llegada = AlGrifo(1);
            var sinEstimado = Viaje(8, null, "fhSalida"); // ya pasó el grifo sin marcar la parada, o sin posición
            var cancelado = Viaje(9, null, "fhSalida"); cancelado.Estado = "CANCELADO"; cancelado.Llegada = AlGrifo(1);
            var crisarEnGrifo = Viaje(10, "CRISAR", "fhSalida"); crisarEnGrifo.Latitud = -3.42855; crisarEnGrifo.Longitud = -79.96242;
            var yendo = Viaje(11, null, "fhCruceEcuador", "fhSalidaTCI");
            var enPeru = Viaje(12, null, "fhSalidaBase2");
            var viejo = Viaje(13, null, "fhCruceEcuador"); viejo.Horas["fhCruceEcuador"] = Ahora.AddDays(-6); // el GPS le perdió el rastro

            var u = ExportacionGrifoSantaRosa.Unidades(new[] { enCaminoTarde, enCaminoPronto, enGrifo, yaCargo, descargando, enBase, novopan, sinEstimado, cancelado, crisarEnGrifo, yendo, enPeru, viejo }, Ahora);
            Assert.Equal(new[] { 3, 10, 2, 1, 5, 11 }, u.Select(x => x.Viaje.Id));
            Assert.Equal(ExportacionGrifoSantaRosa.Situacion.VanADescargar, u[5].Situacion);
            Assert.Equal(ExportacionGrifoSantaRosa.Situacion.EnElGrifo, u[0].Situacion);
            Assert.Equal(ExportacionGrifoSantaRosa.Situacion.EnCamino, u[2].Situacion);
            Assert.Equal(ExportacionGrifoSantaRosa.Situacion.Descargando, u[4].Situacion);
        }

        [Fact]
        public void Texto_ParaEnviarAlGrifo()
        {
            var a = Viaje(1, null, "fhSalida"); a.Llegada = AlGrifo(3); a.Chofer2 = "JUAN PEREZ"; a.Lugar = "NARANJAL"; a.Velocidad = 70;
            var b = Viaje(2, null, "fhLlegadaPlantaEcuador");
            string t = ExportacionGrifoSantaRosa.Texto(ExportacionGrifoSantaRosa.Unidades(new[] { a, b }, Ahora), Ahora);
            Assert.StartsWith("CARROS QUE BAJAN AL GRIFO SANTA ROSA - 22/10 12:00", t);
            Assert.Contains("Total: 2 (1 en camino al grifo, 0 en el grifo, 1 descargando, 0 yendo a descargar)", t);
            Assert.Contains("EN CAMINO AL GRIFO (1)", t);
            Assert.Contains("1. T1 / C1 - JUAN PEREZ (Vitapro): NARANJAL, faltan 90 km (en marcha a las 11:50). Llega aprox. 22/10 15:00", t);
            Assert.Contains("TODAVÍA DESCARGANDO, BAJAN DESPUÉS (1)", t);
            Assert.Contains("1. T2 / C2 (Vitapro): en JAVE desde las 22/10 10:00", t);
            Assert.DoesNotContain("EN EL GRIFO AHORA", t);
            Assert.Equal("", ExportacionGrifoSantaRosa.Texto(new List<ExportacionGrifoSantaRosa.Unidad>(), Ahora));
        }

        // ---------- Hora al grifo de los que todavía no salen de la planta ----------

        // Descargas de 2 a 6 h (como Jave: mediana ~3,5 h).
        private static readonly List<double> Duraciones = Enumerable.Range(0, 21).Select(i => 120.0 + i * 12).ToList();

        [Fact]
        public void DuracionRestante_CuentaLoQueYaLlevaDescargando()
        {
            var sinEmpezar = ExportacionGrifoSantaRosa.DuracionRestante(Duraciones, 0);
            Assert.Equal(240, sinEmpezar[1], 0);                                 // mediana de 2 a 6 h
            var conTresHoras = ExportacionGrifoSantaRosa.DuracionRestante(Duraciones, 180);
            Assert.InRange(conTresHoras[1], 60, 100);                           // le falta ~1,5 h, no 4
            Assert.True(conTresHoras[0] <= conTresHoras[1] && conTresHoras[1] <= conTresHoras[2]);
            Assert.Equal(new double[] { 15, 30, 60 }, ExportacionGrifoSantaRosa.DuracionRestante(Duraciones, 400)); // ya pasó lo normal
        }

        [Fact]
        public void DescargandoYYendoADescargar_TienenHoraAlGrifo()
        {
            var descargando = Viaje(1, null, "fhLlegadaPlantaEcuador");          // en Jave desde hace 2 h
            var yendo = Viaje(2, null, "fhCruceEcuador", "fhSalidaTCI");
            yendo.Llegada = new LlegadaEstimada.Estimado
            {
                Destino = "JAVE", KmRestantes = 90, Llegada = Ahora.AddHours(2), Desde = Ahora.AddHours(1.5), Hasta = Ahora.AddHours(3),
                HoraPosicion = Ahora.AddMinutes(-5), Muestras = 10,
            };
            var duraciones = new Dictionary<string, List<double>> { ["JAVE"] = Duraciones, ["*"] = Duraciones };
            var u = ExportacionGrifoSantaRosa.Unidades(new[] { yendo, descargando }, Ahora, duraciones);

            var d = u.Single(x => x.Viaje.Id == 1);
            var y = u.Single(x => x.Viaje.Id == 2);
            Assert.NotNull(d.AlGrifo);
            Assert.NotNull(y.AlGrifo);
            Assert.Equal(LlegadaEstimada.DestinoGrifo, d.AlGrifo.Destino);
            Assert.True(d.AlGrifo.Llegada > Ahora.AddHours(2));                  // termina de descargar y luego ~4 h de camino
            Assert.True(y.AlGrifo.Llegada > yendo.Llegada.Llegada.AddHours(4));   // llega a Jave, descarga ~4 h y luego el camino
            Assert.True(d.AlGrifo.Desde <= d.AlGrifo.Llegada && d.AlGrifo.Llegada <= d.AlGrifo.Hasta);
            Assert.Contains("al grifo aprox.", ExportacionGrifoSantaRosa.Texto(u, Ahora));
        }

        [Fact]
        public void Excel_PrimeraHojaSimplePorHoraYDetalleEnLaSegunda()
        {
            var t = new System.Data.DataTable();
            t.Columns.Add("idSeguimiento", typeof(int));
            ViajeResumen ConFila(ViajeResumen v) { var f = t.NewRow(); f["idSeguimiento"] = v.Id; t.Rows.Add(f); v.Fila = f; v.Chofer2 = "CHOFER " + v.Id; return v; }
            var tarde = ConFila(Viaje(1, null, "fhSalida")); tarde.Llegada = AlGrifo(5);
            var pronto = ConFila(Viaje(2, null, "fhSalida")); pronto.Llegada = AlGrifo(1);
            var enGrifo = ConFila(Viaje(3, null, "fhSalida", "fhLlegadaGrifo"));
            var u = ExportacionGrifoSantaRosa.Unidades(new[] { tarde, pronto, enGrifo }, Ahora);

            var r = ExportacionGrifoSantaRosa.Construir(u, "prueba", Ahora);
            Assert.Equal(new[] { "CONDUCTOR", "TRACTO", "LLEGADA AL GRIFO APROX." }, r.Columnas.Select(c => c.Titulo));
            Assert.Equal(new[] { 3, 2, 1 }, r.Filas.Select(f => (int)f["idSeguimiento"]));   // en el grifo, luego por hora
            Assert.StartsWith("En el grifo desde", (string)r.Columnas[2].Valor(r.Filas[0]));
            Assert.Single(r.HojasAdicionales);

            var empresa = new WebSGV.Helpers.EmpresaInfo { RazonSocial = "SERVICIOS GENERALES VIVIANA E.I.R.L.", Ruc = "20483851171" };
            var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(WebSGV.Services.Reportes.ReporteTabularExportador.GenerarExcel(r, empresa)));
            Assert.Equal(new[] { "Llegada al grifo", "Detalle" }, wb.Worksheets.Select(w => w.Name));
        }

        // ---------- Llegada al grifo y a la base ----------

        [Fact]
        public void Retorno_AntesDelGrifoSeEstimaLaLlegadaAlGrifo()
        {
            var e = LlegadaEstimada.ParaViaje(Horas("fhSalidaBase2", "fhSalida"), "EN_CURSO", "JAVE",
                "T7J-861", "TBM-898", "T7J-861", NaranjalLat, NaranjalLng, Ahora);
            Assert.NotNull(e);
            Assert.Equal(LlegadaEstimada.DestinoGrifo, e.Destino);
            Assert.InRange(e.KmRestantes, 40, 150);
            Assert.InRange((e.Llegada - Ahora).TotalHours, 0.5, 12);
        }

        [Fact]
        public void Retorno_EnElGrifoODespues_SeEstimaLaLlegadaALaBase()
        {
            Assert.Equal(LlegadaEstimada.DestinoBase, LlegadaEstimada.ParaRetorno(false, -3.42855, -79.96242, Ahora)?.Destino);
            // Pasando por Arenillas de vuelta, con la parada en el grifo ya marcada.
            var e = LlegadaEstimada.ParaViaje(Horas("fhSalidaBase2", "fhSalida", "fhLlegadaGrifo", "fhSalidaGrifo"), "EN_CURSO", "JAVE",
                "T7J-861", "TBM-898", "T7J-861", -3.5500, -80.0600, Ahora);
            Assert.NotNull(e);
            Assert.Equal(LlegadaEstimada.DestinoBase, e.Destino);
            Assert.InRange((e.Llegada - Ahora).TotalHours, 3, 20);
            // El retorno no va en el reporte de llegadas a planta.
            var viajes = new List<ViajeResumen> { new ViajeResumen { Tracto2 = "T7J-861", Carreta = "T0L-981", Llegada = e } };
            Assert.Equal("", LlegadaEstimada.Reporte(viajes, Ahora));
        }

        [Fact]
        public void Crisar_DeVueltaDeLaTroncal_SeEstimaLaLlegadaAlGrifo()
        {
            var e = LlegadaEstimada.ParaRetornoCrisar(Horas("fhLlegadaPlantaEcuador", "fhSalida"), "EN_CURSO", "BNH712", "CBV-761", "BNH-712",
                NaranjalLat, NaranjalLng, Ahora);
            Assert.Equal(LlegadaEstimada.DestinoGrifo, e?.Destino);
            Assert.Null(LlegadaEstimada.ParaRetornoCrisar(Horas("fhLlegadaPlantaEcuador"), "EN_CURSO", "BNH712", "CBV-761", "BNH-712",
                NaranjalLat, NaranjalLng, Ahora));
        }
    }
}
