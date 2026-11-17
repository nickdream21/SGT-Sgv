using System;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Vehiculos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// Placa bloqueada con historial (bloque 5, B5-4, decisión D2) contra la BD de pruebas, dentro de una
    /// <see cref="TransaccionDePrueba"/>: no deja datos. Vehículos y despachos inventados (placas ZZ8-9xx).
    /// Los casos de error van al final de cada prueba.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class PlacaConHistorialBdTests
    {
        public PlacaConHistorialBdTests() => BaseDePruebas.Preparar();

        private static int Primero(string sql) => Convert.ToInt32(DbHelper.EjecutarEscalar(sql));

        private static string Valor(string sql, int id) =>
            (string)DbHelper.EjecutarEscalar(sql, DbHelper.Param("@id", id));

        /// <summary>Despacho de prueba (anulado: igual cuenta como historial) con el tracto y la carreta dados.</summary>
        private static int Despacho(int idTracto, int idCarreta) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                INSERT INTO Despachos (numeroDespacho, fechaDespacho, horaDespacho, idConductor, idTracto, idCarreta, idCliente,
                                       lugarOperacion, tipoOperacion, activo)
                OUTPUT INSERTED.idDespacho
                VALUES ('ZZ-B54', CAST(dbo.fn_AhoraPeru() AS DATE), '08:00', @cond, @tracto, @carreta, @cliente,
                        'Trujillo', 'CARGA', 0)",
                DbHelper.Param("@cond", Primero("SELECT MIN(idConductor) FROM Conductor")),
                DbHelper.Param("@tracto", idTracto), DbHelper.Param("@carreta", idCarreta),
                DbHelper.Param("@cliente", Primero("SELECT MIN(idCliente) FROM Cliente"))));

        private static bool ConHistorialEnGrilla(TipoVehiculo tipo, int id)
        {
            DataTable dt = DbHelper.ConsultarTabla(VehiculoService.ConsultaGrilla(tipo));
            string columnaId = tipo == TipoVehiculo.Tracto ? "idTracto" : "idCarreta";
            return (bool)dt.Rows.Cast<DataRow>().Single(r => Convert.ToInt32(r[columnaId]) == id)["conHistorial"];
        }

        [FactBD]
        public void Tracto_con_despacho_edita_marca_y_formato_pero_no_cambia_la_placa()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ8-901", "VOLVO", "FH");
                Assert.False(ConHistorialEnGrilla(TipoVehiculo.Tracto, id));

                Despacho(id, Primero("SELECT MIN(idCarreta) FROM Carreta"));
                Assert.True(ConHistorialEnGrilla(TipoVehiculo.Tracto, id));

                // Marca y modelo sí; la placa escrita de otra forma (misma clave) también
                VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "zz8 901", "SCANIA", "G420");
                Assert.Equal("ZZ8-901", Valor("SELECT placaTracto FROM Tracto WHERE idTracto = @id", id));
                Assert.Equal("SCANIA", Valor("SELECT marca FROM Tracto WHERE idTracto = @id", id));

                // Error al final: otra placa. Trae la placa guardada para que el modal la vuelva a mostrar.
                var ex = Assert.Throws<VehiculoService.PlacaBloqueadaException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "ZZ8-902", "SCANIA", "G420"));
                Assert.Equal("ZZ8-901", ex.PlacaGuardada);
                Assert.Equal("La placa de ZZ8-901 no se puede cambiar: tiene 1 despacho(s) registrados. " +
                             "Si la placa está mal, registre el vehículo correcto y desactive este.", ex.Message);
                Assert.Equal("ZZ8-901", Valor("SELECT placaTracto FROM Tracto WHERE idTracto = @id", id));
            }
        }

        [FactBD]
        public void Carreta_con_despacho_no_cambia_la_placa()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ8-903", "RANDON", "SR");
                Despacho(Primero("SELECT MIN(idTracto) FROM Tracto"), id);
                Assert.True(ConHistorialEnGrilla(TipoVehiculo.Carreta, id));

                VehiculoService.Actualizar(TipoVehiculo.Carreta, id, "ZZ8-903", "RANDON", "SR GR 3E");
                Assert.Equal("SR GR 3E", Valor("SELECT modelo FROM Carreta WHERE idCarreta = @id", id));

                var ex = Assert.Throws<VehiculoService.PlacaBloqueadaException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Carreta, id, "ZZ8-904", "RANDON", "SR GR 3E"));
                Assert.Contains("tiene 1 despacho(s) registrados", ex.Message);
                Assert.Equal("ZZ8-903", Valor("SELECT placaCarreta FROM Carreta WHERE idCarreta = @id", id));
            }
        }

        [FactBD]
        public void Sin_despachos_ni_referencias_la_placa_se_cambia()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ8-905", "VOLVO", "FH");

                VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "ZZ8-906", "VOLVO", "FH");
                Assert.Equal("ZZ8-906", Valor("SELECT placaTracto FROM Tracto WHERE idTracto = @id", id));
                Assert.False(ConHistorialEnGrilla(TipoVehiculo.Tracto, id));
            }
        }

        [FactBD]
        public void Placa_guardada_vacia_no_coincide_con_celdas_vacias_de_otras_tablas()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Tracto, "ZZ8-909", "VOLVO", "FH");
                // Placa vacía (dato antiguo) y una celda de placa vacía en DestinoDescarga: no son el mismo vehículo
                DbHelper.EjecutarNonQuery("UPDATE Tracto SET placaTracto = '' WHERE idTracto = @id", DbHelper.Param("@id", id));
                DbHelper.EjecutarNonQuery(@"
                    INSERT INTO DestinoDescarga (numeroPedido, placaCarreta, placaTracto, idPlanta, origen, usuario)
                    VALUES ('ZZ-B54', 'ZZ8911', '', @planta, 'MANUAL', 'prueba')",
                    DbHelper.Param("@planta", Primero("SELECT MIN(idPlanta) FROM Planta")));
                Assert.False(ConHistorialEnGrilla(TipoVehiculo.Tracto, id));

                VehiculoService.Actualizar(TipoVehiculo.Tracto, id, "ZZ8-910", "VOLVO", "FH");
                Assert.Equal("ZZ8-910", Valor("SELECT placaTracto FROM Tracto WHERE idTracto = @id", id));
            }
        }

        [FactBD]
        public void Referencia_solo_por_texto_de_la_placa_tambien_bloquea()
        {
            using (new TransaccionDePrueba())
            {
                int id = VehiculoService.Registrar(TipoVehiculo.Carreta, "ZZ8-907", "RANDON", "SR");
                // DestinoDescarga guarda la placa sin guion: se compara por clave
                DbHelper.EjecutarNonQuery(@"
                    INSERT INTO DestinoDescarga (numeroPedido, placaCarreta, placaTracto, idPlanta, origen, usuario)
                    VALUES ('ZZ-B54', 'ZZ8907', NULL, @planta, 'MANUAL', 'prueba')",
                    DbHelper.Param("@planta", Primero("SELECT MIN(idPlanta) FROM Planta")));
                Assert.True(ConHistorialEnGrilla(TipoVehiculo.Carreta, id));

                var ex = Assert.Throws<VehiculoService.PlacaBloqueadaException>(
                    () => VehiculoService.Actualizar(TipoVehiculo.Carreta, id, "ZZ8-908", "RANDON", "SR"));
                Assert.Contains("el semirremolque tiene historial registrado (1 destino(s) de descarga)", ex.Message);
            }
        }
    }
}
