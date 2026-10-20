using System;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Seguimiento de exportación contra la BD de pruebas (SeguimientoHito y los sp_SE_* de la
    /// pantalla nueva), dentro de una <see cref="TransaccionDePrueba"/>: no deja datos. El pedido
    /// y las placas son inventados.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class SeguimientoViajesBdTests
    {
        public SeguimientoViajesBdTests() => BaseDePruebas.Preparar();

        private const string Pedido = "9900000123";

        private static int RegistrarGps(int id, string columna, DateTime hora, string fuente = "GPS") =>
            Convert.ToInt32(DbHelper.EjecutarEscalarSp("sp_SE_RegistrarHitoGps",
                DbHelper.Param("@idSeguimiento", id), DbHelper.Param("@columna", columna), DbHelper.Param("@horaGps", hora),
                DbHelper.Param("@fuenteGps", fuente), DbHelper.Param("@latitud", -8.135571m), DbHelper.Param("@longitud", -79.0132m),
                DbHelper.Param("@regla", "Primer punto dentro de la planta")));

        private static int Despacho(string tipo, bool internacional, int idTracto, int idConductor, int idCarreta, int idCliente,
            int? idPlanta, int? idDespachoCarga, string pedido) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                                       lugarOperacion, tipoOperacion, numeroPedido, esInternacional, esExportacion, idPlanta, idDespachoCarga, activo)
                OUTPUT INSERTED.idDespacho
                VALUES (@num, CAST(dbo.fn_AhoraPeru() AS DATE), '08:00', @cond, @tracto, @carreta, @cliente,
                        @lugar, @tipo, @pedido, @intl, 1, @planta, @carga, 1)",
                DbHelper.Param("@num", "ZZ-" + tipo), DbHelper.Param("@cond", idConductor), DbHelper.Param("@tracto", idTracto),
                DbHelper.Param("@carreta", idCarreta), DbHelper.Param("@cliente", idCliente),
                DbHelper.Param("@lugar", internacional ? "Guayaquil" : "Trujillo"), DbHelper.Param("@tipo", tipo),
                DbHelper.Param("@pedido", pedido), DbHelper.Param("@intl", internacional), DbHelper.Param("@planta", idPlanta),
                DbHelper.Param("@carga", idDespachoCarga)));

        private static int Primero(string sql) => Convert.ToInt32(DbHelper.EjecutarEscalar(sql));

        [FactBD]
        public void Sincronizar_CreaElViajeDesdeLaCargaYLoCompletaConLaDescarga()
        {
            using (new TransaccionDePrueba())
            {
                const string pedido = "9900000456";
                int tracto1 = Primero("SELECT MIN(idTracto) FROM Tracto"), tracto2 = Primero("SELECT MAX(idTracto) FROM Tracto");
                int chofer1 = Primero("SELECT MIN(idConductor) FROM Conductor"), chofer2 = Primero("SELECT MAX(idConductor) FROM Conductor");
                int carreta = Primero("SELECT MIN(idCarreta) FROM Carreta"), cliente = Primero("SELECT MIN(idCliente) FROM Cliente");
                int jave = Primero("SELECT idPlanta FROM Planta WHERE nombre = 'Jave'");
                DateTime desde = DateTime.Today.AddDays(-1);

                int idCarga = Despacho("CARGA", false, tracto1, chofer1, carreta, cliente, null, null, pedido);
                Assert.True(SeguimientoViajesService.SincronizarDespachos(desde).Item1 >= 1);
                var v = SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = desde, Hasta = DateTime.Today, Texto = pedido }).Single();
                Assert.Equal("DESPACHO", v.Origen);
                Assert.Equal(Convert.ToString(DbHelper.EjecutarEscalar("SELECT placaTracto FROM Tracto WHERE idTracto = @id", DbHelper.Param("@id", tracto1))).Trim(), v.Tracto1);
                Assert.Equal(Convert.ToString(DbHelper.EjecutarEscalar("SELECT placaCarreta FROM Carreta WHERE idCarreta = @id", DbHelper.Param("@id", carreta))).Trim(), v.Carreta);
                Assert.Null(v.Tracto2);
                Assert.Equal("Programado", v.Fase.Nombre);

                // La descarga en Ecuador completa el viaje (tracto, chofer y planta).
                Despacho("DESCARGA", true, tracto2, chofer2, carreta, cliente, jave, idCarga, pedido);
                SeguimientoViajesService.SincronizarDespachos(desde);
                v = SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = desde, Hasta = DateTime.Today, Texto = pedido }).Single();
                Assert.Equal(Convert.ToString(DbHelper.EjecutarEscalar("SELECT placaTracto FROM Tracto WHERE idTracto = @id", DbHelper.Param("@id", tracto2))).Trim(), v.Tracto2);
                Assert.Equal("JAVE", v.Planta);

                // Volver a sincronizar no duplica.
                Assert.Equal(0, SeguimientoViajesService.SincronizarDespachos(desde).Item1);
                Assert.Equal(1, Primero($"SELECT COUNT(*) FROM SeguimientoExportacion WHERE idDespachoOrigen = {idCarga}"));

                // Un viaje importado del Excel (origen NULL) no sale en el tablero salvo que se pida.
                DbHelper.EjecutarNonQuery("INSERT INTO SeguimientoExportacion (cliente, fhProgramacion, estado, activo) VALUES (@p, dbo.fn_AhoraPeru(), 'EN_CURSO', 1)", DbHelper.Param("@p", pedido));
                Assert.Single(SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = desde, Hasta = DateTime.Today, Texto = pedido }));
                Assert.Equal(2, SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = desde, Hasta = DateTime.Today, Texto = pedido, IncluirHistorial = true }).Count);

                // Despacho de carga anulado: el viaje queda anulado.
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE idDespacho = @id", DbHelper.Param("@id", idCarga));
                SeguimientoViajesService.SincronizarDespachos(desde);
                v = SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = desde, Hasta = DateTime.Today, Texto = pedido }).Single();
                Assert.Equal("Anulado", v.Fase.Nombre);
            }
        }

        [FactBD]
        public void ViajeNuevo_GpsLlenaVacios_ExcelYManualNoSePisan_YElTableroLoMuestra()
        {
            using (new TransaccionDePrueba())
            {
                int id = SeguimientoViajesService.GuardarDatosViaje(0, Pedido, "zzc-901", "zzt-101", "Chofer Prueba", null, null,
                    null, new DateTime(2026, 9, 30, 7, 45, 0), null);
                Assert.True(id > 0);

                // El GPS llena una columna vacía y guarda su evidencia.
                Assert.Equal(1, RegistrarGps(id, "fhIngresoPlanta", new DateTime(2026, 9, 30, 6, 1, 28)));

                // Una hora ya registrada (como las del Excel) no se pisa, pero el valor del GPS queda guardado.
                DbHelper.EjecutarNonQuery("UPDATE SeguimientoExportacion SET fhSalidaPlanta = '2026-09-30T11:55:00' WHERE idSeguimiento = @id", DbHelper.Param("@id", id));
                Assert.Equal(0, RegistrarGps(id, "fhSalidaPlanta", new DateTime(2026, 9, 30, 11, 55, 53)));

                var d = SeguimientoViajesService.ObtenerDetalle(id);
                var ingreso = d.Hitos.Single(h => h.Definicion.Columna == "fhIngresoPlanta");
                Assert.Equal(new DateTime(2026, 9, 30, 6, 1, 28), ingreso.Hora);
                Assert.Equal("GPS", ingreso.Fuente);
                Assert.Equal(-8.135571m, ingreso.Latitud);
                var salida = d.Hitos.Single(h => h.Definicion.Columna == "fhSalidaPlanta");
                Assert.Equal(new DateTime(2026, 9, 30, 11, 55, 0), salida.Hora);
                Assert.Null(salida.Fuente);
                Assert.Equal(new DateTime(2026, 9, 30, 11, 55, 53), salida.HoraGps);
                Assert.Equal("ZZC-901", d.Resumen.Carreta);
                Assert.Equal("Retorno a base", d.Resumen.Fase.Nombre);

                // Corrección manual: queda MANUAL con motivo y el GPS ya no la cambia.
                SeguimientoViajesService.CorregirHito(id, "fhIngresoPlanta", new DateTime(2026, 9, 30, 6, 5, 0), "prueba de corrección", null);
                Assert.Equal(0, RegistrarGps(id, "fhIngresoPlanta", new DateTime(2026, 9, 30, 6, 1, 28)));
                ingreso = SeguimientoViajesService.ObtenerDetalle(id).Hitos.Single(h => h.Definicion.Columna == "fhIngresoPlanta");
                Assert.Equal(new DateTime(2026, 9, 30, 6, 5, 0), ingreso.Hora);
                Assert.Equal("MANUAL", ingreso.Fuente);
                Assert.Equal("prueba de corrección", ingreso.MotivoCorreccion);

                // La llegada a planta en Ecuador corrige también la del almacén.
                SeguimientoViajesService.CorregirHito(id, "fhLlegadaPlantaEcuador", new DateTime(2026, 10, 3, 9, 58, 24), "prueba", null);
                var fila = SeguimientoViajesService.ObtenerDetalle(id).Fila;
                Assert.Equal(new DateTime(2026, 10, 3, 9, 58, 24), Convert.ToDateTime(fila["fhLlegadaAlmacen"]));

                // Datos manuales y cierre.
                SeguimientoViajesService.GuardarDatosManuales(id, null, new DateTime(2026, 9, 30, 7, 0, 0), null, null, null, null,
                    "COMPLEX", 1, 0, 2, "lluvia", null);
                var tablero = SeguimientoViajesService.ListarTablero(new FiltroTablero { Desde = DateTime.Today, Hasta = DateTime.Today, Texto = Pedido });
                var v = Assert.Single(tablero);
                Assert.Contains(v.Avisos, a => a.Texto == "Falta término de carga");
                Assert.Contains(v.Avisos, a => a.Texto == "Descarga sin tracto asignado");

                SeguimientoViajesService.CambiarEstado(id, "FINALIZADO", null);
                Assert.Equal("cerrado", SeguimientoViajesService.ObtenerDetalle(id).Resumen.Fase.Grupo);

                // Errores al final: un SP que hace ROLLBACK en su CATCH aborta la transacción de la prueba.
                Assert.ThrowsAny<Exception>(() => SeguimientoViajesService.CorregirHito(id, "fhIngresoPlanta", null, " ", null));
            }
        }
    }
}
