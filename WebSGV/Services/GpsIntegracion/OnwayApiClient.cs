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
        // Tamaños de página (probados contra el API el 2026-10-09). Un día de historial de un
        // tracto trae ~1 500 puntos: con 50 por página eran ~31 llamadas por día a 2 req/s y
        // "Verificar GPS" tardaba ~7 min; con 1 000 son 2 llamadas por día.
        private const int PageSizeDispositivos = 200;
        private const int PageSizeHistorial = 5000; // un día completo (~1 500-2 000 puntos) en una sola llamada

        /// <summary>
        /// Estado que comparten las instancias: el rate limit (2 requests/segundo) y las listas
        /// guardadas por unos minutos. Las instancias de <c>new OnwayApiClient()</c> usan uno solo
        /// (<see cref="EstadoCompartido"/>); las de prueba, el suyo propio.
        /// </summary>
        private sealed class Estado
        {
            public readonly object RateLock = new object();
            public DateTime UltimaLlamadaUtc = DateTime.MinValue;
            public List<OnwayDevice> Dispositivos;
            public DateTime DispositivosHastaUtc;
            public Dictionary<string, OnwayDevice> Foto;
            public DateTime FotoHastaUtc;
        }

        private static readonly Estado EstadoCompartido = new Estado();

        private static readonly HttpClient Http = CrearHttpClient(null);

        /// <summary>
        /// Fuerza TLS 1.2 (el <c>SystemDefault</c> de .NET Framework 4.8 debería alcanzar en
        /// Windows moderno, pero se fija explícitamente para no depender de la configuración del
        /// SO) y fija un User-Agent identificable. Auth0/Location World pueden rechazar
        /// silenciosamente (401 "access_denied") peticiones sin User-Agent o con una huella TLS
        /// atípica, tratándolas como tráfico sospechoso, mientras que un cliente como Postman
        /// (con TLS/User-Agent "normales") pasa sin problema con las mismas credenciales.
        /// Con <paramref name="manejador"/> (solo pruebas) las peticiones no salen a la red.
        /// </summary>
        private static HttpClient CrearHttpClient(HttpMessageHandler manejador)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var http = manejador == null ? new HttpClient() : new HttpClient(manejador);
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("SGV-WebSGV/1.0 (+https://sgv.serviciosgeneralesviviana.com)");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            return http;
        }

        private readonly HttpClient _http;
        private readonly Func<DateTime> _relojUtc;
        private readonly Action<TimeSpan> _esperar;
        private readonly IAlmacenAccesoOnway _almacen;
        private readonly Func<string, string> _leerConfig;
        private readonly Estado _estado;

        private string _accessToken;
        private string _userId;

        /// <summary>Cliente real: el HttpClient, el rate limit y las listas son los compartidos; el caché del token, la BD.</summary>
        public OnwayApiClient()
            : this(Http, () => DateTime.UtcNow, Thread.Sleep, AlmacenAccesoOnwayBd.Instancia,
                   clave => ConfigurationManager.AppSettings[clave], EstadoCompartido)
        {
        }

        /// <summary>
        /// Solo pruebas: las peticiones van a <paramref name="manejador"/> (un HttpClient propio, con el
        /// mismo tiempo límite y cabeceras; el compartido no se toca), la hora sale de
        /// <paramref name="relojUtc"/> y las esperas (rate limit y reintentos) van a
        /// <paramref name="esperar"/> en lugar de dormir (sin ella, no espera). El rate limit y las
        /// listas guardadas son de esta instancia. <paramref name="almacen"/> y
        /// <paramref name="leerConfig"/> son obligatorios a propósito: un valor por defecto a la BD
        /// real dejaría a una prueba escribir un token falso en <c>OnwayAuthCache</c> y tumbar el GPS
        /// del sitio, o leer las credenciales reales.
        /// </summary>
        internal OnwayApiClient(HttpMessageHandler manejador, Func<DateTime> relojUtc,
            IAlmacenAccesoOnway almacen, Func<string, string> leerConfig, Action<TimeSpan> esperar = null)
            : this(CrearHttpClient(manejador ?? throw new ArgumentNullException(nameof(manejador))),
                   relojUtc ?? throw new ArgumentNullException(nameof(relojUtc)),
                   esperar ?? (_ => { }),
                   almacen ?? throw new ArgumentNullException(nameof(almacen)),
                   leerConfig ?? throw new ArgumentNullException(nameof(leerConfig)), new Estado())
        {
        }

        /// <summary>True si este cliente usa el rate limit y las listas que comparten los clientes reales.</summary>
        internal bool UsaEstadoCompartido => ReferenceEquals(_estado, EstadoCompartido);

        private OnwayApiClient(HttpClient http, Func<DateTime> relojUtc, Action<TimeSpan> esperar,
            IAlmacenAccesoOnway almacen, Func<string, string> leerConfig, Estado estado)
        {
            _http = http;
            _relojUtc = relojUtc;
            _esperar = esperar;
            _almacen = almacen;
            _leerConfig = leerConfig;
            _estado = estado;
        }

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
        /// <paramref name="forzarRefresco"/> es true. Hoy nadie lo fuerza: ante un 401/403,
        /// <see cref="EjecutarConReintento{T}"/> renueva solo la sesión (ver su comentario).
        /// </summary>
        public string ObtenerTokenValido(bool forzarRefresco = false)
        {
            if (!forzarRefresco)
            {
                string vigente = TokenVigenteEnCache();
                if (vigente != null) return vigente;
            }

            // Solo un request a la vez renueva (ver OnwayAuthCacheService.ConBloqueoDeRenovacion).
            return _almacen.ConBloqueoDeRenovacion(() =>
            {
                // Mientras esperábamos el bloqueo, otro request pudo haber renovado: reusar.
                if (!forzarRefresco)
                {
                    string renovadoPorOtro = TokenVigenteEnCache();
                    if (renovadoPorOtro != null) return renovadoPorOtro;
                }
                return SolicitarTokenNuevo();
            });
        }

        private string TokenVigenteEnCache()
        {
            var cache = _almacen.Obtener();
            return cache != null && cache.TokenExpiraEn > _relojUtc().AddMinutes(10) ? cache.AccessToken : null;
        }

        private string SolicitarTokenNuevo()
        {
            string clientId = _leerConfig("OnwayAuth0ClientId");
            string clientSecret = _leerConfig("OnwayAuth0ClientSecret");
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
            DateTime expiraEn = _relojUtc().AddSeconds(respuesta.ExpiresIn);
            _almacen.GuardarToken(respuesta.AccessToken, expiraEn);

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
                var cache = _almacen.Obtener();
                if (cache != null && !string.IsNullOrEmpty(cache.OnwayUserId) && cache.AccessToken == accessToken)
                    return cache.OnwayUserId;
            }

            string username = _leerConfig("OnwayUsername");
            string password = _leerConfig("OnwayPassword");
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("Faltan OnwayUsername/OnwayPassword en appSettings.Secrets.config.");

            string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/sessions";
            var respuesta = Post<OnwaySessionResponse>(url, new { username, password }, accessToken);

            _almacen.GuardarSesion(respuesta.ClientId, respuesta.UserId);
            return respuesta.UserId;
        }

        /// <summary>
        /// Lista todos los dispositivos GPS de la cuenta (pagina automáticamente). Si el API
        /// rechaza la credencial vigente (401/403), renueva solo la sesión (mismo token) y reintenta una vez.
        /// </summary>
        public List<OnwayDevice> ListarDispositivos() => ListarDispositivos(false);

        /// <summary>
        /// Con <paramref name="conUltimaPosicion"/> (deviceExpandEnum=1, no documentado en el swagger, verificado el
        /// 22/10/2026) cada dispositivo trae su última posición y odómetro: una sola consulta para toda la flota.
        /// </summary>
        public List<OnwayDevice> ListarDispositivos(bool conUltimaPosicion)
        {
            return EjecutarConReintento((accessToken, userId) =>
            {
                var todos = new List<OnwayDevice>();
                int page = 0;
                while (true)
                {
                    string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/users/{userId}/devices" +
                                 $"?deviceExpandEnum={(conUltimaPosicion ? "1" : "none")}&deviceFieldSortEnum=imei&directionSortEnum=asc&page={page}&pageSize={PageSizeDispositivos}";
                    var respuesta = Get<OnwayDeviceListResponse>(url, accessToken);
                    if (respuesta.Content == null || respuesta.Content.Count == 0) break;

                    todos.AddRange(respuesta.Content);
                    if (todos.Count >= respuesta.Records) break;
                    page++;
                }
                return todos;
            });
        }

        // Lista de dispositivos compartida entre instancias por unos minutos: "Actualizar GPS"
        // consulta varios viajes seguidos y cada uno la pedía completa otra vez.
        private static readonly TimeSpan VigenciaDispositivos = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Busca un dispositivo por placa (campo <c>alias</c> del API) entre todos los de la cuenta.
        /// Null si no hay match. Si la placa no está en la lista guardada, la vuelve a pedir una vez
        /// (equipo recién instalado).
        /// </summary>
        public OnwayDevice BuscarDispositivoPorPlaca(string placa)
        {
            if (string.IsNullOrWhiteSpace(placa)) return null;
            var lista = _estado.Dispositivos;
            bool recienPedida = false;
            if (lista == null || _relojUtc() > _estado.DispositivosHastaUtc)
            {
                lista = RenovarDispositivos();
                recienPedida = true;
            }
            var encontrado = Buscar(lista, placa);
            if (encontrado == null && !recienPedida)
                encontrado = Buscar(RenovarDispositivos(), placa);
            return encontrado;
        }

        // Foto de la flota (última posición de cada equipo): la comparten los viajes de una misma actualización.
        private static readonly TimeSpan VigenciaFoto = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Última posición de todos los equipos, por placa (alias en mayúsculas), en una sola consulta. Sirve
        /// también de lista de dispositivos. Se guarda un minuto.
        /// </summary>
        public Dictionary<string, OnwayDevice> UltimasPosiciones()
        {
            var foto = _estado.Foto;
            if (foto != null && _relojUtc() < _estado.FotoHastaUtc) return foto;
            var lista = ListarDispositivos(true);
            foto = lista.Where(d => !string.IsNullOrWhiteSpace(d.Alias))
                        .GroupBy(d => d.Alias.Trim().ToUpperInvariant())
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _estado.Foto = foto;
            _estado.FotoHastaUtc = _relojUtc().Add(VigenciaFoto);
            _estado.Dispositivos = lista;
            _estado.DispositivosHastaUtc = _relojUtc().Add(VigenciaDispositivos);
            return foto;
        }

        private List<OnwayDevice> RenovarDispositivos()
        {
            var lista = ListarDispositivos();
            _estado.Dispositivos = lista;
            _estado.DispositivosHastaUtc = _relojUtc().Add(VigenciaDispositivos);
            return lista;
        }

        private static OnwayDevice Buscar(List<OnwayDevice> lista, string placa) =>
            lista.FirstOrDefault(d => string.Equals(d.Alias?.Trim(), placa.Trim(), StringComparison.OrdinalIgnoreCase));

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
                             $"?from={Uri.EscapeDataString(desdeStr)}&to={Uri.EscapeDataString(hastaStr)}&page={page}&pageSize={PageSizeHistorial}";
                var respuesta = Get<OnwayHistoryResponse>(url, accessToken);
                if (respuesta.Content == null || respuesta.Content.Count == 0) break;

                todos.AddRange(respuesta.Content);
                if (todos.Count >= respuesta.Records) break;
                page++;
            }
            return todos;
        }

        /// <summary>
        /// Historial CANBUS (horómetro) de un dispositivo, con el mismo límite de 1 día por
        /// llamada que <see cref="ObtenerHistorial"/>. Las unidades sin CANBUS devuelven vacío.
        /// </summary>
        public List<OnwayCanbusPoint> ObtenerHistorialCanbus(string deviceId, DateTime desdeUtc, DateTime hastaUtc) =>
            EjecutarConReintento((accessToken, userId) =>
            {
                var todos = new List<OnwayCanbusPoint>();
                int page = 0;
                string desdeStr = desdeUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");
                string hastaStr = hastaUtc.ToString("yyyy-MM-ddTHH:mm:ssZ");

                while (true)
                {
                    string url = $"{ApiBaseUrl}/v1/{Domain}/{Subdomain}/users/{userId}/devices/{deviceId}/history/canbus" +
                                 $"?from={Uri.EscapeDataString(desdeStr)}&to={Uri.EscapeDataString(hastaStr)}&page={page}&pageSize={PageSizeHistorial}";
                    var respuesta = Get<OnwayCanbusResponse>(url, accessToken);
                    if (respuesta.Content == null || respuesta.Content.Count == 0) break;

                    todos.AddRange(respuesta.Content);
                    if (todos.Count >= respuesta.Records) break;
                    page++;
                }
                return todos;
            });

        // ------------------------------------------------------------------
        // HTTP de bajo nivel
        // ------------------------------------------------------------------

        private T Get<T>(string url, string bearerToken) =>
            Ejecutar<T>(url, permiteReintento: true, crearRequest: () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(bearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                return request;
            });

        private T Post<T>(string url, object payload, string bearerToken)
        {
            string json = JsonConvert.SerializeObject(payload);
            // El POST de token a Auth0 NO se reintenta: si el primer intento llegó a crear el token
            // (aunque respondiera 5xx), un segundo intento sería rechazado por "un token activo".
            bool esToken = url.IndexOf("/oauth/token", StringComparison.OrdinalIgnoreCase) >= 0;
            return Ejecutar<T>(url, permiteReintento: !esToken, crearRequest: () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrEmpty(bearerToken))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return request;
            });
        }

        private const int MaxIntentos = 3;

        /// <summary>
        /// Envía la petición respetando el rate limit. Ante 429 (demasiadas peticiones) o errores
        /// transitorios del proveedor (502/503/504) reintenta hasta <see cref="MaxIntentos"/> veces
        /// con espera creciente (o la indicada por Retry-After, máx. 5 s). Antes fallaba al primer error.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2025:Do not pass 'IDisposable' instances into unawaited tasks",
            Justification = "SendAsync se espera en el acto con GetAwaiter().GetResult(): la tarea termina antes de que el using libere la petición.")]
        private T Ejecutar<T>(string url, bool permiteReintento, Func<HttpRequestMessage> crearRequest)
        {
            for (int intento = 1; ; intento++)
            {
                RespetarRateLimit();
                using (var request = crearRequest())
                using (var response = _http.SendAsync(request).GetAwaiter().GetResult())
                {
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (response.IsSuccessStatusCode)
                        return JsonConvert.DeserializeObject<T>(body);

                    int estado = (int)response.StatusCode;
                    bool transitorio = estado == 429 || estado == 502 || estado == 503 || estado == 504;
                    if (permiteReintento && transitorio && intento < MaxIntentos)
                    {
                        TimeSpan espera = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(intento);
                        if (espera > TimeSpan.FromSeconds(5)) espera = TimeSpan.FromSeconds(5);
                        LogSGV.Advertencia("Onway API respondió {StatusCode} en {Url}; reintento {Intento} en {Espera} s",
                            estado, url, intento + 1, espera.TotalSeconds);
                        _esperar(espera);
                        continue;
                    }

                    LogSGV.Error("Onway API error {StatusCode} en {Url}: {Body}", estado, url, body);
                    throw new OnwayApiException(estado, url, body);
                }
            }
        }

        /// <summary>Throttle simple para respetar el límite de 2 requests/segundo del API.</summary>
        private void RespetarRateLimit()
        {
            lock (_estado.RateLock)
            {
                TimeSpan espera = _estado.UltimaLlamadaUtc.AddMilliseconds(550) - _relojUtc();
                if (espera > TimeSpan.Zero)
                    _esperar(espera);
                _estado.UltimaLlamadaUtc = _relojUtc();
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
                ? "No se pudo renovar el acceso al GPS: Auth0 lo rechazó. O ya hay un token activo en otro equipo (se libera solo al vencer), o cambió el Client Secret del GPS en appSettings.Secrets.config. Avise a sistemas."
                : "No se pudo conectar con el sistema GPS. Intente más tarde.";
    }
}
