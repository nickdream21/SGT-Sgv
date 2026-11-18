using System;
using System.Globalization;

namespace WebSGV.Services.Common
{
    /// <summary>
    /// Lógica pura (sin System.Web ni BD) para parsear y validar montos de dinero.
    /// Extraída de <c>DashboardConductor.aspx.cs</c>.
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
        /// Lee un monto que escribió el usuario en un campo HTML <c>type="number"</c> (punto
        /// decimal, cultura invariante; acepta notación exponencial como <c>1e15</c>, que el
        /// navegador también deja pasar) y lo valida con <see cref="ValidarMonto(decimal?, string)"/>.
        /// Vacío = 0. Un texto que no es número, un negativo o un monto sobre el tope lanzan
        /// <see cref="ErrorNegocioException"/> con el nombre del campo, para que el usuario sepa
        /// qué corregir (antes se convertía en 0 o pasaba sin tope).
        /// </summary>
        public static decimal LeerMonto(string valor, string campo)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0m;
            if (!decimal.TryParse(valor.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal d))
                throw new ErrorNegocioException($"El monto de «{campo}» no es un número válido.");
            return ValidarMonto(d, campo);
        }

        /// <summary>
        /// Valida un monto deserializado desde un campo controlado por el cliente.
        /// A diferencia de <see cref="ParseMonto"/>, lanza <see cref="ErrorNegocioException"/>
        /// (que hereda de <see cref="InvalidOperationException"/>) en lugar de clampar: el mensaje
        /// dice qué campo falla y se puede mostrar al usuario. La excepción también deshace la
        /// transacción en curso. Rechaza negativos, montos sobre <see cref="MontoMaximo"/> y más
        /// de 2 decimales.
        /// </summary>
        public static decimal ValidarMonto(decimal? monto, string campo)
        {
            decimal valor = monto ?? 0m;
            if (valor < 0m)
                throw new ErrorNegocioException(
                    $"El monto de «{campo}» no puede ser negativo.");
            if (valor > MontoMaximo)
                throw new ErrorNegocioException(
                    $"El monto de «{campo}» supera el máximo permitido ({MontoMaximo.ToString("N0", CultureInfo.InvariantCulture)}).");
            // Todas las columnas de destino son de 2 decimales (DECIMAL(12,2) en la liquidación desde la
            // migración 51, (18,2) en factura/flete y movimientos). Un tercer decimal lo redondearía SQL
            // Server sin avisar y lo guardado no sería lo que se escribió (ni lo que se firmó).
            // "12.50" o "12.500" pasan: solo se rechaza un decimal distinto de cero después del segundo.
            if (decimal.Round(valor, 2) != valor)
                throw new ErrorNegocioException(
                    $"Ingrese el monto de «{campo}» con máximo 2 decimales.");
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
