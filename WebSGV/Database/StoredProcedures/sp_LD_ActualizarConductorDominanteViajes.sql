-- ============================================================
-- Recalcula el conductor dominante (el que mas despachos activos
-- tiene) para cada viaje asociado a los despachos indicados,
-- y actualiza fechaUltimaActividad.
-- Recibe lista de IDs de despachos separados por coma.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ActualizarConductorDominanteViajes
    @idsDespachos VARCHAR(MAX),
    @fechaActual  DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    -- Tabla temporal con los viajes afectados
    DECLARE @viajesAfectados TABLE (idViajeProgreso INT);

    INSERT INTO @viajesAfectados (idViajeProgreso)
    SELECT DISTINCT idViajeProgreso
    FROM Despachos
    WHERE idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND idViajeProgreso IS NOT NULL;

    -- Para cada viaje, determinar el conductor con mas despachos activos
    UPDATE vp
    SET vp.idConductor          = sub.idConductor,
        vp.fechaUltimaActividad = @fechaActual
    FROM ViajesEnProgreso vp
    INNER JOIN @viajesAfectados va ON va.idViajeProgreso = vp.idViajeProgreso
    CROSS APPLY (
        SELECT TOP 1 d.idConductor
        FROM Despachos d
        WHERE d.idViajeProgreso = vp.idViajeProgreso
          AND d.activo = 1
        GROUP BY d.idConductor
        ORDER BY COUNT(*) DESC
    ) sub;
END
