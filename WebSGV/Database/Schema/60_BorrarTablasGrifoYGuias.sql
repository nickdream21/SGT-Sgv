-- 60_BorrarTablasGrifoYGuias.sql (2026-11-21, tras publicar 92d4dc6; parte de M-106; U-074)
--
-- DROP de 8 tablas que ni el código publicado (92d4dc6) ni el de HEAD usan, ni otro objeto de la BD que se
-- quede (revisado el 2026-11-21 con git grep y sys.sql_modules / sys.sql_expression_dependencies):
--   Grifo e Ingreso de combustible en Ecuador (módulo retirado; sus SP se fueron en la 57): DetalleTicketEcuador,
--   IngresoCombustibleEcuador, DespachoCombustibleObra, AbastecimientoCombustible (con su trigger
--   TR_AbastecimientoCombustible_Auditoria, el único objeto de la BD sin archivo en el repo) y sus catálogos
--   LugarAbastecimiento (2 filas: "Grifo Cochera 03", "Otro") y TipoCarro (6 tipos de vehículo para el grifo).
--   GuiasTransportista (con su trigger TR_GuiasTransportista_Auditoria, de la migración 52) y OrdenViajeAjuste.
-- Medido en sgvActualizada el 2026-11-21: todas vacías salvo los dos catálogos.
--
-- Se quitan también dos FK de tablas que se quedan hacia GuiasTransportista: DetalleOrdenViaje.idGuia y
-- Manifiesto.idGuia. Las dos tablas están vacías y sin uso en el código; las columnas idGuia se quedan (sin FK)
-- hasta decidir el retiro de esas tablas. Si alguna fila tiene idGuia, se detiene (60003).
--
-- Fuera a propósito (tienen datos que pueden servir; lo decide el usuario): camionetas (23), volquetes (40),
-- Obras (1) y ClientesObra (1). Al borrar AbastecimientoCombustible y DespachoCombustibleObra pierden solo
-- las FK que llegaban desde ellas; sus datos no cambian.
--
-- Antes de borrar: cuenta las filas de cada tabla (THROW 60001 si hay más que las medidas), busca FK que
-- lleguen desde una tabla que se queda, salvo las dos de idGuia (60002), y módulos que se quedan y nombran una
-- tabla de la lista (60005). Así, en otra BD se detiene sin borrar nada. Idempotente (IF OBJECT_ID).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

-- ------------------------------------------------------------------ 0. Lista y controles (no borra nada)
IF OBJECT_ID('tempdb..#retiradas') IS NOT NULL DROP TABLE #retiradas;
CREATE TABLE #retiradas (orden INT NOT NULL PRIMARY KEY, tabla SYSNAME COLLATE DATABASE_DEFAULT NOT NULL, maxFilas INT NOT NULL, filas INT NULL);
INSERT INTO #retiradas (orden, tabla, maxFilas) VALUES
    (1, 'DetalleTicketEcuador', 0), (2, 'IngresoCombustibleEcuador', 0), (3, 'DespachoCombustibleObra', 0),
    (4, 'AbastecimientoCombustible', 0), (5, 'LugarAbastecimiento', 2), (6, 'TipoCarro', 6),
    (7, 'GuiasTransportista', 0), (8, 'OrdenViajeAjuste', 0);

-- FK desde tablas que se quedan que sí se quitan (vacías y sin uso en el código).
IF OBJECT_ID('tempdb..#fkPermitidas') IS NOT NULL DROP TABLE #fkPermitidas;
CREATE TABLE #fkPermitidas (tablaPadre SYSNAME COLLATE DATABASE_DEFAULT NOT NULL, columna SYSNAME COLLATE DATABASE_DEFAULT NOT NULL,
                            tablaRef SYSNAME COLLATE DATABASE_DEFAULT NOT NULL);
INSERT INTO #fkPermitidas VALUES ('DetalleOrdenViaje', 'idGuia', 'GuiasTransportista'), ('Manifiesto', 'idGuia', 'GuiasTransportista');

DECLARE @orden INT, @tabla SYSNAME, @n INT, @sql NVARCHAR(MAX), @msg NVARCHAR(2048);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT orden, tabla FROM #retiradas WHERE OBJECT_ID(N'dbo.' + tabla, 'U') IS NOT NULL;
OPEN c;
FETCH NEXT FROM c INTO @orden, @tabla;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @n = COUNT(*) FROM dbo.' + QUOTENAME(@tabla) + N';';
    EXEC sp_executesql @sql, N'@n INT OUTPUT', @n = @n OUTPUT;
    UPDATE #retiradas SET filas = @n WHERE orden = @orden;
    PRINT CONCAT(@tabla, ': ', @n, ' fila(s).');
    FETCH NEXT FROM c INTO @orden, @tabla;
END
CLOSE c; DEALLOCATE c;

