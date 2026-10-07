-- ============================================================
-- sp_ReporteBalanceFinanciero
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteBalanceFinanciero]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @numeroPedido VARCHAR(50) = NULL,
    @idCliente VARCHAR(50) = NULL,
    @tipoTransaccion VARCHAR(50) = NULL,
    @montoMinimo DECIMAL(18,2) = NULL,
    @montoMaximo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos financieros
    SELECT 
        Transacciones.numeroOrdenViaje AS NroOrdenViaje,
        f.numeroPedido AS NumeroPedido,
        Transacciones.fechaSalida AS FechaTransaccion,
        Transacciones.TipoTransaccion,
        Transacciones.Concepto,
        CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
        cl.nombre AS Cliente,
        Transacciones.IngresoSoles,
        Transacciones.IngresoDolares,
        Transacciones.EgresoSoles,
        Transacciones.EgresoDolares
    FROM (
        -- Despacho
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Despacho' AS Concepto,
            ISNULL(i.despachoSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- PrÃ©stamo
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'PrÃ©stamo' AS Concepto,
            ISNULL(i.prestamoSoles, 0) AS IngresoSoles,
            ISNULL(i.prestamosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.prestamoSoles > 0 OR i.prestamosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Mensualidad
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Mensualidad' AS Concepto,
            ISNULL(i.mensualidadSoles, 0) AS IngresoSoles,
            ISNULL(i.mensualidadDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.mensualidadSoles > 0 OR i.mensualidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Otros
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            'Otros' AS Concepto,
            ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Peajes
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Peaje' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- AlimentaciÃ³n
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'AlimentaciÃ³n' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.alimentacionSoles, 0) AS EgresoSoles,
            ISNULL(e.alimentacionDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.alimentacionSoles > 0 OR e.alimentacionDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Apoyo Seguridad
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Apoyo Seguridad' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.apoyoseguridadSoles, 0) AS EgresoSoles,
            ISNULL(e.apoyoseguridadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Reparaciones
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Reparaciones' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.reparacionesVariosSoles, 0) AS EgresoSoles,
            ISNULL(e.repacionesVariosDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Movilidad
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Movilidad' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.movilidadSoles, 0) AS EgresoSoles,
            ISNULL(e.movilidadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.movilidadSoles > 0 OR e.movilidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Hospedaje
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Hospedaje' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.hospedajeSoles, 0) AS EgresoSoles,
            ISNULL(e.hospedajeDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.hospedajeSoles > 0 OR e.hospedajeDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Combustible
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Combustible' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.combustibleSoles, 0) AS EgresoSoles,
            ISNULL(e.combustibleDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.combustibleSoles > 0 OR e.combustibleDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Encarpada/Desencarpada
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            'Encarpada/Desencarpada' AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- CategorÃ­as Adicionales (gastos adicionales)
        SELECT 
            ov.numeroOrdenViaje,
            ov.idConductor,
            ov.idCliente,
            ov.idCPIC,
            ov.fechaSalida,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
    ) AS Transacciones
    JOIN CPIC cpic ON Transacciones.idCPIC = cpic.idCPIC
    JOIN Factura f ON cpic.idFactura = f.idFactura
    LEFT JOIN Conductor c ON Transacciones.idConductor = c.idConductor
    LEFT JOIN Cliente cl ON Transacciones.idCliente = cl.idCliente
    WHERE (@montoMinimo IS NULL OR (IngresoSoles >= @montoMinimo) OR (IngresoDolares >= @montoMinimo) 
           OR (EgresoSoles >= @montoMinimo) OR (EgresoDolares >= @montoMinimo))
          AND (@montoMaximo IS NULL OR (IngresoSoles <= @montoMaximo) OR (IngresoDolares <= @montoMaximo) 
               OR (EgresoSoles <= @montoMaximo) OR (EgresoDolares <= @montoMaximo))
    ORDER BY FechaTransaccion, NroOrdenViaje, TipoTransaccion;
    
    -- CÃ¡lculo de indicadores financieros 
    SELECT 
        SUM(IngresoSoles) AS TotalIngresosSoles,
        SUM(IngresoDolares) AS TotalIngresosDolares,
        SUM(EgresoSoles) AS TotalEgresosSoles,
        SUM(EgresoDolares) AS TotalEgresosDolares,
        COUNT(*) AS TotalTransacciones
    FROM (
        -- Despacho
        SELECT 
            ISNULL(i.despachoSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- PrÃ©stamo
        SELECT 
            ISNULL(i.prestamoSoles, 0) AS IngresoSoles,
            ISNULL(i.prestamosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.prestamoSoles > 0 OR i.prestamosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Mensualidad
        SELECT 
            ISNULL(i.mensualidadSoles, 0) AS IngresoSoles,
            ISNULL(i.mensualidadDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.mensualidadSoles > 0 OR i.mensualidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Otros
        SELECT 
            ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Ingreso')
        
        UNION ALL
        
        -- Peajes
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- AlimentaciÃ³n
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.alimentacionSoles, 0) AS EgresoSoles,
            ISNULL(e.alimentacionDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.alimentacionSoles > 0 OR e.alimentacionDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Apoyo Seguridad
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.apoyoseguridadSoles, 0) AS EgresoSoles,
            ISNULL(e.apoyoseguridadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Reparaciones
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.reparacionesVariosSoles, 0) AS EgresoSoles,
            ISNULL(e.repacionesVariosDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Movilidad
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.movilidadSoles, 0) AS EgresoSoles,
            ISNULL(e.movilidadDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.movilidadSoles > 0 OR e.movilidadDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Hospedaje
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.hospedajeSoles, 0) AS EgresoSoles,
            ISNULL(e.hospedajeDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.hospedajeSoles > 0 OR e.hospedajeDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Combustible
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.combustibleSoles, 0) AS EgresoSoles,
            ISNULL(e.combustibleDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.combustibleSoles > 0 OR e.combustibleDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- Encarpada/Desencarpada
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
        
        UNION ALL
        
        -- CategorÃ­as Adicionales (gastos adicionales)
        SELECT 
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND (@numeroPedido IS NULL OR EXISTS (
                SELECT 1 FROM CPIC cpic 
                JOIN Factura f ON cpic.idFactura = f.idFactura 
                WHERE cpic.idCPIC = ov.idCPIC AND f.numeroPedido = @numeroPedido
              ))
              AND (@numeroPedido IS NOT NULL OR ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta)
              AND (@idCliente IS NULL OR ov.idCliente = @idCliente)
              AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Egreso')
    ) AS Transacciones
    WHERE (@montoMinimo IS NULL OR (IngresoSoles >= @montoMinimo) OR (IngresoDolares >= @montoMinimo) 
           OR (EgresoSoles >= @montoMinimo) OR (EgresoDolares >= @montoMinimo))
          AND (@montoMaximo IS NULL OR (IngresoSoles <= @montoMaximo) OR (IngresoDolares <= @montoMaximo) 
               OR (EgresoSoles <= @montoMaximo) OR (EgresoDolares <= @montoMaximo));
END
GO
