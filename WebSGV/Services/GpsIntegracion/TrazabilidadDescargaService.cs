using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.GpsIntegracion;
using static WebSGV.Services.GpsIntegracion.TrazabilidadCargaService;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Tramo de descarga de un tracto: sale de base, pasa por la bodega nacional (Complex), el CEBAF,
    /// cruza a Ecuador, pasa por TCI y descarga en Jave o Inbalnor. Cada hito es null si el GPS no lo muestra.
    /// </summary>
    public class ResultadoTramoDescarga
    {
        public HitoGps SalidaBase { get; set; }
        public HitoGps LlegadaComplex { get; set; }
        public HitoGps IngresoComplex { get; set; }
        public HitoGps SalidaComplex { get; set; }
        public HitoGps LlegadaCebaf { get; set; }
        public HitoGps Cruce { get; set; }
        public HitoGps LlegadaTci { get; set; }
        public HitoGps SalidaTci { get; set; }

        /// <summary>No entró a TCI: llegada y salida son las del control aduanero cercano.</summary>
        public bool PasoDirectoTci { get; set; }

        /// <summary>"JAVE" o "INBALNOR"; null si no se detectó la descarga.</summary>
        public string PlantaDescarga { get; set; }

        /// <summary>Cerca o afuera de la planta: va en F.H.LL.Planta y en F.H.LL.Almacén.</summary>
        public HitoGps LlegadaPlanta { get; set; }
        public HitoGps IngresoPlanta { get; set; }
        public HitoGps SalidaPlanta { get; set; }
        public HitoGps LlegadaBase { get; set; }
    }

    /// <summary>
    /// Detección del tramo de descarga con las mismas reglas que el de carga
    /// (<see cref="TrazabilidadCargaService"/>): visitas a geocercas en polígono, cada parada buscada
    /// después de la anterior, y la salida de base como la última salida antes de la primera parada.
    /// Si una parada no aparece (sin GPS ese rato), las siguientes se buscan desde la última encontrada.
    /// </summary>
    public static class TrazabilidadDescargaService
    {
        /// <summary>Estadía mínima en el acceso de una bodega (Complex, Jave, Inbalnor): menos es pasar de largo.</summary>
        private const int MinutosMinimosEnBodega = 10;

        /// <summary>Estadía mínima en TCI (el trámite dura pocos minutos).</summary>
        private const int MinutosMinimosEnTci = 3;

        /// <summary>Días máximos de la salida de la carga al Complex y del Complex a la planta: más lejos es otro viaje.</summary>
        private const int DiasMaximosEntreAnclas = 4;

        /// <summary>Días máximos de la salida de la planta de descarga a la llegada a la base.</summary>
        private const int DiasMaximosRegreso = 7;

        public static ResultadoTramoDescarga DetectarTramoDescarga(IEnumerable<OnwayHistoryPoint> historial, DateTime desdeUtc)
        {
            var puntos = Ordenar(historial);
            var r = new ResultadoTramoDescarga();
            int cursor = puntos.FindIndex(p => Hora(p) >= desdeUtc);
            if (cursor < 0) return r;

            // El tramo empieza cuando el tracto sale de la base con la carreta, después de la carga.
            // Sin esa salida no hay descarga todavía: lo que vea antes (p. ej. pasar por el CEBAF
            // volviendo de Ecuador de su viaje anterior) no es de este viaje (caso del 19/10/2026).
            var salidaBase = Visitas(puntos, GeocercasTrazabilidad.BaseSullana)
                .FirstOrDefault(v => v.Cerrada && v.Ultimo + 1 >= cursor);
            if (salidaBase == null || Hora(puntos[salidaBase.Ultimo + 1]) > Hora(puntos[cursor]).AddDays(DiasMaximosEntreAnclas))
                return r;
            DateTime desdeSalidaUtc = Hora(puntos[salidaBase.Ultimo + 1]);
            cursor = salidaBase.Ultimo + 1;
            int primerHito = -1;
            void Marcar(int i) { if (primerHito < 0) primerHito = i; }

            // 1. Bodega nacional (Complex): llegada a la cola, ingreso y salida del patio.
            var complex = PrimeraVisita(puntos, GeocercasTrazabilidad.ComplexAcceso, cursor, MinutosMinimosEnBodega);
            if (complex != null && Hora(puntos[complex.Primero]) > Hora(puntos[cursor]).AddDays(DiasMaximosEntreAnclas))
                complex = null; // demasiado lejos: es de otro viaje del mismo tracto
            if (complex != null)
            {
                r.LlegadaComplex = Hito(puntos[complex.Primero], "Primer punto en la cola del Complex (Av. Juan Velasco Alvarado)");
                Marcar(complex.Primero);
                var patio = Visitas(puntos, GeocercasTrazabilidad.ComplexPatio)
                    .FirstOrDefault(v => v.Primero >= complex.Primero && v.Primero <= complex.Ultimo);
                if (patio != null)
                {
                    r.IngresoComplex = Hito(puntos[patio.Primero], "Primer punto dentro del patio del Complex");
                    if (patio.Cerrada) r.SalidaComplex = Hito(puntos[patio.Ultimo + 1], "Primer punto fuera del patio del Complex");
                }
                cursor = complex.Cerrada ? complex.Ultimo + 1 : complex.Ultimo;
            }

            // 2. Planta de descarga (el ancla, como la planta de Trujillo en la carga): la primera de
            // Jave o Inbalnor donde la unidad se queda y entra. CEBAF, cruce y TCI se buscan solo
            // entre el Complex y la planta: si uno no aparece queda vacío, en vez de tomarlo del
            // viaje siguiente (caso T7J-861 del 18/09: sin punto en el CEBAF, se tomaba el del 22/09).
            var jave = BuscarDescarga(puntos, GeocercasTrazabilidad.JaveAcceso, GeocercasTrazabilidad.JavePlanta, cursor);
            var inbalnor = BuscarDescarga(puntos, GeocercasTrazabilidad.InbalnorAcceso, GeocercasTrazabilidad.InbalnorPlanta, cursor);
            var descarga = jave != null && (inbalnor == null || jave.Item1.Primero <= inbalnor.Item1.Primero) ? jave : inbalnor;
            if (descarga != null && Hora(puntos[descarga.Item1.Primero]) > Hora(puntos[cursor]).AddDays(DiasMaximosEntreAnclas))
                descarga = null;
            int limite = descarga != null ? descarga.Item1.Primero : FinDeVentana(puntos, cursor, DiasMaximosEntreAnclas);

            // 3. CEBAF y cruce: zonas de paso; si el GPS no marcó un punto adentro, se estima el
            // momento en que la unidad la atravesó entre dos puntos seguidos.
            var cebaf = PrimerPaso(puntos, GeocercasTrazabilidad.Cebaf, cursor, limite, "dentro del CEBAF", "por el CEBAF");
            if (cebaf != null)
            {
                r.LlegadaCebaf = cebaf.Item2;
                Marcar(cebaf.Item1);
                cursor = cebaf.Item1;
            }
            var cruce = PrimerPaso(puntos, GeocercasTrazabilidad.Cruce, cursor, limite, "en la E50 después de la aduana de Ecuador", "a la E50 después de la aduana de Ecuador");
            if (cruce != null)
            {
                r.Cruce = cruce.Item2;
                Marcar(cruce.Item1);
                cursor = cruce.Item1;
            }

            // 4. TCI. Si se coordinó para que pase directo, no entra y se toma el control aduanero
            // cercano; si entró, ese control viene después y no cuenta.
            var tci = PrimeraVisita(puntos, GeocercasTrazabilidad.Tci, cursor, MinutosMinimosEnTci);
            var control = PrimeraVisita(puntos, GeocercasTrazabilidad.TciControlAduana, cursor, 1);
            if (tci != null && tci.Primero >= limite) tci = null;
            if (control != null && control.Primero >= limite) control = null;
            r.PasoDirectoTci = tci == null || (control != null && control.Primero < tci.Primero);
            var visitaTci = r.PasoDirectoTci ? control : tci;
            if (visitaTci != null)
            {
                string lugar = r.PasoDirectoTci ? "el control aduanero junto a TCI (paso directo)" : "TCI";
                r.LlegadaTci = Hito(puntos[visitaTci.Primero], "Primer punto en " + lugar);
                if (visitaTci.Cerrada)
                    r.SalidaTci = Hito(puntos[visitaTci.Ultimo + 1], "Primer punto fuera " + (r.PasoDirectoTci ? "del control aduanero junto a TCI (paso directo)" : "de TCI"));
                Marcar(visitaTci.Primero);
                cursor = visitaTci.Cerrada ? visitaTci.Ultimo + 1 : visitaTci.Ultimo;
            }
            else r.PasoDirectoTci = false;

            // 5. Hitos de la planta de descarga.
            if (descarga != null)
            {
                string nombre = descarga == jave ? "Jave" : "Inbalnor";
                r.PlantaDescarga = nombre.ToUpperInvariant();
                r.LlegadaPlanta = Hito(puntos[descarga.Item1.Primero], "Primer punto en el acceso a " + nombre);
                r.IngresoPlanta = Hito(puntos[descarga.Item2.Primero], "Primer punto dentro de la bodega " + nombre);
                Marcar(descarga.Item1.Primero);
                if (descarga.Item2.Cerrada)
                {
                    r.SalidaPlanta = Hito(puntos[descarga.Item2.Ultimo + 1], "Primer punto fuera de la bodega " + nombre);
                    cursor = descarga.Item2.Ultimo + 1;
                }
                else cursor = -1; // sigue descargando: todavía no hay regreso
            }

            // 6. Salida de base antes de la primera parada y regreso a la base al final.
            if (primerHito >= 0)
                r.SalidaBase = BuscarSalidaBaseAntesDe(puntos, primerHito);
            else
                // Salió y va hacia Tumbes, pero todavía no llega al Complex: "rumbo a Ecuador".
                r.SalidaBase = SalidaEnCamino(puntos, desdeSalidaUtc, RumboAEcuador);
            if (r.SalidaPlanta != null && cursor >= 0)
            {
                // Hasta 7 días: a veces el tracto hace otro servicio en Ecuador antes de volver (BFK-879
                // fue a Quito después de descargar en Inbalnor el 10/10/2026 y llegó a la base el 13/10).
                BuscarRegresoBase(puntos, cursor, out var llegada, out _, out _, DiasMaximosRegreso);
                r.LlegadaBase = llegada ?? SiguioATrujilloSinEntrar(puntos, cursor);
            }
            return r;
        }

        /// <summary>
        /// Vuelta corrida: después de descargar, el tracto sigue de frente a Trujillo a su próxima
        /// carga sin entrar a la base (casos ASU-862, que pasó por Marcavelica, y TBM-815, que fue
        /// por Sechura, el 20/10/2026: el viaje se quedaba "Retorno de Ecuador" para siempre). Si
        /// llega al sur de Piura sin haber entrado a la base, el regreso termina en el punto donde
        /// pasó más cerca de ella.
        /// </summary>
        internal static HitoGps SiguioATrujilloSinEntrar(List<OnwayHistoryPoint> puntos, int desde)
        {
            var baseSullana = GeocercasTrazabilidad.BaseSullana;
            int idxCerca = -1;
            double metrosMinimos = double.MaxValue;
            for (int i = desde; i < puntos.Count; i++)
            {
                var p = puntos[i];
                if (baseSullana.Contiene(p.Lat, p.Lng)) return null; // entró: es una llegada normal
                if (RumboATrujillo(p))
                    return idxCerca < 0 ? null : Hito(puntos[idxCerca],
                        $"No entró a la base: siguió hacia Trujillo (punto más cercano a la base, a {metrosMinimos / 1000:0.0} km)");
                double metros = CheckpointMatchingService.CalcularDistanciaMetros(
                    p.Lat, p.Lng, baseSullana.LatitudCentro, baseSullana.LongitudCentro);
                if (metros < metrosMinimos)
                {
                    metrosMinimos = metros;
                    idxCerca = i;
                }
            }
            return null;
        }

        private static Visita PrimeraVisita(List<OnwayHistoryPoint> puntos, GeocercaGps zona, int desde, int minutosMinimos) =>
            Visitas(puntos, zona).FirstOrDefault(v => v.Primero >= desde &&
                (Hora(puntos[v.Ultimo]) - Hora(puntos[v.Primero])).TotalMinutes >= minutosMinimos);

        /// <summary>Índice del primer punto posterior a <paramref name="dias"/> días desde <paramref name="desde"/> (o el final).</summary>
        private static int FinDeVentana(List<OnwayHistoryPoint> puntos, int desde, int dias)
        {
            DateTime tope = Hora(puntos[desde]).AddDays(dias);
            int i = puntos.FindIndex(desde, p => Hora(p) > tope);
            return i < 0 ? puntos.Count : i;
        }

        /// <summary>
        /// Primer paso por una zona entre los índices [desde, hasta): el primer punto adentro o, si
        /// la unidad la atravesó entre dos puntos seguidos sin marcar ninguno adentro (de noche el
        /// GPS reporta cada 1-2 km), el momento estimado repartiendo el tiempo entre esos dos puntos.
        /// Devuelve el índice del punto que sigue al paso, para seguir buscando desde ahí.
        /// </summary>
        private static Tuple<int, HitoGps> PrimerPaso(List<OnwayHistoryPoint> puntos, GeocercaGps zona, int desde, int hasta,
            string textoDentro, string textoPaso)
        {
            const double MetrosEntreMuestras = 30;
            const double MinutosMaximosEntrePuntos = 15; // con un hueco mayor no se sabe por dónde fue
            for (int i = desde; i < hasta && i < puntos.Count; i++)
            {
                var p = puntos[i];
                if (zona.Contiene(p.Lat, p.Lng))
                    return Tuple.Create(i, Hito(p, "Primer punto " + textoDentro));
                if (i == desde) continue;

                var a = puntos[i - 1];
                double minutos = (Hora(p) - Hora(a)).TotalMinutes;
                double metros = CheckpointMatchingService.CalcularDistanciaMetros(a.Lat, a.Lng, p.Lat, p.Lng);
                if (minutos <= 0 || minutos > MinutosMaximosEntrePuntos || metros < MetrosEntreMuestras * 2) continue;

                int n = (int)(metros / MetrosEntreMuestras);
                for (int k = 1; k < n; k++)
                {
                    double f = (double)k / n;
                    double lat = a.Lat + f * (p.Lat - a.Lat), lng = a.Lng + f * (p.Lng - a.Lng);
                    if (!zona.Contiene(lat, lng)) continue;
                    DateTime t = Hora(a).AddTicks((long)((Hora(p) - Hora(a)).Ticks * f));
                    var estimado = new OnwayHistoryPoint { Lat = lat, Lng = lng, MessageTime = t, GenerateTime = t, Speed = p.Speed };
                    return Tuple.Create(i, new HitoGps
                    {
                        HoraUtc = t, Punto = estimado,
                        Fuente = $"Paso {textoPaso} estimado entre dos puntos del GPS ({Hora(a).AddHours(-5):HH:mm:ss} y {Hora(p).AddHours(-5):HH:mm:ss})",
                    });
                }
            }
            return null;
        }

        /// <summary>Visita al acceso de la planta (10+ min) con una estadía dentro de la bodega en la que se movió.</summary>
        private static Tuple<Visita, Visita> BuscarDescarga(List<OnwayHistoryPoint> puntos, GeocercaGps acceso, GeocercaGps planta, int desde)
        {
            var visitasPlanta = Visitas(puntos, planta);
            foreach (var va in Visitas(puntos, acceso).Where(v => v.Primero >= desde &&
                         (Hora(puntos[v.Ultimo]) - Hora(puntos[v.Primero])).TotalMinutes >= MinutosMinimosEnBodega))
            {
                var vp = visitasPlanta.FirstOrDefault(v => v.Primero >= va.Primero && v.Primero <= va.Ultimo && SeMovioAdentro(puntos, v));
                if (vp != null) return Tuple.Create(va, vp);
            }
            return null;
        }
    }
}
