using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Seguimiento de un cliente tercero contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>: la carga programada con el Word crea su viaje (tipoRuta,
    /// lugar de carga), la sección de descarga del Word le pone el lugar de entrega, y el tablero
    /// lo filtra por cliente con el STATUS de terceros.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class SeguimientoTercerosBdTests
    {
        public SeguimientoTercerosBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void CargaDePolinplast_ViajeEntregaYTablero()
        {
            using (new TransaccionDePrueba())
            {
                DateTime fecha = FechaHelper.Hoy().AddDays(6);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho = @f", DbHelper.Param("@f", fecha));

                int idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    INSERT INTO Cliente (ruc, nombre, activo, esExportador, monedaFacturacion, fechaRegistro)
                    VALUES ('20999999991', 'POLINPLAST S.A.C. (prueba)', 1, 0, 'PEN', GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);"));
                DataRow planta = DbHelper.ConsultarTabla("SELECT TOP 1 idPlanta, nombre FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta").Rows[0];
                int idConductor = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor DESC"));
                DataRow tracto = DbHelper.ConsultarTabla("SELECT TOP 1 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC").Rows[0];
                DataRow carreta = DbHelper.ConsultarTabla("SELECT TOP 1 idCarreta, placaCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC").Rows[0];

                // 1) Programar la carga: el seguimiento crea el viaje del tercero.
                var r = ProgramacionDespachoService.Programar(new List<ProgramacionDespachoService.FilaElegida>
                {
                    new ProgramacionDespachoService.FilaElegida
                    {
                        IdConductor = idConductor, IdTracto = Convert.ToInt32(tracto[0]), IdCarreta = Convert.ToInt32(carreta[0]),
                        Hora = new TimeSpan(6, 0, 0), Etiqueta = "fila 1"
                    }
                }, idCliente, Convert.ToInt32(planta["idPlanta"]), fecha, esExportacion: false, usuario: "test");
                Assert.Empty(r.Errores);

                DataRow viaje = DbHelper.ConsultarTabla(
                    "SELECT idSeguimiento, cliente, tipoRuta, lugarCarga, lugarEntrega FROM SeguimientoExportacion WHERE idDespachoOrigen = @id AND activo = 1",
                    DbHelper.Param("@id", r.Creados[0])).Rows[0];
                int idSeguimiento = Convert.ToInt32(viaje["idSeguimiento"]);
                Assert.Equal("POLINPLAST", viaje["tipoRuta"]);
                Assert.Equal("POLINPLAST", viaje["cliente"]);
                Assert.Equal(planta["nombre"].ToString().Trim().ToUpperInvariant(), viaje["lugarCarga"]);
                Assert.Equal(DBNull.Value, viaje["lugarEntrega"]);

                // 2) El Word del día siguiente trae POLINPLAST-VIRU con esa carreta: completa el lugar de entrega.
                var word = new ProgramacionTransporte
                {
                    Fecha = fecha.AddDays(1),
                    Secciones =
                    {
                        new SeccionProgramacion
                        {
                            Titulo = "POLINPLAST-VIRU",
                            Filas = { new FilaProgramacion { Chofer = "X", Tracto = tracto["placaTracto"].ToString(), Carreta = carreta["placaCarreta"].ToString(), Horario = "08:00" } }
                        }
                    }
                };
                var entregas = ProgramacionDespachoService.ProponerEntregas(word);
                var entrega = Assert.Single(entregas);
                Assert.Equal(idSeguimiento, entrega.IdSeguimiento);
                Assert.Equal("VIRU", entrega.Lugar);
                Assert.False(entrega.Relevo);
                Assert.False(entrega.YaRegistrada);

                Assert.Equal(1, ProgramacionDespachoService.RegistrarEntregas(entregas, null));
                Assert.Equal("VIRU", DbHelper.EjecutarEscalar("SELECT lugarEntrega FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento)));
                Assert.True(ProgramacionDespachoService.ProponerEntregas(word).Single().YaRegistrada);
                Assert.Equal(0, ProgramacionDespachoService.RegistrarEntregas(ProgramacionDespachoService.ProponerEntregas(word), null));

                // 3) Tablero: sale con el filtro del cliente y de terceros, no con el de Vitapro.
                var filtro = new FiltroTablero { Desde = fecha.AddDays(-1), Hasta = fecha.AddDays(1), Cliente = "POLINPLAST" };
                var v = Assert.Single(SeguimientoViajesService.ListarTablero(filtro), x => x.Id == idSeguimiento);
                Assert.Equal("UNIDAD PROGRAMADA PARA CARGA - " + planta["nombre"].ToString().Trim().ToUpperInvariant(), v.Status);
                Assert.Equal(2, v.TotalCarga);
                Assert.Equal("VIRU", v.LugarEntrega);
                filtro.Cliente = "TERCEROS";
                Assert.Contains(SeguimientoViajesService.ListarTablero(filtro), x => x.Id == idSeguimiento);
                filtro.Cliente = "VITAPRO";
                Assert.DoesNotContain(SeguimientoViajesService.ListarTablero(filtro), x => x.Id == idSeguimiento);

                // 4) Anular el despacho anula el viaje.
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE idDespacho = @id", DbHelper.Param("@id", r.Creados[0]));
                SeguimientoViajesService.SincronizarDespachos(fecha.AddDays(-1));
                Assert.Equal("CANCELADO", DbHelper.EjecutarEscalar("SELECT estado FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento)));
            }
        }

        /// <summary>
        /// Carga de Vitapro programada con el Word (pedido PENDIENTE) y, al día siguiente, la sección GUAYAQUIL-VITAPRO
        /// del Word con su pedido: el viaje toma el pedido y el tracto y chofer de descarga sin esperar el ZIP, y la
        /// sincronización no le devuelve el PENDIENTE (caso de los Word del 25, 26 y 27/10/2026).
        /// </summary>
        [FactBD]
        public void DescargaVitaproDelWord_PedidoYTractoSinElZip()
        {
            using (new TransaccionDePrueba())
            {
                DateTime fecha = FechaHelper.Hoy().AddDays(6);
                const string pedido = "4499999999";
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho = @f", DbHelper.Param("@f", fecha));

                int idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    INSERT INTO Cliente (ruc, nombre, activo, esExportador, monedaFacturacion, fechaRegistro)
                    VALUES ('20999999992', 'VITAPRO S.A. (prueba)', 1, 1, 'USD', GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);"));
                int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
                int idConductor = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor DESC"));
                var tractos = DbHelper.ConsultarTabla("SELECT TOP 2 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC").Rows;
                DataRow carreta = DbHelper.ConsultarTabla("SELECT TOP 1 idCarreta, placaCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC").Rows[0];
                string tractoDescarga = tractos[1]["placaTracto"].ToString().Trim();

                var r = ProgramacionDespachoService.Programar(new List<ProgramacionDespachoService.FilaElegida>
                {
                    new ProgramacionDespachoService.FilaElegida
                    {
                        IdConductor = idConductor, IdTracto = Convert.ToInt32(tractos[0]["idTracto"]), IdCarreta = Convert.ToInt32(carreta[0]),
                        Hora = new TimeSpan(5, 30, 0), Etiqueta = "fila 1"
                    }
                }, idCliente, idPlanta, fecha, esExportacion: true, usuario: "test");
                Assert.Empty(r.Errores);
                int idSeguimiento = Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT idSeguimiento FROM SeguimientoExportacion WHERE idDespachoOrigen = @id AND activo = 1", DbHelper.Param("@id", r.Creados[0])));
                Assert.Equal("PENDIENTE", DbHelper.EjecutarEscalar("SELECT cliente FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento)));

                // El Word trae el pedido encima de la tabla; CAMBIO es el chofer que trajo la carga.
                var word = new ProgramacionTransporte
                {
                    Fecha = fecha,
                    Secciones =
                    {
                        new SeccionProgramacion
                        {
                            Titulo = "GUAYAQUIL-VITAPRO", NumeroPedido = pedido,
                            Filas = { new FilaProgramacion { Chofer = "LUIS PONCE", Tracto = tractoDescarga, Carreta = carreta["placaCarreta"].ToString(), Cambio = "CESAR MARIN" } }
                        }
                    }
                };
                var descarga = Assert.Single(ProgramacionDespachoService.ProponerEntregas(word));
                Assert.True(descarga.EsDescargaVitapro);
                Assert.Equal(idSeguimiento, descarga.IdSeguimiento);
                Assert.True(descarga.Relevo);
                Assert.False(descarga.YaRegistrada);

                Assert.Equal(1, ProgramacionDespachoService.RegistrarEntregas(new[] { descarga }, null));
                Assert.True(ProgramacionDespachoService.ProponerEntregas(word).Single().YaRegistrada);

                // La sincronización (cada 10 min con el GPS automático) no le devuelve el PENDIENTE del despacho.
                SeguimientoViajesService.SincronizarDespachos(fecha.AddDays(-1));
                DataRow viaje = DbHelper.ConsultarTabla("SELECT cliente, tracto2, conductorDestino FROM SeguimientoExportacion WHERE idSeguimiento = @id",
                    DbHelper.Param("@id", idSeguimiento)).Rows[0];
                Assert.Equal(pedido, viaje["cliente"]);
                Assert.Equal(tractoDescarga, viaje["tracto2"]);
                Assert.Equal("LUIS PONCE", viaje["conductorDestino"]);

                // Cuando el ZIP completa el despacho con otro pedido, manda el despacho.
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET numeroPedido = '4499999998' WHERE idDespacho = @id", DbHelper.Param("@id", r.Creados[0]));
                SeguimientoViajesService.SincronizarDespachos(fecha.AddDays(-1));
                Assert.Equal("4499999998", DbHelper.EjecutarEscalar("SELECT cliente FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento)));
            }
        }
    }
}
