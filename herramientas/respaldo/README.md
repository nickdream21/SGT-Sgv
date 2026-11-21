# Respaldo propio de la base de datos (E1-1)

`respaldar-bd.ps1` exporta la BD de SGV a un `.bacpac` (esquema + datos) en una carpeta **fuera de somee y fuera del repo**, y aplica una retención. Solo lee la BD.

En somee el usuario de la app es `db_owner` pero no tiene permiso `BACKUP`, por eso se usa `sqlpackage /Action:Export` en vez de un `.bak`.

## Uso

```powershell
# Ver qué haría (no exporta ni borra nada)
.\herramientas\respaldo\respaldar-bd.ps1 -Carpeta D:\RespaldosSGV -SoloListar

# Respaldar la BD de pruebas (sgvActualizada)
.\herramientas\respaldo\respaldar-bd.ps1 -Carpeta D:\RespaldosSGV

# Con la carpeta en una variable de entorno (útil para una tarea programada)
$env:SGV_CARPETA_RESPALDO = 'D:\RespaldosSGV'
.\herramientas\respaldo\respaldar-bd.ps1

# Producción (sgvTransporte): exige la confirmación explícita
.\herramientas\respaldo\respaldar-bd.ps1 -Carpeta D:\RespaldosSGV -Entorno produccion -ConfirmoProduccion

# Probar la regla de retención (sin BD ni archivos)
.\herramientas\respaldo\probar-retencion.ps1
```

Resultado: `<Carpeta>\bd\<nombreBD>_aaaaMMdd_HHmm.bacpac` y una línea por ejecución en `<Carpeta>\bd\respaldos.log`. Al terminar imprime ruta, tamaño y duración.

## Cómo funciona

- Lee la cadena `ConexionSGV` de `WebSGV\connectionStrings.config` (pruebas) o `connectionStrings.Production.config` (producción), igual que `WebSGV\Database\sql.ps1`, y comprueba que apunte a la BD esperada.
- La contraseña no va en la línea de comandos: se escribe en un archivo de respuesta temporal de `sqlpackage` (`sqlpackage @archivo`) en `%TEMP%` del usuario, que se borra al terminar (también si falla). Si `sqlpackage` la repitiera en un mensaje, se enmascara con `********`. Mientras dura la exportación, ese archivo existe en `%TEMP%` (solo accesible para el usuario de Windows).
- `sqlpackage` exporta a una carpeta **local** (`%LOCALAPPDATA%\SGV-respaldo\bd`); si termina bien, se copia al destino como `*.parcial.bacpac`, se verifica allí (tamaño con reintentos o SHA-256, igual que el espejo: Google Drive informa un tamaño incompleto justo después de escribir) y se renombra. El local se borra siempre. Si falla: código de salida distinto de 0, se borra el parcial, se anota `ERROR` en el log y **no** se aplica la retención.
- Se rechaza cualquier carpeta dentro del repo (el `.bacpac` contiene datos reales y contraseñas cifradas de usuarios).

## Retención

Conserva los **30 respaldos más recientes** y, de los anteriores, **el primero de cada mes** de los últimos **12 meses**. Borra el resto. Solo considera archivos con el patrón `<nombreBD>_aaaaMMdd_HHmm.bacpac`; cualquier otro archivo de la carpeta no se toca.

## Requisitos

- `sqlpackage` en el PATH: `dotnet tool install -g microsoft.sqlpackage` (probado con 170.5.96).
- `WebSGV\connectionStrings.config` (o el de producción) en la máquina; están gitignored.
- Windows PowerShell 5.1 o superior.

# Espejo de los archivos del sitio (E1-2)

`respaldar-archivos.ps1` copia por FTP los archivos del sitio publicado a la misma carpeta de respaldos, para no depender de somee:

| Servidor (FTP) | Copia local |
|---|---|
| `/www.sgvtransporte.somee.com/Uploads` (CPIC, manifiestos, facturas, guías…) | `<Carpeta>\archivos\Uploads\...` |
| `/www.sgvtransporte.somee.com/App_Data/OrdenesViaje` (PDF firmados, se conservan 5 años) | `<Carpeta>\archivos\OrdenesViaje\...` |

## Primera vez: guardar la contraseña FTP

```powershell
.\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial
```

