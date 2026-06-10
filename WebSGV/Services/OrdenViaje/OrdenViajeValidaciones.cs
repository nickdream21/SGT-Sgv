using System;
using System.Globalization;
using System.Linq;

namespace WebSGV.Services.OrdenViaje
{
    /// <summary>
    /// Validaciones puras (sin System.Web ni BD) del módulo de Orden de Viaje.
    /// Extraídas de <c>AgregarOrdenViaje.aspx.cs</c> y <c>DashboardConductor.aspx.cs</c>
    /// (que las compartían duplicadas). El code-behind conserva adaptadores delgados.
    /// </summary>
    public static class OrdenViajeValidaciones
    {
        /// <summary>
        /// Valida el formato del número de orden: exactamente 6 dígitos, no "000000".
        /// Devuelve el mensaje de error, o <see cref="string.Empty"/> si es válido.
        /// </summary>
        public static string ValidarFormatoNumeroOrden(string numero)
        {
            if (string.IsNullOrWhiteSpace(numero))
                return "El número de orden es obligatorio.";

            if (numero.Length != 6)
                return $"El número de orden debe tener exactamente 6 dígitos. Ingresado: {numero.Length} caracteres.";

            if (!numero.All(char.IsDigit))
                return "El número de orden debe contener solo números (0-9).";

            if (numero == "000000")
                return "El número de orden no puede ser '000000'.";

            return string.Empty;
        }

        /// <summary>
        /// Valida que una cadena represente una hora válida en formato H:mm o HH:mm
        /// (00:00–23:59). Acepta con y sin cero inicial para interoperar con distintos
        /// dispositivos. Usa cultura invariante para no depender del locale del servidor.
        /// </summary>
        public static bool ValidarFormatoHora(string hora)
        {
            if (string.IsNullOrWhiteSpace(hora)) return false;
            string[] formatos = { @"h\:mm", @"hh\:mm" };
            if (!TimeSpan.TryParseExact(hora.Trim(), formatos, CultureInfo.InvariantCulture,
                    TimeSpanStyles.None, out TimeSpan ts))
                return false;
            return ts.Hours >= 0 && ts.Hours <= 23 && ts.Minutes >= 0 && ts.Minutes <= 59;
        }

        /// <summary>
        /// Indica si una fecha cae dentro del rango admitido por SQL Server
        /// (1753-01-01 … 9999-12-31) y no es <see cref="DateTime.MinValue"/>.
        /// </summary>
        public static bool EsFechaValidaSQL(DateTime fecha)
        {
            return fecha >= new DateTime(1753, 1, 1)
                && fecha <= new DateTime(9999, 12, 31)
                && fecha != DateTime.MinValue;
        }
    }
}
