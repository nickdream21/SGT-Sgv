using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using WebSGV.Helpers;

namespace WebSGV.Services.Liquidaciones
{
    /// <summary>
    /// Acceso a datos (SQL y stored procedures) de la revisión de liquidaciones
    /// (<c>LiquidacionesPendientes.aspx</c>). Extraído del code-behind: la validación
    /// de sesión, el saneamiento, el mapeo a DTO/controles, la auditoría, los PDFs y
    /// las firmas permanecen en el code-behind; este servicio solo ejecuta el SQL.
    /// No se modifica ninguna consulta ni stored procedure.
    ///
    /// Nota: los métodos transaccionales con efectos colaterales (AprobarConAjustes,
    /// CorregirAjustesAprobada) y el armador de DTO ObtenerDetalleLiquidacion siguen en
    /// el code-behind a la espera de un pase dedicado, por estar entrelazados con
    /// HttpContext/PDF/Firma y para no reestructurar la transacción crítica sin pruebas.
    /// </summary>
    public static class LiquidacionesPendientesService
    {
        /// <summary>Número de orden a partir del id (escalar, puede ser null/DBNull).</summary>
        public static object ObtenerNumeroOrden(int idOrdenViaje) =>
            DbHelper.EjecutarEscalar(
                "SELECT numeroOrdenViaje FROM OrdenViaje WHERE idOrdenViaje = @idOrdenViaje",
                DbHelper.Param("@idOrdenViaje", idOrdenViaje));

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
    }
}
