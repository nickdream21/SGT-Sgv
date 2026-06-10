using System;
using System.Globalization;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Lógica de negocio pura (sin dependencias de System.Web, BD ni HttpContext)
    /// del módulo de Liquidaciones. Extraída de
    /// <c>Views/LiquidacionesPendientes.aspx.cs</c> para poder probarla con xUnit.
    /// El code-behind conserva métodos adaptadores que delegan aquí.
    /// </summary>
    public static class LiquidacionCalculos
    {
        /// <summary>Fecha mínima admitida en los filtros de liquidaciones.</summary>
        public static readonly DateTime FechaMinimaPermitida = new DateTime(2000, 1, 1);

        /// <summary>Monto máximo permitido para un ajuste administrativo.</summary>
        public const decimal MontoMaximoAjuste = 9_999_999m;

        /// <summary>
        /// Valida los montos de descuento/reintegro de un ajuste administrativo.
        /// Devuelve el mensaje de error, o <c>null</c> si los montos son válidos.
        /// </summary>
        public static string ValidarMontosAjuste(decimal descS, decimal descD, decimal reintS, decimal reintD)
        {
            if (descS < 0 || descD < 0 || reintS < 0 || reintD < 0)
                return "Los montos de descuento y reintegro no pueden ser negativos.";

            if (descS > MontoMaximoAjuste || descD > MontoMaximoAjuste ||
                reintS > MontoMaximoAjuste || reintD > MontoMaximoAjuste)
                return "Los montos no pueden superar S/ 9,999,999.";

            return null;
        }

        /// <summary>Clase CSS según el signo del balance (cero o positivo = positivo).</summary>
        public static string ClaseBalance(decimal balance) =>
            balance >= 0 ? "balance-positivo" : "balance-negativo";

        /// <summary>Prioridad de atención según las horas pendientes.</summary>
        public static string Prioridad(int horasPendientes)
        {
            if (horasPendientes > 24) return "urgente";
            if (horasPendientes >= 12) return "alta";
            return "normal";
        }

        /// <summary>Formatea las horas pendientes como "Xd Yh", "Xd" o "Yh".</summary>
        public static string FormatearTiempo(int horasPendientes)
        {
            if (horasPendientes >= 24)
            {
                int dias = horasPendientes / 24;
                int horasRestantes = horasPendientes % 24;
                return horasRestantes > 0 ? $"{dias}d {horasRestantes}h" : $"{dias}d";
            }

            return $"{horasPendientes}h";
        }

        /// <summary>Recorta y normaliza un texto libre a un máximo de caracteres.</summary>
        public static string NormalizarTexto(string texto, int maximo)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
            string limpio = texto.Trim();
            return limpio.Length > maximo ? limpio.Substring(0, maximo) : limpio;
        }

        /// <summary>
        /// Decodifica un PNG en base64 (admite prefijo data URI "data:image/png;base64,").
        /// Devuelve <c>null</c> si la cadena es vacía o no es base64 válido.
        /// </summary>
        public static byte[] DecodificarPngBase64(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            try
            {
                int comma = s.IndexOf(',');
                string body = (comma >= 0 && s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    ? s.Substring(comma + 1)
                    : s;
                return Convert.FromBase64String(body);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Intenta parsear una fecha de filtro en formato ISO estricto (yyyy-MM-dd).
        /// Una cadena vacía es válida y produce <paramref name="fecha"/> = <c>null</c>.
        /// Rechaza fechas anteriores a <see cref="FechaMinimaPermitida"/> o más de un
        /// año posteriores a <paramref name="hoy"/>.
        /// </summary>
        public static bool TryParseFechaFiltro(string fechaTexto, DateTime hoy, out DateTime? fecha)
        {
            fecha = null;
            if (string.IsNullOrWhiteSpace(fechaTexto)) return true;

            if (!DateTime.TryParseExact(fechaTexto.Trim(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime valor))
                return false;

            if (valor.Date < FechaMinimaPermitida || valor.Date > hoy.Date.AddYears(1))
                return false;

            fecha = valor.Date;
            return true;
        }
    }
}
