using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using WebSGV.Helpers;
using WebSGV.Services.Common;

namespace WebSGV.Services.Usuarios
{
    /// <summary>
    /// Activar / desactivar usuarios desde <c>GestionUsuarios.aspx</c> con estado destino explícito (bloque 5, B5-8),
    /// igual que los maestros (B5-3). Antes el botón invertía el estado a ciegas: con F5 o dos pestañas el
    /// administrador de sistema reactivaba al usuario que acababa de desactivar. El cambio renueva
    /// <c>selloSesion</c> en el mismo UPDATE, solo si el estado cambia de verdad: al desactivar se cierran las
    /// sesiones abiertas del usuario en su próxima petición (<see cref="SesionVigenteHelper"/>).
    /// </summary>
    public static class UsuarioEstadoService
    {
        public const string MensajeAutodesactivacion = "No puedes desactivar tu propia cuenta.";
        public const string MensajeRolNoVigente = "Asigne un rol vigente antes de activar este usuario.";

        /// <summary>M-066: desactivar o cambiar el rol del último ADMINISTRADOR DE SISTEMA activo.</summary>
        public const string MensajeUltimoAdminSistema =
            "No se puede dejar el sistema sin un administrador de sistema activo. Active o asigne otro antes de este cambio.";

        /// <summary>Acción de auditoría del intento rechazado de dejar el sistema sin administrador de sistema.</summary>
        public const string AccionUltimoAdminRechazado = "ULTIMO_ADMIN_SISTEMA_RECHAZADO";

        /// <summary>
        /// Lote que hace <c>{0}</c> (un UPDATE con <c>OUTPUT inserted.idUsuario INTO @cambiadas (id)</c>) solo si no
        /// deja el sistema sin ADMINISTRADOR DE SISTEMA activo (M-066). Con <c>@quitaAdmin = 1</c>, si el usuario
        /// <c>@id</c> es hoy un administrador de sistema activo y no hay OTRO activo, no cambia nada y devuelve
        /// <c>bloqueado = 1</c>. El candado de aplicación serializa a dos administradores que se desactivan (o se
        /// cambian el rol) mutuamente a la vez: el segundo espera, ve que el primero ya no está activo y falla, sin
        /// interbloqueo. <c>UPDLOCK, HOLDLOCK</c> retiene las filas leídas hasta el COMMIT. Sin ROLLBACK en el camino
        /// normal: dentro de otra transacción (pruebas) el COMMIT solo baja el contador.
        /// Devuelve una fila: <c>bloqueado</c> (bit) y <c>filas</c> (filas cambiadas).
        /// </summary>
        internal const string LoteConGuardaAdminSistema = @"
SET NOCOUNT ON;
SET XACT_ABORT ON;
DECLARE @bloqueado BIT = 0;
DECLARE @cambiadas TABLE (id INT);
DECLARE @candado INT;
BEGIN TRAN;
EXEC @candado = sp_getapplock @Resource = N'SGV_Usuarios_AdminSistema', @LockMode = 'Exclusive',
                              @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @candado < 0
    THROW 51201, N'Otro administrador está cambiando usuarios en este momento. Inténtelo de nuevo en unos segundos.', 1;
IF @quitaAdmin = 1
   AND EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
                WHERE idUsuario = @id AND activo = 1 AND rol = @rolAdminSistema)
   AND NOT EXISTS (SELECT 1 FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
                    WHERE rol = @rolAdminSistema AND activo = 1 AND idUsuario <> @id)
    SET @bloqueado = 1;
IF @bloqueado = 0
BEGIN
{0}
END
COMMIT;
SELECT @bloqueado AS bloqueado, (SELECT COUNT(*) FROM @cambiadas) AS filas;";

        internal static readonly TablaActivable TablaUsuarios = new TablaActivable(
            "Usuarios", "idUsuario", "activo", "nombreUsuario", "usuario", femenino: false,
            setAdicional: "selloSesion = NEWID()");

        /// <summary>
        /// Deja al usuario en <paramref name="nuevoActivo"/>. Si ya lo estaba no lo toca, no renueva el sello ni
        /// audita. Nadie se desactiva a sí mismo (<see cref="ErrorNegocioException"/>): se quedaría sin acceso y,
        /// si es el único administrador de sistema, sin quién lo reactive.
        /// </summary>
        public static ResultadoCambioEstado CambiarEstado(int idUsuario, bool nuevoActivo, int? idUsuarioActual)
        {
            if (!nuevoActivo && idUsuarioActual.HasValue && idUsuario == idUsuarioActual.Value)
                throw new ErrorNegocioException(MensajeAutodesactivacion);

            // Un rol retirado (no vigente, RolesHelper.EsRolVigente) solo puede quedar en una cuenta inactiva
            // (CK_Usuarios_Rol, migraciones 54 y 63): sin esto la BD rechazaría el UPDATE con un error técnico.
            if (nuevoActivo)
                ValidarRolParaActivar(DbHelper.EjecutarEscalar("SELECT rol FROM Usuarios WHERE idUsuario = @id",
                    DbHelper.Param("@id", idUsuario)) as string);

            ResultadoCambioEstado r = nuevoActivo
                ? EstadoActivoService.Cambiar(TablaUsuarios, idUsuario, true)
                : Desactivar(idUsuario);
            // Nuevo sello: la caché de 60 s no debe seguir dando por vigente la sesión del usuario.
            if (r.Cambiado)
                SesionVigenteHelper.InvalidarCache(idUsuario);
            return r;
        }

