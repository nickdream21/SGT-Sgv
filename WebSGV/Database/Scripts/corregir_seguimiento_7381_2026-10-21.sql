-- ============================================================================
-- Viaje 7381 del seguimiento (TBM-898, carreta TDX-994, carga programada el 21/10/2026):
-- "Actualizar GPS" le copió las 5 horas de la carga anterior del mismo tracto (viaje 7095,
-- carreta T0L-981: salida de base 19/10 13:48, planta 20/10 06:56-09:37, base 20/10 21:01).
-- El código ya no lo hace (la carga empieza después de la salida de planta del viaje anterior
-- del tracto); este script borra esas horas para que el GPS registre las de verdad (salió de
-- base el 20/10 a las 21:44 y a las 10:45 del 21/10 ya estaba en la cola de Trujillo).
-- Solo toca las columnas cuya hora vino del GPS y es la misma del viaje 7095.
--   WebSGV\Database\sql.ps1 -Archivo WebSGV\Database\Scripts\corregir_seguimiento_7381_2026-10-21.sql
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @cols TABLE (columna VARCHAR(40));
INSERT @cols
SELECT h.columna
FROM dbo.SeguimientoHito h
JOIN dbo.SeguimientoHito a ON a.idSeguimiento = 7095 AND a.columna = h.columna AND a.horaGps = h.horaGps
WHERE h.idSeguimiento = 7381 AND h.fuente IN ('GPS', 'ONWAY')
  AND h.columna IN ('fhSalidaBase1', 'fhLlegadaTrujillo', 'fhIngresoPlanta', 'fhSalidaPlanta', 'fhLlegadaBase2');

UPDATE se SET
    fhSalidaBase1     = CASE WHEN EXISTS (SELECT 1 FROM @cols WHERE columna = 'fhSalidaBase1')     THEN NULL ELSE fhSalidaBase1 END,
    fhLlegadaTrujillo = CASE WHEN EXISTS (SELECT 1 FROM @cols WHERE columna = 'fhLlegadaTrujillo') THEN NULL ELSE fhLlegadaTrujillo END,
    fhIngresoPlanta   = CASE WHEN EXISTS (SELECT 1 FROM @cols WHERE columna = 'fhIngresoPlanta')   THEN NULL ELSE fhIngresoPlanta END,
    fhSalidaPlanta    = CASE WHEN EXISTS (SELECT 1 FROM @cols WHERE columna = 'fhSalidaPlanta')    THEN NULL ELSE fhSalidaPlanta END,
    fhLlegadaBase2    = CASE WHEN EXISTS (SELECT 1 FROM @cols WHERE columna = 'fhLlegadaBase2')    THEN NULL ELSE fhLlegadaBase2 END,
    fechaModificacion = dbo.fn_AhoraPeru()
FROM dbo.SeguimientoExportacion se
WHERE se.idSeguimiento = 7381;

DELETE FROM dbo.SeguimientoHito WHERE idSeguimiento = 7381 AND columna IN (SELECT columna FROM @cols);

SELECT (SELECT COUNT(*) FROM @cols) AS horasBorradas;
COMMIT TRANSACTION;
