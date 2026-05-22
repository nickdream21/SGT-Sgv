-- ============================================================
-- SCRIPT: 07_script_limpieza_transaccional_SOLO_NUEVA_BD.sql
-- ============================================================
-- ███████████████████████████████████████████████████████████
-- ██                                                       ██
-- ██   ⛔  PELIGRO — LEER ANTES DE EJECUTAR  ⛔            ██
-- ██                                                       ██
-- ██  ESTE SCRIPT BORRA DATOS TRANSACCIONALES.             ██
-- ██                                                       ██
-- ██  SOLO EJECUTAR EN:                                    ██
-- ██      ✅ LA NUEVA BASE PREMIUM (destino limpio)        ██
-- ██                                                       ██
-- ██  JAMÁS EJECUTAR EN:                                   ██
-- ██      ❌ sgvActualizada (base original)                ██
-- ██      ❌ Ninguna base con datos reales de producción   ██
-- ██                                                       ██
-- ██  Si no estás 100% seguro de en qué BD estás,         ██
-- ██  DETENTE y verifica con: SELECT DB_NAME()             ██
-- ██                                                       ██
-- ███████████████████████████████████████████████████████████
--
-- PROPÓSITO:
--   Limpiar datos transaccionales de prueba en la nueva BD
--   respetando el orden correcto de FKs.
--   Deja intactos: maestros, estructura, SPs, triggers, índices.
--
-- CONSERVA:
--   ✅ Conductor, Tracto, Carreta, Cliente, Producto, Planta
--   ✅ Roles, TipoCarro, EstacionesPeaje, LugarAbastecimiento
--   ✅ FormatoControlado, Rutas, Lugares, Operadores, EquiposMaquinaria
--   ✅ Stored Procedures (todos)
--   ✅ Triggers (todos, incluyendo FirmaDigital append-only)
--   ✅ Índices, FKs, constraints
--
-- LIMPIA (tablas transaccionales de prueba):
--   🔴 Despachos, ViajesEnProgreso, OrdenViaje y sus detalles
--   🔴 FirmaDigital, AuditoriaLog, Auditoria
--   🔴 AbastecimientoCombustible, CPIC, Factura y relacionados
--   🔴 SeguimientoExportacion (si se decide limpiar)
--   🔴 Liquidaciones, SubTramos, AsignacionesMaquinaria
--
-- FECHA: 2026-05-22
-- PROYECTO: SGT-SGV
-- ============================================================

-- VERIFICACIÓN DE SEGURIDAD: confirmar que estamos en la BD correcta
DECLARE @NombreBD NVARCHAR(100) = DB_NAME();
DECLARE @ServidorActual NVARCHAR(100) = @@SERVERNAME;

PRINT '================================================================'
PRINT '⛔ VERIFICACIÓN DE SEGURIDAD PRE-LIMPIEZA'
PRINT 'Base de datos actual: ' + @NombreBD
PRINT 'Servidor actual:      ' + @ServidorActual
PRINT 'Fecha/Hora:           ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT '================================================================'
PRINT ''

-- GUARDIA DE SEGURIDAD: Si detecta la BD original, PARA TODO
IF @NombreBD = 'sgvActualizada'
BEGIN
    RAISERROR('🚨 SCRIPT DETENIDO: Estás conectado a la base ORIGINAL (sgvActualizada). Este script solo debe ejecutarse en la nueva base de producción.', 20, 1) WITH LOG;
    RETURN;
END

PRINT 'Base de datos verificada: ' + @NombreBD
PRINT 'Iniciando limpieza de datos transaccionales...'
PRINT ''

