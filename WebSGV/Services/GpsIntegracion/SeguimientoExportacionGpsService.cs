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
    /// por día el historial del Tracto correspondiente a cada tramo (Tracto 1 = carga
    /// Base-Trujillo-Base, Tracto 2 = descarga Base-Ecuador-Base), cacheando el historial por
    /// (dispositivo, día) para no repetir llamadas al API, y detecta cada tramo con geocercas en
    /// polígono (<see cref="TrazabilidadCargaService"/>, <see cref="TrazabilidadDescargaService"/>).
    /// Los campos que no son un punto geográfico (F.H. Registro, F.H. Programación, autorización de
    /// nacionalización, inicio/término de carga y descarga, incidencias) no se tocan — siguen siendo manuales.
    /// </summary>
    public static class SeguimientoExportacionGpsService
    {
        /// <summary>Días de historial del Tracto 2 que se revisan desde que la carga salió de la planta.</summary>
        private const int VentanaMaximaDias = 12;

        /// <summary>Las 18 columnas de fecha/hora que puede llenar el GPS.</summary>
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

                Func<int, DateTime, DateTime, List<OnwayHistoryPoint>> historialEntre = (numeroTracto, desdeDia, hastaDia) =>
                {
                    var puntos = new List<OnwayHistoryPoint>();
                    for (DateTime dia = desdeDia; dia <= hastaDia && dia <= FechaHelper.Hoy(); dia = dia.AddDays(1))
                        puntos.AddRange(obtenerHistorialDia(numeroTracto, dia));
                    return puntos;
                };

                var valoresEncontrados = new Dictionary<string, DateTime>();
                var senalPorColumna = new Dictionary<string, string>();

                // Llena la columna con el hito si estaba vacía; los valores ya registrados no se tocan.
                void Registrar(string columna, HitoGps hito)
                {
                    if (hito == null || valoresYaConfirmados.ContainsKey(columna)) return;
                    DateTime horaUtc = hito.HoraUtc.AddTicks(-(hito.HoraUtc.Ticks % TimeSpan.TicksPerSecond)); // el portal muestra segundos enteros
                    valoresEncontrados[columna] = FechaHelper.ConvertirDeUtc(horaUtc);
                    senalPorColumna[columna] = hito.Fuente;
                }

                // Tramo de carga (Tracto 1) y de descarga (Tracto 2) con geocercas en polígono y
                // visitas en orden (TrazabilidadCargaService / TrazabilidadDescargaService), no con
                // círculos por columna: el círculo del ingreso a planta tomaba la espera en la cola de
                // la avenida como ingreso (caso real ANU-794 del 19/10/2026: 07:05 en vez de 15:12).
                string avisoPasoDirecto = null;
                DateTime? salidaPlantaUtc = valoresYaConfirmados.TryGetValue("fhSalidaPlanta", out var salidaPlantaConfirmada)
                    ? FechaHelper.ConvertirAUtc(salidaPlantaConfirmada) : (DateTime?)null;
                if (dispositivo1 != null)
                {
                    var carga = TrazabilidadCargaService.DetectarTramoCarga(
                        historialEntre(1, fechaInicio.AddDays(-2), fechaInicio.AddDays(4)), FechaHelper.ConvertirAUtc(fechaInicio));
                    Registrar("fhSalidaBase1", carga.SalidaBase);
                    Registrar("fhLlegadaTrujillo", carga.LlegadaTrujillo);
                    Registrar("fhIngresoPlanta", carga.IngresoPlanta);
                    Registrar("fhSalidaPlanta", carga.SalidaPlanta);
                    Registrar("fhLlegadaBase2", carga.LlegadaBase);
                    if (!salidaPlantaUtc.HasValue && carga.SalidaPlanta != null) salidaPlantaUtc = carga.SalidaPlanta.HoraUtc;
                    if (carga.PasoDirectoBase != null)
                        avisoPasoDirecto = $" El tracto 1 pasó directo frente a la base sin entrar ({FechaHelper.ConvertirDeUtc(carga.PasoDirectoBase.HoraUtc):dd/MM HH:mm}).";
                }

                // La descarga se busca desde que la carga salió de la planta (o desde la fecha del
                // viaje si no se sabe): la carreta la lleva otro tracto, o el mismo después de pasar por base.
                string rama = ramaExistente;
                if (dispositivo2 != null)
                {
                    DateTime desdeDescargaUtc = salidaPlantaUtc ?? FechaHelper.ConvertirAUtc(fechaInicio);
                    DateTime diaDescarga = FechaHelper.ConvertirDeUtc(desdeDescargaUtc).Date;
                    var descarga = TrazabilidadDescargaService.DetectarTramoDescarga(
                        historialEntre(2, diaDescarga.AddDays(-1), diaDescarga.AddDays(VentanaMaximaDias)), desdeDescargaUtc);
                    Registrar("fhSalidaBase2", descarga.SalidaBase);
                    Registrar("fhLlegadaBodegaNacional", descarga.LlegadaComplex);
                    Registrar("fhIngresoBodegaNacional", descarga.IngresoComplex);
                    Registrar("fhSalidaBodegaNacional", descarga.SalidaComplex);
                    Registrar("fhLlegadaCEBAF", descarga.LlegadaCebaf);
                    Registrar("fhCruceEcuador", descarga.Cruce);
                    Registrar("fhLlegadaTCI", descarga.LlegadaTci);
                    Registrar("fhSalidaTCI", descarga.SalidaTci);
                    Registrar("fhLlegadaPlantaEcuador", descarga.LlegadaPlanta);
                    Registrar("fhLlegadaAlmacen", descarga.LlegadaPlanta); // la administradora pone el mismo valor
                    Registrar("fhIngreso", descarga.IngresoPlanta);
                    Registrar("fhSalida", descarga.SalidaPlanta);
                    Registrar("fhLlegadaBaseFinal", descarga.LlegadaBase);
                    if (rama == null) rama = descarga.PlantaDescarga;
                    if (descarga.PasoDirectoTci && descarga.LlegadaTci != null)
                        avisoPasoDirecto += " Pasó directo por TCI: se tomó el control aduanero cercano.";
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
                string ramaTexto = (rama != null ? $" (ruta: {rama})" : " (no se detectó si fue por Jave o Inbalnor)") + avisoPasoDirecto;
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

            sets.Add("fechaModificacion = dbo.fn_AhoraPeru()");

            string sql = $"UPDATE SeguimientoExportacion SET {string.Join(", ", sets)} WHERE idSeguimiento = @id";
            parametros.Add(DbHelper.Param("@id", idSeguimiento));

            DbHelper.EjecutarNonQuery(sql, parametros.ToArray());
        }
    }
}
