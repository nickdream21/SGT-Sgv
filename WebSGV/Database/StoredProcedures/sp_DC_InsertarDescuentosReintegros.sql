CREATE OR ALTER PROCEDURE sp_DC_InsertarDescuentosReintegros
    @numeroOrdenViaje  VARCHAR(50),
    @descuentoSoles    DECIMAL(18,2) = 0,
    @descuentoDolares  DECIMAL(18,2) = 0,
    @reintegroSoles    DECIMAL(18,2) = 0,
    @reintegroDolares  DECIMAL(18,2) = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DescuentosReintegros (
        numeroOrdenViaje, descuentoSoles, descuentoDolares, 
        reintegroSoles, reintegroDolares, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @descuentoSoles, @descuentoDolares,
        @reintegroSoles, @reintegroDolares, GETDATE(), 1
    );
END
