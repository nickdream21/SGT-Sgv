-- ============================================================
-- Anula todos los despachos de un lote y anula automaticamente
-- los viajes que queden sin despachos activos no-anulados.
-- Recibe lista de IDs de despachos separados por coma.
-- Retorna: cantidad de viajes anulados.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_AnularLote
    @idsDespachos VARCHAR(MAX),
    @usuario      VARCHAR(50),
    @fechaActual  DATETIME,
    @viajesAnulados INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @viajesAnulados = 0;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Parsear la lista de IDs una sola vez
        DECLARE @ids TABLE (idDespacho INT);
        INSERT INTO @ids (idDespacho)
        SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ',');

        -- 1. Marcar despachos como ANULADO
        UPDATE Despachos
        SET estadoDespacho      = 'ANULADO',
            usuarioModificacion = @usuario,
            fechaModificacion   = @fechaActual
        WHERE idDespacho IN (SELECT idDespacho FROM @ids);

        -- 2. Obtener viajes afectados
        DECLARE @viajesAfectados TABLE (idViajeProgreso INT);

        INSERT INTO @viajesAfectados (idViajeProgreso)
        SELECT DISTINCT idViajeProgreso
        FROM Despachos
        WHERE idDespacho IN (SELECT idDespacho FROM @ids)
          AND idViajeProgreso IS NOT NULL;

        -- 3. Anular viajes que ya no tienen despachos activos no-anulados
        UPDATE ViajesEnProgreso
        SET estadoViaje          = 'ANULADO',
            fechaUltimaActividad = @fechaActual
        WHERE idViajeProgreso IN (SELECT idViajeProgreso FROM @viajesAfectados)
          AND estadoViaje = 'ABIERTO'
          AND NOT EXISTS (
              SELECT 1
              FROM Despachos d
              WHERE d.idViajeProgreso = ViajesEnProgreso.idViajeProgreso
                AND d.activo = 1
                AND d.estadoDespacho <> 'ANULADO'
          );

        SET @viajesAnulados = @@ROWCOUNT;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
