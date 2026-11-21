-- 54_RestriccionesDominio.sql (2026-11-21, Etapa 3, E3-6, cierra M-050)
--
-- CHECK de dominio (estados, tipos, etapa de firma, rol), regla ANULADO <=> activo = 0 en Despachos,
-- montos >= 0, y los NOT NULL que faltaban. OrdenViaje.estadoAprobacion pierde el DEFAULT 'APROBADO'
-- (un INSERT que omitiera la columna daba la orden por aprobada sin firma) y pasa a NOT NULL con
-- DEFAULT 'PENDIENTE'.
--
-- Dominios medidos en sgvActualizada el 2026-11-21 (tras E3-5) y comparados con lo que escriben los SP,
-- el C# y las pruebas [FactBD]. Cada regla cuenta antes las filas que la violarían y se detiene con
-- THROW 54001 (con el conteo y la consulta) si hay alguna: no corrige datos. Todo se crea WITH CHECK
-- (is_not_trusted = 0). Idempotente: lo que ya existe se salta (y se revalida si quedó sin validar).
--
-- Compatible con el código publicado (60853e8) solo si antes se aplica sp_SE_ImportarLote de E3-6: la
-- pantalla ImportarSeguimiento publicada todavía manda COMPLETADO y PENDIENTE (los rechazaría
-- CK_SeguimientoExportacion_Estado) y ese SP los normaliza a FINALIZADO y EN_CURSO. Fuera de eso, ningún
-- SP ni pantalla escribe valores fuera de estos dominios (revisado contra 60853e8). Las tablas de
-- liquidación (OrdenViaje, Detalle*, Ingresos, Egresos...) están vacías.

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

-- Duración (se imprime al final).
IF OBJECT_ID('tempdb..#inicio') IS NOT NULL DROP TABLE #inicio;
SELECT SYSDATETIME() AS t INTO #inicio;
GO

-- ------------------------------------------------------------------ utilidades (solo esta sesión)
IF OBJECT_ID('tempdb..#AgregarCheck') IS NOT NULL DROP PROCEDURE #AgregarCheck;
GO
CREATE PROCEDURE #AgregarCheck
    @tabla  SYSNAME,          -- p. ej. 'dbo.Despachos'
    @nombre SYSNAME,
    @expr   NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sql NVARCHAR(MAX), @n INT, @msg NVARCHAR(2048);

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = @nombre AND parent_object_id = OBJECT_ID(@tabla))
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = @nombre AND (is_not_trusted = 1 OR is_disabled = 1))
        BEGIN
            SET @sql = N'ALTER TABLE ' + @tabla + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@nombre) + N';';
            EXEC (@sql);
            PRINT @nombre + N': revalidado.';
        END
        RETURN;
    END

    -- NOT (expr) excluye las filas en que expr es NULL: lo mismo que acepta un CHECK.
    SET @sql = N'SELECT @n = COUNT(*) FROM ' + @tabla + N' WHERE NOT (' + @expr + N');';
    EXEC sp_executesql @sql, N'@n INT OUTPUT', @n = @n OUTPUT;
    IF @n > 0
    BEGIN
        SET @msg = CONCAT(N'54_RestriccionesDominio: ', @n, N' fila(s) de ', @tabla, N' violan ', @nombre,
                          N'. Control: SELECT * FROM ', @tabla, N' WHERE NOT (', @expr, N'). No se cambió esa regla ni las siguientes.');
        THROW 54001, @msg, 1;
    END

    SET @sql = N'ALTER TABLE ' + @tabla + N' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(@nombre) + N' CHECK (' + @expr + N');';
    EXEC (@sql);
    PRINT @nombre + N': creado.';
END
GO

