using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos.Importacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Lectura de los documentos de un pedido (guía de remisión, packing list, factura).
    /// Los textos imitan las líneas que <see cref="ExtractorTextoPdf"/> saca de los PDF
    /// del cliente, con datos inventados: los documentos reales no se versionan.
    /// </summary>
    public class LectorDocumentosPedidoTests
    {
        // ── Textos de ejemplo ─────────────────────────────────────────────────

        private static IList<string> Guia(string numero, string chofer, string licencia, string placa,
            string entrega, string transporte, string peso, params string[] bienes)
        {
            var lineas = new List<string>
            {
                "R.U.C. N° 20111111111",
                "EMPRESA CLIENTE S.A.",
                "T: (511) 315 0800 Prov. Const. del Callao GUÍA DE REMISIÓN",
                "ELECTRÓNICA REMITENTE",
                numero,
                "Fecha Emisión : 07/10/2026",
                "Fecha de Traslado : 07/10/2026",
                "Razón Social / Señor : CLIENTE ECUADOR CIA LTDA Tipo y Nro. Documento : 0999999999001",
                "Panamericana Norte KM 558--Trujillo-Moche",
                "Dirección Origen :",
                "Dirección Llegada : AV. PANAMERICANA NORTE KM-1256-TUMBES - TUMBES",
                "Razón Social : TRANSPORTISTA EIRL Tipo y Nro de Documento : RUC 20222222222",
                $"Nombre Chofer : {chofer} Nro Licencia de Conducir : {licencia}",
                $"Placa / Carreta : {placa} Mod. Transporte : 01 - TRANSPORTE PÚBLICO",
                "Motivo de Traslado Nro Pedido Nro Entrega Nro Transporte / FO Pto. Expedición Peso Total de Carga + UM",
                "OTROS - Traslado para exportación que aún no",
                $"4400000001 {entrega} {transporte} 2201 {peso} KGM",
                "cuenta con DAM numerada",
                "CÓDIGO DESCRIPCIÓN CANTIDAD PESO"
            };
            lineas.AddRange(bienes);
            lineas.Add("Total 29818.8 KG");
            return lineas;
        }

        private static IList<string> PaginaPackingList(string placa, string chofer, string transporte, params string[] filas)
        {
            var lineas = new List<string>
            {
                "EMPRESA CLIENTE S.A.",
                "PACKING LIST Nº 2026 - 1234567",
                "Destinatario: CD CLIENTE MILAGRO Página: 1 / 1",
                "Dirección - Ciudad - País: KM. 29 VIA DURAN - YAGUACHI Guayas Ecuador",
                "Nº Código Descripción Cantidad Unidad Peso Bruto Peso Neto Unidad Fecha Prod. Fecha Venc. Lote"
            };
            lineas.AddRange(filas);
            lineas.AddRange(new[]
            {
                "TOTAL 1,188.000 UND 29,818.800 29,700.000 KG",
                $"Contenedor Nº Placa Camión {placa}",
                $"Contenedor Tipo CAM31T Chofer {chofer}",
                $"Fecha Despacho 07.10.2026 Nº de Transporte SAP {transporte}",
                "Nº Pedido Cliente SAP 4400000001"
            });
            return lineas;
        }

        private static IList<string> Factura() => new List<string>
        {
            "Empresa Cliente S.A.",
            "R.U.C. 20111111111",
            "FACTURA ELECTRONICA",
            "F222 - 00001234",
            "Referencia: SAP/4400000001",
            "Puerto de Carga: Trujillo - Perú",
            "Puerto de Descarga: Guayaquil - Ecuador",
            "Tipo de moneda: DOLAR AMERICANO",
            "Fecha de Emisión: Callao 07 de Octubre de 2026",
            "Cantidad Unidad Descripción Valor Unitario Valor Venta",
            "1,188.00 UND Producto Uno 35% 2.0 29,700.00 21.5200 25,557.44",
            "1,188.00 UND Producto Dos 33% 2.0 29,700.00 21.5000 25,537.62",
            "2,376.00 59,400.00",
            "TOTAL VALOR FLETE INTERNACIONAL 3,600.00",
            "IMPORTE TOTAL $ 54,695.06"
        };

        private const string Bien1A = "7000001 Producto Uno 35% 2.0 - LOTE0001 668.0000 EA 16766.8 KG";
        private const string Bien1B = "7000001 Producto Uno 35% 2.0 - LOTE0002 520.0000 EA 13052 KG";
        private const string Bien2 = "7000002 Producto Dos 33% 2.0 - LOTE0003 1188.0000 EA 29818.8 KG";

        private const string Fila1A = "1 7000001 Producto Uno 35% 2.0 668.000 UND 16,766.800 16,700.000 KG 01.10.2026 26.09.2027 LOTE0001";
        private const string Fila1B = "2 7000001 Producto Uno 35% 2.0 520.000 UND 13,052.000 13,000.000 KG 02.10.2026 27.09.2027 LOTE0002";
        private const string Fila2 = "1 7000002 Producto Dos 33% 2.0 1,188.000 UND 29,818.800 29,700.000 KG 01.10.2026 26.09.2027 LOTE0003";

        private static LectorDocumentosPedido.DocumentoLeido Doc(string nombre, params IList<string>[] paginas) =>
            new LectorDocumentosPedido.DocumentoLeido { NombreArchivo = nombre, Paginas = paginas.ToList() };

        private static List<LectorDocumentosPedido.DocumentoLeido> PedidoCompleto() => new List<LectorDocumentosPedido.DocumentoLeido>
        {
            Doc("factura.pdf", Factura()),
            Doc("PL.pdf",
                PaginaPackingList("AAA111", "PEREZ GOMEZ JUAN", "1000001", Fila1A, Fila1B),
                PaginaPackingList("BBB222", "TORRES DIAZ ROSA", "1000002", Fila2)),
            Doc("guia1.pdf", Guia("T660 - 00000001", "PEREZ GOMEZ JUAN", "B12345678", "AAA111", "8000001", "1000001", "29818.800", Bien1A, Bien1B)),
            Doc("guia2.pdf", Guia("T660 - 00000002", "TORRES DIAZ ROSA", "B87654321", "BBB222", "8000002", "1000002", "29818.800", Bien2))
        };

        // ── Clasificación ─────────────────────────────────────────────────────

        [Fact]
        public void Clasificar_ReconoceCadaTipoDeDocumento()
        {
            Assert.Equal(TipoDocumentoPedido.GuiaRemision, LectorDocumentosPedido.Clasificar(new[] { Guia("T660 - 00000001", "A B C", "B12345678", "AAA111", "1", "2", "1") }));
            Assert.Equal(TipoDocumentoPedido.PackingList, LectorDocumentosPedido.Clasificar(new[] { PaginaPackingList("AAA111", "A B C", "1") }));
            Assert.Equal(TipoDocumentoPedido.Factura, LectorDocumentosPedido.Clasificar(new[] { Factura() }));
            Assert.Equal(TipoDocumentoPedido.Desconocido, LectorDocumentosPedido.Clasificar(new IList<string>[] { new[] { "Carta de Porte Internacional" } }));
        }

        // ── Guía ──────────────────────────────────────────────────────────────

        [Fact]
        public void LeerGuia_SacaIdentidadDelTractoYSuCarga()
        {
            var g = LectorDocumentosPedido.LeerGuia(Guia("T660 - 00016349", "PEREZ GOMEZ JUAN", "B43347960", "C6K799",
                "85921827", "1288126", "29818.800", Bien1A, Bien1B));

            Assert.Equal("T660-00016349", g.NumeroGuia);
            Assert.Equal(new DateTime(2026, 10, 7), g.FechaTraslado);
            Assert.Equal("PEREZ GOMEZ JUAN", g.NombreChofer);
            Assert.Equal("B43347960", g.Licencia);
            Assert.Equal("43347960", g.DniSegunLicencia);
            Assert.Equal("C6K799", g.Placa);
            Assert.Equal("4400000001", g.NumeroPedido);
            Assert.Equal("85921827", g.NumeroEntrega);
            Assert.Equal("1288126", g.NumeroTransporte);
            Assert.Equal(29818.8m, g.PesoDeclaradoKg);
            Assert.True(g.EsExportacion);
            Assert.Equal("Panamericana Norte KM 558--Trujillo-Moche", g.Origen);   // el valor quedó en la línea anterior

            Assert.Equal(2, g.Lineas.Count);
            Assert.Equal("7000001", g.Lineas[0].CodigoProducto);
            Assert.Equal("Producto Uno 35% 2.0", g.Lineas[0].Descripcion);
            Assert.Equal("LOTE0001", g.Lineas[0].Lote);
            Assert.Equal(668m, g.Lineas[0].Cantidad);
            Assert.Equal(16766.8m, g.Lineas[0].PesoBrutoKg);
            Assert.Equal(13052m, g.Lineas[1].PesoBrutoKg);
        }

        [Fact]
        public void LeerGuia_PlacaConCarreta_SeparaAmbas()
        {
            var lineas = Guia("T660 - 00000001", "A B C", "B12345678", "AAA111", "1", "2", "1").ToList();
            int i = lineas.FindIndex(l => l.StartsWith("Placa"));
            lineas[i] = "Placa / Carreta : C6K-799 / T1X-123 Mod. Transporte : 01";

            var g = LectorDocumentosPedido.LeerGuia(lineas);

            Assert.Equal("C6K799", g.Placa);
            Assert.Equal("T1X123", g.PlacaCarreta);
        }

        // ── Packing list ──────────────────────────────────────────────────────

        [Fact]
        public void LeerPackingList_UnTractoPorPaginaConPesoNetoYFechas()
        {
            var tractos = LectorDocumentosPedido.LeerPackingList(new[]
            {
                PaginaPackingList("AAA111", "PEREZ GOMEZ JUAN", "1000001", Fila1A, Fila1B),
                PaginaPackingList("BBB222", "TORRES DIAZ ROSA", "1000002", Fila2)
            }, out string numero, out string destino);

            Assert.Equal("2026-1234567", numero);
            Assert.Contains("Ecuador", destino);
            Assert.Equal(2, tractos.Count);

            var t = tractos[0];
            Assert.Equal("AAA111", t.Placa);
            Assert.Equal("PEREZ GOMEZ JUAN", t.NombreChofer);
            Assert.Equal("1000001", t.NumeroTransporte);
            Assert.Equal(2, t.Lineas.Count);
            Assert.Equal(16766.8m, t.Lineas[0].PesoBrutoKg);
            Assert.Equal(16700m, t.Lineas[0].PesoNetoKg);
            Assert.Equal(new DateTime(2026, 10, 1), t.Lineas[0].FechaProduccion);
            Assert.Equal(new DateTime(2027, 9, 26), t.Lineas[0].FechaVencimiento);
            Assert.Equal("LOTE0001", t.Lineas[0].Lote);
            Assert.Equal(29700m, t.PesoNetoTotalKg);
            Assert.Equal(1188m, tractos[1].CantidadTotal);   // "1,188.000" con coma de miles
        }

        // ── Factura ───────────────────────────────────────────────────────────

        [Fact]
        public void LeerFactura_SacaCabeceraYTotales()
        {
            var f = LectorDocumentosPedido.LeerFactura(Factura(), out string ruc);

            Assert.Equal("20111111111", ruc);
            Assert.Equal("F222-00001234", f.NumeroFactura);
            Assert.Equal(new DateTime(2026, 10, 7), f.FechaEmision);
            Assert.Equal("USD", f.Moneda);
            Assert.Equal("4400000001", f.NumeroPedido);
            Assert.Equal("Trujillo - Perú", f.PuertoCarga);
            Assert.Equal("Guayaquil - Ecuador", f.PuertoDescarga);
            Assert.Equal(54695.06m, f.ValorTotal);
            Assert.Equal(3600m, f.ValorFlete);
            Assert.Equal(2376m, f.CantidadTotal);
            Assert.Equal(59400m, f.PesoNetoTotalKg);
        }

        // ── Pedido completo ───────────────────────────────────────────────────

        [Fact]
        public void Leer_PedidoCompleto_CombinaGuiaYPackingListSinAdvertencias()
        {
            var pedido = LectorDocumentosPedido.Leer(PedidoCompleto());

            Assert.Empty(pedido.Advertencias);
            Assert.Empty(pedido.ArchivosIgnorados);
            Assert.Equal("4400000001", pedido.NumeroPedido);
            Assert.Equal("20111111111", pedido.RucRemitente);
            Assert.True(pedido.EsExportacion);
            Assert.Equal("Panamericana Norte KM 558--Trujillo-Moche", pedido.Origen);   // se carga en Trujillo
            Assert.Contains("Ecuador", pedido.Destino);                                   // la carga va a Ecuador
            Assert.Equal("F222-00001234", pedido.Factura.NumeroFactura);
            Assert.Equal(2, pedido.Tractos.Count);
            Assert.Equal(2376m, pedido.CantidadTotal);

            var t = pedido.Tractos[0];
            Assert.Equal("1000001", t.NumeroTransporte);
            Assert.Equal("T660-00000001", t.NumeroGuia);        // de la guía
            Assert.Equal("8000001", t.NumeroEntrega);
            Assert.Equal("B12345678", t.Licencia);
            Assert.True(t.TienePackingList);
            Assert.Equal(16700m, t.Lineas[0].PesoNetoKg);       // del packing list
            Assert.Equal(new DateTime(2026, 10, 1), t.Lineas[0].FechaProduccion);
        }

        [Fact]
        public void Leer_SoloGuias_UsaLaCargaDeLaGuia()
        {
            var docs = PedidoCompleto().Where(d => d.NombreArchivo.StartsWith("guia")).ToList();

            var pedido = LectorDocumentosPedido.Leer(docs);

            Assert.Equal(2, pedido.Tractos.Count);
            Assert.All(pedido.Tractos, t => Assert.False(t.TienePackingList));
            Assert.Equal(16766.8m, pedido.Tractos[0].Lineas[0].PesoBrutoKg);
            Assert.Null(pedido.Tractos[0].Lineas[0].PesoNetoKg);
            Assert.Contains(pedido.Advertencias, a => a.Contains("factura"));
        }

        [Fact]
        public void Leer_FaltaUnaGuia_AvisaQueLaFacturaNoCuadra()
        {
            var docs = PedidoCompleto().Where(d => d.NombreArchivo != "guia2.pdf" && d.NombreArchivo != "PL.pdf").ToList();

            var pedido = LectorDocumentosPedido.Leer(docs);

            Assert.Single(pedido.Tractos);
            Assert.Contains(pedido.Advertencias, a => a.Contains("2,376") || a.Contains("2.376"));
        }

        [Fact]
        public void Leer_PackingListDistintoDeLaGuia_Avisa()
        {
            var docs = PedidoCompleto();
            docs[1] = Doc("PL.pdf",
                PaginaPackingList("AAA111", "PEREZ GOMEZ JUAN", "1000001",
                    "1 7000001 Producto Uno 35% 2.0 600.000 UND 15,060.000 15,000.000 KG 01.10.2026 26.09.2027 LOTE0009"),
                PaginaPackingList("BBB222", "TORRES DIAZ ROSA", "1000002", Fila2));

            var pedido = LectorDocumentosPedido.Leer(docs);

            Assert.Contains(pedido.Advertencias, a => a.Contains("bolsas") && a.Contains("AAA111"));
            Assert.Contains(pedido.Advertencias, a => a.Contains("lotes") && a.Contains("AAA111"));
        }

        [Fact]
        public void Leer_GuiaRepetida_SeTomaUnaSolaVez()
        {
            var docs = PedidoCompleto();
            docs.Add(Doc("guia1-copia.pdf", docs[2].Paginas.ToArray()));

            var pedido = LectorDocumentosPedido.Leer(docs);

            Assert.Equal(2, pedido.Tractos.Count);
            Assert.Contains(pedido.Advertencias, a => a.Contains("repite"));
        }

        [Fact]
        public void Leer_ArchivoDesconocido_SeInformaComoIgnorado()
        {
            var docs = PedidoCompleto();
            docs.Add(Doc("cpic.pdf", new[] { "Carta de Porte Internacional por Carretera ( CPIC)" }));

            var pedido = LectorDocumentosPedido.Leer(docs);

            Assert.Contains(pedido.ArchivosIgnorados, a => a.StartsWith("cpic.pdf"));
        }

        // ── Utilidades ────────────────────────────────────────────────────────

        [Theory]
        [InlineData("B43347960", "43347960")]
        [InlineData("q16592594", "16592594")]
        [InlineData("43347960", null)]
        [InlineData("BB4334796", null)]
        [InlineData(null, null)]
        public void LicenciaConducir_ExtraeElDni(string licencia, string dni) =>
            Assert.Equal(dni, LicenciaConducir.ExtraerDni(licencia));

        [Theory]
        [InlineData("ROMERO CORTEZ JAIME", "ROMERO", "CORTEZ", "JAIME")]
        [InlineData("FLORES VARGAS CRISTH ANDRE", "FLORES", "VARGAS", "CRISTH ANDRE")]
        [InlineData("QUISPE JUAN", "QUISPE", "", "JUAN")]
        public void DividirNombre_ApellidosPrimero(string completo, string paterno, string materno, string nombres)
        {
            LectorDocumentosPedido.DividirNombre(completo, out string p, out string m, out string n);
            Assert.Equal(paterno, p);
            Assert.Equal(materno, m);
            Assert.Equal(nombres, n);
        }

        [Fact]
        public void NombrePareceTruncado_CuandoLlegaAlTopeDelCampo()
        {
            Assert.True(LectorDocumentosPedido.NombrePareceTruncado("SAAVEDRA MAURICIO FR"));
            Assert.False(LectorDocumentosPedido.NombrePareceTruncado("ROMERO CORTEZ JAIME"));
        }

        [Theory]
        [InlineData("C6K-799", "C6K799")]
        [InlineData(" c6k 799 ", "C6K799")]
        [InlineData("", null)]
        public void NormalizarPlaca_SinGuionNiEspacios(string placa, string esperado) =>
            Assert.Equal(esperado, LectorDocumentosPedido.NormalizarPlaca(placa));

        [Fact]
        public void Normalizar_QuitaTildesParaComparar() =>
            Assert.Equal(LectorDocumentosPedido.Normalizar("Nicovita Katal Camaron 35%"),
                         LectorDocumentosPedido.Normalizar("Nicovita  Katal Camarón 35%"));
    }
}
