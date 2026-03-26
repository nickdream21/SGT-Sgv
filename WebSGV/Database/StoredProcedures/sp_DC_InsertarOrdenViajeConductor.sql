CREATE OR ALTER PROCEDURE sp_DC_InsertarOrdenViajeConductor
    @numeroOrdenViaje  VARCHAR(50),
    @fechaSalida       DATE,
    @horaSalida        TIME          = NULL,
    @fechaLlegada      DATE,
    @horaLlegada       TIME          = NULL,
    @idConductor       INT,
    @idTracto          INT,
    @idCarreta         INT,
    @observaciones     VARCHAR(250)  = NULL,
    @idViajeProgreso   INT,
    @idUsuarioRegistro INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO OrdenViaje (
        numeroOrdenViaje, fechaSalida, horaSalida, fechaLlegada, horaLlegada, 
        idConductor, idTracto, idCarreta, observaciones, 
        estadoViaje, tipoViaje, idViajeProgreso,
        registradoPor, idUsuarioRegistro, estadoAprobacion, fechaRegistro
    ) 
    VALUES (
        @numeroOrdenViaje, @fechaSalida, @horaSalida, @fechaLlegada, @horaLlegada, 
        @idConductor, @idTracto, @idCarreta, @observaciones, 
        'PENDIENTE', 'NACIONAL', @idViajeProgreso,
        'CONDUCTOR', @idUsuarioRegistro, 'PENDIENTE', GETDATE()
    );

    SELECT SCOPE_IDENTITY() AS idOrdenViaje;
END
