using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSGV.Services.GpsIntegracion;

namespace WebSGV.Tests.GpsIntegracion
{
    /// <summary>Una petición que recibió la API falsa.</summary>
    internal sealed class PeticionOnway
    {
        public string Metodo { get; set; }
        public string Url { get; set; }
        public string Autorizacion { get; set; }
        public string Cuerpo { get; set; }
        public string UserAgent { get; set; }

        public bool EsToken => Url.Contains("/oauth/token");
        public bool EsSesion => Url.EndsWith("/sessions", StringComparison.Ordinal);
        public bool EsDispositivos => Url.Contains("/devices?");
        public bool EsHistorial => Url.Contains("/history?");
        public bool EsCanbus => Url.Contains("/history/canbus?");
    }

    /// <summary>
    /// API de Onway falsa para <see cref="OnwayApiClient"/>: nunca sale a la red. Cada petición se
    /// anota y la responde <see cref="Responder"/>; por defecto el token y la sesión de
    /// <c>Datos/Onway</c> y un 404 para lo demás.
    /// </summary>
    internal sealed class ApiOnwayFalsa : HttpMessageHandler
    {
        public static readonly string Carpeta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Datos", "Onway");

        public List<PeticionOnway> Peticiones { get; } = new List<PeticionOnway>();
        public Func<PeticionOnway, HttpResponseMessage> Responder { get; set; }

        public ApiOnwayFalsa(Func<PeticionOnway, HttpResponseMessage> responder = null)
        {
            Responder = responder ?? (p => null);
        }

        public static string Json(string archivo) => File.ReadAllText(Path.Combine(Carpeta, archivo), Encoding.UTF8);

        public static HttpResponseMessage Ok(string archivo) => ConCuerpo(HttpStatusCode.OK, Json(archivo));

        public static HttpResponseMessage ConCuerpo(HttpStatusCode estado, string cuerpo) =>
            new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

        public static HttpResponseMessage Estado(int estado, TimeSpan? reintentarEn = null)
        {
            var r = ConCuerpo((HttpStatusCode)estado, "{\"error\":\"de prueba\"}");
            if (reintentarEn.HasValue) r.Headers.RetryAfter = new RetryConditionHeaderValue(reintentarEn.Value);
            return r;
        }

        public int Cuantas(Func<PeticionOnway, bool> filtro) => Peticiones.Count(filtro);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var p = new PeticionOnway
            {
                Metodo = request.Method.Method,
                Url = request.RequestUri.ToString(),
                Autorizacion = request.Headers.Authorization?.ToString(),
                Cuerpo = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult(),
                UserAgent = request.Headers.UserAgent.ToString()
            };
            Peticiones.Add(p);
            var respuesta = Responder(p);
            if (respuesta == null)
            {
                if (p.EsToken) respuesta = Ok("token.json");
                else if (p.EsSesion) respuesta = Ok("sesion.json");
                else respuesta = Estado(404);
            }
            return Task.FromResult(respuesta);
        }
    }

    /// <summary>Caché del token en memoria (en el sitio es la fila de <c>OnwayAuthCache</c>).</summary>
    internal sealed class AlmacenAccesoEnMemoria : IAlmacenAccesoOnway
    {
        public OnwayAuthCache Cache { get; set; }
        public int Bloqueos { get; private set; }
        public List<Tuple<string, DateTime>> TokensGuardados { get; } = new List<Tuple<string, DateTime>>();

        public OnwayAuthCache Obtener() => Cache == null ? null : new OnwayAuthCache
        {
            AccessToken = Cache.AccessToken,
            TokenExpiraEn = Cache.TokenExpiraEn,
            OnwayClientId = Cache.OnwayClientId,
            OnwayUserId = Cache.OnwayUserId
        };

        // Como el MERGE de OnwayAuthCacheService.GuardarToken: la sesión anterior se borra.
        public void GuardarToken(string accessToken, DateTime expiraEnUtc)
        {
            TokensGuardados.Add(Tuple.Create(accessToken, expiraEnUtc));
            Cache = new OnwayAuthCache { AccessToken = accessToken, TokenExpiraEn = expiraEnUtc };
        }

        public void GuardarSesion(string clientId, string userId)
        {
            if (Cache == null) return; // el UPDATE ... WHERE idCache = 1 no toca nada si no hay fila
            Cache.OnwayClientId = clientId;
            Cache.OnwayUserId = userId;
        }

        public T ConBloqueoDeRenovacion<T>(Func<T> trabajo)
        {
            Bloqueos++;
            return trabajo();
        }
    }

    /// <summary>Reloj que solo avanza cuando el cliente "espera" (o la prueba lo mueve).</summary>
    internal sealed class RelojFalso
    {
        public DateTime AhoraUtc { get; set; } = new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc);
        public List<TimeSpan> Esperas { get; } = new List<TimeSpan>();

        public void Esperar(TimeSpan t)
        {
            Esperas.Add(t);
            AhoraUtc = AhoraUtc.Add(t);
        }
    }

    /// <summary>Arma un <see cref="OnwayApiClient"/> contra la API falsa, con credenciales ficticias.</summary>
    internal sealed class EscenarioOnway
    {
        public const string TokenEnCache = "token-en-cache";
        public const string UsuarioEnCache = "usuario-en-cache";

        public RelojFalso Reloj { get; } = new RelojFalso();
        public ApiOnwayFalsa Api { get; } = new ApiOnwayFalsa();
        public AlmacenAccesoEnMemoria Almacen { get; } = new AlmacenAccesoEnMemoria();
        public Dictionary<string, string> Config { get; } = new Dictionary<string, string>
        {
            ["OnwayAuth0ClientId"] = "id-ficticio",
            ["OnwayAuth0ClientSecret"] = "secreto-ficticio",
            ["OnwayUsername"] = "usuario-ficticio",
            ["OnwayPassword"] = "clave-ficticia"
        };

        /// <summary>Token vigente (vence en una hora) con su sesión ya creada.</summary>
        public EscenarioOnway ConTokenYSesionEnCache()
        {
            Almacen.Cache = new OnwayAuthCache
            {
                AccessToken = TokenEnCache,
                TokenExpiraEn = Reloj.AhoraUtc.AddHours(1),
                OnwayClientId = "cliente-en-cache",
                OnwayUserId = UsuarioEnCache
            };
            return this;
        }

        public OnwayApiClient Cliente() =>
            new OnwayApiClient(Api, () => Reloj.AhoraUtc, Almacen,
                clave => Config.TryGetValue(clave, out var v) ? v : null, Reloj.Esperar);

        /// <summary>Esperas que no son las del rate limit (las de los reintentos).</summary>
        public List<TimeSpan> EsperasDeReintento() =>
            Reloj.Esperas.Where(e => e != TimeSpan.FromMilliseconds(550)).ToList();
    }
}
