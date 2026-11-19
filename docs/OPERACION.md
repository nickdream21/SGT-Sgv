# Operación de SGV: vigilancia del sitio

Para enterarse si el sitio cae o si el GPS automático deja de correr (estuvo desactivado desde el 27/10/2026 sin que nadie lo notara).

## 1. La página de salud

`https://<sitio>/Salud.ashx` es anónima, solo lee y no escribe en la BD ni en la auditoría. Acepta GET y HEAD; un POST recibe 405 (PUT, DELETE y OPTIONS los responde IIS antes de llegar a la página). La respuesta solo dice `{"ok":true}` o `{"ok":false}`: el detalle va al log del sitio (`App_Data/logs`). Se reutiliza 20 s (solo BD) o 120 s (con GPS), así que un arreglo puede tardar hasta 2 min en verse.

| Llamada | Responde |
|---|---|
| `/Salud.ashx` | `200 {"ok":true}` si la BD responde a un `SELECT 1` en 5 s; si no, `503 {"ok":false}` |
| `/Salud.ashx?gps=1` | Además, `503 {"ok":false}` si a la vez: hay viajes que el GPS automático debería consultar, el sitio lleva más de 60 min arriba y la última corrida pedida por cron-job.org (no el botón del tablero) fue hace más de 60 min o no hubo |

"Viajes que debería consultar" es la misma regla del GPS automático (`SeguimientoViajesService.AbiertosParaGps`): no cerrados, con tracto y ya programados. La primera hora tras un reciclaje no avisa (la hora de la última corrida vive en memoria). Si la consulta GPS más reciente de esos viajes (`fhUltimaConsultaGps`) tiene más de 90 min, solo queda una advertencia en el log. En el log, los avisos del GPS ("Salud: GPS ...") se escriben al cambiar el estado o como mucho una vez por hora.

## 2. Crear los monitores en UptimeRobot (una vez)

1. Cuenta gratuita en https://uptimerobot.com (la crea el usuario) y, en *Alert Contacts*, el correo (y el celular si se quiere) que recibirá los avisos.
2. *Add New Monitor*, dos veces:

| Campo | Monitor 1: sitio y BD | Monitor 2: GPS automático |
|---|---|---|
| Monitor Type | Keyword | Keyword |
| Friendly Name | SGV - sitio | SGV - GPS automático |
| URL | `https://<sitio>/Salud.ashx` | `https://<sitio>/Salud.ashx?gps=1` |
| Keyword | `"ok":true` | `"ok":true` |
| Alert when | Keyword **not exists** | Keyword **not exists** |
| Monitoring Interval | 5 minutes | 5 minutes |

3. Marcar el contacto de alertas en ambos y guardar. A los pocos minutos los dos deben quedar en verde (*Up*). Un 503 también cuenta como caída aunque no se lea la palabra clave.

## 3. Qué hacer si avisan

**SGV - sitio caído**
1. Abrir el sitio en el navegador. Si no carga: revisar el panel de somee (sitio detenido, cuota, dominio) y si hay una publicación a medias (`app_offline.htm` olvidado; ver `herramientas/publicar`).
2. Si carga pero `Salud.ashx` da 503: la BD no responde. Probar `WebSGV/Database/sql.ps1 -Entorno produccion -ConfirmoProduccion "SELECT 1"`; si falla, es somee (estado del servidor SQL, contraseña rotada sin actualizar `connectionStrings.config`).
3. El motivo exacto queda en el log del sitio: "Salud: la base de datos no respondió".

**SGV - GPS automático atrasado** (el monitor 1 está en verde y el 2 no; en el log: "Salud: GPS ... atrasado")
1. Entrar a cron-job.org y revisar la tarea del GPS: si está desactivada o su historial muestra 403, la clave `GpsAutomaticoClave` del servidor no coincide (pasó el 27/10 al publicar con un `appSettings.Secrets.config` viejo). Ver `herramientas/gps-automatico/README.md`.
2. Si cron-job.org llama bien (202/200), abrir el tablero de seguimiento y presionar "Actualizar GPS": el mensaje dice si falla el proveedor GPS (Onway) y para qué unidades.
3. Si no hay viajes en curso, el monitor no avisa.
4. Si el log dice "última consulta GPS ... (más de 90 min)" pero el cron corre: las unidades están quietas o sin equipo en Onway; no es una caída.
