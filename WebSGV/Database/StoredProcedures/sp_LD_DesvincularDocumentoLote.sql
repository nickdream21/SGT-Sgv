-- ============================================================
-- Desvincula documentos (Factura o CPIC) de un lote de despachos.
-- @tipoDocumento: 'FACTURA' o 'CPIC'
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_DesvincularDocumentoLote
    @idsDespachos   VARCHAR(MAX),
    @tipoDocumento  VARCHAR(10)    -- 'FACTURA' o 'CPIC'
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    IF @tipoDocumento = 'FACTURA'
    BEGIN
        UPDATE Despachos
        SET idFactura = NULL
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
    ELSE IF @tipoDocumento = 'CPIC'
    BEGIN
        UPDATE Despachos
        SET idCPIC = NULL
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','));
    END
END
