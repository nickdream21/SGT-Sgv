-- ============================================================
-- ¿Ya existen la factura o la CPIC? ("Iniciar lote" y "Finalizar lote" de
-- RegistroDespacho, que no reutilizan documentos existentes.)
-- La CPIC se compara por número; la factura por número y, si se indica,
-- cliente (@idCliente, 2026-11-17: dos clientes pueden usar el mismo
-- número). Sin @idCliente compara solo el número (compatibilidad).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_ValidarDocumentosDuplicados
    @numeroFactura  VARCHAR(50) = NULL,
    @numeroCPIC     VARCHAR(50) = NULL,
    @facturaExiste  BIT OUTPUT,
    @cpicExiste     BIT OUTPUT,
    @idCliente      INT         = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @facturaExiste = 0;
    SET @cpicExiste    = 0;
    SET @numeroFactura = NULLIF(LTRIM(RTRIM(@numeroFactura)), '');
    SET @numeroCPIC    = NULLIF(LTRIM(RTRIM(@numeroCPIC)), '');

    IF @numeroFactura IS NOT NULL
        SELECT @facturaExiste = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
        FROM Factura
        WHERE numeroFactura = @numeroFactura
          AND (@idCliente IS NULL OR idCliente = @idCliente);

    IF @numeroCPIC IS NOT NULL
        SELECT @cpicExiste = CASE WHEN COUNT(*) > 0 THEN 1 ELSE 0 END
        FROM CPIC
        WHERE numeroCPIC = @numeroCPIC;
END
