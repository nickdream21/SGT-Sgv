-- ============================================================
-- 23_ViajesEnProgresoEsInternacional.sql
-- ViajesEnProgreso.esInternacional nunca se llenaba: sp_CrearDespacho no lo guardaba al
-- crear o ampliar el viaje (en sgvActualizada los 12 viajes estaban en NULL; detectado el
-- 2026-10-11 por los tests de integracion). Efectos: DashboardConductor no veia ningun viaje
-- internacional y ocultaba los peajes extranjeros en la liquidacion, el historial decia
-- siempre NACIONAL y RegistroDespacho podia mezclar ambitos en un mismo viaje.
--
-- El SP ya lo guarda (basta un despacho internacional para que el viaje lo sea). Esta
-- migracion rellena los viajes existentes con la misma regla a partir de sus despachos
-- activos; los viajes sin despachos quedan en NULL.
-- Idempotente: solo toca viajes que siguen en NULL.
-- Ejecutar con Database/aplicar-migraciones.ps1.
-- ============================================================
SET NOCOUNT ON;

UPDATE vp
SET esInternacional = d.algunoInternacional
FROM dbo.ViajesEnProgreso vp
CROSS APPLY (
    SELECT MAX(CAST(ISNULL(esInternacional, 0) AS INT)) AS algunoInternacional
    FROM dbo.Despachos
    WHERE idViajeProgreso = vp.idViajeProgreso
      AND activo = 1
) d
WHERE vp.esInternacional IS NULL
  AND d.algunoInternacional IS NOT NULL;

PRINT CAST(@@ROWCOUNT AS VARCHAR(10)) + ' viaje(s) con esInternacional rellenado.';
GO
