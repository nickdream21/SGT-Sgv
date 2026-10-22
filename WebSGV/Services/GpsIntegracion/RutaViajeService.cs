using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Common;
using WebSGV.Services.Exportacion;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>Un hito sobre el mapa: dónde y cuándo pasó.</summary>
    public sealed class HitoRuta
    {
        public string Nombre { get; set; }
        public double Lat { get; set; }
        public double Lng { get; set; }
        public long T { get; set; }
        public string Hora { get; set; }

        /// <summary>true: el punto es el que guardó el GPS; false: se ubicó en el recorrido por la hora.</summary>
        public bool PuntoGps { get; set; }
    }

    /// <summary>El recorrido de un tracto en un tramo del viaje (carga o descarga).</summary>
    public sealed class RutaTramo
    {
        public string Clave { get; set; }
        public string Titulo { get; set; }
        public string Tracto { get; set; }
        public string Chofer { get; set; }

        /// <summary>[lat, lng, hora en ms epoch, velocidad km/h] ordenados por hora.</summary>
        public List<double[]> Puntos { get; set; } = new List<double[]>();
        public List<HitoRuta> Hitos { get; set; } = new List<HitoRuta>();
        public double Km { get; set; }
        public string Desde { get; set; }
        public string Hasta { get; set; }

        /// <summary>El tramo no tiene todavía su llegada a base: el recorrido llega hasta ahora.</summary>
        public bool EnCurso { get; set; }
        public string Aviso { get; set; }
    }

    public sealed class ZonaRuta
    {
        public string Nombre { get; set; }
        public List<double[]> Vertices { get; set; }
    }

    public sealed class RutaViaje
    {
        public List<RutaTramo> Tramos { get; set; } = new List<RutaTramo>();
        public List<ZonaRuta> Zonas { get; set; } = new List<ZonaRuta>();
    }

    /// <summary>
    /// Recorrido GPS de un viaje de exportación para el mapa del detalle: el tracto de carga entre
    /// su salida y su regreso a base, y el de descarga entre su salida y su regreso, con los hitos y
    /// las geocercas. El historial sale del API de Onway (1 día por llamada, 2 req/s), así que un
    /// tramo ya cerrado se guarda en ~/App_Data/RutasGps y no se vuelve a consultar; uno en curso se
    /// guarda 10 minutos en memoria.
    /// </summary>
    public static class RutaViajeService
    {
        /// <summary>Puntos máximos por tramo que se mandan al navegador.</summary>
        private const int MaximoPuntos = 2500;

        /// <summary>Un tramo nunca pide más de estos días de historial.</summary>
        private const int DiasMaximos = 6;

        /// <summary>Distancia mínima entre dos puntos guardados (el resto es la unidad parada).</summary>
        private const double MetrosMinimos = 40;

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static RutaViaje Obtener(int idSeguimiento)
        {
            var d = SeguimientoViajesService.ObtenerDetalle(idSeguimiento);
            if (d == null) throw new ErrorNegocioException("Ese viaje ya no existe.");
            var v = d.Resumen;
            var hitos = d.Hitos.ToDictionary(h => h.Definicion.Columna, StringComparer.OrdinalIgnoreCase);
            DateTime ahora = FechaHelper.Ahora();

            DateTime? H(string columna) => hitos.TryGetValue(columna, out var h) ? h.Hora : null;

            var ruta = new RutaViaje { Zonas = Zonas() };
            var cliente = new Lazy<OnwayApiClient>(() => new OnwayApiClient());

            // Cliente tercero: la carga en su planta y, desde que sale cargado, el camino a la entrega y el fin.
            // Con relevo (Crisar) la carga termina en la base y la descarga empieza cuando el otro tracto sale de
            // ahí con la carreta: antes se dibujaba desde la salida de Cartavio el viaje anterior de ese tracto.
            if (RutaTercero.Es(v.TipoRuta))
            {
                bool relevo = !string.IsNullOrWhiteSpace(v.Tracto2) && !string.Equals(v.Tracto1?.Trim(), v.Tracto2.Trim(), StringComparison.OrdinalIgnoreCase);
                DateTime? desde = (H("fhLlegadaTrujillo") ?? v.FhProgramacion)?.AddHours(-14);
                DateTime? finCarga = relevo ? H("fhLlegadaBase2") : H("fhSalidaPlanta");
                ruta.Tramos.Add(Tramo(cliente, "carga", "Carga en " + RutaTercero.Nombre(v.LugarCarga, "la planta de carga") + (relevo ? " → base" : ""),
                    v.Tracto1, v.Chofer1, idSeguimiento, desde, finCarga, (H("fhSalidaPlanta") ?? desde?.AddHours(22))?.AddHours(14), ahora,
                    v.Hitos.Where(x => x.Tramo == TramoViaje.Carga).Select(x => hitos[x.Columna])));
                DateTime? desdeDescarga = relevo ? H("fhSalidaBase2") : H("fhSalidaPlanta");
                ruta.Tramos.Add(Tramo(cliente, "descarga", "Entrega en " + RutaTercero.Nombre(v.LugarEntrega, "destino por confirmar"),
                    relevo ? v.Tracto2 : v.Tracto1, relevo ? v.Chofer2 : v.Chofer1, idSeguimiento,
                    desdeDescarga, H("fhLlegadaBaseFinal"), desdeDescarga?.AddDays(6), ahora,
                    v.Hitos.Where(x => x.Tramo == TramoViaje.Descarga).Select(x => hitos[x.Columna])));
                return ruta;
            }

            // Carga: desde la salida de base (o 14 h antes del ingreso a planta) hasta la llegada a base.
            DateTime? anclaCarga = H("fhIngresoPlanta") ?? H("fhLlegadaTrujillo") ?? v.FhProgramacion;
            DateTime? desdeCarga = H("fhSalidaBase1") ?? anclaCarga?.AddHours(-14);
            DateTime? hastaCarga = H("fhLlegadaBase2");
            ruta.Tramos.Add(Tramo(cliente, "carga", "Carga: Base → Trujillo → Base", v.Tracto1, v.Chofer1, idSeguimiento,
                desdeCarga, hastaCarga, (H("fhSalidaPlanta") ?? desdeCarga)?.AddHours(20), ahora,
                HitosSeguimiento.Carga.Select(x => hitos[x.Columna])));

            // Descarga: recién cuando el tracto 2 sale de base después de la carga.
            ruta.Tramos.Add(Tramo(cliente, "descarga", "Descarga: Base → Ecuador → Base", v.Tracto2, v.Chofer2, idSeguimiento,
                H("fhSalidaBase2"), H("fhLlegadaBaseFinal"), H("fhSalidaBase2")?.AddDays(5), ahora,
                HitosSeguimiento.Descarga.Select(x => hitos[x.Columna])));

            return ruta;
        }

        private static RutaTramo Tramo(Lazy<OnwayApiClient> cliente, string clave, string titulo, string tracto, string chofer,
            int idSeguimiento, DateTime? desde, DateTime? hasta, DateTime? hastaEstimado, DateTime ahora, IEnumerable<HitoDetalle> hitos)
        {
            var t = new RutaTramo { Clave = clave, Titulo = titulo, Tracto = tracto, Chofer = chofer };
            if (string.IsNullOrWhiteSpace(tracto)) { t.Aviso = "El viaje no tiene tracto para este tramo."; return t; }
            if (!desde.HasValue)
            {
                t.Aviso = clave == "descarga" ? "El tracto de descarga todavía no sale de base." : "Sin horas para ubicar el recorrido.";
                return t;
            }

            DateTime fin = hasta ?? Min(hastaEstimado ?? ahora, ahora);
            t.EnCurso = !hasta.HasValue;
            DateTime ini = desde.Value.AddMinutes(-20);
            fin = Min(fin.AddMinutes(hasta.HasValue ? 20 : 0), ini.AddDays(DiasMaximos));
            if (fin <= ini) { t.Aviso = "Las horas del tramo no forman un intervalo válido."; return t; }
            t.Desde = desde.Value.ToString("dd/MM/yyyy HH:mm");
            t.Hasta = (hasta ?? fin).ToString("dd/MM/yyyy HH:mm");

            try
            {
                t.Puntos = Puntos(cliente, idSeguimiento, clave, tracto.Trim().ToUpperInvariant(), ini, fin, cerrado: hasta.HasValue);
            }
            catch (OnwayApiException ex)
            {
                LogSGV.Error(ex, "Ruta GPS del seguimiento {IdSeguimiento} ({Tramo})", idSeguimiento, clave);
                t.Aviso = ex.MensajeParaUsuario();
            }
            catch (ErrorNegocioException ex)
            {
                t.Aviso = ex.Message;
            }
            if (t.Puntos.Count == 0 && t.Aviso == null) t.Aviso = "El GPS no tiene posiciones de " + tracto + " en ese intervalo.";

            for (int i = 1; i < t.Puntos.Count; i++)
                t.Km += Metros(t.Puntos[i - 1][0], t.Puntos[i - 1][1], t.Puntos[i][0], t.Puntos[i][1]) / 1000.0;
            t.Km = Math.Round(t.Km, 1);

            foreach (var h in hitos.Where(x => x.Hora.HasValue))
            {
                long ms = (long)Ms(FechaHelper.ConvertirAUtc(h.Hora.Value));
                var hr = new HitoRuta { Nombre = h.Definicion.Nombre, T = ms, Hora = h.Hora.Value.ToString("dd/MM HH:mm") };
                if (h.Latitud.HasValue && h.Longitud.HasValue && h.Hora == h.HoraGps)
                {
                    hr.Lat = (double)h.Latitud.Value; hr.Lng = (double)h.Longitud.Value; hr.PuntoGps = true;
                }
                else
                {
                    // Hora del Excel o corregida a mano: se ubica en el punto del recorrido más cercano en el tiempo.
                    var cerca = t.Puntos.OrderBy(p => Math.Abs(p[2] - ms)).FirstOrDefault();
                    if (cerca == null || Math.Abs(cerca[2] - ms) > TimeSpan.FromMinutes(45).TotalMilliseconds) continue;
                    hr.Lat = cerca[0]; hr.Lng = cerca[1];
                }
                t.Hitos.Add(hr);
            }
            return t;
        }

        private static List<double[]> Puntos(Lazy<OnwayApiClient> cliente, int idSeguimiento, string clave, string placa,
            DateTime iniLocal, DateTime finLocal, bool cerrado)
        {
            string nombre = $"{idSeguimiento}_{clave}_{placa}_{iniLocal:yyyyMMddHHmm}_{finLocal:yyyyMMddHHmm}.json";
            string carpeta = HostingEnvironment.MapPath("~/App_Data/RutasGps") ?? Path.Combine(Path.GetTempPath(), "SGV_RutasGps");
            string archivo = Path.Combine(carpeta, nombre);
            string claveMemoria = "RutaGps|" + nombre;

            if (cerrado && File.Exists(archivo))
                return JsonConvert.DeserializeObject<List<double[]>>(File.ReadAllText(archivo));
            if (HttpRuntime.Cache[claveMemoria] is List<double[]> enMemoria)
                return enMemoria;

            var dispositivo = cliente.Value.BuscarDispositivoPorPlaca(placa);
            if (dispositivo == null) throw new ErrorNegocioException($"La placa {placa} no está registrada en el GPS (Onway).");

            var crudos = new List<OnwayHistoryPoint>();
            DateTime iniUtc = FechaHelper.ConvertirAUtc(iniLocal), finUtc = FechaHelper.ConvertirAUtc(finLocal);
            for (DateTime d = iniUtc; d < finUtc; d = d.AddDays(1)) // el API acepta como máximo 1 día por llamada
                crudos.AddRange(cliente.Value.ObtenerHistorial(dispositivo.Id, d, Min(d.AddDays(1).AddSeconds(-1), finUtc)));

            var puntos = Simplificar(crudos);
            if (cerrado)
            {
                Directory.CreateDirectory(carpeta);
                File.WriteAllText(archivo, JsonConvert.SerializeObject(puntos));
            }
            else
                HttpRuntime.Cache.Insert(claveMemoria, puntos, null, DateTime.UtcNow.AddMinutes(10), Cache.NoSlidingExpiration);
            return puntos;
        }

        /// <summary>Más rápido que esto entre dos puntos es un error del equipo, no un camión.</summary>
        private const double KmHMaximo = 160;

        /// <summary>
        /// Quita los puntos sueltos que el equipo marca lejos de la ruta y que vuelven al instante
        /// (AVO-818 el 20/10/2026 04:39: 3,5 km y de regreso en 6 s, más de 2 000 km/h): en el mapa
        /// dibujaban un zigzag. Un punto se descarta si para llegar a él desde el último bueno habría
        /// que ir a más de <see cref="KmHMaximo"/>.
        /// </summary>
        internal static List<OnwayHistoryPoint> SinSaltos(List<OnwayHistoryPoint> ordenados)
        {
            var buenos = new List<OnwayHistoryPoint>();
            int descartados = 0;
            foreach (var p in ordenados)
            {
                var a = buenos.Count > 0 ? buenos[buenos.Count - 1] : null;
                if (a != null && descartados < 3) // tres seguidos "imposibles": el malo era el anterior, se sigue con estos
                {
                    double horas = (TrazabilidadCargaService.Hora(p) - TrazabilidadCargaService.Hora(a)).TotalHours;
                    double km = Metros(a.Lat, a.Lng, p.Lat, p.Lng) / 1000.0;
                    if (km > 1 && km / Math.Max(horas, 1.0 / 3600) > KmHMaximo) { descartados++; continue; }
                }
                descartados = 0;
                buenos.Add(p);
            }
            return buenos;
        }

        /// <summary>
        /// Ordena por hora, descarta posiciones sin coordenadas y los puntos a menos de 40 m del
        /// anterior (la unidad parada), y si aún son muchos se queda con uno de cada n.
        /// </summary>
        public static List<double[]> Simplificar(IEnumerable<OnwayHistoryPoint> crudos)
        {
            var lista = new List<double[]>();
            double[] ultimo = null;
            foreach (var p in SinSaltos(crudos.Where(x => Math.Abs(x.Lat) > 0.01 && Math.Abs(x.Lng) > 0.01)
                                              .OrderBy(TrazabilidadCargaService.Hora).ToList()))
            {
                var punto = new[] { Math.Round(p.Lat, 5), Math.Round(p.Lng, 5), Ms(TrazabilidadCargaService.Hora(p)), Math.Round(p.Speed) };
                if (ultimo != null && Metros(ultimo[0], ultimo[1], punto[0], punto[1]) < MetrosMinimos)
                {
                    ultimo[2] = punto[2]; // sigue en el mismo sitio: se corre la hora para que la animación espere ahí
                    continue;
                }
                if (ultimo != null && ultimo[2] != lista[lista.Count - 1][2]) lista.Add(new[] { ultimo[0], ultimo[1], ultimo[2], 0d });
                lista.Add(punto);
                ultimo = (double[])punto.Clone();
            }
            if (ultimo != null && lista.Count > 0 && ultimo[2] != lista[lista.Count - 1][2]) lista.Add(new[] { ultimo[0], ultimo[1], ultimo[2], 0d });

            if (lista.Count <= MaximoPuntos) return lista;
            double paso = (double)lista.Count / MaximoPuntos;
            var reducida = new List<double[]>();
            for (double i = 0; i < lista.Count; i += paso) reducida.Add(lista[(int)i]);
            if (reducida[reducida.Count - 1] != lista[lista.Count - 1]) reducida.Add(lista[lista.Count - 1]);
            return reducida;
        }

        private static List<ZonaRuta> Zonas() =>
            typeof(GeocercasTrazabilidad).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(GeocercaGps))
                .Select(f => (GeocercaGps)f.GetValue(null))
                .Select(g => new ZonaRuta { Nombre = g.Nombre, Vertices = g.Vertices.Select(x => new[] { x.Item1, x.Item2 }).ToList() })
                .ToList();

        private static double Ms(DateTime utc) => (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - Epoch).TotalMilliseconds;

        private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        private static double Metros(double lat1, double lng1, double lat2, double lng2)
        {
            const double r = 6371000;
            double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * r * Math.Asin(Math.Sqrt(a));
        }
    }
}
