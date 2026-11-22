using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using WebSGV.Helpers;

namespace WebSGV.Services.Usuarios
{
    /// <summary>Qué hace el login tras una contraseña correcta (E4-14).</summary>
    public enum DecisionSegundoFactor
    {
        /// <summary>Sin segundo factor: la sesión se inicia como siempre.</summary>
        SesionDirecta,
        /// <summary>Tiene el factor activo: pide el código antes de iniciar la sesión.</summary>
        PedirCodigo,
        /// <summary>Interruptor encendido y su rol lo exige, pero no lo tiene: debe darlo de alta antes de entrar.</summary>
        ExigirAlta
    }

    /// <summary>
    /// Deja pasar un evento como mucho una vez por intervalo (p. ej. registrar en el log el mismo error en cada login).
    /// Seguro entre hilos. Un reloj que vuelve hacia atrás deja pasar el evento.
    /// </summary>
    public sealed class LimiteFrecuencia
    {
        private readonly TimeSpan _intervalo;
        private readonly object _candado = new object();
        private DateTime? _ultimo;

        public LimiteFrecuencia(TimeSpan intervalo) { _intervalo = intervalo; }

        public bool Toca(DateTime ahoraUtc)
        {
            lock (_candado)
            {
                if (_ultimo.HasValue && ahoraUtc >= _ultimo.Value && ahoraUtc - _ultimo.Value < _intervalo)
                    return false;
                _ultimo = ahoraUtc;
                return true;
            }
        }
    }

    /// <summary>
    /// Códigos de recuperación recién generados que MiSeguridad muestra hasta que el usuario pulsa "Ya los guardé",
    /// como máximo <see cref="SegundoFactorReglas.VidaCodigosPorMostrar"/>. Así no se pierden con una recarga ni con
    /// el cambio de contraseña obligatorio (el modal no se abre mientras están en pantalla).
    /// </summary>
    [Serializable]
    public sealed class CodigosRecuperacionPorMostrar
    {
        public string[] Codigos { get; set; }
        public DateTime DesdeUtc { get; set; }
        /// <summary>Vienen de activar el factor (no de generar códigos nuevos): cambia el mensaje.</summary>
        public bool PorActivacion { get; set; }

        public bool Vigente(DateTime ahoraUtc)
        {
            if (Codigos == null || Codigos.Length == 0) return false;
            TimeSpan desde = ahoraUtc - DesdeUtc;
            return desde >= TimeSpan.Zero && desde < SegundoFactorReglas.VidaCodigosPorMostrar;
        }
    }

    /// <summary>
    /// Reglas puras del segundo factor (E4-14, opciones recomendadas de U-079), probadas en
    /// <c>SegundoFactorReglasTests</c>: decisión del login, vida del estado pendiente, formato de los códigos
    /// de recuperación y su hash.
    ///
    /// Códigos de recuperación: 16 caracteres Base32 (80 bits aleatorios, se muestran como XXXX-XXXX-XXXX-XXXX)
    /// guardados como <c>s256.&lt;sal&gt;.&lt;SHA-256(sal + código)&gt;</c>, con sal de 16 bytes por código. No se
    /// usa <see cref="PasswordHelper.HashPassword"/> (PBKDF2 de 310 000 iteraciones): al entrar se compara con
    /// los 10 códigos sin usar y serían 10 derivaciones por intento (segundos de CPU, y una vía de saturar el
    /// servidor). El hash lento protege contraseñas elegidas por personas, que son adivinables; un código de
    /// 80 bits generado por el sistema no lo es (2^80 SHA-256 no se recorren), así que basta un hash rápido
    /// con sal, como un token de acceso. Quien lea la BD tampoco puede usarlos sin la contraseña de la cuenta.
    /// </summary>
    public static class SegundoFactorReglas
    {
        /// <summary>Interruptor de Web.config: true = obligatorio para los roles de <see cref="RolesHelper.ExigeSegundoFactor"/>.</summary>
        public const string ClaveExigir = "Seguridad.ExigirSegundoFactor";

        /// <summary>Vida máxima del estado pendiente (contraseña correcta, falta el código).</summary>
        public static readonly TimeSpan VidaPendiente = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Vida del estado pendiente cuando el usuario debe dar de alta el factor antes de entrar: instalar la app,
        /// escanear el QR y escribir el primer código lleva más que dar un código. 10 minutos y no 15: el secreto se
        /// genera al abrir la pantalla (después de iniciar el estado), así nunca llega a <see cref="VidaAltaPendiente"/>
        /// mientras el estado vale y el QR que tiene delante no se reemplaza a mitad del alta.
        /// </summary>
        public static readonly TimeSpan VidaPendienteAlta = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Hasta cuándo se retoma un alta pendiente (mismo QR). sp_US_ActivarSegundoFactor la acepta hasta 15 min;
        /// 14 deja margen para que el código escrito llegue antes de que el SP la dé por vencida.
        /// </summary>
        public static readonly TimeSpan VidaAltaPendiente = TimeSpan.FromMinutes(14);

