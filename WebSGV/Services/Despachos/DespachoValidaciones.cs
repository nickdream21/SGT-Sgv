using System;
using System.Globalization;
using System.Text.RegularExpressions;
using WebSGV.Services.Common;

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

        /// <summary>
        /// Fecha ISO estricta (yyyy-MM-dd) que escribió el usuario; si no lo es, <see cref="ErrorNegocioException"/>
        /// con el nombre del campo (antes un TryParse fallido guardaba 0001-01-01 o un Parse lanzaba un error genérico).
        /// </summary>
        public static DateTime LeerFechaIso(string valor, string campo)
        {
            if (!TryParseFechaIso((valor ?? "").Trim(), out DateTime fecha))
                throw new ErrorNegocioException($"La fecha de «{campo}» no es válida.");
            return fecha;
        }

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

        /// <summary>Clave de Web.config con los días hacia atrás que acepta "Programar con el Word".</summary>
        public const string ClaveDiasAtrasProgramacionWord = "Despachos.DiasAtrasProgramacionWord";

        /// <summary>Días hacia atrás por defecto (y hacia adelante, siempre) de la fecha de carga del Word.</summary>
        public const int DiasProgramacionWordPorDefecto = 7;

        /// <summary>
        /// Días hacia atrás de la fecha de carga del Word según el texto de Web.config: un entero entre 1 y 60;
        /// vacío, no numérico o fuera de rango da <see cref="DiasProgramacionWordPorDefecto"/>.
        /// </summary>
        public static int DiasAtrasProgramacionWord(string valorConfig) =>
            int.TryParse((valorConfig ?? "").Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int dias)
            && dias >= 1 && dias <= 60
                ? dias
                : DiasProgramacionWordPorDefecto;

        /// <summary>
        /// Fecha de carga del Word dentro de [hoy - <paramref name="diasAtras"/>, hoy + 7]. Hacia adelante
        /// queda fijo en 7: ampliar la ventana solo sirve para regularizar días pasados.
        /// </summary>
        public static bool FechaCargaWordEsValida(DateTime fecha, DateTime hoy, int diasAtras) =>
            fecha.Date >= hoy.Date.AddDays(-diasAtras) &&
            fecha.Date <= hoy.Date.AddDays(DiasProgramacionWordPorDefecto);

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
