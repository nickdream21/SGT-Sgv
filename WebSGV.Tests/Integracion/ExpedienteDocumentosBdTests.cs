using System;
using System.IO;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// PDF de las guías (migración 40) y expediente de un pedido contra la BD de pruebas. Lo que escribe va
    /// dentro de una <see cref="TransaccionDePrueba"/>; los archivos, a una carpeta temporal que se borra.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ExpedienteDocumentosBdTests
    {
        public ExpedienteDocumentosBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void Cada_lote_trae_solo_sus_despachos()
        {
            // Un lote sin pedido ("NOPEDIDO") traía también los despachos con pedido del mismo día y planta:
            // el expediente de 3 cargas de prueba sin pedido del 03/11 mostraba el pedido 4400134478 y su factura.
            var lotes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null);
            Assert.NotEmpty(lotes);
            foreach (var lote in lotes)
                Assert.True(lote.CantidadDespachos == ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual).Count,
                    "El lote " + lote.IdLoteVirtual + " no trae los mismos despachos que la bandeja.");
        }

        [FactBD]
        public void Expediente_junta_la_carga_con_el_mci_de_su_descarga()
        {
            // Pedido real de Vitapro (21/10): 6 cargas con factura, CPIC y guías; los 6 MCI están en las descargas.
            var e = ExpedienteDocumentosService.Obtener("1_4400133952_2026-10-21_CARGA_0_2");
            if (e == null) return;   // la BD de pruebas se reconstruyó sin ese pedido

            Assert.Equal("PEDIDO", e.Perfil);
            Assert.Equal(6, e.Filas.Count);
            Assert.Empty(e.DescargasSueltas);
            Assert.All(e.Filas, f => Assert.True(f.IdDescarga.HasValue));
            Assert.All(e.Filas, f => Assert.Contains(f.Manifiestos, m => m.RutaPdf != null));
            Assert.NotNull(e.Factura.Numero);
            Assert.Single(e.Cpics);
            Assert.Equal(0, e.Faltan);

            // El lote de descarga del mismo pedido abre el mismo expediente.
            string loteDescarga = DbHelper.EjecutarEscalar(@"
                SELECT TOP 1 CONCAT(idCliente, '_', numeroPedido, '_', CONVERT(VARCHAR(10), fechaDespacho, 120), '_DESCARGA_',
                                    CAST(esInternacional AS VARCHAR(1)), '_', idPlanta)
                FROM Despachos WHERE activo = 1 AND numeroPedido = '4400133952' AND tipoOperacion = 'DESCARGA'").ToString();
            var desdeDescarga = ExpedienteDocumentosService.Obtener(loteDescarga);
            Assert.Equal(e.Filas.Select(f => f.IdCarga), desdeDescarga.Filas.Select(f => f.IdCarga));
        }

        [FactBD]
        public void Factura_de_flete_y_expediente_se_enlazan_en_ambos_sentidos()
        {
            // Pedido 4400134386 (Vitapro): factura comercial F222-00008581, cobrada en la factura de flete E001-18724.
            var lotes = WebSGV.Services.Facturas.FacturaFleteService.LotesPorReferencia(new[] { "F222-00008581", "NO-EXISTE-1" });
            if (!lotes.TryGetValue("F222-00008581", out string lote)) return;   // la BD de pruebas se reconstruyó sin ese pedido
            Assert.False(lotes.ContainsKey("NO-EXISTE-1"));
            Assert.Contains("_4400134386_", lote);
            Assert.Contains("_CARGA_", lote);

            var e = ExpedienteDocumentosService.Obtener(lote);
            Assert.NotNull(e);
            Assert.Equal("4400134386", e.NumeroPedido);
            Assert.Contains(e.FacturasFlete, f => f.Numero == "E001-18724" && f.RutaPdf != null);
        }

        [FactBD]
        public void Guarda_el_pdf_de_la_guia_y_no_lo_duplica()
        {
            if (!GuiaDocumentoService.TablaExiste) return;
            string raiz = Path.Combine(Path.GetTempPath(), "sgv-guias-" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
            try
            {
                using (new TransaccionDePrueba())
                {
                    object fila = DbHelper.EjecutarEscalar(@"
                        SELECT TOP 1 idDespacho FROM Despachos
                        WHERE activo = 1 AND guiaRemitente LIKE '%/%' ORDER BY idDespacho DESC");
                    if (fila == null) return;
                    int id = Convert.ToInt32(fila);
                    string guia = DbHelper.EjecutarEscalar("SELECT guiaRemitente FROM Despachos WHERE idDespacho = @d", DbHelper.Param("@d", id))
                                          .ToString().Split('/').Last().Trim();
                    DbHelper.EjecutarNonQuery("UPDATE DocumentosGuia SET activo = 0 WHERE idDespacho = @d", DbHelper.Param("@d", id));

                    // La segunda guía de "A / B" también encuentra su despacho.
                    Assert.Contains(id, GuiaDocumentoService.DespachosDeGuia(guia, GuiaDocumentoService.REMITENTE));

                    byte[] pdf = { 0x25, 0x50, 0x44, 0x46, 1, 2, 3 };
                    Assert.Equal("NUEVO", GuiaDocumentoService.Guardar(id, GuiaDocumentoService.REMITENTE, guia, "guia.pdf", pdf, "test", raiz));
                    Assert.Equal("SIN_CAMBIOS", GuiaDocumentoService.Guardar(id, GuiaDocumentoService.REMITENTE, guia, "guia.pdf", pdf, "test", raiz));
                    Assert.Equal("REEMPLAZADO", GuiaDocumentoService.Guardar(id, GuiaDocumentoService.REMITENTE, guia, "guia2.pdf", new byte[] { 0x25, 9 }, "test", raiz));

                    var docs = GuiaDocumentoService.PorDespachos(new[] { id });
                    var suya = Assert.Single(docs, d => d.NumeroGuia == guia);
                    Assert.Equal("guia2.pdf", suya.NombreOriginal);
                    Assert.Contains(guia, GuiaDocumentoService.NumerosConPdf(new[] { guia }, GuiaDocumentoService.REMITENTE));
                    Assert.Equal(2, Directory.GetFiles(raiz, "*.pdf", SearchOption.AllDirectories).Length);
                }
            }
            finally
            {
                if (Directory.Exists(raiz)) Directory.Delete(raiz, true);
            }
        }
    }
}
