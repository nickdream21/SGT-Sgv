-- ============================================================================
-- Afrecho: descarga falsa en el lado peruano de la frontera. Al reconectar la batería en TCI, el GPS
-- de AVM-877 (viaje 7785) mandó dos posiciones viejas (27/10/2026 07:42 en Aguas Verdes y 07:44 en
-- el cruce) y el sitio las tomó como la salida de TCI y la descarga "en AGUAS VERDES". El código ya
-- descarta esas posiciones y nunca toma una parada en el Perú como la descarga del afrecho.
-- Este script vacía en los viajes AFRECHO con la descarga detectada en el Perú las horas que puso el
-- GPS desde la salida de TCI (no las del Excel ni las corregidas a mano), borra esos hitos (estaban
-- mal) y quita el lugar de entrega que dedujo el GPS, para que "Actualizar GPS" lo vuelva a calcular.
-- Devuelve los viajes tocados.
--   WebSGV\Database\sql.ps1 -Archivo WebSGV\Database\Scripts\recalcular_afrecho_descarga_en_peru_2026-10-27.sql
--   herramientas\sitio-local\actualizar-gps.ps1 -Ids <los idSeguimiento devueltos>
-- ============================================================================
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @columnas TABLE (columna VARCHAR(40) PRIMARY KEY);
INSERT @columnas VALUES ('fhSalidaTCI'), ('fhLlegadaPlantaEcuador'), ('fhLlegadaAlmacen'), ('fhSalida'), ('fhLlegadaBaseFinal');

DECLARE @viajes TABLE (idSeguimiento INT PRIMARY KEY, lugarEntrega VARCHAR(60), regla VARCHAR(200));
INSERT @viajes
SELECT s.idSeguimiento, s.lugarEntrega, h.regla
FROM dbo.SeguimientoExportacion s
JOIN dbo.SeguimientoHito h ON h.idSeguimiento = s.idSeguimiento AND h.columna = 'fhLlegadaPlantaEcuador'
WHERE s.activo = 1 AND s.tipoRuta = 'AFRECHO'
  AND h.fuente IN ('GPS', 'ONWAY') AND h.latitud < -3.40 AND h.longitud < -80.225;   -- lado peruano

DECLARE @gps TABLE (idSeguimiento INT, columna VARCHAR(40));
INSERT @gps
SELECT h.idSeguimiento, h.columna
FROM dbo.SeguimientoHito h
JOIN @viajes v ON v.idSeguimiento = h.idSeguimiento
JOIN @columnas c ON c.columna = h.columna
WHERE h.fuente IN ('GPS', 'ONWAY');

UPDATE s SET
    fhSalidaTCI            = CASE WHEN EXISTS (SELECT 1 FROM @gps g WHERE g.idSeguimiento = s.idSeguimiento AND g.columna = 'fhSalidaTCI') THEN NULL ELSE s.fhSalidaTCI END,
    fhLlegadaPlantaEcuador = CASE WHEN EXISTS (SELECT 1 FROM @gps g WHERE g.idSeguimiento = s.idSeguimiento AND g.columna = 'fhLlegadaPlantaEcuador') THEN NULL ELSE s.fhLlegadaPlantaEcuador END,
    fhLlegadaAlmacen       = CASE WHEN EXISTS (SELECT 1 FROM @gps g WHERE g.idSeguimiento = s.idSeguimiento AND g.columna = 'fhLlegadaAlmacen') THEN NULL ELSE s.fhLlegadaAlmacen END,
    fhSalida               = CASE WHEN EXISTS (SELECT 1 FROM @gps g WHERE g.idSeguimiento = s.idSeguimiento AND g.columna = 'fhSalida') THEN NULL ELSE s.fhSalida END,
    fhLlegadaBaseFinal     = CASE WHEN EXISTS (SELECT 1 FROM @gps g WHERE g.idSeguimiento = s.idSeguimiento AND g.columna = 'fhLlegadaBaseFinal') THEN NULL ELSE s.fhLlegadaBaseFinal END,
    -- El lugar que dedujo el GPS de esa parada ("AGUAS VERDES"); el del Word ("HUAQUILLAS") se queda.
    lugarEntrega           = CASE WHEN v.regla LIKE '%(' + s.lugarEntrega + ')%' THEN NULL ELSE s.lugarEntrega END,
    fechaModificacion      = dbo.fn_AhoraPeru()
FROM dbo.SeguimientoExportacion s JOIN @viajes v ON v.idSeguimiento = s.idSeguimiento;

DELETE h FROM dbo.SeguimientoHito h
JOIN @viajes v ON v.idSeguimiento = h.idSeguimiento
JOIN @columnas c ON c.columna = h.columna;

SELECT v.idSeguimiento, v.lugarEntrega AS lugarEntregaAnterior, v.regla AS descargaAnterior,
       (SELECT COUNT(*) FROM @gps g WHERE g.idSeguimiento = v.idSeguimiento) AS horasVaciadas
FROM @viajes v ORDER BY v.idSeguimiento;
COMMIT TRANSACTION;
