using System;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class PasswordHelperTests
    {
        // ── HashPassword ────────────────────────────────────────────────────────

        [Fact]
        public void HashPassword_RetornaFormatoCorrecto()
        {
            string hash = PasswordHelper.HashPassword("miPassword");
            string[] partes = hash.Split('.');
            Assert.Equal(3, partes.Length);
            Assert.Equal("10000", partes[0]);
        }

        [Fact]
        public void HashPassword_MismaPasswordGeneraHashesDiferentes()
        {
            string h1 = PasswordHelper.HashPassword("password123");
            string h2 = PasswordHelper.HashPassword("password123");
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
            string hash = PasswordHelper.HashPassword("admin123");
            Assert.True(PasswordHelper.VerifyPassword("admin123", hash));
        }

        [Fact]
        public void VerifyPassword_PasswordIncorrectaRetornaFalse()
        {
            string hash = PasswordHelper.HashPassword("admin123");
            Assert.False(PasswordHelper.VerifyPassword("wrong", hash));
        }

        [Fact]
        public void VerifyPassword_PasswordVaciaRetornaFalse()
        {
            string hash = PasswordHelper.HashPassword("admin123");
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
            string hash = PasswordHelper.HashPassword("Admin123");
            Assert.False(PasswordHelper.VerifyPassword("admin123", hash)); // distinta capitalización
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

        // ── NeedsMigration ──────────────────────────────────────────────────────

        [Fact]
        public void NeedsMigration_FormatoNuevoRetornaFalse()
        {
            string hash = PasswordHelper.HashPassword("test");
            Assert.False(PasswordHelper.NeedsMigration(hash));
        }

        [Fact]
        public void NeedsMigration_TextoPlanoRetornaTrue()
        {
            Assert.True(PasswordHelper.NeedsMigration("admin123"));
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

        [Fact]
        public void VerificarIngresada_GuardadaConEspacioFinal_EntraEscribiendolaIgual()
        {
            // Así quedaron las creadas antes de que el cambio de contraseña y Gestión de usuarios recortaran.
            string hash = PasswordHelper.HashPassword("clave123 ");
            Assert.True(PasswordHelper.VerificarIngresada("clave123 ", hash));
        }

        [Fact]
        public void VerificarIngresada_GuardadaRecortada_EntraAunqueEscribaUnEspacio()
        {
            // Restablecer contraseña la guarda recortada; el celular agrega el espacio al escribir.
            string hash = PasswordHelper.HashPassword("clave123");
            Assert.True(PasswordHelper.VerificarIngresada("clave123 ", hash));
            Assert.True(PasswordHelper.VerificarIngresada(" clave123", hash));
        }

        [Fact]
        public void VerificarIngresada_ContrasenaDistinta_NoEntra()
        {
            string hash = PasswordHelper.HashPassword("clave123");
            Assert.False(PasswordHelper.VerificarIngresada("clave124", hash));
            Assert.False(PasswordHelper.VerificarIngresada("   ", hash));
            Assert.False(PasswordHelper.VerificarIngresada(null, hash));
        }

        [Fact]
        public void VerificarIngresada_GuardadaConEspacioFinal_EscritaSinEl_NoEntra()
        {
            // Límite conocido: solo se prueba la escrita y la recortada, nunca se agrega un espacio.
            // Ya no se crean así: todas las pantallas guardan la contraseña recortada.
            string hash = PasswordHelper.HashPassword("clave123 ");
            Assert.False(PasswordHelper.VerificarIngresada("clave123", hash));
        }
    }
}
