using System;
using System.Data;
using System.Security.Cryptography;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Conversión del hash del formato anterior (PBKDF2-SHA1) a PBKDF2-SHA256 al iniciar sesión
    /// (PasswordHelper.ActualizarHashSiCorresponde), contra la BD de pruebas dentro de una
    /// <see cref="TransaccionDePrueba"/>: el usuario es inventado y no queda guardado.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ConversionHashContrasenaBdTests
    {
        public ConversionHashContrasenaBdTests() => BaseDePruebas.Preparar();

        private const string Clave = "Clave-Antigua 2024";

        private static string HashFormatoAnterior(string password)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000))
                return "10000." + Convert.ToBase64String(salt) + "." + Convert.ToBase64String(pbkdf2.GetBytes(32));
        }

        private static int CrearUsuario(string hash) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, requiereCambioContrasena)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Prueba hash', @pass, 'ADMIN', 1, 1)",
                DbHelper.Param("@user", "zz_hash_" + Guid.NewGuid().ToString("N").Substring(0, 12)),
                DbHelper.Param("@pass", hash)));

        private static DataRow Leer(int id) =>
            DbHelper.ConsultarTabla(
                "SELECT contrasena, selloSesion, requiereCambioContrasena FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", id)).Rows[0];

        [FactBD]
        public void HashAnterior_SeConvierte_SinTocarSelloNiCambioObligatorio()
        {
            using (new TransaccionDePrueba())
            {
                string viejo = HashFormatoAnterior(Clave);
                int id = CrearUsuario(viejo);
                DataRow antes = Leer(id);

                Assert.True(PasswordHelper.VerificarIngresada(Clave + " ", viejo, out string coincidente));
                Assert.Equal(Clave, coincidente);
                Assert.True(PasswordHelper.ActualizarHashSiCorresponde(id, viejo, coincidente));

                DataRow despues = Leer(id);
                string nuevo = (string)despues["contrasena"];
                Assert.StartsWith("sha256." + PasswordHelper.IteracionesObjetivo + ".", nuevo);
                Assert.True(PasswordHelper.VerifyPassword(Clave, nuevo));
                Assert.False(PasswordHelper.NecesitaRehash(nuevo));
                Assert.Equal((Guid)antes["selloSesion"], (Guid)despues["selloSesion"]);
                Assert.Equal(Convert.ToBoolean(antes["requiereCambioContrasena"]), Convert.ToBoolean(despues["requiereCambioContrasena"]));

                // Ya convertido: no vuelve a escribir.
                Assert.False(PasswordHelper.ActualizarHashSiCorresponde(id, nuevo, Clave));

                // Con el hash anterior desactualizado (otro proceso lo cambió): 0 filas, no toca nada.
                Assert.False(PasswordHelper.ActualizarHashSiCorresponde(id, viejo, Clave));
                Assert.Equal(nuevo, (string)Leer(id)["contrasena"]);
            }
        }
    }
}
