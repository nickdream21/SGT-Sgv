-- ============================================================
-- sp_ReporteVehiculosAsignados
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteVehiculosAsignados]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @placaVehiculo VARCHAR(50) = NULL,
    @marcaVehiculo VARCHAR(50) = NULL,
    @modeloVehiculo VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de vehículos asignados
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        cpic.numeroCPIC,
        t.placaTracto,
        t.marca AS MarcaTracto,
        t.modelo AS ModeloTracto,
        cr.placaCarreta,
        cr.marca AS MarcaCarreta,
        cr.modelo AS ModeloCarreta,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        cl.nombre AS Cliente,
        p.nombre AS Producto,
        ov.fechaSalida,
        ov.horaSalida,
        ov.fechaLlegada,
        ov.horaLlegada,
        CASE 
            WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
              OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
            ELSE DATEDIFF(HOUR, 
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
            )
        END AS HorasViaje,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN Producto p ON ov.idProducto = p.idProducto
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
    WHERE (@numeroPedido IS NULL 
            OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
        AND (@numeroPedido IS NOT NULL 
            OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
        AND (@idCliente IS NULL 
            OR ov.idCliente = @idCliente)
        AND (@placaVehiculo IS NULL 
            OR t.placaTracto LIKE '%' + @placaVehiculo + '%' 
            OR cr.placaCarreta LIKE '%' + @placaVehiculo + '%')
        AND (@marcaVehiculo IS NULL 
            OR t.marca = @marcaVehiculo 
            OR cr.marca = @marcaVehiculo)
        AND (@modeloVehiculo IS NULL 
            OR t.modelo = @modeloVehiculo 
            OR cr.modelo = @modeloVehiculo)
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;
    
    -- Cálculo de indicadores
    WITH VehiculosData AS (
        SELECT 
            ov.idOrdenViaje,
            t.placaTracto,
            cr.placaCarreta,
            CASE 
                WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                  OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
                ELSE DATEDIFF(HOUR, 
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
                )
            END AS HorasViaje
        FROM OrdenViaje ov
        LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
        LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        WHERE (@numeroPedido IS NULL 
                OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
            AND (@numeroPedido IS NOT NULL 
                OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
            AND (@idCliente IS NULL 
                OR ov.idCliente = @idCliente)
            AND (@placaVehiculo IS NULL 
                OR t.placaTracto LIKE '%' + @placaVehiculo + '%' 
                OR cr.placaCarreta LIKE '%' + @placaVehiculo + '%')
            AND (@marcaVehiculo IS NULL 
                OR t.marca = @marcaVehiculo 
                OR cr.marca = @marcaVehiculo)
            AND (@modeloVehiculo IS NULL 
                OR t.modelo = @modeloVehiculo 
                OR cr.modelo = @modeloVehiculo)
    )
    SELECT 
        COUNT(DISTINCT placaTracto) AS TotalTractos,
        COUNT(DISTINCT placaCarreta) AS TotalCarretas,
        COUNT(DISTINCT idOrdenViaje) AS TotalViajes,
        AVG(CASE WHEN HorasViaje IS NOT NULL THEN HorasViaje ELSE 0 END) AS PromedioHorasViaje,
        MAX(HorasViaje) AS MaximoHorasViaje
    FROM VehiculosData;
END
GO
