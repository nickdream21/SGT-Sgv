-- =============================================================================
-- SP: sp_DC_RegistrarFirmaAdmin
-- Propósito: Insertar una firma administrativa (Nivel C - simple, metadata
--            únicamente) como constancia de aprobación de la liquidación.
--            No captura trazo canvas; usa las credenciales autenticadas del
--            administrador como prueba de identidad e intención.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_RegistrarFirmaAdmin
    @idOrdenViaje        INT,
    @idUsuarioFirmante   INT,
    @dniFirmante         VARCHAR(15),
    @nombreFirmante      VARCHAR(150),
    @hashDocumento       CHAR(64),
    @textoConsentimiento VARCHAR(1000),
    @ipOrigen            VARCHAR(45),
    @userAgent           VARCHAR(500),
    @rutaPdfFirmado      VARCHAR(500),
    @idFirmaSalida       INT           OUTPUT,
    @resultado           INT           OUTPUT,
    @mensaje             VARCHAR(500)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM OrdenViaje
        WHERE idOrdenViaje = @idOrdenViaje
          AND idFirmaConductor IS NOT NULL
          AND estadoAprobacion IN ('PENDIENTE', 'APROBADO')
    )
    BEGIN
        SET @resultado     = 0;
        SET @mensaje       = 'La Orden de Viaje no existe, no tiene firma del conductor o no está en un estado válido.';
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
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'C',
            @idUsuarioFirmante, @dniFirmante, @nombreFirmante, 'ADMIN',
            NULL, @hashDocumento, @textoConsentimiento,
            SYSUTCDATETIME(), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V'
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        UPDATE OrdenViaje
        SET idFirmaAdmin            = @idFirmaSalida,
            fechaAprobacionFirmada  = SYSUTCDATETIME(),
            rutaPdfFirmado          = ISNULL(@rutaPdfFirmado, rutaPdfFirmado),
            hashPdfFirmado          = @hashDocumento
        WHERE idOrdenViaje = @idOrdenViaje;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Firma administrativa registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @resultado     = 0;
        SET @idFirmaSalida = 0;
        SET @mensaje       = 'Error al registrar la firma administrativa: ' + ERROR_MESSAGE();
    END CATCH
END
GO
