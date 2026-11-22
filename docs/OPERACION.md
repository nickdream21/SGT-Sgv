# Operación de SGV: vigilancia del sitio y avisos de errores

Para enterarse si el sitio cae o si el GPS automático deja de correr (estuvo desactivado desde el 27/10/2026 sin que nadie lo notara).

**Dónde está cada procedimiento**

| Para | Documento |
|---|---|
| Publicar, volver atrás, y lo de antes y después de cada publicación | [`herramientas/publicar/README.md`](../herramientas/publicar/README.md) |
| Respaldo y restauración | [`docs/PROCEDIMIENTO_RESPALDO.md`](PROCEDIMIENTO_RESPALDO.md) (detalle técnico en `herramientas/respaldo/README.md`) |
| Pasar el sitio a la BD de producción `sgvTransporte` | [`docs/PASO_A_PRODUCCION.md`](PASO_A_PRODUCCION.md) |
| Preparar una PC (archivos fuera de git, herramientas, variables) | [`docs/PREPARAR_MAQUINA.md`](PREPARAR_MAQUINA.md) |
| GPS automático (cron-job.org) | [`herramientas/gps-automatico/README.md`](../herramientas/gps-automatico/README.md) |
| Vigilancia del sitio y avisos de errores | este documento |

## 1. La página de salud

`https://sgvtransporte.somee.com/Salud.ashx` es anónima, solo lee y no escribe en la BD ni en la auditoría. Acepta GET y HEAD; un POST recibe 405 (PUT, DELETE y OPTIONS los responde IIS antes de llegar a la página). La respuesta solo dice `{"ok":true}` o `{"ok":false}`: el detalle va al log del sitio (`App_Data/logs`). Se reutiliza 20 s (solo BD) o 120 s (con GPS), así que un arreglo puede tardar hasta 2 min en verse.

| Llamada | Responde |
|---|---|
| `/Salud.ashx` | `200 {"ok":true}` si la BD responde a un `SELECT 1` en 5 s; si no, `503 {"ok":false}` |
| `/Salud.ashx?gps=1` | Además, `503 {"ok":false}` si a la vez: hay viajes que el GPS automático debería consultar, el sitio lleva más de 60 min arriba y la última corrida pedida por cron-job.org (no el botón del tablero) fue hace más de 60 min o no hubo (regla del cron). También `503` si el cron sí corrió en la última hora pero la última corrida programada en la que Onway respondió bien fue hace más de 120 min o no hubo, con el sitio más de 120 min arriba (regla del proveedor: credenciales vencidas, caída de Onway) |

"Viajes que debería consultar" es la misma regla del GPS automático (`SeguimientoViajesService.AbiertosParaGps`): no cerrados, con tracto y ya programados. La primera hora tras un reciclaje no avisa (la hora de la última corrida vive en memoria). Si la consulta GPS más reciente de esos viajes (`fhUltimaConsultaGps`) tiene más de 90 min, solo queda una advertencia en el log. En el log, los avisos del GPS ("Salud: GPS ...") dicen qué regla falló (`atrasado`: el cron no corre; `onway_sin_respuesta`: el cron corre pero Onway no responde; `cron_con_error`: igual, pero la última corrida programada terminó con una excepción y no llegó a consultar a Onway) y se escriben al cambiar el estado o como mucho una vez por hora.

"Onway respondió bien" es la consulta de la última posición de toda la flota que hace cada corrida (`ResultadoActualizacionGps.OnwayRespondio`; la hora queda en memoria en `ActualizacionGpsAutomatica.UltimaRespuestaOnwayProgramada`). Una flota vacía (respuesta 200 sin unidades: cuenta o subdominio cambiado) cuenta como fallo. No depende de que algún viaje se pueda seguir: unidades quietas o sin equipo GPS en Onway no cuentan como falla. Por eso no se usa `fhUltimaConsultaGps`, que no se escribe en esos casos. Solo cuentan las corridas de cron-job.org, no el botón del tablero; tras un reciclaje la marca se pierde y la regla espera 120 min.

