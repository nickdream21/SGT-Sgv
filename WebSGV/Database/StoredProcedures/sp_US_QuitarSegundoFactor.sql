-- ============================================================
-- Quita el segundo factor de un usuario (E4-14: celular y códigos perdidos). Lo usan GestionUsuarios
-- ("Quitar segundo factor", otro ADMINISTRADOR DE SISTEMA) y Database/Scripts/quitar-segundo-factor.ps1;
-- cada uno registra su auditoría. Borra el factor (vigente y pendiente) y todos sus códigos de
-- recuperación y renueva selloSesion: las sesiones abiertas del usuario se cierran en su próxima
-- petición (SesionVigenteHelper). Devuelve quitado = 1 si había algo que quitar (si no, no toca el sello).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_QuitarSegundoFactor
    @idUsuario INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @filas INT = 0;

    BEGIN TRAN;

    DELETE FROM dbo.UsuarioCodigoRecuperacion WHERE idUsuario = @idUsuario;
    SET @filas = @@ROWCOUNT;

    DELETE FROM dbo.UsuarioSegundoFactor WHERE idUsuario = @idUsuario;
    SET @filas = @filas + @@ROWCOUNT;

    IF @filas > 0
        UPDATE dbo.Usuarios SET selloSesion = NEWID() WHERE idUsuario = @idUsuario;

    COMMIT;

    SELECT CAST(CASE WHEN @filas > 0 THEN 1 ELSE 0 END AS BIT) AS quitado;
END
