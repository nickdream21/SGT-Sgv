using System.Collections.Generic;
using System.Linq;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// Reglas puras de la factura y la CPIC en "Editar lote" y "Factura y CPIC por despacho" (bloque 4, tarea 11).
    /// El error que se corrige: el panel de la CPIC se ocultaba en la carga nacional y al guardar se desvinculaba
    /// la CPIC de todos los despachos del lote.
    /// </summary>
    public class EdicionDocumentosLoteTests
    {
        [Theory]
        //           tipo        intl   tieneCpic perfil    fact cpic  -> factura cpic  varios
        [InlineData("CARGA",    true,  false,    "PEDIDO", 1,   0,      true,   true,  false)]
        [InlineData("DESCARGA", true,  true,     "PEDIDO", 0,   1,      false,  true,  false)]
        [InlineData("CARGA",    false, false,    "PEDIDO", 1,   0,      true,   false, false)]
        // Carga nacional con CPIC (Vitapro C0 que comparte la CPIC con su descarga): ahora se ve y no se pierde.
        [InlineData("CARGA",    false, true,     "PEDIDO", 1,   1,      true,   true,  false)]
        // Tercero que cruza (perfil CPIC) aunque el lote aún no tenga CPIC.
        [InlineData("CARGA",    false, false,    "CPIC",   0,   0,      true,   true,  false)]
        [InlineData("DESCARGA", false, false,    "PEDIDO", 0,   0,      false,  false, false)]
        [InlineData("",         true,  true,     "PEDIDO", 1,   1,      false,  false, false)]
        // Más de una factura o CPIC (Novopan): no se editan aquí.
        [InlineData("CARGA",    true,  true,     "CPIC",   2,   2,      false,  false, true)]
        [InlineData("CARGA",    true,  true,     "CPIC",   1,   2,      false,  false, true)]
        [InlineData("CARGA",    false, false,    "PEDIDO", 2,   0,      false,  false, true)]
        public void Paneles_segun_tipo_ambito_y_documentos(string tipo, bool intl, bool tieneCpic, string perfil,
            int facturas, int cpics, bool factura, bool cpic, bool varios)
        {
            var p = EdicionDocumentosLote.Paneles(tipo, intl, tieneCpic, perfil, facturas, cpics);

            Assert.Equal(factura, p.MostrarFactura);
            Assert.Equal(cpic, p.MostrarCpic);
            Assert.Equal(varios, p.VariosDocumentos);
        }

        [Theory]
        [InlineData(true, "0003959", true)]
        [InlineData(true, "  ", false)]
        [InlineData(true, null, false)]
        [InlineData(false, "0003959", false)]   // panel oculto: no se escribe (ni se desvincula)
        public void Gestionar_solo_con_panel_visible_y_numero(bool visible, string numero, bool esperado) =>
            Assert.Equal(esperado, EdicionDocumentosLote.Gestionar(visible, numero));

        [Fact]
        public void Documentos_distintos_por_id_con_cuantos_despachos_los_llevan()
        {
            var despachos = new List<DocumentoDespachoLote>
            {
                new DocumentoDespachoLote { IdDespacho = 1, IdFactura = 485, NumeroFactura = "001-098-485", FacturaConPdf = true, IdCPIC = 185, NumeroCPIC = "0000185" },
                new DocumentoDespachoLote { IdDespacho = 2, IdFactura = 485, NumeroFactura = "001-098-485", FacturaConPdf = true, IdCPIC = 185, NumeroCPIC = "0000185" },
                new DocumentoDespachoLote { IdDespacho = 3, IdFactura = 484, NumeroFactura = "001-098-484", IdCPIC = 184, NumeroCPIC = "0000184", CpicConPdf = true },
                new DocumentoDespachoLote { IdDespacho = 4 }
            };

            var docs = EdicionDocumentosLote.DocumentosDistintos(despachos);

            Assert.Equal(new[] { "FACTURA:484", "FACTURA:485", "CPIC:184", "CPIC:185" }, docs.Select(d => d.Clave).ToArray());
            Assert.Equal(new[] { 1, 2, 1, 2 }, docs.Select(d => d.Despachos).ToArray());
            Assert.Equal(new[] { false, true, true, false }, docs.Select(d => d.ConPdf).ToArray());
            Assert.Empty(EdicionDocumentosLote.DocumentosDistintos(null));
        }

        [Fact]
        public void Marcados_solo_los_del_lote_sin_repetir()
        {
            Assert.Equal(new[] { 2, 5 }, EdicionDocumentosLote.MarcadosDelLote(new[] { 5, 2, 99, 5 }, new[] { 1, 2, 5 }));
            Assert.Empty(EdicionDocumentosLote.MarcadosDelLote(new[] { 7 }, new[] { 1 }));
            Assert.Empty(EdicionDocumentosLote.MarcadosDelLote(null, null));
        }

        [Theory]
        [InlineData("FACTURA:12", true, "FACTURA", 12)]
        [InlineData("CPIC:34", true, "CPIC", 34)]
        [InlineData("CPIC:0", false, null, 0)]
        [InlineData("GUIA:3", false, null, 0)]
        [InlineData("CPIC:x", false, null, 0)]
        [InlineData("CPIC:1:2", false, null, 0)]
        [InlineData(null, false, null, 0)]
        public void Leer_clave_de_documento(string clave, bool ok, string tipo, int id)
        {
            Assert.Equal(ok, EdicionDocumentosLote.LeerClave(clave, out string t, out int i));
            Assert.Equal(tipo, t);
            if (ok) Assert.Equal(id, i);
        }

        [Theory]
        //          panel  quitar  numero     -> accion
        [InlineData(false, false, "F001-1",   AccionDocumentoLote.NADA)]
        [InlineData(false, true,  "F001-1",   AccionDocumentoLote.NADA)]      // panel oculto: nada, aunque llegue marcada
        [InlineData(true,  false, "F001-1",   AccionDocumentoLote.GESTIONAR)]
        [InlineData(true,  false, "  ",       AccionDocumentoLote.NADA)]
        [InlineData(true,  false, null,       AccionDocumentoLote.NADA)]
        [InlineData(true,  true,  "F001-1",   AccionDocumentoLote.QUITAR)]    // Quitar gana al número
        [InlineData(true,  true,  "",         AccionDocumentoLote.QUITAR)]
        public void Accion_segun_panel_casilla_y_numero(bool panel, bool quitar, string numero, AccionDocumentoLote esperada)
        {
            Assert.Equal(esperada, EdicionDocumentosLote.Accion(panel, quitar, numero));
        }
    }
}
