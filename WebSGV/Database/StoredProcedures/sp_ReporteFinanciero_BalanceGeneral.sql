-- ============================================================
-- sp_ReporteFinanciero_BalanceGeneral
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteFinanciero_BalanceGeneral]
    @fechaDesde DATETIME,
    @fechaHasta DATETIME,
    @tipoTransaccion VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Variables para verificación de datos
    DECLARE @hayDatos BIT = 0;
    
    -- Verificar si hay datos según el tipo de transacción
    IF @tipoTransaccion = 'Solo Ingresos'
    BEGIN
        IF EXISTS (
            SELECT 1 
            FROM (
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
                      i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
                      i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
                      i.otrosSoles > 0 OR i.otrosDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE (ia.soles > 0 OR ia.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            ) AS Ingresos
        )
        BEGIN
            SET @hayDatos = 1;
        END
    END
    ELSE IF @tipoTransaccion = 'Solo Egresos'
    BEGIN
        IF EXISTS (
            SELECT 1 
            FROM (
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                        e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                        e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                        e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                        e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                        e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                        e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                        e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE (ca.soles > 0 OR ca.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            ) AS Egresos
        )
        BEGIN
            SET @hayDatos = 1;
        END
    END
    ELSE -- Todas las transacciones
    BEGIN
        IF EXISTS (
            SELECT 1 
            FROM (
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
                      i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
                      i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR
                      i.otrosSoles > 0 OR i.otrosDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE (ia.soles > 0 OR ia.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                        e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                        e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                        e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                        e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                        e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                        e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                        e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                
                UNION
                
                SELECT 1 AS Existe
                FROM OrdenViaje ov 
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE (ca.soles > 0 OR ca.dolares > 0)
                AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            ) AS Transacciones
        )
        BEGIN
            SET @hayDatos = 1;
        END
    END
    
    -- Si no hay datos, devolver estructura vacía
    IF @hayDatos = 0
    BEGIN
        -- Resultado 1: Tabla vacía con estructura correcta
        SELECT 
            CAST(NULL AS VARCHAR(50)) AS NroOrdenViaje,
            CAST(NULL AS VARCHAR(50)) AS NumeroPedido,
            CAST(NULL AS DATETIME) AS FechaTransaccion,
            CAST(NULL AS VARCHAR(20)) AS TipoTransaccion,
            CAST(NULL AS VARCHAR(50)) AS Concepto,
            CAST(NULL AS VARCHAR(100)) AS Conductor,
            CAST(NULL AS VARCHAR(100)) AS Cliente,
            CAST(0 AS DECIMAL(18,2)) AS IngresoSoles,
            CAST(0 AS DECIMAL(18,2)) AS IngresoDolares,
            CAST(0 AS DECIMAL(18,2)) AS EgresoSoles,
            CAST(0 AS DECIMAL(18,2)) AS EgresoDolares,
            CAST(NULL AS VARCHAR(250)) AS Observaciones
        WHERE 1 = 0; -- No devuelve filas pero mantiene la estructura
        
        -- Resultado 2: Indicadores en cero
        SELECT 
            0 AS TotalIngresosSoles,
            0 AS TotalIngresosDolares,
            0 AS TotalEgresosSoles,
            0 AS TotalEgresosDolares,
            0 AS BalanceSoles,
            0 AS BalanceDolares,
            0 AS TotalRegistros;
            
        RETURN;
    END
    
    -- Si hay datos, procesamos los resultados normalmente:
    
    -- Resultado 1: Datos principales del reporte según el filtro seleccionado
    IF @tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = ''
    BEGIN
        -- Ingresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            ia.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales (Egresos)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Ingresos'
    BEGIN
        -- Ingresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna 
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            CASE 
                WHEN i.despachoSoles > 0 OR i.despachoDolares > 0 THEN 'Despacho'
                WHEN i.prestamoSoles > 0 OR i.prestamosDolares > 0 THEN 'Préstamo'
                WHEN i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 THEN 'Mensualidad'
                WHEN i.otrosSoles > 0 OR i.otrosDolares > 0 THEN 'Otros'
                ELSE 'No especificado'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS IngresoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            CASE
                WHEN i.despachoSoles > 0 THEN i.descDespacho
                WHEN i.prestamoSoles > 0 THEN i.descPrestamo
                WHEN i.mensualidadSoles > 0 THEN i.descMensualidad
                WHEN i.otrosSoles > 0 THEN i.descOtrosAutorizados
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR 
              i.prestamoSoles > 0 OR i.prestamosDolares > 0 OR 
              i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR 
              i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ia.soles, 0) AS IngresoSoles,
            ISNULL(ia.dolares, 0) AS IngresoDolares,
            0 AS EgresoSoles,
            0 AS EgresoDolares,
            ia.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END
    ELSE IF @tipoTransaccion = 'Solo Egresos'
    BEGIN
        -- Solo Egresos (combinando egresos regulares y categorías adicionales)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna 
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            CASE 
                WHEN e.peajesSoles > 0 OR e.peajesDolares > 0 THEN 'Peaje'
                WHEN e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 THEN 'Alimentación'
                WHEN e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 THEN 'Apoyo Seguridad'
                WHEN e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 THEN 'Reparaciones'
                WHEN e.movilidadSoles > 0 OR e.movilidadDolares > 0 THEN 'Movilidad'
                WHEN e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 THEN 'Hospedaje'
                WHEN e.combustibleSoles > 0 OR e.combustibleDolares > 0 THEN 'Combustible'
                WHEN e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0 THEN 'Encarpada/Desencarpada'
                ELSE 'Otros gastos'
            END AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS EgresoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS EgresoDolares,
            CASE
                WHEN e.peajesSoles > 0 THEN e.descPeajes
                WHEN e.alimentacionSoles > 0 THEN e.descAlimentacion
                WHEN e.apoyoseguridadSoles > 0 THEN e.descApoyoSeguridad
                WHEN e.reparacionesVariosSoles > 0 THEN e.descReparacionesVarios
                WHEN e.movilidadSoles > 0 THEN e.descMovilidad
                WHEN e.hospedajeSoles > 0 THEN e.descHospedaje
                WHEN e.combustibleSoles > 0 THEN e.descCombustible
                WHEN e.encarpada_desencarpadaSoles > 0 THEN e.descEncarpadaDesencarpada
                ELSE NULL
            END AS Observaciones
        FROM OrdenViaje ov
        JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR 
                e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
                e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR 
                e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0 OR 
                e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
                e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR 
                e.combustibleSoles > 0 OR e.combustibleDolares > 0 OR 
                e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        UNION ALL
        
        -- Categorías Adicionales (usar la misma estructura exacta de columnas)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje, -- Nombre consistente de la columna
            f.numeroPedido AS NumeroPedido,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            0 AS IngresoSoles,
            0 AS IngresoDolares,
            ISNULL(ca.soles, 0) AS EgresoSoles,
            ISNULL(ca.dolares, 0) AS EgresoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN CPIC cpic ON ov.idCPIC = cpic.idCPIC
        LEFT JOIN Factura f ON cpic.idFactura = f.idFactura
        LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        
        ORDER BY FechaTransaccion DESC, TipoTransaccion;
    END;

    -- Resultado 2: Indicadores calculados
    SELECT 
        -- Total Ingresos (regulares)
        ISNULL((
            SELECT SUM(ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
                         ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0))
            FROM OrdenViaje ov
            JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) +
        -- Total Ingresos adicionales
        ISNULL((
            SELECT SUM(ISNULL(ia.soles, 0))
            FROM OrdenViaje ov
            JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) AS TotalIngresosSoles,
        
        -- Total Ingresos (regulares) en dólares
        ISNULL((
            SELECT SUM(ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
                         ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0))
            FROM OrdenViaje ov
            JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) +
        -- Total Ingresos adicionales en dólares
        ISNULL((
            SELECT SUM(ISNULL(ia.dolares, 0))
            FROM OrdenViaje ov
            JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
        ), 0) AS TotalIngresosDolares,
        
        -- Total Egresos (regulares)
        ISNULL((
            SELECT SUM(ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
                         ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
                         ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
                         ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0))
            FROM OrdenViaje ov
            JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) + 
        -- Total Egresos adicionales
        ISNULL((
            SELECT SUM(ISNULL(ca.soles, 0))
            FROM OrdenViaje ov
            JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) AS TotalEgresosSoles,
        
        -- Total Egresos (regulares) en dólares
        ISNULL((
            SELECT SUM(ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
                         ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
                         ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
                         ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0))
            FROM OrdenViaje ov
            JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) + 
        -- Total Egresos adicionales en dólares
        ISNULL((
            SELECT SUM(ISNULL(ca.dolares, 0))
            FROM OrdenViaje ov
            JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
            WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
            AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
        ), 0) AS TotalEgresosDolares,
        
        -- Balance Soles
        (
            -- Total Ingresos en soles
            ISNULL((
                SELECT SUM(ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
                             ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0))
                FROM OrdenViaje ov
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0) +
            -- Ingresos adicionales en soles
            ISNULL((
                SELECT SUM(ISNULL(ia.soles, 0))
                FROM OrdenViaje ov
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0)
        ) - 
        (
            -- Total Egresos en soles
            ISNULL((
                SELECT SUM(ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
                             ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
                             ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
                             ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0))
                FROM OrdenViaje ov
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0) + 
            -- Egresos adicionales en soles
            ISNULL((
                SELECT SUM(ISNULL(ca.soles, 0))
                FROM OrdenViaje ov
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0)
        ) AS BalanceSoles,
        
        -- Balance Dólares
        (
            -- Total Ingresos en dólares
            ISNULL((
                SELECT SUM(ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
                             ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0))
                FROM OrdenViaje ov
                JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0) +
            -- Ingresos adicionales en dólares
            ISNULL((
                SELECT SUM(ISNULL(ia.dolares, 0))
                FROM OrdenViaje ov
                JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
            ), 0)
        ) - 
        (
            -- Total Egresos en dólares
            ISNULL((
                SELECT SUM(ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
                             ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
                             ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
                             ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0))
                FROM OrdenViaje ov
                JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0) + 
            -- Egresos adicionales en dólares
            ISNULL((
                SELECT SUM(ISNULL(ca.dolares, 0))
                FROM OrdenViaje ov
                JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
                WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
                AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
            ), 0)
        ) AS BalanceDolares,
        
        -- Total registros
        (SELECT COUNT(*) 
         FROM (
             -- Ingresos regulares
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
             
             UNION
             
             -- Ingresos adicionales
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Ingresos')
             
             UNION
             
             -- Egresos regulares
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
             
             UNION
             
             -- Egresos adicionales
             SELECT ov.numeroOrdenViaje 
             FROM OrdenViaje ov 
             JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
             WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
             AND (@tipoTransaccion IS NULL OR @tipoTransaccion = 'Todas' OR @tipoTransaccion = '' OR @tipoTransaccion = 'Solo Egresos')
         ) AS Transacciones
        ) AS TotalRegistros;
END
GO
