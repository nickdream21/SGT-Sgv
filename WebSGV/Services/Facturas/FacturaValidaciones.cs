using System;
using System.Text.RegularExpressions;

namespace WebSGV.Services.Facturas
{
    /// <summary>
    /// Validaciones puras (sin System.Web ni BD) del módulo de Facturas.
    /// Extraídas de <c>AgregarFactura.aspx.cs</c>. El code-behind conserva adaptadores.
    /// </summary>
    public static class FacturaValidaciones
    {
        /// <summary>
        /// Valida y normaliza el número de factura a uno de los formatos canónicos:
        /// "F222 - 00004267" o "001-098-000326757". Si la entrada ya cumple un formato,
        /// se devuelve sin cambios; si puede repararse (12 alfanuméricos que empiezan por
        /// F, o 15 dígitos), se reconstruye. En cualquier otro caso lanza
        /// <see cref="FormatException"/>.
        /// </summary>
        public static string ValidarYFormatearNumeroFactura(string numeroFactura)
        {
            if (string.IsNullOrWhiteSpace(numeroFactura))
                throw new FormatException("El número de factura no puede estar vacío.");

            // Formato 1: F222 - 00004267 (formato original)
            string pattern1 = @"^F\d{3} - \d{8}$";

            // Formato 2: 001-098-000326757 (nuevo formato)
            string pattern2 = @"^\d{3}-\d{3}-\d{9}$";

            // Si ya cumple alguno de los formatos, retornarlo sin cambios
            if (Regex.IsMatch(numeroFactura, pattern1) || Regex.IsMatch(numeroFactura, pattern2))
            {
                return numeroFactura;
            }

            // Intentar corregir el formato tipo F222 - 00004267
            string soloNumerosYLetras = Regex.Replace(numeroFactura, @"[^A-Za-z0-9]", "");

            if (soloNumerosYLetras.Length == 12 && soloNumerosYLetras.StartsWith("F", StringComparison.OrdinalIgnoreCase))
            {
                string codigo = soloNumerosYLetras.Substring(0, 4).ToUpper(); // Ejemplo: "F222"
                string secuencia = soloNumerosYLetras.Substring(4);           // Ejemplo: "00004267"
                return $"{codigo} - {secuencia}";
            }

            // Intentar corregir el formato tipo 001-098-000326757
            string soloNumeros = Regex.Replace(numeroFactura, @"[^0-9]", "");

            if (soloNumeros.Length == 15) // 3 + 3 + 9 = 15 dígitos
            {
                string parte1 = soloNumeros.Substring(0, 3);   // 001
                string parte2 = soloNumeros.Substring(3, 3);   // 098
                string parte3 = soloNumeros.Substring(6, 9);   // 000326757
                return $"{parte1}-{parte2}-{parte3}";
            }

            // Si no es posible corregir con ningún formato, lanzar excepción
            throw new FormatException("El número de factura debe tener uno de estos formatos: 'F222 - 00004267' o '001-098-000326757'.");
        }
    }
}
