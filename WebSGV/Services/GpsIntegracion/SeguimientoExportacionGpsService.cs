using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.Exportacion;

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

        /// <summary>Días de historial de un viaje de tercero desde su programación (Quito → Trujillo y vuelta a la base).</summary>
        private const int DiasViajeTercero = 12;

        /// <summary>Los terceros llegan a la planta de carga desde la noche anterior.</summary>
        private const int HorasAntesCargaTercero = 12;

        /// <summary>Las 18 columnas de fecha/hora que puede llenar el GPS.</summary>
        private static readonly string[] ColumnasGps =
        {
            "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta", "fhLlegadaBase2",
            "fhSalidaBase2", "fhLlegadaBodegaNacional", "fhIngresoBodegaNacional", "fhSalidaBodegaNacional",
            "fhLlegadaCEBAF", "fhCruceEcuador", "fhLlegadaTCI", "fhSalidaTCI",
            "fhLlegadaPlantaEcuador", "fhLlegadaAlmacen", "fhIngreso", "fhSalida", "fhLlegadaBaseFinal",
        };

        /// <summary>Salidas de base de cada tramo: se registran mientras la unidad va en camino y se pueden afinar después.</summary>
        private static readonly HashSet<string> ColumnasSalidaBase =
            new HashSet<string>(new[] { "fhSalidaBase1", "fhSalidaBase2" }, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Trae en paralelo a <see cref="HistorialGpsCache"/> el historial que van a leer varios
        /// viajes: cada tramo es (placa, primer día, último día). Después cada
        /// <see cref="ConsultarYActualizar"/> lo encuentra en memoria o en disco.
        /// </summary>
        public static void PrecargarHistorial(IEnumerable<Tuple<string, DateTime, DateTime>> tramos, TimeSpan presupuesto)
        {
            try
            {
                var cliente = new OnwayApiClient();
                var hoy = FechaHelper.Hoy();
                var dias = new List<KeyValuePair<OnwayDevice, DateTime>>();
                foreach (var placa in tramos.GroupBy(t => t.Item1.Trim().ToUpperInvariant()))
                {
                    var dispositivo = cliente.BuscarDispositivoPorPlaca(placa.Key);
                    if (dispositivo == null) continue;
                    foreach (var t in placa)
                        for (DateTime dia = t.Item2.Date; dia <= t.Item3.Date && dia <= hoy; dia = dia.AddDays(1))
                            dias.Add(new KeyValuePair<OnwayDevice, DateTime>(dispositivo, dia));
                }
                HistorialGpsCache.Precargar(cliente, dias, presupuesto);
            }
            catch (OnwayApiException ex)
            {
                // Cada viaje vuelve a intentarlo y muestra el error.
                LogSGV.Error(ex, "No se pudo precargar el historial GPS de los viajes abiertos");
            }
        }

        /// <summary>
        /// Días de historial que lee <see cref="ConsultarYActualizar"/> en cada tramo, a partir de lo
        /// que ya muestra el tablero (aproximado: lo que falte se pide al consultar el viaje).
        /// </summary>
        public static IEnumerable<Tuple<string, DateTime, DateTime>> DiasDeHistorial(string tracto1, string tracto2,
            DateTime inicio, DateTime? salidaPlanta, bool cargaCompleta, string tractoActivo = null)
        {
            // La posición actual del tracto que lleva la carga sale del historial de hoy.
            if (!string.IsNullOrWhiteSpace(tractoActivo))
                yield return Tuple.Create(tractoActivo, FechaHelper.Hoy(), FechaHelper.Hoy());
            if (!string.IsNullOrWhiteSpace(tracto1) && !cargaCompleta)
                yield return Tuple.Create(tracto1, inicio.Date.AddDays(-2), inicio.Date.AddDays(4));
            if (!string.IsNullOrWhiteSpace(tracto2))
            {
                DateTime dia = (salidaPlanta ?? inicio).Date;
                yield return Tuple.Create(tracto2, dia.AddDays(-1), dia.AddDays(VentanaMaximaDias));
            }
        }

        /// <summary>Lo mismo que <see cref="DiasDeHistorial"/> para un viaje de un cliente tercero (un solo recorrido).</summary>
        public static IEnumerable<Tuple<string, DateTime, DateTime>> DiasDeHistorialTercero(string tracto1, string tracto2,
            DateTime inicio, string tractoActivo)
        {
            if (!string.IsNullOrWhiteSpace(tractoActivo))
                yield return Tuple.Create(tractoActivo, FechaHelper.Hoy(), FechaHelper.Hoy());
            if (!string.IsNullOrWhiteSpace(tracto1))
                yield return Tuple.Create(tracto1, inicio.Date.AddDays(-1), inicio.Date.AddDays(DiasViajeTercero));
            if (!string.IsNullOrWhiteSpace(tracto2) && !string.Equals(tracto1, tracto2, StringComparison.OrdinalIgnoreCase))
                yield return Tuple.Create(tracto2, inicio.Date, inicio.Date.AddDays(DiasViajeTercero));
        }

        /// <summary>
        /// Guarda en el viaje el último punto del tracto activo (hoy; ayer si hoy todavía no reportó).
        /// Si el GPS falla no se pierde la consulta de las horas: la posición queda como estaba.
        /// </summary>
        /// <summary>Hora local de un hito, en segundos enteros como la muestra el portal.</summary>
        private static DateTime HoraLocalGps(HitoGps hito) =>
            FechaHelper.ConvertirDeUtc(hito.HoraUtc.AddTicks(-(hito.HoraUtc.Ticks % TimeSpan.TicksPerSecond)));

        private static void GuardarUltimaPosicion(int idSeguimiento, OnwayApiClient cliente, string placa,
            string placaTracto1, OnwayDevice dispositivo1, OnwayDevice dispositivo2)
        {
            var dispositivo = placa == null ? null
                : string.Equals(placa, placaTracto1, StringComparison.OrdinalIgnoreCase) ? dispositivo1 : dispositivo2;
            if (dispositivo == null) return;

            try
            {
                OnwayHistoryPoint ultimo = null;
                for (int atras = 0; atras <= 1 && ultimo == null; atras++)
                    ultimo = HistorialGpsCache.Dia(cliente, dispositivo, FechaHelper.Hoy().AddDays(-atras))
                        .Where(p => p != null && !(p.Lat == 0 && p.Lng == 0))
                        .OrderBy(TrazabilidadCargaService.Hora)
                        .LastOrDefault();
                if (ultimo == null) return;

                string direccion = ultimo.Address;
                DbHelper.EjecutarNonQuery(
                    @"UPDATE SeguimientoExportacion SET fhUltimaPosicion = @hora, ultimaLatitud = @lat, ultimaLongitud = @lng,
                             ultimaVelocidad = @vel, ultimaDireccion = @dir, ultimaPlacaGps = @placa, gpsAproximadoDe = NULL
                      WHERE idSeguimiento = @id",
                    DbHelper.Param("@id", idSeguimiento),
                    DbHelper.Param("@hora", FechaHelper.ConvertirDeUtc(TrazabilidadCargaService.Hora(ultimo))),
                    DbHelper.Param("@lat", Math.Round((decimal)ultimo.Lat, 6)),
                    DbHelper.Param("@lng", Math.Round((decimal)ultimo.Lng, 6)),
                    DbHelper.Param("@vel", Math.Round((decimal)ultimo.Speed, 1)),
                    DbHelper.Param("@dir", direccion != null && direccion.Length > 300 ? direccion.Substring(0, 300) : direccion),
                    DbHelper.Param("@placa", placa));
            }
            catch (OnwayApiException ex)
            {
                LogSGV.Error(ex, "No se pudo leer la posición actual de {Placa} para el seguimiento {IdSeguimiento}", placa, idSeguimiento);
            }
        }

        public static ResultadoConsultaGpsExportacion ConsultarYActualizar(int idSeguimiento)
        {
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT se.tracto1, se.tracto2, se.fhSalidaBase1, se.fhProgramacion, se.fechaRegistro,
                       se.fhLlegadaTrujillo, se.fhIngresoPlanta, se.fhSalidaPlanta, se.fhLlegadaBase2,
                       se.fhSalidaBase2, se.fhLlegadaBodegaNacional, se.fhIngresoBodegaNacional, se.fhSalidaBodegaNacional,
                       se.fhLlegadaCEBAF, se.fhCruceEcuador, se.fhLlegadaTCI, se.fhSalidaTCI,
                       se.fhLlegadaPlantaEcuador, se.fhLlegadaAlmacen, se.fhIngreso, se.fhSalida, se.fhLlegadaBaseFinal,
                       se.bodegaEcuatoriana, se.tipoRuta, se.lugarCarga, se.lugarEntrega, se.lugarFin,
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
            string tipoRuta = row["tipoRuta"] == DBNull.Value ? null : row["tipoRuta"].ToString().Trim();
            bool esTercero = RutaTercero.Es(tipoRuta);
            string lugarEntrega = row["lugarEntrega"] == DBNull.Value ? null : row["lugarEntrega"].ToString().Trim();

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

            // La carga de este viaje es posterior a la del viaje anterior del mismo tracto: sin este
            // límite, el viaje programado al día siguiente tomaba la visita a la planta del anterior
            // (TBM-898 del 21/10/2026, carreta TDX-994, quedó con las horas de su carga del 20/10).
            // Los terceros llegan a la planta de carga la noche antes (Chapaira, Cartavio).
            DateTime desdeCargaUtc = FechaHelper.ConvertirAUtc(esTercero ? fechaInicio.AddHours(-HorasAntesCargaTercero) : fechaInicio);
            if (!string.IsNullOrEmpty(placaTracto1))
            {
                object salidaAnterior = DbHelper.EjecutarEscalar(@"
                    SELECT MAX(fhSalidaPlanta) FROM SeguimientoExportacion
                    WHERE activo = 1 AND idSeguimiento <> @id AND ISNULL(estado, '') <> 'CANCELADO'
                      AND REPLACE(tracto1, '-', '') = REPLACE(@tracto, '-', '')
                      AND CAST(fhProgramacion AS DATE) < CAST(@prog AS DATE)",
                    DbHelper.Param("@id", idSeguimiento), DbHelper.Param("@tracto", placaTracto1),
                    DbHelper.Param("@prog", row["fhProgramacion"] == DBNull.Value ? (object)fechaInicio : row["fhProgramacion"]));
                if (salidaAnterior != null && salidaAnterior != DBNull.Value)
                {
                    DateTime salidaAnteriorUtc = FechaHelper.ConvertirAUtc(Convert.ToDateTime(salidaAnterior));
                    if (salidaAnteriorUtc > desdeCargaUtc) desdeCargaUtc = salidaAnteriorUtc;
                }
            }

            try
            {
                var cliente = new OnwayApiClient();

                var dispositivo1 = string.IsNullOrEmpty(placaTracto1) ? null : cliente.BuscarDispositivoPorPlaca(placaTracto1);
                var dispositivo2 = string.IsNullOrEmpty(placaTracto2) ? null : cliente.BuscarDispositivoPorPlaca(placaTracto2);
                if (dispositivo1 == null && dispositivo2 == null)
                    return ResultadoConsultaGpsExportacion.Fallo(
                        $"{string.Join(" ni ", new[] { placaTracto1, placaTracto2 }.Where(p => !string.IsNullOrEmpty(p)).Distinct())} no tiene equipo GPS en Onway.");

                // Los días ya terminados salen de HistorialGpsCache (disco), no del API.
                Func<int, DateTime, List<OnwayHistoryPoint>> obtenerHistorialDia = (numeroTracto, dia) =>
                {
                    var dispositivo = numeroTracto == 1 ? dispositivo1 : dispositivo2;
                    return dispositivo == null ? new List<OnwayHistoryPoint>() : HistorialGpsCache.Dia(cliente, dispositivo, dia);
                };

                Func<int, DateTime, DateTime, List<OnwayHistoryPoint>> historialEntre = (numeroTracto, desdeDia, hastaDia) =>
                {
                    var puntos = new List<OnwayHistoryPoint>();
                    for (DateTime dia = desdeDia; dia <= hastaDia && dia <= FechaHelper.Hoy(); dia = dia.AddDays(1))
                        puntos.AddRange(obtenerHistorialDia(numeroTracto, dia));
                    return puntos;
                };

                // Lo que detecta el GPS se guarda siempre en SeguimientoHito (hora, punto, regla),
                // aunque la columna ya tenga valor: así el detalle muestra "Excel 06:01 / GPS 06:01:28".
                // sp_SE_RegistrarHitoGps solo llena las columnas vacías; una hora del Excel o
                // corregida a mano no se toca.
                var detectados = new List<Tuple<string, HitoGps>>();
                void Registrar(string columna, HitoGps hito)
                {
                    if (hito != null) detectados.Add(Tuple.Create(columna, hito));
                }

                // Un tramo con todas sus horas registradas y ya revisadas por el GPS (o corregidas a
                // mano) no se vuelve a consultar: ahorra días de historial en cada actualización.
                var hitosGuardados = DbHelper.ConsultarTabla(
                        "SELECT columna, horaGps, regla FROM SeguimientoHito WHERE idSeguimiento = @id AND (horaGps IS NOT NULL OR fuente = 'MANUAL')",
                        DbHelper.Param("@id", idSeguimiento))
                    .Rows.Cast<DataRow>().ToList();
                var revisadas = new HashSet<string>(hitosGuardados.Select(r => Convert.ToString(r["columna"])), StringComparer.OrdinalIgnoreCase);
                // Lo que el GPS ya había detectado igual en una consulta anterior (misma hora y regla) no se vuelve a escribir.
                var yaGuardado = new HashSet<string>(hitosGuardados
                    .Where(r => r["horaGps"] != DBNull.Value)
                    .Select(r => Convert.ToString(r["columna"]) + "|" + Convert.ToDateTime(r["horaGps"]).ToString("s") + "|" + Convert.ToString(r["regla"])),
                    StringComparer.OrdinalIgnoreCase);
                bool TramoCompleto(IEnumerable<string> columnas) =>
                    columnas.All(c => valoresYaConfirmados.ContainsKey(c) && revisadas.Contains(c));
                var columnasCarga = new[] { "fhSalidaBase1", "fhLlegadaTrujillo", "fhIngresoPlanta", "fhSalidaPlanta", "fhLlegadaBase2" };
                var columnasDescarga = ColumnasGps.Except(columnasCarga).ToArray();

                // Tramo de carga (Tracto 1) y de descarga (Tracto 2) con geocercas en polígono y
                // visitas en orden (TrazabilidadCargaService / TrazabilidadDescargaService), no con
                // círculos por columna: el círculo del ingreso a planta tomaba la espera en la cola de
                // la avenida como ingreso (caso real ANU-794 del 19/10/2026: 07:05 en vez de 15:12).
                string avisoPasoDirecto = null;
                DateTime? salidaPlantaUtc = valoresYaConfirmados.TryGetValue("fhSalidaPlanta", out var salidaPlantaConfirmada)
                    ? FechaHelper.ConvertirAUtc(salidaPlantaConfirmada) : (DateTime?)null;

                // Clientes terceros: un solo recorrido (carga → frontera → descarga → fin) con sus geocercas.
                string lugarEntregaDetectado = null, lugarFinDetectado = null;
                DateTime? llegadaGrifoTercero = null, salidaGrifoTercero = null;
                if (esTercero)
                {
                    DateTime hasta = fechaInicio.AddDays(DiasViajeTercero);
                    var tercero = TrazabilidadTercerosService.Detectar(tipoRuta,
                        row["lugarCarga"] == DBNull.Value ? null : row["lugarCarga"].ToString(), lugarEntrega,
                        historialEntre(1, fechaInicio.AddDays(-1), hasta),
                        dispositivo2 != null && !string.Equals(placaTracto1, placaTracto2, StringComparison.OrdinalIgnoreCase)
                            ? historialEntre(2, fechaInicio, hasta) : null,
                        desdeCargaUtc, descargaAsignada: !string.IsNullOrEmpty(placaTracto2));
                    foreach (var h in tercero.Hitos) Registrar(h.Item1, h.Item2);
                    if (lugarEntrega == null) lugarEntregaDetectado = tercero.LugarEntrega;
                    lugarFinDetectado = tercero.LugarFin;
                    if (tercero.LlegadaGrifo != null) llegadaGrifoTercero = HoraLocalGps(tercero.LlegadaGrifo);
                    if (tercero.SalidaGrifo != null) salidaGrifoTercero = HoraLocalGps(tercero.SalidaGrifo);
                }

                if (!esTercero && dispositivo1 != null && !TramoCompleto(columnasCarga))
                {
                    var carga = TrazabilidadCargaService.DetectarTramoCarga(
                        historialEntre(1, fechaInicio.AddDays(-2), fechaInicio.AddDays(4)), desdeCargaUtc);
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
                bool enEcuador = false;
                DateTime? llegadaGrifo = llegadaGrifoTercero, salidaGrifo = salidaGrifoTercero;
                if (!esTercero && dispositivo2 != null && !TramoCompleto(columnasDescarga))
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
                    // La planta donde el GPS vio entrar a la unidad manda sobre la del despacho o la
                    // lista de destinos (desvío en ruta: la lista decía Milagro y fue a Jave).
                    if (descarga.PlantaDescarga != null) rama = descarga.PlantaDescarga;
                    enEcuador = descarga.Cruce != null || descarga.LlegadaTci != null || valoresYaConfirmados.ContainsKey("fhCruceEcuador");
                    if (descarga.PasoDirectoTci && descarga.LlegadaTci != null)
                        avisoPasoDirecto += " Pasó directo por TCI: se tomó el control aduanero cercano.";
                    // Grifo Santa Rosa de vuelta (no es hito del STATUS GENERAL: va en sus columnas).
                    if (descarga.LlegadaGrifo != null) llegadaGrifo = HoraLocalGps(descarga.LlegadaGrifo);
                    if (descarga.SalidaGrifo != null) salidaGrifo = HoraLocalGps(descarga.SalidaGrifo);
                }

                var valoresEncontrados = new Dictionary<string, DateTime>();
                var senalPorColumna = new Dictionary<string, string>();
                foreach (var d in detectados)
                {
                    var hito = d.Item2;
                    DateTime horaUtc = hito.HoraUtc.AddTicks(-(hito.HoraUtc.Ticks % TimeSpan.TicksPerSecond)); // el portal muestra segundos enteros
                    DateTime horaLocal = FechaHelper.ConvertirDeUtc(horaUtc);
                    bool esAlertaOnway = hito.Fuente != null && hito.Fuente.StartsWith("Alerta de geocerca de Onway", StringComparison.OrdinalIgnoreCase);
                    string regla = hito.Fuente != null && hito.Fuente.Length > 200 ? hito.Fuente.Substring(0, 200) : hito.Fuente;
                    if (valoresYaConfirmados.ContainsKey(d.Item1) && yaGuardado.Contains(d.Item1 + "|" + horaLocal.ToString("s") + "|" + regla))
                        continue;
                    object aplicado = DbHelper.EjecutarEscalarSp("sp_SE_RegistrarHitoGps",
                        DbHelper.Param("@idSeguimiento", idSeguimiento),
                        DbHelper.Param("@columna", d.Item1),
                        DbHelper.Param("@horaGps", horaLocal),
                        DbHelper.Param("@fuenteGps", esAlertaOnway ? "ONWAY" : "GPS"),
                        DbHelper.Param("@latitud", hito.Punto != null ? (object)Math.Round((decimal)hito.Punto.Lat, 6) : null),
                        DbHelper.Param("@longitud", hito.Punto != null ? (object)Math.Round((decimal)hito.Punto.Lng, 6) : null),
                        DbHelper.Param("@regla", regla),
                        // La salida de base puede haberse registrado "en camino" (provisional): el GPS la puede afinar.
                        DbHelper.Param("@reemplazarGps", ColumnasSalidaBase.Contains(d.Item1)));
                    if (Convert.ToInt32(aplicado) == 1)
                    {
                        valoresEncontrados[d.Item1] = horaLocal;
                        senalPorColumna[d.Item1] = hito.Fuente;
                    }
                }

                // Dónde está ahora la unidad (para el STATUS del tablero): el tracto que lleva la carga.
                var horasHoy = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
                foreach (var c in valoresYaConfirmados) horasHoy[c.Key] = c.Value;
                foreach (var c in valoresEncontrados) horasHoy[c.Key] = c.Value;
                GuardarUltimaPosicion(idSeguimiento, cliente,
                    esTercero ? RutaTercero.TractoActivo(horasHoy, placaTracto1, placaTracto2) : StatusUnidad.TractoActivo(horasHoy, placaTracto1, placaTracto2),
                    placaTracto1, dispositivo1, dispositivo2);

                DbHelper.EjecutarNonQuery(
                    "UPDATE SeguimientoExportacion SET fhUltimaConsultaGps = dbo.fn_AhoraPeru()" +
                    (rama != null && !string.Equals(rama, ramaExistente, StringComparison.OrdinalIgnoreCase)
                        ? ", bodegaEcuatoriana = @rama, bodegaDescarga = @rama" : "") +
                    (lugarEntregaDetectado != null ? ", lugarEntrega = ISNULL(lugarEntrega, @entrega)" : "") +
                    (lugarFinDetectado != null ? ", lugarFin = @fin" : "") +
                    (llegadaGrifo.HasValue ? ", fhLlegadaGrifo = @grifoLlegada, fhSalidaGrifo = @grifoSalida" : "") +
                    " WHERE idSeguimiento = @id",
                    DbHelper.Param("@id", idSeguimiento), DbHelper.Param("@rama", rama),
                    DbHelper.Param("@entrega", lugarEntregaDetectado), DbHelper.Param("@fin", lugarFinDetectado),
                    DbHelper.Param("@grifoLlegada", llegadaGrifo), DbHelper.Param("@grifoSalida", salidaGrifo));

                int totalConfirmados = valoresEncontrados.Count + valoresYaConfirmados.Count;
                if (totalConfirmados == 0)
                    return ResultadoConsultaGpsExportacion.Fallo("El GPS no reportó ningún punto de control coincidente para este viaje.");

                if (valoresEncontrados.Count > 0)
                    AuditoriaHelper.Registrar("CONSULTAR_HORA_GPS", "SeguimientoExportacion", idSeguimiento,
                        $"GPS actualizó {valoresEncontrados.Count} campo(s) de fecha/hora" +
                        (rama != null && rama != ramaExistente
                            ? $" (ruta detectada: {rama}{(ramaExistente != null ? ", antes " + ramaExistente : "")})." : "."));

                // ColumnasGps es la única fuente de verdad de qué campos llena el GPS: es la misma
                // lista que alimenta el SELECT y `valoresYaConfirmados`, así que el conteo y el
                // detalle no pueden desincronizarse si mañana se agrega un checkpoint.
                // "No se detectó la planta" solo tiene sentido si la unidad ya está en Ecuador.
                string ramaTexto = (rama != null ? $" (ruta: {rama})" : enEcuador ? " (no se detectó si fue por Jave o Inbalnor)" : "") + avisoPasoDirecto;
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
    }
}
