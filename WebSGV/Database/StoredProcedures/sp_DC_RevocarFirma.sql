-- =============================================================================
-- SP: sp_DC_RevocarFirma
-- Propósito: Revocar una firma digital existente insertando una nueva fila
--            append-only con estadoFirma='A' y referencia a la firma original.
--            La tabla FirmaDigital tiene triggers que bloquean UPDATE/DELETE,
--            por lo que la "anulación" se materializa como un INSERT nuevo.
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

    IF @estadoOrig = 'A'
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
        VALUES
        (
            @tipoDocumento, @idDocumento, 'C',
            @idUsuarioRevocador, NULL, @nombreRevocador, 'ADMIN',
            NULL, @hashDoc,
            'Revocación de firma id=' + CAST(@idFirmaOriginal AS VARCHAR(20)),
            SYSUTCDATETIME(), @ipOrigen, @userAgent,
            'A', @idFirmaOriginal, @motivoAnulacion
        );

        SET @idFirmaRevocacion = SCOPE_IDENTITY();
        SET @resultado         = 1;
        SET @mensaje           = 'Firma revocada correctamente (append-only).';
    END TRY
    BEGIN CATCH
        SET @resultado         = 0;
        SET @idFirmaRevocacion = 0;
        SET @mensaje           = 'Error al revocar la firma: ' + ERROR_MESSAGE();
    END CATCH
END
GO
