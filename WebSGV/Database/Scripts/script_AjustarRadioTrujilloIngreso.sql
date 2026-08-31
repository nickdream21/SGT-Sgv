-- =============================================================================
-- Ajuste de calibración: radio del checkpoint TRUJILLO_INGRESO en PuntoControlGps.
--
-- Contexto (2026-08-31/09-01, viaje real ASU-862): con el radio original (300m),
-- DetectarLlegada matcheaba a veces un grupo de puntos GPS a ~162m del checkpoint
-- (una zona distinta, más arriba en la misma calle — probablemente donde el
-- camión espera/maniobra antes de entrar) en vez del ingreso real a planta, que
-- según coordenadas confirmadas por la administradora de transporte queda a
-- ~20-95m del checkpoint:
--   - 27/08/2026 08:45:31  -8.136205, -79.013214  (~95m — "entrando a planta")
--   - 27/08/2026 08:45:45  -8.135617, -79.013400   (~45m)
--   - 27/08/2026 08:46:10  -8.135521, -79.013170   (~20m — la más exacta)
-- vs. el grupo incorrecto, más lejos:
--   -8.136772, -79.012730  (~162m)
--   -8.136801, -79.012910  (~162m)
--
-- 300m alcanzaba a cubrir ambos grupos; 120m deja margen de sobra a ambos lados
-- (95m del ingreso real vs. 162m del grupo incorrecto) sin arriesgar excluir el
-- ingreso real por drift normal del GPS.
--
-- El radio de TRUJILLO_SALIDA (500m) NO se toca a propósito: para una salida el
-- algoritmo necesita ver al camión alejándose (puntos naturalmente más lejos del
-- checkpoint conforme avanza), eso ya se resuelve con la ventana de confirmación
-- de DetectarSalida, no achicando el radio — achicarlo ahí arriesgaría excluir
-- salidas reales ya validadas contra datos reales (ver CheckpointMatchingServiceTests).
-- =============================================================================
SET NOCOUNT ON;

UPDATE PuntoControlGps
SET radioMetros = 120
WHERE nombre = 'TRUJILLO_INGRESO';

SELECT nombre, latitud, longitud, radioMetros FROM PuntoControlGps WHERE nombre = 'TRUJILLO_INGRESO';
