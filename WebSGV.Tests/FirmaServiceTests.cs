using System.IO;
using System.Web;
using WebSGV.Services;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-7: auxiliares de <see cref="FirmaService"/> sin BD (lo que va al SP y a la constancia del
    /// PDF). Las firmas mismas se prueban con BD en FlujoDespachoLiquidacionTests.FirmaService.
    /// </summary>
    public class FirmaServiceTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]
        [InlineData("DNI 12345678", "12345678")]
        [InlineData("CE  001234567 ", "001234567")]   // lo que sigue al primer espacio, sin espacios
        [InlineData("12345678", "12345678")]
        [InlineData(" 12345678 ", "12345678")]
        [InlineData("DNI", "DNI")]
        public void SoloNumero_QuitaElTipoDeDocumento(string documento, string esperado)
        {
            Assert.Equal(esperado, FirmaService.SoloNumero(documento));
        }

        [Theory]
        [InlineData(null, 3, null)]
        [InlineData("", 3, "")]
        [InlineData("abc", 3, "abc")]
        [InlineData("ab", 3, "ab")]
        [InlineData("abcd", 3, "abc")]
        public void Recortar_CortaSoloLoQueExcede(string texto, int maximo, string esperado)
        {
            Assert.Equal(esperado, FirmaService.Recortar(texto, maximo));
        }

        [Fact]
        public void Recortar_UserAgentLargo_QuedaEn500()
        {
            Assert.Equal(new string('u', 500), FirmaService.Recortar(new string('u', 501), 500));
        }

        [Theory]
        [InlineData(EtapaFirma.Declaracion, TextosFirma.Declaracion)]
        [InlineData(EtapaFirma.Revision, TextosFirma.Revision)]
        [InlineData(EtapaFirma.Conformidad, TextosFirma.Conformidad)]
        public void SinPrefijoOrden_DevuelveElTextoQueAceptoElFirmante(string etapa, string texto)
        {
            Assert.Equal(texto, FirmaService.SinPrefijoOrden(TextosFirma.Consentimiento(etapa, "OV-2026-000123")));
            // Con el número vacío también (queda "Orden de viaje N.° . texto").
            Assert.Equal(texto, FirmaService.SinPrefijoOrden(TextosFirma.Consentimiento(etapa, null)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Consentimiento de prueba")]                       // firmas del sistema anterior
        [InlineData("orden de viaje N.° OV-1. Texto")]                  // distingue mayúsculas
        [InlineData("Orden de viaje N.° OV-2026-000123 sin punto")]     // prefijo sin ". " que lo cierre
        public void SinPrefijoOrden_SinElPrefijo_NoCambiaElTexto(string texto)
        {
            Assert.Equal(texto, FirmaService.SinPrefijoOrden(texto));
        }

        [Fact]
        public void SinPrefijoOrden_CortaEnElPrimerPuntoYEspacioTrasElNumero()
        {
            Assert.Equal("Uno. Dos.", FirmaService.SinPrefijoOrden("Orden de viaje N.° OV.7. Uno. Dos."));
        }

        [Fact]
        public void UrlVerificacion_SinUrlPublicaNiRequest_EsNull()
        {
            // Las pruebas no tienen App.UrlPublica ni HttpContext: el PDF sale sin enlace de verificación.
            var anterior = HttpContext.Current;
            try
            {
                HttpContext.Current = null;
                Assert.Null(FirmaService.UrlVerificacion());
            }
            finally { HttpContext.Current = anterior; }
        }

        [Theory]
        [InlineData("https://localhost:44390/Views/DashboardConductor.aspx?x=1", "https://localhost:44390/Views/VerificarDocumento.aspx")]
        [InlineData("http://sgv.ejemplo.pe/Views/LiquidacionesPendientes.aspx", "http://sgv.ejemplo.pe/Views/VerificarDocumento.aspx")]
        public void UrlVerificacion_SinUrlPublica_UsaElHostDelRequest(string urlRequest, string esperada)
        {
            var anterior = HttpContext.Current;
            try
            {
                HttpContext.Current = new HttpContext(new HttpRequest("", urlRequest, ""), new HttpResponse(TextWriter.Null));
                // Fuera del sitio no hay ruta de la aplicación: se usa la ruta fija de la página.
                Assert.Equal(esperada, FirmaService.UrlVerificacion());
            }
            finally { HttpContext.Current = anterior; }
        }
    }
}
