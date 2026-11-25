using System;
using System.Data;
using System.Linq;
using Newtonsoft.Json.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-17: lo que hacía GestionUsuarios.aspx.cs con SQL en línea (alta, lectura por id, listados) y la búsqueda
    /// de la cuenta del ingreso de Login.aspx.cs, ahora en <see cref="GestionUsuariosService"/> e
    /// <see cref="InicioSesionHelper.BuscarCuentaParaIngreso"/>, más editar y desactivar
    /// (<see cref="UsuarioEstadoService"/>) como los usa la página. Contra la BD de pruebas dentro de una
    /// <see cref="TransaccionDePrueba"/>: usuarios y conductores inventados que no quedan guardados. Se escribieron
    /// con el SQL copiado tal cual de las páginas, antes de cambiarlas. Los casos de error van al final de cada prueba.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class GestionUsuariosServiceBdTests
    {
        public GestionUsuariosServiceBdTests() => BaseDePruebas.Preparar();

        private const string Clave = "Montana-Azul 7341";

        private static string NombreNuevo() => "zz_e617_" + Guid.NewGuid().ToString("N").Substring(0, 12);

        private static readonly Random Azar = new Random();

        /// <summary>Conductor tiene triggers: OUTPUT ... INTO una variable de tabla.</summary>
        private static int CrearConductor() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"DECLARE @n TABLE (id INT);
                  INSERT INTO Conductor (nombre, apPaterno, apMaterno, carnetExtranjeria, activo)
                  OUTPUT INSERTED.idConductor INTO @n
                  VALUES ('FICTICIO', 'PRUEBAUNO', 'PRUEBADOS', @carnet, 1);
                  SELECT id FROM @n;",
                DbHelper.Param("@carnet", "9" + Azar.Next(10000000, 99999999).ToString())));

        private static int CrearUsuarioDirecto(string nombreUsuario, string rol = "ADMIN", int? idConductor = null, bool activo = true) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo, idConductor)
                  OUTPUT INSERTED.idUsuario
                  VALUES (@user, 'Persona Ficticia', 'x', @rol, @activo, @idConductor)",
                DbHelper.Param("@user", nombreUsuario),
                DbHelper.Param("@rol", rol),
                DbHelper.Param("@activo", activo),
                DbHelper.Param("@idConductor", (object)idConductor ?? DBNull.Value)));

        private static Guid Sello(int id) =>
            (Guid)DbHelper.EjecutarEscalar("SELECT selloSesion FROM Usuarios WHERE idUsuario = @id", DbHelper.Param("@id", id));

        private static int Usuarios(string nombreUsuario) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Usuarios WHERE nombreUsuario = @u",
                DbHelper.Param("@u", nombreUsuario)));

        // ── Alta ────────────────────────────────────────────────────────────────

        [FactBD]
        public void Crear_GuardaRolHashDebeCambiarYAuditaSinLaContrasena()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = CrearConductor();
                string nombreUsuario = NombreNuevo();

                int id = GestionUsuariosService.Crear(nombreUsuario, "Persona Ficticia", RolesHelper.ROL_CONDUCTOR, Clave, idConductor);
                Assert.True(id > 0);

                DataRow r = DbHelper.ConsultarTabla(
                    @"SELECT nombreUsuario, nombre, contrasena, rol, activo, idConductor,
                             ISNULL(requiereCambioContrasena, 0) AS requiere, selloSesion
                      FROM Usuarios WHERE idUsuario = @id", DbHelper.Param("@id", id)).Rows[0];
                Assert.Equal(nombreUsuario, (string)r["nombreUsuario"]);
                Assert.Equal("Persona Ficticia", (string)r["nombre"]);
                Assert.Equal(RolesHelper.ROL_CONDUCTOR, (string)r["rol"]);
                Assert.True(Convert.ToBoolean(r["activo"]));
                Assert.Equal(idConductor, Convert.ToInt32(r["idConductor"]));
                Assert.True(Convert.ToBoolean(r["requiere"]));
                Assert.StartsWith("sha256.", (string)r["contrasena"]);
                Assert.True(PasswordHelper.VerifyPassword(Clave, (string)r["contrasena"]));
                Assert.NotEqual(Guid.Empty, (Guid)r["selloSesion"]);

                DataTable aud = DbHelper.ConsultarTabla(
                    @"SELECT Descripcion, ValoresAnteriores, ValoresNuevos FROM AuditoriaLog
                      WHERE TablaAfectada = 'Usuarios' AND IdRegistroAfectado = @id AND Accion = 'INSERT'",
                    DbHelper.Param("@id", id.ToString()));
                Assert.Equal(1, aud.Rows.Count);
                Assert.Equal($"Nuevo usuario creado: {nombreUsuario}, rol: {RolesHelper.ROL_CONDUCTOR}", (string)aud.Rows[0]["Descripcion"]);
                string nuevos = (string)aud.Rows[0]["ValoresNuevos"];
                var json = JObject.Parse(nuevos);
                Assert.Equal(nombreUsuario, (string)json["nombreUsuario"]);
                Assert.Equal(idConductor, (int)json["idConductor"]);
                Assert.Equal(1, (int)json["requiereCambioContrasena"]);
                Assert.DoesNotContain(Clave, nuevos);
                Assert.DoesNotContain("sha256.", nuevos);
                Assert.Null(json["contrasena"]);
            }
        }

        [FactBD]
        public void Crear_SinConductor_GuardaNull()
        {
            using (new TransaccionDePrueba())
            {
                int id = GestionUsuariosService.Crear(NombreNuevo(), "Persona Ficticia", RolesHelper.ROL_CONTABILIDAD, Clave, null);
                UsuarioGestion u = GestionUsuariosService.Leer(id);
                Assert.Null(u.IdConductor);
                Assert.Equal(RolesHelper.ROL_CONTABILIDAD, u.Rol);
            }
        }

        [FactBD]
        public void Crear_ConNombreRepetido_ErrorNegocioYNoCreaOtro()
        {
            using (new TransaccionDePrueba())
            {
                string nombreUsuario = NombreNuevo();
                CrearUsuarioDirecto(nombreUsuario);

                var exValidar = Assert.Throws<ErrorNegocioException>(() => GestionUsuariosService.ValidarNombreLibre(nombreUsuario));
                Assert.Equal(GestionUsuariosService.MensajeNombreRepetido, exValidar.Message);
                GestionUsuariosService.ValidarNombreLibre(NombreNuevo());   // otro nombre: sin error

                var ex = Assert.Throws<ErrorNegocioException>(() =>
                    GestionUsuariosService.Crear(nombreUsuario, "Otra Persona", "ADMIN", Clave, null));
                Assert.Equal(GestionUsuariosService.MensajeNombreRepetido, ex.Message);
                Assert.Equal(1, Usuarios(nombreUsuario));
            }
        }

        [FactBD]
        public void Crear_ConConductorDeOtroUsuario_ErrorNegocioYNoCrea()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = CrearConductor();
                int idDueno = CrearUsuarioDirecto(NombreNuevo(), RolesHelper.ROL_CONDUCTOR, idConductor);

                // El propio usuario puede conservar su conductor al editarse; otro no.
                GestionUsuariosService.ValidarConductorLibre(idConductor, idDueno);
                var exValidar = Assert.Throws<ErrorNegocioException>(() => GestionUsuariosService.ValidarConductorLibre(idConductor, 0));
                Assert.Equal(GestionUsuariosService.MensajeConductorAsociado, exValidar.Message);

                string nombreUsuario = NombreNuevo();
                var ex = Assert.Throws<ErrorNegocioException>(() =>
                    GestionUsuariosService.Crear(nombreUsuario, "Persona Ficticia", RolesHelper.ROL_CONDUCTOR, Clave, idConductor));
                Assert.Equal(GestionUsuariosService.MensajeConductorAsociado, ex.Message);
                Assert.Equal(0, Usuarios(nombreUsuario));
            }
        }

        [FactBD]
        public void Crear_ConContrasenaFueraDePolitica_ErrorNegocioYNoCrea()
        {
            using (new TransaccionDePrueba())
            {
                string nombreUsuario = NombreNuevo();
                var ex = Assert.Throws<ErrorNegocioException>(() =>
                    GestionUsuariosService.Crear(nombreUsuario, "Persona Ficticia", "ADMIN", "corta", null));
                Assert.Equal(ContrasenaNuevaHelper.ValidarParaNuevoUsuario("corta", nombreUsuario, "Persona Ficticia", null), ex.Message);
                Assert.Equal(0, Usuarios(nombreUsuario));
            }
        }

        // ── Lectura por id (antes 6 consultas en la página) ───────────────────────

        [FactBD]
        public void Leer_TraeLoQueTraianLasSeisConsultas()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = CrearConductor();
                string nombreUsuario = NombreNuevo();
                int id = CrearUsuarioDirecto(nombreUsuario, RolesHelper.ROL_CONDUCTOR, idConductor, activo: false);

                UsuarioGestion u = GestionUsuariosService.Leer(id);
                Assert.NotNull(u);                                    // existe (acción de la fila)
                Assert.Equal(id, u.IdUsuario);
                Assert.Equal(nombreUsuario, u.NombreUsuario);         // modal, activar, quitar 2.º factor, restablecer
                Assert.Equal("Persona Ficticia", u.Nombre);           // modal y auditoría de la edición
                Assert.Equal(RolesHelper.ROL_CONDUCTOR, u.Rol);       // modal, activar y edición
                Assert.Equal(idConductor, u.IdConductor);             // modal y auditoría de la edición
                Assert.False(u.Activo);

                int idSinNombre = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    @"INSERT INTO Usuarios (nombreUsuario, nombre, contrasena, rol, activo)
                      OUTPUT INSERTED.idUsuario VALUES (@u, NULL, 'x', 'ADMIN', 1)", DbHelper.Param("@u", NombreNuevo())));
                UsuarioGestion sinNombre = GestionUsuariosService.Leer(idSinNombre);
                Assert.Equal("", sinNombre.Nombre);
                Assert.Null(sinNombre.IdConductor);
                Assert.True(sinNombre.Activo);

                int inexistente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT ISNULL(MAX(idUsuario), 0) + 1000 FROM Usuarios"));
                Assert.Null(GestionUsuariosService.Leer(inexistente));
            }
        }

        // ── Listados ────────────────────────────────────────────────────────────

        [FactBD]
        public void Listar_FiltraPorTextoRolYEstado_YTotalesCuentaActivos()
        {
            using (new TransaccionDePrueba())
            {
                var (totalAntes, activosAntes) = GestionUsuariosService.Totales();
                string prefijo = "zz_e617l_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                int a = CrearUsuarioDirecto(prefijo + "_a", RolesHelper.ROL_CONTABILIDAD, activo: true);
                int b = CrearUsuarioDirecto(prefijo + "_b", RolesHelper.ROL_CONTABILIDAD, activo: false);
                int c = CrearUsuarioDirecto(prefijo + "_c", "ADMIN", activo: true);

                var (total, activos) = GestionUsuariosService.Totales();
                Assert.Equal(totalAntes + 3, total);
                Assert.Equal(activosAntes + 2, activos);

                int[] Ids(DataTable dt) => dt.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["idUsuario"])).ToArray();
                DataTable todos = GestionUsuariosService.Listar(prefijo, "", "");
                Assert.Equal(new[] { a, b, c }.OrderBy(x => x), Ids(todos).OrderBy(x => x));
                Assert.True(todos.Columns.Contains("segundoFactor"));
                Assert.Equal(new[] { a, b }.OrderBy(x => x), Ids(GestionUsuariosService.Listar(prefijo, RolesHelper.ROL_CONTABILIDAD, "")).OrderBy(x => x));
                Assert.Equal(new[] { b }, Ids(GestionUsuariosService.Listar(prefijo, "", "0")));
                Assert.Equal(new[] { a }, Ids(GestionUsuariosService.Listar(prefijo, RolesHelper.ROL_CONTABILIDAD, "1")));
                // Activos primero (ORDER BY activo DESC).
                Assert.False(Convert.ToBoolean(todos.Rows[todos.Rows.Count - 1]["activo"]));
                // El texto es literal: % y _ no son comodines.
                Assert.Empty(Ids(GestionUsuariosService.Listar(prefijo + "%", "", "")));

                Assert.Contains(RolesHelper.ROL_CONTABILIDAD, GestionUsuariosService.RolesEnUso());
            }
        }

        [FactBD]
        public void ConductoresParaVincular_MuestraElUsuarioQueYaLoTiene()
        {
            using (new TransaccionDePrueba())
            {
                int libre = CrearConductor();
                int tomado = CrearConductor();
                string nombreUsuario = NombreNuevo();
                CrearUsuarioDirecto(nombreUsuario, RolesHelper.ROL_CONDUCTOR, tomado);

                var filas = GestionUsuariosService.ConductoresParaVincular().Rows.Cast<DataRow>()
                    .ToDictionary(r => Convert.ToInt32(r["idConductor"]));
                Assert.Equal(DBNull.Value, filas[libre]["nombreUsuario"]);
                Assert.Equal(nombreUsuario, (string)filas[tomado]["nombreUsuario"]);
                Assert.Contains("FICTICIO PRUEBAUNO PRUEBADOS", (string)filas[libre]["descripcion"]);
            }
        }

        // ── Editar y desactivar como los usa la página ───────────────────────────

        [FactBD]
        public void Editar_SoloElNombreNoCambiaElSello_ElRolOElConductorSi()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuarioDirecto(NombreNuevo(), RolesHelper.ROL_CONTABILIDAD);
                Guid sello0 = Sello(id);

                Assert.Equal(1, UsuarioEstadoService.ActualizarDatos(id, "Nombre Cambiado", RolesHelper.ROL_CONTABILIDAD, null));
                Assert.Equal(sello0, Sello(id));
                Assert.Equal("Nombre Cambiado", GestionUsuariosService.Leer(id).Nombre);

                Assert.Equal(1, UsuarioEstadoService.ActualizarDatos(id, "Nombre Cambiado", "ADMIN", null));
                Guid sello1 = Sello(id);
                Assert.NotEqual(sello0, sello1);
                Assert.Equal("ADMIN", GestionUsuariosService.Leer(id).Rol);

                int idConductor = CrearConductor();
                Assert.Equal(1, UsuarioEstadoService.ActualizarDatos(id, "Nombre Cambiado", RolesHelper.ROL_CONDUCTOR, idConductor));
                Assert.NotEqual(sello1, Sello(id));
                Assert.Equal(idConductor, GestionUsuariosService.Leer(id).IdConductor);
            }
        }

        [FactBD]
        public void Desactivar_RenuevaElSello_YReactivarLoVuelveAActivar()
        {
            using (new TransaccionDePrueba())
            {
                int id = CrearUsuarioDirecto(NombreNuevo(), RolesHelper.ROL_CONTABILIDAD);
                Guid sello0 = Sello(id);

                ResultadoCambioEstado r = UsuarioEstadoService.CambiarEstado(id, false, null);
                Assert.True(r.Cambiado);
                Assert.False(GestionUsuariosService.Leer(id).Activo);
                Guid sello1 = Sello(id);
                Assert.NotEqual(sello0, sello1);

                // Ya inactivo: no cambia nada ni renueva el sello.
                Assert.False(UsuarioEstadoService.CambiarEstado(id, false, null).Cambiado);
                Assert.Equal(sello1, Sello(id));

                Assert.True(UsuarioEstadoService.CambiarEstado(id, true, null).Cambiado);
                Assert.True(GestionUsuariosService.Leer(id).Activo);

                // Error al final: nadie se desactiva a sí mismo.
                Assert.Throws<ErrorNegocioException>(() => UsuarioEstadoService.CambiarEstado(id, false, id));
                Assert.True(GestionUsuariosService.Leer(id).Activo);
            }
        }

        // ── Cuenta del ingreso (Login) ───────────────────────────────────────────

        [FactBD]
        public void BuscarCuentaParaIngreso_TraeLaCuentaActivaOInactiva_YNullSiNoExiste()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = CrearConductor();
                int id = GestionUsuariosService.Crear(NombreNuevo(), "  Persona Ficticia  ", RolesHelper.ROL_CONDUCTOR, Clave, idConductor);
                string nombreUsuario = GestionUsuariosService.Leer(id).NombreUsuario;

                CuentaIngreso c = InicioSesionHelper.BuscarCuentaParaIngreso(nombreUsuario);
                Assert.NotNull(c);
                Assert.Equal(id, c.IdUsuario);
                Assert.Equal(nombreUsuario, c.NombreUsuario);
                Assert.Equal("Persona Ficticia", c.Nombre);
                Assert.Equal(RolesHelper.ROL_CONDUCTOR, c.Rol);
                Assert.Equal(idConductor, c.IdConductor);
                Assert.True(c.Activo);
                Assert.True(c.RequiereCambioContrasena);
                Assert.Equal(Sello(id), c.SelloSesion);
                Assert.False(c.SegundoFactorActivo);
                Assert.True(PasswordHelper.VerifyPassword(Clave, c.HashContrasena));

                UsuarioEstadoService.CambiarEstado(id, false, null);
                CuentaIngreso inactiva = InicioSesionHelper.BuscarCuentaParaIngreso(nombreUsuario);
                Assert.NotNull(inactiva);
                Assert.False(inactiva.Activo);

                Assert.Null(InicioSesionHelper.BuscarCuentaParaIngreso(NombreNuevo()));
            }
        }
    }
}
