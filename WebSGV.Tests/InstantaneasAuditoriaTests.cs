using System;
using System.Data;
using Newtonsoft.Json.Linq;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// JSON que va a AuditoriaLog (ValoresAnteriores / ValoresNuevos) al revisar, revertir o saldar una
    /// liquidación: que conserve los montos, los motivos, las entregas unidas y el saldo.
    /// </summary>
    public class InstantaneasAuditoriaTests
    {
        private static DataRow Fila(params (string columna, Type tipo, object valor)[] campos)
        {
            var t = new DataTable();
            foreach (var c in campos) t.Columns.Add(c.columna, c.tipo);
            DataRow r = t.NewRow();
            foreach (var c in campos) r[c.columna] = c.valor ?? DBNull.Value;
            t.Rows.Add(r);
            return r;
        }

        private static DataRow Detalle(bool conAjuste) => Fila(
            ("numeroOrdenViaje", typeof(string), "OV-000123"),
            ("estadoAprobacion", typeof(string), "APROBADO"),
            ("ajusteActivo", typeof(bool), conAjuste ? (object)true : null),
            ("descuentoSoles", typeof(decimal), conAjuste ? (object)15.5m : null),
            ("descuentoDolares", typeof(decimal), conAjuste ? (object)0m : null),
            ("observacionesDescuento", typeof(string), conAjuste ? "Peaje sin boleta" : null),
            ("reintegroSoles", typeof(decimal), conAjuste ? (object)0m : null),
            ("reintegroDolares", typeof(decimal), conAjuste ? (object)0m : null),
            ("observacionesReintegro", typeof(string), null),
            ("despachoSoles", typeof(decimal), 500m),
            ("despachoDolares", typeof(decimal), 20m),
            ("descDespacho", typeof(string), "Adelanto en base"),
            ("mensualidadSoles", typeof(decimal), 0m),
            ("mensualidadDolares", typeof(decimal), 0m),
            ("descMensualidad", typeof(string), null),
            ("prestamoSoles", typeof(decimal), 0m),
            ("prestamosDolares", typeof(decimal), 0m),
            ("descPrestamo", typeof(string), null),
            ("otrosSoles", typeof(decimal), 0m),
            ("otrosDolares", typeof(decimal), 0m),
            ("descOtrosAutorizados", typeof(string), null));

        private static DataRow Resumen(int pagada) => Fila(
            ("IngresosSoles", typeof(decimal), 500m), ("IngresosAdSoles", typeof(decimal), 0m),
            ("GastosSoles", typeof(decimal), 420m), ("GastosAdSoles", typeof(decimal), 10m),
            ("DescuentoSoles", typeof(decimal), 15.5m), ("ReintegroSoles", typeof(decimal), 0m),
            ("IngresosDolares", typeof(decimal), 20m), ("IngresosAdDolares", typeof(decimal), 0m),
            ("GastosDolares", typeof(decimal), 25m), ("GastosAdDolares", typeof(decimal), 0m),
            ("DescuentoDolares", typeof(decimal), 0m), ("ReintegroDolares", typeof(decimal), 0m),
            ("Pagada", typeof(int), pagada));

        [Fact]
        public void InstantaneaMontos_ConservaAjustesMotivosEntregasYSaldo()
        {
            JObject j = JObject.Parse(LiquidacionesPendientesService.ArmarInstantaneaMontos(Detalle(true), Resumen(1), new[] { 7, 9 }));

            Assert.Equal("OV-000123", (string)j["numeroOrdenViaje"]);
            Assert.Equal("APROBADO", (string)j["estadoAprobacion"]);
            Assert.True((bool)j["ajustes"]["activo"]);
            Assert.Equal(15.5m, (decimal)j["ajustes"]["descuentoSoles"]);
            Assert.Equal("Peaje sin boleta", (string)j["ajustes"]["motivoDescuento"]);
            Assert.Equal(JTokenType.Null, j["ajustes"]["motivoReintegro"].Type);
            Assert.Equal(500m, (decimal)j["entregado"]["despachoSoles"]);
            Assert.Equal(20m, (decimal)j["entregado"]["despachoDolares"]);
            Assert.Equal("Adelanto en base", (string)j["entregado"]["descDespacho"]);
            Assert.Equal(new[] { 7, 9 }, j["idsMovimientos"].ToObject<int[]>());
            // Saldo = entregado − gastos + descuento − reintegro (LiquidacionCalculos.SaldoFinal).
            Assert.Equal(500m - 430m + 15.5m, (decimal)j["saldo"]["soles"]);
            Assert.Equal(20m - 25m, (decimal)j["saldo"]["dolares"]);
            Assert.True((bool)j["saldo"]["pagada"]);
        }

        [Fact]
        public void InstantaneaMontos_SinAjusteNiResumen_DejaNulos()
        {
            JObject j = JObject.Parse(LiquidacionesPendientesService.ArmarInstantaneaMontos(Detalle(false), null, null));

            Assert.Equal(JTokenType.Null, j["ajustes"].Type);
            Assert.Equal(JTokenType.Null, j["saldo"].Type);
            Assert.Empty(j["idsMovimientos"]);
        }

        [Fact]
        public void InstantaneaPago_ConservaElPagoYSusLiquidaciones()
        {
            DataRow pago = Fila(
                ("idPagoSaldo", typeof(int), 41), ("idConductor", typeof(int), 3),
                ("fechaPago", typeof(DateTime), new DateTime(2026, 11, 15)), ("medio", typeof(string), "PLANILLA"),
                ("numeroOperacion", typeof(string), "Q1-NOV"), ("observacion", typeof(string), null),
                ("montoSoles", typeof(decimal), 85.5m), ("montoDolares", typeof(decimal), -5m),
                ("usuarioRegistro", typeof(string), "admin"), ("anulado", typeof(bool), false),
                ("motivoAnulacion", typeof(string), null), ("usuarioAnulacion", typeof(string), null));
            var detalle = new DataTable();
            detalle.Columns.Add("idOrdenViaje", typeof(int));
            detalle.Columns.Add("numeroOrdenViaje", typeof(string));
            detalle.Columns.Add("saldoSoles", typeof(decimal));
            detalle.Columns.Add("saldoDolares", typeof(decimal));
            detalle.Columns.Add("activo", typeof(bool));
            detalle.Rows.Add(123, "OV-000123", 85.5m, -5m, true);

            JObject j = JObject.Parse(SaldosConductorService.ArmarInstantaneaPago(pago, detalle));

            Assert.Equal(41, (int)j["idPagoSaldo"]);
            Assert.Equal("2026-11-15", (string)j["fechaPago"]);
            Assert.Equal("PLANILLA", (string)j["medio"]);
            Assert.Equal(85.5m, (decimal)j["montoSoles"]);
            Assert.False((bool)j["anulado"]);
            Assert.Single(j["detalle"]);
            Assert.Equal("OV-000123", (string)j["detalle"][0]["numeroOrdenViaje"]);
            Assert.Equal(-5m, (decimal)j["detalle"][0]["saldoDolares"]);
            Assert.True((bool)j["detalle"][0]["activo"]);
        }
    }
}
