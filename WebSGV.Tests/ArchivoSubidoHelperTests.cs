using System.Linq;
using System.Text;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Validación común de archivos subidos (tarea 14): vacío, tamaño, extensión y que el contenido
    /// sea de verdad del tipo que dice la extensión (firma de los primeros bytes).
    /// </summary>
    public class ArchivoSubidoHelperTests
    {
        private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n%âãÏÓ\n1 0 obj\n");
        private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D };
        private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46 };
        private static readonly byte[] Zip = { 0x50, 0x4B, 0x03, 0x04, 0x14, 0, 6, 0 };
        private static readonly byte[] Ole = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        private static readonly byte[] Exe = { 0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0 };   // MZ

        private const long Max = ArchivoSubidoHelper.MaxDocumento;

        private static string Validar(string nombre, byte[] contenido, long? largo = null) =>
            ArchivoSubidoHelper.Validar(nombre, largo ?? contenido.Length, contenido, Max, ArchivoSubidoHelper.ExtensionesDocumento);

        [Fact]
        public void PdfValido_SeAcepta()
        {
            Assert.Null(Validar("factura.pdf", Pdf));
            Assert.Null(Validar("FACTURA.PDF", Pdf));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Pdf, ArchivoSubidoHelper.TipoReal(Pdf));
        }

        [Fact]
        public void Pdf_ConBasuraAntesDeLaFirmaDentroDelPrimerKb_SeAcepta()
        {
            byte[] contenido = Enumerable.Repeat((byte)0x20, 900).Concat(Pdf).ToArray();
            Assert.True(ArchivoSubidoHelper.EsPdf(contenido));
            Assert.Null(Validar("escaneado.pdf", contenido));
        }

        [Fact]
        public void Pdf_ConLaFirmaDespuesDelPrimerKb_SeRechaza()
        {
            byte[] contenido = Enumerable.Repeat((byte)0x20, ArchivoSubidoHelper.BytesCabecera).Concat(Pdf).ToArray();
            Assert.False(ArchivoSubidoHelper.EsPdf(contenido));
        }

        [Fact]
        public void PuntoPdf_ConUnEjecutableOTexto_SeRechaza()
        {
            Assert.Equal("el archivo no es un PDF (está dañado o le cambiaron la extensión)", Validar("factura.pdf", Exe));
            Assert.NotNull(Validar("factura.pdf", Encoding.UTF8.GetBytes("hola, esto no es un PDF")));
            Assert.NotNull(Validar("factura.pdf", Encoding.ASCII.GetBytes("%PDF")));   // sin el guion
        }

        [Fact]
        public void Png_RenombradoAPdf_SeRechaza()
        {
            Assert.Contains("no es un PDF", Validar("factura.pdf", Png));
            Assert.Contains("no es un PNG", Validar("foto.png", Pdf));
            Assert.Contains("no es un JPG", Validar("foto.jpg", Pdf));
            Assert.Contains("no es un JPG", Validar("foto.jpeg", Exe));
        }

        [Fact]
        public void Imagenes_Word_Validos()
        {
            Assert.Null(Validar("foto.png", Png));
            Assert.Null(Validar("foto.jpg", Jpeg));
            Assert.Null(Validar("foto.jpeg", Jpeg));
            Assert.Null(Validar("cpic.docx", Zip));
            Assert.Null(Validar("cpic.doc", Ole));
            Assert.Contains("no es un Word .docx", Validar("cpic.docx", Ole));
            Assert.Contains("no es un PDF", Validar("factura.pdf", Jpeg));
        }

        [Fact]
        public void Imagenes_ConLaFirmaDeLaOtraImagen_SeAceptan()
        {
            // PNG guardado como .jpg (y al revés): es una foto igual.
            Assert.Null(Validar("foto.jpg", Png));
            Assert.Null(Validar("foto.jpeg", Png));
            Assert.Null(Validar("foto.png", Jpeg));
        }

        [Fact]
        public void TipoReal_ReconoceCadaFirma()
        {
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Png, ArchivoSubidoHelper.TipoReal(Png));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Jpeg, ArchivoSubidoHelper.TipoReal(Jpeg));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Zip, ArchivoSubidoHelper.TipoReal(Zip));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Ole, ArchivoSubidoHelper.TipoReal(Ole));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Desconocido, ArchivoSubidoHelper.TipoReal(Exe));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Desconocido, ArchivoSubidoHelper.TipoReal(new byte[0]));
            Assert.Equal(ArchivoSubidoHelper.TipoArchivo.Desconocido, ArchivoSubidoHelper.TipoReal(null));
            Assert.True(ArchivoSubidoHelper.EsZip(Zip));
            Assert.False(ArchivoSubidoHelper.EsZip(Pdf));
        }

        [Fact]
        public void Vacio_SeRechaza()
        {
            Assert.Equal("el archivo está vacío", Validar("factura.pdf", new byte[0]));
            Assert.Equal("el archivo está vacío", ArchivoSubidoHelper.Validar("factura.pdf", 0, null, Max, ".pdf"));
        }

        [Fact]
        public void Tamano_UnByteSobreElTope_SeRechaza_ElTopeExacto_SeAcepta()
        {
            Assert.Null(Validar("factura.pdf", Pdf, Max));
            Assert.Equal("el archivo supera el tamaño máximo permitido (10 MB)", Validar("factura.pdf", Pdf, Max + 1));
        }

        [Fact]
        public void Extension_NoPermitida_SeRechazaConLaLista()
        {
            Assert.Equal("tipo de archivo no permitido. Use PDF, DOC, DOCX, JPG o PNG", Validar("virus.exe", Exe));
            Assert.Equal("tipo de archivo no permitido. Use PDF o ZIP",
                ArchivoSubidoHelper.Validar("hoja.xlsx", Zip.Length, Zip, Max, ".pdf", ".zip"));
            Assert.Equal("tipo de archivo no permitido. Use PDF, JPG o PNG",
                ArchivoSubidoHelper.Validar("sin_extension", Pdf.Length, Pdf, Max, ArchivoSubidoHelper.ExtensionesPdfOImagen));
        }

        [Fact]
        public void Constantes_DeLaTarea14()
        {
            Assert.Equal(10 * 1024 * 1024, ArchivoSubidoHelper.MaxDocumento);
            Assert.Equal(20 * 1024 * 1024, ArchivoSubidoHelper.MaxPdfImportacion);
            Assert.Equal(5 * 1024 * 1024, ArchivoSubidoHelper.MaxWord);
            Assert.Equal("10 MB", ArchivoSubidoHelper.TextoTamano(ArchivoSubidoHelper.MaxDocumento));
        }

        [Fact]
        public void LeerCabecera_LeeHastaUnKbYDejaElFlujoAlInicio()
        {
            byte[] contenido = Enumerable.Range(0, 3000).Select(i => (byte)i).ToArray();
            using (var ms = new System.IO.MemoryStream(contenido))
            {
                ms.Position = 500;
                byte[] cabecera = ArchivoSubidoHelper.LeerCabecera(ms);
                Assert.Equal(ArchivoSubidoHelper.BytesCabecera, cabecera.Length);
                Assert.Equal(contenido.Take(ArchivoSubidoHelper.BytesCabecera), cabecera);
                Assert.Equal(0, ms.Position);
            }
            using (var corto = new System.IO.MemoryStream(Pdf))
                Assert.Equal(Pdf, ArchivoSubidoHelper.LeerCabecera(corto));
        }
    }
}
