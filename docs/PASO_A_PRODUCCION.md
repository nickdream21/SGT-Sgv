# Paso a producción (`sgvTransporte`)

Procedimiento único, en orden, para que el sitio publicado deje la BD de pruebas `sgvActualizada` y pase a la de producción `sgvTransporte`. **Se hace solo cuando el usuario lo decida**; hasta entonces no se toca `sgvTransporte`.

Situación hoy (2026-11-21): `https://sgvtransporte.somee.com` usa `sgvActualizada`, que tiene **datos reales** desde el 2026-10-19 (no borrar ni limpiar nada allí sin confirmar). `sgvTransporte` (plan Premium de somee: 2000 MB de datos frente a los 500 MB de `sgvActualizada`) existe pero está atrasada en migraciones, SP y datos. Lo de cada publicación (orden migración → SP → DLL, secretos, permisos, comprobaciones) está en [`herramientas/publicar/README.md`](../herramientas/publicar/README.md), "Antes y después de publicar".

Idea del procedimiento: el sitio sigue atendiendo con `sgvActualizada` mientras se pone al día `sgvTransporte` (pasos 2 a 7). Solo al final se cambia la cadena de conexión del servidor (paso 9); las cargas del paso 8 que pasan por el sitio se hacen justo después. Así no importa el orden SP/DLL de cada cambio: cuando el sitio llega a `sgvTransporte`, ya tiene todas las migraciones y SP del commit publicado.

Todos los comandos se corren desde la raíz del repo, en una PC con `WebSGV/connectionStrings.Production.config` (gitignored; apunta a `sgvTransporte`; ver [`docs/PREPARAR_MAQUINA.md`](PREPARAR_MAQUINA.md)). Ninguno imprime la contraseña. Con `-Entorno produccion` todos exigen `-ConfirmoProduccion`, salvo con `-SoloListar` (no escribe) en `aplicar-migraciones.ps1`, `aplicar-procedimientos.ps1`, `reparar-tildes-bd.ps1` y `migrar-contrasenas-texto-plano.ps1`; `sql.ps1` y `respaldar-bd.ps1` lo exigen siempre.

## 0. Decisión previa (usuario)

**Qué datos lleva `sgvTransporte`.** Este procedimiento pone al día la `sgvTransporte` que ya existe y vuelve a cargar en ella lo que solo se cargó en pruebas (paso 8). Pero todo lo que se registró en el sitio desde el 2026-10-19 (despachos, liquidaciones, firmas, documentos) está **solo en `sgvActualizada`**, y los archivos del servidor (`Uploads/`, PDF firmados de `App_Data/OrdenesViaje`, con su SHA-256 en `FirmaDigital`) apuntan a filas de esa BD. Decidir antes:

- **A. Poner al día la `sgvTransporte` existente** (lo que sigue). Los datos de `sgvActualizada` no pasan: los PDF firmados y los documentos del servidor quedan sin su fila (no se pueden verificar en `Views/VerificarDocumento.aspx`).
- **B. Llevar a `sgvTransporte` una copia de `sgvActualizada`** (restaurar un `.bacpac` en una `sgvTransporte` vacía, como en [`docs/PROCEDIMIENTO_RESPALDO.md`](PROCEDIMIENTO_RESPALDO.md), sección 4). Entonces sobran los pasos 3 a 7.3 y el 8 (la copia ya trae migraciones, SP, contraseñas y datos); el paso 7.4 (desactivar usuarios de roles retirados o de prueba con contraseña conocida) **sí** aplica, y también los pasos 9 a 11. Con B, **congelar el sitio** desde que empieza la exportación del `.bacpac` hasta el cambio de cadena del paso 9, para que nada se escriba en `sgvActualizada` que no llegue a la copia: poner `app_offline.htm` en la raíz del sitio por FTP (como hace `publicar.ps1`) y pausar "SGV GPS automático" en cron-job.org (UptimeRobot avisará mientras tanto). Al terminar el paso 9, borrar `app_offline.htm` y reactivar la tarea de cron-job.org (*Test run*: 202).

`docs/migracion_somee/` (2026-05) planteaba una tercera vía (BD nueva con solo datos maestros); está **superada**: sus scripts son una foto del esquema de mayo (antes de las migraciones 16–51, con tablas y SP de Grifo y Maquinaria ya retirados). Si se quiere empezar con solo datos maestros, rehacer el análisis con el esquema actual; `02_clasificacion_tablas.md` sirve solo como referencia de qué tablas son maestras.

