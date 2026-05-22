-- ============================================================
-- SCRIPT: 08_script_validacion_post_migracion.sql
-- BASE DE DATOS: NUEVA BASE PREMIUM (después de migración)
-- PROPÓSITO: Validar que la nueva base de producción está
--            correctamente configurada y lista para operar.
-- MODO: SELECT ONLY — VERIFICACIÓN SIN CAMBIOS
-- FECHA: 2026-05-22
-- ============================================================

PRINT '================================================================'
PRINT 'SGT-SGV — VALIDACIÓN POST-MIGRACIÓN'
PRINT 'Base: ' + DB_NAME()
PRINT 'Servidor: ' + @@SERVERNAME
PRINT 'Fecha: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '================================================================'
PRINT ''

-- ============================================================
-- TEST 1: Estructura — Conteo de objetos de BD
-- ============================================================
PRINT '=== TEST 1: Conteo de objetos de base de datos ==='
PRINT '(Comparar con: ~69 tablas, 135 SPs, 38 triggers, 111 FKs)'
PRINT ''

SELECT
    (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
     WHERE TABLE_TYPE='BASE TABLE'
     AND TABLE_NAME NOT LIKE '%Backup%') AS TablasSinBackups,
    (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0) AS StoredProcedures,
    (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) AS Triggers,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS ClavesForeignKey,
    (SELECT COUNT(*) FROM sys.indexes
     WHERE is_primary_key=0 AND name IS NOT NULL AND type_desc!='HEAP') AS IndicesSecundarios;

-- ============================================================
-- TEST 2: Tablas maestras — Verificar datos migrados
-- ============================================================
PRINT ''
PRINT '=== TEST 2: Conteo de datos maestros (deben tener registros) ==='
PRINT ''

SELECT
    'Conductor'         AS TablaMaestra, COUNT(*) AS Registros,
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END AS Estado
FROM Conductor
UNION ALL
SELECT 'Tracto',        COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM Tracto
UNION ALL
SELECT 'Carreta',       COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM Carreta
UNION ALL
SELECT 'Cliente',       COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM Cliente
UNION ALL
SELECT 'Producto',      COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM Producto
UNION ALL
SELECT 'Roles',         COUNT(*),
    CASE WHEN COUNT(*) >= 6 THEN '✅ OK' ELSE '❌ FALTAN ROLES' END FROM Roles
UNION ALL
SELECT 'FormatoControlado', COUNT(*),
    CASE WHEN COUNT(*) >= 2 THEN '✅ OK' ELSE '❌ FALTAN FORMATOS PDF' END FROM FormatoControlado
UNION ALL
SELECT 'EstacionesPeaje', COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM EstacionesPeaje
UNION ALL
SELECT 'TipoCarro',     COUNT(*),
    CASE WHEN COUNT(*) >= 6 THEN '✅ OK' ELSE '❌ FALTAN TIPOS' END FROM TipoCarro
UNION ALL
SELECT 'LugarAbastecimiento', COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA' END FROM LugarAbastecimiento
UNION ALL
SELECT 'Planta',        COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM Planta
UNION ALL
SELECT 'Ruta',          COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA — REVISAR' END FROM Ruta
UNION ALL
SELECT 'camionetas',    COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA' END FROM camionetas
UNION ALL
SELECT 'volquetes',     COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '❌ VACÍA' END FROM volquetes
UNION ALL
SELECT 'Usuarios',      COUNT(*),
    CASE WHEN COUNT(*) > 0 THEN '✅ OK' ELSE '⚠️ VACÍA — crear admin antes de usar' END FROM Usuarios;

-- ============================================================
-- TEST 3: Tablas transaccionales — Deben estar vacías
-- ============================================================
PRINT ''
PRINT '=== TEST 3: Tablas transaccionales (deben estar VACÍAS) ==='
PRINT ''

SELECT
    'Despachos'                  AS TablaTransaccional, COUNT(*) AS Registros,
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END AS Estado
FROM Despachos
UNION ALL
SELECT 'ViajesEnProgreso',       COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM ViajesEnProgreso
UNION ALL
SELECT 'OrdenViaje',             COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM OrdenViaje
UNION ALL
SELECT 'FirmaDigital',           COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM FirmaDigital
UNION ALL
SELECT 'AbastecimientoCombustible', COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM AbastecimientoCombustible
UNION ALL
SELECT 'CPIC',                   COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM CPIC
UNION ALL
SELECT 'Factura',                COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM Factura
UNION ALL
SELECT 'AuditoriaLog',           COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE ENTRADAS PREVIAS' END FROM AuditoriaLog
UNION ALL
SELECT 'Auditoria',              COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE ENTRADAS PREVIAS' END FROM Auditoria
UNION ALL
SELECT 'Liquidaciones',          COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM Liquidaciones
UNION ALL
SELECT 'DetallePeajes',          COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM DetallePeajes
UNION ALL
SELECT 'Egresos',                COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM Egresos
UNION ALL
SELECT 'Ingresos',               COUNT(*),
    CASE WHEN COUNT(*) = 0 THEN '✅ Vacía (OK)' ELSE '⚠️ TIENE DATOS' END FROM Ingresos;

