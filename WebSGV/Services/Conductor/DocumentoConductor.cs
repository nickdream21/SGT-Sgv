using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace WebSGV.Services.Conductor
{
    /// <summary>
    /// Forma única del documento de un conductor (bloque 5, B5-2). La usan la Lista de choferes
    /// (<see cref="ListaChoferesService"/>) y el alta/edición manual (<see cref="RegistroConductorService"/>),
    /// para que el mismo documento escrito de dos formas ("2719446" y "002719446") sea el mismo conductor.
    ///
    /// Reglas (las mismas que aplicaba la Lista de choferes):
    /// <list type="bullet">
    /// <item>DNI: 8 dígitos. Con 7 se completa el cero inicial (Excel lo guarda como número y lo pierde).</item>
    /// <item>Carnet de extranjería: 9 dígitos. Si es solo dígitos y tiene menos, se completa con ceros a la
    /// izquierda (002719446). Uno con letras o guion se deja como está (en mayúsculas).</item>
    /// </list>
    /// Los espacios intermedios se quitan en ambos.
    /// </summary>
    public static class DocumentoConductor
    {
        /// <summary>Largo de <c>Conductor.DNI</c> (char(8)).</summary>
        public const int LargoDni = 8;

        /// <summary>Dígitos de un carnet de extranjería peruano.</summary>
        public const int DigitosCarnet = 9;

        /// <summary>Largo de <c>Conductor.carnetExtranjeria</c> (char(11)).</summary>
        public const int LargoCarnet = 11;

        /// <summary>Mínimo de caracteres de un carnet escrito a mano.</summary>
        public const int MinimoCarnet = 6;

        private static readonly Regex RxSoloDigitos = new Regex(@"^\d+$", RegexOptions.Compiled);
        private static readonly Regex RxCarnet = new Regex(@"^[A-Z0-9\-]+$", RegexOptions.Compiled);

        private static string SinEspacios(string texto) =>
            Regex.Replace(texto ?? "", @"\s+", "").ToUpper(CultureInfo.InvariantCulture);

        /// <summary>
        /// DNI de 8 dígitos, o null si no lo es. Uno de 7 dígitos recupera el cero inicial.
        /// </summary>
        public static string NormalizarDni(string dni)
        {
            string d = SinEspacios(dni);
            if (!RxSoloDigitos.IsMatch(d)) return null;
            if (d.Length == LargoDni - 1) return "0" + d;
            return d.Length == LargoDni ? d : null;
        }

        /// <summary>
        /// Carnet en su forma guardada: sin espacios, en mayúsculas y, si es solo dígitos con menos de 9,
        /// completado con ceros a la izquierda. Null si queda vacío. No valida el largo: ver
        /// <see cref="EsCarnetValido"/>.
        /// </summary>
        public static string NormalizarCarnet(string carnet)
        {
            string c = SinEspacios(carnet);
            if (c.Length == 0) return null;
            return RxSoloDigitos.IsMatch(c) && c.Length < DigitosCarnet ? c.PadLeft(DigitosCarnet, '0') : c;
        }

        /// <summary>
        /// ¿Lo que escribió el usuario es un carnet aceptable? De 6 a 11 letras, dígitos o guion
        /// (antes de completar ceros: "2719446" vale, "123" no).
        /// </summary>
        public static bool EsCarnetValido(string carnetEscrito)
        {
            string c = SinEspacios(carnetEscrito);
            return c.Length >= MinimoCarnet && c.Length <= LargoCarnet && RxCarnet.IsMatch(c)
                && c.Any(char.IsDigit);
        }
    }
}