## 1. Antes de empezar

1. Que nadie esté usando el sitio (avisar) y que no haya una publicación a medias.
2. Guardar la cadena actual del servidor: bajar por FTP `connectionStrings.config` del sitio a una carpeta fuera del repo (sin copiar la contraseña a ningún lado). Esa copia es la vuelta atrás del paso 9. Que el sitio usa hoy `sgvActualizada` ya está comprobado (2026-11-21: los LOGIN desde IP públicas del 20 y 21/11 están en su `AuditoriaLog`); solo falta guardar la copia, y de paso confirmar el `Initial Catalog`.
3. Comprobar que la PC tiene `connectionStrings.Production.config` y que apunta a `sgvTransporte`:

   ```powershell
   .\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion "SELECT DB_NAME() AS bd"
   ```

## 2. Respaldo previo

```powershell
.\herramientas\respaldo\respaldar-bd.ps1 -Entorno produccion -ConfirmoProduccion     # sgvTransporte tal como está
.\herramientas\respaldo\respaldar-bd.ps1                                             # sgvActualizada (la del sitio)
.\herramientas\respaldo\respaldar-archivos.ps1                                       # Uploads y PDF firmados
```

(`-Carpeta` o la variable `SGV_CARPETA_RESPALDO`; detalle en `docs/PROCEDIMIENTO_RESPALDO.md`.) No seguir si alguno no termina con código 0.

## 3. Controles antes de las migraciones (solo SELECT)

Correr cada uno con `.\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion "<consulta>"` (o `-Archivo`). Las migraciones 48 a 51 se detienen solas (THROW 51000) si encuentran duplicados o desbordes, pero es mejor saberlo antes y decidir cómo depurar.

| Antes de | Consulta | Qué se espera / qué hacer |
|---|---|---|
| 30 (plantas Milagro y Jave, tabla `DestinoDescarga`) | `SELECT idPlanta, nombre, esInternacional, activo FROM dbo.Planta WHERE UPPER(LTRIM(RTRIM(nombre))) IN ('MILAGRO','JAVE')` | Ninguna fila (la 30 las crea) o filas que ya sean las del Ecuador (`esInternacional = 1`). Si existe una con ese nombre que es otra cosa, corregirla antes: la 30 no la crea de nuevo. |
| 47 (auditoría de solo inserción) y `Seguridad.ConvertirHashAlIngresar=true` de Release | `SELECT name FROM sys.triggers WHERE parent_id = OBJECT_ID('dbo.Usuarios'); SELECT CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Usuarios' AND COLUMN_NAME = 'contrasena'` | Triggers: ninguno, o revisar que no choquen con `OUTPUT ... INTO`. Largo de `contrasena`: 84 o más (en pruebas, 100); si es menor, los hashes nuevos no caben. |
| 48 (una fila financiera por orden) | `SELECT estadoViaje, estadoAprobacion, COUNT(*) FROM dbo.OrdenViaje GROUP BY estadoViaje, estadoAprobacion` | Si hay órdenes `COMPLETADO` con `estadoAprobacion` distinto de `APROBADO` (o NULL, o un valor antiguo como `COMPLETADO`/`RECHAZADO`), desaparecerían de Aprobadas, Contabilidad, Reportes y Saldos: corregirlas antes. Duplicados (la 48 falla si hay): `SELECT numeroOrdenViaje, COUNT(*) FROM dbo.Ingresos WHERE numeroOrdenViaje IS NOT NULL GROUP BY numeroOrdenViaje HAVING COUNT(*) > 1`, y lo mismo con `Egresos` y `DescuentosReintegros`. |
| 49 (CPIC y factura únicas) | `SELECT LTRIM(RTRIM(numeroCPIC)), COUNT(*) FROM dbo.CPIC GROUP BY LTRIM(RTRIM(numeroCPIC)) HAVING COUNT(*) > 1` y `SELECT LTRIM(RTRIM(numeroFactura)), idCliente, COUNT(*) FROM dbo.Factura GROUP BY LTRIM(RTRIM(numeroFactura)), idCliente HAVING COUNT(*) > 1` | Ninguna fila. Si hay, depurar a mano (en pruebas, las facturas anuladas se renombraron con `-ANULADA`). |
| 50 (placa de semirremolque única) | `SELECT REPLACE(REPLACE(UPPER(placaCarreta),'-',''),' ','') AS clave, COUNT(*) FROM dbo.Carreta WHERE placaCarreta IS NOT NULL GROUP BY REPLACE(REPLACE(UPPER(placaCarreta),'-',''),' ','') HAVING COUNT(*) > 1` y lo mismo con `placaTracto` en `dbo.Tracto` | Ninguna fila (`ABC-123` = `ABC123`). Si hay, unificar los vehículos repetidos antes. |
| 51 (montos FLOAT → DECIMAL(12,2)) | La consulta de abajo | Por columna: filas, mínimo, máximo, cuántos valores tienen más de 2 decimales y en cuántas órdenes `POR_CONFORMAR`. Si alguna orden `POR_CONFORMAR` tiene valores con más de 2 decimales, la administración vuelve a firmar su revisión después de migrar. |

