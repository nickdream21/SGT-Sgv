using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Zona del mapa como polígono (no como círculo): un círculo no separa la cola de la avenida
    /// de la planta que está a 150 m, un polígono dibujado sobre el terreno sí.
    /// </summary>
    public class GeocercaGps
    {
        public string Codigo { get; }
        public string Nombre { get; }

        /// <summary>
        /// Nombre de la geocerca equivalente en el portal de Onway, si existe. Sus alertas traen
        /// el texto "El dispositivo X llegó a / salió de {NombreOnway}" y se prefieren al polígono.
        /// </summary>
        public string NombreOnway { get; }

        /// <summary>Vértices en orden (latitud, longitud); el polígono se cierra solo.</summary>
        public IReadOnlyList<Tuple<double, double>> Vertices { get; }

        public GeocercaGps(string codigo, string nombre, string nombreOnway, params double[] latLng)
        {
            if (latLng.Length < 6 || latLng.Length % 2 != 0)
                throw new ArgumentException("Un polígono necesita al menos 3 vértices (lat, lng).", nameof(latLng));
            Codigo = codigo;
            Nombre = nombre;
            NombreOnway = nombreOnway;
            Vertices = Enumerable.Range(0, latLng.Length / 2)
                .Select(i => Tuple.Create(latLng[2 * i], latLng[2 * i + 1]))
                .ToList();
        }

        /// <summary>Círculo como polígono de 24 lados, para zonas que todavía no se dibujaron sobre el terreno.</summary>
        public static GeocercaGps Circulo(string codigo, string nombre, double lat, double lng, double radioMetros)
        {
            const int Lados = 24;
            double dLat = radioMetros / 111320.0, dLng = dLat / Math.Cos(lat * Math.PI / 180.0);
            var v = new double[Lados * 2];
            for (int i = 0; i < Lados; i++)
            {
                double a = 2 * Math.PI * i / Lados;
                v[2 * i] = lat + dLat * Math.Sin(a);
                v[2 * i + 1] = lng + dLng * Math.Cos(a);
            }
            return new GeocercaGps(codigo, nombre, null, v);
        }

        /// <summary>Punto dentro del polígono (ray casting; a estas escalas lat/lng se tratan como planos).</summary>
        public bool Contiene(double lat, double lng)
        {
            bool dentro = false;
            for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
            {
                double yi = Vertices[i].Item1, xi = Vertices[i].Item2;
                double yj = Vertices[j].Item1, xj = Vertices[j].Item2;
                if ((yi > lat) != (yj > lat) && lng < (xj - xi) * (lat - yi) / (yj - yi) + xi)
                    dentro = !dentro;
            }
            return dentro;
        }

        /// <summary>Distancia en metros del punto al lado más cercano del polígono.</summary>
        public double DistanciaAlBordeMetros(double lat, double lng)
        {
            // Proyección equirectangular local alrededor del punto: error despreciable a cientos de metros.
            const double MetrosPorGrado = 111320.0;
            double cosLat = Math.Cos(lat * Math.PI / 180.0);
            double minimo = double.MaxValue;
            for (int i = 0, j = Vertices.Count - 1; i < Vertices.Count; j = i++)
            {
                double ax = (Vertices[j].Item2 - lng) * MetrosPorGrado * cosLat, ay = (Vertices[j].Item1 - lat) * MetrosPorGrado;
                double bx = (Vertices[i].Item2 - lng) * MetrosPorGrado * cosLat, by = (Vertices[i].Item1 - lat) * MetrosPorGrado;
                double dx = bx - ax, dy = by - ay;
                double largo2 = dx * dx + dy * dy;
                double t = largo2 == 0 ? 0 : Math.Max(0, Math.Min(1, -(ax * dx + ay * dy) / largo2));
                double px = ax + t * dx, py = ay + t * dy;
                minimo = Math.Min(minimo, Math.Sqrt(px * px + py * py));
            }
            return minimo;
        }

        public double LatitudCentro => Vertices.Average(v => v.Item1);
        public double LongitudCentro => Vertices.Average(v => v.Item2);
    }

    /// <summary>
    /// Geocercas del tramo de carga Base Sullana → Vitapro Trujillo → Base. Dibujadas con las
    /// vías y terrenos de OpenStreetMap y comprobadas con el historial real de ANU-794 del
    /// 18-19/10/2026 (WebSGV.Tests/Datos/Trazabilidad), que la administradora de transporte
    /// marcó punto por punto.
    /// </summary>
    public static class GeocercasTrazabilidad
    {
        /// <summary>
        /// Terreno de la base en Sullana. El borde este queda a ~12 m de la calzada de la
        /// Panamericana: una unidad que pasa de largo por la carretera no entra.
        /// </summary>
        public static readonly GeocercaGps BaseSullana = new GeocercaGps(
            "BASE_SULLANA", "Base Sullana", "Base Viviana Sullana",
            -4.95580, -80.69752,
            -4.95550, -80.70040,
            -4.95560, -80.70225,
            -4.95680, -80.70225,
            -4.95700, -80.69985,
            -4.95705, -80.69752);

        /// <summary>
        /// Entrada de la base, en el cruce de la Panamericana: aquí se detienen las unidades antes de
        /// entrar, a veces varios minutos con el motor apagado, y es la hora que se anota como llegada
        /// (casos AMQ-859 del 04/10 y BPT-788 del 07/10).
        /// </summary>
        public const double BaseSullanaEntradaLat = -4.95638, BaseSullanaEntradaLng = -80.69730;
        public const double BaseSullanaEntradaRadioMetros = 60;

        /// <summary>
        /// Llegada a Trujillo: el tramo de la Av. Gonzales Prada que da a la planta (±60 m del eje),
        /// desde ~400 m al sur del cruce con Calle Fundo Larrea —donde hacen cola y esperan de noche
        /// (caso AMQ-859 del 03/10)— hasta pasada la planta, más la planta misma. Por aquí entran las
        /// unidades que vienen por la vía de acceso diagonal.
        /// </summary>
        public static readonly GeocercaGps TrujilloAcceso = new GeocercaGps(
            "TRUJILLO_ACCESO", "Trujillo - acceso Vitapro (Av. Gonzales Prada)", null,
            -8.14050, -79.012750,
            -8.136853, -79.013572,
            -8.135610, -79.014024,
            -8.134464, -79.014425,
            -8.13300, -79.014913,
            -8.13300, -79.01150,
            -8.13420, -79.01050,
            -8.13560, -79.01050,
            -8.13750, -79.01220,
            -8.14050, -79.01165);

        /// <summary>
        /// Terreno de la planta Vitapro Trujillo: entre la Av. Gonzales Prada (oeste), la calle que
        /// sale al noreste desde la avenida y Calle Fundo Larrea (sur), con el patio del noreste donde
        /// algunas unidades esperan adentro (caso BNG-862 del 30/09). El borde oeste va ~8 m al este
        /// del eje de la avenida (el GPS de una unidad que sube por la avenida marca 6-10 m al este
        /// del eje) y el borde sur, desde la tranquera (lat -8.1356), deja fuera la cola del cruce con
        /// Calle Fundo Larrea (lat -8.1363 hacia el sur).
        /// </summary>
        public static readonly GeocercaGps TrujilloPlanta = new GeocercaGps(
            "TRUJILLO_PLANTA", "Planta Vitapro Trujillo", null,
            -8.13585, -79.013310,
            -8.135610, -79.013401,
            -8.135458, -79.013454,
            -8.135311, -79.013506,
            -8.134464, -79.013802,
            -8.133743, -79.012635,
            -8.133262, -79.011876,
            -8.13440, -79.01095,
            -8.13530, -79.01110,
            -8.13585, -79.01240);

        // ---- Tramo de descarga (Tracto 2): Base → Complex → CEBAF → cruce → TCI → Jave / Inbalnor ----
        // Dibujadas con OpenStreetMap y comprobadas con el historial de BFI-922 del 02-03/10/2026
        // (pedido 4400133136, descarga en Jave), que la administradora marcó punto por punto.

        /// <summary>
        /// Llegada a la bodega nacional (Complex, Aguas Verdes): la Av. Juan Velasco Alvarado frente al
        /// Complex, donde hacen cola, más el patio.
        /// </summary>
        public static readonly GeocercaGps ComplexAcceso = new GeocercaGps(
            "COMPLEX_ACCESO", "Complex (bodega nacional) - acceso", null,
            -3.48805, -80.26255,
            -3.48740, -80.26185,
            -3.48700, -80.26140,
            -3.48700, -80.26080,
            -3.48820, -80.25985,
            -3.48880, -80.26050,
            -3.48850, -80.26210);

        /// <summary>
        /// Patio del Complex: al noreste del muro que da a la cola, ~15 m antes del muro para tomar
        /// la puerta. Se sale por el noroeste.
        /// </summary>
        public static readonly GeocercaGps ComplexPatio = new GeocercaGps(
            "COMPLEX_PATIO", "Complex (bodega nacional) - patio", null,
            -3.488150, -80.26086,
            -3.487483, -80.261413,
            -3.48710, -80.26095,
            -3.48777, -80.26040);

        /// <summary>Terreno del CEBAF (OpenStreetMap). La Panamericana lo cruza: la llegada es el primer punto adentro.</summary>
        public static readonly GeocercaGps Cebaf = new GeocercaGps(
            "CEBAF", "CEBAF", null,
            -3.510423, -80.251737,
            -3.513346, -80.251447,
            -3.512170, -80.249817,
            -3.512027, -80.249618,
            -3.512327, -80.249039,
            -3.510990, -80.247907,
            -3.510688, -80.248288,
            -3.509994, -80.247789);

        /// <summary>
        /// Cruce a Ecuador: la Transversal Sur (E50) al este de la salida de la aduana ecuatoriana.
        /// El cruce es el primer punto en la E50 después de la aduana.
        /// </summary>
        public static readonly GeocercaGps Cruce = new GeocercaGps(
            "CRUCE_ECUADOR", "Cruce a Ecuador (E50)", null,
            -3.49640, -80.21470,
            -3.49640, -80.21200,
            -3.49710, -80.21200,
            -3.49710, -80.21470);

        /// <summary>
        /// TCI (Transcomerinter, Huaquillas): el terreno más el tramo de la Vía a Chacras frente a la
        /// puerta, donde esperan. La espera más al norte de la vía no cuenta como llegada.
        /// </summary>
        public static readonly GeocercaGps Tci = new GeocercaGps(
            "TCI", "TCI (Transcomerinter)", null,
            -3.53540, -80.18502,
            -3.53640, -80.18608,
            -3.536803, -80.185856,
            -3.537758, -80.184638,
            -3.536795, -80.183507,
            -3.535966, -80.184140,
            -3.53540, -80.18442);

        /// <summary>
        /// Llegada a Jave (F.H.LL.Planta / Almacén): la orilla de la Vía Durán–Yaguachi en la entrada de
        /// servicio, donde esperan, y las calles hasta las bodegas.
        /// </summary>
        public static readonly GeocercaGps JaveAcceso = new GeocercaGps(
            "JAVE_ACCESO", "Jave - acceso", null,
            -2.17190, -79.79745,
            -2.17460, -79.79990,
            -2.17880, -79.79850,
            -2.17920, -79.79560,
            -2.17580, -79.79420,
            -2.17300, -79.79650);

        /// <summary>Bodega Jave: el bloque de bodegas detrás de la puerta de (-2.1772, -79.7961).</summary>
        public static readonly GeocercaGps JavePlanta = new GeocercaGps(
            "JAVE_PLANTA", "Bodega Jave", null,
            -2.17700, -79.79595,
            -2.17655, -79.79668,
            -2.17785, -79.79835,
            -2.17870, -79.79760,
            -2.17775, -79.79595);

        /// <summary>
        /// Paso directo por TCI: cuando se coordina con TCI la unidad no entra y se toma el punto
        /// cercano, el control de la Aduana del Ecuador en la Transversal Sur, ~400 m al noreste de TCI
        /// (caso BSD-716 del 29/09). Las que sí entran a TCI también se detienen aquí después.
        /// </summary>
        public static readonly GeocercaGps TciControlAduana = new GeocercaGps(
            "TCI_CONTROL", "Control de la Aduana del Ecuador (paso directo por TCI)", null,
            -3.5318, -80.1790,
            -3.5318, -80.1770,
            -3.5330, -80.1770,
            -3.5330, -80.1790);

        /// <summary>
        /// Grifo Santa Rosa: estación de combustible de la Troncal de la Costa en El Retiro (Machala, camino
        /// a Santa Rosa; en OpenStreetMap "p&amp;s", -3.42855, -79.96242). Después de descargar, 111 de 112
        /// viajes del 15/09 al 20/10/2026 paran ahí a cargar combustible antes de volver a la base (mediana
        /// 4,6 h después de salir de la planta y 2,2 h en el grifo; a veces duermen). El círculo cubre el
        /// patio donde se estacionan; pasar de largo por la carretera dura menos de los 10 minutos que cuentan.
        /// </summary>
        public static readonly GeocercaGps GrifoSantaRosa =
            GeocercaGps.Circulo("GRIFO_SANTA_ROSA", "Grifo Santa Rosa (El Retiro, Machala)", -3.42855, -79.96280, 150);

        /// <summary>
        /// Llegada a Inbalnor (F.H.LL.Planta / Almacén): el camino de acceso desde la carretera y el
        /// terreno. Comprobada con el historial de BSD-716 del 30/09/2026.
        /// </summary>
        public static readonly GeocercaGps InbalnorAcceso = new GeocercaGps(
            "INBALNOR_ACCESO", "Inbalnor - acceso", null,
            -2.2125, -79.6370,
            -2.2125, -79.6324,
            -2.2165, -79.6324,
            -2.2165, -79.6370);

        /// <summary>
        /// Planta Inbalnor (en OpenStreetMap, "Nicovita SA") sin el camino de acceso del lado este,
        /// donde hacen cola hasta la puerta de (-2.21415, -79.63304); se sale por el sur.
        /// </summary>
        public static readonly GeocercaGps InbalnorPlanta = new GeocercaGps(
            "INBALNOR_PLANTA", "Planta Inbalnor", null,
            -2.215695, -79.633548,
            -2.214306, -79.632984,
            -2.214056, -79.633282,
            -2.21330, -79.632575,
            -2.212970, -79.632441,
            -2.210299, -79.633791,
            -2.209992, -79.634591,
            -2.212531, -79.635722,
            -2.214567, -79.636540,
            -2.214650, -79.636518);
    }
}
