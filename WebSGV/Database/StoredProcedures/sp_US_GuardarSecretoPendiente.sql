-- ============================================================
-- Guarda el secreto TOTP (cifrado) de un alta o cambio de celular que todavía no se confirmó con un
-- código (E4-14). No toca el secreto vigente: el factor activo sigue valiendo hasta que
-- sp_US_ActivarSegundoFactor confirme el nuevo.
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_GuardarSecretoPendiente
    @idUsuario      INT,
    @secretoCifrado VARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRAN;

    UPDATE dbo.UsuarioSegundoFactor WITH (UPDLOCK, HOLDLOCK)
       SET secretoPendienteCifrado = @secretoCifrado,
           pendienteDesdeUtc       = SYSUTCDATETIME(),
           actualizadoUtc          = SYSUTCDATETIME()
     WHERE idUsuario = @idUsuario;

    IF @@ROWCOUNT = 0
        INSERT INTO dbo.UsuarioSegundoFactor (idUsuario, activo, secretoPendienteCifrado, pendienteDesdeUtc)
        VALUES (@idUsuario, 0, @secretoCifrado, SYSUTCDATETIME());

    COMMIT;
END
