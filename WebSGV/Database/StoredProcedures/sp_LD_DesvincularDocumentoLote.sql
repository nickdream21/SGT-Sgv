-- ============================================================
-- Desvincula documentos (Factura o CPIC) de un lote de despachos.
-- @tipoDocumento: 'FACTURA' o 'CPIC'
-- Recibe lista de IDs de despachos separados por coma.
-- Un despacho anulado (activo = 0 o ANULADO) no se toca: si la
-- lista trae alguno, THROW 50015 y no se escribe nada (2026-11-17).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_DesvincularDocumentoLote
    @idsDespachos   VARCHAR(MAX),
    @tipoDocumento  VARCHAR(10)    -- 'FACTURA' o 'CPIC'
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    IF EXISTS (SELECT 1 FROM Despachos d
               WHERE d.idDespacho IN (SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
                 AND (d.activo = 0 OR d.estadoDespacho = 'ANULADO'))
        THROW 50015, N'El lote está anulado: solo se puede consultar.', 1;

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