        /// <summary>
        /// Desactivar con la guarda del último administrador de sistema (M-066). Mismos mensajes y auditoría que
        /// <see cref="EstadoActivoService.Cambiar"/>; si ya estaba inactivo no toca nada ni audita.
        /// </summary>
        private static ResultadoCambioEstado Desactivar(int idUsuario)
        {
            object nombreBd = DbHelper.EjecutarEscalar("SELECT nombreUsuario FROM Usuarios WHERE idUsuario = @id",
                DbHelper.Param("@id", idUsuario));
            if (nombreBd == null)
                throw new ErrorNegocioException("El usuario ya no existe; recargue la página.");
            string nombre = (nombreBd as string ?? "").Trim();
            if (nombre.Length == 0) nombre = "#" + idUsuario.ToString(CultureInfo.InvariantCulture);

            int filas = EjecutarConGuarda(idUsuario, true,
                @"UPDATE dbo.Usuarios SET activo = 0, selloSesion = NEWID()
                  OUTPUT inserted.idUsuario INTO @cambiadas (id)
                   WHERE idUsuario = @id AND activo <> 0;",
                "Intento rechazado de desactivar al último administrador de sistema activo: " + nombre);

            if (filas == 0)
                return new ResultadoCambioEstado
                {
                    Cambiado = false,
                    Mensaje = EstadoActivoService.MensajeYaEstaba(TablaUsuarios.Sustantivo, TablaUsuarios.Femenino, false)
                };

            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Usuario " + nombre + " desactivado",
                valoresAnteriores: EstadoActivoService.DescribirEstado(true),
                valoresNuevos: EstadoActivoService.DescribirEstado(false));
            return new ResultadoCambioEstado
            {
                Cambiado = true,
                Mensaje = EstadoActivoService.MensajeCambiado(TablaUsuarios.Sustantivo, TablaUsuarios.Femenino, false, nombre)
            };
        }

        /// <summary>
        /// Guarda nombre, rol y conductor vinculado de un usuario existente (GestionUsuarios, Editar). Cambiar el rol
        /// o el conductor renueva <c>selloSesion</c> (cierra sus sesiones abiertas). Si el rol nuevo no es
        /// ADMINISTRADOR DE SISTEMA y el usuario es el último administrador de sistema activo, no cambia nada,
        /// audita el intento y lanza <see cref="ErrorNegocioException"/> (M-066). Devuelve las filas cambiadas.
        /// La validación del rol (vigente, propio) y la auditoría del cambio las hace la página.
        /// </summary>
        public static int ActualizarDatos(int idUsuario, string nombre, string rol, int? idConductor)
        {
            string rolNormalizado = (rol ?? "").Trim().ToUpperInvariant();
            bool quitaAdmin = rolNormalizado != RolesHelper.ROL_ADMIN_SISTEMA;
            int filas = EjecutarConGuarda(idUsuario, quitaAdmin,
                @"UPDATE dbo.Usuarios
                     SET selloSesion = CASE WHEN ISNULL(rol, '') <> @rol
                                              OR ISNULL(idConductor, -1) <> ISNULL(@idConductor, -1)
                                            THEN NEWID() ELSE selloSesion END,
                         nombre = @nombre, rol = @rol, idConductor = @idConductor
                  OUTPUT inserted.idUsuario INTO @cambiadas (id)
                   WHERE idUsuario = @id;",
                "Intento rechazado de cambiar el rol del último administrador de sistema activo a " + rolNormalizado,
                DbHelper.Param("@nombre", nombre),
                DbHelper.Param("@rol", rolNormalizado),
                DbHelper.Param("@idConductor", idConductor.HasValue ? (object)idConductor.Value : DBNull.Value));
            SesionVigenteHelper.InvalidarCache(idUsuario);
            return filas;
        }

        /// <summary>
        /// Corre <paramref name="update"/> dentro de <see cref="LoteConGuardaAdminSistema"/>. Si la guarda lo
        /// bloquea, audita <paramref name="descripcionRechazo"/> y lanza <see cref="ErrorNegocioException"/>.
        /// </summary>
        private static int EjecutarConGuarda(int idUsuario, bool quitaAdmin, string update, string descripcionRechazo,
            params SqlParameter[] extra)
        {
            var parametros = new List<SqlParameter>
            {
                DbHelper.Param("@id", idUsuario),
                DbHelper.Param("@quitaAdmin", quitaAdmin),
                DbHelper.Param("@rolAdminSistema", RolesHelper.ROL_ADMIN_SISTEMA)
            };
            parametros.AddRange(extra);

            DataTable dt = DbHelper.ConsultarTabla(LoteConGuardaAdminSistema.Replace("{0}", update), parametros.ToArray());
            DataRow r = dt.Rows[0];
            if (Convert.ToBoolean(r["bloqueado"], CultureInfo.InvariantCulture))
            {
                AuditoriaHelper.Registrar(AccionUltimoAdminRechazado, "Usuarios", idUsuario, descripcionRechazo);
                throw new ErrorNegocioException(MensajeUltimoAdminSistema);
            }
            return Convert.ToInt32(r["filas"], CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Rechaza activar una cuenta cuyo rol no es vigente (<see cref="RolesHelper.EsRolVigente"/>). Un rol
        /// null (usuario inexistente) no se valida aquí: lo resuelve <see cref="EstadoActivoService"/>.
        /// </summary>
        internal static void ValidarRolParaActivar(string rol)
        {
            if (rol != null && !RolesHelper.EsRolVigente(rol))
                throw new ErrorNegocioException(MensajeRolNoVigente);
        }
    }
}
