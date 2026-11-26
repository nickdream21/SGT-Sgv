using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Newtonsoft.Json;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Editar lote seguro y auditado (2026-11-17, bloque 4, tareas 12 y 13) contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>: no deja datos. Los despachos se crean aquí (FlujoDespachoLiquidacionTests).
    /// Los casos que terminan en THROW van al final de cada prueba.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class EdicionLoteBdTests
    {
        public EdicionLoteBdTests() => BaseDePruebas.Preparar();

        // ── Ayudantes ────────────────────────────────────────────────────────

        private static InstantaneaLote Foto(List<int> ids)
        {
            InstantaneaLote foto = null;
            DbHelper.EnTransaccion((c, t) => foto = ListaDespachosService.ObtenerInstantaneaLote(c, t, ids));
            return foto;
        }

        /// <summary>Lo que manda Editar lote sin tocar nada: los datos guardados del primer despacho y el conductor de cada uno.</summary>
        private static GuardarCambiosLoteInput SinTocar(List<int> ids, string usuario = "PRUEBA-EDICION")
        {
            var foto = Foto(ids);
            var d = foto.Despachos[0];
            return new GuardarCambiosLoteInput
            {
                IdsDespachos = ids,
                FechaDespacho = d.FechaDespacho.Value,
                NumeroPedido = d.NumeroPedido,
                IdPlanta = d.IdPlanta.Value,
                TipoOperacion = d.TipoOperacion,
                EsInternacional = d.EsInternacional,
                UsuarioModificacion = usuario,
                FechaActual = FechaHelper.Ahora(),
                CambiosConductores = foto.Despachos.Where(x => x.IdConductor.HasValue).ToDictionary(x => x.IdDespacho, x => x.IdConductor.Value)
            };
        }

        /// <summary>Dos despachos nuevos en el mismo viaje y con los mismos datos de lote.</summary>
        private static List<int> LoteDeDos(out FlujoDespachoLiquidacionTests.DespachoDePrueba primero)
        {
            primero = FlujoDespachoLiquidacionTests.CrearDespachoDePrueba();
            var segundo = FlujoDespachoLiquidacionTests.CrearDespachoDePrueba();
            DbHelper.EjecutarNonQuery(@"
                UPDATE d2 SET fechaDespacho = d1.fechaDespacho, numeroPedido = d1.numeroPedido, idPlanta = d1.idPlanta,
                              lugarOperacion = d1.lugarOperacion, tipoOperacion = d1.tipoOperacion, esInternacional = d1.esInternacional,
                              idConductor = d1.idConductor, idViajeProgreso = d1.idViajeProgreso, idFactura = NULL, idCPIC = NULL
                FROM Despachos d2 JOIN Despachos d1 ON d1.idDespacho = @d1
                WHERE d2.idDespacho = @d2;
                UPDATE Despachos SET idFactura = NULL, idCPIC = NULL WHERE idDespacho = @d1;",
                DbHelper.Param("@d1", primero.IdDespacho), DbHelper.Param("@d2", segundo.IdDespacho));
            return new List<int> { primero.IdDespacho, segundo.IdDespacho };
        }

        private static DataRow Despacho(int id) => DbHelper.ConsultarTabla(
            "SELECT idConductor, idViajeProgreso, idFactura, idCPIC, usuarioModificacion, fechaModificacion FROM Despachos WHERE idDespacho = @id",
            DbHelper.Param("@id", id)).Rows[0];

        private static object Fecha(DataRow r) => r["fechaModificacion"] == DBNull.Value ? null : (object)Convert.ToDateTime(r["fechaModificacion"]);

        private static readonly Random Azar = new Random();
        private static string NumeroCpic() => "9" + Azar.Next(100000, 999999);
        private static string NumeroFactura() => "F999-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();

        private static void ActualizarConSp(int idDespacho, DateTime fecha, string pedido, int idPlanta, string tipo, bool internacional, int? idConductor) =>
            DbHelper.EjecutarNonQuerySp("sp_LD_ActualizarDespachoEnLote",
                DbHelper.Param("@idDespacho", idDespacho),
                DbHelper.Param("@fechaDespacho", fecha),
                DbHelper.Param("@numeroPedido", (object)pedido ?? DBNull.Value),
                DbHelper.Param("@idPlanta", idPlanta),
                DbHelper.Param("@tipoOperacion", tipo),
                DbHelper.Param("@esInternacional", internacional),
                DbHelper.Param("@idConductor", (object)idConductor ?? DBNull.Value),
                DbHelper.Param("@usuarioModificacion", "PRUEBA-SP"),
                DbHelper.Param("@fechaActual", FechaHelper.Ahora()));

        // ── Pruebas ──────────────────────────────────────────────────────────

        [FactBD]
        public void Guardar_sin_cambios_no_escribe_y_da_SinCambios()
        {
            using (new TransaccionDePrueba())
            {
                var ids = LoteDeDos(out _);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET usuarioModificacion = 'ANTES', fechaModificacion = '2026-01-02T03:04:05' WHERE idDespacho IN (@a, @b)",
                    DbHelper.Param("@a", ids[0]), DbHelper.Param("@b", ids[1]));

                var r = ListaDespachosService.GuardarCambiosLote(SinTocar(ids));

                Assert.True(r.SinCambios);
                Assert.Null(r.AntesJson);
                foreach (int id in ids)
                {
                    var d = Despacho(id);
                    Assert.Equal("ANTES", d["usuarioModificacion"]);
                    Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), Fecha(d));
                }
            }
        }

        [FactBD]
        public void Cambiar_el_conductor_de_un_despacho_reubica_solo_ese_y_las_instantaneas_lo_reflejan()
        {
            using (new TransaccionDePrueba())
            {
                var ids = LoteDeDos(out var primero);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET usuarioModificacion = 'ANTES', fechaModificacion = '2026-01-02T03:04:05' WHERE idDespacho IN (@a, @b)",
                    DbHelper.Param("@a", ids[0]), DbHelper.Param("@b", ids[1]));
                int viaje = primero.IdViaje;
                var input = SinTocar(ids);
                input.CambiosConductores[ids[1]] = primero.IdOtroConductor;

                var r = ListaDespachosService.GuardarCambiosLote(input);

                Assert.False(r.SinCambios);
                Assert.Equal(new Dictionary<int, int> { [ids[1]] = primero.IdOtroConductor }, r.ConductoresCambiados);

                var cambiado = Despacho(ids[1]);
                Assert.Equal(primero.IdOtroConductor, Convert.ToInt32(cambiado["idConductor"]));
                Assert.NotEqual(viaje, Convert.ToInt32(cambiado["idViajeProgreso"]));
                Assert.Equal("PRUEBA-EDICION", cambiado["usuarioModificacion"]);

                var intacto = Despacho(ids[0]);
                Assert.Equal(primero.IdConductor, Convert.ToInt32(intacto["idConductor"]));
                Assert.Equal(viaje, Convert.ToInt32(intacto["idViajeProgreso"]));
                Assert.Equal("ANTES", intacto["usuarioModificacion"]);
                Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), Fecha(intacto));

                var antes = JsonConvert.DeserializeObject<InstantaneaLote>(r.AntesJson);
                var despues = JsonConvert.DeserializeObject<InstantaneaLote>(r.DespuesJson);
                Assert.Equal(primero.IdConductor, antes.Despachos.Single(d => d.IdDespacho == ids[1]).IdConductor);
                Assert.Equal(primero.IdOtroConductor, despues.Despachos.Single(d => d.IdDespacho == ids[1]).IdConductor);
                Assert.Equal(viaje, antes.Despachos.Single(d => d.IdDespacho == ids[1]).IdViajeProgreso);
                Assert.NotEqual(viaje, despues.Despachos.Single(d => d.IdDespacho == ids[1]).IdViajeProgreso);
            }
        }

        [FactBD]
        public void GestionarCPICLote_con_usuario_escribe_quien_y_cuando_y_Quitar_CPIC_la_conserva()
        {
            using (new TransaccionDePrueba())
            {
                var ids = LoteDeDos(out _);
                DateTime inicio = FechaHelper.Ahora().AddMinutes(-5);

                var cpic = ListaDespachosService.RegistrarCpicLote(ids, NumeroCpic(), FechaHelper.Hoy(), 10m, "PRUEBA-CPIC");
                foreach (int id in ids)
                {
                    var d = Despacho(id);
                    Assert.Equal(cpic.Id, Convert.ToInt32(d["idCPIC"]));
                    Assert.Equal("PRUEBA-CPIC", d["usuarioModificacion"]);
                    Assert.True((DateTime)Fecha(d) >= inicio);
                }

                // Quitar la CPIC del lote desde Editar lote.
                var input = SinTocar(ids, "PRUEBA-QUITAR");
                input.QuitarCpic = true;
                var r = ListaDespachosService.GuardarCambiosLote(input);

                Assert.Equal(new[] { cpic.Id }, r.CpicsQuitadas);
                foreach (int id in ids)
                {
                    var d = Despacho(id);
                    Assert.Equal(DBNull.Value, d["idCPIC"]);
                    Assert.Equal("PRUEBA-QUITAR", d["usuarioModificacion"]);
                }
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM CPIC WHERE idCPIC = @id", DbHelper.Param("@id", cpic.Id))));
                Assert.Null(JsonConvert.DeserializeObject<InstantaneaLote>(r.DespuesJson).Despachos[0].IdCPIC);
            }
        }

        /// <summary>
        /// E6-28: tras guardar en EditarLote.aspx la página vuelve al detalle con el id que arma EditarLoteReglas.IdTrasGuardar;
        /// ese id debe ser el mismo que arma sp_LD_ObtenerLotesRegistrados para el lote ya editado (fecha y pedido nuevos).
        /// </summary>
        [FactBD]
        public void Cambiar_fecha_y_pedido_el_id_tras_guardar_abre_el_mismo_lote()
        {
            using (new TransaccionDePrueba())
            {
                var ids = LoteDeDos(out _);
                int idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT idCliente FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", ids[0])));
                var d = Foto(ids).Despachos[0];
                string original = EditarLoteReglas.IdLoteVirtual(idCliente, d.NumeroPedido, d.FechaDespacho.Value, d.TipoOperacion,
                    d.EsInternacional, d.IdPlanta.Value);
                var antes = ListaDespachosService.ObtenerLotePorId(original);
                Assert.NotNull(antes);
                Assert.Equal(ids.OrderBy(i => i), antes.IdsDespachos.OrderBy(i => i));

                var input = SinTocar(ids);
                input.FechaDespacho = d.FechaDespacho.Value.Date.AddDays(-3);
                input.NumeroPedido = "9" + Azar.Next(100000000, 999999999).ToString("D9");
                var r = ListaDespachosService.GuardarCambiosLote(input);
                Assert.False(r.SinCambios);

                string candidato = EditarLoteReglas.IdTrasGuardar(antes, input);
                Assert.NotEqual(original, candidato);
                Assert.True(ListaDespachosReglas.EsIdLoteValido(candidato), candidato);
                var despues = ListaDespachosService.ObtenerLotePorId(candidato);
                Assert.NotNull(despues);
                Assert.Equal(ids.OrderBy(i => i), despues.IdsDespachos.OrderBy(i => i));
                Assert.Null(ListaDespachosService.ObtenerLotePorId(original));

                Assert.Equal("~/Views/ListaDespachos.aspx?lote=" + System.Web.HttpUtility.UrlEncode(candidato) + "&msg=lote_guardado",
                    EditarLoteReglas.DestinoTrasGuardar(original, candidato, false, id => ListaDespachosService.ObtenerLotePorId(id) != null));
            }
        }

        [FactBD]
        public void Quitar_factura_dejando_su_CPIC_avisa_50025()
        {
            using (new TransaccionDePrueba())
            {
                var ids = LoteDeDos(out _);
                var factura = ListaDespachosService.RegistrarFacturaLote(ids, NumeroFactura(), FechaHelper.Hoy(), 100m, "USD", "test");
                var cpic = ListaDespachosService.RegistrarCpicLote(ids, NumeroCpic(), FechaHelper.Hoy(), 10m, "test");
                Assert.Equal(factura.Id, Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT idFactura FROM CPIC WHERE idCPIC = @id", DbHelper.Param("@id", cpic.Id))));

                var input = SinTocar(ids);
                input.QuitarFactura = true;

                var ex = Assert.Throws<SqlException>(() => ListaDespachosService.GuardarCambiosLote(input));
                Assert.Equal(50025, ex.Number);
                Assert.Contains("Quite también la CPIC", ex.Message);
            }
        }

        [FactBD]
        public void Viaje_con_liquidacion_mismos_valores_no_lanza_y_cambiar_la_fecha_lanza_50024()
        {
            using (new TransaccionDePrueba())
            {
                var liquidacion = FlujoDespachoLiquidacionTests.CrearLiquidacionDePrueba();
                int id = liquidacion.Despachos[0];
                var d = Foto(new List<int> { id }).Despachos[0];
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET usuarioModificacion = 'ANTES' WHERE idDespacho = @id", DbHelper.Param("@id", id));

                // Lo que hace el código publicado (cce61fd): llamar al SP con los mismos valores y el mismo conductor.
                ActualizarConSp(id, d.FechaDespacho.Value, d.NumeroPedido, d.IdPlanta.Value, d.TipoOperacion, d.EsInternacional, d.IdConductor);
                Assert.Equal("ANTES", Despacho(id)["usuarioModificacion"]);

                // Cambiar solo el pedido desde Editar lote sí se guarda: no es dato de la liquidación.
                var input = SinTocar(new List<int> { id });
                input.NumeroPedido = d.NumeroPedido == "9900000123" ? "9900000124" : "9900000123";
                var r = ListaDespachosService.GuardarCambiosLote(input);
                Assert.False(r.SinCambios);
                Assert.Equal(input.NumeroPedido, Convert.ToString(DbHelper.EjecutarEscalar(
                    "SELECT numeroPedido FROM Despachos WHERE idDespacho = @id", DbHelper.Param("@id", id))));
                Assert.Equal("PRUEBA-EDICION", Despacho(id)["usuarioModificacion"]);
                d.NumeroPedido = input.NumeroPedido;

                // Error al final: cambiar la fecha de un despacho liquidado.
                var ex = Assert.Throws<SqlException>(() =>
                    ActualizarConSp(id, d.FechaDespacho.Value.AddDays(1), d.NumeroPedido, d.IdPlanta.Value, d.TipoOperacion, d.EsInternacional, null));
                Assert.Equal(50024, ex.Number);
                Assert.Contains("liquidación", ex.Message);
            }
        }

        [FactBD]
        public void Cambiar_el_conductor_de_un_despacho_liquidado_lanza()
        {
            using (new TransaccionDePrueba())
            {
                var liquidacion = FlujoDespachoLiquidacionTests.CrearLiquidacionDePrueba();
                var ids = new List<int> { liquidacion.Despachos[0] };
                var input = SinTocar(ids);
                input.CambiosConductores[ids[0]] = liquidacion.IdOtroConductor;

                var ex = Assert.Throws<SqlException>(() => ListaDespachosService.GuardarCambiosLote(input));
                Assert.Equal(50024, ex.Number);
            }
        }
    }
}
