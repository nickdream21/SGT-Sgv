using System;
using System.Diagnostics;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using WebSGV.Services.Reportes;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Tope del tablero de seguimiento contra la BD de pruebas (B6-2), solo lectura: la exportación de un año con los
    /// importados trae los mismos viajes que un COUNT con los mismos filtros, y la tabla se corta en 500 sabiendo el total.
    /// </summary>
    public class TableroSeguimientoTopeBdTests
    {
        private readonly ITestOutputHelper _salida;

        public TableroSeguimientoTopeBdTests(ITestOutputHelper salida)
        {
            BaseDePruebas.Preparar();
            _salida = salida;
        }

        /// <summary>
        /// La regla del tablero escrita aparte (sin texto, planta ni cliente): programados en el rango y, siempre, los
        /// abiertos de los despachos o creados a mano.
        /// </summary>
        private static int Contar(DateTime desde, DateTime hasta, bool historial) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                SELECT COUNT(*) FROM SeguimientoExportacion se
                WHERE se.activo = 1
                  AND (COALESCE(se.fhProgramacion, se.fechaRegistro) >= @desde AND COALESCE(se.fhProgramacion, se.fechaRegistro) < @hasta
                       OR se.origen IN ('DESPACHO', 'MANUAL') AND ISNULL(se.estado, '') NOT IN ('FINALIZADO', 'CANCELADO') AND se.fhLlegadaBaseFinal IS NULL)
                  AND (@historial = 1 OR se.origen IN ('DESPACHO', 'MANUAL'))",
                DbHelper.Param("@desde", desde), DbHelper.Param("@hasta", hasta.AddDays(1)), DbHelper.Param("@historial", historial)));

        [FactBD]
        public void ExportarUnAnioConImportados_TraeTodosLosViajesDelCount()
        {
            var filtro = new FiltroTablero { Desde = new DateTime(2024, 1, 1), Hasta = new DateTime(2024, 12, 31), IncluirHistorial = true };
            int esperados = Contar(filtro.Desde, filtro.Hasta, true);
            Assert.True(esperados > SeguimientoViajesService.MaximoFilasTablero,
                $"La BD de pruebas debería tener más de {SeguimientoViajesService.MaximoFilasTablero} viajes en 2024 (tiene {esperados}).");

            var reloj = Stopwatch.StartNew();
            var exportacion = SeguimientoViajesService.ListarTableroConTotal(filtro, SeguimientoViajesService.MaximoFilasExportacion);
            long msConsulta = reloj.ElapsedMilliseconds;
            Assert.False(exportacion.Cortado);
            Assert.Null(exportacion.MotivoNoExportar());
            Assert.Equal(esperados, exportacion.Total);
            Assert.Equal(esperados, exportacion.Viajes.Count);
            Assert.Equal(esperados, exportacion.Viajes.Select(v => v.Id).Distinct().Count());
            // La cuenta no se cuela como columna del viaje (el STATUS GENERAL exporta la fila).
            Assert.DoesNotContain(exportacion.Viajes[0].Fila.Table.Columns.Cast<System.Data.DataColumn>(), c => c.ColumnName.StartsWith("sgv", StringComparison.OrdinalIgnoreCase));

            // El Excel del STATUS GENERAL con todos (lo que hace el botón, sin la página).
            long memoriaAntes = GC.GetTotalMemory(true);
            reloj.Restart();
            var vitapro = exportacion.Viajes.Where(v => !RutaTercero.Es(v.TipoRuta)).ToList();
            var reporte = ExportacionStatusGeneral.Construir(vitapro.Select(v => v.Fila).ToList(), new[] { "Prueba 2024" }, "Prueba",
                vitapro.ToDictionary(v => v.Id, v => v.Status));
            byte[] excel = ReporteTabularExportador.GenerarExcel(reporte, new EmpresaInfo());
            long msExcel = reloj.ElapsedMilliseconds;
            long memoriaPico = GC.GetTotalMemory(false) - memoriaAntes;
            Assert.Equal(vitapro.Count, reporte.Filas.Count);
            Assert.True(excel.Length > 0);
            _salida.WriteLine($"2024 con importados: {esperados} viajes; consulta + resumen {msConsulta} ms; STATUS GENERAL {vitapro.Count} filas, " +
                $"Excel {excel.Length / 1024} KB en {msExcel} ms; memoria administrada ~{memoriaPico / (1024 * 1024)} MB.");

            // La tabla: los 500 más recientes, en el mismo orden, sabiendo cuántos hay.
            reloj.Restart();
            var tabla = SeguimientoViajesService.ListarTableroConTotal(filtro, SeguimientoViajesService.MaximoFilasTablero);
            _salida.WriteLine($"Tabla (TOP 500 + COUNT): {reloj.ElapsedMilliseconds} ms.");
            Assert.True(tabla.Cortado);
            Assert.Equal(esperados, tabla.Total);
            Assert.Equal(SeguimientoViajesService.MaximoFilasTablero, tabla.Viajes.Count);
            Assert.Equal(exportacion.Viajes.Take(SeguimientoViajesService.MaximoFilasTablero).Select(v => v.Id), tabla.Viajes.Select(v => v.Id));
            Assert.NotNull(tabla.AvisoTabla());

            // ListarTablero (GPS automático, dashboard, grifo) sigue con los mismos 500.
            Assert.Equal(tabla.Viajes.Select(v => v.Id), SeguimientoViajesService.ListarTablero(filtro).Select(v => v.Id));

            // La imagen del STATUS con el máximo que admite (500) se genera.
            reloj.Restart();
            byte[] png = ReporteTabularExportador.GenerarImagen(ExportacionStatusUnidades.Construir(tabla.Viajes, new[] { "Prueba" }, "Prueba"), new EmpresaInfo());
            _salida.WriteLine($"Imagen STATUS de 500 viajes: {png.Length / 1024} KB en {reloj.ElapsedMilliseconds} ms.");
            Assert.True(png.Length > 0);
        }

        [FactBD]
        public void TopeMenorQueLosViajes_ElTotalSigueSiendoElDelCount()
        {
            var filtro = new FiltroTablero { Desde = new DateTime(2025, 1, 1), Hasta = new DateTime(2025, 12, 31), IncluirHistorial = true };
            int esperados = Contar(filtro.Desde, filtro.Hasta, true);
            var l = SeguimientoViajesService.ListarTableroConTotal(filtro, 3);
            Assert.Equal(Math.Min(3, esperados), l.Viajes.Count);
            Assert.Equal(esperados, l.Total);
            Assert.Equal(esperados > 3, l.Cortado);
        }
    }
}
