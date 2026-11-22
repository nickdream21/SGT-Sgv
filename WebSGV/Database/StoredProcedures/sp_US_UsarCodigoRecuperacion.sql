-- ============================================================
-- Marca como usado un código de recuperación (E4-14). Atómico: solo si era del usuario, no estaba
-- usado y el factor sigue activo; dos peticiones con el mismo código: solo una lo consigue.
-- Devuelve aceptado (1/0) y los códigos que le quedan sin usar.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_UsarCodigoRecuperacion
    @idUsuario INT,
    @idCodigo  INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE c
       SET usadoUtc = SYSUTCDATETIME()
      FROM dbo.UsuarioCodigoRecuperacion c
     WHERE c.id = @idCodigo
       AND c.idUsuario = @idUsuario
       AND c.usadoUtc IS NULL
       AND EXISTS (SELECT 1 FROM dbo.UsuarioSegundoFactor f
                    WHERE f.idUsuario = @idUsuario AND f.activo = 1);

    DECLARE @filas INT = @@ROWCOUNT;

    SELECT CAST(CASE WHEN @filas = 1 THEN 1 ELSE 0 END AS BIT) AS aceptado,
           (SELECT COUNT(*) FROM dbo.UsuarioCodigoRecuperacion
             WHERE idUsuario = @idUsuario AND usadoUtc IS NULL) AS restantes;
END