IF OBJECT_ID('tempdb..#ColumnaNotNull') IS NOT NULL DROP PROCEDURE #ColumnaNotNull;
GO
-- Pasa una columna (var)char a NOT NULL conservando tipo y largo. El DEFAULT que tenga se quita antes
-- y lo vuelve a poner el llamador (con nombre fijo).
CREATE PROCEDURE #ColumnaNotNull
    @tabla   SYSNAME,
    @columna SYSNAME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @sql NVARCHAR(MAX), @n INT, @msg NVARCHAR(2048), @tipo NVARCHAR(200), @df SYSNAME, @intercalacion SYSNAME;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(@tabla) AND name = @columna AND is_nullable = 1)
        RETURN;

    SET @sql = N'SELECT @n = COUNT(*) FROM ' + @tabla + N' WHERE ' + QUOTENAME(@columna) + N' IS NULL;';
    EXEC sp_executesql @sql, N'@n INT OUTPUT', @n = @n OUTPUT;
    IF @n > 0
    BEGIN
        SET @msg = CONCAT(N'54_RestriccionesDominio: ', @n, N' fila(s) de ', @tabla, N' con ', @columna, N' NULL; no se puede pasar a NOT NULL.');
        THROW 54002, @msg, 1;
    END

    -- Tipo, largo (en caracteres: nchar/nvarchar guardan 2 bytes) e intercalación de la columna: ALTER COLUMN
    -- sin COLLATE la dejaría con la intercalación por defecto de la BD.
    SELECT @tipo = TYPE_NAME(c.user_type_id) + N'(' + CASE WHEN c.max_length = -1 THEN N'MAX'
                       WHEN TYPE_NAME(c.user_type_id) IN ('nchar', 'nvarchar') THEN CAST(c.max_length / 2 AS NVARCHAR(10))
                       ELSE CAST(c.max_length AS NVARCHAR(10)) END + N')',
           @intercalacion = c.collation_name,
           @df = dc.name
    FROM sys.columns c
    LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    WHERE c.object_id = OBJECT_ID(@tabla) AND c.name = @columna;

    IF @df IS NOT NULL
    BEGIN
        SET @sql = N'ALTER TABLE ' + @tabla + N' DROP CONSTRAINT ' + QUOTENAME(@df) + N';';
        EXEC (@sql);
    END

    SET @sql = N'ALTER TABLE ' + @tabla + N' ALTER COLUMN ' + QUOTENAME(@columna) + N' ' + @tipo
             + ISNULL(N' COLLATE ' + @intercalacion, N'') + N' NOT NULL;';
    EXEC (@sql);
    PRINT @tabla + N'.' + @columna + N': NOT NULL.';
END
GO

-- ------------------------------------------------------------------ 1. NOT NULL y DEFAULT (M-050)
-- OrdenViaje.estadoAprobacion: sin DEFAULT 'APROBADO'; NOT NULL DEFAULT 'PENDIENTE'.
DECLARE @df SYSNAME = (SELECT dc.name FROM sys.default_constraints dc
                       JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
                       WHERE dc.parent_object_id = OBJECT_ID('dbo.OrdenViaje') AND c.name = 'estadoAprobacion'
                         AND dc.definition <> '(''PENDIENTE'')');
IF @df IS NOT NULL
BEGIN
    DECLARE @sqlDf NVARCHAR(400) = N'ALTER TABLE dbo.OrdenViaje DROP CONSTRAINT ' + QUOTENAME(@df) + N';';
    EXEC (@sqlDf);
    PRINT 'OrdenViaje.estadoAprobacion: DEFAULT anterior quitado (' + @df + ').';
END
GO
EXEC #ColumnaNotNull 'dbo.OrdenViaje', 'estadoAprobacion';
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
               JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
               WHERE dc.parent_object_id = OBJECT_ID('dbo.OrdenViaje') AND c.name = 'estadoAprobacion')
    ALTER TABLE dbo.OrdenViaje ADD CONSTRAINT DF_OrdenViaje_EstadoAprobacion DEFAULT ('PENDIENTE') FOR estadoAprobacion;
GO
EXEC #ColumnaNotNull 'dbo.OrdenViaje', 'estadoViaje';
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
               JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
               WHERE dc.parent_object_id = OBJECT_ID('dbo.OrdenViaje') AND c.name = 'estadoViaje')
    ALTER TABLE dbo.OrdenViaje ADD CONSTRAINT DF_OrdenViaje_EstadoViaje DEFAULT ('PENDIENTE') FOR estadoViaje;
GO
EXEC #ColumnaNotNull 'dbo.Usuarios', 'rol';
GO

