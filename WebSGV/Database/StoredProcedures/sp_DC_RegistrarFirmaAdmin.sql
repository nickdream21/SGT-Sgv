-- =============================================================================
-- SP: sp_DC_RegistrarFirmaAdmin
-- Propósito: Registrar la firma de REVISIÓN de la administración (etapa REVISION,
--            nivel C: sin trazo, con las credenciales autenticadas del usuario) sobre
--            la liquidación final que se envía al conductor para su conformidad.
--            Se llama después de sp_LD_EnviarLiquidacionAConformidad: la orden debe
--            estar POR_CONFORMAR y sin otra firma de revisión vigente.
-- @huellaContenido: SHA-256 de los montos y motivos revisados. La conformidad del
-- conductor solo se acepta si firma exactamente esa misma huella.
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
    @mensaje             VARCHAR(500)  OUTPUT,
    @codigoVerificacion  VARCHAR(20)   = NULL,
    @metodoAutenticacion VARCHAR(150)  = NULL,
    @huellaContenido     CHAR(64)      = NULL,
    @fechaHoraFirmaUtc   DATETIME2(0)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @usuario VARCHAR(50) = (SELECT nombreUsuario FROM Usuarios WHERE idUsuario = @idUsuarioFirmante);

    -- Compatibilidad con el código publicado antes de la conformidad (2026-11-11): aprobaba primero
    -- (APROBADO) y firmaba después, sin huella. Se acepta ese caso hasta publicar el código nuevo.
    IF NOT EXISTS (
        SELECT 1
        FROM OrdenViaje
        WHERE idOrdenViaje = @idOrdenViaje
          AND idFirmaConductor IS NOT NULL
          AND (
                (estadoAprobacion = 'POR_CONFORMAR' AND idFirmaAdmin IS NULL)
             OR (@huellaContenido IS NULL AND estadoAprobacion = 'APROBADO')
              )
    )
    BEGIN
        SET @resultado     = 0;
        SET @mensaje       = 'La Orden de Viaje no existe, no tiene firma del conductor o no está lista para la firma de revisión.';
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
            estadoFirma, etapa, codigoVerificacion, metodoAutenticacion, huellaContenido, usuarioFirmante
        )
        VALUES
        (
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'C',
            @idUsuarioFirmante, @dniFirmante, @nombreFirmante, 'ADMIN',
            NULL, @hashDocumento, @textoConsentimiento,
            ISNULL(@fechaHoraFirmaUtc, SYSUTCDATETIME()), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V', 'REVISION', @codigoVerificacion, @metodoAutenticacion, @huellaContenido, @usuario
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        UPDATE OrdenViaje
        SET idFirmaAdmin            = @idFirmaSalida,
            fechaAprobacionFirmada  = ISNULL(@fechaHoraFirmaUtc, SYSUTCDATETIME()),
            rutaPdfFirmado          = ISNULL(@rutaPdfFirmado, rutaPdfFirmado),
            hashPdfFirmado          = @hashDocumento
        WHERE idOrdenViaje = @idOrdenViaje;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Firma de revisión registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @resultado     = 0;
        SET @idFirmaSalida = 0;
        SET @mensaje       = 'Error al registrar la firma de revisión: ' + ERROR_MESSAGE();
    END CATCH
END
GO
