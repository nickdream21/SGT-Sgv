-- ============================================================
-- sp_ReporteProductosConductor
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteProductosConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL,
    @dniConductor VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(50) = NULL,
    @idProducto VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de productos transportados
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        p.nombre AS Producto,
        ISNULL(dov.cantidadBolsas, 0) AS CantidadBolsas,
        ISNULL(cp.pesoKg, 0) AS PesoKg,
        gt.numeroGuiaTransportista AS GuiaTransportista,
        gt.numeroGuiaCliente AS GuiaCliente,
        ov.fechaSalida AS FechaSalida,
        ov.fechaLlegada AS FechaLlegada,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt2
         LEFT JOIN PlantaDescarga pd ON gt2.plantaDescarga = pd.nombre OR TRY_CAST(gt2.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt2.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga,
        cl.nombre AS Cliente
    FROM OrdenViaje ov
    JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
    LEFT JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
    LEFT JOIN Producto p ON 
        CASE 
            WHEN dov.idProducto IS NOT NULL THEN dov.idProducto
            ELSE ov.idProducto
        END = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
      AND (@idConductor IS NULL OR c.idConductor = @idConductor)
      AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
      AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
           OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
           OR c.apMaterno LIKE '%' + @nombreConductor + '%')
      AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    ORDER BY ov.fechaSalida DESC, ov.numeroOrdenViaje;
    
    -- Cálculo de indicadores - Resultado 2
    WITH ProductosData AS (
        SELECT 
            ov.numeroOrdenViaje,
            p.nombre AS Producto,
            ISNULL(dov.cantidadBolsas, 0) AS CantidadBolsas,
            ISNULL(cp.pesoKg, 0) AS PesoKg
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN GuiasTransportista gt ON ov.numeroOrdenViaje = gt.numeroOrdenViaje
        LEFT JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
        LEFT JOIN Producto p ON 
            CASE 
                WHEN dov.idProducto IS NOT NULL THEN dov.idProducto
                ELSE ov.idProducto
            END = p.idProducto
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN CPIC_Productos cp ON cpic.idCPIC = cp.idCPIC AND p.idProducto = cp.idProducto
        WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
          AND (@idConductor IS NULL OR c.idConductor = @idConductor)
          AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
          AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
               OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
               OR c.apMaterno LIKE '%' + @nombreConductor + '%')
          AND (@idProducto IS NULL OR p.idProducto = @idProducto)
    ),
    ProductoStats AS (
        SELECT 
            Producto,
            SUM(CantidadBolsas) AS TotalBolsas,
            SUM(PesoKg) AS TotalPeso
        FROM ProductosData
        WHERE Producto IS NOT NULL
        GROUP BY Producto
    ),
    MaxProducto AS (
        SELECT TOP 1
            Producto,
            TotalBolsas
        FROM ProductoStats
        ORDER BY TotalBolsas DESC
    )
    SELECT 
        (SELECT COUNT(DISTINCT numeroOrdenViaje) FROM ProductosData) AS TotalViajes,
        (SELECT COUNT(DISTINCT Producto) FROM ProductosData WHERE Producto IS NOT NULL) AS TotalProductos,
        ISNULL(SUM(pd.CantidadBolsas), 0) AS TotalBolsas,
        ISNULL(SUM(pd.PesoKg), 0) AS TotalPesoKg,
        (SELECT TOP 1 Producto FROM MaxProducto) AS ProductoMasTransportado,
        (SELECT TOP 1 TotalBolsas FROM MaxProducto) AS BolsasProductoMasTransportado
    FROM ProductosData pd;
END
GO
