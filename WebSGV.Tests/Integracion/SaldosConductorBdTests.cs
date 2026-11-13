using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Cuenta corriente del conductor contra la BD de pruebas, dentro de una <see cref="TransaccionDePrueba"/>:
    /// registrar cómo se saldó una liquidación aprobada, que no se salde dos veces y anularlo.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class SaldosConductorBdTests
    {
        [FactBD]
        public void RegistrarYAnular_LaLiquidacionVuelveAPendienteYNoSeSaldaDosVeces()
        {
            using (new TransaccionDePrueba())
            {
                DataRow orden = DbHelper.ConsultarTabla(@"
                    SELECT TOP 1 ov.idOrdenViaje, ov.idConductor FROM OrdenViaje ov
                    WHERE ov.estadoAprobacion = 'APROBADO'
                      AND NOT EXISTS (SELECT 1 FROM PagoSaldoConductorDetalle d WHERE d.idOrdenViaje = ov.idOrdenViaje AND d.activo = 1)
                    ORDER BY ov.idOrdenViaje DESC").Rows[0];
                int idOrden = Convert.ToInt32(orden["idOrdenViaje"]), idConductor = Convert.ToInt32(orden["idConductor"]);

                DataRow antes = SaldosConductorService.Listar(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1), idConductor, false, false, new[] { idOrden }).Rows[0];
                Assert.Equal("Pendiente", antes["Estado"]);

                int idPago = SaldosConductorService.RegistrarPago(idConductor, new[] { idOrden }, new DateTime(2026, 11, 15), "PLANILLA", "Q1-NOV", "prueba", "PRUEBA");

                DataRow despues = SaldosConductorService.Listar(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1), idConductor, false, false, new[] { idOrden }).Rows[0];
                Assert.Equal(idPago, Convert.ToInt32(despues["idPagoSaldo"]));
                Assert.StartsWith("Saldado 15/11/2026", Convert.ToString(despues["Estado"]));
                Assert.Equal(Convert.ToDecimal(antes["SaldoSoles"]),
                    Convert.ToDecimal(DbHelper.EjecutarEscalar("SELECT montoSoles FROM PagoSaldoConductor WHERE idPagoSaldo = @p", DbHelper.Param("@p", idPago))));

                // Solo pendientes: ya no sale.
                var pendientes = SaldosConductorService.Listar(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1), idConductor, false, true);
                Assert.DoesNotContain(pendientes.Rows.Cast<DataRow>(), r => Convert.ToInt32(r["idOrdenViaje"]) == idOrden);

                SaldosConductorService.AnularPago(idPago, "registrado por error", "PRUEBA");
                DataRow anulada = SaldosConductorService.Listar(new DateTime(2000, 1, 1), new DateTime(2100, 1, 1), idConductor, false, false, new[] { idOrden }).Rows[0];
                Assert.Equal("Pendiente", anulada["Estado"]);

                // Al final (el error del SP aborta la transacción de la prueba): no se salda dos veces.
                SaldosConductorService.RegistrarPago(idConductor, new[] { idOrden }, new DateTime(2026, 11, 15), "EFECTIVO", null, null, "PRUEBA");
                var ex = Assert.ThrowsAny<Exception>(() =>
                    SaldosConductorService.RegistrarPago(idConductor, new[] { idOrden }, new DateTime(2026, 11, 15), "EFECTIVO", null, null, "PRUEBA"));
                Assert.Contains("ya tiene registrado", ex.Message);
            }
        }
    }
}
