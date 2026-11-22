-- 59_BorrarRevocarFirmaYPortador.sql (2026-11-21, tras publicar 92d4dc6; cierra M-054 y M-056; U-074)
--
-- 1. DROP de sp_DC_RevocarFirma. Sin uso desde el 2026-11-15 (se quitó el WebMethod RechazarLiquidacion);
--    lo nombraba solo FirmaService.RevocarFirma, que nadie llamaba y se borra en el mismo cambio. El sitio
--    publicado (92d4dc6) conserva ese método, pero ninguna pantalla lo invoca.
-- 2. MovimientoDineroViaje.idConductorPortador (con su FK FK_MovimientoDineroViaje_Portador): la migración 43
--    dejó de exigirla el 2026-11-11 y ni el código publicado (92d4dc6) ni el de HEAD la leen ni la escriben.
--    sp_LD_RegistrarMovimientoViaje aceptaba e ignoraba @idConductorPortador; en este cambio deja de
--    aceptarlo (ningún C# lo pasa): ESE SP SE APLICA ANTES QUE ESTA MIGRACIÓN (línea "Requiere SP" de abajo;
--    si el SP viejo sigue en la BD, el paso 0 se detiene con 59002).
--    Medido en sgvActualizada el 2026-11-21: 3 movimientos, todos anulados (activo = 0, de prueba del
--    2026-11-11); 1 con portador. Tope: como mucho 1 fila con valor y ninguna activa (59003).
--
-- Antes de borrar busca módulos que se quedan y nombran como palabra el SP o la columna (THROW 59001/59002).
-- Idempotente (OBJECT_ID / COL_LENGTH).
--
-- Requiere SP: sp_LD_RegistrarMovimientoViaje.sql

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

-- ------------------------------------------------------------------ 0. Controles (no borra nada)
DECLARE @msg NVARCHAR(2048), @modulos NVARCHAR(MAX);