Consulta de control de la 51 (solo lectura; si ya no hay columnas FLOAT/REAL, lo dice):

```sql
DECLARE @sql NVARCHAR(MAX) = N'';
SELECT @sql += CASE WHEN @sql = N'' THEN N'' ELSE N' UNION ALL ' END +
    N'SELECT ''' + t.name + N''' AS tabla, ''' + c.name + N''' AS columna, COUNT(' + QUOTENAME(c.name) + N') AS filas, ' +
    N'MIN(' + QUOTENAME(c.name) + N') AS minimo, MAX(' + QUOTENAME(c.name) + N') AS maximo, ' +
    N'SUM(CASE WHEN CAST(' + QUOTENAME(c.name) + N' AS DECIMAL(19,6)) <> ROUND(CAST(' + QUOTENAME(c.name) + N' AS DECIMAL(19,6)), 2) THEN 1 ELSE 0 END) AS conMasDe2Decimales, ' +
    N'COUNT(DISTINCT CASE WHEN CAST(' + QUOTENAME(c.name) + N' AS DECIMAL(19,6)) <> ROUND(CAST(' + QUOTENAME(c.name) + N' AS DECIMAL(19,6)), 2) ' +
    N'AND o.estadoAprobacion = ''POR_CONFORMAR'' THEN x.numeroOrdenViaje END) AS ordenesPorConformar ' +
    N'FROM dbo.' + QUOTENAME(t.name) + N' x LEFT JOIN dbo.OrdenViaje o ON o.numeroOrdenViaje = x.numeroOrdenViaje'
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.name IN ('Ingresos', 'Egresos', 'IngresosAdicionales', 'CategoriasAdicionales')
  AND ty.name IN ('float', 'real');
IF @sql = N'' SELECT 'No hay columnas FLOAT/REAL: la 51 ya está aplicada' AS resultado; ELSE EXEC sp_executesql @sql;
```

**Seguimiento mal importado** (antes de volver a importar el STATUS GENERAL): en pruebas se borraron los 215 registros que dejó la importación del 14/09/2026 con `Database/Scripts/limpiar_seguimiento_importacion_2026-09-14.sql` (borra solo si son exactamente 215). Ver si `sgvTransporte` los tiene:

```sql
SELECT COUNT(*) FROM dbo.SeguimientoExportacion
WHERE idDespachoOrigen IS NULL AND idDespachoDestino IS NULL AND estado = 'COMPLETADO'
  AND fechaRegistro >= '2026-09-14T13:19:00' AND fechaRegistro < '2026-09-14T13:20:00'
```

215 → correr ese script con `-Entorno produccion -ConfirmoProduccion`; 0 → nada que hacer; otro número → revisar a mano (el script no borrará).

## 4. Migraciones

```powershell
.\WebSGV\Database\aplicar-migraciones.ps1 -Entorno produccion -SoloListar          # revisar la lista de pendientes
.\WebSGV\Database\aplicar-migraciones.ps1 -Entorno produccion -ConfirmoProduccion
```

Aplica en orden todas las pendientes de `Database/Schema/` (hoy 01–51) y las registra en `dbo.SchemaVersion`. Si una se detiene, corregir el dato que dice el mensaje y repetir (son idempotentes). Algunas que conviene conocer: la **26 borra `Despachos.idProducto`**; la 16 pone la hora de Perú (`dbo.fn_AhoraPeru()`); la 46 agrega `Usuarios.selloSesion` (al pasar, todas las sesiones se cierran una vez); la 47 protege `AuditoriaLog` y `Auditoria` contra UPDATE/DELETE.

## 5. Procedimientos almacenados

En la misma sesión que el paso 4 (la 51 y sus 5 SP van juntos):

