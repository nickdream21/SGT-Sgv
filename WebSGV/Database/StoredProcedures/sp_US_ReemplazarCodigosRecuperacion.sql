-- ============================================================
-- Reemplaza los códigos de recuperación de un usuario con el segundo factor activo (E4-14,
-- "Generar códigos nuevos" en MiSeguridad): borra todos, usados o no, e inserta @hashesCodigos
-- (hashes separados por '|'). Devuelve reemplazado = 1 o 0 (sin factor activo no cambia nada).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_ReemplazarCodigosRecuperacion
    @idUsuario     INT,
    @hashesCodigos VARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @reemplazado BIT = 0;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.UsuarioSegundoFactor WITH (UPDLOCK, HOLDLOCK)
                WHERE idUsuario = @idUsuario AND activo = 1)
    BEGIN
        DELETE FROM dbo.UsuarioCodigoRecuperacion WHERE idUsuario = @idUsuario;

        INSERT INTO dbo.UsuarioCodigoRecuperacion (idUsuario, hash)
        SELECT @idUsuario, LTRIM(RTRIM(value))
        FROM STRING_SPLIT(@hashesCodigos, '|')
        WHERE LTRIM(RTRIM(value)) <> '';

        UPDATE dbo.UsuarioSegundoFactor SET actualizadoUtc = SYSUTCDATETIME() WHERE idUsuario = @idUsuario;
        SET @reemplazado = 1;
    END

    COMMIT;

    SELECT @reemplazado AS reemplazado;
END
