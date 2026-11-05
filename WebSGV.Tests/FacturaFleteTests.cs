using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Facturas;
using WebSGV.Services.Facturas;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Facturas de flete: lectura de la representación impresa de SUNAT (las líneas que saca
    /// ExtractorTextoPdf, con datos inventados) y conciliación con lo cargado.
    /// </summary>
    public class FacturaFleteTests
    {
        private static IList<IList<string>> Factura(string numero = "E001-18645", string toneladas = "207.90", string tarifa = "62.00",
            string total = "12,889.80", string neto = "12,889.80", string observacion = "OS: 4301306711",
            string comercial = "COMERCIAL: F222-00008426 - C.P.I 0003892", string moneda = "DOLAR AMERICANO",
            string vence = "12/11/2026", string rucEmisor = "20111111111")
        {
            return new List<IList<string>>
            {
                new List<string>
                {
                    "FACTURA ELECTRONICA",
                    "TRANSPORTES DEMO E.I.R.L.",
                    "RUC: " + rucEmisor,
                    "JR. DEMO 123",
                    numero,
                    "LIMA - LIMA - LIMA",
                    "Fecha de Emisión : 02/10/2026 Forma de pago: Crédito",
                    "Señor(es) : CLIENTE DEMO S.A.",
                    "RUC : 20222222222",
                    "AV. DEMO 4793 URB.",
                    "Dirección del Cliente : CALLAO",
                    "Tipo de Moneda : " + moneda,
                    "Observación : " + observacion,
                    "Cantidad Unidad Medida Descripción Valor Unitario",
                    $"{toneladas} TONELADAS SERVICIO DE TRANSPORTE DE TRUJILLO A GUAYAQUIL CON {tarifa}",
                    "NICOVITA KATAL Y CLASSIC CAMARON, SEGUN FACTURA",
                    comercial,
                    "Valor de Venta de Operaciones Gratuitas : $0.00 Sub Total Ventas : $ " + total,
                    "Anticipos : $ 0.00",
                    "Descuentos : $ 0.00",
                    "Valor Venta : $ " + total,
                    "ISC : $ 0.00",
                    "SON: DOCE MIL OCHOCIENTOS OCHENTA Y NUEVE Y 80/100 DOLAR",
                    "IGV : $ 0.00",
                    "AMERICANO",
                    "Importe Total : $ " + total,
                    "Información del crédito :",
                    "Monto neto pendiente de pago : $ " + neto,
                    "Total de Cuotas : 1",
                    "Nº Cuota Fec. Venc. Monto Nº Cuota Fec. Venc. Monto Nº Cuota Fec. Venc. Monto",
                    $"1 {vence} {neto}",
                    "Esta es una representación impresa de la factura electrónica, generada en el Sistema de SUNAT. Puede"
                }
            };
        }

        // ── Lectura ──────────────────────────────────────────────────────────

        [Fact]
        public void Lee_los_datos_de_la_factura()
        {
            var f = LectorFacturaFlete.Leer(Factura());

            Assert.True(f.EsValida, string.Join(" ", f.Errores));
            Assert.Equal("E001-18645", f.NumeroFactura);
            Assert.Equal("20111111111", f.RucEmisor);
            Assert.Equal("20222222222", f.RucCliente);
            Assert.Equal("CLIENTE DEMO S.A.", f.NombreCliente);
            Assert.Equal(new DateTime(2026, 10, 2), f.FechaEmision);
            Assert.Equal("Crédito", f.FormaPago);
            Assert.Equal("USD", f.Moneda);
            Assert.Equal("4301306711", f.OrdenServicio);
            Assert.Equal(207.90m, f.Cantidad);
            Assert.Equal("TONELADAS", f.UnidadMedida);
            Assert.Equal(62.00m, f.ValorUnitario);
            Assert.Equal(12889.80m, f.ValorVenta);
            Assert.Equal(0m, f.Igv);
            Assert.Equal(12889.80m, f.ImporteTotal);
            Assert.Equal(12889.80m, f.MontoNetoPendiente);
            Assert.Equal(new DateTime(2026, 11, 12), f.FechaVencimiento);
            Assert.Equal("F222-00008426", f.NumeroFacturaComercial);
            Assert.Equal("0003892", f.NumeroCPIC);
            Assert.Contains("GUAYAQUIL", f.Descripcion);
            Assert.Empty(f.Observaciones);
        }

        [Fact]
        public void La_factura_comercial_partida_en_dos_lineas_y_la_observacion_con_TG_se_leen()
        {
            var lineas = Factura(observacion: "OS: 4301317188 - TG: 1292334", comercial: "FACTURA COMERCIAL: F222-8515 - C.P.I 3929");
            lineas[0][15] = "NICOVITA KATAL, TERAP Y CLASSIC CAMARON, SEGUN";
            var f = LectorFacturaFlete.Leer(lineas);

            Assert.Equal("4301317188", f.OrdenServicio);
            Assert.Equal("F222-00008515", f.NumeroFacturaComercial);
            Assert.Equal("0003929", f.NumeroCPIC);
        }

        [Fact]
        public void Avisa_si_el_monto_del_credito_no_es_el_total()
        {
            var f = LectorFacturaFlete.Leer(Factura(neto: "12,889.20"));

            Assert.True(f.EsValida);
            Assert.Equal(12889.20m, f.MontoNetoPendiente);
            Assert.Contains(f.Observaciones, o => o.Contains("monto pendiente del crédito") && o.Contains("0.60"));
        }

        [Fact]
        public void Avisa_si_cantidad_por_tarifa_no_da_el_valor_de_venta()
        {
            var f = LectorFacturaFlete.Leer(Factura(toneladas: "237.60"));
            Assert.Contains(f.Observaciones, o => o.Contains("valor de venta"));
        }

        [Fact]
        public void Soles_y_sin_factura_comercial()
        {
            var f = LectorFacturaFlete.Leer(Factura(moneda: "SOLES", comercial: "SEGUN GUIAS VARIAS"));
            Assert.Equal("PEN", f.Moneda);
            Assert.Null(f.NumeroFacturaComercial);
            Assert.Contains(f.Observaciones, o => o.Contains("no menciona la factura comercial"));
        }

        [Fact]
        public void Un_documento_que_no_es_factura_no_es_valido()
        {
            var f = LectorFacturaFlete.Leer(new List<IList<string>> { new List<string> { "GUÍA DE REMISIÓN", "T660-00016196" } });
            Assert.False(f.EsValida);
            Assert.Contains(f.Errores, e => e.Contains("factura electrónica"));
        }

        [Theory]
        [InlineData("F222-8426", "F222-00008426")]
        [InlineData("f222 - 00008426", "F222-00008426")]
        [InlineData("F222-00008426", "F222-00008426")]
        [InlineData("8426", null)]
        public void Normaliza_el_numero_de_la_factura_comercial(string entrada, string esperado) =>
            Assert.Equal(esperado, LectorFacturaFlete.NormalizarNumero(entrada));

        // ── Conciliación ─────────────────────────────────────────────────────

        private static readonly DateTime Desde = new DateTime(2026, 10, 1), Hasta = new DateTime(2026, 10, 31);
        private static int _id;

        private static PedidoCargado Pedido(string comercial, int unidades, decimal toneladas, int dia = 5, int sinCarga = 0, int idCliente = 1) =>
            new PedidoCargado
            {
                IdCliente = idCliente, Cliente = idCliente == 1 ? "VITAPRO" : "OTRO", NumeroPedido = "44000" + dia, FacturaComercial = comercial,
                FechaCarga = new DateTime(2026, 10, dia), Unidades = unidades, ToneladasNetas = toneladas, UnidadesSinCarga = sinCarga,
                ViajesSeguimiento = unidades
            };

        private static FacturaFleteRegistrada Flete(string comercial, decimal toneladas, int dia = 7, string unidad = "TONELADAS",
                                                    decimal? neto = null) =>
            new FacturaFleteRegistrada
            {
                IdFacturaFlete = ++_id, NumeroFactura = "E001-" + (18000 + _id), FechaEmision = new DateTime(2026, 10, dia),
                FechaVencimiento = new DateTime(2026, 10, dia).AddDays(41), IdCliente = 1, Cliente = "VITAPRO", Moneda = "USD",
                Cantidad = toneladas, UnidadMedida = unidad, ValorUnitario = 62m, ImporteTotal = Math.Round(toneladas * 62m, 2),
                MontoNetoPendiente = neto ?? Math.Round(toneladas * 62m, 2), FacturaComercial = comercial
            };

        private static Dictionary<int, FacturaFleteService.TarifaCliente> Tarifas(int facturas = 1) =>
            new Dictionary<int, FacturaFleteService.TarifaCliente>
            {
                [1] = new FacturaFleteService.TarifaCliente { FacturasRegistradas = facturas, ValorUnitario = 62m, UnidadMedida = "TONELADAS", Moneda = "USD" },
                [2] = new FacturaFleteService.TarifaCliente { FacturasRegistradas = 0 }
            };

        private static List<FilaConciliacion> Conciliar(IList<PedidoCargado> pedidos, IList<FacturaFleteRegistrada> facturas,
            Dictionary<string, DateTime?> cargas = null) =>
            FacturaFleteService.Conciliar(pedidos, facturas, Tarifas(), cargas ?? new Dictionary<string, DateTime?>(), Desde, Hasta, out _);

        [Fact]
        public void Facturado_igual_a_lo_cargado_es_conforme()
        {
            var filas = Conciliar(new[] { Pedido("F222-00000001", 7, 207.90m) }, new[] { Flete("F222-00000001", 207.90m) });

            var f = Assert.Single(filas);
            Assert.Equal(EstadoConciliacion.Conforme, f.Estado);
            Assert.Equal(0m, f.Diferencia);
            Assert.Equal(12889.80m, f.ImporteFacturado);
            Assert.Empty(f.Avisos);
        }

        [Fact]
        public void Una_unidad_facturada_que_falta_en_el_sistema_sale_como_diferencia()
        {
            var filas = Conciliar(new[] { Pedido("F222-00000002", 6, 178.20m) }, new[] { Flete("F222-00000002", 207.90m) });

            var f = Assert.Single(filas);
            Assert.Equal(EstadoConciliacion.Diferencia, f.Estado);
            Assert.Equal(29.70m, f.Diferencia);
            Assert.Contains("unidad(es) facturada(s) de más o que faltan en el sistema", f.Avisos[0]);
            Assert.Contains("207", f.Avisos[0]);
        }

        [Fact]
        public void La_diferencia_de_redondeo_se_tolera()
        {
            var filas = Conciliar(new[] { Pedido("F222-00000003", 1, 29.68m) }, new[] { Flete("F222-00000003", 29.70m) });
            Assert.Equal(EstadoConciliacion.Conforme, Assert.Single(filas).Estado);
        }

        [Fact]
        public void Por_viaje_compara_unidades()
        {
            var filas = Conciliar(new[] { Pedido("F222-00000004", 3, 90m) }, new[] { Flete("F222-00000004", 3m, unidad: "VIAJE") });
            var f = Assert.Single(filas);
            Assert.Equal(EstadoConciliacion.Conforme, f.Estado);
            Assert.Equal("unid.", f.Unidad);
        }

        [Fact]
        public void Cargado_sin_factura_de_flete_queda_por_facturar_con_estimado()
        {
            var f = Assert.Single(Conciliar(new[] { Pedido("F222-00000005", 8, 237.60m) }, new FacturaFleteRegistrada[0]));
            Assert.Equal(EstadoConciliacion.PorFacturar, f.Estado);
            Assert.Equal(14731.20m, f.ImporteEstimado);
            Assert.Equal("USD", f.Moneda);
        }

        [Fact]
        public void Carga_programada_sin_documentos_espera_el_zip()
        {
            var f = Assert.Single(Conciliar(new[] { Pedido(null, 7, 0m, sinCarga: 7) }, new FacturaFleteRegistrada[0]));
            Assert.Equal(EstadoConciliacion.SinDocumentos, f.Estado);
            Assert.Null(f.ImporteEstimado);
        }

        [Fact]
        public void Factura_cuyo_pedido_no_esta_en_el_sistema_sale_sin_carga()
        {
            var f = Assert.Single(Conciliar(new PedidoCargado[0], new[] { Flete("F222-00000006", 148.50m) },
                                            new Dictionary<string, DateTime?> { ["F222-00000006"] = null }));
            Assert.Equal(EstadoConciliacion.SinCarga, f.Estado);
            Assert.Contains("no está en el sistema", f.Avisos[0]);
        }

        [Fact]
        public void Factura_de_una_carga_de_otro_periodo_no_se_lista()
        {
            var filas = Conciliar(new PedidoCargado[0], new[] { Flete("F222-00000007", 148.50m) },
                                  new Dictionary<string, DateTime?> { ["F222-00000007"] = new DateTime(2026, 9, 28) });
            Assert.Empty(filas);
        }

        [Fact]
        public void Dos_facturas_para_el_mismo_pedido_se_suman_y_avisan()
        {
            var f = Assert.Single(Conciliar(new[] { Pedido("F222-00000008", 2, 59.40m) },
                                            new[] { Flete("F222-00000008", 29.70m), Flete("F222-00000008", 29.70m) }));
            Assert.Equal(EstadoConciliacion.Conforme, f.Estado);
            Assert.Equal(59.40m, f.CantidadFacturada);
            Assert.Contains(f.Avisos, a => a.Contains("2 facturas de flete"));
        }

        [Fact]
        public void Avisa_el_credito_distinto_del_total()
        {
            var f = Assert.Single(Conciliar(new[] { Pedido("F222-00000009", 7, 207.90m) },
                                            new[] { Flete("F222-00000009", 207.90m, neto: 12889.20m) }));
            Assert.Equal(EstadoConciliacion.Conforme, f.Estado);
            Assert.Contains(f.Avisos, a => a.Contains("monto pendiente del crédito"));
        }

        [Fact]
        public void Clientes_sin_facturas_registradas_no_salen_por_facturar()
        {
            var filas = FacturaFleteService.Conciliar(new[] { Pedido(null, 2, 50m, idCliente: 2) }, new FacturaFleteRegistrada[0],
                Tarifas(), new Dictionary<string, DateTime?>(), Desde, Hasta, out var sinFacturas);
            Assert.Empty(filas);
            Assert.Equal(2, sinFacturas["OTRO"]);
        }

        [Fact]
        public void El_excel_tiene_conciliacion_y_facturas()
        {
            var facturas = new[] { Flete("F222-00000010", 207.90m) };
            var filas = Conciliar(new[] { Pedido("F222-00000010", 6, 178.20m) }, facturas);
            var r = FacturaFleteService.Reporte(filas, facturas, Desde, Hasta, null, "prueba", new DateTime(2026, 11, 5));

            Assert.Single(r.Filas);
            Assert.Equal("Diferencia", r.Filas[0]["Estado"]);
            var hoja = Assert.Single(r.HojasAdicionales);
            Assert.Single(hoja.Filas);
            Assert.NotNull(Services.Reportes.ReporteTabularExportador.GenerarExcel(r, new Helpers.EmpresaInfo { RazonSocial = "DEMO" }));
        }
    }
}
