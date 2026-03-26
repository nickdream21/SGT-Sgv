CREATE OR ALTER PROCEDURE sp_DC_ObtenerHistorialLiquidaciones
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        ov.idOrdenViaje,
        ov.numeroOrdenViaje,
        ov.fechaSalida,
        ov.fechaLlegada,
        ISNULL(ov.estadoAprobacion, 'PENDIENTE') AS estadoAprobacion,
        ISNULL(ing.totalSoles, 0) AS totalIngresosSoles,
        ISNULL(ing.totalDolares, 0) AS totalIngresosDolares,
        (ISNULL(eg.peajesSoles, 0) + ISNULL(eg.alimentacionSoles, 0) + 
         ISNULL(eg.apoyoseguridadSoles, 0) + ISNULL(eg.reparacionesVariosSoles, 0) + 
         ISNULL(eg.movilidadSoles, 0) + ISNULL(eg.encarpada_desencarpadaSoles, 0) + 
         ISNULL(eg.hospedajeSoles, 0) + ISNULL(eg.combustibleSoles, 0)) AS totalGastosSoles,
        (ISNULL(eg.peajesDolares, 0) + ISNULL(eg.alimentacionDolares, 0) + 
         ISNULL(eg.apoyoseguridadDolares, 0) + ISNULL(eg.repacionesVariosDolares, 0) + 
         ISNULL(eg.movilidadDolares, 0) + ISNULL(eg.encarpada_desencarpadaDolares, 0) + 
         ISNULL(eg.hospedajeDolares, 0) + ISNULL(eg.combustibleDolares, 0)) AS totalGastosDolares
    FROM OrdenViaje ov
    LEFT JOIN Ingresos ing ON ov.numeroOrdenViaje = ing.numeroOrdenViaje
    LEFT JOIN Egresos eg ON ov.numeroOrdenViaje = eg.numeroOrdenViaje
    WHERE ov.idConductor = @idConductor
        AND ov.registradoPor = 'CONDUCTOR'
    ORDER BY ov.fechaRegistro DESC;
END
