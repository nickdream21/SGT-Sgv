# Procedimiento de respaldo y restauración de SGV

Para quien opera el sistema. Los scripts están en `herramientas\respaldo\` y se corren en Windows PowerShell desde la carpeta del repositorio. El detalle técnico de cada uno está en `herramientas\respaldo\README.md`.

## 1. Qué se respalda y dónde

| Qué | Cómo | Dónde queda |
|---|---|---|
| Base de datos (todas las tablas, procedimientos, vistas, triggers y datos) | `respaldar-bd.ps1` exporta un `.bacpac` | `<Carpeta>\bd\sgvActualizada_aaaaMMdd_HHmm.bacpac` |
| Archivos subidos (CPIC, manifiestos, facturas, guías) | `respaldar-archivos.ps1` los copia por FTP | `<Carpeta>\archivos\Uploads\` |
| PDF firmados de las órdenes de viaje | el mismo script | `<Carpeta>\archivos\OrdenesViaje\` |

`<Carpeta>` es una carpeta **fuera del repositorio** (recomendado: una carpeta sincronizada con OneDrive o Google Drive en la PC de la oficina). Se puede fijar una vez con la variable de entorno `SGV_CARPETA_RESPALDO`.

Los respaldos contienen datos reales (conductores, montos, contraseñas cifradas): no los suba al repositorio ni los comparta.

La BD se guarda 30 días y, de antes, un respaldo por mes durante 12 meses. Los archivos nunca se borran del respaldo.

## 2. Cómo correr cada script

```powershell
# Respaldo de la BD (unos 40 segundos)
.\herramientas\respaldo\respaldar-bd.ps1 -Carpeta D:\RespaldosSGV

# Primera vez en una PC: guardar la contraseña FTP (queda cifrada para este usuario de Windows)
.\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial

# Copia de archivos (solo baja lo nuevo) + verificación de los PDF firmados
.\herramientas\respaldo\respaldar-archivos.ps1 -Carpeta D:\RespaldosSGV

# Ensayo de restauración (importa el último .bacpac en el SQL Server de esta PC, compara y lo borra)
.\herramientas\respaldo\ensayar-restauracion.ps1 -Carpeta D:\RespaldosSGV

