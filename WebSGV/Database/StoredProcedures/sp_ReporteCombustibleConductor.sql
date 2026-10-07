-- ============================================================
-- sp_ReporteCombustibleConductor
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteCombustibleConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de combustible
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        ov.fechaSalida AS FechaSalida,
        ov.fechaLlegada AS FechaLlegada,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        t.placaTracto AS Placa,
        t.marca AS Marca,
        t.modelo AS Modelo,
        la.nombreAbastecimiento AS EstacionServicio,
        ac.fechaHora AS FechaAbastecimiento,
        ac.distanciaRutaKM AS DistanciaKm,
        ac.galonesTotalAbastecidos AS Galones,
        ac.precioDolar AS PrecioUnitario,
        (ac.galonesTotalAbastecidos * ac.precioDolar) AS Total,
        ac.rendimientoPromedio AS RendimientoKmGalon,
        ac.observaciones AS Observaciones,
        cl.nombre AS Cliente
    FROM OrdenViaje ov
    JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN AbastecimientoCombustible ac ON ac.idOrdenViaje = ov.idOrdenViaje
    LEFT JOIN LugarAbastecimiento la ON ac.idLugarAbastecimiento = la.idLugarAbastecimiento
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    WHERE ac.idAbastecimientoCombustible IS NOT NULL
      AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
      AND (@idConductor IS NULL OR @idConductor = '0' OR c.idConductor = @idConductor)
    ORDER BY ov.fechaSalida DESC, ov.numeroOrdenViaje;
    
    -- Cálculo de indicadores - Resultados agregados
    SELECT 
        COUNT(DISTINCT t.placaTracto) AS ContadorVehiculos,
        ISNULL(SUM(ac.distanciaRutaKM), 0) AS TotalKilometros,
        ISNULL(SUM(ac.galonesTotalAbastecidos), 0) AS TotalGalones,
        ISNULL(SUM(ac.galonesTotalAbastecidos * ac.precioDolar), 0) AS TotalDolares,
        CASE 
            WHEN SUM(ac.galonesTotalAbastecidos) > 0 
            THEN ISNULL(SUM(ac.distanciaRutaKM) / SUM(ac.galonesTotalAbastecidos), 0) 
            ELSE 0 
        END AS RendimientoGeneral
    FROM OrdenViaje ov
    JOIN Conductor c ON ov.idConductor = c.idConductor
    JOIN Tracto t ON ov.idTracto = t.idTracto
    JOIN AbastecimientoCombustible ac ON ac.idOrdenViaje = ov.idOrdenViaje
    WHERE ac.idAbastecimientoCombustible IS NOT NULL
      AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
      AND (@idConductor IS NULL OR @idConductor = '0' OR c.idConductor = @idConductor);
END
GO
