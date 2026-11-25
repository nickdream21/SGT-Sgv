using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Auditoria;
using WebSGV.Services.Usuarios;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-20: las cifras de Auditoría y del panel del administrador de sistema salieron de las páginas a
    /// <see cref="ConsultaAuditoriaService"/> y <see cref="GestionUsuariosService"/>. Cada lectura se fija contra la
    /// consulta que tenía la página (copiada aquí tal cual), y "registros de hoy" (ahora por rango de la hora de
    /// Perú, uno solo para las dos páginas) contra el <c>CAST(FechaHora AS DATE) = CAST(dbo.fn_AhoraPeru() AS DATE)</c>
    /// de antes, con registros en los bordes del día. Los que escriben van en una <see cref="TransaccionDePrueba"/>.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class PanelAdminSistemaBdTests
    {
        public PanelAdminSistemaBdTests() => BaseDePruebas.Preparar();

        private const string HoyAntes =
            "SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(dbo.fn_AhoraPeru() AS DATE)";

        private static int Escalar(string sql) => Convert.ToInt32(DbHelper.EjecutarEscalar(sql));

        private static string Filas(DataTable dt) =>
            string.Join("\n", dt.Rows.Cast<DataRow>().Select(r => string.Join("|", r.ItemArray.Select(v => Convert.ToString(v)))));

        private static void Registro(DateTime fechaHora) =>
            DbHelper.EjecutarNonQuery(
                "INSERT INTO AuditoriaLog (FechaHora, NombreUsuario, RolUsuario, Accion, TablaAfectada, Descripcion) " +
                "VALUES (@f, 'prueba.e620', 'ADMINISTRADOR DE SISTEMA', 'PRUEBA_E620', 'PruebaE620', 'borde del día (test)')",
                DbHelper.Param("@f", fechaHora));

        [FactBD]
        public void RegistrosDeHoy_PorRango_CuentaLosBordesDelDiaComoElCastDeAntes()
        {
            using (new TransaccionDePrueba())
            {
                int antes = ConsultaAuditoriaService.ContarRegistrosDeHoy();
                Assert.Equal(Escalar(HoyAntes), antes);

                DateTime hoy = FechaHelper.Hoy();
                Registro(hoy);                                      // 00:00:00.000 de hoy: cuenta
                Registro(hoy.AddDays(1).AddMilliseconds(-3));       // 23:59:59.997 de hoy: cuenta
                Registro(hoy.AddMilliseconds(-3));                  // 23:59:59.997 de ayer: no
                Registro(hoy.AddDays(1));                           // 00:00 de mañana: no

                int despues = ConsultaAuditoriaService.ContarRegistrosDeHoy();
                Assert.Equal(antes + 2, despues);
                Assert.Equal(Escalar(HoyAntes), despues);
                Assert.Equal(despues, ConsultaAuditoriaService.ObtenerResumen().Hoy);
            }
        }

        [FactBD]
        public void Resumen_IgualALaConsultaQueTeniaLaPagina()
        {
            DataRow r = DbHelper.ConsultarTabla(@"
                    SELECT
                        (SELECT COUNT(*) FROM AuditoriaLog) AS TotalRegistros,
                        (SELECT COUNT(*) FROM AuditoriaLog WHERE CAST(FechaHora AS DATE) = CAST(dbo.fn_AhoraPeru() AS DATE)) AS RegistrosHoy,
                        (SELECT COUNT(DISTINCT NombreUsuario) FROM AuditoriaLog WHERE FechaHora >= DATEADD(DAY, -7, dbo.fn_AhoraPeru())) AS UsuariosActivos,
                        (SELECT COUNT(DISTINCT TablaAfectada) FROM AuditoriaLog) AS TablasAfectadas").Rows[0];
            ResumenAuditoria s = ConsultaAuditoriaService.ObtenerResumen();
            Assert.NotNull(s);
            Assert.Equal(Convert.ToInt32(r["TotalRegistros"]), s.Total);
            Assert.Equal(Convert.ToInt32(r["RegistrosHoy"]), s.Hoy);
            Assert.Equal(Convert.ToInt32(r["UsuariosActivos"]), s.Usuarios7Dias);
            Assert.Equal(Convert.ToInt32(r["TablasAfectadas"]), s.Tablas);
            Assert.True(s.Total > 0, "La BD de pruebas debería tener registros de auditoría.");
        }

        [FactBD]
        public void TablasYUltimosEventos_IgualesALasConsultasQueTeniaLaPagina()
        {
            var antes = DbHelper.ConsultarTabla("SELECT DISTINCT TablaAfectada FROM AuditoriaLog ORDER BY TablaAfectada")
                .Rows.Cast<DataRow>().Select(f => f["TablaAfectada"].ToString()).ToList();
            Assert.Equal(antes, ConsultaAuditoriaService.ObtenerTablas());

            DataTable ultimos = ConsultaAuditoriaService.ObtenerUltimosEventos();
            Assert.Equal(new[] { "NombreUsuario", "Accion", "TablaAfectada", "FechaHora" },
                ultimos.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
            Assert.Equal(8, ultimos.Rows.Count);
            // Mismas fechas en el mismo orden (la consulta no desempata registros del mismo instante).
            var fechasAntes = DbHelper.ConsultarTabla(
                    "SELECT TOP 8 NombreUsuario, Accion, TablaAfectada, FechaHora FROM AuditoriaLog ORDER BY FechaHora DESC")
                .Rows.Cast<DataRow>().Select(f => Convert.ToDateTime(f["FechaHora"])).ToList();
            Assert.Equal(fechasAntes, ultimos.Rows.Cast<DataRow>().Select(f => Convert.ToDateTime(f["FechaHora"])).ToList());
        }

        [FactBD]
        public void Usuarios_PorEstadoYPorRol_IgualesALasConsultasQueTeniaLaPagina()
        {
            DataRow r = DbHelper.ConsultarTabla(@"
                SELECT
                    COUNT(*) AS total,
                    ISNULL(SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END), 0) AS activos,
                    ISNULL(SUM(CASE WHEN activo = 0 THEN 1 ELSE 0 END), 0) AS inactivos
                FROM Usuarios").Rows[0];
            var (total, activos, inactivos) = GestionUsuariosService.ContarPorEstado();
            Assert.Equal(Convert.ToInt32(r["total"]), total);
            Assert.Equal(Convert.ToInt32(r["activos"]), activos);
            Assert.Equal(Convert.ToInt32(r["inactivos"]), inactivos);
            Assert.Equal(total, activos + inactivos);
            Assert.Equal(GestionUsuariosService.Totales(), (total, activos));

            // Por rol: el orden entre roles con el mismo total no está fijado; se comparan como conjunto.
            string porRolAntes = Filas(Ordenar(DbHelper.ConsultarTabla("SELECT rol, COUNT(*) AS total FROM Usuarios GROUP BY rol ORDER BY total DESC")));
            DataTable porRol = GestionUsuariosService.ContarPorRol();
            Assert.Equal(new[] { "rol", "total" }, porRol.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
            Assert.Equal(porRolAntes, Filas(Ordenar(porRol)));
            var totales = porRol.Rows.Cast<DataRow>().Select(f => Convert.ToInt32(f["total"])).ToList();
            Assert.Equal(totales.OrderByDescending(n => n).ToList(), totales);
        }

        private static DataTable Ordenar(DataTable dt)
        {
            var v = dt.DefaultView;
            v.Sort = "total DESC, rol ASC";
            return v.ToTable();
        }
    }
}
