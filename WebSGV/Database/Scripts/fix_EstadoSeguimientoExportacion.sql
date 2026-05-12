-- ============================================================================
-- Recalcula el campo "estado" de SeguimientoExportacion para los registros
-- existentes (en especial los importados desde Excel que quedaron como
-- EN_CURSO aun cuando el viaje ya tenia fhTerminoDescarga / fhSalida).
--
-- Reglas:
--   COMPLETADO -> tiene fhTerminoDescarga o fhSalida
--   EN_CURSO   -> tiene algun hito intermedio
--   PENDIENTE  -> no tiene hitos
-- ============================================================================

UPDATE SeguimientoExportacion
SET estado =
    CASE
        WHEN fhTerminoDescarga IS NOT NULL OR fhSalida IS NOT NULL
            THEN 'COMPLETADO'
        WHEN fhSalidaBase1 IS NOT NULL
          OR fhLlegadaTrujillo IS NOT NULL
          OR fhIngresoPlanta IS NOT NULL
          OR fhInicioCarga IS NOT NULL
          OR fhTerminoCarga IS NOT NULL
          OR fhSalidaPlanta IS NOT NULL
          OR fhLlegadaBase2 IS NOT NULL
          OR fhSalidaBase2 IS NOT NULL
          OR fhLlegadaBodegaNacional IS NOT NULL
          OR fhIngresoBodegaNacional IS NOT NULL
          OR fhSalidaBodegaNacional IS NOT NULL
          OR fhLlegadaCEBAF IS NOT NULL
          OR fhCruceEcuador IS NOT NULL
          OR fhAutorizacionNacionalizacion IS NOT NULL
          OR fhLlegadaTCI IS NOT NULL
          OR fhSalidaTCI IS NOT NULL
          OR fhLlegadaPlantaEcuador IS NOT NULL
          OR fhLlegadaAlmacen IS NOT NULL
          OR fhIngreso IS NOT NULL
          OR fhInicioDescarga IS NOT NULL
            THEN 'EN_CURSO'
        ELSE 'PENDIENTE'
    END
WHERE activo = 1;

-- Diagnostico: distribucion por estado
SELECT estado, COUNT(*) AS total
FROM SeguimientoExportacion
WHERE activo = 1
GROUP BY estado
ORDER BY estado;
