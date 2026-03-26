-- ============================================================
-- Obtiene despachos con su conductor actual, para el grid
-- de asignacion de conductores en la edicion de lotes.
-- Recibe lista de IDs separados por coma.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerDespachosConductoresLote
    @idsDespachos VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF @idsDespachos IS NULL OR LEN(@idsDespachos) = 0
        RETURN;

    SELECT
        d.idDespacho,
        d.numeroDespacho,
        d.fechaDespacho,
        d.idConductor,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreConductorActual
    FROM Despachos d
    INNER JOIN Conductor c ON d.idConductor = c.idConductor
    WHERE d.idDespacho IN (SELECT CAST(value AS INT) FROM STRING_SPLIT(@idsDespachos, ','))
      AND d.activo = 1
    ORDER BY d.numeroDespacho;
END
