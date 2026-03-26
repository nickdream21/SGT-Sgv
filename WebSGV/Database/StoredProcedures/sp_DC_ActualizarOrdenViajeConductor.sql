CREATE OR ALTER PROCEDURE sp_DC_ActualizarOrdenViajeConductor
    @numeroOrdenViaje VARCHAR(50),
    @fechaSalida      DATE,
    @horaSalida       TIME          = NULL,
    @fechaLlegada     DATE,
    @horaLlegada      TIME          = NULL,
    @observaciones    VARCHAR(250)  = NULL,
    @idUsuarioRegistro INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE OrdenViaje SET
        fechaSalida       = @fechaSalida,
        horaSalida        = @horaSalida,
        fechaLlegada      = @fechaLlegada,
        horaLlegada       = @horaLlegada,
        observaciones     = @observaciones,
        estadoAprobacion  = 'PENDIENTE',
        idUsuarioRegistro = @idUsuarioRegistro,
        fechaRegistro     = GETDATE()
    WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
