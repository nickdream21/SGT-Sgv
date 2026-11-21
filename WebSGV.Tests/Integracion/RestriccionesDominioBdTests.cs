using System;
using System.Data.SqlClient;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E3-6, migraciones 54 y 55: la BD rechaza los datos fuera de dominio (error 547, conflicto con una
    /// restricción). Cada prueba va en su propia <see cref="TransaccionDePrueba"/> y termina en el error:
    /// no deja datos.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class RestriccionesDominioBdTests
    {
        public RestriccionesDominioBdTests() => BaseDePruebas.Preparar();

        private static void Rechaza(string restriccion, string sql, params SqlParameter[] parametros)
        {
            var ex = Assert.Throws<SqlException>(() => DbHelper.EjecutarNonQuery(sql, parametros));
            Assert.Equal(547, ex.Number);
            Assert.Contains(restriccion, ex.Message);
        }

        private static int UnDespachoActivo() => Convert.ToInt32(DbHelper.EjecutarEscalar(
            "SELECT TOP 1 idDespacho FROM Despachos WHERE activo = 1 AND estadoDespacho <> 'ANULADO' ORDER BY idDespacho DESC"));

        [FactBD]
        public void Dar_de_baja_un_despacho_sin_anularlo_lo_rechaza_la_BD()
        {
            using (new TransaccionDePrueba())
                Rechaza("CK_Despachos_AnuladoInactivo", "UPDATE Despachos SET activo = 0 WHERE idDespacho = @id",
                    DbHelper.Param("@id", UnDespachoActivo()));
        }

        [FactBD]
        public void Un_estado_de_despacho_desconocido_lo_rechaza_la_BD()
        {
            using (new TransaccionDePrueba())
                Rechaza("CK_Despachos_Estado", "UPDATE Despachos SET estadoDespacho = 'EN_PROCESO' WHERE idDespacho = @id",
                    DbHelper.Param("@id", UnDespachoActivo()));
        }

        [FactBD]
        public void Un_seguimiento_COMPLETADO_lo_rechaza_la_BD()
        {
            using (new TransaccionDePrueba())
                Rechaza("CK_SeguimientoExportacion_Estado",
                    "UPDATE TOP (1) SeguimientoExportacion SET estado = 'COMPLETADO' WHERE estado = 'FINALIZADO'");
        }

        [FactBD]
        public void Un_usuario_activo_con_rol_retirado_lo_rechaza_la_BD()
        {
            using (new TransaccionDePrueba())
                Rechaza("CK_Usuarios_Rol",
                    "INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo) VALUES (@u, 'Prueba E3-6', 'x', 'OPERADOR', 1)",
                    DbHelper.Param("@u", "zz_e36_" + Guid.NewGuid().ToString("N").Substring(0, 10)));
        }

        [FactBD]
        public void Un_usuario_inactivo_con_rol_retirado_si_lo_acepta()
        {
            using (new TransaccionDePrueba())
            {
                int filas = DbHelper.EjecutarNonQuery(
                    "INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo) VALUES (@u, 'Prueba E3-6', 'x', 'OPERADOR', 0)",
                    DbHelper.Param("@u", "zz_e36_" + Guid.NewGuid().ToString("N").Substring(0, 10)));
                Assert.Equal(1, filas);
            }
        }

        [FactBD]
        public void Activar_un_usuario_con_rol_retirado_avisa_sin_llegar_a_la_BD()
        {
            using (new TransaccionDePrueba())
            {
                int id = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo) OUTPUT INSERTED.idUsuario
                      VALUES (@u, 'Prueba E3-6', 'x', 'OPERADOR', 0)",
                    DbHelper.Param("@u", "zz_e36_" + Guid.NewGuid().ToString("N").Substring(0, 10))));
                var ex = Assert.Throws<WebSGV.Services.Common.ErrorNegocioException>(
                    () => WebSGV.Services.Usuarios.UsuarioEstadoService.CambiarEstado(id, true, null));
                Assert.Equal(WebSGV.Services.Usuarios.UsuarioEstadoService.MensajeRolNoVigente, ex.Message);
                Assert.False(Convert.ToBoolean(DbHelper.EjecutarEscalar("SELECT activo FROM Usuarios WHERE idUsuario = @id",
                    DbHelper.Param("@id", id))));
            }
        }

        [FactBD]
        public void Un_seguimiento_con_despacho_inexistente_lo_rechaza_la_BD()
        {
            using (new TransaccionDePrueba())
                Rechaza("FK_SeguimientoExportacion_DespachoOrigen",
                    "UPDATE TOP (1) SeguimientoExportacion SET idDespachoOrigen = -1");
        }
    }
}
