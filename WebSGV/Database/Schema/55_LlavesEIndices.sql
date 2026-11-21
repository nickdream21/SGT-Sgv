-- 55_LlavesEIndices.sql (2026-11-21, Etapa 3, E3-6)
--
-- 1. FK repetidas: se quitan las que duplican a otra (misma tabla, columnas, tabla referida y acciones);
--    queda la más antigua. En sgvActualizada son 14 (6 en AbastecimientoCombustible, 5 en OrdenViaje,
--    2 en DetalleOrdenViaje y 1 en GuiasTransportista), con nombres generados por SQL Server que en otra
--    BD serán distintos: por eso se buscan por estructura y no por nombre.
-- 2. Cascadas de documentos a NO ACTION: borrar una Factura o un CPIC borraba en cascada las filas de
--    DocumentosFactura / DocumentosCPIC y dejaba sus PDF huérfanos en ~/Uploads. Ningún código borra
--    facturas ni CPIC; si alguna vez se hace, que falle y obligue a tratar los documentos.
-- 3. Las 10 FK que faltaban (huérfanos medidos el 2026-11-21: 0 tras E3-5). Sin FK a propósito:
--    AuditoriaLog.IdUsuario, Auditoria.IdRegistro y FirmaDigital.idDocumento (auditoría polimórfica;
--    las firmas sobreviven al retiro de la orden, DN-04 y DN-06).
-- 4. Índices repetidos en Despachos: IX_Despachos_Fecha e IX_Despachos_FechaDespacho (fechaDespacho,
--    estadoDespacho) los cubre IX_Despachos_Fecha_Estado; IX_Despachos_Conductor (idConductor,
--    fechaDespacho) lo cubre IX_Despachos_Conductor_Fecha. Solo se quitan si el que los cubre existe.
-- 5. Índices de apoyo para FK sin índice en tablas con datos: Despachos.idFactura, idCPIC e
--    idOrdenViaje; SeguimientoExportacion.idDespachoDestino; MovimientoDineroViaje.idViajeProgreso.
--
-- Antes de cada FK cuenta los huérfanos y se detiene con THROW 55001 si hay alguno. Todo WITH CHECK
-- (is_not_trusted = 0). Idempotente. Compatible con el código publicado (60853e8).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
GO

-- Duración (se imprime al final).
IF OBJECT_ID('tempdb..#inicio') IS NOT NULL DROP TABLE #inicio;
SELECT SYSDATETIME() AS t INTO #inicio;
GO

-- ------------------------------------------------------------------ 1. FK repetidas
DECLARE @fk TABLE (object_id INT PRIMARY KEY, nombre SYSNAME, tabla NVARCHAR(300), firma NVARCHAR(MAX));
INSERT INTO @fk (object_id, nombre, tabla, firma)
SELECT fk.object_id, fk.name, QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(fk.parent_object_id)),
       CONCAT(fk.parent_object_id, '|', fk.referenced_object_id, '|', fk.delete_referential_action, '|', fk.update_referential_action, '|',
              (SELECT CONCAT(fc.parent_column_id, '>', fc.referenced_column_id, ',')
               FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id = fk.object_id
               ORDER BY fc.constraint_column_id FOR XML PATH('')))
FROM sys.foreign_keys fk;

DECLARE @sobra TABLE (nombre SYSNAME, tabla NVARCHAR(300));
INSERT INTO @sobra (nombre, tabla)
SELECT a.nombre, a.tabla FROM @fk a
WHERE EXISTS (SELECT 1 FROM @fk b WHERE b.firma = a.firma AND b.object_id < a.object_id);

DECLARE @nombre SYSNAME, @tabla NVARCHAR(300), @sql NVARCHAR(MAX), @quitadas INT = 0;
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT nombre, tabla FROM @sobra ORDER BY tabla, nombre;
OPEN c;
FETCH NEXT FROM c INTO @nombre, @tabla;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE ' + @tabla + N' DROP CONSTRAINT ' + QUOTENAME(@nombre) + N';';
    EXEC (@sql);
    SET @quitadas += 1;
    FETCH NEXT FROM c INTO @nombre, @tabla;
END
CLOSE c; DEALLOCATE c;
PRINT CONCAT('FK repetidas quitadas: ', @quitadas, '.');
GO

