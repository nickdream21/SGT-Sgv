-- ============================================================
-- sp_ReporteConductoresAsignados
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteConductoresAsignados]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(100) = NULL,
    @dniConductor VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de conductores asignados
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        cpic.numeroCPIC,
        c.DNI,
        c.carnetExtranjeria,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        c.telefono,
        c.correo,
        t.placaTracto,
        cr.placaCarreta,
        cl.nombre AS Cliente,
        (
            SELECT STUFF(
                (
                    SELECT ', ' + p.nombre
                    FROM GuiasTransportista gt
                    JOIN DetalleOrdenViaje dov ON gt.idGuia = dov.idGuia
                    JOIN Producto p ON dov.idProducto = p.idProducto
                    WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje
                    FOR XML PATH('')
                ), 1, 2, '')
        ) AS Producto,
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
        (SELECT TOP 1 r.nombre 
         FROM GuiasTransportista gt 
         JOIN Ruta r ON TRY_CAST(gt.ruta1 AS INT) = r.idRuta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje) AS Ruta,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    JOIN Factura f ON cpic.idFactura = f.idFactura
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    WHERE (@numeroPedido IS NULL OR f.numeroPedido = @numeroPedido)
        AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
        AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
        AND (@nombreConductor IS NULL 
             OR c.nombre LIKE '%' + @nombreConductor + '%'
             OR c.apPaterno LIKE '%' + @nombreConductor + '%'
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
    ORDER BY c.apPaterno, c.apMaterno, c.nombre, ov.fechaSalida DESC;
    
    -- Cálculo de indicadores
    WITH ConductoresData AS (
        SELECT 
            ov.idOrdenViaje,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
            CASE 
                WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                  OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN NULL
                ELSE DATEDIFF(HOUR, 
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
                )
            END AS HorasViaje
        FROM OrdenViaje ov
        JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        WHERE (@numeroPedido IS NULL OR f.numeroPedido = @numeroPedido)
            AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
            AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
            AND (@nombreConductor IS NULL 
                 OR c.nombre LIKE '%' + @nombreConductor + '%'
                 OR c.apPaterno LIKE '%' + @nombreConductor + '%'
                 OR c.apMaterno LIKE '%' + @nombreConductor + '%')
            AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
    ),
    ViajesPorConductor AS (
        SELECT 
            NombreConductor,
            COUNT(*) AS NumViajes,
            SUM(ISNULL(HorasViaje, 0)) AS TotalHoras
        FROM ConductoresData
        GROUP BY NombreConductor
    ),
    ConductorMasViajes AS (
        SELECT TOP 1
            NombreConductor,
            NumViajes
        FROM ViajesPorConductor
        ORDER BY NumViajes DESC, NombreConductor
    )
    SELECT 
        (SELECT COUNT(DISTINCT NombreConductor) FROM ConductoresData) AS TotalConductores,
        COUNT(*) AS TotalViajes,
        SUM(ISNULL(HorasViaje, 0)) AS TotalHoras,
        CASE 
            WHEN COUNT(*) > 0 
            THEN CAST(SUM(ISNULL(HorasViaje, 0)) AS FLOAT) / COUNT(*) 
            ELSE 0 
        END AS PromedioHorasPorViaje,
        (SELECT NombreConductor FROM ConductorMasViajes) AS ConductorMasViajes,
        (SELECT NumViajes FROM ConductorMasViajes) AS NumViajesConductorMasViajes
    FROM ConductoresData;
END
GO
