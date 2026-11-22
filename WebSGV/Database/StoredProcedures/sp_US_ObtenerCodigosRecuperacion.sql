-- ============================================================
-- Códigos de recuperación sin usar (id y hash) de un usuario con el segundo factor activo (E4-14).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_ObtenerCodigosRecuperacion
    @idUsuario INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT c.id, c.hash
    FROM dbo.UsuarioCodigoRecuperacion c
    JOIN dbo.UsuarioSegundoFactor f ON f.idUsuario = c.idUsuario AND f.activo = 1
    WHERE c.idUsuario = @idUsuario
      AND c.usadoUtc IS NULL;
END
