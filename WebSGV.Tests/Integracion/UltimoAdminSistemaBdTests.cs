using System;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E4-12, M-066: nunca queda el sistema sin un ADMINISTRADOR DE SISTEMA activo, contra la BD de pruebas dentro de
    /// una <see cref="TransaccionDePrueba"/>. Cada prueba deja como únicos administradores de sistema activos a los
    /// que crea (los reales se desactivan solo dentro de la transacción, que nunca se confirma). Los casos que fallan
    /// van al final de cada prueba.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class UltimoAdminSistemaBdTests
    {
        public UltimoAdminSistemaBdTests() => BaseDePruebas.Preparar();

        private static int CrearAdminSistema() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, idConductor)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Prueba E4-12', 'x', @rol, 1, NULL)",
                DbHelper.Param("@user", "zz_e412_" + Guid.NewGuid().ToString("N").Substring(0, 12)),
                DbHelper.Param("@rol", RolesHelper.ROL_ADMIN_SISTEMA)));

        /// <summary>Solo dentro de la transacción de la prueba: se deshace al terminar.</summary>
        private static void DesactivarAdminsSistemaReales() =>
            DbHelper.EjecutarNonQuery("UPDATE Usuarios SET activo = 0 WHERE rol = @rol AND activo = 1",
                DbHelper.Param("@rol", RolesHelper.ROL_ADMIN_SISTEMA));

        private static bool Activo(int id) =>
            Convert.ToBoolean(DbHelper.EjecutarEscalar("SELECT activo FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", id)));

        private static string Rol(int id) =>
            Convert.ToString(DbHelper.EjecutarEscalar("SELECT rol FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", id)));

        private static Guid Sello(int id) =>
            (Guid)DbHelper.EjecutarEscalar("SELECT selloSesion FROM Usuarios WHERE idUsuario = @id", DbHelper.Param("@id", id));

        private static int Auditorias(int id, string accion) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"SELECT COUNT(*) FROM AuditoriaLog
                   WHERE TablaAfectada = 'Usuarios' AND IdRegistroAfectado = @id AND Accion = @accion",
                DbHelper.Param("@id", id.ToString()), DbHelper.Param("@accion", accion)));

        private static int ActivosAdminSistema() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Usuarios WHERE rol = @rol AND activo = 1",
                DbHelper.Param("@rol", RolesHelper.ROL_ADMIN_SISTEMA)));

        [FactBD]
        public void ConDosActivos_DesactivarUnoFunciona_YElSegundoYaNo()
        {
            using (new TransaccionDePrueba())
            {
                DesactivarAdminsSistemaReales();
                int a = CrearAdminSistema();
                int b = CrearAdminSistema();
                Assert.Equal(2, ActivosAdminSistema());

                // a desactiva a b (el usuario conectado es a)
                ResultadoCambioEstado r = UsuarioEstadoService.CambiarEstado(b, false, a);
                Assert.True(r.Cambiado);
                Assert.False(Activo(b));
                Guid selloA = Sello(a);

                // Ahora a es el último: otra sesión de b (antes de cerrarse) o cualquier otro no lo desactiva.
                var ex = Assert.Throws<ErrorNegocioException>(() => UsuarioEstadoService.CambiarEstado(a, false, b));
                Assert.Equal(UsuarioEstadoService.MensajeUltimoAdminSistema, ex.Message);
                Assert.True(Activo(a));
                Assert.Equal(selloA, Sello(a));                 // 0 filas: ni el sello cambió
                Assert.Equal(1, ActivosAdminSistema());
                Assert.Equal(1, Auditorias(a, UsuarioEstadoService.AccionUltimoAdminRechazado));
                Assert.Equal(0, Auditorias(a, "UPDATE"));
            }
        }

        [FactBD]
        public void UltimoActivo_EditarSuNombreFunciona_PeroCambiarleElRolNo()
        {
            using (new TransaccionDePrueba())
            {
                DesactivarAdminsSistemaReales();
                int a = CrearAdminSistema();

                // Mismo rol: no quita un administrador, se guarda.
                Assert.Equal(1, UsuarioEstadoService.ActualizarDatos(a, "Prueba E4-12 editada", "administrador de sistema", null));
                Assert.Equal(RolesHelper.ROL_ADMIN_SISTEMA, Rol(a));
                Guid sello = Sello(a);

                var ex = Assert.Throws<ErrorNegocioException>(() =>
                    UsuarioEstadoService.ActualizarDatos(a, "Prueba E4-12 editada", RolesHelper.ROL_SUPERVISOR, null));
                Assert.Equal(UsuarioEstadoService.MensajeUltimoAdminSistema, ex.Message);
                Assert.Equal(RolesHelper.ROL_ADMIN_SISTEMA, Rol(a));
                Assert.Equal(sello, Sello(a));
                Assert.Equal(1, Auditorias(a, UsuarioEstadoService.AccionUltimoAdminRechazado));
            }
        }

        [FactBD]
        public void ConDosActivos_CambiarElRolDeUnoFunciona_YDelOtroYaNo()
        {
            using (new TransaccionDePrueba())
            {
                DesactivarAdminsSistemaReales();
                int a = CrearAdminSistema();
                int b = CrearAdminSistema();
                Guid selloB = Sello(b);

                Assert.Equal(1, UsuarioEstadoService.ActualizarDatos(b, "Prueba E4-12", RolesHelper.ROL_SUPERVISOR, null));
                Assert.Equal(RolesHelper.ROL_SUPERVISOR, Rol(b));
                Assert.NotEqual(selloB, Sello(b));               // cambiar el rol cierra sus sesiones

                // Un inactivo se puede desactivar "otra vez" o cambiar de rol: no quita a ningún activo.
                int c = CrearAdminSistema();
                Assert.True(UsuarioEstadoService.CambiarEstado(c, false, a).Cambiado);
                Assert.Equal(1, UsuarioEstadoService.ActualizarDatos(c, "Prueba E4-12", RolesHelper.ROL_CONTABILIDAD, null));

                var ex = Assert.Throws<ErrorNegocioException>(() =>
                    UsuarioEstadoService.ActualizarDatos(a, "Prueba E4-12", RolesHelper.ROL_ADMIN_TRANSPORTE, null));
                Assert.Equal(UsuarioEstadoService.MensajeUltimoAdminSistema, ex.Message);
                Assert.Equal(RolesHelper.ROL_ADMIN_SISTEMA, Rol(a));
                Assert.True(Activo(a));
            }
        }
    }
}
