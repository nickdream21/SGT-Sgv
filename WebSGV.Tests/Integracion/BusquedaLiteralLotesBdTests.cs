using System;
using System.Collections.Generic;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Solo lectura (bloque 4, tarea 16): las búsquedas de ListaDespachos son literales —"%", "_" y "[" no son
    /// comodines del LIKE en sp_LD_ObtenerLotesRegistrados ni en sp_LD_ObtenerViajesActivos— y el estado del
    /// manifiesto que sale de la columna DespachosManifiestoCompleto es el mismo que el cálculo anterior (una
    /// consulta de ids por lote más los documentos de manifiesto).
    /// </summary>
    public class BusquedaLiteralLotesBdTests
    {
        public BusquedaLiteralLotesBdTests() => BaseDePruebas.Preparar();

        private static int Contar(string sql, string texto) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(sql, DbHelper.Param("@t", texto)));

        /// <summary>Lotes activos cuyo campo contiene el texto tal cual (CHARINDEX no tiene comodines).</summary>
        private static int LotesConTexto(string expresion, string texto) => Contar($@"
            SELECT COUNT(*) FROM (
                SELECT DISTINCT d.idCliente, ISNULL(d.numeroPedido, 'NOPEDIDO') p, d.fechaDespacho, d.tipoOperacion, d.esInternacional, d.idPlanta
                FROM Despachos d
                LEFT JOIN Factura f ON f.idFactura = d.idFactura
                LEFT JOIN CPIC cp ON cp.idCPIC = d.idCPIC
                LEFT JOIN Conductor c ON c.idConductor = d.idConductor
                INNER JOIN Cliente cl ON cl.idCliente = d.idCliente
                WHERE d.activo = 1 AND CHARINDEX(@t, {expresion}) > 0) x", texto);

        [FactBD]
        public void Buscar_comodines_en_lotes_no_devuelve_todo()
        {
            int todos = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null).Count;
            Assert.True(todos > 1, "La BD de pruebas no tiene lotes con qué comparar.");

            foreach (string t in new[] { "%", "_", "[" , "%%", "[a-z]%" })
            {
                Assert.Equal(LotesConTexto("d.numeroPedido", t),
                    ListaDespachosService.ObtenerLotesRegistrados(null, null, null, t, null, null, null).Count);
                Assert.Equal(LotesConTexto("f.numeroFactura", t),
                    ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null, numeroFactura: t).Count);
                Assert.Equal(LotesConTexto("cp.numeroCPIC", t),
                    ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null, numeroCPIC: t).Count);
                Assert.Equal(LotesConTexto("CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, ''))", t),
                    ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null, nombreConductor: t).Count);
                Assert.True(ListaDespachosService.ObtenerLotesRegistrados(null, null, null, t, null, null, null).Count < todos);
            }
        }

        [FactBD]
        public void Buscar_un_pedido_real_lo_sigue_encontrando()
        {
            var conPedido = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null)
                .FirstOrDefault(l => !string.IsNullOrEmpty(l.NumeroPedido));
            if (conPedido == null) return;                                         // BD sin pedidos: nada que comprobar
            string parte = conPedido.NumeroPedido.Substring(0, Math.Min(4, conPedido.NumeroPedido.Length));
            Assert.Contains(ListaDespachosService.ObtenerLotesRegistrados(null, null, null, parte, null, null, null),
                l => l.IdLoteVirtual == conPedido.IdLoteVirtual);
        }

        [FactBD]
        public void Buscar_comodines_en_viajes_activos_no_devuelve_todo()
        {
            foreach (string t in new[] { "%", "_", "[" })
            {
                int esperados = Contar(@"
                    SELECT COUNT(*) FROM ViajesEnProgreso vp
                    WHERE vp.estadoViaje = 'ABIERTO' AND vp.activo = 1
                      AND EXISTS (SELECT 1 FROM Despachos d WHERE d.idViajeProgreso = vp.idViajeProgreso AND d.activo = 1)
                      AND CHARINDEX(@t, vp.numeroViajeProgreso) > 0", t);
                Assert.Equal(esperados, ListaDespachosService.ObtenerViajesActivos(null, null, t).Count);
            }
        }

        /// <summary>
        /// El estado del manifiesto de cada lote (activos y anulados) es el mismo que daba el cálculo anterior de
        /// ListaDespachos: los ids de despacho de cada lote internacional (sp_LD_ObtenerIdsDespachosDeLote) y, de
        /// ellos, cuántos tienen documento de cruce y de retorno.
        /// </summary>
        [FactBD]
        public void Estado_del_manifiesto_igual_que_el_calculo_por_lote()
        {
            var lotes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null)
                .Concat(ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ANULADO")).ToList();
            Assert.NotEmpty(lotes);
            foreach (var lote in lotes)
                Assert.True(EstadoAnterior(lote) == ListaDespachosService.EstadoManifiesto(lote),
                    $"Lote {lote.IdLoteVirtual}: antes {EstadoAnterior(lote)}, ahora {ListaDespachosService.EstadoManifiesto(lote)}");
        }

        /// <summary>El cálculo que hacía ListaDespachos.CalcularEstadosManifiestoPorLote, para un lote.</summary>
        private static string EstadoAnterior(Models.Despachos.LoteRegistrado lote)
        {
            if (!lote.EsInternacional || lote.EstadoLote == "ANULADO") return "N/A";
            List<int> ids = ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual);
            if (ids.Count == 0) return "N/A";
            var tipos = ManifiestoService.ObtenerDocumentosPorDespachos(ids).GroupBy(d => d.IdDespacho)
                .ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(x => x.TipoManifiesto)));
            int completos = ids.Count(id => tipos.TryGetValue(id, out var t)
                && t.Contains(ManifiestoService.TIPO_CRUCE) && t.Contains(ManifiestoService.TIPO_RETORNO));
            return completos == ids.Count ? "Completo" : completos == 0 ? "Pendiente" : $"Parcial {completos}/{ids.Count}";
        }
    }
}
