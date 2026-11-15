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
    /// </summary>
    public class ControlIntentosLogin
    {
        public const int MaxFallosPorIp = 20;
        public const int MaxFallosPorCuenta = 5;
        public static readonly TimeSpan BloqueoIp = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan BloqueoCuenta = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan VentanaIntentos = TimeSpan.FromMinutes(15);

        /// <summary>Instancia del sitio, sobre HttpRuntime.Cache.</summary>
        public static readonly ControlIntentosLogin Sitio =
            new ControlIntentosLogin(new AlmacenCacheAspNet(), () => DateTime.UtcNow);

        private readonly IAlmacenIntentosLogin _almacen;
        private readonly Func<DateTime> _relojUtc;
        private readonly object _candado = new object();

        public ControlIntentosLogin(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc)
        {
            _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
            _relojUtc = relojUtc ?? throw new ArgumentNullException(nameof(relojUtc));
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
            DateTime ahora = _relojUtc();
            lock (_candado)
            {
                int segIp = SegundosBloqueo(claveIp, ahora);
                int segCuenta = SegundosBloqueo(claveCuenta, ahora);
                if (segIp > 0 || segCuenta > 0)
                    return new Reserva { Rechazado = true, SegundosRestantes = Math.Max(segIp, segCuenta) };

                var r = new Reserva { ClaveIp = claveIp, ClaveCuenta = claveCuenta };
                r.BloqueoIp = SumarFallo(claveIp, MaxFallosPorIp, BloqueoIp, ahora, out r.PrimerFalloIpPrevio);
                r.BloqueoCuenta = SumarFallo(claveCuenta, MaxFallosPorCuenta, BloqueoCuenta, ahora, out r.PrimerFalloCuentaPrevio);
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
                _almacen.Quitar(r.ClaveCuenta);
                Devolver(r.ClaveIp, r.BloqueoIp, r.PrimerFalloIpPrevio, MaxFallosPorIp, ahora);
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
                Devolver(r.ClaveIp, r.BloqueoIp, r.PrimerFalloIpPrevio, MaxFallosPorIp, ahora);
                Devolver(r.ClaveCuenta, r.BloqueoCuenta, r.PrimerFalloCuentaPrevio, MaxFallosPorCuenta, ahora);
            }
        }

        private int SegundosBloqueo(string clave, DateTime ahora)
        {
            var i = _almacen.Leer(clave);
            if (i == null || i.BloqueadoHastaUtc <= ahora) return 0;
            return Math.Max(1, (int)Math.Ceiling((i.BloqueadoHastaUtc - ahora).TotalSeconds));
        }

        /// <summary>Suma un fallo; al llegar a <paramref name="maxFallos"/> dentro de la ventana bloquea. Devuelve true si este fallo bloqueó.</summary>
        private bool SumarFallo(string clave, int maxFallos, TimeSpan duracionBloqueo, DateTime ahora, out DateTime primerFalloPrevio)
        {
            var i = _almacen.Leer(clave);
            if (i == null || ahora - i.PrimerFalloUtc > VentanaIntentos)
                i = new IntentosLogin { PrimerFalloUtc = ahora };
            primerFalloPrevio = i.PrimerFalloUtc;

            i.Fallidos++;
            bool bloqueo = false;
            if (i.Fallidos >= maxFallos)
            {
                i.BloqueadoHastaUtc = ahora.Add(duracionBloqueo);
                i.Fallidos = 0;
                i.PrimerFalloUtc = ahora;
                bloqueo = true;
            }
            Guardar(clave, i);
            return bloqueo;
        }

        /// <summary>
        /// Quita un intento reservado. Si esa reserva fue la que bloqueó, se levanta el bloqueo y
        /// el contador queda en máximo − 1 (como estaba antes de la reserva).
        /// </summary>
        private void Devolver(string clave, bool reservaBloqueo, DateTime primerFalloPrevio, int maxFallos, DateTime ahora)
        {
            var i = _almacen.Leer(clave);
            if (i == null) return;

            if (reservaBloqueo && i.BloqueadoHastaUtc > ahora)
            {
                i.BloqueadoHastaUtc = DateTime.MinValue;
                i.Fallidos = maxFallos - 1;
                i.PrimerFalloUtc = primerFalloPrevio;
                // La ventana original ya venció: esos fallos no cuentan más.
                if (ahora - i.PrimerFalloUtc > VentanaIntentos) { _almacen.Quitar(clave); return; }
            }
            else if (i.Fallidos > 0)
            {
                i.Fallidos--;
            }

            if (i.Fallidos == 0 && i.BloqueadoHastaUtc <= ahora)
                _almacen.Quitar(clave);
            else
                Guardar(clave, i);
        }

        private void Guardar(string clave, IntentosLogin i)
        {
            DateTime vence = i.PrimerFalloUtc.Add(VentanaIntentos);
            if (i.BloqueadoHastaUtc > vence) vence = i.BloqueadoHastaUtc;
            _almacen.Guardar(clave, i, vence);
        }

        /// <summary>
        /// HttpRuntime.Cache con vencimiento absoluto (antes iban en Application state y nunca se
        /// borraban) y prioridad NotRemovable: la presión de memoria no debe borrar un bloqueo antes
        /// de tiempo; las entradas vencen solas.
        /// </summary>
        private class AlmacenCacheAspNet : IAlmacenIntentosLogin
        {
            private const string Prefijo = "SGV_LoginIntentos_";

            public IntentosLogin Leer(string clave) => HttpRuntime.Cache[Prefijo + clave] as IntentosLogin;

            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) =>
                HttpRuntime.Cache.Insert(Prefijo + clave, intentos, null, venceUtc,
                    Cache.NoSlidingExpiration, CacheItemPriority.NotRemovable, null);

            public void Quitar(string clave) => HttpRuntime.Cache.Remove(Prefijo + clave);
        }
    }
}
