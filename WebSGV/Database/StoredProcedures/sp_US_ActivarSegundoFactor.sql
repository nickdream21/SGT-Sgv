-- ============================================================
-- Confirma el alta (o el cambio de celular) del segundo factor (E4-14): el secreto pendiente pasa a
-- ser el vigente, con el paso TOTP del código que lo confirmó como último usado, y los códigos de
-- recuperación se reemplazan por @hashesCodigos (hashes separados por '|'). Solo si el pendiente
-- sigue siendo @secretoPendienteCifrado (otra pestaña no empezó otra alta) y tiene menos de 15
-- minutos. Devuelve activado = 1 si se activó, 0 si no cambió nada.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_ActivarSegundoFactor
    @idUsuario               INT,
    @secretoPendienteCifrado VARCHAR(200),
    @paso                    BIGINT,
    @hashesCodigos           VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @activado BIT = 0;

    BEGIN TRAN;

    UPDATE dbo.UsuarioSegundoFactor
       SET secretoCifrado          = secretoPendienteCifrado,
           secretoPendienteCifrado = NULL,
           pendienteDesdeUtc       = NULL,
           activo                  = 1,
           ultimoPasoUsado         = @paso,
           activadoUtc             = SYSUTCDATETIME(),
           actualizadoUtc          = SYSUTCDATETIME()
     WHERE idUsuario = @idUsuario
       AND secretoPendienteCifrado = @secretoPendienteCifrado
       AND pendienteDesdeUtc >= DATEADD(MINUTE, -15, SYSUTCDATETIME());

    IF @@ROWCOUNT = 1
    BEGIN
        DELETE FROM dbo.UsuarioCodigoRecuperacion WHERE idUsuario = @idUsuario;

        INSERT INTO dbo.UsuarioCodigoRecuperacion (idUsuario, hash)
        SELECT @idUsuario, LTRIM(RTRIM(value))
        FROM STRING_SPLIT(@hashesCodigos, '|')
        WHERE LTRIM(RTRIM(value)) <> '';

        SET @activado = 1;
    END

    COMMIT;

    SELECT @activado AS activado;
END
