using System.Data.SqlClient;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Migración 47: AuditoriaLog es de solo inserción. Cada intento va en su propia
    /// <see cref="TransaccionDePrueba"/> porque el THROW del trigger aborta la transacción.
    /// El WHERE no apunta a ninguna fila real: el trigger INSTEAD OF se dispara igual
    /// (es por sentencia), así que sin el trigger la prueba falla por no lanzar la excepción.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class AuditoriaInalterableTests
    {
        public AuditoriaInalterableTests() => BaseDePruebas.Preparar();

        [FactBD]
        public void ModificarAuditoriaLog_LoRechazaElTrigger()
        {
            using (new TransaccionDePrueba())
            {
                var ex = Assert.Throws<SqlException>(() => DbHelper.EjecutarNonQuery(
                    "UPDATE AuditoriaLog SET Descripcion = 'alterado' WHERE IdAuditoria = -1"));
                Assert.Equal(51000, ex.Number);
                Assert.Contains("solo inserción", ex.Message);
            }
        }

        [FactBD]
        public void BorrarAuditoriaLog_LoRechazaElTrigger()
        {
            using (new TransaccionDePrueba())
            {
                var ex = Assert.Throws<SqlException>(() => DbHelper.EjecutarNonQuery(
                    "DELETE FROM AuditoriaLog WHERE IdAuditoria = -1"));
                Assert.Equal(51000, ex.Number);
                Assert.Contains("solo inserción", ex.Message);
            }
        }
    }
}
