using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Exportacion;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Recorrido de los viajes de terceros contra el GPS real (Datos/Terceros) y el STATUS que tienen en
    /// el Excel STATUS EXTRA VIVIANA:
    ///   BNG-758 carga 26/08/2026 Polinplast Chapaira → Casa Grande, "UNIDAD DESCARGADA EN COCHERA TRUJILLO"
    ///   BNG-758 carga 14/10 Crisar Cartavio → (relevo CBX-832) La Troncal, "UNIDAD DESCARGADA EN SU TOTALIDAD"
    ///   BFK-879 carga 11/10 Novopan Quito → Trujillo, "UNIDAD DESCARGADA EN SU TOTALIDAD"
    /// </summary>
    public class TrazabilidadTercerosTests
    {
        private static readonly string Carpeta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datos", "Terceros");

        private static List<OnwayHistoryPoint> Gps(string archivo) =>
            JsonConvert.DeserializeObject<List<OnwayHistoryPoint>>(File.ReadAllText(Path.Combine(Carpeta, archivo)));

        private static DateTime Utc(string horaPeru) =>
            DateTime.SpecifyKind(DateTime.ParseExact(horaPeru, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture).AddHours(5), DateTimeKind.Utc);

        private static void Hora(ResultadoTramoTercero r, string columna, string esperadaPeru)
        {
            var h = r.Buscar(columna);
            Assert.True(h != null, columna + " no se detectó");
            var diferencia = (h.HoraUtc - Utc(esperadaPeru)).Duration();
            Assert.True(diferencia <= TimeSpan.FromMinutes(2), $"{columna}: {h.HoraUtc.AddHours(-5):dd/MM HH:mm} en vez de {esperadaPeru}");
        }

        private static string StatusFinal(string ruta, ResultadoTramoTercero r, string lugarCarga, string lugarEntrega)
        {
            var horas = r.Hitos.ToDictionary(h => h.Item1, h => (DateTime?)h.Item2.HoraUtc.AddHours(-5));
            return RutaTercero.Status(ruta, horas, "EN_CURSO", lugarCarga, lugarEntrega ?? r.LugarEntrega, r.LugarFin, null, null, DateTime.Now);
        }

        [Fact]
        public void Polinplast_DescargaEnCasaGrandeYTerminaEnLaCochera()
        {
            var r = TrazabilidadTercerosService.Detectar("POLINPLAST", "CHAPAIRA", "CASAGRANDE",
                Gps("BNG-758_2026-08-26_polinplast.json"), null, Utc("2026-08-25 12:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-08-26 07:05");
            Hora(r, "fhSalidaPlanta", "2026-08-26 13:13");
            Hora(r, "fhLlegadaPlantaEcuador", "2026-08-27 06:36");
            Hora(r, "fhSalida", "2026-08-27 10:09");
            // Antes de la cochera espera ~2 h en la cola de Vitapro (13:07–15:05): eso no es el fin.
            Hora(r, "fhLlegadaBaseFinal", "2026-08-27 15:10");
            Assert.Equal(RutaTercero.FinCochera, r.LugarFin);
            Assert.Equal("UNIDAD DESCARGADA EN COCHERA TRUJILLO", StatusFinal("POLINPLAST", r, "CHAPAIRA", "CASAGRANDE"));
        }

        [Fact]
        public void Polinplast_SinLugarDeEntrega_LoDetectaElGps()
        {
            var r = TrazabilidadTercerosService.Detectar("POLINPLAST", "CHAPAIRA", null,
                Gps("BNG-758_2026-08-26_polinplast.json"), null, Utc("2026-08-25 12:00"));
            Assert.Equal("CASAGRANDE", r.LugarEntrega);
            Hora(r, "fhLlegadaPlantaEcuador", "2026-08-27 06:36");
        }

        [Fact]
        public void Crisar_ConRelevo_PasaPorBaseComplexYTciHastaLaTroncal()
        {
            var r = TrazabilidadTercerosService.Detectar("CRISAR", "CARTAVIO", "GUAYAQUIL",
                Gps("BNG-758_2026-10-14_crisar.json"), Gps("CBX-832_2026-10-14_crisar.json"), Utc("2026-10-13 12:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-10-13 19:48");
            Hora(r, "fhSalidaPlanta", "2026-10-14 14:06");
            Hora(r, "fhLlegadaBase2", "2026-10-15 01:11");
            Hora(r, "fhSalidaBase2", "2026-10-15 10:23");          // CBX-832 sale con la carreta
            Hora(r, "fhLlegadaBodegaNacional", "2026-10-15 18:30");
            Hora(r, "fhLlegadaTCI", "2026-10-15 20:21");
            Hora(r, "fhSalidaTCI", "2026-10-16 18:47");            // durmió en TCI
            Hora(r, "fhLlegadaPlantaEcuador", "2026-10-19 08:46");
            Hora(r, "fhSalida", "2026-10-19 13:19");
            Hora(r, "fhLlegadaBaseFinal", "2026-10-20 02:52");
            Assert.Equal("BASE", r.LugarFin);
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", StatusFinal("CRISAR", r, "CARTAVIO", "GUAYAQUIL"));
        }

        [Fact]
        public void Crisar_RelevoQueNoSaleDeLaBase_NoTomaLaFronteraDeOtroViaje()
        {
            // El tracto de descarga anda en otro viaje (aquí: el mismo recorrido sin pasar por la base). Sin salir de
            // la base con la carreta no hay descarga: antes se tomaban su TCI y La Troncal (BSD-716, 20/10/2026).
            var otroViaje = Gps("CBX-832_2026-10-14_crisar.json")
                .Where(p => !GeocercasTrazabilidad.BaseSullana.Contiene(p.Lat, p.Lng)).ToList();
            var r = TrazabilidadTercerosService.Detectar("CRISAR", "CARTAVIO", "GUAYAQUIL",
                Gps("BNG-758_2026-10-14_crisar.json"), otroViaje, Utc("2026-10-13 12:00"));

            Hora(r, "fhLlegadaBase2", "2026-10-15 01:11");
            Assert.Null(r.Buscar("fhSalidaBase2"));
            Assert.Null(r.Buscar("fhLlegadaTCI"));
            Assert.Null(r.Buscar("fhLlegadaPlantaEcuador"));
            Assert.Equal("UNIDAD CARGADA EN BASE SULLANA", StatusFinal("CRISAR", r, "CARTAVIO", "GUAYAQUIL"));
        }

        [Fact]
        public void Novopan_CargaEnQuitoEntraPorElCebafYDescargaEnTrujillo()
        {
            var r = TrazabilidadTercerosService.Detectar("NOVOPAN", "QUITO", null,
                Gps("BFK-879_2026-10-11_novopan.json"), null, Utc("2026-10-10 12:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-10-11 08:47");
            Hora(r, "fhSalidaPlanta", "2026-10-11 22:14");
            Hora(r, "fhLlegadaCEBAF", "2026-10-13 07:34");
            Hora(r, "fhLlegadaPlantaEcuador", "2026-10-15 10:28");
            Hora(r, "fhSalida", "2026-10-15 14:45");
            Assert.Equal("TRUJILLO", r.LugarEntrega);
            // Después de descargar espera en el grifo de Curva de Sun: en el Excel es "EN SU TOTALIDAD".
            Assert.Equal(RutaTercero.FinEsperaTrujillo, r.LugarFin);
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", StatusFinal("NOVOPAN", r, "QUITO", null));
        }

        [Fact]
        public void Novopan_YaEstabaEnQuitoAntesDeLaFecha_IgualTomaLaCarga()
        {
            // El Excel de Novopan pone el día siguiente al de la carga (T7J-861: cargó el 04/10, el Excel dice 05/10).
            var r = TrazabilidadTercerosService.Detectar("NOVOPAN", "QUITO", "TRUJILLO",
                Gps("BFK-879_2026-10-11_novopan.json"), null, Utc("2026-10-11 12:00"));
            Hora(r, "fhLlegadaTrujillo", "2026-10-11 08:47");
            Hora(r, "fhLlegadaPlantaEcuador", "2026-10-15 10:28");
        }

        [Fact]
        public void Crisar_SinTractoDeDescargaAsignado_SeQuedaEnLaBase()
        {
            // Sin la sección de descarga del Word, el tracto de carga que sale de la base puede llevar otra carreta.
            var r = TrazabilidadTercerosService.Detectar("CRISAR", "CARTAVIO", "GUAYAQUIL",
                Gps("BNG-758_2026-10-14_crisar.json"), null, Utc("2026-10-13 12:00"), descargaAsignada: false);
            Hora(r, "fhLlegadaBase2", "2026-10-15 01:11");
            Assert.Null(r.Buscar("fhSalidaBase2"));
            Assert.Null(r.Buscar("fhLlegadaPlantaEcuador"));
        }

        [Fact]
        public void SinVisitaALaPlantaDeCarga_NoHayViaje()
        {
            // Desde después de la descarga: la unidad ya no vuelve a Chapaira en el historial.
            var r = TrazabilidadTercerosService.Detectar("POLINPLAST", "CHAPAIRA", "CASAGRANDE",
                Gps("BNG-758_2026-08-26_polinplast.json"), null, Utc("2026-08-27 12:00"));
            Assert.Empty(r.Hitos);
        }
    }
}
