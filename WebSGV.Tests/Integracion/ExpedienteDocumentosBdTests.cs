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