## 2. Crear los monitores en UptimeRobot (una vez)

1. Cuenta gratuita en https://uptimerobot.com (la crea el usuario) y, en *Alert Contacts*, el correo (y el celular si se quiere) que recibirá los avisos.
2. *Add New Monitor*, dos veces:

| Campo | Monitor 1: sitio y BD | Monitor 2: GPS automático |
|---|---|---|
| Monitor Type | Keyword | Keyword |
| Friendly Name | SGV - sitio | SGV - GPS automático |
| URL | `https://sgvtransporte.somee.com/Salud.ashx` | `https://sgvtransporte.somee.com/Salud.ashx?gps=1` |
| Keyword | `"ok":true` | `"ok":true` |
| Alert when | Keyword **not exists** | Keyword **not exists** |
| Monitoring Interval | 5 minutes | 5 minutes |

3. Marcar el contacto de alertas en ambos y guardar. A los pocos minutos los dos deben quedar en verde (*Up*). Un 503 también cuenta como caída aunque no se lea la palabra clave.

## 3. Qué hacer si avisan

**SGV - sitio caído**
1. Abrir el sitio en el navegador. Si no carga: revisar el panel de somee (sitio detenido, cuota, dominio) y si hay una publicación a medias (`app_offline.htm` olvidado; ver `herramientas/publicar`).
2. Si carga pero `Salud.ashx` da 503: la BD no responde. Probar `WebSGV/Database/sql.ps1 "SELECT 1"` (sin `-Entorno`: la BD de pruebas `sgvActualizada`, que es la que usa hoy el sitio publicado; tras el [paso a producción](PASO_A_PRODUCCION.md) sería `-Entorno produccion -ConfirmoProduccion`); si falla, es somee (estado del servidor SQL, contraseña rotada sin actualizar el `connectionStrings.config` del servidor y el de la PC).
3. El motivo exacto queda en el log del sitio: "Salud: la base de datos no respondió".

**SGV - GPS automático atrasado** (el monitor 1 está en verde y el 2 no; en el log: "Salud: GPS atrasado", "onway_sin_respuesta" o "cron_con_error")

Si el log dice `onway_sin_respuesta` o `cron_con_error`, el cron llama bien: revisar primero en el log del sitio las líneas "Actualización automática del GPS" (resumen de cada corrida) y las de nivel Error de esas horas. Con `cron_con_error` la corrida se cae antes de llegar a Onway (BD, sincronización de despachos): el detalle está en ese Error. Con `onway_sin_respuesta` el problema es Onway: ir al paso 2 y, si el mensaje dice que Onway no responde o rechaza el acceso, revisar las credenciales de Onway en `appSettings.Secrets.config` del servidor o el estado del servicio con el proveedor.

1. Entrar a cron-job.org y revisar la tarea del GPS: si está desactivada o su historial muestra 403, la clave `GpsAutomaticoClave` del servidor no coincide (pasó el 27/10 al publicar con un `appSettings.Secrets.config` viejo). Ver `herramientas/gps-automatico/README.md`.
2. Si cron-job.org llama bien (202/200), abrir el tablero de seguimiento y presionar "Actualizar GPS": el mensaje dice si falla el proveedor GPS (Onway) y para qué unidades.
3. Si no hay viajes en curso, el monitor no avisa.
4. Si el log dice "última consulta GPS ... (más de 90 min)" pero el cron corre: las unidades están quietas o sin equipo en Onway; no es una caída.

## 4. Aviso por correo de errores no controlados

Cada error no controlado del sitio (el que llega a `Global.asax` `Application_Error` y queda en el log como "Error no controlado en ...") y cada error de un postback parcial de UpdatePanel (`Site.Master`, "Error en postback parcial de ...") manda un correo de texto plano, en segundo plano, sin frenar la respuesta ni cambiar lo que ve el usuario. Código: `Services/Operacion/AvisoErrores.cs` (reglas, probadas en `AvisoErroresTests`) y `Helpers/CorreoHelper.cs` (envío común, el mismo de los avisos de liquidaciones y de la recuperación de contraseña).

