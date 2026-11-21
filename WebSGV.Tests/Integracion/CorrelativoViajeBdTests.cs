using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;
using WebSGV.Models.Despachos;
using WebSGV.Services.Despachos;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E3-3 (M-051): el número de viaje VP-AAAA-NNN sale de dbo.Correlativo (migración 53) vía
    /// sp_SiguienteNumeroViaje, no de COUNT(*) + 1. Borrar un viaje no hace repetir su número, desde 1000
    /// lleva todas las cifras y un número ya usado se salta. Las pruebas que escriben usan el año 2099 para
    /// no bloquear la fila del año en curso, todo dentro de una <see cref="TransaccionDePrueba"/>; el caso de
    /// error va al final de su método.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class CorrelativoViajeBdTests
    {
        private const int AnioPrueba = 2099;

        public CorrelativoViajeBdTests() => BaseDePruebas.Preparar();

        private static int ConductorExistente() =>
            Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor"));

        private static void FijarCorrelativo(int anio, int ultimo)
        {
            DbHelper.EjecutarNonQuery("DELETE FROM Correlativo WHERE serie = 'VP' AND anio = @a", DbHelper.Param("@a", anio));
            DbHelper.EjecutarNonQuery("INSERT INTO Correlativo (serie, anio, ultimo) VALUES ('VP', @a, @u)",
                DbHelper.Param("@a", anio), DbHelper.Param("@u", ultimo));
        }

        /// <summary>Llama a sp_CrearViajeProgreso con la fecha dada (el servicio usa siempre la de hoy).</summary>
        private static int CrearViaje(int idConductor, DateTime fecha)
        {
            using (var conn = new SqlConnection(DbHelper.ConnectionString))
            using (var cmd = new SqlCommand("sp_CrearViajeProgreso", conn) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@idConductor", idConductor);
                cmd.Parameters.AddWithValue("@descripcion", "Prueba correlativo");
                cmd.Parameters.AddWithValue("@usuario", "PRUEBA");
                cmd.Parameters.AddWithValue("@fechaActual", fecha);
                SqlParameter id = cmd.Parameters.Add("@idViajeProgreso", SqlDbType.Int);
                id.Direction = ParameterDirection.Output;
                conn.Open();
                cmd.ExecuteNonQuery();
                return Convert.ToInt32(id.Value);
            }
        }

        private static string NumeroDe(int idViaje) =>
            Convert.ToString(DbHelper.EjecutarEscalar("SELECT numeroViajeProgreso FROM ViajesEnProgreso WHERE idViajeProgreso = @v",
                DbHelper.Param("@v", idViaje)));

        private static string Esperado(int anio, int n) => "VP-" + anio + "-" + (n < 1000 ? n.ToString("000") : n.ToString());

        [FactBD]
        public void ViajeBorrado_NoRepiteSuNumero()
        {
            using (new TransaccionDePrueba())
            {
                DbHelper.EjecutarNonQuery("DELETE FROM Correlativo WHERE serie = 'VP' AND anio = @a", DbHelper.Param("@a", AnioPrueba));
                int conductor = ConductorExistente();
                var fecha = new DateTime(AnioPrueba, 3, 15, 10, 0, 0);

                int primero = CrearViaje(conductor, fecha);
                Assert.Equal("VP-2099-001", NumeroDe(primero));

                DbHelper.EjecutarNonQuery("DELETE FROM ViajesEnProgreso WHERE idViajeProgreso = @v", DbHelper.Param("@v", primero));

                // Con COUNT(*) + 1 volvía a salir 001.
                Assert.Equal("VP-2099-002", NumeroDe(CrearViaje(conductor, fecha)));
            }
        }

        [FactBD]
        public void Desde1000_LlevaTodasLasCifras()
        {
            using (new TransaccionDePrueba())
            {
                FijarCorrelativo(AnioPrueba, 999);
                int conductor = ConductorExistente();
                var fecha = new DateTime(AnioPrueba, 6, 1, 8, 0, 0);

                Assert.Equal("VP-2099-1000", NumeroDe(CrearViaje(conductor, fecha)));
                Assert.Equal("VP-2099-1001", NumeroDe(CrearViaje(conductor, fecha)));
            }
        }

        [FactBD]
        public void NumeroYaUsado_SeSalta()
        {
            using (new TransaccionDePrueba())
            {
                FijarCorrelativo(AnioPrueba, 0);
                int conductor = ConductorExistente();
                var fecha = new DateTime(AnioPrueba, 1, 10, 9, 0, 0);

                // Viaje con el número 001 creado por fuera del correlativo.
                DbHelper.EjecutarNonQuery(@"INSERT INTO ViajesEnProgreso (numeroViajeProgreso, idConductor, fechaInicio,
                        fechaUltimaActividad, descripcionViaje, usuarioCreacion, estadoViaje, activo, cantidadDespachos, fechaCreacion)
                    VALUES ('VP-2099-001', @c, @f, @f, 'Prueba correlativo', 'PRUEBA', 'ABIERTO', 1, 0, @f)",
                    DbHelper.Param("@c", conductor), DbHelper.Param("@f", fecha));

                Assert.Equal("VP-2099-002", NumeroDe(CrearViaje(conductor, fecha)));
                Assert.Equal(2, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT ultimo FROM Correlativo WHERE serie = 'VP' AND anio = @a", DbHelper.Param("@a", AnioPrueba))));
            }
        }

        [FactBD]
        public void CorrelativoNoAceptaNegativos()
        {
            using (new TransaccionDePrueba())
            {
                DbHelper.EjecutarNonQuery("DELETE FROM Correlativo WHERE serie = 'VP' AND anio = @a", DbHelper.Param("@a", AnioPrueba));

                // Último paso: el error puede dejar abortada la transacción de la prueba.
                var ex = Assert.Throws<SqlException>(() => DbHelper.EjecutarNonQuery(
                    "INSERT INTO Correlativo (serie, anio, ultimo) VALUES ('VP', @a, -1)", DbHelper.Param("@a", AnioPrueba)));
                Assert.Contains("CK_Correlativo_ultimo", ex.Message);
            }
        }

        /// <summary>Solo lectura: ningún año quedó con el correlativo por debajo del mayor número emitido.</summary>
        [FactBD]
        public void Correlativo_NoQuedaPorDebajoDeLosNumerosEmitidos()
        {
            DataTable atrasados = DbHelper.ConsultarTabla(@"
                SELECT v.anio, v.maximo, c.ultimo
                FROM (SELECT TRY_CAST(SUBSTRING(numeroViajeProgreso, 4, 4) AS INT) AS anio,
                             MAX(TRY_CAST(SUBSTRING(numeroViajeProgreso, 9, 20) AS INT)) AS maximo
                      FROM ViajesEnProgreso
                      WHERE numeroViajeProgreso LIKE 'VP-[0-9][0-9][0-9][0-9]-[0-9]%'
                        AND SUBSTRING(numeroViajeProgreso, 9, 20) NOT LIKE '%[^0-9]%'
                      GROUP BY TRY_CAST(SUBSTRING(numeroViajeProgreso, 4, 4) AS INT)) v
                LEFT JOIN Correlativo c ON c.serie = 'VP' AND c.anio = v.anio
                WHERE c.ultimo IS NULL OR c.ultimo < v.maximo");
            Assert.True(atrasados.Rows.Count == 0,
                "Años con Correlativo por debajo del mayor número de viaje: " + string.Join(", ",
                    System.Linq.Enumerable.Select(System.Linq.Enumerable.Cast<DataRow>(atrasados.Rows), r => r["anio"] + " (máx. " + r["maximo"] + ")")));
        }

        /// <summary>
        /// Ruta real: RegistroDespachoService.CrearDespachoIndividual sin viaje abre uno con el siguiente número del
        /// año en curso. Corta a propósito: bloquea la fila del año en curso hasta el rollback.
        /// </summary>
        [FactBD]
        public void DespachoSinViaje_AbreViajeConElSiguienteNumero()
        {
            using (new TransaccionDePrueba())
            {
                DataRow d = DbHelper.ConsultarTabla(@"
                    SELECT (SELECT TOP 1 idConductor FROM Conductor WHERE activo = 1 ORDER BY idConductor) AS idConductor,
                           (SELECT TOP 1 idTracto FROM Tracto WHERE activo = 1 ORDER BY idTracto) AS idTracto,
                           (SELECT TOP 1 idCarreta FROM Carreta WHERE activo = 1 ORDER BY idCarreta) AS idCarreta,
                           (SELECT TOP 1 idCliente FROM Cliente WHERE activo = 1 ORDER BY idCliente) AS idCliente,
                           (SELECT TOP 1 idPlanta FROM Planta WHERE activo = 1 ORDER BY idPlanta) AS idPlanta").Rows[0];

                int anio = FechaHelper.Ahora().Year;
                object previo = DbHelper.EjecutarEscalar("SELECT ultimo FROM Correlativo WHERE serie = 'VP' AND anio = @a", DbHelper.Param("@a", anio));
                int ultimo = previo == null || previo == DBNull.Value ? 0 : Convert.ToInt32(previo);

                var lote = new LoteDespachos
                {
                    FechaProgramacion = FechaHelper.Hoy().ToString("yyyy-MM-dd"),
                    IdCliente = Convert.ToInt32(d["idCliente"]),
                    TipoOperacion = "CARGA",
                    EsInternacional = false,
                    IdPlanta = Convert.ToInt32(d["idPlanta"]),
                    UsuarioCreacion = "test"
                };
                lote.Conductores.Add(new ConductorLote
                {
                    IdConductor = Convert.ToInt32(d["idConductor"]),
                    IdTracto = Convert.ToInt32(d["idTracto"]),
                    IdCarreta = Convert.ToInt32(d["idCarreta"]),
                    IdViajeProgreso = null
                });

                int idDespacho = RegistroDespachoService.CrearDespachoIndividual(lote, lote.Conductores[0], null, null);
                int idViaje = Convert.ToInt32(DbHelper.EjecutarEscalar("SELECT idViajeProgreso FROM Despachos WHERE idDespacho = @d",
                    DbHelper.Param("@d", idDespacho)));

                Assert.Equal(Esperado(anio, ultimo + 1), NumeroDe(idViaje));
            }
        }
    }
}
