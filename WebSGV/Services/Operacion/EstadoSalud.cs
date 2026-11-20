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

        /// <summary>al_dia, atrasado, onway_sin_respuesta o sin_viajes; null si no se pidió el GPS o la BD no respondió. Solo para el log.</summary>
        public string Gps { get; set; }

        /// <summary>Minutos desde la última corrida programada del GPS automático; null si no hubo o no aplica. Solo para el log.</summary>
        public int? Minutos { get; set; }

        /// <summary>Minutos desde la última respuesta válida de Onway en una corrida programada; null si no hubo o no aplica. Solo para el log.</summary>
        public int? MinutosOnway { get; set; }

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
    /// - Con GPS y proveedor (<see cref="EvaluarConGpsYOnway"/>): si el cron está al día y corrió en la última hora, también
    ///   503 (onway_sin_respuesta) si la última respuesta válida de Onway en una corrida programada (la foto de la flota,
    ///   que no depende de que algún viaje se pueda seguir) fue hace más de <see cref="UmbralOnwayMinutos"/> min o no
    ///   hubo, con la aplicación más de <see cref="UmbralOnwayMinutos"/> min arriba (la marca vive en memoria).
    /// Todas las fechas en el mismo reloj (UTC en Salud.ashx).
    /// </summary>
    public static class EstadoSalud
    {
        public const string GpsAlDia = "al_dia";
        public const string GpsAtrasado = "atrasado";
        public const string GpsSinViajes = "sin_viajes";
        public const string GpsOnwaySinRespuesta = "onway_sin_respuesta";

        /// <summary>Como <see cref="GpsOnwaySinRespuesta"/> (también 503), pero la última corrida programada terminó con excepción: no llegó a Onway.</summary>
        public const string GpsCronConError = "cron_con_error";

        /// <summary>El GPS automático corre cada 10–15 min: una hora sin corridas es que dejó de correr.</summary>
        public const int UmbralGpsMinutos = 60;

        /// <summary>
        /// El cron corre pero Onway no responde bien (credenciales vencidas, caída) en ninguna corrida desde hace esto:
        /// varias corridas seguidas, para que un corte corto del proveedor no despierte a nadie.
        /// </summary>
        public const int UmbralOnwayMinutos = 120;

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

        /// <summary>
        /// <see cref="EvaluarConGps"/> y además el proveedor GPS: si el cron está al día y corrió hace
        /// <see cref="UmbralGpsMinutos"/> min o menos, 503 (<see cref="GpsOnwaySinRespuesta"/>) cuando
        /// <paramref name="ultimaRespuestaOnway"/> (última corrida programada con respuesta válida de Onway) tiene más de
        /// <paramref name="umbralOnwayMinutos"/> min o no hay, y la aplicación lleva más de eso arriba. Si el cron está
        /// atrasado, manda esa regla (sin cron no hay consultas a Onway que juzgar). Con
        /// <paramref name="ultimaProgramadaConError"/> el 503 se mantiene pero el estado es <see cref="GpsCronConError"/>.
        /// </summary>
        public static ResultadoSalud EvaluarConGpsYOnway(bool bdOk, int viajesActivos, DateTime inicioAplicacion,
            DateTime? ultimoInicioProgramado, DateTime? ultimaRespuestaOnway, DateTime ahora,
            int umbralMinutos = UmbralGpsMinutos, int umbralOnwayMinutos = UmbralOnwayMinutos, bool ultimaProgramadaConError = false)
        {
            if (umbralOnwayMinutos < 1) throw new ArgumentOutOfRangeException(nameof(umbralOnwayMinutos));
            ResultadoSalud r = EvaluarConGps(bdOk, viajesActivos, inicioAplicacion, ultimoInicioProgramado, ahora, umbralMinutos);
            if (!r.Ok || r.Gps != GpsAlDia) return r;

            int? minutosOnway = ultimaRespuestaOnway.HasValue ? MinutosEntre(ultimaRespuestaOnway.Value, ahora) : (int?)null;
            r.MinutosOnway = minutosOnway;
            bool cronRecien = r.Minutos.HasValue && r.Minutos.Value <= umbralMinutos;
            if (!cronRecien) return r; // al día solo por el arranque reciente: aún no hay corridas que juzgar

            bool recienIniciada = MinutosEntre(inicioAplicacion, ahora) <= umbralOnwayMinutos;
            bool sinRespuesta = !recienIniciada && (!minutosOnway.HasValue || minutosOnway.Value > umbralOnwayMinutos);
            if (sinRespuesta)
            {
                r.Ok = false;
                r.CodigoHttp = 503;
                r.Gps = ultimaProgramadaConError ? GpsCronConError : GpsOnwaySinRespuesta;
            }
            return r;
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
