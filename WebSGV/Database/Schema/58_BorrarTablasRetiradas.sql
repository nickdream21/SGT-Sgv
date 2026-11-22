-- 58_BorrarTablasRetiradas.sql (2026-11-21, Etapa 3, E3-8; cierra M-058; U-074)
--
-- 1. Usuarios.idOperador (con su FK a Operadores) y OrdenViaje.tipoViaje (con su DEFAULT): ninguna
--    pantalla los usa. tipoViaje solo lo escribía sp_DC_InsertarOrdenViajeConductor, que en este mismo
--    cambio deja de hacerlo: ESE SP SE APLICA ANTES QUE ESTA MIGRACIÓN. aplicar-migraciones.ps1 lo aplica
--    solo por la línea "Requiere SP" de abajo; si el SP viejo sigue en la BD, el paso 0 se detiene (58005).
-- 2. ViajesEnProgreso.observaciones: de ntext (obsoleto) a nvarchar(max), con su misma intercalación.
-- 3. DROP de 18 tablas retiradas, vacías salvo Operadores y EquiposMaquinaria (1 fila de maestro cada una,
--    medido el 2026-11-21): Maquinaria (PartesDiariosTrabajo, AsignacionesMaquinaria, Operadores,
--    EquiposMaquinaria), ubigeo sin uso (Distrito, Provincia, Departamento, Pais), CPIC_Productos (con su
--    trigger TR_CPIC_Productos_Auditoria), detalle viejo de la liquidación (DetalleAlimentacion,
--    DetalleApoyoSeguridad, DetalleEncapada, DetalleMovilidad) y el modelo de segmentos y subtramos
--    (DetalleSegmento, SegmentosOrdenViaje, ProductosOperacion, OperacionesSubTramo, SubTramos). Ninguna la
--    usa el código publicado (60853e8), el de HEAD ni un objeto de la BD que se quede.
--
-- Fuera a propósito (los usa el código publicado o una tabla que se queda; lo decide el usuario):
-- AbastecimientoCombustible, DespachoCombustibleObra, IngresoCombustibleEcuador (VehiculoService de 60853e8
-- los consulta al cambiar la placa), DetalleTicketEcuador, camionetas, volquetes, LugarAbastecimiento,
-- TipoCarro, Obras y ClientesObra (cuelgan de esas por FK), GuiasTransportista (FK desde DetalleOrdenViaje
-- y Manifiesto), OrdenViajeAjuste, Indicadores y UploadHistory.
--
-- Antes de borrar: cuenta las filas de cada tabla (THROW 58001 si hay más que las medidas), busca FK que
-- lleguen desde una tabla que se queda (THROW 58002) y módulos que se quedan y nombran una tabla o columna
-- que se borra (THROW 58005). Así, en otra BD se detiene sin borrar nada. Idempotente (IF OBJECT_ID /
-- COL_LENGTH).
--
-- Requiere SP: sp_DC_InsertarOrdenViajeConductor.sql

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

