CREATE OR ALTER PROCEDURE sp_ObtenerPlantasPorAmbito
    @esInternacional BIT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT nombre
    FROM Planta
    WHERE esInternacional = @esInternacional
      AND activo = 1
    ORDER BY nombre;
END