IF EXISTS (SELECT 1 FROM #retiradas WHERE filas > maxFilas)
BEGIN
    SELECT @msg = N'60_BorrarTablasGrifoYGuias: hay más filas que las medidas el 2026-11-21 en ' + STUFF((
        SELECT N', ' + tabla + N' (' + CAST(filas AS NVARCHAR(10)) + N' > ' + CAST(maxFilas AS NVARCHAR(10)) + N')'
        FROM #retiradas WHERE filas > maxFilas FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'')
        + N'. Revisar esos datos (respaldo o D- nuevo) antes; no se borró nada.';
    THROW 60001, @msg, 1;
END

DECLARE @fks NVARCHAR(MAX) = STUFF((
    SELECT N', ' + OBJECT_NAME(f.parent_object_id) + N'.' + f.name + N' -> ' + OBJECT_NAME(f.referenced_object_id)
    FROM sys.foreign_keys f
    WHERE OBJECT_NAME(f.referenced_object_id) IN (SELECT tabla FROM #retiradas)
      AND OBJECT_NAME(f.parent_object_id) NOT IN (SELECT tabla FROM #retiradas)
      AND NOT EXISTS (SELECT 1 FROM #fkPermitidas p JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
                      WHERE p.tablaPadre = OBJECT_NAME(f.parent_object_id) AND p.tablaRef = OBJECT_NAME(f.referenced_object_id)
                        AND p.columna = COL_NAME(fc.parent_object_id, fc.parent_column_id))
    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @fks IS NOT NULL
BEGIN
    SET @msg = N'60_BorrarTablasGrifoYGuias: FK desde tablas que se quedan: ' + @fks + N'. No se borró nada.';
    THROW 60002, @msg, 1;
END

-- Las dos columnas idGuia que pierden su FK deben estar vacías.
DECLARE @padre SYSNAME, @col SYSNAME;
DECLARE p CURSOR LOCAL FAST_FORWARD FOR SELECT tablaPadre, columna FROM #fkPermitidas WHERE COL_LENGTH(N'dbo.' + tablaPadre, columna) IS NOT NULL;
OPEN p;
FETCH NEXT FROM p INTO @padre, @col;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'SELECT @n = COUNT(*) FROM dbo.' + QUOTENAME(@padre) + N' WHERE ' + QUOTENAME(@col) + N' IS NOT NULL;';
    EXEC sp_executesql @sql, N'@n INT OUTPUT', @n = @n OUTPUT;
    IF @n > 0
    BEGIN
        SET @msg = CONCAT(N'60_BorrarTablasGrifoYGuias: ', @n, N' fila(s) de ', @padre, N' con ', @col,
                          N' (medido el 2026-11-21: 0). Revisarlas antes; no se borró nada.');
        THROW 60003, @msg, 1;
    END
    FETCH NEXT FROM p INTO @padre, @col;
END
CLOSE p; DEALLOCATE p;

-- Módulos (SP, vistas, funciones, triggers) que se quedan y nombran como palabra una tabla de la lista.
-- No cuentan los triggers de las propias tablas que se borran.
DECLARE @modulos NVARCHAR(MAX) = STUFF((
    SELECT N', ' + o.name + N' (' + r.tabla + N')'
    FROM sys.sql_modules m
    JOIN sys.objects o ON o.object_id = m.object_id
    JOIN #retiradas r ON PATINDEX(N'%[^A-Za-z0-9_]' + r.tabla + N'[^A-Za-z0-9_]%', N' ' + m.definition + N' ') > 0
    WHERE NOT (o.type = 'TR' AND OBJECT_NAME(o.parent_object_id) IN (SELECT tabla FROM #retiradas))
    ORDER BY o.name
    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @modulos IS NOT NULL
BEGIN
    SET @msg = N'60_BorrarTablasGrifoYGuias: objetos que se quedan nombran una tabla que se borra: ' + @modulos
             + N'. Revisar ese objeto; no se borró nada.';
    THROW 60005, @msg, 1;
END
GO

-- ------------------------------------------------------------------ 1. FK y tablas
-- Primero todas las FK que salen de una tabla de la lista o llegan a ella (las que llegan desde tablas que se
-- quedan son solo las dos de idGuia: el paso 0 descartó las demás).
DECLARE @fk SYSNAME, @padre NVARCHAR(300), @sqlFk NVARCHAR(600);
DECLARE f CURSOR LOCAL FAST_FORWARD FOR
    SELECT fk.name, QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id))
    FROM sys.foreign_keys fk
    WHERE OBJECT_NAME(fk.parent_object_id) IN (SELECT tabla FROM #retiradas)
       OR OBJECT_NAME(fk.referenced_object_id) IN (SELECT tabla FROM #retiradas);
OPEN f;
FETCH NEXT FROM f INTO @fk, @padre;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sqlFk = N'ALTER TABLE ' + @padre + N' DROP CONSTRAINT ' + QUOTENAME(@fk) + N';';
    EXEC (@sqlFk);
    PRINT 'FK ' + @padre + N'.' + @fk + ': borrada.';
    FETCH NEXT FROM f INTO @fk, @padre;
END
CLOSE f; DEALLOCATE f;

DECLARE @tabla SYSNAME, @sql NVARCHAR(400), @borradas INT = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT tabla FROM #retiradas ORDER BY orden;
OPEN c;
FETCH NEXT FROM c INTO @tabla;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(N'dbo.' + @tabla, 'U') IS NOT NULL
    BEGIN
        SET @sql = N'DROP TABLE dbo.' + QUOTENAME(@tabla) + N';';
        EXEC (@sql);
        SET @borradas += 1;
        PRINT @tabla + ': borrada.';
    END
    FETCH NEXT FROM c INTO @tabla;
END
CLOSE c; DEALLOCATE c;

IF EXISTS (SELECT 1 FROM #retiradas WHERE OBJECT_ID(N'dbo.' + tabla, 'U') IS NOT NULL)
    THROW 60004, N'60_BorrarTablasGrifoYGuias: quedó alguna tabla de la lista sin borrar.', 1;

PRINT CONCAT('60_BorrarTablasGrifoYGuias: listo (', @borradas, ' tablas borradas en esta ejecución).');
DROP TABLE #retiradas;
DROP TABLE #fkPermitidas;
GO
