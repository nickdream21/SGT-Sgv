-- Montos de la liquidación en DECIMAL(12,2) en vez de FLOAT (2026-11-18, tarea "Montos FLOAT -> DECIMAL").
--
-- Las 30 columnas de dinero de Ingresos, Egresos, IngresosAdicionales y CategoriasAdicionales eran FLOAT:
-- 12.35 se guardaba como 12.3499999999999996..., las sumas arrastraban error binario y el monto que se
-- releía no era exactamente el que se firmó. Pasan a DECIMAL(12,2) NULL (hasta 9 999 999 999.99), como el
-- resto del dinero del sistema. Los SP que las escriben (sp_DC_InsertarIngresos, sp_DC_InsertarEgresos,
-- sp_DC_InsertarGastoAdicional, sp_DC_InsertarIngresoAdicional, sp_LD_RegistrarIngresosEntregados) pasan
-- sus parámetros a DECIMAL(12,2) con los mismos nombres y defaults (el C# publicado ya envía decimal).
--
-- Conversión en DOS pasos a propósito: FLOAT -> DECIMAL(19,6) -> DECIMAL(12,2).
-- Un CAST directo FLOAT -> DECIMAL(12,2) redondea sobre el valor binario exacto (0.285 es 0.28499999...
-- y queda 0.28; 2.675 queda 2.67). El PDF y la huella firmada se calcularon en C# con
-- Convert.ToDecimal(double).ToString("0.00"), que da 0.29 y 2.68. Pasando antes por 6 decimales el
-- resultado coincide con lo que se mostró y se firmó.
--
-- Antes de cambiar nada comprueba que ningún valor desborda DECIMAL(12,2); si alguno lo hace, no cambia
-- nada y falla con la columna y el recuento (hay que corregir ese dato a mano).
-- Borra solo las estadísticas automáticas (_WA_Sys_*) de cada columna, que impiden el ALTER COLUMN; SQL
-- Server las vuelve a crear cuando las necesite. No hay índices, CHECK, vistas ni triggers sobre ellas.
-- Idempotente: solo actúa sobre columnas FLOAT o DECIMAL de escala 6 (paso intermedio); todo va en una
-- transacción (XACT_ABORT), así que un fallo a medias no deja columnas en DECIMAL(19,6).

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

DECLARE @columnas TABLE (orden INT IDENTITY(1,1), tabla SYSNAME, columna SYSNAME);
INSERT INTO @columnas (tabla, columna) VALUES
    ('Ingresos', 'despachoSoles'), ('Ingresos', 'despachoDolares'),
    ('Ingresos', 'prestamoSoles'), ('Ingresos', 'prestamosDolares'),
    ('Ingresos', 'mensualidadSoles'), ('Ingresos', 'mensualidadDolares'),
    ('Ingresos', 'otrosSoles'), ('Ingresos', 'otrosDolares'),
    ('Ingresos', 'totalSoles'), ('Ingresos', 'totalDolares'),
    ('Egresos', 'peajesSoles'), ('Egresos', 'peajesDolares'),
    ('Egresos', 'alimentacionSoles'), ('Egresos', 'alimentacionDolares'),
    ('Egresos', 'apoyoseguridadSoles'), ('Egresos', 'apoyoseguridadDolares'),
    ('Egresos', 'reparacionesVariosSoles'), ('Egresos', 'repacionesVariosDolares'),
    ('Egresos', 'movilidadSoles'), ('Egresos', 'movilidadDolares'),
    ('Egresos', 'encarpada_desencarpadaSoles'), ('Egresos', 'encarpada_desencarpadaDolares'),
    ('Egresos', 'hospedajeSoles'), ('Egresos', 'hospedajeDolares'),
    ('Egresos', 'combustibleSoles'), ('Egresos', 'combustibleDolares'),
    ('IngresosAdicionales', 'soles'), ('IngresosAdicionales', 'dolares'),
    ('CategoriasAdicionales', 'soles'), ('CategoriasAdicionales', 'dolares');

DECLARE @i INT = 1, @n INT = (SELECT COUNT(*) FROM @columnas);
DECLARE @tabla SYSNAME, @columna SYSNAME, @tipo TINYINT, @escala TINYINT, @sql NVARCHAR(MAX),
        @fuera INT, @msg NVARCHAR(400), @estadistica SYSNAME;

-- 1) Comprobación previa: ningún valor FLOAT desborda DECIMAL(12,2) una vez redondeado a 2 decimales.
WHILE @i <= @n
BEGIN
    SELECT @tabla = tabla, @columna = columna FROM @columnas WHERE orden = @i;

    SELECT @tipo = c.system_type_id
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@tabla)) AND c.name = @columna;

    IF @@ROWCOUNT = 0
    BEGIN
        SET @msg = CONCAT(N'51_MontosLiquidacionDecimal: no existe la columna dbo.', @tabla, N'.', @columna, N'.');
        THROW 51000, @msg, 1;
    END

    IF @tipo = 62
    BEGIN
        SET @sql = N'SELECT @fuera = COUNT(*) FROM dbo.' + QUOTENAME(@tabla)
                 + N' WHERE ABS(' + QUOTENAME(@columna) + N') >= 9999999999.995';
        EXEC sp_executesql @sql, N'@fuera INT OUTPUT', @fuera = @fuera OUTPUT;
        IF @fuera > 0
        BEGIN
            SET @msg = CONCAT(N'51_MontosLiquidacionDecimal: dbo.', @tabla, N'.', @columna, N' tiene ', @fuera,
                              N' valor(es) que no caben en DECIMAL(12,2). Corríjalos antes de migrar.');
            THROW 51000, @msg, 1;
        END
    END

    SET @i += 1;
