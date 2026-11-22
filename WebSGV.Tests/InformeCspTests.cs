using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WebSGV.Services.Operacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Receptor de informes CSP (E4-5, CspInforme.ashx): formatos, recorte, tamaño, límites y que nunca lanza.</summary>
    public class InformeCspTests
    {
        private const string TipoAntiguo = "application/csp-report";
        private const string TipoReporting = "application/reports+json";

        private const string CuerpoAntiguo =
            "{\"csp-report\":{\"document-uri\":\"https://sgv.example/Views/Inicio.aspx?token=SECRETO123#seccion\"," +
            "\"referrer\":\"https://sgv.example/Views/Login.aspx?x=1\",\"violated-directive\":\"script-src-elem\"," +
            "\"effective-directive\":\"script-src-elem\",\"original-policy\":\"default-src 'self'\"," +
            "\"disposition\":\"report\",\"blocked-uri\":\"https://malo.example:8443/x.js?k=VALOR\"," +
            "\"status-code\":200,\"script-sample\":\"alert('MUESTRA')\"}}";

        private const string CuerpoReporting =
            "[{\"type\":\"csp-violation\",\"age\":10,\"url\":\"https://sgv.example/Views/A.aspx?id=7\",\"user_agent\":\"x\"," +
            "\"body\":{\"documentURL\":\"https://sgv.example/Views/A.aspx?id=7#f\",\"effectiveDirective\":\"script-src-attr\"," +
            "\"blockedURL\":\"inline\",\"disposition\":\"report\",\"sample\":\"__doPostBack('SECRETO')\",\"statusCode\":200}}," +
            "{\"type\":\"deprecation\",\"body\":{\"id\":\"x\"}}," +
            "{\"type\":\"csp-violation\",\"body\":{\"documentURL\":\"https://sgv.example/Views/B.aspx\",\"effectiveDirective\":\"frame-ancestors\"," +
            "\"blockedURL\":\"https://otro.example/marco?p=1\",\"disposition\":\"enforce\"}}]";

        private static int contadorPrefijo;

        private static LimiteInformesCsp LimiteNuevo(Func<DateTime> reloj, int porIp = 20, int total = 300) =>
            new LimiteInformesCsp("PRUEBA|Csp|" + Guid.NewGuid().ToString("N") + "|" + (contadorPrefijo++) + "|", reloj, porIp, total);

        private static Stream Cuerpo(string texto) => new MemoryStream(Encoding.UTF8.GetBytes(texto ?? ""));

        private static (int Codigo, List<ViolacionCsp> Log) Enviar(string metodo, string tipo, string cuerpo,
            LimiteInformesCsp limite = null, string ip = "10.0.0.1", long? largo = null)
        {
            var log = new List<ViolacionCsp>();
            byte[] bytes = Encoding.UTF8.GetBytes(cuerpo ?? "");
            int codigo = InformeCsp.Procesar(metodo, tipo, largo ?? bytes.Length, new MemoryStream(bytes), ip,
                limite ?? LimiteNuevo(() => DateTime.UtcNow), log.Add);
            return (codigo, log);
        }

        // ── Formatos ──

        [Fact]
        public void FormatoCspReport_RegistraDirectivaOrigenYRuta()
        {
            var r = Enviar("POST", TipoAntiguo, CuerpoAntiguo);
            Assert.Equal(204, r.Codigo);
            ViolacionCsp v = Assert.Single(r.Log);
            Assert.Equal("script-src-elem", v.Directiva);
            Assert.Equal("https://malo.example:8443", v.Origen);
            Assert.Equal("/Views/Inicio.aspx", v.Ruta);
            Assert.Equal("informe", v.Politica);
        }

        [Fact]
        public void FormatoReportsJson_SoloLasViolacionesCsp()
        {
            var r = Enviar("POST", TipoReporting, CuerpoReporting);
            Assert.Equal(204, r.Codigo);
            Assert.Equal(2, r.Log.Count);

            Assert.Equal("script-src-attr", r.Log[0].Directiva);
            Assert.Equal("inline", r.Log[0].Origen);
            Assert.Equal("/Views/A.aspx", r.Log[0].Ruta);
            Assert.Equal("informe", r.Log[0].Politica);

            Assert.Equal("frame-ancestors", r.Log[1].Directiva);
            Assert.Equal("https://otro.example", r.Log[1].Origen);
            Assert.Equal("/Views/B.aspx", r.Log[1].Ruta);
            Assert.Equal("aplicada", r.Log[1].Politica);
        }

        [Fact]
        public void TipoConCharset_SeAcepta()
        {
            Assert.Single(Enviar("POST", "application/csp-report; charset=utf-8", CuerpoAntiguo).Log);
            Assert.Single(Enviar("POST", "application/json", CuerpoAntiguo).Log);
        }

        [Fact]
        public void ViolatedDirectiveAntiguoConValor_SoloElNombre()
        {
            string cuerpo = "{\"csp-report\":{\"document-uri\":\"https://s/x.aspx\",\"violated-directive\":\"script-src 'self'\",\"blocked-uri\":\"eval\"}}";
            ViolacionCsp v = Assert.Single(Enviar("POST", TipoAntiguo, cuerpo).Log);
            Assert.Equal("script-src", v.Directiva);
            Assert.Equal("eval", v.Origen);
        }

        // ── Lo que nunca llega al log ──

        [Fact]
        public void Log_SinQuerystringFragmentoNiMuestra()
        {
            var todo = Enviar("POST", TipoAntiguo, CuerpoAntiguo).Log.Concat(Enviar("POST", TipoReporting, CuerpoReporting).Log);
            foreach (ViolacionCsp v in todo)
            {
                string linea = string.Join("|", v.Directiva, v.Origen, v.Ruta, v.Politica);
                Assert.DoesNotContain("?", linea);
                Assert.DoesNotContain("#", linea);
                Assert.DoesNotContain("SECRETO", linea);
                Assert.DoesNotContain("VALOR", linea);
                Assert.DoesNotContain("MUESTRA", linea);
                Assert.DoesNotContain("alert", linea);
                Assert.DoesNotContain("__doPostBack", linea);
            }
        }

        [Theory]
        [InlineData("https://sgv.example/Views/X.aspx?token=abc#f", "/Views/X.aspx")]
        [InlineData("http://localhost:50291/Views/Login.aspx", "/Views/Login.aspx")]
        [InlineData("/Views/Y.aspx?a=1", "/Views/Y.aspx")]
        [InlineData("/Views/Z.aspx#parte", "/Views/Z.aspx")]
        [InlineData("about:blank", "about")]
        [InlineData("", "(vacía)")]
        [InlineData(null, "(vacía)")]
        [InlineData("?solo=consulta", "(vacía)")]
        public void RutaSinConsulta(string documento, string esperado)
        {
            Assert.Equal(esperado, InformeCsp.RutaSinConsulta(documento));
        }

        [Theory]
        [InlineData("inline", "inline")]
        [InlineData("EVAL", "eval")]
        [InlineData("wasm-eval", "wasm-eval")]
        [InlineData("data", "data")]
        [InlineData("data:image/png;base64,iVBORw0KGgo=", "data")]
        [InlineData("blob:https://sgv.example/1234-5678", "blob")]
        [InlineData("https://tile.openstreetmap.org/3/4/5.png?x=1", "https://tile.openstreetmap.org")]
        [InlineData("https://cdn.example:8443/a/b.js", "https://cdn.example:8443")]
        [InlineData("wss://x.example/socket?t=1", "wss://x.example")]
        [InlineData("chrome-extension://abcdef/script.js", "chrome-extension")]
        [InlineData("", "(vacío)")]
        [InlineData(null, "(vacío)")]
        [InlineData("<script>", "(otro)")]
        public void RecortarOrigen(string bloqueado, string esperado)
        {
            Assert.Equal(esperado, InformeCsp.RecortarOrigen(bloqueado));
        }

        [Fact]
        public void CaracteresDeControl_NoPartenLaLineaDelLog()
        {
            string cuerpo = "{\"csp-report\":{\"document-uri\":\"/Views/A\\r\\n[ERR] falso.aspx\",\"effective-directive\":\"img-src\\n\",\"blocked-uri\":\"x\"}}";
            ViolacionCsp v = Assert.Single(Enviar("POST", TipoAntiguo, cuerpo).Log);
            Assert.DoesNotContain("\n", v.Ruta);
            Assert.DoesNotContain("\r", v.Ruta);
            Assert.Equal("img-src", v.Directiva);
        }

        [Fact]
        public void DirectivaRara_Desconocida()
        {
            Assert.Equal("(desconocida)", InformeCsp.LimpiarDirectiva("script<src"));
            Assert.Equal("(desconocida)", InformeCsp.LimpiarDirectiva(new string('a', 41)));
            Assert.Equal("(desconocida)", InformeCsp.LimpiarDirectiva(null));
        }

        // ── Método, tamaño, tipo, JSON roto ──

        [Theory]
        [InlineData("GET")]
        [InlineData("HEAD")]
        [InlineData("PUT")]
        public void NoPost_405SinLog(string metodo)
        {
            var r = Enviar(metodo, TipoAntiguo, CuerpoAntiguo);
            Assert.Equal(405, r.Codigo);
            Assert.Empty(r.Log);
        }

        [Fact]
        public void CuerpoDeMasDe8Kb_413SinLog()
        {
            string grande = CuerpoAntiguo.Replace("default-src 'self'", "default-src " + new string('a', InformeCsp.BytesMaximos));
            var declarado = Enviar("POST", TipoAntiguo, grande);
            Assert.Equal(413, declarado.Codigo);
            Assert.Empty(declarado.Log);

            // Sin Content-Length (chunked): lo corta la lectura.
            var sinLargo = Enviar("POST", TipoAntiguo, grande, largo: -1);
            Assert.Equal(413, sinLargo.Codigo);
            Assert.Empty(sinLargo.Log);
        }

        [Fact]
        public void CuerpoDeJusto8Kb_SeAcepta()
        {
            string relleno = new string('a', InformeCsp.BytesMaximos - CuerpoAntiguo.Length);
            string justo = CuerpoAntiguo.Replace("default-src 'self'", "default-src 'self'" + relleno);
            Assert.Equal(InformeCsp.BytesMaximos, Encoding.UTF8.GetByteCount(justo));
            var r = Enviar("POST", TipoAntiguo, justo);
            Assert.Equal(204, r.Codigo);
            Assert.Single(r.Log);
        }

        [Theory]
        [InlineData(TipoAntiguo, "{\"csp-report\":")]
        [InlineData(TipoAntiguo, "no es json")]
        [InlineData(TipoAntiguo, "")]
        [InlineData(TipoAntiguo, "{\"otra\":{}}")]
        [InlineData(TipoReporting, "[1,2,{\"type\":\"csp-violation\"}]")]
        [InlineData("text/plain", CuerpoAntiguo)]
        [InlineData(null, CuerpoAntiguo)]
        public void JsonRotoOTipoDesconocido_204SinLog(string tipo, string cuerpo)
        {
            var r = Enviar("POST", tipo, cuerpo);
            Assert.Equal(204, r.Codigo);
            Assert.Empty(r.Log);
        }

        [Fact]
        public void SiElLogLanza_204Igual()
        {
            int codigo = InformeCsp.Procesar("POST", TipoAntiguo, -1, Cuerpo(CuerpoAntiguo), "10.0.0.1",
                LimiteNuevo(() => DateTime.UtcNow), _ => throw new InvalidOperationException("falla del log"));
            Assert.Equal(204, codigo);
        }

        [Fact]
        public void SinCuerpo_204()
        {
            Assert.Equal(204, InformeCsp.Procesar("POST", TipoAntiguo, 0, null, "10.0.0.1", LimiteNuevo(() => DateTime.UtcNow), null));
        }

        [Fact]
        public void UnEnvio_NoMiraMasDeDiezInformes()
        {
            string cuerpo = ReportsJson(Enumerable.Range(0, 15).Select(i => "/Views/P" + i + ".aspx").ToArray());
            Assert.Equal(InformeCsp.MaximoPorEnvio, Enviar("POST", TipoReporting, cuerpo).Log.Count);
        }

        // ── Límites ──

        /// <summary>Informe csp-report de un eval en <paramref name="ruta"/> (cada ruta es una violación distinta).</summary>
        private static string CspReport(string ruta, string bloqueado = "eval") =>
            "{\"csp-report\":{\"document-uri\":\"https://sgv.example" + ruta + "?x=1\",\"effective-directive\":\"script-src\"," +
            "\"blocked-uri\":\"" + bloqueado + "\",\"disposition\":\"report\"}}";

        private static string ReportsJson(params string[] rutas) =>
            "[" + string.Join(",", rutas.Select(r =>
                "{\"type\":\"csp-violation\",\"body\":{\"documentURL\":\"https://s" + r + "\",\"effectiveDirective\":\"img-src\",\"blockedURL\":\"data\"}}")) + "]";

        [Fact]
        public void LimitePorIp_VeinteEnElMinutoYLuegoOtroMinuto()
        {
            DateTime ahora = new DateTime(2026, 11, 22, 10, 15, 5, DateTimeKind.Utc);
            var limite = LimiteNuevo(() => ahora);

            for (int i = 0; i < InformeCsp.MaximoPorIpPorMinuto; i++)
                Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/Views/P" + i + ".aspx"), limite, "10.0.0.1").Log);

            var sobra = Enviar("POST", TipoAntiguo, CspReport("/Views/Nueva.aspx"), limite, "10.0.0.1");
            Assert.Equal(204, sobra.Codigo);
            Assert.Empty(sobra.Log);

            // Otra IP sí entra; la primera vuelve a entrar al minuto siguiente.
            Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/Views/OtraIp.aspx"), limite, "10.0.0.2").Log);
            ahora = ahora.AddMinutes(1);
            Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/Views/Nueva.aspx"), limite, "10.0.0.1").Log);
        }

        [Fact]
        public void LimitePorIp_CuentaCadaInformeDeUnEnvio()
        {
            var limite = LimiteNuevo(() => new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc), porIp: 3);
            Assert.Equal(2, Enviar("POST", TipoReporting, ReportsJson("/a1.aspx", "/a2.aspx"), limite).Log.Count);
            Assert.Single(Enviar("POST", TipoReporting, ReportsJson("/b1.aspx", "/b2.aspx"), limite).Log);
            Assert.Empty(Enviar("POST", TipoReporting, ReportsJson("/c1.aspx", "/c2.aspx"), limite).Log);
        }

        // ── Deduplicación ──

        [Fact]
        public void Deduplicacion_CincuentaIgualesNoGastanCupoYUnaDistintaSeRegistra()
        {
            DateTime ahora = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
            var limite = LimiteNuevo(() => ahora);
            string eval = CspReport("/Views/Inicio.aspx");

            int registradas = 0;
            for (int i = 0; i < 50; i++)
            {
                var r = Enviar("POST", TipoAntiguo, eval, limite, "10.0.0.1");
                Assert.Equal(204, r.Codigo);
                registradas += r.Log.Count;
            }
            Assert.Equal(1, registradas);

            // 50 envíos de la misma IP en el mismo minuto, pero las repeticiones no gastaron cupo.
            ViolacionCsp nueva = Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/Views/Inicio.aspx", "https://malo.example/x.js"), limite, "10.0.0.1").Log);
            Assert.Equal("https://malo.example", nueva.Origen);
        }

        [Fact]
        public void Deduplicacion_TrasLaHoraSeVuelveARegistrar()
        {
            DateTime ahora = new DateTime(2026, 11, 22, 10, 30, 0, DateTimeKind.Utc);
            var limite = LimiteNuevo(() => ahora);
            string eval = CspReport("/Views/Inicio.aspx");

            Assert.Single(Enviar("POST", TipoAntiguo, eval, limite).Log);
            ahora = ahora.AddMinutes(59);
            Assert.Empty(Enviar("POST", TipoAntiguo, eval, limite).Log);
            ahora = ahora.AddMinutes(1);
            Assert.Single(Enviar("POST", TipoAntiguo, eval, limite).Log);
            Assert.Empty(Enviar("POST", TipoAntiguo, eval, limite).Log);
        }

        [Fact]
        public void Deduplicacion_EnElMismoEnvioCuentaUnaVez()
        {
            var r = Enviar("POST", TipoReporting, ReportsJson("/a.aspx", "/a.aspx", "/a.aspx", "/b.aspx"));
            Assert.Equal(new[] { "/a.aspx", "/b.aspx" }, r.Log.Select(v => v.Ruta).ToArray());
        }

        [Fact]
        public void Deduplicacion_ConTope_LasDeMasCuentanContraElCupo()
        {
            DateTime ahora = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
            var limite = new LimiteInformesCsp("PRUEBA|Csp|" + Guid.NewGuid().ToString("N") + "|", () => ahora,
                porIpPorMinuto: 100, totalPorHora: 100, deduplicadasPorHora: 2);

            Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/a.aspx"), limite).Log);
            Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/b.aspx"), limite).Log);
            Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/c.aspx"), limite).Log);

            // a y b quedaron recordadas; c no (tope de 2), así que cada repetición va contra el cupo normal.
            Assert.Empty(Enviar("POST", TipoAntiguo, CspReport("/a.aspx"), limite).Log);
            Assert.Empty(Enviar("POST", TipoAntiguo, CspReport("/b.aspx"), limite).Log);
            Assert.Single(Enviar("POST", TipoAntiguo, CspReport("/c.aspx"), limite).Log);
        }

        [Fact]
        public void ClaveDeduplicacion_NormalizadaYConHashSiEsLarga()
        {
            var a = new ViolacionCsp { Politica = "informe", Directiva = "script-src", Origen = "eval", Ruta = "/Views/Inicio.aspx" };
            var b = new ViolacionCsp { Politica = "informe", Directiva = "script-src", Origen = "eval", Ruta = "/VIEWS/inicio.ASPX" };
            Assert.Equal("informe|script-src|eval|/views/inicio.aspx", InformeCsp.ClaveDeduplicacion(a));
            Assert.Equal(InformeCsp.ClaveDeduplicacion(a), InformeCsp.ClaveDeduplicacion(b));

            var larga = new ViolacionCsp { Politica = "informe", Directiva = "img-src", Origen = "data", Ruta = "/" + new string('x', 190) };
            string clave = InformeCsp.ClaveDeduplicacion(larga);
            Assert.StartsWith("sha256:", clave);
            Assert.Equal(7 + 64, clave.Length);
            Assert.NotEqual(clave, InformeCsp.ClaveDeduplicacion(
                new ViolacionCsp { Politica = "informe", Directiva = "img-src", Origen = "data", Ruta = "/" + new string('y', 190) }));
        }

        [Fact]
        public void LimiteTotal_TrescientosPorHoraEntreTodasLasIp()
        {
            DateTime ahora = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
            var limite = LimiteNuevo(() => ahora);

            int aceptados = 0;
            for (int i = 0; i < InformeCsp.MaximoTotalPorHora + 20; i++)
                if (limite.Reservar("10.1." + (i / 200) + "." + (i % 200))) aceptados++;
            Assert.Equal(InformeCsp.MaximoTotalPorHora, aceptados);
            Assert.False(limite.Reservar("10.9.9.9"));

            ahora = ahora.AddHours(1);
            Assert.True(limite.Reservar("10.9.9.9"));
        }

        // ── Handler ──

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        [Fact]
        public void Handler_SinSesionNoStoreYRegistradoEnElProyecto()
        {
            string raiz = RaizWeb();
            string codigo = File.ReadAllText(Path.Combine(raiz, "CspInforme.ashx.cs"));
            // Sin IRequiresSessionState / IReadOnlySessionState: solo IHttpHandler.
            Assert.Matches(@"public class CspInforme : IHttpHandler\r?\n", codigo);
            Assert.Contains("Cache.SetNoStore()", codigo);
            Assert.DoesNotContain("script-sample", codigo);

            string csproj = File.ReadAllText(Path.Combine(raiz, "WebSGV.csproj"));
            Assert.Contains("<Content Include=\"CspInforme.ashx\" />", csproj);
            Assert.Contains("<Compile Include=\"CspInforme.ashx.cs\">", csproj);
            Assert.Contains("<Compile Include=\"Services\\Operacion\\InformeCsp.cs\" />", csproj);
        }
    }
}
