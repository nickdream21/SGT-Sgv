using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;

namespace WebSGV.Helpers
{
    /// <summary>Contador de intentos de una clave (IP, cuenta, cuenta desde una IP...).</summary>
    public class IntentosLogin
    {
        public int Fallidos;
        public DateTime PrimerFalloUtc;
        public DateTime BloqueadoHastaUtc;
    }

    /// <summary>Almacén simple de contadores (clave → contador): la caché de ASP.NET (respaldo del
    /// sitio) o un diccionario en las pruebas. Las reglas las aplica <see cref="AlmacenAtomicoEnProceso"/>.</summary>
    public interface IAlmacenIntentosLogin
    {
        IntentosLogin Leer(string clave);
        void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc);
        void Quitar(string clave);
    }

    /// <summary>Un nivel de una reserva: su clave y su límite.</summary>
    public sealed class NivelIntento
    {
        public string Clave;
        public ControlIntentosLogin.Limite Limite;
    }

    /// <summary>Resultado de reservar un intento en varios niveles a la vez.</summary>
    public sealed class ResultadoReservaIntentos
    {
        /// <summary>Algún nivel ya estaba bloqueado: no se sumó nada.</summary>
        public bool Rechazado;
        public int SegundosRestantes;
        /// <summary>Por nivel (mismo orden): esta reserva lo dejó bloqueado.</summary>
        public bool[] Bloqueos;
        /// <summary>Por nivel: inicio de la ventana antes de la reserva (para devolverla).</summary>
        public DateTime[] PrimerFalloPrevio;
    }

    /// <summary>
    /// Operaciones atómicas sobre los contadores: leer, decidir y sumar en un solo paso. En el sitio,
    /// <see cref="AlmacenBdIntentos"/> (un SP por operación, sin candado del proceso); en el respaldo y
    /// en las pruebas, <see cref="AlmacenAtomicoEnProceso"/> (con su candado).
    /// </summary>
    public interface IAlmacenIntentosAtomico
    {
        ResultadoReservaIntentos Reservar(IList<NivelIntento> niveles, DateTime ahoraUtc);
        void Devolver(string clave, bool reservaBloqueo, DateTime primerFalloPrevio, ControlIntentosLogin.Limite limite, DateTime ahoraUtc);
        void Quitar(string clave);
    }

    /// <summary>
    /// Anti-fuerza-bruta por niveles. El login usa tres (<see cref="CrearLogin"/>, <see cref="ReservarLogin"/>):
    ///  - por IP: 20 fallos en 15 min → 5 min (frena a un atacante que prueba muchas cuentas; con 5, los
    ///    errores de tipeo de toda la oficina, que sale por una sola IP pública, la bloqueaban entera);
    ///  - por cuenta desde una IP: 5 fallos en 15 min → 15 min (no bloquea esa cuenta desde otra IP);
    ///  - por cuenta, desde cualquier IP: 30 fallos en 1 h → 15 min (frena a quien rota IPs).
    /// Los de cuenta cuentan aunque el usuario no exista, para no revelar qué cuentas existen. Las claves
    /// de cuenta son el SHA-256 de lo escrito, nunca el texto (a veces se escribe la contraseña ahí). Un
    /// usuario con caracteres fuera de <see cref="UsuarioConFormatoValido"/> cuenta en una clave fija
    /// (<c>USR_invalido</c>): la colación de la BD trata variantes (ancho completo, U+FEFF, NUL) como la
    /// misma cuenta, y cada variante daría otro hash para esquivar el límite.
    /// El intento se <b>reserva</b> (se cuenta como fallo) antes de validar contra la BD, en una sola
    /// operación atómica del almacén: así N peticiones en paralelo no pasan todas la verificación antes de
    /// que la primera sume su fallo. Si la contraseña era correcta o la BD falló, el intento se devuelve.
    /// Las instancias de dos niveles (IP y cuenta, <see cref="Reservar"/>) las usan la firma
    /// (<c>porIp: null</c>, ver <see cref="ReautenticacionHelper"/>), la recuperación de contraseña y
    /// VerificarDocumento.
    /// </summary>
    public class ControlIntentosLogin
    {
        public const int MaxFallosPorIp = 20;
        /// <summary>Fallos de una cuenta desde una misma IP.</summary>
        public const int MaxFallosPorCuenta = 5;
        /// <summary>Fallos de una cuenta desde cualquier IP, dentro de <see cref="VentanaCuentaGlobal"/>.</summary>
        public const int MaxFallosCuentaGlobal = 30;
        public static readonly TimeSpan BloqueoIp = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan BloqueoCuenta = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan VentanaIntentos = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan VentanaCuentaGlobal = TimeSpan.FromHours(1);
        public static readonly TimeSpan BloqueoCuentaGlobal = TimeSpan.FromMinutes(15);

        /// <summary>Clave fija de cuenta para lo que no tiene formato de usuario (<see cref="UsuarioConFormatoValido"/>).</summary>
        public const string ClaveCuentaInvalida = "USR_invalido";

        /// <summary>Instancia del sitio (login, tres niveles), sobre la tabla IntentoAcceso.</summary>
        public static readonly ControlIntentosLogin Sitio = CrearLogin(AlmacenBdIntentos.Sitio, () => DateTime.UtcNow);

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

        /// <summary>Un nivel: su límite y si es de IP (al acertar, la IP solo recupera el intento;
        /// los de cuenta se limpian).</summary>
        private sealed class Nivel
        {
            public readonly Limite Limite;
            public readonly bool EsIp;
            public Nivel(Limite limite, bool esIp) { Limite = limite; EsIp = esIp; }
        }

        private readonly IAlmacenIntentosAtomico _almacen;
        private readonly Func<DateTime> _relojUtc;
        /// <summary>Un elemento null = nivel omitido (su clave se ignora).</summary>
        private readonly Nivel[] _niveles;

        /// <summary>Dos niveles: IP 20 fallos → 5 min, cuenta 5 fallos → 15 min, ventana 15 min.</summary>
        public ControlIntentosLogin(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc)
            : this(EnProceso(almacen), relojUtc,
                   new Limite(MaxFallosPorIp, BloqueoIp, VentanaIntentos),
                   new Limite(MaxFallosPorCuenta, BloqueoCuenta, VentanaIntentos))
        {
        }

        /// <param name="porIp">null: no se controla por IP (la clave de IP se ignora).</param>
        public ControlIntentosLogin(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc, Limite porIp, Limite porCuenta)
            : this(EnProceso(almacen), relojUtc, porIp, porCuenta)
        {
        }

        /// <param name="porIp">null: no se controla por IP (la clave de IP se ignora).</param>
        public ControlIntentosLogin(IAlmacenIntentosAtomico almacen, Func<DateTime> relojUtc, Limite porIp, Limite porCuenta)
            : this(almacen, relojUtc, new[]
              {
                  porIp == null ? null : new Nivel(porIp, esIp: true),
                  new Nivel(porCuenta ?? throw new ArgumentNullException(nameof(porCuenta)), esIp: false)
              })
        {
        }

        private ControlIntentosLogin(IAlmacenIntentosAtomico almacen, Func<DateTime> relojUtc, Nivel[] niveles)
        {
            _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
            _relojUtc = relojUtc ?? throw new ArgumentNullException(nameof(relojUtc));
            _niveles = niveles;
        }

        private static IAlmacenIntentosAtomico EnProceso(IAlmacenIntentosLogin almacen) =>
            new AlmacenAtomicoEnProceso(almacen ?? throw new ArgumentNullException(nameof(almacen)));

        /// <summary>Control del login con los tres niveles (IP, cuenta desde la IP, cuenta global).</summary>
        public static ControlIntentosLogin CrearLogin(IAlmacenIntentosLogin almacen, Func<DateTime> relojUtc) =>
            CrearLogin(EnProceso(almacen), relojUtc);

        /// <summary>Control del login con los tres niveles sobre un almacén atómico (el de la BD).</summary>
        public static ControlIntentosLogin CrearLogin(IAlmacenIntentosAtomico almacen, Func<DateTime> relojUtc) =>
            new ControlIntentosLogin(almacen, relojUtc, new[]
            {
                new Nivel(new Limite(MaxFallosPorIp, BloqueoIp, VentanaIntentos), esIp: true),
                new Nivel(new Limite(MaxFallosPorCuenta, BloqueoCuenta, VentanaIntentos), esIp: false),
                new Nivel(new Limite(MaxFallosCuentaGlobal, BloqueoCuentaGlobal, VentanaCuentaGlobal), esIp: false)
            });

        public class Reserva
        {
            /// <summary>Ya estaba bloqueado: no se reservó nada ni hay que devolver.</summary>
            public bool Rechazado;
            public int SegundosRestantes;
            /// <summary>Esta reserva fue la que llegó al máximo y dejó bloqueada la IP.</summary>
            public bool BloqueoIp;
            /// <summary>Esta reserva dejó bloqueada la cuenta (desde la IP o en todas).</summary>
            public bool BloqueoCuenta;
            /// <summary>Login: el bloqueo de cuenta es solo desde esta IP (si no, es de la cuenta en todas).</summary>
            public bool BloqueoCuentaSoloDesdeIp;
            internal string[] Claves;
            internal bool[] Bloqueos;
            /// <summary>Inicio de la ventana antes de la reserva: si esta reserva bloqueó y se devuelve,
            /// se restaura (si no, levantar el bloqueo alargaría la ventana).</summary>
            internal DateTime[] PrimerFalloPrevio;
            /// <summary>Ya se devolvió: devolver dos veces quitaría el fallo de otro usuario.</summary>
            internal bool Devuelta;
        }

        /// <summary>SHA-256 en hexadecimal (minúsculas) del texto en UTF-8.</summary>
        public static string Sha256Hex(string texto)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(texto ?? ""));
                var sb = new StringBuilder(h.Length * 2);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static readonly Regex FormatoUsuario =
            new Regex(@"^[A-Za-z0-9._@-]+\z", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Lo escrito (recortado) tiene solo letras ASCII, dígitos y <c>. _ @ -</c>: así son todos los
        /// usuarios y correos de la BD (95 de 95 el 2026-11-21). Con cualquier otro carácter el login y la
        /// recuperación responden lo mismo sin buscar la cuenta, porque la colación de <c>Usuarios</c>
        /// (<c>Modern_Spanish_CI_AS</c>) iguala variantes de ancho completo, U+FEFF o NUL a la cuenta real.
        /// </summary>
        public static bool UsuarioConFormatoValido(string usuario) =>
            usuario != null && FormatoUsuario.IsMatch(usuario.Trim());

        /// <summary>Lo escrito en el campo de usuario, como se compara: recortado y en minúsculas.</summary>
        public static string NormalizarUsuario(string usuario) => (usuario ?? "").Trim().ToLowerInvariant();

        public static string ClaveIp(string ip) => "IP_" + (ip ?? "unknown");
        /// <summary>Cuenta en todas las IP: <c>USR_</c> + SHA-256 del usuario (nunca el texto);
        /// <see cref="ClaveCuentaInvalida"/> si no tiene formato de usuario.</summary>
        public static string ClaveCuenta(string usuario) =>
            UsuarioConFormatoValido(usuario) ? "USR_" + Sha256Hex(NormalizarUsuario(usuario)) : ClaveCuentaInvalida;
        /// <summary>Cuenta desde una IP: <c>UIP_</c> + SHA-256 del usuario y la IP; null (nivel omitido)
        /// si el usuario no tiene formato válido.</summary>
        public static string ClaveCuentaIp(string usuario, string ip) =>
            UsuarioConFormatoValido(usuario) ? "UIP_" + Sha256Hex(NormalizarUsuario(usuario) + "\n" + (ip ?? "unknown")) : null;

        /// <summary>
        /// Instancias de dos niveles: si la IP o la cuenta están bloqueadas, rechaza. Si no, suma el
        /// intento como fallo a las dos.
        /// </summary>
        public Reserva Reservar(string claveIp, string claveCuenta)
        {
            if (_niveles.Length != 2) throw new InvalidOperationException("Este control tiene " + _niveles.Length + " niveles: use ReservarLogin.");
            return ReservarClaves(new[] { claveIp, claveCuenta });
        }

        /// <summary>Login (control de <see cref="CrearLogin"/>): reserva el intento en la IP, en la
        /// cuenta desde esa IP y en la cuenta global (un usuario sin formato válido: la IP y
        /// <see cref="ClaveCuentaInvalida"/>).</summary>
        public Reserva ReservarLogin(string ip, string usuario)
        {
            if (_niveles.Length != 3) throw new InvalidOperationException("ReservarLogin necesita el control de CrearLogin.");
            var r = ReservarClaves(new[] { ClaveIp(ip), ClaveCuentaIp(usuario, ip), ClaveCuenta(usuario) });
            if (!r.Rechazado) r.BloqueoCuentaSoloDesdeIp = r.Bloqueos[1] && !r.Bloqueos[2];
            return r;
        }

        private Reserva ReservarClaves(string[] claves)
        {
            int n = _niveles.Length;
            var niveles = new List<NivelIntento>(n);
            var indices = new List<int>(n);
            for (int k = 0; k < n; k++)
            {
                if (_niveles[k] == null || claves[k] == null) { claves[k] = null; continue; }
                niveles.Add(new NivelIntento { Clave = claves[k], Limite = _niveles[k].Limite });
                indices.Add(k);
            }

            var res = _almacen.Reservar(niveles, _relojUtc());
            if (res.Rechazado)
                return new Reserva { Rechazado = true, SegundosRestantes = Math.Max(1, res.SegundosRestantes) };

            var r = new Reserva { Claves = claves, Bloqueos = new bool[n], PrimerFalloPrevio = new DateTime[n] };
            for (int j = 0; j < indices.Count; j++)
            {
                int k = indices[j];
                r.Bloqueos[k] = res.Bloqueos[j];
                r.PrimerFalloPrevio[k] = res.PrimerFalloPrevio[j];
                if (!r.Bloqueos[k]) continue;
                if (_niveles[k].EsIp) r.BloqueoIp = true;
                else r.BloqueoCuenta = true;
            }
            return r;
        }

        /// <summary>Marca la reserva como devuelta; false si ya lo estaba (o no hay nada que devolver).</summary>
        private static bool TomarParaDevolver(Reserva r)
        {
            if (r == null || r.Rechazado) return false;
            lock (r)
            {
                if (r.Devuelta) return false;
                r.Devuelta = true;
                return true;
            }
        }

        /// <summary>
        /// Login correcto: se limpian los niveles de cuenta y se devuelve el intento a la IP (no se
        /// limpia: una cuenta válida no debe servir para "resetear" los intentos de esa IP).
        /// </summary>
        public void LoginCorrecto(Reserva r)
        {
            if (!TomarParaDevolver(r)) return;
            DateTime ahora = _relojUtc();
            for (int k = 0; k < _niveles.Length; k++)
            {
                if (r.Claves[k] == null) continue;
                if (_niveles[k].EsIp)
                    _almacen.Devolver(r.Claves[k], r.Bloqueos[k], r.PrimerFalloPrevio[k], _niveles[k].Limite, ahora);
                else
                    _almacen.Quitar(r.Claves[k]);
            }
        }

        /// <summary>La BD falló: no se sabe si la contraseña era buena, así que no se castiga.</summary>
        public void ErrorAlValidar(Reserva r)
        {
            if (!TomarParaDevolver(r)) return;
            DateTime ahora = _relojUtc();
            for (int k = 0; k < _niveles.Length; k++)
                if (r.Claves[k] != null)
                    _almacen.Devolver(r.Claves[k], r.Bloqueos[k], r.PrimerFalloPrevio[k], _niveles[k].Limite, ahora);
        }

        /// <summary>
        /// HttpRuntime.Cache con vencimiento absoluto y prioridad NotRemovable: la presión de memoria
        /// no debe borrar un bloqueo antes de tiempo; las entradas vencen solas. Es el respaldo de
        /// <see cref="AlmacenBdIntentos"/> (dentro de un <see cref="AlmacenAtomicoEnProceso"/>): se pierde al reciclar.
        /// </summary>
        internal sealed class AlmacenCacheAspNet : IAlmacenIntentosLogin
        {
            internal static readonly AlmacenCacheAspNet Instancia = new AlmacenCacheAspNet();

            private const string Prefijo = "SGV_LoginIntentos_";

            private AlmacenCacheAspNet() { }

            public IntentosLogin Leer(string clave)
            {
                var i = HttpRuntime.Cache[Prefijo + clave] as IntentosLogin;
                // Copia: quien la lee la modifica antes de guardarla.
                return i == null ? null : new IntentosLogin { Fallidos = i.Fallidos, PrimerFalloUtc = i.PrimerFalloUtc, BloqueadoHastaUtc = i.BloqueadoHastaUtc };
            }

            public void Guardar(string clave, IntentosLogin intentos, DateTime venceUtc) =>
                HttpRuntime.Cache.Insert(Prefijo + clave,
                    new IntentosLogin { Fallidos = intentos.Fallidos, PrimerFalloUtc = intentos.PrimerFalloUtc, BloqueadoHastaUtc = intentos.BloqueadoHastaUtc },
                    null, venceUtc, Cache.NoSlidingExpiration, CacheItemPriority.NotRemovable, null);

            public void Quitar(string clave) => HttpRuntime.Cache.Remove(Prefijo + clave);
        }
    }

    /// <summary>
    /// Las reglas de reserva y devolución sobre un almacén simple, bajo un candado propio (la misma
    /// lógica que sp_US_ReservarIntentoAcceso y sp_US_DevolverIntentoAcceso). Es el respaldo en el
    /// proceso de <see cref="AlmacenBdIntentos"/> y el almacén de las pruebas sin BD.
    /// </summary>
    public sealed class AlmacenAtomicoEnProceso : IAlmacenIntentosAtomico
    {
        private readonly IAlmacenIntentosLogin _almacen;
        private readonly object _candado = new object();

        public AlmacenAtomicoEnProceso(IAlmacenIntentosLogin almacen)
        {
            _almacen = almacen ?? throw new ArgumentNullException(nameof(almacen));
        }

        public ResultadoReservaIntentos Reservar(IList<NivelIntento> niveles, DateTime ahora)
        {
            int n = niveles.Count;
            lock (_candado)
            {
                var leidos = new IntentosLogin[n];
                int segundos = 0;
                for (int k = 0; k < n; k++)
                {
                    leidos[k] = _almacen.Leer(niveles[k].Clave);
                    segundos = Math.Max(segundos, SegundosBloqueo(leidos[k], ahora));
                }
                if (segundos > 0)
                    return new ResultadoReservaIntentos { Rechazado = true, SegundosRestantes = segundos };

                var res = new ResultadoReservaIntentos { Bloqueos = new bool[n], PrimerFalloPrevio = new DateTime[n] };
                for (int k = 0; k < n; k++)
                    res.Bloqueos[k] = SumarFallo(niveles[k].Clave, leidos[k], niveles[k].Limite, ahora, out res.PrimerFalloPrevio[k]);
                return res;
            }
        }

        /// <summary>
        /// Quita un intento reservado. Si esa reserva fue la que bloqueó, se levanta el bloqueo y
        /// el contador queda en máximo − 1 (como estaba antes de la reserva).
        /// </summary>
        public void Devolver(string clave, bool reservaBloqueo, DateTime primerFalloPrevio, ControlIntentosLogin.Limite limite, DateTime ahora)
        {
            lock (_candado)
            {
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
        }

        public void Quitar(string clave)
        {
            lock (_candado) _almacen.Quitar(clave);
        }

        private static int SegundosBloqueo(IntentosLogin i, DateTime ahora)
        {
            if (i == null || i.BloqueadoHastaUtc <= ahora) return 0;
            return Math.Max(1, (int)Math.Ceiling((i.BloqueadoHastaUtc - ahora).TotalSeconds));
        }

        /// <summary>Suma un fallo al contador ya leído; al llegar al máximo del <paramref name="limite"/>
        /// dentro de su ventana bloquea. Devuelve true si este fallo bloqueó.</summary>
        private bool SumarFallo(string clave, IntentosLogin i, ControlIntentosLogin.Limite limite, DateTime ahora, out DateTime primerFalloPrevio)
        {
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

        private void Guardar(string clave, IntentosLogin i, ControlIntentosLogin.Limite limite)
        {
            DateTime vence = i.PrimerFalloUtc.Add(limite.Ventana);
            if (i.BloqueadoHastaUtc > vence) vence = i.BloqueadoHastaUtc;
            _almacen.Guardar(clave, i, vence);
        }
    }

    /// <summary>
    /// Contadores en la tabla IntentoAcceso (migración 61): cada operación es un SP atómico
    /// (sp_US_ReservarIntentoAcceso lee, decide y suma los niveles de una reserva en un solo viaje;
    /// sp_US_DevolverIntentoAcceso; sp_US_QuitarIntentoAcceso), sin candado del proceso, y sobreviven al
    /// reciclaje. Si la BD falla, se usa el <c>respaldo</c> en el proceso durante <see cref="PausaTrasFallo"/>
    /// (sin esperar el tiempo de espera de la conexión en cada login) y queda un registro en el log, sin la
    /// clave. El respaldo empieza con lo que tenga (los contadores de la BD no se copian). Un bloqueo mutuo
    /// (1205) se reintenta dos veces antes de pasar al respaldo. Una clave de más de 100 caracteres se
    /// guarda como <c>H_</c> + su SHA-256.
    /// </summary>
    public sealed class AlmacenBdIntentos : IAlmacenIntentosAtomico
    {
        public static readonly TimeSpan PausaTrasFallo = TimeSpan.FromMinutes(1);
        internal const int LargoMaximoClave = 100;
        private const int ReintentosPorBloqueoMutuo = 2;

        /// <summary>Almacén del sitio: BD con la caché de ASP.NET de respaldo.</summary>
        public static readonly AlmacenBdIntentos Sitio =
            new AlmacenBdIntentos(ControlIntentosLogin.AlmacenCacheAspNet.Instancia, () => DateTime.UtcNow);

        private readonly IAlmacenIntentosAtomico _respaldo;
        private readonly Func<DateTime> _relojUtc;
        private readonly Func<IList<NivelIntento>, DateTime, ResultadoReservaIntentos> _reservarBd;
        private readonly Action<string, bool, DateTime, ControlIntentosLogin.Limite, DateTime> _devolverBd;
        private readonly Action<string> _quitarBd;
        private readonly object _candadoEstado = new object();
        private DateTime _respaldoHastaUtc = DateTime.MinValue;

        public AlmacenBdIntentos(IAlmacenIntentosLogin respaldo, Func<DateTime> relojUtc)
            : this(new AlmacenAtomicoEnProceso(respaldo), relojUtc, ReservarEnBd, DevolverEnBd, QuitarDeBd)
        {
        }

        /// <summary>Con el acceso a la BD inyectable (pruebas del respaldo).</summary>
        internal AlmacenBdIntentos(IAlmacenIntentosAtomico respaldo, Func<DateTime> relojUtc,
            Func<IList<NivelIntento>, DateTime, ResultadoReservaIntentos> reservarBd,
            Action<string, bool, DateTime, ControlIntentosLogin.Limite, DateTime> devolverBd,
            Action<string> quitarBd)
        {
            _respaldo = respaldo ?? throw new ArgumentNullException(nameof(respaldo));
            _relojUtc = relojUtc ?? throw new ArgumentNullException(nameof(relojUtc));
            _reservarBd = reservarBd ?? throw new ArgumentNullException(nameof(reservarBd));
            _devolverBd = devolverBd ?? throw new ArgumentNullException(nameof(devolverBd));
            _quitarBd = quitarBd ?? throw new ArgumentNullException(nameof(quitarBd));
        }

        /// <summary>true mientras se usa solo el respaldo tras un fallo de la BD.</summary>
        public bool UsandoRespaldo
        {
            get { lock (_candadoEstado) return _relojUtc() < _respaldoHastaUtc; }
        }

        public ResultadoReservaIntentos Reservar(IList<NivelIntento> niveles, DateTime ahoraUtc)
        {
            if (!UsandoRespaldo)
            {
                var enBd = new List<NivelIntento>(niveles.Count);
                foreach (var nv in niveles) enBd.Add(new NivelIntento { Clave = ClaveBd(nv.Clave), Limite = nv.Limite });
                try { return ConReintentos(() => _reservarBd(enBd, ahoraUtc)); }
                catch (Exception ex) { PasarARespaldo(ex, "reservar"); }
            }
            return _respaldo.Reservar(niveles, ahoraUtc);
        }

        public void Devolver(string clave, bool reservaBloqueo, DateTime primerFalloPrevio, ControlIntentosLogin.Limite limite, DateTime ahoraUtc)
        {
            if (!UsandoRespaldo)
            {
                try
                {
                    ConReintentos(() => { _devolverBd(ClaveBd(clave), reservaBloqueo, primerFalloPrevio, limite, ahoraUtc); return true; });
                    return;
                }
                catch (Exception ex) { PasarARespaldo(ex, "devolver"); }
            }
            _respaldo.Devolver(clave, reservaBloqueo, primerFalloPrevio, limite, ahoraUtc);
        }

        public void Quitar(string clave)
        {
            // En el respaldo también: pudo quedar algo de una caída anterior de la BD.
            _respaldo.Quitar(clave);
            if (UsandoRespaldo) return;
            try { ConReintentos(() => { _quitarBd(ClaveBd(clave)); return true; }); }
            catch (Exception ex) { PasarARespaldo(ex, "quitar"); }
        }

        internal static string ClaveBd(string clave)
        {
            clave = clave ?? "";
            return clave.Length <= LargoMaximoClave ? clave : "H_" + ControlIntentosLogin.Sha256Hex(clave);
        }

        private static T ConReintentos<T>(Func<T> operacion)
        {
            for (int intento = 0; ; intento++)
            {
                try { return operacion(); }
                catch (SqlException ex) when (ex.Number == 1205 && intento < ReintentosPorBloqueoMutuo) { }
            }
        }

        private void PasarARespaldo(Exception ex, string operacion)
        {
            bool avisar;
            lock (_candadoEstado)
            {
                DateTime ahora = _relojUtc();
                avisar = ahora >= _respaldoHastaUtc;
                _respaldoHastaUtc = ahora.Add(PausaTrasFallo);
            }
            if (!avisar) return;
            try
            {
                LogSGV.Error(ex, "No se pudo {Operacion} un intento de acceso en la tabla IntentoAcceso; se usa la caché del proceso durante {Segundos} s",
                    operacion, (int)PausaTrasFallo.TotalSeconds);
            }
            catch { /* el log no puede romper el login */ }
        }

        private static ResultadoReservaIntentos ReservarEnBd(IList<NivelIntento> niveles, DateTime ahoraUtc)
        {
            if (niveles.Count < 1 || niveles.Count > 3)
                throw new ArgumentException("Una reserva tiene de 1 a 3 niveles.", nameof(niveles));

            var p = new List<SqlParameter> { Fecha("@ahoraUtc", ahoraUtc) };
            for (int k = 0; k < niveles.Count; k++)
            {
                string s = (k + 1).ToString();
                var lim = niveles[k].Limite;
                p.Add(DbHelper.Param("@clave" + s, niveles[k].Clave));
                p.Add(DbHelper.Param("@maxFallos" + s, lim.MaxFallos));
                p.Add(DbHelper.Param("@ventanaSeg" + s, (int)Math.Ceiling(lim.Ventana.TotalSeconds)));
                p.Add(DbHelper.Param("@bloqueoSeg" + s, (int)Math.Ceiling(lim.Bloqueo.TotalSeconds)));
            }

            DataTable dt = DbHelper.ConsultarTablaSp("sp_US_ReservarIntentoAcceso", p.ToArray());
            DataRow f = dt.Rows[0];
            if (Convert.ToBoolean(f["rechazado"]))
                return new ResultadoReservaIntentos { Rechazado = true, SegundosRestantes = Convert.ToInt32(f["segundosRestantes"]) };

            var res = new ResultadoReservaIntentos { Bloqueos = new bool[niveles.Count], PrimerFalloPrevio = new DateTime[niveles.Count] };
            for (int k = 0; k < niveles.Count; k++)
            {
                string s = (k + 1).ToString();
                res.Bloqueos[k] = Convert.ToBoolean(f["bloqueo" + s]);
                object previo = f["primerFalloPrevio" + s];
                res.PrimerFalloPrevio[k] = previo == DBNull.Value
                    ? ahoraUtc
                    : DateTime.SpecifyKind(Convert.ToDateTime(previo), DateTimeKind.Utc);
            }
            return res;
        }

        private static void DevolverEnBd(string clave, bool reservaBloqueo, DateTime primerFalloPrevio,
            ControlIntentosLogin.Limite limite, DateTime ahoraUtc)
        {
            DbHelper.EjecutarNonQuerySp("sp_US_DevolverIntentoAcceso",
                DbHelper.Param("@clave", clave),
                Fecha("@ahoraUtc", ahoraUtc),
                DbHelper.Param("@reservaBloqueo", reservaBloqueo),
                Fecha("@primerFalloPrevio", primerFalloPrevio == default(DateTime) ? (DateTime?)null : primerFalloPrevio),
                DbHelper.Param("@maxFallos", limite.MaxFallos),
                DbHelper.Param("@ventanaSeg", (int)Math.Ceiling(limite.Ventana.TotalSeconds)));
        }

        private static void QuitarDeBd(string clave) =>
            DbHelper.EjecutarNonQuerySp("sp_US_QuitarIntentoAcceso", DbHelper.Param("@clave", clave));

        private static SqlParameter Fecha(string nombre, DateTime? valor) =>
            new SqlParameter(nombre, SqlDbType.DateTime2) { Scale = 0, Value = (object)valor ?? DBNull.Value };
    }
}
