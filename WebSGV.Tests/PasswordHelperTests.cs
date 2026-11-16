using System;
using System.Security.Cryptography;
using System.Text;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class PasswordHelperTests
    {
        /// <summary>Hash del formato anterior, armado como lo hacía PasswordHelper antes (PBKDF2-SHA1, 10 000).</summary>
        private static string HashFormatoAnterior(string password, int iteraciones = 10000)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iteraciones))
                return iteraciones + "." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(pbkdf2.GetBytes(32));
        }

        private static string Hex(byte[] b) => BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();

        // ── Derivación (CNG) ────────────────────────────────────────────────────

        [Fact]
        public void Derivar_Sha256_VectorConocido()
        {
            // PBKDF2-HMAC-SHA256, P="password", S="salt", c=4096, dkLen=32 (RFC 7914 / vectores públicos).
            byte[] dk = PasswordHelper.Derivar("password", Encoding.ASCII.GetBytes("salt"), 4096, 32, true);
            Assert.Equal("c5e478d59288c841aa530db6845c4c8d962893a001ce4e11a4963873aa98134a", Hex(dk));
        }

        [Fact]
        public void Derivar_Sha1_VectorRfc6070()
        {
            // RFC 6070: P="password", S="salt", c=4096, dkLen=20.
            byte[] dk = PasswordHelper.Derivar("password", Encoding.ASCII.GetBytes("salt"), 4096, 20, false);
            Assert.Equal("4b007901b765489abead49d926f721d065a429c1", Hex(dk));
        }

        [Fact]
        public void Derivar_IgualQueRfc2898ConAcentos()
        {
            // La contraseña va a bytes con UTF-8 sin BOM, igual que Rfc2898DeriveBytes(string, ...).
            byte[] salt = Encoding.ASCII.GetBytes("sal-de-16-bytes!");
            const string clave = "Contraseña Ñandú €";
            using (var k = new Rfc2898DeriveBytes(clave, salt, 1000, HashAlgorithmName.SHA256))
                Assert.Equal(Hex(k.GetBytes(32)), Hex(PasswordHelper.Derivar(clave, salt, 1000, 32, true)));
            using (var k = new Rfc2898DeriveBytes(clave, salt, 1000))
                Assert.Equal(Hex(k.GetBytes(32)), Hex(PasswordHelper.Derivar(clave, salt, 1000, 32, false)));
        }

        // ── HashPassword ────────────────────────────────────────────────────────

        [Fact]
        public void HashPassword_RetornaFormatoCorrecto()
        {
            string hash = PasswordHelper.HashPassword("miPassword");
            string[] partes = hash.Split('.');
            Assert.Equal(4, partes.Length);
            Assert.Equal("sha256", partes[0]);
            Assert.Equal(PasswordHelper.IteracionesObjetivo.ToString(), partes[1]);
            Assert.Equal(16, Convert.FromBase64String(partes[2]).Length);
            Assert.Equal(32, Convert.FromBase64String(partes[3]).Length);
            Assert.StartsWith("sha256.", hash);
            Assert.True(hash.Length <= 100, "Usuarios.contrasena es varchar(100); largo: " + hash.Length);
            Assert.True(PasswordHelper.VerifyPassword("miPassword", hash));
        }

        [Fact]
        public void IteracionesObjetivo_PorDefecto310000()
        {
            // Las pruebas no tienen Web.config: vale el valor por defecto.
            Assert.Equal(310000, PasswordHelper.IteracionesObjetivo);
            Assert.True(PasswordHelper.ConvertirHashAlIngresar);
        }

        [Fact]
        public void HashPassword_ConElMaximoDeIteraciones_CabeEnLaColumna()
        {
            string hash = PasswordHelper.HashPassword("x", 1) // largo sin depender de las iteraciones reales
                .Replace("sha256.1.", "sha256." + PasswordHelper.IteracionesMaximas + ".");
            Assert.True(hash.Length <= 100);
        }

        [Fact]
        public void HashPassword_MismaPasswordGeneraHashesDiferentes()
        {
            string h1 = PasswordHelper.HashPassword("password123", 1000);
            string h2 = PasswordHelper.HashPassword("password123", 1000);
            Assert.NotEqual(h1, h2); // salt aleatorio → siempre distinto
        }

        [Fact]
        public void HashPassword_LanzaExcepcionSiPasswordEsNula()
        {
            Assert.Throws<ArgumentNullException>(() => PasswordHelper.HashPassword(null));
        }

        [Fact]
        public void HashPassword_LanzaExcepcionSiPasswordEsVacia()
        {
            Assert.Throws<ArgumentNullException>(() => PasswordHelper.HashPassword(""));
        }

        // ── VerifyPassword ──────────────────────────────────────────────────────

        [Fact]
        public void VerifyPassword_PasswordCorrectaRetornaTrue()
        {
            string hash = PasswordHelper.HashPassword("admin123", 1000);
            Assert.True(PasswordHelper.VerifyPassword("admin123", hash));
        }

        [Fact]
        public void VerifyPassword_PasswordIncorrectaRetornaFalse()
        {
            string hash = PasswordHelper.HashPassword("admin123", 1000);
            Assert.False(PasswordHelper.VerifyPassword("wrong", hash));
        }

        [Fact]
        public void VerifyPassword_PasswordVaciaRetornaFalse()
        {
            string hash = PasswordHelper.HashPassword("admin123", 1000);
            Assert.False(PasswordHelper.VerifyPassword("", hash));
        }

        [Fact]
        public void VerifyPassword_HashVacioRetornaFalse()
        {
            Assert.False(PasswordHelper.VerifyPassword("admin123", ""));
        }

        [Fact]
        public void VerifyPassword_HashNuloRetornaFalse()
        {
            Assert.False(PasswordHelper.VerifyPassword("admin123", null));
        }

        [Fact]
        public void VerifyPassword_DiferenciaEntrePasswordsSimilares()
        {
            string hash = PasswordHelper.HashPassword("Admin123", 1000);
            Assert.False(PasswordHelper.VerifyPassword("admin123", hash)); // distinta capitalización
        }

        [Fact]
        public void VerifyPassword_FormatoAnteriorSha1_SeSigueAceptando()
        {
            string viejo = HashFormatoAnterior("Clave-Vieja 2024");
            Assert.True(PasswordHelper.VerifyPassword("Clave-Vieja 2024", viejo));
            Assert.False(PasswordHelper.VerifyPassword("Clave-Vieja 2025", viejo));
        }

        [Fact]
        public void VerifyPassword_FormatosRaros_FalseSinExcepcion()
        {
            string bueno = PasswordHelper.HashPassword("clave123", 1000);
            string[] p = bueno.Split('.');
            string salt = p[2], hash = p[3];

            foreach (string raro in new[]
            {
                "sha256." + (PasswordHelper.IteracionesMaximas + 1) + "." + salt + "." + hash,   // demasiadas iteraciones
                "sha256.0." + salt + "." + hash,
                "sha256.-5." + salt + "." + hash,
                "sha256.abc." + salt + "." + hash,
                "SHA256.1000." + salt + "." + hash,              // prefijo desconocido
                "sha512.1000." + salt + "." + hash,
                "sha256.1000.%%%." + hash,                       // base64 inválido
                "sha256.1000." + salt + ".",                     // hash vacío
                "sha256.1000.AAAA." + hash,                      // salt de 3 bytes
                "1000." + salt,                                  // 2 partes
                "a.b.c.d.e",
                "...",
                (PasswordHelper.IteracionesMaximas + 1) + "." + salt + "." + hash
            })
                Assert.False(PasswordHelper.VerifyPassword("clave123", raro), raro);
        }

        // ── Formato legacy (texto plano): ya no se acepta ───────────────────────

        [Fact]
        public void VerifyPassword_TextoPlanoGuardado_YaNoSeAcepta()
        {
            // Aunque coincida literalmente: las contraseñas en texto plano se migraron a PBKDF2
            // (Database/Scripts/migrar-contrasenas-texto-plano.ps1) y el respaldo se eliminó.
            Assert.False(PasswordHelper.VerifyPassword("admin123", "admin123"));
        }

        [Fact]
        public void VerifyPassword_FormatoLegacyTextoPlanoNoCoincide()
        {
            Assert.False(PasswordHelper.VerifyPassword("otro", "admin123"));
        }

        // ── NecesitaRehash ──────────────────────────────────────────────────────

        [Fact]
        public void NecesitaRehash_FormatoAnterior_True()
        {
            Assert.True(PasswordHelper.NecesitaRehash(HashFormatoAnterior("clave123", 1000)));
        }

        [Fact]
        public void NecesitaRehash_Sha256ConMenosIteraciones_True_ConIgualesOMas_False()
        {
            Assert.True(PasswordHelper.NecesitaRehash(PasswordHelper.HashPassword("clave123", 1000)));
            Assert.True(PasswordHelper.NecesitaRehash(PasswordHelper.HashPassword("clave123", 1000), 1001));
            Assert.False(PasswordHelper.NecesitaRehash(PasswordHelper.HashPassword("clave123", 1000), 1000));
            // Nunca baja: con más iteraciones que el objetivo no se regenera.
            Assert.False(PasswordHelper.NecesitaRehash(PasswordHelper.HashPassword("clave123", 2000), 1000));
        }

        [Fact]
        public void NecesitaRehash_HashActual_False()
        {
            Assert.False(PasswordHelper.NecesitaRehash(PasswordHelper.HashPassword("clave123")));
        }

        [Fact]
        public void NecesitaRehash_TextoPlanoONulo_False()
        {
            Assert.False(PasswordHelper.NecesitaRehash("admin123"));
            Assert.False(PasswordHelper.NecesitaRehash(null));
            Assert.False(PasswordHelper.NecesitaRehash(""));
        }

        // ── IgualarCostoVerificacion (login sin delatar usuarios) ───────────────

        [Fact]
        public void IgualarCosto_HashAnteriorOTextoPlano_VerificaContraElFicticio()
        {
            string ficticio = PasswordHelper.HashPassword("nadie-la-tiene", 1000);
            Assert.True(PasswordHelper.IgualarCostoVerificacion("clave", HashFormatoAnterior("clave", 1000), ficticio));
            Assert.True(PasswordHelper.IgualarCostoVerificacion("clave", PasswordHelper.HashPassword("clave", 1000), ficticio)); // menos iteraciones que el objetivo
            Assert.True(PasswordHelper.IgualarCostoVerificacion("clave", "texto-plano", ficticio));
            Assert.True(PasswordHelper.IgualarCostoVerificacion("clave", "", ficticio));
        }

        [Fact]
        public void IgualarCosto_HashConLasIteracionesObjetivo_NoHaceNada()
        {
            // Ya costó lo mismo que el ficticio: verificar otra vez duplicaría el tiempo.
            string ficticio = PasswordHelper.HashPassword("nadie-la-tiene", 1000);
            Assert.False(PasswordHelper.IgualarCostoVerificacion("clave", PasswordHelper.HashPassword("clave"), ficticio));
        }

        // ── NeedsMigration ──────────────────────────────────────────────────────

        [Fact]
        public void NeedsMigration_FormatoNuevoRetornaFalse()
        {
            // Si devolviera true, migrar-contrasenas-texto-plano.ps1 hashearía el hash y la contraseña se perdería.
            Assert.False(PasswordHelper.NeedsMigration(PasswordHelper.HashPassword("test", 1000)));
        }

        [Fact]
        public void NeedsMigration_FormatoAnteriorRetornaFalse()
        {
            Assert.False(PasswordHelper.NeedsMigration(HashFormatoAnterior("test", 1000)));
        }

        [Fact]
        public void NeedsMigration_TextoPlanoRetornaTrue()
        {
            Assert.True(PasswordHelper.NeedsMigration("admin123"));
            Assert.True(PasswordHelper.NeedsMigration("a.b.c"));        // texto plano con puntos
            Assert.True(PasswordHelper.NeedsMigration("una.clave.con.puntos"));
        }

        [Fact]
        public void NeedsMigration_NuloRetornaTrue()
        {
            Assert.True(PasswordHelper.NeedsMigration(null));
        }

        [Fact]
        public void NeedsMigration_VacioRetornaTrue()
        {
            Assert.True(PasswordHelper.NeedsMigration(""));
        }

        // ── VerificarIngresada ──────────────────────────────────────────────────

        [Fact]
        public void VerificarIngresada_GuardadaConEspacioFinal_EntraEscribiendolaIgual()
        {
            // Así quedaron las creadas antes de que el cambio de contraseña y Gestión de usuarios recortaran.
            string hash = PasswordHelper.HashPassword("clave123 ", 1000);
            Assert.True(PasswordHelper.VerificarIngresada("clave123 ", hash));
        }

        [Fact]
        public void VerificarIngresada_GuardadaRecortada_EntraAunqueEscribaUnEspacio()
        {
            // Restablecer contraseña la guarda recortada; el celular agrega el espacio al escribir.
            string hash = PasswordHelper.HashPassword("clave123", 1000);
            Assert.True(PasswordHelper.VerificarIngresada("clave123 ", hash));
            Assert.True(PasswordHelper.VerificarIngresada(" clave123", hash));
        }

        [Fact]
        public void VerificarIngresada_ContrasenaDistinta_NoEntra()
        {
            string hash = PasswordHelper.HashPassword("clave123", 1000);
            Assert.False(PasswordHelper.VerificarIngresada("clave124", hash));
            Assert.False(PasswordHelper.VerificarIngresada("   ", hash));
            Assert.False(PasswordHelper.VerificarIngresada(null, hash));
        }

        [Fact]
        public void VerificarIngresada_GuardadaConEspacioFinal_EscritaSinEl_NoEntra()
        {
            // Límite conocido: solo se prueba la escrita y la recortada, nunca se agrega un espacio.
            // Ya no se crean así: todas las pantallas guardan la contraseña recortada.
            string hash = PasswordHelper.HashPassword("clave123 ", 1000);
            Assert.False(PasswordHelper.VerificarIngresada("clave123", hash));
        }

        [Fact]
        public void VerificarIngresada_Coincidente_DiceQueVarianteCoincidio()
        {
            string recortada = PasswordHelper.HashPassword("clave123", 1000);
            Assert.True(PasswordHelper.VerificarIngresada("clave123 ", recortada, out string c1));
            Assert.Equal("clave123", c1);   // la recortada: es la que hay que volver a hashear

            string conEspacio = PasswordHelper.HashPassword("clave123 ", 1000);
            Assert.True(PasswordHelper.VerificarIngresada("clave123 ", conEspacio, out string c2));
            Assert.Equal("clave123 ", c2);  // la escrita tal cual

            Assert.True(PasswordHelper.VerificarIngresada("Vieja-1", HashFormatoAnterior("Vieja-1", 1000), out string c3));
            Assert.Equal("Vieja-1", c3);

            Assert.False(PasswordHelper.VerificarIngresada("otra", recortada, out string c4));
            Assert.Null(c4);
        }

        // ── ValidarNueva ────────────────────────────────────────────────────────

        [Fact]
        public void ValidarNueva_MinimoOchoMaximo128()
        {
            Assert.Equal(8, PasswordHelper.LargoMinimoNueva);
            Assert.Equal(128, PasswordHelper.LargoMaximoNueva);
            const string msjCorta = "La contraseña debe tener al menos 8 caracteres.";
            Assert.Equal(msjCorta, PasswordHelper.ValidarNueva(null));
            Assert.Equal(msjCorta, PasswordHelper.ValidarNueva(""));
            Assert.Equal(msjCorta, PasswordHelper.ValidarNueva("1234567"));
            Assert.Null(PasswordHelper.ValidarNueva("12345678"));
            Assert.Null(PasswordHelper.ValidarNueva(new string('a', 128)));
            Assert.Equal("La contraseña no puede tener más de 128 caracteres.", PasswordHelper.ValidarNueva(new string('a', 129)));
        }

        [Fact]
        public void ContrasenaExistenteDeSeisCaracteres_SigueEntrando()
        {
            // El mínimo de 8 es solo para contraseñas nuevas.
            Assert.True(PasswordHelper.VerificarIngresada("abc123", PasswordHelper.HashPassword("abc123", 1000)));
            Assert.True(PasswordHelper.VerificarIngresada("abc123", HashFormatoAnterior("abc123", 1000)));
        }
    }
}
