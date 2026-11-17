using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Tarea 17: la columna "Factura / CPIC" de la lista de lotes muestra todas las facturas y CPIC del lote
    /// (Novopan lleva dos el mismo día), no solo el MAX.
    /// </summary>
    public class LotesVariosDocumentosTests
    {
        [Fact]
        public void NumerosDistintos_QuitaRepetidosYOrdena()
        {
            Assert.Equal(new[] { "001-098-000331046", "001-098-000331060" },
                LoteRegistrado.NumerosDistintos("001-098-000331060|001-098-000331046|001-098-000331060| ", "x"));
        }

        [Fact]
        public void NumerosDistintos_SinColumnaNueva_UsaElMax()
        {
            // BD con la versión anterior del SP: NumerosFactura no viene.
            Assert.Equal(new[] { "F001-1" }, LoteRegistrado.NumerosDistintos(null, "F001-1"));
            Assert.Empty(LoteRegistrado.NumerosDistintos(null, ""));
        }

        [Fact]
        public void TextoCorto_UnoDosYNinguno()
        {
            Assert.Equal("—", LoteRegistrado.TextoCorto(new string[0]));
            Assert.Equal("A", LoteRegistrado.TextoCorto(new[] { "A" }));
            Assert.Equal("A + 1 más", LoteRegistrado.TextoCorto(new[] { "A", "B" }));
            Assert.Equal("A + 2 más", LoteRegistrado.TextoCorto(new[] { "A", "B", "C" }));
            Assert.Equal("A, B", LoteRegistrado.ListaCompleta(new[] { "A", "B" }));
        }

        [Fact]
        public void Lote_ConDosFacturas_LasDevuelveAmbas()
        {
            var lote = new LoteRegistrado { NumeroFactura = "B", NumerosFactura = "A|B|B|A", NumeroCPIC = "2", NumerosCPIC = "1|2" };
            Assert.Equal(new[] { "A", "B" }, lote.Facturas);
            Assert.Equal(new[] { "1", "2" }, lote.Cpics);
        }

        /// <summary>Solo lectura: en la BD de pruebas, cada lote lista tantas facturas y CPIC como cuenta el SP.</summary>
        [FactBD]
        public void Lotes_ListanTodasSusFacturasYCpic()
        {
            BaseDePruebas.Preparar();
            var lotes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null);
            Assert.NotEmpty(lotes);
            foreach (var l in lotes)
            {
                Assert.True(l.CantidadFacturas == l.Facturas.Count, $"Lote {l.IdLoteVirtual}: {l.CantidadFacturas} facturas y lista {string.Join(", ", l.Facturas)}");
                Assert.True(l.CantidadCPIC == l.Cpics.Count, $"Lote {l.IdLoteVirtual}: {l.CantidadCPIC} CPIC y lista {string.Join(", ", l.Cpics)}");
            }
        }
    }
}
