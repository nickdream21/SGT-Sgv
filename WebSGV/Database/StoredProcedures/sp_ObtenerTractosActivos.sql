CREATE OR ALTER PROCEDURE sp_ObtenerTractosActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idTracto, placaTracto
    FROM Tracto
    WHERE activo = 1
    ORDER BY placaTracto;
END
