-- ============================================================
-- sp_GenerarReporteRendimientoPorRuta
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GenerarReporteRendimientoPorRuta]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(10) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Resultado 1: Datos principales del reporte
    SELECT 
        r.nombre AS NombreRuta,
        t.placaTracto AS PlacaTracto,
        COUNT(ac.idAbastecimientoCombustible) AS CantidadViajes,
        SUM(ac.distanciaRutaKM) AS DistanciaTotal,
        SUM(ac.galonesTotalConsumidos) AS GalonesTotal,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 THEN 
                SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos)
            ELSE 0 
        END AS RendimientoPromedio,
        MIN(ac.rendimientoPromedio) AS RendimientoMinimo,
        MAX(ac.rendimientoPromedio) AS RendimientoMaximo,
        AVG(ac.rendimientoPromedio) AS RendimientoAverage,
        SUM(ac.montoTotalGalonesComprados) AS CostoTotal
    FROM AbastecimientoCombustible ac
    JOIN Ruta r ON ac.idRuta = r.idRuta
    JOIN Tracto t ON ac.idTracto = t.idTracto
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND ac.distanciaRutaKM > 0 
    AND ac.galonesTotalConsumidos > 0
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    GROUP BY r.nombre, t.placaTracto
    ORDER BY r.nombre, 
             CASE 
                 WHEN SUM(ac.galonesTotalConsumidos) > 0 THEN 
                     SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos)
                 ELSE 0 
             END DESC;

    -- Resultado 2: Indicadores calculados
    SELECT
        COUNT(DISTINCT r.nombre) AS TotalRutas,
        COUNT(DISTINCT t.placaTracto) AS TotalVehiculos,
        SUM(ac.distanciaRutaKM) AS TotalDistancia,
        SUM(ac.galonesTotalConsumidos) AS TotalGalones,
        CASE 
            WHEN SUM(ac.galonesTotalConsumidos) > 0 THEN 
                SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalConsumidos)
            ELSE 0 
        END AS RendimientoGeneral,
        SUM(ac.montoTotalGalonesComprados) AS CostoTotalGeneral,
        (
            SELECT TOP 1 r.nombre 
            FROM AbastecimientoCombustible ac2
            JOIN Ruta r ON ac2.idRuta = r.idRuta
            JOIN Tracto t ON ac2.idTracto = t.idTracto
            WHERE ac2.fechaHora BETWEEN @fechaDesde AND @fechaHasta
            AND ac2.distanciaRutaKM > 0 
            AND ac2.galonesTotalConsumidos > 0
            AND (@idTracto IS NULL OR t.idTracto = @idTracto)
            AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
            GROUP BY r.nombre
            ORDER BY 
                CASE 
                    WHEN SUM(ac2.galonesTotalConsumidos) > 0 THEN 
                        SUM(ac2.distanciaRutaKM) / SUM(ac2.galonesTotalConsumidos)
                    ELSE 0 
                END DESC
        ) AS RutaMejorRendimiento,
        (
            SELECT TOP 1 
                CASE 
                    WHEN SUM(ac2.galonesTotalConsumidos) > 0 THEN 
                        SUM(ac2.distanciaRutaKM) / SUM(ac2.galonesTotalConsumidos)
                    ELSE 0 
                END
            FROM AbastecimientoCombustible ac2
            JOIN Ruta r ON ac2.idRuta = r.idRuta
            JOIN Tracto t ON ac2.idTracto = t.idTracto
            WHERE ac2.fechaHora BETWEEN @fechaDesde AND @fechaHasta
            AND ac2.distanciaRutaKM > 0 
            AND ac2.galonesTotalConsumidos > 0
            AND (@idTracto IS NULL OR t.idTracto = @idTracto)
            AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
            GROUP BY r.nombre
            ORDER BY 
                CASE 
                    WHEN SUM(ac2.galonesTotalConsumidos) > 0 THEN 
                        SUM(ac2.distanciaRutaKM) / SUM(ac2.galonesTotalConsumidos)
                    ELSE 0 
                END DESC
        ) AS ValorMejorRendimiento
    FROM AbastecimientoCombustible ac
    JOIN Ruta r ON ac.idRuta = r.idRuta
    JOIN Tracto t ON ac.idTracto = t.idTracto
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND ac.distanciaRutaKM > 0 
    AND ac.galonesTotalConsumidos > 0
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%');
END
GO
