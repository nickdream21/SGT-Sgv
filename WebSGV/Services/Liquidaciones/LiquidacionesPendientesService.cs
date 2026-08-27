using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
    /// Las transacciones de aprobación/corrección (AprobarConAjustes, CorregirAjustesAprobada)
    /// y el armador de DTO ObtenerDetalleLiquidacion también viven aquí: el SQL/SP se movió
    /// verbatim y los WebMethods del code-behind conservan sesión, validación, el objeto de
    /// respuesta, el PDF/Firma y la auditoría. La orquestación de PDF (archivado en disco vía
    /// HostingEnvironment + PdfOrdenViajeService) permanece en el code-behind; aquí sólo están
    /// sus dos consultas (ObtenerRutaPdfArchivado / GuardarRutaPdfArchivado).
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

        /// <summary>Aprueba la liquidación vía <c>sp_AprobarLiquidacion</c>.</summary>
        public static DataTable Aprobar(string numeroOrdenViaje, int idUsuarioAprobacion, object observaciones) =>
            DbHelper.ConsultarTablaSp("sp_AprobarLiquidacion",
                DbHelper.Param("@numeroOrdenViaje", numeroOrdenViaje),
                DbHelper.Param("@idUsuarioAprobacion", idUsuarioAprobacion),
                DbHelper.Param("@observaciones", observaciones));

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

        /// <summary>
        /// Liquidaciones aprobadas (viajes COMPLETADO) con filtros opcionales. El SQL se
        /// arma dinámicamente igual que en el code-behind original; el cálculo de balance
        /// y el mapeo se hacen fuera.
        /// </summary>
        public static DataTable ObtenerAprobadas(int idConductor, string fechaDesde, string fechaHasta,
            string numeroOrden, DateTime? fd, DateTime? fh)
        {
            string query = @"
                        SELECT
                            ov.idOrdenViaje,
                            ov.numeroOrdenViaje,
                            c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS NombreConductor,
                            t.placaTracto,
                            ca.placaCarreta,
                            ov.fechaSalida,
                            ov.fechaLlegada,
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

        /// <summary>Número de orden sólo si el viaje está COMPLETADO (para revertir).</summary>
        public static object ObtenerNumeroOrdenCompletado(int idOrdenViaje) =>
            DbHelper.EjecutarEscalar(
                "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id AND estadoViaje = 'COMPLETADO'",
                DbHelper.Param("@id", idOrdenViaje));

        /// <summary>Revierte la aprobación devolviendo el viaje a PENDIENTE. Filas afectadas.</summary>
        public static int RevertirEstado(int idOrdenViaje, string motivo) =>
            DbHelper.EjecutarNonQuery(@"
                        UPDATE OrdenViaje
                        SET estadoViaje = 'PENDIENTE',
                            estadoAprobacion = 'PENDIENTE',
                            observaciones = ISNULL(observaciones, '') + CHAR(13) + CHAR(10) +
                                '[REVERSION ' + CONVERT(varchar, GETDATE(), 120) + '] ' + @motivo
                        WHERE idOrdenViaje = @id AND estadoViaje = 'COMPLETADO'",
                DbHelper.Param("@id", idOrdenViaje),
                DbHelper.Param("@motivo", motivo));

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
                                '[RECHAZO ' + CONVERT(varchar, GETDATE(), 120) + '] ' + @motivo
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

        /// <summary>Resultado de <see cref="AprobarConAjustes"/> (sin presentación).</summary>
        public class ResultadoAprobacionAjustes
        {
            /// <summary><c>true</c> si la transacción se confirmó (commit).</summary>
            public bool Exito { get; set; }
            /// <summary>Mensaje de error de negocio (orden inexistente o rechazo del SP) cuando <see cref="Exito"/> es <c>false</c>.</summary>
            public string Mensaje { get; set; }
            /// <summary>Número de orden de viaje resuelto (disponible en éxito).</summary>
            public string NumeroOrdenViaje { get; set; }
        }

        /// <summary>
        /// Transacción de aprobación con ajustes: resuelve el número de orden, hace UPSERT de
        /// <c>DescuentosReintegros</c> (sólo si hay valores) y ejecuta <c>sp_AprobarLiquidacion</c>.
        /// SQL/SP movido verbatim del code-behind. Hace rollback (vía dispose en los retornos
        /// tempranos de negocio, y explícito + re-propagación ante excepción). La validación de
        /// sesión/montos, el objeto de respuesta, el PDF y la auditoría quedan en el WebMethod.
        /// </summary>
        public static ResultadoAprobacionAjustes AprobarConAjustes(
            int idOrdenViaje, decimal descuentoSoles, decimal descuentoDolares,
            decimal reintegroSoles, decimal reintegroDolares, string notaAprobacion, int idUsuario)
        {
            string numeroOrdenViaje = null;

            using (SqlConnection conn = new SqlConnection(DbHelper.ConnectionString))
            {
                conn.Open();
                using (SqlTransaction tran = conn.BeginTransaction())
                {
                try
                {

                // 1. Obtener numeroOrdenViaje sólo cuando está pendiente y firmada
                using (SqlCommand cmd = new SqlCommand(
                    @"SELECT numeroOrdenViaje
                      FROM OrdenViaje
                      WHERE idOrdenViaje = @id
                        AND estadoAprobacion = 'PENDIENTE'
                        AND idFirmaConductor IS NOT NULL", conn, tran))
                {
                    cmd.Parameters.AddWithValue("@id", idOrdenViaje);
                    object result = cmd.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                        return new ResultadoAprobacionAjustes { Exito = false, Mensaje = "La liquidación no está pendiente o aún no tiene firma del conductor." };
                    numeroOrdenViaje = result.ToString();
                }

                // 2. UPSERT DescuentosReintegros (solo si hay valores)
                if (descuentoSoles != 0 || descuentoDolares != 0 || reintegroSoles != 0 || reintegroDolares != 0)
                {
                    string upsertSql = @"
                            IF EXISTS (SELECT 1 FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden)
                                UPDATE DescuentosReintegros
                                SET descuentoSoles = @descS, descuentoDolares = @descD,
                                    reintegroSoles = @reintS, reintegroDolares = @reintD,
                                    activo = 1
                                WHERE numeroOrdenViaje = @numeroOrden
                            ELSE
                                INSERT INTO DescuentosReintegros
                                    (numeroOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares, activo)
                                VALUES (@numeroOrden, @descS, @descD, @reintS, @reintD, 1)";

                    using (SqlCommand cmd = new SqlCommand(upsertSql, conn, tran))
                    {
                        cmd.Parameters.AddWithValue("@numeroOrden", numeroOrdenViaje);
                        cmd.Parameters.AddWithValue("@descS", descuentoSoles);
                        cmd.Parameters.AddWithValue("@descD", descuentoDolares);
                        cmd.Parameters.AddWithValue("@reintS", reintegroSoles);
                        cmd.Parameters.AddWithValue("@reintD", reintegroDolares);
                        cmd.ExecuteNonQuery();
                        System.Diagnostics.Debug.WriteLine($"✅ Ajustes guardados: DescS={descuentoSoles} DescD={descuentoDolares} ReintS={reintegroSoles} ReintD={reintegroDolares}");
                    }
                }

                // 3. Llamar sp_AprobarLiquidacion
                using (SqlCommand cmd = new SqlCommand("sp_AprobarLiquidacion", conn, tran))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@numeroOrdenViaje", numeroOrdenViaje);
                    cmd.Parameters.AddWithValue("@idUsuarioAprobacion", idUsuario);
                    cmd.Parameters.AddWithValue("@observaciones",
                        string.IsNullOrWhiteSpace(notaAprobacion) ? (object)DBNull.Value : notaAprobacion.Trim());

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int resultado = Convert.ToInt32(reader["Resultado"]);
                            string mensaje = reader["Mensaje"].ToString();
                            if (resultado != 1)
                                return new ResultadoAprobacionAjustes { Exito = false, Mensaje = mensaje };
                        }
                    }
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

            return new ResultadoAprobacionAjustes { Exito = true, NumeroOrdenViaje = numeroOrdenViaje };
        }

        /// <summary>Resultado de <see cref="CorregirAjustesAprobada"/>.</summary>
        public class ResultadoCorreccionAjustes
        {
            /// <summary><c>false</c> si la orden de viaje no existe (no se aplicó nada).</summary>
            public bool OrdenEncontrada { get; set; }
            /// <summary>Número de orden de viaje corregido (cuando existe).</summary>
            public string NumeroOrdenViaje { get; set; }
        }

        /// <summary>
        /// Transacción de corrección de ajustes de una liquidación ya aprobada: UPSERT de
        /// <c>DescuentosReintegros</c> + registro del cambio en <c>observaciones</c> para
        /// auditoría. SQL movido verbatim (usa <see cref="DbHelper.EnTransaccion"/>). La
        /// validación de sesión/montos/motivo y el objeto de respuesta quedan en el WebMethod.
        /// </summary>
        public static ResultadoCorreccionAjustes CorregirAjustesAprobada(
            int idOrdenViaje, decimal descuentoSoles, decimal descuentoDolares,
            decimal reintegroSoles, decimal reintegroDolares, string motivo, int idUsuario)
        {
            string numeroOrdenViaje = null;
            bool ordenEncontrada = true;

            DbHelper.EnTransaccion((conn, tran) =>
            {
                // 1. Obtener número de orden
                object result = DbHelper.EjecutarEscalar(conn, tran,
                    "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @id",
                    DbHelper.Param("@id", idOrdenViaje));
                if (result == null || result == DBNull.Value)
                {
                    ordenEncontrada = false;
                    return;
                }
                numeroOrdenViaje = result.ToString();

                // 2. UPSERT DescuentosReintegros
                string upsertSql = @"
                        IF EXISTS (SELECT 1 FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden)
                            UPDATE DescuentosReintegros
                            SET descuentoSoles = @descS, descuentoDolares = @descD,
                                reintegroSoles = @reintS, reintegroDolares = @reintD,
                                activo = 1
                            WHERE numeroOrdenViaje = @numeroOrden
                        ELSE
                            INSERT INTO DescuentosReintegros
                                (numeroOrdenViaje, descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares, activo)
                            VALUES (@numeroOrden, @descS, @descD, @reintS, @reintD, 1)";

                DbHelper.EjecutarNonQuery(conn, tran, upsertSql,
                    DbHelper.Param("@numeroOrden", numeroOrdenViaje),
                    DbHelper.Param("@descS", descuentoSoles),
                    DbHelper.Param("@descD", descuentoDolares),
                    DbHelper.Param("@reintS", reintegroSoles),
                    DbHelper.Param("@reintD", reintegroDolares));

                // 3. Registrar la corrección en observaciones para auditoría
                DbHelper.EjecutarNonQuery(conn, tran, @"
                        UPDATE OrdenViaje
                        SET observaciones = ISNULL(observaciones, '') + CHAR(13) + CHAR(10) +
                            '[CORRECCION AJUSTES ' + CONVERT(varchar, GETDATE(), 120) + '] ' + @motivo +
                            ' | Desc S/' + CAST(@descS AS varchar) + ' $' + CAST(@descD AS varchar) +
                            ' | Reint S/' + CAST(@reintS AS varchar) + ' $' + CAST(@reintD AS varchar)
                        WHERE idOrdenViaje = @id",
                    DbHelper.Param("@id", idOrdenViaje),
                    DbHelper.Param("@motivo", motivo),
                    DbHelper.Param("@descS", descuentoSoles),
                    DbHelper.Param("@descD", descuentoDolares),
                    DbHelper.Param("@reintS", reintegroSoles),
                    DbHelper.Param("@reintD", reintegroDolares));

                System.Diagnostics.Debug.WriteLine($"✅ Ajustes corregidos para {numeroOrdenViaje} por usuario {idUsuario}");
                System.Diagnostics.Debug.WriteLine($"   DescS={descuentoSoles} DescD={descuentoDolares} ReintS={reintegroSoles} ReintD={reintegroDolares}");
            });

            return new ResultadoCorreccionAjustes { OrdenEncontrada = ordenEncontrada, NumeroOrdenViaje = numeroOrdenViaje };
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

                    -- Conductor y Vehículos
                    c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
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

                            // ✅ INGRESOS ADICIONALES (detalle + total)
                            using (SqlCommand cmdAdicionales = new SqlCommand(
                                "SELECT nombreCategoria, soles, dolares, descripcion FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrden", conn))
                            {
                                cmdAdicionales.Parameters.AddWithValue("@numeroOrden", detalle.NumeroOrdenViaje);
                                using (SqlDataReader readerAdicionales = cmdAdicionales.ExecuteReader())
                                {
                                    while (readerAdicionales.Read())
                                    {
                                        decimal s = Convert.ToDecimal(readerAdicionales["soles"]);
                                        decimal d = Convert.ToDecimal(readerAdicionales["dolares"]);
                                        detalle.IngresosAdicionalesSoles += s;
                                        detalle.IngresosAdicionalesDolares += d;
                                        detalle.TotalIngresosSoles += s;
                                        detalle.TotalIngresosDolares += d;
                                        detalle.DetallesIngresosAdicionales.Add(new ItemAdicional
                                        {
                                            Nombre = readerAdicionales["nombreCategoria"].ToString(),
                                            Soles = s,
                                            Dolares = d,
                                            Descripcion = readerAdicionales["descripcion"].ToString()
                                        });
                                    }
                                }
                            }

                            // ✅ GASTOS ADICIONALES (detalle + total)
                            decimal gastosAdicionalesSoles = 0;
                            decimal gastosAdicionalesDolares = 0;

                            using (SqlCommand cmdGastosAd = new SqlCommand(
                                "SELECT nombreCategoria, soles, dolares, descripcion FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrden", conn))
                            {
                                cmdGastosAd.Parameters.AddWithValue("@numeroOrden", detalle.NumeroOrdenViaje);
                                using (SqlDataReader readerGastosAd = cmdGastosAd.ExecuteReader())
                                {
                                    while (readerGastosAd.Read())
                                    {
                                        decimal s = Convert.ToDecimal(readerGastosAd["soles"]);
                                        decimal d = Convert.ToDecimal(readerGastosAd["dolares"]);
                                        gastosAdicionalesSoles += s;
                                        gastosAdicionalesDolares += d;
                                        detalle.DetallesGastosAdicionales.Add(new ItemAdicional
                                        {
                                            Nombre = readerGastosAd["nombreCategoria"].ToString(),
                                            Soles = s,
                                            Dolares = d,
                                            Descripcion = readerGastosAd["descripcion"].ToString()
                                        });
                                    }
                                }
                            }

                            // Guardar gastos adicionales para desglose
                            detalle.GastosAdicionalesSoles = gastosAdicionalesSoles;
                            detalle.GastosAdicionalesDolares = gastosAdicionalesDolares;

                            // ✅ DETALLE DE PEAJES
                            using (SqlCommand cmdP = new SqlCommand(
                                "SELECT estacion, CONVERT(varchar,fecha,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetallePeajes WHERE numeroOrdenViaje = @n ORDER BY fecha", conn))
                            {
                                cmdP.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                using (SqlDataReader rP = cmdP.ExecuteReader())
                                {
                                    while (rP.Read())
                                        detalle.DetallesPeajes.Add(new DetallePeajeItem
                                        {
                                            Estacion = rP["estacion"].ToString(),
                                            Fecha = rP["fecha"].ToString(),
                                            Comprobante = rP["numeroComprobante"].ToString(),
                                            Soles = Convert.ToDecimal(rP["montoSoles"]),
                                            Dolares = Convert.ToDecimal(rP["montoDolares"]),
                                            Observaciones = rP["observaciones"].ToString()
                                        });
                                }
                            }

                            // ✅ DETALLE DE REPARACIONES
                            using (SqlCommand cmdR = new SqlCommand(
                                "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante", conn))
                            {
                                cmdR.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                using (SqlDataReader rR = cmdR.ExecuteReader())
                                {
                                    while (rR.Read())
                                        detalle.DetallesReparaciones.Add(new DetalleGenericoItem
                                        {
                                            Fecha = rR["fecha"].ToString(),
                                            Comprobante = rR["numeroComprobante"].ToString(),
                                            Soles = Convert.ToDecimal(rR["montoSoles"]),
                                            Dolares = Convert.ToDecimal(rR["montoDolares"]),
                                            Observaciones = rR["observaciones"].ToString()
                                        });
                                }
                            }

                            // ✅ DETALLE DE HOSPEDAJE
                            using (SqlCommand cmdH = new SqlCommand(
                                "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleHospedaje WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante", conn))
                            {
                                cmdH.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                using (SqlDataReader rH = cmdH.ExecuteReader())
                                {
                                    while (rH.Read())
                                        detalle.DetallesHospedaje.Add(new DetalleGenericoItem
                                        {
                                            Fecha = rH["fecha"].ToString(),
                                            Comprobante = rH["numeroComprobante"].ToString(),
                                            Soles = Convert.ToDecimal(rH["montoSoles"]),
                                            Dolares = Convert.ToDecimal(rH["montoDolares"]),
                                            Observaciones = rH["observaciones"].ToString()
                                        });
                                }
                            }

                            // ✅ DETALLE DE COMBUSTIBLE
                            using (SqlCommand cmdC = new SqlCommand(
                                "SELECT CONVERT(varchar,fechaComprobante,103) AS fecha, numeroComprobante, montoSoles, montoDolares, observaciones FROM DetalleCombustible WHERE numeroOrdenViaje = @n ORDER BY fechaComprobante", conn))
                            {
                                cmdC.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                using (SqlDataReader rC = cmdC.ExecuteReader())
                                {
                                    while (rC.Read())
                                        detalle.DetallesCombustible.Add(new DetalleGenericoItem
                                        {
                                            Fecha = rC["fecha"].ToString(),
                                            Comprobante = rC["numeroComprobante"].ToString(),
                                            Soles = Convert.ToDecimal(rC["montoSoles"]),
                                            Dolares = Convert.ToDecimal(rC["montoDolares"]),
                                            Observaciones = rC["observaciones"].ToString()
                                        });
                                }
                            }

                            // ✅ DESCUENTOS Y REINTEGROS
                            using (SqlCommand cmdDR = new SqlCommand(
                                "SELECT TOP 1 descuentoSoles, descuentoDolares, reintegroSoles, reintegroDolares FROM DescuentosReintegros WHERE numeroOrdenViaje = @n", conn))
                            {
                                cmdDR.Parameters.AddWithValue("@n", detalle.NumeroOrdenViaje);
                                using (SqlDataReader rDR = cmdDR.ExecuteReader())
                                {
                                    if (rDR.Read())
                                    {
                                        detalle.DescuentoSoles = Convert.ToDecimal(rDR["descuentoSoles"]);
                                        detalle.DescuentoDolares = Convert.ToDecimal(rDR["descuentoDolares"]);
                                        detalle.ReintegroSoles = Convert.ToDecimal(rDR["reintegroSoles"]);
                                        detalle.ReintegroDolares = Convert.ToDecimal(rDR["reintegroDolares"]);
                                    }
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
