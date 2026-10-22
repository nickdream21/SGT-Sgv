using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Decide si un viaje necesita la consulta completa al GPS (historial del día y detección de hitos) o si
    /// sus tractos siguen donde estaban en la última consulta completa. Antes cada "Actualizar GPS" pedía el
    /// historial de cada tracto aunque estuviera parado en la base, en una cola o descargando; ahora una sola
    /// consulta trae la última posición de toda la flota (<see cref="OnwayApiClient.UltimasPosiciones"/>) y
    /// solo se analiza lo que se movió. Cada <see cref="Vigencia"/> se analiza igual, por si acaso.
    /// La memoria se pierde si el sitio se reinicia: entonces todos se analizan una vez.
    /// </summary>
    public static class MovimientoGps
    {
        /// <summary>Más que esto entre dos posiciones es movimiento (el GPS de una unidad parada varía ~10–30 m).</summary>
        public const double MetrosMinimos = 50;

        /// <summary>Un viaje quieto igual se analiza completo si su última consulta completa tiene más que esto.</summary>
        public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(45);

        private sealed class Pasada
        {
            public DateTime HoraUtc;
            public Dictionary<string, double[]> Posiciones;
        }

        private static readonly ConcurrentDictionary<int, Pasada> ultimas = new ConcurrentDictionary<int, Pasada>();

        /// <summary>
        /// True si el viaje tuvo una consulta completa hace menos de <see cref="Vigencia"/> y desde entonces ninguno
        /// de sus tractos se movió más de <see cref="MetrosMinimos"/> (todos tienen posición ahora y entonces).
        /// </summary>
        public static bool SinMovimiento(int idViaje, IDictionary<string, double[]> posicionesAhora, DateTime ahoraUtc)
        {
            if (!ultimas.TryGetValue(idViaje, out var p) || ahoraUtc - p.HoraUtc > Vigencia) return false;
            return SonLasMismas(p.Posiciones, posicionesAhora);
        }

        /// <summary>Guarda dónde estaban los tractos del viaje en su consulta completa.</summary>
        public static void Registrar(int idViaje, IDictionary<string, double[]> posiciones, DateTime ahoraUtc) =>
            ultimas[idViaje] = new Pasada { HoraUtc = ahoraUtc, Posiciones = new Dictionary<string, double[]>(posiciones, StringComparer.OrdinalIgnoreCase) };

        /// <summary>Mismos tractos y ninguno a más de <see cref="MetrosMinimos"/> de donde estaba.</summary>
        public static bool SonLasMismas(IDictionary<string, double[]> antes, IDictionary<string, double[]> ahora)
        {
            if (antes == null || ahora == null || antes.Count == 0 || antes.Count != ahora.Count) return false;
            foreach (var kv in ahora)
            {
                if (!antes.TryGetValue(kv.Key, out var a)) return false;
                if (Metros(a[0], a[1], kv.Value[0], kv.Value[1]) > MetrosMinimos) return false;
            }
            return true;
        }

        public static double Metros(double lat1, double lng1, double lat2, double lng2)
        {
            double dLat = (lat2 - lat1) * Math.PI / 180, dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            return 2 * 6371000 * Math.Asin(Math.Sqrt(a));
        }

        /// <summary>Posición actual [lat, lng] de cada tracto que aparece en la foto de la flota.</summary>
        public static Dictionary<string, double[]> Posiciones(IEnumerable<string> tractos, IDictionary<string, Models.GpsIntegracion.OnwayDevice> foto)
        {
            var res = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in tractos.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToUpperInvariant()).Distinct())
                if (foto.TryGetValue(t, out var d) && d.Expand?.LastLocation != null)
                    res[t] = new[] { d.Expand.LastLocation.Latitude, d.Expand.LastLocation.Longitude };
            return res;
        }
    }
}