-- ------------------------------------------------------------------ utilidades (solo esta sesión)
IF OBJECT_ID('tempdb..#AgregarFk') IS NOT NULL DROP PROCEDURE #AgregarFk;
GO
-- Crea la FK (NO ACTION) si no hay ya una FK de esa columna a esa tabla; si la hay con otra acción
-- de borrado o actualización, la vuelve a crear como NO ACTION con el nombre indicado.
CREATE PROCEDURE #AgregarFk
    @tabla     SYSNAME,     -- 'dbo.X'
    @columna   SYSNAME,
    @referida  SYSNAME,     -- 'dbo.Y'
    @colRef    SYSNAME,
    @nombre    SYSNAME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sql NVARCHAR(MAX), @n INT, @msg NVARCHAR(2048), @existente SYSNAME, @accion INT;

    SELECT TOP 1 @existente = fk.name, @accion = fk.delete_referential_action + fk.update_referential_action
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID(@tabla) AND fk.referenced_object_id = OBJECT_ID(@referida)
      -- Por id de columna, no por nombre: un procedimiento temporal compara con la intercalación de tempdb.
      AND fc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(@tabla), @columna, 'ColumnId')
      AND (SELECT COUNT(*) FROM sys.foreign_key_columns x WHERE x.constraint_object_id = fk.object_id) = 1;

    IF @existente IS NOT NULL AND @accion = 0
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @existente AND (is_not_trusted = 1 OR is_disabled = 1))
        BEGIN
            SET @sql = N'ALTER TABLE ' + @tabla + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@existente) + N';';
            EXEC (@sql);
            PRINT @existente + N': revalidada.';
        END
        RETURN;
    END

    SET @sql = N'SELECT @n = COUNT(*) FROM ' + @tabla + N' x WHERE x.' + QUOTENAME(@columna) + N' IS NOT NULL'
             + N' AND NOT EXISTS (SELECT 1 FROM ' + @referida + N' r WHERE r.' + QUOTENAME(@colRef) + N' = x.' + QUOTENAME(@columna) + N');';
    EXEC sp_executesql @sql, N'@n INT OUTPUT', @n = @n OUTPUT;
    IF @n > 0
    BEGIN
        SET @msg = CONCAT(N'55_LlavesEIndices: ', @n, N' fila(s) de ', @tabla, N'.', @columna, N' sin fila en ', @referida,
                          N'; no se crea ', @nombre, N'. Corregir los datos antes (D- nuevo).');
        THROW 55001, @msg, 1;
    END

    IF @existente IS NOT NULL
    BEGIN
        SET @sql = N'ALTER TABLE ' + @tabla + N' DROP CONSTRAINT ' + QUOTENAME(@existente) + N';';
        EXEC (@sql);
        PRINT @existente + N': quitada (tenía CASCADE).';
    END

    SET @sql = N'ALTER TABLE ' + @tabla + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@nombre)
             + N' FOREIGN KEY (' + QUOTENAME(@columna) + N') REFERENCES ' + @referida + N' (' + QUOTENAME(@colRef) + N');';
    EXEC (@sql);
    PRINT @nombre + N': creada.';
END
GO

-- ------------------------------------------------------------------ 2. Cascadas de documentos
EXEC #AgregarFk 'dbo.DocumentosFactura', 'idFactura', 'dbo.Factura', 'idFactura', 'FK_DocumentosFactura_Factura';
EXEC #AgregarFk 'dbo.DocumentosCPIC',    'idCPIC',    'dbo.CPIC',    'idCPIC',    'FK_DocumentosCPIC_CPIC';
GO

