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

        /// <summary>Crisar de vuelta de La Troncal: parada en el grifo Santa Rosa (no es hito de la ruta: va en sus columnas).</summary>
        public HitoGps LlegadaGrifo { get; set; }
        public HitoGps SalidaGrifo { get; set; }

        public HitoGps Buscar(string columna) => Hitos.FirstOrDefault(h => h.Item1 == columna)?.Item2;
    }

    /// <summary>
    /// Recorrido de los viajes de Crisar, Novopan y Polinplast con las geocercas de
    /// <see cref="GeocercasTerceros"/> y las de Vitapro en la frontera (Complex, TCI, CEBAF):
    ///
    ///   Crisar      Cartavio → base Sullana → (relevo) Complex → TCI → La Troncal → fin
    ///   Novopan     Quito → TCI → CEBAF → Piura o Trujillo → fin
    ///   Polinplast  Chapaira → destino (Virú, Casa Grande, ACP…) → fin
    ///   Afrecho     Alicorp Paita → Complex → TCI → primera parada larga en Ecuador (sin geocerca aún) → fin
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
            void Marcar(Tuple<GeocercaGps, Visita> v, string llegada, string salida, string nombre)
            {
                if (v == null) return;
                Agregar(llegada, Hito(pd[v.Item2.Primero], "Primer punto en " + nombre));
                if (salida != null && v.Item2.Cerrada) Agregar(salida, Hito(pd[v.Item2.Ultimo + 1], "Primer punto fuera de " + nombre));
                c = v.Item2.Cerrada ? v.Item2.Ultimo + 1 : v.Item2.Ultimo;
            }
            void Bodega(GeocercaGps paso, int minutos, string llegada, string salida, string nombre) =>
                Marcar(Primera(pd, new[] { paso }, c, hasta, minutos), llegada, salida, nombre);
            // TCI como en Vitapro (TrazabilidadDescargaService): si no entra al patio (o entra menos de los minutos),
            // el control de la Aduana del Ecuador de al lado. BSD-716 de Crisar, 22/10/2026: 2 min en TCI y 7 en el
            // control; sin esto no se marcaba TCI y el viaje no llegaba nunca a La Troncal.
            void Tci()
            {
                var tci = Primera(pd, new[] { GeocercasTrazabilidad.Tci }, c, hasta, MinutosMinimosEnTci);
                var control = Primera(pd, new[] { GeocercasTrazabilidad.TciControlAduana }, c, hasta, 1);
                bool directo = tci == null || (control != null && control.Item2.Primero < tci.Item2.Primero);
                Marcar(directo ? control : tci, "fhLlegadaTCI", "fhSalidaTCI", directo ? "el control de la Aduana junto a TCI (paso directo)" : "TCI");
            }
            if (ruta == RutaTercero.Crisar || ruta == RutaTercero.Afrecho)
            {
                Bodega(GeocercasTrazabilidad.ComplexAcceso, MinutosMinimosEnBodega, "fhLlegadaBodegaNacional", "fhSalidaBodegaNacional", "la cola del Complex");
                Tci();
            }
            else if (ruta == RutaTercero.Novopan)
            {
                Tci();
                Bodega(GeocercasTrazabilidad.Cebaf, 0, "fhLlegadaCEBAF", null, "el CEBAF");
            }

            // Afrecho: todavía sin geocerca de descarga, es la primera parada larga después de salir de TCI
            // (ya en Ecuador), con el lugar que da la dirección de Onway. Por confirmar con cada viaje.
            Visita enDescarga = descarga?.Item2;
            string nombreDescarga = descarga?.Item1.Nombre;
            string lugarDetectado = descarga == null ? null : GeocercasTerceros.LugarDe(descarga.Item1);
            if (enDescarga == null && ruta == RutaTercero.Afrecho && candidatas.Length == 0 && r.Buscar("fhSalidaTCI") != null)
            {
                enDescarga = ParadaLarga(pd, c, limiteDescarga, MinutosMinimosParadaDescarga, ZonasDeFrontera);
                if (enDescarga != null)
                {
                    lugarDetectado = StatusUnidad.LugarDeDireccion(pd[enDescarga.Primero].Address);
                    nombreDescarga = "la primera parada larga en Ecuador" + (lugarDetectado == null ? "" : " (" + lugarDetectado + ")");
                }
            }

            if (enDescarga == null) return r;
            if (zonasEntrega.Count == 0) r.LugarEntrega = lugarDetectado;
            Agregar("fhLlegadaPlantaEcuador", Hito(pd[enDescarga.Primero], "Primer punto en " + nombreDescarga));
            Agregar("fhLlegadaAlmacen", Hito(pd[enDescarga.Primero], "Primer punto en " + nombreDescarga));
            if (!enDescarga.Cerrada) return r;
            Agregar("fhSalida", Hito(pd[enDescarga.Ultimo + 1], "Primer punto fuera de " + nombreDescarga));

            // 5. Fin del viaje.
            int cursorFin = enDescarga.Ultimo + 1;
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

            // 6. Crisar: el grifo Santa Rosa de vuelta (86 de 86 viajes de sep–oct 2026 paran ahí), antes del fin.
            if (ruta == RutaTercero.Crisar)
            {
                var grifo = Primera(pd, new[] { GeocercasTrazabilidad.GrifoSantaRosa }, cursorFin, fin != null ? fin.Item2.Primero : limiteFin,
                    MinutosMinimosEnBodega, abiertaSinMinimo: false);
                if (grifo != null)
                {
                    r.LlegadaGrifo = Hito(pd[grifo.Item2.Primero], "Primer punto en el grifo Santa Rosa (El Retiro)");
                    if (grifo.Item2.Cerrada) r.SalidaGrifo = Hito(pd[grifo.Item2.Ultimo + 1], "Primer punto fuera del grifo Santa Rosa");
                }
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
            Tuple.Create(GeocercasTerceros.AlicorpPaita, "PAITA", 60),
        };

        /// <summary>Parada que cuenta como descarga cuando no hay geocerca (afrecho): una hora detenido.</summary>
        private const int MinutosMinimosParadaDescarga = 60;

        /// <summary>Velocidad (km/h) por debajo de la cual la unidad está detenida.</summary>
        private const double KmhDetenido = 3;

        /// <summary>Esperas de la frontera que no son la descarga.</summary>
        private static readonly GeocercaGps[] ZonasDeFrontera =
        {
            GeocercasTrazabilidad.ComplexAcceso, GeocercasTrazabilidad.Tci, GeocercasTrazabilidad.TciControlAduana,
            GeocercasTrazabilidad.Cebaf, GeocercasTrazabilidad.Cruce
        };

        /// <summary>
        /// La primera parada de al menos <paramref name="minutos"/> (puntos seguidos a <see cref="KmhDetenido"/>
        /// o menos) que empieza en [desde, hasta) fuera de las zonas dadas. Abierta si el historial termina
        /// detenido: la unidad sigue ahí (vale aunque todavía no llegue a los minutos).
        /// </summary>
        private static Visita ParadaLarga(List<OnwayHistoryPoint> puntos, int desde, int hasta, int minutos, GeocercaGps[] excluir)
        {
            int i = Math.Max(desde, 0);
            while (i < hasta && i < puntos.Count)
            {
                if (puntos[i].Speed > KmhDetenido) { i++; continue; }
                int j = i;
                while (j + 1 < puntos.Count && puntos[j + 1].Speed <= KmhDetenido) j++;
                bool cerrada = j + 1 < puntos.Count;
                bool larga = !cerrada || (Hora(puntos[j + 1]) - Hora(puntos[i])).TotalMinutes >= minutos;
                if (larga && !excluir.Any(z => z.Contiene(puntos[i].Lat, puntos[i].Lng)))
                    return new Visita { Primero = i, Ultimo = j, Cerrada = cerrada };
                i = j + 1;
            }
            return null;
        }

        private static GeocercaGps CargaPorDefecto(string ruta)
        {
            switch (ruta)
            {
                case RutaTercero.Crisar: return GeocercasTerceros.CrisarCartavio;
                case RutaTercero.Novopan: return GeocercasTerceros.NovopanQuito;
                case RutaTercero.Polinplast: return GeocercasTerceros.PolinplastChapaira;
                case RutaTercero.Afrecho: return GeocercasTerceros.AlicorpPaita;
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
