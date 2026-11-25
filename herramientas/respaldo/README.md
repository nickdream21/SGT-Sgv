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

Registra "SGV - Respaldo diario" (23:00, `-Hora` para cambiarla): `respaldar-bd.ps1`, `sellar-registros.ps1` (E4-11) y `respaldar-archivos.ps1` con la misma carpeta. Corre con el usuario actual **solo con la sesión iniciada** (puede estar bloqueada) para no guardar la contraseña de Windows; sin sesión (S4U) no podría descifrar la credencial FTP de DPAPI. Si se perdió una ejecución, corre apenas sea posible. La salida queda en `<Carpeta>\tarea-ultima-ejecucion.log`.

## Ensayar migraciones y procedimientos antes de la BD viva (E3-1)

```powershell
# Último .bacpac de <Carpeta>\bd\ → sgvEnsayo_aaaaMMdd_HHmm local → migraciones → SP y humo → [FactBD] → borra
.\herramientas\respaldo\ensayar-migracion.ps1 -Carpeta 'G:\Unidades compartidas\Respaldos_Sistemas_SGV'

# Con un respaldo recién exportado (respaldar-bd.ps1, solo lee pruebas) y consultas de control
.\herramientas\respaldo\ensayar-migracion.ps1 -RespaldoNuevo -ArchivoControl .\control.sql

# Conservar la BD de ensayo para revisarla o romperla a mano (bórrela después), sin pruebas
.\herramientas\respaldo\ensayar-migracion.ps1 -Conservar -SinPruebas
.\WebSGV\Database\sql.ps1 "SELECT COUNT(*) FROM dbo.SchemaVersion" -Entorno ensayo -BaseDeDatos sgvEnsayo_20261121_1300

# Anonimizarla para capturas o videos (aborta si no es sgvEnsayo_*; ver herramientas/manual/README.md)
.\WebSGV\Database\sql.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_20261121_1300 -Archivo herramientas\respaldo\anonimizar-ensayo.sql
```

`anonimizar-ensayo.sql` reescribe nombres, documentos, contactos, usuarios guardados como texto, trazos de firma y textos libres, y vacía la auditoría (la lista completa está en su cabecera; cada columna de la lista se toca solo si existe). `Usuarios.nombreUsuario` pasa a `usuario<id>` salvo las cuentas de prueba de la tabla `@UsuariosDePrueba` al inicio del script (las de `privado/CREDENCIALES_USUARIOS.txt` menos el conductor, que entra con un documento): es una lista explícita, no "lo que no sea solo dígitos", para que un usuario con nombre de persona tampoco quede. Si se crea una cuenta de prueba nueva, agréguela ahí. El SELECT final debe dar 0 en todo salvo `conductores`, `usuarios` y `usuariosDePrueba`.

