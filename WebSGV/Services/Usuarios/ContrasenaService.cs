using System;
using System.Collections.Generic;
using System.Data;
using WebSGV.Helpers;

namespace WebSGV.Services.Usuarios
{
    /// <summary>
    /// Todo lo que guarda una contraseña nueva o un enlace de recuperación (E6-16): cambiar la propia desde el
    /// menú (Site.Master), restablecer la de otro usuario (GestionUsuarios, administrador de sistema), emitir el
    /// enlace del correo (RecuperarContrasena) y consumirlo (RestablecerContrasena). La contraseña se guarda en un
    /// solo UPDATE (<see cref="GuardarNueva"/>), que además borra el enlace pendiente y renueva el sello de sesión
    /// (cierra las sesiones abiertas con la contraseña anterior). La conversión del hash al iniciar sesión (misma
    /// contraseña, sin tocar el sello) sigue en <see cref="PasswordHelper.ActualizarHashSiCorresponde"/>.
    /// Las páginas conservan sus guardas (sesión, fila propia, reautenticación del administrador, límites por IP
    /// e igualación de tiempos) y sus mensajes; aquí van la política, el hash, el SQL y la auditoría.
    /// </summary>
    public static class ContrasenaService
    {
        public const string MensajeCamposObligatorios = "Todos los campos son obligatorios.";
        public const string MensajeNuevasNoCoinciden = "Las contraseñas nuevas no coinciden.";
        public const string MensajeIgualALaActual = "La nueva contraseña debe ser distinta de la actual.";
        public const string MensajeSinCuenta = "No se encontró la cuenta de usuario activa.";
        public const string MensajeNoCoinciden = "Las contraseñas no coinciden.";

        /// <summary>Vigencia del enlace del correo de recuperación (el correo dice "1 hora").</summary>
        public static readonly TimeSpan VigenciaToken = TimeSpan.FromHours(1);

        /// <summary>
        /// El único UPDATE de una contraseña nueva, siempre de la cuenta <c>@id</c> (con <c>@id</c> 0 no toca nada).
        /// Con <c>@hashToken</c>, además, solo si esa cuenta está activa y tiene ese enlace vigente: cambia la
        /// contraseña y consume el enlace en la misma sentencia (dos envíos con el mismo enlace no pueden ganar
        /// ambos, y solo se toca la cuenta cuya política se validó). OUTPUT ... INTO una variable de tabla:
        /// funciona aunque la tabla tenga triggers. Devuelve el idUsuario cambiado o ninguna fila.
        /// </summary>
        internal const string SqlGuardarNueva = @"
DECLARE @Actualizado TABLE (idUsuario INT);
UPDATE Usuarios
SET contrasena = @hash,
    requiereCambioContrasena = @requiereCambio,
    resetToken = NULL,
    resetTokenExpira = NULL,
    selloSesion = @sello
OUTPUT inserted.idUsuario INTO @Actualizado
WHERE @id > 0 AND idUsuario = @id
  AND (@hashToken IS NULL
       OR (resetToken = @hashToken AND resetTokenExpira > @ahora AND activo = 1));
SELECT TOP 1 idUsuario FROM @Actualizado;";

        // ── Cambiar la propia (menú de Site.Master) ───────────────────────────────

        /// <summary>Resultado del cambio de la propia contraseña.</summary>
        public sealed class CambioPropio
        {
            /// <summary>Mensaje para mostrar si no se cambió; null si se cambió.</summary>
            public string Error { get; internal set; }
            /// <summary>Sello nuevo de la cuenta: la sesión que hizo el cambio lo guarda y sigue abierta.</summary>
            public Guid SelloNuevo { get; internal set; }
            public bool Cambiada => Error == null;
        }

