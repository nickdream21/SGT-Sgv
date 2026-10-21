using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.GpsIntegracion;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>Un hito del recorrido detectado en el GPS, con el punto que lo prueba.</summary>
    public class HitoGps
    {
        /// <summary>Hora del punto tal como la muestra el portal de Onway (hora de recepción), en UTC.</summary>
        public DateTime HoraUtc { get; set; }
        public OnwayHistoryPoint Punto { get; set; }

        /// <summary>Cómo se detectó, para que la administradora sepa de dónde salió la hora.</summary>
        public string Fuente { get; set; }
    }

    /// <summary>
    /// Tramo de carga de un tracto: sale de base, llega a Trujillo, entra y sale de la planta de
    /// Vitapro, y vuelve a base (o pasa directo). Cada hito es null si el GPS no lo muestra.
    /// </summary>
    public class ResultadoTramoCarga
    {
        public HitoGps SalidaBase { get; set; }
        public HitoGps LlegadaTrujillo { get; set; }
        public HitoGps IngresoPlanta { get; set; }
        public HitoGps SalidaPlanta { get; set; }
        public HitoGps LlegadaBase { get; set; }

        /// <summary>Salida de la base después del regreso, si el mismo tracto vuelve a salir.</summary>
        public HitoGps SalidaBaseRetorno { get; set; }

        /// <summary>No entró a la base al volver: momento en que pasó más cerca de ella.</summary>
        public HitoGps PasoDirectoBase { get; set; }
    }

    /// <summary>
    /// Detección del tramo de carga en el historial GPS del tracto, leyendo el recorrido como una
    /// secuencia de visitas a geocercas y no columna por columna:
    ///   1. Se ancla en lo más inconfundible: la primera estadía de 10+ min dentro de la planta
    ///      desde <c>desdeUtc</c>. Primer punto dentro = ingreso; primer punto afuera = salida.
    ///   2. La llegada a Trujillo es el inicio de la visita al acceso (avenida y cola) que
    ///      contiene ese ingreso: la espera en la cola nunca se confunde con el ingreso.
    ///   3. La salida de base es la última salida de la base ANTES de llegar a Trujillo, así
    ///      que los movimientos de rutina de días previos (grifo, taller) no cuentan.
    ///   4. Después de la planta: primera entrada a la base, o paso directo si sigue al norte
    ///      sin entrar.
    /// Lógica pura, sin BD ni HTTP: se prueba con historiales reales guardados.
    /// </summary>
    public static class TrazabilidadCargaService
    {
        /// <summary>Estadía mínima en la planta: menos que esto es un paso, no una carga.</summary>
        public const int MinutosMinimosEnPlanta = 10;

        /// <summary>Un tramo afuera más corto que esto, entre dos puntos adentro, es ruido del GPS.</summary>
        public const int MinutosToleranciaFuera = 3;

        /// <summary>
        /// Una estadía solo se corta cuando la unidad está a más de esto del borde: el GPS de una
        /// unidad estacionada junto a la cerca puede marcarla unos metros afuera por mucho rato.
        /// </summary>
        public const double MetrosAlejamiento = 25;

        /// <summary>Ventana para cruzar una alerta de geocerca de Onway con la detección del polígono.</summary>
        private const int MinutosCruceAlertaOnway = 15;

        private const int DiasMaximosEntreHitos = 3;

        public static ResultadoTramoCarga DetectarTramoCarga(IEnumerable<OnwayHistoryPoint> historial, DateTime desdeUtc)
        {
            var puntos = Ordenar(historial);
            var resultado = new ResultadoTramoCarga();

            var planta = GeocercasTrazabilidad.TrujilloPlanta;
            var visitaPlanta = Visitas(puntos, planta)
                .FirstOrDefault(v => Hora(puntos[v.Primero]) >= desdeUtc &&
                                     (Hora(puntos[v.Ultimo]) - Hora(puntos[v.Primero])).TotalMinutes >= MinutosMinimosEnPlanta &&
                                     SeMovioAdentro(puntos, v));
            if (visitaPlanta == null)
            {
                // Todavía no llega a la planta: si ya salió de base y va por la Panamericana hacia el
                // sur (pasado Piura), el viaje está "rumbo a Trujillo" (caso CBU-945 del 20/10/2026).
                resultado.SalidaBase = SalidaEnCamino(puntos, desdeUtc.AddDays(-1), RumboATrujillo)
                                       ?? PasoFrenteALaBase(puntos, desdeUtc.AddDays(-1), puntos.Count);

                // Ya está en la cola de la Av. Gonzales Prada esperando entrar: llegó a Trujillo. Antes
                // la llegada esperaba al ingreso a la planta y la unidad seguía "rumbo a Trujillo" horas
                // después de llegar (CBV-788, CBV-761, BNI-736, BNH-735 y BSC-900 el 21/10/2026).
                var enCola = VisitaEnCola(puntos, resultado.SalidaBase?.HoraUtc ?? desdeUtc);
                if (enCola != null)
                {
                    resultado.LlegadaTrujillo = Hito(puntos[enCola.Primero], "Primer punto en la Av. Gonzales Prada (en cola, todavía sin entrar a la planta)");
                    resultado.SalidaBase = SalidaBaseAntesDeTrujillo(puntos, enCola.Primero, desdeUtc) ?? resultado.SalidaBase;
                }
                return resultado;
            }

            resultado.IngresoPlanta = Hito(puntos[visitaPlanta.Primero], "Primer punto dentro de la planta");
            if (visitaPlanta.Cerrada)
                resultado.SalidaPlanta = Hito(puntos[visitaPlanta.Ultimo + 1], "Primer punto fuera de la planta");

            // 2. Llegada a Trujillo: inicio de la visita al acceso que contiene el ingreso.
            var visitaAcceso = Visitas(puntos, GeocercasTrazabilidad.TrujilloAcceso)
                .FirstOrDefault(v => v.Primero <= visitaPlanta.Primero && visitaPlanta.Primero <= v.Ultimo);
            int idxLlegada = visitaAcceso?.Primero ?? visitaPlanta.Primero;
            resultado.LlegadaTrujillo = Hito(puntos[idxLlegada], "Primer punto en la Av. Gonzales Prada (acceso a planta)");

            // 3. Salida de base: última salida antes de llegar a Trujillo.
            resultado.SalidaBase = SalidaBaseAntesDeTrujillo(puntos, idxLlegada, desdeUtc);

            // 4. Regreso: primera entrada a la base o paso directo.
            if (visitaPlanta.Cerrada)
            {
                BuscarRegresoBase(puntos, visitaPlanta.Ultimo + 1, out var llegada, out var salidaRetorno, out var pasoDirecto);
                resultado.LlegadaBase = llegada;
                resultado.SalidaBaseRetorno = salidaRetorno;
                resultado.PasoDirectoBase = pasoDirecto;
            }

            return resultado;
        }

        /// <summary>
        /// Salida de base del viaje que llegó a Trujillo en <paramref name="idxLlegada"/>: la última
        /// salida antes de esa llegada. Si entre esa salida y Trujillo el tracto estuvo en Ecuador,
        /// vino directo de su descarga sin pasar por la base (vuelta corrida) y esa salida es del
        /// viaje anterior: se toma el paso frente a la base.
        /// </summary>
        private static HitoGps SalidaBaseAntesDeTrujillo(List<OnwayHistoryPoint> puntos, int idxLlegada, DateTime desdeUtc)
        {
            var salida = BuscarSalidaBaseAntesDe(puntos, idxLlegada);
            if (salida != null && puntos.Take(idxLlegada).Any(p => Hora(p) >= salida.HoraUtc && EnEcuador(p)))
                salida = null;
            return salida ?? PasoFrenteALaBase(puntos, desdeUtc.AddDays(-1), idxLlegada);
        }

        /// <summary>
        /// Estadía en el acceso a la planta (avenida y cola) desde <paramref name="desdeUtc"/>, sin
        /// haber entrado todavía: de <see cref="MinutosMinimosEnPlanta"/> o más, o en curso (el
        /// historial termina con la unidad ahí). Un paso corto por la avenida no cuenta.
        /// </summary>
        internal static Visita VisitaEnCola(List<OnwayHistoryPoint> puntos, DateTime desdeUtc) =>
            Visitas(puntos, GeocercasTrazabilidad.TrujilloAcceso)
                .FirstOrDefault(v => Hora(puntos[v.Primero]) >= desdeUtc &&
                                     (!v.Cerrada || (Hora(puntos[v.Ultimo]) - Hora(puntos[v.Primero])).TotalMinutes >= MinutosMinimosEnPlanta));

        /// <summary>Puntos con coordenadas, ordenados por la hora del portal.</summary>
        internal static List<OnwayHistoryPoint> Ordenar(IEnumerable<OnwayHistoryPoint> historial) =>
            (historial ?? Enumerable.Empty<OnwayHistoryPoint>())
                .Where(p => p != null && !(p.Lat == 0 && p.Lng == 0))
                .OrderBy(Hora)
                .ToList();

        /// <summary>Hora del punto como la muestra el portal (recepción); el envío, si no la hay.</summary>
        public static DateTime Hora(OnwayHistoryPoint p) => p.GenerateTime ?? p.MessageTime;

        internal static HitoGps Hito(OnwayHistoryPoint p, string fuente) =>
            new HitoGps { HoraUtc = Hora(p), Punto = p, Fuente = fuente };

        internal class Visita
        {
            public int Primero;
            public int Ultimo;

            /// <summary>False si el historial termina con la unidad todavía adentro.</summary>
            public bool Cerrada;
        }

        /// <summary>
        /// Estadías dentro de la geocerca. Una estadía se corta recién cuando la unidad lleva más de
        /// <see cref="MinutosToleranciaFuera"/> afuera y ya está a más de <see cref="MetrosAlejamiento"/>
        /// del borde; la salida es el primer punto afuera después del último adentro.
        /// </summary>
        internal static List<Visita> Visitas(List<OnwayHistoryPoint> puntos, GeocercaGps zona)
        {
            var visitas = new List<Visita>();
            Visita actual = null;
            for (int i = 0; i < puntos.Count; i++)
            {
                var p = puntos[i];
                if (actual == null)
                {
                    if (zona.Contiene(p.Lat, p.Lng))
                        actual = new Visita { Primero = i, Ultimo = i };
                    continue;
                }

                if (zona.Contiene(p.Lat, p.Lng))
                {
                    actual.Ultimo = i;
                }
                else if ((Hora(p) - Hora(puntos[actual.Ultimo])).TotalMinutes > MinutosToleranciaFuera &&
                         zona.DistanciaAlBordeMetros(p.Lat, p.Lng) > MetrosAlejamiento)
                {
                    actual.Cerrada = true;
                    visitas.Add(actual);
                    actual = null;
                    if (zona.Contiene(p.Lat, p.Lng))
                        actual = new Visita { Primero = i, Ultimo = i };
                }
            }

            if (actual != null)
            {
                // Puntos afuera al final, aunque sin cumplir la tolerancia de tiempo, ya muestran la
                // salida si la unidad se alejó del borde. Estacionada junto a la cerca sigue adentro
                // (ASU-862 del 20/10/2026, parado en la puerta de la planta: se marcaba la salida).
                actual.Cerrada = puntos.Skip(actual.Ultimo + 1)
                    .Any(p => zona.DistanciaAlBordeMetros(p.Lat, p.Lng) > MetrosAlejamiento);
                visitas.Add(actual);
            }
            return visitas;
        }

        /// <summary>
        /// Cargar implica maniobrar dentro de la planta. Una "estadía" en la que la unidad nunca se
        /// movió es un salto del GPS de una unidad detenida afuera (caso BFI-890 del 28/09: 30 min
        /// clavado en una coordenada dentro de la planta mientras esperaba en la cola).
        /// </summary>
        internal static bool SeMovioAdentro(List<OnwayHistoryPoint> puntos, Visita v)
        {
            const double VelocidadMovimientoKmh = 3, MetrosRecorridos = 15;
            var primero = puntos[v.Primero];
            for (int i = v.Primero; i <= v.Ultimo; i++)
            {
                var p = puntos[i];
                if (p.Speed >= VelocidadMovimientoKmh ||
                    CheckpointMatchingService.CalcularDistanciaMetros(p.Lat, p.Lng, primero.Lat, primero.Lng) >= MetrosRecorridos)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Rumbo a Trujillo: al sur de Piura por la Panamericana (lat. -5.40, ~50 km al sur de la base).
        /// Más cerca puede ser una vuelta de rutina a Piura (taller, grifo).
        /// </summary>
        internal static bool RumboATrujillo(OnwayHistoryPoint p) => p.Lat < -5.40;

        /// <summary>
        /// Rumbo a Ecuador: la Panamericana hacia Talara, ~10 km al norte de la base (pasando
        /// Marcavelica/Mallares; la base está en lat. -4.956).
        /// </summary>
        internal static bool RumboAEcuador(OnwayHistoryPoint p) => p.Lat > -4.87 && p.Lng < -80.0;

        /// <summary>Al norte de Tumbes (lat. -3.6): pasando la frontera, en Ecuador.</summary>
        internal static bool EnEcuador(OnwayHistoryPoint p) => p.Lat > -3.45;

        /// <summary>
        /// Salida de base de un tramo que todavía no llega a su primera parada (planta de Trujillo o
        /// Complex): la última salida de la base después de <paramref name="desdeUtc"/>, siempre que
        /// la unidad no haya vuelto a entrar y ya esté en la ruta del tramo (<paramref name="enRuta"/>).
        /// Sin esto el viaje seguía "en base" hasta llegar a la primera parada, horas después (casos
        /// BFH-852 y otras 6 descargas del 20/10/2026). Es provisional: cuando aparece la primera
        /// parada se toma la salida de antes de esa parada, que en un viaje normal es la misma.
        /// </summary>
        internal static HitoGps SalidaEnCamino(List<OnwayHistoryPoint> puntos, DateTime desdeUtc, Func<OnwayHistoryPoint, bool> enRuta)
        {
            var baseSullana = GeocercasTrazabilidad.BaseSullana;
            var ultima = Visitas(puntos, baseSullana).LastOrDefault();
            if (ultima == null || !ultima.Cerrada) return null; // sigue en la base (o no pasó por ella)
            int idxSalida = ultima.Ultimo + 1;
            if (Hora(puntos[idxSalida]) < desdeUtc) return null;
            if (!puntos.Skip(idxSalida).Any(enRuta)) return null; // salió, pero todavía no está en la ruta
            var porPoligono = Hito(puntos[idxSalida], "Primer punto fuera de la base (en camino, sin otra parada todavía)");
            return PreferirAlertaOnway(puntos, porPoligono, baseSullana, "salió de", ultima.Primero, puntos.Count) ?? porPoligono;
        }

        /// <summary>
        /// Vuelta corrida: el tracto viene de descargar en Ecuador y sigue de frente a Trujillo sin
        /// entrar a la base (TBM-815 y ASU-862 el 20/10/2026). Su salida de base es el momento en
        /// que pasó frente a ella, a 2 km o menos, antes de seguir al sur de Piura; es la misma hora
        /// que cierra su descarga anterior (<see cref="TrazabilidadDescargaService.SiguioATrujilloSinEntrar"/>).
        /// Si entra a la base no aplica: esa salida la encuentra la regla normal.
        /// </summary>
        internal static HitoGps PasoFrenteALaBase(List<OnwayHistoryPoint> puntos, DateTime desdeUtc, int hasta)
        {
            const double MetrosCerca = 2000;
            var baseSullana = GeocercasTrazabilidad.BaseSullana;
            int idxCerca = -1;
            double metrosMinimos = double.MaxValue;
            bool alSur = false;
            bool recienSalido = false;
            HitoGps paso = null;
            for (int i = 0; i < hasta && i < puntos.Count; i++)
            {
                var p = puntos[i];
                if (Hora(p) < desdeUtc) continue;
                if (baseSullana.Contiene(p.Lat, p.Lng))
                {
                    idxCerca = -1; // entró: no es una vuelta corrida
                    metrosMinimos = double.MaxValue;
                    recienSalido = true;
                    alSur = false;
                    continue;
                }
                if (RumboATrujillo(p))
                {
                    if (!alSur && idxCerca >= 0 && metrosMinimos <= MetrosCerca)
                        paso = Hito(puntos[idxCerca], $"No entró a la base: pasó frente a ella rumbo a Trujillo (a {metrosMinimos / 1000:0.0} km)");
                    alSur = true;
                    idxCerca = -1;
                    metrosMinimos = double.MaxValue;
                    continue;
                }
                alSur = false;
                double metros = CheckpointMatchingService.CalcularDistanciaMetros(
                    p.Lat, p.Lng, baseSullana.LatitudCentro, baseSullana.LongitudCentro);
                // Al salir de la base la unidad también pasa cerca de ella: eso es una salida, no un paso.
                if (recienSalido)
                {
                    if (metros > MetrosCerca) recienSalido = false;
                    continue;
                }
                if (metros < metrosMinimos)
                {
                    metrosMinimos = metros;
                    idxCerca = i;
                }
            }
            return paso; // el último paso rumbo al sur antes de hasta
        }

        internal static HitoGps BuscarSalidaBaseAntesDe(List<OnwayHistoryPoint> puntos, int idxLlegada)
        {
            var baseSullana = GeocercasTrazabilidad.BaseSullana;
            DateTime limite = Hora(puntos[idxLlegada]).AddDays(-DiasMaximosEntreHitos);
            for (int i = idxLlegada - 1; i >= 0 && Hora(puntos[i]) >= limite; i--)
            {
                if (!baseSullana.Contiene(puntos[i].Lat, puntos[i].Lng)) continue;
                var porPoligono = Hito(puntos[i + 1], "Primer punto fuera de la base");
                return PreferirAlertaOnway(puntos, porPoligono, baseSullana, "salió de", 0, idxLlegada) ?? porPoligono;
            }
            return null;
        }

        /// <summary>
        /// Regreso a la base desde <paramref name="idxSalidaPlanta"/>: llegada (o paso directo si pasa
        /// cerca sin entrar y se aleja) y, si vuelve a salir, esa salida.
        /// </summary>
        internal static void BuscarRegresoBase(List<OnwayHistoryPoint> puntos, int idxSalidaPlanta,
            out HitoGps llegada, out HitoGps salidaRetorno, out HitoGps pasoDirecto, int diasMaximos = DiasMaximosEntreHitos)
        {
            llegada = salidaRetorno = pasoDirecto = null;
            var baseSullana = GeocercasTrazabilidad.BaseSullana;
            DateTime limite = Hora(puntos[idxSalidaPlanta]).AddDays(diasMaximos);
            const double MetrosCerca = 2000, MetrosPasoDeLargo = 5000;

            int idxMasCerca = -1;
            double distanciaMinima = double.MaxValue;
            for (int i = idxSalidaPlanta; i < puntos.Count && Hora(puntos[i]) <= limite; i++)
            {
                var p = puntos[i];
                if (baseSullana.Contiene(p.Lat, p.Lng))
                {
                    // La llegada es cuando se detiene en la entrada, no cuando por fin entra: se
                    // retrocede mientras la unidad siga junto a la entrada antes de entrar.
                    int idxLlegada = i;
                    while (idxLlegada - 1 > idxSalidaPlanta && CheckpointMatchingService.CalcularDistanciaMetros(
                               puntos[idxLlegada - 1].Lat, puntos[idxLlegada - 1].Lng,
                               GeocercasTrazabilidad.BaseSullanaEntradaLat, GeocercasTrazabilidad.BaseSullanaEntradaLng)
                           <= GeocercasTrazabilidad.BaseSullanaEntradaRadioMetros)
                        idxLlegada--;
                    llegada = idxLlegada < i
                        ? Hito(puntos[idxLlegada], "Se detuvo en la entrada de la base y luego entró")
                        : PreferirAlertaOnway(puntos, Hito(p, "Primer punto dentro de la base"), baseSullana, "llegó a", idxSalidaPlanta, puntos.Count)
                          ?? Hito(p, "Primer punto dentro de la base");

                    var visita = Visitas(puntos.Skip(i).ToList(), baseSullana).FirstOrDefault();
                    if (visita != null && visita.Cerrada)
                    {
                        var salida = Hito(puntos[i + visita.Ultimo + 1], "Primer punto fuera de la base");
                        salidaRetorno = PreferirAlertaOnway(puntos, salida, baseSullana, "salió de", i, puntos.Count) ?? salida;
                    }
                    return;
                }

                double distancia = CheckpointMatchingService.CalcularDistanciaMetros(
                    p.Lat, p.Lng, baseSullana.LatitudCentro, baseSullana.LongitudCentro);
                if (distancia < distanciaMinima)
                {
                    distanciaMinima = distancia;
                    idxMasCerca = i;
                }

                // Ya pasó cerca de la base y se alejó sin entrar (al norte si viene de Trujillo, al sur
                // si vuelve de Ecuador): pasó directo.
                if (distanciaMinima <= MetrosCerca && distancia >= MetrosPasoDeLargo)
                {
                    pasoDirecto = Hito(puntos[idxMasCerca], "Pasó frente a la base sin entrar");
                    return;
                }
            }
        }

        /// <summary>
        /// Si Onway registró la alerta de su propia geocerca ("El dispositivo X salió de Base
        /// Viviana Sullana") cerca de lo que detectó el polígono, se usa esa hora: es la que ve la
        /// administradora en el portal.
        /// </summary>
        private static HitoGps PreferirAlertaOnway(List<OnwayHistoryPoint> puntos, HitoGps porPoligono,
            GeocercaGps zona, string verbo, int desdeIdx, int hastaIdx)
        {
            if (string.IsNullOrEmpty(zona.NombreOnway)) return null;
            string texto = verbo + " " + zona.NombreOnway;
            var alerta = puntos
                .Skip(desdeIdx).Take(hastaIdx - desdeIdx)
                .Where(p => p.Body?.Es != null && p.Body.Es.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(p => Math.Abs((Hora(p) - porPoligono.HoraUtc).TotalMinutes) <= MinutosCruceAlertaOnway)
                .OrderBy(p => Math.Abs((Hora(p) - porPoligono.HoraUtc).TotalSeconds))
                .FirstOrDefault();
            return alerta == null ? null : Hito(alerta, "Alerta de geocerca de Onway (" + zona.NombreOnway + ")");
        }
    }
}
