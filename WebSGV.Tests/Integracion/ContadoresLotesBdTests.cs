using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-29 (M-138) contra la BD de pruebas: <c>sp_LD_ContarLotesActivos</c> da los mismos contadores que el cálculo de
    /// antes (<see cref="ContadoresLotes.DesdeLotes"/> sobre todos los lotes activos de <c>sp_LD_ObtenerLotesRegistrados</c>),
    /// para varios "hoy" y por cada cliente; la lista con tope trae los primeros en el orden del SP y el total real; y un
    /// lote nuevo suma en los contadores (dentro de una <see cref="TransaccionDePrueba"/>). En la colección de escritura
    /// para que ninguna otra prueba escriba entre las dos lecturas que se comparan.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ContadoresLotesBdTests
    {
        private readonly ITestOutputHelper _salida;

        public ContadoresLotesBdTests(ITestOutputHelper salida)
        {
            BaseDePruebas.Preparar();
            _salida = salida;
        }

        private static ContadoresLotes Oraculo(DateTime hoy, int? idCliente = null) =>
            ContadoresLotes.DesdeLotes(ListaDespachosService.ObtenerLotesRegistrados(idCliente, null, null, null, null, null, "ACTIVO"), hoy);

        [FactBD]
        public void Contadores_IgualesAlCalculoDeAntes_ParaVariosDiasYPorCliente()
        {
            DateTime hoy = FechaHelper.Hoy();
            foreach (DateTime dia in new[] { hoy, hoy.AddDays(-30), hoy.AddDays(30) })
            {
                ContadoresLotes esperado = Oraculo(dia), real = ListaDespachosService.ContarLotesActivos(dia);
                _salida.WriteLine($"{dia:yyyy-MM-dd}: SP {real} | C# {esperado}");
                Assert.Equal(esperado, real);
            }

            var hoyTotal = ListaDespachosService.ContarLotesActivos(hoy);
            Assert.True(hoyTotal.LotesActivos > 0, "La BD de pruebas debería tener lotes activos.");
            Assert.True(hoyTotal.ConDocumentosPendientes > 0, "La BD de pruebas debería tener lotes con documentos pendientes.");

            var clientes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ACTIVO")
                .Select(l => l.IdCliente).Distinct().OrderBy(id => id).ToList();
            Assert.NotEmpty(clientes);
            int suma = 0;
            foreach (int idCliente in clientes)
            {
                ContadoresLotes esperado = Oraculo(hoy, idCliente), real = ListaDespachosService.ContarLotesActivos(hoy, idCliente);
                Assert.True(esperado.Equals(real), $"Cliente {idCliente}: SP {real} | C# {esperado}");
                suma += real.LotesActivos;
            }
            // Cada lote es de un cliente: la suma por cliente es el total.
            Assert.Equal(hoyTotal.LotesActivos, suma);
        }

        [FactBD]
        public void ListaConTope_TraeLosPrimerosEnElOrdenDelSp_YElTotal()
        {
            const int tope = 5;
            List<LoteRegistrado> todos = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ACTIVO");
            Assert.True(todos.Count > tope, $"La BD de pruebas debería tener más de {tope} lotes activos (tiene {todos.Count}).");

            ListadoLotes listado = ListaDespachosService.ObtenerLotesRegistradosConTotal(null, null, null, null, null, null, "ACTIVO",
                null, null, null, null, tope);
            Assert.Equal(tope, listado.Lotes.Count);
            Assert.Equal(todos.Count, listado.Total);
            Assert.True(listado.Cortado);
            Assert.Equal(todos.Take(tope).Select(l => l.IdLoteVirtual), listado.Lotes.Select(l => l.IdLoteVirtual));
            // Sin PENDIENTES, los más recientes primero.
            Assert.Equal(todos.Max(l => l.FechaCreacion), listado.Lotes[0].FechaCreacion);

            // Con PENDIENTES, los más antiguos por fecha de programación.
            List<LoteRegistrado> pendientes = ListaDespachosService.ObtenerLotesRegistrados(null, null, null, null, null, null, "ACTIVO",
                documentos: ListaDespachosService.DOCUMENTOS_PENDIENTES);
            Assert.True(pendientes.Count > 1, "La BD de pruebas debería tener más de un lote con documentos pendientes.");
            int topePendientes = Math.Min(tope, pendientes.Count - 1);
            ListadoLotes listadoPendientes = ListaDespachosService.ObtenerLotesRegistradosConTotal(null, null, null, null, null, null, "ACTIVO",
                null, null, null, ListaDespachosService.DOCUMENTOS_PENDIENTES, topePendientes);
            Assert.Equal(topePendientes, listadoPendientes.Lotes.Count);
            Assert.Equal(pendientes.Count, listadoPendientes.Total);
            Assert.True(listadoPendientes.Cortado);
            Assert.Equal(pendientes.Take(topePendientes).Select(l => l.IdLoteVirtual), listadoPendientes.Lotes.Select(l => l.IdLoteVirtual));
            var fechas = listadoPendientes.Lotes.Select(l => l.FechaProgramacion).ToList();
            Assert.Equal(fechas.OrderBy(f => f), fechas);
            Assert.True(fechas.Max() <= pendientes.Skip(topePendientes).Min(l => l.FechaProgramacion),
                "El tope dejó fuera un lote pendiente más antiguo que los mostrados.");
            Assert.Equal(pendientes.Count, ListaDespachosService.ContarLotesActivos(FechaHelper.Hoy()).ConDocumentosPendientes);

            // Un tope mayor que todos: no corta y el total es el mismo.
            ListadoLotes sinCorte = ListaDespachosService.ObtenerLotesRegistradosConTotal(null, null, null, null, null, null, "ACTIVO",
                null, null, null, null, todos.Count + 10);
            Assert.False(sinCorte.Cortado);
            Assert.Equal(todos.Count, sinCorte.Lotes.Count);
            Assert.Equal(todos.Count, sinCorte.Total);
        }

        [FactBD]
        public void LoteNuevoConDocumentosPendientes_SumaEnLosContadores()
        {
            using (new TransaccionDePrueba())
            {
                DateTime hoy = FechaHelper.Hoy();
                DateTime fecha = hoy.AddDays(6);
                DbHelper.EjecutarNonQuery("UPDATE Despachos SET activo = 0, estadoDespacho = 'ANULADO' WHERE fechaDespacho = @f", DbHelper.Param("@f", fecha));

                ContadoresLotes antes = ListaDespachosService.ContarLotesActivos(hoy);
                ContadoresLotes antesDentroDe10 = ListaDespachosService.ContarLotesActivos(hoy.AddDays(10));

                int idCliente = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 AND UPPER(nombre) LIKE '%VITAPRO%' ORDER BY idCliente"));
                int idPlanta = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 AND esInternacional = 0 ORDER BY idPlanta"));
                DataTable conductores = DbHelper.ConsultarTabla("SELECT TOP 2 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor DESC");
                DataTable tractos = DbHelper.ConsultarTabla("SELECT TOP 2 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto DESC");
                DataTable carretas = DbHelper.ConsultarTabla("SELECT TOP 2 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta DESC");
                var filas = Enumerable.Range(0, 2).Select(i => new ProgramacionDespachoService.FilaElegida
                {
                    IdConductor = Convert.ToInt32(conductores.Rows[i][0]),
                    IdTracto = Convert.ToInt32(tractos.Rows[i][0]),
                    IdCarreta = Convert.ToInt32(carretas.Rows[i][0]),
                    Hora = new TimeSpan(6 + i, 0, 0),
                    Etiqueta = "fila " + (i + 1)
                }).ToList();
                var r = ProgramacionDespachoService.Programar(filas, idCliente, idPlanta, fecha, esExportacion: false, usuario: "test");
                Assert.Empty(r.Errores);
                Assert.Equal(2, r.Creados.Count);

                // Hoy: un lote activo más, con documentos pendientes; todavía no atrasado (es de dentro de 6 días).
                ContadoresLotes despues = ListaDespachosService.ContarLotesActivos(hoy);
                Assert.Equal(antes.LotesActivos + 1, despues.LotesActivos);
                Assert.Equal(antes.ConDocumentosPendientes + 1, despues.ConDocumentosPendientes);
                Assert.Equal(antes.Atrasados, despues.Atrasados);
                Assert.Equal(antes.ConManifiestosPendientes, despues.ConManifiestosPendientes); // nacional: sin manifiesto
                Assert.Equal(Oraculo(hoy), despues);

                // Dentro de 10 días lleva 4 sin documentos: atrasado.
                ContadoresLotes despuesDentroDe10 = ListaDespachosService.ContarLotesActivos(hoy.AddDays(10));
                Assert.Equal(antesDentroDe10.Atrasados + 1, despuesDentroDe10.Atrasados);
                Assert.Equal(Oraculo(hoy.AddDays(10)), despuesDentroDe10);

                // Y por cliente también.
                Assert.Equal(Oraculo(hoy, idCliente), ListaDespachosService.ContarLotesActivos(hoy, idCliente));
            }
        }
    }
}
