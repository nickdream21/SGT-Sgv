-- ============================================================
-- sp_ReporteConsumoCombustibleVehiculo
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteConsumoCombustibleVehiculo]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(10) = NULL,
    @numeroAbastecimiento VARCHAR(10) = NULL,
    @productoCombustible VARCHAR(100) = NULL,
    @idLugarAbastecimiento INT = NULL,
    @galonesMinimos DECIMAL(11, 2) = NULL,
    @rendimientoMinimo DECIMAL(11, 2) = NULL,
    @tipoReporte VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para obtener datos de consumo de combustible
    SELECT 
        ac.numeroAbastecimientoCombustible AS NumeroAbastecimiento,
        t.placaTracto AS PlacaTracto,
        cr.placaCarreta AS PlacaCarreta,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        ac.producto AS ProductoCombustible,
        la.nombreAbastecimiento AS LugarAbastecimiento,
        ac.fechaHora AS FechaAbastecimiento,
        ac.galonesRutaAsignada AS GalonesAsignados,
        ac.galonesCompradosRuta AS GalonesComprados,
        ac.galonesTotalAbastecidos AS TotalGalones,
        ac.galonesAlFinalizar AS GalonesSobrantes,
        ac.galonesTotalConsumidos AS GalonesConsumidos,
        ac.precioDolar AS PrecioDolar,
        ac.montoTotalGalonesComprados AS MontoTotal,
        ac.distanciaRutaKM AS DistanciaKM,
        ac.rendimientoPromedio AS RendimientoKmGalon,
        r.nombre AS NombreRuta,
        ov.numeroOrdenViaje AS NumeroOrdenViaje
    FROM AbastecimientoCombustible ac
    LEFT JOIN Tracto t ON ac.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ac.idCarreta = cr.idCarreta
    LEFT JOIN Conductor c ON ac.idConductor = c.idConductor
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    LEFT JOIN Ruta r ON ac.idRuta = r.idRuta
    LEFT JOIN OrdenViaje ov ON ac.idOrdenViaje = ov.idOrdenViaje
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@numeroAbastecimiento IS NULL OR ac.numeroAbastecimientoCombustible LIKE '%' + @numeroAbastecimiento + '%')
    AND (@productoCombustible IS NULL OR ac.producto LIKE '%' + @productoCombustible + '%')
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    AND (@galonesMinimos IS NULL OR ac.galonesTotalConsumidos >= @galonesMinimos)
    AND (@rendimientoMinimo IS NULL OR ac.rendimientoPromedio >= @rendimientoMinimo)
    AND (
        @tipoReporte IS NULL OR
        (@tipoReporte = 'sobrante' AND ac.galonesAlFinalizar > 0) OR
        (@tipoReporte = 'comprado' AND ac.galonesCompradosRuta > 0)
    )
    ORDER BY ac.fechaHora DESC;
    
    -- Consulta para calcular indicadores
    SELECT 
        SUM(ISNULL(ac.galonesTotalConsumidos, 0)) AS TotalGalonesConsumidos,
        SUM(ISNULL(ac.galonesCompradosRuta, 0)) AS TotalGalonesComprados,
        SUM(ISNULL(ac.distanciaRutaKM, 0)) AS TotalDistanciaKm,
        SUM(ISNULL(ac.montoTotalGalonesComprados, 0)) AS TotalMontoGastado,
        COUNT(*) AS TotalRegistros,
        AVG(CASE WHEN ac.rendimientoPromedio > 0 THEN ac.rendimientoPromedio ELSE NULL END) AS RendimientoPromedio,
        CASE 
            WHEN SUM(ISNULL(ac.galonesTotalConsumidos, 0)) > 0 
            THEN SUM(ISNULL(ac.distanciaRutaKM, 0)) / SUM(ISNULL(ac.galonesTotalConsumidos, 0)) 
            ELSE 0 
        END AS RendimientoGlobal,
        COUNT(DISTINCT ac.idTracto) AS VehiculosDistintos,
        COUNT(DISTINCT ac.idConductor) AS ConductoresDistintos,
        COUNT(DISTINCT ac.idLugarAbastecimiento) AS LugaresDistintos,
        COUNT(DISTINCT ac.idRuta) AS RutasDistintas
    FROM AbastecimientoCombustible ac
    LEFT JOIN Tracto t ON ac.idTracto = t.idTracto
    WHERE ac.fechaHora BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@numeroAbastecimiento IS NULL OR ac.numeroAbastecimientoCombustible LIKE '%' + @numeroAbastecimiento + '%')
    AND (@productoCombustible IS NULL OR ac.producto LIKE '%' + @productoCombustible + '%')
    AND (@idLugarAbastecimiento IS NULL OR ac.idLugarAbastecimiento = @idLugarAbastecimiento)
    AND (@galonesMinimos IS NULL OR ac.galonesTotalConsumidos >= @galonesMinimos)
    AND (@rendimientoMinimo IS NULL OR ac.rendimientoPromedio >= @rendimientoMinimo)
    AND (
        @tipoReporte IS NULL OR
        (@tipoReporte = 'sobrante' AND ac.galonesAlFinalizar > 0) OR
        (@tipoReporte = 'comprado' AND ac.galonesCompradosRuta > 0)
    );
END
GO
