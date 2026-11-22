-- ============================================================
-- Estado del segundo factor de un usuario (E4-14, migración 62): secreto vigente y pendiente
-- (cifrados), último paso TOTP aceptado y códigos de recuperación sin usar. Sin filas si el
-- usuario nunca empezó el alta.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_ObtenerSegundoFactor
    @idUsuario INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT f.activo,
           f.secretoCifrado,
           f.ultimoPasoUsado,
           f.secretoPendienteCifrado,
           f.pendienteDesdeUtc,
           f.activadoUtc,
           (SELECT COUNT(*) FROM dbo.UsuarioCodigoRecuperacion c
             WHERE c.idUsuario = f.idUsuario AND c.usadoUtc IS NULL) AS codigosRestantes
    FROM dbo.UsuarioSegundoFactor f
    WHERE f.idUsuario = @idUsuario;
END
