CREATE OR ALTER PROCEDURE sp_CrearFactura
    @numeroFactura  VARCHAR(50),
    @valorTotal     DECIMAL(18,2),
    @fechaEmision   DATE,
    @numeroPedido   VARCHAR(10) = NULL,
    @idCliente      INT,
    @idFactura      INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente)
    VALUES (@numeroFactura, @valorTotal, @fechaEmision, @numeroPedido, @idCliente);

    SET @idFactura = SCOPE_IDENTITY();
END
