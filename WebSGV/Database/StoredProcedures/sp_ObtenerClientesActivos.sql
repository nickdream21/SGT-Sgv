CREATE OR ALTER PROCEDURE sp_ObtenerClientesActivos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idCliente, nombre
    FROM Cliente
    WHERE activo = 1
    ORDER BY nombre;
END
