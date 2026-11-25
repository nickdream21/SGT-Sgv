using System;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Exportacion
{
    /// <summary>Lo que devuelve sp_SE_ImportarLote (su primera fila).</summary>
    public sealed class ResultadoImportacionLote
    {
        public int Insertados { get; set; }
        public int Actualizados { get; set; }
        /// <summary>Filas que repetían pedido + tracto + programación en el archivo (se tomó la última).</summary>
        public int DuplicadosEnArchivo { get; set; }
        /// <summary>Viajes de despachos o creados a mano: no se tocan.</summary>
        public int Omitidos { get; set; }
    }

    /// <summary>
    /// La escritura de la importación del seguimiento desde Excel (E6-20: antes en ImportarSeguimiento.aspx.cs): la
    /// tabla con la forma de dbo.TipoSeguimientoImportacion y el envío de todas las filas en una llamada a
    /// sp_SE_ImportarLote. La lectura del Excel sigue en la página.
    /// </summary>
    public static class ImportacionSeguimientoService
    {
        /// <summary>Segundos de espera de sp_SE_ImportarLote (un STATUS GENERAL completo son miles de filas).</summary>
        public const int SegundosEspera = 300;

        /// <summary>Estructura idéntica a dbo.TipoSeguimientoImportacion (mismo orden de columnas).</summary>
        public static DataTable CrearTablaImportacion()
        {
            var t = new DataTable();
            t.Columns.Add("fila", typeof(int));
            foreach (var c in new[] { "cliente", "conductorOrigen", "tracto1", "carreta", "conductorDestino", "tracto2" })
                t.Columns.Add(c, typeof(string));
            foreach (var c in new[] { "fhSalidaBase1", "fhLlegadaTrujillo", "fhRegistro", "fhProgramacion",
                                      "fhIngresoPlanta", "fhInicioCarga", "fhTerminoCarga", "fhSalidaPlanta",
                                      "fhLlegadaBase2", "fhSalidaBase2",
                                      "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("bodegaNacional", typeof(string));
            foreach (var c in new[] { "fhLlegadaCEBAF", "fhCruceEcuador", "fhAutorizacionNacionalizacion" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("bodegaEcuatoriana", typeof(string));
            foreach (var c in new[] { "fhLlegadaTCI", "fhSalidaTCI" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("bodegaDescarga", typeof(string));
            foreach (var c in new[] { "fhLlegadaPlantaEcuador", "fhLlegadaAlmacen", "fhIngreso",
                                      "fhInicioDescarga", "fhTerminoDescarga", "fhSalida", "fhLlegadaBaseFinal" })
                t.Columns.Add(c, typeof(DateTime));
            t.Columns.Add("motivoRetraso", typeof(string));
            t.Columns.Add("estado", typeof(string));
            return t;
        }

        /// <summary>
        /// Envía <paramref name="filas"/> (de <see cref="CrearTablaImportacion"/>) en una llamada a sp_SE_ImportarLote.
        /// Sin filas no llama a la BD y devuelve todo en 0. El SP no audita: la auditoría la registra quien llama.
        /// Los errores de BD se propagan.
        /// </summary>
        public static ResultadoImportacionLote ImportarLote(DataTable filas, int? idUsuario)
        {
            var resultado = new ResultadoImportacionLote();
            if (filas == null || filas.Rows.Count == 0) return resultado;

            using (var conn = new SqlConnection(DbHelper.ConnectionString))
            using (var cmd = new SqlCommand("sp_SE_ImportarLote", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandTimeout = SegundosEspera;
                var p = cmd.Parameters.Add("@filas", SqlDbType.Structured);
                p.TypeName = "dbo.TipoSeguimientoImportacion";
                p.Value = filas;
                cmd.Parameters.Add("@idUsuario", SqlDbType.Int).Value = (object)idUsuario ?? DBNull.Value;

                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    if (rd.Read())
                    {
                        resultado.Insertados = Convert.ToInt32(rd["insertados"]);
                        resultado.Actualizados = Convert.ToInt32(rd["actualizados"]);
                        resultado.DuplicadosEnArchivo = Convert.ToInt32(rd["duplicadosEnArchivo"]);
                        resultado.Omitidos = Convert.ToInt32(rd["omitidos"]);
                    }
                }
            }
            return resultado;
        }
    }
}
