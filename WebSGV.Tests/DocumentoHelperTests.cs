using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests
{
    public class DocumentoHelperTests
    {
        private const string Raiz = @"C:\sitio\Uploads\";

        [Theory]
        [InlineData(@"C:\sitio\Uploads\CPIC\2026\10\CPIC_123.pdf")]
        [InlineData(@"C:\sitio\uploads\Factura\x.pdf")]            // sin distinguir mayúsculas
        [InlineData(@"C:\sitio\Uploads\CPIC\..\Factura\x.pdf")]    // ".." que sigue dentro
        public void EstaDentroDe_RutasDentroDeUploads_True(string ruta)
        {
            Assert.True(DocumentoHelper.EstaDentroDe(ruta, Raiz));
        }

        [Theory]
        [InlineData(@"C:\sitio\Uploads\..\Web.config")]
        [InlineData(@"C:\sitio\App_Data\OrdenesViaje\x.pdf")]
        [InlineData(@"C:\sitio\UploadsFalso\x.pdf")]               // prefijo parecido, otra carpeta
        [InlineData(@"C:\sitio\Uploads")]                           // la raíz misma no es un archivo dentro
        [InlineData("")]
        [InlineData(null)]
        public void EstaDentroDe_RutasFuera_False(string ruta)
        {
            Assert.False(DocumentoHelper.EstaDentroDe(ruta, Raiz));
        }

        [Theory]
        [InlineData("1234567", "1234567")]
        [InlineData("F001-123", "F001-123")]
        [InlineData("a/../..", "a______")]
        [InlineData(@"..\..\Web", "______Web")]
        [InlineData("F001 - 123", "F001_-_123")]
        [InlineData("", "SIN_NUMERO")]
        [InlineData(null, "SIN_NUMERO")]
        public void NombreSeguro_SoloLetrasDigitosGuiones(string entrada, string esperado)
        {
            Assert.Equal(esperado, DocumentoHelper.NombreSeguro(entrada));
        }
    }
}