END

-- 2) Conversión: FLOAT -> DECIMAL(19,6) -> DECIMAL(12,2), columna por columna, en una sola transacción.
BEGIN TRANSACTION;

SET @i = 1;
WHILE @i <= @n
BEGIN
    SELECT @tabla = tabla, @columna = columna FROM @columnas WHERE orden = @i;

    -- Estadísticas automáticas de esa columna (bloquean el ALTER COLUMN).
    DECLARE estadisticas CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.name
        FROM sys.stats s
        JOIN sys.stats_columns sc ON sc.object_id = s.object_id AND sc.stats_id = s.stats_id
        JOIN sys.columns c ON c.object_id = sc.object_id AND c.column_id = sc.column_id
        WHERE s.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@tabla)) AND c.name = @columna AND s.auto_created = 1;
    OPEN estadisticas;
    FETCH NEXT FROM estadisticas INTO @estadistica;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @sql = N'DROP STATISTICS dbo.' + QUOTENAME(@tabla) + N'.' + QUOTENAME(@estadistica);
        EXEC sp_executesql @sql;
        FETCH NEXT FROM estadisticas INTO @estadistica;
    END
    CLOSE estadisticas;
    DEALLOCATE estadisticas;

    SELECT @tipo = c.system_type_id, @escala = c.scale
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.' + QUOTENAME(@tabla)) AND c.name = @columna;

    IF @tipo = 62  -- float
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tabla) + N' ALTER COLUMN ' + QUOTENAME(@columna) + N' DECIMAL(19,6) NULL';
        EXEC sp_executesql @sql;
        SET @tipo = 106;
        SET @escala = 6;
    END

    IF @tipo = 106 AND @escala = 6  -- paso intermedio
    BEGIN
        SET @sql = N'ALTER TABLE dbo.' + QUOTENAME(@tabla) + N' ALTER COLUMN ' + QUOTENAME(@columna) + N' DECIMAL(12,2) NULL';
        EXEC sp_executesql @sql;
        PRINT CONCAT('dbo.', @tabla, '.', @columna, ': DECIMAL(12,2).');
    END

    SET @i += 1;
END

COMMIT TRANSACTION;
GO
