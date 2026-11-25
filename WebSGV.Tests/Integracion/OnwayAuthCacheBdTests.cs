using System;
using WebSGV.Services.GpsIntegracion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// <see cref="OnwayAuthCacheService"/> contra la BD de pruebas (E6-8), dentro de una
    /// <see cref="TransaccionDePrueba"/>: el token real que haya en la fila no se toca ni se muestra.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class OnwayAuthCacheBdTests
    {
        public OnwayAuthCacheBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void GuardarToken_BorraLaSesion_YGuardarSesionLaCompleta()
        {
            using (new TransaccionDePrueba())
            {
                var expira = new DateTime(2026, 11, 21, 15, 30, 0, DateTimeKind.Utc);
                OnwayAuthCacheService.GuardarToken("token-de-prueba-bd", expira);

                var tras = OnwayAuthCacheService.Obtener();
                Assert.Equal("token-de-prueba-bd", tras.AccessToken);
                Assert.Equal(expira, tras.TokenExpiraEn);
                Assert.Equal(DateTimeKind.Utc, tras.TokenExpiraEn.Kind);
                Assert.Null(tras.OnwayClientId);
                Assert.Null(tras.OnwayUserId);

                OnwayAuthCacheService.GuardarSesion("cliente-de-prueba", "usuario-de-prueba");
                var conSesion = OnwayAuthCacheService.Obtener();
                Assert.Equal("cliente-de-prueba", conSesion.OnwayClientId);
                Assert.Equal("usuario-de-prueba", conSesion.OnwayUserId);
                Assert.Equal("token-de-prueba-bd", conSesion.AccessToken);
            }
        }

        [FactBD]
        public void ConBloqueoDeRenovacion_DevuelveLoDelTrabajo_YLiberaElBloqueo()
        {
            Assert.Equal(7, OnwayAuthCacheService.ConBloqueoDeRenovacion(() => 7, esperaMaximaMs: 5000));
            // Liberado: se puede volver a tomar en el acto.
            Assert.Equal("otra vez", OnwayAuthCacheService.ConBloqueoDeRenovacion(() => "otra vez", esperaMaximaMs: 1000));
        }

        [FactBD]
        public void ConBloqueoDeRenovacion_SiElTrabajoFalla_LiberaIgual()
        {
            Assert.Throws<InvalidOperationException>(() =>
                OnwayAuthCacheService.ConBloqueoDeRenovacion<int>(() => throw new InvalidOperationException("de prueba"), esperaMaximaMs: 1000));
            Assert.Equal(1, OnwayAuthCacheService.ConBloqueoDeRenovacion(() => 1, esperaMaximaMs: 1000));
        }

        // El cliente con el almacén de la BD no se prueba dentro de una TransaccionDePrueba: la renovación
        // abre la conexión del bloqueo y, con ella abierta, otra para leer y guardar el token; dos
        // conexiones a la vez en un TransactionScope lo promueven a transacción distribuida.
    }
}
