-- ============================================================
-- SCRIPT: 05_script_validacion_pre_migracion.sql
-- BASE DE DATOS: sgvActualizada (BASE ORIGINAL - SOLO LECTURA)
-- PROPÓSITO: Inspeccionar el estado actual de la BD antes
--            de iniciar cualquier migración.
-- MODO: SELECT ONLY — NINGÚN CAMBIO A LA BASE DE DATOS
-- FECHA: 2026-05-22
-- PROYECTO: SGT-SGV - SERVICIOS GENERALES VIVIANA E.I.R.L.
-- ============================================================
-- INSTRUCCIONES:
-- 1. Ejecutar en la base ORIGINAL (sgvActualizada)
-- 2. NO ejecutar en la nueva base hasta después de la migración
-- 3. Guardar el resultado como evidencia pre-migración
-- ============================================================

PRINT '================================================================'
PRINT 'SGT-SGV — VALIDACIÓN PRE-MIGRACIÓN'
PRINT 'Base de datos: sgvActualizada'
PRINT 'Fecha: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT 'MODO: SOLO LECTURA'
PRINT '================================================================'
PRINT ''

-- ============================================================
-- SECCIÓN 1: CONTEO DE REGISTROS POR TABLA
-- ============================================================
PRINT '--- SECCIÓN 1: CANTIDAD DE REGISTROS POR TABLA ---'
PRINT ''

SELECT
    t.TABLE_NAME AS Tabla,
    ISNULL(SUM(p.rows), 0) AS RegistrosAproximados
FROM INFORMATION_SCHEMA.TABLES t
LEFT JOIN sys.objects o ON o.name = t.TABLE_NAME AND o.schema_id = SCHEMA_ID(t.TABLE_SCHEMA)
LEFT JOIN sys.partitions p ON p.object_id = o.object_id AND p.index_id IN (0,1)
WHERE t.TABLE_TYPE = 'BASE TABLE'
GROUP BY t.TABLE_NAME
ORDER BY RegistrosAproximados DESC, t.TABLE_NAME;

-- ============================================================
-- SECCIÓN 2: TABLAS SIN CLAVE PRIMARIA
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 2: TABLAS SIN CLAVE PRIMARIA ---'
PRINT '(Estas tablas no pueden usar TRUNCATE directamente y tienen riesgos de datos duplicados)'
PRINT ''

SELECT
    t.TABLE_NAME AS TablaSinPK,
    (SELECT SUM(p.rows)
     FROM sys.partitions p
     INNER JOIN sys.objects o ON p.object_id = o.object_id
     WHERE o.name = t.TABLE_NAME AND p.index_id IN (0,1)) AS Registros
FROM INFORMATION_SCHEMA.TABLES t
WHERE t.TABLE_TYPE = 'BASE TABLE'
  AND t.TABLE_NAME NOT IN (
    SELECT tc.TABLE_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
  )
ORDER BY t.TABLE_NAME;

-- ============================================================
-- SECCIÓN 3: COLUMNAS QUE PARECEN FK POR NOMBRE (SIN FK FORMAL)
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 3: COLUMNAS CON NOMBRE "id*" SIN FK FORMAL ---'
PRINT '(Posibles dependencias implícitas no declaradas como FK)'
PRINT ''

SELECT
    c.TABLE_NAME AS Tabla,
    c.COLUMN_NAME AS Columna,
    c.DATA_TYPE AS TipoDato
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.COLUMN_NAME LIKE 'id%'
  AND c.COLUMN_NAME != (
    SELECT kcu.COLUMN_NAME
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
        ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
    WHERE tc.TABLE_NAME = c.TABLE_NAME
      AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
      AND kcu.COLUMN_NAME = c.COLUMN_NAME
  )
  AND NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
    INNER JOIN sys.columns col ON fkc.parent_object_id = col.object_id AND fkc.parent_column_id = col.column_id
    WHERE tp.name = c.TABLE_NAME AND col.name = c.COLUMN_NAME
  )
  AND c.TABLE_NAME IN (
    SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'
  )
