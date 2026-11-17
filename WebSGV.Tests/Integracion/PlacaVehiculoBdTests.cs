using System;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Services.Common;
using WebSGV.Services.Vehiculos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Placas únicas (bloque 5, B5-1; migración 50) contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>: no deja datos. Usa placas inventadas (ZZ9-99x) que no existen.
    /// Los casos de error van al final de cada prueba.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class PlacaVehiculoBdTests
    {
        public PlacaVehiculoBdTests() => BaseDePruebas.Preparar();

        private static string PlacaDe(TipoVehiculo tipo, int id) =>
            tipo == TipoVehiculo.Tracto
                ? (string)DbHelper.EjecutarEscalar("SELECT placaTracto FROM Tracto WHERE idTracto = @id", DbHelper.Param("@id", id))
                : (string)DbHelper.EjecutarEscalar("SELECT placaCarreta FROM Carreta WHERE idCarreta = @id", DbHelper.Param("@id", id));

        [FactBD]
        public void Carreta_con_la_misma_placa_sin_guion_se_rechaza()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ9-999", "RANDON", "SR GR 3E");
                Assert.Equal("ZZ9-999", PlacaDe(TipoVehiculo.Carreta, id));

                var ex = Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ9999", "RANDON", "SR GR 3E"));
                Assert.Contains("Ya existe la placa ZZ9-999 (registrada como ZZ9-999)", ex.Message);
            }
        }

        [FactBD]
        public void Tracto_se_guarda_canonico_y_no_choca_consigo_mismo_al_editar()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Tracto, "zz9998", "volvo", "fh 460");
                Assert.Equal("ZZ9-998", PlacaDe(TipoVehiculo.Tracto, id));

                // Editar sin cambiar la placa (escrita de otra forma) no es un duplicado
                VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "ZZ9 998", "VOLVO", "FH 500");
                Assert.Equal("ZZ9-998", PlacaDe(TipoVehiculo.Tracto, id));
                Assert.Equal("FH 500", (string)DbHelper.EjecutarEscalar(
                    "SELECT modelo FROM Tracto WHERE idTracto = @id", DbHelper.Param("@id", id)));

                int otro = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ9-997", "VOLVO", "FH");
                var ex = Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Tracto, otro, "zz9998", "VOLVO", "FH"));
                Assert.Contains("Ya existe la placa ZZ9-998", ex.Message);
            }
        }

        [FactBD]
        public void Edicion_de_carreta_valida_la_placa_y_la_marca()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ9-996", "RANDON", "SR");

                Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Carreta, id, "ZZ.996", "RANDON", "SR"));
                Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Carreta, id, "ZZ9-996", new string('R', 31), "SR"));
                Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Carreta, int.MaxValue, "ZZ9-995", "RANDON", "SR"));
                Assert.Equal("ZZ9-996", PlacaDe(TipoVehiculo.Carreta, id));
            }
        }

        [FactBD]
        public void Vehiculo_antiguo_con_modelo_de_un_caracter_se_edita_sin_tocar_el_modelo()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ9-993", "VOLVO", "FH");
                // Como los tractos 90-98 y 102: modelo de 1 carácter cargado antes de la validación
                DbHelper.EjecutarNonQuery("UPDATE Tracto SET modelo = '1' WHERE idTracto = @id", DbHelper.Param("@id", id));

                VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "ZZ9 992", "VOLVO", "1");
                Assert.Equal("ZZ9-992", PlacaDe(TipoVehiculo.Tracto, id));
                Assert.Equal("1", (string)DbHelper.EjecutarEscalar(
                    "SELECT modelo FROM Tracto WHERE idTracto = @id", DbHelper.Param("@id", id)));

                var ex = Assert.Throws<ErrorNegocioException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "ZZ9-992", "VOLVO", "X"));
                Assert.Contains("Modelo inválido", ex.Message);
            }
        }

        [FactBD]
        public void Indice_unico_rechaza_un_insert_directo_con_la_misma_placa()
        {
            using (new TransaccionDePrueba())
            {
                VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ9-994", "RANDON", "SR");

                // Error al final: la violación de UX_Carreta_placaCarreta
                var ex = Assert.Throws<SqlException>(() => DbHelper.EjecutarNonQuery(
                    "INSERT INTO Carreta (placaCarreta, marca, modelo, activo) VALUES ('ZZ9-994', 'X', 'Y', 1)"));
                Assert.True(ex.Number == 2601 || ex.Number == 2627, "Número de error: " + ex.Number);
                Assert.Contains("UX_Carreta_placaCarreta", ex.Message);
            }
        }
    }
}