-- ============================================================
-- TEST 4: Triggers críticos — Estado habilitado
-- ============================================================
PRINT ''
PRINT '=== TEST 4: Estado de triggers (todos deben estar HABILITADOS) ==='
PRINT ''

SELECT
    t.name AS NombreTrigger,
    OBJECT_NAME(t.parent_id) AS Tabla,
    CASE t.is_disabled
        WHEN 0 THEN '✅ HABILITADO'
        WHEN 1 THEN '❌ DESHABILITADO — RIESGO'
    END AS Estado,
    CASE t.is_instead_of_trigger
        WHEN 1 THEN '🔴 APPEND-ONLY (FirmaDigital)'
        ELSE '📋 Auditoría'
    END AS Tipo
FROM sys.triggers t
WHERE t.parent_class = 1
ORDER BY t.is_disabled DESC, OBJECT_NAME(t.parent_id), t.name;

-- ============================================================
-- TEST 5: FirmaDigital — Verificar integridad del trigger append-only
-- ============================================================
PRINT ''
PRINT '=== TEST 5: FirmaDigital — Verificar triggers INSTEAD OF ==='
PRINT ''

SELECT
    t.name AS Trigger_Nombre,
    OBJECT_NAME(t.parent_id) AS Tabla,
    te.type_desc AS Evento,
    CASE t.is_instead_of_trigger WHEN 1 THEN '✅ INSTEAD OF (Bloquea operación)' ELSE 'AFTER (Normal)' END AS TipoTrigger,
    CASE t.is_disabled WHEN 0 THEN '✅ ACTIVO' ELSE '❌ DESHABILITADO' END AS Estado
FROM sys.triggers t
INNER JOIN sys.trigger_events te ON t.object_id = te.object_id
WHERE OBJECT_NAME(t.parent_id) = 'FirmaDigital'
ORDER BY t.name;

-- ============================================================
-- TEST 6: Constraints de unicidad — No deben tener conflictos
-- ============================================================
PRINT ''
PRINT '=== TEST 6: Constraints UNIQUE activos en tablas maestras ==='
PRINT ''

SELECT
    t.name AS Tabla,
    i.name AS Constraint_Unico,
    STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columnas
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.is_unique = 1 AND i.is_primary_key = 0
GROUP BY t.name, i.name
ORDER BY t.name;

-- ============================================================
-- TEST 7: FormatoControlado — Verificar formatos PDF requeridos
-- ============================================================
PRINT ''
PRINT '=== TEST 7: Formatos Controlados para PDF ==='
PRINT '(SGV-CDF-F-05 y SGV-CDF-F-06 son OBLIGATORIOS para la app)'
PRINT ''

SELECT
    codigoFormato,
    nombre,
    version,
    fechaVigencia,
    CASE activo WHEN 1 THEN '✅ Activo' ELSE '❌ Inactivo' END AS Estado
FROM FormatoControlado
ORDER BY codigoFormato;

-- Verificar que los dos formatos críticos existen
SELECT
    CASE WHEN EXISTS(SELECT 1 FROM FormatoControlado WHERE codigoFormato='SGV-CDF-F-05' AND activo=1)
         THEN '✅ SGV-CDF-F-05 OK' ELSE '❌ FALTA SGV-CDF-F-05 — PDFs de OrdenViaje fallarán' END AS F05,
    CASE WHEN EXISTS(SELECT 1 FROM FormatoControlado WHERE codigoFormato='SGV-CDF-F-06' AND activo=1)
         THEN '✅ SGV-CDF-F-06 OK' ELSE '❌ FALTA SGV-CDF-F-06 — PDFs de Abastecimiento fallarán' END AS F06;

-- ============================================================
-- TEST 8: Roles del sistema — Verificar configuración
-- ============================================================
PRINT ''
PRINT '=== TEST 8: Roles del sistema ==='
PRINT ''

SELECT
    nombreRol,
    nivel,
    CASE activo WHEN 1 THEN '✅ Activo' ELSE '❌ Inactivo' END AS Estado
