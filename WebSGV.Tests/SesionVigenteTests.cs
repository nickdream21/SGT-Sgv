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
            Assert.Equal(ResultadoSesion.Cerrada, SesionVigenteHelper.EvaluarSesion(Sello, "ADMIN", null, Bd("SUPERVISOR")));
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
    }
}
