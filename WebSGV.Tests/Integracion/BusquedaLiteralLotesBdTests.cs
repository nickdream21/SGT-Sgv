using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Solo lectura (bloque 4, tarea 16): las búsquedas de ListaDespachos son literales —"%", "_" y "[" no son
    /// comodines del LIKE en sp_LD_ObtenerLotesRegistrados ni en sp_LD_ObtenerViajesActivos— y el estado del
    /// manifiesto que sale de las columnas DespachosManifiestoCompleto y DespachosEsperanManifiesto es el que da la
    /// regla de M-242 calculada aparte (una consulta de ids por lote más los documentos de manifiesto): un MCI de
    /// cualquier tipo en cada despacho no anulado de un lote que lo espera (internacional o carga de perfil CPIC).
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
        /// El estado del manifiesto de cada lote (activos y anulados) es el de la regla de M-242 calculada por lote: los
        /// ids de despacho del lote (sp_LD_ObtenerIdsDespachosDeLote), los no anulados si el lote espera MCI y, de ellos,
        /// cuántos tienen algún documento de manifiesto, de cruce o de retorno.
        /// </summary>
        [FactBD]
        public void Estado_del_manifiesto_igual_que_el_calculo_por_lote()
        {
            var lotes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null)
                .Concat(ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ANULADO")).ToList();
            Assert.NotEmpty(lotes);
            foreach (var lote in lotes)
            {
                string esperado = EstadoEsperado(lote), obtenido = ListaDespachosService.EstadoManifiesto(lote);
                Assert.True(esperado == obtenido, $"Lote {lote.IdLoteVirtual}: esperado {esperado}, ahora {obtenido}");
            }
        }

        /// <summary>
        /// M-242: en cada lote activo con planta, "Completo" si y solo si espera MCI y ningún despacho que lo espera está
        /// sin él, y los que lo esperan son los completos más los que no lo tienen. Novopan solo recibe el MCI de retorno:
        /// un lote suyo con MCI de retorno nunca queda "Pendiente" (antes salía siempre así).
        /// </summary>
        [FactBD]
        public void Manifiesto_completo_con_un_MCI_de_cualquier_tipo()
        {
            var activos = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, null)
                .Where(l => l.EstadoLote == "ACTIVO" && l.IdPlanta > 0).ToList();
            Assert.NotEmpty(activos);
            foreach (var lote in activos)
            {
                string estado = ListaDespachosService.EstadoManifiesto(lote);
                Assert.True(lote.DespachosEsperanManifiesto == lote.DespachosManifiestoCompleto + lote.DespachosSinManifiesto,
                    $"Lote {lote.IdLoteVirtual}: esperan {lote.DespachosEsperanManifiesto}, completos {lote.DespachosManifiestoCompleto}, sin MCI {lote.DespachosSinManifiesto}");
                Assert.True((estado == "Completo") == (lote.DespachosEsperanManifiesto > 0 && lote.DespachosSinManifiesto == 0),
                    $"Lote {lote.IdLoteVirtual}: estado {estado}, esperan {lote.DespachosEsperanManifiesto}, sin MCI {lote.DespachosSinManifiesto}");
            }

            foreach (var lote in activos.Where(l => (l.NombreCliente ?? "").ToUpperInvariant().Contains("NOVOPAN")))
            {
                List<int> ids = ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual);
                bool conRetorno = ManifiestoService.ObtenerDocumentosPorDespachos(ids)
                    .Any(d => string.Equals((d.TipoManifiesto ?? "").Trim(), ManifiestoService.TIPO_RETORNO, StringComparison.OrdinalIgnoreCase));
                if (conRetorno)
                    Assert.True(ListaDespachosService.EstadoManifiesto(lote) != "Pendiente",
                        $"Lote de Novopan {lote.IdLoteVirtual} con MCI de retorno sale \"Pendiente\".");
            }
        }

        /// <summary>La regla de M-242 calculada aparte, para un lote.</summary>
        private static string EstadoEsperado(Models.Despachos.LoteRegistrado lote)
        {
            bool esperaMci = lote.EsInternacional || (lote.PerfilDocumentos == "CPIC" && lote.TipoOperacion == "CARGA");
            if (!esperaMci || lote.EstadoLote == "ANULADO" || lote.IdPlanta <= 0) return "N/A";
            List<int> ids = ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual);
            if (ids.Count == 0) return "N/A";
            var vigentes = DbHelper.ConsultarTabla(@"
                SELECT idDespacho FROM Despachos
                WHERE idDespacho IN (SELECT value FROM STRING_SPLIT(@ids, ',')) AND estadoDespacho <> 'ANULADO'",
                DbHelper.Param("@ids", string.Join(",", ids)))
                .Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["idDespacho"])).ToList();
            if (vigentes.Count == 0) return "N/A";
            var conMci = new HashSet<int>(ManifiestoService.ObtenerDocumentosPorDespachos(vigentes).Select(d => d.IdDespacho));
            int completos = vigentes.Count(conMci.Contains);
            return completos == vigentes.Count ? "Completo" : completos == 0 ? "Pendiente" : $"Parcial {completos}/{vigentes.Count}";
        }
    }
}
