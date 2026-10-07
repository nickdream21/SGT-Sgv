-- ============================================================
-- sp_ReporteFinancieroConductor
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteFinancieroConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL,
    @dniConductor VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(50) = NULL,
    @tipoTransaccion VARCHAR(50) = NULL,
    @montoMinimo DECIMAL(18,2) = NULL,
    @montoMaximo DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para las transacciones financieras
    WITH ResultadosCombinados AS (
        -- Ingresos regulares
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Despacho' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.despachoSoles, 0) AS MontoSoles,
            ISNULL(i.despachoDolares, 0) AS MontoDolares,
            i.descDespacho AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Préstamo' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.prestamoSoles, 0) AS MontoSoles,
            ISNULL(i.prestamosDolares, 0) AS MontoDolares,
            i.descPrestamo AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.prestamoSoles > 0 OR i.prestamosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Mensualidad' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.mensualidadSoles, 0) AS MontoSoles,
            ISNULL(i.mensualidadDolares, 0) AS MontoDolares,
            i.descMensualidad AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.mensualidadSoles > 0 OR i.mensualidadDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            'Otros' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(i.otrosSoles, 0) AS MontoSoles,
            ISNULL(i.otrosDolares, 0) AS MontoDolares,
            i.descOtrosAutorizados AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Ingreso' AS TipoTransaccion,
            ia.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ia.soles, 0) AS MontoSoles,
            ISNULL(ia.dolares, 0) AS MontoDolares,
            ia.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos regulares separados por cada tipo
        -- Peajes
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Peaje' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.peajesSoles, 0) AS MontoSoles,
            ISNULL(e.peajesDolares, 0) AS MontoDolares,
            e.descPeajes AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Alimentación
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Alimentación' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.alimentacionSoles, 0) AS MontoSoles,
            ISNULL(e.alimentacionDolares, 0) AS MontoDolares,
            e.descAlimentacion AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.alimentacionSoles > 0 OR e.alimentacionDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Apoyo Seguridad
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Apoyo Seguridad' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.apoyoseguridadSoles, 0) AS MontoSoles,
            ISNULL(e.apoyoseguridadDolares, 0) AS MontoDolares,
            e.descApoyoSeguridad AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Reparaciones Varios
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Reparaciones' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.reparacionesVariosSoles, 0) AS MontoSoles,
            ISNULL(e.repacionesVariosDolares, 0) AS MontoDolares,
            e.descReparacionesVarios AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.reparacionesVariosSoles > 0 OR e.repacionesVariosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Movilidad
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Movilidad' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.movilidadSoles, 0) AS MontoSoles,
            ISNULL(e.movilidadDolares, 0) AS MontoDolares,
            e.descMovilidad AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.movilidadSoles > 0 OR e.movilidadDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Hospedaje
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Hospedaje' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.hospedajeSoles, 0) AS MontoSoles,
            ISNULL(e.hospedajeDolares, 0) AS MontoDolares,
            e.descHospedaje AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.hospedajeSoles > 0 OR e.hospedajeDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Combustible
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Combustible' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.combustibleSoles, 0) AS MontoSoles,
            ISNULL(e.combustibleDolares, 0) AS MontoDolares,
            e.descCombustible AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.combustibleSoles > 0 OR e.combustibleDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Encarpada/Desencarpada
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            'Encarpada/Desencarpada' AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(e.encarpada_desencarpadaSoles, 0) AS MontoSoles,
            ISNULL(e.encarpada_desencarpadaDolares, 0) AS MontoDolares,
            e.descEncarpadaDesencarpada AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos adicionales (Categorías Adicionales)
        SELECT 
            ov.numeroOrdenViaje AS NroOrdenViaje,
            ov.fechaSalida AS FechaTransaccion,
            'Egreso' AS TipoTransaccion,
            ca.nombreCategoria AS Concepto,
            CONCAT(c.nombre, ' ', c.apPaterno, ' ', c.apMaterno) AS Conductor,
            cl.nombre AS Cliente,
            ISNULL(ca.soles, 0) AS MontoSoles,
            ISNULL(ca.dolares, 0) AS MontoDolares,
            ca.descripcion AS Observaciones
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        LEFT JOIN Cliente cl ON ov.idCliente = cl.idCliente
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
    )
    -- Aplicar filtros adicionales
    SELECT * FROM ResultadosCombinados
    WHERE (@tipoTransaccion IS NULL OR TipoTransaccion = @tipoTransaccion)
      AND (@montoMinimo IS NULL OR (MontoSoles >= @montoMinimo OR MontoDolares >= @montoMinimo))
      AND (@montoMaximo IS NULL OR (MontoSoles <= @montoMaximo OR MontoDolares <= @montoMaximo))
    ORDER BY FechaTransaccion DESC, TipoTransaccion, Concepto;
    
    -- Cálculo de indicadores - Resultados agregados
    WITH ResultadosCombinados AS (
        -- Ingresos regulares
        SELECT 
            'Ingreso' AS TipoTransaccion,
            ISNULL(i.despachoSoles, 0) + ISNULL(i.prestamoSoles, 0) + 
            ISNULL(i.mensualidadSoles, 0) + ISNULL(i.otrosSoles, 0) AS MontoSoles,
            ISNULL(i.despachoDolares, 0) + ISNULL(i.prestamosDolares, 0) + 
            ISNULL(i.mensualidadDolares, 0) + ISNULL(i.otrosDolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Ingresos i ON ov.numeroOrdenViaje = i.numeroOrdenViaje
        WHERE (i.despachoSoles > 0 OR i.despachoDolares > 0 OR i.prestamoSoles > 0 OR 
               i.prestamosDolares > 0 OR i.mensualidadSoles > 0 OR i.mensualidadDolares > 0 OR
               i.otrosSoles > 0 OR i.otrosDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Ingresos adicionales
        SELECT 
            'Ingreso' AS TipoTransaccion,
            ISNULL(ia.soles, 0) AS MontoSoles,
            ISNULL(ia.dolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN IngresosAdicionales ia ON ov.numeroOrdenViaje = ia.numeroOrdenViaje
        WHERE (ia.soles > 0 OR ia.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos regulares
        SELECT 
            'Egreso' AS TipoTransaccion,
            ISNULL(e.peajesSoles, 0) + ISNULL(e.alimentacionSoles, 0) + 
            ISNULL(e.apoyoseguridadSoles, 0) + ISNULL(e.reparacionesVariosSoles, 0) + 
            ISNULL(e.movilidadSoles, 0) + ISNULL(e.hospedajeSoles, 0) + 
            ISNULL(e.combustibleSoles, 0) + ISNULL(e.encarpada_desencarpadaSoles, 0) AS MontoSoles,
            ISNULL(e.peajesDolares, 0) + ISNULL(e.alimentacionDolares, 0) + 
            ISNULL(e.apoyoseguridadDolares, 0) + ISNULL(e.repacionesVariosDolares, 0) + 
            ISNULL(e.movilidadDolares, 0) + ISNULL(e.hospedajeDolares, 0) + 
            ISNULL(e.combustibleDolares, 0) + ISNULL(e.encarpada_desencarpadaDolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        LEFT JOIN Egresos e ON ov.numeroOrdenViaje = e.numeroOrdenViaje
        WHERE (e.peajesSoles > 0 OR e.peajesDolares > 0 OR e.alimentacionSoles > 0 OR e.alimentacionDolares > 0 OR 
               e.apoyoseguridadSoles > 0 OR e.apoyoseguridadDolares > 0 OR e.reparacionesVariosSoles > 0 OR 
               e.repacionesVariosDolares > 0 OR e.movilidadSoles > 0 OR e.movilidadDolares > 0 OR 
               e.hospedajeSoles > 0 OR e.hospedajeDolares > 0 OR e.combustibleSoles > 0 OR 
               e.combustibleDolares > 0 OR e.encarpada_desencarpadaSoles > 0 OR e.encarpada_desencarpadaDolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
        
        UNION ALL
        
        -- Egresos adicionales (Categorías Adicionales)
        SELECT 
            'Egreso' AS TipoTransaccion,
            ISNULL(ca.soles, 0) AS MontoSoles,
            ISNULL(ca.dolares, 0) AS MontoDolares
        FROM OrdenViaje ov
        JOIN Conductor c ON ov.idConductor = c.idConductor
        JOIN CategoriasAdicionales ca ON ov.numeroOrdenViaje = ca.numeroOrdenViaje
        WHERE (ca.soles > 0 OR ca.dolares > 0)
        AND ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
    )
    
    -- Cálculo de totales por tipo de transacción
    SELECT
        SUM(CASE WHEN TipoTransaccion = 'Ingreso' THEN MontoSoles ELSE 0 END) AS TotalIngresosSoles,
        SUM(CASE WHEN TipoTransaccion = 'Ingreso' THEN MontoDolares ELSE 0 END) AS TotalIngresosDolares,
        SUM(CASE WHEN TipoTransaccion = 'Egreso' THEN MontoSoles ELSE 0 END) AS TotalEgresosSoles,
        SUM(CASE WHEN TipoTransaccion = 'Egreso' THEN MontoDolares ELSE 0 END) AS TotalEgresosDolares,
        COUNT(CASE WHEN TipoTransaccion = 'Ingreso' THEN 1 END) AS ContadorIngresos,
        COUNT(CASE WHEN TipoTransaccion = 'Egreso' THEN 1 END) AS ContadorEgresos
    FROM ResultadosCombinados
    WHERE (@tipoTransaccion IS NULL OR TipoTransaccion = @tipoTransaccion)
      AND (@montoMinimo IS NULL OR (MontoSoles >= @montoMinimo OR MontoDolares >= @montoMinimo))
      AND (@montoMaximo IS NULL OR (MontoSoles <= @montoMaximo OR MontoDolares <= @montoMaximo));
END
GO
