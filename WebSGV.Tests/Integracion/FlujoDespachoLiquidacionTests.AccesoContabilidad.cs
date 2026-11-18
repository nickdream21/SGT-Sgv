using System;
using WebSGV.Helpers;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// B6-6 (H7, D3): CONTABILIDAD solo ve el detalle y el PDF de una liquidación APROBADO,
    /// aunque la pida por su id. La administración ve todas; el conductor, las suyas.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        [FactBD]
        public void AccesoPorId_ContabilidadSoloVeAprobado()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                const string Contabilidad = RolesHelper.ROL_CONTABILIDAD;
                const string Admin = RolesHelper.ROL_ADMIN;

                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));

                // PENDIENTE
                Assert.False(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, Contabilidad));
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, Admin));
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioConductor, RolesHelper.ROL_CONDUCTOR));
                Assert.False(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, RolesHelper.ROL_CONDUCTOR));

                // POR_CONFORMAR
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);
                var envio = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                Assert.Equal(EstadosOrden.PorConformar,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
                Assert.False(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, Contabilidad));
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, Admin));
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, RolesHelper.ROL_SUPERVISOR));

                // APROBADO
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);
                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huella, out string mensaje));
                Assert.Equal(EstadosOrden.Aprobado,
                    Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, Contabilidad), mensaje);
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, Admin));
                Assert.True(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioConductor, RolesHelper.ROL_CONDUCTOR));

                // Orden inexistente: CONTABILIDAD recibe lo mismo que con una no permitida.
                int inexistente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT ISNULL(MAX(idOrdenViaje), 0) + 1000 FROM OrdenViaje"));
                Assert.False(AccesoLiquidacion.PuedeConsultar(inexistente, datos.IdUsuarioAdmin, Contabilidad));
                // Otros roles sin acceso, aun con una orden aprobada.
                Assert.False(AccesoLiquidacion.PuedeConsultar(idOrden, datos.IdUsuarioAdmin, RolesHelper.ROL_ADMIN_SISTEMA));
            }
        }
    }
}
