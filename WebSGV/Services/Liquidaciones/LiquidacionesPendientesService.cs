using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using WebSGV.Helpers;
using WebSGV.Models.Liquidaciones;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) de la revisión de liquidaciones
    /// (<c>LiquidacionesPendientes.aspx</c>). Extraído del code-behind: la validación
    /// de sesión, el saneamiento, el mapeo a DTO/controles, la auditoría, los PDFs y
    /// las firmas permanecen en el code-behind; este servicio solo ejecuta el SQL.
    /// No se modifica ninguna consulta ni stored procedure.
    ///
    /// La transacción de revisión (EnviarAConformidad: ajustes con motivo + dinero entregado +
    /// estado POR_CONFORMAR) y el armador de DTO ObtenerDetalleLiquidacion también viven aquí;
    /// los WebMethods del code-behind conservan sesión, validación, el objeto de respuesta y la
    /// auditoría. Las firmas y sus PDF los registra <c>FirmaService</c>: declaración del conductor
    /// → revisión de la administración → conformidad del conductor (recién ahí queda APROBADO).
    /// </summary>
    public static class LiquidacionesPendientesService
    {
        /// <summary>Número de orden a partir del id (escalar, puede ser null/DBNull).</summary>
        public static object ObtenerNumeroOrden(int idOrdenViaje) =>
            DbHelper.EjecutarEscalar(
                "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @idOrdenViaje",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje));

        /// <summary>
        /// Verifica ownership para endpoints consumidos por el conductor. La autorización
        /// se valida aquí y no sólo en la página que muestra el botón, evitando IDOR por
        /// invocación directa del PageMethod.
        /// </summary>
        public static bool OrdenPerteneceAUsuarioConductor(int idOrdenViaje, int idUsuario)
        {
            object resultado = DbHelper.EjecutarEscalar(@"
                SELECT COUNT(1)
                FROM OrdenViaje ov
                INNER JOIN Usuarios u ON u.idConductor = ov.idConductor
                WHERE ov.idOrdenViaje = @idOrdenViaje
                  AND u.idUsuario = @idUsuario
                  AND u.activo = 1",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje),
                DbHelper.Param("@idUsuario", idUsuario));

            return resultado != null && resultado != DBNull.Value && Convert.ToInt32(resultado) > 0;
        }

        /// <summary>Liquidaciones pendientes vía <c>sp_ObtenerLiquidacionesPendientes</c>.</summary>
        public static DataTable ObtenerPendientes(int? idConductor, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var pars = new List<SqlParameter>();
            if (idConductor.HasValue) pars.Add(DbHelper.Param("@idConductor", idConductor.Value));
            if (fechaDesde.HasValue) pars.Add(DbHelper.Param("@fechaDesde", fechaDesde.Value));
            if (fechaHasta.HasValue) pars.Add(DbHelper.Param("@fechaHasta", fechaHasta.Value));

            return DbHelper.ConsultarTablaSp("sp_ObtenerLiquidacionesPendientes", pars.ToArray());
        }

        /// <summary>Rechaza la liquidación vía <c>sp_RechazarLiquidacion</c>.</summary>
        public static DataTable Rechazar(string numeroOrdenViaje, int idUsuarioAprobacion, object observaciones) =>
            DbHelper.ConsultarTablaSp("sp_RechazarLiquidacion",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje),
                DbHelper.Param("@idUsuarioAprobacion", idUsuarioAprobacion),
                DbHelper.Param("@observaciones", observaciones));

        /// <summary>Autocompletado de conductores activos por nombre (TOP 15).</summary>
        public static DataTable BuscarConductores(string term) =>
            DbHelper.ConsultarTabla(@"
                        SELECT TOP 15
                            c.idConductor,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreCompleto
                        FROM Conductor c
                        INNER JOIN Usuarios u ON c.idConductor = u.idConductor
                        WHERE c.activo = 1
                          AND (c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '')) LIKE @term
                        ORDER BY nombreCompleto",
                DbHelper.Param("@term", "%" + term.Trim() + "%"));

        /// <summary>Columnas del resumen de una liquidación (bandejas de aprobadas y por conformar).</summary>
        private const string SelectResumenLiquidacion = @"
                        SELECT
                            ov.idOrdenViaje,
                            ov.numeroOrdenViaje,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS NombreConductor,
                            t.placaTracto,
                            ca.placaCarreta,
                            ov.fechaSalida,
                            ov.fechaLlegada,
                            ov.fechaEnvioConformidad,
                            ov.fechaConformidad,
                            ISNULL(dr.descuentoSoles, 0) AS DescuentoSoles,
                            ISNULL(dr.descuentoDolares, 0) AS DescuentoDolares,
                            ISNULL(dr.reintegroSoles, 0) AS ReintegroSoles,
                            ISNULL(dr.reintegroDolares, 0) AS ReintegroDolares,
                            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresosSoles,
                            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresosDolares,
                            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + ISNULL(e.movilidadSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) + ISNULL(e.hospedajeSoles, 0) + ISNULL(e.combustibleSoles, 0) AS GastosSoles,
                            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + ISNULL(e.movilidadDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) + ISNULL(e.hospedajeDolares, 0) + ISNULL(e.combustibleDolares, 0) AS GastosDolares,
                            ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdSoles,
                            ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosAdDolares,
                            ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdSoles,
                            ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosAdDolares
                        FROM OrdenViaje ov
                        INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                        INNER JOIN Tracto t ON ov.idTracto = t.idTracto
                        INNER JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                        LEFT JOIN DescuentosReintegros dr ON dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1
                        LEFT JOIN Ingresos i ON i.numeroOrdenViaje = ov.numeroOrdenViaje
                        LEFT JOIN Egresos e ON e.numeroOrdenViaje = ov.numeroOrdenViaje
                        ";

        /// <summary>
        /// Liquidaciones aprobadas (viajes COMPLETADO) con filtros opcionales. El SQL se
        /// arma dinámicamente igual que en el code-behind original; el cálculo de balance
        /// y el mapeo se hacen fuera.
        /// </summary>
        public static DataTable ObtenerAprobadas(int idConductor, string fechaDesde, string fechaHasta,
            string numeroOrden, DateTime? fd, DateTime? fh)
        {
            string query = SelectResumenLiquidacion + @"
                        WHERE ov.estadoViaje = 'COMPLETADO'";

            if (idConductor > 0)
                query += " AND ov.idConductor = @idConductor";
            if (!string.IsNullOrEmpty(fechaDesde))
                query += " AND ov.fechaSalida >= @fechaDesde";
            if (!string.IsNullOrEmpty(fechaHasta))
                query += " AND ov.fechaSalida <= @fechaHasta";
            if (!string.IsNullOrEmpty(numeroOrden))
                query += " AND ov.numeroOrdenViaje LIKE '%' + @numeroOrden + '%'";

            query += " ORDER BY ov.fechaSalida DESC";

            var pars = new List<SqlParameter>();
            if (idConductor > 0)
                pars.Add(DbHelper.Param("@idConductor", idConductor));
            if (fd.HasValue)
                pars.Add(DbHelper.Param("@fechaDesde", fd.Value));
            if (fh.HasValue)
                pars.Add(DbHelper.Param("@fechaHasta", fh.Value));
            if (!string.IsNullOrEmpty(numeroOrden))
                pars.Add(DbHelper.Param("@numeroOrden", numeroOrden));

            return DbHelper.ConsultarTabla(query, pars.ToArray());
        }

        /// <summary>Datos de la orden pendiente (número y firma) para el flujo de rechazo.</summary>
        public static DataTable ObtenerOrdenParaRechazo(int idOrdenViaje) =>
            DbHelper.ConsultarTabla(
                "SELECT numeroOrdenViaje, idFirmaConductor FROM OrdenViaje WHERE idOrdenViaje = @id AND estadoAprobacion = 'PENDIENTE'",
                DbHelper.Param("@id", idOrdenViaje));

        /// <summary>Marca la orden como RECHAZADO (habilita re-envío). Filas afectadas.</summary>
        public static int MarcarRechazada(int idOrdenViaje, string motivo) =>
            DbHelper.EjecutarNonQuery(@"
                        UPDATE OrdenViaje
                        SET estadoAprobacion = 'RECHAZADO',
                            observaciones = ISNULL(observaciones, '') + CHAR(13) + CHAR(10) +
                                '[RECHAZO ' + CONVERT(varchar, dbo.fn_AhoraPeru(), 120) + '] ' + @motivo
                        WHERE idOrdenViaje = @id AND estadoAprobacion = 'PENDIENTE'",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@motivo", motivo));

        /// <summary>
        /// Salida/llegada actuales de una orden aún PENDIENTE de aprobación (para que la
        /// administradora corrija la salida). Devuelve fila vacía si ya fue aprobada/rechazada.
        /// </summary>
        public static DataTable ObtenerSalidaLlegadaPendiente(int idOrdenViaje) =>
            DbHelper.ConsultarTabla(
                @"SELECT numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada
                  FROM OrdenViaje
                  WHERE idOrdenViaje = @id AND estadoAprobacion = 'PENDIENTE'",
                DbHelper.Param("@id", idOrdenViaje));

        /// <summary>
        /// Corrige la fecha/hora de salida de una orden PENDIENTE (solo administradora, auditado
        /// en el code-behind). Devuelve las filas afectadas (0 si ya no está pendiente).
        /// </summary>
        public static int CorregirSalidaPendiente(int idOrdenViaje, DateTime fechaSalida, TimeSpan horaSalida) =>
            DbHelper.EjecutarNonQuery(@"
                        UPDATE OrdenViaje
                        SET fechaSalida = @fechaSalida,
                            horaSalida  = @horaSalida
                        WHERE idOrdenViaje = @id AND estadoAprobacion = 'PENDIENTE'",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@fechaSalida", fechaSalida.Date),
                DbHelper.Param("@horaSalida", horaSalida));

        /// <summary>Resultado de <see cref="EnviarAConformidad"/> (sin presentación).</summary>
        public class ResultadoEnvioConformidad
        {
            /// <summary><c>true</c> si la transacción se confirmó (commit).</summary>
            public bool Exito { get; set; }
            /// <summary>Mensaje de error de negocio cuando <see cref="Exito"/> es <c>false</c>.</summary>
            public string Mensaje { get; set; }
            /// <summary>Número de orden de viaje resuelto (disponible en éxito).</summary>
            public string NumeroOrdenViaje { get; set; }
        }

        /// <summary>
        /// Cierra la revisión de la administración en una sola transacción: guarda los ajustes con
        /// su motivo en <c>DescuentosReintegros</c>, el dinero entregado al conductor y deja la orden
        /// POR_CONFORMAR (<c>sp_LD_EnviarLiquidacionAConformidad</c>). El viaje no se completa aquí:
        /// lo completa la conformidad firmada del conductor. La firma de revisión y su PDF los
        /// registra después <c>FirmaService.RegistrarRevisionAdmin</c>.
        /// </summary>
        public static ResultadoEnvioConformidad EnviarAConformidad(
            int idOrdenViaje, AjustesLiquidacion ajustes, string notaAprobacion, int idUsuario,
            IngresosEntregados ingresos = null, IEnumerable<int> idsMovimientos = null)
        {
            ajustes = ajustes ?? new AjustesLiquidacion();
            string error = LiquidacionCalculos.ValidarMontosAjuste(ajustes.DescuentoSoles, ajustes.DescuentoDolares,
                ajustes.ReintegroSoles, ajustes.ReintegroDolares)
                ?? ReglasFirma.ValidarMotivosAjuste(ajustes.DescuentoSoles, ajustes.DescuentoDolares, ajustes.MotivoDescuento,
                    ajustes.ReintegroSoles, ajustes.ReintegroDolares, ajustes.MotivoReintegro);
            if (error != null)
                return new ResultadoEnvioConformidad { Exito = false, Mensaje = error };

            string numeroOrdenViaje = null;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction())
                {
                    try
                    {
                        // 1. La orden debe estar pendiente, con la declaración firmada y sin revisión vigente.
                        object result = DbHelper.EjecutarEscalar(conn, tran, @"
                            SELECT numeroOrdenViaje
                            FROM OrdenViaje WITH (UPDLOCK)
                            WHERE idOrdenViaje = @id
                              AND estadoAprobacion = 'PENDIENTE'
                              AND idFirmaConductor IS NOT NULL",
                            DbHelper.Param("@id", idOrdenViaje));
                        if (result == null || result == DBNull.Value)
                            return new ResultadoEnvioConformidad { Exito = false, Mensaje = "La liquidación no está pendiente o aún no tiene firma del conductor." };
                        numeroOrdenViaje = result.ToString();

                        // 2. Ajustes con su motivo. Sin ajustes se desactiva la fila que hubiera de una
                        //    revisión anterior (si no, un descuento ya retirado seguiría sumando).
                        bool hayAjustes = ajustes.HayDescuento || ajustes.HayReintegro;
                        DbHelper.EjecutarNonQuery(conn, tran, @"
                            IF EXISTS (SELECT 1 FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden)
                                UPDATE DescuentosReintegros
                                SET descuentoSoles = @descS, descuentoDolares = @descD,
                                    reintegroSoles = @reintS, reintegroDolares = @reintD,
                                    observacionesDescuento = @motivoDesc, observacionesReintegro = @motivoReint,
                                    activo = @activo
                                WHERE numeroOrdenViaje = @numeroOrden
                            ELSE IF @activo = 1
                                INSERT INTO DescuentosReintegros
                                    (numeroOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares,
                                     observacionesDescuento, observacionesReintegro, fechaCreacion, activo)
                                VALUES (@numeroOrden, @descS, @descD, @reintS, @reintD,
                                        @motivoDesc, @motivoReint, dbo.fn_AhoraPeru(), 1)",
                            DbHelper.Param("@numeroOrden", numeroOrdenViaje),
                            DbHelper.Param("@descS", ajustes.DescuentoSoles),
                            DbHelper.Param("@descD", ajustes.DescuentoDolares),
                            DbHelper.Param("@reintS", ajustes.ReintegroSoles),
                            DbHelper.Param("@reintD", ajustes.ReintegroDolares),
                            DbHelper.Param("@motivoDesc", ajustes.HayDescuento ? (object)ajustes.MotivoDescuento.Trim() : DBNull.Value),
                            DbHelper.Param("@motivoReint", ajustes.HayReintegro ? (object)ajustes.MotivoReintegro.Trim() : DBNull.Value),
                            DbHelper.Param("@activo", hayAjustes));

                        // 3. Dinero entregado al conductor (lo registra la administración, no el conductor).
                        //    null = la pantalla no lo envió y se respeta lo que ya hubiera.
                        if (ingresos != null)
                        {
                            DbHelper.EjecutarNonQuerySp(conn, tran, "sp_LD_RegistrarIngresosEntregados",
                                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje),
                                DbHelper.Param("@despachoSoles", ingresos.DespachoSoles),
                                DbHelper.Param("@despachoDolares", ingresos.DespachoDolares),
                                DbHelper.Param("@prestamoSoles", ingresos.PrestamoSoles),
                                DbHelper.Param("@prestamoDolares", ingresos.PrestamoDolares),
                                DbHelper.Param("@mensualidadSoles", ingresos.MensualidadSoles),
                                DbHelper.Param("@mensualidadDolares", ingresos.MensualidadDolares),
                                DbHelper.Param("@otrosSoles", ingresos.OtrosSoles),
                                DbHelper.Param("@otrosDolares", ingresos.OtrosDolares),
                                DbHelper.Param("@descDespacho", (object)ingresos.DescDespacho ?? DBNull.Value),
                                DbHelper.Param("@descMensualidad", (object)ingresos.DescMensualidad ?? DBNull.Value),
                                DbHelper.Param("@descOtros", (object)ingresos.DescOtros ?? DBNull.Value),
                                DbHelper.Param("@descPrestamo", (object)ingresos.DescPrestamo ?? DBNull.Value));
                        }

                        // 3b. Entregas y pagos directos elegidos en la revisión (null = la pantalla no los envió).
                        if (idsMovimientos != null)
                            MovimientosViajeService.UnirAOrden(conn, tran, numeroOrdenViaje, idsMovimientos);

                        // 4. Estado POR_CONFORMAR.
                        DataTable dt = DbHelper.ConsultarTablaSp(conn, tran, "sp_LD_EnviarLiquidacionAConformidad",
                            DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje),
                            DbHelper.Param("@idUsuarioAprobacion", idUsuario),
                            DbHelper.Param("@observaciones",
                                string.IsNullOrWhiteSpace(notaAprobacion) ? (object)DBNull.Value : notaAprobacion.Trim()));
                        if (dt.Rows.Count == 0 || Convert.ToInt32(dt.Rows[0]["Resultado"]) != 1)
                        {
                            tran.Rollback();
                            return new ResultadoEnvioConformidad
                            {
                                Exito = false,
                                Mensaje = dt.Rows.Count > 0 ? dt.Rows[0]["Mensaje"].ToString() : "No se pudo enviar la liquidación al conductor."
                            };
                        }

                        tran.Commit();
                    }
                    catch
                    {
                        tran.Rollback();
                        throw;
                    }
                }
            }

            return new ResultadoEnvioConformidad { Exito = true, NumeroOrdenViaje = numeroOrdenViaje };
        }

        /// <summary>
        /// Liquidaciones revisadas por la administración que esperan la conformidad del conductor
        /// (mismas columnas que <see cref="ObtenerAprobadas"/> más la fecha de envío).
        /// </summary>
        public static DataTable ObtenerPorConformar(int idConductor) =>
            DbHelper.ConsultarTabla(SelectResumenLiquidacion + @"
                        WHERE ov.estadoAprobacion = 'POR_CONFORMAR'
                          AND (@idConductor = 0 OR ov.idConductor = @idConductor)
                        ORDER BY ov.fechaEnvioConformidad",
                DbHelper.Param("@idConductor", idConductor));

        public const string MensajeOrdenFirmadaNoEditable =
            "Esta liquidación tiene firmas electrónicas: el conductor firmó sus gastos y no se pueden modificar sin su firma. " +
            "Para corregirla, recházala (el conductor la corrige y la vuelve a firmar) o registra un descuento o reintegro con su motivo al aprobarla.";

        /// <summary>
        /// La orden tiene alguna firma (declaración, revisión o conformidad): sus montos no se
        /// editan a mano desde AgregarOrdenViaje, porque cambiarían lo que alguien ya firmó.
        /// </summary>
        public static bool OrdenTieneFirmas(int idOrdenViaje)
        {
            object r = DbHelper.EjecutarEscalar(@"
                SELECT COUNT(1) FROM OrdenViaje
                WHERE idOrdenViaje = @id
                  AND (idFirmaConductor IS NOT NULL OR idFirmaAdmin IS NOT NULL OR idFirmaConformidad IS NOT NULL)",
                DbHelper.Param("@id", idOrdenViaje));
            return r != null && r != DBNull.Value && Convert.ToInt32(r) > 0;
        }

        /// <summary>"DNI 12345678" o "CE 001234567" (vacío si no tiene ninguno registrado).</summary>
        internal static string DocumentoIdentidad(object dni, object carnet)
        {
            string d = dni == null || dni == DBNull.Value ? "" : dni.ToString().Trim();
            if (d.Length > 0) return "DNI " + d;
            string c = carnet == null || carnet == DBNull.Value ? "" : carnet.ToString().Trim();
            return c.Length > 0 ? "CE " + c : "";
        }

        /// <summary>Ruta relativa del PDF archivado de la orden (o <c>null</c> si no hay).</summary>
        public static string ObtenerRutaPdfArchivado(int idOrdenViaje)
        {
            object r = DbHelper.EjecutarEscalar(
                "SELECT rutaPdfFirmado FROM OrdenViaje WHERE idOrdenViaje = @id",
                DbHelper.Param("@id", idOrdenViaje));
            return r == null || r == DBNull.Value ? null : r.ToString();
        }

        /// <summary>Persiste la ruta relativa y el hash del PDF archivado en <c>OrdenViaje</c>.</summary>
        public static void GuardarRutaPdfArchivado(int idOrdenViaje, string rutaRelativa, string hash) =>
            DbHelper.EjecutarNonQuery(@"
                UPDATE OrdenViaje
                   SET rutaPdfFirmado = @ruta,
                       hashPdfFirmado = @hash
                 WHERE idOrdenViaje = @id",
                DbHelper.Param("@ruta", rutaRelativa),
                DbHelper.Param("@hash", hash),
                DbHelper.Param("@id", idOrdenViaje));

        /// <summary>
        /// Arma el DTO completo del detalle de una liquidación (cabecera + ingresos/egresos
        /// principales y desglosados + ítems detallados por categoría + adicionales +
        /// descuentos/reintegros), leyendo las distintas tablas sobre una sola conexión.
        /// SQL movido verbatim del code-behind; devuelve <c>null</c> si la orden no existe.
        /// La validación de sesión queda en el WebMethod del code-behind.
        /// </summary>
        public static DetalleLiquidacion ObtenerDetalleLiquidacion(int idOrdenViaje)
        {
            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                // ✅ CONSULTA CORREGIDA: Incluye todas las tablas
                string query = @"
                SELECT
                    -- Orden
                    ov.numeroOrdenViaje,
                    ov.fechaSalida,
                    ov.horaSalida,
                    ov.fechaLlegada,
                    ov.horaLlegada,
                    ov.horaLlegadaDeclarada,
                    ov.horaLlegadaGps,
                    ov.observaciones,
                    ov.estadoAprobacion,
                    ov.observacionesAprobacion,
                    ov.observacionConductor,
                    ov.fechaObservacionConductor,

                    -- Conductor y Vehículos
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
                    c.DNI AS dniConductor,
                    c.carnetExtranjeria AS carnetConductor,
                    c.numeroLicencia AS licenciaConductor,
                    t.placaTracto AS placaTracto,
                    ca.placaCarreta AS placaCarreta,

                    -- Ingresos principales
                    ISNULL(i.despachoSoles, 0) AS despachoSoles,
                    ISNULL(i.despachoDolares, 0) AS despachoDolares,
                    ISNULL(i.prestamoSoles, 0) AS prestamoSoles,
                    ISNULL(i.prestamosDolares, 0) AS prestamoDolares,
                    ISNULL(i.mensualidadSoles, 0) AS mensualidadSoles,
                    ISNULL(i.mensualidadDolares, 0) AS mensualidadDolares,
                    ISNULL(i.otrosSoles, 0) AS otrosSoles,
                    ISNULL(i.otrosDolares, 0) AS otrosDolares,

                    -- Gastos principales
                    ISNULL(e.peajesSoles, 0) AS gastosPeajesSoles,
                    ISNULL(e.peajesDolares, 0) AS gastosPeajesDolares,
                    ISNULL(e.alimentacionSoles, 0) AS gastosAlimentacionSoles,
                    ISNULL(e.alimentacionDolares, 0) AS gastosAlimentacionDolares,
                    ISNULL(e.apoyoseguridadSoles, 0) AS gastosApoyoSeguridadSoles,
                    ISNULL(e.apoyoseguridadDolares, 0) AS gastosApoyoSeguridadDolares,
                    ISNULL(e.reparacionesVariosSoles, 0) AS gastosReparacionesSoles,
                    ISNULL(e.repacionesVariosDolares, 0) AS gastosReparacionesDolares,
                    ISNULL(e.movilidadSoles, 0) AS gastosMovilidadSoles,
                    ISNULL(e.movilidadDolares, 0) AS gastosMovilidadDolares,
                    ISNULL(e.encarpada_desencarpadaSoles, 0) AS gastosEncarpadaSoles,
                    ISNULL(e.encarpada_desencarpadaDolares, 0) AS gastosEncarpadaDolares,
                    ISNULL(e.hospedajeSoles, 0) AS gastosHospedajeSoles,
                    ISNULL(e.hospedajeDolares, 0) AS gastosHospedajeDolares,
                    ISNULL(e.combustibleSoles, 0) AS gastosCombustibleSoles,
                    ISNULL(e.combustibleDolares, 0) AS gastosCombustibleDolares,

                    -- Descripciones Egresos
                    ISNULL(e.descPeajes, '') AS descPeajes,
                    ISNULL(e.descAlimentacion, '') AS descAlimentacion,
                    ISNULL(e.descApoyoSeguridad, '') AS descApoyoSeguridad,
                    ISNULL(e.descReparacionesVarios, '') AS descReparaciones,
                    ISNULL(e.descMovilidad, '') AS descMovilidad,
                    ISNULL(e.descEncarpadaDesencarpada, '') AS descEncarpada,
                    ISNULL(e.descHospedaje, '') AS descHospedaje,
                    ISNULL(e.descCombustible, '') AS descCombustible,

                    -- Descripciones Ingresos
                    ISNULL(i.descDespacho, '') AS descDespacho,
                    ISNULL(i.descPrestamo, '') AS descPrestamo,
                    ISNULL(i.descMensualidad, '') AS descMensualidad,
                    ISNULL(i.descOtrosAutorizados, '') AS descOtros

                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                INNER JOIN Tracto t ON ov.idTracto = t.idTracto
                INNER JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.idOrdenViaje = @idOrdenViaje";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@idOrdenViaje", idOrdenViaje);
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            // ✅ Calcular ingresos (principales + adicionales)
                            decimal ingresosPrincipalesSoles =
                                Convert.ToDecimal(reader["despachoSoles"]) +
                                Convert.ToDecimal(reader["prestamoSoles"]) +
                                Convert.ToDecimal(reader["mensualidadSoles"]) +
                                Convert.ToDecimal(reader["otrosSoles"]);

                            decimal ingresosPrincipalesDolares =
                                Convert.ToDecimal(reader["despachoDolares"]) +
                                Convert.ToDecimal(reader["prestamoDolares"]) +
                                Convert.ToDecimal(reader["mensualidadDolares"]) +
                                Convert.ToDecimal(reader["otrosDolares"]);

                            DetalleLiquidacion detalle = new DetalleLiquidacion
                            {
                                IdOrdenViaje = idOrdenViaje,
                                NumeroOrdenViaje = reader["numeroOrdenViaje"].ToString(),
                                NombreConductor = reader["nombreConductor"].ToString(),
                                PlacaTracto = reader["placaTracto"].ToString(),
                                PlacaCarreta = reader["placaCarreta"].ToString(),
                                FechaSalida  = reader["fechaSalida"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["fechaSalida"]).ToString("dd/MM/yyyy") : "—",
                                FechaSalidaISO = reader["fechaSalida"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["fechaSalida"]).ToString("yyyy-MM-dd") : "",
                                HoraSalida = reader["horaSalida"] != DBNull.Value
                                    ? ((TimeSpan)reader["horaSalida"]).ToString(@"hh\:mm") : "",
                                HoraLlegadaSistema = reader["horaLlegada"] != DBNull.Value
                                    ? ((TimeSpan)reader["horaLlegada"]).ToString(@"hh\:mm") : "",
                                HoraLlegadaDeclarada = reader["horaLlegadaDeclarada"] != DBNull.Value
                                    ? ((TimeSpan)reader["horaLlegadaDeclarada"]).ToString(@"hh\:mm") : "",
                                HoraLlegadaGps = reader["horaLlegadaGps"] != DBNull.Value
                                    ? ((TimeSpan)reader["horaLlegadaGps"]).ToString(@"hh\:mm") : "",
                                EstadoAprobacion = reader["estadoAprobacion"]?.ToString() ?? "",
                                FechaLlegada = reader["fechaLlegada"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["fechaLlegada"]).ToString("dd/MM/yyyy") : "—",
                                Observaciones = reader["observaciones"]?.ToString() ?? "",
                                NotaAprobacion = reader["observacionesAprobacion"] != DBNull.Value
                                    ? reader["observacionesAprobacion"].ToString() : "",
                                ObservacionConductor = reader["observacionConductor"] != DBNull.Value
                                    ? reader["observacionConductor"].ToString() : "",
                                FechaObservacionConductor = reader["fechaObservacionConductor"] != DBNull.Value
                                    ? Convert.ToDateTime(reader["fechaObservacionConductor"]).ToString("dd/MM/yyyy HH:mm") : "",
                                DocumentoConductor = DocumentoIdentidad(reader["dniConductor"], reader["carnetConductor"]),
                                LicenciaConductor = reader["licenciaConductor"] != DBNull.Value
                                    ? reader["licenciaConductor"].ToString().Trim() : "",

                                // Ingresos (aún falta sumar adicionales)
                                TotalIngresosSoles = ingresosPrincipalesSoles,
                                TotalIngresosDolares = ingresosPrincipalesDolares,

                                // Gastos desglosados
                                GastosPeajesSoles = Convert.ToDecimal(reader["gastosPeajesSoles"]),
                                GastosPeajesDolares = Convert.ToDecimal(reader["gastosPeajesDolares"]),
                                GastosAlimentacionSoles = Convert.ToDecimal(reader["gastosAlimentacionSoles"]),
                                GastosAlimentacionDolares = Convert.ToDecimal(reader["gastosAlimentacionDolares"]),
                                GastosApoyoSeguridadSoles = Convert.ToDecimal(reader["gastosApoyoSeguridadSoles"]),
                                GastosApoyoSeguridadDolares = Convert.ToDecimal(reader["gastosApoyoSeguridadDolares"]),
                                GastosReparacionesSoles = Convert.ToDecimal(reader["gastosReparacionesSoles"]),
                                GastosReparacionesDolares = Convert.ToDecimal(reader["gastosReparacionesDolares"]),
                                GastosMovilidadSoles = Convert.ToDecimal(reader["gastosMovilidadSoles"]),
                                GastosMovilidadDolares = Convert.ToDecimal(reader["gastosMovilidadDolares"]),
                                GastosEncarpadaSoles = Convert.ToDecimal(reader["gastosEncarpadaSoles"]),
                                GastosEncarpadaDolares = Convert.ToDecimal(reader["gastosEncarpadaDolares"]),
                                GastosHospedajeSoles = Convert.ToDecimal(reader["gastosHospedajeSoles"]),
                                GastosHospedajeDolares = Convert.ToDecimal(reader["gastosHospedajeDolares"]),
                                GastosCombustibleSoles = Convert.ToDecimal(reader["gastosCombustibleSoles"]),
                                GastosCombustibleDolares = Convert.ToDecimal(reader["gastosCombustibleDolares"]),

                                // Ingresos desglosados
                                DespachoSoles = Convert.ToDecimal(reader["despachoSoles"]),
                                DespachoDolares = Convert.ToDecimal(reader["despachoDolares"]),
                                PrestamoSoles = Convert.ToDecimal(reader["prestamoSoles"]),
                                PrestamoDolares = Convert.ToDecimal(reader["prestamoDolares"]),
                                MensualidadSoles = Convert.ToDecimal(reader["mensualidadSoles"]),
                                MensualidadDolares = Convert.ToDecimal(reader["mensualidadDolares"]),
                                OtrosIngresosSoles = Convert.ToDecimal(reader["otrosSoles"]),
                                OtrosIngresosDolares = Convert.ToDecimal(reader["otrosDolares"]),

                                DescPeajes = reader["descPeajes"].ToString(),
                                DescAlimentacion = reader["descAlimentacion"].ToString(),
                                DescApoyoSeguridad = reader["descApoyoSeguridad"].ToString(),
                                DescReparaciones = reader["descReparaciones"].ToString(),
                                DescMovilidad = reader["descMovilidad"].ToString(),
                                DescEncarpada = reader["descEncarpada"].ToString(),
                                DescHospedaje = reader["descHospedaje"].ToString(),
                                DescCombustible = reader["descCombustible"].ToString(),
                                DescDespacho = reader["descDespacho"].ToString(),
                                DescPrestamo = reader["descPrestamo"].ToString(),
                                DescMensualidad = reader["descMensualidad"].ToString(),
                                DescOtros = reader["descOtros"].ToString(),

                                DetallesPeajes = new List<DetallePeajeItem>(),
                                DetallesReparaciones = new List<DetalleGenericoItem>(),
                                DetallesHospedaje = new List<DetalleGenericoItem>(),
                                DetallesCombustible = new List<DetalleGenericoItem>(),
                                DetallesIngresosAdicionales = new List<ItemAdicional>(),
                                DetallesGastosAdicionales = new List<ItemAdicional>()
                            };

                            reader.Close(); // ✅ Cerrar reader antes de nuevas consultas

                            // Un solo viaje a la BD para todos los detalles (varios SELECT, un resultado
                            // por cada uno): antes eran 6 consultas seguidas y, con la BD remota, cada
                            // una sumaba ~100 ms. El orden de los resultados es el de los SELECT.
                            decimal gastosAdicionalesSoles = 0;
                            decimal gastosAdicionalesDolares = 0;

                            const string sqlDetalles =
                                "SELECT nombreCategoria, soles, dolares, descripcion FROM IngresosAdicionales WHERE numeroOrdenViaje = @n;" +
                                "SELECT nombreCategoria, soles, dolares, descripcion FROM CategoriasAdicionales WHERE numeroOrdenViaje = @n;" +
                                "SELECT estacion, CONVERT(varchar,fecha,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetallePeajes WHERE numeroOrdenViaje = @n ORDER BY fecha;" +
                                "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante;" +
                                "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleHospedaje WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante;" +
                                "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleCombustible WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante;" +
                                "SELECT TOP 1 descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares, observacionesDescuento, observacionesReintegro FROM DescuentosReintegros WHERE numeroOrdenViaje = @n AND activo = 1;" +
                                MovimientosViajeService.ConsultaDeOrden + ";";

                            using (SqlCommand cmdDetalles = new SqlCommand(sqlDetalles, conn))
                            {
                                cmdDetalles.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                using (SqlDataReader rd = cmdDetalles.ExecuteReader())
                                {
                                    // 1) INGRESOS ADICIONALES (detalle + total)
                                    while (rd.Read())
                                    {
                                        decimal s = Convert.ToDecimal(rd["soles"]);
                                        decimal d = Convert.ToDecimal(rd["dolares"]);
                                        detalle.IngresosAdicionalesSoles += s;
                                        detalle.IngresosAdicionalesDolares += d;
                                        detalle.TotalIngresosSoles += s;
                                        detalle.TotalIngresosDolares += d;
                                        detalle.DetallesIngresosAdicionales.Add(new ItemAdicional
                                        {
                                            Nombre = rd["nombreCategoria"].ToString(),
                                            Soles = s,
                                            Dolares = d,
                                            Descripcion = rd["descripcion"].ToString()
                                        });
                                    }

                                    // 2) GASTOS ADICIONALES (detalle + total)
                                    rd.NextResult();
                                    while (rd.Read())
                                    {
                                        decimal s = Convert.ToDecimal(rd["soles"]);
                                        decimal d = Convert.ToDecimal(rd["dolares"]);
                                        gastosAdicionalesSoles += s;
                                        gastosAdicionalesDolares += d;
                                        detalle.DetallesGastosAdicionales.Add(new ItemAdicional
                                        {
                                            Nombre = rd["nombreCategoria"].ToString(),
                                            Soles = s,
                                            Dolares = d,
                                            Descripcion = rd["descripcion"].ToString()
                                        });
                                    }

                                    // Guardar gastos adicionales para desglose
                                    detalle.GastosAdicionalesSoles = gastosAdicionalesSoles;
                                    detalle.GastosAdicionalesDolares = gastosAdicionalesDolares;

                                    // 3) DETALLE DE PEAJES
                                    rd.NextResult();
                                    while (rd.Read())
                                        detalle.DetallesPeajes.Add(new DetallePeajeItem
                                        {
                                            Estacion = rd["estacion"].ToString(),
                                            Fecha = rd["fecha"].ToString(),
                                            Comprobante = rd["numeroComprobante"].ToString(),
                                            Soles = Convert.ToDecimal(rd["montoSoles"]),
                                            Dolares = Convert.ToDecimal(rd["montoDolares"]),
                                            Observaciones = rd["observaciones"].ToString()
                                        });

                                    // 4) REPARACIONES, 5) HOSPEDAJE, 6) COMBUSTIBLE (mismas columnas)
                                    foreach (List<DetalleGenericoItem> destino in new[]
                                        { detalle.DetallesReparaciones, detalle.DetallesHospedaje, detalle.DetallesCombustible })
                                    {
                                        rd.NextResult();
                                        while (rd.Read())
                                            destino.Add(new DetalleGenericoItem
                                            {
                                                Fecha = rd["fecha"].ToString(),
                                                Comprobante = rd["numeroComprobante"].ToString(),
                                                Soles = Convert.ToDecimal(rd["montoSoles"]),
                                                Dolares = Convert.ToDecimal(rd["montoDolares"]),
                                                Observaciones = rd["observaciones"].ToString()
                                            });
                                    }

                                    // 7) DESCUENTOS Y REINTEGROS
                                    rd.NextResult();
                                    if (rd.Read())
                                    {
                                        detalle.DescuentoSoles = Convert.ToDecimal(rd["descuentoSoles"]);
                                        detalle.DescuentoDolares = Convert.ToDecimal(rd["descuentoDolares"]);
                                        detalle.ReintegroSoles = Convert.ToDecimal(rd["reintegroSoles"]);
                                        detalle.ReintegroDolares = Convert.ToDecimal(rd["reintegroDolares"]);
                                        detalle.DescuentoMotivo = rd["observacionesDescuento"] != DBNull.Value ? rd["observacionesDescuento"].ToString() : "";
                                        detalle.ReintegroMotivo = rd["observacionesReintegro"] != DBNull.Value ? rd["observacionesReintegro"].ToString() : "";
                                    }

                                    // 8) ENTREGAS Y PAGOS DIRECTOS unidos a esta liquidación (misma conexión:
                                    //    una segunda conexión dentro de una transacción la volvería distribuida).
                                    rd.NextResult();
                                    var tabla = new DataTable();
                                    tabla.Load(rd);
                                    var movimientos = MovimientosViajeService.Leer(tabla);
                                    detalle.Entregas = movimientos.Where(m => m.Tipo == MovimientosViajeService.Entrega).ToList();
                                    detalle.PagosDirectos = movimientos.Where(m => m.Tipo == MovimientosViajeService.PagoDirecto).ToList();
                                }
                            }


                            // ✅ CALCULAR TOTAL DE GASTOS
                            detalle.TotalGastosSoles =
                                detalle.GastosPeajesSoles +
                                detalle.GastosAlimentacionSoles +
                                detalle.GastosApoyoSeguridadSoles +
                                detalle.GastosReparacionesSoles +
                                detalle.GastosMovilidadSoles +
                                detalle.GastosEncarpadaSoles +
                                detalle.GastosHospedajeSoles +
                                detalle.GastosCombustibleSoles +
                                gastosAdicionalesSoles;

                            detalle.TotalGastosDolares =
                                detalle.GastosPeajesDolares +
                                detalle.GastosAlimentacionDolares +
                                detalle.GastosApoyoSeguridadDolares +
                                detalle.GastosReparacionesDolares +
                                detalle.GastosMovilidadDolares +
                                detalle.GastosEncarpadaDolares +
                                detalle.GastosHospedajeDolares +
                                detalle.GastosCombustibleDolares +
                                gastosAdicionalesDolares;


                            return detalle;
                        }
                    }
                }
            }

            return null;
        }
    }
}