```powershell
.\WebSGV\Database\aplicar-procedimientos.ps1 -Entorno produccion -SoloListar       # FALTA / DISTINTO
.\WebSGV\Database\aplicar-procedimientos.ps1 -Entorno produccion -ConfirmoProduccion
```

Revisar cada DISTINTO antes de aplicar: el repo es la fuente de verdad, pero si `sgvTransporte` tuviera un SP más nuevo que el repo, primero se actualiza el archivo del repo (y se prueba en pruebas). Después, comprobar la 51:

```sql
SELECT t.name AS tabla, c.name AS columna FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.name IN ('Ingresos','Egresos','IngresosAdicionales','CategoriasAdicionales') AND ty.name IN ('float','real');
SELECT OBJECT_NAME(p.object_id) AS sp, p.name AS parametro FROM sys.parameters p
JOIN sys.types ty ON ty.user_type_id = p.user_type_id
WHERE ty.name IN ('float','real') AND OBJECT_NAME(p.object_id) IN
  ('sp_DC_InsertarIngresos','sp_DC_InsertarEgresos','sp_DC_InsertarGastoAdicional','sp_DC_InsertarIngresoAdicional','sp_LD_RegistrarIngresosEntregados');
```

Las dos deben devolver 0 filas.

## 6. SP retirados

Ninguno de los dos los llama el código publicado.

1. `WebSGV/Database/Scripts/borrar-sp-se-sin-uso.sql` (8 `DROP PROCEDURE IF EXISTS sp_SE_*`). Antes, confirmar que nada de la BD los llama (los jobs del Agente SQL de somee no se pueden revisar):

   ```sql
   SELECT OBJECT_NAME(object_id) AS objeto FROM sys.sql_modules WHERE definition LIKE '%sp[_]SE[_]%'
   ```

   Solo deben salir los propios `sp_SE_*` que siguen en el repo y los 8 que se borran (y comentarios). En `sgvActualizada` los 8 siguen existiendo al 2026-11-21: correrlo también allí (primero allí).
2. `WebSGV/Database/Scripts/borrar_sp_AprobarLiquidacion.sql` (`DROP PROCEDURE IF EXISTS sp_AprobarLiquidacion`). En `sgvActualizada` ya no existe y su archivo ya no está en `StoredProcedures/`.

```powershell
.\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo WebSGV\Database\Scripts\borrar-sp-se-sin-uso.sql
.\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo WebSGV\Database\Scripts\borrar_sp_AprobarLiquidacion.sql
```

## 7. Tildes, contraseñas y usuarios

1. Tildes corruptas en objetos de la BD que no están en el repo:

   ```powershell
   .\WebSGV\Database\Scripts\reparar-tildes-bd.ps1 -Entorno produccion -SoloListar
   .\WebSGV\Database\Scripts\reparar-tildes-bd.ps1 -Entorno produccion -ConfirmoProduccion
   ```

2. **Crítico — contraseñas en texto plano a PBKDF2** (compilar antes la solución): sin esto, los usuarios con contraseña en texto plano (en pruebas eran 66 conductores) no pueden iniciar sesión.

   ```powershell
   .\WebSGV\Database\Scripts\migrar-contrasenas-texto-plano.ps1 -Entorno produccion -SoloListar
   .\WebSGV\Database\Scripts\migrar-contrasenas-texto-plano.ps1 -Entorno produccion -ConfirmoProduccion
   ```

3. Cuenta de administrador de sistema: `privado/cambiar_password_nickdream_PROD.sql` (fuera de git; su paso 1 lista la cuenta, porque el nombre exacto en producción no se pudo confirmar). Correrlo en SSMS o con `sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo`.
4. Usuarios que no deben quedar activos en producción: los de roles retirados (`admin.maquinaria`, `operador`, `admin.grifo`: al entrar verían "Tu sesión ha expirado o no es válida") y los de prueba con contraseña conocida (`privado/CREDENCIALES_USUARIOS.txt`). Desactivarlos o cambiarles la contraseña desde Gestión de usuarios, según decida el usuario.

## 8. Datos cargados solo en pruebas (solo opción A)

Lo que se cargó con scripts o herramientas únicamente en `sgvActualizada` y hay que repetir contra `sgvTransporte`. El punto 2 puede ir antes del paso 9; los puntos 1 y 3 usan el sitio, así que van justo después del paso 9 (antes de abrirlo a los usuarios). **No** correr `Database/Scripts/retirar_liquidaciones_prueba_2026-11-16.sql`: era solo para `sgvActualizada` (ids fijos).

