-- =============================================================================
-- Parada en el grifo Santa Rosa (El Retiro, Machala) de vuelta de Ecuador
--
-- Después de descargar en Jave o Inbalnor casi todas las unidades cargan combustible en ese grifo
-- antes de volver a la base (111 de 112 viajes del 15/09 al 20/10/2026). "Actualizar GPS" guarda la
-- llegada y la salida del grifo; el tablero muestra "UNIDAD DESCARGADA EN GRIFO SANTA ROSA" y el
-- botón "Cuadro grifo Santa Rosa" baja el cuadro de las unidades que están retornando.
-- No son hitos del STATUS GENERAL (la administradora no los anota en ese Excel).
--
-- Idempotente. Ejecutar PRIMERO en sgvActualizada (pruebas) y recién después en producción.
-- =============================================================================
SET NOCOUNT ON;
GO

IF COL_LENGTH('dbo.SeguimientoExportacion', 'fhLlegadaGrifo') IS NULL
BEGIN
    ALTER TABLE dbo.SeguimientoExportacion ADD
        fhLlegadaGrifo DATETIME NULL,
        fhSalidaGrifo  DATETIME NULL;
    PRINT 'SeguimientoExportacion: llegada y salida del grifo Santa Rosa agregadas.';
END
GO
