using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests
{
    public class ReporteExcelBuilderTests
    {
        private static ReporteExcelDatos DatosBase(string numeroPedido = null) => new ReporteExcelDatos
        {
            Titulo = "Reporte de Pedidos",
            Periodo = "Período: 2026-01-01 al 2026-01-31",
            NumeroPedidoFiltrado = numeroPedido,
            Encabezados = new List<string> { "Nº Pedido", "Cliente", "Monto" },
            Filas = new List<IList<string>>
            {
                new List<string> { "P-001", "Vitapro", "100.00" },
                new List<string> { "",      "Novopan", "50.00" },
                new List<string> { "P-002", "Vitapro" }          // fila corta: la 3ra celda queda vacía
            },
            Resumen = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Total Ingresos:", "S/ 150.00"),
                new KeyValuePair<string, string>("Balance:", "S/ 150.00")
            }
        };

        private static IXLWorksheet Abrir(byte[] archivo)
        {
            var wb = new XLWorkbook(new MemoryStream(archivo));
            return wb.Worksheet("Reporte");
        }

        [Fact]
        public void Generar_SinFiltroDePedido_EncabezadosEnFila4YDatosDebajo()
        {
            var ws = Abrir(ReporteExcelBuilder.Generar(DatosBase()));

            Assert.Equal("Reporte de Pedidos", ws.Cell(1, 1).GetString());
            Assert.Equal("Período: 2026-01-01 al 2026-01-31", ws.Cell(2, 1).GetString());
            Assert.Equal("Nº Pedido", ws.Cell(4, 1).GetString());
            Assert.Equal("Monto", ws.Cell(4, 3).GetString());
            Assert.Equal("P-001", ws.Cell(5, 1).GetString());
            Assert.Equal("Novopan", ws.Cell(6, 2).GetString());
            Assert.Equal("", ws.Cell(7, 3).GetString());
        }

        [Fact]
        public void Generar_ConFiltroDePedido_AgregaFilaPedidoYEncabezadosEnFila5()
        {
            var ws = Abrir(ReporteExcelBuilder.Generar(DatosBase("P-001")));

            Assert.Equal("Número de Pedido: P-001", ws.Cell(3, 1).GetString());
            Assert.Equal("Nº Pedido", ws.Cell(5, 1).GetString());
            Assert.Equal("P-001", ws.Cell(6, 1).GetString());
            Assert.True(ws.Cell(6, 1).Style.Font.Bold);                       // pedido filtrado resaltado
            Assert.Equal(XLColor.LightYellow, ws.Cell(6, 1).Style.Fill.BackgroundColor);
        }

        [Fact]
        public void Generar_SinFiltroDePedido_NoResaltaCeldasVacias()
        {
            var ws = Abrir(ReporteExcelBuilder.Generar(DatosBase()));

            // Fila 6 = "" en Nº Pedido: antes se resaltaba porque "" == filtro vacío.
            Assert.False(ws.Cell(6, 1).Style.Font.Bold);
        }

        [Fact]
        public void Generar_ResumenDebajoDeLosDatos()
        {
            var ws = Abrir(ReporteExcelBuilder.Generar(DatosBase()));

            // 3 filas de datos desde la fila 5 → "Resumen" en 3 + 4 + 3 = fila 10
            Assert.Equal("Resumen", ws.Cell(10, 1).GetString());
            Assert.Equal("Total Ingresos:", ws.Cell(11, 1).GetString());
            Assert.Equal("S/ 150.00", ws.Cell(11, 2).GetString());
            Assert.Equal("Balance:", ws.Cell(12, 1).GetString());
        }

        [Theory]
        [InlineData("conductor", null, "Reporte_Viajes_Conductor_20260131_083005.xlsx")]
        [InlineData("pedido", "", "Reporte_Pedidos_20260131_083005.xlsx")]
        [InlineData("pedido", "P-001", "Reporte_Pedido_P-001_20260131_083005.xlsx")]
        [InlineData("pedido", "a/b;c\"d", "Reporte_Pedido_a_b_c_d_20260131_083005.xlsx")]
        [InlineData("otro", null, "Reporte_General_20260131_083005.xlsx")]
        public void NombreArchivo_SegunTipo(string tipo, string pedido, string esperado)
        {
            Assert.Equal(esperado, ReporteExcelBuilder.NombreArchivo(tipo, pedido, new DateTime(2026, 1, 31, 8, 30, 5)));
        }

        [Theory]
        [InlineData("<span class='text-success'>S/ 10.00</span>", "S/ 10.00")]
        [InlineData("S/ 5.00", "S/ 5.00")]
        [InlineData(null, "")]
        public void QuitarHtml_DejaSoloElTexto(string entrada, string esperado)
        {
            Assert.Equal(esperado, ReporteExcelBuilder.QuitarHtml(entrada));
        }
    }
}