ORDER BY c.TABLE_NAME, c.COLUMN_NAME;

-- ============================================================
-- SECCIÓN 4: STORED PROCEDURES EXISTENTES
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 4: STORED PROCEDURES (135 total) ---'
PRINT ''

SELECT
    name AS NombreSP,
    create_date AS FechaCreacion,
    modify_date AS FechaModificacion,
    CASE
        WHEN name LIKE 'sp_DC_%' THEN 'Dashboard Conductor'
        WHEN name LIKE 'sp_LD_%' THEN 'Lotes Despacho'
        WHEN name LIKE 'sp_SE_%' THEN 'Seguimiento Exportación'
        WHEN name LIKE 'sp_MQ_%' THEN 'Maquinaria'
        WHEN name LIKE 'sp_Reporte%' OR name LIKE 'sp_Generar%' THEN 'Reportes'
        WHEN name LIKE '%Prueba%' OR name LIKE '%Test%' THEN '⚠️ PRUEBA/TEST'
        WHEN name LIKE '%Temporal%' OR name LIKE '%Temp%' THEN '⚠️ TEMPORAL'
        ELSE 'General/Legado'
    END AS Modulo
FROM sys.procedures
WHERE is_ms_shipped = 0
ORDER BY Modulo, name;

-- ============================================================
-- SECCIÓN 5: TRIGGERS EXISTENTES
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 5: TRIGGERS (38 total) ---'
PRINT ''

SELECT
    t.name AS NombreTrigger,
    OBJECT_NAME(t.parent_id) AS TablaAfectada,
    t.is_disabled AS EstaDeshabilitado,
    t.is_instead_of_trigger AS EsInsteadOf,
    te.type_desc AS TipoEvento,
    CASE
        WHEN t.is_instead_of_trigger = 1 THEN '🔴 APPEND-ONLY (FirmaDigital)'
        ELSE '📋 Auditoría'
    END AS Proposito
FROM sys.triggers t
INNER JOIN sys.trigger_events te ON t.object_id = te.object_id
WHERE t.parent_class = 1
ORDER BY TablaAfectada, NombreTrigger, TipoEvento;

-- ============================================================
-- SECCIÓN 6: ÍNDICES PRINCIPALES (PK + UNIQUE)
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 6: ÍNDICES ÚNICOS Y PRIMARIOS ---'
PRINT ''

SELECT
    t.name AS Tabla,
    i.name AS NombreIndice,
    i.type_desc AS Tipo,
    i.is_primary_key AS EsPK,
    i.is_unique AS EsUnico,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name IS NOT NULL
  AND i.type_desc != 'HEAP'
  AND (i.is_primary_key = 1 OR i.is_unique = 1)
GROUP BY t.name, i.name, i.type_desc, i.is_primary_key, i.is_unique
ORDER BY t.name, i.is_primary_key DESC, i.name;

-- ============================================================
-- SECCIÓN 7: ÚLTIMOS REGISTROS POR TABLA (COLUMNAS FECHA)
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 7: RANGO DE FECHAS EN TABLAS OPERACIONALES ---'
PRINT ''

-- Despachos
SELECT 'Despachos' AS Tabla,
       MIN(fechaDespacho) AS PrimeraFecha,
       MAX(fechaDespacho) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM Despachos;

-- OrdenViaje
SELECT 'OrdenViaje' AS Tabla,
       MIN(fechaRegistro) AS PrimeraFecha,
       MAX(fechaRegistro) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM OrdenViaje;

-- FirmaDigital
SELECT 'FirmaDigital' AS Tabla,
       MIN(fechaHoraFirma) AS PrimeraFecha,
       MAX(fechaHoraFirma) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM FirmaDigital;

-- AuditoriaLog
SELECT 'AuditoriaLog' AS Tabla,
       MIN(FechaHora) AS PrimeraFecha,
       MAX(FechaHora) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM AuditoriaLog;

