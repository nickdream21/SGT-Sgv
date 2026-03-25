CREATE OR ALTER PROCEDURE sp_ValidarDocumentosDuplicados
    @numeroFactura  VARCHAR(50) = NULL,
    @numeroCPIC     VARCHAR(50) = NULL,
    @facturaExiste  BIT OUTPUT,
    @cpicExiste     BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @facturaExiste = 0;
    SET @cpicExiste    = 0;

    IF @numeroFactura IS NOT NULL
        SELECT @facturaExiste = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
        FROM Factura
        WHERE numeroFactura = @numeroFactura;

    IF @numeroCPIC IS NOT NULL
        SELECT @cpicExiste = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
        FROM CPIC
        WHERE numeroCPIC = @numeroCPIC;
END
