using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using Xunit;

namespace WebSGV.Tests.Integracion
{
    /// <summary>
    /// sp_SE_ImportarLote contra la BD de pruebas (B6-3, decisión D2): la importación del Excel solo
    /// empareja viajes importados (origen NULL). Un viaje de despachos o creado a mano no se toca
    /// (estado, hitos GPS) ni se duplica: se cuenta en "omitidos". Todo dentro de una
    /// <see cref="TransaccionDePrueba"/>; el pedido y las placas son inventados.
    /// </summary>
    [Collection(TransaccionDePrueba.ColeccionEscritura)]
    public class ImportarLoteSeguimientoBdTests
    {
        public ImportarLoteSeguimientoBdTests() => BaseDePruebas.Preparar();

        private const string Pedido = "9900000777";
        private static readonly DateTime Prog = new DateTime(2026, 10, 5, 7, 30, 0);

        /// <summary>
        /// Programación de cada tracto: como en la realidad, los camiones de un mismo pedido salen con
        /// minutos de diferencia (zzi-101 → 07:35, zzi-102 → 07:40...). Mismo pedido + misma hora = mismo viaje.
        /// </summary>
        private static DateTime ProgDe(string tracto) => Prog.AddMinutes(5 * (int.Parse(tracto.Substring(4)) - 100));

        /// <summary>Tabla con las columnas de dbo.TipoSeguimientoImportacion, en su orden (leídas de la BD).</summary>
        private static DataTable TablaDelTipo()
        {
            var columnas = DbHelper.ConsultarTabla(@"
                SELECT c.name, TYPE_NAME(c.user_type_id) AS tipo
                FROM sys.table_types tt
                JOIN sys.columns c ON c.object_id = tt.type_table_object_id
                WHERE tt.name = 'TipoSeguimientoImportacion'
                ORDER BY c.column_id");
            Assert.True(columnas.Rows.Count > 0, "No existe dbo.TipoSeguimientoImportacion");

            var t = new DataTable();
            foreach (DataRow c in columnas.Rows)
            {
                string tipo = (string)c["tipo"];
                Type clr = tipo == "int" ? typeof(int) : tipo.Contains("date") ? typeof(DateTime) : typeof(string);
                t.Columns.Add((string)c["name"], clr);
            }
            return t;
        }

        private static void Fila(DataTable t, int fila, string tracto, string estado, params (string Columna, object Valor)[] valores)
        {
            var r = t.NewRow();
            r["fila"] = fila;
            r["cliente"] = Pedido;
            r["tracto1"] = tracto;
            r["fhProgramacion"] = ProgDe(tracto);
            r["estado"] = estado;
            foreach (var v in valores) r[v.Columna] = v.Valor;
            t.Rows.Add(r);
        }

        private static DataTable Importar(DataTable filas)
        {
            var p = new SqlParameter("@filas", SqlDbType.Structured) { TypeName = "dbo.TipoSeguimientoImportacion", Value = filas };
            return DbHelper.ConsultarTablaSp("sp_SE_ImportarLote", p, DbHelper.Param("@idUsuario", null));
        }

        private static int Viaje(string tracto, string origen, string estado, string conductor,
                                 string pedido = Pedido, DateTime? programacion = null) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(@"
                INSERT INTO SeguimientoExportacion (cliente, tracto1, fhProgramacion, conductorOrigen, estado, origen, activo)
                OUTPUT INSERTED.idSeguimiento
                VALUES (@cliente, @tracto, @prog, @conductor, @estado, @origen, 1)",
                DbHelper.Param("@cliente", pedido), DbHelper.Param("@tracto", tracto), DbHelper.Param("@prog", programacion ?? ProgDe(tracto)),
                DbHelper.Param("@conductor", conductor), DbHelper.Param("@estado", estado), DbHelper.Param("@origen", origen)));

