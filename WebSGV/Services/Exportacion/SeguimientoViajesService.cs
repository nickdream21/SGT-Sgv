using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.GpsIntegracion;
using WebSGV.Services.GpsIntegracion;

namespace WebSGV.Services.Exportacion
{
    /// <summary>Un hito en el detalle: su hora, de dónde salió y la evidencia del GPS.</summary>
    public sealed class HitoDetalle
    {
        public DefinicionHito Definicion { get; set; }
        public DateTime? Hora { get; set; }

        /// <summary>GPS | ONWAY | EXCEL | MANUAL; null = registrada antes de guardar el origen.</summary>
        public string Fuente { get; set; }
        public DateTime? HoraGps { get; set; }
        public string FuenteGps { get; set; }
        public decimal? Latitud { get; set; }
        public decimal? Longitud { get; set; }
        public string Regla { get; set; }
        public string MotivoCorreccion { get; set; }
        public DateTime? FechaCorreccion { get; set; }
    }

    public sealed class ViajeDetalle
    {
        public ViajeResumen Resumen { get; set; }
        public DataRow Fila { get; set; }
        public List<HitoDetalle> Hitos { get; set; }
    }

    public sealed class ResultadoActualizacionGps
    {
        public int Consultados { get; set; }
        public int CamposNuevos { get; set; }
        public int Pendientes { get; set; }

        /// <summary>Viajes cuyos tractos no se movieron: solo se refrescó la hora de su posición.</summary>
        public int SinMovimiento { get; set; }

        /// <summary>Viajes sin señal del GPS aproximados con su grupo (<see cref="AproximacionGrupo"/>).</summary>
        public int Aproximados { get; set; }

        public List<string> Errores { get; } = new List<string>();

        /// <summary>
        /// Onway respondió bien a la consulta de la flota (última posición de todas las unidades; <see cref="FlotaRespondio"/>).
        /// No depende de que algún viaje se pueda seguir: con unidades sin equipo GPS o quietas también es true. Salud.ashx
        /// lo usa para saber si el proveedor responde (<see cref="ActualizacionGpsAutomatica.UltimaRespuestaOnwayProgramada"/>).
        /// </summary>
        public bool OnwayRespondio { get; set; }

        /// <summary>
        /// Respuesta válida de la flota: no null y con al menos una unidad. Una flota vacía con 200 (cuenta o subdominio
        /// cambiado) cuenta como fallo.
        /// </summary>
        public static bool FlotaRespondio<T>(IReadOnlyCollection<T> foto) => foto != null && foto.Count > 0;

        private const string SinEquipo = "no tiene equipo GPS en Onway";

        /// <summary>
        /// Errores de la consulta. Una unidad sin equipo GPS no es una falla: salía en cada actualización
        /// como "Sin respuesta del GPS", una vez por viaje, y tapaba los errores de verdad.
        /// </summary>
        public List<string> Fallas => Errores.Where(x => x.IndexOf(SinEquipo, StringComparison.OrdinalIgnoreCase) < 0).ToList();

        /// <summary>Placas sin equipo GPS, una vez cada una.</summary>
        public List<string> UnidadesSinEquipo => Errores
            .Where(x => x.IndexOf(SinEquipo, StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(x =>
            {
                string antes = x.Substring(0, x.IndexOf(SinEquipo, StringComparison.OrdinalIgnoreCase)).TrimEnd();
                int i = antes.LastIndexOf("): ", StringComparison.Ordinal);
                return i >= 0 ? antes.Substring(i + 3).Trim() : antes;
            })
            .Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>El resultado en palabras, para el tablero (el botón y la actualización en segundo plano).</summary>
        public string MensajeParaUsuario(bool conBotonParaSeguir = true)
        {
            if (Consultados == 0 && SinMovimiento == 0 && Errores.Count == 0)
                return "No hay viajes abiertos con tracto asignado en este rango.";
            string texto = $"Se consultó el GPS de {Consultados} viaje(s): {CamposNuevos} hora(s) nueva(s)." +
                (SinMovimiento > 0 ? $" {SinMovimiento} sin movimiento desde la última consulta (solo se actualizó su hora)." : "") +
                (Aproximados > 0 ? $" {Aproximados} sin señal del GPS, aproximado(s) con su grupo." : "") +
                (Pendientes > 0 ? $" Faltan {Pendientes}" + (conBotonParaSeguir ? ": vuelve a presionar el botón para seguir." : ": siguen en la próxima consulta.") : "");
            var sinEquipo = UnidadesSinEquipo;
            if (sinEquipo.Count > 0)
                texto += $" Sin equipo GPS en Onway (sus horas se llenan a mano): {string.Join(", ", sinEquipo)}.";
            var fallas = Fallas;
            if (fallas.Count > 0)
                texto += " Sin respuesta del GPS: " + string.Join("; ", fallas.Take(3)) + (fallas.Count > 3 ? $" y {fallas.Count - 3} más" : "") + ".";
            return texto;
        }
    }


    /// <summary>
    /// Datos del tablero y del detalle del seguimiento de exportación (SeguimientoExportacion +
    /// SeguimientoHito). Las escrituras van por los sp_SE_* y quedan en la auditoría.
    /// </summary>
    public static class SeguimientoViajesService
    {
        /// <summary>
        /// Tope de la tabla en pantalla y de las listas internas (GPS automático, dashboard, grifo): con "Incluir los
        /// importados" y un año entero hay más de 2000 viajes y la página se vuelve lenta. Si se corta, la tabla avisa.
        /// </summary>
        public const int MaximoFilasTablero = 500;

        /// <summary>
        /// Tope de seguridad de los Excel del tablero: un reporte trae todo lo filtrado (no recorta en silencio) y por
        /// encima de esto se niega y pide acotar las fechas. Un año de importados son ~2800 viajes (2024).
        /// </summary>
        public const int MaximoFilasExportacion = 20000;

        private static readonly string[] ColumnasHora =
            HitosSeguimiento.Todos.Select(h => h.Columna)
                .Concat(new[] { "fhLlegadaAlmacen", "fhRegistro", "fhInicioCarga", "fhTerminoCarga",
                                "fhAutorizacionNacionalizacion", "fhInicioDescarga", "fhTerminoDescarga",
                                "fhLlegadaGrifo", "fhSalidaGrifo" })
                .Distinct().ToArray();

        // Paso directo por TCI: la regla del hito de llegada lo dice (TrazabilidadDescargaService).
        private const string SqlBase = @"
            SELECT se.*,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM SeguimientoHito h
                                          WHERE h.idSeguimiento = se.idSeguimiento AND h.columna = 'fhLlegadaTCI'
                                            AND h.regla LIKE '%paso directo%') THEN 1 ELSE 0 END AS BIT) AS pasoDirectoTci
            FROM SeguimientoExportacion se
            WHERE se.activo = 1";

