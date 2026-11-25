using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WebSGV.Helpers;
using WebSGV.Services;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-6: decide si un PDF firmado guardado se entrega (huella igual) o se declara alterado o
    /// perdido, y que un PDF firmado nunca se regenera. Sin BD: la lectura y el incidente se inyectan.
    /// </summary>
    public class ArchivoOrdenViajeServiceTests
    {
        private const string Ruta = @"C:\archivo\OV_1.pdf";
        private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7 orden firmada");
        private static readonly string HuellaPdf = HashHelper.ComputeSha256(Pdf);

        private readonly List<string> _incidentes = new List<string>();
        private readonly List<string> _leidas = new List<string>();

        private PdfArchivado Resolver(DocumentoFirmado doc, string rutaFisica, byte[] contenido)
            => ArchivoOrdenViajeService.Resolver(doc, rutaFisica,
                r => { _leidas.Add(r); return contenido; },
                d => _incidentes.Add(d));

        private static DocumentoFirmado Firmado(string hash, string ruta = "~/App_Data/OrdenesViaje/OV_1.pdf")
            => new DocumentoFirmado { Firmado = true, Hash = hash, RutaRelativa = ruta };

        // ---------- Firmado ----------

        [Fact]
        public void Firmado_HuellaIgual_DevuelveLosBytesSinIncidente()
        {
            var r = Resolver(Firmado(HuellaPdf), Ruta, Pdf);

            Assert.True(r.Ok);
            Assert.Same(Pdf, r.Bytes);
            Assert.Equal(HuellaPdf, r.Hash);
            Assert.Empty(_incidentes);
            Assert.Equal(new[] { Ruta }, _leidas);
        }

        [Fact]
        public void Firmado_HuellaEnMayusculas_SeAceptaIgual()
        {
            var r = Resolver(Firmado(HuellaPdf.ToUpperInvariant()), Ruta, Pdf);

            Assert.True(r.Ok);
            Assert.Equal(HuellaPdf, r.Hash);
            Assert.Empty(_incidentes);
        }

        [Fact]
        public void Firmado_HuellaDistinta_Alterado_ConIncidente()
        {
            string otra = HashHelper.ComputeSha256(Encoding.ASCII.GetBytes("otro"));
            var r = Resolver(Firmado(otra), Ruta, Pdf);

            Assert.False(r.Ok);
            Assert.Equal(ArchivoOrdenViajeService.MensajeAlterado, r.Error);
            Assert.Null(r.Bytes);
            string detalle = Assert.Single(_incidentes);
            Assert.Equal($"El PDF firmado ~/App_Data/OrdenesViaje/OV_1.pdf no coincide con su huella (registrada {otra}, archivo {HuellaPdf})", detalle);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Firmado_SinHuellaRegistrada_Alterado_ConIncidente(string hash)
        {
            var r = Resolver(Firmado(hash), Ruta, Pdf);

            Assert.Equal(ArchivoOrdenViajeService.MensajeAlterado, r.Error);
            Assert.Null(r.Bytes);
            Assert.Contains("(registrada , archivo " + HuellaPdf + ")", Assert.Single(_incidentes));
        }

        [Fact]
        public void Firmado_HuellaConEspacios_NoCoincide_Alterado()
        {
            // HashesIguales compara largo y caracteres: un espacio sobrante cuenta como alteración.
            var r = Resolver(Firmado(HuellaPdf + " "), Ruta, Pdf);

            Assert.Equal(ArchivoOrdenViajeService.MensajeAlterado, r.Error);
            Assert.Single(_incidentes);
        }

        [Fact]
        public void Firmado_ArchivoVacio_SeCompruebaSuHuella()
        {
            // Un archivo de 0 bytes existe: no es "no encontrado", es una huella distinta.
            var r = Resolver(Firmado(HuellaPdf), Ruta, new byte[0]);

            Assert.Equal(ArchivoOrdenViajeService.MensajeAlterado, r.Error);
            Assert.Single(_incidentes);
        }

        [Fact]
        public void Firmado_SinArchivo_NoEncontrado_ConIncidente()
        {
            var r = Resolver(Firmado(HuellaPdf), Ruta, null);

            Assert.False(r.Ok);
            Assert.Equal(ArchivoOrdenViajeService.MensajeNoEncontrado, r.Error);
            Assert.Null(r.Bytes);
            Assert.Equal("PDF firmado no encontrado en el archivo: ~/App_Data/OrdenesViaje/OV_1.pdf", Assert.Single(_incidentes));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Firmado_RutaFisicaVacia_NoEncontrado_SinLeer(string rutaFisica)
        {
            var r = Resolver(Firmado(HuellaPdf), rutaFisica, Pdf);

            Assert.Equal(ArchivoOrdenViajeService.MensajeNoEncontrado, r.Error);
            Assert.Empty(_leidas);
            Assert.Single(_incidentes);
        }

        [Fact]
        public void Firmado_SinRutaRegistrada_IncidenteLoDice()
        {
            var r = Resolver(Firmado(HuellaPdf, ruta: null), null, null);

            Assert.Equal(ArchivoOrdenViajeService.MensajeNoEncontrado, r.Error);
            Assert.Equal("PDF firmado no encontrado en el archivo: (sin ruta)", Assert.Single(_incidentes));
        }

        // ---------- Sin firmas ----------

        [Fact]
        public void SinFirmas_Archivado_LoDevuelveSinRegenerarNiIncidente()
        {
            var doc = new DocumentoFirmado { Firmado = false, Hash = null, RutaRelativa = "~/App_Data/OrdenesViaje/OV_2.pdf" };

            var r = Resolver(doc, Ruta, Pdf);

            Assert.NotNull(r);
            Assert.True(r.Ok);
            Assert.Same(Pdf, r.Bytes);
            Assert.Equal(HuellaPdf, r.Hash);
            Assert.Empty(_incidentes);
        }

        [Fact]
        public void SinFirmas_Archivado_NoCompruebaLaHuellaRegistrada()
        {
            // Conducta actual: sin firmas se entrega lo guardado aunque su huella no sea la registrada.
            var doc = new DocumentoFirmado { Firmado = false, Hash = new string('0', 64), RutaRelativa = "x.pdf" };

            var r = Resolver(doc, Ruta, Pdf);

            Assert.True(r.Ok);
            Assert.Equal(HuellaPdf, r.Hash);
            Assert.Empty(_incidentes);
        }

        [Fact]
        public void SinFirmas_SinArchivo_DevuelveNullParaQueSeGenere()
        {
            var doc = new DocumentoFirmado { Firmado = false, RutaRelativa = "~/App_Data/OrdenesViaje/OV_2.pdf" };

            Assert.Null(Resolver(doc, Ruta, null));
            Assert.Null(Resolver(doc, null, Pdf));
            Assert.Empty(_incidentes);
        }

        // ---------- Lectura real ----------

        [Fact]
        public void LeerSiExiste_ArchivoExistente_DevuelveSusBytes_YFaltante_Null()
        {
            string ruta = Path.Combine(Path.GetTempPath(), "sgv_e6_6_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                File.WriteAllBytes(ruta, Pdf);
                Assert.Equal(Pdf, ArchivoOrdenViajeService.LeerSiExiste(ruta));
            }
            finally
            {
                File.Delete(ruta);
            }
            Assert.Null(ArchivoOrdenViajeService.LeerSiExiste(ruta));
        }

        [Fact]
        public void Resolver_ConLecturaReal_ArchivoFirmadoEnDisco()
        {
            string ruta = Path.Combine(Path.GetTempPath(), "sgv_e6_6_" + Guid.NewGuid().ToString("N") + ".pdf");
            try
            {
                File.WriteAllBytes(ruta, Pdf);
                var r = ArchivoOrdenViajeService.Resolver(Firmado(HuellaPdf), ruta,
                    ArchivoOrdenViajeService.LeerSiExiste, d => _incidentes.Add(d));
                Assert.True(r.Ok);
                Assert.Equal(Pdf, r.Bytes);
            }
            finally
            {
                File.Delete(ruta);
            }
            var perdido = ArchivoOrdenViajeService.Resolver(Firmado(HuellaPdf), ruta,
                ArchivoOrdenViajeService.LeerSiExiste, d => _incidentes.Add(d));
            Assert.Equal(ArchivoOrdenViajeService.MensajeNoEncontrado, perdido.Error);
            Assert.Single(_incidentes);
        }

        // ---------- RutaFisica ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RutaFisica_Vacia_Null(string ruta)
        {
            Assert.Null(ArchivoOrdenViajeService.RutaFisica(ruta));
        }

        [Theory]
        [InlineData(@"C:\SGV\App_Data\OrdenesViaje\OV_1.pdf")]
        [InlineData(@"\\servidor\archivo\OV_1.pdf")]
        [InlineData(@"\App_Data\OV_1.pdf")]
        [InlineData("/App_Data/OV_1.pdf")]
        public void RutaFisica_Absoluta_SeDevuelveTalCual(string ruta)
        {
            // Path.IsPathRooted: también "\x" y "/x" (sin unidad) se toman como absolutas.
            Assert.Equal(ruta, ArchivoOrdenViajeService.RutaFisica(ruta));
        }

        [Theory]
        [InlineData("~/App_Data/OrdenesViaje/OV_1.pdf")]
        [InlineData("App_Data/OrdenesViaje/OV_1.pdf")]
        public void RutaFisica_VirtualORelativa_FueraDeIis_Null(string ruta)
        {
            // "~" y relativa pasan por HostingEnvironment.MapPath (la relativa como "~/" + ruta);
            // fuera de IIS no hay entorno de hosting y MapPath devuelve null: se trata como "sin archivo".
            Assert.Null(ArchivoOrdenViajeService.RutaFisica(ruta));
        }

        [Fact]
        public void RutaFisica_CaracteresInvalidos_Null()
        {
            // Path.IsPathRooted lanza con caracteres no válidos; el catch lo convierte en null.
            Assert.Null(ArchivoOrdenViajeService.RutaFisica("App_Data/OV|<1>.pdf"));
        }
    }
}
