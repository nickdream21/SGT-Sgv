CREATE OR ALTER PROCEDURE sp_DC_ObtenerViajesActivosConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        vp.idViajeProgreso,
        vp.numeroViajeProgreso,
        c.idConductor,
        c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreConductor,
        vp.fechaInicio,
        vp.fechaUltimaActividad,
        vp.estadoViaje,
        ISNULL(vp.descripcionViaje, '') AS descripcionViaje,
        (SELECT COUNT(*) FROM Despachos d2 
         WHERE d2.idViajeProgreso = vp.idViajeProgreso AND d2.activo = 1) AS cantidadDespachos,
        ISNULL(vp.esInternacional, 0) AS esInternacional
    FROM ViajesEnProgreso vp
    CROSS JOIN Conductor c
    WHERE c.idConductor = @idConductor
        AND vp.estadoViaje = 'ABIERTO'
        AND (
            vp.idConductor = @idConductor
            OR EXISTS (
                SELECT 1 FROM Despachos d 
                WHERE d.idViajeProgreso = vp.idViajeProgreso 
                AND d.idConductor = @idConductor 
                AND d.activo = 1
            )
        )
    ORDER BY vp.fechaInicio ASC;
END
