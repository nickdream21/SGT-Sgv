using System.Text.RegularExpressions;

namespace WebSGV.Services.Vehiculos
{
    /// <summary>
    /// Formas de una placa de tracto o semirremolque (bloque 5, B5-1). Lógica pura.
    ///
    ///  - <see cref="Clave"/>: lo que identifica a la placa al comparar (mayúsculas, sin guion ni espacios):
    ///    "ABC-123", "abc123" y "ABC 123 " son la misma placa. En SQL es
    ///    <c>REPLACE(REPLACE(UPPER(placa),'-',''),' ','')</c>.
    ///  - <see cref="Canonica"/>: cómo se guarda. Las placas peruanas (6 letras/números) van como "ABC-123",
    ///    igual que las 235 que ya hay en la BD; otra forma se deja en mayúsculas y sin espacios.
    /// </summary>
    public static class PlacaVehiculo
    {
        private static readonly Regex RxNoAlfanumerico = new Regex(@"[^A-Za-z0-9]", RegexOptions.Compiled);
        private static readonly Regex RxEspacios = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex RxSoloLetrasNumerosGuion = new Regex(@"^[A-Za-z0-9\s-]+$", RegexOptions.Compiled);
        private static readonly Regex RxTresLetrasCuatroDigitos = new Regex(@"^[A-Z]{3}[0-9]{4}$", RegexOptions.Compiled);

        /// <summary>
        /// Placa en mayúsculas, sin guion ni espacios (ni otros signos); null si viene vacía.
        /// Es la misma normalización que usa la lectura de documentos (<c>LectorDocumentosPedido.NormalizarPlaca</c>).
        /// </summary>
        public static string Clave(string placa) =>
            string.IsNullOrWhiteSpace(placa) ? null : RxNoAlfanumerico.Replace(placa, "").ToUpperInvariant();

        /// <summary>
        /// Placa como se guarda: 6 letras/números (con o sin guion o espacios) → "AAA-999"
        /// ("abc123", "AB-C123" → "ABC-123"); 3 letras + 4 dígitos → "AAA-9999" ("ABC 1234" → "ABC-1234");
        /// cualquier otra forma (otros 7 caracteres, otros signos), en mayúsculas y sin espacios, para que la
        /// validación de formato decida. Vacía → null.
        /// </summary>
        public static string Canonica(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa)) return null;

            string clave = Clave(placa);
            if (RxSoloLetrasNumerosGuion.IsMatch(placa))
            {
                if (clave.Length == 6 || RxTresLetrasCuatroDigitos.IsMatch(clave))
                    return clave.Substring(0, 3) + "-" + clave.Substring(3);
            }

            return RxEspacios.Replace(placa, "").ToUpperInvariant();
        }
    }
}
