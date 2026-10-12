using System;
using System.Data;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using WebSGV.Helpers;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Excel y PDF de los reportes tabulares (ReportesOrdenesViaje), sin BD.</summary>
    public class ReporteTabularTests
    {
        private static readonly EmpresaInfo Empresa = new EmpresaInfo
        {
            RazonSocial = "SERVICIOS GENERALES VIVIANA E.I.R.L.", Ruc = "20483851171", DomicilioFiscal = "Lima"
        };

        private static ReporteTabular Reporte(int filas)
        {
            var dt = new DataTable();
            dt.Columns.Add("Conductor", typeof(string));
            dt.Columns.Add("Fecha", typeof(DateTime));
            dt.Columns.Add("Neto", typeof(decimal));
            dt.Columns.Add("Dias", typeof(int));
            for (int i = 0; i < filas; i++)
                dt.Rows.Add("JUAN   PEREZ  " + i, new DateTime(2026, 7, 1).AddDays(i), i % 2 == 0 ? 10m : -4m, i);

            var r = new ReporteTabular
            {
                Titulo = "Reporte de prueba",
                NombreHoja = "Prueba: hoja/1",
                Filas = dt.Rows.Cast<DataRow>().ToList(),
                Nota = "Nota de prueba"
            };
            r.Filtros.Add("Período: 01/07/2026 al 31/07/2026");
            r.Columnas.Add(new ColumnaReporte { Titulo = "Conductor", Tipo = TipoColumnaReporte.Texto, Ancho = 2f, Valor = d => d["Conductor"] });
            r.Columnas.Add(new ColumnaReporte { Titulo = "Fecha", Tipo = TipoColumnaReporte.Fecha, Valor = d => d["Fecha"] });
            r.Columnas.Add(new ColumnaReporte { Titulo = "Neto S/", Tipo = TipoColumnaReporte.Soles, Valor = d => d["Neto"], VerdeSiPositivo = true });
            r.Columnas.Add(new ColumnaReporte { Titulo = "Días", Tipo = TipoColumnaReporte.Entero, Valor = d => d["Dias"], Alerta = d => (int)d["Dias"] > 7 });
            r.Resumen.Add(new LineaResumen { Etiqueta = "Total", Valor = 12.5m, Tipo = TipoColumnaReporte.Soles, Destacada = true });
            return r;
        }

        private static IXLWorksheet Hoja(ReporteTabular r)
        {
            var wb = new XLWorkbook(new MemoryStream(ReporteTabularExportador.GenerarExcel(r, Empresa)));
            return wb.Worksheet(1);
        }

        private static int FilaCabecera(IXLWorksheet ws) =>
            ws.RowsUsed().First(f => f.Cell(1).GetString() == "Conductor").RowNumber();

        [Fact]
        public void Excel_ConLogo_LoIncrustaYCorreElTextoDeLaEmpresa()
        {
            string logo = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo-prueba.png");
            File.WriteAllBytes(logo, Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="));
            try
            {
                var wb = new XLWorkbook(new MemoryStream(ReporteTabularExportador.GenerarExcel(Reporte(3), Empresa, logo)));
                var ws = wb.Worksheet(1);

                Assert.Single(ws.Pictures);
                Assert.Equal(Empresa.RazonSocial, ws.Cell(1, 2).GetString());
            }
            finally { File.Delete(logo); }
        }

        [Fact]
        public void Excel_EncabezadoConEmpresaTituloYFiltros()
        {
            var ws = Hoja(Reporte(3));

            Assert.Equal("Prueba hoja 1", ws.Name); // sin caracteres que Excel no admite en el nombre
            Assert.Equal(Empresa.RazonSocial, ws.Cell(1, 1).GetString());
            Assert.Contains("RUC 20483851171", ws.Cell(2, 1).GetString());
            Assert.Contains(ws.RowsUsed(), f => f.Cell(1).GetString() == "Reporte de prueba");
            Assert.Contains(ws.RowsUsed(), f => f.Cell(1).GetString() == "Período: 01/07/2026 al 31/07/2026");
        }

        [Fact]
        public void Excel_FechasYMontosSonValoresNoTexto()
        {
            var ws = Hoja(Reporte(3));
            int cab = FilaCabecera(ws);

            Assert.Equal("JUAN PEREZ 0", ws.Cell(cab + 1, 1).GetString()); // espacios repetidos colapsados
            Assert.Equal(XLDataType.DateTime, ws.Cell(cab + 1, 2).DataType);
            Assert.Equal(XLDataType.Number, ws.Cell(cab + 1, 3).DataType);
            Assert.Equal(-4m, (decimal)ws.Cell(cab + 2, 3).GetDouble());
            Assert.Equal(XLDataType.Number, ws.Cell(cab + 1, 4).DataType);
        }

        [Fact]
        public void Excel_TotalConSubtotalFiltroYFilaFija()
        {
            var ws = Hoja(Reporte(3));
            int cab = FilaCabecera(ws);

            Assert.Equal($"SUBTOTAL(109,C{cab + 1}:C{cab + 3})", ws.Cell(cab + 4, 3).FormulaA1);
            Assert.True(ws.AutoFilter.IsEnabled);
            Assert.Equal(cab, ws.SheetView.SplitRow);
            Assert.Equal(XLPageOrientation.Landscape, ws.PageSetup.PageOrientation);
        }

        [Fact]
        public void Excel_SinFilas_AvisaYNoAgregaTotales()
        {
            var ws = Hoja(Reporte(0));
            int cab = FilaCabecera(ws);

            Assert.Contains("No hay registros", ws.Cell(cab + 1, 1).GetString());
            Assert.False(ws.Cell(cab + 2, 3).HasFormula);
        }

        [Fact]
        public void Pdf_MuchasFilas_ContinuaEnVariasPaginas()
        {
            int Paginas(ReporteTabular r) => ReporteTabularExportador.CrearPdf(r, Empresa, null).GenerateImages().Count();

            Assert.Equal(1, Paginas(Reporte(5)));
            Assert.True(Paginas(Reporte(120)) > 1);
            Assert.Equal(1, Paginas(Reporte(0)));
        }
    }
}