# Programar el respaldo diario (ver primero qué se registraría)
.\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV -SoloListar
.\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV
.\herramientas\respaldo\programar-respaldo.ps1 -Quitar
```

Cada script termina con código 0 si todo salió bien. Cada ejecución deja una línea en `<Carpeta>\bd\respaldos.log`, `<Carpeta>\archivos\respaldos.log` o `<Carpeta>\bd\ensayos.log`.

Requisitos en la PC: `sqlpackage` (`dotnet tool install -g microsoft.sqlpackage`), `WebSGV\connectionStrings.config` (no está en git; se copia a mano) y, para el ensayo, un SQL Server local (Express o LocalDB).

## 3. Frecuencia recomendada

- **BD y archivos: todos los días a las 23:00** con la tarea programada "SGV - Respaldo diario". La tarea corre con su usuario de Windows y solo si la sesión está iniciada (puede estar bloqueada); si la PC estaba apagada, corre apenas se inicie sesión. Así no hace falta guardar la contraseña de Windows. La salida de la última ejecución queda en `<Carpeta>\tarea-ultima-ejecucion.log`.
- **Ensayo de restauración: una vez al mes** (lista del punto 6).
- Copie la carpeta de respaldos a otro disco o a la nube si no está en una carpeta sincronizada.

## 4. Restaurar en un desastre real

### Base de datos

1. Elija el `.bacpac` más reciente que esté sano: córralo antes por `ensayar-restauracion.ps1 -Bacpac <archivo> -SinSomee` (si somee no responde) y use uno anterior si falla.
2. Destino:
   - **Otro SQL Server** (propio o de otro proveedor): cree el servidor y corra
     `sqlpackage /Action:Import /SourceFile:<archivo.bacpac> /TargetServerName:<servidor> /TargetDatabaseName:<nombre> /TargetUser:<usuario> /TargetPassword:<clave> /TargetTrustServerCertificate:True`
   - **somee** (si la BD se perdió o se dañó): en el panel de somee cree una BD **vacía** y corra el mismo comando apuntando a ella (sqlpackage solo importa en una BD vacía). Si la BD dañada sigue existiendo, no la borre hasta tener la nueva funcionando.
3. El `.bacpac` trae el login SQL del usuario de somee; en un servidor nuevo se crea con una contraseña aleatoria. Asígnele una contraseña (`ALTER LOGIN [...] WITH PASSWORD = '...'`) o cree un usuario propio con rol `db_owner` en la BD.
4. Actualice la cadena `ConexionSGV` en `WebSGV\connectionStrings.config` (pruebas) o `connectionStrings.Production.config` (producción) con el servidor, la BD, el usuario y la contraseña nuevos, y cambie la cadena del servidor: con un cliente FTP, subir a la raíz del sitio (`/www.sgvtransporte.somee.com/`) ese archivo **con el nombre `connectionStrings.config`** (el `Web.config` lee `configSource="connectionStrings.config"`), desde una copia fuera del repo que se borra después. `publicar.ps1` nunca sube este archivo. El sitio se reinicia solo al cambiarlo (ver `docs/PASO_A_PRODUCCION.md`, paso 9).
5. Pruebe: inicio de sesión, una liquidación firmada y `Views/VerificarDocumento.aspx` con un código de verificación.

### Archivos

1. Por FTP (FileZilla u otro), suba el contenido de `<Carpeta>\archivos\Uploads\` a `/www.sgvtransporte.somee.com/Uploads` y el de `<Carpeta>\archivos\OrdenesViaje\` a `/www.sgvtransporte.somee.com/App_Data/OrdenesViaje`. Si es otro hosting, a las mismas carpetas dentro del sitio.
2. No suba los archivos `*__anterior_aaaaMMdd_HHmmss.*`: son versiones viejas que el respaldo guarda por si acaso.
3. Verifique los PDF firmados (punto 5).

## 5. Verificar los PDF firmados

```powershell
.\herramientas\respaldo\verificar-pdf-firmados.ps1 -Carpeta D:\RespaldosSGV
```

Compara el SHA-256 de cada PDF con el guardado en la BD (`FirmaDigital` y `OrdenViaje`). Resultado esperado: 0 faltantes y 0 distintos. Un PDF "distinto" no es válido como evidencia: busque si hay una versión `__anterior_` que coincida (el script lo indica).

## 6. Lista del ensayo mensual

- [ ] `<Carpeta>\bd\respaldos.log` y `<Carpeta>\archivos\respaldos.log` tienen una línea `OK` de cada día del último mes (o casi: la tarea no corre si la PC estuvo apagada varios días).
- [ ] Correr `ensayar-restauracion.ps1 -Carpeta <Carpeta>`: debe terminar en **RESULTADO: OK** (código 0). Un **AVISO** con "filas distintas" se revisa (suele ser alguien usando el sistema durante el ensayo); "cambió después del respaldo" es normal.
- [ ] Anotar la duración que muestra el resumen (queda en `ensayos.log`).
- [ ] Correr `verificar-pdf-firmados.ps1 -Carpeta <Carpeta>`: 0 faltantes y 0 distintos.
- [ ] Abrir dos archivos al azar de `<Carpeta>\archivos\Uploads\` y comprobar que se ven.
- [ ] Comprobar que la carpeta de respaldos está copiada fuera de la PC (OneDrive/Drive/disco externo).
- [ ] Si se rotó la contraseña FTP o de la BD: repetir `respaldar-archivos.ps1 -GuardarCredencial` o actualizar `connectionStrings.config`.
- [ ] Confirmar que en el SQL local no quedó ninguna BD `sgvRestaurada_*` (el ensayo la borra salvo con `-Conservar`).
