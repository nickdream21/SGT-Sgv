using System;
using System.IO;
using System.Web;
using System.Web.SessionState;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Una sola clave de sesión para el usuario (auditoría de seguridad, tarea 8D-2): Login guarda
    /// "UsuarioID" como entero y todo se lee con <see cref="SesionHelper"/>. Antes había además
    /// "IdUsuario" y RegistroDespacho leía "UsuarioID" (texto) con <c>as int?</c>: siempre null.
    /// </summary>
    public class SesionHelperTests
    {
        /// <summary>Petición falsa con sesión InProc vacía, instalada en HttpContext.Current.</summary>
        private sealed class ContextoFalso : IDisposable
        {
            public HttpSessionState Sesion { get; }

            public ContextoFalso()
            {
                var ctx = new HttpContext(
                    new HttpRequest("", "http://localhost/Views/Inicio.aspx", ""),
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
        public void IdUsuario_GuardadoComoEntero()
        {
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveUsuarioId] = 15;
                Assert.Equal(15, SesionHelper.IdUsuario(c.Sesion));
                Assert.Equal(15, SesionHelper.IdUsuarioActual);
                Assert.True(SesionHelper.HaySesion(c.Sesion));
            }
        }

        [Fact]
        public void IdUsuario_GuardadoComoTexto_SesionAnterior()
        {
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveUsuarioId] = "15";
                Assert.Equal(15, SesionHelper.IdUsuario(c.Sesion));
                Assert.Equal(15, SesionHelper.ObtenerUsuarioId(c.Sesion));
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("abc")]
        public void IdUsuario_AusenteOBasura_EsNullYNoHaySesion(string valor)
        {
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveUsuarioId] = valor;
                Assert.Null(SesionHelper.IdUsuario(c.Sesion));
                Assert.False(SesionHelper.HaySesion(c.Sesion));
                Assert.False(SesionHelper.HaySesionActual);
                Assert.Equal(0, SesionHelper.ObtenerUsuarioId(c.Sesion));
            }
        }

        [Fact]
        public void SinSesion_NoLanza()
        {
            HttpContext.Current = null;
            Assert.Null(SesionHelper.IdUsuario(null));
            Assert.Equal("", SesionHelper.Rol(null));
            Assert.Equal("", SesionHelper.Nombre(null));
            Assert.False(SesionHelper.HaySesion(null));
            Assert.Null(SesionHelper.IdUsuarioActual);
            Assert.Equal("", SesionHelper.RolActual);
            Assert.Equal(0, SecurityHelper.ObtenerIdUsuario());
            Assert.False(SecurityHelper.TieneSesionActiva());
        }

        [Theory]
        [InlineData(" admin ", "ADMIN")]
        [InlineData("Conductor", "CONDUCTOR")]
        [InlineData("ADMINISTRADOR DE GRIFO", "ADMINISTRADOR DE GRIFO")]
        [InlineData(null, "")]
        public void Rol_SinEspaciosYEnMayusculas(string guardado, string esperado)
        {
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveRol] = guardado;
                Assert.Equal(esperado, SesionHelper.Rol(c.Sesion));
                Assert.Equal(esperado, SesionHelper.RolActual);
                Assert.Equal(esperado, SecurityHelper.ObtenerRol());
                Assert.Equal(esperado, RolesHelper.ObtenerRolActual());
            }
        }

        [Fact]
        public void Iniciar_GuardaIdEnteroRolYNombre()
        {
            using (var c = new ContextoFalso())
            {
                SesionHelper.Iniciar(c.Sesion, 42, "SUPERVISOR", "Ana Pérez");

                Assert.IsType<int>(c.Sesion[SesionHelper.ClaveUsuarioId]);
                Assert.Equal(42, (int)c.Sesion[SesionHelper.ClaveUsuarioId]);
                Assert.Equal("SUPERVISOR", c.Sesion[SesionHelper.ClaveRol]);
                Assert.Equal("Ana Pérez", c.Sesion[SesionHelper.ClaveNombre]);
                Assert.Equal("Ana Pérez", SesionHelper.NombreActual);
                Assert.Null(c.Sesion["IdUsuario"]); // la clave duplicada ya no existe
            }
        }

        // E4-12: la hora de inicio (duración máxima de 12 h) y una sesión nueva sin reautenticación previa.
        [Fact]
        public void Iniciar_GuardaLaHoraDeInicioYBorraLaReautenticacionAnterior()
        {
            using (var c = new ContextoFalso())
            {
                SesionHelper.MarcarReautenticacionAdmin(c.Sesion, DateTime.UtcNow);
                DateTime antes = DateTime.UtcNow;
                SesionHelper.Iniciar(c.Sesion, 7, "ADMINISTRADOR DE SISTEMA", "Admin");
                DateTime despues = DateTime.UtcNow;

                DateTime? inicio = SesionHelper.InicioSesionUtc(c.Sesion);
                Assert.True(inicio.HasValue && inicio.Value >= antes && inicio.Value <= despues);
                Assert.Null(SesionHelper.ReautenticacionAdminUtc(c.Sesion));

                DateTime marca = new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);
                SesionHelper.MarcarReautenticacionAdmin(c.Sesion, marca);
                Assert.Equal(marca, SesionHelper.ReautenticacionAdminUtc(c.Sesion));
            }
        }

        [Fact]
        public void InicioYReautenticacion_SinSesionOConOtroTipo_SonNull()
        {
            Assert.Null(SesionHelper.InicioSesionUtc(null));
            Assert.Null(SesionHelper.ReautenticacionAdminUtc(null));
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveInicioSesionUtc] = "2026-11-22";
                Assert.Null(SesionHelper.InicioSesionUtc(c.Sesion));
            }
        }

        [Fact]
        public void SecurityHelper_LeeElIdDeUsuarioID()
        {
            using (var c = new ContextoFalso())
            {
                SesionHelper.Iniciar(c.Sesion, 7, "ADMIN", "Admin");

                Assert.Equal(7, SecurityHelper.ObtenerIdUsuario());
                Assert.True(SecurityHelper.TieneSesionActiva());
                Assert.True(RolesHelper.TieneSesionActiva());
                Assert.True(SecurityHelper.EsAdmin());
            }
        }

        // E4-2: el id del conductor también pasa solo por SesionHelper.
        [Theory]
        [InlineData("CONDUCTOR", 15, 15)]
        [InlineData(" chofer ", 15, 15)]
        [InlineData("CONDUCTOR", null, null)]
        [InlineData("ADMIN", 15, null)]       // otro rol nunca deja id de conductor
        public void Iniciar_IdConductorSoloSiElRolEsConductor(string rol, int? idConductor, int? esperado)
        {
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveIdConductor] = 99; // valor de una sesión anterior
                SesionHelper.Iniciar(c.Sesion, 7, rol, "Usuario", idConductor);

                Assert.Equal(esperado, SesionHelper.IdConductor(c.Sesion));
                Assert.Equal(esperado, SesionHelper.IdConductorActual);
            }
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(15, 15)]
        [InlineData("15", 15)]   // sesión antigua guardada como texto
        [InlineData("x", null)]
        public void IdConductor_ToleraNuloYOtrosTipos(object guardado, int? esperado)
        {
            using (var c = new ContextoFalso())
            {
                c.Sesion[SesionHelper.ClaveIdConductor] = guardado;
                Assert.Equal(esperado, SesionHelper.IdConductor(c.Sesion));
            }
            Assert.Null(SesionHelper.IdConductor(null));
        }
    }
}
