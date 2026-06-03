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

        // ── Formato legacy (texto plano) ────────────────────────────────────────

        [Fact]
        public void VerifyPassword_FormatoLegacyTextoPlanoCoincide()
        {
            Assert.True(PasswordHelper.VerifyPassword("admin123", "admin123"));
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
    }
}
