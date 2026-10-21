using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using WebSGV.Services.Despachos.Importacion;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Despacho en dos momentos contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>: se programa con el Word (sin documentos), el
    /// seguimiento crea el viaje con pedido PENDIENTE, y al llegar el correo cada guía
    /// completa su despacho, también la de un cambio de chofer (por descarte).
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ProgramacionDespachoBdTests
    {
        public ProgramacionDespachoBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void ProgramarConElWord_YCompletarConLasGuias()
        {
            using (new TransaccionDePrueba())
            {
                // Un día sin cargas, para no chocar con lo registrado
                DateTime fecha = FechaHelper.Hoy().AddDays(5);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho = @f", DbHelper.Param("@f", fecha));

                DataRow cliente = DbHelper.ConsultarTabla("SELECT TOP 1 idCliente, ruc FROM Cliente WHERE activo = 1 AND ruc IS NOT NULL ORDER BY idCliente").Rows[0];
                int idCliente = Convert.ToInt32(cliente["idCliente"]);
                int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
                var conductores = Ids("SELECT TOP 2 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor");
                var tractos = DbHelper.ConsultarTabla("SELECT TOP 3 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto").Rows.Cast<DataRow>().ToList();
                var carretas = Ids("SELECT TOP 2 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta");

                var filas = new List<ProgramacionDespachoService.FilaElegida>
                {
                    new ProgramacionDespachoService.FilaElegida { IdConductor = conductores[0], IdTracto = Id(tractos[0]), IdCarreta = carretas[0], Hora = new TimeSpan(5, 30, 0), Etiqueta = "fila 1" },
                    new ProgramacionDespachoService.FilaElegida { IdConductor = conductores[1], IdTracto = Id(tractos[1]), IdCarreta = carretas[1], Hora = new TimeSpan(5, 35, 0), Etiqueta = "fila 2" }
                };

                // 1) Programar: dos cargas sin documentos, con la hora del Word
                var r = ProgramacionDespachoService.Programar(filas, idCliente, idPlanta, fecha, esExportacion: true, usuario: "test");
                Assert.Empty(r.Errores);
                Assert.Equal(2, r.Creados.Count);

                DataRow d1 = DbHelper.ConsultarTabla(
                    "SELECT numeroPedido, numeroTransporte, horaDespacho, idViajeProgreso, esExportacion FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", r.Creados[0])).Rows[0];
                Assert.Equal(DBNull.Value, d1["numeroPedido"]);
                Assert.Equal(DBNull.Value, d1["numeroTransporte"]);
                Assert.Equal(new TimeSpan(5, 30, 0), d1["horaDespacho"]);
                Assert.NotEqual(DBNull.Value, d1["idViajeProgreso"]);   // la liquidación no espera al correo

                // El seguimiento de exportación ya tiene el viaje, con pedido PENDIENTE
                Assert.Equal("PENDIENTE", DbHelper.EjecutarEscalar(
                    "SELECT cliente FROM SeguimientoExportacion WHERE idDespachoOrigen = @id AND activo = 1", DbHelper.Param("@id", r.Creados[0])));

                // Programar otra vez lo mismo no duplica
                var otra = ProgramacionDespachoService.Programar(filas, idCliente, idPlanta, fecha, true, "test");
                Assert.Empty(otra.Creados);
                Assert.Equal(2, otra.Errores.Count);

                var programados = ProgramacionDespachoService.PendientesDeDocumentos(idCliente, fecha, fecha);
                Assert.Equal(2, programados.Count);

                // 2) Llega el correo: una guía del mismo tracto y otra de un chofer cambiado
                //    (la guía dice otro tracto: se une por descarte)
                var pedido = new ImportacionPedido { RucRemitente = cliente["ruc"].ToString().Trim(), NumeroPedido = "4400999999" };
                pedido.Tractos.Add(new TractoImportado
                {
                    NumeroTransporte = "T" + Sufijo(), NumeroGuia = "T660-1", FechaTraslado = fecha,
                    Placa = LectorDocumentosPedido.NormalizarPlaca(tractos[0]["placaTracto"].ToString()),
                    Lineas = { new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto Uno", Lote = "L1", Cantidad = 1188m, PesoBrutoKg = 29818.8m } }
                });
                pedido.Tractos.Add(new TractoImportado
                {
                    NumeroTransporte = "T" + Sufijo(), NumeroGuia = "T660-2", FechaTraslado = fecha,
                    Placa = LectorDocumentosPedido.NormalizarPlaca(tractos[2]["placaTracto"].ToString()),
                    Lineas = { new LineaCarga { CodigoProducto = "7000001", Descripcion = "Producto Uno", Lote = "L2", Cantidad = 1188m, PesoBrutoKg = 29818.8m } }
                });
                ImportacionPedidoService.Asociar(pedido);
                ImportacionPedidoService.UsarProgramados(pedido);

                Assert.True(pedido.Programacion.DesdeDespachos);
                Assert.Equal(r.Creados[0], pedido.Tractos[0].Programacion.IdDespachoProgramado);
                Assert.Equal(r.Creados[1], pedido.Tractos[1].Programacion.IdDespachoProgramado);
                Assert.Equal(conductores[1], pedido.Tractos[1].IdConductor);   // queda el chofer programado
                Assert.Equal(Id(tractos[1]), pedido.Tractos[1].IdTracto);

                // 3) Completar: documentos y carga al despacho programado, sin crear otro
                var lote = new LoteDespachos
                {
                    FechaProgramacion = fecha.ToString("yyyy-MM-dd"), IdCliente = idCliente, TipoOperacion = "CARGA",
                    IdPlanta = idPlanta, NumeroPedido = pedido.NumeroPedido, EsExportacion = true,
                    DestinoCarga = "CD CLIENTE MILAGRO Guayas Ecuador", UsuarioCreacion = "test"
                };
                var conductor = new ConductorLote
                {
                    IdDespachoProgramado = r.Creados[1], IdConductor = conductores[1],
                    GuiaRemitente = pedido.Tractos[1].NumeroGuia, NumeroTransporte = pedido.Tractos[1].NumeroTransporte,
                    Carga = pedido.Tractos[1].Lineas
                };
                int antes = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Despachos WHERE activo = 1"));
                Assert.Equal(r.Creados[1], RegistroDespachoService.CompletarDespachoProgramado(lote, conductor, null, null));
                Assert.Equal(antes, Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM Despachos WHERE activo = 1")));

                DataRow d2 = DbHelper.ConsultarTabla(
                    "SELECT numeroPedido, numeroTransporte, guiaRemitente, idConductor, idTracto, destinoCarga FROM Despachos WHERE idDespacho = @id",
                    DbHelper.Param("@id", r.Creados[1])).Rows[0];
                Assert.Equal("4400999999", d2["numeroPedido"]);
                Assert.Equal(conductor.NumeroTransporte, d2["numeroTransporte"]);
                Assert.Equal("T660-2", d2["guiaRemitente"]);
                Assert.Equal(conductores[1], d2["idConductor"]);
                Assert.Equal(Id(tractos[1]), d2["idTracto"]);
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM DespachoCarga WHERE idDespacho = @id", DbHelper.Param("@id", r.Creados[1]))));

                // Ya no espera documentos, y el viaje del seguimiento toma el pedido
                Assert.Single(ProgramacionDespachoService.PendientesDeDocumentos(idCliente, fecha, fecha));
                SeguimientoViajesService.SincronizarDespachos(fecha.AddDays(-1));
                Assert.Equal("4400999999", DbHelper.EjecutarEscalar(
                    "SELECT cliente FROM SeguimientoExportacion WHERE idDespachoOrigen = @id AND activo = 1", DbHelper.Param("@id", r.Creados[1])));

                // Leer el mismo correo otra vez no lo completa dos veces (error al final: el SP no abrió transacción)
                var ex = Assert.Throws<SqlException>(() => RegistroDespachoService.CompletarDespachoProgramado(lote, conductor, null, null));
                Assert.Contains("ya tiene sus documentos", ex.Message);
            }
        }

        private static string Sufijo() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();

        private static int Id(DataRow r) => Convert.ToInt32(r[0]);

        private static List<int> Ids(string sql) =>
            DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r[0])).ToList();
    }
}
