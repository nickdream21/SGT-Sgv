using System;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Base32 de RFC 4648 (alfabeto A-Z, 2-7), el que usan las apps autenticadoras para el secreto TOTP.
    /// Codifica sin relleno; al decodificar acepta con o sin '=', mayúsculas o minúsculas y espacios.
    /// </summary>
    public static class Base32
    {
        private const string Alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>Codifica los bytes en Base32 sin '=' de relleno.</summary>
        public static string Codificar(byte[] datos)
        {
            if (datos == null) throw new ArgumentNullException(nameof(datos));
            var sb = new StringBuilder((datos.Length * 8 + 4) / 5);
            int buffer = 0, bits = 0;
            foreach (byte b in datos)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0)
                sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        /// <summary>
        /// Decodifica texto Base32. Ignora espacios y acepta sin relleno o con el '=' final exacto de RFC 4648;
        /// rechaza con <see cref="FormatException"/> cualquier otro carácter, un '=' en medio, un relleno de
        /// cantidad incorrecta, longitudes imposibles y bits finales distintos de cero.
        /// </summary>
        public static byte[] Decodificar(string texto)
        {
            if (texto == null) throw new ArgumentNullException(nameof(texto));

            var limpio = new StringBuilder(texto.Length);
            foreach (char c in texto)
                if (!char.IsWhiteSpace(c)) limpio.Append(c);
            string conRelleno = limpio.ToString();
            string s = conRelleno.TrimEnd('=');
            int relleno = conRelleno.Length - s.Length;

            int resto = s.Length % 8;
            if (resto == 1 || resto == 3 || resto == 6)
                throw new FormatException("Texto Base32 con una longitud inválida.");
            // Relleno: ninguno, o exactamente el de RFC 4648 para esa longitud (no se aceptan '=' de más ni de menos).
            if (relleno != 0 && relleno != RellenoEsperado(resto))
                throw new FormatException("Texto Base32 con un relleno '=' inválido.");

            var salida = new byte[s.Length * 5 / 8];
            int buffer = 0, bits = 0, i = 0;
            foreach (char c in s)
            {
                int valor = ValorDe(c);
                if (valor < 0)
                    throw new FormatException("Texto Base32 con un carácter inválido.");
                buffer = ((buffer << 5) | valor) & 0xFFF; // como mucho 7 bits pendientes + 5 nuevos
                bits += 5;
                if (bits >= 8)
                {
                    salida[i++] = (byte)(buffer >> (bits - 8));
                    bits -= 8;
                }
            }
            // Decodificación estricta: los bits sobrantes del último carácter deben ser cero
            // (así cada secuencia de bytes tiene una sola forma válida, p. ej. "MZ" no es "MY").
            if (bits > 0 && (buffer & ((1 << bits) - 1)) != 0)
                throw new FormatException("Texto Base32 con bits finales distintos de cero.");
            return salida;
        }

        private static int RellenoEsperado(int resto)
        {
            switch (resto)
            {
                case 2: return 6;
                case 4: return 4;
                case 5: return 3;
                case 7: return 1;
                default: return 0;
            }
        }

        private static int ValorDe(char c)
        {
            if (c >= 'A' && c <= 'Z') return c - 'A';
            if (c >= 'a' && c <= 'z') return c - 'a';
            if (c >= '2' && c <= '7') return c - '2' + 26;
            return -1;
        }
    }
}
