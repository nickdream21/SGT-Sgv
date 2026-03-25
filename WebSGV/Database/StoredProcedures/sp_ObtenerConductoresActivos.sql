CREATE OR ALTER PROCEDURE sp_ObtenerConductoresActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        idConductor,
        CONCAT(nombre, ' ', apPaterno, ' ', apMaterno, ' - DNI: ', ISNULL(DNI, carnetExtranjeria)) AS NombreCompleto
    FROM Conductor
    WHERE activo = 1
    ORDER BY nombre, apPaterno, apMaterno;
END
