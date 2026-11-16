-- =============================================================================
-- SP: sp_DC_RetirarLiquidacion
-- Propósito: El conductor retira su liquidación antes de que la administración la revise:
--            borra la orden y sus datos financieros y reabre sus viajes.
-- 2026-11-15: si la declaración ya estaba firmada, en la misma transacción se ANULA su
--   firma (append-only, motivo RETIRO_CONDUCTOR) antes de borrar la orden: el código de
--   verificación del PDF queda como anulado en VerificarDocumento.aspx.
-- =============================================================================
CREATE OR ALTER PROCEDURE sp_DC_RetirarLiquidacion
    @idOrdenViaje INT,
    @idConductor  INT,              -- NUEVO: conductor autenticado (IDOR guard)
    @resultado INT OUTPUT,          -- 0=error, 1=éxito
    @mensaje VARCHAR(500) OUTPUT,
    @numeroOrdenViajeSalida VARCHAR(50) OUTPUT,
    @idViajeProgresoSalida INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @numeroOrdenViaje VARCHAR(50);
    DECLARE @idViajeProgreso INT = 0;
    DECLARE @idConductorOrden INT = 0;
    DECLARE @idFirmaConductor INT;

    -- 1. Verificar que la liquidación existe, está PENDIENTE, fue registrada por el conductor
    --    Y pertenece al conductor autenticado (IDOR guard)
    SELECT 
        @numeroOrdenViaje = numeroOrdenViaje,
        @idViajeProgreso = ISNULL(idViajeProgreso, 0),
        @idConductorOrden = ISNULL(idConductor, 0),
        @idFirmaConductor = idFirmaConductor
    FROM OrdenViaje 
    WHERE idOrdenViaje = @idOrdenViaje 
        AND estadoAprobacion = 'PENDIENTE' 
        AND registradoPor = 'CONDUCTOR'
        AND idConductor   = @idConductor   -- solo puede retirar su propia orden
        AND fechaEnvioConformidad IS NULL; -- ya revisada por la administración: no se borra

    IF @numeroOrdenViaje IS NULL
    BEGIN
        SET @resultado = 0;
        SET @mensaje = 'No se puede retirar esta liquidación. Es posible que ya haya sido revisada por la administración o no le pertenece.';
        SET @numeroOrdenViajeSalida = '';
        SET @idViajeProgresoSalida = 0;
        RETURN;
    END

    -- Fallback: si idViajeProgreso es 0, buscarlo por conductor
    IF @idViajeProgreso = 0 AND @idConductorOrden > 0
    BEGIN
        SELECT TOP 1 @idViajeProgreso = idViajeProgreso 
        FROM ViajesEnProgreso 
        WHERE idConductor = @idConductorOrden AND estadoViaje = 'CERRADO' 
        ORDER BY fechaCierre DESC;
    END

    IF @idViajeProgreso = 0
    BEGIN
        SET @resultado = 0;
        SET @mensaje = 'No se pudo determinar el viaje a reabrir. Contacte con la administración.';
        SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
        SET @idViajeProgresoSalida = 0;
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Releer la orden con bloqueo: entre la lectura de arriba y aquí el conductor pudo
        -- firmarla en otra pestaña (o la administración, procesarla). Con la fila bloqueada,
        -- la firma que se anula es exactamente la que queda vinculada al borrar.
        DECLARE @sigueIgual BIT = 0;
        SELECT @sigueIgual = 1,
               @idFirmaConductor = idFirmaConductor
        FROM OrdenViaje WITH (UPDLOCK, HOLDLOCK)
        WHERE idOrdenViaje = @idOrdenViaje
          AND estadoAprobacion = 'PENDIENTE'
          AND registradoPor = 'CONDUCTOR'
          AND idConductor = @idConductor
          AND fechaEnvioConformidad IS NULL;

        IF @sigueIgual = 0
        BEGIN
            -- No se escribió nada: COMMIT solo libera el bloqueo.
            COMMIT TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'La liquidación cambió de estado mientras la retiraba. Recargue la página.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 2. Anular (append-only) la declaración firmada: la orden se borra, la firma no.
        IF @idFirmaConductor IS NOT NULL
        BEGIN
            DECLARE @idUsuario INT, @nombreUsuario VARCHAR(150);
            SELECT TOP 1 @idUsuario = idUsuario,
                   @nombreUsuario = LTRIM(RTRIM(ISNULL(nombre, '') + ' ' + ISNULL(apellido, '')))
            FROM Usuarios
            WHERE idConductor = @idConductor AND activo = 1
              AND UPPER(LTRIM(RTRIM(rol))) IN ('CONDUCTOR', 'CHOFER')
            ORDER BY idUsuario;

            INSERT INTO FirmaDigital
            (
                tipoDocumento, idDocumento, nivelFirma,
                idUsuarioFirmante, dniFirmante, nombreFirmante, rolFirmante,
                imagenTrazoPng, hashDocumento, textoConsentimiento,
                fechaHoraFirma, ipOrigen, userAgent,
                estadoFirma, idFirmaAnulada, motivoAnulacion, etapa
            )
            SELECT f.tipoDocumento, f.idDocumento, 'C',
                   @idUsuario, NULL, COALESCE(NULLIF(@nombreUsuario, ''), f.nombreFirmante), 'CONDUCTOR',
                   NULL, f.hashDocumento,
                   'Anulación de la firma id=' + CAST(f.idFirma AS VARCHAR(20)) + ' (RETIRO_CONDUCTOR, orden ' + @numeroOrdenViaje + ')',
                   SYSUTCDATETIME(), NULL, NULL,
                   'A', f.idFirma,
                   'RETIRO_CONDUCTOR: el conductor retiró su liquidación antes de la revisión; esta declaración deja de valer.',
                   'ANULACION'
            FROM FirmaDigital f
            WHERE f.idFirma = @idFirmaConductor
              AND NOT EXISTS (SELECT 1 FROM FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma);
        END

        -- 3. Eliminar datos financieros (9 tablas)
        DELETE FROM Ingresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM Egresos WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM IngresosAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM CategoriasAdicionales WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DescuentosReintegros WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetallePeajes WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleReparacionesVarios WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleHospedaje WHERE numeroOrdenViaje = @numeroOrdenViaje;
        DELETE FROM DetalleCombustible WHERE numeroOrdenViaje = @numeroOrdenViaje;

        -- 4. Eliminar la OrdenViaje
        DELETE FROM OrdenViaje 
        WHERE idOrdenViaje = @idOrdenViaje AND estadoAprobacion = 'PENDIENTE'
          AND (idFirmaConductor = @idFirmaConductor OR (idFirmaConductor IS NULL AND @idFirmaConductor IS NULL));

        IF @@ROWCOUNT = 0
        BEGIN
            -- Ya se escribió (anulación y datos financieros): aquí sí corresponde ROLLBACK.
            -- No debería ocurrir: la fila está bloqueada desde la relectura.
            ROLLBACK TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'La liquidación cambió de estado mientras la retiraba. Recargue la página.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 5. Re-abrir el viaje en progreso y los demás que cerró la misma liquidación (el
        --    conductor liquida todos sus viajes abiertos juntos; sp_DC_CerrarViajesProgreso los
        --    cierra en un solo UPDATE, con la misma fechaCierre). Antes solo se reabría uno.
        DECLARE @viajes TABLE (idViajeProgreso INT PRIMARY KEY);
        INSERT INTO @viajes (idViajeProgreso)
        SELECT o.idViajeProgreso
        FROM ViajesEnProgreso p
        INNER JOIN ViajesEnProgreso o
                ON o.idViajeProgreso = p.idViajeProgreso
                OR (o.idConductor = p.idConductor AND o.estadoViaje = 'CERRADO'
                    AND p.fechaCierre IS NOT NULL AND o.fechaCierre = p.fechaCierre)
        WHERE p.idViajeProgreso = @idViajeProgreso;

        UPDATE ViajesEnProgreso
        SET estadoViaje = 'ABIERTO',
            fechaCierre = NULL
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajes);

        IF @@ROWCOUNT = 0
        BEGIN
            ROLLBACK TRANSACTION;
            SET @resultado = 0;
            SET @mensaje = 'No se pudo reabrir el viaje. El viaje no existe o ya estaba abierto.';
            SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
            SET @idViajeProgresoSalida = @idViajeProgreso;
            RETURN;
        END

        -- 6. Revertir despachos a PROGRAMADO
        UPDATE Despachos 
        SET estadoDespacho = 'PROGRAMADO',
            fechaModificacion = dbo.fn_AhoraPeru()
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajes)
            AND activo = 1;

        COMMIT TRANSACTION;

        SET @resultado = 1;
        SET @mensaje = 'Liquidación retirada exitosamente. El viaje ha sido reabierto y puede volver a liquidar.';
        SET @numeroOrdenViajeSalida = @numeroOrdenViaje;
        SET @idViajeProgresoSalida = @idViajeProgreso;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Sin ERROR_MESSAGE() hacia el usuario: el error sube al C# (LogSGV + texto genérico).
        THROW;
    END CATCH
END
