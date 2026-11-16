-- =============================================================================
-- SP: sp_DC_RegistrarFirmaConductor
-- Propósito: Registrar la firma del conductor sobre su DECLARACIÓN JURADA de gastos
--            (etapa DECLARACION) y vincularla a la Orden de Viaje junto con el PDF
--            firmado, su hash SHA-256 y su código de verificación. Transaccional.
--            La conformidad con la liquidación final es otra firma
--            (sp_DC_RegistrarConformidadConductor).
-- El DNI se toma de la tabla Conductor (no del cliente); @dniFirmante solo se usa
-- si el conductor no tiene DNI ni carné de extranjería registrado.
-- 2026-11-15: la declaración se firma una sola vez, con la orden PENDIENTE y sin firma
--   vigente (una reliquidación anula la anterior en sp_DC_ActualizarOrdenViajeConductor).
--   Dentro de la transacción la orden se vuelve a leer con UPDLOCK/HOLDLOCK antes de
--   insertar: de dos firmas simultáneas solo vale una, y la que llega tarde sale con
--   COMMIT sin haber escrito nada (nunca hace ROLLBACK de la transacción de quien llama).
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
    @mensaje             VARCHAR(500)  OUTPUT,
    @codigoVerificacion  VARCHAR(20)   = NULL,
    @metodoAutenticacion VARCHAR(150)  = NULL,
    @usuarioFirmante     VARCHAR(50)   = NULL,
    @fechaHoraFirmaUtc   DATETIME2(0)  = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @numeroOrdenViaje VARCHAR(50);
    DECLARE @estadoAprobacion VARCHAR(20);
    DECLARE @dniConductor     VARCHAR(15);
    DECLARE @usuario          VARCHAR(50);
    DECLARE @idFirmaVigente   INT;
    DECLARE @idFirmaAdmin     INT;

    -- Validar que la orden pertenece al conductor autenticado.
    -- La validación se repite aquí para que no pueda omitirse llamando al SP directamente.
    SELECT @numeroOrdenViaje = ov.numeroOrdenViaje,
           @estadoAprobacion = ov.estadoAprobacion,
           @dniConductor     = COALESCE(NULLIF(LTRIM(RTRIM(c.DNI)), ''), NULLIF(LTRIM(RTRIM(c.carnetExtranjeria)), '')),
           @usuario          = u.nombreUsuario,
           @idFirmaVigente   = ov.idFirmaConductor,
           @idFirmaAdmin     = ov.idFirmaAdmin
    FROM OrdenViaje ov
    INNER JOIN Usuarios u ON u.idConductor = ov.idConductor
    LEFT  JOIN Conductor c ON c.idConductor = ov.idConductor
    WHERE ov.idOrdenViaje = @idOrdenViaje
      AND u.idUsuario = @idUsuarioFirmante
      AND u.activo = 1
      AND UPPER(LTRIM(RTRIM(u.rol))) IN ('CONDUCTOR', 'CHOFER');

    IF @numeroOrdenViaje IS NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La Orden de Viaje no existe o no pertenece al conductor autenticado.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @estadoAprobacion <> 'PENDIENTE'
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = CASE WHEN @estadoAprobacion = 'REABIERTO'
                              THEN 'La liquidación fue rechazada: corríjala y vuelva a enviarla antes de firmar.'
                              ELSE 'La Orden de Viaje no está pendiente de firma.' END;
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @idFirmaVigente IS NOT NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La declaración de esta liquidación ya está firmada.';
        SET @idFirmaSalida = 0;
        RETURN;
    END

    IF @idFirmaAdmin IS NOT NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje   = 'La liquidación ya fue revisada por la administración; no se puede volver a firmar la declaración.';
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

        IF NOT EXISTS (SELECT 1 FROM OrdenViaje WITH (UPDLOCK, HOLDLOCK)
                       WHERE idOrdenViaje = @idOrdenViaje
                         AND estadoAprobacion = 'PENDIENTE'
                         AND idFirmaConductor IS NULL
                         AND idFirmaAdmin IS NULL)
        BEGIN
            -- Otra firma (u otro cambio de estado) llegó primero. No se escribió nada:
            -- COMMIT solo libera el bloqueo.
            COMMIT TRANSACTION;
            SET @resultado     = 0;
            SET @idFirmaSalida = 0;
            SET @mensaje       = 'La declaración ya fue firmada o la liquidación cambió de estado. Actualice la pantalla.';
            RETURN;
        END

        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent, rutaPdfFirmado,
            estadoFirma, etapa, codigoVerificacion, metodoAutenticacion, usuarioFirmante
        )
        VALUES
        (
            'ORDEN_VIAJE', CAST(@idOrdenViaje AS VARCHAR(50)), 'B',
            @idUsuarioFirmante, COALESCE(@dniConductor, @dniFirmante), @nombreFirmante, 'CONDUCTOR',
            @imagenTrazoPng, @hashDocumento, @textoConsentimiento,
            ISNULL(@fechaHoraFirmaUtc, SYSUTCDATETIME()), @ipOrigen, @userAgent, @rutaPdfFirmado,
            'V', 'DECLARACION', @codigoVerificacion, @metodoAutenticacion, COALESCE(@usuarioFirmante, @usuario)
        );

        SET @idFirmaSalida = SCOPE_IDENTITY();

        -- Vincular la firma a la Orden de Viaje. Una observación anterior del conductor ya se
        -- atendió (la orden se rechazó y la vuelve a declarar): queda en el historial de observaciones.
        UPDATE OrdenViaje
        SET idFirmaConductor          = @idFirmaSalida,
            rutaPdfFirmado            = @rutaPdfFirmado,
            hashPdfFirmado            = @hashDocumento,
            fechaEnvioFirmado         = ISNULL(@fechaHoraFirmaUtc, SYSUTCDATETIME()),
            observacionConductor      = NULL,
            fechaObservacionConductor = NULL
        WHERE idOrdenViaje = @idOrdenViaje;   -- fila bloqueada y verificada arriba

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje   = 'Firma del conductor registrada correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        -- Sin ERROR_MESSAGE() hacia el usuario: el error sube al C#, que borra el PDF, lo
        -- registra (LogSGV) y muestra un texto genérico (MensajeErrorHelper).
        THROW;
    END CATCH
END
GO
