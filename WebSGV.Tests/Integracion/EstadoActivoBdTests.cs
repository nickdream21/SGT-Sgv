using System;
using System.Data;
using WebSGV.Helpers;
using WebSGV.Services.Clientes;
using WebSGV.Services.Common;
using WebSGV.Services.Conductor;
using WebSGV.Services.Usuarios;
using WebSGV.Services.Vehiculos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Bloque 5, B5-3, contra la BD de pruebas dentro de una <see cref="TransaccionDePrueba"/> (no deja datos):
    /// desactivar dos veces (F5 o dos pestañas) deja el maestro inactivo, el segundo intento dice "ya estaba" y
    /// se audita una sola vez, con el estado anterior y el nuevo. Tracto y Conductor tienen trigger de
    /// auditoría: prueban que el UPDATE con OUTPUT ... INTO funciona con trigger. Los errores van al final.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class EstadoActivoBdTests
    {
        public EstadoActivoBdTests() => BaseDePruebas.Preparar();

        private static bool Activo(string tabla, string columnaId, string columnaActivo, int id) =>
            Convert.ToBoolean(DbHelper.EjecutarEscalar(
                $"SELECT {columnaActivo} FROM {tabla} WHERE {columnaId} = @id", DbHelper.Param("@id", id)));

        private static DataTable AuditoriasDeEstado(string tabla, int id) =>
            DbHelper.ConsultarTabla(
                @"SELECT ValoresAnteriores, ValoresNuevos, Descripcion FROM AuditoriaLog
                   WHERE TablaAfectada = @t AND IdRegistroAfectado = @id AND Accion = 'UPDATE'
                     AND ValoresNuevos LIKE 'Estado:%'
                ORDER BY 1",
                DbHelper.Param("@t", tabla), DbHelper.Param("@id", id.ToString()));

        private static int InsertarCliente() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Cliente (nombre, usuarioRegistro, activo) VALUES ('ZZ PRUEBA B5-3', 'PRUEBA', 1);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);"));

        [FactBD]
        public void Desactivar_dos_veces_un_tracto_deja_inactivo_y_audita_una_vez()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ9-989", "VOLVO", "FH");

                ResultadoCambioEstado primero = VehiculoService.CambiarEstado(TipoVehiculo.Tracto, id, false);
                Assert.True(primero.Cambiado);
                Assert.Equal("Tracto ZZ9-989 desactivado correctamente.", primero.Mensaje);

                // Segunda pestaña o F5: antes lo reactivaba
                ResultadoCambioEstado segundo = VehiculoService.CambiarEstado(TipoVehiculo.Tracto, id, false);
                Assert.False(segundo.Cambiado);
                Assert.Equal("El tracto ya estaba inactivo; se actualizó la lista.", segundo.Mensaje);
                Assert.False(Activo("Tracto", "idTracto", "activo", id));

                DataTable aud = AuditoriasDeEstado("Tracto", id);
                Assert.Equal(1, aud.Rows.Count);
                Assert.Equal("Estado: Activo", aud.Rows[0]["ValoresAnteriores"]);
                Assert.Equal("Estado: Inactivo", aud.Rows[0]["ValoresNuevos"]);

                Assert.True(VehiculoService.CambiarEstado(TipoVehiculo.Tracto, id, true).Cambiado);
                Assert.True(Activo("Tracto", "idTracto", "activo", id));
                Assert.Equal(2, AuditoriasDeEstado("Tracto", id).Rows.Count);
            }
        }

        [FactBD]
        public void Desactivar_dos_veces_un_semirremolque_y_un_conductor()
        {
            using (new TransaccionDePrueba())
            {
                int carreta = VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ9-988", "RANDON", "SR");
                Assert.True(VehiculoService.CambiarEstado(TipoVehiculo.Carreta, carreta, false).Cambiado);
                Assert.Equal("El semirremolque ya estaba inactivo; se actualizó la lista.",
                    VehiculoService.CambiarEstado(TipoVehiculo.Carreta, carreta, false).Mensaje);
                Assert.False(Activo("Carreta", "idCarreta", "activo", carreta));
                Assert.Equal(1, AuditoriasDeEstado("Carreta", carreta).Rows.Count);

                int conductor = RegistroConductorService.Registrar(RegistroConductorService.TipoCarnet, "9990031",
                    "PRUEBA", "ESTADO", "", "");
                Assert.True(RegistroConductorService.CambiarEstado(conductor, false).Cambiado);
                Assert.False(RegistroConductorService.CambiarEstado(conductor, false).Cambiado);
                Assert.False(Activo("Conductor", "idConductor", "activo", conductor));
                DataTable audConductor = AuditoriasDeEstado("Conductor", conductor);
                Assert.Equal(1, audConductor.Rows.Count);
                // Sin el relleno de las columnas CHAR entre nombre y apellido
                Assert.Equal("Conductor PRUEBA ESTADO desactivado", audConductor.Rows[0]["Descripcion"]);
            }
        }

        [FactBD]
        public void Cliente_peaje_planta_y_catalogo_desactivan_una_sola_vez()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = InsertarCliente();
                Assert.True(MaestrosService.CambiarEstado(MaestroSimple.Cliente, cliente, false).Cambiado);
                Assert.Equal("El cliente ya estaba inactivo; se actualizó la lista.",
                    MaestrosService.CambiarEstado(MaestroSimple.Cliente, cliente, false).Mensaje);
                Assert.Equal(1, AuditoriasDeEstado("Cliente", cliente).Rows.Count);

                int peaje = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "INSERT INTO EstacionesPeaje (nombre, activo) VALUES ('ZZ PRUEBA B5-3', 1); SELECT CAST(SCOPE_IDENTITY() AS INT);"));
                Assert.True(MaestrosService.CambiarEstado(MaestroSimple.EstacionPeaje, peaje, false).Cambiado);
                Assert.Equal("La estación de peaje ya estaba inactiva; se actualizó la lista.",
                    MaestrosService.CambiarEstado(MaestroSimple.EstacionPeaje, peaje, false).Mensaje);
                Assert.False(Activo("EstacionesPeaje", "idEstacion", "activo", peaje));
                Assert.Equal(1, AuditoriasDeEstado("EstacionesPeaje", peaje).Rows.Count);

                int planta = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "INSERT INTO Planta (nombre, esInternacional, activo) VALUES ('ZZ PRUEBA B5-3', 0, 1); SELECT CAST(SCOPE_IDENTITY() AS INT);"));
                Assert.True(MaestrosService.CambiarEstado(MaestroSimple.Planta, planta, false).Cambiado);
                Assert.False(MaestrosService.CambiarEstado(MaestroSimple.Planta, planta, false).Cambiado);
                Assert.Equal(1, AuditoriasDeEstado("Planta", planta).Rows.Count);

                // Catálogo: Crear devuelve el id real (la auditoría del alta lo usa) y "activa" en plantas de carga
                int producto = CatalogoClienteService.Crear(TipoCatalogo.Producto, cliente, "ZZ PRODUCTO B5-3", null);
                Assert.True(producto > 0);
                Assert.Equal("ZZ PRODUCTO B5-3", (string)DbHelper.EjecutarEscalar(
                    "SELECT nombre FROM Producto WHERE idProducto = @id", DbHelper.Param("@id", producto)));
                Assert.True(CatalogoClienteService.CambiarActivo(TipoCatalogo.Producto, producto, false).Cambiado);
                Assert.Equal("El producto ya estaba inactivo; se actualizó la lista.",
                    CatalogoClienteService.CambiarActivo(TipoCatalogo.Producto, producto, false).Mensaje);
                Assert.Equal(1, AuditoriasDeEstado("Producto", producto).Rows.Count);

                int plantaCarga = CatalogoClienteService.Crear(TipoCatalogo.PlantaCarga, cliente, "ZZ PLANTA B5-3", "DIR");
                Assert.True(CatalogoClienteService.CambiarActivo(TipoCatalogo.PlantaCarga, plantaCarga, false).Cambiado);
                Assert.Equal("La planta de carga ya estaba inactiva; se actualizó la lista.",
                    CatalogoClienteService.CambiarActivo(TipoCatalogo.PlantaCarga, plantaCarga, false).Mensaje);
                Assert.False(Activo("PlantaCarga", "idPlantaCarga", "activa", plantaCarga));
                Assert.Equal(1, AuditoriasDeEstado("PlantaCarga", plantaCarga).Rows.Count);
            }
        }

        private static int CrearUsuario(string nombreUsuario) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Prueba B5-8', 'x', 'SUPERVISOR', 1)",   // rol vigente: CK_Usuarios_Rol (migración 54)
                DbHelper.Param("@user", nombreUsuario)));

        private static Guid Sello(int idUsuario) =>
            (Guid)DbHelper.EjecutarEscalar("SELECT selloSesion FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));

        [FactBD]
        public void Desactivar_dos_veces_un_usuario_renueva_el_sello_y_audita_una_sola_vez()
        {
            using (new TransaccionDePrueba())
            {
                string nombre = "zz_b58_" + Guid.NewGuid().ToString("N").Substring(0, 10);
                int id = CrearUsuario(nombre);
                int otroAdmin = CrearUsuario(nombre + "_a");
                Guid selloInicial = Sello(id);

                ResultadoCambioEstado primero = UsuarioEstadoService.CambiarEstado(id, false, otroAdmin);
                Assert.True(primero.Cambiado);
                Assert.Equal($"Usuario {nombre} desactivado correctamente.", primero.Mensaje);
                Assert.False(Activo("Usuarios", "idUsuario", "activo", id));
                Guid selloDesactivado = Sello(id);
                Assert.NotEqual(selloInicial, selloDesactivado);   // sus sesiones abiertas se cierran
                Assert.False(SesionVigenteHelper.LeerEstadoDeBd(id).Activo);

                // F5 o segunda pestaña: antes lo reactivaba
                ResultadoCambioEstado segundo = UsuarioEstadoService.CambiarEstado(id, false, otroAdmin);
                Assert.False(segundo.Cambiado);
                Assert.Equal("El usuario ya estaba inactivo; se actualizó la lista.", segundo.Mensaje);
                Assert.False(Activo("Usuarios", "idUsuario", "activo", id));
                Assert.Equal(selloDesactivado, Sello(id));          // sin cambio real no se renueva

                DataTable aud = AuditoriasDeEstado("Usuarios", id);
                Assert.Equal(1, aud.Rows.Count);
                Assert.Equal("Estado: Activo", aud.Rows[0]["ValoresAnteriores"]);
                Assert.Equal("Estado: Inactivo", aud.Rows[0]["ValoresNuevos"]);
                Assert.Equal($"Usuario {nombre} desactivado", aud.Rows[0]["Descripcion"]);

                // Reactivar también renueva el sello y se audita
                Assert.True(UsuarioEstadoService.CambiarEstado(id, true, otroAdmin).Cambiado);
                Assert.True(Activo("Usuarios", "idUsuario", "activo", id));
                Assert.NotEqual(selloDesactivado, Sello(id));
                Assert.Equal(2, AuditoriasDeEstado("Usuarios", id).Rows.Count);

                // Error al final: nadie se desactiva a sí mismo; no toca la fila ni audita
                Guid selloActual = Sello(id);
                var ex = Assert.Throws<ErrorNegocioException>(() => UsuarioEstadoService.CambiarEstado(id, false, id));
                Assert.Equal(UsuarioEstadoService.MensajeAutodesactivacion, ex.Message);
                Assert.True(Activo("Usuarios", "idUsuario", "activo", id));
                Assert.Equal(selloActual, Sello(id));
                Assert.Equal(2, AuditoriasDeEstado("Usuarios", id).Rows.Count);
            }
        }

        [FactBD]
        public void Maestro_que_ya_no_existe_avisa_sin_auditar()
        {
            using (new TransaccionDePrueba())
            {
                // Error al final: no toca la BD, pero se deja aquí por la regla de la colección
                var ex = Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.CambiarEstado(TipoVehiculo.Tracto, int.MaxValue, false));
                Assert.Equal("El tracto ya no existe; recargue la página.", ex.Message);
                Assert.Equal(0, AuditoriasDeEstado("Tracto", int.MaxValue).Rows.Count);

                Assert.Throws<ErrorNegocioException>(
                    () => MaestrosService.CambiarEstado(MaestroSimple.EstacionPeaje, int.MaxValue, true));
                Assert.Throws<ErrorNegocioException>(
                    () => UsuarioEstadoService.CambiarEstado(int.MaxValue, false, 1));
            }
        }
    }
}
