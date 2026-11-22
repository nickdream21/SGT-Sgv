using System;
using System.Collections.Generic;
using System.Linq;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Reglas de "Recuperar contraseña" sin BD ni SMTP (las prueba WebSGV.Tests):
    /// límite de solicitudes, a qué usuario corresponde lo escrito y qué hacer con él.
    ///
    /// La página muestra siempre <see cref="MensajeUnico"/> (no existe, sin correo, error de BD,
    /// SMTP caído, límite alcanzado) para no revelar qué cuentas existen.
    ///
    /// Límite (<see cref="ControlIntentosLogin"/>, mismo almacén que el login, la tabla IntentoAcceso, con
    /// claves <c>RECUP_IP_</c> / <c>RECUP_USR_</c>, que no chocan con <c>IP_</c>, <c>UIP_</c>, <c>USR_</c> ni <c>REAUT_</c>):
    /// cada solicitud se reserva y nunca se devuelve (es un límite de pedidos, no de fallos) y
    /// cuenta aunque el usuario no exista. Semántica exacta:
    ///  - por cuenta (SHA-256 del texto escrito, recortado y en minúsculas; nunca el texto): las solicitudes 1.ª a 3.ª dentro de 60 min se
    ///    atienden; la 3.ª deja la cuenta bloqueada 60 min contados desde ella, así que la 4.ª se
    ///    rechaza. La ventana de 60 min empieza en la 1.ª solicitud: si pasan más de 60 min sin
    ///    llegar a 3, el contador vuelve a empezar.
    ///  - por IP: igual con 10 solicitudes en 15 min → la 11.ª se rechaza durante 15 min desde la 10.ª.
    /// Una solicitud rechazada no suma ni alarga el bloqueo.
    /// </summary>
    public static class SolicitudRecuperacion
    {
        public const string MensajeUnico =
            "Si la cuenta existe y tiene un correo registrado, recibirá las instrucciones en unos minutos. " +
            "Si no tiene correo registrado, pida al administrador del sistema que restablezca su contraseña.";

        public static readonly ControlIntentosLogin.Limite LimitePorIp =
            new ControlIntentosLogin.Limite(10, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        public static readonly ControlIntentosLogin.Limite LimitePorCuenta =
            new ControlIntentosLogin.Limite(3, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));

        /// <summary>Instancia del sitio, sobre la tabla IntentoAcceso (la caché del proceso de respaldo).</summary>
        public static readonly ControlIntentosLogin Control =
            Crear(AlmacenBdIntentos.Sitio, () => DateTime.UtcNow);

        /// <summary>Control con los límites de recuperación sobre otro almacén/reloj (pruebas).</summary>
        public static ControlIntentosLogin Crear(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc) =>
            new ControlIntentosLogin(almacen, relojUtc, LimitePorIp, LimitePorCuenta);

        private static ControlIntentosLogin Crear(IAlmacenIntentosAtomico almacen, Func<DateTime> relojUtc) =>
            new ControlIntentosLogin(almacen, relojUtc, LimitePorIp, LimitePorCuenta);

        public static string ClaveIp(string ip) =>
            "RECUP_IP_" + (string.IsNullOrWhiteSpace(ip) ? IpClienteHelper.Desconocida : ip.Trim());

        /// <summary>Lo que no tiene formato de usuario ni de correo (<see cref="ControlIntentosLogin.UsuarioConFormatoValido"/>)
        /// cuenta en esta clave fija y no se busca en la BD.</summary>
        public const string ClaveCuentaInvalida = "RECUP_USR_invalido";

        public static string ClaveCuenta(string usuarioOEmail) =>
            ControlIntentosLogin.UsuarioConFormatoValido(usuarioOEmail)
                ? "RECUP_USR_" + ControlIntentosLogin.Sha256Hex(ControlIntentosLogin.NormalizarUsuario(usuarioOEmail))
                : ClaveCuentaInvalida;

        /// <summary>Se busca la cuenta solo si lo escrito tiene formato de usuario o de correo: la colación
        /// de la BD iguala variantes (ancho completo, U+FEFF, NUL) a la cuenta real.</summary>
        public static bool SeBuscaLaCuenta(string usuarioOEmail) => ControlIntentosLogin.UsuarioConFormatoValido(usuarioOEmail);

        /// <summary>
        /// Reserva la solicitud (no se devuelve nunca). true: se atiende; false: límite alcanzado.
        /// </summary>
        public static bool Permitir(ControlIntentosLogin control, string ip, string usuarioOEmail)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));
            return !control.Reservar(ClaveIp(ip), ClaveCuenta(usuarioOEmail)).Rechazado;
        }

        /// <summary>Fila de Usuarios activa que coincide por nombre de usuario o por email.</summary>
        public class Candidato
        {
            public int IdUsuario;
            public string Email;
            public string Nombre;
            /// <summary>Coincidió por <c>nombreUsuario</c> (si no, solo por email).</summary>
            public bool PorNombreUsuario;
        }

        /// <summary>
        /// Prefiere la coincidencia por nombre de usuario. Si solo coincide por email y hay más de
        /// un usuario con ese email, es ambiguo: null (no se hace nada).
        /// </summary>
        public static Candidato Elegir(IEnumerable<Candidato> filas)
        {
            var lista = (filas ?? Enumerable.Empty<Candidato>()).Where(f => f != null).ToList();

            var porNombre = lista.Where(f => f.PorNombreUsuario).ToList();
            if (porNombre.Count > 0)
                return porNombre.Count == 1 ? porNombre[0] : null;

            return lista.Count == 1 ? lista[0] : null;
        }

        /// <summary>
        /// RestablecerContrasena: 20 tokens inválidos desde una IP en 15 min la bloquean 15 min
        /// (el 20.º bloquea, el 21.º se rechaza). Cada token se reserva antes de validarlo y se
        /// devuelve si era válido o si la BD falló (<see cref="ControlIntentosLogin.ErrorAlValidar"/>).
        /// </summary>
        public static readonly ControlIntentosLogin.Limite LimiteTokensInvalidos =
            new ControlIntentosLogin.Limite(20, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

        /// <summary>Instancia del sitio para los tokens inválidos (solo un nivel, clave <see cref="ClaveTokenInvalido"/>).</summary>
        public static readonly ControlIntentosLogin ControlTokens =
            CrearControlTokens(AlmacenBdIntentos.Sitio, () => DateTime.UtcNow);

        public static ControlIntentosLogin CrearControlTokens(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc) =>
            new ControlIntentosLogin(almacen, relojUtc, porIp: null, porCuenta: LimiteTokensInvalidos);

        private static ControlIntentosLogin CrearControlTokens(IAlmacenIntentosAtomico almacen, Func<DateTime> relojUtc) =>
            new ControlIntentosLogin(almacen, relojUtc, porIp: null, porCuenta: LimiteTokensInvalidos);

        public static string ClaveTokenInvalido(string ip) =>
            "RESET_IP_" + (string.IsNullOrWhiteSpace(ip) ? IpClienteHelper.Desconocida : ip.Trim());

        public enum Accion
        {
            /// <summary>Límite alcanzado, no existe o es ambiguo: no se hace nada.</summary>
            Ninguna,
            /// <summary>Existe pero no tiene email: no se genera token; se registra.</summary>
            SinCorreo,
            /// <summary>Generar token, guardar su hash y enviar el correo en segundo plano.</summary>
            EnviarCorreo
        }

        public static Accion Decidir(bool permitido, Candidato usuario)
        {
            if (!permitido || usuario == null) return Accion.Ninguna;
            return string.IsNullOrWhiteSpace(usuario.Email) ? Accion.SinCorreo : Accion.EnviarCorreo;
        }
    }
}
