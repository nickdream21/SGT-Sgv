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
        /// de control, en 3 niveles de confianza decrecientes:
        /// 1) el primer evento "Ignition Off" (vehículo apagado) dentro del radio que además se
        ///    sostiene — es decir, el vehículo no sale del radio dentro de los siguientes
        ///    <paramref name="ventanaConfirmacionMinutos"/> minutos. Esto descarta un apagado/
        ///    encendido breve mientras el vehículo todavía está maniobrando cerca del punto
        ///    (p. ej. en una cola de ingreso) y no se ha asentado de verdad todavía — si ese
        ///    primer evento resulta ser un blip de ese tipo, se prueba con el siguiente evento
        ///    de apagado dentro del radio, y así sucesivamente;
        /// 2) si ningún evento de apagado se sostiene (o no hay ninguno), la primera racha de
        ///    <paramref name="minPuntosParadaSostenida"/> o más puntos consecutivos dentro del
        ///    radio con velocidad baja — confirma que se quedó, no que solo pasó cerca;
        /// 3) si tampoco hay racha sostenida, el primer punto de posición (por hora) que haya
        ///    caído dentro del radio (comportamiento histórico, como último recurso).
        /// Devuelve null si el vehículo nunca estuvo dentro del radio ese día.
        /// </summary>
        public static OnwayHistoryPoint DetectarLlegada(
            IEnumerable<OnwayHistoryPoint> historial, double latCheckpoint, double lngCheckpoint, int radioMetros,
            double velocidadMaximaParadaKmh = 5.0, int minPuntosParadaSostenida = 2, int ventanaConfirmacionMinutos = 15)
        {
            if (historial == null) return null;

            var ordenado = historial.OrderBy(p => p.MessageTime).ToList();

            bool EnRadio(OnwayHistoryPoint p) =>
                CalcularDistanciaMetros(p.Lat, p.Lng, latCheckpoint, lngCheckpoint) <= radioMetros;

            bool SigueDentroDelRadioTrasElEvento(OnwayHistoryPoint evento)
            {
                DateTime limite = evento.MessageTime.AddMinutes(ventanaConfirmacionMinutos);
                return !ordenado.Any(p =>
                    p.MessageTime > evento.MessageTime && p.MessageTime <= limite && !EnRadio(p));
            }

            var candidatosApagado = ordenado.Where(p => EsEventoApagado(p) && EnRadio(p));
            foreach (var candidato in candidatosApagado)
            {
                if (SigueDentroDelRadioTrasElEvento(candidato))
                    return candidato;
            }

            bool EnParada(OnwayHistoryPoint p) => EnRadio(p) && p.Speed < velocidadMaximaParadaKmh;

            for (int i = 0; i < ordenado.Count; i++)
            {
                if (!EnParada(ordenado[i])) continue;
                int j = i;
                while (j < ordenado.Count && EnParada(ordenado[j])) j++;
                if (j - i >= minPuntosParadaSostenida) return ordenado[i];
                i = j - 1;
            }

            return ordenado.FirstOrDefault(EnRadio);
        }

        /// <summary>
        /// Busca en el historial (no necesita venir ordenado) el momento de salida real de un
        /// punto de control: el punto a partir del cual el vehículo se aleja de forma sostenida
        /// sin volver a quedar parado dentro del radio (más allá de una parada breve tipo
        /// portón/semáforo), evaluado solo dentro de los siguientes
        /// <paramref name="ventanaConfirmacionMinutos"/> minutos desde ese punto — no
        /// indefinidamente hacia el resto del historial disponible. Esto descarta arranques
        /// falsos cercanos (maniobras dentro del radio que aceleran brevemente y vuelven a
        /// quedar parados) sin dejar que un paso posterior sin relación por la misma zona —
        /// horas después, por otro motivo — invalide una salida que ya fue real y sostenida en
        /// su momento. Devuelve null si el vehículo nunca estuvo dentro del radio, o si aún no
        /// hay una salida confirmada en esta ventana (p. ej. sigue parado hasta el final del
        /// historial disponible).
        /// </summary>
        public static OnwayHistoryPoint DetectarSalida(
            IEnumerable<OnwayHistoryPoint> historial,
            double latCheckpoint, double lngCheckpoint, int radioMetros,
            double velocidadMinimaKmh = 3.0,
            int toleranciaParadaBreveMinutos = 2,
            int ventanaConfirmacionMinutos = 15)
        {
            if (historial == null) return null;

            var ordenado = historial.OrderBy(p => p.MessageTime).ToList();
            if (ordenado.Count == 0) return null;

            bool EnRadio(OnwayHistoryPoint p) =>
                CalcularDistanciaMetros(p.Lat, p.Lng, latCheckpoint, lngCheckpoint) <= radioMetros;

            int primerIdxEnRadio = ordenado.FindIndex(EnRadio);
            if (primerIdxEnRadio == -1) return null; // nunca estuvo en el radio: no hay salida que buscar

            // ¿Hay, desde `desde` en adelante y antes de `limiteVentana`, un tramo sostenido de
            // parada-en-zona? Distingue un regreso real e inmediato (el vehículo vuelve a
            // quedarse ahí mismo) de un blip de portón/semáforo — acotado en el tiempo para no
            // confundirlo con un paso posterior no relacionado, mucho más tarde.
            bool ExisteRegresoSostenidoDespues(int desde, DateTime limiteVentana)
            {
                int j = desde;
                while (j < ordenado.Count && ordenado[j].MessageTime <= limiteVentana)
                {
                    bool paradaEnZona = EnRadio(ordenado[j]) && ordenado[j].Speed < velocidadMinimaKmh;
                    if (!paradaEnZona) { j++; continue; }

                    int k = j;
                    while (k < ordenado.Count && ordenado[k].MessageTime <= limiteVentana &&
                           EnRadio(ordenado[k]) && ordenado[k].Speed < velocidadMinimaKmh) k++;

                    TimeSpan duracion = ordenado[k - 1].MessageTime - ordenado[j].MessageTime;
                    if (duracion >= TimeSpan.FromMinutes(toleranciaParadaBreveMinutos))
                        return true;

                    j = k; // era ruido breve — seguir buscando más adelante, aún dentro de la ventana
                }
                return false;
            }

            for (int i = primerIdxEnRadio; i < ordenado.Count; i++)
            {
                if (ordenado[i].Speed < velocidadMinimaKmh) continue; // sigue detenido/maniobrando
                DateTime limite = ordenado[i].MessageTime.AddMinutes(ventanaConfirmacionMinutos);
                if (!ExisteRegresoSostenidoDespues(i + 1, limite)) return ordenado[i]; // alejamiento sostenido confirmado
            }

            return null; // aún no hay una salida confirmada en esta ventana
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
