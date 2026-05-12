-- =============================================================================
-- RESET: SeguimientoExportacion
--   Borra TODA la data de seguimiento de exportación y resetea el IDENTITY
--   para arrancar limpio (ej. cargar solo abril 2025).
--
--   USO:
--     1. Ejecutar este script en SSMS contra la BD del sistema.
--     2. Luego ir a Views/Exportacion/RegistroSeguimiento.aspx y usar el
--        "Importar Excel" con el archivo abril2025.xlsx.
--     3. Validar conteos con la sección de verificación al final.
--
--   ADVERTENCIA: Operación destructiva. NO HAY UNDO. Hace BACKUP automático
--                en SeguimientoExportacion_Backup_YYYYMMDD por seguridad.
-- =============================================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- ── 1. Backup automático ───────────────────────────────────────────────
    DECLARE @bkpName SYSNAME =
        'SeguimientoExportacion_Backup_' + CONVERT(VARCHAR(8), GETDATE(), 112)
        + '_' + REPLACE(CONVERT(VARCHAR(8), GETDATE(), 108), ':', '');

    DECLARE @sql NVARCHAR(MAX) =
        'SELECT * INTO ' + QUOTENAME(@bkpName) + ' FROM SeguimientoExportacion;';

    IF EXISTS (SELECT 1 FROM SeguimientoExportacion)
    BEGIN
        EXEC sp_executesql @sql;
        PRINT 'Backup creado: ' + @bkpName;
    END
    ELSE
    BEGIN
        PRINT 'Tabla vacia, no se hizo backup.';
    END

    -- ── 2. Borrado completo ────────────────────────────────────────────────
    DECLARE @cnt INT = (SELECT COUNT(*) FROM SeguimientoExportacion);
    PRINT 'Registros antes de borrar: ' + CAST(@cnt AS VARCHAR(10));

    DELETE FROM SeguimientoExportacion;

    -- ── 3. Reset del IDENTITY ──────────────────────────────────────────────
    DBCC CHECKIDENT ('SeguimientoExportacion', RESEED, 0);

    PRINT 'Registros borrados. IDENTITY reseteado a 1.';

    COMMIT TRANSACTION;
    PRINT '== RESET COMPLETO ==';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
    THROW;
END CATCH
GO

-- =============================================================================
-- VERIFICACIÓN POST-IMPORTACIÓN
-- Ejecuta este bloque DESPUÉS de subir abril2025.xlsx por la UI
-- =============================================================================
PRINT '';
PRINT '== Estado actual de la tabla ==';
SELECT COUNT(*) AS totalRegistros FROM SeguimientoExportacion;

PRINT '';
PRINT '== Resumen por mes (fhProgramacion) ==';
SELECT
    YEAR(fhProgramacion)  AS anio,
    MONTH(fhProgramacion) AS mes,
    COUNT(*)                                                AS totalRegistros,
    SUM(CASE WHEN fhLlegadaTrujillo IS NOT NULL THEN 1 END) AS conLlegadaTrujillo,
    SUM(CASE WHEN fhIngresoPlanta   IS NOT NULL THEN 1 END) AS conIngresoPlanta,
    SUM(CASE WHEN fhInicioCarga     IS NOT NULL THEN 1 END) AS conInicioCarga,
    SUM(CASE WHEN fhTerminoCarga    IS NOT NULL THEN 1 END) AS conTerminoCarga,
    SUM(CASE WHEN fhSalidaPlanta    IS NOT NULL THEN 1 END) AS conSalidaPlanta,
    SUM(CASE WHEN fhIngresoBodegaNacional IS NOT NULL THEN 1 END) AS conBodegaNacional,
    SUM(CASE WHEN fhLlegadaTCI      IS NOT NULL THEN 1 END) AS conTCI,
    SUM(CASE WHEN fhLlegadaCEBAF    IS NOT NULL THEN 1 END) AS conCEBAF
FROM SeguimientoExportacion
WHERE activo = 1 AND fhProgramacion IS NOT NULL
GROUP BY YEAR(fhProgramacion), MONTH(fhProgramacion)
ORDER BY anio, mes;
GO

-- =============================================================================
-- ROLLBACK opcional: si algo salió mal puedes restaurar desde el backup
-- (descomenta y edita el nombre del backup según el printed arriba)
-- =============================================================================
-- TRUNCATE TABLE SeguimientoExportacion;
-- DBCC CHECKIDENT ('SeguimientoExportacion', RESEED, 0);
-- SET IDENTITY_INSERT SeguimientoExportacion ON;
-- INSERT INTO SeguimientoExportacion (idSeguimiento, cliente, ... )
-- SELECT idSeguimiento, cliente, ... FROM SeguimientoExportacion_Backup_YYYYMMDD_HHMMSS;
-- SET IDENTITY_INSERT SeguimientoExportacion OFF;
