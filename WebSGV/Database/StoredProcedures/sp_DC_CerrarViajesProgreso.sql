CREATE OR ALTER PROCEDURE sp_DC_CerrarViajesProgreso
    @idsViajes VARCHAR(MAX),  -- CSV de IDs
    @filasViajesCerrados INT OUTPUT,
    @filasDespachosCerrados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Cerrar viajes en progreso
    UPDATE ViajesEnProgreso 
    SET estadoViaje = 'CERRADO',
        fechaCierre = GETDATE()
    WHERE idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND estadoViaje = 'ABIERTO';

    SET @filasViajesCerrados = @@ROWCOUNT;

    -- Marcar despachos como COMPLETADO
    UPDATE Despachos 
    SET estadoDespacho = 'COMPLETADO',
        fechaModificacion = GETDATE()
    WHERE idViajeProgreso IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsViajes, ','))
        AND activo = 1;

    SET @filasDespachosCerrados = @@ROWCOUNT;
END
