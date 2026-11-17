using System;
using System.Data;
using System.Linq;
using System.Transactions;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// "Anular lote" de verdad (auditoría bloque 4, tarea 9): sp_LD_AnularLote deja los despachos
    /// ANULADO + activo = 0 (salen de la liquidación del conductor y del seguimiento), recalcula
    /// cantidadDespachos, anula el viaje ABIERTO que queda vacío y no toca un lote con liquidación
    /// (OrdenViaje del viaje, o del viaje principal que cerró a la vez: mismo conductor y fechaCierre).
    /// sp_LD_EliminarLote (código publicado) delega en él. Los lotes anulados solo se ven con el filtro
    /// ANULADO de sp_LD_ObtenerLotesRegistrados.
    /// Todo dentro de una <see cref="TransaccionDePrueba"/>: no deja datos. El 50010 se lanza dentro de
    /// la transacción del SP y su ROLLBACK aborta también la de la prueba: cada caso de error va en su
    /// propio método y lo que se comprueba después se lee fuera de la transacción.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class AnularLoteBdTests
    {
        public AnularLoteBdTests() => BaseDePruebas.Preparar();

        /// <summary>Viaje con liquidación: el suyo propio o el principal que lo cerró (mismo conductor y fechaCierre).</summary>
        private const string ViajeLiquidado = @"(v.idOrdenViajeGenerada IS NOT NULL
              OR EXISTS (SELECT 1 FROM OrdenViaje o WHERE o.idViajeProgreso = v.idViajeProgreso)
              OR EXISTS (SELECT 1 FROM OrdenViaje o JOIN ViajesEnProgreso p ON p.idViajeProgreso = o.idViajeProgreso
                         WHERE p.idConductor = v.idConductor AND p.fechaCierre IS NOT NULL AND p.fechaCierre = v.fechaCierre))";

        private static System.Data.SqlClient.SqlParameter[] Parametros(object[] pares)
        {
            var ps = new System.Data.SqlClient.SqlParameter[pares.Length / 2];
            for (int i = 0; i < ps.Length; i++) ps[i] = DbHelper.Param((string)pares[2 * i], pares[2 * i + 1]);
            return ps;
        }

        private static int Entero(string sql, params object[] pares) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(sql, Parametros(pares)));

        private static string Texto(string sql, params object[] pares) =>
            Convert.ToString(DbHelper.EjecutarEscalar(sql, Parametros(pares)));

        private static void Ejecutar(string sql, params object[] pares) =>
            DbHelper.EjecutarNonQuery(sql, Parametros(pares));

        /// <summary>Lee fuera de la transacción de la prueba (que un ROLLBACK del SP dejó abortada).</summary>
        private static T FueraDeLaPrueba<T>(Func<T> lectura)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
                return lectura();
        }

        /// <summary>Un viaje sin liquidación con al menos dos despachos activos, puesto en <paramref name="estado"/> dentro de la transacción.</summary>
        private static int ViajeSinLiquidacion(string estado = "ABIERTO")
        {
            int viaje = Entero(@"SELECT TOP 1 v.idViajeProgreso FROM ViajesEnProgreso v
                WHERE NOT " + ViajeLiquidado + @"
                  AND (SELECT COUNT(*) FROM Despachos d WHERE d.idViajeProgreso = v.idViajeProgreso AND d.activo = 1) >= 2
                ORDER BY v.idViajeProgreso DESC");
            Ejecutar("UPDATE ViajesEnProgreso SET estadoViaje = @e WHERE idViajeProgreso = @v", "@e", estado, "@v", viaje);
            return viaje;
        }

        private static int[] DespachosActivos(int viaje) =>
            DbHelper.ConsultarTabla("SELECT idDespacho FROM Despachos WHERE idViajeProgreso = @v AND activo = 1 ORDER BY idDespacho",
                    DbHelper.Param("@v", viaje))
                .Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["idDespacho"])).ToArray();

        private static bool LiquidacionTrae(int viaje, int idDespacho) =>
            DbHelper.ConsultarTablaSp("sp_DC_ObtenerDespachosViajesActivos", DbHelper.Param("@idsViajes", viaje.ToString()))
                .Rows.Cast<DataRow>().Any(r => Convert.ToInt32(r["idDespacho"]) == idDespacho);

        private static int Cantidad(int viaje) =>
            Entero("SELECT cantidadDespachos FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", viaje);

        private static string EstadoViaje(int viaje) =>
            Texto("SELECT estadoViaje FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", viaje);

        private static void AfirmarAnulado(int idDespacho)
        {
            Assert.Equal("ANULADO", Texto("SELECT estadoDespacho FROM Despachos WHERE idDespacho = @d", "@d", idDespacho));
            Assert.Equal(0, Entero("SELECT CAST(activo AS INT) FROM Despachos WHERE idDespacho = @d", "@d", idDespacho));
        }

        private static void AfirmarIntacto(int idDespacho)
        {
            Assert.Equal(1, Entero("SELECT CAST(activo AS INT) FROM Despachos WHERE idDespacho = @d", "@d", idDespacho));
            Assert.NotEqual("ANULADO", Texto("SELECT estadoDespacho FROM Despachos WHERE idDespacho = @d", "@d", idDespacho));
        }

        [FactBD]
        public void AnularLote_DejaAnuladoInactivoRecalculaYAnulaElViajeVacio()
        {
            using (new TransaccionDePrueba())
            {
                int viaje = ViajeSinLiquidacion();
                int[] despachos = DespachosActivos(viaje);
                Assert.True(LiquidacionTrae(viaje, despachos[0]));

                // Un despacho: sale de la liquidación, el viaje sigue ABIERTO con un despacho menos.
                Assert.Equal(0, ListaDespachosService.AnularLote(despachos[0].ToString(), "PRUEBA"));
                AfirmarAnulado(despachos[0]);
                Assert.False(LiquidacionTrae(viaje, despachos[0]));
                Assert.Equal(despachos.Length - 1, Cantidad(viaje));
                Assert.Equal("ABIERTO", EstadoViaje(viaje));

                // El resto (incluye el ya anulado: idempotente): el viaje queda vacío y ANULADO.
                Assert.Equal(1, ListaDespachosService.AnularLote(string.Join(",", despachos), "PRUEBA"));
                foreach (int d in despachos) AfirmarAnulado(d);
                Assert.Equal(0, Cantidad(viaje));
                Assert.Equal("ANULADO", EstadoViaje(viaje));
                Assert.Equal(0, DbHelper.ConsultarTablaSp("sp_DC_ObtenerDespachosViajesActivos",
                    DbHelper.Param("@idsViajes", viaje.ToString())).Rows.Count);

                // Otra vez: no falla y no anula nada más.
                Assert.Equal(0, ListaDespachosService.AnularLote(string.Join(",", despachos), "PRUEBA"));
            }
        }

        [FactBD]
        public void EliminarLote_DelegaEnAnular()
        {
            using (new TransaccionDePrueba())
            {
                int viaje = ViajeSinLiquidacion();
                int[] despachos = DespachosActivos(viaje);

                DbHelper.EjecutarNonQuerySp("sp_LD_EliminarLote",
                    DbHelper.Param("@idsDespachos", string.Join(",", despachos)),
                    DbHelper.Param("@usuario", "PRUEBA"),
                    DbHelper.Param("@fechaActual", FechaHelper.Ahora()));

                foreach (int d in despachos) AfirmarAnulado(d);
                Assert.Equal(0, Cantidad(viaje));
                Assert.Equal("ANULADO", EstadoViaje(viaje));
                Assert.False(LiquidacionTrae(viaje, despachos[0]));
            }
        }

        [FactBD]
        public void ListaConEspaciosComaFinalYDuplicados_SeAnulaUnaVez()
        {
            using (new TransaccionDePrueba())
            {
                int viaje = ViajeSinLiquidacion();
                int[] despachos = DespachosActivos(viaje);
                int d = despachos[0];

                Assert.Equal(0, ListaDespachosService.AnularLote($" {d}, {d} ,", "PRUEBA"));
                AfirmarAnulado(d);
                Assert.Equal(despachos.Length - 1, Cantidad(viaje));
                AfirmarIntacto(despachos[1]);
            }
        }

        [FactBD]
        public void DespachoSinViaje_SeAnulaSinTocarViajes()
        {
            using (new TransaccionDePrueba())
            {
                int d = Entero("SELECT TOP 1 idDespacho FROM Despachos WHERE activo = 1 AND idViajeProgreso IS NULL ORDER BY idDespacho DESC");

                Assert.Equal(0, ListaDespachosService.AnularLote(d.ToString(), "PRUEBA"));
                AfirmarAnulado(d);
            }
        }

        [FactBD]
        public void ViajeCerradoSinLiquidacion_NoSeAnulaYRecalculaCantidad()
        {
            using (new TransaccionDePrueba())
            {
                int viaje = ViajeSinLiquidacion("CERRADO");
                int[] despachos = DespachosActivos(viaje);

                Assert.Equal(0, ListaDespachosService.AnularLote(string.Join(",", despachos), "PRUEBA"));
                foreach (int d in despachos) AfirmarAnulado(d);
                Assert.Equal(0, Cantidad(viaje));
                Assert.Equal("CERRADO", EstadoViaje(viaje));
            }
        }

        [FactBD]
        public void LoteAnulado_SoloApareceConElFiltroAnulado()
        {
            using (new TransaccionDePrueba())
            {
                // Un lote (cliente, pedido, fecha, operación, ámbito, planta) sin despachos en viajes liquidados.
                var clave = DbHelper.ConsultarTabla(@"
                    SELECT TOP 1 x.idCliente, x.fechaDespacho, MAX(x.idDespacho) AS idDespacho
                    FROM (
                        SELECT d.*, CASE WHEN v.idViajeProgreso IS NOT NULL AND " + ViajeLiquidado + @" THEN 1 ELSE 0 END AS liquidado
                        FROM Despachos d
                        LEFT JOIN ViajesEnProgreso v ON v.idViajeProgreso = d.idViajeProgreso
                        WHERE d.activo = 1 AND d.idPlanta IS NOT NULL
                    ) x
                    GROUP BY x.idCliente, ISNULL(x.numeroPedido, 'NOPEDIDO'), x.fechaDespacho, x.tipoOperacion, x.esInternacional, x.idPlanta
                    HAVING SUM(x.liquidado) = 0
                    ORDER BY MAX(x.idDespacho) DESC").Rows[0];
                int idCliente = Convert.ToInt32(clave["idCliente"]);
                DateTime fecha = Convert.ToDateTime(clave["fechaDespacho"]).Date;
                int unDespacho = Convert.ToInt32(clave["idDespacho"]);

                Func<string, System.Collections.Generic.List<Models.Despachos.LoteRegistrado>> lotes = estado =>
                    ListaDespachosService.ObtenerLotesRegistrados(idCliente, null, null, null, fecha, fecha, estado);

                var lote = lotes(null).Single(l => ListaDespachosService.ObtenerIdsDespachosDeLote(l.IdLoteVirtual).Contains(unDespacho));
                var ids = ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual);
                Assert.DoesNotContain(lotes("ANULADO"), l => l.IdLoteVirtual == lote.IdLoteVirtual);

                ListaDespachosService.AnularLote(string.Join(",", ids), "PRUEBA");

                // Sin filtro (y con ACTIVO) ya no aparece; con ANULADO aparece, marcado y con todas sus unidades.
                Assert.DoesNotContain(lotes(null), l => l.IdLoteVirtual == lote.IdLoteVirtual);
                Assert.DoesNotContain(lotes("ACTIVO"), l => l.IdLoteVirtual == lote.IdLoteVirtual);
                var anulado = Assert.Single(lotes("ANULADO"), l => l.IdLoteVirtual == lote.IdLoteVirtual);
                Assert.Equal("ANULADO", anulado.EstadoLote);
                Assert.Equal(ids.Count, anulado.CantidadDespachos);
            }
        }

        // ---------------- Casos de error: cada uno en su método y su transacción ----------------

        [FactBD]
        public void ListaNoNumerica_Error50011()
        {
            using (new TransaccionDePrueba())
            {
                int d = DespachosActivos(ViajeSinLiquidacion())[0];

                var ex = Assert.ThrowsAny<Exception>(() => ListaDespachosService.AnularLote(d + ",abc", "PRUEBA"));
                Assert.Contains(((System.Data.SqlClient.SqlException)ex).Errors.Cast<System.Data.SqlClient.SqlError>(), e => e.Number == 50011);
                Assert.Contains("no es válida", MensajeErrorHelper.ParaUsuario(ex));
                AfirmarIntacto(d);   // el 50011 sale antes de abrir la transacción: la de la prueba sigue viva
            }
        }

        [FactBD]
        public void LoteConLiquidacion_NoSeAnulaNadaYAvisa()
        {
            int conLiquidacion, libre;
            using (new TransaccionDePrueba())
            {
                conLiquidacion = Entero(@"SELECT TOP 1 d.idDespacho FROM Despachos d
                    JOIN OrdenViaje o ON o.idViajeProgreso = d.idViajeProgreso WHERE d.activo = 1 ORDER BY d.idDespacho DESC");
                libre = DespachosActivos(ViajeSinLiquidacion())[0];

                var ex = Assert.ThrowsAny<Exception>(() => ListaDespachosService.AnularLote(libre + "," + conLiquidacion, "PRUEBA"));
                Assert.Contains(((System.Data.SqlClient.SqlException)ex).Errors.Cast<System.Data.SqlClient.SqlError>(), e => e.Number == 50010);
                Assert.Contains("liquidación", MensajeErrorHelper.ParaUsuario(ex));
            }

            // Nada cambió, ni siquiera el despacho del viaje sin liquidación.
            FueraDeLaPrueba(() => { AfirmarIntacto(conLiquidacion); AfirmarIntacto(libre); return 0; });
        }

        [FactBD]
        public void ViajeHermanoDeUnaLiquidacion_NoSeAnulaYAvisa()
        {
            int despacho;
            string numeroViaje;
            using (new TransaccionDePrueba())
            {
                // Viaje principal de una liquidación (la OrdenViaje apunta a él) y otro viaje del mismo conductor
                // que la liquidación cerró a la vez: misma fechaCierre, sin OrdenViaje propia.
                var principal = DbHelper.ConsultarTabla(@"SELECT TOP 1 p.idViajeProgreso, p.idConductor, p.fechaCierre
                    FROM OrdenViaje o JOIN ViajesEnProgreso p ON p.idViajeProgreso = o.idViajeProgreso
                    WHERE p.fechaCierre IS NOT NULL ORDER BY o.idOrdenViaje DESC").Rows[0];
                int hermano = ViajeSinLiquidacion("CERRADO");
                Ejecutar("UPDATE ViajesEnProgreso SET idConductor = @c, fechaCierre = @f WHERE idViajeProgreso = @v",
                    "@c", principal["idConductor"], "@f", principal["fechaCierre"], "@v", hermano);
                despacho = DespachosActivos(hermano)[0];
                numeroViaje = Texto("SELECT numeroViajeProgreso FROM ViajesEnProgreso WHERE idViajeProgreso = @v", "@v", hermano);

                var ex = Assert.ThrowsAny<Exception>(() => ListaDespachosService.AnularLote(despacho.ToString(), "PRUEBA"));
                Assert.Contains(((System.Data.SqlClient.SqlException)ex).Errors.Cast<System.Data.SqlClient.SqlError>(), e => e.Number == 50010);
                string mensaje = MensajeErrorHelper.ParaUsuario(ex);
                Assert.Contains("liquidación", mensaje);
                Assert.Contains(numeroViaje, mensaje);
            }

            FueraDeLaPrueba(() => { AfirmarIntacto(despacho); return 0; });
        }
    }
}
