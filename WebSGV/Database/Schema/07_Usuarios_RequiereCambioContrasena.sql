-- ============================================================
-- Agrega la columna que fuerza el cambio de contraseña en el
-- primer login de usuarios nuevos o migrados (Site.Master.cs,
-- Login.aspx.cs, GestionUsuarios.aspx.cs ya la leen/escriben).
-- ============================================================
SET NOCOUNT ON;

IF COL_LENGTH('dbo.Usuarios', 'requiereCambioContrasena') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios
        ADD requiereCambioContrasena BIT NOT NULL CONSTRAINT DF_Usuarios_requiereCambioContrasena DEFAULT (0);
END;
GO
