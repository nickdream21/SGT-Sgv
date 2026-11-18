using System;
using System.Data;
using System.Linq;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// B6-7 (D4): el filtro "Estado de la liquidación" del reporte personalizado de ReportesOrdenesViaje
    /// sigue a <c>OrdenViaje.estadoAprobacion</c> en cada paso del flujo (antes filtraba por estadoViaje,
    /// que mezcla PENDIENTE y POR_CONFORMAR). Todo dentro de <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        [FactBD]
        public void ReportePersonalizado_FiltraPorElEstadoDeAprobacionDelFlujo()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);

                // Declarada y firmada por el conductor: PENDIENTE de revisión.
                AssertSoloEn(datos, idOrden, EstadosOrden.Pendiente);

                var envio = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);

                // Revisada por la administración: "Por conformar" la trae y "Pendiente" ya no (antes estadoViaje
                // seguía en PENDIENTE y la orden salía como pendiente).
                Assert.Equal("PENDIENTE", Fila("SELECT estadoViaje FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoViaje"]);
                AssertSoloEn(datos, idOrden, EstadosOrden.PorConformar);

                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huella, out string mensaje));
                AssertSoloEn(datos, idOrden, EstadosOrden.Aprobado);
            }
        }

        /// <summary>
        /// La orden aparece con "Todos" y con <paramref name="estado"/>, en ninguna otra opción, y la
        /// columna Estado trae el estado de aprobación (lo que muestra la exportación).
        /// </summary>
        private static void AssertSoloEn(DatosBase datos, int idOrden, string estado)
        {
            Assert.Equal(estado, Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
            foreach (string opcion in FiltroEstadoLiquidacion.Permitidos)
            {
                DataRow fila = Personalizado(datos, opcion).Rows.Cast<DataRow>()
                    .FirstOrDefault(r => Convert.ToString(r["NumeroLiquidacion"]) == NumeroDe(idOrden));
                bool debeEstar = opcion == FiltroEstadoLiquidacion.Todos || opcion == estado;
                Assert.True(debeEstar == (fila != null),
                    $"Orden {estado}: con el filtro {opcion} {(debeEstar ? "no aparece" : "aparece")}");
                if (fila != null)
                    Assert.Equal(estado, Convert.ToString(fila["Estado"]));
            }
        }

        private static DataTable Personalizado(DatosBase datos, string estado) =>
            ReportesOrdenesViajeService.ObtenerReportePersonalizado(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1),
                estado, datos.IdConductor, 0, "", "", "fecha_desc");

        private static string NumeroDe(int idOrden) =>
            Convert.ToString(Fila("SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["numeroOrdenViaje"]);
    }
}
