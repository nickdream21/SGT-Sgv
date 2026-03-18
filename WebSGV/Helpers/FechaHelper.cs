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
    }
}
