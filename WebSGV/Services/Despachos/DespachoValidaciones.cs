using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WebSGV.Services.Despachos
{
    /// <summary>
    /// Validaciones puras (sin dependencias de System.Web ni BD) del módulo de
    /// Registro de Despacho. Extraídas de <c>Views/RegistroDespacho.aspx.cs</c>
    /// para poder probarlas con xUnit. El code-behind conserva métodos
    /// adaptadores que delegan aquí.
    /// </summary>
    public static class DespachoValidaciones
    {
        /// <summary>Parsea una fecha en formato ISO estricto (yyyy-MM-dd).</summary>
        public static bool TryParseFechaIso(string valor, out DateTime fecha) =>
            DateTime.TryParseExact(valor, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

        /// <summary>El número de pedido debe ser exactamente 10 dígitos.</summary>
        public static bool ValidarNumeroPedido(string numeroPedido) =>
            numeroPedido != null && Regex.IsMatch(numeroPedido, @"^\d{10}$");

        /// <summary>
        /// Fecha de despacho válida: ISO (yyyy-MM-dd), año entre 2000 y 2100, y dentro
        /// de la ventana [-365, +30] días respecto a <paramref name="hoy"/>.
        /// </summary>
        public static bool FechaDespachoEsValida(string valor, DateTime hoy)
        {
            return TryParseFechaIso(valor, out DateTime fecha) &&
                   fecha.Year >= 2000 &&
                   fecha.Year <= 2100 &&
                   fecha.Date >= hoy.Date.AddDays(-365) &&
                   fecha.Date <= hoy.Date.AddDays(30);
        }

        /// <summary>
        /// Fecha de emisión (factura/CPIC) válida: ISO (yyyy-MM-dd), año entre 2000 y
        /// 2100, y no posterior a <paramref name="hoy"/>.
        /// </summary>
        public static bool FechaEmisionEsValida(string valor, DateTime hoy)
        {
            return TryParseFechaIso(valor, out DateTime fecha) &&
                   fecha.Year >= 2000 &&
                   fecha.Year <= 2100 &&
                   fecha.Date <= hoy.Date;
        }
    }
}
