CREATE OR ALTER PROCEDURE sp_DC_InsertarDetallePeaje
    @numeroOrdenViaje  VARCHAR(50),
    @estacion          VARCHAR(100),
    @fecha             DATE,
    @numeroComprobante VARCHAR(50)   = NULL,
    @montoSoles        DECIMAL(18,2) = 0,
    @montoDolares      DECIMAL(18,2) = 0,
    @observaciones     VARCHAR(250)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DetallePeajes (
        numeroOrdenViaje, estacion, fecha, numeroComprobante, 
        montoSoles, montoDolares, observaciones, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @estacion, @fecha, @numeroComprobante,
        @montoSoles, @montoDolares, @observaciones, GETDATE(), 1
    );
END
