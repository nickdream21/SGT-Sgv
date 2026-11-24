using System;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E4-14 (migración 62 y sp_US_*SegundoFactor*): contra la BD de pruebas dentro de una
    /// <see cref="TransaccionDePrueba"/> con un usuario inventado que no queda guardado. Un paso TOTP no se
    /// registra dos veces, un código de recuperación no vale dos veces y quitar el factor cambia el sello y borra
    /// los códigos. Ningún SP de estas pruebas lanza: no hay casos de error que aborten la transacción.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class SegundoFactorBdTests
    {
        public SegundoFactorBdTests() => BaseDePruebas.Preparar();

        private static readonly CifradoSecreto Cifrado = new CifradoSecreto(Enumerable.Range(0, 32).Select(i => (byte)(i + 3)).ToArray());

        private static int CrearUsuario() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, idConductor)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Prueba E4-14', 'x', @rol, 1, NULL)",
                DbHelper.Param("@user", "zz_e414_" + Guid.NewGuid().ToString("N").Substring(0, 12)),
                DbHelper.Param("@rol", RolesHelper.ROL_CONTABILIDAD)));

        private static Guid Sello(int id) =>
            (Guid)DbHelper.EjecutarEscalar("SELECT selloSesion FROM Usuarios WHERE idUsuario = @id", DbHelper.Param("@id", id));

        private static int Codigos(int id) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM UsuarioCodigoRecuperacion WHERE idUsuario = @id",
                DbHelper.Param("@id", id)));

        /// <summary>Alta completa (pendiente + activación en el paso indicado); devuelve el secreto cifrado vigente.</summary>
        private static string Activar(int id, long paso, out string[] codigos)
        {
            string pendiente = Cifrado.Cifrar(Totp.GenerarSecreto());
            DbHelper.EjecutarNonQuerySp("sp_US_GuardarSecretoPendiente",
                DbHelper.Param("@idUsuario", id), DbHelper.Param("@secretoCifrado", pendiente));
            codigos = SegundoFactorService.GenerarCodigos(out string hashes);
            Assert.True(SegundoFactorService.ActivarBd(id, pendiente, paso, hashes));
            return pendiente;
        }

        [FactBD]
        public void AltaPendiente_NoActivaHastaConfirmar_YConOtroPendienteNoActiva()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                Assert.Null(SegundoFactorService.Obtener(id));

                string pendiente = Cifrado.Cifrar(Totp.GenerarSecreto());
                DbHelper.EjecutarNonQuerySp("sp_US_GuardarSecretoPendiente",
                    DbHelper.Param("@idUsuario", id), DbHelper.Param("@secretoCifrado", pendiente));
                EstadoSegundoFactor e = SegundoFactorService.Obtener(id);
                Assert.False(e.Activo);
                Assert.Equal(pendiente, e.SecretoPendienteCifrado);
                Assert.False(InicioSesionHelper.CargarDatosLogin(id).SegundoFactorActivo);

                // Otra pestaña empezó otra alta: confirmar la primera no activa nada.
                string otro = Cifrado.Cifrar(Totp.GenerarSecreto());
                DbHelper.EjecutarNonQuerySp("sp_US_GuardarSecretoPendiente",
                    DbHelper.Param("@idUsuario", id), DbHelper.Param("@secretoCifrado", otro));
                SegundoFactorService.GenerarCodigos(out string hashes);
                Assert.False(SegundoFactorService.ActivarBd(id, pendiente, 100, hashes));
                Assert.False(SegundoFactorService.Obtener(id).Activo);

                Assert.True(SegundoFactorService.ActivarBd(id, otro, 100, hashes));
                e = SegundoFactorService.Obtener(id);
                Assert.True(e.Activo);
                Assert.Equal(otro, e.SecretoCifrado);
                Assert.Null(e.SecretoPendienteCifrado);
                Assert.Equal(100L, e.UltimoPasoUsado);
                Assert.Equal(SegundoFactorReglas.CantidadCodigosRecuperacion, e.CodigosRestantes);
                Assert.True(InicioSesionHelper.CargarDatosLogin(id).SegundoFactorActivo);
            }
        }

        [FactBD]
        public void PasoTotp_NoSeRegistraDosVeces_NiUnoAnterior()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                string secreto = Activar(id, 1000, out _);

                Assert.False(SegundoFactorService.RegistrarPasoBd(id, secreto, 1000)); // el del alta
                Assert.True(SegundoFactorService.RegistrarPasoBd(id, secreto, 1001));
                Assert.False(SegundoFactorService.RegistrarPasoBd(id, secreto, 1001)); // mismo código otra vez
                Assert.False(SegundoFactorService.RegistrarPasoBd(id, secreto, 999));
                Assert.False(SegundoFactorService.RegistrarPasoBd(id, "v1.otro.secreto.x", 1005)); // otro secreto
                Assert.Equal(1001L, SegundoFactorService.Obtener(id).UltimoPasoUsado);
            }
        }

        [FactBD]
        public void CodigoDeRecuperacion_NoValeDosVeces()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                Activar(id, 5, out string[] codigos);

                var guardados = SegundoFactorService.LeerCodigosBd(id);
                Assert.Equal(10, guardados.Count);
                string normalizado = SegundoFactorReglas.NormalizarCodigoRecuperacion(codigos[3]);
                var fila = guardados.Single(c => SegundoFactorReglas.VerificarCodigoRecuperacion(normalizado, c.Value));

                Tuple<bool, int> primero = SegundoFactorService.UsarCodigoBd(id, fila.Key);
                Assert.True(primero.Item1);
                Assert.Equal(9, primero.Item2);

                Tuple<bool, int> segundo = SegundoFactorService.UsarCodigoBd(id, fila.Key);
                Assert.False(segundo.Item1);
                Assert.Equal(9, segundo.Item2);
                Assert.DoesNotContain(SegundoFactorService.LeerCodigosBd(id), c => c.Key == fila.Key);

                // Un código de otro usuario no sirve.
                int otro = CrearUsuario();
                Activar(otro, 5, out _);
                int deOtro = SegundoFactorService.LeerCodigosBd(otro).First().Key;
                Assert.False(SegundoFactorService.UsarCodigoBd(id, deOtro).Item1);
            }
        }

        [FactBD]
        public void RegenerarCodigos_ReemplazaTodos()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                Activar(id, 5, out string[] viejos);
                string[] nuevos = SegundoFactorService.RegenerarCodigos(id, "zz");
                Assert.Equal(10, nuevos.Length);
                var guardados = SegundoFactorService.LeerCodigosBd(id);
                Assert.Equal(10, guardados.Count);
                string viejo = SegundoFactorReglas.NormalizarCodigoRecuperacion(viejos[0]);
                Assert.DoesNotContain(guardados, c => SegundoFactorReglas.VerificarCodigoRecuperacion(viejo, c.Value));

                int sinFactor = CrearUsuario();
                Assert.Null(SegundoFactorService.RegenerarCodigos(sinFactor, "zz"));
            }
        }

        [FactBD]
        public void Quitar_CambiaElSello_YBorraFactorYCodigos()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario();
                Activar(id, 5, out _);
                Guid antes = Sello(id);
                Assert.Equal(10, Codigos(id));

                Assert.True(SegundoFactorService.Quitar(id));
                Assert.NotEqual(antes, Sello(id));
                Assert.Equal(0, Codigos(id));
                Assert.Null(SegundoFactorService.Obtener(id));
                Assert.False(InicioSesionHelper.CargarDatosLogin(id).SegundoFactorActivo);

                // Sin nada que quitar no toca el sello.
                Guid despues = Sello(id);
                Assert.False(SegundoFactorService.Quitar(id));
                Assert.Equal(despues, Sello(id));
            }
        }
    }
}
