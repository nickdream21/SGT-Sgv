-- ============================================================
-- sp_ReporteRendimientoPorRutaCombustible
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteRendimientoPorRutaCombustible]
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
                AND a.idRuta IS NOT NULL
            ) THEN 1 
            ELSE 0 
        END AS ExistenRegistros;
    
    -- Tabla principal: Rendimiento por ruta
    SELECT 
        r.idRuta,
        r.nombre AS nombreRuta,
        COUNT(DISTINCT a.idTracto) AS cantidadVehiculos,
        COUNT(DISTINCT a.idConductor) AS cantidadConductores,
        COUNT(a.idAbastecimientoCombustible) AS cantidadAbastecimientos,
        SUM(a.galonesTotalAbastecidos) AS totalGalonesAbastecidos,
        SUM(a.distanciaRutaKM) AS totalKilometrosRecorridos,
        CASE 
            WHEN SUM(a.galonesTotalAbastecidos) = 0 THEN 0
            ELSE SUM(a.distanciaRutaKM) / SUM(a.galonesTotalAbastecidos)
        END AS rendimientoPromedio,
        MIN(CASE WHEN a.rendimientoPromedio <= 0 THEN NULL ELSE a.rendimientoPromedio END) AS rendimientoMinimo,
        MAX(a.rendimientoPromedio) AS rendimientoMaximo,
        SUM(a.montoTotalGalonesComprados) AS costoTotalCombustible
    FROM 
        AbastecimientoCombustible a
    INNER JOIN 
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento)
    GROUP BY 
        r.idRuta, r.nombre
    ORDER BY 
        rendimientoPromedio DESC;
    
    -- Tabla secundaria: Estadísticas generales para el encabezado
    SELECT 
        COUNT(DISTINCT r.idRuta) AS totalRutas,
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
        Ruta r ON a.idRuta = r.idRuta
    WHERE 
        a.fechaHora BETWEEN @FechaDesde AND @FechaHasta
        AND (@IdLugarAbastecimiento IS NULL OR a.idLugarAbastecimiento = @IdLugarAbastecimiento);
END
GO
