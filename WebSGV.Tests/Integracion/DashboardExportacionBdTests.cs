using System;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// B6-1: el Dashboard de exportación no cuenta los viajes CANCELADO (despacho de carga anulado) en los viajes
    /// programados, los "sin llegada", el tránsito ni los pedidos, ni en el período anterior. Dentro de una
    /// <see cref="TransaccionDePrueba"/>; los pedidos son inventados y el mes (marzo de 2019) es anterior a los datos.
    /// sp_SE_CambiarEstado no saca de CANCELADO a un viaje (cerrarlo lo haría contar de nuevo).
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class DashboardExportacionBdTests
    {
        public DashboardExportacionBdTests() => BaseDePruebas.Preparar();

        private const int Anio = 2019, Mes = 3;
        private const string PedidoEnCurso = "9900000611", PedidoCancelado = "9900000612", PedidoCanceladoAnterior = "9900000613";

        /// <summary>Viaje de Vitapro con llegada a Trujillo pendiente y el tránsito al cliente completo (salida de planta → planta Ecuador).</summary>
        private static void Insertar(string pedido, DateTime programacion, string estado) =>
            DbHelper.EjecutarNonQuery(@"
                INSERT INTO SeguimientoExportacion (cliente, fhProgramacion, estado, activo, origen, bodegaDescarga,
                                                    fhSalidaPlanta, fhLlegadaPlantaEcuador)
                VALUES (@pedido, @prog, @estado, 1, 'MANUAL', 'JAVE', @salida, @llegada)",
                DbHelper.Param("@pedido", pedido), DbHelper.Param("@prog", programacion), DbHelper.Param("@estado", estado),
                DbHelper.Param("@salida", programacion.AddHours(6)), DbHelper.Param("@llegada", programacion.AddHours(60)));

        [FactBD]
        public void CargarPeriodo_NoCuentaLosViajesCancelados_YSiLosEnCurso()
        {
            using (new TransaccionDePrueba())
            {
                var antes = DashboardExportacionService.CargarPeriodo(Anio, Mes);
                var antesAnio = DashboardExportacionService.CargarPeriodo(Anio, 0);

                Insertar(PedidoEnCurso, new DateTime(Anio, Mes, 10, 8, 0, 0), "EN_CURSO");
                Insertar(PedidoCancelado, new DateTime(Anio, Mes, 12, 8, 0, 0), "CANCELADO");
                Insertar(PedidoCanceladoAnterior, new DateTime(Anio, Mes - 1, 12, 8, 0, 0), "CANCELADO");

                var despues = DashboardExportacionService.CargarPeriodo(Anio, Mes);
                Assert.Equal(antes.Kpi.Viajes + 1, despues.Kpi.Viajes);
                Assert.Equal(antes.Kpi.Pedidos + 1, despues.Kpi.Pedidos);
                Assert.Equal(antes.Kpi.SinLlegada + 1, despues.Kpi.SinLlegada);
                Assert.Equal(antes.Kpi.Transito.N + 1, despues.Kpi.Transito.N);
                Assert.Equal(antes.Kpi.ViajesAnterior, despues.Kpi.ViajesAnterior);
                Assert.Equal(antes.Serie.Single(p => p.Clave == 12).Viajes, despues.Serie.Single(p => p.Clave == 12).Viajes);
                Assert.Equal(antes.Serie.Single(p => p.Clave == 10).Viajes + 1, despues.Serie.Single(p => p.Clave == 10).Viajes);
                Assert.Contains(despues.Pedidos, p => p.Pedido == PedidoEnCurso);
                Assert.DoesNotContain(despues.Pedidos, p => p.Pedido == PedidoCancelado);
                Assert.Equal(antes.Plantas.Where(p => p.Planta == "JAVE").Sum(p => p.Viajes) + 1,
                             despues.Plantas.Where(p => p.Planta == "JAVE").Sum(p => p.Viajes));

                // Mirando el año entero, igual: solo el EN_CURSO.
                var despuesAnio = DashboardExportacionService.CargarPeriodo(Anio, 0);
                Assert.Equal(antesAnio.Kpi.Viajes + 1, despuesAnio.Kpi.Viajes);
                Assert.DoesNotContain(despuesAnio.Pedidos, p => p.Pedido == PedidoCancelado || p.Pedido == PedidoCanceladoAnterior);

                // El mes siguiente lo compara con marzo: el anterior tampoco cuenta el cancelado.
                var abril = DashboardExportacionService.CargarPeriodo(Anio, Mes + 1);
                Assert.Equal((antes.Kpi.Viajes + 1), abril.Kpi.ViajesAnterior ?? 0);
            }
        }

        [FactBD]
        public void CambiarEstado_UnViajeCanceladoNoSeCierraNiSeReabre()
        {
            using (new TransaccionDePrueba())
            {
                Insertar(PedidoEnCurso, new DateTime(Anio, Mes, 10, 8, 0, 0), "EN_CURSO");
                Insertar(PedidoCancelado, new DateTime(Anio, Mes, 12, 8, 0, 0), "CANCELADO");
                int enCurso = Id(PedidoEnCurso), cancelado = Id(PedidoCancelado);
                var antes = DashboardExportacionService.CargarPeriodo(Anio, Mes);

                // Un viaje en curso se cierra y se reabre como siempre; anular uno ya anulado no falla.
                SeguimientoViajesService.CambiarEstado(enCurso, "FINALIZADO", null);
                Assert.Equal("FINALIZADO", Estado(enCurso));
                SeguimientoViajesService.CambiarEstado(enCurso, "EN_CURSO", null);
                Assert.Equal("EN_CURSO", Estado(enCurso));
                SeguimientoViajesService.CambiarEstado(cancelado, "CANCELADO", null);

                // Errores al final: el anulado no pasa a FINALIZADO ni a EN_CURSO y sigue sin contar en el panel.
                foreach (var nuevo in new[] { "FINALIZADO", "EN_CURSO" })
                {
                    var ex = Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CambiarEstado(cancelado, nuevo, null));
                    Assert.Equal(SeguimientoViajesService.MensajeViajeAnulado, ex.Message);
                }
                Assert.Equal("CANCELADO", Estado(cancelado));
                Assert.Equal(antes.Kpi.Viajes, DashboardExportacionService.CargarPeriodo(Anio, Mes).Kpi.Viajes);
            }
        }

        private static int Id(string pedido) => Convert.ToInt32(DbHelper.EjecutarEscalar(
            "SELECT idSeguimiento FROM SeguimientoExportacion WHERE cliente = @p", DbHelper.Param("@p", pedido)));

        private static string Estado(int id) => Convert.ToString(DbHelper.EjecutarEscalar(
            "SELECT estado FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", id)));
    }
}
