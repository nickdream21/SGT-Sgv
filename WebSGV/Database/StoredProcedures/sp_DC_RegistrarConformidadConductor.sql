-- =============================================================================
-- SP: sp_DC_RegistrarConformidadConductor
-- Propósito: Registrar la firma de CONFORMIDAD del conductor con la liquidación
--            final revisada por la administración (etapa CONFORMIDAD) y completar
--            el viaje (APROBADO / COMPLETADO). Transaccional.
-- Garantías:
--   - La orden pertenece al conductor autenticado y está POR_CONFORMAR.
--   - Existe la firma de revisión de la administración.
--   - @huellaContenido es la misma que firmó la administración: si los montos o los
--     motivos cambiaron después de que el conductor abrió la pantalla, no se acepta.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_RegistrarConformidadConductor
    @idOrdenViaje        INT,
    @idUsuarioFirmante   INT,
    @nombreFirmante      VARCHAR(150),
    @imagenTrazoPng      VARBINARY(MAX),
    @hashDocumento       CHAR(64),
    @huellaContenido     CHAR(64),
    @textoConsentimiento VARCHAR(1000),
    @codigoVerificacion  VARCHAR(20),
    @metodoAutenticacion VARCHAR(150),
    @ipOrigen            VARCHAR(45),
    @userAgent           VARCHAR(500),
    @rutaPdfFirmado      VARCHAR(500),
    @fechaHoraFirmaUtc   DATETIME2(0)  = NULL,
    @idFirmaSalida       INT           OUTPUT,
    @resultado           INT           OUTPUT,
    @mensaje             VARCHAR(500)  OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @estadoAprobacion VARCHAR(20);
    DECLARE @idFirmaAdmin     INT;
    DECLARE @dniConductor     VARCHAR(15);
    DECLARE @usuario          VARCHAR(50);
    DECLARE @existe           BIT = 0;

    SELECT @existe           = 1,
           @estadoAprobacion = ov.estadoAprobacion,
           @idFirmaAdmin     = ov.idFirmaAdmin,
           @dniConductor     = COALESCE(NULLIF(LTRIM(RTRIM(c.DNI)), ''), NULLIF(LTRIM(RTRIM(c.carnetExtranjeria)), '')),
           @usuario          = u.nombreUsuario
    FROM OrdenViaje ov
    INNER JOIN Usuarios u ON u.idConductor = ov.idConductor
    LEFT  JOIN Conductor c ON c.idConductor = ov.idConductor
    WHERE ov.idOrdenViaje = @idOrdenViaje
      AND u.idUsuario = @idUsuarioFirmante
      AND u.activo = 1
      AND UPPER(LTRIM(RTRIM(u.rol))) IN ('CONDUCTOR', 'CHOFER');

    SET @idFirmaSalida = 0;
    SET @resultado     = 0;

    IF @existe = 0
    BEGIN
        SET @mensaje = 'La Orden de Viaje no existe o no pertenece al conductor autenticado.';
        RETURN;
    END

    IF @estadoAprobacion <> 'POR_CONFORMAR' OR @idFirmaAdmin IS NULL
    BEGIN
        SET @mensaje = 'La liquidación no está esperando su conformidad.';
        RETURN;
    END

    IF NOT EXISTS (SELECT 1 FROM FirmaDigital
                   WHERE idFirma = @idFirmaAdmin AND huellaContenido = @huellaContenido)
    BEGIN
        SET @mensaje = 'La liquidación cambió mientras la revisaba. Vuelva a abrirla para ver los montos actualizados.';
        RETURN;
    END

    IF @imagenTrazoPng IS NULL OR DATALENGTH(@imagenTrazoPng) < 100
    BEGIN
        SET @mensaje = 'La imagen de la firma es inválida o está vacía.';
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
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'B',
            @idUsuarioFirmante, @dniConductor, @nombreFirmante, 'CONDUCTOR',
            @imagenTrazoPng, @hashDocumento, @textoConsentimiento,
            ISNULL(@fechaHoraFirmaUtc, SYSUTCDATETIME()), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V', 'CONFORMIDAD', @codigoVerificacion, @metodoAutenticacion, @huellaContenido, @usuario
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        UPDATE OrdenViaje
        SET idFirmaConformidad = @idFirmaSalida,
            fechaConformidad   = dbo.fn_AhoraPeru(),
            estadoAprobacion   = 'APROBADO',
            estadoViaje        = 'COMPLETADO',
            fechaAprobacion    = dbo.fn_AhoraPeru(),
            rutaPdfFirmado     = @rutaPdfFirmado,
            hashPdfFirmado     = @hashDocumento
        WHERE idOrdenViaje = @idOrdenViaje
          AND estadoAprobacion = 'POR_CONFORMAR';

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @idFirmaSalida = 0;
            SET @mensaje = 'La liquidación ya no está esperando su conformidad.';
            RETURN;
        END

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Conformidad registrada. La liquidación quedó aprobada.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @resultado     = 0;
        SET @idFirmaSalida = 0;
        SET @mensaje       = 'Error al registrar la conformidad: ' + ERROR_MESSAGE();
    END CATCH
END
GO
