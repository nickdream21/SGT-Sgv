using System.Text.RegularExpressions;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Validaciones de entrada reutilizables del lado servidor. Hoy solo la placa: los demás validadores
    /// (DNI, teléfono, documento, pedido, orden, nombres, texto de búsqueda) no tenían ninguna llamada y se
    /// borraron en E6-20 (M-174); los montos se leen con MontoHelper.LeerMonto.
    ///
    /// Uso:
    /// <code>
    ///   if (!ValidacionHelper.EsPlaca(txtPlaca.Text)) { MostrarMensaje("Placa inválida."); return; }
    /// </code>
    /// </summary>
    public static class ValidacionHelper
    {
        private static readonly Regex RxPlaca = new Regex(@"^[A-Z0-9-]{6,10}$", RegexOptions.Compiled);

        /// <summary>Placa de tracto/semirremolque: 6 a 10 caracteres (letras, números o guion), sin espacios a los lados; false si está vacía.</summary>
        public static bool EsPlaca(string valor) => Coincide(RxPlaca, NormalizarMayus(valor));

        private static bool Coincide(Regex rx, string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return false;
            return rx.IsMatch(valor.Trim());
        }

        private static string NormalizarMayus(string valor)
        {
            return valor == null ? null : valor.Trim().ToUpperInvariant();
        }
    }
}
