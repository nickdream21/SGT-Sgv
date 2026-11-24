using System;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Sesión vigente (auditoría de seguridad, tarea 5): cuándo una sesión abierta se cierra porque
    /// cambiaron los datos de acceso del usuario, qué rutas son públicas y qué peticiones son AJAX.
    /// </summary>
    public class SesionVigenteTests
    {
        private static readonly Guid Sello = Guid.NewGuid();

        private static EstadoSesionUsuario Bd(string rol = "ADMIN", int? idConductor = null, bool activo = true, Guid? sello = null) =>
            new EstadoSesionUsuario
            {
                Existe = true,
                SelloSesion = sello ?? Sello,
                Activo = activo,
                Rol = rol,
                IdConductor = idConductor
            };

        // ── Duración máxima de la sesión (E4-12) ──────────────────────────

        private static readonly DateTime InicioSesion = new DateTime(2026, 11, 22, 8, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void DuracionMaxima_EsDeDoceHoras()
        {
            Assert.Equal(TimeSpan.FromHours(12), SesionVigenteHelper.DuracionMaxima);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(11 * 60 + 59, false)]
        [InlineData(12 * 60, true)]          // a las 12 h justas se cierra
        [InlineData(30 * 60, true)]
        [InlineData(-60, false)]             // inicio en el futuro (reloj que volvió atrás): no se mide
        public void DuracionMaxima_CierraAlCumplirDoceHoras(int minutosDesdeInicio, bool esperado)
        {
            Assert.Equal(esperado, SesionVigenteHelper.SuperoDuracionMaxima(InicioSesion, InicioSesion.AddMinutes(minutosDesdeInicio)));
        }

        [Fact]
        public void DuracionMaxima_SinHoraDeInicio_SeCierra()
        {
            Assert.True(SesionVigenteHelper.SuperoDuracionMaxima(null, InicioSesion));
        }

        [Fact]
        public void DuracionMaxima_GlobalLaCompruebaAntesDelSelloYLoginMuestraSuMensaje()
        {
            var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "WebSGV.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            string global = System.IO.File.ReadAllText(System.IO.Path.Combine(dir.FullName, "WebSGV", "Global.asax.cs"));
            string login = System.IO.File.ReadAllText(System.IO.Path.Combine(dir.FullName, "WebSGV", "Views", "Login.aspx.cs"));

            int duracion = global.IndexOf("SesionVigenteHelper.SuperoDuracionMaxima(SesionHelper.InicioSesionUtc(sesion), DateTime.UtcNow)", StringComparison.Ordinal);
            int sello = global.IndexOf("SesionVigenteHelper.ObtenerEstado(", StringComparison.Ordinal);
            Assert.True(duracion > 0 && duracion < sello, "Global.asax debe cerrar la sesión vencida antes de evaluar el sello.");
            Assert.Contains("case SesionVigenteHelper.CodigoSesionVencida:", login);
            Assert.Contains("MostrarMensaje(SesionVigenteHelper.MensajeDuracionMaxima)", login);
        }

        [Fact]
        public void MismosDatos_SigueVigente()
        {
            Assert.Equal(ResultadoSesion.Vigente, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, Bd()));
        }

        [Fact]
        public void ConductorConMismoVinculo_SigueVigente()
        {
            Assert.Equal(ResultadoSesion.Vigente,
                SesionVigenteHelper.EvaluarSesion(Sello, "CONDUCTOR", 15, Bd("CONDUCTOR", 15)));
        }

        [Theory]
        [InlineData("admin")]
        [InlineData(" ADMIN ")]
        [InlineData("Admin")]
        public void RolConDistintoCasoOEspacios_SigueVigente(string rolSesion)
        {
            Assert.Equal(ResultadoSesion.Vigente, SesionVigenteHelper.EvaluarSesion(Sello, rolSesion, null, Bd(" ADMIN")));
        }

        [Fact]
        public void SesionSinSello_SeCierra()
        {
            // Sesiones abiertas antes de este control: no tienen sello y se cierran.
            Assert.Equal(ResultadoSesion.Cerrada, SesionVigenteHelper.EvaluarSesion(null, "ADMIN", null, Bd()));
        }

        [Fact]
        public void UsuarioInexistente_SeCierra()
        {
            Assert.Equal(ResultadoSesion.Cerrada,
                SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, EstadoSesionUsuario.Inexistente()));
            Assert.Equal(ResultadoSesion.Cerrada, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, null));
        }

        [Fact]
        public void UsuarioInactivo_SeCierra()
        {
            Assert.Equal(ResultadoSesion.Cerrada, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, Bd(activo: false)));
        }

        [Fact]
        public void SelloDistinto_SeCierra()
        {
            // Cambio de contraseña o restablecimiento: el sello se renueva.
            Assert.Equal(ResultadoSesion.Cerrada,
                SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, Bd(sello: Guid.NewGuid())));
        }

        [Fact]
        public void RolDistinto_SeCierra()
        {
            Assert.Equal(ResultadoSesion.Cerrada, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, Bd("CONTABILIDAD")));
        }

        [Fact]
        public void ConductorVinculadoDistinto_SeCierra()
        {
            Assert.Equal(ResultadoSesion.Cerrada,
                SesionVigenteHelper.EvaluarSesion(Sello, "CONDUCTOR", 15, Bd("CONDUCTOR", 16)));
            Assert.Equal(ResultadoSesion.Cerrada,
                SesionVigenteHelper.EvaluarSesion(Sello, "CONDUCTOR", 15, Bd("CONDUCTOR", null)));
            Assert.Equal(ResultadoSesion.Cerrada,
                SesionVigenteHelper.EvaluarSesion(Sello, "CONDUCTOR", null, Bd("CONDUCTOR", 15)));
        }

        [Fact]
        public void ConductorVinculadoDeOtroRol_NoCuenta()
        {
            // El login solo guarda IdConductor para el rol conductor: un admin con idConductor en la BD sigue vigente.
            Assert.Equal(ResultadoSesion.Vigente, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, Bd("ADMIN", 15)));
        }

        [Theory]
        [InlineData("Login.aspx")]
        [InlineData("login.aspx")]
        [InlineData("RecuperarContrasena.aspx")]
        [InlineData("RestablecerContrasena.aspx")]
        [InlineData("VerificarDocumento.aspx")]
        [InlineData("Error.aspx")]
        [InlineData("Error404.aspx")]
        [InlineData("")]
        [InlineData(null)]
        public void EsRutaPublica_PaginasSinSesion(string archivo)
        {
            Assert.True(SesionVigenteHelper.EsRutaPublica(archivo));
        }

        [Theory]
        [InlineData("Inicio.aspx")]
        [InlineData("FirmarLiquidacion.aspx")]
        [InlineData("DescargarPdfOrdenViaje.aspx")]
        [InlineData("GpsAutomatico.ashx")]
        public void EsRutaPublica_PaginasProtegidas_False(string archivo)
        {
            Assert.False(SesionVigenteHelper.EsRutaPublica(archivo));
        }

        [Theory]
        [InlineData("/Views/LiquidacionesPendientes.aspx", "/RegistrarFirmaConductor", true, true)]   // PageMethod
        [InlineData("/Views/Inicio.aspx", "", true, false)]                                          // página
        [InlineData("/Views/Inicio.aspx", "/", true, false)]
        [InlineData("/Views/Exportacion/GpsAutomatico.ashx", "", false, true)]                       // handler
        public void EsLlamadaAjax(string ruta, string pathInfo, bool handlerEsPagina, bool esperado)
        {
            Assert.Equal(esperado, SesionVigenteHelper.EsLlamadaAjax(ruta, pathInfo, handlerEsPagina));
        }

        [Fact]
        public void NombreMetodo_DelPathInfo()
        {
            Assert.Equal("MantenerSesionActiva", SesionVigenteHelper.NombreMetodo("/MantenerSesionActiva"));
            Assert.Equal("", SesionVigenteHelper.NombreMetodo(null));
        }

        // ── De dónde sale el estado: caché o BD ──────────────────────────────

        private sealed class Lector
        {
            public int Lecturas, Guardados;
            public EstadoSesionUsuario Bd;
            public EstadoSesionUsuario Leer() { Lecturas++; return Bd; }
            public void Guardar(EstadoSesionUsuario e) => Guardados++;
        }

        [Fact]
        public void ResolverEstado_SinCache_LeeDeBdYGuarda()
        {
            var l = new Lector { Bd = Bd() };
            Assert.Same(l.Bd, SesionVigenteHelper.ResolverEstado(null, Sello, l.Leer, l.Guardar));
            Assert.Equal(1, l.Lecturas);
            Assert.Equal(1, l.Guardados);
        }

        [Fact]
        public void ResolverEstado_CacheConMismoSello_NoVaALaBd()
        {
            var l = new Lector { Bd = Bd() };
            var cache = Bd();
            Assert.Same(cache, SesionVigenteHelper.ResolverEstado(cache, Sello, l.Leer, l.Guardar));
            Assert.Equal(0, l.Lecturas);
        }

        [Fact]
        public void ResolverEstado_CacheConSelloViejo_ReleeDeBdYNoCierraUnaSesionRecienSellada()
        {
            // La caché quedó con el sello anterior (carrera con el re-sellado): se relee y la sesión sigue vigente.
            var l = new Lector { Bd = Bd() };
            var cacheVieja = Bd(sello: Guid.NewGuid());
            var estado = SesionVigenteHelper.ResolverEstado(cacheVieja, Sello, l.Leer, l.Guardar);
            Assert.Same(l.Bd, estado);
            Assert.Equal(1, l.Lecturas);
            Assert.Equal(1, l.Guardados);
            Assert.Equal(ResultadoSesion.Vigente, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, estado));
        }

        [Fact]
        public void ResolverEstado_SelloRealmenteCambiado_SeReleeYSeCierra()
        {
            var nuevo = Guid.NewGuid();
            var l = new Lector { Bd = Bd(sello: nuevo) };
            var estado = SesionVigenteHelper.ResolverEstado(Bd(sello: nuevo), Sello, l.Leer, l.Guardar);
            Assert.Equal(1, l.Lecturas);
            Assert.Equal(ResultadoSesion.Cerrada, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, estado));
        }

        [Fact]
        public void ResolverEstado_SesionSinSello_UsaLaCache()
        {
            var l = new Lector { Bd = Bd() };
            var cache = Bd();
            Assert.Same(cache, SesionVigenteHelper.ResolverEstado(cache, null, l.Leer, l.Guardar));
            Assert.Equal(0, l.Lecturas);
        }

        [Fact]
        public void ResolverEstado_BdNoResponde_DevuelveNullSinGuardar()
        {
            var l = new Lector { Bd = null };
            Assert.Null(SesionVigenteHelper.ResolverEstado(Bd(sello: Guid.NewGuid()), Sello, l.Leer, l.Guardar));
            Assert.Equal(0, l.Guardados);
        }

        [Fact]
        public void ValoresDeSesion_SeLeenConTolerancia()
        {
            Assert.Equal(Sello, SesionVigenteHelper.SelloDeSesion(Sello));
            Assert.Equal(Sello, SesionVigenteHelper.SelloDeSesion(Sello.ToString()));
            Assert.Null(SesionVigenteHelper.SelloDeSesion(null));
            Assert.Equal(7, SesionVigenteHelper.EnteroDeSesion("7"));
            Assert.Equal(7, SesionVigenteHelper.EnteroDeSesion(7));
            Assert.Null(SesionVigenteHelper.EnteroDeSesion(null));
        }

        // ===== Login.aspx?error=sesion (E4-8): no cierra una sesión vigente, sin bucles =====

        private static readonly DateTime Ahora = new DateTime(2026, 11, 22, 15, 0, 0, DateTimeKind.Utc);
        private const string Inicio = "~/Views/Inicio.aspx";

        [Fact]
        public void LoginErrorSesion_SinSesion_MuestraElFormulario()
        {
            Assert.Equal(DecisionLoginErrorSesion.MostrarFormulario,
                SesionVigenteHelper.DecidirLoginErrorSesion(false, false, null, null, Ahora));
            Assert.Equal(DecisionLoginErrorSesion.MostrarFormulario,
                SesionVigenteHelper.DecidirLoginErrorSesion(false, true, Inicio, Ahora.AddSeconds(-1), Ahora));
        }

        [Fact]
        public void LoginErrorSesion_SesionVigente_VaASuInicioSinCerrarla()
        {
            Assert.Equal(DecisionLoginErrorSesion.IrAInicio,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, null, Ahora));
            // El rebote anterior fue hace más de la ventana: es otro enlace viejo, no un bucle.
            Assert.Equal(DecisionLoginErrorSesion.IrAInicio,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, Ahora - SesionVigenteHelper.VentanaReboteLogin, Ahora));
            Assert.Equal(DecisionLoginErrorSesion.IrAInicio,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, Ahora.AddMinutes(-10), Ahora));
        }

        [Theory]
        [InlineData(false, Inicio)]   // sin sello: sesión a medias (Global.asax la cierra si la BD responde)
        [InlineData(true, null)]      // rol sin página de inicio (RolesHelper.UrlInicioSegunRol)
        [InlineData(true, "")]
        public void LoginErrorSesion_SesionAMedias_SeCierra(bool tieneSello, string urlInicio)
        {
            Assert.Equal(DecisionLoginErrorSesion.LimpiarSesion,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, tieneSello, urlInicio, null, Ahora));
        }

        [Fact]
        public void LoginErrorSesion_ElInicioLaDevuelve_SeCierraYNoHayBucle()
        {
            // 1) Llega con sesión vigente: al inicio (Login guarda la hora del envío).
            var primera = SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, null, Ahora);
            Assert.Equal(DecisionLoginErrorSesion.IrAInicio, primera);
            // 2) El inicio la rechaza y la devuelve al instante: se cierra.
            Assert.Equal(DecisionLoginErrorSesion.LimpiarSesion,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, Ahora, Ahora.AddMilliseconds(300)));
            Assert.Equal(DecisionLoginErrorSesion.LimpiarSesion,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, Ahora, Ahora.AddSeconds(29)));
            // 3) Ya sin sesión: formulario, sin más redirecciones.
            Assert.Equal(DecisionLoginErrorSesion.MostrarFormulario,
                SesionVigenteHelper.DecidirLoginErrorSesion(false, false, null, null, Ahora.AddSeconds(1)));
            // Reloj que vuelve atrás: ante la duda, se cierra.
            Assert.Equal(DecisionLoginErrorSesion.LimpiarSesion,
                SesionVigenteHelper.DecidirLoginErrorSesion(true, true, Inicio, Ahora.AddSeconds(5), Ahora));
        }
    }
}