-- AbastecimientoCombustible
SELECT 'AbastecimientoCombustible' AS Tabla,
       MIN(fechaHora) AS PrimeraFecha,
       MAX(fechaHora) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM AbastecimientoCombustible;

-- ViajesEnProgreso
SELECT 'ViajesEnProgreso' AS Tabla,
       MIN(fechaInicio) AS PrimeraFecha,
       MAX(fechaUltimaActividad) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM ViajesEnProgreso;

-- SeguimientoExportacion
SELECT 'SeguimientoExportacion' AS Tabla,
       MIN(fechaRegistro) AS PrimeraFecha,
       MAX(fechaRegistro) AS UltimaFecha,
       COUNT(*) AS TotalRegistros,
       estado AS Estado
FROM SeguimientoExportacion
GROUP BY estado;

-- Indicadores (muestra rango)
SELECT 'Indicadores' AS Tabla,
       MIN(fechaHoraRegistro) AS PrimeraFecha,
       MAX(fechaHoraRegistro) AS UltimaFecha,
       COUNT(*) AS TotalRegistros
FROM Indicadores;

-- ============================================================
-- SECCIÓN 8: TABLAS CON NOMBRES SOSPECHOSOS
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 8: OBJETOS CON NOMBRES RELACIONADOS A PRUEBA/TEMP ---'
PRINT ''

SELECT
    o.name AS NombreObjeto,
    o.type_desc AS TipoObjeto,
    o.create_date AS FechaCreacion,
    o.modify_date AS FechaModificacion
FROM sys.objects o
WHERE (
    o.name LIKE '%Backup%'
    OR o.name LIKE '%prueba%'
    OR o.name LIKE '%Prueba%'
    OR o.name LIKE '%test%'
    OR o.name LIKE '%Test%'
    OR o.name LIKE '%demo%'
    OR o.name LIKE '%Demo%'
    OR o.name LIKE '%ejemplo%'
    OR o.name LIKE '%Ejemplo%'
    OR o.name LIKE '%temporal%'
    OR o.name LIKE '%Temporal%'
    OR o.name LIKE '%Temp%'
)
AND o.type NOT IN ('S', 'IT', 'SQ')  -- Excluir objetos del sistema
ORDER BY o.type_desc, o.name;

-- ============================================================
-- SECCIÓN 9: RESUMEN DE FKs CON ON DELETE CASCADE
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 9: FK CON ON DELETE CASCADE (riesgo en limpieza) ---'
PRINT ''

SELECT
    fk.name AS NombreFK,
    tp.name AS TablaHijo,
    cp.name AS ColumnaHijo,
    tr.name AS TablaPadre,
    cr.name AS ColumnaPadre,
    fk.delete_referential_action_desc AS AccionDelete
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
WHERE fk.delete_referential_action_desc != 'NO_ACTION'
ORDER BY tp.name;

-- ============================================================
-- SECCIÓN 10: RESUMEN EJECUTIVO
-- ============================================================
PRINT ''
PRINT '--- SECCIÓN 10: RESUMEN EJECUTIVO ---'
PRINT ''

SELECT
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE') AS TotalTablas,
    (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0) AS TotalSPs,
    (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) AS TotalTriggers,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS TotalFKs,
    (SELECT COUNT(*) FROM sys.indexes WHERE is_primary_key=0 AND name IS NOT NULL AND type_desc!='HEAP') AS TotalIndicesSecundarios,
    (SELECT SUM(p.rows)
     FROM sys.partitions p
     INNER JOIN sys.tables t ON p.object_id = t.object_id
     WHERE p.index_id IN (0,1)) AS TotalRegistrosTodosTablas;

PRINT ''
PRINT '================================================================'
PRINT 'FIN DE VALIDACIÓN PRE-MIGRACIÓN'
PRINT 'Guardar resultados como evidencia antes de continuar.'
PRINT '================================================================'