        /// <summary>
        /// Viajes del tablero: los programados en el rango de fechas y, siempre, los abiertos de los despachos o creados
        /// a mano (aunque se programaran antes), para que ninguno en curso quede fuera. Con texto, en todas las fechas.
        /// </summary>
        public static List<ViajeResumen> ListarTablero(FiltroTablero f) => ListarTableroConTotal(f, MaximoFilasTablero).Viajes;

        /// <summary>
        /// Como <see cref="ListarTablero(FiltroTablero)"/>, hasta <paramref name="maximo"/> viajes (los más recientes) y
        /// con cuántos cumplen los filtros, contados en la misma consulta (COUNT(*) OVER antes del TOP).
        /// </summary>
        public static ListadoTablero ListarTableroConTotal(FiltroTablero f, int maximo)
        {
            if (maximo < 1) throw new ArgumentOutOfRangeException(nameof(maximo));
            string texto = string.IsNullOrWhiteSpace(f.Texto) ? null : f.Texto.Trim();
            string planta = string.IsNullOrWhiteSpace(f.Planta) ? null : f.Planta.Trim().ToUpperInvariant();
            // Para los vencidos (la regla de ViajeVencido: abierto y programado antes de hoy − DiasVencido), el viaje siguiente
            // de la misma carreta. El OUTER APPLY va después del TOP: corre solo sobre las filas que se traen y, por la condición
            // de vencido, solo para esas (~0,6 s sobre toda la tabla, 7400 viajes, 27/11/2026).
            DataTable dt = DbHelper.ConsultarTabla($@"
                SELECT x.*, pos.idSeguimiento AS {ColumnaPosteriorId}, pos.cliente AS {ColumnaPosteriorPedido}, pos.fecha AS {ColumnaPosteriorFecha}
                FROM (
                SELECT TOP (@maximo) t.*, COUNT(*) OVER () AS {ColumnaTotal} FROM ({SqlBase}
                  AND (@texto IS NOT NULL OR COALESCE(se.fhProgramacion, se.fechaRegistro) >= @desde AND COALESCE(se.fhProgramacion, se.fechaRegistro) < @hasta
                       OR se.origen IN ('DESPACHO', 'MANUAL') AND ISNULL(se.estado, '') NOT IN ('FINALIZADO', 'CANCELADO') AND se.fhLlegadaBaseFinal IS NULL)
                  AND (@texto IS NULL OR se.cliente LIKE @like OR se.carreta LIKE @like OR se.tracto1 LIKE @like OR se.tracto2 LIKE @like
                       OR se.conductorOrigen LIKE @like OR se.conductorDestino LIKE @like OR se.lugarEntrega LIKE @like)
                  AND (@planta IS NULL OR UPPER(se.bodegaDescarga) = @planta OR UPPER(se.bodegaEcuatoriana) = @planta)
                  AND (@historial = 1 OR se.origen IN ('DESPACHO', 'MANUAL'))
                  AND (@cliente IS NULL OR (@cliente = 'VITAPRO' AND se.tipoRuta IS NULL)
                       OR (@cliente = 'TERCEROS' AND se.tipoRuta IS NOT NULL) OR se.tipoRuta = @cliente)
                ) t
                ORDER BY COALESCE(t.fhProgramacion, t.fechaRegistro) DESC, t.cliente, t.idSeguimiento DESC
                ) x
                OUTER APPLY (
                    SELECT TOP 1 p.idSeguimiento, p.cliente, COALESCE(p.fhProgramacion, p.fechaRegistro) AS fecha
                    FROM SeguimientoExportacion p
                    WHERE ISNULL(x.estado, '') NOT IN ('FINALIZADO', 'CANCELADO') AND x.fhLlegadaBaseFinal IS NULL
                      AND COALESCE(x.fhProgramacion, x.fechaRegistro) < DATEADD(DAY, -@diasVencido, CAST(CAST(dbo.fn_AhoraPeru() AS DATE) AS DATETIME))
                      AND x.carreta IS NOT NULL AND p.carreta = x.carreta AND p.activo = 1 AND p.idSeguimiento <> x.idSeguimiento
                      AND ISNULL(p.estado, '') <> 'CANCELADO'
                      AND COALESCE(p.fhProgramacion, p.fechaRegistro) > COALESCE(x.fhProgramacion, x.fechaRegistro)
                    ORDER BY COALESCE(p.fhProgramacion, p.fechaRegistro), p.idSeguimiento
                ) pos
                ORDER BY COALESCE(x.fhProgramacion, x.fechaRegistro) DESC, x.cliente, x.idSeguimiento DESC",
                DbHelper.Param("@diasVencido", ViajeVencido.DiasVencido),
                DbHelper.Param("@desde", f.Desde.Date),
                DbHelper.Param("@hasta", f.Hasta.Date.AddDays(1)),
                DbHelper.Param("@texto", texto),
                DbHelper.Param("@like", texto == null ? null : DbHelper.PatronContiene(texto)),
                DbHelper.Param("@planta", planta),
                DbHelper.Param("@historial", f.IncluirHistorial),
                DbHelper.Param("@cliente", string.IsNullOrWhiteSpace(f.Cliente) ? null : f.Cliente.Trim().ToUpperInvariant()),
                DbHelper.Param("@maximo", maximo));
            int total = dt.Rows.Count == 0 ? 0 : Convert.ToInt32(dt.Rows[0][ColumnaTotal]);
            var viajes = new List<ViajeResumen>(dt.Rows.Count);
            DateTime ahora = FechaHelper.Ahora();
            foreach (DataRow r in dt.Rows)
            {
                var v = Resumir(r);
                if (v.Vencido && !r.IsNull(ColumnaPosteriorId))
                {
                    v.PosteriorId = Convert.ToInt32(r[ColumnaPosteriorId]);
                    v.PosteriorPedido = Texto(r, ColumnaPosteriorPedido);
                    v.PosteriorFecha = Fecha(r, ColumnaPosteriorFecha);
                }
                if (v.Vencido) AgregarAvisoVencido(v, ahora);
                viajes.Add(v);
            }
            // La cuenta y el viaje posterior no son del viaje: fuera antes de que la fila llegue al STATUS GENERAL.
            foreach (var c in new[] { ColumnaTotal, ColumnaPosteriorId, ColumnaPosteriorPedido, ColumnaPosteriorFecha })
                dt.Columns.Remove(c);
            return new ListadoTablero(viajes, total, maximo);
        }

