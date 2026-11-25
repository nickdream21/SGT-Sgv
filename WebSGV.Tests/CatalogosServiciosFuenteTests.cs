using System.IO;
using System.Text.RegularExpressions;
using WebSGV.Services.Catalogos;
using WebSGV.Services.Clientes;
using WebSGV.Tests.Integracion;
using Xunit;

namespace WebSGV.Tests
{
    /// <summary>
    /// E6-19: los catálogos traducen a "nombre/RUC repetido" solo la violación de SU índice único (por nombre); la de
    /// otro índice sigue como error técnico. Prueba de fuente (sin BD): cada <c>catch</c> de clave duplicada nombra el
    /// índice, y los nombres son los que crea la migración 64.
    /// </summary>
    public class CatalogosServiciosFuenteTests
    {
        [Theory]
        [InlineData("Clientes/ClienteService.cs", "IndiceRuc", 2)]
        [InlineData("Catalogos/PlantaService.cs", "IndiceNombreAmbito", 2)]
        [InlineData("Catalogos/EstacionPeajeService.cs", "IndiceNombre", 2)]
        public void CadaCatchDeDuplicado_NombraSuIndice(string archivo, string constante, int catches)
        {
            string cs = File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Services", archivo));
            Assert.Equal(catches, Regex.Matches(cs, @"catch \(SqlException ex\) when \(EsClaveDuplicada\(ex, " + constante + @"\)\)").Count);
            Assert.Equal(catches, Regex.Matches(cs, @"catch \(SqlException").Count);
            Assert.Contains("(ex.Number == 2601 || ex.Number == 2627) && ex.Message.IndexOf(indice, StringComparison.OrdinalIgnoreCase) >= 0", cs);
        }

        [Fact]
        public void LosNombresDeIndice_SonLosDeLaMigracion64()
        {
            string sql = File.ReadAllText(Path.Combine(BaseDePruebas.RaizRepo, "WebSGV", "Database", "Schema", "64_PlantaNombreUnico.sql"));
            Assert.Equal("UX_Planta_Nombre_Ambito", PlantaService.IndiceNombreAmbito);
            Assert.Equal("UQ_Cliente_Ruc", ClienteService.IndiceRuc);
            Assert.Equal("UK_EstacionesPeaje_Nombre", EstacionPeajeService.IndiceNombre);
            Assert.Contains("CREATE UNIQUE NONCLUSTERED INDEX UX_Planta_Nombre_Ambito ON dbo.Planta (nombre, esInternacional);", sql);
            Assert.Contains("CREATE UNIQUE INDEX UQ_Cliente_Ruc ON dbo.Cliente (ruc) WHERE ruc IS NOT NULL;", sql);
            Assert.Contains("ADD CONSTRAINT UK_EstacionesPeaje_Nombre UNIQUE (nombre);", sql);
            Assert.Contains("THROW 50064", sql);
        }
    }
}
