-- ============================================================
-- SP:    sp_AprobarLiquidacion
-- Módulo: Liquidaciones Pendientes
-- Descripción: Aprueba una orden de viaje pendiente registrada
--              por un conductor. Marca la orden como APROBADO /
--              COMPLETADO y retorna Resultado/Mensaje para que
--              el código C# evalúe el resultado sin excepciones.
-- Sincronizado con producción: 2026-04-28
-- ============================================================
CREATE OR ALTER PROCEDURE sp_AprobarLiquidacion
    @numeroOrdenViaje    VARCHAR(50),
    @idUsuarioAprobacion INT,
    @observaciones       VARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- Actualizar estado de la orden
        UPDATE OrdenViaje
        SET
            estadoAprobacion      = 'APROBADO',
            fechaAprobacion       = GETDATE(),
            idUsuarioAprobacion   = @idUsuarioAprobacion,
            observacionesAprobacion = @observaciones,
            estadoViaje           = 'COMPLETADO'
        WHERE numeroOrdenViaje  = @numeroOrdenViaje
          AND estadoAprobacion  = 'PENDIENTE';

        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('No se encontró la liquidación o ya fue procesada', 16, 1);
            RETURN;
        END

        COMMIT TRANSACTION;
        SELECT 1 AS Resultado, 'Liquidación aprobada exitosamente' AS Mensaje;

    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SELECT 0 AS Resultado, ERROR_MESSAGE() AS Mensaje;
    END CATCH

END
