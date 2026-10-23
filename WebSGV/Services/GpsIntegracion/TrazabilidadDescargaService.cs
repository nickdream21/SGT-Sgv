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

        /// <summary>Parada en el grifo Santa Rosa de vuelta a la base (casi todas cargan combustible ahí).</summary>
        public HitoGps LlegadaGrifo { get; set; }
        public HitoGps SalidaGrifo { get; set; }

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

                // 7. Grifo Santa Rosa, entre la salida de la planta y la llegada a la base.
                var grifo = PrimeraVisita(puntos, GeocercasTrazabilidad.GrifoSantaRosa, cursor, MinutosMinimosEnBodega);
                if (grifo != null && Hora(puntos[grifo.Primero]) <= Hora(puntos[cursor]).AddDays(DiasMaximosRegreso)
                    && (r.LlegadaBase == null || Hora(puntos[grifo.Primero]) < r.LlegadaBase.HoraUtc))
                {
                    r.LlegadaGrifo = Hito(puntos[grifo.Primero], "Primer punto en el grifo Santa Rosa (El Retiro)");
                    if (grifo.Cerrada) r.SalidaGrifo = Hito(puntos[grifo.Ultimo + 1], "Primer punto fuera del grifo Santa Rosa");
                }
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

        /// <summary>Más de esto afuera de la bodega es que salió a esperar: la entrada siguiente es otra.</summary>
        private const int MinutosEsperaAfuera = 60;

        /// <summary>Una entrada corta seguida de irse en estos minutos es el paso por la esquina al salir, no una entrada.</summary>
        private const int MinutosPasoAlIrse = 15;

        /// <summary>Hasta esta distancia del acceso la unidad sigue esperando su turno, aunque salga del acceso.</summary>
        private const double MetrosEsperaCerca = 3000;

        /// <summary>Horas máximas esperando cerca de la planta entre una entrada y la siguiente.</summary>
        private const int HorasMaximasEspera = 24;

        /// <summary>
        /// Visita al acceso de la planta (10+ min) con una estadía dentro de la bodega en la que se movió.
        /// Devuelve el acceso (de la llegada a la última salida) y la bodega (del ingreso a la salida).
        /// </summary>
        private static Tuple<Visita, Visita> BuscarDescarga(List<OnwayHistoryPoint> puntos, GeocercaGps acceso, GeocercaGps planta, int desde)
        {
            var visitasAcceso = Visitas(puntos, acceso);
            var visitasPlanta = Visitas(puntos, planta);
            for (int k = 0; k < visitasAcceso.Count; k++)
            {
                var va = visitasAcceso[k];
                if (va.Primero < desde || (Hora(puntos[va.Ultimo]) - Hora(puntos[va.Primero])).TotalMinutes < MinutosMinimosEnBodega)
                    continue;
                var dentro = visitasPlanta.Where(v => v.Primero >= va.Primero && v.Primero <= va.Ultimo)
                    .SkipWhile(v => !SeMovioAdentro(puntos, v)).ToList();
                if (dentro.Count == 0) continue;

                // Si espera afuera del acceso pero a la vista de la planta y vuelve a entrar, sigue siendo
                // la misma descarga; mientras siga cerca, todavía no salió.
                var fin = va;
                bool sigueEsperando = false;
                for (int j = k + 1; fin.Cerrada; j++)
                {
                    int hasta = j < visitasAcceso.Count ? visitasAcceso[j].Primero : puntos.Count;
                    if (!EsperoCerca(puntos, acceso, fin.Ultimo + 1, hasta)) break;
                    if (j >= visitasAcceso.Count) { sigueEsperando = true; break; }
                    var siguiente = visitasAcceso[j];
                    dentro.AddRange(visitasPlanta.Where(v => v.Primero >= siguiente.Primero && v.Primero <= siguiente.Ultimo));
                    fin = siguiente;
                }

                // En Jave a veces pasan la puerta pero no los descargan: salen a esperar (a la orilla de la
                // vía, horas) y vuelven a entrar a descargar. El ingreso es el de la última entrada, después
                // de más de 1 h afuera (pedidos 4400133406, 4400133436 y 4400133753 de octubre de 2026: el
                // ingreso era el del primer paso por la puerta). Se cuentan los puntos adentro, no las
                // estadías: la orilla donde esperan queda a pocos metros de la cerca y la estadía no se
                // corta (ARN-917 el 10/10).
                var ultima = dentro.Last();
                var entradas = new List<Visita>();
                int ultimoDentro = -1;
                bool huboAfuera = false;
                for (int i = dentro[0].Primero; i <= ultima.Ultimo; i++)
                {
                    if (!planta.Contiene(puntos[i].Lat, puntos[i].Lng)) { huboAfuera = true; continue; }
                    if (ultimoDentro >= 0 && !(huboAfuera && (Hora(puntos[i]) - Hora(puntos[ultimoDentro])).TotalMinutes > MinutosEsperaAfuera))
                        entradas[entradas.Count - 1].Ultimo = i;
                    else
                        entradas.Add(new Visita { Primero = i, Ultimo = i });
                    ultimoDentro = i;
                    huboAfuera = false;
                }
                // Al volver no siempre se queda dentro del polígono: descarga en el patio de al lado
                // (BSD-900 el 17/10: 9 min en la bodega y casi 2 h en el patio). Solo no cuenta un paso
                // corto justo antes de irse (ASU-748 el 17/10: 15 s por la esquina y a los 5 min se fue).
                var ingreso = entradas.Last();
                if (entradas.Count > 1 && (Hora(puntos[ingreso.Ultimo]) - Hora(puntos[ingreso.Primero])).TotalMinutes < MinutosMinimosEnBodega
                    && fin.Cerrada && (Hora(puntos[fin.Ultimo]) - Hora(puntos[ingreso.Ultimo])).TotalMinutes <= MinutosPasoAlIrse)
                    ingreso = entradas[entradas.Count - 2];

                // Al pasar la puerta la unidad cruza la esquina de las bodegas y se estaciona en el patio
                // de al lado: eso no es la salida (BSD-900 y otras 7 en Jave el 21/10/2026: "salían" al
                // minuto de entrar y seguían adentro). La salida es la última de la bodega, y solo
                // cuando la unidad ya dejó el recinto.
                bool salio = fin.Cerrada && ultima.Cerrada && !sigueEsperando;
                return Tuple.Create(new Visita { Primero = va.Primero, Ultimo = fin.Ultimo, Cerrada = salio },
                                    new Visita { Primero = ingreso.Primero, Ultimo = ultima.Ultimo, Cerrada = salio });
            }
            return null;
        }

        /// <summary>
        /// Entre los índices [desde, hasta) la unidad se quedó a menos de <see cref="MetrosEsperaCerca"/>
        /// del acceso y no más de <see cref="HorasMaximasEspera"/> horas: esperaba para volver a entrar.
        /// </summary>
        private static bool EsperoCerca(List<OnwayHistoryPoint> puntos, GeocercaGps acceso, int desde, int hasta)
        {
            if (desde >= puntos.Count) return true;
            DateTime tope = Hora(puntos[desde]).AddHours(HorasMaximasEspera);
            if (hasta < puntos.Count && Hora(puntos[hasta]) > tope) return false;
            for (int i = desde; i < hasta; i++)
                if (Hora(puntos[i]) > tope || acceso.DistanciaAlBordeMetros(puntos[i].Lat, puntos[i].Lng) > MetrosEsperaCerca)
                    return false;
            return true;
        }
    }
}
