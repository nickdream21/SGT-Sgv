-- =============================================================================
-- SP: sp_DC_RegistrarFirmaConductor
-- Propósito: Insertar una firma digital del conductor (Nivel B - avanzada canvas)
--            sobre una Orden de Viaje, y vincularla en la tabla OrdenViaje junto
--            con el PDF firmado y su hash de integridad. Transaccional.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_RegistrarFirmaConductor
    @idOrdenViaje        INT,
    @idUsuarioFirmante   INT,
    @dniFirmante         VARCHAR(15),
    @nombreFirmante      VARCHAR(150),
    @imagenTrazoPng      VARBINARY(MAX),
    @hashDocumento       CHAR(64),
    @textoConsentimiento VARCHAR(1000),
    @ipOrigen            VARCHAR(45),
    @userAgent           VARCHAR(500),
    @rutaPdfFirmado      VARCHAR(500),
    @idFirmaSalida       INT           OUTPUT,
    @resultado           INT           OUTPUT,  -- 0=error, 1=éxito
    @mensaje             VARCHAR(500)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @numeroOrdenViaje VARCHAR(50);
    DECLARE @estadoAprobacion VARCHAR(20);

    -- Validar que la orden existe y está PENDIENTE
    SELECT @numeroOrdenViaje = numeroOrdenViaje,
           @estadoAprobacion = estadoAprobacion
    FROM OrdenViaje
    WHERE idOrdenViaje = @idOrdenViaje;

    IF @numeroOrdenViaje IS NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La Orden de Viaje no existe.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @estadoAprobacion NOT IN ('PENDIENTE', 'RECHAZADO')
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La Orden de Viaje no está en un estado válido para firmar (debe ser PENDIENTE o RECHAZADO).';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @imagenTrazoPng IS NULL OR DATALENGTH(@imagenTrazoPng) < 100
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La imagen de la firma es inválida o está vacía.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent, rutaPdfFirmado,
            estadoFirma
        )
        VALUES
        (
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'B',
            @idUsuarioFirmante, @dniFirmante, @nombreFirmante, 'CONDUCTOR',
            @imagenTrazoPng, @hashDocumento, @textoConsentimiento,
            SYSUTCDATETIME(), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V'
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        -- Vincular la firma a la Orden de Viaje
        UPDATE OrdenViaje
        SET idFirmaConductor   = @idFirmaSalida,
            rutaPdfFirmado     = @rutaPdfFirmado,
            hashPdfFirmado     = @hashDocumento,
            fechaEnvioFirmado  = SYSUTCDATETIME()
        WHERE idOrdenViaje = @idOrdenViaje;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Firma del conductor registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @resultado     = 0;
        SET @idFirmaSalida = 0;
        SET @mensaje       = 'Error al registrar la firma: ' + ERROR_MESSAGE();
    END CATCH
END
GO
