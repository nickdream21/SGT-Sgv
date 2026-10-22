-- =============================================================================
-- Viajes de Crisar del 19/10/2026 (seguimiento 7466–7472, primeras cargas de terceros): la
-- primera consulta al GPS tomó para el tracto de descarga (relevo) el TCI y La Troncal de su
-- viaje anterior (BSD-716, ABJ-805, BFI-890 y BFR-730 terminaban una descarga de Vitapro;
-- BNH-712, BPT-919 y AVO-818 una de Crisar del Word del 19/10). Se corrigió la detección (el
-- tramo de descarga empieza cuando el tracto de descarga sale de la base con la carreta): aquí
-- se borran las horas después de la llegada a la base para que el GPS las vuelva a detectar.
-- Solo sgvActualizada. Idempotente.
-- =============================================================================
SET NOCOUNT ON;

DECLARE @viajes TABLE (id INT PRIMARY KEY);
INSERT INTO @viajes SELECT idSeguimiento FROM dbo.SeguimientoExportacion
WHERE idSeguimiento BETWEEN 7466 AND 7472 AND tipoRuta = 'CRISAR';

UPDATE se SET
    fhSalidaBase2 = NULL, fhLlegadaBodegaNacional = NULL, fhIngresoBodegaNacional = NULL, fhSalidaBodegaNacional = NULL,
    fhLlegadaCEBAF = NULL, fhCruceEcuador = NULL, fhLlegadaTCI = NULL, fhSalidaTCI = NULL,
    fhLlegadaPlantaEcuador = NULL, fhLlegadaAlmacen = NULL, fhIngreso = NULL, fhSalida = NULL, fhLlegadaBaseFinal = NULL,
    lugarFin = NULL, fechaModificacion = dbo.fn_AhoraPeru()
FROM dbo.SeguimientoExportacion se JOIN @viajes v ON v.id = se.idSeguimiento;
PRINT CONCAT('Viajes corregidos: ', @@ROWCOUNT);

DELETE h FROM dbo.SeguimientoHito h JOIN @viajes v ON v.id = h.idSeguimiento
WHERE h.columna IN ('fhSalidaBase2', 'fhLlegadaBodegaNacional', 'fhIngresoBodegaNacional', 'fhSalidaBodegaNacional',
                    'fhLlegadaCEBAF', 'fhCruceEcuador', 'fhLlegadaTCI', 'fhSalidaTCI',
                    'fhLlegadaPlantaEcuador', 'fhLlegadaAlmacen', 'fhIngreso', 'fhSalida', 'fhLlegadaBaseFinal');
PRINT CONCAT('Hitos borrados: ', @@ROWCOUNT);
