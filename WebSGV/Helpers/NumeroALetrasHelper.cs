using System;
using System.Globalization;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Convierte montos decimales a su representación en letras (español peruano),
    /// usado en documentos formales como Órdenes de Viaje / Liquidaciones.
    ///
    /// Ejemplo:
    ///   NumeroALetrasHelper.Convertir(1234.56m, "SOLES")
    ///   -> "MIL DOSCIENTOS TREINTA Y CUATRO CON 56/100 SOLES"
    /// </summary>
    public static class NumeroALetrasHelper
    {
        private static readonly string[] UNIDADES =
        {
            "", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
            "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE",
            "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE"
        };

        private static readonly string[] DECENAS =
        {
            "", "", "VEINTI", "TREINTA", "CUARENTA", "CINCUENTA",
            "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
        };

        private static readonly string[] CENTENAS =
        {
            "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
            "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
        };

        /// <summary>
        /// Convierte un monto a letras con el sufijo de moneda y los decimales en formato "XX/100".
        /// </summary>
        /// <param name="monto">Monto a convertir. Puede ser negativo (prefijo "MENOS").</param>
        /// <param name="moneda">Nombre de la moneda en mayúsculas. Ejemplos: "SOLES", "DÓLARES AMERICANOS".</param>
        public static string Convertir(decimal monto, string moneda)
        {
            bool negativo = monto < 0;
            decimal abs = Math.Abs(monto);

            long entero = (long)Math.Floor(abs);
            int decimales = (int)Math.Round((abs - entero) * 100m, MidpointRounding.AwayFromZero);
            if (decimales == 100) { entero += 1; decimales = 0; }

            string letras = ConvertirEntero(entero);
            if (string.IsNullOrEmpty(letras)) letras = "CERO";

            string signo = negativo ? "MENOS " : "";
            string dec = decimales.ToString("00", CultureInfo.InvariantCulture);
            string suf = string.IsNullOrEmpty(moneda) ? "" : " " + moneda;

            return signo + letras + " CON " + dec + "/100" + suf;
        }

        private static string ConvertirEntero(long n)
        {
            if (n == 0) return "";
            if (n < 0) return "";

            if (n >= 1000000)
            {
                long millones = n / 1000000;
                long resto = n % 1000000;
                string pref = (millones == 1) ? "UN MILLÓN" : ConvertirEntero(millones) + " MILLONES";
                return (resto > 0) ? pref + " " + ConvertirEntero(resto) : pref;
            }

            if (n >= 1000)
            {
                long miles = n / 1000;
                long resto = n % 1000;
                string pref = (miles == 1) ? "MIL" : ConvertirEntero(miles) + " MIL";
                return (resto > 0) ? pref + " " + ConvertirEntero(resto) : pref;
            }

            return Seccion((int)n);
        }

        private static string Seccion(int n)
        {
            if (n == 0) return "";
            if (n <= 20) return UNIDADES[n];

            if (n < 30)
            {
                int u = n - 20;
                return "VEINTI" + (u == 0 ? "" : UnidadMinuscula(u));
            }

            if (n < 100)
            {
                int d = n / 10;
                int u = n % 10;
                return DECENAS[d] + (u > 0 ? " Y " + UNIDADES[u] : "");
            }

            if (n == 100) return "CIEN";

            if (n < 1000)
            {
                int c = n / 100;
                int r = n % 100;
                return CENTENAS[c] + (r > 0 ? " " + Seccion(r) : "");
            }

            return n.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Para VEINTI + unidad se usa la unidad compuesta ("VEINTIUNO", "VEINTIDÓS", etc.).
        /// </summary>
        private static string UnidadMinuscula(int u)
        {
            switch (u)
            {
                case 1: return "UNO";
                case 2: return "DÓS";
                case 3: return "TRÉS";
                case 4: return "CUATRO";
                case 5: return "CINCO";
                case 6: return "SÉIS";
                case 7: return "SIETE";
                case 8: return "OCHO";
                case 9: return "NUEVE";
                default: return "";
            }
        }
    }
}
