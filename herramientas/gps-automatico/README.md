# Actualización automática del GPS del seguimiento

El tablero del seguimiento (RegistroSeguimiento) se actualiza solo si algo llama cada 10–15 minutos a:

```
GET https://sgvtransporte.somee.com/Views/Exportacion/GpsAutomatico.ashx
Encabezado:  X-SGV-Clave: <GpsAutomaticoClave>
```

La clave es `GpsAutomaticoClave` del `appSettings.Secrets.config` **del servidor** (no está en git y `publicar.ps1`
no la sube: el archivo del servidor se cambia aparte). La llamada responde al instante (202 = empezó, 200 = ya hay una en curso o la anterior fue hace menos de
5 min) y la actualización corre en el servidor: sincroniza los despachos nuevos, trae en **una sola consulta** la
última posición de toda la flota y analiza con el historial solo los viajes cuyos tractos se movieron (los
quietos solo refrescan la hora de su posición; cada 45 min se analizan todos igual). Sin clave o con otra: 403.

Un temporizador dentro del sitio no sirve: somee apaga la aplicación cuando nadie la visita. La llamada
periódica además la mantiene despierta. Con el tablero abierto, cada 2 min se pregunta si hay datos nuevos y
aparece "El GPS se actualizó a las HH:mm — Ver datos nuevos".

## Opción A (recomendada): cron-job.org

Gratis y funciona aunque la PC de la oficina esté apagada.

1. Crear una cuenta en https://cron-job.org (la crea el usuario; una vez).
2. *Create cronjob*:
   - URL: `https://sgvtransporte.somee.com/Views/Exportacion/GpsAutomatico.ashx`
   - Schedule: *Every 10 minutes* (o 15).
   - *Advanced* → *Headers*: `X-SGV-Clave` = el valor de `GpsAutomaticoClave`.
   - Request method: GET. Timeout: el que venga (responde en menos de 1 s).
3. *Test run*: debe responder 202 con `"iniciada":true`. El historial de cron-job.org muestra cada llamada.

## Opción B: Programador de tareas de Windows (PC de la oficina)

Solo funciona mientras la PC esté encendida.

```powershell
# Probar una vez
.\herramientas\gps-automatico\Llamar-GpsAutomatico.ps1

# Registrar la tarea cada 10 minutos (una vez; pide confirmación de Windows)
$accion = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$PWD\herramientas\gps-automatico\Llamar-GpsAutomatico.ps1`""
$disparo = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 10)
Register-ScheduledTask -TaskName 'SGV - GPS automático' -Action $accion -Trigger $disparo
```

## Si cambia la clave

Cambiarla en el `appSettings.Secrets.config` del servidor como dice "Cambiar una clave sin publicar" en
`herramientas/publicar/README.md` (bajarlo por FTP, editarlo, subirlo y reciclar el grupo de aplicaciones) y
después en cron-job.org (la tarea de Windows lee el archivo local sola). Ponerla también en el
`appSettings.Secrets.config` local de cada PC: `publicar.ps1` no compila el paquete si falta.

## Si cron-job.org manda 403 o desactiva la tarea

403 = el `appSettings.Secrets.config` del servidor no tiene la misma `GpsAutomaticoClave` que el encabezado
`X-SGV-Clave` de la tarea (pasó el 27/10/2026, cuando se publicaba desde Visual Studio y se subió un archivo viejo;
`publicar.ps1` ya no sube secretos). Corregir la clave en el archivo del servidor ("Cambiar una clave sin publicar"
en `herramientas/publicar/README.md`, o poner como clave el valor del encabezado de la tarea), reciclar el grupo de
aplicaciones y en cron-job.org activar la tarea y hacer *Test run* (202). El tablero avisa si falta la clave.