-- ------------------------------------------------------------------ 2. Estados, tipos, etapa y rol
EXEC #AgregarCheck 'dbo.Despachos', 'CK_Despachos_Estado',
     N'estadoDespacho IN (''PROGRAMADO'', ''COMPLETADO'', ''ANULADO'')';
EXEC #AgregarCheck 'dbo.Despachos', 'CK_Despachos_TipoOperacion',
     N'tipoOperacion IN (''CARGA'', ''DESCARGA'')';
-- Un despacho dado de baja está ANULADO y uno ANULADO está dado de baja (sp_LD_AnularLote hace las dos cosas).
EXEC #AgregarCheck 'dbo.Despachos', 'CK_Despachos_AnuladoInactivo',
     N'(activo = 0 AND estadoDespacho = ''ANULADO'') OR (activo = 1 AND estadoDespacho <> ''ANULADO'')';
EXEC #AgregarCheck 'dbo.ViajesEnProgreso', 'CK_ViajesEnProgreso_Estado',
     N'estadoViaje IN (''ABIERTO'', ''CERRADO'', ''ANULADO'')';
EXEC #AgregarCheck 'dbo.OrdenViaje', 'CK_OrdenViaje_EstadoAprobacion',
     N'estadoAprobacion IN (''PENDIENTE'', ''POR_CONFORMAR'', ''REABIERTO'', ''APROBADO'')';
EXEC #AgregarCheck 'dbo.OrdenViaje', 'CK_OrdenViaje_EstadoViaje',
     N'estadoViaje IN (''PENDIENTE'', ''COMPLETADO'')';
EXEC #AgregarCheck 'dbo.SeguimientoExportacion', 'CK_SeguimientoExportacion_Estado',
     N'estado IN (''EN_CURSO'', ''FINALIZADO'', ''CANCELADO'')';
EXEC #AgregarCheck 'dbo.SeguimientoExportacion', 'CK_SeguimientoExportacion_Origen',
     N'origen IS NULL OR origen IN (''DESPACHO'', ''MANUAL'')';
-- etapa NULL: 20 firmas históricas anteriores a la columna (FirmaDigital es append-only) y las revocaciones.
EXEC #AgregarCheck 'dbo.FirmaDigital', 'CK_FirmaDigital_Etapa',
     N'etapa IS NULL OR etapa IN (''DECLARACION'', ''REVISION'', ''CONFORMIDAD'', ''ANULACION'')';
EXEC #AgregarCheck 'dbo.FirmaDigital', 'CK_FirmaDigital_EstadoFirma',
     N'estadoFirma IN (''V'', ''A'')';
-- Roles vigentes; los retirados (Grifo, Maquinaria, Operador) solo en cuentas inactivas (U-015, E3-5).
EXEC #AgregarCheck 'dbo.Usuarios', 'CK_Usuarios_Rol',
     N'rol IN (''ADMIN'', ''ADMINISTRADOR'', ''ADMINISTRADOR DE TRANSPORTE'', ''ADMINISTRADOR DE SISTEMA'', ''CONDUCTOR'', ''CHOFER'', ''SUPERVISOR'', ''CONTABILIDAD'') OR (activo = 0 AND rol IN (''ADMINISTRADOR DE GRIFO'', ''ADMINISTRADOR DE MAQUINARIA'', ''OPERADOR''))';
EXEC #AgregarCheck 'dbo.FacturaFlete', 'CK_FacturaFlete_Moneda',
     N'moneda IN (''PEN'', ''USD'')';
GO