Pide la contraseña FTP de `SGVTRANSPORTES` (no se muestra) y la guarda en `privado\ftp-respaldo.xml` con `Export-Clixml`: queda cifrada con DPAPI y **solo la puede leer el mismo usuario de Windows en la misma máquina** (en otra PC u otro usuario hay que repetirlo). Antes comprueba que `privado/` esté en `.gitignore`, y al final prueba la conexión listando la raíz del sitio.

**Repetir `-GuardarCredencial` cada vez que se rote la contraseña FTP** (si no, el espejo fallará al conectar).

## Uso

```powershell
# Ver qué bajaría (lista el servidor; no baja ni crea nada)
.\herramientas\respaldo\respaldar-archivos.ps1 -Carpeta D:\RespaldosSGV -SoloListar

# Espejo (incremental) + verificación de los PDF firmados
.\herramientas\respaldo\respaldar-archivos.ps1 -Carpeta D:\RespaldosSGV

# Con la variable de entorno de E1-1
$env:SGV_CARPETA_RESPALDO = 'D:\RespaldosSGV'
.\herramientas\respaldo\respaldar-archivos.ps1

# Solo verificar los PDF firmados de una copia contra la BD (pruebas, solo SELECT)
.\herramientas\respaldo\verificar-pdf-firmados.ps1 -Carpeta D:\RespaldosSGV          # usa archivos\OrdenesViaje
.\herramientas\respaldo\verificar-pdf-firmados.ps1 -Carpeta WebSGV\App_Data\OrdenesViaje

# Probar la lógica del espejo (sin FTP ni BD)
.\herramientas\respaldo\probar-espejo.ps1
```

## Cómo funciona el espejo

- **Solo lectura en el servidor**: usa únicamente `LIST`/`NLST`/`SIZE`/`RETR`; nunca sube, renombra ni borra nada en somee. Entiende el `LIST` en formato Unix y MS-DOS/IIS; si no lo reconoce, usa `NLST` + `SIZE`.
- **Incremental**: baja solo lo que no está en local o cuyo tamaño cambió. Se excluye toda carpeta `_temp` (borradores, p. ej. `Uploads\Factura\_temp`).
- **Nunca borra en local**: un archivo borrado en el servidor sigue en el respaldo. Si un archivo cambió de tamaño, la copia local anterior se conserva como `<nombre>__anterior_aaaaMMdd_HHmmss.<ext>` antes de poner la nueva.
- Cada archivo se baja primero a una carpeta **local** (`%LOCALAPPDATA%\SGV-respaldo\descargando`, fuera de Google Drive/OneDrive), se comprueba ahí que llegó entero (mismo tamaño que en el servidor) y se calcula su SHA-256. Ante un error de red se reintenta una vez; si vuelve a fallar se cuenta como error y se sigue con el resto.
- Después se copia a `<nombre>.descargando` en la carpeta final, se verifica allí y se renombra: primero el tamaño, releído cada 500 ms hasta ~10 s; si no cuadra, el SHA-256 leyendo el contenido. Solo es error si tampoco coincide el contenido. Motivo: Google Drive para escritorio (`G:\Unidades compartidas\...`) informa un tamaño incompleto justo después de escribir (se vio "llegaron 14520 bytes de 370309" con el archivo entero). Si el archivo final ya tiene el mismo contenido, no se toca ni se crea una versión `__anterior_`. La copia común está en `herramientas\comun\CopiaVerificada.psm1`.
- Al final llama a `verificar-pdf-firmados.ps1` sobre `<Carpeta>\archivos\OrdenesViaje`, imprime el resumen (revisados, bajados, cambiados, sin cambios, errores, verificación) y añade una línea a `<Carpeta>\archivos\respaldos.log`.
- Rechaza cualquier carpeta dentro del repo (igual que `respaldar-bd.ps1`).
- Código de salida: `0` todo bien; `1` errores al listar o bajar; `2` la descarga fue bien pero la verificación encontró PDF faltantes o distintos (o no se pudo hacer).
- El FTP de somee no admite TLS: usuario y contraseña viajan sin cifrar por la red (igual que en `publicar.ps1`).

## Verificación de los PDF firmados

`verificar-pdf-firmados.ps1` calcula el SHA-256 de cada archivo y lo compara con la BD (solo `SELECT`): `FirmaDigital.rutaPdfFirmado` + `hashDocumento` (todas las firmas con PDF, también las anuladas: siguen siendo evidencia) y `OrdenViaje.rutaPdfFirmado` + `hashPdfFirmado`. Informa:

