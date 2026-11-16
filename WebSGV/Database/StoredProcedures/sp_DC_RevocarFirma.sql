-- =============================================================================
-- SP: sp_DC_RevocarFirma
-- Propósito: Revocar una firma digital existente insertando una nueva fila
--            append-only con estadoFirma='A' y referencia a la firma original.
--            La tabla FirmaDigital tiene triggers que bloquean UPDATE/DELETE,
--            por lo que la "anulación" se materializa como un INSERT nuevo.
-- 2026-11-15: una firma ya anulada (por esta u otra vía, idFirmaAnulada) no se vuelve a
--   revocar. Sin uso desde la misma fecha (se quitó el WebMethod RechazarLiquidacion);
--   se conserva por si se retoma la revocación manual.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_RevocarFirma
    @idFirmaOriginal    INT,
    @idUsuarioRevocador INT,
    @nombreRevocador    VARCHAR(150),
    @motivoAnulacion    VARCHAR(500),
    @ipOrigen           VARCHAR(45),
    @userAgent          VARCHAR(500),
    @idFirmaRevocacion  INT          OUTPUT,
    @resultado          INT          OUTPUT,
    @mensaje            VARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @tipoDocumento VARCHAR(40);
    DECLARE @idDocumento   VARCHAR(50);
    DECLARE @hashDoc       CHAR(64);
    DECLARE @estadoOrig    CHAR(1);

    SELECT @tipoDocumento = tipoDocumento,
           @idDocumento   = idDocumento,
           @hashDoc       = hashDocumento,
           @estadoOrig    = estadoFirma
    FROM FirmaDigital
    WHERE idFirma = @idFirmaOriginal;

    IF @tipoDocumento IS NULL
    BEGIN
        SET @resultado        = 0;
        SET @mensaje          = 'La firma original no existe.';
        SET @idFirmaRevocacion = 0;
        RETURN;
    END

    IF @estadoOrig = 'A' OR EXISTS (SELECT 1 FROM FirmaDigital WHERE idFirmaAnulada = @idFirmaOriginal)
    BEGIN
        SET @resultado        = 0;
        SET @mensaje          = 'La firma ya está anulada.';
        SET @idFirmaRevocacion = 0;
        RETURN;
    END

    BEGIN TRY
        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent,
            estadoFirma, idFirmaAnulada, motivoAnulacion
        )
        SELECT
            @tipoDocumento, @idDocumento, 'C',
            @idUsuarioRevocador, NULL, @nombreRevocador, 'ADMIN',
            NULL, @hashDoc,
            'Revocación de firma id=' + CAST(@idFirmaOriginal AS VARCHAR(20)),
            SYSUTCDATETIME(), @ipOrigen, @userAgent,
            'A', @idFirmaOriginal, @motivoAnulacion
        WHERE NOT EXISTS (SELECT 1 FROM FirmaDigital WITH (UPDLOCK, HOLDLOCK) WHERE idFirmaAnulada = @idFirmaOriginal);

        IF @@ROWCOUNT = 0
        BEGIN
            SET @resultado         = 0;
            SET @idFirmaRevocacion = 0;
            SET @mensaje           = 'La firma ya está anulada.';
            RETURN;
        END

        SET @idFirmaRevocacion = SCOPE_IDENTITY();
        SET @resultado         = 1;
        SET @mensaje           = 'Firma revocada correctamente (append-only).';
    END TRY
    BEGIN CATCH
        -- Sin ERROR_MESSAGE() hacia el usuario: el error sube al C# (LogSGV + texto genérico).
        THROW;
    END CATCH
END
GO