        private const string ColumnaTotal = "sgvTotalFiltrado";
        private const string ColumnaPosteriorId = "sgvPosteriorId";
        private const string ColumnaPosteriorPedido = "sgvPosteriorPedido";
        private const string ColumnaPosteriorFecha = "sgvPosteriorFecha";

        /// <summary>Aviso de peligro del vencido, el primero de la fila.</summary>
        private static void AgregarAvisoVencido(ViajeResumen v, DateTime ahora) =>
            (v.Avisos = v.Avisos ?? new List<AvisoViaje>()).Insert(0, new AvisoViaje
            {
                Tipo = TipoAviso.Peligro,
                Texto = ViajeVencido.Aviso(v.DiasAbierto, v.Carreta, v.PosteriorPedido, v.PosteriorId, v.PosteriorFecha, ahora),
            });

        /// <summary>
        /// Crea los viajes que faltan a partir de los despachos de carga de exportación desde
        /// <paramref name="desde"/> y los completa con su despacho de descarga y su planta
        /// (sp_SE_SincronizarDespachos). Devuelve (creados, actualizados).
        /// </summary>
        public static Tuple<int, int> SincronizarDespachos(DateTime desde)
        {
            DataTable dt = DbHelper.ConsultarTablaSp("sp_SE_SincronizarDespachos", DbHelper.Param("@desde", desde.Date));
            if (dt.Rows.Count == 0) return Tuple.Create(0, 0);
            return Tuple.Create(Convert.ToInt32(dt.Rows[0]["creados"]), Convert.ToInt32(dt.Rows[0]["actualizados"]));
        }

        /// <summary>Hora (de Perú) de la última consulta al GPS entre los viajes abiertos; null si ninguno se consultó. La pide el tablero abierto (GpsAutomatico.ashx?estado=1).</summary>
        public static DateTime? UltimaConsultaGpsAbiertos()
        {
            object ultima = DbHelper.EjecutarEscalar(
                "SELECT MAX(fhUltimaConsultaGps) FROM SeguimientoExportacion WHERE activo = 1 AND ISNULL(estado, '') NOT IN ('FINALIZADO', 'CANCELADO')");
            return ultima == null || ultima == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(ultima);
        }

