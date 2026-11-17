-- ============================================================
-- Crea una factura (RegistroDespacho, "Finalizar lote"; bandeja de documentos).
-- @moneda: PEN o USD (migracion 28). Opcional para no romper a quien llame sin ella.
-- La factura es única por número y cliente (migración 49, 2026-11-17): si ese
-- cliente ya la tiene, THROW 50022 con un mensaje claro en vez del error del
-- índice único.
-- ============================================================
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

    SET @numeroFactura = LTRIM(RTRIM(@numeroFactura));

    IF EXISTS (SELECT 1 FROM Factura WITH (UPDLOCK, HOLDLOCK) WHERE numeroFactura = @numeroFactura AND idCliente = @idCliente)
    BEGIN
        DECLARE @msg NVARCHAR(400) = CONCAT(N'La factura ', @numeroFactura, N' ya está registrada para este cliente.');
        THROW 50022, @msg, 1;
    END;

    INSERT INTO Factura (numeroFactura, valorTotal, fechaEmision, numeroPedido, idCliente, moneda)
    VALUES (@numeroFactura, @valorTotal, @fechaEmision, @numeroPedido, @idCliente, ISNULL(@moneda, 'PEN'));

    SET @idFactura = SCOPE_IDENTITY();
END
