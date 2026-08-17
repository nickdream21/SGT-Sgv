-- ============================================================
-- Regla: un conductor sólo puede estar asociado a un usuario.
-- La aplicación usa Usuarios.idConductor para autorizar consultas
-- y firmas, por lo que una asociación ambigua es un riesgo de acceso.
-- ============================================================
SET NOCOUNT ON;

IF COL_LENGTH('dbo.Usuarios', 'idConductor') IS NULL
    THROW 50001, 'No existe Usuarios.idConductor. Aplique primero el esquema base.', 1;

IF EXISTS (
    SELECT idConductor
    FROM dbo.Usuarios
    WHERE idConductor IS NOT NULL
    GROUP BY idConductor
    HAVING COUNT(*) > 1
)
    THROW 50002, 'Existen conductores asociados a más de un usuario. Corrija los duplicados antes de crear el índice.', 1;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Usuarios')
      AND name = 'UX_Usuarios_idConductor'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Usuarios_idConductor
        ON dbo.Usuarios (idConductor)
        WHERE idConductor IS NOT NULL;
END;
GO