1. **Choferes:** en el sitio (ya sobre `sgvTransporte`, paso 9), `RegistroChoferes` → "Actualizar con la lista de choferes actuales" con `docs/DOCUMENTACION/LISTA DE CHOFERES ACTUALES (corregida).xlsx` (fuera de git; usa `sp_RegistrarConductorImportado`, del paso 5). Deja inactivos a los que ya no trabajan. Después, `Database/Scripts/registrar_choferes_listas_2026-10-24.sql` (registra como inactivos los de las listas de 2025 y aplica el estado ACTIVO/BAJA de la lista de octubre; se puede repetir) y los scripts sueltos de registro de choferes que haya en `privado/`. Revisar antes qué hace cada uno: sus nombres y DNI no se copian a ningún documento.
2. **Cargas de terceros con CPIC, MCI y facturas** (`herramientas/carga-historica-correo`, herramienta `TercerosConDocumentos`, con los archivos de tareas `tareas_terceros_2026-11-06.txt` y `tareas_polinplast_2026-11-06.txt` de la PC de la oficina; ver su README). **Ojo:** `Ejecutar.ps1` hoy solo usa `WebSGV\connectionStrings.config` (pruebas): para correrlo contra producción hay que prepararlo antes (no cambiar la cadena de pruebas a mano). Las GRE de Polinplast que se subieron por la bandeja: volver a subirlas desde `docs/DOCUMENTACION/GRE SUNAT NOV 2026`. Lo mismo con las demás cargas históricas del correo (`CompletarConCorreo`, `AdjuntarPdfFacturas`), si se quieren en producción.
3. **Seguimiento recalculado:** solo si `sgvTransporte` tiene esos viajes de octubre de 2026. Con el sitio ya sobre `sgvTransporte`:

   ```powershell
   .\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo WebSGV\Database\Scripts\recalcular_ingreso_salida_ecuador_2026-10-23.sql
   .\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo WebSGV\Database\Scripts\recalcular_afrecho_descarga_en_peru_2026-10-27.sql
   .\herramientas\sitio-local\actualizar-gps.ps1 -Sitio https://sgvtransporte.somee.com -Ids <los idSeguimiento que devolvió cada script>
   ```

   (o "Actualizar GPS" en el detalle de cada viaje). Cada script devuelve los viajes que tocó. `actualizar-gps.ps1` entra con `admin.transporte` y su contraseña de `privado/CREDENCIALES_USUARIOS.txt`: en producción, `-Usuario` con una cuenta que exista allí y esté en ese archivo.

## 9. Cambiar la cadena del servidor

1. Con un cliente FTP, subir a la raíz del sitio (`/www.sgvtransporte.somee.com/`) el contenido de `connectionStrings.Production.config` **con el nombre `connectionStrings.config`** (el `Web.config` lee `configSource="connectionStrings.config"`). Hacerlo desde una copia fuera del repo y borrarla después. `publicar.ps1` nunca sube este archivo.
2. El sitio se reinicia solo al cambiar ese archivo (todas las sesiones se cierran).
3. Vuelta atrás: subir otra vez la copia guardada en el paso 1.2.

## 10. Comprobar

1. Las comprobaciones de "Después" de `herramientas/publicar/README.md` (Login 200, `Salud.ashx` y `?gps=1` `{"ok":true}`, log, monitores, cron).
2. A mano, iniciar sesión con cada rol y abrir "Ver historial" en RegistroDespacho. Los tests de integración **no** se pueden correr contra producción (solo aceptan `sgvActualizada`).
3. `.\WebSGV\Database\aplicar-procedimientos.ps1 -Entorno produccion -SoloListar` y `aplicar-migraciones.ps1 -Entorno produccion -SoloListar`: nada pendiente.

## 11. Después del cambio

- **Respaldo diario:** `programar-respaldo.ps1` registra la tarea con `respaldar-bd.ps1` sin `-Entorno`, es decir, de `sgvActualizada`. Hay que adaptarlo para que respalde `sgvTransporte` antes de dar el paso por cerrado (y `verificar-pdf-firmados.ps1`, que también usa pruebas por defecto).
- Cada publicación siguiente aplica migraciones y SP a **las dos** BD (primero pruebas, luego producción), con el orden de `herramientas/publicar/README.md`.
- Actualizar los documentos que dicen que el sitio usa `sgvActualizada`: este, `CLAUDE.md`, `docs/OPERACION.md`, `docs/PROCEDIMIENTO_RESPALDO.md`, `herramientas/publicar/README.md` y `herramientas/respaldo/README.md`.
