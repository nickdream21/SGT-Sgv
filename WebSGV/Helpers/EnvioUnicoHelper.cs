using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI.WebControls;
using WebSGV.Services.Common;

namespace WebSGV.Helpers
{
    /// <summary>
    /// Tokens de un solo uso emitidos en la sesión, agrupados por formulario (clave). Núcleo puro de
    /// <see cref="EnvioUnicoHelper"/>: sin System.Web, se prueba solo. Por clave guarda los últimos
    /// <see cref="EnvioUnicoHelper.MaximoPorClave"/> pendientes (varias pestañas abiertas del mismo
    /// formulario siguen funcionando; la más vieja se descarta).
    /// </summary>
    [Serializable]
    public sealed class TokensEnvioUnico
    {
        private readonly Dictionary<string, List<string>> _pendientes = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly int _maximoPorClave;

        public TokensEnvioUnico(int maximoPorClave = EnvioUnicoHelper.MaximoPorClave)
        {
            if (maximoPorClave < 1) throw new ArgumentOutOfRangeException(nameof(maximoPorClave));
            _maximoPorClave = maximoPorClave;
        }

        /// <summary>Token nuevo (GUID sin guiones) pendiente para <paramref name="clave"/>.</summary>
        public string Emitir(string clave)
        {
            if (string.IsNullOrWhiteSpace(clave)) throw new ArgumentException("Falta la clave del formulario.", nameof(clave));
            string token = Guid.NewGuid().ToString("N");
            // La sesión InProc ya atiende de a una petición por sesión; el lock cubre igual cualquier otro uso.
            lock (_pendientes)
            {
                if (!_pendientes.TryGetValue(clave, out List<string> lista))
                    _pendientes[clave] = lista = new List<string>();
                lista.Add(token);
                while (lista.Count > _maximoPorClave)
                    lista.RemoveAt(0);
            }
            return token;
        }

        /// <summary>
        /// True si <paramref name="token"/> estaba pendiente para <paramref name="clave"/>; lo marca como usado
        /// (otra llamada con el mismo token devuelve false). Vacío, null o desconocido: false.
        /// </summary>
        public bool Consumir(string clave, string token)
        {
            if (string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(token)) return false;
            lock (_pendientes)
            {
                return _pendientes.TryGetValue(clave, out List<string> lista) && lista.Remove(token.Trim());
            }
        }

        /// <summary>Cantidad de tokens pendientes de <paramref name="clave"/> (para las pruebas).</summary>
        public int Pendientes(string clave)
        {
            lock (_pendientes)
            {
                return clave != null && _pendientes.TryGetValue(clave, out List<string> lista) ? lista.Count : 0;
            }
        }
    }

    /// <summary>
    /// Envío único en el servidor (E5-3): red para las altas que no son idempotentes, por si llegan dos POST
    /// del mismo formulario (pestaña duplicada, F5 que reenvía el POST, o el tope del bloqueo del navegador de
    /// E5-2 que venció). La página emite un token al mostrar el formulario (<see cref="Emitir(string, HiddenField)"/>,
    /// en un HiddenField; con UpdatePanel, dentro del panel que se refresca) y lo consume justo antes de escribir
    /// (<see cref="Consumir(string, string)"/>): el segundo POST con el mismo token no escribe y la página muestra
    /// <see cref="MensajeYaRegistrado"/>.
    ///
    /// La sesión es <c>InProc</c> (Web.config, <c>sessionState mode="InProc"</c>) y ASP.NET atiende de a una las
    /// peticiones de la misma sesión con escritura (las páginas usan <c>EnableSessionState</c> por defecto): la
    /// segunda petición encuentra el token ya usado. Aun así el almacén usa <c>lock</c>.
    ///
    /// Los tokens viven solo en <see cref="ClaveSesion"/>; ninguna página toca la sesión por su cuenta. Sin sesión
    /// autenticada lanza <see cref="ErrorNegocioException"/> con <see cref="MensajeSinSesion"/>.
    /// </summary>
    public static class EnvioUnicoHelper
    {
        /// <summary>Clave de sesión del almacén (<see cref="TokensEnvioUnico"/>).</summary>
        public const string ClaveSesion = "EnvioUnico.Tokens";

        /// <summary>Tokens pendientes por formulario: alcanza para varias pestañas abiertas del mismo.</summary>
        public const int MaximoPorClave = 20;

        public const string MensajeYaRegistrado =
            "Ya se registró: este formulario ya se había enviado. Revise la lista antes de registrar otra vez.";

        public const string MensajeSinSesion = "Su sesión expiró; vuelva a iniciar sesión.";

        // ── Núcleo (se prueba con una sesión falsa) ───────────────────────

        /// <summary>Emite un token para <paramref name="clave"/> en <paramref name="sesion"/> y lo devuelve.</summary>
        public static string Emitir(HttpSessionStateBase sesion, string clave) => Almacen(sesion).Emitir(clave);

        /// <summary>True si el token estaba pendiente; lo marca como usado. Ver <see cref="TokensEnvioUnico.Consumir"/>.</summary>
        public static bool Consumir(HttpSessionStateBase sesion, string clave, string token) =>
            Almacen(sesion).Consumir(clave, token);

        private static readonly object CerrojoCreacion = new object();

        private static TokensEnvioUnico Almacen(HttpSessionStateBase sesion)
        {
            if (!SesionHelper.HaySesionEn(sesion))
                throw new ErrorNegocioException(MensajeSinSesion);
            lock (CerrojoCreacion)
            {
                if (!(sesion[ClaveSesion] is TokensEnvioUnico almacen))
                    sesion[ClaveSesion] = almacen = new TokensEnvioUnico();
                return almacen;
            }
        }

        // ── Sobre la petición actual ─────────────────────────────────────

        private static HttpSessionStateBase SesionActual
        {
            get
            {
                var s = HttpContext.Current?.Session;
                return s == null ? null : new HttpSessionStateWrapper(s);
            }
        }

        /// <summary>Pone en <paramref name="campo"/> un token nuevo para <paramref name="clave"/>.</summary>
        public static void Emitir(string clave, HiddenField campo)
        {
            if (campo == null) throw new ArgumentNullException(nameof(campo));
            campo.Value = Emitir(SesionActual, clave);
        }

        /// <summary>True si el token del formulario estaba pendiente (y lo marca como usado).</summary>
        public static bool Consumir(string clave, string token) => Consumir(SesionActual, clave, token);
    }
}