BEGIN TRY
    BEGIN TRANSACTION LimpiezaTransaccional;

    -- ============================================================
    -- PASO 1: DESHABILITAR TRIGGERS DE AUDITORÍA TEMPORALMENTE
    -- (para evitar que la limpieza genere millones de filas de log)
    -- ============================================================
    PRINT 'Paso 1: Deshabilitando triggers de auditoría...'

    DISABLE TRIGGER TR_AbastecimientoCombustible_Auditoria ON AbastecimientoCombustible;
    DISABLE TRIGGER TR_Carreta_Auditoria ON Carreta;
    DISABLE TRIGGER TR_Conductor_Auditoria ON Conductor;
    DISABLE TRIGGER TR_CPIC_Auditoria ON CPIC;
    DISABLE TRIGGER TR_CPIC_Productos_Auditoria ON CPIC_Productos;
    DISABLE TRIGGER TR_Factura_Auditoria ON Factura;
    DISABLE TRIGGER TR_GuiasTransportista_Auditoria ON GuiasTransportista;
    DISABLE TRIGGER TR_Indicadores_Auditoria ON Indicadores;
    DISABLE TRIGGER TR_Liquidaciones_Auditoria ON Liquidaciones;
    DISABLE TRIGGER TR_OrdenViaje_Auditoria ON OrdenViaje;
    DISABLE TRIGGER TR_Tracto_Auditoria ON Tracto;
    DISABLE TRIGGER TR_UploadHistory_Auditoria ON UploadHistory;

    PRINT '  ✅ Triggers de auditoría deshabilitados'
    PRINT ''

    -- ============================================================
    -- PASO 2: DESHABILITAR TRIGGERS APPEND-ONLY DE FIRMADIGITAL
    -- (necesario para poder limpiar la tabla en la nueva BD)
    -- ⚠️ SOLO EN LA NUEVA BD — NUNCA en la base original
    -- ============================================================
    PRINT 'Paso 2: Deshabilitando triggers append-only de FirmaDigital...'
    PRINT '        (SOLO válido en nueva BD — registros son todos de prueba)'

    DISABLE TRIGGER trg_FirmaDigital_NoDelete ON FirmaDigital;
    DISABLE TRIGGER trg_FirmaDigital_NoUpdate ON FirmaDigital;

    PRINT '  ✅ Triggers FirmaDigital deshabilitados'
    PRINT ''

    -- ============================================================
    -- PASO 3: LIMPIEZA EN ORDEN (más dependiente → menos dependiente)
    -- ============================================================
    PRINT 'Paso 3: Limpiando datos transaccionales...'
    PRINT ''

    -- Nivel 6 (más dependientes) — Detalles de OrdenViaje
    PRINT '  Limpiando detalles de OrdenViaje...'
    DELETE FROM DetalleTicketEcuador;     PRINT '    DetalleTicketEcuador:     ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleSegmento;          PRINT '    DetalleSegmento:          ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM ProductosOperacion;       PRINT '    ProductosOperacion:       ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM OperacionesSubTramo;      PRINT '    OperacionesSubTramo:      ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleCombustible;       PRINT '    DetalleCombustible:       ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleAlimentacion;      PRINT '    DetalleAlimentacion:      ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleApoyoSeguridad;    PRINT '    DetalleApoyoSeguridad:    ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleEncapada;          PRINT '    DetalleEncapada:          ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleHospedaje;         PRINT '    DetalleHospedaje:         ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleMovilidad;         PRINT '    DetalleMovilidad:         ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleReparacionesVarios;PRINT '    DetalleReparacionesVarios:' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetallePeajes;            PRINT '    DetallePeajes:            ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM Egresos;                  PRINT '    Egresos:                  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM Ingresos;                 PRINT '    Ingresos:                 ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM IngresosAdicionales;      PRINT '    IngresosAdicionales:      ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DescuentosReintegros;     PRINT '    DescuentosReintegros:     ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM CategoriasAdicionales;    PRINT '    CategoriasAdicionales:    ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DetalleOrdenViaje;        PRINT '    DetalleOrdenViaje:        ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM SegmentosOrdenViaje;      PRINT '    SegmentosOrdenViaje:      ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- Guías y manifiestos
    DELETE FROM Manifiesto;               PRINT '    Manifiesto:               ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM GuiasTransportista;       PRINT '    GuiasTransportista:       ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- Partes diarios y combustible de obra
    DELETE FROM PartesDiariosTrabajo;     PRINT '    PartesDiariosTrabajo:     ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DespachoCombustibleObra;  PRINT '    DespachoCombustibleObra:  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM AsignacionesMaquinaria;   PRINT '    AsignacionesMaquinaria:   ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- OrdenViajeAjuste (antes de FirmaDigital)
    DELETE FROM OrdenViajeAjuste;         PRINT '    OrdenViajeAjuste:         ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- Abastecimiento de combustible
    DELETE FROM AbastecimientoCombustible;PRINT '    AbastecimientoCombustible:' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- Ecuador
    DELETE FROM IngresoCombustibleEcuador;PRINT '    IngresoCombustibleEcuador:' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- Liquidaciones y sub-tramos (antes de OrdenViaje)
    DELETE FROM SubTramos;                PRINT '    SubTramos:                ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM Liquidaciones;            PRINT '    Liquidaciones:            ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    PRINT ''
    PRINT '  Limpiando OrdenViaje (primero limpiar FirmaDigital porque OV tiene FK a FD)...'

    -- FirmaDigital (después de limpiar OrdenViaje.idFirmaConductor y idFirmaAdmin)
    -- Primero ponemos las FK de OV a null, luego borramos
    UPDATE OrdenViaje SET idFirmaConductor = NULL, idFirmaAdmin = NULL
    WHERE idFirmaConductor IS NOT NULL OR idFirmaAdmin IS NOT NULL;
    PRINT '    OrdenViaje.idFirma* puestos a NULL para permitir limpiar FirmaDigital'

    DELETE FROM FirmaDigital;             PRINT '    FirmaDigital:             ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM OrdenViaje;               PRINT '    OrdenViaje:               ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    PRINT ''
    PRINT '  Limpiando Despachos y ViajesEnProgreso...'

    -- Despachos tienen FK a OrdenViaje y ViajesEnProgreso
    UPDATE Despachos SET idOrdenViaje = NULL WHERE idOrdenViaje IS NOT NULL;
    DELETE FROM Despachos;                PRINT '    Despachos:                ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM ViajesEnProgreso;         PRINT '    ViajesEnProgreso:         ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    PRINT ''
    PRINT '  Limpiando documentos y registros de CPIC y Facturas...'

    -- DocumentosCPIC y DocumentosFactura (cascade, pero explícito)
    DELETE FROM DocumentosCPIC;           PRINT '    DocumentosCPIC:           ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM DocumentosFactura;        PRINT '    DocumentosFactura:        ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM CPIC_Productos;           PRINT '    CPIC_Productos:           ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM CPIC;                     PRINT '    CPIC:                     ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM Factura;                  PRINT '    Factura:                  ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    PRINT ''
    PRINT '  Limpiando registros misceláneos...'

    DELETE FROM UploadHistory;            PRINT '    UploadHistory:            ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- ============================================================
    -- 👤 SEGUIMIENTO EXPORTACIÓN — COMENTADO (requiere decisión)
    -- ============================================================
    -- Los 679 registros son todos de estado COMPLETADO (mayo 2026)
    -- Si son datos de prueba → descomentar
    -- Si son datos reales a conservar → mantener comentado
    -- ============================================================
    /*
    PRINT '  Limpiando SeguimientoExportacion (decisión del propietario)...'
    DELETE FROM SeguimientoExportacion;   PRINT '    SeguimientoExportacion:   ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    */

    PRINT ''
    PRINT '  Limpiando logs de auditoría...'
    DELETE FROM AuditoriaLog;             PRINT '    AuditoriaLog:             ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'
    DELETE FROM Auditoria;                PRINT '    Auditoria:                ' + CAST(@@ROWCOUNT AS VARCHAR) + ' registros eliminados'

    -- ============================================================
    -- PASO 4: REHABILITAR TRIGGERS
    -- ============================================================
    PRINT ''
    PRINT 'Paso 4: Rehabilitando todos los triggers...'

    ENABLE TRIGGER TR_AbastecimientoCombustible_Auditoria ON AbastecimientoCombustible;
    ENABLE TRIGGER TR_Carreta_Auditoria ON Carreta;
    ENABLE TRIGGER TR_Conductor_Auditoria ON Conductor;
    ENABLE TRIGGER TR_CPIC_Auditoria ON CPIC;
    ENABLE TRIGGER TR_CPIC_Productos_Auditoria ON CPIC_Productos;
    ENABLE TRIGGER TR_Factura_Auditoria ON Factura;
    ENABLE TRIGGER TR_GuiasTransportista_Auditoria ON GuiasTransportista;
    ENABLE TRIGGER TR_Indicadores_Auditoria ON Indicadores;
    ENABLE TRIGGER TR_Liquidaciones_Auditoria ON Liquidaciones;
    ENABLE TRIGGER TR_OrdenViaje_Auditoria ON OrdenViaje;
    ENABLE TRIGGER TR_Tracto_Auditoria ON Tracto;
    ENABLE TRIGGER TR_UploadHistory_Auditoria ON UploadHistory;

    -- CRÍTICO: Rehabilitar los triggers APPEND-ONLY de FirmaDigital
    ENABLE TRIGGER trg_FirmaDigital_NoDelete ON FirmaDigital;
    ENABLE TRIGGER trg_FirmaDigital_NoUpdate ON FirmaDigital;

    PRINT '  ✅ Todos los triggers rehabilitados correctamente'

    -- ============================================================
    -- PASO 5: VERIFICACIÓN FINAL
    -- ============================================================
    PRINT ''
    PRINT 'Paso 5: Verificación final de tablas limpias...'

    SELECT
        'Despachos'                 AS Tabla, COUNT(*) AS RegistrosRestantes FROM Despachos
    UNION ALL SELECT 'ViajesEnProgreso',        COUNT(*) FROM ViajesEnProgreso
    UNION ALL SELECT 'OrdenViaje',              COUNT(*) FROM OrdenViaje
    UNION ALL SELECT 'FirmaDigital',            COUNT(*) FROM FirmaDigital
    UNION ALL SELECT 'AbastecimientoCombustible', COUNT(*) FROM AbastecimientoCombustible
    UNION ALL SELECT 'CPIC',                    COUNT(*) FROM CPIC
    UNION ALL SELECT 'Factura',                 COUNT(*) FROM Factura
    UNION ALL SELECT 'AuditoriaLog',            COUNT(*) FROM AuditoriaLog
    UNION ALL SELECT 'Auditoria',               COUNT(*) FROM Auditoria
    UNION ALL SELECT 'SeguimientoExportacion',  COUNT(*) FROM SeguimientoExportacion
    UNION ALL SELECT 'Liquidaciones',           COUNT(*) FROM Liquidaciones;
    -- Todas deben devolver 0 (excepto SeguimientoExportacion si no se limpió)

    PRINT ''
    PRINT 'Verificando que maestros se conservaron intactos...'
    SELECT
        'Conductor'         AS Tabla, COUNT(*) AS RegistrosConservados FROM Conductor
    UNION ALL SELECT 'Tracto',      COUNT(*) FROM Tracto
    UNION ALL SELECT 'Carreta',     COUNT(*) FROM Carreta
    UNION ALL SELECT 'Cliente',     COUNT(*) FROM Cliente
    UNION ALL SELECT 'Producto',    COUNT(*) FROM Producto
    UNION ALL SELECT 'Roles',       COUNT(*) FROM Roles
    UNION ALL SELECT 'EstacionesPeaje', COUNT(*) FROM EstacionesPeaje
    UNION ALL SELECT 'FormatoControlado', COUNT(*) FROM FormatoControlado;

    COMMIT TRANSACTION LimpiezaTransaccional;

    PRINT ''
    PRINT '✅ LIMPIEZA COMPLETADA EXITOSAMENTE — TRANSACCIÓN CONFIRMADA'
    PRINT 'La nueva base de producción está lista para uso.'

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION LimpiezaTransaccional;

    -- Rehabilitar triggers aunque haya error (no dejarlos deshabilitados)
    BEGIN TRY
        ENABLE TRIGGER TR_AbastecimientoCombustible_Auditoria ON AbastecimientoCombustible;
        ENABLE TRIGGER TR_OrdenViaje_Auditoria ON OrdenViaje;
        ENABLE TRIGGER TR_Factura_Auditoria ON Factura;
        ENABLE TRIGGER TR_CPIC_Auditoria ON CPIC;
        ENABLE TRIGGER TR_Tracto_Auditoria ON Tracto;
        ENABLE TRIGGER TR_Conductor_Auditoria ON Conductor;
        ENABLE TRIGGER TR_Carreta_Auditoria ON Carreta;
        ENABLE TRIGGER TR_Liquidaciones_Auditoria ON Liquidaciones;
        ENABLE TRIGGER TR_UploadHistory_Auditoria ON UploadHistory;
        ENABLE TRIGGER trg_FirmaDigital_NoDelete ON FirmaDigital;
        ENABLE TRIGGER trg_FirmaDigital_NoUpdate ON FirmaDigital;
        PRINT '  ⚠️ Triggers rehabilitados tras error.'
    END TRY
    BEGIN CATCH
        PRINT '  ⚠️ No se pudo rehabilitar algunos triggers. Verificar manualmente.'
    END CATCH;

    PRINT ''
    PRINT '❌ ERROR — ROLLBACK EJECUTADO — NINGÚN DATO FUE MODIFICADO'
    PRINT 'Error #:       ' + CAST(ERROR_NUMBER() AS VARCHAR)
    PRINT 'Mensaje:       ' + ERROR_MESSAGE()
    PRINT 'Línea:         ' + CAST(ERROR_LINE() AS VARCHAR)
    PRINT 'Procedimiento: ' + ISNULL(ERROR_PROCEDURE(), 'Script directo')
    PRINT ''
    PRINT 'Corregir el error e intentar de nuevo.'

    THROW;
END CATCH;

PRINT ''
PRINT '================================================================'
PRINT 'FIN DEL SCRIPT DE LIMPIEZA TRANSACCIONAL'
PRINT '================================================================'
