using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
    /// paginación y el rate limit (2 req/s), y expone solo lo necesario para el
    /// caso de uso de hora de llegada por GPS: listar dispositivos e historial de posiciones.
    ///
    /// IMPORTANTE: el API de Auth0 exige NO generar más de un token cada 24h (podría
    /// bloquear la generación). Por eso <see cref="ObtenerTokenValido"/> siempre intenta
    /// reusar el token persistido en <see cref="OnwayAuthCacheService"/> antes de pedir uno nuevo.
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

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Devuelve un token de acceso válido, reusando el cacheado en BD si no está por
        /// expirar (margen de 10 minutos). Solo pide uno nuevo a Auth0 si hace falta.
        /// </summary>
        public string ObtenerTokenValido()
        {
            var cache = OnwayAuthCacheService.Obtener();
            if (cache != null && cache.TokenExpiraEn > DateTime.UtcNow.AddMinutes(10))
                return cache.AccessToken;

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
        /// token/24h — solo la generación del token Auth0 lo hace).
        /// </summary>
        public string ObtenerUserId(string accessToken)
        {
            var cache = OnwayAuthCacheService.Obtener();
            if (cache != null && !string.IsNullOrEmpty(cache.OnwayUserId) && cache.AccessToken == accessToken)
                return cache.OnwayUserId;

            string username = ConfigurationManager.AppSettings["OnwayUsername"];
            string password = ConfigurationManager.AppSettings["OnwayPassword"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Faltan OnwayUsername/OnwayPassword en appSettings.Secrets.config.");

            string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/sessions";
            var respuesta = Post<OnwaySessionResponse>(url, new { username, password }, accessToken);

            OnwayAuthCacheService.GuardarSesion(respuesta.ClientId, respuesta.UserId);
            return respuesta.UserId;
        }

        /// <summary>Lista todos los dispositivos GPS de la cuenta (pagina automáticamente).</summary>
        public List<OnwayDevice> ListarDispositivos(string accessToken, string userId)
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
        }

        /// <summary>Busca un dispositivo por placa (campo <c>alias</c> del API) entre todos los de la cuenta. Null si no hay match.</summary>
        public OnwayDevice BuscarDispositivoPorPlaca(string accessToken, string userId, string placa)
        {
            if (string.IsNullOrWhiteSpace(placa)) return null;
            var dispositivos = ListarDispositivos(accessToken, userId);
            return dispositivos.FirstOrDefault(d =>
                string.Equals(d.Alias?.Trim(), placa.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Historial de posiciones/eventos de un dispositivo entre <paramref name="desde"/> y
        /// <paramref name="hasta"/> (rango máximo permitido por el API: 1 día). Pagina automáticamente.
        /// </summary>
        public List<OnwayHistoryPoint> ObtenerHistorial(string accessToken, string userId, string deviceId, DateTime desdeUtc, DateTime hastaUtc)
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
    }
}
