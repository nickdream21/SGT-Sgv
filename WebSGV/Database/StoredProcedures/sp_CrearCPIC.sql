-- ============================================================
-- Crea una CPIC (RegistroDespacho, "Finalizar lote").
-- La CPIC es única por número (migración 49, 2026-11-17): si ya existe,
-- THROW 50021 con un mensaje claro en vez del error del índice único.
-- Una factura no tiene dos CPIC: si @idFactura ya tiene una, THROW 50019.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_CrearCPIC
    @numeroCPIC         VARCHAR(50),
    @idFactura          INT = NULL,
    @valorTotalFlete    DECIMAL(18,2),
    @fechaEmision       DATE,
    @idCPIC             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @numeroCPIC = LTRIM(RTRIM(@numeroCPIC));
    DECLARE @msg NVARCHAR(400);

    IF EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE numeroCPIC = @numeroCPIC)
    BEGIN
        SET @msg = CONCAT(N'La CPIC ', @numeroCPIC, N' ya está registrada en el sistema.');
        THROW 50021, @msg, 1;
    END;

    IF @idFactura IS NOT NULL AND EXISTS (SELECT 1 FROM CPIC WITH (UPDLOCK, HOLDLOCK) WHERE idFactura = @idFactura)
    BEGIN
        SELECT TOP 1 @msg = CONCAT(N'La factura ', f.numeroFactura, N' ya tiene la CPIC ', c.numeroCPIC, N'.')
        FROM CPIC c JOIN Factura f ON f.idFactura = c.idFactura WHERE c.idFactura = @idFactura ORDER BY c.idCPIC;
        THROW 50019, @msg, 1;
    END;

    INSERT INTO CPIC (numeroCPIC, idFactura, valorTotalFlete, fechaEmision, pesoNeto, pesoBruto)
    VALUES (@numeroCPIC, @idFactura, @valorTotalFlete, @fechaEmision, 0, 0);

    SET @idCPIC = SCOPE_IDENTITY();
END
