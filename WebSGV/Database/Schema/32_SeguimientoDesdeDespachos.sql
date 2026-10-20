-- =============================================================================
-- El seguimiento de exportación nace de los despachos (programación), no del Excel
--
-- SeguimientoExportacion.origen dice de dónde salió cada viaje:
--   DESPACHO  creado por sp_SE_SincronizarDespachos a partir del despacho de carga de
--             exportación (y completado con su despacho de descarga en Ecuador).
--   MANUAL    creado con "Nuevo viaje" en la pantalla de seguimiento.
--   NULL      registros anteriores: importados del Excel STATUS GENERAL o del formulario
--             viejo. El tablero ya no los muestra por defecto (son historia).
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'origen') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD origen VARCHAR(10) NULL;
    PRINT 'SeguimientoExportacion: columna origen agregada.';
END
GO

-- Los pocos viajes ya vinculados a un despacho vienen de la programación.
UPDATE dbo.SeguimientoExportacion SET origen = 'DESPACHO'
WHERE origen IS NULL AND idDespachoOrigen IS NOT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeguimientoExportacion_DespachoOrigen' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
BEGIN
    CREATE INDEX IX_SeguimientoExportacion_DespachoOrigen ON dbo.SeguimientoExportacion(idDespachoOrigen) WHERE idDespachoOrigen IS NOT NULL;
    PRINT 'SeguimientoExportacion: índice IX_SeguimientoExportacion_DespachoOrigen creado.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SeguimientoExportacion_Origen' AND object_id = OBJECT_ID('dbo.SeguimientoExportacion'))
BEGIN
    CREATE INDEX IX_SeguimientoExportacion_Origen ON dbo.SeguimientoExportacion(origen) WHERE origen IS NOT NULL;
    PRINT 'SeguimientoExportacion: índice IX_SeguimientoExportacion_Origen creado.';
END
GO
