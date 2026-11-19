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

# 2b. Publicar y además borrar los sobrantes (pide escribir BORRAR).
.\herramientas\publicar\publicar.ps1 -BorrarSobrantes
```

Cada publicación queda en `<SGV_CARPETA_PUBLICACIONES>\`:

```
20261119_1504_3b2f8b0\
    sitio\              <- lo que se sube (lo mismo que hace Visual Studio al publicar)
    manifiesto.txt      <- commit, rama, cambios sin confirmar, fecha y SHA-256 de cada archivo
20261119_1504_3b2f8b0.zip
ultimo-publicado.txt    <- la última publicación subida con este script
publicaciones.log       <- una línea por subida
```

- **Cambios sin confirmar**: si hay cambios sin commit en `WebSGV/` (o `packages/`, `WebSGV.sln`), se niega; `-PermitirCambiosSinConfirmar` lo permite y lo anota en el manifiesto. Lo normal es publicar siempre un commit.
- **Comprobaciones del paquete** (si alguna falla, no compara ni sube): sin `App_Data`, `Uploads`, `Database`, archivos de secretos ni `*.pdb`; con `bin\WebSGV.dll`, los nativos de QuestPDF (`bin\runtimes\win-x86\native` y `win-x64`) y `Web.config` de Release (sin `debug="true"`, con la regla `ForzarHTTPS`). Se pueden correr solas sobre cualquier carpeta: `publicar.ps1 -ComprobarPaquete <carpeta>`.
- **Qué se sube**: lo que no está en el servidor o tiene otro tamaño, y lo de mismo tamaño cuyo SHA-256 no coincide con el de la última publicación hecha con este script (o si no hay registro, o el marcador del servidor no coincide: ver abajo). `-SubirTodo` sube todo.
- **Sin credencial** hace todo lo local, avisa `FALTA LA CREDENCIAL FTP` y no toca el servidor.

### Cómo sube (y qué pasa si falla)

Pasos, en este orden (`-SoloListar` los muestra en la línea "Pasos de la subida"):

1. **MarcadorEnCurso**: sobrescribe `publicacion.txt` con `EN CURSO <paquete> <fecha>` antes de tocar nada. Si algo falla después, el marcador queda así y la próxima publicación no confía en lo publicado (sube todo lo de mismo tamaño).
2. **Mantenimiento**: solo si hay algo de `bin\` o `Web.config` que subir, pone `app_offline.htm` en la raíz **antes del contenido** (así nunca se sirven páginas nuevas con la DLL vieja); mientras esté, IIS muestra "Actualizando el sistema, vuelve en unos minutos" en vez del sitio. Si solo cambia contenido, no se pone.
3. **Contenido** (`.aspx`, CSS, JS, imágenes...), 4. **Bin** (`bin\`), 5. **WebConfig** (`Web.config`).
6. **QuitarMantenimiento**: borra `app_offline.htm` (también si quedó de una subida fallida anterior).
7. Sobrantes (solo con `-BorrarSobrantes` y BORRAR) y, si todo salió bien, el **marcador** `publicacion.txt` definitivo.

Al primer error de un paso no se pasa al siguiente (el paso en curso sí se termina): si falla el contenido, no se toca `bin\` ni `Web.config`. **Si `app_offline.htm` pudo quedar puesto (incluso si falló su propia subida), la salida lo dice en rojo: el sitio sigue EN MANTENIMIENTO** hasta repetir la publicación o usar `-VolverA` con la versión anterior; al terminar bien se quita solo. Para quitarlo a mano, borrar `app_offline.htm` de la raíz por FTP.

### Marcador `publicacion.txt` y varias máquinas

Al terminar bien, el script sube a la raíz del sitio `publicacion.txt` con el nombre del paquete y el commit. Antes de planificar lo lee del servidor (solo lectura) y solo confía en los SHA-256 de la última publicación de **esta** máquina (`ultimo-publicado.txt`) si se cumple todo:

- el marcador existe, no está `EN CURSO` y coincide en paquete y commit con `ultimo-publicado.txt`;
- la fecha (MDTM) de `bin/WebSGV.dll` en el servidor no es más nueva que la de `publicacion.txt` (si alguien subió una DLL después del marcador, se publicó sin este script). Si MDTM falla, no se confía.

Si no se confía (publicó la otra PC, la publicación anterior falló, se nota una DLL subida desde Visual Studio, o es la primera vez), todo lo de mismo tamaño se sube (como `-SubirTodo`) y la salida dice el motivo.

**Una publicación desde Visual Studio NO se detecta si no cambió `bin/WebSGV.dll`** (VS no toca el marcador): después de publicar desde Visual Studio, use `-SubirTodo`.

Ejemplo: la laptop publica `A` (marcador = `A`); luego la oficina publica `B` (marcador = `B`). Cuando la laptop vuelva a publicar, su `ultimo-publicado.txt` dice `A` y el servidor `B`: sube todo lo de mismo tamaño, sin saltarse nada que la oficina haya cambiado. `app_offline.htm` y `publicacion.txt` nunca cuentan como sobrantes.

## Volver atrás

```powershell
.\herramientas\publicar\publicar.ps1 -VolverA D:\PublicacionesSGV\20261118_1830_cce61fd.zip -SoloListar   # ver qué cambia
.\herramientas\publicar\publicar.ps1 -VolverA D:\PublicacionesSGV\20261118_1830_cce61fd.zip              # subirlo
```

- Acepta la carpeta o el `.zip` de una publicación. Comprueba que los archivos coinciden con su manifiesto y sube el paquete **completo**.
- Si el paquete es **anterior al commit `773dad0`** (contraseñas en formato `sha256.`), o no se puede saber (p. ej. el commit no existe en este clon), se niega salvo `-Forzar`: con ese código nadie podría iniciar sesión.
- Si el paquete **no tiene `manifiesto.txt`**, no se puede comprobar su integridad ni su commit: exige `-SinManifiesto` (y, como el commit es desconocido, también `-Forzar`). Cada opción acepta un riesgo distinto; ninguna cubre a la otra.
- Volver atrás no deshace migraciones ni SP de la BD: si la versión nueva cambió la BD, revisar antes que la vieja funcione con ella.
- Las páginas que solo existen en la versión nueva quedan como sobrantes; se borran con `-BorrarSobrantes`.

## Lo que no se toca nunca

- `App_Data\` (PDF firmados), `Uploads\` (documentos) y los archivos de secretos (`connectionStrings*.config`, `appSettings.Secrets.config`): no van en el paquete, no se listan, no se suben y no se borran, aunque falten en el paquete. Esto lo fijan `probar-publicar.ps1` y el candado `ExcludeFromPackageFolders` de `WebSGV.csproj`.
- Los **secretos del servidor**: el paquete no los lleva (a diferencia de publicar desde Visual Studio, que subía los de la PC). Si cambia un secreto (p. ej. una clave nueva en `appSettings.Secrets.config`), subir ese archivo a mano por FTP, a sabiendas de qué BD/claves lleva.
- Nunca se borran carpetas (las vacías quedan) ni nada que no sea `.aspx/.ascx/.ashx/.asmx/.master/.cs/.js/.css/.dll/.config` o esté en `Database/`.

## Visual Studio

`WebSGV\Properties\PublishProfiles\Paquete.pubxml` publica a carpeta con los mismos candados (los `*.pubxml` están en `.gitignore`, así que existe solo en la máquina donde se creó). Una vez que se publique con `publicar.ps1`, **no publicar también desde Visual Studio** (`FTPProfile1`): no usa `app_offline.htm`, sube los secretos de la PC y no actualiza el marcador `publicacion.txt`. El script solo lo nota si VS cambió `bin/WebSGV.dll` (fecha MDTM más nueva que el marcador); si no, **NO se detecta**: después de publicar desde Visual Studio use `-SubirTodo`.

## Pruebas

```powershell
.\herramientas\publicar\probar-publicar.ps1                                  # sin red
.\herramientas\publicar\probar-publicar.ps1 -Paquete <publicación>\sitio     # además con un paquete real
.\herramientas\respaldo\probar-espejo.ps1                                    # el módulo FTP común
```