- **OK**: existe y el hash coincide.
- **Faltantes**: en la BD pero no en la carpeta.
- **Distintos**: el hash no coincide. Si dos firmas usaron la misma ruta (PDF generados en máquinas distintas), indica con cuál coincide el archivo; si una versión `__anterior_` guardada por el espejo coincide, también lo dice.
- **Sobrantes**: en la carpeta sin registro en la BD (no es error).

Sale con código `1` si hay faltantes o distintos. Por defecto usa la BD de pruebas (`sgvActualizada`, la que usa hoy el sitio publicado); `-Entorno produccion -ConfirmoProduccion` para la otra.

# Ensayo de restauración y tarea programada (E1-3)

El procedimiento completo para personas (qué se respalda, cómo restaurar en un desastre, lista del ensayo mensual) está en [`docs/PROCEDIMIENTO_RESPALDO.md`](../../docs/PROCEDIMIENTO_RESPALDO.md).

## Ensayar la restauración

```powershell
# Importa el .bacpac más reciente de <Carpeta>\bd\ en el SQL Server local, compara y borra la BD
.\herramientas\respaldo\ensayar-restauracion.ps1 -Carpeta D:\RespaldosSGV

# Un archivo concreto, conservando la BD restaurada para revisarla a mano
.\herramientas\respaldo\ensayar-restauracion.ps1 -Bacpac D:\RespaldosSGV\bd\sgvActualizada_20261119_2300.bacpac -Conservar

# Sin comparar con somee (desastre real, somee caído)
.\herramientas\respaldo\ensayar-restauracion.ps1 -Carpeta D:\RespaldosSGV -SinSomee
```

- Importa con `sqlpackage /Action:Import` en una BD **nueva** `sgvRestaurada_aaaaMMdd_HHmm` del SQL Server **local** (por defecto `.` y, si no responde, `(localdb)\MSSQLLocalDB`; autenticación de Windows). Rechaza servidores que no sean de esta máquina (somee incluido), nombres que contengan `sgvActualizada`/`sgvTransporte` y BD que ya existan.
- Compara la BD restaurada con el propio `.bacpac` (`Origin.xml`: número de tablas, procedimientos, vistas, funciones y triggers y total de filas; `model.xml`: nombres de los objetos) y con somee (solo `SELECT`: filas por tabla y nombres). Si una tabla que cambia sola (`AuditoriaLog`, `Auditoria`, `SeguimientoExportacion`, `SeguimientoHito`, `PuntoControlGps`; parámetro `-TablasCambiantes`) tiene más filas en somee, se informa como "cambió después del respaldo".
- Comprueba los triggers de solo inserción de `FirmaDigital` y `AuditoriaLog`: intenta `UPDATE` y `DELETE` dentro de una transacción en la BD restaurada local, exige que fallen y hace `ROLLBACK`.
- Al final borra la BD restaurada (salvo `-Conservar`) y el login SQL que trae el `.bacpac` si la importación lo creó en el servidor local.
- Código de salida: `0` OK; `1` la importación falló, faltan objetos o tablas, el total de filas no coincide o un trigger no bloquea; `2` restauración fiel al `.bacpac` pero con diferencias frente a somee que hay que revisar. Añade una línea a `<carpeta del .bacpac>\ensayos.log`.

## Programar el respaldo diario

```powershell
.\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV -SoloListar   # muestra la tarea, no registra
.\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV               # registra (o reemplaza)
.\herramientas\respaldo\programar-respaldo.ps1 -Quitar
```

Registra "SGV - Respaldo diario" (23:00, `-Hora` para cambiarla): `respaldar-bd.ps1` y luego `respaldar-archivos.ps1` con la misma carpeta. Corre con el usuario actual **solo con la sesión iniciada** (puede estar bloqueada) para no guardar la contraseña de Windows; sin sesión (S4U) no podría descifrar la credencial FTP de DPAPI. Si se perdió una ejecución, corre apenas sea posible. La salida queda en `<Carpeta>\tarea-ultima-ejecucion.log`.

## Qué NO cubre todavía

- Copiar la carpeta de respaldos a otro disco o a la nube (se recomienda que `<Carpeta>` esté dentro de OneDrive/Google Drive).
