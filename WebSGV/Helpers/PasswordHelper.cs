using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Hash de contraseñas con PBKDF2 y salt aleatorio. Dos formatos guardados en Usuarios.contrasena:
    /// <list type="bullet">
    /// <item><c>sha256.{iteraciones}.{salt_base64}.{hash_base64}</c>: el actual (PBKDF2-HMAC-SHA256,
    /// <see cref="IteracionesObjetivo"/> iteraciones).</item>
    /// <item><c>{iteraciones}.{salt_base64}.{hash_base64}</c>: el anterior (PBKDF2-HMAC-SHA1, 10 000
    /// iteraciones). Se sigue verificando y se convierte al nuevo cuando la persona inicia sesión
    /// (<see cref="ActualizarHashSiCorresponde"/>).</item>
    /// </list>
    /// La derivación usa CNG (bcrypt.dll), unas 7 veces más rápida que Rfc2898DeriveBytes en x86;
    /// si no se puede cargar, usa Rfc2898DeriveBytes (mismo resultado byte a byte).
    /// </summary>
    public static class PasswordHelper
    {
        private const int SaltSize = 16;       // 128 bits
        private const int HashSize = 32;       // 256 bits
        private const string PrefijoSha256 = "sha256";

        /// <summary>Iteraciones de PBKDF2-SHA256 si Web.config no dice otra cosa (OWASP 2023).</summary>
        public const int IteracionesPorDefecto = 310000;
        /// <summary>Piso: un valor menor en Web.config se sube a este.</summary>
        public const int IteracionesMinimas = 100000;
        /// <summary>Un hash guardado con más iteraciones se rechaza (evita que un valor alterado cuelgue el servidor).</summary>
        public const int IteracionesMaximas = 5000000;

        /// <summary>Largo mínimo y máximo de una contraseña NUEVA (las existentes más cortas siguen entrando).</summary>
        public const int LargoMinimoNueva = 8;
        public const int LargoMaximoNueva = 128;

        public const string ClaveIteraciones = "Seguridad.IteracionesContrasena";
        public const string ClaveConvertirAlIngresar = "Seguridad.ConvertirHashAlIngresar";

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>
        /// Iteraciones con las que se generan los hashes nuevos: appSettings
        /// <c>Seguridad.IteracionesContrasena</c> (por defecto 310 000, mínimo 100 000, máximo 5 000 000).
        /// </summary>
        public static int IteracionesObjetivo
        {
            get
            {
                int valor;
                string texto = ConfigurationManager.AppSettings[ClaveIteraciones];
                if (!int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out valor))
                    valor = IteracionesPorDefecto;
                return Math.Min(IteracionesMaximas, Math.Max(IteracionesMinimas, valor));
            }
        }

        /// <summary>
        /// appSettings <c>Seguridad.ConvertirHashAlIngresar</c> (por defecto true). En false los hashes
        /// del formato anterior no se convierten al iniciar sesión (publicación en dos fases).
        /// </summary>
        public static bool ConvertirHashAlIngresar
        {
            get
            {
                bool valor;
                return !bool.TryParse(ConfigurationManager.AppSettings[ClaveConvertirAlIngresar], out valor) || valor;
            }
        }

        /// <summary>
        /// Genera el hash de una contraseña con salt aleatorio, en el formato
        /// <c>sha256.{iteraciones}.{salt_base64}.{hash_base64}</c>.
        /// </summary>
        public static string HashPassword(string password)
        {
            return HashPassword(password, IteracionesObjetivo);
        }

        /// <summary>Igual que <see cref="HashPassword(string)"/> con las iteraciones indicadas (pruebas).</summary>
        internal static string HashPassword(string password, int iteraciones)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));
            if (iteraciones < 1 || iteraciones > IteracionesMaximas)
                throw new ArgumentOutOfRangeException(nameof(iteraciones));

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            byte[] hash = Derivar(password, salt, iteraciones, HashSize, true);
            return PrefijoSha256 + "." + iteraciones.ToString(CultureInfo.InvariantCulture) + "."
                   + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Verifica si una contraseña coincide con el hash almacenado (formato actual o anterior).
        /// Un formato desconocido, texto plano o más de <see cref="IteracionesMaximas"/> iteraciones
        /// devuelven false sin excepción.
        /// </summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            HashGuardado h;
            if (!TryLeer(storedHash, out h) || h.Iteraciones > IteracionesMaximas)
                return false;

            byte[] calculado = Derivar(password, h.Salt, h.Iteraciones, h.Hash.Length, h.EsSha256);
            // Comparación en tiempo constante para evitar timing attacks
            return SlowEquals(h.Hash, calculado);
        }

        /// <summary>
        /// Verifica la contraseña tal como se escribió y, si no coincide y tiene espacios al inicio
        /// o al final, también recortada. Hoy todas las pantallas la guardan recortada, pero antes el
        /// cambio de contraseña y Gestión de usuarios no, así que quien la creó con un espacio al
        /// final (el autocompletado del celular lo agrega) no podía volver a entrar.
        /// Límite conocido: una guardada CON espacio final no entra si se escribe sin él.
        /// </summary>
        public static bool VerificarIngresada(string password, string storedHash)
        {
            string coincidente;
            return VerificarIngresada(password, storedHash, out coincidente);
        }

        /// <summary>
        /// Como <see cref="VerificarIngresada(string, string)"/>; <paramref name="coincidente"/> es la
        /// variante que coincidió (la escrita o la recortada) o null. Es la que hay que volver a
        /// hashear al convertir el formato.
        /// </summary>
        public static bool VerificarIngresada(string password, string storedHash, out string coincidente)
        {
            coincidente = null;
            if (VerifyPassword(password, storedHash))
            {
                coincidente = password;
                return true;
            }
            string recortada = password?.Trim();
            if (!string.IsNullOrEmpty(recortada) && recortada != password && VerifyPassword(recortada, storedHash))
            {
                coincidente = recortada;
                return true;
            }
            return false;
        }

        /// <summary>
        /// true si el hash guardado debe regenerarse: formato anterior (SHA1) o SHA256 con menos
        /// iteraciones que <see cref="IteracionesObjetivo"/>. Nunca baja las iteraciones.
        /// </summary>
        public static bool NecesitaRehash(string storedHash)
        {
            return NecesitaRehash(storedHash, IteracionesObjetivo);
        }

        internal static bool NecesitaRehash(string storedHash, int iteracionesObjetivo)
        {
            HashGuardado h;
            if (!TryLeer(storedHash, out h))
                return false;
            return !h.EsSha256 || h.Iteraciones < iteracionesObjetivo;
        }

        /// <summary>
        /// Login sin delatar qué usuarios existen: un usuario inexistente se verifica contra
        /// <paramref name="hashFicticio"/> (PBKDF2-SHA256 con las iteraciones objetivo), pero uno con
        /// hash del formato anterior (SHA1 10 000, ~30 ms) responde mucho antes. Si el hash guardado
        /// cuesta menos que el objetivo (formato anterior, menos iteraciones o un valor que no es hash)
        /// y no se pagó ya una derivación al objetivo (la conversión), verifica también contra
        /// <paramref name="hashFicticio"/> y descarta el resultado. Devuelve true si lo hizo.
        /// </summary>
        public static bool IgualarCostoVerificacion(string password, string storedHash, string hashFicticio)
        {
            if (!NecesitaRehash(storedHash) && !NeedsMigration(storedHash))
                return false;
            // VerificarIngresada como con el inexistente: con espacios al inicio o al final paga dos veces.
            VerificarIngresada(password, hashFicticio);
            return true;
        }

        /// <summary>
        /// true si el valor guardado NO es un hash de ninguno de los dos formatos (texto plano, vacío):
        /// lo usa Database/Scripts/migrar-contrasenas-texto-plano.ps1, que hashea el valor como si
        /// fuera la contraseña. Un hash del formato anterior o del actual nunca necesita migrarse.
        /// </summary>
        public static bool NeedsMigration(string storedHash)
        {
            HashGuardado h;
            return !TryLeer(storedHash, out h);
        }

        /// <summary>
        /// Valida una contraseña NUEVA (ya recortada por quien llama). Devuelve el mensaje para el
        /// usuario o null si sirve.
        /// </summary>
        public static string ValidarNueva(string nueva)
        {
            if (string.IsNullOrEmpty(nueva) || nueva.Length < LargoMinimoNueva)
                return "La contraseña debe tener al menos " + LargoMinimoNueva + " caracteres.";
            if (nueva.Length > LargoMaximoNueva)
                return "La contraseña no puede tener más de " + LargoMaximoNueva + " caracteres.";
            return null;
        }

        /// <summary>Texto del placeholder de los campos de contraseña nueva.</summary>
        public static string TextoMinimoNueva => "Mínimo " + LargoMinimoNueva + " caracteres";

        /// <summary>
        /// Tras un inicio de sesión correcto: si el hash guardado es del formato anterior (o tiene
        /// menos iteraciones que el objetivo) y la conversión está habilitada, guarda el hash nuevo de
        /// <paramref name="coincidente"/> (la variante que coincidió). El UPDATE exige que el hash
        /// siga siendo <paramref name="hashAnterior"/>: si otro proceso lo cambió entre medio, no
        /// toca nada. No cambia el sello de sesión ni requiereCambioContrasena (la contraseña es la
        /// misma). Devuelve true si actualizó la fila. Los errores de BD se propagan: quien llama
        /// decide (el login los registra y sigue).
        /// </summary>
        public static bool ActualizarHashSiCorresponde(int idUsuario, string hashAnterior, string coincidente)
        {
            if (idUsuario <= 0 || string.IsNullOrEmpty(coincidente) || string.IsNullOrEmpty(hashAnterior))
                return false;
            if (!ConvertirHashAlIngresar || !NecesitaRehash(hashAnterior))
                return false;

            int iteraciones = IteracionesObjetivo;
            var reloj = Stopwatch.StartNew();
            string nuevo = HashPassword(coincidente, iteraciones);
            long ms = reloj.ElapsedMilliseconds;

            int filas = DbHelper.EjecutarNonQuery(
                @"UPDATE Usuarios SET contrasena = @nuevo
                  WHERE idUsuario = @id AND contrasena = @anterior COLLATE Latin1_General_BIN2",
                DbHelper.Param("@nuevo", nuevo),
                DbHelper.Param("@id", idUsuario),
                DbHelper.Param("@anterior", hashAnterior));

            if (filas <= 0)
                return false;

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario,
                "Hash de contraseña actualizado a PBKDF2-SHA256 (" + iteraciones + " iteraciones)");
            LogSGV.Info("Hash de contraseña del usuario {IdUsuario} convertido a PBKDF2-SHA256 ({Iteraciones} iteraciones, {Milisegundos} ms)",
                idUsuario, iteraciones, ms);
            return true;
        }

        // ── Lectura del formato ────────────────────────────────────────────────

        private struct HashGuardado
        {
            public bool EsSha256;
            public int Iteraciones;
            public byte[] Salt;
            public byte[] Hash;
        }

        /// <summary>
        /// Lee cualquiera de los dos formatos. No aplica el tope de iteraciones (eso lo hace
        /// VerifyPassword): un valor con muchas iteraciones sigue siendo un hash, no texto plano.
        /// </summary>
        private static bool TryLeer(string valor, out HashGuardado h)
        {
            h = default(HashGuardado);
            if (string.IsNullOrEmpty(valor))
                return false;

            string[] partes = valor.Split('.');
            int inicio;
            if (partes.Length == 3)
            {
                inicio = 0;
                h.EsSha256 = false;
            }
            else if (partes.Length == 4 && string.Equals(partes[0], PrefijoSha256, StringComparison.Ordinal))
            {
                inicio = 1;
                h.EsSha256 = true;
            }
            else
            {
                return false;
            }

            int iteraciones;
            if (!int.TryParse(partes[inicio], NumberStyles.None, CultureInfo.InvariantCulture, out iteraciones) || iteraciones < 1)
                return false;

            try
            {
                h.Salt = Convert.FromBase64String(partes[inicio + 1]);
                h.Hash = Convert.FromBase64String(partes[inicio + 2]);
            }
            catch (FormatException)
            {
                return false;
            }

            // Rfc2898DeriveBytes exige salt de 8 bytes o más; el hash, entre 128 y 512 bits.
            if (h.Salt.Length < 8 || h.Salt.Length > 64 || h.Hash.Length < 16 || h.Hash.Length > 64)
                return false;

            h.Iteraciones = iteraciones;
            return true;
        }

        // ── Derivación PBKDF2 ──────────────────────────────────────────────────

        private static int _cngNoDisponible;

        /// <summary>PBKDF2-HMAC-SHA256 (o SHA1) de la contraseña en UTF-8. Interno para las pruebas con vectores conocidos.</summary>
        internal static byte[] Derivar(string password, byte[] salt, int iteraciones, int largo, bool sha256)
        {
            if (Volatile.Read(ref _cngNoDisponible) == 0)
            {
                byte[] bytes = Utf8.GetBytes(password);
                try
                {
                    return Pbkdf2Cng.Derivar(sha256 ? "SHA256" : "SHA1", bytes, salt, iteraciones, largo);
                }
                catch (DllNotFoundException ex) { MarcarCngNoDisponible(ex); }
                catch (EntryPointNotFoundException ex) { MarcarCngNoDisponible(ex); }
                // Estado distinto de 0 (algoritmo no disponible, parámetro rechazado): mismo respaldo.
                // Así HashFicticio (inicializador estático del login) nunca termina en TypeInitializationException.
                catch (CryptographicException ex) { MarcarCngNoDisponible(ex); }
                finally
                {
                    Array.Clear(bytes, 0, bytes.Length);
                }
            }

            // Rfc2898DeriveBytes(string, ...) convierte con UTF-8 sin BOM: mismo resultado que CNG.
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iteraciones,
                       sha256 ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1))
            {
                return pbkdf2.GetBytes(largo);
            }
        }

        private static void MarcarCngNoDisponible(Exception ex)
        {
            if (Interlocked.Exchange(ref _cngNoDisponible, 1) == 0)
                LogSGV.Error(ex, "PBKDF2 por CNG (bcrypt.dll) no disponible: se usa Rfc2898DeriveBytes (más lento)");
        }

        /// <summary>PBKDF2 de Windows CNG: BCryptDeriveKeyPBKDF2 con un proveedor HMAC.</summary>
        private static class Pbkdf2Cng
        {
            private const int BCRYPT_ALG_HANDLE_HMAC_FLAG = 0x8;

            [DllImport("bcrypt.dll", CharSet = CharSet.Unicode)]
            private static extern int BCryptOpenAlgorithmProvider(out IntPtr phAlgorithm, string pszAlgId, string pszImplementation, int dwFlags);

            [DllImport("bcrypt.dll")]
            private static extern int BCryptCloseAlgorithmProvider(IntPtr hAlgorithm, int dwFlags);

            [DllImport("bcrypt.dll")]
            private static extern int BCryptDeriveKeyPBKDF2(IntPtr hPrf, byte[] pbPassword, int cbPassword,
                byte[] pbSalt, int cbSalt, ulong cIterations, byte[] pbDerivedKey, int cbDerivedKey, int dwFlags);

            internal static byte[] Derivar(string algoritmo, byte[] password, byte[] salt, int iteraciones, int largo)
            {
                IntPtr alg;
                int estado = BCryptOpenAlgorithmProvider(out alg, algoritmo, null, BCRYPT_ALG_HANDLE_HMAC_FLAG);
                if (estado != 0)
                    throw new CryptographicException("BCryptOpenAlgorithmProvider devolvió 0x" + estado.ToString("X8", CultureInfo.InvariantCulture));
                try
                {
                    byte[] clave = new byte[largo];
                    estado = BCryptDeriveKeyPBKDF2(alg, password, password.Length, salt, salt.Length,
                        (ulong)iteraciones, clave, largo, 0);
                    if (estado != 0)
                        throw new CryptographicException("BCryptDeriveKeyPBKDF2 devolvió 0x" + estado.ToString("X8", CultureInfo.InvariantCulture));
                    return clave;
                }
                finally
                {
                    BCryptCloseAlgorithmProvider(alg, 0);
                }
            }
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
