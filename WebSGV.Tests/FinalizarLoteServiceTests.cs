using System;
using System.Collections.Generic;
using System.IO;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>Partes puras de "Finalizar lote" todo o nada (tarea 10 del bloque 4).</summary>
    public class FinalizarLoteServiceTests
    {
        private static LoteDespachos Lote(bool internacional = false)
        {
            var lote = new LoteDespachos
            {
                NombreCliente = "VITAPRO", TipoOperacion = "CARGA", EsInternacional = internacional,
                PlantaOperacion = "Planta Trujillo", NumeroPedido = "4400123"
            };
            lote.Documentacion.NumeroFactura = "F001-1";
            lote.Documentacion.NumeroCPIC = "123456";
            return lote;
        }

        [Fact]
        public void Archivos_declarados_que_no_estan_en_disco_bloquean_el_lote()
        {
            var lote = Lote(internacional: true);
            lote.Documentacion.FacturaArchivoRutaTemp = @"C:\Uploads\Factura\_temp\a.pdf";
            lote.Documentacion.CPICArchivoRutaTemp = @"C:\Uploads\CPIC\_temp\b.pdf";
            lote.Conductores.Add(new ConductorLote
            {
                NombreConductor = "JUAN", ManifiestoCruceRutaTemp = @"C:\Uploads\Manifiesto\_temp\c.pdf",
                ManifiestoRegresoRutaTemp = @"C:\Uploads\Manifiesto\_temp\d.pdf"
            });

            var errores = FinalizarLoteService.ArchivosFaltantes(lote, ruta => ruta.EndsWith("b.pdf"));

            Assert.Equal(3, errores.Count);
            Assert.Contains(errores, e => e.Contains("El documento de la factura F001-1 que adjuntó al iniciar el lote ya no está en el servidor. Cancele el lote"));
            Assert.Contains(errores, e => e.StartsWith("JUAN") && e.Contains("manifiesto de cruce"));
            Assert.Contains(errores, e => e.StartsWith("JUAN") && e.Contains("manifiesto de retorno"));
            Assert.DoesNotContain(errores, e => e.Contains("CPIC"));
        }

        [Fact]
        public void Sin_archivos_declarados_no_hay_faltantes()
        {
            var lote = Lote();
            lote.Conductores.Add(new ConductorLote { NombreConductor = "JUAN" });
            Assert.Empty(FinalizarLoteService.ArchivosFaltantes(lote, _ => false));
        }

        [Fact]
        public void Guia_repetida_dentro_del_lote_bloquea()
        {
            var lote = Lote();
            lote.Conductores.Add(new ConductorLote { NombreConductor = "JUAN", GuiaRemitente = "T001-100" });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "PEDRO", GuiaRemitente = " t001-100 " });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "LUIS", GuiaRemitente = "T001-101" });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "ANA" });
            lote.Conductores.Add(new ConductorLote { NombreConductor = "EVA", GuiaRemitente = "  " });

            var errores = FinalizarLoteService.GuiasRepetidasEnLote(lote);

            var error = Assert.Single(errores);
            Assert.Contains("T001-100", error);
            Assert.Contains("JUAN", error);
            Assert.Contains("PEDRO", error);
        }

        [Fact]
        public void La_auditoria_lleva_todos_los_ids()
        {
            var lote = Lote();
            string texto = FinalizarLoteService.DescripcionAuditoria(lote, new List<int> { 501, 502 }, new List<int> { 400 }, 77, 88);

            Assert.Contains("Lote de 3 despacho(s)", texto);
            Assert.Contains("Cliente: VITAPRO", texto);
            Assert.Contains("Planta: Planta Trujillo", texto);
            Assert.Contains("Pedido: 4400123", texto);
            Assert.Contains("Factura: F001-1 (id 77)", texto);
            Assert.Contains("CPIC: 123456 (id 88)", texto);
            Assert.Contains("Despachos creados: 501, 502", texto);
            Assert.Contains("despachos programados 400", texto);
        }

        [Fact]
        public void La_ruta_destino_queda_dentro_de_la_raiz_con_el_nombre_de_siempre()
        {
            string raiz = Path.Combine(Path.GetTempPath(), "sgv_raiz") + Path.DirectorySeparatorChar;
            var a = FinalizarLoteService.RutaDestino(raiz, "Factura", "FACTURA_15", ".pdf", new DateTime(2026, 3, 4, 5, 6, 7));

            Assert.Equal("FACTURA_15_20260304_050607.pdf", a.NombreArchivo);
            Assert.Equal("~/Uploads/Factura/2026/03/FACTURA_15_20260304_050607.pdf", a.RutaVirtual);
            Assert.Equal(Path.Combine(raiz, "Factura", "2026", "03", a.NombreArchivo), a.RutaFisica);
            Assert.StartsWith(raiz, Path.GetFullPath(a.RutaFisica), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void La_ruta_destino_fuera_de_la_raiz_se_rechaza()
        {
            string raiz = Path.Combine(Path.GetTempPath(), "sgv_raiz") + Path.DirectorySeparatorChar;
            Assert.Throws<InvalidOperationException>(() =>
                FinalizarLoteService.RutaDestino(raiz, @"..\..\otra", "FACTURA_15", ".pdf", DateTime.Now));
            Assert.Throws<InvalidOperationException>(() =>
                FinalizarLoteService.RutaDestino(null, "Factura", "FACTURA_15", ".pdf", DateTime.Now));
        }
    }
}
