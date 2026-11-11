using System;
using System.Collections.Concurrent;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Confirmación de identidad al firmar: el conductor vuelve a escribir su contraseña aunque ya
    /// tenga la sesión abierta. Prueba que firmó él y no otra persona con su celular desbloqueado,
    /// y queda como método de autenticación en la constancia de la firma.
    /// Tras <see cref="MaxIntentos"/> contraseñas incorrectas en <see cref="Ventana"/>, la firma de
    /// esa cuenta queda bloqueada <see cref="Bloqueo"/> (mismo criterio que el inicio de sesión).
    /// </summary>
    public static class ReautenticacionHelper
    {
        public const int MaxIntentos = 5;
        public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);

        private sealed class Intentos
        {
            public int Fallos;
            public DateTime PrimerFalloUtc;
            public DateTime BloqueadoHastaUtc;
        }

        private static readonly ConcurrentDictionary<int, Intentos> _intentos = new ConcurrentDictionary<int, Intentos>();

        /// <summary>
        /// Verifica la contraseña del usuario. Devuelve null si es correcta o el mensaje para mostrar.
        /// Una caída de la BD no cuenta como intento fallido.
        /// </summary>
        public static string Verificar(int idUsuario, string contrasena)
        {
            if (idUsuario <= 0)
                return "Sesión no válida. Inicie sesión nuevamente.";
            if (string.IsNullOrEmpty(contrasena))
                return "Escriba su contraseña para confirmar que es usted quien firma.";

            var ahora = DateTime.UtcNow;
            if (_intentos.TryGetValue(idUsuario, out var previo))
            {
                lock (previo)
                {
                    if (previo.BloqueadoHastaUtc > ahora)
                    {
                        int minutos = (int)Math.Ceiling((previo.BloqueadoHastaUtc - ahora).TotalMinutes);
                        return "Demasiadas contraseñas incorrectas. Podrá volver a firmar en " + minutos + " minuto(s).";
                    }
                }
            }

            object hash;
            try
            {
                hash = DbHelper.EjecutarEscalar(
                    "SELECT contrasena FROM Usuarios WHERE idUsuario = @id AND activo = 1",
                    DbHelper.Param("@id", idUsuario));
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "No se pudo verificar la contraseña al firmar (usuario {IdUsuario})", idUsuario);
                return "No se pudo verificar su contraseña en este momento. Intente nuevamente en unos minutos.";
            }

            if (hash != null && hash != DBNull.Value && PasswordHelper.VerifyPassword(contrasena, hash.ToString()))
            {
                _intentos.TryRemove(idUsuario, out _);
                return null;
            }

            var registro = _intentos.GetOrAdd(idUsuario, _ => new Intentos { PrimerFalloUtc = ahora });
            bool bloqueado;
            lock (registro)
            {
                if (ahora - registro.PrimerFalloUtc > Ventana)
                {
                    registro.Fallos = 0;
                    registro.PrimerFalloUtc = ahora;
                }
                registro.Fallos++;
                bloqueado = registro.Fallos >= MaxIntentos;
                if (bloqueado)
                {
                    registro.BloqueadoHastaUtc = ahora + Bloqueo;
                    registro.Fallos = 0;
                    registro.PrimerFalloUtc = ahora;
                }
            }

            AuditoriaHelper.Registrar("FIRMA_CONTRASENA_INCORRECTA", "Usuarios", idUsuario,
                bloqueado ? "Contraseña incorrecta al firmar: firma bloqueada temporalmente" : "Contraseña incorrecta al firmar");

            return bloqueado
                ? "Demasiadas contraseñas incorrectas. La firma quedó bloqueada por " + (int)Bloqueo.TotalMinutes + " minutos."
                : "La contraseña no es correcta.";
        }
    }
}
