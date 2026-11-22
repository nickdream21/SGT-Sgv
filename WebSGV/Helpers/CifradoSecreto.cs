using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Cifra el secreto TOTP del segundo factor para guardarlo en la BD (E4-13), de modo que quien lea
    /// la BD o un respaldo no pueda generar códigos. AES-256-CBC con IV aleatorio y HMAC-SHA256
    /// (encrypt-then-MAC sobre <c>v1|iv|ct</c>). Las claves de cifrado y de MAC se derivan de la clave
    /// maestra con HMAC-SHA256 y etiquetas distintas: nunca se usa la misma clave para las dos cosas.
    /// Formato guardado: <c>v1.&lt;iv b64url&gt;.&lt;ct b64url&gt;.&lt;tag b64url&gt;</c>.
    /// La clave maestra es <c>Seguridad.ClaveSegundoFactor</c> (32 bytes en base64) de
    /// <c>appSettings.Secrets.config</c>; <see cref="DesdeConfiguracion"/> la valida al usarse.
    /// </summary>
    public sealed class CifradoSecreto
    {
        public const string ClaveConfiguracion = "Seguridad.ClaveSegundoFactor";
        public const string Version = "v1";
        public const int BytesClave = 32;

        private const int BytesIv = 16;
        private const int BytesTag = 32;
        private static readonly byte[] EtiquetaCifrado = Encoding.ASCII.GetBytes("SGV|segundo-factor|cifrado|v1");
        private static readonly byte[] EtiquetaMac = Encoding.ASCII.GetBytes("SGV|segundo-factor|mac|v1");

        private readonly byte[] _claveCifrado;
        private readonly byte[] _claveMac;

        /// <summary>Con la clave maestra directa (pruebas). Debe medir exactamente 32 bytes.</summary>
        public CifradoSecreto(byte[] claveMaestra)
        {
            if (claveMaestra == null) throw new ArgumentNullException(nameof(claveMaestra));
            if (claveMaestra.Length != BytesClave)
                throw new ArgumentException("La clave maestra del segundo factor debe medir " + BytesClave
                    + " bytes (mide " + claveMaestra.Length + ").", nameof(claveMaestra));

            using (var hmac = new HMACSHA256(claveMaestra))
            {
                _claveCifrado = hmac.ComputeHash(EtiquetaCifrado);
                _claveMac = hmac.ComputeHash(EtiquetaMac);
            }
        }

        /// <summary>
        /// Con la clave de <c>appSettings</c> <see cref="ClaveConfiguracion"/>. Lanza
        /// <see cref="InvalidOperationException"/> si falta, no es base64 o no mide 32 bytes.
        /// </summary>
        public static CifradoSecreto DesdeConfiguracion()
            => new CifradoSecreto(ClaveDesdeTexto(ConfigurationManager.AppSettings[ClaveConfiguracion]));

        /// <summary>Convierte el valor de configuración (base64) en la clave maestra; separado para probarlo.</summary>
        public static byte[] ClaveDesdeTexto(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                throw new InvalidOperationException("Falta la clave '" + ClaveConfiguracion
                    + "' en appSettings.Secrets.config (ver appSettings.Secrets.config.example).");
            byte[] clave;
            try { clave = Convert.FromBase64String(valor.Trim()); }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("La clave '" + ClaveConfiguracion + "' no es base64 válido.", ex);
            }
            if (clave.Length != BytesClave)
                throw new InvalidOperationException("La clave '" + ClaveConfiguracion + "' debe medir "
                    + BytesClave + " bytes en base64 (mide " + clave.Length + ").");
            return clave;
        }

        /// <summary>Cifra el secreto; cada llamada usa un IV nuevo, así el mismo secreto da textos distintos.</summary>
        public string Cifrar(byte[] secreto)
        {
            if (secreto == null) throw new ArgumentNullException(nameof(secreto));

            byte[] iv = new byte[BytesIv];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(iv);

            byte[] ct;
            using (var aes = CrearAes())
            using (var enc = aes.CreateEncryptor(_claveCifrado, iv))
                ct = enc.TransformFinalBlock(secreto, 0, secreto.Length);

            byte[] tag = CalcularTag(iv, ct);
            return Version + "." + Base64Url(iv) + "." + Base64Url(ct) + "." + Base64Url(tag);
        }

        /// <summary>
        /// Descifra lo que devolvió <see cref="Cifrar"/>. Verifica el tag en tiempo constante antes de
        /// descifrar; versión, formato o tag inválidos lanzan <see cref="CryptographicException"/>.
        /// </summary>
        public byte[] Descifrar(string cifrado)
        {
            if (cifrado == null) throw new ArgumentNullException(nameof(cifrado));
            string[] partes = cifrado.Split('.');
            if (partes.Length != 4)
                throw new CryptographicException("Secreto cifrado con formato inválido.");
            if (!string.Equals(partes[0], Version, StringComparison.Ordinal))
                throw new CryptographicException("Versión de secreto cifrado no reconocida.");

            byte[] iv = DesdeBase64Url(partes[1]);
            byte[] ct = DesdeBase64Url(partes[2]);
            byte[] tag = DesdeBase64Url(partes[3]);
            if (iv.Length != BytesIv || tag.Length != BytesTag || ct.Length == 0 || ct.Length % BytesIv != 0)
                throw new CryptographicException("Secreto cifrado con formato inválido.");

            if (!IgualesTiempoConstante(CalcularTag(iv, ct), tag))
                throw new CryptographicException("El secreto cifrado fue alterado o la clave no corresponde.");

            using (var aes = CrearAes())
            using (var dec = aes.CreateDecryptor(_claveCifrado, iv))
                return dec.TransformFinalBlock(ct, 0, ct.Length);
        }

        private byte[] CalcularTag(byte[] iv, byte[] ct)
        {
            byte[] version = Encoding.ASCII.GetBytes(Version + "|");
            var datos = new byte[version.Length + iv.Length + 1 + ct.Length];
            Buffer.BlockCopy(version, 0, datos, 0, version.Length);
            Buffer.BlockCopy(iv, 0, datos, version.Length, iv.Length);
            datos[version.Length + iv.Length] = (byte)'|';
            Buffer.BlockCopy(ct, 0, datos, version.Length + iv.Length + 1, ct.Length);
            using (var hmac = new HMACSHA256(_claveMac))
                return hmac.ComputeHash(datos);
        }

        private static Aes CrearAes()
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        private static bool IgualesTiempoConstante(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diferencia = 0;
            for (int i = 0; i < a.Length; i++)
                diferencia |= a[i] ^ b[i];
            return diferencia == 0;
        }

        private static string Base64Url(byte[] datos)
            => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] DesdeBase64Url(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                throw new CryptographicException("Secreto cifrado con formato inválido.");
            foreach (char c in texto)
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_'))
                    throw new CryptographicException("Secreto cifrado con formato inválido.");
            string s = texto.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
                case 1: throw new CryptographicException("Secreto cifrado con formato inválido.");
            }
            try { return Convert.FromBase64String(s); }
            catch (FormatException ex) { throw new CryptographicException("Secreto cifrado con formato inválido.", ex); }
        }
    }
}