        public static ViajeDetalle ObtenerDetalle(int idSeguimiento)
        {
            DataTable dt = DbHelper.ConsultarTabla(SqlBase + " AND se.idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento));
            if (dt.Rows.Count == 0) return null;
            DataRow fila = dt.Rows[0];

            var meta = DbHelper.ConsultarTabla("SELECT * FROM SeguimientoHito WHERE idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento))
                .Rows.Cast<DataRow>()
                .ToDictionary(r => Convert.ToString(r["columna"]), r => r, StringComparer.OrdinalIgnoreCase);

            var resumen = Resumir(fila);
            var hitos = resumen.Hitos.Select(def =>
            {
                meta.TryGetValue(def.Columna, out DataRow m);
                return new HitoDetalle
                {
                    Definicion = def,
                    Hora = Fecha(fila, def.Columna),
                    Fuente = m == null ? null : Texto(m, "fuente"),
                    HoraGps = m == null ? null : Fecha(m, "horaGps"),
                    FuenteGps = m == null ? null : Texto(m, "fuenteGps"),
                    Latitud = m == null || m.IsNull("latitud") ? (decimal?)null : Convert.ToDecimal(m["latitud"]),
                    Longitud = m == null || m.IsNull("longitud") ? (decimal?)null : Convert.ToDecimal(m["longitud"]),
                    Regla = m == null ? null : Texto(m, "regla"),
                    MotivoCorreccion = m == null ? null : Texto(m, "motivoCorreccion"),
                    FechaCorreccion = m == null ? null : Fecha(m, "fechaCorreccion"),
                };
            }).ToList();

            return new ViajeDetalle { Resumen = resumen, Fila = fila, Hitos = hitos };
        }

        /// <summary>Corrige la hora de un hito a mano (con motivo); la llegada a planta en Ecuador también cambia la del almacén.</summary>
        public static void CorregirHito(int idSeguimiento, string columna, DateTime? valor, string motivo, int? idUsuario)
        {
            var def = HitosSeguimiento.Buscar(columna) ?? throw new Common.ErrorNegocioException("Ese hito no existe.");
            foreach (var c in new[] { def.Columna }.Concat(def.ColumnasIguales))
                DbHelper.EjecutarNonQuerySp("sp_SE_CorregirHito",
                    DbHelper.Param("@idSeguimiento", idSeguimiento),
                    DbHelper.Param("@columna", c),
                    DbHelper.Param("@valor", valor),
                    DbHelper.Param("@motivo", motivo),
                    DbHelper.Param("@idUsuario", idUsuario));
            AuditoriaHelper.Registrar("CORREGIR_HITO", "SeguimientoExportacion", idSeguimiento,
                $"{def.Nombre}: {(valor.HasValue ? valor.Value.ToString("dd/MM/yyyy HH:mm:ss") : "sin hora")}. Motivo: {motivo}");
        }

        public static int GuardarDatosViaje(int idSeguimiento, string pedido, string carreta, string tracto1, string chofer1,
            string tracto2, string chofer2, string planta, DateTime? fhProgramacion, int? idUsuario)
        {
            var salida = new System.Data.SqlClient.SqlParameter("@idResultado", SqlDbType.Int) { Direction = ParameterDirection.Output };
            DbHelper.EjecutarNonQuerySp("sp_SE_GuardarDatosViaje",
                DbHelper.Param("@idSeguimiento", idSeguimiento),
                DbHelper.Param("@cliente", pedido),
                DbHelper.Param("@carreta", Mayus(carreta)),
                DbHelper.Param("@tracto1", Mayus(tracto1)),
                DbHelper.Param("@conductorOrigen", Mayus(chofer1)),
                DbHelper.Param("@tracto2", Mayus(tracto2)),
                DbHelper.Param("@conductorDestino", Mayus(chofer2)),
                DbHelper.Param("@bodegaDescarga", string.IsNullOrWhiteSpace(planta) ? null : planta.Trim().ToUpperInvariant()),
                DbHelper.Param("@fhProgramacion", fhProgramacion),
                DbHelper.Param("@idUsuario", idUsuario),
                salida);
            int id = Convert.ToInt32(salida.Value);
            AuditoriaHelper.Registrar(idSeguimiento == 0 ? "INSERT" : "UPDATE", "SeguimientoExportacion", id,
                $"Datos del viaje: pedido {pedido}, carreta {carreta}, carga {tracto1}, descarga {tracto2}.");
            return id;
        }

        public static void GuardarDatosManuales(int idSeguimiento, DateTime? fhRegistro, DateTime? inicioCarga, DateTime? terminoCarga,
            DateTime? autorizacion, DateTime? inicioDescarga, DateTime? terminoDescarga, string bodegaNacional,
            int sacosRobados, int sacosRotos, int sacosMojados, string motivoRetraso, int? idUsuario)
        {
            DbHelper.EjecutarNonQuerySp("sp_SE_GuardarDatosManuales",
                DbHelper.Param("@idSeguimiento", idSeguimiento),
                DbHelper.Param("@fhRegistro", fhRegistro),
                DbHelper.Param("@fhInicioCarga", inicioCarga),
                DbHelper.Param("@fhTerminoCarga", terminoCarga),
                DbHelper.Param("@fhAutorizacionNacionalizacion", autorizacion),
                DbHelper.Param("@fhInicioDescarga", inicioDescarga),
                DbHelper.Param("@fhTerminoDescarga", terminoDescarga),
                DbHelper.Param("@bodegaNacional", bodegaNacional),
                DbHelper.Param("@sacosRobados", sacosRobados),
                DbHelper.Param("@sacosRotos", sacosRotos),
                DbHelper.Param("@sacosMojados", sacosMojados),
                DbHelper.Param("@motivoRetraso", motivoRetraso),
                DbHelper.Param("@idUsuario", idUsuario));
            AuditoriaHelper.Registrar("UPDATE", "SeguimientoExportacion", idSeguimiento, "Datos manuales del seguimiento (carga, descarga, sacos, retraso).");
        }

        /// <summary>
        /// Un viaje CANCELADO (su despacho de carga se anuló, sp_SE_SincronizarDespachos) no se cierra ni se reabre: si
        /// pasara a FINALIZADO volvería a contar en el Dashboard de exportación. sp_SE_CambiarEstado lo rechaza con este texto.
        /// </summary>
        public const string MensajeViajeAnulado = "Este viaje está anulado (su despacho se anuló); no se puede cerrar ni reabrir.";

        public static bool EsAnulado(string estado) => string.Equals((estado ?? "").Trim(), "CANCELADO", StringComparison.OrdinalIgnoreCase);

        public static void CambiarEstado(int idSeguimiento, string estado, int? idUsuario)
        {
            DbHelper.EjecutarNonQuerySp("sp_SE_CambiarEstado",
                DbHelper.Param("@idSeguimiento", idSeguimiento),
                DbHelper.Param("@estado", estado),
                DbHelper.Param("@idUsuario", idUsuario));
            AuditoriaHelper.Registrar(estado == "FINALIZADO" ? "FINALIZAR" : "UPDATE", "SeguimientoExportacion", idSeguimiento,
                "Estado del viaje: " + estado);
        }

        /// <summary>
        /// Cierre administrativo de un viaje vencido (<see cref="ViajeVencido"/>): pasa a FINALIZADO con su motivo, la fecha
        /// y el usuario (sp_SE_CerrarVencido, que rechaza uno cerrado, anulado o no vencido). Lo pide un administrador
        /// desde la pestaña Vencidos del tablero; queda en la auditoría.
        /// </summary>
        public static void CerrarVencido(int idSeguimiento, string motivo, int? idUsuario)
        {
            string m = (motivo ?? "").Trim();
            if (m.Length == 0) throw new Common.ErrorNegocioException("Escribe el motivo del cierre.");
            if (m.Length > ViajeVencido.LargoMaximoMotivo)
                throw new Common.ErrorNegocioException($"El motivo admite hasta {ViajeVencido.LargoMaximoMotivo} caracteres.");
            DbHelper.EjecutarNonQuerySp("sp_SE_CerrarVencido",
                DbHelper.Param("@idSeguimiento", idSeguimiento),
                DbHelper.Param("@motivo", m),
                DbHelper.Param("@idUsuario", idUsuario));
            AuditoriaHelper.Registrar("CIERRE_ADMINISTRATIVO", "SeguimientoExportacion", idSeguimiento, m);
        }

        /// <summary>
        /// <see cref="CerrarVencido"/> de varios viajes, cada uno por su lado: uno que no se puede cerrar no frena a los
        /// demás. Devuelve cuántos se cerraron y el motivo de los que no (texto para el usuario).
        /// </summary>
        public static Tuple<int, List<string>> CerrarVencidos(IEnumerable<int> ids, string motivo, int? idUsuario)
        {
            int cerrados = 0;
            var errores = new List<string>();
            foreach (int id in (ids ?? Enumerable.Empty<int>()).Distinct())
            {
                try
                {
                    CerrarVencido(id, motivo, idUsuario);
                    cerrados++;
                }
                catch (Exception ex)
                {
                    errores.Add($"Viaje #{id}: " + MensajeErrorHelper.ParaUsuario(ex, "Error al cerrar el viaje vencido {Id}", id));
                }
            }
            return Tuple.Create(cerrados, errores);
        }

        /// <summary>
        /// Los viajes cuyo GPS se consulta (<see cref="ActualizarGpsAbiertos"/>): no cerrados, con algún tracto y ya
        /// programados (las cargas de mañana o después, que el tablero muestra, aún no salen). Salud.ashx usa la
        /// misma regla para saber si el GPS automático está atrasado.
        /// Un vencido (<see cref="ViajeVencido"/>) cuya carreta ya salió en un viaje posterior no se consulta: su GPS es el
        /// del viaje nuevo. El vencido sin viaje posterior sigue (al final de la cola, <see cref="OrdenarParaGps"/>).
        /// </summary>
        public static IEnumerable<ViajeResumen> AbiertosParaGps(IEnumerable<ViajeResumen> viajes, DateTime hoy) =>
            viajes
                .Where(v => v.Fase.Grupo != "cerrado" && (!string.IsNullOrWhiteSpace(v.Tracto1) || !string.IsNullOrWhiteSpace(v.Tracto2)))
                .Where(v => !(v.FhProgramacion.HasValue && v.FhProgramacion.Value.Date > hoy.Date))
                .Where(v => !(v.Vencido && v.PosteriorId.HasValue));

        /// <summary>Un viaje programado hace más de esto y todavía abierto se consulta al final (la regla del vencido).</summary>
        private const int DiasViajeAtrasado = ViajeVencido.DiasVencido;

        /// <summary>
        /// Consulta el GPS de los viajes abiertos del tablero, hasta <paramref name="maximo"/> viajes
        /// o <paramref name="presupuesto"/> de tiempo. Primero las cargas en curso (las programadas
        /// más recientes antes), luego las descargas y los retornos; los viajes atrasados al final.
        /// El historial de hoy de todos los tractos se trae antes en paralelo.
        /// </summary>
        public static ResultadoActualizacionGps ActualizarGpsAbiertos(IEnumerable<ViajeResumen> viajes, int maximo, TimeSpan presupuesto)
        {
            DateTime hoy = FechaHelper.Hoy();
            var abiertos = OrdenarParaGps(AbiertosParaGps(viajes, hoy), hoy).ToList();
            var r = new ResultadoActualizacionGps();
            var reloj = Stopwatch.StartNew();

            // Una sola consulta con la última posición de toda la flota: los viajes cuyos tractos no se movieron
            // desde su última consulta completa solo refrescan la hora de su posición (MovimientoGps).
            Dictionary<string, OnwayDevice> foto = null;
            try { foto = new OnwayApiClient().UltimasPosiciones(); }
            catch (Exception ex) when (ex is OnwayApiException || ex is System.Net.Http.HttpRequestException || ex is System.Threading.Tasks.TaskCanceledException)
            {
                LogSGV.Advertencia("No se pudo traer la última posición de la flota: {Error}", ex.Message); // se consulta todo, como antes
            }
            DateTime ahoraUtc = DateTime.UtcNow;
            r.OnwayRespondio = ResultadoActualizacionGps.FlotaRespondio(foto);
            if (foto != null)
            {
                var quietos = abiertos.Where(v => MovimientoGps.SinMovimiento(v.Id, MovimientoGps.Posiciones(new[] { v.Tracto1, v.Tracto2 }, foto), ahoraUtc)).ToList();
                foreach (var v in quietos)
                    RefrescarPosicion(v, foto);
                r.SinMovimiento = quietos.Count;
                abiertos = abiertos.Except(quietos).ToList();
            }

            var tramos = abiertos.Take(maximo).SelectMany(v =>
            {
                v.Horas.TryGetValue("fhSalidaBase1", out var salidaBase);
                v.Horas.TryGetValue("fhSalidaPlanta", out var salidaPlanta);
                DateTime inicio = (salidaBase ?? v.FhProgramacion ?? FechaHelper.Hoy()).Date;
                if (RutaTercero.Es(v.TipoRuta))
                    return SeguimientoExportacionGpsService.DiasDeHistorialTercero(v.Tracto1, v.Tracto2, inicio,
                        RutaTercero.TractoActivo(v.Horas, v.Tracto1, v.Tracto2));
                return SeguimientoExportacionGpsService.DiasDeHistorial(v.Tracto1, v.Tracto2, inicio, salidaPlanta,
                    v.AvanceCarga == HitosSeguimiento.Carga.Count, StatusUnidad.TractoActivo(v.Horas, v.Tracto1, v.Tracto2));
            }).ToList();
            SeguimientoExportacionGpsService.PrecargarHistorial(tramos, TimeSpan.FromTicks(presupuesto.Ticks * 2 / 3));

            foreach (var v in abiertos)
            {
                if (r.Consultados >= maximo || reloj.Elapsed > presupuesto) break;
                var res = SeguimientoExportacionGpsService.ConsultarYActualizar(v.Id);
                r.Consultados++;
                if (res.Exito) r.CamposNuevos += res.Campos.Count(c => c.Encontrado && !c.YaEstabaConfirmado);
                else r.Errores.Add($"{v.Pedido} ({v.Tracto1 ?? v.Tracto2}, carreta {v.Carreta}): {res.Mensaje}");
                if (res.Exito && foto != null)
                    MovimientoGps.Registrar(v.Id, MovimientoGps.Posiciones(new[] { v.Tracto1, v.Tracto2 }, foto), ahoraUtc);
            }
            r.Pendientes = abiertos.Count - r.Consultados;
            if (foto != null) r.Aproximados = AproximarPorGrupo(foto);
            return r;
        }

        /// <summary>Días hacia atrás (por programación) en que se buscan los grupos para aproximar a los que no tienen señal.</summary>
        private const int DiasGrupo = 20;

        /// <summary>
        /// Los viajes cuyo tracto no manda señal toman los hitos y la posición de su grupo (<see cref="AproximacionGrupo"/>).
        /// Se hace con todos los viajes abiertos, no solo los del filtro del tablero, para que el grupo esté completo.
        /// </summary>
        private static int AproximarPorGrupo(Dictionary<string, OnwayDevice> foto)
        {
            DateTime hoy = FechaHelper.Hoy();
            var viajes = ListarTablero(new FiltroTablero { Desde = hoy.AddDays(-DiasGrupo), Hasta = hoy.AddDays(1) });
            DateTime? Senal(string tracto)
            {
                if (!foto.TryGetValue(tracto, out var d) || d.Expand?.LastLocation == null) return null;
                var l = d.Expand.LastLocation;
                DateTime? horaUtc = l.GeneratedTime ?? l.MessageTime;
                return horaUtc.HasValue ? FechaHelper.ConvertirDeUtc(DateTime.SpecifyKind(horaUtc.Value, DateTimeKind.Utc)) : (DateTime?)null;
            }
            var aproximados = AproximacionGrupo.Calcular(viajes, Senal, FechaHelper.Ahora(), v => Texto(v.Fila, "ultimaDireccion"));
            foreach (var a in aproximados)
            {
                string regla = $"Aproximado con su grupo: el GPS de {a.Tracto} no tiene señal";
                foreach (var h in a.Hitos)
                    DbHelper.ConsultarTablaSp("sp_SE_RegistrarHitoGps",
                        DbHelper.Param("@idSeguimiento", a.Viaje.Id),
                        DbHelper.Param("@columna", h.Key),
                        DbHelper.Param("@horaGps", h.Value),
                        DbHelper.Param("@fuenteGps", "GRUPO"),
                        DbHelper.Param("@regla", regla));
                if (a.Posicion != null)
                    DbHelper.EjecutarNonQuery(@"
                        UPDATE SeguimientoExportacion
                        SET fhUltimaPosicion = @hora, ultimaLatitud = @lat, ultimaLongitud = @lng, ultimaVelocidad = @vel,
                            ultimaDireccion = @dir, ultimaPlacaGps = @placa, gpsAproximadoDe = @de, fhUltimaConsultaGps = dbo.fn_AhoraPeru()
                        WHERE idSeguimiento = @id",
                        DbHelper.Param("@id", a.Viaje.Id), DbHelper.Param("@hora", a.Posicion.Hora),
                        DbHelper.Param("@lat", Math.Round((decimal)a.Posicion.Latitud, 6)), DbHelper.Param("@lng", Math.Round((decimal)a.Posicion.Longitud, 6)),
                        DbHelper.Param("@vel", a.Posicion.Velocidad.HasValue ? Math.Round((decimal)a.Posicion.Velocidad.Value, 1) : (decimal?)null),
                        DbHelper.Param("@dir", a.Posicion.Direccion), DbHelper.Param("@placa", a.Tracto), DbHelper.Param("@de", a.Posicion.DeTracto));
            }
            if (aproximados.Count > 0)
                LogSGV.Info("Aproximados con su grupo (GPS sin señal): {Viajes}", string.Join(", ", aproximados.Select(a => a.Tracto + " #" + a.Viaje.Id)));
            return aproximados.Count;
        }

        /// <summary>
        /// Minutos que duró cada etapa en Ecuador en los últimos 60 días, para estimar cuándo baja al grifo un carro que
        /// todavía no sale de la planta: "TCI|planta" (llegada → salida de TCI), "ESPERA|planta" (llegada a la planta →
        /// ingreso) y "DESCARGA|planta" (ingreso → salida). Planta = JAVE, INBALNOR (el lugar de entrega en los
        /// terceros); "ETAPA|*" junta todas las de Vitapro. Se guarda 6 h en memoria.
        /// </summary>
        public static Dictionary<string, List<double>> DuracionesEnEcuador()
        {
            const string clave = "SGV|DuracionesEnEcuador";
            if (System.Web.HttpRuntime.Cache[clave] is Dictionary<string, List<double>> guardadas) return guardadas;
            DataTable dt = DbHelper.ConsultarTabla(@"
                SELECT e.etapa, UPPER(LTRIM(RTRIM(CASE WHEN se.tipoRuta IS NULL THEN se.bodegaDescarga ELSE se.lugarEntrega END))) AS planta,
                       CASE WHEN se.tipoRuta IS NULL THEN 1 ELSE 0 END AS vitapro, DATEDIFF(MINUTE, e.desde, e.hasta) AS minutos
                FROM SeguimientoExportacion se
                CROSS APPLY (VALUES
                    ('TCI', se.fhLlegadaTCI, se.fhSalidaTCI),
                    ('ESPERA', ISNULL(se.fhLlegadaPlantaEcuador, se.fhLlegadaAlmacen), se.fhIngreso),
                    ('DESCARGA', se.fhIngreso, se.fhSalida)
                ) e(etapa, desde, hasta)
                WHERE se.activo = 1 AND ISNULL(se.estado, '') <> 'CANCELADO'
                  AND ISNULL(se.fhSalida, se.fhSalidaTCI) >= DATEADD(DAY, -60, dbo.fn_AhoraPeru())
                  AND e.desde IS NOT NULL AND e.hasta IS NOT NULL AND DATEDIFF(MINUTE, e.desde, e.hasta) BETWEEN 0 AND 4320");
            var res = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            void Agregar(string k, double m)
            {
                if (!res.TryGetValue(k, out var lista)) res[k] = lista = new List<double>();
                lista.Add(m);
            }
            foreach (DataRow r in dt.Rows)
            {
                string etapa = Texto(r, "etapa"), planta = Texto(r, "planta");
                double minutos = Convert.ToDouble(r["minutos"]);
                if (planta != null) Agregar(etapa + "|" + planta, minutos);
                if (Convert.ToInt32(r["vitapro"]) == 1) Agregar(etapa + "|*", minutos);
            }
            foreach (var l in res.Values) l.Sort();
            System.Web.HttpRuntime.Cache.Insert(clave, res, null, DateTime.UtcNow.AddHours(6), System.Web.Caching.Cache.NoSlidingExpiration);
            return res;
        }

        /// <summary>
        /// Viaje sin movimiento: la posición guardada pasa a la hora y velocidad de la foto de la flota (el lugar y
        /// la dirección siguen siendo los mismos). Así el tablero no muestra "última señal" de una unidad parada.
        /// </summary>
        private static void RefrescarPosicion(ViajeResumen v, Dictionary<string, OnwayDevice> foto)
        {
            string activo = RutaTercero.Es(v.TipoRuta) ? RutaTercero.TractoActivo(v.Horas, v.Tracto1, v.Tracto2)
                                                       : StatusUnidad.TractoActivo(v.Horas, v.Tracto1, v.Tracto2);
            if (string.IsNullOrWhiteSpace(activo) || !foto.TryGetValue(activo.Trim(), out var d) || d.Expand?.LastLocation == null) return;
            var l = d.Expand.LastLocation;
            DateTime? horaUtc = l.GeneratedTime ?? l.MessageTime;
            if (!horaUtc.HasValue) return;
            DbHelper.EjecutarNonQuery(@"
                UPDATE SeguimientoExportacion
                SET fhUltimaPosicion = @hora, ultimaVelocidad = @velocidad, ultimaLatitud = @lat, ultimaLongitud = @lng,
                    ultimaPlacaGps = @placa, gpsAproximadoDe = NULL, fhUltimaConsultaGps = dbo.fn_AhoraPeru()
                WHERE idSeguimiento = @id AND (fhUltimaPosicion IS NULL OR fhUltimaPosicion < @hora)",
                DbHelper.Param("@id", v.Id), DbHelper.Param("@hora", FechaHelper.ConvertirDeUtc(DateTime.SpecifyKind(horaUtc.Value, DateTimeKind.Utc))),
                DbHelper.Param("@velocidad", Math.Round((decimal)l.Speed, 1)), DbHelper.Param("@lat", Math.Round((decimal)l.Latitude, 6)),
                DbHelper.Param("@lng", Math.Round((decimal)l.Longitude, 6)), DbHelper.Param("@placa", activo.Trim().ToUpperInvariant()));
        }

        /// <summary>Orden de "Actualizar GPS": cargas, descargas, retornos y al final los atrasados.</summary>
        public static IEnumerable<ViajeResumen> OrdenarParaGps(IEnumerable<ViajeResumen> viajes, DateTime hoy)
        {
            int Prioridad(ViajeResumen v)
            {
                if (v.FhProgramacion.HasValue && v.FhProgramacion.Value.Date < hoy.Date.AddDays(-DiasViajeAtrasado)) return 3;
                switch (v.Fase.Grupo)
                {
                    case "carga": return 0;
                    case "base":
                    case "ecuador": return 1;
                    default: return 2;
                }
            }
            return viajes
                .OrderBy(Prioridad)
                .ThenByDescending(v => Prioridad(v) == 0 ? v.FhProgramacion ?? DateTime.MinValue : DateTime.MinValue)
                .ThenBy(v => v.UltimaConsultaGps ?? DateTime.MinValue);
        }

        private static ViajeResumen Resumir(DataRow r) => ConVencido(ResumirFila(r), r);

        /// <summary>
        /// Vencido (<see cref="ViajeVencido"/>) y el cierre administrativo. El aviso del vencido lo agrega el tablero
        /// (<see cref="ListarTableroConTotal"/>), que también sabe si la carreta ya está en un viaje posterior.
        /// </summary>
        private static ViajeResumen ConVencido(ViajeResumen v, DataRow r)
        {
            DateTime hoy = FechaHelper.Hoy();
            DateTime? referencia = Fecha(r, "fhProgramacion") ?? Fecha(r, "fechaRegistro");
            v.DiasAbierto = ViajeVencido.DiasAbierto(referencia, hoy);
            v.Vencido = ViajeVencido.Es(v.Fase?.Grupo, referencia, hoy);
            v.MotivoCierre = Texto(r, "motivoCierre");
            v.FhCierreAdministrativo = Fecha(r, "fhCierreAdministrativo");
            return v;
        }

        private static ViajeResumen ResumirFila(DataRow r)
        {
            var horas = ColumnasHora.ToDictionary(c => c, c => Fecha(r, c), StringComparer.OrdinalIgnoreCase);
            string estado = Texto(r, "estado");
            string tracto1 = Texto(r, "tracto1"), tracto2 = Texto(r, "tracto2");
            string tipoRuta = Texto(r, "tipoRuta");
            if (RutaTercero.Es(tipoRuta)) return ConGrupo(ResumirTercero(r, horas, estado, tipoRuta, tracto1, tracto2), r);
            var cargaEstimada = CargaEstimada.Completar(horas);
            var ultimo = FaseViaje.UltimoHito(horas);
            return ConRetornoConCarga(ConGrupo(new ViajeResumen
            {
                Id = Convert.ToInt32(r["idSeguimiento"]),
                Pedido = Texto(r, "cliente"),
                Carreta = Texto(r, "carreta"),
                Tracto1 = tracto1,
                Chofer1 = Texto(r, "conductorOrigen"),
                Tracto2 = tracto2,
                Chofer2 = Texto(r, "conductorDestino"),
                Planta = Texto(r, "bodegaDescarga") ?? Texto(r, "bodegaEcuatoriana"),
                Estado = estado,
                Origen = Texto(r, "origen"),
                FhProgramacion = Fecha(r, "fhProgramacion"),
                UltimaConsultaGps = Fecha(r, "fhUltimaConsultaGps"),
                UltimaPosicion = Fecha(r, "fhUltimaPosicion"),
                Lugar = StatusUnidad.LugarDeDireccion(Texto(r, "ultimaDireccion")),
                Horas = horas,
                Fase = FaseViaje.Calcular(horas, estado),
                Status = StatusUnidad.Texto(horas, estado, Texto(r, "bodegaDescarga") ?? Texto(r, "bodegaEcuatoriana"),
                    StatusUnidad.LugarDeDireccion(Texto(r, "ultimaDireccion")), Fecha(r, "fhUltimaPosicion"), Fecha(r, "fhUltimaConsultaGps") ?? FechaHelper.Ahora()),
                Avisos = FaseViaje.Avisos(horas, tracto1, tracto2, r.Table.Columns.Contains("pasoDirectoTci") && !r.IsNull("pasoDirectoTci") && Convert.ToBoolean(r["pasoDirectoTci"]), FechaHelper.Ahora()),
                AvanceCarga = FaseViaje.Avance(horas, TramoViaje.Carga),
                AvanceDescarga = FaseViaje.Avance(horas, TramoViaje.Descarga),
                UltimoHito = ultimo?.Item1.Nombre,
                UltimoHitoHora = ultimo?.Item2,
                Velocidad = Numero(r, "ultimaVelocidad"),
                Latitud = Numero(r, "ultimaLatitud"),
                Longitud = Numero(r, "ultimaLongitud"),
                Llegada = LlegadaEstimada.ParaViaje(horas, estado, Texto(r, "bodegaDescarga") ?? Texto(r, "bodegaEcuatoriana"),
                    Texto(r, "ultimaPlacaGps"), tracto1, tracto2, Numero(r, "ultimaLatitud"), Numero(r, "ultimaLongitud"), Fecha(r, "fhUltimaPosicion")),
                Fila = r,
                HorasEstimadas = cargaEstimada,
            }, r), r);
        }

        /// <summary>
        /// La unidad vuelve de Ecuador con carga y descarga en Trujillo (sección IMPORTACION-TRUJILLO del Word):
        /// el viaje de ida lo muestra como "Retorno con carga" y lleva un aviso con el día programado.
        /// </summary>
        private static ViajeResumen ConRetornoConCarga(ViajeResumen v, DataRow r)
        {
            if (!r.Table.Columns.Contains("retornoConCarga") || r.IsNull("retornoConCarga") || !Convert.ToBoolean(r["retornoConCarga"]))
                return v;
            string destino = Texto(r, "retornoDestino") ?? "Trujillo";
            destino = char.ToUpperInvariant(destino[0]) + destino.Substring(1).ToLowerInvariant();
            var fh = Fecha(r, "fhRetornoProgramado");
            if (v.Fase.Grupo == "retorno")
                v.Fase = new FaseCalculada { Nombre = "Retorno con carga a " + destino, Grupo = "retorno" };
            if (v.Fase.Grupo != "cerrado")
                (v.Avisos = v.Avisos ?? new List<AvisoViaje>()).Add(new AvisoViaje
                {
                    Tipo = TipoAviso.Info,
                    Texto = $"Vuelve de Ecuador con carga y descarga en {destino}" + (fh.HasValue ? $" (programado {fh.Value:dd/MM HH:mm})." : "."),
                });
            return v;
        }

        /// <summary>Posición tomada de su grupo porque su GPS no tiene señal: se avisa en el tablero.</summary>
        private static ViajeResumen ConGrupo(ViajeResumen v, DataRow r)
        {
            v.GpsAproximadoDe = Texto(r, "gpsAproximadoDe");
            if (v.GpsAproximadoDe != null && v.Fase.Grupo != "cerrado")
                (v.Avisos = v.Avisos ?? new List<AvisoViaje>()).Add(new AvisoViaje
                {
                    Tipo = TipoAviso.Alerta,
                    Texto = $"GPS de {Texto(r, "ultimaPlacaGps")} sin señal: posición y horas aproximadas con su grupo (como {v.GpsAproximadoDe}).",
                });
            return v;
        }

        /// <summary>Viaje de un cliente tercero: sus hitos, fase, STATUS y avisos salen de <see cref="RutaTercero"/>.</summary>
        private static ViajeResumen ResumirTercero(DataRow r, Dictionary<string, DateTime?> horas, string estado, string tipoRuta,
            string tracto1, string tracto2)
        {
            string lugarCarga = Texto(r, "lugarCarga"), lugarEntrega = Texto(r, "lugarEntrega");
            string lugar = StatusUnidad.LugarDeDireccion(Texto(r, "ultimaDireccion"));
            var hitos = RutaTercero.Hitos(tipoRuta, lugarCarga, lugarEntrega);
            bool Hay(DefinicionHito d) => horas.TryGetValue(d.Columna, out var v) && v.HasValue;
            var ultimo = hitos.Where(Hay).Select(d => Tuple.Create(d, horas[d.Columna].Value)).OrderByDescending(t => t.Item2).FirstOrDefault();
            return new ViajeResumen
            {
                Id = Convert.ToInt32(r["idSeguimiento"]),
                Pedido = Texto(r, "cliente") ?? tipoRuta,
                Carreta = Texto(r, "carreta"),
                Tracto1 = tracto1,
                Chofer1 = Texto(r, "conductorOrigen"),
                Tracto2 = tracto2,
                Chofer2 = Texto(r, "conductorDestino"),
                Planta = lugarEntrega,
                Estado = estado,
                TipoRuta = tipoRuta,
                LugarCarga = lugarCarga,
                LugarEntrega = lugarEntrega,
                Hitos = hitos,
                Origen = Texto(r, "origen"),
                FhProgramacion = Fecha(r, "fhProgramacion"),
                UltimaConsultaGps = Fecha(r, "fhUltimaConsultaGps"),
                UltimaPosicion = Fecha(r, "fhUltimaPosicion"),
                Lugar = lugar,
                Horas = horas,
                Fase = RutaTercero.Fase(tipoRuta, horas, estado, lugarCarga, lugarEntrega),
                Status = RutaTercero.Status(tipoRuta, horas, estado, lugarCarga, lugarEntrega, Texto(r, "lugarFin"),
                    lugar, Fecha(r, "fhUltimaPosicion"), Fecha(r, "fhUltimaConsultaGps") ?? FechaHelper.Ahora()),
                Avisos = RutaTercero.Avisos(tipoRuta, horas, tracto1, lugarCarga, lugarEntrega, FechaHelper.Ahora()),
                AvanceCarga = hitos.Count(d => d.Tramo == TramoViaje.Carga && Hay(d)),
                AvanceDescarga = hitos.Count(d => d.Tramo == TramoViaje.Descarga && Hay(d)),
                UltimoHito = ultimo?.Item1.Nombre,
                UltimoHitoHora = ultimo?.Item2,
                Velocidad = Numero(r, "ultimaVelocidad"),
                Latitud = Numero(r, "ultimaLatitud"),
                Longitud = Numero(r, "ultimaLongitud"),
                Llegada = string.Equals(tipoRuta, "CRISAR", StringComparison.OrdinalIgnoreCase)
                    ? LlegadaEstimada.ParaRetornoCrisar(horas, estado, Texto(r, "ultimaPlacaGps"), tracto1, tracto2,
                        Numero(r, "ultimaLatitud"), Numero(r, "ultimaLongitud"), Fecha(r, "fhUltimaPosicion"))
                    : null,
                Fila = r,
            };
        }

        private static DateTime? Fecha(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && !r.IsNull(c) ? Convert.ToDateTime(r[c]) : (DateTime?)null;

        private static double? Numero(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && !r.IsNull(c) ? Convert.ToDouble(r[c]) : (double?)null;

        private static string Texto(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r.IsNull(c)) return null;
            string s = Convert.ToString(r[c]).Trim();
            return s.Length == 0 ? null : s;
        }

        private static string Mayus(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
    }
}
