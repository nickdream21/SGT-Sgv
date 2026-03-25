CREATE OR ALTER PROCEDURE sp_CrearCPIC
    @numeroCPIC         VARCHAR(50),
    @idFactura          INT = NULL,
    @valorTotalFlete    DECIMAL(18,2),
    @fechaEmision       DATE,
    @idCPIC             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto)
    VALUES (@numeroCPIC, @idFactura, @valorTotalFlete, @fechaEmision, 0, 0);

    SET @idCPIC = SCOPE_IDENTITY();
END
