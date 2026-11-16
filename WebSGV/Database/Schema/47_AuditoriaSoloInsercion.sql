-- Auditoría de solo inserción (2026-11-16, auditoría de seguridad, tarea 8A: auditoría inalterable).
--
-- AuditoriaLog (la llena AuditoriaHelper) y la tabla heredada Auditoria (la llenan los triggers
-- TR_*_Auditoria) solo admiten INSERT: los triggers INSTEAD OF UPDATE / INSTEAD OF DELETE de abajo
-- rechazan cualquier cambio o borrado con THROW 51000, igual que FirmaDigital (02_FirmaDigital.sql).
-- Ningún SP ni código de la app modifica ni borra estas tablas.
--
-- LÍMITE: no es inviolable. Un usuario con db_owner (o ALTER sobre la tabla) puede
-- DISABLE TRIGGER / DROP TRIGGER o hacer TRUNCATE TABLE (que no dispara triggers). Esto disuade el
-- borrado accidental o casual y obliga a un paso explícito que queda en el historial de la BD; la
-- prueba BaseDeDatosTests verifica que los cuatro triggers existan y estén habilitados.
--
-- docs/migracion_somee/07_script_limpieza_transaccional_SOLO_NUEVA_BD.sql borra ambas tablas: debe
-- deshabilitar estos triggers antes (está anotado en su cabecera).
--
-- Idempotente: crea cada trigger solo si no existe.

IF OBJECT_ID('dbo.trg_AuditoriaLog_NoUpdate', 'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.trg_AuditoriaLog_NoUpdate
ON dbo.AuditoriaLog
INSTEAD OF UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N''La tabla AuditoriaLog es de solo inserción: sus registros no pueden modificarse.'', 1;
END');
GO

IF OBJECT_ID('dbo.trg_AuditoriaLog_NoDelete', 'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.trg_AuditoriaLog_NoDelete
ON dbo.AuditoriaLog
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N''La tabla AuditoriaLog es de solo inserción: sus registros no pueden eliminarse.'', 1;
END');
GO

IF OBJECT_ID('dbo.trg_Auditoria_NoUpdate', 'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.trg_Auditoria_NoUpdate
ON dbo.Auditoria
INSTEAD OF UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N''La tabla Auditoria es de solo inserción: sus registros no pueden modificarse.'', 1;
END');
GO

IF OBJECT_ID('dbo.trg_Auditoria_NoDelete', 'TR') IS NULL
    EXEC(N'CREATE TRIGGER dbo.trg_Auditoria_NoDelete
ON dbo.Auditoria
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    THROW 51000, N''La tabla Auditoria es de solo inserción: sus registros no pueden eliminarse.'', 1;
END');
GO
