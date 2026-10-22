using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Hora aproximada de llegada de una unidad en camino (a Trujillo a cargar, o a Jave/Inbalnor a
    /// descargar) desde su última posición del GPS. No es distancia ÷ velocidad: sale de lo que tardaron
    /// de verdad los viajes anteriores desde ese mismo punto del camino y a esa hora del día, con las
    /// paradas de siempre (descanso en Mórrope, la noche en Pacasmayo, el parqueo de Naranjal). Los
    /// perfiles los arma herramientas/perfil-llegada con el historial de Onway (PerfilesLlegada.json).
    /// Lógica pura: se prueba sin BD ni GPS.
    /// </summary>
    public static class LlegadaEstimada
    {
        public const string CargaTrujillo = "CARGA_TRUJILLO";

        /// <summary>De vuelta de Ecuador: de la salida de Jave o Inbalnor a la llegada a la base.</summary>
        public const string RetornoBase = "RETORNO_BASE";
        public const string DestinoBase = "BASE SULLANA";

        /// <summary>Una posición más lejos que esto del camino no se estima (otro camino, taller, desvío).</summary>
        public const double MaximoFueraDeRutaKm = 8;

        /// <summary>Muestras del mismo tramo (±km) que se comparan.</summary>
        private const double VentanaKm = 5;

        /// <summary>Con menos viajes que esto en la misma franja horaria se amplía la franja.</summary>
        private const int MuestrasMinimas = 8;

        private static readonly double[] FranjasHoras = { 1.5, 3, 6, 12 };

        /// <summary>Un camino (polilínea cada 1 km) y lo que tardaron los viajes desde cada punto.</summary>
        public sealed class Ruta
        {
            public string Codigo { get; set; }
            public string Destino { get; set; }
            public double LargoKm { get; set; }
            public int Viajes { get; set; }
            /// <summary>[lat, lng] cada 1 km desde el inicio del tramo.</summary>
            public List<double[]> Polilinea { get; set; } = new List<double[]>();
            /// <summary>[km, hora del día (0–24), minutos que faltaban para llegar], una por viaje cada 5 km.</summary>
            public List<double[]> Muestras { get; set; } = new List<double[]>();

            public Proyeccion Proyectar(double lat, double lng)
            {
                int mejor = 0; double dMin = double.MaxValue;
                for (int i = 0; i < Polilinea.Count; i++)
                {
                    double d = DistanciaKm(lat, lng, Polilinea[i][0], Polilinea[i][1]);
                    if (d < dMin) { dMin = d; mejor = i; }
                }
                return new Proyeccion { Km = mejor, DistanciaKm = dMin };
            }
        }

        public struct Proyeccion
        {
            public double Km;
            public double DistanciaKm;
        }

        public sealed class Estimado
        {
            public string Destino { get; set; }
            public double KmRestantes { get; set; }
            /// <summary>Mediana de los viajes comparables; Desde/Hasta: la mitad de ellos llegó entre esas horas.</summary>
            public DateTime Llegada { get; set; }
            public DateTime Desde { get; set; }
            public DateTime Hasta { get; set; }
            public int Muestras { get; set; }
            public DateTime HoraPosicion { get; set; }

            /// <summary>"23/10 14:30 (13:50–16:10)"; el día del rango solo si cambia.</summary>
            public string Texto
            {
                get
                {
                    string Hora(DateTime t) => t.Date == Llegada.Date ? t.ToString("HH:mm", CultureInfo.InvariantCulture)
                                                                      : t.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
                    return $"{Llegada.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)} ({Hora(Desde)}–{Hora(Hasta)})";
                }
            }
        }

        private static readonly Lazy<Dictionary<string, Ruta>> perfiles = new Lazy<Dictionary<string, Ruta>>(CargarPerfiles);

        public static Ruta Perfil(string codigo) =>
            codigo != null && perfiles.Value.TryGetValue(codigo, out var r) ? r : null;

        public static string RutaDescarga(string planta) =>
            string.IsNullOrWhiteSpace(planta) ? null : "DESCARGA_" + planta.Trim().ToUpperInvariant();

        /// <summary>Llegada estimada desde una posición, o null si no hay perfil o la unidad está fuera del camino.</summary>
        public static Estimado Estimar(string codigoRuta, double lat, double lng, DateTime horaPosicion) =>
            Estimar(Perfil(codigoRuta), lat, lng, horaPosicion);

        public static Estimado Estimar(Ruta ruta, double lat, double lng, DateTime horaPosicion)
        {
            if (ruta == null || ruta.Polilinea.Count < 2 || ruta.Muestras.Count == 0) return null;
            var p = ruta.Proyectar(lat, lng);
            if (p.DistanciaKm > MaximoFueraDeRutaKm) return null;

            double hora = horaPosicion.TimeOfDay.TotalHours;
            var cerca = ruta.Muestras.Where(m => Math.Abs(m[0] - p.Km) <= VentanaKm).ToList();
            List<double> minutos = null;
            foreach (double franja in FranjasHoras)
            {
                minutos = cerca.Where(m => DiferenciaHoras(m[1], hora) <= franja).Select(m => m[2]).ToList();
                if (minutos.Count >= MuestrasMinimas) break;
            }
            if (minutos == null || minutos.Count == 0) return null;
            minutos.Sort();

            return new Estimado
            {
                Destino = ruta.Destino,
                KmRestantes = Math.Max(0, ruta.LargoKm - p.Km),
                Llegada = Redondear(horaPosicion.AddMinutes(Percentil(minutos, 0.5))),
                Desde = Redondear(horaPosicion.AddMinutes(Percentil(minutos, 0.25))),
                Hasta = Redondear(horaPosicion.AddMinutes(Percentil(minutos, 0.75))),
                Muestras = minutos.Count,
                HoraPosicion = horaPosicion,
            };
        }

        /// <summary>
        /// Llegada estimada de un viaje de Vitapro que va en camino a la planta: a Trujillo (salió de base y
        /// no llegó) o a Jave/Inbalnor (ya salió de TCI, o cruzó sin pasar por TCI, y no llegó). La posición
        /// tiene que ser del tracto que va a esa planta. Null en cualquier otro caso: en base, en cola, en
        /// el Complex o en TCI esperando (eso depende de la aduana, no del camino).
        /// </summary>
        public static Estimado ParaViaje(IDictionary<string, DateTime?> h, string estado, string planta, string placaGps,
            string tracto1, string tracto2, double? lat, double? lng, DateTime? horaPosicion)
        {
            bool Hay(params string[] columnas) => columnas.Any(c => h.TryGetValue(c, out var v) && v.HasValue);
            string e = (estado ?? "").Trim().ToUpperInvariant();
            if (e == "FINALIZADO" || e == "CANCELADO" || !lat.HasValue || !lng.HasValue || !horaPosicion.HasValue) return null;

            string ruta = null;
            if (Hay("fhSalidaBase1") && !Hay("fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta") && MismaPlaca(placaGps, tracto1))
                ruta = CargaTrujillo;
            else if ((Hay("fhSalidaTCI") || (Hay("fhCruceEcuador") && !Hay("fhLlegadaTCI")))
                     && !Hay("fhLlegadaPlantaEcuador", "fhLlegadaAlmacen", "fhIngreso", "fhSalida") && MismaPlaca(placaGps, tracto2))
                ruta = RutaDescarga(planta);
            else if (Hay("fhSalida") && !Hay("fhLlegadaBaseFinal") && MismaPlaca(placaGps, tracto2))
                ruta = RetornoBase;
            return ruta == null ? null : Estimar(ruta, lat.Value, lng.Value, horaPosicion.Value);
        }

        private static bool MismaPlaca(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b)
            && string.Equals(a.Replace("-", "").Trim(), b.Replace("-", "").Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Texto para enviar ("me piden el estado de las unidades que aún no llegan"): las unidades en camino
        /// a cada planta, la que llega primero arriba, con dónde están, cuánto les falta y la hora aproximada.
        /// </summary>
        public static string Reporte(IEnumerable<ViajeResumen> viajes, DateTime ahora)
        {
            // El retorno a la base va en el cuadro del grifo Santa Rosa, no aquí.
            var enCamino = viajes.Where(v => v.Llegada != null && v.Llegada.Destino != DestinoBase).ToList();
            if (enCamino.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"UNIDADES EN CAMINO A PLANTA - {ahora.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)}");
            sb.AppendLine("Llegada aproximada según el GPS y lo que tardaron los viajes anteriores desde el mismo punto y a la misma hora.");
            foreach (var g in enCamino.GroupBy(v => v.Llegada.Destino).OrderBy(g => g.Key == "TRUJILLO" ? 0 : 1).ThenBy(g => g.Key))
            {
                sb.AppendLine();
                sb.AppendLine(g.Key == "TRUJILLO" ? "CARGA EN TRUJILLO" : "DESCARGA EN " + g.Key);
                foreach (var v in g.OrderBy(x => x.Llegada.Llegada))
                {
                    bool carga = g.Key == "TRUJILLO";
                    string tracto = carga ? v.Tracto1 : v.Tracto2 ?? v.Tracto1;
                    string chofer = carga ? v.Chofer1 : v.Chofer2 ?? v.Chofer1;
                    var l = v.Llegada;
                    string senal = ahora - l.HoraPosicion > TimeSpan.FromHours(2)
                        ? $"última señal {l.HoraPosicion.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)}"
                        : (v.Velocidad.HasValue && v.Velocidad.Value < 3 ? "detenida" : "en marcha") + " a las " + l.HoraPosicion.ToString("HH:mm", CultureInfo.InvariantCulture);
                    string atraso = l.Llegada < ahora ? " (ya debería haber llegado)" : "";
                    sb.AppendLine($"- {tracto} / {v.Carreta} - {chofer}: {(string.IsNullOrWhiteSpace(v.Lugar) ? "en ruta" : v.Lugar)}, " +
                                  $"faltan {l.KmRestantes:0} km ({senal}). Llega aprox. {l.Texto}{atraso}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Diferencia entre dos horas del día, dando la vuelta a medianoche (23:00 y 01:00 = 2 h).</summary>
        public static double DiferenciaHoras(double a, double b)
        {
            double d = Math.Abs(a - b) % 24;
            return Math.Min(d, 24 - d);
        }

        public static double Percentil(List<double> ordenados, double p)
        {
            double pos = (ordenados.Count - 1) * p;
            int i = (int)Math.Floor(pos);
            return i + 1 < ordenados.Count ? ordenados[i] + (ordenados[i + 1] - ordenados[i]) * (pos - i) : ordenados[i];
        }

        /// <summary>A los 10 minutos: no es una hora exacta.</summary>
        private static DateTime Redondear(DateTime t)
        {
            long diez = TimeSpan.FromMinutes(10).Ticks;
            return new DateTime((t.Ticks + diez / 2) / diez * diez, t.Kind);
        }

        public static double DistanciaKm(double lat1, double lng1, double lat2, double lng2)
        {
            double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
            double x = dLng * Math.Cos((lat1 + lat2) / 2 * Math.PI / 180);
            return Math.Sqrt(x * x + dLat * dLat) * 6371;
        }

        public static Dictionary<string, Ruta> LeerPerfiles(string json)
        {
            var archivo = JsonConvert.DeserializeAnonymousType(json, new { rutas = new List<Ruta>() });
            return (archivo?.rutas ?? new List<Ruta>()).ToDictionary(r => r.Codigo, StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, Ruta> CargarPerfiles()
        {
            var asm = Assembly.GetExecutingAssembly();
            string nombre = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("PerfilesLlegada.json", StringComparison.OrdinalIgnoreCase));
            if (nombre == null) return new Dictionary<string, Ruta>(StringComparer.OrdinalIgnoreCase);
            using (var s = asm.GetManifestResourceStream(nombre))
            using (var r = new StreamReader(s))
                return LeerPerfiles(r.ReadToEnd());
        }
    }
}
