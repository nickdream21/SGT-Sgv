-- Al reenviar una orden rechazada se borran sus datos financieros y se vuelven a insertar: los
-- motivos del descuento/reintegro de la administración viajan aquí para no perderse. Son opcionales
-- (NULL por defecto) para que el código anterior, que no los manda, siga funcionando igual.
CREATE OR ALTER PROCEDURE sp_DC_InsertarDescuentosReintegros
    @numeroOrdenViaje        VARCHAR(50),
    @descuentoSoles          DECIMAL(18,2) = 0,
    @descuentoDolares        DECIMAL(18,2) = 0,
    @reintegroSoles          DECIMAL(18,2) = 0,
    @reintegroDolares        DECIMAL(18,2) = 0,
    @observacionesDescuento  VARCHAR(500)  = NULL,
    @observacionesReintegro  VARCHAR(500)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO DescuentosReintegros (
        numeroOrdenViaje, descuentoSoles, descuentoDolares,
        reintegroSoles, reintegroDolares,
        observacionesDescuento, observacionesReintegro, fechaCreacion, activo
    )
    VALUES (
        @numeroOrdenViaje, @descuentoSoles, @descuentoDolares,
        @reintegroSoles, @reintegroDolares,
        @observacionesDescuento, @observacionesReintegro, dbo.fn_AhoraPeru(), 1
    );
END
