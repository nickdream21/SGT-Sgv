using System;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;
using WebSGV.Services.Common;
using WebSGV.Services.Despachos;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Envío único en el servidor (E5-3) con las escrituras reales: el flujo de cada manejador es
    /// "Consumir el token del formulario → escribir", con los mismos servicios que llaman las páginas
    /// (<see cref="MovimientosViajeService.Registrar"/> en MovimientosViaje, <see cref="RegistroDespachoService.CrearNuevoViajeProgreso"/>
    /// en RegistroDespacho). Dos POST con el mismo token dejan una sola fila. Que las páginas llamen a Consumir antes de
    /// escribir lo fija <c>EnvioUnicoHelperTests.Pagina_ConsumeElTokenAntesDeEscribir</c> (el manejador vive en el
    /// code-behind). Todo dentro de <see cref="TransaccionDePrueba"/>; el caso de sesión vencida va al final.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class EnvioUnicoBdTests
    {
        public EnvioUnicoBdTests() => BaseDePruebas.Preparar();

        private static int ConductorExistente() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor"));

        private static int UsuarioExistente() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idUsuario FROM Usuarios ORDER BY idUsuario"));

        /// <summary>Lo que hace el manejador de la página: sin token válido no escribe.</summary>
        private static bool EnviarFormulario(EnvioUnicoHelperTests.SesionFalsa sesion, string clave, string token, Action escribir)
        {
            if (!EnvioUnicoHelper.Consumir(sesion, clave, token)) return false;
            escribir();
            return true;
        }

        [FactBD]
        public void MovimientoDelViaje_DosEnviosConElMismoToken_DejanUnaFila()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = ConductorExistente(), idUsuario = UsuarioExistente();
                string marca = "Prueba E5-3 " + Guid.NewGuid().ToString("N");
                var sesion = EnvioUnicoHelperTests.SesionFalsa.ConUsuario(idUsuario);
                string clave = WebSGV.Views.MovimientosViaje.ClaveEnvioUnico;
                string token = EnvioUnicoHelper.Emitir(sesion, clave);

                Action registrar = () =>
                {
                    var r = MovimientosViajeService.Registrar(new MovimientoViaje
                    {
                        Tipo = MovimientosViajeService.Entrega, IdConductor = idConductor, Fecha = FechaHelper.Ahora().AddMinutes(-30),
                        Concepto = "ADELANTO", Medio = "EFECTIVO", MontoSoles = 1m, Descripcion = marca
                    }, idUsuario);
                    Assert.True(r.Exito, r.Mensaje);
                };

                Assert.True(EnviarFormulario(sesion, clave, token, registrar));
                Assert.False(EnviarFormulario(sesion, clave, token, registrar));   // pestaña duplicada / F5

                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM MovimientoDineroViaje WHERE descripcion = @d", DbHelper.Param("@d", marca))));

                // Con el token nuevo que lleva la respuesta, el siguiente registro legítimo sí entra.
                Assert.True(EnviarFormulario(sesion, clave, EnvioUnicoHelper.Emitir(sesion, clave), registrar));
                Assert.Equal(2, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM MovimientoDineroViaje WHERE descripcion = @d", DbHelper.Param("@d", marca))));
            }
        }

        [FactBD]
        public void CrearNuevoViaje_DosEnviosConElMismoToken_DejanUnViaje_YSinSesionFalla()
        {
            using (new TransaccionDePrueba())
            {
                int idConductor = ConductorExistente();
                string marca = "Prueba E5-3 " + Guid.NewGuid().ToString("N");
                var sesion = EnvioUnicoHelperTests.SesionFalsa.ConUsuario(UsuarioExistente());
                string clave = WebSGV.Views.RegistroDespacho.ClaveEnvioUnicoViaje;
                string token = EnvioUnicoHelper.Emitir(sesion, clave);
                Action crear = () => RegistroDespachoService.CrearNuevoViajeProgreso(idConductor, marca, "PRUEBA");

                Assert.True(EnviarFormulario(sesion, clave, token, crear));
                Assert.False(EnviarFormulario(sesion, clave, token, crear));

                Func<int> viajes = () => Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM ViajesEnProgreso WHERE descripcionViaje = @d", DbHelper.Param("@d", marca)));
                Assert.Equal(1, viajes());

                // Al final (caso de error): con la sesión vencida falla con el mensaje de sesión y no escribe.
                var vencida = new EnvioUnicoHelperTests.SesionFalsa();
                var e = Assert.Throws<ErrorNegocioException>(() => EnviarFormulario(vencida, clave, token, crear));
                Assert.Equal(EnvioUnicoHelper.MensajeSinSesion, e.Message);
                Assert.Equal(1, viajes());
            }
        }
    }
}
