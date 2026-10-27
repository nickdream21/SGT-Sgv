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
        public void Crisar_DeVueltaDeLaTroncal_MarcaElGrifoSantaRosa()
        {
            var r = TrazabilidadTercerosService.Detectar("CRISAR", "CARTAVIO", "GUAYAQUIL",
                Gps("BNG-758_2026-10-14_crisar.json"), Gps("CBX-832_2026-10-14_crisar.json"), Utc("2026-10-13 12:00"));
            // Sale de La Troncal 13:19, para en el grifo 17:33–18:46 y llega a la base 02:52.
            Assert.NotNull(r.LlegadaGrifo);
            Assert.InRange((r.LlegadaGrifo.HoraUtc - Utc("2026-10-19 17:33")).TotalMinutes, -3, 3);
            Assert.InRange((r.SalidaGrifo.HoraUtc - Utc("2026-10-19 18:46")).TotalMinutes, -3, 3);
        }

        [Fact]
        public void Crisar_SinEntrarAlPatioDeTci_TomaElControlDeLaAduana()
        {
            // BSD-716, 22/10/2026: 2 min en el patio de TCI y 7 en el control de al lado. Sin el patio, el control.
            var sinPatio = Gps("CBX-832_2026-10-14_crisar.json").Where(p => !GeocercasTrazabilidad.Tci.Contiene(p.Lat, p.Lng)).ToList();
            var r = TrazabilidadTercerosService.Detectar("CRISAR", "CARTAVIO", "GUAYAQUIL",
                Gps("BNG-758_2026-10-14_crisar.json"), sinPatio, Utc("2026-10-13 12:00"));
            var tci = r.Buscar("fhLlegadaTCI");
            Assert.NotNull(tci);
            Assert.Contains("paso directo", tci.Fuente);
            Hora(r, "fhLlegadaPlantaEcuador", "2026-10-19 08:46");   // y sigue hasta La Troncal
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
        public void Afrecho_PrimerViaje_LlegaAAlicorpPaitaYSigueCargando()
        {
            // AVM-877 el 24/10/2026: sale de la base 04:34, llega por la Av. Industrial 06:51, espera en la puerta
            // hasta las 07:59 y desde las 08:06 carga; sigue adentro al final del historial (y un salto suelto del
            // GPS a 24 km a las 06:50:59 no corta la llegada).
            var r = TrazabilidadTercerosService.Detectar("AFRECHO", "PAITA", null,
                Gps("AVM-877_2026-10-24_afrecho.json"), null, Utc("2026-10-24 00:00"));
            Hora(r, "fhLlegadaTrujillo", "2026-10-24 06:52");
            Assert.Null(r.Buscar("fhSalidaPlanta"));
            Assert.Equal("UNIDAD EN PROCESO DE CARGA - PAITA", StatusFinal("AFRECHO", r, "PAITA", null));
        }

        [Fact]
        public void Afrecho_EsperaEnLaBaseYEnTci_SinDescargaFalsaAlReconectarElGps()
        {
            // AVM-877, 24–27/10/2026: carga en Paita, vuelve a la base y espera 36 h, baja por la costa y desde las 14:41
            // del 26/10 está en TCI. Cortan la batería a las 16:18; al reconectarla (27/10 07:42) el equipo manda dos
            // posiciones viejas, en Aguas Verdes y en el cruce, y a las 07:50 vuelve a estar en TCI. El sitio tomaba
            // esas posiciones como la salida de TCI y la descarga "en AGUAS VERDES" (que además es Perú).
            var r = TrazabilidadTercerosService.Detectar("AFRECHO", "PAITA", "HUAQUILLAS",
                Gps("AVM-877_2026-10-24_afrecho_tci.json"), null, Utc("2026-10-24 00:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-10-24 06:52");
            Hora(r, "fhSalidaPlanta", "2026-10-24 14:45");
            Hora(r, "fhLlegadaBase2", "2026-10-24 16:32");
            Hora(r, "fhSalidaBase2", "2026-10-26 04:48");
            Hora(r, "fhLlegadaTCI", "2026-10-26 14:41");
            Assert.Null(r.Buscar("fhSalidaTCI"));
            Assert.Null(r.Buscar("fhLlegadaPlantaEcuador"));
            Assert.Equal("UNIDAD CARGADA EN TCI - ECUADOR", StatusFinal("AFRECHO", r, "PAITA", "HUAQUILLAS"));
        }

        [Fact]
        public void Afrecho_VuelveALaBase_EstaEnBaseCargado()
        {
            // El mismo viaje hasta el 25/10: la unidad está en la base con la carga ("CAMINO A FRONTERA" antes).
            var gps = Gps("AVM-877_2026-10-24_afrecho_tci.json").Where(p => (p.GenerateTime ?? p.MessageTime) < Utc("2026-10-25 12:00")).ToList();
            var r = TrazabilidadTercerosService.Detectar("AFRECHO", "PAITA", null, gps, null, Utc("2026-10-24 00:00"));

            Hora(r, "fhLlegadaBase2", "2026-10-24 16:32");
            Assert.Null(r.Buscar("fhSalidaBase2"));
            var horas = r.Hitos.ToDictionary(h => h.Item1, h => (DateTime?)h.Item2.HoraUtc.AddHours(-5));
            Assert.Equal("En base, cargado", RutaTercero.Fase("AFRECHO", horas, "EN_CURSO", "PAITA", null).Nombre);
            Assert.Equal("UNIDAD CARGADA EN BASE SULLANA", StatusFinal("AFRECHO", r, "PAITA", null));
        }

        [Fact]
        public void Afrecho_Enero_DescargaEnHuaquillas()
        {
            // BPT-919, carga del 10/01/2026 (Excel STATUS EXTRA: AFRECHO, PAITA → "TCI"): 35 h en la base, Zarumilla,
            // 3,8 h en Huaquillas (la descarga, sin pasar por TCI) y después descanso en Bellavista y vuelta a la base.
            var r = TrazabilidadTercerosService.Detectar("AFRECHO", "PAITA", null,
                Gps("BPT-919_2026-01-10_afrecho.json"), null, Utc("2026-01-10 00:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-01-10 10:02");
            Hora(r, "fhSalidaPlanta", "2026-01-10 17:06");
            Hora(r, "fhLlegadaBase2", "2026-01-10 18:39");
            Hora(r, "fhSalidaBase2", "2026-01-12 05:56");
            Hora(r, "fhLlegadaPlantaEcuador", "2026-01-12 14:52");
            Hora(r, "fhSalida", "2026-01-12 18:38");
            Hora(r, "fhLlegadaBaseFinal", "2026-01-13 12:47");
            Assert.Equal("HUAQUILLAS", r.LugarEntrega);
            Assert.Equal("BASE", r.LugarFin);
        }

        [Fact]
        public void Polinplast_JayancaPueblo()
        {
            // BPT-788, 23–24/10/2026: Chapaira → Jayanca, pero en el pueblo (a 15 km del punto de Rapel).
            var r = TrazabilidadTercerosService.Detectar("POLINPLAST", "CHAPAIRA", "JAYANCA",
                Gps("BPT-788_2026-10-23_polinplast_jayanca.json"), null, Utc("2026-10-23 00:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-10-23 06:49");
            Hora(r, "fhSalidaPlanta", "2026-10-23 10:43");
            Hora(r, "fhLlegadaPlantaEcuador", "2026-10-24 08:23");
            Hora(r, "fhSalida", "2026-10-24 13:01");
        }

        private static OnwayHistoryPoint Punto(string horaPeru, double lat, double lng, double kmh, string direccion = null) =>
            new OnwayHistoryPoint { MessageTime = Utc(horaPeru), Lat = lat, Lng = lng, Speed = kmh, Address = direccion };

        private static IEnumerable<OnwayHistoryPoint> Parado(GeocercaGps g, params string[] horas) =>
            horas.Select(h => Punto(h, g.LatitudCentro, g.LongitudCentro, 0));

        [Fact]
        public void Afrecho_SinGeocercaDeDescarga_TomaLaPrimeraParadaLargaDespuesDeTci()
        {
            // Recorrido supuesto (todavía no hay un viaje completo): Paita → Complex → TCI → Balsas → base.
            var gps = new List<OnwayHistoryPoint> { Punto("2026-10-24 07:00", -5.00, -80.90, 40) };
            gps.AddRange(Parado(GeocercasTerceros.AlicorpPaita, "2026-10-24 08:06", "2026-10-24 10:00", "2026-10-24 12:00"));
            gps.Add(Punto("2026-10-24 12:30", -5.00, -80.80, 50));
            gps.AddRange(Parado(GeocercasTrazabilidad.ComplexAcceso, "2026-10-24 18:00", "2026-10-24 19:00", "2026-10-24 20:00"));
            gps.Add(Punto("2026-10-24 20:30", -4.30, -80.50, 60));
            gps.AddRange(Parado(GeocercasTrazabilidad.Tci, "2026-10-24 21:00", "2026-10-24 22:00", "2026-10-24 23:00"));
            gps.Add(Punto("2026-10-24 23:30", -3.60, -80.00, 50));
            // Parada corta (20 min) en el camino: no es la descarga.
            gps.Add(Punto("2026-10-24 23:45", -3.55, -80.05, 0, "Vía Arenillas, Arenillas, El Oro"));
            gps.Add(Punto("2026-10-25 00:05", -3.60, -79.95, 45));
            foreach (var h in new[] { "2026-10-25 01:00", "2026-10-25 02:00", "2026-10-25 04:00" })
                gps.Add(Punto(h, -3.77, -79.84, 0, "Calle Sucre, Balsas, El Oro"));
            gps.Add(Punto("2026-10-25 04:30", -3.60, -80.00, 40));
            gps.AddRange(Parado(GeocercasTrazabilidad.BaseSullana, "2026-10-25 09:00", "2026-10-25 10:00", "2026-10-25 11:00"));

            var r = TrazabilidadTercerosService.Detectar("AFRECHO", "PAITA", null, gps, null, Utc("2026-10-24 00:00"));

            Hora(r, "fhLlegadaTrujillo", "2026-10-24 08:06");
            Hora(r, "fhSalidaPlanta", "2026-10-24 12:30");
            Hora(r, "fhLlegadaBodegaNacional", "2026-10-24 18:00");
            Hora(r, "fhSalidaBodegaNacional", "2026-10-24 20:30");
            Hora(r, "fhLlegadaTCI", "2026-10-24 21:00");
            Hora(r, "fhSalidaTCI", "2026-10-24 23:30");
            Hora(r, "fhLlegadaPlantaEcuador", "2026-10-25 01:00");
            Hora(r, "fhSalida", "2026-10-25 04:30");
            Hora(r, "fhLlegadaBaseFinal", "2026-10-25 09:00");
            Assert.Equal("BALSAS", r.LugarEntrega);
            Assert.Equal("BASE", r.LugarFin);
            Assert.Equal("UNIDAD DESCARGADA EN SU TOTALIDAD", StatusFinal("AFRECHO", r, "PAITA", null));
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
