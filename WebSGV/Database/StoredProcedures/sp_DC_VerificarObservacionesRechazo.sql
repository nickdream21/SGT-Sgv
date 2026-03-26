CREATE OR ALTER PROCEDURE sp_DC_VerificarObservacionesRechazo
    @idsViajes VARCHAR(MAX)  -- CSV de IDs de viajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        ov.numeroOrdenViaje,
        ov.observacionesRechazo,
        ov.fechaRechazo,
        u.nombre + ' ' + u.apellido AS rechazadoPor
    FROM OrdenViaje ov
    LEFT JOIN Usuarios u ON ov.idUsuarioAprobacion = u.idUsuario
    WHERE ov.idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND ov.estadoAprobacion = 'REABIERTO'
        AND ov.observacionesRechazo IS NOT NULL
        AND ov.observacionesRechazo != ''
    ORDER BY ov.fechaRechazo DESC;
END
