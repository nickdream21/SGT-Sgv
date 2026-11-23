# Publicar SGV

Herramientas para subir el sitio a somee (`ftp://sgvtransporte.somee.com`, carpeta `/www.sgvtransporte.somee.com`).

| Script | Qué hace |
|---|---|
| `publicar.ps1` | Compila en Release, guarda un **paquete por versión** (carpeta + `.zip` + manifiesto), lo comprueba, lo compara con el servidor y lo sube. También vuelve a una versión anterior (`-VolverA`) y lista/borra los archivos que sobran en el servidor. |
| `probar-publicar.ps1` | Pruebas sin red de la lógica de `publicar.ps1` (qué subir, qué borrar, comprobaciones del paquete, manifiesto, vuelta atrás). |
| `subir-uploads.ps1` | Sube a mano los documentos de `WebSGV\Uploads` (no los publica el paquete). |

El FTP común (listado, credencial) está en `herramientas\comun\Ftp.psm1`, compartido con `herramientas\respaldo\`.

## Antes de la primera vez (una vez por máquina)

1. Credencial FTP (la misma del respaldo; se guarda cifrada para su usuario de Windows en `privado\ftp-respaldo.xml`):

   ```powershell
   .\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial
   ```

2. Carpeta donde se guardan las publicaciones, **fuera del repo** (el script se niega si está dentro):

   ```powershell
   [Environment]::SetEnvironmentVariable('SGV_CARPETA_PUBLICACIONES', 'D:\PublicacionesSGV', 'User')
   ```

3. `nuget restore WebSGV.sln` si los paquetes no están bajados. Hace falta Visual Studio (MSBuild se busca con `vswhere`).

## Publicar

```powershell
# 1. Ensayo: compila, guarda el paquete y muestra qué subiría y qué sobra. No escribe en el servidor.
.\herramientas\publicar\publicar.ps1 -SoloListar

# 2. Publicar de verdad (sube lo nuevo o distinto, por pasos: ver "Cómo sube" más abajo).
.\herramientas\publicar\publicar.ps1

# 2b. Publicar y además borrar los sobrantes dentro de la ventana de mantenimiento (pide escribir BORRAR al empezar).
#     La primera publicación con este script debe ser así (ver "Cómo sube").
.\herramientas\publicar\publicar.ps1 -BorrarSobrantes
```

Cada publicación queda en `<SGV_CARPETA_PUBLICACIONES>\`:

```
20261119_1504_3b2f8b0\
    sitio\              <- lo que se sube
    manifiesto.txt      <- commit, rama, cambios sin confirmar, fecha y SHA-256 de cada archivo
