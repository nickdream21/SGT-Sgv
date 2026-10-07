-- ============================================================
-- sp_ReporteProductosPorCliente
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteProductosPorCliente]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idCliente INT = NULL,
    @idProducto INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para productos por cliente
    SELECT 
        cl.idCliente,
        cl.nombre AS NombreCliente,
        cl.ruc AS RUC,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsas,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilos,
        -- Usando FOR XML PATH en lugar de STRING_AGG para compatibilidad
        STUFF((
            SELECT ', ' + p2.nombre
            FROM OrdenViaje ov2
            JOIN GuiasTransportista gt2 ON ov2.numeroOrdenViaje = gt2.numeroOrdenViaje
            JOIN DetalleOrdenViaje dov2 ON gt2.idGuia = dov2.idGuia
            JOIN Producto p2 ON dov2.idProducto = p2.idProducto
            WHERE ov2.idCliente = cl.idCliente
              AND ov2.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND (@idProducto IS NULL OR p2.idProducto = @idProducto)
            GROUP BY p2.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Productos,
        -- Usando FOR XML PATH para los destinos también
        STUFF((
            SELECT ', ' + ISNULL(pd.nombre, 'Sin destino')
            FROM OrdenViaje ov3
            JOIN GuiasTransportista gt3 ON ov3.numeroOrdenViaje = gt3.numeroOrdenViaje
            LEFT JOIN PlantaDescarga pd ON gt3.plantaDescarga = pd.nombre OR TRY_CAST(gt3.plantaDescarga AS INT) = pd.idPlanta
            WHERE ov3.idCliente = cl.idCliente
              AND ov3.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND pd.nombre IS NOT NULL
            GROUP BY pd.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Destinos
    FROM Cliente cl
    JOIN OrdenViaje ov ON cl.idCliente = ov.idCliente
    JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idCliente IS NULL OR cl.idCliente = @idCliente)
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    GROUP BY cl.idCliente, cl.nombre, cl.ruc
    ORDER BY SUM(ISNULL(dov.cantidadBolsas, 0)) DESC, COUNT(DISTINCT ov.numeroOrdenViaje) DESC;
    
    -- Calcular totales generales para indicadores (esta parte no cambia)
    SELECT 
        COUNT(DISTINCT cl.idCliente) AS TotalClientes,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsasGlobal,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilosGlobal
    FROM Cliente cl
    JOIN OrdenViaje ov ON cl.idCliente = ov.idCliente
    JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idCliente IS NULL OR cl.idCliente = @idCliente)
        AND (@idProducto IS NULL OR p.idProducto = @idProducto);
END
GO