- Toma el `.bacpac` más reciente de `<Carpeta>\bd\` (o `-Bacpac`; `-RespaldoNuevo` exporta uno antes) y muestra su fecha y antigüedad. `-Carpeta` por defecto es `$env:SGV_CARPETA_RESPALDO`.
- Lo importa con `sqlpackage` en una BD **nueva** `sgvEnsayo_aaaaMMdd_HHmm` (`-NombreBd`, siempre `sgvEnsayo_*`) de un SQL Server **local** (`.` y, si no responde, `(localdb)\MSSQLLocalDB`; `-Servidor`), con la autenticación de Windows. Rechaza servidores remotos y BD que ya existan.
- Corre `aplicar-migraciones.ps1` y `aplicar-procedimientos.ps1` con `-Entorno ensayo -BaseDeDatos <ensayo>`, cada uno en su proceso (su salida queda en el log). El segundo incluye la **ejecución de humo** (M-052): compila cada SP del repo con `sys.dm_exec_describe_first_result_set_for_object`; se revisan todas las filas de error de cada SP, y una fuera de los 4 permitidos (`$ErroresHumoPermitidos`: SQL dinámico y tablas temporales, cada SP con su número; 11529/11501 solo acompañando a ese número) hace fallar el ensayo. Como esa vista solo informa el **primer** error, cada SP pasa además por `sys.dm_sql_referenced_entities`, que falla (2020, 207) si referencia una tabla o columna que no existe: así un objeto que falte más adelante en uno de los 4 permitidos no queda tapado (probado). Límite: una columna que falte en una sentencia que usa una tabla temporal (`sp_SE_SincronizarDespachos`) no se detecta; eso lo cubren las `[FactBD]` del ensayo.
- Corre las pruebas de integración (`WebSGV.Tests.Integracion`, con `vstest`) contra el ensayo poniendo `SGV_TEST_CONNSTR`; `BaseDePruebas` acepta `sgvEnsayo_*` solo si el servidor es local. Compile antes (`msbuild WebSGV.sln /p:Configuration=Debug`) o use `-SinPruebas`.
- `-ArchivoControl`: un `.sql` con consultas `SELECT` de control que se ejecuta al final con `sql.ps1 -Entorno ensayo`; su resultado sale en el log.
- Al final borra la BD de ensayo (tiene datos reales) y el login SQL que crea la importación (el `.bacpac` trae el usuario de somee y `sqlpackage` crea su login con una contraseña aleatoria). Solo borra un login si cumple **todo**: figura como `SqlLogin` en el `model.xml` del `.bacpac`, es de tipo `S` (login SQL, nunca uno de Windows), no existía en el servidor antes del ensayo y su SID es el de un usuario de la BD de ensayo (se consulta antes del `DROP DATABASE`). Con `-Conservar` no borra nada e imprime los comandos para la BD y para esos logins.
- **Log**: la salida completa en `<Carpeta>\ensayos\ensayo-migracion_aaaaMMdd_HHmm.log` y una línea por ensayo en `<Carpeta>\ensayos\ensayos.log`. Va junto a los respaldos (fuera del repo, ya tiene datos reales y está en la unidad compartida) en vez de `C:\PublicacionesSGV`, que es de las publicaciones; no contiene claves (la conexión local no usa contraseña y nunca se lee `connectionStrings.config`).
- Código de salida: `0` todo en verde; `1` falló la importación, una migración, un SP, el humo, una prueba, el control o el borrado.
- **Obligatorio** antes de aplicar migraciones o SP a `sgvActualizada` (la BD del sitio; `herramientas/publicar/README.md`, "Antes") o a producción (`docs/PASO_A_PRODUCCION.md`, pasos 4 y 5): el humo de `aplicar-procedimientos.ps1` sobre la BD viva avisa, pero después de aplicar. Requisitos de la PC: `docs/PREPARAR_MAQUINA.md`, sección 5.1.

## Sello de firmas y auditoría (E4-11)

```powershell
.\herramientas\respaldo\sellar-registros.ps1 -Carpeta D:\RespaldosSGV                     # pruebas (sgvActualizada)
.\herramientas\respaldo\sellar-registros.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_20261122_1000 -Carpeta C:\temp\sellos
.\herramientas\respaldo\sellar-registros.ps1 -Carpeta D:\RespaldosSGV -AceptarCambios -Motivo 'por qué es legítimo'
.\herramientas\respaldo\probar-sellar.ps1                                                  # funciones puras, sin BD
```

- Mientras la app sea `db_owner`, los triggers de solo inserción de `FirmaDigital`, `AuditoriaLog` y `Auditoria` se pueden desactivar. El sello deja la prueba **fuera de la BD**: SHA-256 de cada fila (todas las columnas, descubiertas en `sys.columns` y ordenadas por nombre; fechas ISO con 7 decimales, números invariantes, binarios en hex, NULL distinto de `''`) y el estado y el SHA-256 de la definición de sus triggers. Solo `SELECT`, fila por fila.
- Queda en `<Carpeta>\sellos\sello-<bd>_aaaaMMdd_HHmm.txt.gz` (texto con tabuladores comprimido; ~256 KB hoy), con una línea por ejecución en `<Carpeta>\sellos\sellos.log`. Carpeta hermana de `bd\` y no dentro: no tiene datos de las filas (solo ids y hashes), así que se puede copiar aparte, y no se mezcla con los `.bacpac` que buscan los ensayos. Cada sello lleva el nombre y el hash global del sello base (cadena) y su propio hash global en la última línea.
- Compara con el sello **base**: el más reciente con estado OK. **FALLA** (código 1) si una fila que existía cambió o falta, si falta una columna, si un trigger falta, está desactivado o cambió su definición, o si una tabla no tiene un trigger activo para `UPDATE` y otro para `DELETE`. Filas y columnas nuevas son normales (con una columna nueva, las filas viejas se comparan con las columnas del base). Una FALLA sigue saliendo cada día hasta que alguien la revise y la acepte con `-AceptarCambios -Motivo`. Código 2: no se pudo sellar.
- Aviso: no hay correo en las herramientas. `-ScriptAviso` (o `$env:SGV_SCRIPT_AVISO`) es un `.ps1` que recibe `-Asunto` y `-Cuerpo`; sin él, el aviso es la salida de la tarea y `sellos.log`.
- Retención: la de los `.bacpac` (30 + el primero de cada mes de 12), sin borrar nunca el sello base.
- La tarea diaria lo corre después de `respaldar-bd.ps1` (también si la exportación falló); su FALLA no afecta al respaldo. Resultado de la tarea: 11 = FALLA del sello, 12 = no se pudo sellar. **Tras actualizar el repo hay que volver a registrar la tarea** (`programar-respaldo.ps1 -Carpeta ...`).

## Qué NO cubre todavía

- Copiar la carpeta de respaldos a otro disco o a la nube (se recomienda que `<Carpeta>` esté dentro de OneDrive/Google Drive).
