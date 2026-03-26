CREATE OR ALTER PROCEDURE sp_DC_ObtenerEstacionesPeaje
AS
BEGIN
    SET NOCOUNT ON;

    SELECT nombre
    FROM EstacionesPeaje 
    WHERE activo = 1
    ORDER BY nombre;
END
