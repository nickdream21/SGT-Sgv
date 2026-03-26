-- ============================================================
-- Eliminacion logica (activo = 0) de todos los despachos de un
-- lote. Recalcula contadores de los viajes afectados.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_EliminarLote
    @idsDespachos VARCHAR(MAX),
    @usuario      VARCHAR(50),
    @fechaActual  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Parsear la lista de IDs una sola vez
        DECLARE @ids TABLE (idDespacho INT);
        INSERT INTO @ids (idDespacho)
        SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',');

        -- 1. Obtener viajes afectados ANTES de eliminar
        DECLARE @viajesAfectados TABLE (idViajeProgreso INT);

        INSERT INTO @viajesAfectados (idViajeProgreso)
        SELECT DISTINCT idViajeProgreso
        FROM Despachos
        WHERE idDespacho IN (SELECT idDespacho FROM @ids)
          AND idViajeProgreso IS NOT NULL;

        -- 2. Eliminacion logica de los despachos
        UPDATE Despachos
        SET activo               = 0,
            usuarioModificacion  = @usuario,
            fechaModificacion    = @fechaActual
        WHERE idDespacho IN (SELECT idDespacho FROM @ids);

        -- 3. Recalcular contadores de viajes afectados
        UPDATE vp
        SET vp.cantidadDespachos    = ISNULL(sub.Total, 0),
            vp.fechaUltimaActividad = @fechaActual
        FROM ViajesEnProgreso vp
        INNER JOIN @viajesAfectados va ON va.idViajeProgreso = vp.idViajeProgreso
        LEFT JOIN (
            SELECT idViajeProgreso, COUNT(*) AS Total
            FROM Despachos
            WHERE activo = 1
            GROUP BY idViajeProgreso
        ) sub ON sub.idViajeProgreso = vp.idViajeProgreso;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
