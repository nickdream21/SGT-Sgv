-- ============================================================
-- Desvincula documentos (Factura o CPIC) de un lote de despachos.
-- @tipoDocumento: 'FACTURA' o 'CPIC'
-- Recibe lista de IDs de despachos separados por coma.
-- Un despacho anulado (activo = 0 o ANULADO) no se toca: si la
-- lista trae alguno, THROW 50015 y no se escribe nada (2026-11-17).
-- Quitar la FACTURA mientras un despacho conserva una CPIC de esa factura
-- dejaría la CPIC colgando: THROW 50025 (quite antes la CPIC).
-- El documento no se borra: solo deja de estar en esos despachos.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_DesvincularDocumentoLote
    @idsDespachos   VARCHAR(MAX),
    @tipoDocumento  VARCHAR(10),   -- 'FACTURA' o 'CPIC'
    @usuario        VARCHAR(100) = NULL  -- quién quita (usuarioModificacion/fechaModificacion; 2026-11-17)
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
        IF EXISTS (SELECT 1
                   FROM Despachos d
                   JOIN CPIC c ON c.idCPIC = d.idCPIC
                   WHERE d.idDespacho IN (SELECT TRY_CAST(LTRIM(RTRIM(value)) AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
                     AND d.idFactura IS NOT NULL
                     AND c.idFactura = d.idFactura)
            THROW 50025, N'Quite también la CPIC: es de esta factura.', 1;

        UPDATE Despachos
        SET idFactura           = NULL,
            usuarioModificacion = COALESCE(LEFT(@usuario, 50), usuarioModificacion),
            fechaModificacion   = CASE WHEN @usuario IS NULL THEN fechaModificacion ELSE dbo.fn_AhoraPeru() END
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
          AND idFactura IS NOT NULL;
    END
    ELSE IF @tipoDocumento = 'CPIC'
    BEGIN
        UPDATE Despachos
        SET idCPIC              = NULL,
            usuarioModificacion = COALESCE(LEFT(@usuario, 50), usuarioModificacion),
            fechaModificacion   = CASE WHEN @usuario IS NULL THEN fechaModificacion ELSE dbo.fn_AhoraPeru() END
        WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
          AND idCPIC IS NOT NULL;
    END
END
