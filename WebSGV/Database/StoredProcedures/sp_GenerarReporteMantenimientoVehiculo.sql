-- ============================================================
-- sp_GenerarReporteMantenimientoVehiculo
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GenerarReporteMantenimientoVehiculo]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(20) = NULL,
    @marcaVehiculo VARCHAR(30) = NULL,
    @modeloVehiculo VARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Resultado 1: Datos principales del reporte
    SELECT 
        ov.numeroOrdenViaje AS NumeroOrden,
        t.placaTracto AS PlacaTracto,
        t.marca AS MarcaVehiculo,
        t.modelo AS ModeloVehiculo,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        'Reparaciones/Mantenimiento' AS Concepto,
        ov.fechaSalida AS Fecha,
        e.reparacionesVariosSoles AS MontoSoles,
        e.repacionesVariosDolares AS MontoDolares,
        e.descReparacionesVarios AS Descripcion,
        'Egreso regular' AS TipoGasto
    FROM OrdenViaje ov
    JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
    WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
    AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
    AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)

    UNION ALL

    SELECT 
        ov.numeroOrdenViaje AS NumeroOrden,
        t.placaTracto AS PlacaTracto,
        t.marca AS MarcaVehiculo,
        t.modelo AS ModeloVehiculo,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        ca.nombreCategoria AS Concepto,
        ov.fechaSalida AS Fecha,
        ca.soles AS MontoSoles,
        ca.dolares AS MontoDolares,
        ca.descripcion AS Descripcion,
        'CategorÃ­a adicional' AS TipoGasto
    FROM OrdenViaje ov
    JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
    WHERE (ca.nombreCategoria LIKE '%manten%' OR 
           ca.nombreCategoria LIKE '%repara%' OR 
           ca.nombreCategoria LIKE '%mecanic%' OR
           ca.descripcion LIKE '%manten%' OR
           ca.descripcion LIKE '%repara%' OR
           ca.descripcion LIKE '%mecanic%')
    AND (ca.soles > 0 OR ca.dolares > 0)
    AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
    AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
    ORDER BY Fecha DESC, NumeroOrden;
    
    -- Resultado 2: Indicadores calculados para el reporte
    SELECT
        COUNT(DISTINCT PlacaTracto) AS TotalVehiculos,
        SUM(MontoSoles) AS TotalSoles,
        SUM(MontoDolares) AS TotalDolares,
        (SELECT TOP 1 PlacaTracto 
         FROM (
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(e.reparacionesVariosSoles, 0)) + SUM(COALESCE(e.repacionesVariosDolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
             WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
             
             UNION ALL
             
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(ca.soles, 0)) + SUM(COALESCE(ca.dolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
             WHERE (ca.nombreCategoria LIKE '%manten%' OR 
                    ca.nombreCategoria LIKE '%repara%' OR 
                    ca.nombreCategoria LIKE '%mecanic%' OR
                    ca.descripcion LIKE '%manten%' OR
                    ca.descripcion LIKE '%repara%' OR
                    ca.descripcion LIKE '%mecanic%')
             AND (ca.soles > 0 OR ca.dolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
         ) AS GastosPorVehiculo
         GROUP BY PlacaTracto
         ORDER BY SUM(TotalGasto) DESC
        ) AS VehiculoMayorGasto,
        (SELECT TOP 1 SUM(TotalGasto)
         FROM (
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(e.reparacionesVariosSoles, 0)) + SUM(COALESCE(e.repacionesVariosDolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
             WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
             
             UNION ALL
             
             SELECT 
                 t.placaTracto AS PlacaTracto,
                 SUM(COALESCE(ca.soles, 0)) + SUM(COALESCE(ca.dolares, 0)) AS TotalGasto
             FROM OrdenViaje ov
             JOIN Tracto t ON ov.idTracto = t.idTracto
             JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
             WHERE (ca.nombreCategoria LIKE '%manten%' OR 
                    ca.nombreCategoria LIKE '%repara%' OR 
                    ca.nombreCategoria LIKE '%mecanic%' OR
                    ca.descripcion LIKE '%manten%' OR
                    ca.descripcion LIKE '%repara%' OR
                    ca.descripcion LIKE '%mecanic%')
             AND (ca.soles > 0 OR ca.dolares > 0)
             AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@idTracto IS NULL OR t.idTracto = @idTracto)
             AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
             AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
             AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
             GROUP BY t.placaTracto
         ) AS GastosPorVehiculo
         GROUP BY PlacaTracto
         ORDER BY SUM(TotalGasto) DESC
        ) AS ValorMayorGasto
    FROM (
        SELECT 
            t.placaTracto AS PlacaTracto,
            e.reparacionesVariosSoles AS MontoSoles,
            e.repacionesVariosDolares AS MontoDolares
        FROM OrdenViaje ov
        JOIN Tracto t ON ov.idTracto = t.idTracto
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idTracto IS NULL OR t.idTracto = @idTracto)
        AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
        AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
        AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
        
        UNION ALL
        
        SELECT 
            t.placaTracto AS PlacaTracto,
            ca.soles AS MontoSoles,
            ca.dolares AS MontoDolares
        FROM OrdenViaje ov
        JOIN Tracto t ON ov.idTracto = t.idTracto
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.nombreCategoria LIKE '%manten%' OR 
               ca.nombreCategoria LIKE '%repara%' OR 
               ca.nombreCategoria LIKE '%mecanic%' OR
               ca.descripcion LIKE '%manten%' OR
               ca.descripcion LIKE '%repara%' OR
               ca.descripcion LIKE '%mecanic%')
        AND (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idTracto IS NULL OR t.idTracto = @idTracto)
        AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
        AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
        AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
    ) AS CombinedData;
END
GO