-- ------------------------------------------------------------------ 3. Montos >= 0
-- Gastos e ingresos de la liquidación: MontoHelper.ValidarMonto ya rechaza negativos en C#.
EXEC #AgregarCheck 'dbo.DetalleAlimentacion',       'CK_DetalleAlimentacion_Montos',       N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetalleApoyoSeguridad',     'CK_DetalleApoyoSeguridad_Montos',     N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetalleCombustible',        'CK_DetalleCombustible_Montos',        N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetalleEncapada',           'CK_DetalleEncapada_Montos',           N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetalleHospedaje',          'CK_DetalleHospedaje_Montos',          N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetalleMovilidad',          'CK_DetalleMovilidad_Montos',          N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetallePeajes',             'CK_DetallePeajes_Montos',             N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.DetalleReparacionesVarios', 'CK_DetalleReparacionesVarios_Montos', N'montoSoles >= 0 AND montoDolares >= 0';
EXEC #AgregarCheck 'dbo.Egresos', 'CK_Egresos_Montos',
     N'alimentacionSoles >= 0 AND alimentacionDolares >= 0 AND apoyoseguridadSoles >= 0 AND apoyoseguridadDolares >= 0 AND combustibleSoles >= 0 AND combustibleDolares >= 0 AND encarpada_desencarpadaSoles >= 0 AND encarpada_desencarpadaDolares >= 0 AND hospedajeSoles >= 0 AND hospedajeDolares >= 0 AND movilidadSoles >= 0 AND movilidadDolares >= 0 AND peajesSoles >= 0 AND peajesDolares >= 0 AND reparacionesVariosSoles >= 0 AND repacionesVariosDolares >= 0';
EXEC #AgregarCheck 'dbo.Ingresos', 'CK_Ingresos_Montos',
     N'despachoSoles >= 0 AND despachoDolares >= 0 AND mensualidadSoles >= 0 AND mensualidadDolares >= 0 AND otrosSoles >= 0 AND otrosDolares >= 0 AND prestamoSoles >= 0 AND prestamosDolares >= 0 AND totalSoles >= 0 AND totalDolares >= 0';
EXEC #AgregarCheck 'dbo.IngresosAdicionales',   'CK_IngresosAdicionales_Montos',   N'soles >= 0 AND dolares >= 0';
EXEC #AgregarCheck 'dbo.CategoriasAdicionales', 'CK_CategoriasAdicionales_Montos', N'soles >= 0 AND dolares >= 0';
EXEC #AgregarCheck 'dbo.DescuentosReintegros',  'CK_DescuentosReintegros_Montos',
     N'descuentoSoles >= 0 AND descuentoDolares >= 0 AND reintegroSoles >= 0 AND reintegroDolares >= 0';
-- Documentos y carga (con datos: 0 negativos medidos).
EXEC #AgregarCheck 'dbo.Factura',          'CK_Factura_ValorTotal',       N'valorTotal >= 0';
EXEC #AgregarCheck 'dbo.CPIC',             'CK_CPIC_Montos',              N'valorTotalFlete >= 0 AND pesoNeto >= 0 AND pesoBruto >= 0';
EXEC #AgregarCheck 'dbo.FacturaFlete',     'CK_FacturaFlete_Montos',
     N'cantidad >= 0 AND valorUnitario >= 0 AND valorVenta >= 0 AND igv >= 0 AND importeTotal >= 0';
EXEC #AgregarCheck 'dbo.FacturaFleteItem', 'CK_FacturaFleteItem_Montos',  N'cantidad >= 0 AND valorUnitario >= 0';
EXEC #AgregarCheck 'dbo.DespachoCarga',    'CK_DespachoCarga_Pesos',      N'pesoBrutoKg >= 0 AND pesoNetoKg >= 0';
GO

-- ------------------------------------------------------------------ 4. Control final
IF EXISTS (SELECT 1 FROM sys.default_constraints dc
           JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
           WHERE dc.parent_object_id = OBJECT_ID('dbo.OrdenViaje') AND c.name = 'estadoAprobacion'
             AND dc.definition LIKE '%APROBADO%')
    THROW 54003, N'54_RestriccionesDominio: OrdenViaje.estadoAprobacion sigue con DEFAULT APROBADO.', 1;
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_not_trusted = 1 OR is_disabled = 1)
    THROW 54004, N'54_RestriccionesDominio: hay CHECK sin validar o deshabilitados.', 1;
DECLARE @ms INT = DATEDIFF(MILLISECOND, (SELECT t FROM #inicio), SYSDATETIME());
PRINT CONCAT('54_RestriccionesDominio: listo en ', @ms, ' ms.');
GO

DROP PROCEDURE #AgregarCheck;
DROP PROCEDURE #ColumnaNotNull;
GO
DROP TABLE #inicio;
GO
