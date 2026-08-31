using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;

namespace WebSGV.Services.GpsIntegracion
{
    /// <summary>
    /// Cliente del Customer API de Location World / Entel Onway. Maneja el flujo de
    /// autenticación en 2 pasos (token Auth0 client-credentials + sesión Onway), la
    /// paginación, el rate limit (2 req/s) y el auto-reintento ante sesión rechazada, y
    /// expone solo lo necesario para el caso de uso de hora de llegada por GPS: listar
    /// dispositivos e historial de posiciones.
    ///
    /// IMPORTANTE (confirmado empíricamente el 2026-08-31): Location World/Auth0 solo permite
    /// UN token activo a la vez por client_id — no por entorno ni por quién lo pide. Si ya hay
    /// un token vigente (p. ej. el que tiene cacheado producción, u otro generado a mano desde
    /// Postman para diagnosticar), CUALQUIER intento de generar uno nuevo se rechaza con 401
    /// "access_denied" aunque las credenciales sean correctas. Por eso <see cref="ObtenerTokenValido"/>
    /// siempre intenta reusar el token persistido en <see cref="OnwayAuthCacheService"/> antes
    /// de pedir uno nuevo, y <see cref="EjecutarConReintento{T}"/> — a propósito — NUNCA fuerza
    /// un token nuevo como parte de su reintento (ver el comentario en ese método). Si el token
    /// cacheado de este entorno ya expiró de verdad y Auth0 rechaza el intento de renovarlo
    /// porque otro entorno ya tiene uno activo, no hay reintento local que lo arregle: hay que
    /// esperar a que ese otro token expire, o compartir manualmente el que esté vigente (ver
    /// <c>WebSGV/Database/Scripts/</c> para un ejemplo de cómo sembrar uno en <c>OnwayAuthCache</c>).
    ///
    /// La sesión de Onway (userId/clientId), en cambio, sí puede renovarse libremente cuantas
    /// veces haga falta sin este límite — no consume el cupo de 1 token/24h.
    /// </summary>
    public class OnwayApiClient
    {
        private const string AuthTokenUrl = "https://location-world.auth0.com/oauth/token";
        private const string ApiBaseUrl = "https://customer-api.location-world.com";
        private const string Domain = "fleet";
        private const string Subdomain = "fleetpe";
        private const int PageSize = 50;

        // Rate limit del API: 2 requests/segundo. Throttle simple compartido entre instancias.
        private static readonly object RateLock = new object();
        private static DateTime _ultimaLlamadaUtc = DateTime.MinValue;

        private static readonly HttpClient Http = CrearHttpClient();

        /// <summary>
        /// Fuerza TLS 1.2 (el <c>SystemDefault</c> de .NET Framework 4.8 debería alcanzar en
        /// Windows moderno, pero se fija explícitamente para no depender de la configuración del
        /// SO) y fija un User-Agent identificable. Auth0/Location World pueden rechazar
        /// silenciosamente (401 "access_denied") peticiones sin User-Agent o con una huella TLS
        /// atípica, tratándolas como tráfico sospechoso, mientras que un cliente como Postman
        /// (con TLS/User-Agent "normales") pasa sin problema con las mismas credenciales.
        /// </summary>
        private static HttpClient CrearHttpClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SGV-WebSGV/1.0 (+https://sgv.serviciosgeneralesviviana.com)");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return http;
        }

        private string _accessToken;
        private string _userId;

        /// <summary>
        /// Asegura credenciales válidas en los campos de instancia, renovando lo que haga
        /// falta. Se usa al inicio de <see cref="EjecutarConReintento{T}"/> y en cada uno de
        /// sus reintentos.
        /// </summary>
        private void AsegurarCredenciales(bool forzarToken = false, bool forzarSesion = false)
        {
            if (_accessToken == null || forzarToken)
                _accessToken = ObtenerTokenValido(forzarToken);
            if (_userId == null || forzarSesion || forzarToken)
                _userId = ObtenerUserId(_accessToken, forzarSesion || forzarToken);
        }

        private static bool EsErrorDeAutenticacion(OnwayApiException ex) =>
            ex.StatusCode == 401 || ex.StatusCode == 403;

