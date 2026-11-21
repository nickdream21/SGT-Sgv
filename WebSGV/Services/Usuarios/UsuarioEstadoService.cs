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

            // Un rol retirado (Grifo, Maquinaria, Operador) solo puede quedar en una cuenta inactiva
            // (CK_Usuarios_Rol, migración 54): sin esto la BD rechazaría el UPDATE con un error técnico.
            if (nuevoActivo)
                ValidarRolParaActivar(DbHelper.EjecutarEscalar("SELECT rol FROM Usuarios WHERE idUsuario = @id",
                    DbHelper.Param("@id", idUsuario)) as string);

            ResultadoCambioEstado r = EstadoActivoService.Cambiar(TablaUsuarios, idUsuario, nuevoActivo);
            // Nuevo sello: la caché de 60 s no debe seguir dando por vigente la sesión del usuario.
            if (r.Cambiado)
                SesionVigenteHelper.InvalidarCache(idUsuario);
            return r;
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
