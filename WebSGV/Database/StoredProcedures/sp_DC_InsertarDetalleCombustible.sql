CREATE OR ALTER PROCEDURE sp_DC_InsertarDetalleCombustible
    @numeroOrdenViaje  VARCHAR(50),
    @fechaComprobante  DATE,
    @numeroComprobante VARCHAR(50)   = NULL,
    @montoSoles        DECIMAL(18,2) = 0,
    @montoDolares      DECIMAL(18,2) = 0,
    @observaciones     VARCHAR(250)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DetalleCombustible (
        numeroOrdenViaje, fechaComprobante, numeroComprobante, 
        montoSoles, montoDolares, observaciones, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @fechaComprobante, @numeroComprobante,
        @montoSoles, @montoDolares, @observaciones, GETDATE(), 1
    );
END
