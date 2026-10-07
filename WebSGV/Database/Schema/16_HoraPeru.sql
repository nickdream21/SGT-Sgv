-- ============================================================
-- 16_HoraPeru.sql
-- El servidor SQL de somee está en hora central de EE. UU. (UTC-6, o UTC-5 en su horario
-- de verano). GETDATE() coincidía con Perú (UTC-5, sin horario de verano) solo de marzo a
-- noviembre; el resto del año quedaba una hora atrasado.
--
-- 1. dbo.fn_AhoraPeru(): hora actual de Perú, independiente de la zona del servidor.
-- 2. Los DEFAULT (getdate()) de las columnas pasan a DEFAULT (dbo.fn_AhoraPeru()).
-- 3. Los procedimientos y triggers que usan getdate() se recrean usando dbo.fn_AhoraPeru().
--
-- Idempotente: una segunda ejecución no encuentra getdate() y no cambia nada.
-- Todo el paso 2+3 va en una transacción: si un objeto falla, no se modifica ninguno.
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
CREATE OR ALTER FUNCTION dbo.fn_AhoraPeru()
RETURNS DATETIME
AS
BEGIN
    -- Perú no tiene horario de verano: siempre UTC-05:00.
    RETURN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '-05:00') AS DATETIME);
END
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- ── 2. DEFAULT de columnas ──────────────────────────────────────────────────
DECLARE @defaults TABLE (restriccion SYSNAME, tabla NVARCHAR(300), columna SYSNAME);

INSERT INTO @defaults (restriccion, tabla, columna)
SELECT dc.name,
       QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name),
       c.name
FROM sys.default_constraints dc
JOIN sys.tables  t ON t.object_id = dc.parent_object_id
JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
WHERE LOWER(dc.definition) LIKE N'%getdate()%';

DECLARE @sql NVARCHAR(MAX), @restriccion SYSNAME, @tabla NVARCHAR(300), @columna SYSNAME;

DECLARE cur_defaults CURSOR LOCAL FAST_FORWARD FOR SELECT restriccion, tabla, columna FROM @defaults;
OPEN cur_defaults;
FETCH NEXT FROM cur_defaults INTO @restriccion, @tabla, @columna;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE ' + @tabla + N' DROP CONSTRAINT ' + QUOTENAME(@restriccion) + N';'
             + N'ALTER TABLE ' + @tabla + N' ADD CONSTRAINT ' + QUOTENAME(@restriccion)
             + N' DEFAULT (dbo.fn_AhoraPeru()) FOR ' + QUOTENAME(@columna) + N';';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM cur_defaults INTO @restriccion, @tabla, @columna;
END
CLOSE cur_defaults;
DEALLOCATE cur_defaults;

DECLARE @totalDefaults INT = (SELECT COUNT(*) FROM @defaults);
PRINT CONCAT('DEFAULT de columnas cambiados a dbo.fn_AhoraPeru(): ', @totalDefaults);

-- ── 3. Procedimientos y triggers ────────────────────────────────────────────
-- Se toma el texto original (CREATE ...), se reemplaza getdate() y se recrea el objeto.
-- La comparación usa una intercalación sin distinción de mayúsculas (GETDATE/getdate).
DECLARE @modulos TABLE (nombre NVARCHAR(300), tipo CHAR(2), definicion NVARCHAR(MAX), deshabilitado BIT, tablaTrigger NVARCHAR(300));

INSERT INTO @modulos (nombre, tipo, definicion, deshabilitado, tablaTrigger)
SELECT QUOTENAME(SCHEMA_NAME(o.schema_id)) + N'.' + QUOTENAME(o.name),
       o.type,
       m.definition,
       ISNULL(tr.is_disabled, 0),
       CASE WHEN o.type = 'TR'
            THEN QUOTENAME(OBJECT_SCHEMA_NAME(o.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(o.parent_object_id)) END
FROM sys.sql_modules m
JOIN sys.objects o ON o.object_id = m.object_id
LEFT JOIN sys.triggers tr ON tr.object_id = o.object_id
WHERE o.type IN ('P', 'TR')
  AND LOWER(m.definition) LIKE N'%getdate()%';

DECLARE @nombre NVARCHAR(300), @tipo CHAR(2), @definicion NVARCHAR(MAX), @deshabilitado BIT, @tablaTrigger NVARCHAR(300);

DECLARE cur_modulos CURSOR LOCAL FAST_FORWARD FOR
    SELECT nombre, tipo, definicion, deshabilitado, tablaTrigger FROM @modulos;
OPEN cur_modulos;
FETCH NEXT FROM cur_modulos INTO @nombre, @tipo, @definicion, @deshabilitado, @tablaTrigger;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @definicion = REPLACE(@definicion COLLATE Latin1_General_CI_AS, N'getdate()', N'dbo.fn_AhoraPeru()');

    SET @sql = CASE WHEN @tipo = 'P' THEN N'DROP PROCEDURE ' ELSE N'DROP TRIGGER ' END + @nombre;
    EXEC sp_executesql @sql;
    EXEC sp_executesql @definicion;

    IF @tipo = 'TR' AND @deshabilitado = 1
    BEGIN
        SET @sql = N'DISABLE TRIGGER ' + @nombre + N' ON ' + @tablaTrigger;
        EXEC sp_executesql @sql;
    END

    FETCH NEXT FROM cur_modulos INTO @nombre, @tipo, @definicion, @deshabilitado, @tablaTrigger;
END
CLOSE cur_modulos;
DEALLOCATE cur_modulos;

DECLARE @totalProcs INT = (SELECT COUNT(*) FROM @modulos WHERE tipo = 'P');
DECLARE @totalTriggers INT = (SELECT COUNT(*) FROM @modulos WHERE tipo = 'TR');
PRINT CONCAT('Procedimientos recreados con dbo.fn_AhoraPeru(): ', @totalProcs);
PRINT CONCAT('Triggers recreados con dbo.fn_AhoraPeru(): ', @totalTriggers);

COMMIT TRANSACTION;
GO
