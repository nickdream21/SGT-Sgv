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

        private static System.Func<string> Contador()
        {
            int n = 0;
            return () => "k" + (++n);
        }

        [Fact]
        public void Registro_MuchosDocumentos_LosPrimerosSiguenAbriendo()
        {
            // Facturas de flete muestra 68 enlaces: antes, al llegar a 50, se perdían todos.
            var reg = new DocumentoHelper.RegistroDocumentos(1000);
            var nueva = Contador();
            string primera = reg.Registrar("~/Uploads/FacturasFlete/2026/f1.pdf", nueva);
            for (int i = 2; i <= 68; i++) reg.Registrar($"~/Uploads/FacturasFlete/2026/f{i}.pdf", nueva);

            Assert.Equal("~/Uploads/FacturasFlete/2026/f1.pdf", reg.Ruta(primera));
            Assert.Equal(68, reg.Cantidad);
        }

        [Fact]
        public void Registro_MismaRuta_ConservaLaLlave()
        {
            var reg = new DocumentoHelper.RegistroDocumentos(1000);
            var nueva = Contador();
            string a = reg.Registrar("~/Uploads/CPIC/x.pdf", nueva);
            string b = reg.Registrar("~/Uploads/cpic/X.pdf", nueva);   // recarga de la página / otra pestaña
            Assert.Equal(a, b);
            Assert.Equal(1, reg.Cantidad);
        }

        [Fact]
        public void Registro_AlLlegarAlMaximo_SaleSoloLaMasAntigua()
        {
            var reg = new DocumentoHelper.RegistroDocumentos(3);
            var nueva = Contador();
            string k1 = reg.Registrar("~/Uploads/a.pdf", nueva);
            string k2 = reg.Registrar("~/Uploads/b.pdf", nueva);
            string k3 = reg.Registrar("~/Uploads/c.pdf", nueva);
            string k4 = reg.Registrar("~/Uploads/d.pdf", nueva);

            Assert.Null(reg.Ruta(k1));
            Assert.Equal("~/Uploads/b.pdf", reg.Ruta(k2));
            Assert.Equal("~/Uploads/c.pdf", reg.Ruta(k3));
            Assert.Equal("~/Uploads/d.pdf", reg.Ruta(k4));
            Assert.Null(reg.Ruta("no-existe"));
            Assert.Null(reg.Ruta(null));
        }
    }
}
