-- ============================================================
-- sp_ReporteViajesConductor
-- Extraido de la BD (sgvActualizada, espejo del esquema de produccion) el 2026-10-07:
-- antes solo existia en la base de datos o en el volcado docs/migracion_somee.
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_ReporteViajesConductor]
    @fechaDesde DATE,
    @fechaHasta DATE,
    @idConductor VARCHAR(50) = NULL,
    @dniConductor VARCHAR(50) = NULL,
    @nombreConductor VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Consulta principal para datos de viajes
    SELECT 
        ov.numeroOrdenViaje AS NroOrdenViaje,
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
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%')
    ORDER BY ov.fechaSalida DESC, ov.horaSalida DESC;

    -- CÃ¡lculo de indicadores financieros
    SELECT
        SUM(ISNULL(ing.despachoSoles, 0) + ISNULL(ing.prestamoSoles, 0) + ISNULL(ing.mensualidadSoles, 0) + ISNULL(ing.otrosSoles, 0) +
            ISNULL(ing.despachoDolares, 0) + ISNULL(ing.prestamosDolares, 0) + ISNULL(ing.mensualidadDolares, 0) + ISNULL(ing.otrosDolares, 0)) AS TotalIngresos,
            
        SUM(ISNULL(eg.peajesSoles, 0) + ISNULL(eg.peajesDolares, 0) + 
            ISNULL(eg.alimentacionSoles, 0) + ISNULL(eg.alimentacionDolares, 0) +
            ISNULL(eg.apoyoseguridadSoles, 0) + ISNULL(eg.apoyoseguridadDolares, 0) + 
            ISNULL(eg.reparacionesVariosSoles, 0) + ISNULL(eg.repacionesVariosDolares, 0) + 
            ISNULL(eg.movilidadSoles, 0) + ISNULL(eg.movilidadDolares, 0) + 
            ISNULL(eg.hospedajeSoles, 0) + ISNULL(eg.hospedajeDolares, 0) + 
            ISNULL(eg.combustibleSoles, 0) + ISNULL(eg.combustibleDolares, 0) + 
            ISNULL(eg.encarpada_desencarpadaSoles, 0) + ISNULL(eg.encarpada_desencarpadaDolares, 0)) + 
        SUM(ISNULL(ca.soles, 0) + ISNULL(ca.dolares, 0)) AS TotalEgresos,
            
        SUM(ISNULL(ac.galonesTotalConsumidos, 0)) AS TotalGalones
    FROM OrdenViaje ov
    LEFT JOIN Conductor c ON ov.idConductor = c.idConductor
    LEFT JOIN Ingresos ing ON ing.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN Egresos eg ON eg.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN CategoriasAdicionales ca ON ca.numeroOrdenViaje = ov.numeroOrdenViaje
    LEFT JOIN AbastecimientoCombustible ac ON ac.idOrdenViaje = ov.idOrdenViaje
    WHERE ov.fechaSalida BETWEEN @fechaDesde AND @fechaHasta
        AND (@idConductor IS NULL OR c.idConductor = @idConductor)
        AND (@dniConductor IS NULL OR c.DNI LIKE '%' + @dniConductor + '%')
        AND (@nombreConductor IS NULL OR c.nombre LIKE '%' + @nombreConductor + '%' 
             OR c.apPaterno LIKE '%' + @nombreConductor + '%' 
             OR c.apMaterno LIKE '%' + @nombreConductor + '%');
END
GO
