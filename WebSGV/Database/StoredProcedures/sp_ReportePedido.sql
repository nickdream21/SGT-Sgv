-- ============================================================
-- sp_ReportePedido
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReportePedido]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @numeroFactura VARCHAR(50) = NULL,
    @valorMinimo DECIMAL(18,2) = NULL,
    @valorMaximo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de pedidos
    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        cpic.numeroCPIC,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS NombreConductor,
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
        (SELECT TOP 1 pd.nombre 
         FROM GuiasTransportista gt
         LEFT JOIN PlantaDescarga pd ON gt.plantaDescarga = pd.nombre OR TRY_CAST(gt.plantaDescarga AS INT) = pd.idPlanta
         WHERE gt.numeroOrdenViaje = ov.numeroOrdenViaje AND pd.nombre IS NOT NULL) AS PlantaDescarga
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Tracto t ON ov.idTracto = t.idTracto
    LEFT JOIN Carreta cr ON ov.idCarreta = cr.idCarreta
    LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
    LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
    LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
    WHERE (@numeroPedido IS NULL OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
        AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
        AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
        AND (@numeroFactura IS NULL OR f.numeroFactura LIKE '%' + @numeroFactura + '%')
        AND (@valorMinimo IS NULL OR cpic.valorTotalFlete >= @valorMinimo)
        AND (@valorMaximo IS NULL OR cpic.valorTotalFlete <= @valorMaximo)
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;
    
    -- Calcular indicadores financieros para las órdenes seleccionadas
    WITH OrdenesSeleccionadas AS (
        SELECT ov.numeroOrdenViaje
        FROM OrdenViaje ov
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        WHERE (@numeroPedido IS NULL OR f.numeroPedido LIKE '%' + @numeroPedido + '%')
            AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
            AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
            AND (@numeroFactura IS NULL OR f.numeroFactura LIKE '%' + @numeroFactura + '%')
            AND (@valorMinimo IS NULL OR cpic.valorTotalFlete >= @valorMinimo)
            AND (@valorMaximo IS NULL OR cpic.valorTotalFlete <= @valorMaximo)
    ),
    HorasViajeCalculadas AS (
        SELECT 
            SUM(CASE 
                WHEN ov.fechaSalida IS NULL OR ov.horaSalida IS NULL 
                  OR ov.fechaLlegada IS NULL OR ov.horaLlegada IS NULL THEN 0
                ELSE DATEDIFF(HOUR, 
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaSalida), CAST(ov.fechaSalida AS datetime)),
                    DATEADD(SECOND, DATEDIFF(SECOND, '00:00:00', ov.horaLlegada), CAST(ov.fechaLlegada AS datetime))
                )
            END) AS TotalHorasViaje
        FROM OrdenViaje ov
        JOIN OrdenesSeleccionadas os ON ov.numeroOrdenViaje = os.numeroOrdenViaje
    ),
    IngresosCalculados AS (
        SELECT 
            SUM(ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
                ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) +
                ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
                ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0)) AS TotalIngresos
        FROM dbo.Ingresos i  -- Uso explícito del nombre de la tabla con prefijo de esquema
        JOIN OrdenesSeleccionadas os ON i.numeroOrdenViaje = os.numeroOrdenViaje
    ),
    EgresosCalculados AS (
        SELECT 
            SUM(ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
                ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
                ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
                ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) +
                ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
                ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
                ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
                ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0)) AS TotalEgresos
        FROM dbo.Egresos e  -- Uso explícito del nombre de la tabla con prefijo de esquema
        JOIN OrdenesSeleccionadas os ON e.numeroOrdenViaje = os.numeroOrdenViaje
    ),
    EgresosAdicionalesCalculados AS (
        SELECT 
            SUM(ISNULL(ca.soles, 0) + ISNULL(ca.dolares, 0)) AS TotalEgresosAdicionales
        FROM dbo.CategoriasAdicionales ca  -- Uso explícito del nombre de la tabla con prefijo de esquema
        JOIN OrdenesSeleccionadas os ON ca.numeroOrdenViaje = os.numeroOrdenViaje
    )
    SELECT 
        (SELECT COUNT(*) FROM OrdenesSeleccionadas) AS TotalPedidos,
        ISNULL((SELECT TotalIngresos FROM IngresosCalculados), 0) AS TotalIngresos,
        ISNULL((SELECT TotalEgresos FROM EgresosCalculados), 0) + 
        ISNULL((SELECT TotalEgresosAdicionales FROM EgresosAdicionalesCalculados), 0) AS TotalEgresos,
        ISNULL((SELECT TotalHorasViaje FROM HorasViajeCalculadas), 0) AS TotalHorasViaje;
END
GO
