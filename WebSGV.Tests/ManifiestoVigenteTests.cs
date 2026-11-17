using System;
using System.Collections.Generic;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Manifiesto vigente (tarea 14 C): al reemplazar un manifiesto la fila anterior queda como historial y
    /// solo se muestra el más reciente de cada despacho y tipo.
    /// </summary>
    public class ManifiestoVigenteTests
    {
        private static DocumentoManifiesto Doc(int id, int idDespacho, string tipo, DateTime subida) =>
            new DocumentoManifiesto { IdDocumentoManifiesto = id, IdDespacho = idDespacho, TipoManifiesto = tipo, FechaSubida = subida };

        [Fact]
        public void Dos_de_cruce_y_uno_de_retorno_dejan_uno_de_cada_tipo_el_mas_reciente()
        {
            var docs = new List<DocumentoManifiesto>
            {
                Doc(10, 5, ManifiestoService.TIPO_CRUCE, new DateTime(2026, 11, 1, 8, 0, 0)),
                Doc(11, 5, ManifiestoService.TIPO_RETORNO, new DateTime(2026, 11, 2, 8, 0, 0)),
                Doc(12, 5, ManifiestoService.TIPO_CRUCE, new DateTime(2026, 11, 3, 8, 0, 0)),
            };

            var vigentes = ManifiestoService.Vigentes(docs);

            Assert.Equal(2, vigentes.Count);
            Assert.Equal(12, Assert.Single(vigentes, d => d.TipoManifiesto == ManifiestoService.TIPO_CRUCE).IdDocumentoManifiesto);
            Assert.Equal(11, Assert.Single(vigentes, d => d.TipoManifiesto == ManifiestoService.TIPO_RETORNO).IdDocumentoManifiesto);
            // Cruce antes que retorno, como la consulta original.
            Assert.Equal(ManifiestoService.TIPO_CRUCE, vigentes[0].TipoManifiesto);
        }

        [Fact]
        public void Cada_despacho_tiene_su_vigente_y_a_igual_fecha_gana_el_id_mayor()
        {
            var misma = new DateTime(2026, 11, 5, 10, 0, 0);
            var docs = new List<DocumentoManifiesto>
            {
                Doc(20, 7, ManifiestoService.TIPO_CRUCE, misma),
                Doc(21, 7, ManifiestoService.TIPO_CRUCE, misma),
                Doc(22, 8, ManifiestoService.TIPO_CRUCE, misma.AddDays(-1)),
            };

            var vigentes = ManifiestoService.Vigentes(docs);

            Assert.Equal(2, vigentes.Count);
            Assert.Equal(21, Assert.Single(vigentes, d => d.IdDespacho == 7).IdDocumentoManifiesto);
            Assert.Equal(22, Assert.Single(vigentes, d => d.IdDespacho == 8).IdDocumentoManifiesto);
        }

        [Fact]
        public void Sin_documentos_devuelve_vacio()
        {
            Assert.Empty(ManifiestoService.Vigentes(null));
            Assert.Empty(ManifiestoService.Vigentes(new List<DocumentoManifiesto>()));
        }
    }
}
