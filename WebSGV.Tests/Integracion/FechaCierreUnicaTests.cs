using System;
using System.IO;
using System.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Los viajes que cierra una misma liquidación comparten la misma <c>fechaCierre</c>: así los reconocen
    /// como "hermanos" sp_RechazarLiquidacion, sp_DC_RetirarLiquidacion, sp_LD_AnularLote,
    /// sp_LD_ActualizarDespachoEnLote y sp_LD_ReubicarDespachoEnViajeDelConductor (comparan por igualdad).
    /// <c>dbo.fn_AhoraPeru()</c> es una UDF escalar no inlineable que SQL Server evalúa por fila, así que en un
    /// UPDATE de varias filas cada viaje podía quedar con otra hora (causa de las pruebas intermitentes
    /// ViajeHermanoDeUnaLiquidacion_NoSeAnulaYAvisa y ConVariosViajesAbiertos_RechazarYRetirarReabren…).
    /// La hora se toma una vez en <c>@ahora</c>.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class FechaCierreUnicaTests
    {
        private static string Fuente(params string[] ruta) =>
            File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, Path.Combine(ruta)));

        [Theory]
        [InlineData("WebSGV/Database/StoredProcedures/sp_DC_CerrarViajesProgreso.sql")]
        [InlineData("WebSGV/Services/Liquidaciones/CierreViajesSinLiquidacion.cs")]
        public void El_cierre_de_viajes_toma_la_hora_una_sola_vez(string ruta)
        {
            string texto = Fuente(ruta.Split('/'));
            Assert.Contains("DECLARE @ahora DATETIME = dbo.fn_AhoraPeru();", texto);
            Assert.Matches(@"fechaCierre\s*=\s*@ahora", texto);
            Assert.DoesNotMatch(@"fechaCierre\s*=\s*dbo\.fn_AhoraPeru\(\)", texto);
        }

        [Fact]
        public void sp_DC_CerrarViajesProgreso_no_evalua_la_hora_por_fila()
        {
            string sp = Fuente("WebSGV", "Database", "StoredProcedures", "sp_DC_CerrarViajesProgreso.sql");
            Assert.Matches(@"fechaModificacion\s*=\s*@ahora", sp);
            // Solo la declaración de @ahora llama a la función (sin contar los comentarios "--").
            string codigo = System.Text.RegularExpressions.Regex.Replace(sp, @"--[^\r\n]*", "");
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(codigo, @"dbo\.fn_AhoraPeru\(\)").Cast<object>());
        }

        [FactBD]
        public void Liquidacion_de_veinte_viajes_los_cierra_con_la_misma_fechaCierre()
        {
            BaseDePruebas.Preparar();
            using (new TransaccionDePrueba())
            {
                var liquidacion = FlujoDespachoLiquidacionTests.CrearLiquidacionDePrueba(viajes: 20);
                Assert.Equal(20, liquidacion.Viajes.Count);

                string ids = string.Join(",", liquidacion.Viajes);
                var fila = DbHelper.ConsultarTabla(@"
                    SELECT COUNT(*) AS viajes,
                           COUNT(fechaCierre) AS conFecha,
                           COUNT(DISTINCT fechaCierre) AS fechasDistintas,
                           SUM(CASE WHEN estadoViaje = 'CERRADO' THEN 1 ELSE 0 END) AS cerrados
                    FROM ViajesEnProgreso
                    WHERE idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@ids, ','))",
                    DbHelper.Param("@ids", ids)).Rows[0];

                Assert.Equal(20, Convert.ToInt32(fila["viajes"]));
                Assert.Equal(20, Convert.ToInt32(fila["conFecha"]));
                Assert.Equal(20, Convert.ToInt32(fila["cerrados"]));
                Assert.Equal(1, Convert.ToInt32(fila["fechasDistintas"]));
            }
        }
    }
}
