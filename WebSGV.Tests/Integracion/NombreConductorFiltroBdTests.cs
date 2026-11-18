using System;
using System.Data;
using WebSGV.Helpers;
using WebSGV.Services.Liquidaciones;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Solo lectura: el nombre que LiquidacionesPendientes vuelve a poner en el cuadro Conductor tras
    /// filtrar es el mismo texto de la sugerencia que eligió el usuario (BuscarConductores).
    /// </summary>
    public class NombreConductorFiltroBdTests
    {
        public NombreConductorFiltroBdTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void NombreConductor_EsIgualAlDeLaSugerencia()
        {
            object nombreBase = DbHelper.EjecutarEscalar(@"
                SELECT TOP 1 RTRIM(c.nombre) FROM Conductor c
                INNER JOIN Usuarios u ON c.idConductor = u.idConductor
                WHERE c.activo = 1 AND LEN(RTRIM(c.nombre)) >= 2
                ORDER BY c.idConductor");
            Assert.True(nombreBase != null && nombreBase != DBNull.Value, "La BD de pruebas no tiene conductores con usuario.");

            DataTable sugerencias = LiquidacionesPendientesService.BuscarConductores(nombreBase.ToString());
            Assert.True(sugerencias.Rows.Count > 0);
            foreach (DataRow fila in sugerencias.Rows)
            {
                int id = Convert.ToInt32(fila["idConductor"]);
                Assert.Equal(fila["nombreCompleto"].ToString().Trim(), LiquidacionesPendientesService.NombreConductor(id));
            }
        }

        [FactBD]
        public void NombreConductor_Inexistente_Vacio()
        {
            Assert.Equal(string.Empty, LiquidacionesPendientesService.NombreConductor(-1));
        }
    }
}