20261119_1504_3b2f8b0.zip
ultimo-publicado.txt    <- la última publicación subida con este script
publicaciones.log       <- una línea por subida
```

- **Cambios sin confirmar**: si hay cambios sin commit en `WebSGV/` (o `packages/`, `WebSGV.sln`), se niega; `-PermitirCambiosSinConfirmar` lo permite y lo anota en el manifiesto. Lo normal es publicar siempre un commit.
- **Solo lo necesario**: al armar el paquete se quitan los binarios nativos de QuestPDF para linux y osx (`bin\runtimes\linux-*`, `osx-*`; el servidor es Windows y usa `win-x86`/`win-x64`). Se quitan aquí y no en el csproj para no tocar el `QuestPDF.targets`. Documentos (`*.md`) y plantillas (`*.example`) no se publican: en el csproj van como `<None>` o fuera de `WebSGV/` (p. ej. los de `docs/`).
- **Comprobaciones del paquete** (si alguna falla, no compara ni sube): sin `App_Data`, `Uploads`, `Database`, archivos de secretos, `*.pdb`, `*.md`, `*.example` ni `bin\runtimes\linux*`/`osx*`; con `bin\WebSGV.dll`, los nativos de QuestPDF (`bin\runtimes\win-x86\native` y `win-x64`) y `Web.config` de Release (sin `debug="true"`, con la regla `ForzarHTTPS`). Se pueden correr solas sobre cualquier carpeta: `publicar.ps1 -ComprobarPaquete <carpeta>`.
- **Qué se sube**: lo que no está en el servidor o tiene otro tamaño, y lo de mismo tamaño cuyo SHA-256 no coincide con el de la última publicación hecha con este script (o si no hay registro, o el marcador del servidor no coincide: ver abajo). `-SubirTodo` sube todo.
- **Sin credencial** hace todo lo local, avisa `FALTA LA CREDENCIAL FTP` y no toca el servidor.

### Cómo sube (y qué pasa si falla)

Pasos, en este orden (`-SoloListar` los muestra en la línea "Pasos de la subida", p. ej. `MarcadorEnCurso -> Mantenimiento -> Contenido (181) -> Bin (72) -> WebConfig (1) -> Sobrantes (261) -> QuitarMantenimiento -> Marcador`):

1. **MarcadorEnCurso**: sobrescribe `publicacion.txt` con `EN CURSO <paquete> <fecha>` antes de tocar nada. Si algo falla después, el marcador queda así y la próxima publicación no confía en lo publicado (sube todo lo de mismo tamaño).
2. **Mantenimiento**: pone `app_offline.htm` en la raíz **antes del contenido** (así nunca se sirven páginas nuevas con la DLL vieja) si hay algo de `bin\` o `Web.config` que subir, **o** sobrantes que borrar en `bin\`, `.config` o páginas (`.aspx`, `.ascx`, `.master`, `.ashx`, `.asmx`, `.asax`, `.cs`). Mientras esté, IIS muestra "Actualizando el sistema, vuelve en unos minutos" en vez del sitio. Si solo cambia contenido (y los sobrantes, si los hay, son CSS/JS/SQL), no se pone.
3. **Contenido** (`.aspx`, CSS, JS, imágenes...), 4. **Bin** (`bin\`), 5. **WebConfig** (`Web.config`).
6. **Sobrantes** (solo con `-BorrarSobrantes` y BORRAR): se borran **dentro de la ventana de mantenimiento**, antes de quitarla: primero las páginas, los `.config` de subcarpetas y los estáticos, después las DLL administradas de `bin\` (para no dejar un `web.config` apuntando a una DLL ya borrada) y al final de todo las **DLL nativas sueltas** (lista fija: `bin/libgcc_s_dw2-1.dll`, `libstdc++-6.dll`, `libwinpthread-1.dll`, `qpdf.dll`, `QuestPdfSkia.dll`, `zlib1.dll`, y todo `bin/runtimes/**`). Así el sitio nunca atiende con páginas viejas cuyo code-behind ya no está en la DLL nueva (darían 500 y correos de `AvisoErrores`).
7. **QuitarMantenimiento**: borra `app_offline.htm` (también si quedó de una subida fallida anterior).
8. Si todo salió bien, el **marcador** `publicacion.txt` definitivo.

BORRAR se pide **antes de escribir nada** en el servidor (no se espera con el sitio en mantenimiento); si no se escribe BORRAR, **se cancela** la publicación sin escribir nada. Con `-SoloListar -BorrarSobrantes` solo se muestra el paso `Sobrantes (N)`: no se pide nada ni se borra. Cada `DELE` vuelve a comprobar que la ruta no es protegida (`App_Data`, `Uploads`, secretos) y que es un sobrante válido.

**Sin `-BorrarSobrantes`**, si en el servidor hay sobrantes de aplicación (páginas, `.config` o DLL administradas de `bin\`), la publicación **se cancela antes de escribir nada** y lo dice en rojo: esas páginas darían 500 con la DLL nueva. Repetir con `-BorrarSobrantes` o, a sabiendas, con `-PermitirSobrantesDeAplicacion`. Con `-SoloListar` solo avisa de que la publicación real se cancelaría. Los estáticos (CSS, JS, SQL) y las DLL nativas sueltas no bloquean. Vale también para `-VolverA`: las páginas que solo existen en la versión más nueva darían 500 con la DLL vieja.

Al primer error de un paso no se pasa al siguiente (el paso en curso sí se termina, salvo **Sobrantes**, que se detiene en el primer borrado que falle: si no se pudo borrar una página o un `web.config`, no se borran las DLL que podría necesitar). Excepción: si falla el borrado de una **DLL nativa suelta** (el proceso de IIS puede tenerla cargada aunque `app_offline.htm` descargue la aplicación), es solo un aviso amarillo: se sigue, se quita el mantenimiento y al final se listan las que quedaron. Si falla el contenido, no se toca `bin\` ni `Web.config`; si falla un borrado, el mantenimiento no se quita. **Si `app_offline.htm` pudo quedar puesto (incluso si falló su propia subida), la salida lo dice en rojo: el sitio sigue EN MANTENIMIENTO** hasta repetir la publicación o usar `-VolverA` con la versión anterior; al terminar bien se quita solo. Para quitarlo a mano, borrar `app_offline.htm` de la raíz por FTP.

> **Primera publicación con este script**: usar `-BorrarSobrantes` (y escribir BORRAR); sin él se cancela. El servidor tiene páginas retiradas (`Views/WebForm2.aspx`, `Views/TestGenerarPdfOrdenViaje.aspx`, Dashboard, Grifo...), DLL de MVC/Razor/FriendlyUrls/EPPlus/itextsharp en `bin\` y un `Views/web.config` de Razor que apunta a ellas: sin borrarlas, esas páginas darían 500 con la DLL nueva. Antes, mirar la lista "(b) Sobrantes" de un `-SoloListar -BorrarSobrantes`.
>
> **Reciclar el grupo de aplicaciones en el panel de somee justo antes** de esa publicación: así el proceso de IIS suelta las DLL nativas antiguas (`bin/QuestPdfSkia.dll`, `qpdf.dll`, `zlib1.dll`...) y se pueden borrar. Si aun así alguna queda bloqueada, la salida la lista en amarillo (el sitio queda publicado y atendiendo): reciclar otra vez y repetir con `-BorrarSobrantes`.

### Marcador `publicacion.txt` y varias máquinas

Al terminar bien, el script sube a la raíz del sitio `publicacion.txt` con el nombre del paquete y el commit. Antes de planificar lo lee del servidor (solo lectura) y solo confía en los SHA-256 de la última publicación de **esta** máquina (`ultimo-publicado.txt`) si se cumple todo:

- el marcador existe, no está `EN CURSO` y coincide en paquete y commit con `ultimo-publicado.txt`;
- la fecha (MDTM) de `bin/WebSGV.dll` en el servidor no es más nueva que la de `publicacion.txt` (si alguien subió una DLL después del marcador, se publicó sin este script). Si MDTM falla, no se confía.

Si no se confía (publicó la otra PC, la publicación anterior falló, se nota una DLL subida desde Visual Studio, o es la primera vez), todo lo de mismo tamaño se sube (como `-SubirTodo`) y la salida dice el motivo.

**Una subida por fuera de este script NO se detecta si no cambió `bin/WebSGV.dll`** (no toca el marcador): en ese caso, la siguiente publicación con `-SubirTodo`.

Ejemplo: la laptop publica `A` (marcador = `A`); luego la oficina publica `B` (marcador = `B`). Cuando la laptop vuelva a publicar, su `ultimo-publicado.txt` dice `A` y el servidor `B`: sube todo lo de mismo tamaño, sin saltarse nada que la oficina haya cambiado. `app_offline.htm` y `publicacion.txt` nunca cuentan como sobrantes.

## Antes y después de publicar

Lo de **cada** publicación. Cómo arma, comprueba y sube el paquete está arriba ("Publicar", "Cómo sube"); aquí solo lo que el script no hace. El sitio publicado usa la BD de pruebas **`sgvActualizada`** (la de producción `sgvTransporte` no se usa todavía; pasar a ella: [`docs/PASO_A_PRODUCCION.md`](../../docs/PASO_A_PRODUCCION.md)).

### Antes

1. **Orden: migración → SP → DLL.** Si el commit trae migraciones (`WebSGV/Database/Schema/`) o SP (`StoredProcedures/`), aplicarlos a `sgvActualizada` **antes** de `publicar.ps1`:

   ```powershell
   .\WebSGV\Database\aplicar-migraciones.ps1   -Entorno pruebas -SoloListar   # luego sin -SoloListar
   .\WebSGV\Database\aplicar-procedimientos.ps1 -Entorno pruebas -SoloListar  # FALTA / DISTINTO; luego sin -SoloListar
   ```

   **Antes, obligatorio:** `.\herramientas\respaldo\ensayar-migracion.ps1` (copia local del `.bacpac` más reciente con las migraciones, los SP, el humo y los `[FactBD]`; ver [`herramientas/respaldo/README.md`](../respaldo/README.md)) con `RESULTADO: OK`. El humo de `aplicar-procedimientos.ps1` sobre la BD viva avisa, pero después de aplicar.

   Como esa BD es la del sitio, **cada SP que se aplica tiene que funcionar con la DLL que está publicada** (parámetros nuevos opcionales, columnas nuevas al final y leídas por nombre) y la DLL que se va a publicar tiene que funcionar con los SP que haya en la BD. Si un cambio no admite las dos versiones a la vez (p. ej. el C# lee una columna que el SP viejo no devuelve), aplicar el SP y publicar seguidos, en ese orden. Al volver atrás (`-VolverA`) la BD no se deshace: comprobar antes que la versión vieja funcione con ella (ver "Volver atrás").
2. **Secretos del servidor.** El paquete no lleva `appSettings.Secrets.config` (ver "Lo que no se toca nunca"); el del servidor debe tener estas claves (solo nombres; la plantilla con un comentario por clave es [`WebSGV/appSettings.Secrets.config.example`](../../WebSGV/appSettings.Secrets.config.example)):
   - `OnwayAuth0ClientId`, `OnwayAuth0ClientSecret`, `OnwayUsername`, `OnwayPassword` (GPS);
   - `GpsAutomaticoClave` (igual al encabezado `X-SGV-Clave` de cron-job.org; si no coincide, 403 y cron-job.org desactiva la tarea);
   - `ApisPeruToken` (búsqueda de DNI/RUC);
   - `SmtpHost`, `SmtpPort`, `SmtpSSL`, `SmtpUser`, `SmtpPass`, `SmtpFrom` (correos y aviso de errores);
   - `Seguridad.ClaveSegundoFactor` (desde E4-14): **la misma** en el servidor y en todas las PCs que usan la misma BD, copiada (nunca generar otra si alguien ya se enroló: sus secretos no se descifrarían). Sin ella, `Mi seguridad` dice "no configurado" y quien tenga el factor activo no puede entrar.

   El interruptor `Seguridad.ExigirSegundoFactor` (en `Web.config`, no es secreto) se publica en `false` hasta que el usuario decida encenderlo (U-092). El paquete lleva `bin\QRCoder.dll` (NuGet QRCoder 1.8.1, sin binarios nativos).

   **Nunca** `AvisoErroresLocal` (es solo para probar el aviso en una PC de desarrollo; ver `docs/OPERACION.md`, sección 4). Además, el `appSettings.Secrets.config` **local** de la PC que publica tiene que tener `GpsAutomaticoClave`: el target `VerificarSecretosAlPublicar` del csproj corta la compilación del paquete si falta (el archivo local no se sube).

   **Cambiar una clave sin publicar:** con un cliente FTP (FileZilla) bajar `/www.sgvtransporte.somee.com/appSettings.Secrets.config` a una carpeta fuera del repo, agregar o cambiar la clave con un editor (sin copiar los valores al chat ni a ningún documento), subirlo al mismo lugar y borrar la copia local. El sitio puede seguir con los valores viejos en memoria hasta reiniciarse: reciclar el grupo de aplicaciones en el panel de somee. Si se cambió `GpsAutomaticoClave`, cambiarla también en cron-job.org (`herramientas/gps-automatico/README.md`).
3. **Permisos de escritura en el servidor.** La cuenta del grupo de aplicaciones de somee debe poder crear, escribir y borrar en estas carpetas (las crea el sitio si faltan; el paquete no las lleva):

   | Carpeta | Qué escribe |
   |---|---|
   | `App_Data/OrdenesViaje` | PDF firmados de las liquidaciones (`OrdenViaje.RutaArchivo`) |
   | `App_Data/RutasGps` | Caché del mapa de la ruta del seguimiento |
   | `App_Data/HistorialGps` | Caché del historial GPS (se borra a los 30 días) |
   | `App_Data/logs` | Log del sitio `sgv-AAAAMMDD.log` (y `_001`, `_002`… si el día pasa de 20 MB) |
   | `Uploads/Factura`, `Uploads/CPIC`, `Uploads/Manifiesto`, `Uploads/Guias`, `Uploads/FacturasFlete`, `Uploads/MovimientosViaje` | Documentos subidos (por año/mes) |
   | `Uploads/Factura/_temp`, `Uploads/CPIC/_temp`, `Uploads/Manifiesto/_temp`, `Uploads/_temp/Bandeja`, `Uploads/_temp/ReporteGps` | Temporales; `Global.asax` borra al arrancar los de más de 24 h |

   Solo hay que revisarlo en un servidor nuevo o si aparece un error de acceso denegado en el log; en somee ya funciona.

### Después

1. `https://sgvtransporte.somee.com/Views/Login.aspx` responde 200 (y no la página de mantenimiento: si sigue, ver "Cómo sube").
2. `https://sgvtransporte.somee.com/Salud.ashx` y `.../Salud.ashx?gps=1` responden `{"ok":true}` (pueden tardar hasta 2 min en reflejar un cambio; detalle en `docs/OPERACION.md`, sección 1).
3. Iniciar sesión en el sitio y abrir las páginas que tocó el cambio (usuarios por rol: `privado/CREDENCIALES_USUARIOS.txt`, solo el que haga falta).
4. Si se borraron sobrantes, cada página retirada responde 404 (p. ej. `Views/WebForm2.aspx`). Cabeceras de seguridad: `curl -I https://sgvtransporte.somee.com/Views/Login.aspx`.
5. Log del servidor: bajar por FTP `App_Data/logs/sgv-AAAAMMDD*.log` del día (todos los `_NNN`) y buscar líneas de nivel Error desde la hora de la publicación; también si llegó algún correo de `AvisoErrores`.
6. **Medir gzip tras publicar** (E5-7). Solo GET, sin sesión:

   ```bash
   curl -s -o /dev/null -D - -H "Accept-Encoding: gzip" https://sgvtransporte.somee.com/Views/Login.aspx | grep -i -E "content-encoding|vary"
   curl -s -o /dev/null -D - -H "Accept-Encoding: gzip" https://sgvtransporte.somee.com/Content/lib/bootstrap-4.6.2/bootstrap.min.css | grep -i -E "content-encoding|cache-control"
   ```

   (`curl -I` también sirve, pero algunos IIS no comprimen las respuestas a HEAD: ante la duda, vale el GET.) IIS comprime un archivo estático recién desde su segunda petición en pocos segundos: repetir la línea del CSS dos o tres veces antes de concluir. Los dos deben traer `Content-Encoding: gzip`, y el CSS además `Cache-Control: max-age=31536000`. Si el **HTML** llega sin `Content-Encoding` (el servidor no tiene el módulo de compresión dinámica), encender el respaldo propio: `<add key="Compresion.Respuesta" value="true" />` en `WebSGV/Web.config` del repo (no solo en el servidor: la siguiente publicación lo pisaría), commit, publicar (no hace falta otro cambio de código) y repetir la medición (Login debe traer `Content-Encoding: gzip` y `Vary: Accept-Encoding`). Si es el **CSS** el que llega sin gzip, el servidor no comprime estáticos: anotarlo, el respaldo no lo cubre. Seguimiento: P-053 en `docs/PRUEBAS_PENDIENTES.md`.
7. Los dos monitores de UptimeRobot ("SGV - Sitio y BD" y "SGV - GPS y Onway") en verde, y en cron-job.org la tarea "SGV GPS automático" activa con la última corrida 200/202 (`docs/OPERACION.md`, secciones 2 y 3).

## Volver atrás

```powershell
.\herramientas\publicar\publicar.ps1 -VolverA D:\PublicacionesSGV\20261118_1830_cce61fd.zip -SoloListar   # ver qué cambia
.\herramientas\publicar\publicar.ps1 -VolverA D:\PublicacionesSGV\20261118_1830_cce61fd.zip              # subirlo
```

- Acepta la carpeta o el `.zip` de una publicación. Comprueba que los archivos coinciden con su manifiesto y sube el paquete **completo**.
- Si el paquete es **anterior al commit `773dad0`** (contraseñas en formato `sha256.`), o no se puede saber (p. ej. el commit no existe en este clon), se niega salvo `-Forzar`: con ese código nadie podría iniciar sesión.
- Si el paquete **no tiene `manifiesto.txt`**, no se puede comprobar su integridad ni su commit: exige `-SinManifiesto` (y, como el commit es desconocido, también `-Forzar`). Cada opción acepta un riesgo distinto; ninguna cubre a la otra.
- Si el paquete es anterior a E1-9 (trae `bin\runtimes\linux*`/`osx*`, `*.md` o `*.example`), se quitan de una copia temporal (la publicación guardada no se toca) antes de comprobarlo y subirlo, y la salida dice cuántos.
- Volver atrás no deshace migraciones ni SP de la BD: si la versión nueva cambió la BD, revisar antes que la vieja funcione con ella.
- Las páginas que solo existen en la versión nueva quedan como sobrantes y darían 500 con la DLL vieja: sin `-BorrarSobrantes` la vuelta atrás se cancela antes de escribir (o, a sabiendas, `-PermitirSobrantesDeAplicacion`).

## Lo que no se toca nunca

- `App_Data\` (PDF firmados; también `App_Data/Abastecimientos`, los PDF del módulo Grifo ya retirado, que se conservan en el servidor), `Uploads\` (documentos) y los archivos de secretos (`connectionStrings*.config`, `appSettings.Secrets.config`): no van en el paquete, no se listan, no se suben y no se borran, aunque falten en el paquete. Esto lo fijan `probar-publicar.ps1` y el candado `ExcludeFromPackageFolders` de `WebSGV.csproj`.
- Los **secretos del servidor**: el paquete no los lleva (a diferencia de publicar desde Visual Studio, que subía los de la PC). Si cambia un secreto (p. ej. una clave nueva en `appSettings.Secrets.config`), ver "Cambiar una clave sin publicar" en "Antes y después de publicar".
- Nunca se borran carpetas (las vacías quedan) ni nada que no sea `.aspx/.ascx/.ashx/.asmx/.master/.cs/.js/.css/.dll/.config`, esté en `Database/` o sea un nativo de linux/osx (`bin/runtimes/linux*`, `osx*`: `.so`, `.dylib`, que ya no se publican).

## Visual Studio

No se publica desde Visual Studio: los perfiles se retiraron en E1-9 (guardados en `privado/perfiles-publicacion-retirados/`); si alguien publicó desde VS, la siguiente publicación va con `-SubirTodo`.

## Pruebas

```powershell
.\herramientas\publicar\probar-publicar.ps1                                  # sin red
.\herramientas\publicar\probar-publicar.ps1 -Paquete <publicación>\sitio     # además con un paquete real
.\herramientas\respaldo\probar-espejo.ps1                                    # el módulo FTP común
```
