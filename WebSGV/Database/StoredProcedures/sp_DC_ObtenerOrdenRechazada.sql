CREATE OR ALTER PROCEDURE sp_DC_ObtenerOrdenRechazada
    @idsViajes VARCHAR(MAX)  -- CSV de IDs de viajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1 
        numeroOrdenViaje, 
        fechaSalida, 
        fechaLlegada, 
        horaSalida, 
        horaLlegada, 
        observaciones
    FROM OrdenViaje
    WHERE idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND estadoAprobacion = 'REABIERTO'
        AND registradoPor = 'CONDUCTOR'
    ORDER BY fechaRegistro DESC;
END
