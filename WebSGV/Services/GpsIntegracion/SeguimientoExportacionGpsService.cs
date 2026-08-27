using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;

namespace WebSGV.Services.GpsIntegracion
{
    public class ResultadoCampoGps
    {
        public string Columna { get; set; }
        public bool Encontrado { get; set; }
    }

    public class ResultadoConsultaGpsExportacion
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public List<ResultadoCampoGps> Campos { get; set; } = new List<ResultadoCampoGps>();

        public static ResultadoConsultaGpsExportacion Fallo(string mensaje) =>
            new ResultadoConsultaGpsExportacion { Exito = false, Mensaje = mensaje };
    }

    /// <summary>
    /// Orquesta la consulta GPS de todo el recorrido de un viaje de exportación (Perú → Ecuador
    /// → Perú), reemplazando el llenado manual del Excel "STATUS GENERAL VIVIANA". Recorre día
    /// por día el historial del Tracto correspondiente a cada tramo (Tracto 1 = nacional
    /// Base-Trujillo-Base, Tracto 2 = internacional Base-Ecuador-Base), cacheando el historial
    /// por (dispositivo, día) para no repetir llamadas al API, y va emparejando cada punto de
    /// control en el orden en que realmente ocurren. Los campos que no son un punto geográfico
    /// (F.H. Registro, F.H. Programación, autorización de nacionalización, inicio/término de
    /// carga y descarga, incidencias) no se tocan — siguen siendo manuales.
    /// </summary>
    public static class SeguimientoExportacionGpsService
    {
        private const int VentanaMaximaDias = 12;

        private class Paso
        {
            public string Checkpoint;
            public int NumeroTracto;
            public string[] Columnas; // 1 columna normal; 2+ para el caso Planta/Almacén Ecuador
        }

        public static ResultadoConsultaGpsExportacion ConsultarYActualizar(int idSeguimiento)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT se.tracto1, se.tracto2, se.fhSalidaBase1, se.fhProgramacion, se.fechaRegistro,
                       d.fechaDespacho AS fechaDespachoOrigen
                FROM SeguimientoExportacion se
                LEFT JOIN Despachos d ON d.idDespacho = se.idDespachoOrigen
                WHERE se.idSeguimiento = @id AND se.activo = 1",
                DbHelper.Param("@id", idSeguimiento));

            if (dt.Rows.Count == 0)
                return ResultadoConsultaGpsExportacion.Fallo("No se encontró el registro de seguimiento.");

            DataRow row = dt.Rows[0];
            string placaTracto1 = row["tracto1"] == DBNull.Value ? null : row["tracto1"].ToString().Trim();
            string placaTracto2 = row["tracto2"] == DBNull.Value ? null : row["tracto2"].ToString().Trim();

            if (string.IsNullOrEmpty(placaTracto1) && string.IsNullOrEmpty(placaTracto2))
                return ResultadoConsultaGpsExportacion.Fallo("El registro no tiene Tracto 1 ni Tracto 2 asignados.");

            // Ancla de búsqueda: prioriza la hora de salida ya confirmada por GPS; si no,
            // la fecha REAL de despacho (Despachos.fechaDespacho, vinculada por idDespachoOrigen)
            // — no la fecha en que se registró el viaje en el sistema, que puede quedar varios
            // días después de que el camión ya salió. Colchón de 2 días hacia atrás como
            // resguardo ante un posible error de tipeo en esa fecha.
            DateTime fechaInicio;
            if (row["fhSalidaBase1"] != DBNull.Value)
            {
                fechaInicio = Convert.ToDateTime(row["fhSalidaBase1"]).Date;
            }
            else
            {
                DateTime fechaAncla =
                    row["fechaDespachoOrigen"] != DBNull.Value ? Convert.ToDateTime(row["fechaDespachoOrigen"]).Date :
                    row["fhProgramacion"] != DBNull.Value ? Convert.ToDateTime(row["fhProgramacion"]).Date :
                    Convert.ToDateTime(row["fechaRegistro"]).Date;
                fechaInicio = fechaAncla.AddDays(-2);
            }

            try
            {
                var cliente = new OnwayApiClient();
                string token = cliente.ObtenerTokenValido();
                string userId = cliente.ObtenerUserId(token);

                var dispositivo1 = string.IsNullOrEmpty(placaTracto1) ? null : cliente.BuscarDispositivoPorPlaca(token, userId, placaTracto1);
                var dispositivo2 = string.IsNullOrEmpty(placaTracto2) ? null : cliente.BuscarDispositivoPorPlaca(token, userId, placaTracto2);

                var cacheHistorial = new Dictionary<string, List<OnwayHistoryPoint>>();
                Func<int, DateTime, List<OnwayHistoryPoint>> obtenerHistorialDia = (numeroTracto, dia) =>
                {
                    var dispositivo = numeroTracto == 1 ? dispositivo1 : dispositivo2;
                    if (dispositivo == null) return new List<OnwayHistoryPoint>();

                    string clave = dispositivo.Id + "|" + dia.ToString("yyyy-MM-dd");
                    if (cacheHistorial.TryGetValue(clave, out var cacheado))
                        return cacheado;

                    DateTime desdeUtc = dia.AddHours(5);
                    DateTime hastaUtc = dia.AddDays(1).AddHours(5).AddSeconds(-1);
                    var historial = cliente.ObtenerHistorial(token, userId, dispositivo.Id, desdeUtc, hastaUtc);
                    cacheHistorial[clave] = historial;
                    return historial;
                };

                // Busca un checkpoint a partir de `desde`, avanzando día por día hasta
                // VentanaMaximaDias. Devuelve el punto encontrado y el día donde se encontró
                // (para que el siguiente paso no busque hacia atrás).
                Func<string, int, DateTime, Tuple<OnwayHistoryPoint, DateTime>> buscar = (nombreCheckpoint, numeroTracto, desde) =>
                {
                    var checkpoint = PuntoControlGpsService.ObtenerPorNombre(nombreCheckpoint);
                    if (checkpoint == null) return null;

                    for (int i = 0; i < VentanaMaximaDias; i++)
                    {
                        DateTime dia = desde.AddDays(i);
                        var historialDia = obtenerHistorialDia(numeroTracto, dia);
                        var match = CheckpointMatchingService.DetectarLlegada(
                            historialDia, checkpoint.Latitud, checkpoint.Longitud, checkpoint.RadioMetros);
                        if (match != null)
                            return Tuple.Create(match, dia);
                    }
                    return null;
                };

                var pasos = new List<Paso>
                {
                    new Paso { Checkpoint = "BASE_SALIDA_1",           NumeroTracto = 1, Columnas = new[] { "fhSalidaBase1" } },
                    new Paso { Checkpoint = "TRUJILLO_LLEGADA",        NumeroTracto = 1, Columnas = new[] { "fhLlegadaTrujillo" } },
                    new Paso { Checkpoint = "TRUJILLO_INGRESO",        NumeroTracto = 1, Columnas = new[] { "fhIngresoPlanta" } },
                    new Paso { Checkpoint = "TRUJILLO_SALIDA",         NumeroTracto = 1, Columnas = new[] { "fhSalidaPlanta" } },
                    new Paso { Checkpoint = "BASE_LLEGADA_2",          NumeroTracto = 1, Columnas = new[] { "fhLlegadaBase2" } },
                    new Paso { Checkpoint = "BASE_SALIDA_2",           NumeroTracto = 2, Columnas = new[] { "fhSalidaBase2" } },
                    new Paso { Checkpoint = "BODEGA_NACIONAL_LLEGADA", NumeroTracto = 2, Columnas = new[] { "fhLlegadaBodegaNacional" } },
                    new Paso { Checkpoint = "BODEGA_NACIONAL_INGRESO", NumeroTracto = 2, Columnas = new[] { "fhIngresoBodegaNacional" } },
                    new Paso { Checkpoint = "BODEGA_NACIONAL_SALIDA",  NumeroTracto = 2, Columnas = new[] { "fhSalidaBodegaNacional" } },
                    new Paso { Checkpoint = "CEBAF_LLEGADA",           NumeroTracto = 2, Columnas = new[] { "fhLlegadaCEBAF" } },
                    new Paso { Checkpoint = "CRUCE_ECUADOR",           NumeroTracto = 2, Columnas = new[] { "fhCruceEcuador" } },
                    new Paso { Checkpoint = "TCI_LLEGADA",             NumeroTracto = 2, Columnas = new[] { "fhLlegadaTCI" } },
                    new Paso { Checkpoint = "TCI_SALIDA",              NumeroTracto = 2, Columnas = new[] { "fhSalidaTCI" } },
                };

                var valoresEncontrados = new Dictionary<string, DateTime>();
                DateTime cursor = fechaInicio;

                foreach (var paso in pasos)
                {
                    var resultado = buscar(paso.Checkpoint, paso.NumeroTracto, cursor);
                    if (resultado == null) continue;

                    DateTime horaLocal = FechaHelper.ConvertirDeUtc(resultado.Item1.MessageTime);
                    foreach (var columna in paso.Columnas)
                        valoresEncontrados[columna] = horaLocal;
                    cursor = resultado.Item2; // no retroceder en los pasos siguientes
                }

                // Bifurcación Jave / Inbalnor: se prueba primero Jave, y si no matchea, Inbalnor.
                string rama = null;
                var resultadoJave = buscar("PLANTA_ECUADOR_JAVE", 2, cursor);
                if (resultadoJave != null)
                {
                    rama = "JAVE";
                    DateTime horaLocal = FechaHelper.ConvertirDeUtc(resultadoJave.Item1.MessageTime);
                    valoresEncontrados["fhLlegadaPlantaEcuador"] = horaLocal;
                    valoresEncontrados["fhLlegadaAlmacen"] = horaLocal;
                    cursor = resultadoJave.Item2;
                }
                else
                {
                    var resultadoInbalnor = buscar("PLANTA_ECUADOR_INBALNOR", 2, cursor);
                    if (resultadoInbalnor != null)
                    {
                        rama = "INBALNOR";
                        DateTime horaLocal = FechaHelper.ConvertirDeUtc(resultadoInbalnor.Item1.MessageTime);
                        valoresEncontrados["fhLlegadaPlantaEcuador"] = horaLocal;
                        valoresEncontrados["fhLlegadaAlmacen"] = horaLocal;
                        cursor = resultadoInbalnor.Item2;
                    }
                }

                if (rama != null)
                {
                    var resultadoIngreso = buscar("INGRESO_" + rama, 2, cursor);
                    if (resultadoIngreso != null)
                    {
                        valoresEncontrados["fhIngreso"] = FechaHelper.ConvertirDeUtc(resultadoIngreso.Item1.MessageTime);
                        cursor = resultadoIngreso.Item2;
                    }

                    var resultadoSalida = buscar("SALIDA_" + rama, 2, cursor);
                    if (resultadoSalida != null)
                    {
                        valoresEncontrados["fhSalida"] = FechaHelper.ConvertirDeUtc(resultadoSalida.Item1.MessageTime);
                        cursor = resultadoSalida.Item2;
                    }
                }

                var resultadoBaseFinal = buscar("BASE_LLEGADA_FINAL", 2, cursor);
                if (resultadoBaseFinal != null)
                    valoresEncontrados["fhLlegadaBaseFinal"] = FechaHelper.ConvertirDeUtc(resultadoBaseFinal.Item1.MessageTime);

                if (valoresEncontrados.Count == 0)
                    return ResultadoConsultaGpsExportacion.Fallo("El GPS no reportó ningún punto de control coincidente para este viaje.");

                GuardarValores(idSeguimiento, valoresEncontrados, rama);

                AuditoriaHelper.Registrar("CONSULTAR_HORA_GPS", "SeguimientoExportacion", idSeguimiento,
                    $"GPS actualizó {valoresEncontrados.Count} campo(s) de fecha/hora" +
                    (rama != null ? $" (ruta detectada: {rama})" : "") + ".");

                var todasLasColumnas = pasos.SelectMany(p => p.Columnas)
                    .Concat(new[] { "fhLlegadaPlantaEcuador", "fhLlegadaAlmacen", "fhIngreso", "fhSalida", "fhLlegadaBaseFinal" })
                    .Distinct();

                var resultadoFinal = new ResultadoConsultaGpsExportacion
                {
                    Exito = true,
                    Mensaje = $"GPS actualizó {valoresEncontrados.Count} de {todasLasColumnas.Count()} campos" +
                              (rama != null ? $" (ruta: {rama})" : " (no se detectó si fue por Jave o Inbalnor)") + "."
                };
                foreach (var columna in todasLasColumnas)
                    resultadoFinal.Campos.Add(new ResultadoCampoGps { Columna = columna, Encontrado = valoresEncontrados.ContainsKey(columna) });

                return resultadoFinal;
            }
            catch (OnwayApiException ex)
            {
                LogSGV.Error(ex, "Error consultando GPS Onway para seguimiento {IdSeguimiento}", idSeguimiento);
                return ResultadoConsultaGpsExportacion.Fallo("No se pudo conectar con el sistema GPS. Intente más tarde.");
            }
        }

        private static void GuardarValores(int idSeguimiento, Dictionary<string, DateTime> valores, string rama)
        {
            if (valores.Count == 0) return;

            var sets = new List<string>();
            var parametros = new List<System.Data.SqlClient.SqlParameter>();
            int i = 0;
            foreach (var kv in valores)
            {
                string paramName = "@v" + i++;
                sets.Add($"{kv.Key} = {paramName}");
                parametros.Add(DbHelper.Param(paramName, kv.Value));
            }

            if (rama != null)
            {
                sets.Add("bodegaEcuatoriana = @rama");
                sets.Add("bodegaDescarga = @rama");
                parametros.Add(DbHelper.Param("@rama", rama));
            }

            sets.Add("fechaModificacion = GETDATE()");

            string sql = $"UPDATE SeguimientoExportacion SET {string.Join(", ", sets)} WHERE idSeguimiento = @id";
            parametros.Add(DbHelper.Param("@id", idSeguimiento));

            DbHelper.EjecutarNonQuery(sql, parametros.ToArray());
        }
    }
}
