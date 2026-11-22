using System;
using System.Globalization;
using System.Text.RegularExpressions;

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
        /// Texto del rechazo cuando un monto escrito no cumple <see cref="IntentarLeerMonto"/>. El navegador
        /// (<c>SGV.leerMonto</c> en <c>Scripts/paginas/SiteMaster.js</c>) muestra el mismo.
        /// </summary>
        public const string MensajeFormatoMonto =
            "Escriba el monto sin separador de miles, con coma o punto para los decimales (por ejemplo 1250,50).";

        // La regla de lectura de montos escritos por el usuario, igual a SGV.leerMonto (SiteMaster.js): signo
        // opcional al inicio (como el NumberStyles.Float de antes; el negativo lo rechaza ValidarMonto), dígitos
        // y como máximo un separador decimal, coma o punto, con 0 a 2 decimales. Sin separador de miles, sin
        // exponente y sin espacios internos. "1,250" y "1.250" son ambiguos (¿miles o 3 decimales?): se
        // rechazan por tener más de 2 decimales. [0-9] y no \d: \d acepta dígitos de otros alfabetos.
        private static readonly Regex RxMonto = new Regex(
            @"^[+-]?(?:[0-9]+(?:[.,][0-9]{0,2})?|[.,][0-9]{1,2})$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Lee un monto escrito por el usuario con la regla única de SGV (ver <see cref="RxMonto"/>), sin
        /// depender de la cultura del hilo: "12,50" y "12.50" son 12.50; "1250" es 1250; "1,250", "1.250,00",
        /// "1e3" o "1 250" no se leen. Recorta los espacios de los extremos. No valida signo ni tope (eso lo
        /// hace <see cref="ValidarMonto(decimal?, string)"/>). Vacío no es un monto: devuelve false.
        /// </summary>
        public static bool IntentarLeerMonto(string valor, out decimal monto)
        {
            monto = 0m;
            if (valor == null) return false;
            string texto = valor.Trim();
            if (!RxMonto.IsMatch(texto)) return false;
            // Solo dígitos, un signo y un separador: con el punto invariante no hay otra lectura posible.
            // Falla solo por desbordamiento (más de ~28 dígitos), que tampoco es un monto válido.
            return decimal.TryParse(texto.Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out monto);
        }

        /// <summary>
        /// Lee un monto que escribió el usuario (TextBox o campo del formulario) con la regla de
        /// <see cref="IntentarLeerMonto"/> y lo valida con <see cref="ValidarMonto(decimal?, string)"/>.
        /// Vacío = 0. Un texto que no cumple la regla, un negativo o un monto sobre el tope lanzan
        /// <see cref="ErrorNegocioException"/> con el nombre del campo, para que el usuario sepa qué corregir.
        /// </summary>
        public static decimal LeerMonto(string valor, string campo)
        {
            if (string.IsNullOrWhiteSpace(valor)) return 0m;
            if (!IntentarLeerMonto(valor, out decimal d))
            {
                // Cumple la regla pero no cabe en un decimal (más de ~28 dígitos): es un monto sobre el tope.
                if (RxMonto.IsMatch(valor.Trim()))
                    return ValidarMonto(decimal.MaxValue, campo);
                throw new ErrorNegocioException($"El monto de «{campo}» no es válido. {MensajeFormatoMonto}");
            }
            return ValidarMonto(d, campo);
        }

        /// <summary>
        /// Valida un monto deserializado desde un campo controlado por el cliente.
        /// A diferencia de un clamp a 0, lanza <see cref="ErrorNegocioException"/>
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
    }
}
