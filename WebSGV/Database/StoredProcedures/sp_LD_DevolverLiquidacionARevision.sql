-- =============================================================================
-- SP: sp_LD_DevolverLiquidacionARevision
-- Propósito: Devolver una liquidación a la bandeja de pendientes de la administración,
--            anulando (append-only) la firma de revisión y, si la hubo, la de
--            conformidad. La declaración jurada del conductor sigue vigente y vuelve a
--            ser el documento de la orden.
--   @origen = 'OBSERVACION' -> el conductor no está de acuerdo con la revisión (POR_CONFORMAR).
--                              Valida que la orden sea suya; guarda su observación.
--             'RETIRO'      -> la administración retira el envío antes de la conformidad (POR_CONFORMAR).
--             'REVERSION'   -> la administración revierte una liquidación aprobada (APROBADO):
--                              al aprobarla de nuevo el conductor vuelve a firmar su conformidad.
--                              No se permite si la liquidación ya tiene su pago o descuento vigente
--                              en Saldos de conductores (primero se anula ese registro).
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_LD_DevolverLiquidacionARevision
    @idOrdenViaje  INT,
    @origen        VARCHAR(20),
    @idUsuario     INT,
    @nombreUsuario VARCHAR(150),
    @motivo        VARCHAR(500),
    @ipOrigen      VARCHAR(45),
    @userAgent     VARCHAR(500),
    @resultado     INT          OUTPUT,
    @mensaje       VARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @estado            VARCHAR(20);
    DECLARE @idConductor       INT;
    DECLARE @idFirmaConductor  INT;
    DECLARE @idFirmaAdmin      INT;
    DECLARE @idFirmaConformidad INT;
    DECLARE @numeroOrden       VARCHAR(50);
    DECLARE @etiqueta          VARCHAR(40);
    DECLARE @ahora             DATETIME = dbo.fn_AhoraPeru();

    SET @resultado = 0;

    IF @origen NOT IN ('OBSERVACION', 'RETIRO', 'REVERSION')
    BEGIN
        SET @mensaje = 'Origen de la devolución no válido.';
        RETURN;
    END

    IF LEN(LTRIM(RTRIM(ISNULL(@motivo, '')))) < 10
    BEGIN
        SET @mensaje = 'El motivo debe tener al menos 10 caracteres.';
        RETURN;
    END

    SELECT @estado             = estadoAprobacion,
           @idConductor        = idConductor,
           @idFirmaConductor   = idFirmaConductor,
           @idFirmaAdmin       = idFirmaAdmin,
           @idFirmaConformidad = idFirmaConformidad,
           @numeroOrden        = numeroOrdenViaje
    FROM OrdenViaje
    WHERE idOrdenViaje = @idOrdenViaje;

    IF @numeroOrden IS NULL
    BEGIN
        SET @mensaje = 'No se encontró la orden de viaje.';
        RETURN;
    END

    IF @origen = 'OBSERVACION' AND NOT EXISTS (
        SELECT 1 FROM Usuarios
        WHERE idUsuario = @idUsuario AND idConductor = @idConductor AND activo = 1
          AND UPPER(LTRIM(RTRIM(rol))) IN ('CONDUCTOR', 'CHOFER'))
    BEGIN
        SET @mensaje = 'La liquidación no pertenece al conductor autenticado.';
        RETURN;
    END

    IF (@origen IN ('OBSERVACION', 'RETIRO') AND @estado <> 'POR_CONFORMAR')
       OR (@origen = 'REVERSION' AND @estado <> 'APROBADO')
    BEGIN
        SET @mensaje = CASE @origen
            WHEN 'REVERSION' THEN 'Solo se puede revertir una liquidación aprobada.'
            ELSE 'La liquidación ya no está esperando la conformidad del conductor.' END;
        RETURN;
    END

    IF @idFirmaConductor IS NULL
    BEGIN
        SET @mensaje = 'La liquidación no tiene la declaración firmada del conductor.';
        RETURN;
    END

    -- Una liquidación aprobada cuyo saldo ya se pagó o descontó no se revierte: el pago quedaría
    -- calculado sobre montos que pueden cambiar. Se anula primero en Saldos de conductores.
    IF @origen = 'REVERSION' AND EXISTS (
        SELECT 1
        FROM PagoSaldoConductorDetalle d
        INNER JOIN PagoSaldoConductor p ON p.idPagoSaldo = d.idPagoSaldo
        WHERE d.idOrdenViaje = @idOrdenViaje AND d.activo = 1 AND p.anulado = 0)
    BEGIN
        SET @mensaje = 'La liquidación ya tiene registrado su pago o descuento. Anúlelo primero en Saldos de conductores y luego revierta.';
        RETURN;
    END

    SET @etiqueta = CASE @origen
        WHEN 'OBSERVACION' THEN 'OBSERVACION DEL CONDUCTOR'
        WHEN 'RETIRO'      THEN 'RETIRO DEL ENVIO AL CONDUCTOR'
        ELSE                    'REVERSION' END;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Anular (append-only) la revisión y la conformidad que dejan de valer.
        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent,
            estadoFirma, idFirmaAnulada, motivoAnulacion, etapa
        )
        SELECT f.tipoDocumento, f.idDocumento, 'C',
               @idUsuario, NULL, ISNULL(@nombreUsuario, ''),
               CASE WHEN @origen = 'OBSERVACION' THEN 'CONDUCTOR' ELSE 'ADMIN' END,
               NULL, f.hashDocumento,
               'Anulación de la firma id=' + CAST(f.idFirma AS VARCHAR(20)) + ' (' + @etiqueta + ')',
               SYSUTCDATETIME(), @ipOrigen, @userAgent,
               'A', f.idFirma, @motivo, 'ANULACION'
        FROM FirmaDigital f
        WHERE f.idFirma IN (@idFirmaAdmin, @idFirmaConformidad)
          AND NOT EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma);

        -- La declaración del conductor vuelve a ser el documento vigente de la orden.
        UPDATE ov
        SET estadoAprobacion          = 'PENDIENTE',
            estadoViaje               = CASE WHEN ov.estadoViaje = 'COMPLETADO' THEN 'PENDIENTE' ELSE ov.estadoViaje END,
            idFirmaAdmin              = NULL,
            idFirmaConformidad        = NULL,
            fechaAprobacionFirmada    = NULL,
            fechaConformidad          = NULL,
            rutaPdfFirmado            = fd.rutaPdfFirmado,
            hashPdfFirmado            = fd.hashDocumento,
            observacionConductor      = CASE WHEN @origen = 'OBSERVACION' THEN @motivo ELSE ov.observacionConductor END,
            fechaObservacionConductor = CASE WHEN @origen = 'OBSERVACION' THEN @ahora ELSE ov.fechaObservacionConductor END,
            observaciones             = ISNULL(ov.observaciones, '') + CHAR(13) + CHAR(10) +
                                        '[' + @etiqueta + ' ' + CONVERT(VARCHAR(19), @ahora, 120) + '] ' + @motivo
        FROM OrdenViaje ov
        INNER JOIN FirmaDigital fd ON fd.idFirma = ov.idFirmaConductor
        WHERE ov.idOrdenViaje = @idOrdenViaje
          AND ov.estadoAprobacion = @estado
          -- Concurrencia: alguien pudo registrar el pago entre la validación y este UPDATE.
          AND (@origen <> 'REVERSION' OR NOT EXISTS (
                SELECT 1
                FROM PagoSaldoConductorDetalle d
                INNER JOIN PagoSaldoConductor p ON p.idPagoSaldo = d.idPagoSaldo
                WHERE d.idOrdenViaje = ov.idOrdenViaje AND d.activo = 1 AND p.anulado = 0));

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @mensaje = 'Otro usuario modificó la liquidación. Actualice la pantalla.';
            RETURN;
        END

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje = CASE @origen
            WHEN 'OBSERVACION' THEN 'Su observación fue enviada a la administración.'
            WHEN 'RETIRO'      THEN 'Envío retirado: la liquidación ' + @numeroOrden + ' volvió a pendientes.'
            ELSE 'Liquidación ' + @numeroOrden + ' revertida: volvió a pendientes y el conductor deberá firmar de nuevo su conformidad.' END;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        -- Sin ERROR_MESSAGE() hacia el usuario: el error sube al C#, que lo registra (LogSGV)
        -- y muestra un texto genérico (MensajeErrorHelper).
        THROW;
    END CATCH
END
GO