        /// <summary>Foto de lo que la importación no debe cambiar: la fila del viaje y sus hitos.</summary>
        private static string Foto(int id)
        {
            var se = DbHelper.ConsultarTabla(@"
                SELECT estado, origen, conductorOrigen, fhSalidaBase2, fhIngresoPlanta, fhTerminoDescarga, motivoRetraso,
                       fechaModificacion, idUsuarioModificacion
                FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", id)).Rows[0];
            var hitos = DbHelper.ConsultarTabla(@"
                SELECT columna, fuente, horaGps, fuenteGps, regla FROM SeguimientoHito WHERE idSeguimiento = @id ORDER BY columna",
                DbHelper.Param("@id", id));
            return string.Join("|", se.ItemArray.Select(v => Convert.ToString(v))) + " # " +
                   string.Join(";", hitos.Rows.Cast<DataRow>().Select(h => string.Join(",", h.ItemArray.Select(v => Convert.ToString(v)))));
        }

        private static int Cuantos(string tracto) =>
            Convert.ToInt32(DbHelper.EjecutarEscalar(
                "SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1 AND cliente = @c AND tracto1 = @t AND fhProgramacion = @p",
                DbHelper.Param("@c", Pedido), DbHelper.Param("@t", tracto), DbHelper.Param("@p", ProgDe(tracto))));

        [FactBD]
        public void Importar_ActualizaLoImportado_CreaLoNuevo_YNoTocaViajesDeDespachoNiManuales()
        {
            using (new TransaccionDePrueba())
            {
                // Existentes: uno importado antes, uno de despachos (FINALIZADO, con hito GPS) y uno creado a mano.
                int importado = Viaje("zzi-102", null, "EN_CURSO", "Conductor Excel");
                int deDespacho = Viaje("zzi-103", "DESPACHO", "FINALIZADO", "Conductor Despacho");
                int manual = Viaje("zzi-104", "MANUAL", "EN_CURSO", "Conductor Manual");
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalarSp("sp_SE_RegistrarHitoGps",
                    DbHelper.Param("@idSeguimiento", deDespacho), DbHelper.Param("@columna", "fhSalidaBase2"),
                    DbHelper.Param("@horaGps", new DateTime(2026, 10, 6, 12, 3, 0)), DbHelper.Param("@fuenteGps", "GPS"),
                    DbHelper.Param("@latitud", -8.135571m), DbHelper.Param("@longitud", -79.0132m),
                    DbHelper.Param("@regla", "Prueba importación"), DbHelper.Param("@reemplazarGps", false))));
                // Llave repetida: un viaje importado y uno de despachos con la misma llave (dato viejo).
                int importadoGemelo = Viaje("zzi-105", null, "EN_CURSO", "Gemelo Excel");
                int despachoGemelo = Viaje("zzi-105", "DESPACHO", "FINALIZADO", "Gemelo Despacho");

                // Mismo tracto y mismo día que un viaje de despachos, pero otro pedido y otra hora (el
                // despacho guarda la programación a las 00:00): es el mismo viaje, no se duplica.
                int despachoOtroPedido = Viaje("zzi-106", "DESPACHO", "FINALIZADO", "Conductor Dia",
                    pedido: "9900000778", programacion: Prog.Date);

                // Mismo pedido y misma hora que un viaje de despachos, pero la placa mal tipeada en el
                // Excel (F60-779 por F6O-779): es el mismo viaje, no se duplica.
                int despachoPlaca = Viaje("zzi-107", "DESPACHO", "FINALIZADO", "Conductor Placa");

                // Pedido de despachos a las 00:00 (así guarda despachos la programación de TODOS sus
                // camiones): una fila del Excel del mismo pedido a las 00:00 con otro tracto es otro camión.
                var medianoche = Prog.Date.AddDays(10);
                int despachoMedianoche = Viaje("zzi-108", "DESPACHO", "EN_CURSO", "Conductor Medianoche",
                    pedido: "9900000779", programacion: medianoche);

                string fotoPlaca = Foto(despachoPlaca), fotoMedianoche = Foto(despachoMedianoche);
                string fotoDespacho = Foto(deDespacho), fotoManual = Foto(manual), fotoGemelo = Foto(despachoGemelo),
                       fotoOtroPedido = Foto(despachoOtroPedido);

                var lote = TablaDelTipo();
                var otraHora = new DateTime(2026, 10, 6, 20, 0, 0);
                Fila(lote, 3, "zzi-101", "PENDIENTE", ("conductorOrigen", "Primera version"));
                Fila(lote, 4, "zzi-101", "EN_CURSO", ("conductorOrigen", "Nuevo"), ("fhIngresoPlanta", otraHora));   // gana la última
                Fila(lote, 5, "zzi-102", "COMPLETADO", ("fhIngresoPlanta", otraHora), ("fhTerminoDescarga", otraHora));
                Fila(lote, 6, "zzi-103", "EN_CURSO", ("conductorOrigen", "Pisado"), ("fhSalidaBase2", otraHora),
                    ("fhIngresoPlanta", otraHora), ("motivoRetraso", "no debe llegar"));
                Fila(lote, 7, "zzi-104", "COMPLETADO", ("conductorOrigen", "Pisado"), ("fhTerminoDescarga", otraHora));
                Fila(lote, 8, "zzi-105", "COMPLETADO", ("fhIngresoPlanta", otraHora));
                Fila(lote, 9, "zzi-106", "COMPLETADO", ("conductorOrigen", "Pisado"), ("fhTerminoDescarga", otraHora));
                var placaMal = lote.NewRow();   // otra placa, mismo pedido y misma hora que zzi-107
                placaMal["fila"] = 11; placaMal["cliente"] = Pedido; placaMal["tracto1"] = "zzi-1O7";
                placaMal["fhProgramacion"] = ProgDe("zzi-107"); placaMal["estado"] = "COMPLETADO";
                placaMal["conductorOrigen"] = "Pisado";
                lote.Rows.Add(placaMal);
                // Mismo tracto, OTRO día: es otro viaje y se crea.
                var lote10 = lote.NewRow();
                lote10["fila"] = 10; lote10["cliente"] = Pedido; lote10["tracto1"] = "zzi-106";
                lote10["fhProgramacion"] = Prog.AddDays(3); lote10["estado"] = "PENDIENTE";
                lote.Rows.Add(lote10);
                var otroCamion = lote.NewRow();
                otroCamion["fila"] = 12; otroCamion["cliente"] = "9900000779"; otroCamion["tracto1"] = "zzi-109";
                otroCamion["fhProgramacion"] = medianoche; otroCamion["estado"] = "PENDIENTE";
                lote.Rows.Add(otroCamion);

                var res = Importar(lote);

                Assert.Single(res.Rows);
                // Columnas por nombre; "omitidos" va al final (el C# publicado lee por nombre).
                Assert.Equal(new[] { "insertados", "actualizados", "duplicadosEnArchivo", "omitidos" },
                    res.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
                var r = res.Rows[0];
                Assert.Equal(3, Convert.ToInt32(r["insertados"]));     // zzi-101, zzi-106 de otro día y zzi-109 a las 00:00
                Assert.Equal(2, Convert.ToInt32(r["actualizados"]));   // zzi-102 y el importado de zzi-105
                Assert.Equal(1, Convert.ToInt32(r["duplicadosEnArchivo"]));
                Assert.Equal(4, Convert.ToInt32(r["omitidos"]));       // zzi-103 (despacho), zzi-104 (manual), zzi-106 (mismo día), zzi-1O7 (placa)

                // Nuevo: se insertó una sola vez, con la última fila del archivo y sin origen.
                Assert.Equal(1, Cuantos("zzi-101"));
                var nuevo = DbHelper.ConsultarTabla(
                    "SELECT conductorOrigen, estado, origen, fhIngresoPlanta FROM SeguimientoExportacion WHERE activo = 1 AND cliente = @c AND tracto1 = 'zzi-101'",
                    DbHelper.Param("@c", Pedido)).Rows[0];
                Assert.Equal("Nuevo", nuevo["conductorOrigen"]);
                Assert.Equal("EN_CURSO", nuevo["estado"]);
                Assert.Equal(DBNull.Value, nuevo["origen"]);
                Assert.Equal(otraHora, nuevo["fhIngresoPlanta"]);

                // Importado antes: se actualiza (estado y hitos; lo vacío no borra lo que había).
                var act = DbHelper.ConsultarTabla(
                    "SELECT estado, conductorOrigen, fhIngresoPlanta FROM SeguimientoExportacion WHERE idSeguimiento = @id",
                    DbHelper.Param("@id", importado)).Rows[0];
                Assert.Equal("COMPLETADO", act["estado"]);
                Assert.Equal("Conductor Excel", act["conductorOrigen"]);
                Assert.Equal(otraHora, act["fhIngresoPlanta"]);
                Assert.Equal(1, Cuantos("zzi-102"));

                // De despachos y manual: intactos (estado, conductor, hitos GPS) y sin duplicado.
                Assert.Equal(fotoDespacho, Foto(deDespacho));
                Assert.Contains("FINALIZADO", fotoDespacho);
                Assert.Contains("Prueba importación", fotoDespacho);   // el hito GPS sigue ahí
                Assert.Equal(1, Cuantos("zzi-103"));
                Assert.Equal(fotoManual, Foto(manual));
                Assert.Equal(1, Cuantos("zzi-104"));

                // Llave con importado y de despachos: se actualiza el importado; el de despachos no cambia.
                Assert.Equal("COMPLETADO", DbHelper.EjecutarEscalar(
                    "SELECT estado FROM SeguimientoExportacion WHERE idSeguimiento = @id", DbHelper.Param("@id", importadoGemelo)));
                Assert.Equal(fotoGemelo, Foto(despachoGemelo));
                Assert.Equal(2, Cuantos("zzi-105"));

                // Mismo tracto y día que el de despachos (otro pedido, otra hora): omitido, intacto y sin duplicado.
                Assert.Equal(fotoOtroPedido, Foto(despachoOtroPedido));
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1 AND tracto1 = 'zzi-106' AND CAST(fhProgramacion AS date) = @d",
                    DbHelper.Param("@d", Prog.Date))));
                // Placa mal tipeada (mismo pedido y hora que zzi-107): omitida, intacto y sin duplicado.
                Assert.Equal(fotoPlaca, Foto(despachoPlaca));
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1 AND cliente = @c AND fhProgramacion = @p",
                    DbHelper.Param("@c", Pedido), DbHelper.Param("@p", ProgDe("zzi-107")))));

                // El de otro día sí se creó (importado, sin origen).
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1 AND tracto1 = 'zzi-106' AND fhProgramacion = @p AND origen IS NULL",
                    DbHelper.Param("@p", Prog.AddDays(3)))));

                // Mismo pedido a las 00:00 con otro tracto: se crea; el de despachos no cambia.
                Assert.Equal(1, Convert.ToInt32(DbHelper.EjecutarEscalar(
                    "SELECT COUNT(*) FROM SeguimientoExportacion WHERE activo = 1 AND tracto1 = 'zzi-109' AND fhProgramacion = @p AND origen IS NULL",
                    DbHelper.Param("@p", medianoche))));
                Assert.Equal(fotoMedianoche, Foto(despachoMedianoche));
            }
        }
    }
}