        /// <summary>Cuánto se siguen mostrando los códigos nuevos si el usuario no pulsa "Ya los guardé".</summary>
        public static readonly TimeSpan VidaCodigosPorMostrar = TimeSpan.FromMinutes(10);

        public const int CantidadCodigosRecuperacion = 10;
        public const int LargoCodigoRecuperacion = 16;
        public const int MaxIntentos = 5;
        public static readonly TimeSpan Bloqueo = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

        /// <summary>Emisor que muestra la app autenticadora.</summary>
        public const string Emisor = "SGV";

        private const string PrefijoHash = "s256";
        private const int BytesSal = 16;
        private const string AlfabetoBase32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        /// <summary>Tabla de la migración 62 que leen el login y Gestión de usuarios.</summary>
        public const string TablaFactor = "UsuarioSegundoFactor";

        /// <summary>True si el error SQL es "Invalid object name" (208) de <see cref="TablaFactor"/>: falta la migración 62.</summary>
        public static bool EsFaltaTablaFactor(int numeroSql, string mensaje) =>
            numeroSql == 208 && mensaje != null && mensaje.IndexOf(TablaFactor, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// Consulta que lee <see cref="TablaFactor"/>, tolerando una publicación sin la migración 62: con el
        /// interruptor apagado, si falta la tabla se registra (<paramref name="registrar"/>) y se repite la consulta
        /// sin ella (<c>consultar(false)</c>: nadie tiene el factor). Con el interruptor encendido el error sigue
        /// (falla cerrado: no se entra sin poder comprobar el factor). Cualquier otro error también sigue.
        /// </summary>
        public static T ConsultarToleraFaltaTabla<T>(Func<bool, T> consultar, bool interruptor,
            Func<Exception, bool> esFaltaTabla, Action<Exception> registrar)
        {
            try
            {
                return consultar(true);
            }
            catch (Exception ex) when (!interruptor && esFaltaTabla(ex))
            {
                registrar?.Invoke(ex);
                return consultar(false);
            }
        }

        /// <summary>Valor del interruptor (false si falta o no es "true").</summary>
        public static bool ExigirSegundoFactor
        {
            get
            {
                bool valor;
                return bool.TryParse(ConfigurationManager.AppSettings[ClaveExigir], out valor) && valor;
            }
        }

        /// <summary>
        /// Tras la contraseña correcta: quien tiene el factor activo da el código (con el interruptor apagado
        /// también: lo activó él); sin factor, el interruptor encendido y un rol que lo lleva obligan al alta;
        /// en cualquier otro caso la sesión empieza como siempre.
        /// </summary>
        public static DecisionSegundoFactor Decidir(bool interruptor, string rol, bool tieneFactorActivo)
        {
            if (tieneFactorActivo)
                return DecisionSegundoFactor.PedirCodigo;
            if (interruptor && RolesHelper.ExigeSegundoFactor(rol))
                return DecisionSegundoFactor.ExigirAlta;
            return DecisionSegundoFactor.SesionDirecta;
        }

        /// <summary>True si el estado pendiente iniciado en <paramref name="inicioUtc"/> sigue valiendo (reloj hacia atrás: no).</summary>
        public static bool PendienteVigente(DateTime inicioUtc, DateTime ahoraUtc, bool exigirAlta = false)
        {
            TimeSpan desde = ahoraUtc - inicioUtc;
            return desde >= TimeSpan.Zero && desde < (exigirAlta ? VidaPendienteAlta : VidaPendiente);
        }

        /// <summary>True si el alta pendiente guardada en <paramref name="pendienteDesdeUtc"/> se puede retomar (mismo QR).</summary>
        public static bool AltaPendienteRetomable(DateTime? pendienteDesdeUtc, DateTime ahoraUtc)
        {
            if (!pendienteDesdeUtc.HasValue) return false;
            TimeSpan desde = ahoraUtc - pendienteDesdeUtc.Value;
            return desde >= TimeSpan.Zero && desde < VidaAltaPendiente;
        }

        /// <summary>
        /// Mensaje de bloqueo, el mismo en el intento que bloquea y en los siguientes; solo cambia la espera
        /// (redondeada hacia arriba, mínimo 1 minuto).
        /// </summary>
        public static string MensajeBloqueo(int segundosRestantes) =>
            "Demasiados códigos incorrectos. Por seguridad, espere " +
            Math.Max(1, (int)Math.Ceiling(segundosRestantes / 60.0)) + " minuto(s) antes de volver a intentarlo.";

        /// <summary>Clave del contador de intentos en IntentoAcceso (E4-4): TOTP_ + SHA-256 del id (69 caracteres).</summary>
        public static string ClaveIntentos(int idUsuario) =>
            "TOTP_" + ControlIntentosLogin.Sha256Hex(idUsuario.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Lo escrito sin espacios ni guiones; null si no es un código TOTP de 6 dígitos.</summary>
        public static string NormalizarCodigoTotp(string escrito)
        {
            string s = QuitarSeparadores(escrito);
            if (s.Length != Totp.Digitos) return null;
            foreach (char c in s)
                if (c < '0' || c > '9') return null;
            return s;
        }

        /// <summary>
        /// Lo escrito como código de recuperación: sin espacios ni guiones, en mayúsculas y con 0→O, 1→I, 8→B
        /// (Base32 no tiene esos dígitos y se confunden al copiarlos a mano). Null si no son 16 caracteres Base32.
        /// </summary>
        public static string NormalizarCodigoRecuperacion(string escrito)
        {
            string s = QuitarSeparadores(escrito).ToUpperInvariant();
            if (s.Length != LargoCodigoRecuperacion) return null;
            var sb = new StringBuilder(s.Length);
            foreach (char c0 in s)
            {
                char c = c0 == '0' ? 'O' : c0 == '1' ? 'I' : c0 == '8' ? 'B' : c0;
                if (AlfabetoBase32.IndexOf(c) < 0) return null;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Código de recuperación nuevo: 16 caracteres Base32 (80 bits de un generador criptográfico).</summary>
        public static string GenerarCodigoRecuperacion()
        {
            var bytes = new byte[10];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Base32.Codificar(bytes);
        }

        /// <summary>XXXX-XXXX-XXXX-XXXX para mostrarlo.</summary>
        public static string FormatearCodigoRecuperacion(string codigo) => Agrupar(codigo, 4, "-");

        /// <summary>Secreto Base32 en grupos de 4 separados por espacio, para copiarlo a mano en la app.</summary>
        public static string AgruparSecreto(string base32) => Agrupar(base32, 4, " ");

        /// <summary>Hash de un código ya normalizado: s256.&lt;sal b64&gt;.&lt;SHA-256(sal + código) b64&gt;.</summary>
        public static string HashCodigoRecuperacion(string codigoNormalizado)
        {
            if (string.IsNullOrEmpty(codigoNormalizado)) throw new ArgumentException("Falta el código.", nameof(codigoNormalizado));
            var sal = new byte[BytesSal];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(sal);
            return PrefijoHash + "." + Convert.ToBase64String(sal) + "." + Convert.ToBase64String(Calcular(sal, codigoNormalizado));
        }

        /// <summary>True si el código normalizado corresponde al hash (comparación en tiempo constante). Un hash mal formado: false.</summary>
        public static bool VerificarCodigoRecuperacion(string codigoNormalizado, string hash)
        {
            if (string.IsNullOrEmpty(codigoNormalizado) || string.IsNullOrEmpty(hash)) return false;
            string[] partes = hash.Split('.');
            if (partes.Length != 3 || partes[0] != PrefijoHash) return false;
            byte[] sal, esperado;
            try
            {
                sal = Convert.FromBase64String(partes[1]);
                esperado = Convert.FromBase64String(partes[2]);
            }
            catch (FormatException) { return false; }
            byte[] calculado = Calcular(sal, codigoNormalizado);
            if (calculado.Length != esperado.Length) return false;
            int diferencia = 0;
            for (int i = 0; i < calculado.Length; i++)
                diferencia |= calculado[i] ^ esperado[i];
            return diferencia == 0;
        }

        private static byte[] Calcular(byte[] sal, string codigo)
        {
            byte[] texto = Encoding.UTF8.GetBytes(codigo);
            var datos = new byte[sal.Length + texto.Length];
            Buffer.BlockCopy(sal, 0, datos, 0, sal.Length);
            Buffer.BlockCopy(texto, 0, datos, sal.Length, texto.Length);
            using (var sha = SHA256.Create())
                return sha.ComputeHash(datos);
        }

        private static string QuitarSeparadores(string escrito)
        {
            if (string.IsNullOrEmpty(escrito)) return "";
            var sb = new StringBuilder(escrito.Length);
            foreach (char c in escrito)
                if (!char.IsWhiteSpace(c) && c != '-') sb.Append(c);
            return sb.ToString();
        }

        private static string Agrupar(string texto, int tamano, string separador)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < texto.Length; i += tamano)
            {
                if (i > 0) sb.Append(separador);
                sb.Append(texto, i, Math.Min(tamano, texto.Length - i));
            }
            return sb.ToString();
        }
    }
}
