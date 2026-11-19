using System;

namespace WebSGV.Services.Operacion
{
    /// <summary>
    /// Resultado de Salud.ashx. Al vigilante (anónimo) solo le llega <see cref="ComoJson"/> = {"ok":true|false};
    /// el detalle (<see cref="Gps"/>, <see cref="Minutos"/>) es para el log.
    /// </summary>
    public sealed class ResultadoSalud
    {
        public bool Ok { get; set; }

        /// <summary>200 si todo está bien; 503 si la BD no responde o el GPS automático está atrasado.</summary>
        public int CodigoHttp { get; set; }

        /// <summary>al_dia, atrasado o sin_viajes; null si no se pidió el GPS o la BD no respondió. Solo para el log.</summary>
        public string Gps { get; set; }

        /// <summary>Minutos desde la última corrida programada del GPS automático; null si no hubo o no aplica. Solo para el log.</summary>
        public int? Minutos { get; set; }

        /// <summary>Lo único que ve el vigilante: sin estado operativo.</summary>
        public string ComoJson() => Ok ? "{\"ok\":true}" : "{\"ok\":false}";
    }

    /// <summary>
    /// Reglas de la página de salud que vigila UptimeRobot (E1-6). Puras: no tocan la BD, la sesión ni la caché.
    /// - Sin GPS: 200 si la BD responde, 503 si no.
    /// - Con GPS: además 503 (atrasado) si hay viajes que el GPS automático debería consultar, la aplicación lleva
    ///   más de <see cref="UmbralGpsMinutos"/> min arriba (tras un reciclaje no se sabe cuándo corrió) y la última
    ///   corrida programada (cron-job.org, no el botón) fue hace más de <see cref="UmbralGpsMinutos"/> min o no hubo.
    ///   No se mira fhUltimaConsultaGps: sin escritura cuando la unidad está quieta o sin equipo GPS, daba falsas alarmas.
    /// Todas las fechas en el mismo reloj (UTC en Salud.ashx).
    /// </summary>
    public static class EstadoSalud
    {
        public const string GpsAlDia = "al_dia";
        public const string GpsAtrasado = "atrasado";
        public const string GpsSinViajes = "sin_viajes";

        /// <summary>El GPS automático corre cada 10–15 min: una hora sin corridas es que dejó de correr.</summary>
        public const int UmbralGpsMinutos = 60;

        /// <summary>fhUltimaConsultaGps más vieja que esto solo deja una advertencia en el log (no decide el 503).</summary>
        public const int UmbralConsultaViejaMinutos = 90;

        /// <summary>Un aviso repetido (mismo estado con problema) se vuelve a escribir en el log como mucho cada esto.</summary>
        public static readonly TimeSpan RepeticionAviso = TimeSpan.FromHours(1);

        /// <summary>Tiempo que se reutiliza la respuesta: el SELECT 1 y la revisión del GPS (que carga el tablero).</summary>
        public static TimeSpan DuracionCache(bool conGps) => TimeSpan.FromSeconds(conGps ? 120 : 20);

        /// <summary>Solo la base de datos.</summary>
        public static ResultadoSalud Evaluar(bool bdOk) =>
            new ResultadoSalud { Ok = bdOk, CodigoHttp = bdOk ? 200 : 503 };

        /// <summary>
        /// Base de datos y GPS automático. Límites en minutos completos: con 60, a los 60 está al día y a los 61 atrasado.
        /// </summary>
        public static ResultadoSalud EvaluarConGps(bool bdOk, int viajesActivos, DateTime inicioAplicacion,
            DateTime? ultimoInicioProgramado, DateTime ahora, int umbralMinutos = UmbralGpsMinutos)
        {
            if (umbralMinutos < 1) throw new ArgumentOutOfRangeException(nameof(umbralMinutos));
            if (!bdOk) return Evaluar(false); // sin BD no se sabe nada del GPS
            if (viajesActivos <= 0) return new ResultadoSalud { Ok = true, CodigoHttp = 200, Gps = GpsSinViajes };

            int? minutos = ultimoInicioProgramado.HasValue ? MinutosEntre(ultimoInicioProgramado.Value, ahora) : (int?)null;
            bool recienIniciada = MinutosEntre(inicioAplicacion, ahora) <= umbralMinutos;
            bool atrasado = !recienIniciada && (!minutos.HasValue || minutos.Value > umbralMinutos);
            return new ResultadoSalud
            {
                Ok = !atrasado,
                CodigoHttp = atrasado ? 503 : 200,
                Gps = atrasado ? GpsAtrasado : GpsAlDia,
                Minutos = minutos,
            };
        }

        /// <summary>La consulta GPS más reciente de los viajes activos es vieja (solo advertencia en el log). Sin consultas: vieja.</summary>
        public static bool ConsultaVieja(int viajesActivos, DateTime? ultimaConsulta, DateTime ahora) =>
            viajesActivos > 0 && (!ultimaConsulta.HasValue || MinutosEntre(ultimaConsulta.Value, ahora) > UmbralConsultaViejaMinutos);

        /// <summary>
        /// Si se escribe el aviso en el log: cuando el estado cambia, o si sigue con problema y pasó
        /// <see cref="RepeticionAviso"/> desde el último. Un estado sin problema (null o "") que no cambia no se repite.
        /// </summary>
        public static bool DebeRegistrar(string estadoAnterior, DateTime? registradoEn, string estadoActual, DateTime ahora)
        {
            if (!string.Equals(estadoAnterior ?? "", estadoActual ?? "", StringComparison.Ordinal)) return true;
            if (string.IsNullOrEmpty(estadoActual)) return false;
            return !registradoEn.HasValue || ahora - registradoEn.Value >= RepeticionAviso;
        }

        /// <summary>Minutos completos de <paramref name="desde"/> a <paramref name="hasta"/>; una hora futura cuenta 0.</summary>
        private static int MinutosEntre(DateTime desde, DateTime hasta) =>
            (int)Math.Max(0, Math.Floor((hasta - desde).TotalMinutes));
    }
}
