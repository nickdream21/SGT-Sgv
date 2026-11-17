using System;
using System.IO;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Facturas;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Bloque 4, tarea 16: el PDF de una factura de flete nunca pisa otro con el mismo nombre, y el estado del
    /// manifiesto de un lote sale de sus conteos (sin una consulta por lote).
    /// </summary>
    public class LimpiezaBloque4Tests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 11, 17, 9, 5, 7);

        [Fact]
        public void Nombres_candidatos_base_luego_fecha_hora_luego_contador()
        {
            var nombres = FacturaFleteService.NombresCandidatos("E001-18645", ".pdf", Ahora).Take(4).ToList();
            Assert.Equal(new[] { "E001-18645.pdf", "E001-18645_20261117090507.pdf",
                                 "E001-18645_20261117090507_2.pdf", "E001-18645_20261117090507_3.pdf" }, nombres);
        }

        [Fact]
        public void Guardar_no_sobrescribe_el_archivo_existente()
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "sgv-pruebas-ff-" + Guid.NewGuid().ToString("N"));
            try
            {
                byte[] primero = { 1, 2, 3 }, segundo = { 4, 5 }, tercero = { 6 };
                Assert.Equal("E001-1.pdf", FacturaFleteService.GuardarSinSobrescribir(carpeta, "E001-1", ".pdf", primero, Ahora));
                Assert.Equal("E001-1_20261117090507.pdf", FacturaFleteService.GuardarSinSobrescribir(carpeta, "E001-1", ".pdf", segundo, Ahora));
                Assert.Equal("E001-1_20261117090507_2.pdf", FacturaFleteService.GuardarSinSobrescribir(carpeta, "E001-1", ".pdf", tercero, Ahora));

                Assert.Equal(primero, File.ReadAllBytes(Path.Combine(carpeta, "E001-1.pdf")));
                Assert.Equal(segundo, File.ReadAllBytes(Path.Combine(carpeta, "E001-1_20261117090507.pdf")));
                Assert.Equal(tercero, File.ReadAllBytes(Path.Combine(carpeta, "E001-1_20261117090507_2.pdf")));
                Assert.Equal(3, Directory.GetFiles(carpeta).Length);
            }
            finally
            {
                if (Directory.Exists(carpeta)) Directory.Delete(carpeta, true);
            }
        }

        [Theory]
        [InlineData(3, 0, false, false, "N/A")]       // nacional
        [InlineData(3, 3, true, true, "N/A")]         // anulado
        [InlineData(0, 0, true, false, "N/A")]        // sin despachos
        [InlineData(3, 3, true, false, "Completo")]
        [InlineData(3, 0, true, false, "Pendiente")]
        [InlineData(3, 1, true, false, "Parcial 1/3")]
        public void Estado_del_manifiesto(int total, int completos, bool internacional, bool anulado, string esperado) =>
            Assert.Equal(esperado, ListaDespachosService.EstadoManifiesto(total, completos, internacional, anulado));

        [Fact]
        public void Estado_del_manifiesto_de_un_lote_sin_planta_es_NA()
        {
            var lote = new LoteRegistrado { EsInternacional = true, EstadoLote = "ACTIVO", CantidadDespachos = 2, IdPlanta = 0 };
            Assert.Equal("N/A", ListaDespachosService.EstadoManifiesto(lote));
            lote.IdPlanta = 5;
            Assert.Equal("Pendiente", ListaDespachosService.EstadoManifiesto(lote));
            lote.DespachosManifiestoCompleto = 2;
            Assert.Equal("Completo", ListaDespachosService.EstadoManifiesto(lote));
        }
    }
}
