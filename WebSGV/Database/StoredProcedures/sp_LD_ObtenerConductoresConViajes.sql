-- ============================================================
-- Obtiene conductores que tienen al menos un viaje ABIERTO activo.
-- Uso: dropdown filtro de conductores en ListaDespachos.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerConductoresConViajes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        c.idConductor,
        CONCAT(c.nombre, ' ', ISNULL(c.apPaterno, ''), ' ', ISNULL(c.apMaterno, '')) AS NombreCompleto
    FROM Conductor c
    INNER JOIN ViajesEnProgreso vp ON c.idConductor = vp.idConductor
    WHERE vp.estadoViaje = 'ABIERTO'
      AND vp.activo = 1
    ORDER BY NombreCompleto;
END
