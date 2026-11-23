using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E5-10 contra la BD de pruebas, solo lectura: Liquidaciones aprobadas de contabilidad paginada en SQL
    /// (50 por página). La página 2 trae ids distintos de la 1, el total de COUNT(*) OVER() coincide con un
    /// COUNT(*) independiente y el rango de fechas de salida filtra. Mientras OrdenViaje no tenga aprobadas
    /// en pruebas, la prueba solo comprueba que la consulta corre y devuelve 0 (queda trivial).
    /// </summary>
    public class LiquidacionesContabilidadPaginaBdTests
    {
        public LiquidacionesContabilidadPaginaBdTests() => BaseDePruebas.Preparar();

        private const string SqlConteo = @"
            SELECT COUNT(*)
            FROM OrdenViaje ov
            INNER JOIN Conductor c ON ov.idConductor = c.idConductor
            LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
            LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
            LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
            WHERE ov.estadoAprobacion = @a
              AND (@d IS NULL OR ov.fechaSalida >= @d)
              AND (@h IS NULL OR ov.fechaSalida <= @h)";

        private static int Contar(DateTime? desde, DateTime? hasta) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(SqlConteo,
                DbHelper.Param("@a", EstadosOrden.Aprobado),
                new System.Data.SqlClient.SqlParameter("@d", SqlDbType.Date) { Value = (object)desde ?? DBNull.Value },
                new System.Data.SqlClient.SqlParameter("@h", SqlDbType.Date) { Value = (object)hasta ?? DBNull.Value }));

        private static int[] Ids(PaginaAprobadas p) =>
            p.Filas.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["idOrdenViaje"])).ToArray();

        [FactBD]
        public void Paginas_TotalYRango()
        {
            int total = Contar(null, null);
            PaginaAprobadas p1 = LiquidacionesContabilidadService.ObtenerPagina(0, "", "", null, null, 0);
            Assert.Equal(total, p1.Total);
            Assert.Equal(Math.Min(total, LiquidacionesContabilidadService.TamanoPagina), p1.Filas.Rows.Count);

            if (total > LiquidacionesContabilidadService.TamanoPagina)
            {
                PaginaAprobadas p2 = LiquidacionesContabilidadService.ObtenerPagina(0, "", "", null, null, 1);
                Assert.Equal(1, p2.IndicePagina);
                Assert.Equal(total, p2.Total);
                Assert.Empty(Ids(p1).Intersect(Ids(p2)));
                // Orden estable: la página 2 sigue a la 1 (fecha de salida descendente).
                Assert.True(Convert.ToDateTime(p1.Filas.Rows[p1.Filas.Rows.Count - 1]["fechaSalida"])
                    >= Convert.ToDateTime(p2.Filas.Rows[0]["fechaSalida"]));
            }

            // Una página que ya no existe devuelve la primera.
            PaginaAprobadas fuera = LiquidacionesContabilidadService.ObtenerPagina(0, "", "", null, null, 100000);
            Assert.Equal(0, fuera.IndicePagina);
            Assert.Equal(total, fuera.Total);

            // Rango: el de la liquidación aprobada más reciente y sus 90 días previos (o los últimos 90 días si no hay).
            object ultima = DbHelper.EjecutarEscalar("SELECT MAX(fechaSalida) FROM OrdenViaje WHERE estadoAprobacion = @a",
                DbHelper.Param("@a", EstadosOrden.Aprobado));
            DateTime hasta = ultima == null || ultima == DBNull.Value ? DateTime.Today : Convert.ToDateTime(ultima).Date;
            DateTime desde = hasta.AddDays(-LiquidacionesContabilidadService.DiasRangoPorDefecto);
            int enRango = Contar(desde, hasta);

            PaginaAprobadas r = LiquidacionesContabilidadService.ObtenerPagina(0, "", "", desde, hasta, 0);
            Assert.Equal(enRango, r.Total);
            Assert.All(r.Filas.Rows.Cast<DataRow>(), f =>
            {
                DateTime s = Convert.ToDateTime(f["fechaSalida"]);
                Assert.InRange(s, desde, hasta);
            });

            // Un rango sin nada (antes de 2000) no trae filas.
            Assert.Equal(0, LiquidacionesContabilidadService.ObtenerPagina(0, "", "", new DateTime(1990, 1, 1), new DateTime(1990, 12, 31), 0).Total);
        }
    }
}
