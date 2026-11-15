using System;
using System.Security.Cryptography;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Token del enlace "Recuperar contraseña". El enlace del correo lleva el token en claro
    /// (32 bytes aleatorios de <see cref="RandomNumberGenerator"/> en base64url, 43 caracteres);
    /// en <c>Usuarios.resetToken</c> solo se guarda su SHA-256 en hex (64 caracteres), así quien
    /// lea la BD o un respaldo no puede usar un enlace pendiente.
    /// </summary>
    public static class TokenRecuperacion
    {
        /// <summary>Bytes aleatorios del token.</summary>
        public const int Bytes = 32;

        /// <summary>Nuevo token: (Token para el enlace, Hash para guardar en la BD).</summary>
        public static (string Token, string Hash) Generar()
        {
            byte[] datos = new byte[Bytes];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(datos);

            string token = Convert.ToBase64String(datos)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return (token, Hash(token));
        }

        /// <summary>SHA-256 del token (UTF-8) en hex minúsculas: lo que se guarda y se busca en la BD.</summary>
        public static string Hash(string token)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
