-- ============================================================
-- Obtiene todos los conductores para el dropdown de edicion
-- de conductores por despacho.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_LD_ObtenerTodosConductores
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        idConductor,
        CONCAT(nombre, ' ', ISNULL(apPaterno, ''), ' ', ISNULL(apMaterno, '')) AS NombreCompleto
    FROM Conductor
    ORDER BY nombre, apPaterno, apMaterno;
END
