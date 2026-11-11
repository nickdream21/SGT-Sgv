using System;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    public class EstadoDocumentalTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 2);

        [Fact]
        public void SinNadaQueFalte_EsCompleto_YNoTieneAntiguedad()
        {
            var e = new EstadoDocumental { Despachos = 6 };

            Assert.True(e.Completo);
            Assert.Equal("Completo", e.Resumen);
            Assert.Empty(e.Faltantes);
            Assert.Equal("", e.Antiguedad(Hoy.AddDays(-10), Hoy));
            Assert.False(e.Atrasado(Hoy.AddDays(-10), Hoy));
        }

        [Fact]
        public void CargaProgramadaConElWord_EsperaElZip()
        {
            var e = new EstadoDocumental { Despachos = 8, SinPedido = 8, SinGuia = 8, SinFactura = 8 };

            Assert.False(e.Completo);
            Assert.True(e.SinNingunDocumento);
            Assert.Equal("Sin documentos (pedido, factura y guías)", e.Resumen);
        }

        [Fact]
        public void FaltaParcial_DiceCuantosDeCuantos()
        {
            var e = new EstadoDocumental { Despachos = 6, SinGuia = 2, SinPdfFactura = 6 };

            Assert.False(e.SinNingunDocumento);
            Assert.Equal(new[] { "Falta guía (2 de 6)", "Sin PDF de factura" }, e.Faltantes);
            Assert.Equal("Falta guía (2 de 6) · Sin PDF de factura", e.Resumen);
        }

        [Fact]
        public void DescargaInternacional_SoloPideCpic()
        {
            var e = new EstadoDocumental { Despachos = 6, SinCPIC = 6 };

            Assert.Equal("Falta CPIC", e.Resumen);

            e = new EstadoDocumental { Despachos = 6, SinPdfCPIC = 6 };
            Assert.Equal("Sin PDF del CPIC", e.Resumen);
        }

        [Fact]
        public void TodoAlMismoTiempo_EnOrdenPedidoFacturaGuiaPdfCpic()
        {
            var e = new EstadoDocumental { Despachos = 3, SinPedido = 1, SinFactura = 1, SinGuia = 3, SinPdfFactura = 2, SinCPIC = 3, SinPdfCPIC = 0 };

            Assert.Equal(new[] { "Falta pedido (1 de 3)", "Falta factura (1 de 3)", "Falta guía", "Sin PDF de factura (2 de 3)", "Falta CPIC" },
                e.Faltantes);
        }

        [Theory]
        [InlineData(0, "hoy", false)]
        [InlineData(1, "hace 1 día", false)]
        [InlineData(3, "hace 3 días", false)]
        [InlineData(4, "hace 4 días", true)]
        [InlineData(-1, "", false)]
        public void Antiguedad_YAtraso_DesdeLaFechaDelDespacho(int diasAtras, string esperado, bool atrasado)
        {
            var e = new EstadoDocumental { Despachos = 1, SinGuia = 1 };
            DateTime fecha = Hoy.AddDays(-diasAtras).AddHours(5);

            Assert.Equal(esperado, e.Antiguedad(fecha, Hoy.AddHours(20)));
            Assert.Equal(atrasado, e.Atrasado(fecha, Hoy.AddHours(20)));
        }

        [Fact]
        public void DeLote_CopiaLosConteosDelSp()
        {
            var lote = new LoteRegistrado
            {
                DespachosVigentes = 4, DespachosSinPedido = 1, DespachosSinGuia = 2, DespachosSinFactura = 3,
                DespachosSinPdfFactura = 4, DespachosSinCPIC = 1, DespachosSinPdfCPIC = 2
            };

            var e = EstadoDocumental.DeLote(lote);

            Assert.Equal(4, e.Despachos);
            Assert.Equal(1, e.SinPedido);
            Assert.Equal(2, e.SinGuia);
            Assert.Equal(3, e.SinFactura);
            Assert.Equal(4, e.SinPdfFactura);
            Assert.Equal(1, e.SinCPIC);
            Assert.Equal(2, e.SinPdfCPIC);
        }
    
        [Fact]
        public void TerceroQueCruza_EsperaLaFacturaComercialDeLaCpic()
        {
            var e = EstadoDocumental.DeLote(new LoteRegistrado
            {
                DespachosVigentes = 5, DespachosSinFactura = 5, DespachosSinCPIC = 5, PerfilDocumentos = "CPIC"
            });

            Assert.False(e.SinNingunDocumento);   // no espera el ZIP del correo
            Assert.Contains("Falta factura comercial (de la CPIC)", e.Faltantes);
            Assert.Contains("Falta CPIC", e.Faltantes);
        }

        [Fact]
        public void TerceroNacional_EsperaNuestraGreTransportista()
        {
            var e = EstadoDocumental.DeLote(new LoteRegistrado { DespachosVigentes = 1, DespachosSinGuia = 1, PerfilDocumentos = "GUIA" });

            Assert.Equal("Falta GRE transportista", e.Resumen);
        }

        [Fact]
        public void SinManifiesto_NoEstaCompleto_YLoDice()
        {
            var e = EstadoDocumental.DeLote(new LoteRegistrado { DespachosVigentes = 4, DespachosSinManifiesto = 1 });

            Assert.False(e.Completo);
            Assert.Equal("Falta manifiesto (MCI) (1 de 4)", e.Resumen);
        }
    }
}
