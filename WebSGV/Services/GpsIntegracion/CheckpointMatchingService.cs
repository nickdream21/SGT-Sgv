using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.GpsIntegracion;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Lógica pura de detección de "llegada a un punto de control" a partir del historial
    /// de posiciones GPS de un vehículo. Sin dependencias de BD ni HTTP — solo matemática
    /// (Haversine) y comparación de eventos, para poder testearla con datos de ejemplo.
    /// </summary>
    public static class CheckpointMatchingService
    {
        private const double RadioTierraMetros = 6371000;

        /// <summary>Distancia en metros entre dos coordenadas (fórmula de Haversine).</summary>
        public static double CalcularDistanciaMetros(double lat1, double lng1, double lat2, double lng2)
        {
            double rLat1 = lat1 * Math.PI / 180;
            double rLat2 = lat2 * Math.PI / 180;
            double deltaLat = (lat2 - lat1) * Math.PI / 180;
            double deltaLng = (lng2 - lng1) * Math.PI / 180;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(rLat1) * Math.Cos(rLat2) *
                       Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return RadioTierraMetros * c;
        }

        /// <summary>
        /// Busca en el historial (no necesita venir ordenado) el momento de llegada al punto
        /// de control. Prioriza el primer evento "Ignition Off" (vehículo apagado) dentro del
        /// radio — es una señal más confiable de "llegó y se quedó" que un punto cualquiera de
        /// posición. Si no hay ningún evento de apagado dentro del radio, cae al primer punto
        /// de posición (por hora) que haya caído dentro del radio. Devuelve null si el
        /// vehículo nunca estuvo dentro del radio ese día.
        /// </summary>
        public static OnwayHistoryPoint DetectarLlegada(
            IEnumerable<OnwayHistoryPoint> historial, double latCheckpoint, double lngCheckpoint, int radioMetros)
        {
            if (historial == null) return null;

            var ordenado = historial.OrderBy(p => p.MessageTime).ToList();

            var primerApagadoEnRadio = ordenado.FirstOrDefault(p =>
                EsEventoApagado(p) &&
                CalcularDistanciaMetros(p.Lat, p.Lng, latCheckpoint, lngCheckpoint) <= radioMetros);
            if (primerApagadoEnRadio != null)
                return primerApagadoEnRadio;

            return ordenado.FirstOrDefault(p =>
                CalcularDistanciaMetros(p.Lat, p.Lng, latCheckpoint, lngCheckpoint) <= radioMetros);
        }

        /// <summary>
        /// Identifica el evento "Ignition Off" (vehículo apagado). El API no documenta un
        /// catálogo estable de <c>alertId</c> en su swagger, así que se matchea por el texto
        /// en inglés de <c>alertDescription</c> (más autodescriptivo que un ID numérico visto
        /// una sola vez en pruebas).
        /// </summary>
        private static bool EsEventoApagado(OnwayHistoryPoint punto) =>
            punto.AlertDescription?.En != null &&
            punto.AlertDescription.En.IndexOf("ignition off", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
