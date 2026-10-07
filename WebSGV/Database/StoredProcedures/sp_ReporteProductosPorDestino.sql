-- ============================================================
-- sp_ReporteProductosPorDestino
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteProductosPorDestino]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idProducto INT = NULL,
    @idPlanta INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para productos por destino
    SELECT 
        pd.idPlanta,
        pd.nombre AS NombreDestino,
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
            WHERE (gt2.plantaDescarga = pd.nombre OR TRY_CAST(gt2.plantaDescarga AS INT) = pd.idPlanta)
              AND ov2.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND (@idProducto IS NULL OR p2.idProducto = @idProducto)
            GROUP BY p2.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Productos,
        -- Usando FOR XML PATH para los clientes tambiÃ©n
        STUFF((
            SELECT ', ' + ISNULL(cl2.nombre, 'Sin cliente')
            FROM OrdenViaje ov3
            JOIN GuiasTransportista gt3 ON ov3.numeroOrdenViaje = gt3.numeroOrdenViaje
            JOIN Cliente cl2 ON ov3.idCliente = cl2.idCliente
            WHERE (gt3.plantaDescarga = pd.nombre OR TRY_CAST(gt3.plantaDescarga AS INT) = pd.idPlanta)
              AND ov3.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
              AND (@idProducto IS NULL OR EXISTS (
                  SELECT 1 FROM DetalleOrdenViaje dov3 
                  JOIN Producto p3 ON dov3.idProducto = p3.idProducto
                  WHERE dov3.idGuia = gt3.idGuia
                    AND (@idProducto IS NULL OR p3.idProducto = @idProducto)
              ))
            GROUP BY cl2.nombre
            FOR XML PATH('')
        ), 1, 2, '') AS Clientes
    FROM PlantaDescarga pd
    JOIN GuiasTransportista gt ON pd.nombre = gt.plantaDescarga OR pd.idPlanta = TRY_CAST(gt.plantaDescarga AS INT)
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
        AND (@idPlanta IS NULL OR pd.idPlanta = @idPlanta)
    GROUP BY pd.idPlanta, pd.nombre
    ORDER BY SUM(ISNULL(dov.cantidadBolsas, 0)) DESC, COUNT(DISTINCT ov.numeroOrdenViaje) DESC;
    
    -- Calcular totales generales para indicadores
    SELECT 
        COUNT(DISTINCT pd.idPlanta) AS TotalDestinos,
        COUNT(DISTINCT p.idProducto) AS TotalProductos,
        COUNT(DISTINCT ov.numeroOrdenViaje) AS TotalViajes,
        SUM(ISNULL(dov.cantidadBolsas, 0)) AS TotalBolsasGlobal,
        SUM(ISNULL(cp.pesoKg, 0)) AS TotalKilosGlobal,
        COUNT(DISTINCT ov.idCliente) AS TotalClientesGlobal
    FROM PlantaDescarga pd
    JOIN GuiasTransportista gt ON pd.nombre = gt.plantaDescarga OR pd.idPlanta = TRY_CAST(gt.plantaDescarga AS INT)
    JOIN OrdenViaje ov ON gt.numeroOrdenViaje = ov.numeroOrdenViaje
    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    JOIN Producto p ON dov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idProducto IS NULL OR p.idProducto = @idProducto)
        AND (@idPlanta IS NULL OR pd.idPlanta = @idPlanta);
END
GO
