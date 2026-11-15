using System;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Confirmación de identidad al firmar: el conductor vuelve a escribir su contraseña aunque ya
    /// tenga la sesión abierta. Prueba que firmó él y no otra persona con su celular desbloqueado,
    /// y queda como método de autenticación en la constancia de la firma.
    /// Tras <see cref="MaxIntentos"/> contraseñas incorrectas en <see cref="Ventana"/>, la firma de
    /// esa cuenta queda bloqueada <see cref="Bloqueo"/> (mismo criterio que el inicio de sesión).
    /// Usa <see cref="ControlIntentosLogin"/> solo por cuenta: el intento se reserva bajo candado
    /// antes de leer la BD, así N peticiones en paralelo no prueban N contraseñas.
    /// </summary>
    public static class ReautenticacionHelper
    {
        public const int MaxIntentos = 5;
        public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);

        /// <summary>Contadores en HttpRuntime.Cache (vencen solos), con el mismo almacén del login.</summary>
        private static readonly ControlIntentosLogin Control = new ControlIntentosLogin(
            ControlIntentosLogin.AlmacenCacheAspNet.Instancia, () => DateTime.UtcNow,
            porIp: null, porCuenta: new ControlIntentosLogin.Limite(MaxIntentos, Bloqueo, Ventana));

        /// <summary>Clave del contador de la firma (no choca con las del login: IP_ / USR_).</summary>
        public static string ClaveFirma(int idUsuario) => "FIRMA_" + idUsuario;

        /// <summary>
        /// Verifica la contraseña del usuario. Devuelve null si es correcta o el mensaje para mostrar.
        /// Una caída de la BD no cuenta como intento fallido.
        /// </summary>
        public static string Verificar(int idUsuario, string contrasena)
        {
            return Verificar(idUsuario, contrasena, LeerHashBd, Control);
        }

        /// <summary>
        /// Igual que <see cref="Verificar(int, string)"/>, con la lectura del hash y el control de
        /// intentos inyectables (pruebas). <paramref name="leerHash"/> devuelve el hash guardado o
        /// null si el usuario no existe o está inactivo; si lanza, se trata como caída de la BD.
        /// </summary>
        internal static string Verificar(int idUsuario, string contrasena, Func<int, string> leerHash, ControlIntentosLogin control)
        {
            if (leerHash == null) throw new ArgumentNullException(nameof(leerHash));
            if (control == null) throw new ArgumentNullException(nameof(control));

            if (idUsuario <= 0)
                return "Sesión no válida. Inicie sesión nuevamente.";
            if (string.IsNullOrEmpty(contrasena))
                return "Escribe tu contraseña para confirmar que eres tú quien firma.";

            var reserva = control.Reservar(null, ClaveFirma(idUsuario));
            if (reserva.Rechazado)
            {
                int minutos = (int)Math.Ceiling(reserva.SegundosRestantes / 60.0);
                return "Demasiadas contraseñas incorrectas. Podrá volver a firmar en " + minutos + " minuto(s).";
            }

            string hash;
            try
            {
                hash = leerHash(idUsuario);
            }
            catch (Exception ex)
            {
                control.ErrorAlValidar(reserva);
                LogSGV.Error(ex, "No se pudo verificar la contraseña al firmar (usuario {IdUsuario})", idUsuario);
                return "No se pudo verificar tu contraseña en este momento. Inténtalo de nuevo en unos minutos.";
            }

            if (!string.IsNullOrEmpty(hash) && PasswordHelper.VerificarIngresada(contrasena, hash))
            {
                control.LoginCorrecto(reserva);
                return null;
            }

            bool bloqueado = reserva.BloqueoCuenta;
            AuditoriaHelper.Registrar("FIRMA_CONTRASENA_INCORRECTA", "Usuarios", idUsuario,
                bloqueado ? "Contraseña incorrecta al firmar: firma bloqueada temporalmente" : "Contraseña incorrecta al firmar");

            return bloqueado
                ? "Demasiadas contraseñas incorrectas. La firma quedó bloqueada por " + (int)Bloqueo.TotalMinutes + " minutos."
                : "La contraseña no es correcta.";
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
