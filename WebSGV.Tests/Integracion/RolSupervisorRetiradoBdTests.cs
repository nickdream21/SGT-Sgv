using System;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E5-19b (U-090, migración 63): el rol SUPERVISOR se retiró. CK_Usuarios_Rol solo lo admite en cuentas
    /// inactivas: el historial sigue valiendo, pero no se crea ni se reactiva una cuenta con ese rol. Contra la BD
    /// de pruebas dentro de <see cref="TransaccionDePrueba"/>; los casos que fallan en la BD van al final de su
    /// prueba (un error del CHECK puede dejar abortada la transacción).
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class RolSupervisorRetiradoBdTests
    {
        public RolSupervisorRetiradoBdTests() => BaseDePruebas.Preparar();

        private static int CrearUsuario(string rol, bool activo) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Prueba E5-19b', 'x', @rol, @activo)",
                DbHelper.Param("@user", "zz_e519b_" + Guid.NewGuid().ToString("N").Substring(0, 12)),
                DbHelper.Param("@rol", rol),
                DbHelper.Param("@activo", activo)));

        private static void AssertRechazaCheck(Action accion)
        {
            var ex = Assert.ThrowsAny<SqlException>(accion);
            Assert.Equal(547, ex.Number);
            Assert.Contains("CK_Usuarios_Rol", ex.Message);
        }

        [FactBD]
        public void Check_SoloAdmiteSupervisorEnCuentasInactivas()
        {
            string definicion = Convert.ToString(DbHelper.EjecutarEscalar(
                @"SELECT definition FROM sys.check_constraints
                  WHERE name = 'CK_Usuarios_Rol' AND parent_object_id = OBJECT_ID('dbo.Usuarios')
                    AND is_disabled = 0 AND is_not_trusted = 0"));
            Assert.False(string.IsNullOrEmpty(definicion), "Falta CK_Usuarios_Rol (o está deshabilitado o sin validar).");
            Assert.Contains("SUPERVISOR", definicion);
            // Con la 54 sola, SUPERVISOR estaba entre los vigentes (fuera del paréntesis de [activo]=(0)).
            int posActivo = definicion.IndexOf("[activo]=(0)", StringComparison.Ordinal);
            Assert.True(posActivo > 0, "El CHECK no tiene la rama de roles retirados ([activo]=(0)).");
            Assert.True(definicion.IndexOf("'SUPERVISOR'", StringComparison.Ordinal) > posActivo,
                "SUPERVISOR sigue entre los roles vigentes: falta la migración 63.");
            Assert.Equal(0, Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM Usuarios WHERE UPPER(LTRIM(RTRIM(rol))) = 'SUPERVISOR' AND activo = 1")));
        }

        [FactBD]
        public void CuentaInactiva_SeCreaYSeEdita_PeroNoSeReactivaPorElServicio()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario("SUPERVISOR", false);
                Assert.Equal(1, DbHelper.EjecutarNonQuery(
                    "UPDATE Usuarios SET nombre = 'Prueba E5-19b editada' WHERE idUsuario = @id", DbHelper.Param("@id", id)));

                // El servicio lo rechaza con un aviso de negocio antes de llegar al CHECK.
                var ex = Assert.Throws<ErrorNegocioException>(() => UsuarioEstadoService.CambiarEstado(id, true, null));
                Assert.Equal(UsuarioEstadoService.MensajeRolNoVigente, ex.Message);
                Assert.False(Convert.ToBoolean(DbHelper.EjecutarEscalar(
                    "SELECT activo FROM Usuarios WHERE idUsuario = @id", DbHelper.Param("@id", id))));
            }
        }

        [FactBD]
        public void Reactivar_una_cuenta_supervisor_la_rechaza_el_check()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario("SUPERVISOR", false);
                // Caso de error al final: el CHECK rechaza el UPDATE (547).
                AssertRechazaCheck(() => DbHelper.EjecutarNonQuery(
                    "UPDATE Usuarios SET activo = 1 WHERE idUsuario = @id", DbHelper.Param("@id", id)));
            }
        }

        [FactBD]
        public void Crear_una_cuenta_supervisor_activa_la_rechaza_el_check()
        {
            using (new TransaccionDePrueba())
            {
                // Caso de error al final: el CHECK rechaza el INSERT (547).
                AssertRechazaCheck(() => CrearUsuario("SUPERVISOR", true));
            }
        }

        [FactBD]
        public void Cambiar_a_supervisor_una_cuenta_activa_lo_rechaza_el_check()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario(RolesHelper.ROL_CONTABILIDAD, true);
                // Caso de error al final: el CHECK rechaza el UPDATE (547).
                AssertRechazaCheck(() => DbHelper.EjecutarNonQuery(
                    "UPDATE Usuarios SET rol = 'SUPERVISOR' WHERE idUsuario = @id", DbHelper.Param("@id", id)));
            }
        }
    }
}
