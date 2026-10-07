-- ============================================================
-- sp_ReporteViajesVehiculo
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteViajesVehiculo]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idTracto VARCHAR(10) = NULL,
    @placaTracto VARCHAR(10) = NULL,
    @marcaVehiculo VARCHAR(30) = NULL,
    @modeloVehiculo VARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Tabla temporal para almacenar resultados principales
    DECLARE @ResultadosViaje TABLE (
        NroOrdenViaje VARCHAR(50),
        placaTracto VARCHAR(10),
        placaCarreta VARCHAR(10),
        NombreConductor VARCHAR(100),
        Cliente VARCHAR(100),
        Producto VARCHAR(250),
        fechaSalida DATE,
        horaSalida TIME(7),
        fechaLlegada DATE,
        horaLlegada TIME(7),
        HorasViaje INT,
        NombreRuta VARCHAR(100),
        PlantaDescarga VARCHAR(100)
    );
    
    -- Consulta principal para obtener datos de viajes
    INSERT INTO @ResultadosViaje
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
        t.placaTracto,
        cr.placaCarreta,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
        cl.nombre AS Cliente,
        -- Producto: Probamos mÃºltiples fuentes, priorizando el nombre del producto
        ISNULL(p.nombre, 
            ISNULL((SELECT TOP 1 dp.nombre FROM DetalleOrdenViaje dov 
                    JOIN GuiasTransportista gt ON dov.idGuia = gt.idGuia 
                    JOIN Producto dp ON dov.idProducto = dp.idProducto 
                    WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje),
                ISNULL((SELECT TOP 1 descripcionProducto FROM GuiasTransportista 
                        WHERE numeroOrdenViaje = ov.numeroOrdenViaje 
                        AND descripcionProducto IS NOT NULL),
                    'No especificado'))) AS Producto,
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
        -- Ruta: Obtener el nombre completo, no solo el ID
        ISNULL(
            (SELECT TOP 1 r.nombre 
             FROM AbastecimientoCombustible ac
             INNER JOIN Ruta r ON ac.idRuta = r.idRuta
             WHERE ac.idOrdenViaje = ov.idOrdenViaje),
            ISNULL(
                (SELECT TOP 1 r.nombre 
                 FROM GuiasTransportista gt
                 INNER JOIN Ruta r ON CAST(gt.ruta1 AS VARCHAR) = CAST(r.idRuta AS VARCHAR)
                 WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje),
                ISNULL(
                    (SELECT TOP 1 gt.ruta1 
                     FROM GuiasTransportista gt 
                     WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje),
                    'No especificada'
                )
            )
        ) AS NombreRuta,
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN Producto p ON ov.idProducto = p.idProducto
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
    AND (@idTracto IS NULL OR t.idTracto = @idTracto)
    AND (@placaTracto IS NULL OR t.placaTracto LIKE '%' + @placaTracto + '%')
    AND (@marcaVehiculo IS NULL OR t.marca = @marcaVehiculo)
    AND (@modeloVehiculo IS NULL OR t.modelo = @modeloVehiculo)
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;
    
    -- Asegurar que valores NULL sean reemplazados con valores predeterminados
    UPDATE @ResultadosViaje
    SET Producto = 'No especificado'
    WHERE Producto IS NULL OR Producto = '';
    
    UPDATE @ResultadosViaje
    SET NombreRuta = 'No especificada'
    WHERE NombreRuta IS NULL OR NombreRuta = '';
    
    -- Devolver los resultados principales
    SELECT * FROM @ResultadosViaje;
    
    -- Calcular y devolver indicadores
    SELECT
        COUNT(*) AS TotalViajes,
        COUNT(DISTINCT Cliente) AS TotalClientesDistintos,
        COUNT(DISTINCT NombreConductor) AS TotalConductoresDistintos,
        SUM(ISNULL(HorasViaje, 0)) AS TotalHoras,
        AVG(CAST(ISNULL(HorasViaje, 0) AS FLOAT)) AS PromedioHoras,
        (SELECT COUNT(*) FROM @ResultadosViaje WHERE HorasViaje IS NOT NULL) AS ViajesCompletados
    FROM @ResultadosViaje;
END
GO
