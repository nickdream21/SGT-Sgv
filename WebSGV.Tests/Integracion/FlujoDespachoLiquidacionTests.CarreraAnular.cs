using System;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using System.Transactions;
using WebSGV.Helpers;
using WebSGV.Models.Conductor;
using WebSGV.Services.Conductor;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Carrera entre "Anular lote" y la liquidación del conductor (2026-11-17). La BD usa
    /// READ_COMMITTED_SNAPSHOT: sp_LD_AnularLote no veía la OrdenViaje que una liquidación en curso
    /// acababa de insertar y anulaba despachos que terminaban dentro de esa liquidación. Ahora la
    /// liquidación empieza con sp_DC_BloquearViajesParaLiquidar (UPDLOCK, HOLDLOCK sobre sus viajes,
    /// 50012 si alguno ya no se puede liquidar) y el número de orden se genera dentro de la misma
    /// transacción. Los casos de error van cada uno en su método.
    /// </summary>
    public partial class FlujoDespachoLiquidacionTests
    {
        private const string TextoViajeNoLiquidable = "anulado o ya se liquidó";

        private static T FueraDeLaTransaccion<T>(Func<T> lectura)
        {
            using (new TransactionScope(TransactionScopeOption.Suppress))
                return lectura();
        }

        private static void AfirmarError50012(Exception ex)
        {
            var sql = Assert.IsType<SqlException>(ex);
            Assert.Contains(sql.Errors.Cast<SqlError>(), e => e.Number == 50012);
            Assert.Contains(TextoViajeNoLiquidable, MensajeErrorHelper.ParaUsuario(ex));
        }

        [FactBD]
        public void EnviarLiquidacion_SinNumero_LoGeneraDentroDeLaTransaccion()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                LiquidacionConductorInput input = NuevaLiquidacion(datos, idViaje);
                input.NumeroOrdenViaje = null;   // como la página: el servicio lo genera

                int idOrden = LiquidacionConductorService.EnviarLiquidacion(input);

                Assert.True(idOrden > 0);
                Assert.Matches(new Regex(@"^OV-" + FechaHelper.Hoy().Year + @"-\d{6}$"), input.NumeroOrdenViaje);
                Assert.Equal(input.NumeroOrdenViaje,
                    Convert.ToString(Fila("SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id", idOrden)["numeroOrdenViaje"]));
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM Egresos WHERE numeroOrdenViaje = @n", DbHelper.Param("@n", input.NumeroOrdenViaje))));
                Assert.Equal("CERRADO", Convert.ToString(Fila(
                    "SELECT estadoViaje FROM ViajesEnProgreso WHERE idViajeProgreso = @id", idViaje)["estadoViaje"]));
            }
        }

        [FactBD]
        public void BloquearViajes_RetieneElViajeHastaElFinDeLaTransaccion()
        {
            // En la BD no hay viajes ABIERTO confirmados y abrir uno en la prueba ya lo bloquearía (X):
            // se usa un viaje CERRADO confirmado. El SP lo bloquea PRIMERO y después lanza 50012; con
            // XACT_ABORT OFF la transacción sigue abierta y el bloqueo se mantiene hasta su fin.
            var viaje = DbHelper.ConsultarTabla(@"SELECT TOP 1 v.idViajeProgreso, v.idConductor
                FROM ViajesEnProgreso v WHERE v.estadoViaje = 'CERRADO' AND v.idConductor IS NOT NULL
                ORDER BY v.idViajeProgreso DESC").Rows[0];
            int idViaje = Convert.ToInt32(viaje["idViajeProgreso"]);
            int idConductor = Convert.ToInt32(viaje["idConductor"]);
            const string conUpdlock = "SET LOCK_TIMEOUT 1000; SELECT COUNT(*) FROM ViajesEnProgreso WITH (UPDLOCK) WHERE idViajeProgreso = @v";
            const string sinPista = "SET LOCK_TIMEOUT 1000; SELECT estadoViaje FROM ViajesEnProgreso WHERE idViajeProgreso = @v";

            using (new TransaccionDePrueba())
            using (var conn = new SqlConnection(BaseDePruebas.Cadena))
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_DC_BloquearViajesParaLiquidar", conn) { CommandType = System.Data.CommandType.StoredProcedure })
                {
                    cmd.Parameters.AddWithValue("@idsViajes", idViaje.ToString());
                    cmd.Parameters.AddWithValue("@idConductor", idConductor);
                    AfirmarError50012(Record.Exception(() => cmd.ExecuteNonQuery()));
                }

                FueraDeLaTransaccion(() =>
                {
                    using (var otra = new SqlConnection(BaseDePruebas.Cadena))
                    {
                        otra.Open();
                        using (var bloqueada = new SqlCommand(conUpdlock, otra))
                        {
                            bloqueada.Parameters.AddWithValue("@v", idViaje);
                            var ex = Assert.Throws<SqlException>(() => bloqueada.ExecuteScalar());
                            Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 1222);
                        }
                        using (var lectura = new SqlCommand(sinPista, otra))
                        {
                            lectura.Parameters.AddWithValue("@v", idViaje);
                            Assert.Equal("CERRADO", Convert.ToString(lectura.ExecuteScalar()));
                        }
                    }
                    return 0;
                });
            }

            // Terminada la transacción, el viaje queda libre.
            Assert.Equal(1, FueraDeLaTransaccion(() => Convert.ToInt32(DbHelper.EjecutarEscalar(conUpdlock, DbHelper.Param("@v", idViaje)))));
        }

        [FactBD]
        public void LiquidacionSinConfirmar_RetieneLaNumeracionHastaElCommit()
        {
            // Una liquidación nueva sin número genera el suyo dentro de su transacción: el TABLOCKX de
            // sp_DC_GenerarNumeroOrden sobre OrdenViaje dura hasta el commit. Un segundo envío simultáneo
            // espera en vez de calcular el mismo OV (antes el número salía de otra conexión y el segundo
            // INSERT chocaba con la UQ). La transacción de la prueba hace de "liquidación sin confirmar".
            const string generar = @"SET LOCK_TIMEOUT 1000;
                DECLARE @n VARCHAR(50); EXEC sp_DC_GenerarNumeroOrden @numeroGenerado = @n OUTPUT; SELECT @n;";

            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);
                LiquidacionConductorInput input = NuevaLiquidacion(datos, idViaje);
                input.NumeroOrdenViaje = null;
                Assert.True(LiquidacionConductorService.EnviarLiquidacion(input) > 0);
                Assert.Matches(new Regex(@"^OV-" + FechaHelper.Hoy().Year + @"-\d{6}$"), input.NumeroOrdenViaje);

                FueraDeLaTransaccion(() =>
                {
                    using (var otra = new SqlConnection(BaseDePruebas.Cadena))
                    using (var cmd = new SqlCommand(generar, otra))
                    {
                        otra.Open();
                        var ex = Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                        Assert.Contains(ex.Errors.Cast<SqlError>(), e => e.Number == 1222);
                    }
                    return 0;
                });
            }

            // Terminada la transacción, la numeración vuelve a responder.
            Assert.Matches(new Regex(@"^OV-" + FechaHelper.Hoy().Year + @"-\d{6}$"),
                FueraDeLaTransaccion(() => DashboardConductorService.GenerarNumeroOrden()));
        }

        // ---------------- Casos de error: cada uno en su método y su transacción ----------------

        [FactBD]
        public void EnviarLiquidacion_ViajeConElLoteAnulado_Avisa50012()
        {
            int idViaje;
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                idViaje = CrearDespacho(datos, NuevoLote(datos), out int idDespacho);
                LiquidacionConductorInput input = NuevaLiquidacion(datos, idViaje);
                input.NumeroOrdenViaje = null;

                // La administración anula el lote mientras el conductor tiene la página abierta.
                Assert.Equal(1, ListaDespachosService.AnularLote(idDespacho.ToString(), "PRUEBA"));

                AfirmarError50012(Record.Exception(() => LiquidacionConductorService.EnviarLiquidacion(input)));
                Assert.Null(input.NumeroOrdenViaje);   // no se generó número
            }

            Assert.Equal(0, FueraDeLaTransaccion(() => Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM OrdenViaje WHERE idViajeProgreso = @v", DbHelper.Param("@v", idViaje)))));
        }

        [FactBD]
        public void EnviarLiquidacion_ViajeAbiertoSinDespachosActivos_Avisa50012()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out int idDespacho);
                LiquidacionConductorInput input = NuevaLiquidacion(datos, idViaje);

                // Todos sus despachos anulados aunque el viaje siguiera ABIERTO.
                ListaDespachosService.AnularLote(idDespacho.ToString(), "PRUEBA");
                DbHelper.EjecutarNonQuery("UPDATE ViajesEnProgreso SET estadoViaje = 'ABIERTO' WHERE idViajeProgreso = @v",
                    DbHelper.Param("@v", idViaje));

                AfirmarError50012(Record.Exception(() => LiquidacionConductorService.EnviarLiquidacion(input)));
            }
        }

        [FactBD]
        public void AnularLote_DespuesDeLiquidar_Avisa50010()
        {
            // Secuencial, no de concurrencia (todo ocurre en la misma transacción): con una liquidación
            // que cierra dos viajes (el segundo, creado a mano y vacío), el lote del principal ya no se
            // puede anular. La concurrencia la cubren BloquearViajes_RetieneElViajeHastaElFinDeLaTransaccion
            // y LiquidacionSinConfirmar_RetieneLaNumeracionHastaElCommit.
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out int idDespacho);
                int viaje2 = RegistroDespachoService.CrearNuevoViajeProgreso(datos.IdConductor, "Segundo viaje (test)", "test");
                LiquidacionConductorInput input = NuevaLiquidacion(datos, idViaje);
                input.IdsViajesActivos = new System.Collections.Generic.List<int> { idViaje, viaje2 };
                Assert.True(LiquidacionConductorService.EnviarLiquidacion(input) > 0);

                var ex = Record.Exception(() => ListaDespachosService.AnularLote(idDespacho.ToString(), "PRUEBA"));
                var sql = Assert.IsType<SqlException>(ex);
                Assert.Contains(sql.Errors.Cast<SqlError>(), e => e.Number == 50010);
            }
        }

        [FactBD]
        public void BloquearViajes_ViajeDeOtroConductor_Avisa50012()
        {
            using (new TransaccionDePrueba())
            {
                var datos = DatosBase.Cargar();
                int idViaje = CrearDespacho(datos, NuevoLote(datos), out _);

                AfirmarError50012(Record.Exception(() => DbHelper.EjecutarNonQuerySp("sp_DC_BloquearViajesParaLiquidar",
                    DbHelper.Param("@idsViajes", idViaje.ToString()),
                    DbHelper.Param("@idConductor", datos.IdOtroConductor))));
            }
        }
    }
}