-- ------------------------------------------------------------------ 0. Lista y controles (no borra nada)
IF OBJECT_ID('tempdb..#retiradas') IS NOT NULL DROP TABLE #retiradas;
CREATE TABLE #retiradas (orden INT NOT NULL PRIMARY KEY, tabla SYSNAME NOT NULL, maxFilas INT NOT NULL, filas INT NULL);
INSERT INTO #retiradas (orden, tabla, maxFilas) VALUES
    (1, 'PartesDiariosTrabajo', 0), (2, 'AsignacionesMaquinaria', 0), (3, 'Operadores', 1), (4, 'EquiposMaquinaria', 1),
    (5, 'Distrito', 0), (6, 'Provincia', 0), (7, 'Departamento', 0), (8, 'Pais', 0),
    (9, 'CPIC_Productos', 0),
    (10, 'DetalleAlimentacion', 0), (11, 'DetalleApoyoSeguridad', 0), (12, 'DetalleEncapada', 0), (13, 'DetalleMovilidad', 0),
    (14, 'DetalleSegmento', 0), (15, 'SegmentosOrdenViaje', 0),
    (16, 'ProductosOperacion', 0), (17, 'OperacionesSubTramo', 0), (18, 'SubTramos', 0);

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
    SELECT @msg = N'58_BorrarTablasRetiradas: hay más filas que las medidas el 2026-11-21 en ' + STUFF((
        SELECT N', ' + tabla + N' (' + CAST(filas AS NVARCHAR(10)) + N' > ' + CAST(maxFilas AS NVARCHAR(10)) + N')'
        FROM #retiradas WHERE filas > maxFilas FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'')
        + N'. Revisar esos datos (respaldo o D- nuevo) antes; no se borró nada.';
    THROW 58001, @msg, 1;
END

-- FK que llegan a una tabla de la lista desde una que se queda (Usuarios.idOperador se quita en el paso 1).
DECLARE @fks NVARCHAR(MAX) = STUFF((
    SELECT N', ' + OBJECT_NAME(f.parent_object_id) + N'.' + f.name + N' -> ' + OBJECT_NAME(f.referenced_object_id)
    FROM sys.foreign_keys f
    WHERE OBJECT_NAME(f.referenced_object_id) IN (SELECT tabla FROM #retiradas)
      AND OBJECT_NAME(f.parent_object_id) NOT IN (SELECT tabla FROM #retiradas)
      AND NOT (f.parent_object_id = OBJECT_ID('dbo.Usuarios') AND OBJECT_NAME(f.referenced_object_id) = 'Operadores')
    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @fks IS NOT NULL
BEGIN
    SET @msg = N'58_BorrarTablasRetiradas: FK desde tablas que se quedan: ' + @fks + N'. No se borró nada.';
    THROW 58002, @msg, 1;
END

-- Módulos (SP, vistas, funciones, triggers) que se quedan y nombran como palabra una tabla de la lista o una
-- columna que se borra. No cuentan los triggers de las propias tablas que se borran ni los alias revisados.
DECLARE @nombres TABLE (nombre SYSNAME NOT NULL);
INSERT INTO @nombres (nombre) SELECT tabla FROM #retiradas;
INSERT INTO @nombres (nombre) VALUES ('idOperador'), ('tipoViaje');
DECLARE @permitidos TABLE (modulo SYSNAME NOT NULL, nombre SYSNAME NOT NULL);
INSERT INTO @permitidos VALUES ('sp_ObtenerHistorialViajesConductor', 'tipoViaje');   -- alias "AS TipoViaje" de esInternacional
DECLARE @modulos NVARCHAR(MAX) = STUFF((
    SELECT N', ' + o.name + N' (' + n.nombre + N')'
    FROM sys.sql_modules m
    JOIN sys.objects o ON o.object_id = m.object_id
    JOIN @nombres n ON PATINDEX(N'%[^A-Za-z0-9_]' + n.nombre + N'[^A-Za-z0-9_]%', N' ' + m.definition + N' ') > 0
    WHERE NOT (o.type = 'TR' AND OBJECT_NAME(o.parent_object_id) IN (SELECT tabla FROM #retiradas))
      AND NOT EXISTS (SELECT 1 FROM @permitidos p WHERE p.modulo = o.name AND p.nombre = n.nombre)
    ORDER BY o.name
    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');
IF @modulos IS NOT NULL
BEGIN
    SET @msg = N'58_BorrarTablasRetiradas: objetos que se quedan nombran algo que se borra: ' + @modulos
             + N'. Aplicar antes sp_DC_InsertarOrdenViajeConductor.sql (y la migración 57) o revisar ese objeto; no se borró nada.';
    THROW 58005, @msg, 1;
END
GO

-- ------------------------------------------------------------------ 1. Columnas sin uso
IF COL_LENGTH('dbo.Usuarios', 'idOperador') IS NOT NULL
BEGIN
    DECLARE @n INT = (SELECT COUNT(*) FROM dbo.Usuarios WHERE idOperador IS NOT NULL);
    IF @n > 0
    BEGIN
        DECLARE @m NVARCHAR(400) = CONCAT(N'58_BorrarTablasRetiradas: ', @n, N' usuario(s) con idOperador; revisarlos antes.');
        THROW 58003, @m, 1;
    END
    DECLARE @fk SYSNAME, @sql NVARCHAR(400);
    DECLARE c CURSOR LOCAL FAST_FORWARD FOR
        SELECT f.name FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = f.object_id
        WHERE f.parent_object_id = OBJECT_ID('dbo.Usuarios') AND COL_NAME(fc.parent_object_id, fc.parent_column_id) = 'idOperador';
    OPEN c; FETCH NEXT FROM c INTO @fk;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'ALTER TABLE dbo.Usuarios DROP CONSTRAINT ' + QUOTENAME(@fk) + N';';
        EXEC (@sql);
        FETCH NEXT FROM c INTO @fk;
    END
    CLOSE c; DEALLOCATE c;
    ALTER TABLE dbo.Usuarios DROP COLUMN idOperador;
    PRINT 'Usuarios.idOperador: borrada.';
END
GO

IF COL_LENGTH('dbo.OrdenViaje', 'tipoViaje') IS NOT NULL
BEGIN
    DECLARE @df SYSNAME = (SELECT dc.name FROM sys.default_constraints dc
                           WHERE dc.parent_object_id = OBJECT_ID('dbo.OrdenViaje')
                             AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = 'tipoViaje');
    IF @df IS NOT NULL
    BEGIN
        DECLARE @sqlDf NVARCHAR(400) = N'ALTER TABLE dbo.OrdenViaje DROP CONSTRAINT ' + QUOTENAME(@df) + N';';
        EXEC (@sqlDf);
    END
    DECLARE @conValor INT = (SELECT COUNT(*) FROM dbo.OrdenViaje WHERE tipoViaje IS NOT NULL);
    PRINT CONCAT('OrdenViaje.tipoViaje: ', @conValor, ' fila(s) con valor.');
    ALTER TABLE dbo.OrdenViaje DROP COLUMN tipoViaje;
    PRINT 'OrdenViaje.tipoViaje: borrada.';
END
GO

-- ------------------------------------------------------------------ 2. ntext a nvarchar(max)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ViajesEnProgreso') AND name = 'observaciones'
             AND TYPE_NAME(user_type_id) = 'ntext')
BEGIN
    DECLARE @intercalacion SYSNAME, @nula BIT;
    SELECT @intercalacion = collation_name, @nula = is_nullable
    FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ViajesEnProgreso') AND name = 'observaciones';
    DECLARE @sqlCol NVARCHAR(400) = N'ALTER TABLE dbo.ViajesEnProgreso ALTER COLUMN observaciones NVARCHAR(MAX) COLLATE '
                                  + @intercalacion + CASE WHEN @nula = 1 THEN N' NULL;' ELSE N' NOT NULL;' END;
    EXEC (@sqlCol);
    PRINT 'ViajesEnProgreso.observaciones: nvarchar(max).';
END
GO

-- ------------------------------------------------------------------ 3. Tablas
-- Primero todas las FK que salen de una tabla de la lista o llegan a ella (el orden de DROP no depende así
-- de las FK que tenga cada BD; las que llegan desde tablas que se quedan ya las descartó el paso 0).
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
    THROW 58004, N'58_BorrarTablasRetiradas: quedó alguna tabla de la lista sin borrar.', 1;

PRINT CONCAT('58_BorrarTablasRetiradas: listo (', @borradas, ' tablas borradas en esta ejecución).');
DROP TABLE #retiradas;
GO
