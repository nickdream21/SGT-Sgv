# Reporte GPS de tramos (Excel)

Herramienta **independiente de la web SGV** para sacar, con datos del GPS (Customer API de
Location World / Entel Onway), un reporte en Excel de los tramos que recorrió un vehículo:
distancia, tiempo de viaje, tiempo en movimiento vs. detenido, paradas y odómetro.

No se compila ni se despliega con `WebSGV`: son scripts de PowerShell sueltos que solo leen
las credenciales de `WebSGV/appSettings.Secrets.config` y el token cacheado en la tabla
`OnwayAuthCache`.

## Cómo se usa

```powershell
# 1) Caso normal: ya hay un token vigente en OnwayAuthCache
.\Generar-ReporteTramos.ps1

# 2) Con un token generado a mano en Postman (ver más abajo)
.\Generar-ReporteTramos.ps1 -AccessToken "eyJ..." -ExpiresIn 86400

# 3) Recalcular sin volver a consultar el API (usa el JSON crudo de datos\)
.\Generar-ReporteTramos.ps1 -SinDescargar

# 4) Cambiar el criterio de parada (por defecto: <= 3 km/h durante >= 3 min)
.\Generar-ReporteTramos.ps1 -SinDescargar -UmbralKmh 3 -MinutosParada 10
```

El resultado queda en `salida\ReporteGPS_<placa>_2tramos.xlsx`, con 5 hojas: `Resumen`
(los tramos lado a lado), y `Paradas` / `Viajes` por cada tramo.

Para reportar **otras fechas u otro vehículo**, editar `$Placa` y `$Tramos` al inicio de
`Generar-ReporteTramos.ps1`: las horas van en hora local de Perú (UTC-5), el script las
convierte a UTC.

## El token de Auth0 (lo único que suele fallar)

Location World permite **un solo token activo por `client_id`**, sin importar el entorno. Si
producción, otra máquina o una pestaña de Postman ya tiene uno vigente, pedir uno nuevo
devuelve `401 access_denied` aunque las credenciales sean correctas. Por eso el script
**siempre reusa** el token cacheado en `OnwayAuthCache` mientras le queden más de 10 minutos.

Si no hay ninguno vigente y Auth0 rechaza la renovación, se genera a mano:

```
POST https://location-world.auth0.com/oauth/token
Content-Type: application/json

{ "client_id": "...", "client_secret": "...",
  "audience": "https://customer-api.location-world.com",
  "grant_type": "client_credentials" }
```

(las credenciales están en `WebSGV/appSettings.Secrets.config`) y se pasa el `access_token`
resultante con `-AccessToken`. El script lo siembra en `OnwayAuthCache`, así que la web
también lo aprovecha. Ver `WebSGV/Database/Scripts/diagnostico_OnwayAuthCache_TokenActivoUnico.sql`.

## Qué da cada endpoint (verificado contra el API real, no solo el swagger)

| Endpoint | Lo que se usa |
|---|---|
| `GET .../devices` | ubicar el `deviceId` por placa (campo `alias`) |
| `GET .../devices/{id}/history` | posición, velocidad, dirección y **`mileage` = odómetro** en km. `mileage` **no está documentado en el swagger** pero sí viene en la respuesta, y coincide con la columna "Odómetro (Km)" del portal: es la fuente principal de distancia |
| `GET .../devices/{id}/trips` | los encendidos del motor: inicio/fin, minutos, distancia, velocidad promedio, score, frenadas y aceleraciones bruscas. `maxSpeed` viene en 0, así que la velocidad máxima se calcula del historial |
| `POST .../devices/trips/operation-indicators` | odómetro inicial/final, distancia y horas de operación / movimiento / ralentí — **redondeadas a horas enteras**, sirven solo como referencia |
| `GET .../devices/{id}/history/canbus` | odómetro y **horómetro** (`totalEngineHours`) por punto — **la unidad de CBV-829 no reporta CANBUS**, devuelve vacío |

Detalles del API que cuesta descubrir:

- `from`/`to` aceptan **máximo 1 día** por llamada; el cliente trocea el rango solo.
- Rate limit de **2 req/s**; hay un throttle de 600 ms entre llamadas.
- `trips` y `operation-indicators` agrupan **por día y según el día en que arrancó el viaje**:
  para no perder el viaje que empezó la noche anterior hay que barrer también el día previo y
  recortar después (eso hace `Get-ViajesDelTramo`).
- El **horómetro por punto no lo expone el API** sin CANBUS. Como equivalente se reporta el
  "tiempo con motor en marcha" sumando la duración de los viajes.

## Archivos

| Archivo | Rol |
|---|---|
| `OnwayCliente.ps1` | autenticación, rate limit, paginación y endpoints (port de `Services/GpsIntegracion/OnwayApiClient.cs`) |
| `AnalisisTramo.ps1` | métricas del tramo: movimiento vs. detenido, paradas, distancias, velocidades |
| `ExcelWriter.ps1` | escritor `.xlsx` (OOXML) sin dependencias |
| `Generar-ReporteTramos.ps1` | punto de entrada; aquí se definen los tramos |
| `datos\` | JSON crudo descargado del API (gitignored) |
| `salida\` | el Excel generado (gitignored) |

## Criterio de "detenido"

Un intervalo entre dos posiciones consecutivas cuenta como **detenido** cuando el odómetro no
avanzó (menos de 50 m) y la velocidad de ambos extremos fue ≤ 3 km/h. Las rachas detenidas de
3 minutos o más se listan como **paradas**; las más cortas (semáforos, frenadas, peajes) se
resumen aparte como "detenciones menores" para no inflar el listado.

El resumen incluye tres medidas de distancia — odómetro, suma de viajes y traza GPS
(haversine) — para poder cruzarlas: si difieren más de ~5 % es señal de huecos de cobertura.
