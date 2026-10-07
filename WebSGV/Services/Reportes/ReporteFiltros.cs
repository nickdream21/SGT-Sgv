using System;
using System.Globalization;

namespace WebSGV.Services.Reportes
{
    /// <summary>
    /// Lógica pura (sin System.Web ni BD) para validar los filtros comunes de Reportes.aspx.
    /// Antes cada GenerarReporte*() hacía <c>DateTime.Parse(txtFechaDesde.Text)</c> sin validar:
    /// una fecha vacía o inválida terminaba en una excepción y un mensaje genérico.
    /// </summary>
    public static class ReporteFiltros
    {
        private static readonly CultureInfo CulturaPeru = CultureInfo.GetCultureInfo("es-PE");

        /// <summary>
        /// Valida el rango de fechas de un reporte. Acepta el formato de los inputs
        /// <c>type="date"</c> (yyyy-MM-dd) y, como respaldo, el formato es-PE (dd/MM/yyyy).
        /// Devuelve <c>null</c> si es válido, o el mensaje para el usuario si no lo es.
        /// </summary>
        public static string ValidarRangoFechas(string desde, string hasta,
                                                out DateTime fechaDesde, out DateTime fechaHasta)
        {
            fechaHasta = DateTime.MinValue;

            if (!IntentarParsearFecha(desde, out fechaDesde))
                return "Ingrese una fecha 'Desde' válida.";

            if (!IntentarParsearFecha(hasta, out fechaHasta))
                return "Ingrese una fecha 'Hasta' válida.";

            if (fechaDesde > fechaHasta)
                return "La fecha 'Desde' no puede ser posterior a la fecha 'Hasta'.";

            return null;
        }

        private static bool IntentarParsearFecha(string texto, out DateTime fecha)
        {
            fecha = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(texto)) return false;
            texto = texto.Trim();

            bool ok = DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
                   || DateTime.TryParse(texto, CulturaPeru, DateTimeStyles.None, out fecha);

            // SQL Server (datetime) no admite fechas anteriores a 1753.
            return ok && fecha.Year >= 1753;
        }
    }
}
