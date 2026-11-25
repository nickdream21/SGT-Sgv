using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests.GpsIntegracion
{
    /// <summary>
    /// <see cref="OnwayApiClient"/> contra una API falsa (E6-8): token y sesión, reintentos, rate limit,
    /// paginación, búsqueda por placa y errores. Nunca llama a la API real ni lee sus secretos.
    /// </summary>
    public class OnwayApiClientTests
    {
        private static readonly TimeSpan RateLimit = TimeSpan.FromMilliseconds(550);

        private static Func<PeticionOnway, HttpResponseMessage> DispositivosEnDosPaginas() => p =>
            !p.EsDispositivos ? null : ApiOnwayFalsa.Ok(p.Url.Contains("page=0") ? "dispositivos-pagina0.json" : "dispositivos-pagina1.json");

        // ---------------- Token y sesión ----------------

        [Fact]
        public void TokenEnCacheVigente_SeReusaConSuSesion_SinPedirOtro()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();

            var lista = e.Cliente().ListarDispositivos();

            Assert.Equal(3, lista.Count);
            Assert.Equal(0, e.Api.Cuantas(p => p.EsToken || p.EsSesion));
            Assert.All(e.Api.Peticiones, p => Assert.Equal("Bearer " + EscenarioOnway.TokenEnCache, p.Autorizacion));
            Assert.All(e.Api.Peticiones, p => Assert.Contains("/users/" + EscenarioOnway.UsuarioEnCache + "/devices", p.Url));
            Assert.Equal(0, e.Almacen.Bloqueos);
        }

        [Fact]
        public void TokenQueVenceEnMasDeDiezMinutos_SigueValiendo()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Almacen.Cache.TokenExpiraEn = e.Reloj.AhoraUtc.AddMinutes(10).AddSeconds(1);

            Assert.Equal(EscenarioOnway.TokenEnCache, e.Cliente().ObtenerTokenValido());
            Assert.Empty(e.Api.Peticiones);
        }

        [Theory]
        [InlineData(10)]   // justo en el margen: ya no vale (la regla es "más de 10 min")
        [InlineData(5)]
        [InlineData(-60)]  // vencido
        public void TokenQueVenceEnDiezMinutosOMenos_SePideUnoNuevo_BajoElBloqueo(int minutosParaVencer)
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Almacen.Cache.TokenExpiraEn = e.Reloj.AhoraUtc.AddMinutes(minutosParaVencer);
            DateTime ahora = e.Reloj.AhoraUtc;

            string token = e.Cliente().ObtenerTokenValido();

            Assert.Equal("token-de-prueba-nuevo", token);
            Assert.Equal(1, e.Almacen.Bloqueos);
            var pedido = Assert.Single(e.Api.Peticiones);
            Assert.True(pedido.EsToken);
            Assert.Equal("POST", pedido.Metodo);
            Assert.Null(pedido.Autorizacion);
            Assert.Contains("\"client_id\":\"id-ficticio\"", pedido.Cuerpo);
            Assert.Contains("\"grant_type\":\"client_credentials\"", pedido.Cuerpo);
            Assert.Contains("SGV-WebSGV/1.0", pedido.UserAgent);
            // Vence según el reloj (expires_in = 86400 s en token.json).
            var guardado = Assert.Single(e.Almacen.TokensGuardados);
            Assert.Equal(ahora.AddSeconds(86400), guardado.Item2);
        }

        [Fact]
        public void SinCache_PideTokenYLuegoSesionConEseToken()
        {
            var e = new EscenarioOnway();
            e.Api.Responder = p => p.EsDispositivos ? ApiOnwayFalsa.Ok("dispositivos-uno.json") : null;

            var lista = e.Cliente().ListarDispositivos();

            Assert.Equal(new[] { "token", "sesion", "dispositivos" },
                e.Api.Peticiones.Select(p => p.EsToken ? "token" : p.EsSesion ? "sesion" : "dispositivos").ToArray());
            var sesion = e.Api.Peticiones[1];
            Assert.Equal("Bearer token-de-prueba-nuevo", sesion.Autorizacion);
            Assert.Contains("\"username\":\"usuario-ficticio\"", sesion.Cuerpo);
            Assert.Contains("/users/usuario-de-prueba/devices", e.Api.Peticiones[2].Url);
            Assert.Equal("usuario-de-prueba", e.Almacen.Cache.OnwayUserId);
            Assert.Single(lista);
        }

        [Fact]
        public void OtroRequestRenovoMientrasSeEsperabaElBloqueo_SeReusaSuToken()
        {
            var e = new EscenarioOnway();
            var almacen = new AlmacenQueRenuevaDuranteElBloqueo(e.Reloj);
            var cliente = new OnwayApiClient(e.Api, () => e.Reloj.AhoraUtc, almacen, c => e.Config[c], e.Reloj.Esperar);

            Assert.Equal("token-de-otro-request", cliente.ObtenerTokenValido());
            Assert.Empty(e.Api.Peticiones);
        }

        [Fact]
        public void ForzarRefresco_PideTokenAunqueElDeCacheSigaVigente()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();

            Assert.Equal("token-de-prueba-nuevo", e.Cliente().ObtenerTokenValido(forzarRefresco: true));
            Assert.Equal(1, e.Api.Cuantas(p => p.EsToken));
        }

        [Fact]
        public void SesionDeOtroToken_SeCreaUnaNueva()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();

            Assert.Equal("usuario-de-prueba", e.Cliente().ObtenerUserId("otro-token"));
            Assert.Equal(1, e.Api.Cuantas(p => p.EsSesion));
        }

        [Fact]
        public void SesionDelMismoToken_SeReusa_SalvoQueSeFuerce()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            var cliente = e.Cliente();

            Assert.Equal(EscenarioOnway.UsuarioEnCache, cliente.ObtenerUserId(EscenarioOnway.TokenEnCache));
            Assert.Empty(e.Api.Peticiones);
            Assert.Equal("usuario-de-prueba", cliente.ObtenerUserId(EscenarioOnway.TokenEnCache, forzarRefresco: true));
            Assert.Equal(1, e.Api.Cuantas(p => p.EsSesion));
        }

        // ---------------- 401/403: una sola renovación ----------------

        [Theory]
        [InlineData(401)]
        [InlineData(403)]
        public void RechazoDeCredencial_RenuevaSoloLaSesionUnaVez_YReintenta(int estado)
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            int gets = 0;
            e.Api.Responder = p => !p.EsDispositivos ? null
                : ++gets == 1 ? ApiOnwayFalsa.Estado(estado) : ApiOnwayFalsa.Ok("dispositivos-uno.json");

            var lista = e.Cliente().ListarDispositivos();

            Assert.Single(lista);
            Assert.Equal(2, e.Api.Cuantas(p => p.EsDispositivos));
            Assert.Equal(1, e.Api.Cuantas(p => p.EsSesion));
            Assert.Equal(0, e.Api.Cuantas(p => p.EsToken)); // a propósito: nunca fuerza un token Auth0 nuevo
            Assert.Contains("/users/usuario-de-prueba/devices", e.Api.Peticiones.Last().Url);
        }

        [Fact]
        public void SegundoRechazo_FallaSinVolverAIntentar()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => p.EsDispositivos ? ApiOnwayFalsa.Estado(401) : null;

            var ex = Assert.Throws<OnwayApiException>(() => e.Cliente().ListarDispositivos());

            Assert.Equal(401, ex.StatusCode);
            Assert.False(ex.EsRechazoDeTokenActivoEnOtroLado);
            Assert.Equal("No se pudo conectar con el sistema GPS. Intente más tarde.", ex.MensajeParaUsuario());
            Assert.Equal(2, e.Api.Cuantas(p => p.EsDispositivos));
            Assert.Equal(1, e.Api.Cuantas(p => p.EsSesion));
            Assert.Empty(e.EsperasDeReintento()); // el 401 no es transitorio
        }

        [Fact]
        public void TokenRechazadoPorAuth0_SeExplicaComoTokenActivoEnOtroLado()
        {
            var e = new EscenarioOnway();
            e.Api.Responder = p => p.EsToken ? ApiOnwayFalsa.Estado(401) : null;

            var ex = Assert.Throws<OnwayApiException>(() => e.Cliente().ListarDispositivos());

            Assert.True(ex.EsRechazoDeTokenActivoEnOtroLado);
            Assert.Contains("Auth0", ex.MensajeParaUsuario());
            Assert.Equal(1, e.Api.Cuantas(p => p.EsToken));
            Assert.Contains("de prueba", ex.ResponseBody);
        }

        // ---------------- Reintentos transitorios y rate limit ----------------

        [Fact]
        public void Error503_EsperaUnSegundoYReintenta()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            int gets = 0;
            e.Api.Responder = p => ++gets == 1 ? ApiOnwayFalsa.Estado(503) : ApiOnwayFalsa.Ok("dispositivos-uno.json");

            Assert.Single(e.Cliente().ListarDispositivos());
            Assert.Equal(new[] { TimeSpan.FromSeconds(1) }, e.EsperasDeReintento());
        }

        [Theory]
        [InlineData(502)]
        [InlineData(503)]
        [InlineData(504)]
        [InlineData(429)]
        public void ErrorTransitorio_TresVeces_Falla_ConEsperaCreciente(int estado)
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => ApiOnwayFalsa.Estado(estado);

            var ex = Assert.Throws<OnwayApiException>(() => e.Cliente().ListarDispositivos());

            Assert.Equal(estado, ex.StatusCode);
            Assert.Equal(3, e.Api.Cuantas(p => p.EsDispositivos));
            Assert.Equal(new[] { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2) }, e.EsperasDeReintento());
        }

        [Theory]
        [InlineData(2, 2)]
        [InlineData(10, 5)] // Retry-After mayor a 5 s se recorta a 5 s
        public void Error429_RespetaRetryAfter_ConTopeDeCincoSegundos(int pedidoSegundos, int esperadoSegundos)
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            int gets = 0;
            e.Api.Responder = p => ++gets == 1
                ? ApiOnwayFalsa.Estado(429, TimeSpan.FromSeconds(pedidoSegundos))
                : ApiOnwayFalsa.Ok("dispositivos-uno.json");

            e.Cliente().ListarDispositivos();

            Assert.Equal(new[] { TimeSpan.FromSeconds(esperadoSegundos) }, e.EsperasDeReintento());
        }

        [Fact]
        public void Error500_FallaAlPrimero_SinReintentar()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => ApiOnwayFalsa.Estado(500);

            var ex = Assert.Throws<OnwayApiException>(() => e.Cliente().ObtenerHistorial("equipo-1", e.Reloj.AhoraUtc, e.Reloj.AhoraUtc));

            Assert.Equal(500, ex.StatusCode);
            Assert.Contains("/devices/equipo-1/history", ex.Url);
            Assert.Single(e.Api.Peticiones);
            Assert.Empty(e.Reloj.Esperas);
        }

        [Fact]
        public void TokenConError503_NoSeReintenta_PorqueElPrimeroPudoCrearlo()
        {
            var e = new EscenarioOnway();
            e.Api.Responder = p => p.EsToken ? ApiOnwayFalsa.Estado(503) : null;

            var ex = Assert.Throws<OnwayApiException>(() => e.Cliente().ObtenerTokenValido());

            Assert.Equal(503, ex.StatusCode);
            Assert.Equal(1, e.Api.Cuantas(p => p.EsToken));
            Assert.Empty(e.EsperasDeReintento());
        }

        [Fact]
        public void SesionConError503_SiSeReintenta()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            int sesiones = 0;
            e.Api.Responder = p => p.EsSesion && ++sesiones == 1 ? ApiOnwayFalsa.Estado(503) : null;

            Assert.Equal("usuario-de-prueba", e.Cliente().ObtenerUserId("otro-token"));
            Assert.Equal(2, sesiones);
        }

        [Fact]
        public void RateLimit_LlamadasSeguidas_EsperanLoQueFaltaParaLos550ms()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();
            var cliente = e.Cliente();

            cliente.ListarDispositivos(); // página 0 (sin espera: primera llamada) y página 1 (550 ms)
            Assert.Equal(new[] { RateLimit }, e.Reloj.Esperas);

            e.Reloj.AhoraUtc = e.Reloj.AhoraUtc.AddMilliseconds(200);
            cliente.ListarDispositivos();
            // 350 ms para completar los 550 desde la última; luego 550 ms entre página y página.
            Assert.Equal(new[] { RateLimit, TimeSpan.FromMilliseconds(350), RateLimit }, e.Reloj.Esperas);

            // Pasado más de medio segundo desde la última: la primera no espera.
            e.Reloj.AhoraUtc = e.Reloj.AhoraUtc.AddSeconds(5);
            e.Reloj.Esperas.Clear();
            cliente.ListarDispositivos();
            Assert.Equal(new[] { RateLimit }, e.Reloj.Esperas);
        }

        // ---------------- Errores que no son del API ----------------

        [Fact]
        public void TiempoLimite_SePropagaComoTaskCanceled()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => throw new TaskCanceledException("tiempo límite de prueba");

            Assert.Throws<TaskCanceledException>(() => e.Cliente().ListarDispositivos());
            Assert.Single(e.Api.Peticiones); // sin reintento
        }

        [Fact]
        public void JsonInvalido_SePropagaComoErrorDeJson()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => ApiOnwayFalsa.ConCuerpo(System.Net.HttpStatusCode.OK, "{ esto no es json");

            Assert.ThrowsAny<JsonException>(() => e.Cliente().ListarDispositivos());
        }

        [Theory]
        [InlineData("OnwayAuth0ClientId")]
        [InlineData("OnwayAuth0ClientSecret")]
        public void SinCredencialesDeAuth0_FallaSinLlamarAlApi(string falta)
        {
            var e = new EscenarioOnway();
            e.Config[falta] = " ";

            var ex = Assert.Throws<InvalidOperationException>(() => e.Cliente().ObtenerTokenValido());
            Assert.Contains("OnwayAuth0ClientId", ex.Message);
            Assert.Empty(e.Api.Peticiones);
        }

        [Theory]
        [InlineData("OnwayUsername")]
        [InlineData("OnwayPassword")]
        public void SinUsuarioDeOnway_FallaSinCrearSesion(string falta)
        {
            var e = new EscenarioOnway();
            e.Config.Remove(falta);

            var ex = Assert.Throws<InvalidOperationException>(() => e.Cliente().ObtenerUserId("un-token"));
            Assert.Contains("OnwayUsername", ex.Message);
            Assert.Empty(e.Api.Peticiones);
        }

        [Fact]
        public void ConstructorDePrueba_ExigeManejadorRelojAlmacenYConfiguracion()
        {
            // Sin valor por defecto a la BD real ni a appSettings: una prueba nunca toca OnwayAuthCache ni los secretos.
            var almacen = new AlmacenAccesoEnMemoria();
            Func<string, string> config = c => "valor-ficticio";
            Func<DateTime> reloj = () => DateTime.UtcNow;
            using (var api = new ApiOnwayFalsa())
            {
                Assert.Equal("manejador", Assert.Throws<ArgumentNullException>(() => new OnwayApiClient(null, reloj, almacen, config)).ParamName);
                Assert.Equal("relojUtc", Assert.Throws<ArgumentNullException>(() => new OnwayApiClient(api, null, almacen, config)).ParamName);
                Assert.Equal("almacen", Assert.Throws<ArgumentNullException>(() => new OnwayApiClient(api, reloj, null, config)).ParamName);
                Assert.Equal("leerConfig", Assert.Throws<ArgumentNullException>(() => new OnwayApiClient(api, reloj, almacen, null)).ParamName);
                Assert.NotNull(new OnwayApiClient(api, reloj, almacen, config)); // sin "esperar": no duerme
            }
        }

        [Fact]
        public void ClientesReales_CompartenElEstado_LosDePrueba_No()
        {
            // El rate limit (2 req/s) y las listas guardadas son de todo el proceso: si cada
            // new OnwayApiClient() tuviera los suyos, varias consultas seguidas pasarían el límite del API.
            Assert.True(new OnwayApiClient().UsaEstadoCompartido);
            Assert.True(new OnwayApiClient().UsaEstadoCompartido);

            var e = new EscenarioOnway();
            Assert.False(e.Cliente().UsaEstadoCompartido);
            Assert.False(e.Cliente().UsaEstadoCompartido);
        }

        // ---------------- Paginación ----------------

        [Fact]
        public void Dispositivos_PaginaHastaCompletarLosRegistros()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();

            var lista = e.Cliente().ListarDispositivos();

            Assert.Equal(new[] { "equipo-1", "equipo-2", "equipo-3" }, lista.Select(d => d.Id).ToArray());
            var urls = e.Api.Peticiones.Select(p => p.Url).ToList();
            Assert.Equal(2, urls.Count);
            Assert.Contains("deviceExpandEnum=none", urls[0]);
            Assert.Contains("page=0&pageSize=200", urls[0]);
            Assert.Contains("page=1&pageSize=200", urls[1]);
            Assert.StartsWith("https://customer-api.location-world.com/v1/fleet/fleetpe/users/", urls[0]);
        }

        [Fact]
        public void Dispositivos_PaginaVacia_Corta()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => ApiOnwayFalsa.ConCuerpo(System.Net.HttpStatusCode.OK, "{\"records\":9,\"content\":[]}");

            Assert.Empty(e.Cliente().ListarDispositivos());
            Assert.Single(e.Api.Peticiones);
        }

        [Fact]
        public void Historial_UsaLaVentanaUtcEnLaUrl_YPagina()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => p.EsHistorial ? ApiOnwayFalsa.Ok("historial-llegada.json") : null;
            var desde = new DateTime(2026, 11, 20, 5, 0, 0, DateTimeKind.Utc);
            var hasta = new DateTime(2026, 11, 21, 4, 59, 59, DateTimeKind.Utc);

            var historial = e.Cliente().ObtenerHistorial("equipo-1", desde, hasta);

            Assert.Equal(5, historial.Count);
            var url = Assert.Single(e.Api.Peticiones).Url;
            Assert.Contains("/users/" + EscenarioOnway.UsuarioEnCache + "/devices/equipo-1/history?", url);
            Assert.Contains("from=2026-11-20T05%3A00%3A00Z&to=2026-11-21T04%3A59%3A59Z", url);
            Assert.Contains("page=0&pageSize=5000", url);
            Assert.Equal("Ignition Off, Vehicle ABC-123", historial[2].AlertDescription.En);
            Assert.Equal(DateTimeKind.Utc, historial[0].MessageTime.Kind);
        }

        [Fact]
        public void Canbus_TraeElHorometro_YCortaAlCompletar()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => p.EsCanbus ? ApiOnwayFalsa.Ok("canbus.json") : null;
            var dia = new DateTime(2026, 11, 20, 5, 0, 0, DateTimeKind.Utc);

            var puntos = e.Cliente().ObtenerHistorialCanbus("equipo-1", dia, dia.AddDays(1));

            Assert.Equal(new double?[] { 15230.5, 15231.4 }, puntos.Select(p => p.TotalEngineHours).ToArray());
            Assert.Contains("/devices/equipo-1/history/canbus?from=2026-11-20T05%3A00%3A00Z", Assert.Single(e.Api.Peticiones).Url);
        }

        [Fact]
        public void Canbus_SinDatos_DevuelveVacio()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => ApiOnwayFalsa.ConCuerpo(System.Net.HttpStatusCode.OK, "{\"records\":0,\"content\":null}");

            Assert.Empty(e.Cliente().ObtenerHistorialCanbus("equipo-1", e.Reloj.AhoraUtc, e.Reloj.AhoraUtc));
        }

        // ---------------- Placa ----------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PlacaVacia_DevuelveNull_SinLlamarAlApi(string placa)
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            Assert.Null(e.Cliente().BuscarDispositivoPorPlaca(placa));
            Assert.Empty(e.Api.Peticiones);
        }

        [Theory]
        [InlineData("ABC-123", "equipo-1")]
        [InlineData(" abc-123 ", "equipo-1")]  // sin espacios y sin distinguir mayúsculas
        [InlineData("XYZ-987", "equipo-2")]    // el alias del API también trae espacios
        [InlineData("def-456", "equipo-3")]
        public void Placa_SeComparaSinEspaciosNiMayusculas(string placa, string idEsperado)
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();

            Assert.Equal(idEsperado, e.Cliente().BuscarDispositivoPorPlaca(placa).Id);
        }

        [Fact]
        public void Placa_SinGuionNoCoincide()
        {
            // La normalización es solo espacios y mayúsculas: "ABC123" no es "ABC-123".
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();

            Assert.Null(e.Cliente().BuscarDispositivoPorPlaca("ABC123"));
        }

        [Fact]
        public void ListaDeDispositivos_SeGuardaTreintaMinutos_YUnaPlacaNuevaLaRenuevaUnaVez()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();
            var cliente = e.Cliente();

            Assert.NotNull(cliente.BuscarDispositivoPorPlaca("ABC-123"));
            Assert.Equal(2, e.Api.Cuantas(p => p.EsDispositivos));

            // Dentro de los 30 min: sin llamadas.
            e.Reloj.AhoraUtc = e.Reloj.AhoraUtc.AddMinutes(29);
            Assert.NotNull(cliente.BuscarDispositivoPorPlaca("DEF-456"));
            Assert.Equal(2, e.Api.Cuantas(p => p.EsDispositivos));

            // Placa que no está: vuelve a pedir la lista una sola vez.
            Assert.Null(cliente.BuscarDispositivoPorPlaca("QQQ-000"));
            Assert.Equal(4, e.Api.Cuantas(p => p.EsDispositivos));

            // Vencida la lista: se pide de nuevo, y si no está no se pide otra vez.
            e.Reloj.AhoraUtc = e.Reloj.AhoraUtc.AddMinutes(31);
            Assert.Null(cliente.BuscarDispositivoPorPlaca("QQQ-000"));
            Assert.Equal(6, e.Api.Cuantas(p => p.EsDispositivos));
        }

        [Fact]
        public void ListaDeDispositivos_NoSeComparteEntreClientesDePrueba()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = DispositivosEnDosPaginas();

            e.Cliente().BuscarDispositivoPorPlaca("ABC-123");
            e.Cliente().BuscarDispositivoPorPlaca("ABC-123");

            Assert.Equal(4, e.Api.Cuantas(p => p.EsDispositivos));
        }

        [Fact]
        public void UltimasPosiciones_PorPlacaEnMayusculas_ElPrimeroGana_YSeGuardaUnMinuto()
        {
            var e = new EscenarioOnway().ConTokenYSesionEnCache();
            e.Api.Responder = p => p.EsDispositivos ? ApiOnwayFalsa.Ok("dispositivos-con-posicion.json") : null;
            var cliente = e.Cliente();

            var foto = cliente.UltimasPosiciones();

            Assert.Equal(new[] { "ABC-123", "XYZ-987" }, foto.Keys.OrderBy(k => k).ToArray());
            Assert.Equal("equipo-1", foto["abc-123"].Id); // repetida: queda la primera; la clave no distingue mayúsculas
            Assert.Equal(55.5, foto["XYZ-987"].Expand.LastLocation.Speed);
            Assert.Contains("deviceExpandEnum=1", Assert.Single(e.Api.Peticiones).Url);

            // También sirve de lista de dispositivos (sin otra llamada).
            Assert.Equal("equipo-2", cliente.BuscarDispositivoPorPlaca("xyz-987").Id);
            e.Reloj.AhoraUtc = e.Reloj.AhoraUtc.AddSeconds(59);
            Assert.Same(foto, cliente.UltimasPosiciones());
            Assert.Single(e.Api.Peticiones);

            e.Reloj.AhoraUtc = e.Reloj.AhoraUtc.AddSeconds(2);
            Assert.NotSame(foto, cliente.UltimasPosiciones());
            Assert.Equal(2, e.Api.Peticiones.Count);
        }

        /// <summary>Simula que otro request dejó un token nuevo mientras este esperaba el bloqueo.</summary>
        private sealed class AlmacenQueRenuevaDuranteElBloqueo : IAlmacenAccesoOnway
        {
            private readonly RelojFalso _reloj;
            private OnwayAuthCache _cache;

            public AlmacenQueRenuevaDuranteElBloqueo(RelojFalso reloj) => _reloj = reloj;

            public OnwayAuthCache Obtener() => _cache;
            public void GuardarToken(string accessToken, DateTime expiraEnUtc) => throw new InvalidOperationException("No debía pedir token.");
            public void GuardarSesion(string clientId, string userId) { }

            public T ConBloqueoDeRenovacion<T>(Func<T> trabajo)
            {
                _cache = new OnwayAuthCache { AccessToken = "token-de-otro-request", TokenExpiraEn = _reloj.AhoraUtc.AddHours(24) };
                return trabajo();
            }
        }
    }
}