SELECT @modulos = STUFF((
    SELECT N', ' + o.name
    FROM sys.sql_modules m JOIN sys.objects o ON o.object_id = m.object_id
    WHERE o.name <> N'sp_DC_RevocarFirma'
      AND PATINDEX(N'%[^A-Za-z0-9_]sp_DC_RevocarFirma[^A-Za-z0-9_]%', N' ' + m.definition + N' ') > 0
    ORDER BY o.name FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @modulos IS NOT NULL
BEGIN
    SET @msg = N'59_BorrarRevocarFirmaYPortador: objetos que se quedan nombran sp_DC_RevocarFirma: ' + @modulos
             + N'. Revisarlos; no se borró nada.';
    THROW 59001, @msg, 1;
END

SELECT @modulos = STUFF((
    SELECT N', ' + o.name
    FROM sys.sql_modules m JOIN sys.objects o ON o.object_id = m.object_id
    WHERE PATINDEX(N'%[^A-Za-z0-9_]idConductorPortador[^A-Za-z0-9_]%', N' ' + m.definition + N' ') > 0
    ORDER BY o.name FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @modulos IS NOT NULL
BEGIN
    SET @msg = N'59_BorrarRevocarFirmaYPortador: objetos que se quedan nombran idConductorPortador: ' + @modulos
             + N'. Aplicar antes sp_LD_RegistrarMovimientoViaje.sql o revisar ese objeto; no se borró nada.';
    THROW 59002, @msg, 1;
END

IF COL_LENGTH('dbo.MovimientoDineroViaje', 'idConductorPortador') IS NOT NULL
BEGIN
    DECLARE @conValor INT, @activos INT;
    EXEC sp_executesql N'SELECT @conValor = COUNT(*), @activos = SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END)
                         FROM dbo.MovimientoDineroViaje WHERE idConductorPortador IS NOT NULL;',
        N'@conValor INT OUTPUT, @activos INT OUTPUT', @conValor = @conValor OUTPUT, @activos = @activos OUTPUT;
    PRINT CONCAT('MovimientoDineroViaje.idConductorPortador: ', @conValor, ' fila(s) con valor, ', ISNULL(@activos, 0), ' activa(s).');
    IF @conValor > 1 OR ISNULL(@activos, 0) > 0
    BEGIN
        SET @msg = CONCAT(N'59_BorrarRevocarFirmaYPortador: ', @conValor, N' movimiento(s) con portador (', ISNULL(@activos, 0),
                          N' activo(s)); medido el 2026-11-21: 1, anulado. Revisar esos datos antes; no se borró nada.');
        THROW 59003, @msg, 1;
    END
END
GO

-- ------------------------------------------------------------------ 1. sp_DC_RevocarFirma
IF OBJECT_ID(N'dbo.sp_DC_RevocarFirma', 'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.sp_DC_RevocarFirma;
    PRINT 'PROCEDURE sp_DC_RevocarFirma: borrado.';
END
GO

-- ------------------------------------------------------------------ 2. idConductorPortador
IF COL_LENGTH('dbo.MovimientoDineroViaje', 'idConductorPortador') IS NOT NULL
BEGIN
    DECLARE @nombre SYSNAME, @sql NVARCHAR(400);
    -- FK (FK_MovimientoDineroViaje_Portador u otro nombre en otra BD)
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT f.name FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
        WHERE f.parent_object_id = OBJECT_ID('dbo.MovimientoDineroViaje')
          AND COL_NAME(fc.parent_object_id, fc.parent_column_id) = 'idConductorPortador';
    OPEN c; FETCH NEXT FROM c INTO @nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'ALTER TABLE dbo.MovimientoDineroViaje DROP CONSTRAINT ' + QUOTENAME(@nombre) + N';';
        EXEC (@sql);
        PRINT 'FK ' + @nombre + ': borrada.';
        FETCH NEXT FROM c INTO @nombre;
    END
    CLOSE c; DEALLOCATE c;

    -- Índices y DEFAULT sobre la columna, si otra BD los tuviera (en sgvActualizada no hay).
    DECLARE i CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT ix.name FROM sys.indexes ix
        JOIN sys.index_columns ic ON ic.object_id = ix.object_id AND ic.index_id = ix.index_id
        WHERE ix.object_id = OBJECT_ID('dbo.MovimientoDineroViaje') AND ix.is_primary_key = 0
          AND COL_NAME(ic.object_id, ic.column_id) = 'idConductorPortador';
    OPEN i; FETCH NEXT FROM i INTO @nombre;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'DROP INDEX ' + QUOTENAME(@nombre) + N' ON dbo.MovimientoDineroViaje;';
        EXEC (@sql);
        PRINT 'Índice ' + @nombre + ': borrado.';
        FETCH NEXT FROM i INTO @nombre;
    END
    CLOSE i; DEALLOCATE i;

    SET @nombre = (SELECT dc.name FROM sys.default_constraints dc
                   WHERE dc.parent_object_id = OBJECT_ID('dbo.MovimientoDineroViaje')
                     AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = 'idConductorPortador');
    IF @nombre IS NOT NULL
    BEGIN
        SET @sql = N'ALTER TABLE dbo.MovimientoDineroViaje DROP CONSTRAINT ' + QUOTENAME(@nombre) + N';';
        EXEC (@sql);
    END

    ALTER TABLE dbo.MovimientoDineroViaje DROP COLUMN idConductorPortador;
    PRINT 'MovimientoDineroViaje.idConductorPortador: borrada.';
END
GO

IF OBJECT_ID(N'dbo.sp_DC_RevocarFirma', 'P') IS NOT NULL OR COL_LENGTH('dbo.MovimientoDineroViaje', 'idConductorPortador') IS NOT NULL
    THROW 59004, N'59_BorrarRevocarFirmaYPortador: quedó el SP o la columna sin borrar.', 1;
PRINT '59_BorrarRevocarFirmaYPortador: listo.';
GO
