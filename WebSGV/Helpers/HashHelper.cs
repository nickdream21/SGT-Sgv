using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Utilidades de hashing SHA-256 para la integridad documental del sistema.
    /// Se usa para:
    ///   - Calcular el hash del PDF de Orden de Viaje al firmar.
    ///   - Recalcular el hash para verificar integridad (pantalla de verificación).
    ///   - Derivar hashes de cadenas canónicas (datos serializados de la firma).
    /// </summary>
    public static class HashHelper
    {
        /// <summary>
        /// Calcula el SHA-256 de un arreglo de bytes y lo devuelve como string hex en minúsculas (64 chars).
        /// </summary>
        public static string ComputeSha256(byte[] contenido)
        {
            if (contenido == null) throw new ArgumentNullException("contenido");

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(contenido);
                return ToHex(hash);
            }
        }

        /// <summary>
        /// Calcula el SHA-256 del contenido leído desde un stream (por ejemplo un PDF en disco).
        /// El stream se consume completamente. No se cierra automáticamente.
        /// </summary>
        public static string ComputeSha256(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                return ToHex(hash);
            }
        }

        /// <summary>
        /// Calcula el SHA-256 del contenido de un archivo en disco.
        /// </summary>
        public static string ComputeSha256FromFile(string rutaArchivo)
        {
            if (string.IsNullOrWhiteSpace(rutaArchivo)) throw new ArgumentNullException("rutaArchivo");
            if (!File.Exists(rutaArchivo)) throw new FileNotFoundException("No se encontró el archivo.", rutaArchivo);

            using (var fs = new FileStream(rutaArchivo, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ComputeSha256(fs);
            }
        }

        /// <summary>
        /// Calcula el SHA-256 de una cadena, codificada en UTF-8 sin BOM.
        /// Útil para "hash de datos canónicos" (concatenación ordenada de campos).
        /// </summary>
        public static string ComputeSha256(string texto)
        {
            if (texto == null) throw new ArgumentNullException("texto");

            byte[] bytes = new UTF8Encoding(false).GetBytes(texto);
            return ComputeSha256(bytes);
        }

        /// <summary>
        /// Compara dos hashes hex en tiempo constante (evita ataques de timing).
        /// Devuelve true si son iguales (ignorando mayúsculas/minúsculas).
        /// </summary>
        public static bool HashesIguales(string hashA, string hashB)
        {
            if (hashA == null || hashB == null) return false;
            if (hashA.Length != hashB.Length) return false;

            int diff = 0;
            for (int i = 0; i < hashA.Length; i++)
            {
                diff |= char.ToLowerInvariant(hashA[i]) ^ char.ToLowerInvariant(hashB[i]);
            }
            return diff == 0;
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
}
