-- ============================================================
-- sp_ReporteConsumoGeneralCombustible
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteConsumoGeneralCombustible]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idLugarAbastecimiento INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal: Consumo general agrupado por fecha y lugar de abastecimiento
    SELECT 
        CAST(ac.fechaHora AS DATE) AS Fecha,
        la.nombreAbastecimiento AS LugarAbastecimiento,
        COUNT(ac.idAbastecimientoCombustible) AS CantidadAbastecimientos,
        SUM(ac.galonesCompradosRuta) AS TotalGalonesComprados,
        SUM(ac.galonesTotalConsumidos) AS TotalGalonesConsumidos,
        SUM(ac.montoTotalGalonesComprados) AS TotalMontoPagado,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 
            THEN SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos) 
            ELSE 0 
        END AS RendimientoPromedio,
        COUNT(DISTINCT ac.idTracto) AS CantidadVehiculos,
        COUNT(DISTINCT ac.idConductor) AS CantidadConductores
    FROM AbastecimientoCombustible ac
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    GROUP BY CAST(ac.fechaHora AS DATE), la.nombreAbastecimiento
    ORDER BY Fecha DESC, LugarAbastecimiento;
    
    -- Resumen general para indicadores
    SELECT 
        COUNT(DISTINCT ac.idAbastecimientoCombustible) AS TotalAbastecimientos,
        COUNT(DISTINCT ac.idTracto) AS TotalVehiculos,
        COUNT(DISTINCT ac.idConductor) AS TotalConductores,
        COUNT(DISTINCT ac.idLugarAbastecimiento) AS TotalLugares,
        SUM(ac.galonesCompradosRuta) AS TotalGalonesComprados,
        SUM(ac.galonesTotalConsumidos) AS TotalGalonesConsumidos,
        SUM(ac.distanciaRutaKM) AS TotalDistanciaRecorrida,
        SUM(ac.montoTotalGalonesComprados) AS TotalGastoMonetario,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 
            THEN SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos) 
            ELSE 0 
        END AS RendimientoGlobalKmGal,
        AVG(CASE WHEN ac.precioDolar > 0 THEN ac.precioDolar ELSE NULL END) AS PrecioPromedioDolar
    FROM AbastecimientoCombustible ac
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento);
    
    -- Datos por lugar de abastecimiento
    SELECT 
        la.nombreAbastecimiento AS LugarAbastecimiento,
        COUNT(ac.idAbastecimientoCombustible) AS CantidadAbastecimientos,
        SUM(ac.galonesCompradosRuta) AS TotalGalonesComprados,
        SUM(ac.galonesTotalConsumidos) AS TotalGalonesConsumidos,
        SUM(ac.montoTotalGalonesComprados) AS TotalMontoPagado,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 
            THEN SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos) 
            ELSE 0 
        END AS RendimientoPromedio
    FROM AbastecimientoCombustible ac
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    GROUP BY la.nombreAbastecimiento
    ORDER BY TotalGalonesConsumidos DESC;
END
GO
