-- @moneda: PEN o USD (migracion 28). Opcional para no romper a quien llame sin ella.
CREATE OR ALTER PROCEDURE sp_CrearFactura
    @numeroFactura  VARCHAR(50),
    @valorTotal     DECIMAL(18,2),
    @fechaEmision   DATE,
    @numeroPedido   VARCHAR(10) = NULL,
    @idCliente      INT,
    @moneda         CHAR(3)     = 'PEN',
    @idFactura      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente, moneda)
    VALUES (@numeroFactura, @valorTotal, @fechaEmision, @numeroPedido, @idCliente, ISNULL(@moneda, 'PEN'));

    SET @idFactura = SCOPE_IDENTITY();
END
