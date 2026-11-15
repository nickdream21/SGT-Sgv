using System;
using System.Web;
using System.Web.Caching;

namespace WebSGV.Helpers
{
    /// <summary>Contador de intentos de login de una clave (IP o cuenta).</summary>
    public class IntentosLogin
    {
        public int Fallidos;
        public DateTime PrimerFalloUtc;
        public DateTime BloqueadoHastaUtc;
    }

    /// <summary>Dónde se guardan los contadores: la caché de ASP.NET en el sitio, un diccionario en las pruebas.</summary>
    public interface IAlmacenIntentosLogin
    {
        IntentosLogin Leer(string clave);
        void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc);
        void Quitar(string clave);
    }

    /// <summary>
    /// Anti-fuerza-bruta del login en dos niveles:
    ///  - por IP: 20 fallos → 5 min (frena a un atacante que prueba muchas cuentas; con 5, los
    ///    errores de tipeo de toda la oficina, que sale por una sola IP pública, la bloqueaban entera);
    ///  - por cuenta: 5 fallos → 15 min (frena a quien rota IPs contra la misma cuenta). Cuenta
    ///    aunque el usuario no exista, para no revelar qué cuentas existen.
    /// El intento se <b>reserva</b> (se cuenta como fallo) antes de validar contra la BD, bajo el
    /// candado: así N peticiones en paralelo no pasan todas la verificación antes de que la primera
    /// sume su fallo. Si la contraseña era correcta o la BD falló, el intento se devuelve.
    /// Los límites se pueden cambiar por instancia (<see cref="Limite"/>); la firma electrónica
    /// usa solo el nivel de cuenta (<c>porIp: null</c>, ver <see cref="ReautenticacionHelper"/>).
    /// </summary>
    public class ControlIntentosLogin
    {
        public const int MaxFallosPorIp = 20;
        public const int MaxFallosPorCuenta = 5;
        public static readonly TimeSpan BloqueoIp = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan BloqueoCuenta = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan VentanaIntentos = TimeSpan.FromMinutes(15);

        /// <summary>Instancia del sitio (login), sobre HttpRuntime.Cache.</summary>
        public static readonly ControlIntentosLogin Sitio =
            new ControlIntentosLogin(AlmacenCacheAspNet.Instancia, () => DateTime.UtcNow);

        /// <summary>Cuántos fallos dentro de <see cref="Ventana"/> bloquean, y por cuánto tiempo.</summary>
        public class Limite
        {
            public int MaxFallos { get; }
            public TimeSpan Bloqueo { get; }
            public TimeSpan Ventana { get; }

            public Limite(int maxFallos, TimeSpan bloqueo, TimeSpan ventana)
            {
                if (maxFallos < 1) throw new ArgumentOutOfRangeException(nameof(maxFallos));
                MaxFallos = maxFallos;
                Bloqueo = bloqueo;
                Ventana = ventana;
            }
        }

        private readonly IAlmacenIntentosLogin _almacen;
        private readonly Func<DateTime> _relojUtc;
        private readonly Limite _porIp;
        private readonly Limite _porCuenta;
        private readonly object _candado = new object();

        /// <summary>Límites del login: IP 20 fallos → 5 min, cuenta 5 fallos → 15 min, ventana 15 min.</summary>
        public ControlIntentosLogin(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc)
            : this(almacen, relojUtc,
                   new Limite(MaxFallosPorIp, BloqueoIp, VentanaIntentos),
                   new Limite(MaxFallosPorCuenta, BloqueoCuenta, VentanaIntentos))
        {
        }

        /// <param name="porIp">null: no se controla por IP (la clave de IP se ignora).</param>
        public ControlIntentosLogin(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc, Limite porIp, Limite porCuenta)
        {
            _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
            _relojUtc = relojUtc ?? throw new ArgumentNullException(nameof(relojUtc));
            _porIp = porIp;
            _porCuenta = porCuenta ?? throw new ArgumentNullException(nameof(porCuenta));
        }

        public class Reserva
        {
            /// <summary>Ya estaba bloqueado: no se reservó nada ni hay que devolver.</summary>
            public bool Rechazado;
            public int SegundosRestantes;
            /// <summary>Esta reserva fue la que llegó al máximo y dejó bloqueada la IP / la cuenta.</summary>
            public bool BloqueoIp;
            public bool BloqueoCuenta;
            internal string ClaveIp;
            internal string ClaveCuenta;
            /// <summary>Inicio de la ventana antes de la reserva: si esta reserva bloqueó y se devuelve,
            /// se restaura (si no, levantar el bloqueo alargaría la ventana de 15 min).</summary>
            internal DateTime PrimerFalloIpPrevio;
            internal DateTime PrimerFalloCuentaPrevio;
            /// <summary>Ya se devolvió: devolver dos veces quitaría el fallo de otro usuario.</summary>
            internal bool Devuelta;
        }

        public static string ClaveIp(string ip) => "IP_" + (ip ?? "unknown");
        public static string ClaveCuenta(string usuario) => "USR_" + (usuario ?? "").ToLowerInvariant();

        /// <summary>
        /// Si la IP o la cuenta están bloqueadas, rechaza. Si no, suma el intento como fallo a las dos.
        /// </summary>
        public Reserva Reservar(string claveIp, string claveCuenta)
        {
            if (_porIp == null) claveIp = null;
            DateTime ahora = _relojUtc();
            lock (_candado)
            {
                int segIp = SegundosBloqueo(claveIp, ahora);
                int segCuenta = SegundosBloqueo(claveCuenta, ahora);
                if (segIp > 0 || segCuenta > 0)
                    return new Reserva { Rechazado = true, SegundosRestantes = Math.Max(segIp, segCuenta) };

                var r = new Reserva { ClaveIp = claveIp, ClaveCuenta = claveCuenta };
                r.BloqueoIp = SumarFallo(claveIp, _porIp, ahora, out r.PrimerFalloIpPrevio);
                r.BloqueoCuenta = SumarFallo(claveCuenta, _porCuenta, ahora, out r.PrimerFalloCuentaPrevio);
                return r;
            }
        }

        /// <summary>
        /// Login correcto: se limpia la cuenta y se devuelve el intento a la IP (no se limpia: una
        /// cuenta válida no debe servir para "resetear" los intentos de esa IP).
        /// </summary>
        public void LoginCorrecto(Reserva r)
        {
            if (r == null || r.Rechazado) return;
            DateTime ahora = _relojUtc();
            lock (_candado)
            {
                if (r.Devuelta) return;
                r.Devuelta = true;
                if (r.ClaveCuenta != null) _almacen.Quitar(r.ClaveCuenta);
                Devolver(r.ClaveIp, r.BloqueoIp, r.PrimerFalloIpPrevio, _porIp, ahora);
            }
        }

        /// <summary>La BD falló: no se sabe si la contraseña era buena, así que no se castiga.</summary>
        public void ErrorAlValidar(Reserva r)
        {
            if (r == null || r.Rechazado) return;
            DateTime ahora = _relojUtc();
            lock (_candado)
            {
                if (r.Devuelta) return;
                r.Devuelta = true;
                Devolver(r.ClaveIp, r.BloqueoIp, r.PrimerFalloIpPrevio, _porIp, ahora);
                Devolver(r.ClaveCuenta, r.BloqueoCuenta, r.PrimerFalloCuentaPrevio, _porCuenta, ahora);
            }
        }

        private int SegundosBloqueo(string clave, DateTime ahora)
        {
            if (clave == null) return 0;
            var i = _almacen.Leer(clave);
            if (i == null || i.BloqueadoHastaUtc <= ahora) return 0;
            return Math.Max(1, (int)Math.Ceiling((i.BloqueadoHastaUtc - ahora).TotalSeconds));
        }

        /// <summary>Suma un fallo; al llegar al máximo del <paramref name="limite"/> dentro de su ventana bloquea.
        /// Devuelve true si este fallo bloqueó. Sin clave (nivel omitido) no hace nada.</summary>
        private bool SumarFallo(string clave, Limite limite, DateTime ahora, out DateTime primerFalloPrevio)
        {
            primerFalloPrevio = default(DateTime);
            if (clave == null || limite == null) return false;

            var i = _almacen.Leer(clave);
            if (i == null || ahora - i.PrimerFalloUtc > limite.Ventana)
                i = new IntentosLogin { PrimerFalloUtc = ahora };
            primerFalloPrevio = i.PrimerFalloUtc;

            i.Fallidos++;
            bool bloqueo = false;
            if (i.Fallidos >= limite.MaxFallos)
            {
                i.BloqueadoHastaUtc = ahora.Add(limite.Bloqueo);
                i.Fallidos = 0;
                i.PrimerFalloUtc = ahora;
                bloqueo = true;
            }
            Guardar(clave, i, limite);
            return bloqueo;
        }

        /// <summary>
        /// Quita un intento reservado. Si esa reserva fue la que bloqueó, se levanta el bloqueo y
        /// el contador queda en máximo − 1 (como estaba antes de la reserva).
        /// </summary>
        private void Devolver(string clave, bool reservaBloqueo, DateTime primerFalloPrevio, Limite limite, DateTime ahora)
        {
            if (clave == null || limite == null) return;
            var i = _almacen.Leer(clave);
            if (i == null) return;

            if (reservaBloqueo && i.BloqueadoHastaUtc > ahora)
            {
                i.BloqueadoHastaUtc = DateTime.MinValue;
                i.Fallidos = limite.MaxFallos - 1;
                i.PrimerFalloUtc = primerFalloPrevio;
                // La ventana original ya venció: esos fallos no cuentan más.
                if (ahora - i.PrimerFalloUtc > limite.Ventana) { _almacen.Quitar(clave); return; }
            }
            else if (i.Fallidos > 0)
            {
                i.Fallidos--;
            }

            if (i.Fallidos == 0 && i.BloqueadoHastaUtc <= ahora)
                _almacen.Quitar(clave);
            else
                Guardar(clave, i, limite);
        }

        private void Guardar(string clave, IntentosLogin i, Limite limite)
        {
            DateTime vence = i.PrimerFalloUtc.Add(limite.Ventana);
            if (i.BloqueadoHastaUtc > vence) vence = i.BloqueadoHastaUtc;
            _almacen.Guardar(clave, i, vence);
        }

        /// <summary>
        /// HttpRuntime.Cache con vencimiento absoluto (antes iban en Application state y nunca se
        /// borraban) y prioridad NotRemovable: la presión de memoria no debe borrar un bloqueo antes
        /// de tiempo; las entradas vencen solas. Lo comparten el login (claves IP_/USR_) y la firma
        /// (FIRMA_): las claves no chocan.
        /// </summary>
        internal sealed class AlmacenCacheAspNet : IAlmacenIntentosLogin
        {
            internal static readonly AlmacenCacheAspNet Instancia = new AlmacenCacheAspNet();

            private const string Prefijo = "SGV_LoginIntentos_";

            private AlmacenCacheAspNet() { }

            public IntentosLogin Leer(string clave) => HttpRuntime.Cache[Prefijo + clave] as IntentosLogin;

            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) =>
                HttpRuntime.Cache.Insert(Prefijo + clave, intentos, null, venceUtc,
                    Cache.NoSlidingExpiration, CacheItemPriority.NotRemovable, null);

            public void Quitar(string clave) => HttpRuntime.Cache.Remove(Prefijo + clave);
        }
    }
}
