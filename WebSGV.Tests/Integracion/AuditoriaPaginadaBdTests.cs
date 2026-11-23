using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Auditoria;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E5-8 (M-103) contra la BD de pruebas, solo lectura: la grilla de Auditoría paginada en SQL trae páginas
    /// distintas, el total de COUNT(*) OVER() coincide con un COUNT(*) con los mismos filtros y el detalle del
    /// registro más grande llega completo.
    /// </summary>
    public class AuditoriaPaginadaBdTests
    {
        public AuditoriaPaginadaBdTests() => BaseDePruebas.Preparar();

        private static int[] Ids(PaginaAuditoria p) =>
            p.Filas.Rows.Cast<DataRow>().Select(r => Convert.ToInt32(r["IdAuditoria"])).ToArray();

        [FactBD]
        public void PaginaDos_TraeIdsDistintosYElTotalCoincideConElCount()
        {
            using (new TransaccionDePrueba())
            {
                var filtro = new FiltroAuditoria();
                int total = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT COUNT(*) FROM AuditoriaLog"));
                Assert.True(total > 2 * ConsultaAuditoriaService.TamanoPagina,
                    $"La BD de pruebas debería tener más de {2 * ConsultaAuditoriaService.TamanoPagina} registros de auditoría (tiene {total}).");

                PaginaAuditoria p1 = ConsultaAuditoriaService.ObtenerPagina(filtro, 0);
                PaginaAuditoria p2 = ConsultaAuditoriaService.ObtenerPagina(filtro, 1);

                Assert.Equal(total, p1.Total);
                Assert.Equal(total, p2.Total);
                Assert.Equal(1, p2.IndicePagina);
                Assert.Equal(ConsultaAuditoriaService.TamanoPagina, Ids(p1).Length);
                Assert.Equal(ConsultaAuditoriaService.TamanoPagina, Ids(p2).Length);
                Assert.Empty(Ids(p1).Intersect(Ids(p2)));
                // Orden estable: la página 2 sigue a la 1.
                Assert.True(Convert.ToDateTime(p1.Filas.Rows[ConsultaAuditoriaService.TamanoPagina - 1]["FechaHora"])
                    >= Convert.ToDateTime(p2.Filas.Rows[0]["FechaHora"]));
                Assert.DoesNotContain(p1.Filas.Columns.Cast<DataColumn>(), c => c.ColumnName.StartsWith("Valores", StringComparison.Ordinal));
                Assert.All(p1.Filas.Rows.Cast<DataRow>(), r => Assert.True((r["Descripcion"] as string ?? "").Length <= ConsultaAuditoriaService.LargoDescripcionGrilla));
            }
        }

        [FactBD]
        public void ConFiltros_ElTotalCoincideConElCountDeLosMismosFiltros()
        {
            using (new TransaccionDePrueba())
            {
                // Filtro con resultados: la tabla del registro más reciente y sus 30 días previos.
                DataTable ultimo = DbHelper.ConsultarTabla(
                    "SELECT TOP 1 TablaAfectada, FechaHora FROM AuditoriaLog ORDER BY FechaHora DESC, IdAuditoria DESC");
                Assert.Equal(1, ultimo.Rows.Count);
                string tabla = ultimo.Rows[0]["TablaAfectada"].ToString();
                DateTime hasta = Convert.ToDateTime(ultimo.Rows[0]["FechaHora"]).Date;
                var filtro = new FiltroAuditoria { Desde = hasta.AddDays(-30), Hasta = hasta, Tabla = tabla };

                int esperado = Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                    SELECT COUNT(*) FROM AuditoriaLog
                    WHERE FechaHora >= @d AND FechaHora < DATEADD(DAY, 1, @h) AND TablaAfectada = @t",
                    DbHelper.Param("@d", filtro.Desde.Value), DbHelper.Param("@h", filtro.Hasta.Value), DbHelper.Param("@t", tabla)));
                Assert.True(esperado > 0);

                PaginaAuditoria p = ConsultaAuditoriaService.ObtenerPagina(filtro, 0);
                Assert.Equal(esperado, p.Total);
                Assert.Equal(Math.Min(esperado, ConsultaAuditoriaService.TamanoPagina), p.Filas.Rows.Count);
                Assert.All(p.Filas.Rows.Cast<DataRow>(), r => Assert.Equal(tabla, r["TablaAfectada"].ToString()));

                // Una página que ya no existe con esos filtros devuelve la primera.
                PaginaAuditoria fuera = ConsultaAuditoriaService.ObtenerPagina(filtro, 100000);
                Assert.Equal(0, fuera.IndicePagina);
                Assert.Equal(esperado, fuera.Total);
            }
        }

        [FactBD]
        public void Detalle_DelRegistroMasGrandeLlegaCompleto()
        {
            using (new TransaccionDePrueba())
            {
                DataTable mayor = DbHelper.ConsultarTabla(@"
                    SELECT TOP 1 IdAuditoria, ValoresAnteriores, ValoresNuevos
                    FROM AuditoriaLog
                    ORDER BY ISNULL(LEN(ValoresAnteriores), 0) + ISNULL(LEN(ValoresNuevos), 0) DESC, IdAuditoria DESC");
                int id = Convert.ToInt32(mayor.Rows[0]["IdAuditoria"]);
                string anteriores = mayor.Rows[0]["ValoresAnteriores"] as string;
                string nuevos = mayor.Rows[0]["ValoresNuevos"] as string;
                Assert.True((anteriores ?? "").Length + (nuevos ?? "").Length > 0);

                DetalleAuditoria d = ConsultaAuditoriaService.ObtenerDetalle(id);
                Assert.True(d.Ok);
                Assert.Equal(id, d.Id);
                if ((anteriores ?? "").Length <= ConsultaAuditoriaService.MaximoCaracteresDetalle)
                {
                    Assert.Equal(anteriores, d.ValoresAnteriores);
                    Assert.False(d.AnterioresRecortados);
                }
                if ((nuevos ?? "").Length <= ConsultaAuditoriaService.MaximoCaracteresDetalle)
                {
                    Assert.Equal(nuevos, d.ValoresNuevos);
                    Assert.False(d.NuevosRecortados);
                }

                Assert.False(ConsultaAuditoriaService.ObtenerDetalle(-1).Ok);
            }
        }
    }
}
