-- ============================================================
-- sp_ReporteRendimientoPorVehiculo
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteRendimientoPorVehiculo]
    @FechaDesde DATETIME,
    @FechaHasta DATETIME,
    @IdLugarAbastecimiento INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Verificar si existen registros para los filtros proporcionados
    SELECT 
        CASE 
            WHEN EXISTS (
                SELECT 1 
                FROM AbastecimientoCombustible a
                WHERE a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
                AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
            ) THEN 1 
            ELSE 0 
        END AS ExistenRegistros;
    
    -- Tabla principal de resultados con datos de rendimiento por vehículo
    SELECT 
        t.idTracto,
        t.placaTracto,
        t.marca,
        t.modelo,
        COUNT(a.idAbastecimientoCombustible) AS cantidadAbastecimientos,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesAbastecidos,
        SUM(a.distanciaRutaKM) AS totalKilometrosRecorridos,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedio,
        SUM(a.montoTotalGalonesComprados) AS costoTotalCombustible
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Tracto t ON a.idTracto = t.idTracto
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
    GROUP BY 
        t.idTracto, t.placaTracto, t.marca, t.modelo
    ORDER BY 
        rendimientoPromedio DESC;
    
    -- Tabla secundaria con estadísticas generales para el encabezado del reporte
    SELECT 
        COUNT(DISTINCT t.idTracto) AS totalVehiculos,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesFlota,
        SUM(a.distanciaRutaKM) AS totalKilometrosFlota,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedioFlota,
        SUM(a.montoTotalGalonesComprados) AS costoTotalFlota
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Tracto t ON a.idTracto = t.idTracto
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento);
    
    -- Tabla detallada de los 10 mejores abastecimientos por rendimiento
    SELECT TOP 10
        a.idAbastecimientoCombustible,
        a.numeroAbastecimientoCombustible,
        t.placaTracto,
        CONCAT(c.nombre, ' ', c.apPaterno) AS nombreConductor,
        a.fechaHora,
        l.nombreAbastecimiento AS lugarAbastecimiento,
        a.distanciaRutaKM,
        a.galonesTotalAbastecidos,
        a.rendimientoPromedio,
        r.nombre AS rutaNombre
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Tracto t ON a.idTracto = t.idTracto
    INNER JOIN 
        Conductor c ON a.idConductor = c.idConductor
    INNER JOIN 
        LugarAbastecimiento l ON a.idLugarAbastecimiento = l.idLugarAbastecimiento
    LEFT JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
        AND a.rendimientoPromedio > 0
    ORDER BY 
        a.rendimientoPromedio DESC;
END
GO