        /// <summary>
        /// Cambia la contraseña del usuario de la sesión. Valida en este orden (mismos mensajes que antes): campos
        /// llenos, política (<see cref="ContrasenaNuevaHelper"/>), confirmación, distinta de la actual, cuenta, y la
        /// actual con <see cref="ReautenticacionHelper"/> (mismo contador de intentos que la firma: 5 incorrectas en
        /// 15 min bloquean ambas). La nueva se guarda recortada (el autocompletado del celular agrega un espacio al
        /// final). Quita el "debe cambiar", borra el enlace pendiente y renueva el sello: se cierran las demás
        /// sesiones del usuario; la que cambió sigue abierta con <see cref="CambioPropio.SelloNuevo"/>. Los errores
        /// de BD se propagan.
        /// </summary>
        public static CambioPropio CambiarPropia(int idUsuario, string actual, string nueva, string confirmacion)
        {
            actual = actual ?? "";
            nueva = (nueva ?? "").Trim();
            confirmacion = (confirmacion ?? "").Trim();

            if (actual.Length == 0 || nueva.Length == 0 || confirmacion.Length == 0)
                return Rechazo(MensajeCamposObligatorios);

            string errorNueva = ContrasenaNuevaHelper.ValidarParaUsuario(nueva, idUsuario);
            if (errorNueva != null)
                return Rechazo(errorNueva);

            if (nueva != confirmacion)
                return Rechazo(MensajeNuevasNoCoinciden);

            if (nueva == actual || nueva == actual.Trim())
                return Rechazo(MensajeIgualALaActual);

            if (idUsuario == 0)
                return Rechazo(MensajeSinCuenta);

            string errorActual = ReautenticacionHelper.Verificar(idUsuario, actual, PropositoReautenticacion.CambioContrasena);
            if (errorActual != null)
                return Rechazo(errorActual);

            // Nuevo sello: se cierran las demás sesiones del usuario; esta sigue abierta
            // porque guarda el sello nuevo (decisión del usuario, auditoría tarea 5).
            Guid nuevoSello = Guid.NewGuid();
            GuardarNueva(idUsuario, null, nueva, requiereCambio: false, sello: nuevoSello);
            SesionVigenteHelper.InvalidarCache(idUsuario);
            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Contrasena cambiada por el usuario");
            return new CambioPropio { SelloNuevo = nuevoSello };
        }

        private static CambioPropio Rechazo(string mensaje) => new CambioPropio { Error = mensaje };

        // ── Restablecer por el administrador de sistema (GestionUsuarios) ─────────

        /// <summary>
        /// El administrador de sistema le pone una contraseña a otro usuario. La página ya comprobó que no es su
        /// propia fila y que su reautenticación está vigente (E4-12). Valida la política y la confirmación (ambas
        /// recortadas); la guarda con "debe cambiar contraseña" (el usuario la cambia al entrar), borra el enlace
        /// pendiente y renueva el sello (se cierran sus sesiones abiertas). Devuelve null si la guardó o el mensaje
        /// para mostrar. Los errores de BD se propagan.
        /// </summary>
        public static string RestablecerPorAdministrador(int idUsuario, string nueva, string confirmacion)
        {
            nueva = (nueva ?? "").Trim();
            confirmacion = (confirmacion ?? "").Trim();

            string errorNueva = ContrasenaNuevaHelper.ValidarParaUsuario(nueva, idUsuario);
            if (errorNueva != null)
                return errorNueva;

            if (nueva != confirmacion)
                return MensajeNoCoinciden;

            GuardarNueva(idUsuario, null, nueva, requiereCambio: true, sello: Guid.NewGuid());
            // Nuevo sello: las sesiones abiertas con la contraseña anterior se cierran.
            SesionVigenteHelper.InvalidarCache(idUsuario);
            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario, "Contraseña restablecida por Admin Sistema");
            return null;
        }

        // ── Enlace del correo de recuperación ──────────────────────────────────────