        /// <summary>
        /// Ejecuta <paramref name="operacion"/> (recibe token y userId vigentes) y, si el API
        /// responde 401/403, renueva SOLO la sesión de Onway (mismo token) y reintenta una vez.
        ///
        /// IMPORTANTE: a propósito NO se fuerza un token Auth0 nuevo aquí. Location World solo
        /// permite un token activo a la vez por client_id — si ya hay uno vigente en otro
        /// entorno (p. ej. producción, u otra pestaña de Postman), Auth0 rechaza con 401
        /// "access_denied" cualquier intento de generar uno nuevo, sin importar que las
        /// credenciales sean correctas. Forzar un token nuevo ante cualquier 401 (como hacía
        /// una versión anterior de este método) solo garantiza un segundo rechazo en ese caso y
        /// nunca ayuda: si el token cacheado sigue vigente, el problema real está en la sesión
        /// (que este único reintento sí resuelve); si el token cacheado ya expiró de verdad, eso
        /// lo maneja <see cref="ObtenerTokenValido"/> por su cuenta la próxima vez que se
        /// necesite un token (no aquí, en medio de un reintento).
        /// </summary>
        private T EjecutarConReintento<T>(Func<string, string, T> operacion)
        {
            AsegurarCredenciales();
            try
            {
                return operacion(_accessToken, _userId);
            }
            catch (OnwayApiException ex) when (EsErrorDeAutenticacion(ex))
            {
                LogSGV.Info("Onway: sesión rechazada por el API ({StatusCode}), renovando sesión (mismo token) y reintentando.", ex.StatusCode);
                AsegurarCredenciales(forzarSesion: true);
                return operacion(_accessToken, _userId);
            }
        }

