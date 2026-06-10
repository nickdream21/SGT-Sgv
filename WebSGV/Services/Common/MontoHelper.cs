using System;
using System.Globalization;

namespace WebSGV.Services.Common
{
    /// <summary>
    /// Lógica pura (sin System.Web ni BD) para parsear y validar montos de dinero.
    /// Extraída de <c>DashboardConductor.aspx.cs</c> y <c>BuscarOrdenViaje.aspx.cs</c>.
    /// Crítica para el flujo de liquidaciones: un error aquí cuesta dinero.
    /// </summary>
    public static class MontoHelper
    {
        /// <summary>Monto máximo permitido para un campo de dinero.</summary>
        public const decimal MontoMaximo = 9_999_999m;

        /// <summary>
        /// Parsea un monto proveniente de un campo HTML <c>type="number"</c>, que
        /// siempre usa punto como separador decimal (cultura invariante), con
        /// independencia de la cultura del servidor (es-PE usa coma). Cadena vacía o
        /// formato inválido devuelven 0; los valores negativos se clampean a 0.
        /// </summary>
        public static decimal ParseMonto(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0m;
            if (!decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d))
                return 0m;
            return d < 0m ? 0m : d;
        }

        /// <summary>
        /// Valida un monto deserializado desde un campo controlado por el cliente.
        /// A diferencia de <see cref="ParseMonto"/>, lanza <see cref="InvalidOperationException"/>
        /// en lugar de clampar, porque un valor negativo o excesivo indica manipulación
        /// del payload (la excepción propaga el rollback de la transacción en curso).
        /// </summary>
        public static decimal ValidarMonto(decimal? monto, string campo)
        {
            decimal valor = monto ?? 0m;
            if (valor < 0m)
                throw new InvalidOperationException(
                    $"Monto inválido en '{campo}': los valores negativos no están permitidos.");
            if (valor > MontoMaximo)
                throw new InvalidOperationException(
                    $"Monto inválido en '{campo}': excede el límite máximo de S/ 9,999,999.");
            return valor;
        }

        /// <summary>Sobrecarga no-nullable de <see cref="ValidarMonto(decimal?, string)"/>.</summary>
        public static decimal ValidarMonto(decimal monto, string campo)
            => ValidarMonto((decimal?)monto, campo);

        /// <summary>
        /// Indica si un texto representa un monto no negativo. Una cadena vacía se
        /// considera válida (true). Usa la cultura actual del servidor, como el código
        /// original del filtro de búsqueda.
        /// </summary>
        public static bool EsMontoNoNegativo(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return true;
            if (decimal.TryParse(valor, out decimal monto)) return monto >= 0;
            return false;
        }

        /// <summary>
        /// Convierte un texto a decimal usando la cultura actual; devuelve 0 si el
        /// formato no es válido.
        /// </summary>
        public static decimal ConvertToDecimal(string value)
        {
            return decimal.TryParse(value, out decimal result) ? result : 0m;
        }
    }
}