**Qué no cubre:** los errores que las páginas ya capturan y muestran con `MensajeErrorHelper` (quedan solo en el log) y los `[WebMethod]` (PageMethods: el ScriptHandler responde el error al navegador sin pasar por `Application_Error`).

**A quién:** la clave `AvisoErroresPara` de `WebSGV/Web.config` (no es secreta), hoy `sistemas.cibertic@serviciosgeneralesviviana.com`. Para cambiarlo, editar ese valor (varios destinatarios separados por comas) y publicar `Web.config`; vacío = no se envía nada. Las peticiones locales (PCs de desarrollo: `Request.IsLocal` o host `localhost`/`127.0.0.1`/`::1`) no avisan. Para probar el aviso en local, agregar temporalmente `<add key="AvisoErroresLocal" value="true" />` en `appSettings` de `Web.config`, provocar el error y quitarla (no se versiona; el asunto lleva el host, p. ej. `localhost:44390`).

**Remitente y servidor:** el SMTP de `appSettings.Secrets.config` (`SmtpHost`, `SmtpPort`, `SmtpSSL`, `SmtpUser`, `SmtpPass`) y `SmtpFrom` como remitente (`sgv-avisos@serviciosgeneralesviviana.com`, alias verificado de la cuenta de Gmail con la que se autentica). Sin `SmtpFrom` se usa `SmtpUser`. TLS siempre salvo `SmtpSSL=false` explícito, 30 s de espera como máximo (el envío va en segundo plano). Un fallo al enviar deja una línea en el log ("No se pudo enviar el correo ... a ***@dominio", sin la dirección completa).

**Límite** (en memoria; se reinicia cuando el sitio se recicla):
- Como mucho 1 correo cada 30 min por *tipo* de error: el tipo es la excepción + el lugar del código (primera línea propia de la pila) + la ruta (sin querystring, sin mayúsculas, con los números como `#`).
- Como mucho 10 correos por día en total (día de Perú).
- Un envío fallido no gasta el cupo: el tipo queda libre y el siguiente error igual vuelve a intentar. Tras 3 fallos seguidos del SMTP se pausan los envíos 15 min ("Aviso de error omitido: envíos en pausa ...").
- La primera omisión de cada tipo en la ventana deja en el log "Aviso de error omitido por límite (tipo ...)"; las siguientes no, para no llenarlo.
- Los omitidos se cuentan y el siguiente correo dice "Y N errores más sin aviso desde el ...": el detalle de esos está en `App_Data/logs`.

**No avisan:** 404, peticiones inválidas (`HttpException` 4xx), "potentially dangerous Request..." (`HttpRequestValidationException`), ViewState vencido o inválido, `ThreadAbortException` (`Response.Redirect`), `ErrorNegocioException` (mensajes para el usuario), "Invalid postback or callback argument" (validación de eventos: un POST manipulado), los envíos demasiado grandes de un postback normal y los sondeos de bots (`.php`, `/wp-`, `/.env`, `/.git`, `cgi-bin`, `phpmyadmin`...).

**Qué trae:** fecha y hora de Perú, sitio (host), URL sin los valores del querystring (solo los nombres: `?id=&token=`), método, usuario y rol si hay sesión, IP, tipo, mensaje, lugar y la excepción completa con sus internas y la pila. Nunca valores de formularios, cookies, contraseñas, cadenas de conexión ni el querystring con valores.

**Qué hacer al recibir uno:** buscar la misma hora en `App_Data/logs/sgv-AAAAMMDD*.log` del servidor (al pasar de 20 MB el día sigue en `sgv-AAAAMMDD_001.log`, `_002`…; allí está todo, también los omitidos), reproducir con la ruta y el usuario del correo, y corregir con una tarea en `docs/PLAN_MEJORAS_SGV.md`.
