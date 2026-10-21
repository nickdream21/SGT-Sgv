using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Services.GpsIntegracion;

namespace WebSGV.Services.Exportacion
{
    public sealed class FiltroTablero
    {
        public DateTime Desde { get; set; }
        public DateTime Hasta { get; set; }

        /// <summary>Pedido, carreta, placa o chofer. Con texto se busca en todas las fechas.</summary>
        public string Texto { get; set; }

        /// <summary>JAVE | INBALNOR | null (todas).</summary>
        public string Planta { get; set; }

        /// <summary>Incluir los viajes importados del Excel o del formulario viejo (origen NULL); por defecto solo despachos y manuales.</summary>
        public bool IncluirHistorial { get; set; }
    }

    /// <summary>Un viaje (pedido y carreta) en el tablero, con su fase y avisos ya calculados.</summary>
    public sealed class ViajeResumen
    {
        public int Id { get; set; }
        public string Pedido { get; set; }
        public string Carreta { get; set; }
        public string Tracto1 { get; set; }
        public string Chofer1 { get; set; }
        public string Tracto2 { get; set; }
        public string Chofer2 { get; set; }
        public string Planta { get; set; }
        public string Estado { get; set; }

        /// <summary>DESPACHO | MANUAL | null (importado del Excel o del formulario viejo).</summary>
        public string Origen { get; set; }
        public DateTime? FhProgramacion { get; set; }
        public DateTime? UltimaConsultaGps { get; set; }

        /// <summary>Texto de STATUS de la unidad (fase + lugar general), para copiar al Excel y enviar.</summary>
        public string Status { get; set; }
        public DateTime? UltimaPosicion { get; set; }
        public string Lugar { get; set; }

        /// <summary>"dd/MM HH:mm" de la última posición del GPS mientras el viaje sigue abierto; null si ya cerró o no hay.</summary>
        public string UbicacionHora => UltimaPosicion.HasValue && Fase != null && Fase.Grupo != "cerrado" ? UltimaPosicion.Value.ToString("dd/MM HH:mm") : null;
        public Dictionary<string, DateTime?> Horas { get; set; }
        public FaseCalculada Fase { get; set; }
        public List<AvisoViaje> Avisos { get; set; }
        public int AvanceCarga { get; set; }
        public int AvanceDescarga { get; set; }
        public string UltimoHito { get; set; }
        public DateTime? UltimoHitoHora { get; set; }

