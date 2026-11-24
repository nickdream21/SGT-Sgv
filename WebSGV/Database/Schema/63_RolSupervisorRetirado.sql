-- 63_RolSupervisorRetirado.sql (2026-11-24, Etapa 5, E5-19b, cierra U-090)
--
-- Retira el rol SUPERVISOR (decisión del usuario, 2026-11-24). CK_Usuarios_Rol (migración 54) se vuelve
-- a crear igual que allí, salvo que SUPERVISOR pasa a la lista de roles retirados: solo se admite en
-- cuentas inactivas (activo = 0). El historial (auditoría, cuentas antiguas) no se rompe, pero ya no se
-- puede crear ni reactivar una cuenta con ese rol. El código ya no lo reconoce (RolesHelper, E5-19b).
--
-- Antes de cambiar nada se detiene con THROW 50063 si hay alguna cuenta ACTIVA con rol SUPERVISOR: no
-- corrige datos; hay que desactivarla o cambiarle el rol (con permiso del usuario) y volver a correrla.
-- Control: SELECT activo, COUNT(*) AS n FROM dbo.Usuarios
--          WHERE UPPER(LTRIM(RTRIM(rol))) = 'SUPERVISOR' GROUP BY activo;
--
-- Idempotente: siempre deja el mismo CHECK (DROP si existe y ADD ... WITH CHECK, en una transacción).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @activos INT = (SELECT COUNT(*) FROM dbo.Usuarios
                        WHERE UPPER(LTRIM(RTRIM(rol))) = 'SUPERVISOR' AND activo = 1);
IF @activos > 0
BEGIN
    DECLARE @msg NVARCHAR(2048) = CONCAT(N'63_RolSupervisorRetirado: hay ', @activos,
        N' cuenta(s) activa(s) con rol SUPERVISOR. Desactívelas o cámbieles el rol antes de aplicar esta migración. ',
        N'Control: SELECT idUsuario, rol, activo FROM dbo.Usuarios WHERE UPPER(LTRIM(RTRIM(rol))) = ''SUPERVISOR'' AND activo = 1. No se cambió nada.');
    THROW 50063, @msg, 1;
END
GO

BEGIN TRAN;

IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = 'CK_Usuarios_Rol' AND parent_object_id = OBJECT_ID('dbo.Usuarios'))
    ALTER TABLE dbo.Usuarios DROP CONSTRAINT CK_Usuarios_Rol;

-- Roles vigentes; los retirados (SUPERVISOR, Grifo, Maquinaria, Operador) solo en cuentas inactivas.
ALTER TABLE dbo.Usuarios WITH CHECK ADD CONSTRAINT CK_Usuarios_Rol CHECK (
    rol IN ('ADMIN', 'ADMINISTRADOR', 'ADMINISTRADOR DE TRANSPORTE', 'ADMINISTRADOR DE SISTEMA', 'CONDUCTOR', 'CHOFER', 'CONTABILIDAD')
    OR (activo = 0 AND rol IN ('SUPERVISOR', 'ADMINISTRADOR DE GRIFO', 'ADMINISTRADOR DE MAQUINARIA', 'OPERADOR'))
);

COMMIT;
PRINT N'CK_Usuarios_Rol: SUPERVISOR solo en cuentas inactivas.';
GO
