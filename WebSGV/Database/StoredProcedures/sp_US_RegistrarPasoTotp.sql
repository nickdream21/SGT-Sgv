-- ============================================================
-- Registra el paso TOTP del código con el que el usuario acaba de entrar (E4-14). Atómico: solo
-- actualiza si el factor sigue activo con el mismo secreto y @paso es MAYOR que el último usado, así
-- un mismo código no vale dos veces (ni en dos peticiones simultáneas: la segunda ya no cumple el
-- WHERE). Devuelve aceptado = 1 o 0; con 0 la entrada se rechaza aunque el código fuera correcto.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_RegistrarPasoTotp
    @idUsuario      INT,
    @secretoCifrado VARCHAR(200),
    @paso           BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.UsuarioSegundoFactor
       SET ultimoPasoUsado = @paso,
           actualizadoUtc  = SYSUTCDATETIME()
     WHERE idUsuario = @idUsuario
       AND activo = 1
       AND secretoCifrado = @secretoCifrado
       AND (ultimoPasoUsado IS NULL OR ultimoPasoUsado < @paso);

    SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS BIT) AS aceptado;
END