        /// <summary>La fila completa de SeguimientoExportacion (para exportar al STATUS GENERAL).</summary>
        public DataRow Fila { get; set; }
    }

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
        public List<string> Errores { get; } = new List<string>();
    }

    /// <summary>
    /// Datos del tablero y del detalle del seguimiento de exportación (SeguimientoExportacion +
    /// SeguimientoHito). Las escrituras van por los sp_SE_* y quedan en la auditoría.
    /// </summary>
    public static class SeguimientoViajesService
    {
        private const int MaximoFilasTablero = 500;

        private static readonly string[] ColumnasHora =
            HitosSeguimiento.Todos.Select(h => h.Columna)
                .Concat(new[] { "fhLlegadaAlmacen", "fhRegistro", "fhInicioCarga", "fhTerminoCarga",
                                "fhAutorizacionNacionalizacion", "fhInicioDescarga", "fhTerminoDescarga" })
                .Distinct().ToArray();

        // Paso directo por TCI: la regla del hito de llegada lo dice (TrazabilidadDescargaService).
        private const string SqlBase = @"
            SELECT se.*,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM SeguimientoHito h
                                          WHERE h.idSeguimiento = se.idSeguimiento AND h.columna = 'fhLlegadaTCI'
                                            AND h.regla LIKE '%paso directo%') THEN 1 ELSE 0 END AS BIT) AS pasoDirectoTci
            FROM SeguimientoExportacion se
            WHERE se.activo = 1";

        public static List<ViajeResumen> ListarTablero(FiltroTablero f)
        {
            string texto = string.IsNullOrWhiteSpace(f.Texto) ? null : f.Texto.Trim();
            string planta = string.IsNullOrWhiteSpace(f.Planta) ? null : f.Planta.Trim().ToUpperInvariant();
            DataTable dt = DbHelper.ConsultarTabla($@"
                SELECT TOP {MaximoFilasTablero} * FROM ({SqlBase}
                  AND (@texto IS NOT NULL OR COALESCE(se.fhProgramacion, se.fechaRegistro) >= @desde AND COALESCE(se.fhProgramacion, se.fechaRegistro) < @hasta)
                  AND (@texto IS NULL OR se.cliente LIKE @like OR se.carreta LIKE @like OR se.tracto1 LIKE @like OR se.tracto2 LIKE @like
                       OR se.conductorOrigen LIKE @like OR se.conductorDestino LIKE @like)
                  AND (@planta IS NULL OR UPPER(se.bodegaDescarga) = @planta OR UPPER(se.bodegaEcuatoriana) = @planta)
                  AND (@historial = 1 OR se.origen IN ('DESPACHO', 'MANUAL'))
                ) t
                ORDER BY COALESCE(t.fhProgramacion, t.fechaRegistro) DESC, t.idSeguimiento DESC",
                DbHelper.Param("@desde", f.Desde.Date),
                DbHelper.Param("@hasta", f.Hasta.Date.AddDays(1)),
                DbHelper.Param("@texto", texto),
                DbHelper.Param("@like", texto == null ? null : "%" + texto + "%"),
                DbHelper.Param("@planta", planta),
                DbHelper.Param("@historial", f.IncluirHistorial));
            return dt.Rows.Cast<DataRow>().Select(Resumir).ToList();
        }

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

        public static ViajeDetalle ObtenerDetalle(int idSeguimiento)
        {
            DataTable dt = DbHelper.ConsultarTabla(SqlBase + " AND se.idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento));
            if (dt.Rows.Count == 0) return null;
            DataRow fila = dt.Rows[0];

            var meta = DbHelper.ConsultarTabla("SELECT * FROM SeguimientoHito WHERE idSeguimiento = @id", DbHelper.Param("@id", idSeguimiento))
                .Rows.Cast<DataRow>()
                .ToDictionary(r => Convert.ToString(r["columna"]), r => r, StringComparer.OrdinalIgnoreCase);

            var hitos = HitosSeguimiento.Todos.Select(def =>
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

            return new ViajeDetalle { Resumen = Resumir(fila), Fila = fila, Hitos = hitos };
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

        public static void CambiarEstado(int idSeguimiento, string estado, int? idUsuario)
        {
            DbHelper.EjecutarNonQuerySp("sp_SE_CambiarEstado",
                DbHelper.Param("@idSeguimiento", idSeguimiento),
                DbHelper.Param("@estado", estado),
                DbHelper.Param("@idUsuario", idUsuario));
            AuditoriaHelper.Registrar(estado == "FINALIZADO" ? "FINALIZAR" : "UPDATE", "SeguimientoExportacion", idSeguimiento,
                "Estado del viaje: " + estado);
        }

        /// <summary>Un viaje programado hace más de esto y todavía abierto se consulta al final.</summary>
        private const int DiasViajeAtrasado = 10;

        /// <summary>
        /// Consulta el GPS de los viajes abiertos del tablero, hasta <paramref name="maximo"/> viajes
        /// o <paramref name="presupuesto"/> de tiempo. Primero las cargas en curso (las programadas
        /// más recientes antes), luego las descargas y los retornos; los viajes atrasados al final.
        /// El historial de hoy de todos los tractos se trae antes en paralelo.
        /// </summary>
        public static ResultadoActualizacionGps ActualizarGpsAbiertos(IEnumerable<ViajeResumen> viajes, int maximo, TimeSpan presupuesto)
        {
            var abiertos = OrdenarParaGps(viajes
                .Where(v => v.Fase.Grupo != "cerrado" && (!string.IsNullOrWhiteSpace(v.Tracto1) || !string.IsNullOrWhiteSpace(v.Tracto2))),
                FechaHelper.Hoy()).ToList();
            var r = new ResultadoActualizacionGps();
            var reloj = Stopwatch.StartNew();

            var tramos = abiertos.Take(maximo).SelectMany(v =>
            {
                v.Horas.TryGetValue("fhSalidaBase1", out var salidaBase);
                v.Horas.TryGetValue("fhSalidaPlanta", out var salidaPlanta);
                DateTime inicio = (salidaBase ?? v.FhProgramacion ?? FechaHelper.Hoy()).Date;
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
            }
            r.Pendientes = abiertos.Count - r.Consultados;
            return r;
        }

        /// <summary>Orden de "Actualizar GPS": cargas, descargas, retornos y al final los atrasados.</summary>
        public static IEnumerable<ViajeResumen> OrdenarParaGps(IEnumerable<ViajeResumen> viajes, DateTime hoy)
        {
            int Prioridad(ViajeResumen v)
            {
                if (v.FhProgramacion.HasValue && v.FhProgramacion.Value.Date < hoy.AddDays(-DiasViajeAtrasado)) return 3;
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

        private static ViajeResumen Resumir(DataRow r)
        {
            var horas = ColumnasHora.ToDictionary(c => c, c => Fecha(r, c), StringComparer.OrdinalIgnoreCase);
            string estado = Texto(r, "estado");
            string tracto1 = Texto(r, "tracto1"), tracto2 = Texto(r, "tracto2");
            var ultimo = FaseViaje.UltimoHito(horas);
            return new ViajeResumen
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
                    StatusUnidad.LugarDeDireccion(Texto(r, "ultimaDireccion")), Fecha(r, "fhUltimaPosicion"), FechaHelper.Ahora()),
                Avisos = FaseViaje.Avisos(horas, tracto1, tracto2, r.Table.Columns.Contains("pasoDirectoTci") && !r.IsNull("pasoDirectoTci") && Convert.ToBoolean(r["pasoDirectoTci"]), FechaHelper.Ahora()),
                AvanceCarga = FaseViaje.Avance(horas, TramoViaje.Carga),
                AvanceDescarga = FaseViaje.Avance(horas, TramoViaje.Descarga),
                UltimoHito = ultimo?.Item1.Nombre,
                UltimoHitoHora = ultimo?.Item2,
                Fila = r,
            };
        }

        private static DateTime? Fecha(DataRow r, string c) =>
            r.Table.Columns.Contains(c) && !r.IsNull(c) ? Convert.ToDateTime(r[c]) : (DateTime?)null;

        private static string Texto(DataRow r, string c)
        {
            if (!r.Table.Columns.Contains(c) || r.IsNull(c)) return null;
            string s = Convert.ToString(r[c]).Trim();
            return s.Length == 0 ? null : s;
        }

        private static string Mayus(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToUpperInvariant();
    }
}
