using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Operacion;
using Xunit;
using Xunit.Abstractions;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// "Estado del sistema" contra la BD de pruebas (E6-12), solo lectura: versión del esquema, sp_OP_ConsultasCostosas
    /// y el tiempo límite de la consulta del Query Store.
    /// </summary>
    public class EstadoSistemaBdTests
    {
        private readonly ITestOutputHelper _salida;

        public EstadoSistemaBdTests(ITestOutputHelper salida)
        {
            BaseDePruebas.Preparar();
            _salida = salida;
        }

        [FactBD]
        public void SpConsultasCostosas_DevuelveSusColumnasYComoMucho10()
        {
            using (DataTable t = DbHelper.ConsultarTablaSp("sp_OP_ConsultasCostosas", DbHelper.Param("@Top", 10), DbHelper.Param("@Dias", 7)))
            {
                Assert.Equal(new[] { "consulta", "ejecuciones", "cpuTotalMs", "cpuMediaMs", "duracionMediaMs" },
                    t.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
                Assert.True(t.Rows.Count <= 10);
                // El texto llega crudo (hasta 4000): la limpieza y el recorte a 200 los hace el servicio.
                foreach (DataRow r in t.Rows)
                    Assert.True(((string)r["consulta"]).Length <= 4000);
            }
            // Fuera de rango se acota (sin error).
            using (DataTable t = DbHelper.ConsultarTablaSp("sp_OP_ConsultasCostosas", DbHelper.Param("@Top", 0), DbHelper.Param("@Dias", 999)))
                Assert.True(t.Rows.Count <= 10);
        }

        [FactBD]
        public void Calcular_ConLaBd_DiceLaVersionDelEsquemaYLasConsultasSinValores()
        {
            string carpeta = Path.Combine(Path.GetTempPath(), "sgv-estado-bd-" + Guid.NewGuid().ToString("N"));
            EstadoDelSistema e = EstadoSistemaService.Calcular(carpeta);
            _salida.WriteLine("BD {0}, {1} migraciones, consultas {2} ({3}), {4} ms",
                e.Bd.Conectada, e.Bd.Migraciones, e.ConsultasDisponibles, e.Consultas.Count, e.MsCalculo);

            Assert.True(e.Bd.Conectada);
            Assert.True(e.Bd.Migraciones > 60);
            Assert.EndsWith(".sql", e.Bd.UltimaMigracion);
            Assert.False(e.Log.CarpetaExiste);
            foreach (ConsultaCostosa c in e.Consultas)
            {
                Assert.True(c.Consulta.Length <= EstadoSistemaService.LargoMaximoConsulta);
                Assert.DoesNotContain("\n", c.Consulta);
                Assert.False(c.Consulta.StartsWith("(@", StringComparison.Ordinal), "sin la lista de parámetros: " + c.Consulta);
                // Ningún literal de texto con su valor (solo quedan '?') ni comentarios.
                Assert.DoesNotContain("'", c.Consulta.Replace("'?'", ""));
                Assert.DoesNotContain("--", c.Consulta);
                Assert.DoesNotContain("/*", c.Consulta);
            }
        }

        [FactBD]
        public void LeerConsultas_SiTardaMasQueElLimite_NoDisponibleSinEsperar()
        {
            var reloj = Stopwatch.StartNew();
            bool disponible = EstadoSistemaService.LeerConsultas(
                c => new SqlCommand("WAITFOR DELAY '00:00:06'; SELECT 1 AS x;", c), 1, out List<ConsultaCostosa> consultas);
            reloj.Stop();
            _salida.WriteLine("No disponible en {0} ms", reloj.ElapsedMilliseconds);

            Assert.False(disponible);
            Assert.Empty(consultas);
            Assert.True(reloj.ElapsedMilliseconds < 5000, "esperó " + reloj.ElapsedMilliseconds + " ms con 1 s de límite");
            Assert.Equal(8, EstadoSistemaService.SegundosConsultas);
        }
    }
}
