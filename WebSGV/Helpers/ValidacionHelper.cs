using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Validaciones de entrada reutilizables del lado servidor.
    ///
    /// Consolida los patrones regex que estaban repetidos y desperdigados en las
    /// páginas (DNI, placa, nombres, teléfono, número de pedido/orden, texto de
    /// búsqueda, montos). El objetivo es que todas las páginas validen igual y que
    /// un cambio de regla se haga en un solo lugar.
    ///
    /// Todos los métodos hacen <c>Trim()</c> internamente. Los validadores de
    /// formato devuelven <c>false</c> para entradas nulas o vacías (es decir,
    /// validan presencia + formato), salvo <see cref="EsTextoBusquedaSeguro"/>,
    /// que admite cadena vacía por usarse en filtros opcionales.
    ///
    /// Uso:
    /// <code>
    ///   if (!ValidacionHelper.EsPlaca(txtPlaca.Text)) { MostrarMensaje("Placa inválida."); return; }
    ///   if (!ValidacionHelper.EsMonto(txtMonto.Text, out decimal monto)) { ... }
    /// </code>
    /// </summary>
    public static class ValidacionHelper
    {
        // Regex compilados una sola vez. Los rangos de caracteres replican los
        // patrones ya usados en RegistroChoferes, RegistroTractos, RegistroClientes, etc.
        private static readonly Regex RxDni = new Regex(@"^\d{8}$", RegexOptions.Compiled);
        private static readonly Regex RxTelefono = new Regex(@"^\d{7,9}$", RegexOptions.Compiled);
        private static readonly Regex RxPlaca = new Regex(@"^[A-Z0-9-]{6,10}$", RegexOptions.Compiled);
        private static readonly Regex RxDocumento = new Regex(@"^[A-Za-z0-9-]{6,12}$", RegexOptions.Compiled);
        private static readonly Regex RxNumeroPedido = new Regex(@"^\d{10}$", RegexOptions.Compiled);
        private static readonly Regex RxNumeroOrden = new Regex(@"^[A-Za-z0-9_/-]+$", RegexOptions.Compiled);

        // Solo letras (con acentos/ñ) y espacios — para nombres y apellidos de persona.
        private static readonly Regex RxNombrePersona =
            new Regex(@"^[A-Za-zÁÉÍÓÚÑáéíóúÜü\s]{2,100}$", RegexOptions.Compiled);

        // Letras, números, espacios y algunos signos — para razón social / nombres genéricos.
        private static readonly Regex RxNombreGenerico =
            new Regex(@"^[A-Za-zÁÉÍÓÚÑáéíóúÜü0-9\s\-\.,&]{3,200}$", RegexOptions.Compiled);

        // Texto admitido en filtros de búsqueda (permite vacío).
        private static readonly Regex RxTextoBusqueda =
            new Regex(@"^[A-Za-z0-9áéíóúÁÉÍÓÚñÑüÜ\s\-\.]*$", RegexOptions.Compiled);

        /// <summary>DNI peruano: exactamente 8 dígitos.</summary>
        public static bool EsDni(string valor) => Coincide(RxDni, valor);

        /// <summary>Teléfono: 7 a 9 dígitos.</summary>
        public static bool EsTelefono(string valor) => Coincide(RxTelefono, valor);

        /// <summary>Placa de tracto/semirremolque: 6 a 10 caracteres (letras, números o guion).</summary>
        public static bool EsPlaca(string valor) => Coincide(RxPlaca, NormalizarMayus(valor));

        /// <summary>Documento de identidad alternativo (carnet de extranjería / pasaporte): 6 a 12 alfanuméricos o guion.</summary>
        public static bool EsDocumentoIdentidad(string valor) => Coincide(RxDocumento, valor);

        /// <summary>Número de pedido: exactamente 10 dígitos.</summary>
        public static bool EsNumeroPedido(string valor) => Coincide(RxNumeroPedido, valor);

        /// <summary>Número de orden de viaje: alfanumérico con guion, guion bajo o barra.</summary>
        public static bool EsNumeroOrden(string valor) => Coincide(RxNumeroOrden, valor);

        /// <summary>Nombre o apellido de persona: solo letras y espacios, 2 a 100 caracteres.</summary>
        public static bool EsNombrePersona(string valor) => Coincide(RxNombrePersona, valor);

        /// <summary>Nombre genérico / razón social: letras, números y signos básicos, 3 a 200 caracteres.</summary>
        public static bool EsNombreGenerico(string valor) => Coincide(RxNombreGenerico, valor);

        /// <summary>
        /// Texto seguro para filtros de búsqueda. Admite cadena vacía (filtro
        /// opcional) y rechaza caracteres que podrían usarse para inyección.
        /// </summary>
        public static bool EsTextoBusquedaSeguro(string valor, int maxLongitud = 100)
        {
            if (valor == null) return true;
            valor = valor.Trim();
            if (valor.Length > maxLongitud) return false;
            return RxTextoBusqueda.IsMatch(valor);
        }

        /// <summary>
        /// Valida un monto en formato decimal (cultura es-PE) y lo devuelve.
        /// Acepta vacío como 0. Rechaza valores negativos.
        /// </summary>
        public static bool EsMonto(string valor, out decimal monto)
        {
            monto = 0m;
            if (string.IsNullOrWhiteSpace(valor)) return true;

            valor = valor.Trim();
            var cultura = CultureInfo.GetCultureInfo("es-PE");
            const NumberStyles estilos = NumberStyles.Number;

            if (!decimal.TryParse(valor, estilos, cultura, out monto) &&
                !decimal.TryParse(valor, estilos, CultureInfo.InvariantCulture, out monto))
            {
                monto = 0m;
                return false;
            }

            return monto >= 0m;
        }

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
