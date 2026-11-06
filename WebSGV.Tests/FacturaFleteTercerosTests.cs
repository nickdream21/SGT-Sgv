using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Facturas;
using WebSGV.Services.Facturas;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Facturas de flete de clientes terceros (octubre de 2026): varias cargas en una factura (Novopan del Ecuador),
    /// el mismo pedido cobrado por tramos a dos clientes (Novopan del Ecuador y Novopan Perú), la factura comercial
    /// de Cartavio partida en dos líneas (Crisar) y las nacionales que citan la guía del remitente (Polinplast).
    /// Las líneas imitan lo que <c>ExtractorTextoPdf</c> saca del PDF de SUNAT.
    /// </summary>
    public class FacturaFleteTercerosTests
    {
        private static IList<IList<string>> Factura(string cliente, string ruc, string moneda, IEnumerable<string> detalle,
                                                    string total, string igv = "0.00")
        {
            var l = new List<string>
            {
                "FACTURA ELECTRONICA", "TRANSPORTES DEMO E.I.R.L.", "RUC: 20111111111", "E001-18700", "LIMA - LIMA - LIMA",
                "Fecha de Emisión : 24/10/2026 Forma de pago: Crédito",
                "Señor(es) : " + cliente,
                "Tipo de Moneda : " + moneda,
                "RUC: " + ruc + " - DIR: VIA LA",
                "Observación :",
                "Cantidad Unidad Medida Descripción Valor Unitario"
            };
            l.AddRange(detalle);
            l.AddRange(new[]
            {
                "Valor de Venta de Operaciones Gratuitas : $0.00 Sub Total Ventas : $ " + total,
                "Valor Venta : $ " + total,
                "IGV : $ " + igv,
                "Importe Total : $ " + total
            });
            return new List<IList<string>> { l };
        }

        [Fact]
        public void Novopan_del_Ecuador_cobra_varias_cargas_en_una_factura()
        {
            var f = LectorFacturaFlete.Leer(Factura("NOVOPAN DEMO DEL ECUADOR S.A", "1790000000001", "DOLAR AMERICANO", new[]
            {
                "2.00 UNIDAD SERVICIO DE TRANSPORTE DE QUITO - ECUADOR A TUMBES 715.00",
                "CON TABLEROS DE MADERA AGLOMERADA, SEGUN FACTURA",
                "COMERCIAL: 001-098-000331036 - C.P.I 0003911",
                "2.00 UNIDAD SERVICIO DE TRANSPORTE DE QUITO - ECUADOR A TUMBES 715.00",
                "CON TABLEROS DE MADERA AGLOMERADA, SEGUN FACTURA",
                "COMERCIAL: 001-098-000331059- C.P.I 0003915"
            }, "2,860.00"));

            Assert.True(f.EsValida);
            Assert.Equal("1790000000001", f.RucCliente);
            Assert.Equal(2, f.Items.Count);
            Assert.Equal("001-098-000331036", f.Items[0].FacturaComercial);
            Assert.Equal("0003911", f.Items[0].Cpic);
            Assert.Equal("001-098-000331059", f.Items[1].FacturaComercial);
            Assert.Equal("0003915", f.Items[1].Cpic);
            Assert.Equal(2m, f.Items[1].Cantidad);
            Assert.Equal(4m, f.Cantidad);
            Assert.Equal("001-098-000331036", f.NumeroFacturaComercial);
            Assert.DoesNotContain(f.Observaciones, o => o.Contains("no menciona"));
        }

        [Fact]
        public void El_tramo_peruano_de_Novopan_trae_sus_manifiestos()
        {
            var f = LectorFacturaFlete.Leer(Factura("NOVOPAN DEMO PERU S.A.C.", "20333333333", "DOLAR AMERICANO", new[]
            {
                "2.00 UNIDAD SERVICIO DE TRANSPORTE DE TUMBES A PIURA CON 610.00",
                "TABLEROS DE MADERA AGLOMERADA, SEGUN FACTURA",
                "COMERCIAL: 001-098-000330903 - C.P.I 0003895, MCI: 00019301,",
                "0019302"
            }, "1,439.60", "219.60"));

            var i = Assert.Single(f.Items);
            Assert.Equal("001-098-000330903", i.FacturaComercial);
            Assert.Equal("0003895", i.Cpic);
            Assert.Equal("0019301, 0019302", i.Manifiestos);
        }

        [Fact]
        public void La_factura_comercial_de_Cartavio_partida_en_dos_lineas_se_lee_con_8_digitos()
        {
            var f = LectorFacturaFlete.Leer(Factura("CRISAR DEMO S.A.C.", "20444444444", "DOLAR AMERICANO", new[]
            {
                "5.00 UNIDAD SERVICIO DE TRANSPORTE DE CARTAVIO- TRUJILLO A LA 2050.00",
                "TRONCAL - GUAYAQUIL CON AZUCAR BLANCO EN BOLSAS DE",
                "POLIPROPILENO, SEGUN FACTURA COMERCIAL: FX01-",
                "0000540 - C.P.I 0003901"
            }, "10,250.00"));

            Assert.Equal("FX01-00000540", f.NumeroFacturaComercial);
            Assert.Equal("0003901", f.NumeroCPIC);
            Assert.Equal(5m, f.Cantidad);
            Assert.Equal("UNIDAD", f.UnidadMedida);
        }

        [Theory]
        [InlineData("SEGUN G.R.R: T700- 00001670 , G.R.T: EG03-00015484", "T700-00001670", "EG03-00015484")]
        [InlineData("SEGUN G.R.R: T700-00001645 - G.R.T EG03-00015172", "T700-00001645", "EG03-00015172")]
        [InlineData("SEGUN G.R.R: T700-0000651 - G.R.T EG03-00015200", "T700-00000651", "EG03-00015200")]
        public void Polinplast_cita_la_guia_del_remitente_y_la_nuestra(string referencia, string remitente, string transportista)
        {
            var f = LectorFacturaFlete.Leer(Factura("POLINPLAST DEMO S.A.C.", "20555555555", "SOLES", new[]
            {
                "1.00 UNIDAD SERVICIO DE TRANSPORTE DE CARRT. PIURA A 1950.00",
                "TAMBOGRANDE A ASCOPE - CASA GRANDE CON CAJAS,",
                referencia
            }, "2,301.00", "351.00"));

            var i = Assert.Single(f.Items);
            Assert.Null(i.FacturaComercial);
            Assert.Equal(remitente, i.GuiaRemitente);
            Assert.Equal(transportista, i.GuiaTransportista);
            Assert.Equal(remitente, i.Referencia);
            Assert.DoesNotContain(f.Observaciones, o => o.Contains("no menciona"));
        }

        [Fact]
        public void La_sobrestadia_no_cita_ninguna_carga()
        {
            var f = LectorFacturaFlete.Leer(Factura("CRISAR DEMO S.A.C.", "20444444444", "DOLAR AMERICANO", new[]
            {
                "1.00 UNIDAD POR SOBRESTADIA DE 10 DIAS POR 5 UNIDADES QUE 5000.00",
                "ESTUVIERON PARADAS EN BODEGA ECUADOR A LA ESPERA",
                "DE NACIONALIZACION (DOC. AGENCIA ECUADOR)- C.P.I",
                "0003880- $. 100DIA/CARRO - SE ADJUNTA DETALLE",
                "1.00 UNIDAD POR SOBRESTADIA DE 03 DIA POR 5 UNIDADES QUE 1500.00",
                "ESPERA DE LA NACIONALIZACION - C.P.I 0003887- SE"
            }, "6,500.00"));

            Assert.Equal(2, f.Items.Count);
            Assert.All(f.Items, x => Assert.Null(x.Referencia));
            Assert.Equal("0003880", f.Items[0].Cpic);
            Assert.Contains(f.Observaciones, o => o.Contains("ni la guía del remitente"));
        }

        [Fact]
        public void Las_lineas_viajan_a_la_bd_como_json()
        {
            var json = FacturaFleteService.ItemsJson(new[]
            {
                new ItemFacturaFlete { Orden = 1, Cantidad = 1m, UnidadMedida = "UNIDAD", GuiaRemitente = "T700-00001643", GuiaTransportista = "EG03-00015116" }
            });
            Assert.Contains("\"guiaRemitente\":\"T700-00001643\"", json);
            Assert.Contains("\"orden\":1", json);
        }

        // ── Conciliación ─────────────────────────────────────────────────────

        private static readonly DateTime Desde = new DateTime(2026, 10, 1), Hasta = new DateTime(2026, 10, 31);
        private static int _id = 500;

        private static PedidoCargado Carga(string comercial, int unidades, int idCliente = 2, string guia = null) => new PedidoCargado
        {
            IdCliente = idCliente, Cliente = idCliente == 2 ? "NOVOPAN" : "POLINPLAST", FacturaComercial = comercial, GuiaRemitente = guia,
            FechaCarga = new DateTime(2026, 10, 2), Unidades = unidades, UnidadesSinCarga = unidades
        };

        private static FacturaFleteRegistrada Flete(int? idCliente, string cliente, string moneda, params ItemFacturaFlete[] items)
        {
            int orden = 0;
            foreach (var i in items) i.Orden = ++orden;
            decimal total = items.Sum(x => x.ValorVenta ?? 0m);
            return new FacturaFleteRegistrada
            {
                IdFacturaFlete = ++_id, NumeroFactura = "E001-" + (18000 + _id), FechaEmision = new DateTime(2026, 10, 21), IdCliente = idCliente,
                Cliente = cliente, RucCliente = idCliente.HasValue ? null : "1790000000001", Moneda = moneda, ImporteTotal = total,
                Cantidad = items.Sum(x => x.Cantidad), UnidadMedida = "UNIDAD", Items = items.ToList()
            };
        }

        private static ItemFacturaFlete Linea(decimal cantidad, decimal tarifa, string comercial = null, string guia = null) =>
            new ItemFacturaFlete { Cantidad = cantidad, UnidadMedida = "UNIDAD", ValorUnitario = tarifa, FacturaComercial = comercial, GuiaRemitente = guia };

        private static List<FilaConciliacion> Conciliar(IList<PedidoCargado> cargas, IList<FacturaFleteRegistrada> facturas) =>
            FacturaFleteService.Conciliar(cargas, facturas, new Dictionary<int, FacturaFleteService.TarifaCliente>
            {
                [2] = new FacturaFleteService.TarifaCliente { FacturasRegistradas = 1, ValorUnitario = 610m, UnidadMedida = "UNIDAD", Moneda = "USD" },
                [7] = new FacturaFleteService.TarifaCliente { FacturasRegistradas = 1, ValorUnitario = 1950m, UnidadMedida = "UNIDAD", Moneda = "PEN" }
            }, new Dictionary<string, DateTime?>(), Desde, Hasta, out _);

        [Fact]
        public void Una_carga_cobrada_por_tramos_a_dos_clientes_es_conforme()
        {
            var ecuador = Flete(null, "NOVOPAN DEL ECUADOR", "USD", Linea(2, 715m, "001-098-000331036"), Linea(2, 715m, "001-098-000331046"));
            var peru = Flete(2, "NOVOPAN", "USD", Linea(2, 610m, "001-098-000331036"));

            var filas = Conciliar(new[] { Carga("001-098-000331036", 2) }, new[] { ecuador, peru });

            var conforme = filas.Single(x => x.Pedido != null);
            Assert.Equal(EstadoConciliacion.Conforme, conforme.Estado);
            Assert.Equal(2m, conforme.CantidadFacturada);
            Assert.Equal(1430m + 1220m, conforme.ImporteFacturado);      // la mitad de la factura del Ecuador + la del Perú
            Assert.Contains(conforme.Avisos, a => a.StartsWith("Facturado por tramos"));
            Assert.DoesNotContain(conforme.Avisos, a => a.Contains("dos veces"));

            // La otra carga de la factura del Ecuador no está en el sistema.
            var sinCarga = filas.Single(x => x.Pedido == null);
            Assert.Equal(EstadoConciliacion.SinCarga, sinCarga.Estado);
            Assert.Equal("001-098-000331046", sinCarga.Referencia);
            Assert.Equal(1430m, sinCarga.ImporteFacturado);
        }

        [Fact]
        public void Un_tramo_con_otra_cantidad_sale_como_diferencia_con_el_cliente()
        {
            var ecuador = Flete(null, "NOVOPAN DEL ECUADOR", "USD", Linea(2, 715m, "001-098-000330905"));
            var peru = Flete(2, "NOVOPAN", "USD", Linea(3, 909m, "001-098-000330905"));

            var f = Assert.Single(Conciliar(new[] { Carga("001-098-000330905", 3) }, new[] { ecuador, peru }));

            Assert.Equal(EstadoConciliacion.Diferencia, f.Estado);
            Assert.Equal(-1m, f.Diferencia);
            Assert.StartsWith("NOVOPAN DEL ECUADOR: se facturaron 2", f.Avisos[0]);
        }

        [Fact]
        public void El_mismo_cliente_dos_veces_por_la_misma_carga_avisa()
        {
            var a = Flete(2, "NOVOPAN", "USD", Linea(2, 610m, "001-098-000331060"));
            var b = Flete(2, "NOVOPAN", "USD", Linea(2, 610m, "001-098-000331060"));

            var f = Assert.Single(Conciliar(new[] { Carga("001-098-000331060", 2) }, new[] { a, b }));

            Assert.Equal(EstadoConciliacion.Diferencia, f.Estado);
            Assert.Contains(f.Avisos, x => x.Contains("revise que no esté facturado dos veces"));
        }

        [Fact]
        public void Polinplast_se_une_por_la_guia_del_remitente()
        {
            var f1 = Flete(7, "POLINPLAST", "PEN", Linea(1, 1950m, guia: "T700-00001645"));
            var filas = Conciliar(new[] { Carga(null, 1, 7, "T700-00001645"), Carga(null, 1, 7, "T700-00001649") }, new[] { f1 });

            Assert.Equal(EstadoConciliacion.Conforme, filas.Single(x => x.Referencia == "T700-00001645").Estado);
            var porFacturar = filas.Single(x => x.Referencia == "T700-00001649");
            Assert.Equal(EstadoConciliacion.PorFacturar, porFacturar.Estado);
            Assert.Equal(1950m, porFacturar.ImporteEstimado);       // por unidad: no espera toneladas leídas de la guía
        }

        [Fact]
        public void Si_la_factura_cita_mal_la_guia_del_remitente_se_une_por_la_nuestra_y_avisa()
        {
            // E001-18693 dice "T700-0000651"; su GRE EG03-00015200 es la de la guía T700-00001651.
            var linea = Linea(1, 1950m, guia: "T700-00000651");
            linea.GuiaTransportista = "EG03-00015200";
            var factura = Flete(7, "POLINPLAST", "PEN", linea);
            var carga = Carga(null, 1, 7, "T700-00001651");
            carga.GuiaTransportista = "EG03-00015200";

            var f = Assert.Single(Conciliar(new[] { carga }, new[] { factura }));

            Assert.Equal(EstadoConciliacion.Conforme, f.Estado);
            Assert.Contains("se unió por nuestra guía de transportista EG03-00015200", f.Avisos[0]);
        }

        [Theory]
        [InlineData(1439.60, 1382.02, false)]    // Novopan Perú: detracción del 4 %
        [InlineData(2537.00, 2435.52, false)]    // Polinplast: detracción del 4 %
        [InlineData(10250.00, 10250.00, false)]  // Crisar: sin detracción
        [InlineData(12889.80, 12889.20, true)]   // E001-18645: 0,60 de menos sin explicación
        public void La_detraccion_no_es_una_diferencia_del_credito(double total, double neto, bool avisa)
        {
            var f = new FacturaFleteRegistrada { ImporteTotal = (decimal)total, MontoNetoPendiente = (decimal)neto };
            Assert.Equal(avisa, FacturaFleteService.NetoNoCuadra(f));
        }

        [Fact]
        public void Una_carga_de_tercero_sin_documentos_queda_sin_documentos()
        {
            var f1 = Flete(7, "POLINPLAST", "PEN", Linea(1, 1950m, guia: "T700-00001645"));
            var filas = Conciliar(new[] { Carga(null, 2, 7) }, new[] { f1 });

            Assert.Contains(filas, x => x.Estado == EstadoConciliacion.SinDocumentos && x.Pedido != null);
            Assert.Contains(filas, x => x.Estado == EstadoConciliacion.SinCarga && x.Referencia == "T700-00001645");
        }
    }
}
