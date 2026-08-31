using System;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Utilidad para obtener la fecha/hora actual en la zona horaria de Perú (UTC-5).
    /// Necesario cuando la aplicación corre en Azure (UTC por defecto).
    /// </summary>
    public static class FechaHelper
    {
        private static readonly TimeZoneInfo ZonaPeruana =
            TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");

        /// <summary>
        /// Retorna la fecha y hora actual en la zona horaria de Perú (UTC-5, sin horario de verano).
        /// Usar este método en lugar de DateTime.Now para timestamps guardados en la base de datos.
        /// </summary>
        public static DateTime Ahora()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaPeruana);
        }

        /// <summary>
        /// Retorna la fecha actual (sin hora) en la zona horaria de Perú.
        /// Usar en lugar de DateTime.Today para comparaciones de fecha.
        /// </summary>
        public static DateTime Hoy()
        {
            return Ahora().Date;
        }

        /// <summary>
        /// Convierte un <see cref="DateTime"/> en UTC (p. ej. <c>messageTime</c> del API de
        /// Onway/CarSync, que reporta en UTC) a la zona horaria de Perú. Fuerza
        /// <see cref="DateTimeKind.Utc"/> antes de convertir, sin importar cómo haya quedado
        /// el <c>Kind</c> tras la deserialización JSON.
        /// </summary>
        public static DateTime ConvertirDeUtc(DateTime utc)
        {
            DateTime utcSeguro = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utcSeguro, ZonaPeruana);
        }

        /// <summary>
        /// Conversión inversa de <see cref="ConvertirDeUtc"/>: de hora local de Perú (p. ej. un
        /// valor ya guardado en BD por una consulta GPS anterior) a UTC, para poder retomar un
        /// cursor de búsqueda expresado en UTC sin tener que volver a consultar el API.
        /// </summary>
        public static DateTime ConvertirAUtc(DateTime horaLocal)
        {
            DateTime localSinEspecificar = DateTime.SpecifyKind(horaLocal, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(localSinEspecificar, ZonaPeruana);
        }
    }
}
