using System.IO;
using System.Text;
using System.Xml.Linq;
using ClosedXML.Excel;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Topes de la importación del seguimiento desde Excel (B6-3): solo .xlsx de verdad, 10 MB,
    /// filas y columnas de la hoja, y coherencia con el límite de envío de Web.config.
    /// </summary>
    public class LimitesImportacionSeguimientoTests
    {
        private static byte[] XlsxReal()
        {
            using (var wb = new XLWorkbook())
            using (var ms = new MemoryStream())
            {
                wb.Worksheets.Add("SEGUIMIENTO").Cell(1, 1).Value = "CLIENTE";
                wb.SaveAs(ms);
                return ms.ToArray();
            }
        }

        [Fact]
        public void Xlsx_DeVerdad_SeAcepta()
        {
            byte[] x = XlsxReal();
            Assert.Null(LimitesImportacionSeguimiento.ValidarArchivo("STATUS GENERAL OCTUBRE 2026.XLSX", x.Length, x));
        }

        [Fact]
        public void Xls_Antiguo_SeRechazaConComoConvertirlo()
        {
            byte[] ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            string error = LimitesImportacionSeguimiento.ValidarArchivo("status.xls", ole.Length, ole);
            Assert.NotNull(error);
            Assert.Contains(".xlsx", error);
            Assert.Contains("Guardar como", error);
        }

        [Fact]
        public void TextoRenombradoAXlsx_SeRechaza()
        {
            byte[] texto = Encoding.UTF8.GetBytes("cliente;tracto\n4400;ABC-123");
            string error = LimitesImportacionSeguimiento.ValidarArchivo("status.xlsx", texto.Length, texto);
            Assert.Equal("Archivo Excel: el archivo no es un XLSX (está dañado o le cambiaron la extensión).", error);
        }

        [Theory]
        [InlineData("status.csv")]
        [InlineData("status.pdf")]
        [InlineData("status")]
        public void OtraExtension_SeRechaza(string nombre)
        {
            byte[] x = XlsxReal();
            Assert.Contains("tipo de archivo no permitido. Use XLSX", LimitesImportacionSeguimiento.ValidarArchivo(nombre, x.Length, x));
        }

        [Fact]
        public void MasDe10Mb_SeRechaza()
        {
            byte[] x = XlsxReal();
            Assert.Null(LimitesImportacionSeguimiento.ValidarArchivo("a.xlsx", LimitesImportacionSeguimiento.MaxBytes, x));
            Assert.Contains("10 MB", LimitesImportacionSeguimiento.ValidarArchivo("a.xlsx", LimitesImportacionSeguimiento.MaxBytes + 1L, x));
        }

        [Fact]
        public void Vacio_SeRechaza()
        {
            Assert.Equal("Archivo Excel: el archivo está vacío.", LimitesImportacionSeguimiento.ValidarArchivo("a.xlsx", 0, new byte[0]));
        }

        // --- Tamaño descomprimido (el .xlsx es un ZIP; ClosedXML lo carga entero al abrirlo) ---

        private static string Descomprimido(byte[] zip, long tope = LimitesImportacionSeguimiento.MaxDescomprimido)
        {
            using (var ms = new MemoryStream(zip))
                return LimitesImportacionSeguimiento.ValidarDescomprimido(ms, tope);
        }

        [Fact]
        public void Descomprimido_XlsxNormal_PasaYNoCierraElFlujo()
        {
            byte[] x = XlsxReal();
            using (var ms = new MemoryStream(x))
            {
                Assert.Null(LimitesImportacionSeguimiento.ValidarDescomprimido(ms));
                Assert.True(ms.CanRead);   // leaveOpen: el llamador decide
            }
        }

        [Fact]
        public void Descomprimido_MuyComprimible_SeFrenaAntesDeAbrirlo()
        {
            const int MB = 1024 * 1024;
            // 31 MB de ceros: el ZIP pesa unos 30 KB y pasa sin problema el tope de 10 MB del archivo.
            byte[] bomba = ImportacionZipBombTests.Zip(("xl/worksheets/sheet1.xml", 31 * MB));
            Assert.True(bomba.Length < LimitesImportacionSeguimiento.MaxBytes);
            string error = Descomprimido(bomba);
            Assert.NotNull(error);
            Assert.Contains("demasiado grande", error);
            Assert.Contains("30 MB", error);
            // Justo por debajo del tope pasa.
            Assert.Null(Descomprimido(ImportacionZipBombTests.Zip(("xl/worksheets/sheet1.xml", 29 * MB))));
        }

        [Fact]
        public void Descomprimido_ZipQueMienteSuTamano_SeCuentanLosBytesReales()
        {
            const int MB = 1024 * 1024;
            byte[] zip = ImportacionZipBombTests.Zip(("xl/worksheets/sheet1.xml", 3 * MB));
            byte[] mentiroso = ImportacionZipBombTests.MentirTamano(zip, 10);   // dice 10 bytes
            Assert.Contains("demasiado grande", Descomprimido(mentiroso, tope: 1 * MB));
            Assert.Null(Descomprimido(zip, tope: 4 * MB));
        }

        [Fact]
        public void Descomprimido_TamanoDeclaradoEnorme_SeRechazaSinDescomprimir()
        {
            byte[] zip = ImportacionZipBombTests.Zip(("xl/worksheets/sheet1.xml", 1024));
            Assert.Contains("demasiado grande", Descomprimido(ImportacionZipBombTests.MentirTamano(zip, uint.MaxValue)));
        }

        [Fact]
        public void Descomprimido_ZipDanado_DiceQueEstaDanado()
        {
            byte[] roto = { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            Assert.Contains("dañado", Descomprimido(roto));
        }

        [Theory]
        [InlineData(131, 62)]       // STATUS GENERAL de octubre 2026 (BJ131)
        [InlineData(20000, 150)]    // justo en los topes
        public void Dimensiones_DentroDelTope(int filas, int columnas)
        {
            Assert.Null(LimitesImportacionSeguimiento.ValidarDimensiones(filas, columnas));
        }

        [Fact]
        public void Dimensiones_FueraDelTope_DiceQueHacer()
        {
            string filas = LimitesImportacionSeguimiento.ValidarDimensiones(1048576, 62);
            Assert.Contains("1,048,576 filas", filas);
            Assert.Contains("Divida el archivo", filas);

            string columnas = LimitesImportacionSeguimiento.ValidarDimensiones(131, 16384);
            Assert.Contains("16,384 columnas", columnas);
            Assert.Contains("a la derecha", columnas);
        }

        [Fact]
        public void LimiteDeEnvio_DeLaPagina_DejaPasarElArchivoMaximo()
        {
            // El maxRequestLength de la página (KB) tiene que dejar pasar el archivo de 10 MB más el
            // resto del formulario; si no, ASP.NET corta antes y el usuario nunca ve el mensaje del tope.
            string raiz = Integracion.BaseDePruebas.RaizRepo;
            var web = XDocument.Load(Path.Combine(raiz, "WebSGV", "Web.config"));
            XElement ubicacion = null;
            foreach (var l in web.Root.Elements("location"))
                if ((string)l.Attribute("path") == "Views/Exportacion/ImportarSeguimiento.aspx") ubicacion = l;
            Assert.NotNull(ubicacion);
            int kb = (int)ubicacion.Element("system.web").Element("httpRuntime").Attribute("maxRequestLength");
            Assert.True(kb * 1024L > LimitesImportacionSeguimiento.MaxBytes + 1024L * 1024L);
        }
    }
}
