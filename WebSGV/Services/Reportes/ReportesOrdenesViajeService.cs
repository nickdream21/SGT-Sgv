using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using WebSGV.Helpers;

namespace WebSGV.Services.Reportes
{
    /// <summary>
    /// Consultas (SQL) de <c>ReportesOrdenesViaje.aspx</c>: liquidaciones, viajes activos,
    /// reporte personalizado y el detalle de una orden (AJAX). Extraídas del code-behind: el
    /// armado de GridViews/HTML, el cálculo de totales/balances en memoria y el manejo de
    /// excepciones permanecen en el code-behind; este servicio sólo ejecuta el SQL (parámetros
    /// y columnas movidos verbatim).
    /// </summary>
    public static class ReportesOrdenesViajeService
    {
        // ------------------------------------------------------------------
        // Combos del reporte personalizado
        // ------------------------------------------------------------------

        public static DataTable ObtenerConductoresActivos() =>
            DbHelper.ConsultarTabla(@"
                    SELECT idConductor,
                           CONCAT(nombre, ' ', apPaterno, ' ', ISNULL(apMaterno,'')) AS NombreCompleto
                    FROM Conductor WHERE activo = 1 ORDER BY nombre, apPaterno");

        public static DataTable ObtenerClientes() =>
            DbHelper.ConsultarTabla("SELECT idCliente, nombre FROM Cliente ORDER BY nombre");

        // ------------------------------------------------------------------
        // Liquidaciones (el code-behind añade columnas Monto* calculadas)
        // ------------------------------------------------------------------

        public static DataTable ObtenerLiquidaciones(DateTime fechaDesde, DateTime fechaHasta) =>
            DbHelper.ConsultarTabla(@"
                SELECT
                    c.DNI,
                    CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
                    ov.fechaSalida AS FechaSalida,
                    ov.numeroOrdenViaje AS NumeroLiquidacion,
                    ov.idOrdenViaje AS IdOrdenViaje,
                    ISNULL((SELECT dr.descuentoSoles FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoSoles,
                    ISNULL((SELECT dr.descuentoDolares FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoDolares,
                    ISNULL((SELECT dr.reintegroSoles FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroSoles,
                    ISNULL((SELECT dr.reintegroDolares FROM DescuentosReintegros dr WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroDolares
                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                WHERE ov.fechaSalida BETWEEN @FechaDesde AND @FechaHasta
                AND ov.estadoViaje = 'COMPLETADO'
                ORDER BY ov.fechaSalida DESC, c.nombre",
                DbHelper.Param("@FechaDesde", fechaDesde),
                DbHelper.Param("@FechaHasta", fechaHasta));

        // ------------------------------------------------------------------
        // Viajes activos sin liquidación (query dinámico con filtros)
        // ------------------------------------------------------------------

        public static DataTable ObtenerViajesActivos(string buscarConductor, string estadoViaje)
        {
            var parametrosViajes = new List<SqlParameter>();
            StringBuilder query = new StringBuilder(@"
                    SELECT
                        c.DNI,
                        CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno, '')) AS Conductor,
                        ISNULL(MAX(t.placaTracto), 'N/A') AS PlacaTracto,
                        ISNULL(MAX(ca.placaCarreta), 'N/A') AS PlacaCarreta,
                        -- Un viaje puede tener varios despachos con distinto cliente/destino:
                        -- listamos TODOS los distintos (deduplicados) en vez de elegir uno arbitrario.
                        ISNULL(STUFF((
                            SELECT DISTINCT ', ' + cl2.nombre
                            FROM Despachos d2
                            INNER JOIN Cliente cl2 ON d2.idCliente = cl2.idCliente
                            WHERE d2.idViajeProgreso = vp.idViajeProgreso AND d2.activo = 1
                            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''), 'N/A') AS Cliente,
                        MAX(d.fechaDespacho) AS FechaProgramacion,
                        ISNULL(STUFF((
                            SELECT DISTINCT ', ' + d2.lugarOperacion
                            FROM Despachos d2
                            WHERE d2.idViajeProgreso = vp.idViajeProgreso AND d2.activo = 1
                            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''), 'N/A') AS Destino,
                        COUNT(DISTINCT d.idDespacho) AS CantidadDespachos,
                        vp.fechaInicio AS FechaInicio,
                        DATEDIFF(DAY, vp.fechaInicio, GETDATE()) AS DiasEnViaje,
                        vp.estadoViaje AS Estado,
                        vp.idViajeProgreso AS IdViaje,
                        MAX(d.numeroDespacho) AS NumeroDespacho
                    FROM ViajesEnProgreso vp
                    INNER JOIN Conductor c ON c.idConductor = (
                        SELECT TOP 1 idConductor FROM Despachos
                        WHERE idViajeProgreso = vp.idViajeProgreso AND activo = 1
                        GROUP BY idConductor ORDER BY COUNT(*) DESC
                    )
                    INNER JOIN Despachos d ON vp.idViajeProgreso = d.idViajeProgreso AND d.activo = 1
                    LEFT JOIN Tracto t ON d.idTracto = t.idTracto
                    LEFT JOIN Carreta ca ON d.idCarreta = ca.idCarreta
                    LEFT JOIN Cliente cl ON d.idCliente = cl.idCliente
                    /*
                        Definición de 'Viaje activo sin liquidación':
                        Mismo criterio que la pantalla ListaDespachos (sp_LD_ObtenerViajesActivos):
                        - El viaje en progreso sigue ABIERTO (al liquidar pasa a CERRADO).
                        - El viaje está activo (no anulado).
                        - Tiene al menos un despacho activo asociado.

                        Salvaguarda: si por inconsistencia de datos quedó vinculada una OrdenViaje
                        ya COMPLETADA al viaje, igual lo excluimos. Las órdenes en otros estados
                        (borradores, rechazadas, etc.) NO se consideran liquidación válida.
                    */
                    WHERE vp.estadoViaje = 'ABIERTO'
                      AND vp.activo = 1
                      AND NOT EXISTS (
                          SELECT 1 FROM OrdenViaje ov
                          WHERE ov.idViajeProgreso = vp.idViajeProgreso
                            AND ov.estadoViaje = 'COMPLETADO'
                      )");

            if (!string.IsNullOrEmpty(buscarConductor))
            {
                query.Append(" AND (c.nombre LIKE @BuscarConductor OR c.apPaterno LIKE @BuscarConductor OR c.DNI LIKE @BuscarConductor)");
                parametrosViajes.Add(DbHelper.Param("@BuscarConductor", $"%{buscarConductor}%"));
            }

            if (!string.IsNullOrEmpty(estadoViaje) && estadoViaje != "TODOS")
            {
                query.Append(" AND vp.estadoViaje = @EstadoViaje");
                parametrosViajes.Add(DbHelper.Param("@EstadoViaje", estadoViaje));
            }

            query.Append(" GROUP BY c.DNI, c.nombre, c.apPaterno, c.apMaterno, vp.fechaInicio, vp.estadoViaje, vp.idViajeProgreso");
            query.Append(" ORDER BY vp.fechaInicio DESC");

            return DbHelper.ConsultarTabla(query.ToString(), parametrosViajes.ToArray());
        }

        // ------------------------------------------------------------------
        // Detalle de una orden (AJAX); el code-behind arma el HTML por sección
        // ------------------------------------------------------------------

        /// <summary>Cabecera (información general) de la orden por id.</summary>
        public static DataTable ObtenerCabeceraDetalle(int idOrden) =>
            DbHelper.ConsultarTabla(@"
                        SELECT
                            ov.numeroOrdenViaje,
                            ov.fechaSalida,
                            ov.fechaLlegada,
                            ov.horaSalida,
                            ov.horaLlegada,
                            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
                            t.placaTracto AS PlacaTracto,
                            ISNULL(ca.placaCarreta, 'N/A') AS PlacaCarreta,
                            ov.observaciones
                        FROM OrdenViaje ov
                        INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                        LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
                        LEFT JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                        WHERE ov.idOrdenViaje = @IdOrden",
                DbHelper.Param("@IdOrden", idOrden));

        /// <summary>Ingresos principales (fila única) de la orden.</summary>
        public static DataTable ObtenerIngresosPrincipales(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT
                    despachoSoles, despachoDolares, descDespacho,
                    prestamoSoles, prestamosDolares, descPrestamo,
                    mensualidadSoles, mensualidadDolares, descMensualidad,
                    otrosSoles, otrosDolares, descOtrosAutorizados
                FROM Ingresos
                WHERE numeroOrdenViaje = @numeroOrden",
                DbHelper.Param("@numeroOrden", numeroOrden));

        /// <summary>Ingresos adicionales de la orden.</summary>
        public static DataTable ObtenerIngresosAdicionales(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT nombreCategoria, descripcion, soles, dolares
                FROM IngresosAdicionales
                WHERE numeroOrdenViaje = @numeroOrden",
                DbHelper.Param("@numeroOrden", numeroOrden));

        /// <summary>Gastos principales (fila única) de la orden.</summary>
        public static DataTable ObtenerGastosPrincipales(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT
                    peajesSoles, peajesDolares, descPeajes,
                    alimentacionSoles, alimentacionDolares, descAlimentacion,
                    apoyoseguridadSoles, apoyoseguridadDolares, descApoyoSeguridad,
                    reparacionesVariosSoles, repacionesVariosDolares, descReparacionesVarios,
                    movilidadSoles, movilidadDolares, descMovilidad,
                    hospedajeSoles, hospedajeDolares, descHospedaje,
                    combustibleSoles, combustibleDolares, descCombustible,
                    encarpada_desencarpadaSoles, encarpada_desencarpadaDolares, descEncarpadaDesencarpada
                FROM Egresos
                WHERE numeroOrdenViaje = @numeroOrden",
                DbHelper.Param("@numeroOrden", numeroOrden));

        /// <summary>Gastos adicionales (Categorías Adicionales) de la orden.</summary>
        public static DataTable ObtenerGastosAdicionales(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT nombreCategoria, descripcion, soles, dolares
                FROM CategoriasAdicionales
                WHERE numeroOrdenViaje = @numeroOrden",
                DbHelper.Param("@numeroOrden", numeroOrden));

        /// <summary>Totales agregados de ingresos/gastos/descuentos/reintegros para el balance final.</summary>
        public static DataTable ObtenerBalanceDetalle(string numeroOrden) =>
            DbHelper.ConsultarTabla(@"
                SELECT
                    -- Ingresos
                    ISNULL((SELECT totalSoles FROM Ingresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalIngresosSoles,

                    ISNULL((SELECT totalDolares FROM Ingresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalIngresosDolares,

                    -- Gastos
                    ISNULL((SELECT
                        ISNULL(peajesSoles, 0) + ISNULL(alimentacionSoles, 0) + ISNULL(apoyoseguridadSoles, 0) +
                        ISNULL(reparacionesVariosSoles, 0) + ISNULL(movilidadSoles, 0) + ISNULL(hospedajeSoles, 0) +
                        ISNULL(combustibleSoles, 0) + ISNULL(encarpada_desencarpadaSoles, 0)
                    FROM Egresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalGastosSoles,

                    ISNULL((SELECT
                        ISNULL(peajesDolares, 0) + ISNULL(alimentacionDolares, 0) + ISNULL(apoyoseguridadDolares, 0) +
                        ISNULL(repacionesVariosDolares, 0) + ISNULL(movilidadDolares, 0) + ISNULL(hospedajeDolares, 0) +
                        ISNULL(combustibleDolares, 0) + ISNULL(encarpada_desencarpadaDolares, 0)
                    FROM Egresos WHERE numeroOrdenViaje = @numeroOrden), 0) +
                    ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrden), 0) AS TotalGastosDolares,

                    -- Descuentos y Reintegros
                    ISNULL((SELECT descuentoSoles FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS DescuentoSoles,
                    ISNULL((SELECT descuentoDolares FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS DescuentoDolares,
                    ISNULL((SELECT reintegroSoles FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS ReintegroSoles,
                    ISNULL((SELECT reintegroDolares FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrden AND activo = 1), 0) AS ReintegroDolares",
                DbHelper.Param("@numeroOrden", numeroOrden));

        // ------------------------------------------------------------------
        // Reporte personalizado (query dinámico grande; el code-behind añade el balance)
        // ------------------------------------------------------------------

        public static DataTable ObtenerReportePersonalizado(DateTime fechaDesde, DateTime fechaHasta,
            string estado, int idConductor, int idCliente, string placaTracto, string categoria, string orden)
        {
            string orderBy;
            switch (orden)
            {
                case "fecha_asc": orderBy = "ov.fechaSalida ASC"; break;
                case "conductor": orderBy = "Conductor ASC, ov.fechaSalida DESC"; break;
                case "cliente": orderBy = "Cliente ASC, ov.fechaSalida DESC"; break;
                default: orderBy = "ov.fechaSalida DESC"; break;
            }

            StringBuilder sb = new StringBuilder(@"
                SELECT
                    c.DNI AS DNI,
                    CONCAT(c.nombre, ' ', c.apPaterno, ' ', ISNULL(c.apMaterno,'')) AS Conductor,
                    ov.fechaSalida AS FechaSalida,
                    ov.fechaLlegada AS FechaLlegada,
                    ov.horaSalida AS HoraSalida,
                    ov.horaLlegada AS HoraLlegada,
                    ISNULL(t.placaTracto, 'N/A') AS PlacaTracto,
                    ISNULL(ca.placaCarreta, 'N/A') AS PlacaCarreta,
                    -- Todos los clientes/destinos distintos del viaje (un viaje puede tener varios despachos).
                    ISNULL(STUFF((SELECT DISTINCT ', ' + cl.nombre FROM Despachos d
                                INNER JOIN Cliente cl ON d.idCliente = cl.idCliente
                            WHERE d.idViajeProgreso = ov.idViajeProgreso AND d.activo = 1
                            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''), 'N/A') AS Cliente,
                    ISNULL(STUFF((SELECT DISTINCT ', ' + d.lugarOperacion FROM Despachos d
                            WHERE d.idViajeProgreso = ov.idViajeProgreso AND d.activo = 1
                            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''), 'N/A') AS Destino,
                    ISNULL((SELECT COUNT(DISTINCT d.idDespacho) FROM Despachos d
                            WHERE d.idViajeProgreso = ov.idViajeProgreso AND d.activo = 1), 0) AS CantidadDespachos,
                    ov.numeroOrdenViaje AS NumeroLiquidacion,
                    ov.estadoViaje AS Estado,
                    ISNULL(ov.observaciones, '') AS Observaciones,

                    -- Ingresos por categoría
                    ISNULL((SELECT i.despachoSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IDespachoSoles,
                    ISNULL((SELECT i.despachoDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IDespachoDolares,
                    ISNULL((SELECT i.prestamoSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IPrestamoSoles,
                    ISNULL((SELECT i.prestamosDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IPrestamoDolares,
                    ISNULL((SELECT i.mensualidadSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IMensualidadSoles,
                    ISNULL((SELECT i.mensualidadDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IMensualidadDolares,
                    ISNULL((SELECT i.otrosSoles FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IOtrosSoles,
                    ISNULL((SELECT i.otrosDolares FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IOtrosDolares,
                    ISNULL((SELECT SUM(soles) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IAdicionalesSoles,
                    ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IAdicionalesDolares,
                    ISNULL(STUFF((SELECT '; ' + ia.nombreCategoria + ' S/' + CONVERT(varchar, CAST(ISNULL(ia.soles,0) AS decimal(18,2)))
                                        + CASE WHEN ISNULL(ia.dolares,0) > 0 THEN ' / $' + CONVERT(varchar, CAST(ia.dolares AS decimal(18,2))) ELSE '' END
                                 FROM IngresosAdicionales ia
                                 WHERE ia.numeroOrdenViaje = ov.numeroOrdenViaje
                                   AND (ISNULL(ia.soles,0) > 0 OR ISNULL(ia.dolares,0) > 0)
                                 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS IAdicionalesDetalle,

                    -- Gastos por categoría
                    ISNULL((SELECT e.peajesSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GPeajesSoles,
                    ISNULL((SELECT e.peajesDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GPeajesDolares,
                    ISNULL((SELECT e.alimentacionSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAlimentacionSoles,
                    ISNULL((SELECT e.alimentacionDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAlimentacionDolares,
                    ISNULL((SELECT e.apoyoseguridadSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GApoyoSeguridadSoles,
                    ISNULL((SELECT e.apoyoseguridadDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GApoyoSeguridadDolares,
                    ISNULL((SELECT e.reparacionesVariosSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GReparacionesSoles,
                    ISNULL((SELECT e.repacionesVariosDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GReparacionesDolares,
                    ISNULL((SELECT e.movilidadSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GMovilidadSoles,
                    ISNULL((SELECT e.movilidadDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GMovilidadDolares,
                    ISNULL((SELECT e.hospedajeSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GHospedajeSoles,
                    ISNULL((SELECT e.hospedajeDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GHospedajeDolares,
                    ISNULL((SELECT e.combustibleSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GCombustibleSoles,
                    ISNULL((SELECT e.combustibleDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GCombustibleDolares,
                    ISNULL((SELECT e.encarpada_desencarpadaSoles FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GEncarpadaSoles,
                    ISNULL((SELECT e.encarpada_desencarpadaDolares FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GEncarpadaDolares,
                    ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAdicionalesSoles,
                    ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GAdicionalesDolares,
                    ISNULL(STUFF((SELECT '; ' + gca.nombreCategoria + ' S/' + CONVERT(varchar, CAST(ISNULL(gca.soles,0) AS decimal(18,2)))
                                        + CASE WHEN ISNULL(gca.dolares,0) > 0 THEN ' / $' + CONVERT(varchar, CAST(gca.dolares AS decimal(18,2))) ELSE '' END
                                 FROM CategoriasAdicionales gca
                                 WHERE gca.numeroOrdenViaje = ov.numeroOrdenViaje
                                   AND (ISNULL(gca.soles,0) > 0 OR ISNULL(gca.dolares,0) > 0)
                                 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), 1, 2, ''), '') AS GAdicionalesDetalle,

                    -- Totales agregados (para columnas agregadas y balance)
                    ISNULL((SELECT ISNULL(i.despachoSoles,0) + ISNULL(i.prestamoSoles,0)
                                   + ISNULL(i.mensualidadSoles,0) + ISNULL(i.otrosSoles,0)
                            FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(soles) FROM IngresosAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosSoles,

                    ISNULL((SELECT ISNULL(i.despachoDolares,0) + ISNULL(i.prestamosDolares,0)
                                   + ISNULL(i.mensualidadDolares,0) + ISNULL(i.otrosDolares,0)
                            FROM Ingresos i WHERE i.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(dolares) FROM IngresosAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS IngresosDolares,

                    ISNULL((SELECT ISNULL(e.peajesSoles,0) + ISNULL(e.alimentacionSoles,0)
                                   + ISNULL(e.apoyoseguridadSoles,0) + ISNULL(e.reparacionesVariosSoles,0)
                                   + ISNULL(e.movilidadSoles,0) + ISNULL(e.hospedajeSoles,0)
                                   + ISNULL(e.combustibleSoles,0) + ISNULL(e.encarpada_desencarpadaSoles,0)
                            FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(soles) FROM CategoriasAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosSoles,

                    ISNULL((SELECT ISNULL(e.peajesDolares,0) + ISNULL(e.alimentacionDolares,0)
                                   + ISNULL(e.apoyoseguridadDolares,0) + ISNULL(e.repacionesVariosDolares,0)
                                   + ISNULL(e.movilidadDolares,0) + ISNULL(e.hospedajeDolares,0)
                                   + ISNULL(e.combustibleDolares,0) + ISNULL(e.encarpada_desencarpadaDolares,0)
                            FROM Egresos e WHERE e.numeroOrdenViaje = ov.numeroOrdenViaje), 0)
                    + ISNULL((SELECT SUM(dolares) FROM CategoriasAdicionales
                              WHERE numeroOrdenViaje = ov.numeroOrdenViaje), 0) AS GastosDolares,

                    ISNULL((SELECT dr.descuentoSoles FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoSoles,
                    ISNULL((SELECT dr.descuentoDolares FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS DescuentoDolares,
                    ISNULL((SELECT dr.reintegroSoles FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroSoles,
                    ISNULL((SELECT dr.reintegroDolares FROM DescuentosReintegros dr
                            WHERE dr.numeroOrdenViaje = ov.numeroOrdenViaje AND dr.activo = 1), 0) AS ReintegroDolares

                FROM OrdenViaje ov
                INNER JOIN Conductor c ON ov.idConductor = c.idConductor
                LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
                LEFT JOIN Carreta ca ON ov.idCarreta = ca.idCarreta
                WHERE ov.fechaSalida BETWEEN @FechaDesde AND @FechaHasta ");

            if (!string.IsNullOrEmpty(estado) && estado != "TODOS")
                sb.Append(" AND ov.estadoViaje = @Estado ");
            if (idConductor > 0)
                sb.Append(" AND ov.idConductor = @IdConductor ");
            if (!string.IsNullOrEmpty(placaTracto))
                sb.Append(" AND t.placaTracto LIKE @PlacaTracto ");
            if (idCliente > 0)
                sb.Append(@" AND EXISTS (SELECT 1 FROM Despachos d
                                         WHERE d.idViajeProgreso = ov.idViajeProgreso
                                           AND d.activo = 1 AND d.idCliente = @IdCliente) ");
            if (!string.IsNullOrEmpty(categoria))
                sb.Append(@" AND EXISTS (SELECT 1 FROM CategoriasAdicionales gca
                                         WHERE gca.numeroOrdenViaje = ov.numeroOrdenViaje
                                           AND gca.nombreCategoria LIKE @Categoria
                                           AND (ISNULL(gca.soles,0) > 0 OR ISNULL(gca.dolares,0) > 0)) ");

            sb.Append(" ORDER BY ").Append(orderBy);

            var prs = new List<SqlParameter>();
            prs.Add(DbHelper.Param("@FechaDesde", fechaDesde));
            prs.Add(DbHelper.Param("@FechaHasta", fechaHasta));
            if (!string.IsNullOrEmpty(estado) && estado != "TODOS")
                prs.Add(DbHelper.Param("@Estado", estado));
            if (idConductor > 0)
                prs.Add(DbHelper.Param("@IdConductor", idConductor));
            if (!string.IsNullOrEmpty(placaTracto))
                prs.Add(DbHelper.Param("@PlacaTracto", "%" + placaTracto + "%"));
            if (idCliente > 0)
                prs.Add(DbHelper.Param("@IdCliente", idCliente));
            if (!string.IsNullOrEmpty(categoria))
                prs.Add(DbHelper.Param("@Categoria", "%" + categoria + "%"));

            return DbHelper.ConsultarTabla(sb.ToString(), prs.ToArray());
        }
    }
}
