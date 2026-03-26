CREATE OR ALTER PROCEDURE sp_DC_ObtenerDatosConductor
    @idConductor INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        c.nombre + ' ' + c.apPaterno + ' ' + ISNULL(c.apMaterno, '') AS nombreCompleto,
        c.DNI
    FROM Conductor c
    WHERE c.idConductor = @idConductor;
END
