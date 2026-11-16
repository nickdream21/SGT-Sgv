-- ============================================================
-- SP:    sp_RechazarLiquidacion
-- Módulo: Liquidaciones Pendientes
-- Descripción: Rechaza una liquidación pendiente. Revierte el
--              estadoAprobacion a 'REABIERTO', reabre el viaje
--              en ViajesEnProgreso y devuelve sus despachos a
--              'EN_PROCESO' para que el conductor pueda corregir
--              y volver a liquidar.
-- Sincronizado con producción: 2026-04-28
-- 2026-11-15: solo rechaza una liquidación PENDIENTE que la administración aún no
--   firmó (antes también POR_CONFORMAR o APROBADO, que quedaban trabadas). La orden se
--   lee con UPDLOCK; si ya fue procesada devuelve Resultado 0 sin cambiar nada. Los
--   errores técnicos se relanzan (THROW): el C# los registra y muestra un texto genérico.
-- Nota: el orden de parámetros en producción es
--       (@numeroOrdenViaje, @observaciones, @idUsuarioAprobacion).
--       El C# llama siempre por nombre — el orden no afecta.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_RechazarLiquidacion
    @numeroOrdenViaje    VARCHAR(50),
    @observaciones       NVARCHAR(500),
    @idUsuarioAprobacion INT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Obtener idViajeProgreso y el estado antes de actualizar (bloqueando la fila:
        -- un envío a conformidad simultáneo espera a que termine el rechazo, o al revés).
        DECLARE @idViajeProgreso INT, @estado VARCHAR(20), @idFirmaAdmin INT;

        SELECT @idViajeProgreso = idViajeProgreso,
               @estado          = estadoAprobacion,
               @idFirmaAdmin    = idFirmaAdmin
        FROM OrdenViaje WITH (UPDLOCK, HOLDLOCK)
        WHERE numeroOrdenViaje = @numeroOrdenViaje;

        IF @estado IS NULL OR @estado <> 'PENDIENTE' OR @idFirmaAdmin IS NOT NULL
        BEGIN
            -- No se modificó nada: COMMIT solo libera el bloqueo (un ROLLBACK anularía
            -- también una transacción externa del llamador).
            COMMIT TRANSACTION;
            SELECT 0 AS Resultado,
                   'La liquidación ya fue procesada por la administración (o no existe): no se puede rechazar.' AS Mensaje;
            RETURN;
        END

        -- Actualizar OrdenViaje
        UPDATE OrdenViaje
        SET
            estadoAprobacion     = 'REABIERTO',
            observacionesRechazo = @observaciones,
            fechaRechazo         = dbo.fn_AhoraPeru(),
            idUsuarioAprobacion  = @idUsuarioAprobacion,
            observaciones        = ISNULL(observaciones, '') +
                CHAR(13) + CHAR(10) +
                '**RECHAZADO ' + CONVERT(VARCHAR, dbo.fn_AhoraPeru(), 120) + '**: ' + @observaciones
        WHERE numeroOrdenViaje = @numeroOrdenViaje
          AND estadoAprobacion = 'PENDIENTE'
          AND idFirmaAdmin IS NULL;

        IF @@ROWCOUNT = 0
        BEGIN
            -- Tampoco se modificó nada (el UPDATE es la primera escritura): COMMIT, no ROLLBACK.
            COMMIT TRANSACTION;
            SELECT 0 AS Resultado,
                   'La liquidación ya fue procesada por la administración: no se puede rechazar.' AS Mensaje;
            RETURN;
        END

        -- Reabrir el viaje en progreso (si existe) y los demás viajes que cerró la misma
        -- liquidación: el conductor liquida todos sus viajes abiertos juntos y
        -- sp_DC_CerrarViajesProgreso los cierra en un solo UPDATE (misma fechaCierre).
        IF @idViajeProgreso IS NOT NULL
        BEGIN
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
            SET
                estadoViaje = 'ABIERTO',
                fechaCierre = NULL
            WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajes);

            -- Reabrir despachos asociados
            UPDATE Despachos
            SET estadoDespacho = 'EN_PROCESO'
            WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajes)
              AND activo          = 1;
        END

        COMMIT TRANSACTION;

        SELECT 1 AS Resultado, 'Liquidación rechazada correctamente' AS Mensaje;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        -- Sin ERROR_MESSAGE() hacia el usuario: el error sube al C#, que lo registra
        -- (LogSGV) y muestra un texto genérico (MensajeErrorHelper).
        THROW;
    END CATCH

END;
