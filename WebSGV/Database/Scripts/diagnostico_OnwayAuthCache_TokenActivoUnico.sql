-- =============================================================================
-- Diagnóstico/rescate: OnwayAuthCache cuando "Verificar GPS" falla con
-- "No se pudo conectar con el sistema GPS" y en los logs (App_Data/logs/sgv-*.log)
-- aparece un 401 "access_denied" del propio Auth0 en /oauth/token.
--
-- CAUSA CONFIRMADA (2026-08-31): Location World/Auth0 solo permite UN token
-- activo a la vez por client_id — no por entorno. Si producción (u otra
-- consulta manual desde Postman) ya tiene un token vigente, cualquier intento
-- de generar uno nuevo desde este entorno se rechaza con 401, aunque las
-- credenciales (OnwayAuth0ClientId/Secret, OnwayUsername/Password en
-- appSettings.Secrets.config) sean correctas. No es un problema de
-- credenciales ni de sesión — ningún reintento local lo arregla solo.
--
-- SOLUCIÓN DE RESCATE: reusar el token que SÍ está vigente en este momento
-- (el que tenga producción, o uno recién generado a mano) en vez de pedirle
-- uno nuevo a Auth0. Pasos:
--   1. Obtener un access_token vigente:
--      - O bien copiar el de OnwayAuthCache de la otra base que sí lo tiene
--        vigente (ej. producción, si este entorno es pruebas), o
--      - O bien generarlo a mano en Postman:
--          POST https://location-world.auth0.com/oauth/token
--          Body: { "client_id": "...", "client_secret": "...",
--                   "audience": "https://customer-api.location-world.com",
--                   "grant_type": "client_credentials" }
--        y luego, con ese access_token como Bearer:
--          POST https://customer-api.location-world.com/v1/fleet/fleetpe/sessions
--          Body: { "username": "...", "password": "..." }
--        (las credenciales están en WebSGV/appSettings.Secrets.config, gitignored).
--   2. Completar los 4 valores de abajo con lo que devolvieron esas dos
--      respuestas (access_token + expires_in del paso 1; clientId + userId
--      del paso 2) y ejecutar este UPDATE contra la base de este entorno.
--   3. Reintentar "Verificar GPS" — debería reusar este token sin pedir uno
--      nuevo, ya que OnwayApiClient.ObtenerTokenValido() prioriza el cacheado
--      mientras no esté a menos de 10 minutos de expirar.
--
-- No pegar tokens ni credenciales reales en este archivo al hacer commit —
-- es un script de referencia versionado, no un lugar para secretos.
-- =============================================================================

UPDATE OnwayAuthCache
SET accessToken = '<PEGAR_ACCESS_TOKEN_AQUI>',
    tokenExpiraEn = '<PEGAR_FECHA_UTC_DE_EXPIRACION_AQUI>', -- ej. iat+expires_in del JWT, formato 'yyyy-MM-ddTHH:mm:ss'
    onwayClientId = '<PEGAR_CLIENTID_DE_LA_SESION_AQUI>',
    onwayUserId   = '<PEGAR_USERID_DE_LA_SESION_AQUI>',
    fechaActualizacion = SYSUTCDATETIME()
WHERE idCache = 1;

-- Verificación rápida (no debe mostrar el token completo en pantallas compartidas):
SELECT idCache, LEN(accessToken) AS len_token, tokenExpiraEn, fechaActualizacion
FROM OnwayAuthCache;
