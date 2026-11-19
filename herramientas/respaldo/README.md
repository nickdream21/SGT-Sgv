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
- Se escribe primero `*.parcial.bacpac` y se renombra solo si `sqlpackage` termina bien. Si falla: código de salida distinto de 0, se borra el parcial, se anota `ERROR` en el log y **no** se aplica la retención.
- Se rechaza cualquier carpeta dentro del repo (el `.bacpac` contiene datos reales y contraseñas cifradas de usuarios).

## Retención

Conserva los **30 respaldos más recientes** y, de los anteriores, **el primero de cada mes** de los últimos **12 meses**. Borra el resto. Solo considera archivos con el patrón `<nombreBD>_aaaaMMdd_HHmm.bacpac`; cualquier otro archivo de la carpeta no se toca.

## Requisitos

- `sqlpackage` en el PATH: `dotnet tool install -g microsoft.sqlpackage` (probado con 170.5.96).
- `WebSGV\connectionStrings.config` (o el de producción) en la máquina; están gitignored.
- Windows PowerShell 5.1 o superior.

## Qué NO cubre todavía

- **Archivos** (`WebSGV\Uploads`, PDFs firmados de `App_Data\OrdenesViaje`): tarea E1-2.
- **Restauración** y prueba de que el `.bacpac` se puede importar: tarea E1-3.
- Programarlo (Programador de tareas) y copiar la carpeta a otro disco o a la nube.
