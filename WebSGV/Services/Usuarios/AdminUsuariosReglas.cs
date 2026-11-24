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

    /// <summary>Botón de una fila de <c>GestionUsuarios.aspx</c> (E5-17).</summary>
    public enum AccionFilaUsuarioTipo
    {
        Editar,
        Estado,
        Contrasena,
        QuitarSegundoFactor
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
        /// "Quitar segundo factor" (E4-14): solo sobre otro usuario que lo tiene (activo o con un alta a medias).
        /// El propio no: si lo perdió, se lo quita otro administrador de sistema o el script de emergencia.
        /// </summary>
        public static bool PuedeQuitarSegundoFactor(int idFila, int? idUsuarioActual, bool tieneSegundoFactor) =>
            tieneSegundoFactor && Decidir(idFila, idUsuarioActual).PuedeDesactivar;

        public const string MensajeQuitarSegundoFactorPropio =
            "No puede quitarse su propio segundo factor. Pídaselo a otro administrador de sistema.";

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

        // ── Botones de la fila (E5-17): grilla sin ViewState, un solo botón oculto ─────────────

        /// <summary>Código de cada acción en el argumento de la fila ("EDITAR|12", "ESTADO|12|0", ...).</summary>
        public const string CodigoEditar = "EDITAR";
        public const string CodigoEstado = "ESTADO";
        public const string CodigoContrasena = "CONTRASENA";
        public const string CodigoQuitarSegundoFactor = "QUITAR2F";

        /// <summary>
        /// data-argumento del botón de una fila: "EDITAR|id", "CONTRASENA|id", "QUITAR2F|id" o, para Activar/Desactivar,
        /// "ESTADO|id|estadoDestino" (0 = desactivar si hoy está activo, 1 = activar; B5-8: un F5 no invierte el estado).
        /// </summary>
        public static string ArgumentoFila(AccionFilaUsuarioTipo accion, object idUsuario, object activoActual = null)
        {
            string id = Convert.ToInt32(idUsuario, System.Globalization.CultureInfo.InvariantCulture)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            switch (accion)
            {
                case AccionFilaUsuarioTipo.Editar: return CodigoEditar + "|" + id;
                case AccionFilaUsuarioTipo.Contrasena: return CodigoContrasena + "|" + id;
                case AccionFilaUsuarioTipo.QuitarSegundoFactor: return CodigoQuitarSegundoFactor + "|" + id;
                case AccionFilaUsuarioTipo.Estado:
                    bool activo = activoActual != null && activoActual != DBNull.Value && Convert.ToBoolean(activoActual);
                    return CodigoEstado + "|" + id + "|" + (activo ? "0" : "1");
                default: throw new ArgumentOutOfRangeException(nameof(accion));
            }
        }

        /// <summary>
        /// Lee el argumento de <see cref="ArgumentoFila"/> que llega en un campo oculto (el usuario lo puede cambiar).
        /// Formato estricto: código exacto en mayúsculas, id entero positivo sin signos ni espacios y, solo en ESTADO,
        /// un tercer campo "0" o "1". Cualquier otra cosa da false. Leerlo no autoriza nada: cada acción vuelve a
        /// comprobar la fila propia, la reautenticación y que el usuario exista.
        /// </summary>
        public static bool TryLeerAccionFila(string argumento, out AccionFilaUsuarioTipo accion, out int idUsuario, out bool nuevoActivo)
        {
            accion = AccionFilaUsuarioTipo.Editar;
            idUsuario = 0;
            nuevoActivo = false;
            if (string.IsNullOrEmpty(argumento) || argumento.Length > 40) return false;

            string[] partes = argumento.Split('|');
            if (partes.Length < 2 || partes.Length > 3) return false;
            if (!int.TryParse(partes[1], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int id) || id <= 0)
                return false;

            switch (partes[0])
            {
                case CodigoEditar: accion = AccionFilaUsuarioTipo.Editar; break;
                case CodigoContrasena: accion = AccionFilaUsuarioTipo.Contrasena; break;
                case CodigoQuitarSegundoFactor: accion = AccionFilaUsuarioTipo.QuitarSegundoFactor; break;
                case CodigoEstado: accion = AccionFilaUsuarioTipo.Estado; break;
                default: return false;
            }

            if (accion == AccionFilaUsuarioTipo.Estado)
            {
                if (partes.Length != 3) return false;
                if (partes[2] == "1") nuevoActivo = true;
                else if (partes[2] != "0") return false;
            }
            else if (partes.Length != 2)
                return false;

            idUsuario = id;
            return true;
        }

        /// <summary>Filtro de estado aplicado (campo oculto): "", "0" o "1"; cualquier otro valor cuenta como "todos".</summary>
        public static string FiltroEstadoValido(string valor) =>
            valor == "0" || valor == "1" ? valor : string.Empty;

        /// <summary>Contador del encabezado: "95 usuarios · 89 activos" (sin cifras negativas; "1 usuario · 1 activo").</summary>
        public static string TextoContador(int total, int activos)
        {
            var es = System.Globalization.CultureInfo.GetCultureInfo("es-PE");
            total = Math.Max(0, total);
            activos = Math.Max(0, Math.Min(activos, total));
            return total.ToString("N0", es) + (total == 1 ? " usuario" : " usuarios") + " · " +
                   activos.ToString("N0", es) + (activos == 1 ? " activo" : " activos");
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
