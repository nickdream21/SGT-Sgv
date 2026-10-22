using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Exportacion;
using static WebSGV.Services.GpsIntegracion.TrazabilidadCargaService;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>Lo que el GPS muestra de un viaje de un cliente tercero (<see cref="RutaTercero"/>).</summary>
    public class ResultadoTramoTercero
    {
        /// <summary>(columna de SeguimientoExportacion, hito), en el orden del recorrido.</summary>
        public List<Tuple<string, HitoGps>> Hitos { get; } = new List<Tuple<string, HitoGps>>();

        /// <summary>Lugar de la descarga cuando el viaje no lo tenía y el GPS lo encontró ("VIRU").</summary>
        public string LugarEntrega { get; set; }

        /// <summary>Dónde terminó: BASE, COCHERA TRUJILLO, ESPERA TRUJILLO o la carga siguiente.</summary>
        public string LugarFin { get; set; }

        public HitoGps Buscar(string columna) => Hitos.FirstOrDefault(h => h.Item1 == columna)?.Item2;
    }

    /// <summary>
    /// Recorrido de los viajes de Crisar, Novopan y Polinplast con las geocercas de
    /// <see cref="GeocercasTerceros"/> y las de Vitapro en la frontera (Complex, TCI, CEBAF):
    ///
    ///   Crisar      Cartavio → base Sullana → (relevo) Complex → TCI → La Troncal → fin
    ///   Novopan     Quito → TCI → CEBAF → Piura o Trujillo → fin
    ///   Polinplast  Chapaira → destino (Virú, Casa Grande, ACP…) → fin
    ///
    /// La planta de carga es el ancla: sin ella no hay viaje. La descarga es la segunda ancla: las
    /// paradas intermedias (base, Complex, TCI, CEBAF) solo se buscan entre las dos, para no tomar
    /// las del viaje siguiente del tracto. El fin es la primera parada larga después de descargar en
    /// la base, la cochera de Trujillo o la carga siguiente (Vitapro Trujillo, Cartavio, Chapaira).
    /// </summary>
    public static class TrazabilidadTercerosService
    {
        private const int MinutosMinimosEnCarga = 20;
        private const int MinutosMinimosEnDescarga = 20;
        private const int MinutosMinimosEnBodega = 10;
        private const int MinutosMinimosEnTci = 3;
        private const int MinutosMinimosEnFin = 30;

        /// <summary>Estadía en la cochera de Trujillo para que el viaje termine "EN COCHERA".</summary>
        private const int MinutosMinimosCochera = 20 * 60;

        /// <summary>Días que la carreta de Crisar puede esperar en la base al tracto de descarga (BPT-919: 3, del 16 al 19/10/2026).</summary>
        private const int DiasCarretaEnBase = 6;

        /// <summary>Días desde la programación hasta llegar a la planta de carga.</summary>
        private const int DiasHastaCarga = 3;

        /// <summary>Días de la salida de la carga a la descarga (Quito → Trujillo son ~4).</summary>
        private const int DiasHastaDescarga = 6;

        /// <summary>Días de la descarga al fin del viaje.</summary>
        private const int DiasHastaFin = 7;

        /// <param name="historialCarga">Puntos del tracto que carga.</param>
        /// <param name="historialDescarga">Puntos del tracto que descarga (relevo de Crisar); null si es el mismo.</param>
        /// <param name="desdeUtc">Desde cuándo buscar la planta de carga (vale también si ya estaba ahí: el Excel de Novopan pone el día siguiente al de la carga en Quito).</param>
        /// <param name="descargaAsignada">Crisar: el Word ya dijo qué tracto descarga. Sin eso, el tracto de carga que sale de la base puede llevar otra carreta (AMQ-859 el 20/10/2026 se fue con una de Vitapro).</param>
        public static ResultadoTramoTercero Detectar(string tipoRuta, string lugarCarga, string lugarEntrega,
            IEnumerable<OnwayHistoryPoint> historialCarga, IEnumerable<OnwayHistoryPoint> historialDescarga, DateTime desdeUtc,
            bool descargaAsignada = true)
        {
            var r = new ResultadoTramoTercero();
            string ruta = (tipoRuta ?? "").Trim().ToUpperInvariant();
            var pc = Ordenar(historialCarga);
            var pd = historialDescarga == null ? pc : Ordenar(historialDescarga);
            void Agregar(string columna, HitoGps hito) { if (hito != null) r.Hitos.Add(Tuple.Create(columna, hito)); }

            int cursor = pc.FindIndex(p => Hora(p) >= desdeUtc);
            if (cursor < 0) return r;

            // 1. Planta de carga (ancla).
            var zonaCarga = GeocercasTerceros.DeLugar(ruta, lugarCarga) ?? CargaPorDefecto(ruta);
            if (zonaCarga == null) return r;
            var carga = Primera(pc, new[] { zonaCarga }, cursor, Limite(pc, cursor, DiasHastaCarga), MinutosMinimosEnCarga, yaAdentro: true);
            if (carga == null) return r;
            Agregar("fhLlegadaTrujillo", Hito(pc[carga.Item2.Primero], "Primer punto en " + zonaCarga.Nombre));
            if (!carga.Item2.Cerrada) return r;
            Agregar("fhSalidaPlanta", Hito(pc[carga.Item2.Ultimo + 1], "Primer punto fuera de " + zonaCarga.Nombre));
            DateTime salidaCarga = Hora(pc[carga.Item2.Ultimo + 1]);
            int cursorCarga = carga.Item2.Ultimo + 1;

            // 2. Crisar: el tracto de carga deja la carreta en la base Sullana; de ahí sale el de descarga.
            DateTime desdeDescarga = salidaCarga;
            if (ruta == RutaTercero.Crisar)
            {
                var llegadaBase = Primera(pc, new[] { GeocercasTrazabilidad.BaseSullana }, cursorCarga, Limite(pc, cursorCarga, 2), 1);
                if (llegadaBase != null)
                {
                    Agregar("fhLlegadaBase2", Hito(pc[llegadaBase.Item2.Primero], "Primer punto en la base Sullana"));
                    desdeDescarga = Hora(pc[llegadaBase.Item2.Primero]);
                }
            }

            // Con relevo, el tracto de descarga recoge la carreta en la base: sin llegada a la base no hay descarga todavía.
            if (ruta == RutaTercero.Crisar && historialDescarga != null && r.Buscar("fhLlegadaBase2") == null) return r;
            if (ruta == RutaTercero.Crisar && !descargaAsignada && r.Buscar("fhLlegadaBase2") != null) return r;

            int cursorD = pd.FindIndex(p => Hora(p) >= desdeDescarga);
            if (cursorD < 0) return r;
            if (ruta == RutaTercero.Crisar && r.Buscar("fhLlegadaBase2") != null)
            {
                // La salida de la base con la carreta: la primera salida del tracto de descarga desde ahí.
                var salidaBase = Visitas(pd, GeocercasTrazabilidad.BaseSullana)
                    .FirstOrDefault(v => v.Cerrada && v.Ultimo + 1 >= cursorD && Hora(pd[v.Ultimo + 1]) <= desdeDescarga.AddDays(DiasCarretaEnBase));
                if (salidaBase != null)
                {
                    Agregar("fhSalidaBase2", Hito(pd[salidaBase.Ultimo + 1], "Primer punto fuera de la base Sullana"));
                    cursorD = salidaBase.Ultimo + 1;
                }
                // Con relevo, sin esa salida la carreta sigue en la base: lo que el tracto de descarga haga
                // mientras tanto es de otro viaje (BSD-716 y BNH-712 el 20–21/10/2026 terminaban una descarga
                // de Vitapro y una de Crisar anterior; se tomaban su TCI y su La Troncal).
                else if (historialDescarga != null) return r;
            }

            // 3. Descarga (segunda ancla): el lugar del viaje o, si no se sabe, el primero de la ruta donde se detuvo.
            var zonasEntrega = GeocercasTerceros.ZonasDe(ruta, lugarEntrega);
            var candidatas = zonasEntrega.Count > 0 ? zonasEntrega.ToArray() : DescargasPosibles(ruta);
            int limiteDescarga = Limite(pd, cursorD, DiasHastaDescarga);
            var descarga = Primera(pd, candidatas, cursorD, limiteDescarga, MinutosMinimosEnDescarga);
            int hasta = descarga != null ? descarga.Item2.Primero : limiteDescarga;

            // 4. Paso por la frontera, entre la carga y la descarga.
            int c = cursorD;
            void Bodega(GeocercaGps paso, int minutos, string llegada, string salida, string nombre)
            {
                var v = Primera(pd, new[] { paso }, c, hasta, minutos);
                if (v == null) return;
                Agregar(llegada, Hito(pd[v.Item2.Primero], "Primer punto en " + nombre));
                if (salida != null && v.Item2.Cerrada) Agregar(salida, Hito(pd[v.Item2.Ultimo + 1], "Primer punto fuera de " + nombre));
                c = v.Item2.Cerrada ? v.Item2.Ultimo + 1 : v.Item2.Ultimo;
            }
            if (ruta == RutaTercero.Crisar)
            {
                Bodega(GeocercasTrazabilidad.ComplexAcceso, MinutosMinimosEnBodega, "fhLlegadaBodegaNacional", "fhSalidaBodegaNacional", "la cola del Complex");
                Bodega(GeocercasTrazabilidad.Tci, MinutosMinimosEnTci, "fhLlegadaTCI", "fhSalidaTCI", "TCI");
            }
            else if (ruta == RutaTercero.Novopan)
            {
                Bodega(GeocercasTrazabilidad.Tci, MinutosMinimosEnTci, "fhLlegadaTCI", "fhSalidaTCI", "TCI");
                Bodega(GeocercasTrazabilidad.Cebaf, 0, "fhLlegadaCEBAF", null, "el CEBAF");
            }

            if (descarga == null) return r;
            var zona = descarga.Item1;
            if (zonasEntrega.Count == 0) r.LugarEntrega = GeocercasTerceros.LugarDe(zona);
            Agregar("fhLlegadaPlantaEcuador", Hito(pd[descarga.Item2.Primero], "Primer punto en " + zona.Nombre));
            Agregar("fhLlegadaAlmacen", Hito(pd[descarga.Item2.Primero], "Primer punto en " + zona.Nombre));
            if (!descarga.Item2.Cerrada) return r;
            Agregar("fhSalida", Hito(pd[descarga.Item2.Ultimo + 1], "Primer punto fuera de " + zona.Nombre));

            // 5. Fin del viaje.
            int cursorFin = descarga.Item2.Ultimo + 1;
            int limiteFin = Limite(pd, cursorFin, DiasHastaFin);
            Tuple<GeocercaGps, Visita> fin = null;
            string lugarFin = null;
            foreach (var f in Finales)
            {
                var v = Primera(pd, new[] { f.Item1 }, cursorFin, limiteFin, f.Item3, abiertaSinMinimo: f.Item3 < MinutosMinimosCochera);
                if (v != null && (fin == null || v.Item2.Primero < fin.Item2.Primero)) { fin = v; lugarFin = f.Item2; }
            }
            if (fin != null)
            {
                r.LugarFin = lugarFin;
                Agregar("fhLlegadaBaseFinal", Hito(pd[fin.Item2.Primero], "Primer punto en " + fin.Item1.Nombre + " después de descargar"));
            }
            return r;
        }

        // Donde termina el viaje (zona, lugarFin, minutos mínimos): la base, guardada en Trujillo o ya en la
        // carga siguiente. En la cola de Vitapro Trujillo esperan un par de horas antes de ir a la cochera
        // (BNG-758, 27/08/2026): ahí solo cuenta una espera larga; en el grifo cargan combustible al pasar.
        private static readonly Tuple<GeocercaGps, string, int>[] Finales =
        {
            Tuple.Create(GeocercasTrazabilidad.BaseSullana, "BASE", MinutosMinimosEnFin),
            // La cochera es donde la unidad se guarda un día o más (los 5 viajes de fines de agosto: 1 a 4 días);
            // BPT-788 para unas horas ahí en casi todos sus viajes y el Excel dice "EN SU TOTALIDAD".
            Tuple.Create(GeocercasTerceros.CocheraTrujillo, RutaTercero.FinCochera, MinutosMinimosCochera),
            Tuple.Create(GeocercasTerceros.EsperaGrifoCurvaSun, RutaTercero.FinEsperaTrujillo, 60),
            Tuple.Create(GeocercasTrazabilidad.TrujilloAcceso, "VITAPRO TRUJILLO", 180),
            Tuple.Create(GeocercasTerceros.CrisarCartavio, "CARTAVIO", 60),
            Tuple.Create(GeocercasTerceros.PolinplastChapaira, "CHAPAIRA", 60),
        };

        private static GeocercaGps CargaPorDefecto(string ruta)
        {
            switch (ruta)
            {
                case RutaTercero.Crisar: return GeocercasTerceros.CrisarCartavio;
                case RutaTercero.Novopan: return GeocercasTerceros.NovopanQuito;
                case RutaTercero.Polinplast: return GeocercasTerceros.PolinplastChapaira;
                default: return null;
            }
        }

        private static GeocercaGps[] DescargasPosibles(string ruta)
        {
            switch (ruta)
            {
                case RutaTercero.Crisar: return new[] { GeocercasTerceros.CrisarLaTroncal };
                case RutaTercero.Novopan: return new[] { GeocercasTerceros.NovopanPiura, GeocercasTerceros.NovopanTrujillo };
                case RutaTercero.Polinplast: return GeocercasTerceros.DescargasPolinplast.ToArray();
                default: return new GeocercaGps[0];
            }
        }

        /// <summary>Índice del primer punto después de <paramref name="dias"/> días desde <paramref name="desde"/> (o el final).</summary>
        private static int Limite(List<OnwayHistoryPoint> puntos, int desde, int dias)
        {
            if (desde >= puntos.Count) return puntos.Count;
            DateTime tope = Hora(puntos[desde]).AddDays(dias);
            int i = puntos.FindIndex(desde, p => Hora(p) > tope);
            return i < 0 ? puntos.Count : i;
        }

        /// <summary>
        /// La primera visita a cualquiera de las zonas que empieza en [desde, hasta) y dura al menos
        /// <paramref name="minutos"/> (o sigue adentro: la unidad está ahí ahora).
        /// </summary>
        /// <param name="yaAdentro">Vale también una visita que empezó antes de <paramref name="desde"/> y sigue después.</param>
        /// <param name="abiertaSinMinimo">Una visita en curso vale aunque todavía no dure <paramref name="minutos"/>.</param>
        private static Tuple<GeocercaGps, Visita> Primera(List<OnwayHistoryPoint> puntos, GeocercaGps[] zonas, int desde, int hasta, int minutos,
            bool yaAdentro = false, bool abiertaSinMinimo = true)
        {
            Tuple<GeocercaGps, Visita> mejor = null;
            foreach (var z in zonas)
            {
                var v = Visitas(puntos, z).FirstOrDefault(x => (yaAdentro ? x.Ultimo >= desde : x.Primero >= desde) && x.Primero < hasta &&
                    ((!x.Cerrada && abiertaSinMinimo) || (Hora(puntos[x.Ultimo]) - Hora(puntos[x.Primero])).TotalMinutes >= minutos));
                if (v != null && (mejor == null || v.Primero < mejor.Item2.Primero)) mejor = Tuple.Create(z, v);
            }
            return mejor;
        }
    }
}