        /// <summary>
        /// Devuelve un token de acceso válido, reusando el cacheado en BD si no está por
        /// expirar (margen de 10 minutos). Solo pide uno nuevo a Auth0 si hace falta, o si
        /// <paramref name="forzarRefresco"/> es true (tras un 401/403 inesperado).
        /// </summary>
        public string ObtenerTokenValido(bool forzarRefresco = false)
        {
            if (!forzarRefresco)
            {
                var cacheExistente = OnwayAuthCacheService.Obtener();
                if (cacheExistente != null && cacheExistente.TokenExpiraEn > DateTime.UtcNow.AddMinutes(10))
                    return cacheExistente.AccessToken;
            }

            string clientId = ConfigurationManager.AppSettings["OnwayAuth0ClientId"];
            string clientSecret = ConfigurationManager.AppSettings["OnwayAuth0ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                throw new InvalidOperationException("Faltan OnwayAuth0ClientId/OnwayAuth0ClientSecret en appSettings.Secrets.config.");

            var payload = new
            {
                client_id = clientId,
                client_secret = clientSecret,
                audience = ApiBaseUrl,
                grant_type = "client_credentials"
            };

            var respuesta = Post<OnwayTokenResponse>(AuthTokenUrl, payload, bearerToken: null);
            DateTime expiraEn = DateTime.UtcNow.AddSeconds(respuesta.ExpiresIn);
            OnwayAuthCacheService.GuardarToken(respuesta.AccessToken, expiraEn);

            LogSGV.Info("Onway: token Auth0 nuevo generado, expira {ExpiraEn}", expiraEn);
            return respuesta.AccessToken;
        }

        /// <summary>
        /// Devuelve el <c>userId</c> de la sesión Onway, reusando el guardado en caché si el
        /// token actual ya tiene una sesión asociada (la sesión no consume el cupo de 1
        /// token/24h — solo la generación del token Auth0 lo hace). Con
        /// <paramref name="forzarRefresco"/> en true crea una sesión nueva incondicionalmente
        /// (tras un 401/403 inesperado, o porque el token también se acaba de renovar).
        /// </summary>
        public string ObtenerUserId(string accessToken, bool forzarRefresco = false)
        {
            if (!forzarRefresco)
            {
                var cache = OnwayAuthCacheService.Obtener();
                if (cache != null && !string.IsNullOrEmpty(cache.OnwayUserId) && cache.AccessToken == accessToken)
                    return cache.OnwayUserId;
            }

            string username = ConfigurationManager.AppSettings["OnwayUsername"];
            string password = ConfigurationManager.AppSettings["OnwayPassword"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Faltan OnwayUsername/OnwayPassword en appSettings.Secrets.config.");

            string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/sessions";
            var respuesta = Post<OnwaySessionResponse>(url, new { username, password }, accessToken);

            OnwayAuthCacheService.GuardarSesion(respuesta.ClientId, respuesta.UserId);
            return respuesta.UserId;
        }

        /// <summary>
        /// Lista todos los dispositivos GPS de la cuenta (pagina automáticamente). Renueva
        /// sesión/token solo y reintenta automáticamente si el API rechaza la credencial vigente.
        /// </summary>
        public List<OnwayDevice> ListarDispositivos()
        {
            return EjecutarConReintento((accessToken, userId) =>
            {
                var todos = new List<OnwayDevice>();
                int page = 0;
                while (true)
                {
                    string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/users/{userId}/devices" +
                                 $"?deviceExpandEnum=none&deviceFieldSortEnum=imei&directionSortEnum=asc&page={page}&pageSize={PageSize}";
                    var respuesta = Get<OnwayDeviceListResponse>(url, accessToken);
                    if (respuesta.Content == null || respuesta.Content.Count == 0) break;

                    todos.AddRange(respuesta.Content);
                    if (todos.Count >= respuesta.Records) break;
                    page++;
                }
                return todos;
            });
        }

        /// <summary>Busca un dispositivo por placa (campo <c>alias</c> del API) entre todos los de la cuenta. Null si no hay match.</summary>
        public OnwayDevice BuscarDispositivoPorPlaca(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa)) return null;
            var dispositivos = ListarDispositivos();
            return dispositivos.FirstOrDefault(d =>
                string.Equals(d.Alias?.Trim(), placa.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Historial de posiciones/eventos de un dispositivo entre <paramref name="desdeUtc"/> y
        /// <paramref name="hastaUtc"/> (rango máximo permitido por el API: 1 día). Pagina
        /// automáticamente y reintenta sola si el API rechaza la credencial vigente.
        /// </summary>
        public List<OnwayHistoryPoint> ObtenerHistorial(string deviceId, DateTime desdeUtc, DateTime hastaUtc) =>
            EjecutarConReintento((accessToken, userId) => ObtenerHistorialInterno(accessToken, userId, deviceId, desdeUtc, hastaUtc));

        private List<OnwayHistoryPoint> ObtenerHistorialInterno(string accessToken, string userId, string deviceId, DateTime desdeUtc, DateTime hastaUtc)
        {
            var todos = new List<OnwayHistoryPoint>();
            int page = 0;
            string desdeStr = desdeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string hastaStr = hastaUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

            while (true)
            {
                string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/users/{userId}/devices/{deviceId}/history" +
                             $"?from={Uri.EscapeDataString(desdeStr)}&to={Uri.EscapeDataString(hastaStr)}&page={page}&pageSize={PageSize}";
                var respuesta = Get<OnwayHistoryResponse>(url, accessToken);
                if (respuesta.Content == null || respuesta.Content.Count == 0) break;

                todos.AddRange(respuesta.Content);
                if (todos.Count >= respuesta.Records) break;
                page++;
            }
            return todos;
        }

        // ------------------------------------------------------------------
        // HTTP de bajo nivel
        // ------------------------------------------------------------------

        private static T Get<T>(string url, string bearerToken)
        {
            RespetarRateLimit();
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (!string.IsNullOrEmpty(bearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                return Ejecutar<T>(request);
            }
        }

        private static T Post<T>(string url, object payload, string bearerToken)
        {
            RespetarRateLimit();
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                if (!string.IsNullOrEmpty(bearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

                string json = JsonConvert.SerializeObject(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                return Ejecutar<T>(request);
            }
        }

        private static T Ejecutar<T>(HttpRequestMessage request)
        {
            string url = request.RequestUri.ToString();
            using (var response = Http.SendAsync(request).GetAwaiter().GetResult())
            {
                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    LogSGV.Error("Onway API error {StatusCode} en {Url}: {Body}", (int)response.StatusCode, url, body);
                    throw new OnwayApiException((int)response.StatusCode, url, body);
                }
                return JsonConvert.DeserializeObject<T>(body);
            }
        }

        /// <summary>Throttle simple para respetar el límite de 2 requests/segundo del API.</summary>
        private static void RespetarRateLimit()
        {
            lock (RateLock)
            {
                TimeSpan espera = _ultimaLlamadaUtc.AddMilliseconds(550) - DateTime.UtcNow;
                if (espera > TimeSpan.Zero)
                    Thread.Sleep(espera);
                _ultimaLlamadaUtc = DateTime.UtcNow;
            }
        }
    }

    public class OnwayApiException : Exception
    {
        public int StatusCode { get; }
        public string Url { get; }
        public string ResponseBody { get; }

        public OnwayApiException(int statusCode, string url, string responseBody)
            : base($"Onway API respondió {statusCode} para {url}")
        {
            StatusCode = statusCode;
            Url = url;
            ResponseBody = responseBody;
        }

        /// <summary>
        /// True cuando el 401 vino específicamente del endpoint de token de Auth0 — el caso
        /// confirmado el 2026-08-31: Location World solo permite un token activo a la vez por
        /// client_id, así que esto pasa cuando otro entorno (p. ej. producción) ya tiene uno
        /// vigente, no por credenciales incorrectas ni por una sesión inválida.
        /// </summary>
        public bool EsRechazoDeTokenActivoEnOtroLado =>
            StatusCode == 401 && Url != null && Url.IndexOf("auth0.com/oauth/token", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Mensaje apto para mostrar a la administradora, sin detalles técnicos.</summary>
        public string MensajeParaUsuario() =>
            EsRechazoDeTokenActivoEnOtroLado
                ? "No se pudo renovar el acceso al GPS: Auth0 lo rechazó, probablemente porque ya hay un token activo en otro entorno (ej. producción). No es un problema de credenciales — espera unos minutos y vuelve a intentar."
                : "No se pudo conectar con el sistema GPS. Intente más tarde.";
    }
}
