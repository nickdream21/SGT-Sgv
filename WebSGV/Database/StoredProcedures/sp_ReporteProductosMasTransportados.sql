-- ============================================================
-- sp_ReporteProductosMasTransportados
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteProductosMasTransportados]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idProducto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para ranking de productos mÃ¡s transportados
    SELECT 
        p.idProducto,
        p.nombre AS NombreProducto,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsas,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilos,
        COUNT(DISTINCT c.idConductor) AS TotalConductores,
        COUNT(DISTINCT ov.idCliente) AS TotalClientes,
        COUNT(DISTINCT pd.idPlanta) AS TotalDestinos
    FROM Producto p
    JOIN DetalleOrdenViaje dov ON p.idProducto = dov.idProducto
    JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    LEFT JOIN GuiasTransportista gt2 ON ov.numeroOrdenViaje = gt2.numeroOrdenViaje
    LEFT JOIN PlantaDescarga pd ON gt2.plantaDescarga = pd.nombre OR TRY_CAST(gt2.plantaDescarga AS INT) = pd.idPlanta
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    GROUP BY p.idProducto, p.nombre
    ORDER BY TotalBolsas DESC, TotalViajes DESC;
    
    -- Calcular totales generales para indicadores
    SELECT 
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsasGlobal,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilosGlobal,
        COUNT(DISTINCT ov.idCliente) AS TotalClientesGlobal
    FROM Producto p
    JOIN DetalleOrdenViaje dov ON p.idProducto = dov.idProducto
    JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto);
END
GO
