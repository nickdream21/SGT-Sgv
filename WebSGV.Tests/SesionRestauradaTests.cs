using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using WebSGV.Helpers;
using WebSGV.Views;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Atrás tras cerrar sesión (bfcache): Chrome Android restauraba DashboardConductor con el nombre y el DNI. Las
    /// páginas con sesión llevan el id en el &lt;html&gt;, SesionRestaurada.js las oculta al salir y al volver pregunta
    /// a Views/SesionActiva.ashx (204 = misma sesión; si no, Login?error=sesion sin volver a enviar el formulario).
    /// </summary>
    public class SesionRestauradaTests
    {
        // ── Regla pura ──

        [Theory]
        [InlineData(7, "7", 204)]
        [InlineData(7, " 7 ", 204)]
        [InlineData(null, "7", 401)]   // sesión cerrada (Logout con Session.Abandon)
        [InlineData(null, null, 401)]
        [InlineData(8, "7", 409)]      // otro usuario entró en el mismo navegador: su sesión no se toca
        [InlineData(7, null, 409)]
        [InlineData(7, "", 409)]
        [InlineData(7, "07", 409)]
        [InlineData(7, "<script>", 409)]
        public void CodigoRespuesta_SoloLaMismaSesionDa204(int? idSesion, string idPagina, int esperado)
        {
            Assert.Equal(esperado, SesionRestauradaHelper.CodigoRespuesta(idSesion, idPagina));
        }

        [Fact]
        public void AtributosHtml_SinSesionVacio_ConSesionElIdYLasRutas()
        {
            Func<string, string> abs = r => "/sitio" + r.Substring(1);
            Assert.Equal("", SesionRestauradaHelper.AtributosHtml((int?)null, abs));
            Assert.Equal(" data-sgv-usuario=\"42\" data-sgv-sesion=\"/sitio/Views/SesionActiva.ashx\" data-sgv-login=\"/sitio/Views/Login.aspx\"",
                SesionRestauradaHelper.AtributosHtml(42, abs));
        }

        // ── Manejador con una petición falsa ──

        private static HttpContext Contexto(int? idUsuario, string consulta, StringWriter escrito)
        {
            var ctx = new HttpContext(
                new HttpRequest("", "http://localhost/Views/SesionActiva.ashx" + (consulta == null ? "" : "?" + consulta), consulta ?? ""),
                new HttpResponse(escrito));
            var contenedor = new HttpSessionStateContainer(
                "prueba", new SessionStateItemCollection(), new HttpStaticObjectsCollection(),
                30, true, HttpCookieMode.UseCookies, SessionStateMode.InProc, false);
            SessionStateUtility.AddHttpSessionStateToContext(ctx, contenedor);
            if (idUsuario.HasValue)
                ctx.Session[SesionHelper.ClaveUsuarioId] = idUsuario.Value;
            return ctx;
        }

        [Theory]
        [InlineData(5, "u=5", 204)]
        [InlineData(5, "u=6", 409)]
        [InlineData(null, "u=5", 401)]
        [InlineData(5, null, 409)]
        public void Manejador_RespondeSegunLaSesionSinCuerpo(int? idUsuario, string consulta, int esperado)
        {
            var escrito = new StringWriter();
            HttpContext ctx = Contexto(idUsuario, consulta, escrito);
            SesionActiva.Atender(ctx);
            Assert.Equal(esperado, ctx.Response.StatusCode);
            Assert.Equal("", escrito.ToString());
        }

        // ── Fuente: dónde se aplica y cómo ──

        private static string Leer(string relativo) => File.ReadAllText(Path.Combine(FuentesWeb.RaizWeb(), relativo));

        [Fact]
        public void Manejador_SesionDeSoloLecturaNoStoreYRegistrado()
        {
            string codigo = Leer("Views/SesionActiva.ashx.cs");
            Assert.Matches(@"public class SesionActiva : IHttpHandler, IReadOnlySessionState\r?\n", codigo);
            Assert.Contains("Cache.SetNoStore()", codigo);
            Assert.DoesNotContain("resp.Write", codigo);

            string csproj = Leer("WebSGV.csproj");
            Assert.Contains("<Content Include=\"Views\\SesionActiva.ashx\" />", csproj);
            Assert.Contains("<Compile Include=\"Views\\SesionActiva.ashx.cs\">", csproj);
            Assert.Contains("<Compile Include=\"Helpers\\SesionRestauradaHelper.cs\" />", csproj);
            Assert.Contains("<Content Include=\"Scripts\\paginas\\SesionRestaurada.js\" />", csproj);
        }

        [Theory]
        [InlineData("Site.Master", "<html lang=\"es\"<%= AtributosSesion %>>")]
        [InlineData("Views/FirmarLiquidacion.aspx", "<html lang=\"es\"<%= WebSGV.Helpers.SesionRestauradaHelper.AtributosHtml(Session) %>>")]
        [InlineData("Views/ConformarLiquidacion.aspx", "<html lang=\"es\"<%= WebSGV.Helpers.SesionRestauradaHelper.AtributosHtml(Session) %>>")]
        public void PaginasConSesion_CarganElScriptEnElHead(string archivo, string etiquetaHtml)
        {
            string marcado = Leer(archivo);
            Assert.Contains(etiquetaHtml, marcado);
            string head = Regex.Match(marcado, @"<head\b.*?</head>", RegexOptions.Singleline).Value;
            Assert.Contains("RecursoHelper.Url(\"~/Scripts/paginas/SesionRestaurada.js\")", head);
        }

        [Fact]
        public void Master_SoloPoneLosAtributosConSesionYFueraDeLasPublicas()
        {
            string codigo = Leer("Site.Master.cs");
            int publica = codigo.IndexOf("if (esPaginaPublica)", StringComparison.Ordinal);
            int sinSesion = codigo.IndexOf("if (!TieneSesionActivaLocal())", StringComparison.Ordinal);
            int asignacion = codigo.IndexOf("AtributosSesion = SesionRestauradaHelper.AtributosHtml(Session);", StringComparison.Ordinal);
            Assert.True(publica > 0 && sinSesion > publica && asignacion > sinSesion,
                "AtributosSesion se asigna después de descartar las páginas públicas y la falta de sesión.");
        }

        [Fact]
        public void Script_DestinoSegunElCodigoSinRecargarNiAlert()
        {
            string js = Leer("Scripts/paginas/SesionRestaurada.js");
            // 204 destapa; solo 401 (sin sesión) va con ?error=sesion; 409, 403, error y demora van al Login sin
            // parámetro (con sesión viva, al inicio del rol sin el rompe-bucles de ?error=sesion, que la cerraría).
            Assert.Contains("if (xhr.status === 204) mostrar();", js);
            Assert.Contains("else if (xhr.status === 401) irA(urlLogin + '?error=sesion');", js);
            Assert.Contains("else irA(urlLogin);", js);
            Assert.Contains("xhr.onerror = function () { irA(urlLogin); };", js);
            Assert.Contains("xhr.ontimeout = function () { irA(urlLogin); };", js);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(js, @"\?error=sesion'\)").Cast<System.Text.RegularExpressions.Match>());
            Assert.Contains("location.replace(url)", js);
            // Solo al restaurar desde bfcache: una carga desde el historial ya la validó el servidor (no-store).
            Assert.Contains("window.addEventListener('pageshow', function (e) { if (e.persisted) comprobar(); });", js);
            Assert.Contains("window.addEventListener('pagehide', function (e) { if (e.persisted) ocultar(); });", js);
            Assert.DoesNotContain("back_forward", js);
            Assert.DoesNotContain("getEntriesByType", js);
            // Capa opaca encima de todo (los hijos con visibility: visible e iframes también quedan tapados).
            Assert.Contains("s.position = 'fixed';", js);
            Assert.Contains("s.zIndex = '2147483647';", js);
            Assert.DoesNotContain("reload(", js);
            Assert.DoesNotContain("alert(", js);
        }
    }
}