FROM Roles
ORDER BY nivel DESC;

-- ============================================================
-- TEST 9: Stored Procedures críticos — Verificar que existen
-- ============================================================
PRINT ''
PRINT '=== TEST 9: SPs críticos del sistema ==='
PRINT ''

SELECT
    name AS StoredProcedure,
    CASE WHEN OBJECT_ID(name, 'P') IS NOT NULL THEN '✅ Existe' ELSE '❌ NO EXISTE' END AS Estado,
    modify_date AS UltimaModificacion
FROM (VALUES
    ('sp_DC_RegistrarFirmaConductor'),
    ('sp_DC_RegistrarFirmaAdmin'),
    ('sp_DC_InsertarOrdenViajeConductor'),
    ('sp_DC_ObtenerViajesActivosConductor'),
    ('sp_DC_ObtenerDespachosViajesActivos'),
    ('sp_DC_CerrarViajesProgreso'),
    ('sp_ObtenerConductoresActivos'),
    ('sp_ObtenerTractosActivos'),
    ('sp_ObtenerCarretasActivas'),
    ('sp_ObtenerClientesActivos'),
    ('sp_CrearDespacho'),
    ('sp_CrearViajeProgreso'),
    ('sp_InsertarAbastecimientoCombustible'),
    ('sp_ObtenerViajesActivosParaGrifo'),
    ('sp_SE_Insertar'),
    ('sp_SE_Dashboard_KPIs'),
    ('sp_MQ_InsertarParteDiario'),
    ('sp_MQ_ObtenerAsignacionActiva'),
    ('GenerarNumeroOrdenViaje')
) AS SPs(name)
LEFT JOIN sys.procedures sp ON sp.name = SPs.name AND sp.is_ms_shipped = 0;

-- ============================================================
-- TEST 10: Verificación de número correlativo — No duplicados
-- ============================================================
PRINT ''
PRINT '=== TEST 10: Verificar unicidad de números correlativos ==='
PRINT ''

-- OrdenViaje
SELECT
    'OrdenViaje.numeroOrdenViaje' AS Campo,
    COUNT(*) AS Total,
    COUNT(DISTINCT numeroOrdenViaje) AS Unicos,
    CASE WHEN COUNT(*) = COUNT(DISTINCT numeroOrdenViaje) OR COUNT(*) = 0
         THEN '✅ Sin duplicados' ELSE '❌ DUPLICADOS DETECTADOS' END AS Estado
FROM OrdenViaje
UNION ALL
SELECT
    'Despachos.numeroDespacho',
    COUNT(*),
    COUNT(DISTINCT numeroDespacho),
    CASE WHEN COUNT(*) = COUNT(DISTINCT numeroDespacho) OR COUNT(*) = 0
         THEN '✅ Sin duplicados' ELSE '❌ DUPLICADOS DETECTADOS' END
FROM Despachos
UNION ALL
SELECT
    'AbastecimientoCombustible.numeroAbastecimientoCombustible',
    COUNT(*),
    COUNT(DISTINCT numeroAbastecimientoCombustible),
    CASE WHEN COUNT(*) = COUNT(DISTINCT numeroAbastecimientoCombustible) OR COUNT(*) = 0
         THEN '✅ Sin duplicados' ELSE '❌ DUPLICADOS DETECTADOS' END
FROM AbastecimientoCombustible;

-- ============================================================
-- RESUMEN EJECUTIVO DE VALIDACIÓN
-- ============================================================
PRINT ''
PRINT '================================================================'
PRINT 'RESUMEN DE VALIDACIÓN POST-MIGRACIÓN'
PRINT '================================================================'
PRINT ''
PRINT 'Si todos los tests muestran ✅, la nueva base está lista.'
PRINT 'Si hay ❌, corregir antes de apuntar el dominio final.'
PRINT ''
PRINT 'Próximos pasos:'
PRINT '  1. Actualizar connectionStrings.config con nueva cadena'
PRINT '  2. Republicar la aplicación web en Somee Premium'
PRINT '  3. Probar login con cada rol'
PRINT '  4. Crear un despacho de prueba real'
PRINT '  5. Completar firma digital de prueba y verificar PDF'
PRINT '  6. Verificar descarga de PDF desde el navegador'
PRINT '  7. Verificar que AuditoriaLog registra el despacho de prueba'
PRINT '  8. Si todo OK → apuntar dominio final'
PRINT ''
PRINT '================================================================'
PRINT 'FIN DE VALIDACIÓN POST-MIGRACIÓN'
PRINT '================================================================'
