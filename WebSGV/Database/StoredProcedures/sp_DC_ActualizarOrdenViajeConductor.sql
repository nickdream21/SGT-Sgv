CREATE OR ALTER PROCEDURE sp_DC_ActualizarOrdenViajeConductor
    @numeroOrdenViaje VARCHAR(50),
    @fechaSalida      DATE,
    @horaSalida       TIME          = NULL,
    @fechaLlegada     DATE,
    @horaLlegada      TIME          = NULL,
    @horaLlegadaDeclarada TIME      = NULL,
    @observaciones    VARCHAR(1000) = NULL,   -- 1000: lo que admite el formulario (con 250 se cortaba sin aviso)
    @idUsuarioRegistro INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE OrdenViaje SET
        fechaSalida       = @fechaSalida,
        horaSalida        = @horaSalida,
        fechaLlegada      = @fechaLlegada,
        horaLlegada       = @horaLlegada,
        horaLlegadaDeclarada = @horaLlegadaDeclarada,
        observaciones     = @observaciones,
        estadoAprobacion  = 'PENDIENTE',
        idUsuarioRegistro = @idUsuarioRegistro,
        fechaRegistro     = dbo.fn_AhoraPeru()
    WHERE numeroOrdenViaje = @numeroOrdenViaje;
END
