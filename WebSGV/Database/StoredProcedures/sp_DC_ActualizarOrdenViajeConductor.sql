-- =============================================================================
-- SP: sp_DC_ActualizarOrdenViajeConductor
-- Propósito: Reenvío (reliquidación) de una orden rechazada por la administración
--            (REABIERTO): actualiza los datos generales y la deja PENDIENTE.
-- Firmas (decisión 2026-11-15): la declaración firmada antes del rechazo ya no
--   corresponde a los montos nuevos. En la misma transacción se ANULA (append-only, como
--   sp_LD_DevolverLiquidacionARevision) toda firma vigente de la orden y se desvincula,
--   para que el conductor firme de nuevo su declaración antes de que la administración la
--   pueda revisar (sp_LD_EnviarLiquidacionAConformidad exige idFirmaConductor).
-- Se llama dentro de la transacción del servicio (LiquidacionConductorService, con
-- XACT_ABORT ON): no abre transacción propia; ante un estado inválido hace THROW.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_ActualizarOrdenViajeConductor
    @numeroOrdenViaje VARCHAR(50),
    @fechaSalida      DATE,
    @horaSalida       TIME          = NULL,
    @fechaLlegada     DATE,
    @horaLlegada      TIME          = NULL,
    @horaLlegadaDeclarada TIME      = NULL,
    @observaciones    VARCHAR(1000) = NULL,   -- 1000: lo que admite el formulario (con 250 se cortaba sin aviso)
    @idUsuarioRegistro INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @idOrdenViaje INT, @estado VARCHAR(20);
    DECLARE @idFirmaConductor INT, @idFirmaAdmin INT, @idFirmaConformidad INT;
    DECLARE @nombreUsuario VARCHAR(150);

    SELECT @idOrdenViaje       = idOrdenViaje,
           @estado             = estadoAprobacion,
           @idFirmaConductor   = idFirmaConductor,
           @idFirmaAdmin       = idFirmaAdmin,
           @idFirmaConformidad = idFirmaConformidad
    FROM OrdenViaje WITH (UPDLOCK, HOLDLOCK)
    WHERE numeroOrdenViaje = @numeroOrdenViaje;

    IF @idOrdenViaje IS NULL OR @estado <> 'REABIERTO'
        THROW 50001, 'La liquidación ya no está rechazada: no se puede volver a enviar. Recargue la página.', 1;

    IF @idFirmaConductor IS NOT NULL OR @idFirmaAdmin IS NOT NULL OR @idFirmaConformidad IS NOT NULL
    BEGIN
        SELECT @nombreUsuario = LTRIM(RTRIM(ISNULL(nombre, '') + ' ' + ISNULL(apellido, '')))
        FROM Usuarios WHERE idUsuario = @idUsuarioRegistro;

        INSERT INTO FirmaDigital
        (
            tipoDocumento, idDocumento, nivelFirma,
            idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
            imagenTrazoPng, hashDocumento, textoConsentimiento,
            fechaHoraFirma, ipOrigen, userAgent,
            estadoFirma, idFirmaAnulada, motivoAnulacion, etapa
        )
        SELECT f.tipoDocumento, f.idDocumento, 'C',
               @idUsuarioRegistro, NULL, COALESCE(NULLIF(@nombreUsuario, ''), f.nombreFirmante), 'CONDUCTOR',
               NULL, f.hashDocumento,
               'Anulación de la firma id=' + CAST(f.idFirma AS VARCHAR(20)) + ' (RELIQUIDACION DEL CONDUCTOR, orden ' + @numeroOrdenViaje + ')',
               SYSUTCDATETIME(), NULL, NULL,
               'A', f.idFirma,
               'La administración rechazó la liquidación y el conductor la volvió a enviar: este documento deja de valer y se exige una firma nueva.',
               'ANULACION'
        FROM FirmaDigital f
        WHERE f.idFirma IN (@idFirmaConductor, @idFirmaAdmin, @idFirmaConformidad)
          AND NOT EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma);
    END

    UPDATE OrdenViaje SET
        fechaSalida       = @fechaSalida,
        horaSalida        = @horaSalida,
        fechaLlegada      = @fechaLlegada,
        horaLlegada       = @horaLlegada,
        horaLlegadaDeclarada = @horaLlegadaDeclarada,
        observaciones     = @observaciones,
        estadoAprobacion  = 'PENDIENTE',
        idUsuarioRegistro = @idUsuarioRegistro,
        fechaRegistro     = dbo.fn_AhoraPeru(),
        idFirmaConductor  = NULL,
        idFirmaAdmin      = NULL,
        idFirmaConformidad = NULL,
        rutaPdfFirmado    = NULL,
        hashPdfFirmado    = NULL
    WHERE idOrdenViaje = @idOrdenViaje
      AND estadoAprobacion = 'REABIERTO';

    IF @@ROWCOUNT = 0
        THROW 50001, 'La liquidación ya no está rechazada: no se puede volver a enviar. Recargue la página.', 1;
END