        /// <summary>
        /// Usuarios activos que coinciden por nombre de usuario o por email (la elección la hace
        /// <see cref="SolicitudRecuperacion.Elegir"/>). Los errores de BD se propagan.
        /// </summary>
        public static List<SolicitudRecuperacion.Candidato> CandidatosRecuperacion(string usuarioEmail)
        {
            var lista = new List<SolicitudRecuperacion.Candidato>();
            DataTable dt = DbHelper.ConsultarTabla(
                @"SELECT idUsuario, email, nombre,
                         CASE WHEN nombreUsuario = @UsuarioEmail THEN 1 ELSE 0 END AS porNombreUsuario
                  FROM Usuarios
                  WHERE (nombreUsuario = @UsuarioEmail OR email = @UsuarioEmail)
                  AND activo = 1",
                DbHelper.Param("@UsuarioEmail", usuarioEmail));

            foreach (DataRow r in dt.Rows)
            {
                lista.Add(new SolicitudRecuperacion.Candidato
                {
                    IdUsuario = Convert.ToInt32(r["idUsuario"]),
                    Email = r["email"] == DBNull.Value ? "" : r["email"].ToString().Trim(),
                    Nombre = r["nombre"] == DBNull.Value ? "" : r["nombre"].ToString(),
                    PorNombreUsuario = Convert.ToInt32(r["porNombreUsuario"]) == 1
                });
            }
            return lista;
        }

        /// <summary>
        /// Emite un enlace de recuperación para el usuario: guarda el SHA-256 del token (nunca el token en claro)
        /// con vencimiento en <see cref="VigenciaToken"/>, audita y devuelve el token en claro para el correo.
        /// Un enlace anterior pendiente queda reemplazado. null si la fila no existe. Los errores de BD se propagan.
        /// </summary>
        public static string EmitirTokenRecuperacion(int idUsuario) =>
            EmitirTokenRecuperacion(idUsuario, FechaHelper.Ahora().Add(VigenciaToken));

        /// <summary><see cref="EmitirTokenRecuperacion(int)"/> con el vencimiento a mano (pruebas).</summary>
        internal static string EmitirTokenRecuperacion(int idUsuario, DateTime expiracion)
        {
            var (token, hash) = TokenRecuperacion.Generar();
            int filas = DbHelper.EjecutarNonQuery(
                @"UPDATE Usuarios
                  SET resetToken = @Hash,
                      resetTokenExpira = @Expiracion
                  WHERE idUsuario = @IdUsuario",
                DbHelper.Param("@Hash", hash),
                DbHelper.Param("@Expiracion", expiracion),
                DbHelper.Param("@IdUsuario", idUsuario));
            if (filas <= 0)
                return null;

            AuditoriaHelper.Registrar("RECUPERAR_CONTRASENA", "Usuarios", idUsuario,
                "Enlace de recuperación generado (correo en cola de envío)");
            return token;
        }

        /// <summary>idUsuario del enlace vigente de una cuenta activa, o null si no existe o venció. Los errores de BD se propagan.</summary>
        public static int? IdUsuarioDelToken(string hashToken)
        {
            object id = DbHelper.EjecutarEscalar(
                @"SELECT TOP 1 idUsuario FROM Usuarios
                  WHERE resetToken = @Hash
                  AND resetTokenExpira > @FechaActual
                  AND activo = 1",
                DbHelper.Param("@Hash", hashToken ?? ""),
                DbHelper.Param("@FechaActual", FechaHelper.Ahora()));
            if (id == null || id == DBNull.Value) return null;
            return Convert.ToInt32(id);
        }

        /// <summary>Cómo terminó <see cref="RestablecerConToken"/>.</summary>
        public enum ResultadoEnlace
        {
            /// <summary>Contraseña guardada y enlace consumido.</summary>
            Restablecida,
            /// <summary>El enlace no existe, venció, ya se usó o la cuenta está inactiva.</summary>
            EnlaceNoVigente,
            /// <summary>La BD falló al revisar el enlace o la política (registrado).</summary>
            ErrorAlVerificar,
            /// <summary>La contraseña no cumple la política: <see cref="RestablecimientoConEnlace.Mensaje"/>.</summary>
            NoCumplePolitica,
            /// <summary>No se guardó: el enlace dejó de valer entre medio o la BD falló (registrado).</summary>
            NoSeGuardo
        }

