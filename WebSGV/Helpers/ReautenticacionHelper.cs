using System;

namespace WebSGV.Helpers
{
    /// <summary>Para qué se pide la contraseña de nuevo (cambia mensajes y auditoría, no el contador).</summary>
    public enum PropositoReautenticacion
    {
        /// <summary>Firmar una declaración o conformidad.</summary>
        Firma,
        /// <summary>Confirmar la contraseña actual antes de cambiarla.</summary>
        CambioContrasena,
        /// <summary>
        /// El administrador de sistema confirma su contraseña antes de restablecer la de otro usuario o
        /// cambiarle el rol en GestionUsuarios (E4-12).
        /// </summary>
        AdminUsuarios
    }

    /// <summary>
    /// Confirmación de identidad con la sesión ya abierta: al firmar, el conductor vuelve a escribir
    /// su contraseña (prueba que firmó él y no otra persona con su celular desbloqueado, y queda como
    /// método de autenticación en la constancia de la firma); al cambiar la contraseña, se confirma
    /// la actual.
    /// Tras <see cref="MaxIntentos"/> contraseñas incorrectas en <see cref="Ventana"/>, la cuenta queda
    /// bloqueada <see cref="Bloqueo"/> para firmar y para cambiar la contraseña (mismo criterio que el
    /// inicio de sesión). Ambos propósitos comparten UN contador por usuario: alternarlos no da más
    /// intentos. Usa <see cref="ControlIntentosLogin"/> solo por cuenta: el intento se reserva bajo
    /// candado antes de leer la BD, así N peticiones en paralelo no prueban N contraseñas.
    /// </summary>
    public static class ReautenticacionHelper
    {
        public const int MaxIntentos = 5;
        public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);

        /// <summary>Mismo tope que el inicio de sesión: más largo no se verifica (no se gasta PBKDF2).</summary>
        public const int LargoMaximoIngresada = 200;

        /// <summary>Acción de auditoría de una contraseña incorrecta del administrador en Gestión de usuarios.</summary>
        public const string AccionAdminUsuariosIncorrecta = "ADMIN_USUARIOS_CONTRASENA_INCORRECTA";

        /// <summary>Contadores en la tabla IntentoAcceso (sobreviven al reciclaje), con el mismo almacén del login.</summary>
        private static readonly ControlIntentosLogin Control = new ControlIntentosLogin(
            AlmacenBdIntentos.Sitio, () => DateTime.UtcNow,
            porIp: null, porCuenta: new ControlIntentosLogin.Limite(MaxIntentos, Bloqueo, Ventana));

        /// <summary>
        /// Clave del contador, la misma para firmar y para cambiar la contraseña (no choca con las del
        /// login: IP_ / UIP_ / USR_, ni con las de recuperación: RECUP_ / RESET_). Lleva el id, no texto del usuario.
        /// </summary>
        public static string ClaveReautenticacion(int idUsuario) => "REAUT_" + idUsuario;

        /// <summary>
        /// Verifica la contraseña del usuario para firmar. Devuelve null si es correcta o el mensaje
        /// para mostrar. Una caída de la BD no cuenta como intento fallido.
        /// </summary>
        public static string Verificar(int idUsuario, string contrasena)
        {
            return Verificar(idUsuario, contrasena, PropositoReautenticacion.Firma);
        }

        /// <summary>
        /// Verifica la contraseña del usuario para el propósito indicado. Devuelve null si es correcta
        /// o el mensaje para mostrar. Una caída de la BD no cuenta como intento fallido.
        /// </summary>
        public static string Verificar(int idUsuario, string contrasena, PropositoReautenticacion proposito)
        {
            return Verificar(idUsuario, contrasena, proposito, LeerHashBd, Control);
        }

        /// <summary>Firma, con la lectura del hash, el control y la auditoría inyectables (pruebas).</summary>
        internal static string Verificar(int idUsuario, string contrasena, Func<int, string> leerHash, ControlIntentosLogin control,
            Action<string, string, int, string> auditar = null)
        {
            return Verificar(idUsuario, contrasena, PropositoReautenticacion.Firma, leerHash, control, auditar);
        }

