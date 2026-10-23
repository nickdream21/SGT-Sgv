-- =============================================================================
-- Posición aproximada con el grupo cuando el GPS de la unidad no tiene señal
--
-- BFR-730 (Crisar, 19/10/2026) no manda un solo punto al GPS. Viaja con las demás unidades
-- programadas el mismo día para la misma ruta, así que "Actualizar GPS" le pone la posición de la
-- compañera más cercana al centro del grupo y los hitos que la mayoría ya tiene (fuente GRUPO en
-- SeguimientoHito). gpsAproximadoDe guarda el tracto de esa compañera; NULL = posición del propio GPS.
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'gpsAproximadoDe') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD gpsAproximadoDe VARCHAR(20) NULL;
    PRINT 'SeguimientoExportacion: gpsAproximadoDe agregada.';
END
GO
