using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using WebSGV.Helpers;
using WebSGV.Services.Despachos;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-20, solo lectura: los combos que salieron de las páginas dan lo mismo que las consultas que tenían
    /// (copiadas aquí tal cual): choferes, tractos y carretas del despacho nuevo de la Bandeja
    /// (<see cref="CatalogoNuevoDespacho"/>) y los años del Dashboard de exportación
    /// (<see cref="DashboardExportacionService.AniosConViajes"/>).
    /// </summary>
    public class CombosPaginasBdTests
    {
        public CombosPaginasBdTests() => BaseDePruebas.Preparar();

        private static List<KeyValuePair<int, string>> Antes(string sql) =>
            DbHelper.ConsultarTabla(sql).Rows.Cast<DataRow>()
                .Select(r => new KeyValuePair<int, string>(Convert.ToInt32(r[0]), Regex.Replace(Convert.ToString(r[1]), @"\s+", " "))).ToList();

        [FactBD]
        public void CatalogoNuevoDespacho_IgualALasConsultasQueTeniaLaBandeja()
        {
            CatalogoNuevoDespacho c = CatalogoNuevoDespacho.Leer();
            Assert.Equal(Antes(@"SELECT idConductor, LTRIM(RTRIM(CONCAT(nombre, ' ', apPaterno, ' ', apMaterno))) + CASE WHEN activo = 1 THEN '' ELSE ' (inactivo)' END
                                      FROM Conductor ORDER BY activo DESC, nombre, apPaterno"), c.Conductores);
            Assert.Equal(Antes("SELECT idTracto, LTRIM(RTRIM(placaTracto)) + CASE WHEN activo = 1 THEN '' ELSE ' (inactivo)' END FROM Tracto ORDER BY activo DESC, placaTracto"), c.Tractos);
            Assert.Equal(Antes("SELECT idCarreta, LTRIM(RTRIM(placaCarreta)) + CASE WHEN activo = 1 THEN '' ELSE ' (inactiva)' END FROM Carreta ORDER BY activo DESC, placaCarreta"), c.Carretas);
            Assert.NotEmpty(c.Conductores);
            Assert.NotEmpty(c.Tractos);
            Assert.NotEmpty(c.Carretas);
        }

        [FactBD]
        public void AniosConViajes_IgualALaConsultaQueTeniaElDashboard()
        {
            var antes = DbHelper.ConsultarTabla(@"
                    SELECT DISTINCT YEAR(fhProgramacion) AS anio
                    FROM SeguimientoExportacion
                    WHERE activo = 1 AND fhProgramacion IS NOT NULL")
                .Rows.Cast<DataRow>().Where(r => r["anio"] != DBNull.Value).Select(r => Convert.ToInt32(r["anio"])).OrderBy(a => a).ToList();
            Assert.Equal(antes, DashboardExportacionService.AniosConViajes().OrderBy(a => a).ToList());
            Assert.NotEmpty(antes);
        }
    }
}