        public sealed class RestablecimientoConEnlace
        {
            public ResultadoEnlace Resultado { get; internal set; }
            /// <summary>Mensaje de la política cuando <see cref="ResultadoEnlace.NoCumplePolitica"/>.</summary>
            public string Mensaje { get; internal set; }
            /// <summary>Usuario de la cuenta cuando <see cref="ResultadoEnlace.Restablecida"/>.</summary>
            public int? IdUsuario { get; internal set; }
        }

        /// <summary>
        /// Consume el enlace del correo (<paramref name="hashToken"/> = SHA-256 del token, nunca el token en claro):
        /// revisa que siga vigente, aplica la política con los datos de esa cuenta y guarda la contraseña (ya
        /// recortada por la página) en el mismo UPDATE que borra el enlace. Quien recupera por correo eligió su
        /// contraseña: queda sin "debe cambiar". Renueva el sello (cierra las sesiones abiertas) y audita.
        /// Los errores de BD se registran y se devuelven como resultado (la página muestra su mensaje).
        /// </summary>
        public static RestablecimientoConEnlace RestablecerConToken(string hashToken, string nueva)
        {
            if (string.IsNullOrEmpty(hashToken))
                return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.EnlaceNoVigente };

            string errorNueva;
            int? idDelEnlace;
            try
            {
                idDelEnlace = IdUsuarioDelToken(hashToken);
                if (!idDelEnlace.HasValue)
                    return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.EnlaceNoVigente };
                // Las reglas miran el usuario, el nombre y el DNI de la cuenta del enlace.
                errorNueva = ContrasenaNuevaHelper.ValidarParaUsuario(nueva, idDelEnlace.Value);
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Restablecer contraseña: error al revisar la política de contraseña");
                return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.ErrorAlVerificar };
            }
            if (errorNueva != null)
                return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.NoCumplePolitica, Mensaje = errorNueva };

            int? idUsuario;
            try
            {
                // Solo la cuenta del enlace cuya política se validó, y solo si el enlace sigue vigente.
                idUsuario = GuardarNueva(idDelEnlace.Value, hashToken, nueva, requiereCambio: false, sello: Guid.NewGuid());
            }
            catch (Exception ex)
            {
                LogSGV.Error(ex, "Restablecer contraseña: error al actualizar la contraseña");
                return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.NoSeGuardo };
            }
            if (!idUsuario.HasValue)
                return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.NoSeGuardo };

            SesionVigenteHelper.InvalidarCache(idUsuario.Value);
            AuditoriaHelper.Registrar("UPDATE", "Usuarios", idUsuario.Value,
                "Contraseña restablecida mediante enlace de recuperación");
            return new RestablecimientoConEnlace { Resultado = ResultadoEnlace.Restablecida, IdUsuario = idUsuario };
        }

        // ── El UPDATE ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Guarda el hash PBKDF2 de <paramref name="nueva"/> con <see cref="SqlGuardarNueva"/> en la cuenta
        /// <paramref name="idUsuario"/>; con <paramref name="hashToken"/>, solo si esa cuenta tiene ese enlace vigente.
        /// Devuelve el idUsuario cambiado o null si ninguna fila coincidió (con id 0 o menos, null sin ir a la BD).
        /// </summary>
        internal static int? GuardarNueva(int idUsuario, string hashToken, string nueva, bool requiereCambio, Guid sello)
        {
            if (idUsuario <= 0)
                return null;
            object id = DbHelper.EjecutarEscalar(SqlGuardarNueva,
                DbHelper.Param("@hash", PasswordHelper.HashPassword(nueva)),
                DbHelper.Param("@requiereCambio", requiereCambio),
                DbHelper.Param("@sello", sello),
                DbHelper.Param("@id", idUsuario),
                DbHelper.Param("@hashToken", (object)hashToken ?? DBNull.Value),
                DbHelper.Param("@ahora", FechaHelper.Ahora()));
            if (id == null || id == DBNull.Value) return null;
            return Convert.ToInt32(id);
        }
    }
}
