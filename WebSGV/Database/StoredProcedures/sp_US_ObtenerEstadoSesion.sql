-- ============================================================
-- Estado de acceso de un usuario para validar su sesión abierta (SesionVigenteHelper):
-- sello de sesión, si está activo, rol, conductor vinculado y si debe cambiar la contraseña.
-- Sin filas = el usuario ya no existe (la sesión se cierra).
-- ============================================================
CREATE OR ALTER PROCEDURE sp_US_ObtenerEstadoSesion
    @idUsuario INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.selloSesion,
           u.activo,
           LTRIM(RTRIM(u.rol)) AS rol,
           u.idConductor,
           CAST(ISNULL(u.requiereCambioContrasena, 0) AS BIT) AS requiereCambioContrasena
    FROM dbo.Usuarios u
    WHERE u.idUsuario = @idUsuario;
END
