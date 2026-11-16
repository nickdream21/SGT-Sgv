using System;
using Newtonsoft.Json.Linq;
using WebSGV.Services;
using WebSGV.Services.Conductor;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Edición de órdenes y separación de funciones (auditoría de seguridad, tarea 4): una
    /// liquidación aprobada cuyo saldo ya se pagó o descontó no se revierte hasta anular ese pago,
    /// y la revisión deja en la auditoría los montos de antes y de después.
    /// Todo dentro de <see cref="TransaccionDePrueba"/>.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        [FactBD]
        public void RevertirAprobada_ConPagoVigente_SeRechazaHastaAnularElPago()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                int idOrden = LiquidacionConductorService.EnviarLiquidacion(NuevaLiquidacion(datos, idViaje));
                FirmarComoConductor(idOrden, datos.IdUsuarioConductor);

                // Revisión: la instantánea de antes y después queda para la auditoría.
                var envio = LiquidacionesPendientesService.EnviarAConformidad(idOrden, AjustesDePrueba(), null, datos.IdUsuarioAdmin);
                Assert.True(envio.Exito, envio.Mensaje);
                JObject antes = JObject.Parse(envio.MontosAntes);
                JObject despues = JObject.Parse(envio.MontosDespues);
                Assert.Equal("PENDIENTE", (string)antes["estadoAprobacion"]);
                Assert.Equal("POR_CONFORMAR", (string)despues["estadoAprobacion"]);
                Assert.Equal(40m, (decimal)despues["ajustes"]["descuentoSoles"]);
                Assert.Equal("Alimentación del 18/07 sin comprobante", (string)despues["ajustes"]["motivoDescuento"]);

                string huella = HuellaLiquidacion.Calcular(LiquidacionesPendientesService.ObtenerDetalleLiquidacion(idOrden));
                FirmarRevision(idOrden, datos.IdUsuarioAdmin, huella);
                Assert.Equal(1, FirmarConformidad(idOrden, datos.IdUsuarioConductor, huella, out string mensaje));
                Assert.Equal("APROBADO", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);

                var ctxAdmin = new ContextoFirma { IdUsuario = datos.IdUsuarioAdmin, Ip = "127.0.0.1", UserAgent = "xunit" };
                int idPago = SaldosConductorService.RegistrarPago(datos.IdConductor, new[] { idOrden }, new DateTime(2026, 11, 15),
                    "PLANILLA", "Q1-NOV", "prueba", "PRUEBA");
                Assert.True(Convert.ToBoolean(LiquidacionesPendientesService.ObtenerAprobadas(datos.IdConductor, null, null,
                    Convert.ToString(Fila("SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["numeroOrdenViaje"]),
                    null, null).Rows[0]["Pagada"]));

                // Con el pago vigente no se revierte (el SP responde antes de abrir su transacción).
                var rechazo = FirmaService.DevolverARevision(idOrden, "REVERSION", ctxAdmin, "Admin de prueba", "Reversión en test con el pago vigente");
                Assert.False(rechazo.Exito);
                Assert.Contains("Anúlelo primero en Saldos de conductores", rechazo.Mensaje);
                Assert.Equal("APROBADO", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);

                // La instantánea del pago (lo que va a ValoresAnteriores al anularlo) trae la liquidación.
                JObject pagoAntes = JObject.Parse(SaldosConductorService.InstantaneaPago(idPago));
                Assert.False((bool)pagoAntes["anulado"]);
                Assert.Equal(idOrden, (int)pagoAntes["detalle"][0]["idOrdenViaje"]);

                SaldosConductorService.AnularPago(idPago, "registrado por error en test", "PRUEBA");
                Assert.True((bool)JObject.Parse(SaldosConductorService.InstantaneaPago(idPago))["anulado"]);

                var rev = FirmaService.DevolverARevision(idOrden, "REVERSION", ctxAdmin, "Admin de prueba", "Reversión en test tras anular el pago");
                Assert.True(rev.Exito, rev.Mensaje);
                Assert.Equal("PENDIENTE", Fila("SELECT estadoAprobacion FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["estadoAprobacion"]);
            }
        }
    }
}