        /// <summary>
        /// Igual que <see cref="Verificar(int, string, PropositoReautenticacion)"/>, con la lectura del
        /// hash y el control de intentos inyectables (pruebas). <paramref name="leerHash"/> devuelve el
        /// hash guardado o null si el usuario no existe o está inactivo; si lanza, se trata como caída
        /// de la BD. <paramref name="auditar"/> (accion, tabla, id, descripcion) registra el intento
        /// fallido; por defecto <see cref="AuditoriaHelper.Registrar(string, string, int, string, string, string)"/>.
        /// </summary>
        internal static string Verificar(int idUsuario, string contrasena, PropositoReautenticacion proposito,
            Func<int, string> leerHash, ControlIntentosLogin control, Action<string, string, int, string> auditar = null)
        {
            if (leerHash == null) throw new ArgumentNullException(nameof(leerHash));
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (auditar == null) auditar = (accion, tabla, id, descripcion) => AuditoriaHelper.Registrar(accion, tabla, id, descripcion);

            bool esFirma = proposito == PropositoReautenticacion.Firma;
            bool esAdmin = proposito == PropositoReautenticacion.AdminUsuarios;
            string msjIncorrecta = esFirma || esAdmin ? "La contraseña no es correcta." : "La contraseña actual es incorrecta.";

            if (idUsuario <= 0)
                return "Sesión no válida. Inicie sesión nuevamente.";
            if (string.IsNullOrEmpty(contrasena))
                return esFirma
                    ? "Escribe tu contraseña para confirmar que eres tú quien firma."
                    : esAdmin
                        ? "Escribe tu contraseña para confirmar el cambio."
                        : "Escribe tu contraseña actual.";
            if (contrasena.Length > LargoMaximoIngresada)
                return msjIncorrecta;

            var reserva = control.Reservar(null, ClaveReautenticacion(idUsuario));
            if (reserva.Rechazado)
            {
                int minutos = (int)Math.Ceiling(reserva.SegundosRestantes / 60.0);
                return esFirma
                    ? "Demasiadas contraseñas incorrectas. Podrá volver a firmar en " + minutos + " minuto(s)."
                    : "Demasiadas contraseñas incorrectas. Podrá volver a intentarlo en " + minutos + " minuto(s).";
            }

            string hash;
            try
            {
                hash = leerHash(idUsuario);
            }
            catch (Exception ex)
            {
                control.ErrorAlValidar(reserva);
                LogSGV.Error(ex, esFirma
                    ? "No se pudo verificar la contraseña al firmar (usuario {IdUsuario})"
                    : esAdmin
                        ? "No se pudo verificar la contraseña del administrador en Gestión de usuarios (usuario {IdUsuario})"
                        : "No se pudo verificar la contraseña actual al cambiarla (usuario {IdUsuario})", idUsuario);
                return "No se pudo verificar tu contraseña en este momento. Inténtalo de nuevo en unos minutos.";
            }

            if (!string.IsNullOrEmpty(hash) && PasswordHelper.VerificarIngresada(contrasena, hash))
            {
                control.LoginCorrecto(reserva);
                return null;
            }

            bool bloqueado = reserva.BloqueoCuenta;
            if (esFirma)
                auditar("FIRMA_CONTRASENA_INCORRECTA", "Usuarios", idUsuario,
                    bloqueado ? "Contraseña incorrecta al firmar: firma bloqueada temporalmente" : "Contraseña incorrecta al firmar");
            else if (esAdmin)
                auditar(AccionAdminUsuariosIncorrecta, "Usuarios", idUsuario,
                    bloqueado
                        ? "Contraseña incorrecta al confirmar un cambio en Gestión de usuarios: bloqueada temporalmente"
                        : "Contraseña incorrecta al confirmar un cambio en Gestión de usuarios");
            else
                auditar("CAMBIO_CONTRASENA_INCORRECTA", "Usuarios", idUsuario,
                    bloqueado ? "Contraseña actual incorrecta al cambiarla: bloqueada temporalmente" : "Contraseña actual incorrecta al cambiarla");

            if (!bloqueado)
                return msjIncorrecta;
            if (esFirma)
                return "Demasiadas contraseñas incorrectas. La firma quedó bloqueada por " + (int)Bloqueo.TotalMinutes + " minutos.";
            if (esAdmin)
                return "Demasiadas contraseñas incorrectas. La confirmación, el cambio de contraseña y la firma quedaron bloqueados por " + (int)Bloqueo.TotalMinutes + " minutos.";
            return "Demasiadas contraseñas incorrectas. El cambio de contraseña y la firma quedaron bloqueados por " + (int)Bloqueo.TotalMinutes + " minutos.";
        }

        private static string LeerHashBd(int idUsuario)
        {
            object hash = DbHelper.EjecutarEscalar(
                "SELECT contrasena FROM Usuarios WHERE idUsuario = @id AND activo = 1",
                DbHelper.Param("@id", idUsuario));
            return hash == null || hash == DBNull.Value ? null : hash.ToString();
        }
    }
}
