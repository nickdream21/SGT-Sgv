using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.SessionState;
using System.Web.UI.WebControls;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Envío único en el servidor (E5-3): <see cref="EnvioUnicoHelper"/> emite un token por formulario mostrado y lo
    /// consume antes de escribir; un segundo POST con el mismo token no escribe. Se prueban el núcleo
    /// (<see cref="TokensEnvioUnico"/>), el helper sobre una sesión falsa, el envoltorio sobre HttpContext.Current y,
    /// leyendo los fuentes, que las páginas de la lista cerrada lo usen en su manejador antes de escribir.
    /// </summary>
    public class EnvioUnicoHelperTests
    {
        // ── Núcleo ───────────────────────────────────────────────────────

        [Fact]
        public void Emitir_Consumir_UnaSolaVez()
        {
            var t = new TokensEnvioUnico();
            string token = t.Emitir("F");
            Assert.Matches("^[0-9a-f]{32}$", token);
            Assert.True(t.Consumir("F", token));
            Assert.False(t.Consumir("F", token));
            Assert.Equal(0, t.Pendientes("F"));
        }

        [Fact]
        public void TokenDesconocido_VacioONull_NoSirven()
        {
            var t = new TokensEnvioUnico();
            t.Emitir("F");
            Assert.False(t.Consumir("F", Guid.NewGuid().ToString("N")));
            Assert.False(t.Consumir("F", ""));
            Assert.False(t.Consumir("F", "   "));
            Assert.False(t.Consumir("F", null));
            Assert.False(t.Consumir(null, "x"));
            Assert.False(t.Consumir("SinEmitir", "x"));
            Assert.Equal(1, t.Pendientes("F"));
        }

        [Fact]
        public void TokenDeOtroFormulario_NoSirve()
        {
            var t = new TokensEnvioUnico();
            string token = t.Emitir("A");
            Assert.False(t.Consumir("B", token));
            Assert.True(t.Consumir("A", token));
        }

        [Fact]
        public void VariasPestanas_GuardaLasUltimas20()
        {
            var t = new TokensEnvioUnico();
            var tokens = Enumerable.Range(0, EnvioUnicoHelper.MaximoPorClave + 1).Select(_ => t.Emitir("F")).ToList();
            Assert.Equal(EnvioUnicoHelper.MaximoPorClave, t.Pendientes("F"));

            // La pestaña más vieja quedó fuera; las otras 20 registran una vez cada una, en cualquier orden.
            Assert.False(t.Consumir("F", tokens[0]));
            foreach (string tk in tokens.Skip(1).Reverse())
                Assert.True(t.Consumir("F", tk));
            Assert.All(tokens, tk => Assert.False(t.Consumir("F", tk)));
        }

        [Fact]
        public void Emitir_SinClave_Falla()
        {
            var t = new TokensEnvioUnico();
            Assert.Throws<ArgumentException>(() => t.Emitir(""));
            Assert.Throws<ArgumentException>(() => t.Emitir(null));
        }

        // ── Helper sobre una sesión falsa ────────────────────────────────

        /// <summary>Sesión en memoria (lo único que usa el helper: el indexador).</summary>
        internal sealed class SesionFalsa : HttpSessionStateBase
        {
            private readonly Dictionary<string, object> _datos = new Dictionary<string, object>();
            public override object this[string name]
            {
                get => _datos.TryGetValue(name, out object v) ? v : null;
                set => _datos[name] = value;
            }
            public override void Remove(string name) => _datos.Remove(name);

            /// <summary>Sesión con usuario autenticado (la clave de SesionHelper, como la deja el login).</summary>
            public static SesionFalsa ConUsuario(int idUsuario = 1)
            {
                var s = new SesionFalsa();
                s[SesionHelper.ClaveUsuarioId] = idUsuario;
                return s;
            }
        }

        [Fact]
        public void Helper_GuardaLosTokensEnSuClaveDeSesion()
        {
            var sesion = SesionFalsa.ConUsuario();
            string token = EnvioUnicoHelper.Emitir(sesion, "F");
            Assert.IsType<TokensEnvioUnico>(sesion[EnvioUnicoHelper.ClaveSesion]);
            Assert.True(EnvioUnicoHelper.Consumir(sesion, "F", token));
            Assert.False(EnvioUnicoHelper.Consumir(sesion, "F", token));
        }

        [Fact]
        public void Helper_OtraSesion_NoReconoceElToken()
        {
            string token = EnvioUnicoHelper.Emitir(SesionFalsa.ConUsuario(), "F");
            Assert.False(EnvioUnicoHelper.Consumir(SesionFalsa.ConUsuario(), "F", token));
        }

        [Fact]
        public void Helper_SinSesion_FallaConElMensajeDeSesion()
        {
            var e1 = Assert.Throws<ErrorNegocioException>(() => EnvioUnicoHelper.Emitir((HttpSessionStateBase)null, "F"));
            Assert.Equal(EnvioUnicoHelper.MensajeSinSesion, e1.Message);

            // Sesión vencida: ASP.NET da una sesión nueva, sin usuario.
            var vencida = new SesionFalsa();
            var e2 = Assert.Throws<ErrorNegocioException>(() => EnvioUnicoHelper.Consumir(vencida, "F", Guid.NewGuid().ToString("N")));
            Assert.Equal(EnvioUnicoHelper.MensajeSinSesion, e2.Message);
            Assert.Null(vencida[EnvioUnicoHelper.ClaveSesion]);
            Assert.Equal(EnvioUnicoHelper.MensajeSinSesion, MensajeError.ExtraerMensajeDeNegocio(e2));
        }

        // ── Envoltorio sobre HttpContext.Current ─────────────────────────

        private sealed class ContextoFalso : IDisposable
        {
            public HttpSessionState Sesion { get; }

            public ContextoFalso()
            {
                var ctx = new HttpContext(
                    new HttpRequest("", "http://localhost/Views/RegistroPlantas.aspx", ""),
                    new HttpResponse(new StringWriter()));
                var contenedor = new HttpSessionStateContainer(
                    "prueba", new SessionStateItemCollection(), new HttpStaticObjectsCollection(),
                    30, true, HttpCookieMode.UseCookies, SessionStateMode.InProc, false);
                SessionStateUtility.AddHttpSessionStateToContext(ctx, contenedor);
                HttpContext.Current = ctx;
                Sesion = ctx.Session;
            }

            public void Dispose() => HttpContext.Current = null;
        }

        [Fact]
        public void Envoltorio_EmiteEnElCampoYConsumeUnaVez()
        {
            using (var ctx = new ContextoFalso())
            {
                SesionHelper.Iniciar(ctx.Sesion, 7, "ADMIN", "Prueba");
                var campo = new HiddenField();
                EnvioUnicoHelper.Emitir("F", campo);
                Assert.Matches("^[0-9a-f]{32}$", campo.Value);

                string anterior = campo.Value;
                EnvioUnicoHelper.Emitir("F", campo);   // tras un registro correcto: token nuevo
                Assert.NotEqual(anterior, campo.Value);

                Assert.True(EnvioUnicoHelper.Consumir("F", campo.Value));
                Assert.False(EnvioUnicoHelper.Consumir("F", campo.Value));
                Assert.True(EnvioUnicoHelper.Consumir("F", anterior));   // otra pestaña con el token anterior
            }
        }

        [Fact]
        public void Envoltorio_SinSesionIniciada_FallaConElMensajeDeSesion()
        {
            using (new ContextoFalso())
            {
                var e = Assert.Throws<ErrorNegocioException>(() => EnvioUnicoHelper.Consumir("F", "x"));
                Assert.Equal(EnvioUnicoHelper.MensajeSinSesion, e.Message);
            }
            var e2 = Assert.Throws<ErrorNegocioException>(() => EnvioUnicoHelper.Emitir("F", new HiddenField()));
            Assert.Equal(EnvioUnicoHelper.MensajeSinSesion, e2.Message);
        }

        // ── Fuentes: lista cerrada de páginas ────────────────────────────

        private static string RaizWeb()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "WebSGV", "Views")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "WebSGV");
        }

        private static string Leer(string relativo) =>
            File.ReadAllText(Path.Combine(RaizWeb(), relativo.Replace('/', Path.DirectorySeparatorChar)));

        /// <summary>Cuerpo del método <paramref name="metodo"/> (hasta el siguiente miembro <c>protected</c>/<c>private</c>/<c>public</c>).</summary>
        private static string Cuerpo(string fuente, string metodo)
        {
            var m = Regex.Match(fuente, @"void\s+" + Regex.Escape(metodo) + @"\s*\([^)]*\)\s*\{(?<c>.*?)\n\s{8}(protected|private|public|internal|#region|#endregion)\b",
                RegexOptions.Singleline);
            Assert.True(m.Success, "No se encontró el manejador " + metodo);
            return m.Groups["c"].Value;
        }

        public static IEnumerable<object[]> Paginas() => new[]
        {
            // página o control (sin extensión), extensión del marcado, manejador, campo oculto, clave, primera escritura.
            // "Crear nuevo viaje" de RegistroDespacho vive en ViajesConductor.ascx desde E6-26.
            new object[] { "Views/MovimientosViaje", ".aspx", "btnRegistrar_Click", "hfEnvioUnico", "ClaveEnvioUnico", "fuAdjunto.HasFile" },
            new object[] { "Views/Controles/Despachos/ViajesConductor", ".ascx", "btnCrearNuevoViaje_Click", "hfEnvioUnicoViaje", "ClaveEnvioUnicoViaje", "CrearNuevoViajeProgreso(" },
            new object[] { "Views/RegistroPlantas", ".aspx", "btnRegistrar_Click", "hfEnvioUnico", "ClaveEnvioUnico", "PlantaService.Registrar(" },
        };

        [Theory]
        [MemberData(nameof(Paginas))]
        public void Pagina_ConsumeElTokenAntesDeEscribir(string pagina, string extension, string manejador, string campo, string clave, string escritura)
        {
            string cs = Leer(pagina + extension + ".cs");
            string cuerpo = Cuerpo(cs, manejador);

            int consumir = cuerpo.IndexOf("EnvioUnicoHelper.Consumir(" + clave + ",", StringComparison.Ordinal);
            int escribe = cuerpo.IndexOf(escritura, StringComparison.Ordinal);
            Assert.True(consumir >= 0, $"{pagina}.{manejador} debe llamar a EnvioUnicoHelper.Consumir({clave}, …) (E5-3).");
            Assert.True(escribe > consumir, $"{pagina}.{manejador}: Consumir debe ir antes de «{escritura}».");
            Assert.Contains("EnvioUnicoHelper.MensajeYaRegistrado", cuerpo);

            // Token nuevo en cada respuesta del manejador (el siguiente registro legítimo funciona) y al mostrar el formulario.
            Assert.Contains("EnvioUnicoHelper.Emitir(" + clave + ", " + campo + ")", cuerpo);
            var pageLoad = Cuerpo(cs, "Page_Load");
            Assert.Contains("EnvioUnicoHelper.Emitir(" + clave + ", " + campo + ")", pageLoad);

            Assert.Matches(@"<asp:HiddenField\s+ID=""" + campo + @"""", Leer(pagina + extension));
        }

        [Fact]
        public void RegistroDespacho_TokenDentroDelPanelQueRefresca_YBotonConConfirmacion()
        {
            // El bloque de viajes es ViajesConductor.ascx desde E6-26; la página solo lo coloca.
            Assert.Contains("<sgv:ViajesConductor ID=\"ucViajesConductor\" runat=\"server\" />", Leer("Views/RegistroDespacho.aspx"));
            string aspx = Leer("Views/Controles/Despachos/ViajesConductor.ascx");
            var panel = Regex.Match(aspx, @"<asp:UpdatePanel ID=""UpdatePanelViajes"".*?<ContentTemplate>(?<c>.*?)</ContentTemplate>", RegexOptions.Singleline);
            Assert.True(panel.Success);
            string contenido = panel.Groups["c"].Value;
            int campo = contenido.IndexOf(@"ID=""hfEnvioUnicoViaje""", StringComparison.Ordinal);
            Assert.True(campo >= 0, "hfEnvioUnicoViaje debe estar dentro de UpdatePanelViajes: si no, el token nuevo no llega al navegador.");
            Assert.True(campo < contenido.IndexOf(@"ID=""pnlViajesProgreso""", StringComparison.Ordinal),
                "hfEnvioUnicoViaje debe ir fuera de pnlViajesProgreso (oculto, no se dibuja).");

            var boton = Regex.Match(aspx, @"<asp:Button ID=""btnCrearNuevoViaje"".*?/>", RegexOptions.Singleline);
            Assert.True(boton.Success);
            Assert.Contains("data-confirmar=\"", boton.Value);

            // El bloque no se dibuja hasta elegir un conductor: el token se renueva cada vez que aparece el botón.
            string cargar = Cuerpo(Leer("Views/Controles/Despachos/ViajesConductor.ascx.cs"), "MostrarVarios");
            Assert.True(cargar.IndexOf("EnvioUnicoHelper.Emitir(ClaveEnvioUnicoViaje, hfEnvioUnicoViaje)", StringComparison.Ordinal)
                        > cargar.IndexOf("pnlMultiplesViajes.Visible = true", StringComparison.Ordinal));
        }
    }
}
