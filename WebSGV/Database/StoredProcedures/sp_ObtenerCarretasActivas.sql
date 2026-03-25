CREATE OR ALTER PROCEDURE sp_ObtenerCarretasActivas
AS
BEGIN
    SET NOCOUNT ON;

    SELECT idCarreta, placaCarreta
    FROM Carreta
    WHERE activo = 1
    ORDER BY placaCarreta;
END
