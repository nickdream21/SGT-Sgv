using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.Exportacion;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// E6-20: la escritura de "Importar seguimiento" salió de la página a <see cref="ImportacionSeguimientoService"/>.
    /// La tabla que arma tiene la forma de dbo.TipoSeguimientoImportacion (nombres, orden y tipos), y
    /// <see cref="ImportacionSeguimientoService.ImportarLote"/> devuelve lo mismo que sp_SE_ImportarLote llamado
    /// directo con la misma tabla. Pedido y placas inventados, todo dentro de una <see cref="TransaccionDePrueba"/>.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ImportacionSeguimientoServiceBdTests
    {
        public ImportacionSeguimientoServiceBdTests() => BaseDePruebas.Preparar();

        private const string Pedido = "9900000620";
        private static readonly DateTime Prog = new DateTime(2026, 10, 7, 8, 0, 0);

        [FactBD]
        public void TablaDeImportacion_TieneLaFormaDelTipoDeLaBd()
        {
            DataTable tipo = DbHelper.ConsultarTabla(@"
                SELECT c.name, TYPE_NAME(c.user_type_id) AS tipo
                FROM sys.table_types tt
                JOIN sys.columns c ON c.object_id = tt.type_table_object_id
                WHERE tt.name = 'TipoSeguimientoImportacion'
                ORDER BY c.column_id");
            DataTable t = ImportacionSeguimientoService.CrearTablaImportacion();

            Assert.Equal(tipo.Rows.Cast<DataRow>().Select(r => (string)r["name"]).ToArray(),
                t.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
            foreach (DataRow r in tipo.Rows)
            {
                string sql = (string)r["tipo"];
                Type esperado = sql == "int" ? typeof(int) : sql.Contains("date") ? typeof(DateTime) : typeof(string);
                Assert.True(esperado == t.Columns[(string)r["name"]].DataType, r["name"] + ": " + sql + " → " + t.Columns[(string)r["name"]].DataType.Name);
            }
        }

        private static DataTable DosFilas()
        {
            DataTable t = ImportacionSeguimientoService.CrearTablaImportacion();
            for (int i = 1; i <= 2; i++)
            {
                DataRow r = t.NewRow();
                r["fila"] = i + 1;
                r["cliente"] = Pedido;
                r["tracto1"] = "zze-62" + i;
                r["fhProgramacion"] = Prog.AddMinutes(5 * i);
                r["estado"] = "EN_CURSO";
                t.Rows.Add(r);
            }
            return t;
        }

        private static (int, int, int, int) DelSp(DataTable filas)
        {
            var p = new SqlParameter("@filas", SqlDbType.Structured) { TypeName = "dbo.TipoSeguimientoImportacion", Value = filas };
            DataRow r = DbHelper.ConsultarTablaSp("sp_SE_ImportarLote", p, DbHelper.Param("@idUsuario", null)).Rows[0];
            return (Convert.ToInt32(r["insertados"]), Convert.ToInt32(r["actualizados"]),
                    Convert.ToInt32(r["duplicadosEnArchivo"]), Convert.ToInt32(r["omitidos"]));
        }

        private static (int, int, int, int) Tupla(ResultadoImportacionLote r) => (r.Insertados, r.Actualizados, r.DuplicadosEnArchivo, r.Omitidos);

        [FactBD]
        public void ImportarLote_DevuelveLoMismoQueElSp()
        {
            using (new TransaccionDePrueba())
            {
                // Nuevos: el servicio los inserta.
                Assert.Equal((2, 0, 0, 0), Tupla(ImportacionSeguimientoService.ImportarLote(DosFilas(), null)));
                // Otra vez, por el SP directo y por el servicio: los mismos dos se actualizan.
                Assert.Equal((0, 2, 0, 0), DelSp(DosFilas()));
                Assert.Equal((0, 2, 0, 0), Tupla(ImportacionSeguimientoService.ImportarLote(DosFilas(), null)));
                Assert.Equal(2, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1 AND cliente = @c", DbHelper.Param("@c", Pedido))));

                // Una fila repetida en el archivo: la cuentan igual.
                DataTable repetida = DosFilas();
                repetida.ImportRow(repetida.Rows[1]);
                repetida.Rows[2]["fila"] = 9;
                Assert.Equal(DelSp(repetida), Tupla(ImportacionSeguimientoService.ImportarLote(repetida, null)));

                // Sin filas no llama a la BD.
                Assert.Equal((0, 0, 0, 0), Tupla(ImportacionSeguimientoService.ImportarLote(ImportacionSeguimientoService.CrearTablaImportacion(), null)));
            }
        }
    }
}
