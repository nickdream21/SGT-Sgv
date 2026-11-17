using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// CPIC única por número y factura única por (número, cliente) (bloque 4, tarea 11; migración 49) contra la BD de
    /// pruebas, dentro de una <see cref="TransaccionDePrueba"/>: no deja datos. Usa despachos activos reales a los que,
    /// dentro de la transacción, se les quitan la factura y la CPIC, y números de documento inventados.
    /// Reglas: escribir un número en un lote enlaza TODOS sus despachos al documento que ya existe (sin renombrar), o
    /// renombra la única CPIC/factura del lote, o la crea; "Factura y CPIC por despacho" toca solo los marcados.
    /// Los casos de error van al final de cada prueba (o en su propia prueba).
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class UnicidadFacturaCpicBdTests
    {
        public UnicidadFacturaCpicBdTests() => BaseDePruebas.Preparar();

        // ── Lote: escribir un número ─────────────────────────────────────────

        [FactBD]
        public void Numero_existente_enlaza_todos_los_despachos_sin_crear_ni_renombrar()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var otro = Despachos(cliente, 1, 0);
                var lote = Despachos(cliente, 3, 1);
                string x = NumeroCpic(), y = NumeroCpic();

                var cx = ListaDespachosService.RegistrarCpicLote(otro, x, FechaHelper.Hoy(), 100m, "test");   // la CPIC X ya existe (de otro lote)
                Assert.Equal("CREADA", cx.Accion);
                var cy = ListaDespachosService.RegistrarCpicLote(lote.Take(1).ToList(), y, FechaHelper.Hoy(), 50m, "test");   // el lote ya tenía la Y en uno
                int cpicsAntes = Contar("SELECT COUNT(*) FROM CPIC");

                var r = ListaDespachosService.RegistrarCpicLote(lote, x, FechaHelper.Hoy(), 999m, "test");

                Assert.Equal(cx.Id, r.Id);
                Assert.Equal("ENLAZADA", r.Accion);
                Assert.All(lote, id => Assert.Equal(cx.Id, CpicDe(id)));
                Assert.Equal(cpicsAntes, Contar("SELECT COUNT(*) FROM CPIC"));
                Assert.Equal(y, Texto("SELECT numeroCPIC FROM CPIC WHERE idCPIC = @id", cy.Id));     // la Y no se renombró
                Assert.Equal(100m, Decimal("SELECT valorTotalFlete FROM CPIC WHERE idCPIC = @id", cx.Id));   // la de otro lote no se pisa

                // Factura: igual, por número y cliente.
                string f = NumeroFactura();
                var fx = ListaDespachosService.RegistrarFacturaLote(otro, f, FechaHelper.Hoy(), 10m, "USD", "test");
                var rf = ListaDespachosService.RegistrarFacturaLote(lote, f, FechaHelper.Hoy(), 20m, "PEN", "test");
                Assert.Equal(fx.Id, rf.Id);
                Assert.Equal("ENLAZADA", rf.Accion);
                Assert.All(lote, id => Assert.Equal(fx.Id, FacturaDe(id)));
                Assert.Equal(1, Contar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @id", f));
            }
        }

        [FactBD]
        public void Lote_con_una_cpic_y_numero_nuevo_la_renombra_y_la_ve_el_otro_lote()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var carga = Despachos(cliente, 2, 0);
                var descarga = Despachos(cliente, 1, 2);
                string y = NumeroCpic(), nuevo = NumeroCpic();

                var c = ListaDespachosService.RegistrarCpicLote(carga, y, FechaHelper.Hoy(), 10m, "test");
                Assert.Equal("ENLAZADA", ListaDespachosService.RegistrarCpicLote(descarga, y, FechaHelper.Hoy(), 10m, "test").Accion);

                var r = ListaDespachosService.RegistrarCpicLote(carga, nuevo, FechaHelper.Hoy(), 12m, "test");

                Assert.Equal(c.Id, r.Id);
                Assert.Equal("RENOMBRADA", r.Accion);
                Assert.Equal(nuevo, Texto("SELECT c.numeroCPIC FROM Despachos d JOIN CPIC c ON c.idCPIC = d.idCPIC WHERE d.idDespacho = @id", descarga[0]));
                Assert.Equal(0, Contar("SELECT COUNT(*) FROM CPIC WHERE numeroCPIC = @id", y));
            }
        }

        [FactBD]
        public void Enlace_parcial_se_completa_con_el_mismo_numero()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 3, 0);
                string y = NumeroCpic(), f = NumeroFactura();
                var c = ListaDespachosService.RegistrarCpicLote(lote.Take(1).ToList(), y, FechaHelper.Hoy(), 10m, "test");
                var fa = ListaDespachosService.RegistrarFacturaLote(lote.Take(1).ToList(), f, FechaHelper.Hoy(), 10m, "PEN", "test");

                var r = ListaDespachosService.RegistrarCpicLote(lote, y, FechaHelper.Hoy(), 15m, "test");
                var rf = ListaDespachosService.RegistrarFacturaLote(lote, f, FechaHelper.Hoy(), 30m, "USD", "test");

                Assert.Equal("ACTUALIZADA", r.Accion);
                Assert.Equal("ACTUALIZADA", rf.Accion);
                Assert.All(lote, id => Assert.Equal(c.Id, CpicDe(id)));
                Assert.All(lote, id => Assert.Equal(fa.Id, FacturaDe(id)));
                Assert.Equal(15m, Decimal("SELECT valorTotalFlete FROM CPIC WHERE idCPIC = @id", c.Id));
                Assert.Equal("USD", Texto("SELECT moneda FROM Factura WHERE idFactura = @id", fa.Id));
                // La CPIC queda con la factura de sus despachos.
                Assert.Equal(fa.Id, Contar("SELECT idFactura FROM CPIC WHERE idCPIC = @id", c.Id));
            }
        }

        [FactBD]
        public void Editar_lote_sin_panel_cpic_no_desvincula_la_cpic()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 2, 0);
                var c = ListaDespachosService.RegistrarCpicLote(lote, NumeroCpic(), FechaHelper.Hoy(), 10m, "test");
                DataRow d = DbHelper.ConsultarTabla("SELECT fechaDespacho, numeroPedido, idPlanta, tipoOperacion, esInternacional FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", lote[0])).Rows[0];

                // Lo que arma ListaDespachos con el panel de la CPIC oculto: ni gestionar ni desvincular.
                var res = ListaDespachosService.GuardarCambiosLote(new GuardarCambiosLoteInput
                {
                    IdsDespachos = lote, FechaDespacho = Convert.ToDateTime(d["fechaDespacho"]),
                    NumeroPedido = d["numeroPedido"] as string, IdPlanta = Convert.ToInt32(d["idPlanta"]),
                    TipoOperacion = d["tipoOperacion"].ToString(), EsInternacional = Convert.ToBoolean(d["esInternacional"]),
                    UsuarioModificacion = "test", FechaActual = FechaHelper.Ahora(),
                    GestionarCpic = EdicionDocumentosLote.Gestionar(false, "1234567"), DesvincularCpic = false
                });

                Assert.Null(res.Cpic);
                Assert.All(lote, id => Assert.Equal(c.Id, CpicDe(id)));
            }
        }

        // ── Factura y CPIC por despacho ──────────────────────────────────────

        [FactBD]
        public void Asignar_a_los_marcados_deja_intactos_los_demas()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 4, 0);
                string f1 = NumeroFactura(), f2 = NumeroFactura(), c1 = NumeroCpic(), c2 = NumeroCpic();
                var fa1 = ListaDespachosService.RegistrarFacturaLote(lote, f1, FechaHelper.Hoy(), 10m, "USD", "test");
                var cp1 = ListaDespachosService.RegistrarCpicLote(lote, c1, FechaHelper.Hoy(), 10m, "test");
                var marcados = lote.Skip(2).ToList();

                var r = ListaDespachosService.AsignarDocumentosDespachos(marcados, f2, FechaHelper.Hoy(), 500m, "USD",
                    c2, FechaHelper.Hoy(), 80m, "test");

                Assert.Equal("CREADA", r.AccionFactura);
                Assert.Equal("CREADA", r.AccionCpic);
                Assert.Equal(2, r.Despachos);
                Assert.All(marcados, id => { Assert.Equal(r.IdFactura, FacturaDe(id)); Assert.Equal(r.IdCpic, CpicDe(id)); });
                Assert.All(lote.Take(2), id => { Assert.Equal(fa1.Id, FacturaDe(id)); Assert.Equal(cp1.Id, CpicDe(id)); });
                Assert.Equal(r.IdFactura.Value, Contar("SELECT idFactura FROM CPIC WHERE idCPIC = @id", r.IdCpic.Value));
                Assert.Equal(c1, Texto("SELECT numeroCPIC FROM CPIC WHERE idCPIC = @id", cp1.Id));
                Assert.Equal(cliente, Contar("SELECT idCliente FROM Factura WHERE idFactura = @id", r.IdFactura.Value));

                // La tabla de "Documentos del lote" lista los cuatro documentos, cada uno con sus dos despachos.
                var docs = EdicionDocumentosLote.DocumentosDistintos(ListaDespachosService.ObtenerDocumentosPorDespacho(lote));
                Assert.Equal(4, docs.Count);
                Assert.All(docs, x => Assert.Equal(2, x.Despachos));

                // Asignar un número que ya existe: se usa ese, sin crear otro.
                var otra = ListaDespachosService.AsignarDocumentosDespachos(lote.Take(1).ToList(), f2, null, null, null, c2, null, null, "test");
                Assert.Equal("EXISTENTE", otra.AccionFactura);
                Assert.Equal("EXISTENTE", otra.AccionCpic);
                Assert.Equal(r.IdCpic, CpicDe(lote[0]));
                Assert.Equal(1, Contar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @id", f2));
            }
        }

        [FactBD]
        public void Misma_factura_de_otro_cliente_se_crea_aparte()
        {
            using (new TransaccionDePrueba())
            {
                var clientes = Clientes(2);
                var deA = Despachos(clientes[0], 1, 0);
                var deB = Despachos(clientes[1], 1, 0);
                string f = NumeroFactura();

                var fa = ListaDespachosService.RegistrarFacturaLote(deA, f, FechaHelper.Hoy(), 10m, "PEN", "test");
                Assert.False(RegistroDespachoService.ValidarDocumentosDuplicados(f, null, clientes[1]).FacturaExiste);
                Assert.True(RegistroDespachoService.ValidarDocumentosDuplicados(f, null, clientes[0]).FacturaExiste);
                Assert.True(RegistroDespachoService.ValidarDocumentosDuplicados(" " + f + " ", null).FacturaExiste);   // sin cliente: cualquiera

                var fb = ListaDespachosService.RegistrarFacturaLote(deB, f, FechaHelper.Hoy(), 20m, "PEN", "test");

                Assert.Equal("CREADA", fb.Accion);
                Assert.NotEqual(fa.Id, fb.Id);
                Assert.Equal(clientes[1], Contar("SELECT idCliente FROM Factura WHERE idFactura = @id", fb.Id));
                Assert.Equal(2, Contar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @id", f));
            }
        }

        // ── Solo lectura: datos reales ───────────────────────────────────────

        /// <summary>
        /// Los 4 lotes de Novopan (21/09, 01/10, 02/10 y 12/10 de 2026) llevan dos facturas y dos CPIC cada uno.
        /// DEPENDE DE DATOS REALES de la BD de pruebas (sgvActualizada): cliente Novopan, despachos de CARGA activos de
        /// esas fechas. Fallará si esos lotes se separan en lotes de un solo documento, se corrigen sus facturas/CPIC, se
        /// anulan o se reconstruye la BD de pruebas sin ellos. Si eso pasa a propósito, no es un bug: cambie las fechas a
        /// otro lote real que lleve dos facturas y dos CPIC (SELECT con sp_LD_ObtenerLotesRegistrados, columnas
        /// CantidadFacturas/CantidadCPIC) o, si ya no queda ninguno, borre esta prueba; las reglas ya las cubren las
        /// pruebas que crean sus propios datos dentro de TransaccionDePrueba.
        /// </summary>
        [FactBD]
        public void Lotes_de_Novopan_con_dos_facturas_y_dos_cpic()
        {
            object idNovopan = DbHelper.EjecutarEscalar("SELECT TOP 1 idCliente FROM Cliente WHERE UPPER(nombre) LIKE '%NOVOPAN%' ORDER BY idCliente");
            Assert.NotNull(idNovopan);
            var lotes = ListaDespachosService.ObtenerLotesRegistrados(Convert.ToInt32(idNovopan), "CARGA", null, null,
                new DateTime(2026, 9, 21), new DateTime(2026, 10, 12), "ACTIVO");
            var dobles = lotes.Where(l => l.CantidadCPIC == 2 && l.CantidadFacturas == 2).Select(l => l.FechaProgramacion.Date).ToList();
            foreach (var dia in new[] { new DateTime(2026, 9, 21), new DateTime(2026, 10, 1), new DateTime(2026, 10, 2), new DateTime(2026, 10, 12) })
                Assert.Contains(dia, dobles);
            Assert.All(lotes, l => Assert.True(EdicionDocumentosLote.TieneVariosDocumentos(l.CantidadFacturas, l.CantidadCPIC)
                                                == (l.CantidadFacturas > 1 || l.CantidadCPIC > 1)));

            // El expediente muestra las dos facturas (antes solo la primera) y las dos CPIC, y la pantalla del lote
            // lista los cuatro documentos por su id.
            var lote = lotes.First(l => l.FechaProgramacion.Date == new DateTime(2026, 9, 21) && l.CantidadCPIC == 2);
            var e = ExpedienteDocumentosService.Obtener(lote.IdLoteVirtual);
            Assert.Equal(2, e.Facturas.Count(f => f.Numero != null));
            Assert.Equal(2, e.Cpics.Count(c => c.Numero != null));
            var docs = EdicionDocumentosLote.DocumentosDistintos(
                ListaDespachosService.ObtenerDocumentosPorDespacho(ListaDespachosService.ObtenerIdsDespachosDeLote(lote.IdLoteVirtual)));
            Assert.Equal(2, docs.Count(d => d.Tipo == DocumentoDelLote.FACTURA));
            Assert.Equal(2, docs.Count(d => d.Tipo == DocumentoDelLote.CPIC));
        }

        // ── Errores (al final: el THROW deja la transacción de la prueba a medio camino) ──

        [FactBD]
        public void Lote_con_dos_cpic_no_se_toca_desde_el_lote()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 2, 0);
                ListaDespachosService.RegistrarCpicLote(lote.Take(1).ToList(), NumeroCpic(), FechaHelper.Hoy(), 1m, "test");
                ListaDespachosService.RegistrarCpicLote(lote.Skip(1).ToList(), NumeroCpic(), FechaHelper.Hoy(), 1m, "test");

                var ex = Assert.Throws<SqlException>(() =>
                    ListaDespachosService.RegistrarCpicLote(lote, NumeroCpic(), FechaHelper.Hoy(), 1m, "test"));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50016);
                Assert.Contains("Factura y CPIC por despacho", ex.Message);
            }
        }

        [FactBD]
        public void Lote_con_dos_facturas_no_se_toca_desde_el_lote()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 2, 0);
                ListaDespachosService.AsignarDocumentosDespachos(lote.Take(1).ToList(), NumeroFactura(), null, null, null, null, null, null, "test");
                ListaDespachosService.AsignarDocumentosDespachos(lote.Skip(1).ToList(), NumeroFactura(), null, null, null, null, null, null, "test");

                var ex = Assert.Throws<SqlException>(() =>
                    ListaDespachosService.RegistrarFacturaLote(lote, NumeroFactura(), FechaHelper.Hoy(), 1m, "PEN", "test"));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50017);
            }
        }

        [FactBD]
        public void Cpic_de_otra_factura_no_se_une()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var loteA = Despachos(cliente, 1, 0);
                var loteB = Despachos(cliente, 1, 1);
                string c = NumeroCpic();
                ListaDespachosService.RegistrarFacturaLote(loteA, NumeroFactura(), FechaHelper.Hoy(), 1m, "PEN", "test");
                ListaDespachosService.RegistrarCpicLote(loteA, c, FechaHelper.Hoy(), 1m, "test");
                ListaDespachosService.RegistrarFacturaLote(loteB, NumeroFactura(), FechaHelper.Hoy(), 1m, "PEN", "test");

                var ex = Assert.Throws<SqlException>(() => ListaDespachosService.RegistrarCpicLote(loteB, c, FechaHelper.Hoy(), 1m, "test"));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50018);
            }
        }

        /// <summary>
        /// Bandeja de documentos: la factura comercial del tercero y su CPIC van en una sola transacción. Si la CPIC no se
        /// puede unir (aquí 50018: ya pertenece a otra factura), la factura del tercero no queda creada ni enlazada.
        /// </summary>
        [FactBD]
        public void Bandeja_cpic_que_no_se_une_no_deja_la_factura_del_tercero()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var otro = Despachos(cliente, 1, 0);
                var lote = Despachos(cliente, 2, 1);
                string c = NumeroCpic(), fTercero = NumeroFactura();
                ListaDespachosService.RegistrarFacturaLote(otro, NumeroFactura(), FechaHelper.Hoy(), 1m, "PEN", "test");
                ListaDespachosService.RegistrarCpicLote(otro, c, FechaHelper.Hoy(), 1m, "test");   // la CPIC ya es de otra factura
                var leida = new WebSGV.Services.Despachos.Importacion.CpicLeido
                {
                    Numero = c, FacturaComercial = fTercero, FechaEmision = FechaHelper.Hoy(), ValorFlete = 1m, ValorMercancias = 1m
                };

                var ex = Assert.ThrowsAny<SqlException>(() => BandejaDocumentosService.UnirCpicConFacturaTercero(lote, leida, cliente));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50018);

                // El ROLLBACK del servicio deshace toda la transacción de la prueba (también los UPDATE de Despachos(...)),
                // así que solo se comprueba lo que no puede existir fuera de ella: la factura del tercero, de número
                // inventado. Antes (dos llamadas sueltas) quedaba creada y unida al lote aunque la CPIC fallara.
                // Tras esto la prueba no escribe nada más.
                Assert.Equal(0, Contar("SELECT COUNT(*) FROM Factura WHERE numeroFactura = @id", fTercero));
            }
        }

        [FactBD]
        public void Una_factura_no_tiene_dos_cpic()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 2, 0);
                string f = NumeroFactura();
                ListaDespachosService.AsignarDocumentosDespachos(lote, f, null, null, null, NumeroCpic(), null, null, "test");

                var ex = Assert.Throws<SqlException>(() =>
                    ListaDespachosService.AsignarDocumentosDespachos(lote.Take(1).ToList(), null, null, null, null, NumeroCpic(), null, null, "test"));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50019);
            }
        }

        [FactBD]
        public void Crear_cpic_con_numero_existente_da_mensaje_claro()
        {
            using (new TransaccionDePrueba())
            {
                string c = NumeroCpic();
                var lote = new LoteDespachos();
                lote.Documentacion.NumeroCPIC = c;
                Assert.NotNull(RegistroDespachoService.CrearDocumentoBaseSeparado("CPIC", lote));

                lote.Documentacion.NumeroCPIC = " " + c + " ";
                var ex = Assert.Throws<SqlException>(() => RegistroDespachoService.CrearDocumentoBaseSeparado("CPIC", lote));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50021);
                Assert.Contains("ya está registrada", ex.Message);
            }
        }

        [FactBD]
        public void Crear_factura_repetida_del_mismo_cliente_da_mensaje_claro()
        {
            using (new TransaccionDePrueba())
            {
                var lote = new LoteDespachos { IdCliente = Clientes(1)[0] };
                lote.Documentacion.NumeroFactura = NumeroFactura();
                Assert.NotNull(RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote));

                var ex = Assert.Throws<SqlException>(() => RegistroDespachoService.CrearDocumentoBaseSeparado("FACTURA", lote));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50022);
            }
        }

        [FactBD]
        public void Despacho_anulado_no_se_toca()
        {
            using (new TransaccionDePrueba())
            {
                int cliente = Clientes(1)[0];
                var lote = Despachos(cliente, 2, 0);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0, estadoDespacho = 'ANULADO' WHERE idDespacho = @id", DbHelper.Param("@id", lote[1]));

                var ex = Assert.Throws<SqlException>(() =>
                    ListaDespachosService.AsignarDocumentosDespachos(lote, NumeroFactura(), null, null, null, null, null, null, "test"));
                Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 50015);
            }
        }

        // ── Ayudantes ────────────────────────────────────────────────────────

        /// <summary>Los <paramref name="n"/> clientes con más despachos activos (al menos 6).</summary>
        private static List<int> Clientes(int n)
        {
            var ids = DbHelper.ConsultarTabla($@"
                SELECT TOP {n} d.idCliente FROM Despachos d
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO'
                GROUP BY d.idCliente HAVING COUNT(*) >= 6 ORDER BY COUNT(*) DESC, d.idCliente")
                .Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r[0])).ToList();
            Assert.Equal(n, ids.Count);
            return ids;
        }

        /// <summary>
        /// <paramref name="n"/> despachos activos del cliente (saltando los <paramref name="saltar"/> primeros), sin factura ni
        /// CPIC dentro de la transacción de la prueba.
        /// </summary>
        private static List<int> Despachos(int idCliente, int n, int saltar)
        {
            var ids = DbHelper.ConsultarTabla(@"
                SELECT d.idDespacho FROM Despachos d
                WHERE d.activo = 1 AND ISNULL(d.estadoDespacho, '') <> 'ANULADO' AND d.idCliente = @c
                ORDER BY d.idDespacho DESC OFFSET @s ROWS FETCH NEXT @n ROWS ONLY",
                DbHelper.Param("@c", idCliente), DbHelper.Param("@s", saltar), DbHelper.Param("@n", n))
                .Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r[0])).ToList();
            Assert.Equal(n, ids.Count);
            DbHelper.EjecutarNonQuery("UPDATE Despachos SET idFactura = NULL, idCPIC = NULL WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))",
                DbHelper.Param("@ids", string.Join(",", ids)));
            return ids;
        }

        private static readonly Random Azar = new Random();
        private static string NumeroCpic() => "9" + Azar.Next(100000, 999999);
        private static string NumeroFactura() => "F999-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();

        private static int? CpicDe(int idDespacho) =>
            DbHelper.EjecutarEscalar("SELECT idCPIC FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", idDespacho)) as int?;
        private static int? FacturaDe(int idDespacho) =>
            DbHelper.EjecutarEscalar("SELECT idFactura FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", idDespacho)) as int?;
        private static int Contar(string sql, object id = null) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(sql, DbHelper.Param("@id", id)));
        private static string Texto(string sql, object id) => Convert.ToString(DbHelper.EjecutarEscalar(sql, DbHelper.Param("@id", id)));
        private static decimal Decimal(string sql, object id) => Convert.ToDecimal(DbHelper.EjecutarEscalar(sql, DbHelper.Param("@id", id)));
    }
}
