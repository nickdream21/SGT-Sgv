using System;
using System.IO;
using System.Linq;
using WebSGV.Services.Liquidaciones;
using WebSGV.Services.Reportes;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// B6-7 (D4): el reporte personalizado de ReportesOrdenesViaje filtra por el estado de aprobación del
    /// flujo actual (OrdenViaje.estadoAprobacion), con una sola lista blanca para el combo, la exportación
    /// por querystring y la condición SQL. "Rechazados" (RECHAZADO) y "Completado" ya no se ofrecen.
    /// </summary>
    public class FiltroEstadoLiquidacionTests
    {
        [Fact]
        public void ListaBlanca_SonLosEstadosDelFlujoActual_EnOrden()
        {
            Assert.Equal(new[] { "TODOS", "PENDIENTE", "REABIERTO", "POR_CONFORMAR", "APROBADO" },
                FiltroEstadoLiquidacion.Permitidos.ToArray());
            Assert.Equal(EstadosOrden.Pendiente, FiltroEstadoLiquidacion.Opciones[1].Key);
            Assert.Equal(EstadosOrden.Reabierto, FiltroEstadoLiquidacion.Opciones[2].Key);
            Assert.Equal(EstadosOrden.PorConformar, FiltroEstadoLiquidacion.Opciones[3].Key);
            Assert.Equal(EstadosOrden.Aprobado, FiltroEstadoLiquidacion.Opciones[4].Key);
        }

        [Theory]
        [InlineData(null, "TODOS")]
        [InlineData("", "TODOS")]
        [InlineData("  ", "TODOS")]
        [InlineData("todos", "TODOS")]
        [InlineData(" por_conformar ", "POR_CONFORMAR")]
        [InlineData("Aprobado", "APROBADO")]
        [InlineData("REABIERTO", "REABIERTO")]
        [InlineData("PENDIENTE", "PENDIENTE")]
        public void Normalizar_AceptaLaListaBlanca(string entrada, string esperado) =>
            Assert.Equal(esperado, FiltroEstadoLiquidacion.Normalizar(entrada));

        [Theory]
        [InlineData("RECHAZADO")]   // ningún SP del flujo actual lo produce
        [InlineData("COMPLETADO")]  // es estadoViaje, no estado de aprobación
        [InlineData("CERRADO")]
        [InlineData("POR CONFORMAR")]
        [InlineData("APROBADO' OR 1=1 --")]
        public void Normalizar_RechazaLoQueNoEstaEnLaLista(string entrada)
        {
            Assert.Null(FiltroEstadoLiquidacion.Normalizar(entrada));
            Assert.Throws<ArgumentException>(() => FiltroEstadoLiquidacion.CondicionSql(entrada));
        }

        [Theory]
        [InlineData("TODOS")]
        [InlineData(null)]
        [InlineData("")]
        public void CondicionSql_Todos_NoFiltra(string estado) =>
            Assert.Equal("", FiltroEstadoLiquidacion.CondicionSql(estado));

        [Theory]
        [InlineData("PENDIENTE")]
        [InlineData("REABIERTO")]
        [InlineData("POR_CONFORMAR")]
        [InlineData("aprobado")]
        public void CondicionSql_UnEstado_FiltraPorEstadoAprobacionConParametro(string estado)
        {
            string sql = FiltroEstadoLiquidacion.CondicionSql(estado);
            Assert.Equal(" AND ov.estadoAprobacion = @Estado ", sql);
            Assert.DoesNotContain("estadoViaje", sql);
            Assert.DoesNotContain(estado, sql, StringComparison.OrdinalIgnoreCase);   // el valor va como parámetro
        }

        [Theory]
        [InlineData("TODOS", "Todos")]
        [InlineData("PENDIENTE", "Pendiente de revisión (administración)")]
        [InlineData("REABIERTO", "Devuelta al conductor para corregir")]
        [InlineData("POR_CONFORMAR", "Por conformar (conductor)")]
        [InlineData("APROBADO", "Aprobada (conformidad firmada)")]
        [InlineData("aprobado", "Aprobada (conformidad firmada)")]
        [InlineData("COMPLETADO", "Completado")]          // valor antiguo en la BD: legible, sin romper
        [InlineData("ALGO_RARO", "Algo raro")]
        [InlineData(null, "")]
        public void Etiqueta_TextoClaroParaComboYEncabezados(string estado, string esperado) =>
            Assert.Equal(esperado, FiltroEstadoLiquidacion.Etiqueta(estado));

        [Fact]
        public void Pagina_UsaLaListaBlancaUnica_YViajesActivosSinCerrado()
        {
            string raiz = RaizWeb();
            string aspx = File.ReadAllText(Path.Combine(raiz, "Views", "ReportesOrdenesViaje.aspx"));
            string cs = File.ReadAllText(Path.Combine(raiz, "Views", "ReportesOrdenesViaje.aspx.cs"));
            string js = File.ReadAllText(Path.Combine(raiz, "Scripts", "paginas", "ReportesOrdenesViaje.js"));
            string servicio = File.ReadAllText(Path.Combine(raiz, "Services", "Reportes", "ReportesOrdenesViajeService.cs"));

            // "Viajes activos" lista solo viajes ABIERTO: CERRADO siempre salía vacío, y TODOS daba lo mismo que ABIERTO (M-077).
            Assert.DoesNotContain("Value=\"CERRADO\"", aspx);
            Assert.Contains("EstadosViajePermitidos = { \"ABIERTO\" }", cs);
            Assert.DoesNotContain("'CERRADO'", js);

            // Reporte personalizado: sin opciones fijas en el .aspx; combo y exportación desde FiltroEstadoLiquidacion.
            Assert.DoesNotContain("RECHAZADO", aspx);
            Assert.DoesNotContain("RECHAZADO", js);
            Assert.Contains("EstadosPersonalizadoPermitidos = FiltroEstadoLiquidacion.Permitidos", cs);
            Assert.Contains("FiltroEstadoLiquidacion.Normalizar(q[\"estado\"])", cs);
            Assert.Contains("FiltroEstadoLiquidacion.CondicionSql(estado)", servicio);
            Assert.DoesNotContain("ov.estadoViaje = @Estado", servicio);
        }

        private static string RaizWeb() => Path.Combine(Integracion.BaseDePruebas.RaizRepo, "WebSGV");
    }
}
