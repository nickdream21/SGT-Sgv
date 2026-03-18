using System;
using System.Security.Cryptography;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Utilidad para hash seguro de contraseñas usando PBKDF2 (RFC 2898).
    /// Genera un salt aleatorio y lo almacena junto con el hash.
    /// </summary>
    public static class PasswordHelper
    {
        private const int SaltSize = 16;       // 128 bits
        private const int HashSize = 32;       // 256 bits
        private const int Iterations = 10000;  // Iteraciones PBKDF2

        /// <summary>
        /// Genera el hash de una contraseña con salt aleatorio.
        /// Retorna una cadena en formato: {iterations}.{salt_base64}.{hash_base64}
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // Generar salt aleatorio
            byte[] salt;
            using (var rng = new RNGCryptoServiceProvider())
            {
                salt = new byte[SaltSize];
                rng.GetBytes(salt);
            }

            // Generar hash con PBKDF2
            byte[] hash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                hash = pbkdf2.GetBytes(HashSize);
            }

            // Combinar: iteraciones.salt.hash
            return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>
        /// Verifica si una contraseña coincide con el hash almacenado.
        /// Soporta el formato nuevo (PBKDF2) y el formato antiguo (texto plano) para migración.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            // Verificar si es formato nuevo: {iterations}.{salt}.{hash}
            string[] parts = storedHash.Split('.');
            if (parts.Length == 3)
            {
                // Formato nuevo con hash PBKDF2
                int iterations;
                if (!int.TryParse(parts[0], out iterations))
                    return false;

                byte[] salt;
                byte[] storedHashBytes;
                try
                {
                    salt = Convert.FromBase64String(parts[1]);
                    storedHashBytes = Convert.FromBase64String(parts[2]);
                }
                catch (FormatException)
                {
                    return false;
                }

                // Recalcular hash con el mismo salt
                byte[] computedHash;
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
                {
                    computedHash = pbkdf2.GetBytes(storedHashBytes.Length);
                }

                // Comparación en tiempo constante para evitar timing attacks
                return SlowEquals(storedHashBytes, computedHash);
            }
            else
            {
                // Formato antiguo: texto plano (para migración gradual)
                return string.Equals(password, storedHash, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Determina si un hash almacenado necesita ser migrado al formato nuevo.
        /// </summary>
        public static bool NeedsMigration(string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
                return true;

            string[] parts = storedHash.Split('.');
            return parts.Length != 3;
        }

        /// <summary>
        /// Comparación en tiempo constante para prevenir ataques de timing.
        /// </summary>
        private static bool SlowEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}