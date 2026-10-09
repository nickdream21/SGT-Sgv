using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WebSGV.Services.Exportacion
{
    /// <summary>
    /// Lectura tolerante de las fechas y horas del Excel de Seguimiento de Exportación
    /// (STATUS GENERAL VIVIANA y la plantilla de importación). Lógica pura, sin System.Web
    /// ni BD, para poder probarla con xUnit.
    ///
    /// En el Excel cada hito "F.H." viene como un par de columnas [fecha][hora], o como una
    /// sola celda con fecha y hora. Lo que se digita a mano trae errores que antes se
    /// importaban mal (hora a las 00:00, o la FECHA DE HOY cuando la hora era texto):
    ///   - hora como texto con tipeos: "12:!6", "11;11", "14:49,", "15:42:"
    ///   - hora como número "hh.mm" (9.53 = 09:53) o "hhmm" (1810 = 18:10)
    ///   - fecha con hora en la misma celda: "12/12/25 11.39"
    ///   - fecha con barra faltante: "10/072026"
    ///   - guiones "-" como "no aplica"
    /// Lo que no se puede leer con seguridad se rechaza con un motivo (nunca se inventa).
    /// </summary>
    public static class FechaHoraExcel
    {
        /// <summary>Rango de años admitido: fuera de él es un error de digitación (p. ej. 2152).</summary>
        public const int AnioMinimo = 2023;
        public const int AnioMaximo = 2035;

        // Serial de Excel: 1 = 01/01/1900. Se acepta solo el rango de años válido.
        private static readonly double SerialMinimo = new DateTime(AnioMinimo, 1, 1).ToOADate();
        private static readonly double SerialMaximo = new DateTime(AnioMaximo, 12, 31).AddDays(1).ToOADate();

        private static readonly string[] FormatosFecha =
        {
            "d/M/yyyy", "d/M/yy", "d-M-yyyy", "d-M-yy", "d.M.yyyy", "yyyy-M-d", "yyyy/M/d"
        };

        /// <summary>Resultado de combinar la celda de fecha y la de hora de un hito.</summary>
        public sealed class Resultado
        {
            /// <summary>Fecha y hora leídas; null si la celda estaba vacía o no se pudo leer.</summary>
            public DateTime? Valor { get; set; }
            /// <summary>Motivo del rechazo (null si se leyó bien o estaba vacía).</summary>
            public string Error { get; set; }
            /// <summary>Hora leída cuando falta la fecha (para deducirla con <see cref="DeducirFecha"/>).</summary>
            public TimeSpan? HoraSinFecha { get; set; }
        }

        /// <summary>
        /// Deduce la fecha de un hito que solo tiene hora, usando el hito anterior y el siguiente
        /// del viaje: la fecha es la del hito anterior o el día siguiente, y debe quedar entre
        /// ambos. Solo devuelve valor si exactamente UNA opción respeta ese orden y hay los dos
        /// hitos de referencia (con uno solo, casi siempre hay dos opciones válidas).
        /// Ej.: S. Planta 03/06 10:21, LL. Base "22:13" sin fecha, S. Base 04/06 05:26 → 03/06 22:13.
        /// </summary>
        public static DateTime? DeducirFecha(TimeSpan hora, DateTime? anterior, DateTime? siguiente)
        {
            if (!anterior.HasValue || !siguiente.HasValue || siguiente < anterior) return null;

            DateTime? unica = null;
            int validas = 0;
            foreach (var dia in new[] { anterior.Value.Date, anterior.Value.Date.AddDays(1) })
            {
                DateTime candidata = dia + hora;
                if (candidata >= anterior.Value && candidata <= siguiente.Value)
                {
                    validas++;
                    unica = candidata;
                }
            }
            return validas == 1 ? unica : null;
        }

        /// <summary>
        /// Combina la celda de fecha (obligatoria para que haya valor) y la de hora (opcional).
        /// Sin hora, el hito queda a las 00:00 (igual que fecha + hora vacía en Excel), salvo que
        /// la propia celda de fecha traiga la hora.
        /// </summary>
        public static Resultado Combinar(object celdaFecha, object celdaHora)
        {
            if (EsVacio(celdaFecha))
            {
                // Hora sin fecha: por sí sola no se ubica en el tiempo (ver DeducirFecha).
                if (EsVacio(celdaHora)) return new Resultado();
                var r = new Resultado { Error = "hay hora pero falta la fecha" };
                if (TryLeerHora(celdaHora, out TimeSpan h, out _)) r.HoraSinFecha = h;
                return r;
            }

            if (!TryLeerFecha(celdaFecha, out DateTime fecha, out string errFecha))
                return new Resultado { Error = errFecha };

            if (EsVacio(celdaHora))
                return new Resultado { Valor = fecha };

            if (!TryLeerHora(celdaHora, out TimeSpan hora, out string errHora))
                return new Resultado { Error = errHora };

            return new Resultado { Valor = fecha.Date + hora };
        }

        /// <summary>
        /// Lee una celda de fecha. Acepta DateTime, número de serie de Excel (con o sin fracción
        /// de hora) y texto dd/mm/aa(aa) con hora opcional. Conserva la hora si la trae.
        /// </summary>
        public static bool TryLeerFecha(object valor, out DateTime fecha, out string error)
        {
            fecha = default(DateTime);
            error = null;
            if (EsVacio(valor)) { error = "vacía"; return false; }

            if (valor is DateTime dt)
                return Validar(dt, out fecha, out error);

            if (valor is double || valor is int || valor is decimal)
            {
                double d = Convert.ToDouble(valor, CultureInfo.InvariantCulture);
                if (d < SerialMinimo || d >= SerialMaximo)
                {
                    error = $"fecha fuera de rango ({d.ToString(CultureInfo.InvariantCulture)})";
                    return false;
                }
                return Validar(DateTime.FromOADate(d), out fecha, out error);
            }

            string texto = Convert.ToString(valor, CultureInfo.InvariantCulture).Trim();

            // Número como texto (serial)
            if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial))
                return TryLeerFecha(serial, out fecha, out error);

            // "10/072026" -> "10/07/2026" (falta la segunda barra)
            var sinBarra = Regex.Match(texto, @"^(\d{1,2})/(\d{2})(\d{4})$");
            if (sinBarra.Success)
                texto = $"{sinBarra.Groups[1].Value}/{sinBarra.Groups[2].Value}/{sinBarra.Groups[3].Value}";

            // Fecha y hora en la misma celda: "12/12/25 11.39" o "12/12/2025 11:39"
            string parteHora = null;
            var conHora = Regex.Match(texto, @"^(\S+)\s+(\S+)$");
            if (conHora.Success)
            {
                texto = conHora.Groups[1].Value;
                parteHora = conHora.Groups[2].Value;
            }

            if (!DateTime.TryParseExact(texto, FormatosFecha, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime soloFecha))
            {
                error = $"fecha no reconocida (\"{Convert.ToString(valor, CultureInfo.InvariantCulture).Trim()}\")";
                return false;
            }

            if (parteHora != null)
            {
                if (!TryLeerHora(parteHora, out TimeSpan h, out error)) return false;
                soloFecha = soloFecha.Date + h;
            }
            return Validar(soloFecha, out fecha, out error);
        }

        /// <summary>
        /// Lee una celda de hora. Acepta TimeSpan, DateTime (toma la hora), fracción de día (0..1),
        /// fecha-hora completa (toma la hora), "hh.mm" (9.53), "hhmm" (1810) y texto "hh:mm"
        /// con tipeos frecuentes corregidos ("!" por "1", ";" "," "." por ":").
        /// </summary>
        public static bool TryLeerHora(object valor, out TimeSpan hora, out string error)
        {
            hora = default(TimeSpan);
            error = null;
            if (EsVacio(valor)) { error = "vacía"; return false; }

            if (valor is TimeSpan ts)
            {
                if (ts < TimeSpan.Zero || ts >= TimeSpan.FromDays(1)) { error = "hora fuera de rango"; return false; }
                hora = Redondear(ts);
                return true;
            }

            if (valor is DateTime dt)
            {
                // Una fecha real (no la base 1899/1900 de una celda de hora) sin hora no aporta hora.
                if (dt.Year >= AnioMinimo && dt.TimeOfDay.TotalMinutes < 0.5)
                {
                    error = "en la columna de hora hay una fecha sin hora";
                    return false;
                }
                hora = Redondear(dt.TimeOfDay);
                if (hora >= TimeSpan.FromDays(1)) hora = new TimeSpan(23, 59, 0);
                return true;
            }

            if (valor is double || valor is int || valor is decimal)
                return TryHoraNumero(Convert.ToDouble(valor, CultureInfo.InvariantCulture), out hora, out error);

            string original = Convert.ToString(valor, CultureInfo.InvariantCulture).Trim();
            string texto = original.Replace('!', '1').Replace(';', ':').Replace(',', ':').TrimEnd(':', '.', ' ');

            // Número como texto: "9.53", "1810", "0.72"
            if (Regex.IsMatch(texto, @"^\d+(\.\d+)?$") &&
                double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                return TryHoraNumero(n, out hora, out error);

            // "hh:mm", "hh.mm" o "hh:mm:ss"
            var m = Regex.Match(texto.Replace('.', ':'), @"^(\d{1,2}):(\d{2})(?::(\d{2}))?$");
            if (m.Success)
            {
                int h = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int mi = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                int s = m.Groups[3].Success ? int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) : 0;
                if (h < 24 && mi < 60 && s < 60)
                {
                    hora = new TimeSpan(h, mi, 0);
                    return true;
                }
            }

            error = $"hora no reconocida (\"{original}\")";
            return false;
        }

        private static bool TryHoraNumero(double d, out TimeSpan hora, out string error)
        {
            hora = default(TimeSpan);
            error = null;

            if (d >= 0 && d < 1)
            {
                // Fracción de día (formato hora de Excel)
                hora = Redondear(TimeSpan.FromDays(d));
                if (hora >= TimeSpan.FromDays(1)) hora = new TimeSpan(23, 59, 0);
                return true;
            }

            if (d >= SerialMinimo && d < SerialMaximo)
            {
                // Fecha-hora completa en la columna de hora: vale solo si trae hora.
                double frac = d - Math.Floor(d);
                if (frac * 24 * 60 < 0.5)
                {
                    error = "en la columna de hora hay una fecha sin hora";
                    return false;
                }
                hora = Redondear(TimeSpan.FromDays(frac));
                if (hora >= TimeSpan.FromDays(1)) hora = new TimeSpan(23, 59, 0);
                return true;
            }

            if (d >= 1 && d < 24)
            {
                // "hh.mm" digitado como número: 9.53 = 09:53, 21 = 21:00
                int h = (int)Math.Floor(d);
                int mi = (int)Math.Round((d - h) * 100, MidpointRounding.AwayFromZero);
                if (mi < 60)
                {
                    hora = new TimeSpan(h, mi, 0);
                    return true;
                }
            }
            else if (d >= 100 && d < 2400 && d == Math.Floor(d))
            {
                // "hhmm" sin separador: 1810 = 18:10
                int h = (int)(d / 100), mi = (int)(d % 100);
                if (mi < 60)
                {
                    hora = new TimeSpan(h, mi, 0);
                    return true;
                }
            }

            error = $"hora no reconocida ({d.ToString(CultureInfo.InvariantCulture)})";
            return false;
        }

        private static bool Validar(DateTime dt, out DateTime fecha, out string error)
        {
            fecha = default(DateTime);
            error = null;
            if (dt.Year < AnioMinimo || dt.Year > AnioMaximo)
            {
                error = $"año fuera de rango ({dt.Year})";
                return false;
            }
            fecha = new DateTime(dt.Year, dt.Month, dt.Day) + Redondear(dt.TimeOfDay);
            return true;
        }

        /// <summary>Redondea al minuto (Excel guarda la hora con error de coma flotante).</summary>
        private static TimeSpan Redondear(TimeSpan t) =>
            TimeSpan.FromMinutes(Math.Round(t.TotalMinutes, MidpointRounding.AwayFromZero));

        /// <summary>Celda vacía o con un marcador de "no aplica" ("-", "—", "N/A").</summary>
        public static bool EsVacio(object valor)
        {
            if (valor == null || valor is DBNull) return true;
            if (!(valor is string s)) return false;
            s = s.Trim();
            return s.Length == 0 || s == "-" || s == "—" || s == "--" ||
                   s.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("NA", StringComparison.OrdinalIgnoreCase);
        }
    }
}
