using System;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Sello de sesión (migración 46 y sp_US_ObtenerEstadoSesion) contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>: el usuario es inventado y no queda guardado.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class SelloSesionBdTests
    {
        public SelloSesionBdTests() => BaseDePruebas.Preparar();

        /// <summary>INSERT sin nombrar selloSesion, como el código ya publicado.</summary>
        private static int CrearUsuario(string rol, int? idConductor) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, idConductor)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Prueba sello', 'x', @rol, 1, @idConductor)",
                DbHelper.Param("@user", "zz_sello_" + Guid.NewGuid().ToString("N").Substring(0, 12)),
                DbHelper.Param("@rol", rol),
                DbHelper.Param("@idConductor", (object)idConductor ?? DBNull.Value)));

        private static Guid Sello(int id) =>
            (Guid)DbHelper.EjecutarEscalar("SELECT selloSesion FROM Usuarios WHERE idUsuario = @id", DbHelper.Param("@id", id));

        [FactBD]
        public void InsertSinColumna_RecibeSello_YElSpDevuelveElEstado()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario(" CONDUCTOR ", null);
                Guid sello = Sello(id);
                Assert.NotEqual(Guid.Empty, sello);

                EstadoSesionUsuario estado = SesionVigenteHelper.LeerEstadoDeBd(id);
                Assert.True(estado.Existe);
                Assert.True(estado.Activo);
                Assert.Equal(sello, estado.SelloSesion);
                Assert.Equal("CONDUCTOR", estado.Rol);          // recortado por el SP
                Assert.Null(estado.IdConductor);
                Assert.False(estado.RequiereCambioContrasena);  // NULL/0 → false

                DbHelper.EjecutarNonQuery("UPDATE Usuarios SET activo = 0, requiereCambioContrasena = 1 WHERE idUsuario = @id",
                    DbHelper.Param("@id", id));
                estado = SesionVigenteHelper.LeerEstadoDeBd(id);
                Assert.False(estado.Activo);
                Assert.True(estado.RequiereCambioContrasena);
            }
        }

        [FactBD]
        public void UsuarioInexistente_SpSinFilas()
        {
            Assert.False(SesionVigenteHelper.LeerEstadoDeBd(-987654).Existe);
        }

        [FactBD]
        public void UpdateConNewId_CambiaElSello()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuario("ADMIN", null);
                Guid antes = Sello(id);
                DbHelper.EjecutarNonQuery("UPDATE Usuarios SET selloSesion = NEWID() WHERE idUsuario = @id", DbHelper.Param("@id", id));
                Assert.NotEqual(antes, Sello(id));
            }
        }

        /// <summary>Misma condición que GestionUsuarios al editar: solo rol o conductor renuevan el sello.</summary>
        private static void EditarComoGestionUsuarios(int id, string nombre, string rol, int? idConductor) =>
            DbHelper.EjecutarNonQuery(
                @"UPDATE Usuarios
                  SET selloSesion = CASE WHEN ISNULL(rol, '') <> @rol
                                           OR ISNULL(idConductor, -1) <> ISNULL(@idConductor, -1)
                                         THEN NEWID() ELSE selloSesion END,
                      nombre = @nombre, rol = @rol, idConductor = @idConductor
                  WHERE idUsuario = @id",
                DbHelper.Param("@nombre", nombre),
                DbHelper.Param("@rol", rol),
                DbHelper.Param("@idConductor", (object)idConductor ?? DBNull.Value),
                DbHelper.Param("@id", id));

        [FactBD]
        public void EditarUsuario_SoloRolOConductorRenuevanElSello()
        {
            using (new TransaccionDePrueba())
            {
                // Un conductor sin usuario: UX_Usuarios_idConductor no deja vincular dos usuarios al mismo.
                object libre = DbHelper.EjecutarEscalar(
                    @"SELECT TOP 1 c.idConductor FROM Conductor c
                      WHERE NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idConductor = c.idConductor)
                      ORDER BY c.idConductor");
                Assert.True(libre != null && libre != DBNull.Value, "La BD de pruebas no tiene conductores sin usuario.");
                int idConductor = Convert.ToInt32(libre);
                int id = CrearUsuario("CONDUCTOR", null);
                Guid s0 = Sello(id);

                EditarComoGestionUsuarios(id, "Otro nombre", "conductor", null);   // solo nombre (rol igual sin distinguir caso)
                Assert.Equal(s0, Sello(id));

                EditarComoGestionUsuarios(id, "Otro nombre", "CONDUCTOR", idConductor);   // vincula conductor
                Guid s1 = Sello(id);
                Assert.NotEqual(s0, s1);

                EditarComoGestionUsuarios(id, "Otro nombre", "SUPERVISOR", idConductor);  // cambia rol
                Assert.NotEqual(s1, Sello(id));
            }
        }
    }
}
