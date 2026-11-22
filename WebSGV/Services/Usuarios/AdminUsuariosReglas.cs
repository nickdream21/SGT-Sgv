using System;

namespace WebSGV.Services.Usuarios
{
    /// <summary>Qué acciones muestra la fila de un usuario en <c>GestionUsuarios.aspx</c> (E4-12).</summary>
    public sealed class AccionesFilaUsuario
    {
        public bool PuedeDesactivar { get; set; }
        public bool PuedeRestablecerContrasena { get; set; }
        public bool PuedeCambiarRol { get; set; }
    }

    /// <summary>
    /// Reglas puras de la administración de usuarios (E4-12, M-066), probadas en <c>AdminUsuariosReglasTests</c>.
    /// Ocultar un botón solo ayuda: el servidor vuelve a rechazar cada acción (<see cref="UsuarioEstadoService"/>,
    /// <c>GestionUsuarios.aspx.cs</c>).
    /// </summary>
    public static class AdminUsuariosReglas
    {
        /// <summary>
        /// Cuánto vale una contraseña correcta del administrador para restablecer contraseñas o cambiar roles
        /// sin volver a pedirla. 5 minutos alcanzan para atender varias filas seguidas (p. ej. restablecer la de
        /// dos conductores) y acotan lo que puede hacer otra persona en un equipo que quedó abierto: menos que los
        /// 30 min de inactividad de la sesión.
        /// </summary>
        public static readonly TimeSpan VentanaReautenticacion = TimeSpan.FromMinutes(5);

        public const string MensajeRestablecerPropia =
            "Tu propia contraseña se cambia desde el menú de tu cuenta (Cambiar contraseña), no desde aquí.";

        public const string MensajeCambiarRolPropio =
            "No puede cambiar su propio rol. Pídaselo a otro administrador de sistema.";

        /// <summary>True si la fila es la del usuario conectado.</summary>
        public static bool EsFilaPropia(int idFila, int? idUsuarioActual) =>
            idUsuarioActual.HasValue && idUsuarioActual.Value > 0 && idFila == idUsuarioActual.Value;

        /// <summary>
        /// En la fila propia no hay Desactivar ni Restablecer contraseña, y el rol no se puede cambiar (el resto de
        /// la edición sí). Sin usuario conectado (no debería pasar: la página exige sesión) se ocultan las tres.
        /// </summary>
        public static AccionesFilaUsuario Decidir(int idFila, int? idUsuarioActual)
        {
            bool permitido = idUsuarioActual.HasValue && idUsuarioActual.Value > 0 && !EsFilaPropia(idFila, idUsuarioActual);
            return new AccionesFilaUsuario
            {
                PuedeDesactivar = permitido,
                PuedeRestablecerContrasena = permitido,
                PuedeCambiarRol = permitido
            };
        }

        /// <summary>
        /// Guardar el modal de usuario pide la contraseña del administrador si es un alta (cualquier rol: con la
        /// sesión abierta otra persona podría crearse una cuenta propia) o si cambia el rol de un usuario existente.
        /// Editar solo el nombre o el conductor vinculado no la pide.
        /// </summary>
        public static bool RequiereReautenticacionAlGuardar(bool esAlta, bool cambiaRol) => esAlta || cambiaRol;

        /// <summary>
        /// Activar una cuenta pide la contraseña del administrador si su rol es de administración (ADMIN,
        /// ADMINISTRADOR, ADMINISTRADOR DE TRANSPORTE o ADMINISTRADOR DE SISTEMA): reactivar una cuenta así da
        /// acceso a usuarios, liquidaciones y aprobaciones, igual que crearla. Conductores, supervisores y
        /// contabilidad se activan sin pedirla (se hace a menudo y su alcance es limitado).
        /// </summary>
        public static bool RequiereReautenticacionAlActivar(string rol)
        {
            string r = (rol ?? "").Trim().ToUpperInvariant();
            return WebSGV.Helpers.RolesHelper.EsRolAdmin(r) || r == WebSGV.Helpers.RolesHelper.ROL_ADMIN_SISTEMA;
        }

        /// <summary>
        /// True si la última contraseña correcta del administrador (<paramref name="marcaUtc"/>) sigue dentro de
        /// <see cref="VentanaReautenticacion"/>. Sin marca, o con una marca en el futuro (reloj que volvió atrás),
        /// se pide de nuevo.
        /// </summary>
        public static bool ReautenticacionVigente(DateTime? marcaUtc, DateTime ahoraUtc)
        {
            if (!marcaUtc.HasValue)
                return false;
            TimeSpan desde = ahoraUtc - marcaUtc.Value;
            return desde >= TimeSpan.Zero && desde < VentanaReautenticacion;
        }
    }
}
