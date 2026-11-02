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
    /// Retorno con carga contra la BD de pruebas, dentro de una <see cref="TransaccionDePrueba"/>: la sección
    /// IMPORTACION-TRUJILLO del Word (unidades que cargaron en Ecuador y descargan en Trujillo) se une al viaje
    /// de ida de Vitapro por la carreta, lo marca como retorno con carga y el tablero lo muestra.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class RetornoConCargaBdTests
    {
        public RetornoConCargaBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void ImportacionTrujillo_MarcaElViajeDeIdaDeVitapro_YElTableroLoMuestra()
        {
            using (new TransaccionDePrueba())
            {
                DateTime fechaCarga = FechaHelper.Hoy().AddDays(5);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0 WHERE fechaDespacho = @f", DbHelper.Param("@f", fechaCarga));

                int idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 AND UPPER(nombre) LIKE '%VITAPRO%' ORDER BY idCliente"));
                int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 AND UPPER(nombre) LIKE '%TRUJILLO%' ORDER BY idPlanta"));
                int idConductor = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor DESC"));
                DataRow tracto = DbHelper.ConsultarTabla("SELECT TOP 1 idTracto, placaTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC").Rows[0];
                DataRow carreta = DbHelper.ConsultarTabla("SELECT TOP 1 idCarreta, placaCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC").Rows[0];

                // 1) La ida: carga de Vitapro programada con el Word.
                var r = ProgramacionDespachoService.Programar(new List<ProgramacionDespachoService.FilaElegida>
                {
                    new ProgramacionDespachoService.FilaElegida
                    {
                        IdConductor = idConductor, IdTracto = Convert.ToInt32(tracto[0]), IdCarreta = Convert.ToInt32(carreta[0]),
                        Hora = new TimeSpan(5, 30, 0), Etiqueta = "fila 1"
                    }
                }, idCliente, idPlanta, fechaCarga, esExportacion: true, usuario: "test");
                Assert.Empty(r.Errores);
                DataRow ida = DbHelper.ConsultarTabla(
                    "SELECT idSeguimiento, tipoRuta, retornoConCarga FROM SeguimientoExportacion WHERE idDespachoOrigen = @id AND activo = 1",
                    DbHelper.Param("@id", r.Creados[0])).Rows[0];
                int idSeguimiento = Convert.ToInt32(ida["idSeguimiento"]);
                Assert.Equal(DBNull.Value, ida["tipoRuta"]);
                Assert.False(Convert.ToBoolean(ida["retornoConCarga"]));

                // 2) El Word de seis días después trae IMPORTACION-TRUJILLO con esa carreta.
                DateTime fechaWord = fechaCarga.AddDays(6);
                var word = new ProgramacionTransporte
                {
                    Fecha = fechaWord,
                    Secciones =
                    {
                        new SeccionProgramacion
                        {
                            Titulo = "IMPORTACION-TRUJILLO",
                            Filas = { new FilaProgramacion { Chofer = "X", Tracto = tracto["placaTracto"].ToString(), Carreta = carreta["placaCarreta"].ToString(), Horario = "06:00AM" } }
                        }
                    }
                };
                var entregas = ProgramacionDespachoService.ProponerEntregas(word);
                var entrega = Assert.Single(entregas);
                Assert.True(entrega.EsRetorno);
                Assert.Equal(idSeguimiento, entrega.IdSeguimiento);
                Assert.Equal("TRUJILLO", entrega.Lugar);
                Assert.Equal(fechaWord.AddDays(1).AddHours(6), entrega.FhRetorno);
                Assert.False(entrega.YaRegistrada);

                Assert.Equal(1, ProgramacionDespachoService.RegistrarEntregas(entregas, null));
                DataRow marcado = DbHelper.ConsultarTabla(
                    "SELECT retornoConCarga, retornoDestino, fhRetornoProgramado FROM SeguimientoExportacion WHERE idSeguimiento = @id",
                    DbHelper.Param("@id", idSeguimiento)).Rows[0];
                Assert.True(Convert.ToBoolean(marcado["retornoConCarga"]));
                Assert.Equal("TRUJILLO", marcado["retornoDestino"]);
                Assert.Equal(fechaWord.AddDays(1).AddHours(6), Convert.ToDateTime(marcado["fhRetornoProgramado"]));

                // Repetir el Word no cambia nada.
                Assert.True(ProgramacionDespachoService.ProponerEntregas(word).Single().YaRegistrada);
                Assert.Equal(0, ProgramacionDespachoService.RegistrarEntregas(ProgramacionDespachoService.ProponerEntregas(word), null));

                // 3) El tablero lo muestra como viaje de Vitapro con el aviso del retorno.
                var filtro = new FiltroTablero { Desde = fechaCarga.AddDays(-1), Hasta = fechaCarga.AddDays(1), Cliente = "VITAPRO" };
                var v = Assert.Single(SeguimientoViajesService.ListarTablero(filtro), x => x.Id == idSeguimiento);
                Assert.Contains(v.Avisos, a => a.Texto.StartsWith("Vuelve de Ecuador con carga y descarga en Trujillo"));

                // 4) Sin viaje de ida con esa carreta ni tracto: queda sin unir (no inventa nada).
                var otro = new ProgramacionTransporte
                {
                    Fecha = fechaWord,
                    Secciones = { new SeccionProgramacion { Titulo = "IMPORTACION-TRUJILLO", Filas = { new FilaProgramacion { Chofer = "X", Tracto = "ZZZ-999", Carreta = "ZZZ-998", Horario = "06:00AM" } } } }
                };
                var sinViaje = Assert.Single(ProgramacionDespachoService.ProponerEntregas(otro));
                Assert.Null(sinViaje.IdSeguimiento);
                Assert.True(sinViaje.EsRetorno);
            }
        }
    }
}
