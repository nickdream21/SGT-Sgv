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

        /// <summary>
        /// Saldo final de la liquidación, visto desde el conductor:
        /// <list type="bullet">
        /// <item>positivo: el conductor tiene dinero de la empresa y debe devolverlo;</item>
        /// <item>negativo: el conductor puso de su dinero (gastó más de lo entregado) y la empresa le debe;</item>
        /// <item>cero: cuadra.</item>
        /// </list>
        /// Un <b>descuento</b> al conductor (gasto no reconocido, faltante…) reduce lo que la empresa le
        /// debe o aumenta lo que él devuelve: suma. Un <b>reintegro</b> (la empresa le reconoce algo más)
        /// resta. Hasta el 2026-11-11 el sistema usaba los signos al revés.
        /// </summary>
        public static decimal SaldoFinal(decimal entregado, decimal gastos, decimal descuento, decimal reintegro) =>
            entregado - gastos + descuento - reintegro;

        /// <summary>"La empresa paga al conductor S/ 195.00", "El conductor devuelve a la empresa US$ 10.00"…</summary>
        public static string DescribirSaldo(decimal saldoSoles, decimal saldoDolares)
        {
            var partes = new System.Collections.Generic.List<string>();
            void Agregar(decimal saldo, string moneda)
            {
                if (saldo < 0) partes.Add("la empresa paga al conductor " + moneda + " " + Math.Abs(saldo).ToString("N2", CulturaPe));
                else if (saldo > 0) partes.Add("el conductor devuelve a la empresa " + moneda + " " + saldo.ToString("N2", CulturaPe));
            }
            Agregar(saldoSoles, "S/");
            Agregar(saldoDolares, "US$");
            if (partes.Count == 0) return "Liquidación cuadrada: nadie debe nada.";
            string texto = string.Join("; ", partes);
            return char.ToUpper(texto[0], CulturaPe) + texto.Substring(1) + ".";
        }

        private static readonly CultureInfo CulturaPe = CultureInfo.GetCultureInfo("es-PE");

        /// <summary>Valida el dinero entregado al conductor (null = no se envió, es válido).</summary>
        public static string ValidarIngresosEntregados(WebSGV.Models.Liquidaciones.IngresosEntregados ingresos)
        {
            if (ingresos == null) return null;
            foreach (decimal m in ingresos.Montos())
            {
                if (m < 0) return "Los montos entregados al conductor no pueden ser negativos.";
                if (m > MontoMaximoAjuste) return "Los montos no pueden superar S/ 9,999,999.";
            }
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
        /// Marcadores con los que el sistema agrega notas al final de <c>OrdenViaje.observaciones</c>
        /// (rechazo, reversión, corrección de ajustes), siempre en una línea nueva.
        /// </summary>
        private static readonly string[] MarcadoresNotaSistema =
            { "**RECHAZADO ", "[RECHAZO ", "[REVERSION ", "[CORRECCION AJUSTES ",
              "[OBSERVACION DEL CONDUCTOR ", "[RETIRO DEL ENVIO AL CONDUCTOR " };

        /// <summary>
        /// Devuelve solo lo que escribió el conductor en las observaciones de la orden, sin las
        /// notas que el sistema agrega al final. Se usa al precargar una liquidación rechazada:
        /// sin esto las notas volvían al formulario y se acumulaban en cada reenvío.
        /// </summary>
        public static string ObservacionesDelConductor(string observaciones)
        {
            if (string.IsNullOrEmpty(observaciones)) return string.Empty;

            int corte = observaciones.Length;
            foreach (string marcador in MarcadoresNotaSistema)
            {
                int i = observaciones.IndexOf("\n" + marcador, StringComparison.Ordinal);
                if (i >= 0 && i < corte) corte = i;
                if (observaciones.StartsWith(marcador, StringComparison.Ordinal)) corte = 0;
            }
            return observaciones.Substring(0, corte).TrimEnd();
        }

        /// <summary>
        /// Lo contrario de <see cref="ObservacionesDelConductor"/>: solo las notas que el sistema agregó
        /// (rechazo, reversión, retiro, observación...), para mostrarlas aparte en la revisión.
        /// </summary>
        public static string NotasDelSistema(string observaciones)
        {
            if (string.IsNullOrEmpty(observaciones)) return string.Empty;
            string delConductor = ObservacionesDelConductor(observaciones);
            return observaciones.Substring(delConductor.Length).Trim();
        }

        /// <summary>Tamaño máximo del PNG del trazo de una firma (el más grande registrado pesa ~23 KB).</summary>
        public const int MaximoBytesTrazoPng = 256 * 1024;

        /// <summary>
        /// Ancho y alto máximos del trazo (el lienzo de firma mide bastante menos; con pantallas de
        /// alta densidad puede crecer). El límite de <see cref="MaximoBytesTrazoPng"/> también protege.
        /// </summary>
        public const int MaximoPixelesTrazoPng = 4096;

        private static readonly byte[] FirmaPng = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        /// <summary>
        /// Decodifica el PNG del trazo de una firma en base64 (admite prefijo data URI
        /// "data:image/png;base64,"). Devuelve <c>null</c> si la cadena es vacía, no es base64
        /// válido o no es un trazo aceptable (<see cref="EsTrazoPngValido"/>).
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
                // Antes de decodificar: base64 ocupa 4 caracteres por cada 3 bytes.
                if (body.Length > (MaximoBytesTrazoPng / 3 + 1) * 4 + 64) return null;
                byte[] bytes = Convert.FromBase64String(body);
                return EsTrazoPngValido(bytes) ? bytes : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// El trazo es un PNG (cabecera 89 50 4E 47 0D 0A 1A 0A seguida del bloque IHDR), pesa como
        /// máximo <see cref="MaximoBytesTrazoPng"/> y mide como máximo
        /// <see cref="MaximoPixelesTrazoPng"/> de ancho y de alto. Las medidas se leen del IHDR sin
        /// decodificar la imagen (un PNG pequeño puede declarar millones de píxeles).
        /// </summary>
        public static bool EsTrazoPngValido(byte[] png)
        {
            // 8 (firma) + 4 (largo) + 4 ("IHDR") + 13 (datos) + 4 (CRC)
            if (png == null || png.Length < 33 || png.Length > MaximoBytesTrazoPng) return false;
            for (int i = 0; i < FirmaPng.Length; i++)
                if (png[i] != FirmaPng[i]) return false;

            if (LeerEnteroBigEndian(png, 8) != 13 || png[12] != (byte)'I' || png[13] != (byte)'H' ||
                png[14] != (byte)'D' || png[15] != (byte)'R')
                return false;

            long ancho = LeerEnteroBigEndian(png, 16);
            long alto = LeerEnteroBigEndian(png, 20);
            return ancho > 0 && alto > 0 && ancho <= MaximoPixelesTrazoPng && alto <= MaximoPixelesTrazoPng;
        }

        private static long LeerEnteroBigEndian(byte[] b, int i) =>
            ((long)b[i] << 24) | ((long)b[i + 1] << 16) | ((long)b[i + 2] << 8) | b[i + 3];

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
