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
        /// <summary>Señal de detección débil: el punto salió del último recurso (primer punto dentro del radio), conviene revisarlo a mano.</summary>
        public const string SenalRevisar = "Primer punto detectado (revisar)";

        /// <summary>El campo ya venía confirmado de antes, no se volvió a consultar el GPS.</summary>
        public const string SenalYaConfirmado = "Ya confirmado";

        public string Columna { get; set; }
        public bool Encontrado { get; set; }
        public string SenalConfianza { get; set; }

        /// <summary>True cuando el valor se obtuvo por el último recurso y merece revisión manual.</summary>
        public bool RequiereRevision => Encontrado && SenalConfianza == SenalRevisar;

        /// <summary>True cuando el campo ya estaba confirmado y esta consulta no lo tocó.</summary>
        public bool YaEstabaConfirmado => SenalConfianza == SenalYaConfirmado;
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

        private enum TipoEventoGps { Llegada, Salida }

        private class Paso
        {
            public string Checkpoint;
            public int NumeroTracto;
            public string[] Columnas; // 1 columna normal; 2+ para el caso Planta/Almacén Ecuador
            public TipoEventoGps TipoEvento = TipoEventoGps.Llegada;
        }

        /// <summary>
        /// Clasifica qué tan confiable fue la detección de un punto, para mostrarla en el
        /// panel de resultados y que la administradora sepa qué campos revisar primero.
        ///
        /// El tipo de evento importa: <see cref="CheckpointMatchingService.DetectarSalida"/>
        /// devuelve SIEMPRE un punto en movimiento (velocidad por encima del umbral), así que
        /// clasificarlo por velocidad como se hace con una llegada marcaría toda salida como
        /// dudosa — exactamente al revés de la realidad, porque una salida solo se devuelve
        /// cuando ya se confirmó el alejamiento sostenido sin regreso (si no, devuelve null en
        /// vez de arriesgar un valor).
        /// </summary>
        private static string ClasificarSenal(OnwayHistoryPoint p, TipoEventoGps tipoEvento)
        {
            if (tipoEvento == TipoEventoGps.Salida)
                return "Alejamiento sostenido confirmado";

            if (p.AlertDescription?.En?.IndexOf("ignition off", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Ignition Off";
            if (p.Speed < 5.0)
                return "Parada sostenida";
            return ResultadoCampoGps.SenalRevisar;
        }

        /// <summary>Las 18 columnas de fecha/hora que puede llenar el GPS (ver <c>Paso</c> y la bifurcación Jave/Inbalnor).</summary>
        private static readonly string[] ColumnasGps =
        {
            "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta", "fhLlegadaBase2",
            "fhSalidaBase2", "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional",
            "fhLlegadaCEBAF", "fhCruceEcuador", "fhLlegadaTCI", "fhSalidaTCI",
            "fhLlegadaPlantaEcuador", "fhLlegadaAlmacen", "fhIngreso", "fhSalida", "fhLlegadaBaseFinal",
        };

        public static ResultadoConsultaGpsExportacion ConsultarYActualizar(int idSeguimiento)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT se.tracto1, se.tracto2, se.fhSalidaBase1, se.fhProgramacion, se.fechaRegistro,
                       se.fhLlegadaTrujillo, se.fhIngresoPlanta, se.fhSalidaPlanta, se.fhLlegadaBase2,
                       se.fhSalidaBase2, se.fhLlegadaBodegaNacional, se.fhIngresoBodegaNacional, se.fhSalidaBodegaNacional,
                       se.fhLlegadaCEBAF, se.fhCruceEcuador, se.fhLlegadaTCI, se.fhSalidaTCI,
                       se.fhLlegadaPlantaEcuador, se.fhLlegadaAlmacen, se.fhIngreso, se.fhSalida, se.fhLlegadaBaseFinal,
                       se.bodegaEcuatoriana,
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

            // Campos que ya se confirmaron en una consulta GPS anterior (o a mano): no se
            // vuelven a buscar — "Verificar GPS" solo trabaja sobre lo que realmente falta, en
            // vez de recorrer día por día todo el recorrido cada vez que se presiona el botón.
            var valoresYaConfirmados = new Dictionary<string, DateTime>();
            foreach (var columna in ColumnasGps)
                if (row[columna] != DBNull.Value)
                    valoresYaConfirmados[columna] = Convert.ToDateTime(row[columna]);
            string ramaExistente = row["bodegaEcuatoriana"] == DBNull.Value ? null : row["bodegaEcuatoriana"].ToString();

            // Ancla de búsqueda: prioriza la hora de salida ya confirmada por GPS; si no, la
            // fecha REAL de despacho (Despachos.fechaDespacho, vinculada por idDespachoOrigen)
            // — no la fecha en que se registró el viaje en el sistema, que puede quedar varios
            // días después de que el camión ya salió.
            //
            // A propósito NO se le da ningún colchón hacia atrás (antes se restaban 2 días "por
            // si había un error de tipeo"). Confirmado con un caso real (2026-08-31): un tracto
            // hace movimientos normales de rutina en los días previos al despacho (grifo, taller,
            // pesaje, etc.), y DetectarSalida —que solo evalúa si el vehículo vuelve dentro de
            // los próximos minutos, no si el movimiento tiene que ver con ESTE despacho— toma
            // gustoso cualquiera de esos movimientos de rutina como si fuera la salida real,
            // deteniendo la búsqueda ahí sin llegar nunca al día correcto. La fecha de Despacho
            // es la fuente más confiable que hay (registro formal, no texto libre), así que se
            // busca únicamente hacia ADELANTE desde ese día exacto.
            DateTime fechaInicio;
            if (row["fhSalidaBase1"] != DBNull.Value)
            {
                fechaInicio = Convert.ToDateTime(row["fhSalidaBase1"]).Date;
            }
            else
            {
                fechaInicio =
                    row["fechaDespachoOrigen"] != DBNull.Value ? Convert.ToDateTime(row["fechaDespachoOrigen"]).Date :
                    row["fhProgramacion"] != DBNull.Value ? Convert.ToDateTime(row["fhProgramacion"]).Date :
                    Convert.ToDateTime(row["fechaRegistro"]).Date;
            }

            try
            {
                var cliente = new OnwayApiClient();

                var dispositivo1 = string.IsNullOrEmpty(placaTracto1) ? null : cliente.BuscarDispositivoPorPlaca(placaTracto1);
                var dispositivo2 = string.IsNullOrEmpty(placaTracto2) ? null : cliente.BuscarDispositivoPorPlaca(placaTracto2);

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
                    var historial = cliente.ObtenerHistorial(dispositivo.Id, desdeUtc, hastaUtc);
                    cacheHistorial[clave] = historial;
                    return historial;
                };

                // Busca un checkpoint a partir de `desde`, avanzando día por día hasta
                // VentanaMaximaDias. Devuelve el punto encontrado y el día (local de Perú) al que
                // pertenece, para que el siguiente paso no busque hacia atrás.
                //
                // `cotaInferior` (UTC) descarta todo punto anterior o igual al match previo. Se
                // aplica a TODOS los días de la ventana, no solo al primero: `dia` es una fecha
                // local de Perú y `cotaInferior` es UTC, así que compararlas por fecha ("¿es este
                // el día del match anterior?") mezclaba zonas horarias y dejaba el filtro sin
                // aplicar justo en el día correcto cuando el match previo caía entre las 19:00 y
                // las 23:59 hora Perú (00:00-04:59 UTC del día siguiente). Filtrar por timestamp
                // en todos los días es UTC contra UTC — siempre correcto, y en los días
                // posteriores no descarta nada porque ya son todos más recientes.
                Func<string, int, DateTime, TipoEventoGps, DateTime?, Tuple<OnwayHistoryPoint, DateTime>> buscar =
                    (nombreCheckpoint, numeroTracto, desde, tipoEvento, cotaInferior) =>
                {
                    var checkpoint = PuntoControlGpsService.ObtenerPorNombre(nombreCheckpoint);
                    if (checkpoint == null) return null;

                    Func<DateTime, List<OnwayHistoryPoint>> historialFiltrado = dia =>
                    {
                        var puntos = obtenerHistorialDia(numeroTracto, dia);
                        return cotaInferior.HasValue
                            ? puntos.Where(p => p.MessageTime > cotaInferior.Value).ToList()
                            : puntos;
                    };

                    for (int i = 0; i < VentanaMaximaDias; i++)
                    {
                        DateTime dia = desde.AddDays(i);
                        var historialDia = historialFiltrado(dia);

                        OnwayHistoryPoint match;
                        if (tipoEvento == TipoEventoGps.Salida)
                        {
                            // Una salida necesita confirmar el alejamiento hasta el final de la
                            // ventana: si ocurre cerca de medianoche, un solo día no alcanza.
                            var historialVentana = historialDia
                                .Concat(historialFiltrado(dia.AddDays(1)))
                                .ToList();
                            match = CheckpointMatchingService.DetectarSalida(
                                historialVentana, checkpoint.Latitud, checkpoint.Longitud, checkpoint.RadioMetros);
                        }
                        else
                        {
                            match = CheckpointMatchingService.DetectarLlegada(
                                historialDia, checkpoint.Latitud, checkpoint.Longitud, checkpoint.RadioMetros);
                        }

                        // El día se deriva del propio match, no del día que se estaba barriendo:
                        // en una Salida el punto puede caer en el día de lookahead (dia + 1), y
                        // devolver `dia` dejaría el cursor un día atrás del match real.
                        if (match != null)
                            return Tuple.Create(match, FechaHelper.ConvertirDeUtc(match.MessageTime).Date);
                    }
                    return null;
                };

                var pasos = new List<Paso>
                {
                    new Paso { Checkpoint = "BASE_SALIDA_1",           NumeroTracto = 1, Columnas = new[] { "fhSalidaBase1" },          TipoEvento = TipoEventoGps.Salida },
                    new Paso { Checkpoint = "TRUJILLO_LLEGADA",        NumeroTracto = 1, Columnas = new[] { "fhLlegadaTrujillo" } },
                    new Paso { Checkpoint = "TRUJILLO_INGRESO",        NumeroTracto = 1, Columnas = new[] { "fhIngresoPlanta" } },
                    new Paso { Checkpoint = "TRUJILLO_SALIDA",         NumeroTracto = 1, Columnas = new[] { "fhSalidaPlanta" },          TipoEvento = TipoEventoGps.Salida },
                    new Paso { Checkpoint = "BASE_LLEGADA_2",          NumeroTracto = 1, Columnas = new[] { "fhLlegadaBase2" } },
                    new Paso { Checkpoint = "BASE_SALIDA_2",           NumeroTracto = 2, Columnas = new[] { "fhSalidaBase2" },           TipoEvento = TipoEventoGps.Salida },
                    new Paso { Checkpoint = "BODEGA_NACIONAL_LLEGADA", NumeroTracto = 2, Columnas = new[] { "fhLlegadaBodegaNacional" } },
                    new Paso { Checkpoint = "BODEGA_NACIONAL_INGRESO", NumeroTracto = 2, Columnas = new[] { "fhIngresoBodegaNacional" } },
                    new Paso { Checkpoint = "BODEGA_NACIONAL_SALIDA",  NumeroTracto = 2, Columnas = new[] { "fhSalidaBodegaNacional" },  TipoEvento = TipoEventoGps.Salida },
                    new Paso { Checkpoint = "CEBAF_LLEGADA",           NumeroTracto = 2, Columnas = new[] { "fhLlegadaCEBAF" } },
                    new Paso { Checkpoint = "CRUCE_ECUADOR",           NumeroTracto = 2, Columnas = new[] { "fhCruceEcuador" } },
                    new Paso { Checkpoint = "TCI_LLEGADA",             NumeroTracto = 2, Columnas = new[] { "fhLlegadaTCI" } },
                    new Paso { Checkpoint = "TCI_SALIDA",              NumeroTracto = 2, Columnas = new[] { "fhSalidaTCI" },             TipoEvento = TipoEventoGps.Salida },
                };

                var valoresEncontrados = new Dictionary<string, DateTime>();
                var senalPorColumna = new Dictionary<string, string>();
                DateTime cursor = fechaInicio;
                DateTime? horaCotaInferior = null;

                // Si `columna` ya está confirmada (de una consulta anterior), adelanta el cursor
                // a partir de ese valor sin llamar al API — así el siguiente checkpoint tampoco
                // busca hacia atrás, igual que si se acabara de encontrar recién.
                bool UsarValorConfirmadoSiExiste(string columna)
                {
                    if (!valoresYaConfirmados.TryGetValue(columna, out var horaLocalConfirmada)) return false;
                    cursor = horaLocalConfirmada.Date;
                    horaCotaInferior = FechaHelper.ConvertirAUtc(horaLocalConfirmada);
                    return true;
                }

                foreach (var paso in pasos)
                {
                    if (UsarValorConfirmadoSiExiste(paso.Columnas[0]))
                        continue; // ya confirmado; no se vuelve a consultar el GPS

                    var resultado = buscar(paso.Checkpoint, paso.NumeroTracto, cursor, paso.TipoEvento, horaCotaInferior);
                    if (resultado == null) continue;

                    DateTime horaLocal = FechaHelper.ConvertirDeUtc(resultado.Item1.MessageTime);
                    string senal = ClasificarSenal(resultado.Item1, paso.TipoEvento);
                    foreach (var columna in paso.Columnas)
                    {
                        valoresEncontrados[columna] = horaLocal;
                        senalPorColumna[columna] = senal;
                    }
                    cursor = resultado.Item2; // no retroceder en los pasos siguientes
                    horaCotaInferior = resultado.Item1.MessageTime;
                }

                // Bifurcación Jave / Inbalnor: se prueba primero Jave, y si no matchea, Inbalnor.
                string rama = ramaExistente;
                if (UsarValorConfirmadoSiExiste("fhLlegadaPlantaEcuador"))
                {
                    // ya confirmado (rama ya conocida por ramaExistente); no se vuelve a buscar.
                }
                else
                {
                    var resultadoJave = buscar("PLANTA_ECUADOR_JAVE", 2, cursor, TipoEventoGps.Llegada, horaCotaInferior);
                    if (resultadoJave != null)
                    {
                        rama = "JAVE";
                        DateTime horaLocal = FechaHelper.ConvertirDeUtc(resultadoJave.Item1.MessageTime);
                        string senal = ClasificarSenal(resultadoJave.Item1, TipoEventoGps.Llegada);
                        valoresEncontrados["fhLlegadaPlantaEcuador"] = horaLocal;
                        valoresEncontrados["fhLlegadaAlmacen"] = horaLocal;
                        senalPorColumna["fhLlegadaPlantaEcuador"] = senal;
                        senalPorColumna["fhLlegadaAlmacen"] = senal;
                        cursor = resultadoJave.Item2;
                        horaCotaInferior = resultadoJave.Item1.MessageTime;
                    }
                    else
                    {
                        var resultadoInbalnor = buscar("PLANTA_ECUADOR_INBALNOR", 2, cursor, TipoEventoGps.Llegada, horaCotaInferior);
                        if (resultadoInbalnor != null)
                        {
                            rama = "INBALNOR";
                            DateTime horaLocal = FechaHelper.ConvertirDeUtc(resultadoInbalnor.Item1.MessageTime);
                            string senal = ClasificarSenal(resultadoInbalnor.Item1, TipoEventoGps.Llegada);
                            valoresEncontrados["fhLlegadaPlantaEcuador"] = horaLocal;
                            valoresEncontrados["fhLlegadaAlmacen"] = horaLocal;
                            senalPorColumna["fhLlegadaPlantaEcuador"] = senal;
                            senalPorColumna["fhLlegadaAlmacen"] = senal;
                            cursor = resultadoInbalnor.Item2;
                            horaCotaInferior = resultadoInbalnor.Item1.MessageTime;
                        }
                    }
                }

                if (rama != null)
                {
                    if (!UsarValorConfirmadoSiExiste("fhIngreso"))
                    {
                        var resultadoIngreso = buscar("INGRESO_" + rama, 2, cursor, TipoEventoGps.Llegada, horaCotaInferior);
                        if (resultadoIngreso != null)
                        {
                            valoresEncontrados["fhIngreso"] = FechaHelper.ConvertirDeUtc(resultadoIngreso.Item1.MessageTime);
                            senalPorColumna["fhIngreso"] = ClasificarSenal(resultadoIngreso.Item1, TipoEventoGps.Llegada);
                            cursor = resultadoIngreso.Item2;
                            horaCotaInferior = resultadoIngreso.Item1.MessageTime;
                        }
                    }

                    if (!UsarValorConfirmadoSiExiste("fhSalida"))
                    {
                        var resultadoSalida = buscar("SALIDA_" + rama, 2, cursor, TipoEventoGps.Salida, horaCotaInferior);
                        if (resultadoSalida != null)
                        {
                            valoresEncontrados["fhSalida"] = FechaHelper.ConvertirDeUtc(resultadoSalida.Item1.MessageTime);
                            senalPorColumna["fhSalida"] = ClasificarSenal(resultadoSalida.Item1, TipoEventoGps.Salida);
                            cursor = resultadoSalida.Item2;
                            horaCotaInferior = resultadoSalida.Item1.MessageTime;
                        }
                    }
                }

                if (!UsarValorConfirmadoSiExiste("fhLlegadaBaseFinal"))
                {
                    var resultadoBaseFinal = buscar("BASE_LLEGADA_FINAL", 2, cursor, TipoEventoGps.Llegada, horaCotaInferior);
                    if (resultadoBaseFinal != null)
                    {
                        valoresEncontrados["fhLlegadaBaseFinal"] = FechaHelper.ConvertirDeUtc(resultadoBaseFinal.Item1.MessageTime);
                        senalPorColumna["fhLlegadaBaseFinal"] = ClasificarSenal(resultadoBaseFinal.Item1, TipoEventoGps.Llegada);
                    }
                }

                int totalConfirmados = valoresEncontrados.Count + valoresYaConfirmados.Count;
                if (totalConfirmados == 0)
                    return ResultadoConsultaGpsExportacion.Fallo("El GPS no reportó ningún punto de control coincidente para este viaje.");

                if (valoresEncontrados.Count > 0)
                {
                    GuardarValores(idSeguimiento, valoresEncontrados, rama);

                    var camposSenalDebil = senalPorColumna
                        .Where(kv => kv.Value == ResultadoCampoGps.SenalRevisar)
                        .Select(kv => kv.Key)
                        .ToList();

                    AuditoriaHelper.Registrar("CONSULTAR_HORA_GPS", "SeguimientoExportacion", idSeguimiento,
                        $"GPS actualizó {valoresEncontrados.Count} campo(s) de fecha/hora" +
                        (rama != null && rama != ramaExistente ? $" (ruta detectada: {rama})" : "") +
                        (camposSenalDebil.Count > 0 ? $". Revisar manualmente: {string.Join(", ", camposSenalDebil)}." : "."));
                }

                // ColumnasGps es la única fuente de verdad de qué campos llena el GPS: es la misma
                // lista que alimenta el SELECT y `valoresYaConfirmados`, así que el conteo y el
                // detalle no pueden desincronizarse si mañana se agrega un checkpoint.
                string ramaTexto = rama != null ? $" (ruta: {rama})" : " (no se detectó si fue por Jave o Inbalnor)";
                var resultadoFinal = new ResultadoConsultaGpsExportacion
                {
                    Exito = true,
                    Mensaje = valoresEncontrados.Count > 0
                        ? $"GPS actualizó {valoresEncontrados.Count} de {ColumnasGps.Length} campos ({totalConfirmados} confirmados en total){ramaTexto}."
                        : $"No había campos nuevos por actualizar — ya hay {totalConfirmados} de {ColumnasGps.Length} campos confirmados{ramaTexto}."
                };
                foreach (var columna in ColumnasGps)
                    resultadoFinal.Campos.Add(new ResultadoCampoGps
                    {
                        Columna = columna,
                        Encontrado = valoresEncontrados.ContainsKey(columna) || valoresYaConfirmados.ContainsKey(columna),
                        SenalConfianza = senalPorColumna.ContainsKey(columna) ? senalPorColumna[columna]
                            : valoresYaConfirmados.ContainsKey(columna) ? ResultadoCampoGps.SenalYaConfirmado : null
                    });

                return resultadoFinal;
            }
            catch (OnwayApiException ex)
            {
                LogSGV.Error(ex, "Error consultando GPS Onway para seguimiento {IdSeguimiento}", idSeguimiento);
                return ResultadoConsultaGpsExportacion.Fallo(ex.MensajeParaUsuario());
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
