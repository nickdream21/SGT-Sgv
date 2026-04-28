-- ============================================================
-- SP:    sp_RechazarLiquidacion
-- Módulo: Liquidaciones Pendientes
-- Descripción: Rechaza una liquidación pendiente. Revierte el
--              estadoAprobacion a 'REABIERTO', reabre el viaje
--              en ViajesEnProgreso y devuelve sus despachos a
--              'EN_PROCESO' para que el conductor pueda corregir
--              y volver a liquidar.
-- Sincronizado con producción: 2026-04-28
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

        -- Obtener idViajeProgreso antes de actualizar
        DECLARE @idViajeProgreso INT;

        SELECT @idViajeProgreso = idViajeProgreso
        FROM OrdenViaje
        WHERE numeroOrdenViaje = @numeroOrdenViaje;

        -- Actualizar OrdenViaje
        UPDATE OrdenViaje
        SET
            estadoAprobacion     = 'REABIERTO',
            observacionesRechazo = @observaciones,
            fechaRechazo         = GETDATE(),
            idUsuarioAprobacion  = @idUsuarioAprobacion,
            observaciones        = ISNULL(observaciones, '') +
                CHAR(13) + CHAR(10) +
                '**RECHAZADO ' + CONVERT(VARCHAR, GETDATE(), 120) + '**: ' + @observaciones
        WHERE numeroOrdenViaje = @numeroOrdenViaje;

        -- Reabrir el viaje en progreso (si existe)
        IF @idViajeProgreso IS NOT NULL
        BEGIN
            UPDATE ViajesEnProgreso
            SET
                estadoViaje = 'ABIERTO',
                fechaCierre = NULL
            WHERE idViajeProgreso = @idViajeProgreso;

            -- Reabrir despachos asociados
            UPDATE Despachos
            SET estadoDespacho = 'EN_PROCESO'
            WHERE idViajeProgreso = @idViajeProgreso
              AND activo          = 1;
        END

        COMMIT TRANSACTION;

        SELECT 1 AS Resultado, 'Liquidación rechazada correctamente' AS Mensaje;

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SELECT 0 AS Resultado, ERROR_MESSAGE() AS Mensaje;
    END CATCH

END;