-- ------------------------------------------------------------------ 3. Las 10 FK que faltaban
EXEC #AgregarFk 'dbo.FirmaDigital',           'idUsuarioFirmante',     'dbo.Usuarios',         'idUsuario',       'FK_FirmaDigital_UsuarioFirmante';
EXEC #AgregarFk 'dbo.MovimientoDineroViaje',  'idViajeProgreso',       'dbo.ViajesEnProgreso', 'idViajeProgreso', 'FK_MovimientoDineroViaje_ViajeProgreso';
EXEC #AgregarFk 'dbo.MovimientoDineroViaje',  'idUsuarioRegistro',     'dbo.Usuarios',         'idUsuario',       'FK_MovimientoDineroViaje_UsuarioRegistro';
EXEC #AgregarFk 'dbo.MovimientoDineroViaje',  'idUsuarioAnulacion',    'dbo.Usuarios',         'idUsuario',       'FK_MovimientoDineroViaje_UsuarioAnulacion';
EXEC #AgregarFk 'dbo.OrdenViajeAjuste',       'idUsuarioAdmin',        'dbo.Usuarios',         'idUsuario',       'FK_OrdenViajeAjuste_UsuarioAdmin';
EXEC #AgregarFk 'dbo.SeguimientoExportacion', 'idDespachoOrigen',      'dbo.Despachos',        'idDespacho',      'FK_SeguimientoExportacion_DespachoOrigen';
EXEC #AgregarFk 'dbo.SeguimientoExportacion', 'idDespachoDestino',     'dbo.Despachos',        'idDespacho',      'FK_SeguimientoExportacion_DespachoDestino';
EXEC #AgregarFk 'dbo.SeguimientoExportacion', 'idUsuarioRegistro',     'dbo.Usuarios',         'idUsuario',       'FK_SeguimientoExportacion_UsuarioRegistro';
EXEC #AgregarFk 'dbo.SeguimientoExportacion', 'idUsuarioModificacion', 'dbo.Usuarios',         'idUsuario',       'FK_SeguimientoExportacion_UsuarioModificacion';
EXEC #AgregarFk 'dbo.SeguimientoHito',        'idUsuarioCorreccion',   'dbo.Usuarios',         'idUsuario',       'FK_SeguimientoHito_UsuarioCorreccion';
GO

-- ------------------------------------------------------------------ 4. Índices repetidos en Despachos
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_Fecha_Estado')
BEGIN
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_Fecha')
    BEGIN DROP INDEX IX_Despachos_Fecha ON dbo.Despachos; PRINT 'IX_Despachos_Fecha: quitado.'; END
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_FechaDespacho')
    BEGIN DROP INDEX IX_Despachos_FechaDespacho ON dbo.Despachos; PRINT 'IX_Despachos_FechaDespacho: quitado.'; END
END
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_Conductor_Fecha')
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_Conductor')
BEGIN DROP INDEX IX_Despachos_Conductor ON dbo.Despachos; PRINT 'IX_Despachos_Conductor: quitado.'; END
GO

-- ------------------------------------------------------------------ 5. Índices de apoyo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_Factura')
    CREATE NONCLUSTERED INDEX IX_Despachos_Factura ON dbo.Despachos (idFactura) WHERE idFactura IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_CPIC')
    CREATE NONCLUSTERED INDEX IX_Despachos_CPIC ON dbo.Despachos (idCPIC) WHERE idCPIC IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Despachos') AND name = 'IX_Despachos_OrdenViaje')
    CREATE NONCLUSTERED INDEX IX_Despachos_OrdenViaje ON dbo.Despachos (idOrdenViaje) WHERE idOrdenViaje IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.SeguimientoExportacion') AND name = 'IX_SeguimientoExportacion_DespachoDestino')
    CREATE NONCLUSTERED INDEX IX_SeguimientoExportacion_DespachoDestino ON dbo.SeguimientoExportacion (idDespachoDestino) WHERE idDespachoDestino IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.MovimientoDineroViaje') AND name = 'IX_MovimientoDineroViaje_ViajeProgreso')
    CREATE NONCLUSTERED INDEX IX_MovimientoDineroViaje_ViajeProgreso ON dbo.MovimientoDineroViaje (idViajeProgreso);
PRINT 'Índices de apoyo: listos.';
GO

-- ------------------------------------------------------------------ 6. Control final
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_not_trusted = 1 OR is_disabled = 1)
    THROW 55002, N'55_LlavesEIndices: hay FK sin validar o deshabilitadas.', 1;
IF EXISTS (SELECT 1 FROM sys.foreign_keys
           WHERE parent_object_id IN (OBJECT_ID('dbo.DocumentosFactura'), OBJECT_ID('dbo.DocumentosCPIC'))
             AND delete_referential_action <> 0)
    THROW 55003, N'55_LlavesEIndices: DocumentosFactura o DocumentosCPIC siguen con borrado en cascada.', 1;
DECLARE @ms INT = DATEDIFF(MILLISECOND, (SELECT t FROM #inicio), SYSDATETIME());
PRINT CONCAT('55_LlavesEIndices: listo en ', @ms, ' ms.');
GO

DROP PROCEDURE #AgregarFk;
GO
DROP TABLE #inicio;
GO
