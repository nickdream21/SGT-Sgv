# Paso a producción (`sgvTransporte`)

Procedimiento único, en orden, para que el sitio publicado deje la BD de pruebas `sgvActualizada` y pase a la de producción `sgvTransporte`. **Se hace solo cuando el usuario lo decida**; hasta entonces no se toca `sgvTransporte`.

Situación hoy (2026-11-21): `https://sgvtransporte.somee.com` usa `sgvActualizada`, que tiene **datos reales** desde el 2026-10-19 (no borrar ni limpiar nada allí sin confirmar). `sgvTransporte` (plan Premium de somee: 2000 MB de datos frente a los 500 MB de `sgvActualizada`) existe pero está atrasada en migraciones, SP y datos. Lo de cada publicación (orden migración → SP → DLL, secretos, permisos, comprobaciones) está en [`herramientas/publicar/README.md`](../herramientas/publicar/README.md), "Antes y después de publicar".

Idea del procedimiento: el sitio sigue atendiendo con `sgvActualizada` mientras se pone al día `sgvTransporte` (pasos 2 a 7). Solo al final se cambia la cadena de conexión del servidor (paso 9); las cargas del paso 8 que pasan por el sitio se hacen justo después. Así no importa el orden SP/DLL de cada cambio: cuando el sitio llega a `sgvTransporte`, ya tiene todas las migraciones y SP del commit publicado.

Todos los comandos se corren desde la raíz del repo, en una PC con `WebSGV/connectionStrings.Production.config` (gitignored; apunta a `sgvTransporte`; ver [`docs/PREPARAR_MAQUINA.md`](PREPARAR_MAQUINA.md)). Ninguno imprime la contraseña. Con `-Entorno produccion` todos exigen `-ConfirmoProduccion`, salvo con `-SoloListar` (no escribe) en `aplicar-migraciones.ps1`, `aplicar-procedimientos.ps1`, `reparar-tildes-bd.ps1` y `migrar-contrasenas-texto-plano.ps1`; `sql.ps1` y `respaldar-bd.ps1` lo exigen siempre.

## 0. Decisión previa (usuario)

**Qué datos lleva `sgvTransporte`.** Este procedimiento pone al día la `sgvTransporte` que ya existe y vuelve a cargar en ella lo que solo se cargó en pruebas (paso 8). Pero todo lo que se registró en el sitio desde el 2026-10-19 (despachos, liquidaciones, firmas, documentos) está **solo en `sgvActualizada`**, y los archivos del servidor (`Uploads/`, PDF firmados de `App_Data/OrdenesViaje`, con su SHA-256 en `FirmaDigital`) apuntan a filas de esa BD. Decidir antes:

- **A. Poner al día la `sgvTransporte` existente** (lo que sigue). Los datos de `sgvActualizada` no pasan: los PDF firmados y los documentos del servidor quedan sin su fila (no se pueden verificar en `Views/VerificarDocumento.aspx`).
- **B. Llevar a `sgvTransporte` una copia de `sgvActualizada`** (restaurar un `.bacpac` en una `sgvTransporte` vacía, como en [`docs/PROCEDIMIENTO_RESPALDO.md`](PROCEDIMIENTO_RESPALDO.md), sección 4). Entonces sobran los pasos 3 a 7.3 y el 8 (la copia ya trae migraciones, SP, contraseñas y datos); el paso 7.4 (desactivar usuarios de roles retirados o de prueba con contraseña conocida) **sí** aplica, y también los pasos 9 a 11. Con B, **congelar el sitio** desde que empieza la exportación del `.bacpac` hasta el cambio de cadena del paso 9, para que nada se escriba en `sgvActualizada` que no llegue a la copia: poner `app_offline.htm` en la raíz del sitio por FTP (como hace `publicar.ps1`) y pausar "SGV GPS automático" en cron-job.org (UptimeRobot avisará mientras tanto). Al terminar el paso 9, borrar `app_offline.htm` y reactivar la tarea de cron-job.org (*Test run*: 202).

`docs/archivo/migracion_somee/` (2026-05, archivado) planteaba una tercera vía (BD nueva con solo datos maestros); está **superada**: sus scripts son una foto del esquema de mayo (antes de las migraciones 16–51, con tablas y SP de Grifo y Maquinaria ya retirados). Si se quiere empezar con solo datos maestros, rehacer el análisis con el esquema actual; [`docs/archivo/migracion_somee/02_clasificacion_tablas.md`](archivo/migracion_somee/02_clasificacion_tablas.md) sirve solo como referencia de qué tablas son maestras.

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

**Datos de E3-5 (antes de las migraciones 54 y 55, cuyos CHECK y FK fallarían):** en pruebas se corrigieron el 2026-11-21 con `WebSGV/Database/Scripts/corregir_datos_etapa3_2026-11-21.sql`, que solo corre en `sgvActualizada` o en un ensayo y espera los conteos de pruebas. En `sgvTransporte` medir los mismos controles: `SELECT COUNT(*) FROM Despachos WHERE activo = 0 AND ISNULL(estadoDespacho, '') <> 'ANULADO'`, `SELECT COUNT(*) FROM SeguimientoExportacion WHERE estado = 'COMPLETADO'`, `SELECT COUNT(*) FROM SeguimientoExportacion s WHERE idUsuarioModificacion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion)` y los usuarios de roles retirados, activos e inactivos (`SELECT idUsuario, rol, activo FROM Usuarios WHERE rol IN ('ADMINISTRADOR DE GRIFO', 'ADMINISTRADOR DE MAQUINARIA', 'OPERADOR')`): el CHECK de rol de la migración 54 acepta los inactivos con rol retirado, pero los activos hay que desactivarlos antes (como U-015 en pruebas). Lo que no dé 0 (o salga activo) se anota como `D-`/`U-` nuevo y se corrige con un script propio para producción, con la aprobación del usuario.

## 4. Migraciones

```powershell
.\WebSGV\Database\aplicar-migraciones.ps1 -Entorno produccion -SoloListar          # revisar la lista de pendientes
.\WebSGV\Database\aplicar-migraciones.ps1 -Entorno produccion -ConfirmoProduccion
```

Aplica en orden todas las pendientes de `Database/Schema/` (hoy 01–60) y las registra en `dbo.SchemaVersion`. Si una se detiene, corregir el dato que dice el mensaje y repetir (son idempotentes). Algunas que conviene conocer: la **26 borra `Despachos.idProducto`**; la 16 pone la hora de Perú (`dbo.fn_AhoraPeru()`); la 46 agrega `Usuarios.selloSesion` (al pasar, todas las sesiones se cierran una vez); la 47 protege `AuditoriaLog` y `Auditoria` contra UPDATE/DELETE.

**Migración 52** (`52_TriggersAuditoriaHeredados.sql`, E3-1): crea los 11 triggers `TR_<tabla>_Auditoria` heredados (llenan `Auditoria`; `sp_DC_GenerarNumeroOrden` la lee) solo si faltan; donde ya existen no hace nada y solo queda registrada. Ensayada antes con `herramientas/respaldo/ensayar-migracion.ps1`. Control después (solo SELECT): `SELECT name, is_disabled FROM sys.triggers WHERE name LIKE 'TR[_]%[_]Auditoria'` → los 11 de la migración con `is_disabled = 0` (10 después de la 58, que borra `CPIC_Productos` con su trigger), y 9 después de la 60, que borra `GuiasTransportista` con el suyo; `TR_AbastecimientoCombustible_Auditoria` (sin archivo) también se va con su tabla en la 60. Si `aplicar-migraciones.ps1 -SoloListar` avisa de un hash distinto en `16_HoraPeru.sql`, comparar `dbo.fn_AhoraPeru` con el archivo antes de aceptarlo en `$HashesAceptados` (el de pruebas ya está aceptado, revisado el 2026-11-21).

**Migración 53** (`53_CorrelativoViaje.sql`, E3-3): crea `Correlativo` y la siembra con el número `VP-AAAA-NNN` más alto de cada año de `sgvTransporte` (excluye formatos como `VP-SIM-*`); se detiene con `THROW 51000` si un año queda por debajo. **Debe ir antes que los SP del paso 5** (`sp_CrearDespacho`, `sp_CrearViajeProgreso` y `sp_LD_ReubicarDespachoEnViajeDelConductor` llaman a `sp_SiguienteNumeroViaje`, que usa la tabla). Control después: `SELECT serie, anio, ultimo FROM Correlativo` contra el número más alto de cada año (`MAX(TRY_CAST(SUBSTRING(numeroViajeProgreso, 9, 20) AS INT))` agrupado por `SUBSTRING(numeroViajeProgreso, 4, 4)`, solo filas `LIKE 'VP-[0-9][0-9][0-9][0-9]-%'`): `ultimo` nunca menor.

**Migraciones 54 y 55** (`54_RestriccionesDominio.sql`, `55_LlavesEIndices.sql`, E3-6): CHECK de dominio y montos, `OrdenViaje.estadoAprobacion` NOT NULL con DEFAULT `PENDIENTE` (fuera el `APROBADO`), 10 FK nuevas, documentos sin cascada, FK e índices repetidos fuera. Cada regla cuenta antes y se detiene (THROW 54001/54002 o 55001) con la tabla, la regla y el conteo; no corrigen datos. **Orden: 01–53 → `sp_SE_ImportarLote` → 54 → 55, automático:** `aplicar-migraciones.ps1` aplica ese SP justo antes de la 54 (mapa `$SpRequeridosPorMigracion` del script; `-SoloListar` lo muestra como "antes aplica el SP sp_SE_ImportarLote.sql"; ensayado el 2026-11-21 desde un `.bacpac` anterior a la 54). El resto de los SP va en el paso 5. No importar el STATUS GENERAL durante el paso: la pantalla ImportarSeguimiento de la versión publicada `60853e8` manda COMPLETADO y PENDIENTE, que la 54 rechaza; ese SP los normaliza (COMPLETADO → FINALIZADO, lo demás → EN_CURSO). **Antes**, en `sgvTransporte`, correr los controles de datos de E3-5 (paso 3) y estos (solo SELECT; todos deben dar 0 filas o conteo 0):

```sql
SELECT estadoDespacho, tipoOperacion, activo, COUNT(*) FROM Despachos
 WHERE estadoDespacho NOT IN ('PROGRAMADO','COMPLETADO','ANULADO') OR tipoOperacion NOT IN ('CARGA','DESCARGA')
    OR (activo = 0 AND estadoDespacho <> 'ANULADO') OR (activo = 1 AND estadoDespacho = 'ANULADO')
 GROUP BY estadoDespacho, tipoOperacion, activo;
SELECT estadoViaje, COUNT(*) FROM ViajesEnProgreso WHERE estadoViaje NOT IN ('ABIERTO','CERRADO','ANULADO') GROUP BY estadoViaje;
SELECT estadoAprobacion, estadoViaje, COUNT(*) FROM OrdenViaje
 WHERE ISNULL(estadoAprobacion, '') NOT IN ('PENDIENTE','POR_CONFORMAR','REABIERTO','APROBADO')
    OR ISNULL(estadoViaje, '') NOT IN ('PENDIENTE','COMPLETADO')
 GROUP BY estadoAprobacion, estadoViaje;
SELECT estado, origen, COUNT(*) FROM SeguimientoExportacion
 WHERE estado NOT IN ('EN_CURSO','FINALIZADO','CANCELADO') OR origen NOT IN ('DESPACHO','MANUAL') GROUP BY estado, origen;
SELECT etapa, estadoFirma, COUNT(*) FROM FirmaDigital
 WHERE etapa NOT IN ('DECLARACION','REVISION','CONFORMIDAD','ANULACION') OR estadoFirma NOT IN ('V','A') GROUP BY etapa, estadoFirma;
SELECT rol, activo, COUNT(*) FROM Usuarios
 WHERE rol IS NULL OR (rol NOT IN ('ADMIN','ADMINISTRADOR','ADMINISTRADOR DE TRANSPORTE','ADMINISTRADOR DE SISTEMA','CONDUCTOR','CHOFER','SUPERVISOR','CONTABILIDAD')
   AND NOT (activo = 0 AND rol IN ('ADMINISTRADOR DE GRIFO','ADMINISTRADOR DE MAQUINARIA','OPERADOR')))
 GROUP BY rol, activo;
SELECT moneda, COUNT(*) FROM FacturaFlete WHERE moneda NOT IN ('PEN','USD') GROUP BY moneda;
SELECT 'Factura' t, COUNT(*) n FROM Factura WHERE valorTotal < 0
UNION ALL SELECT 'CPIC', COUNT(*) FROM CPIC WHERE valorTotalFlete < 0 OR pesoNeto < 0 OR pesoBruto < 0
UNION ALL SELECT 'FacturaFlete', COUNT(*) FROM FacturaFlete WHERE cantidad < 0 OR valorUnitario < 0 OR valorVenta < 0 OR igv < 0 OR importeTotal < 0
UNION ALL SELECT 'FacturaFleteItem', COUNT(*) FROM FacturaFleteItem WHERE cantidad < 0 OR valorUnitario < 0
UNION ALL SELECT 'DespachoCarga', COUNT(*) FROM DespachoCarga WHERE pesoBrutoKg < 0 OR pesoNetoKg < 0;
-- Huérfanos de las 10 FK nuevas (la 55 los cuenta y se detiene; aquí se ven antes):
SELECT 'SE.idDespachoOrigen' c, COUNT(*) n FROM SeguimientoExportacion s WHERE idDespachoOrigen IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Despachos d WHERE d.idDespacho = s.idDespachoOrigen)
UNION ALL SELECT 'SE.idDespachoDestino', COUNT(*) FROM SeguimientoExportacion s WHERE idDespachoDestino IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Despachos d WHERE d.idDespacho = s.idDespachoDestino)
UNION ALL SELECT 'SE.idUsuarioRegistro', COUNT(*) FROM SeguimientoExportacion s WHERE idUsuarioRegistro IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioRegistro)
UNION ALL SELECT 'SE.idUsuarioModificacion', COUNT(*) FROM SeguimientoExportacion s WHERE idUsuarioModificacion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = s.idUsuarioModificacion)
UNION ALL SELECT 'SH.idUsuarioCorreccion', COUNT(*) FROM SeguimientoHito h WHERE idUsuarioCorreccion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = h.idUsuarioCorreccion)
UNION ALL SELECT 'FD.idUsuarioFirmante', COUNT(*) FROM FirmaDigital f WHERE idUsuarioFirmante IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = f.idUsuarioFirmante)
UNION ALL SELECT 'MDV.idViajeProgreso', COUNT(*) FROM MovimientoDineroViaje m WHERE idViajeProgreso IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ViajesEnProgreso v WHERE v.idViajeProgreso = m.idViajeProgreso)
UNION ALL SELECT 'MDV.idUsuarioRegistro', COUNT(*) FROM MovimientoDineroViaje m WHERE idUsuarioRegistro IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = m.idUsuarioRegistro)
UNION ALL SELECT 'MDV.idUsuarioAnulacion', COUNT(*) FROM MovimientoDineroViaje m WHERE idUsuarioAnulacion IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = m.idUsuarioAnulacion)
UNION ALL SELECT 'OVA.idUsuarioAdmin', COUNT(*) FROM OrdenViajeAjuste a WHERE idUsuarioAdmin IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Usuarios u WHERE u.idUsuario = a.idUsuarioAdmin);
```

Lo que salga se anota como `D-` nuevo y se corrige (con aprobación del usuario) antes de aplicar. Los montos de liquidación no se miden aquí (en `sgvTransporte` deberían estar vacíos o en cero; si no, la 54 lo dice con el conteo). La 55 busca las FK repetidas por estructura, no por nombre (en `sgvTransporte` tendrán otros nombres generados). Control después: `SELECT name FROM sys.check_constraints WHERE is_not_trusted = 1 OR is_disabled = 1` y lo mismo en `sys.foreign_keys` → 0 filas; la lista exacta está en `WebSGV.Tests/Integracion/IntegridadBdTests.cs`. En el ensayo la 54 tardó 180 ms y la 55, 193 ms.

**Migración 56** (`56_QueryStoreAcotado.sql`, E3-7): Query Store de la BD actual con tope de 100 MB, captura y limpieza por tamaño `AUTO` y `READ_WRITE` (`sgvTransporte` tiene hoy 2000 MB de tope). Es un `ALTER DATABASE` (no va en transacción; `aplicar-migraciones.ps1` no la envuelve) y necesita permiso ALTER sobre la BD: si falta, se detiene con THROW 56001 y hay que hacerlo desde el panel de somee. Control antes y después: `SELECT actual_state_desc, readonly_reason, max_storage_size_mb, current_storage_size_mb, query_capture_mode_desc, size_based_cleanup_mode_desc FROM sys.database_query_store_options` → después `READ_WRITE`, 100, `AUTO`, `AUTO` (si lo usado pasa de 90 MB, la limpieza lo baja al 80 % en las horas siguientes).

**Migraciones 57 y 58** (`57_BorrarObjetosRetirados.sql`, `58_BorrarTablasRetiradas.sql`, E3-8): borran 24 SP, vistas y función retirados y 18 tablas retiradas, `Usuarios.idOperador` y `OrdenViaje.tipoViaje`, y pasan `ViajesEnProgreso.observaciones` a nvarchar(max). **Orden: 57 → `sp_DC_InsertarOrdenViajeConductor` → 58** (ese SP deja de escribir `tipoViaje`). `aplicar-migraciones.ps1` aplica el SP solo, justo antes de la 58, por su línea `-- Requiere SP:`; la 58 además se detiene (THROW 58005) si un objeto que se queda todavía nombra una tabla o columna que borra. La 58 cuenta las filas de cada tabla y se detiene (THROW 58001) si hay más que en pruebas el 2026-11-21 (`Operadores` y `EquiposMaquinaria` 1, las demás 0); también si llega una FK desde una tabla que se queda (58002) o si algún usuario tiene `idOperador` (58003). La 57 se detiene (57001) si otro objeto nombra a uno de los que borra. **Antes, medir en `sgvTransporte`** (solo SELECT) y guardar el resultado: si algo no está vacío, decidir con el usuario si se respalda o se conserva:

```sql
SELECT t.name, SUM(p.rows) AS filas FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id < 2
 WHERE t.name IN ('PartesDiariosTrabajo','AsignacionesMaquinaria','Operadores','EquiposMaquinaria','Distrito','Provincia','Departamento',
                  'Pais','CPIC_Productos','DetalleAlimentacion','DetalleApoyoSeguridad','DetalleEncapada','DetalleMovilidad',
                  'DetalleSegmento','SegmentosOrdenViaje','ProductosOperacion','OperacionesSubTramo','SubTramos')
 GROUP BY t.name ORDER BY t.name;
SELECT COUNT(*) AS usuariosConOperador FROM Usuarios WHERE idOperador IS NOT NULL;
SELECT COUNT(*) AS ordenesConTipoViaje FROM OrdenViaje WHERE tipoViaje IS NOT NULL;
```

**Antes de la 57, medir qué existe en `sgvTransporte`** (solo SELECT; lo que dé NULL ya no está y la 57 lo salta):

```sql
SELECT v.n AS objeto, OBJECT_ID('dbo.' + v.n) AS id FROM (VALUES
 ('sp_InsertarAbastecimientoCombustible'),('sp_InsertarDespachoObra'),('sp_InsertarIngresoEcuador'),('sp_InsertarIndicador'),
 ('sp_MQ_InsertarParteDiario'),('sp_MQ_GenerarNumeroParte'),('sp_MQ_ObtenerAsignacionActiva'),('sp_MQ_ObtenerDatosOperador'),
 ('sp_MQ_ObtenerHistorialPartes'),('sp_SE_BuscarDespachosDisponibles'),('sp_SE_Dashboard_Graficos'),('sp_SE_Dashboard_KPIs'),
 ('sp_SE_Insertar'),('sp_SE_ListarEnCurso'),('sp_SE_Listar'),('sp_SE_ObtenerDespachoPorId'),('sp_SE_ObtenerPorId'),
 ('sp_LD_ActualizarConductorDominanteViajes'),('vw_DespachosCompletos'),('vw_Auditoria'),('vw_IndicadoresExcelCompatible'),
 ('VW_OperacionesConPlantas'),('vw_SegmentosCompletos'),('fn_ValidarDisponibilidadRecursos')) v(n);
```

**Controles después de las migraciones 54 a 58** (solo SELECT; con `.\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo <archivo>.sql`, o cada consulta entre comillas). Cada uno dice lo que debe dar:

```sql
-- 54: los 26 CHECK que quedan tras la 58 (los 4 de DetalleAlimentacion, DetalleApoyoSeguridad, DetalleEncapada
-- y DetalleMovilidad se van con sus tablas). Debe dar 26 filas, todas con is_not_trusted = 0 e is_disabled = 0.
SELECT name, is_not_trusted, is_disabled FROM sys.check_constraints WHERE name IN (
 'CK_Despachos_Estado','CK_Despachos_TipoOperacion','CK_Despachos_AnuladoInactivo','CK_ViajesEnProgreso_Estado',
 'CK_OrdenViaje_EstadoAprobacion','CK_OrdenViaje_EstadoViaje','CK_SeguimientoExportacion_Estado','CK_SeguimientoExportacion_Origen',
 'CK_FirmaDigital_Etapa','CK_FirmaDigital_EstadoFirma','CK_Usuarios_Rol','CK_FacturaFlete_Moneda',
 'CK_DetalleCombustible_Montos','CK_DetalleHospedaje_Montos','CK_DetallePeajes_Montos','CK_DetalleReparacionesVarios_Montos',
 'CK_Egresos_Montos','CK_Ingresos_Montos','CK_IngresosAdicionales_Montos','CK_CategoriasAdicionales_Montos',
 'CK_DescuentosReintegros_Montos','CK_Factura_ValorTotal','CK_CPIC_Montos','CK_FacturaFlete_Montos','CK_FacturaFleteItem_Montos',
 'CK_DespachoCarga_Pesos') ORDER BY name;
-- 54: estadoAprobacion NOT NULL con DEFAULT PENDIENTE (M-050). Debe dar is_nullable = 0 y ('PENDIENTE').
SELECT c.is_nullable, dc.definition FROM sys.columns c
LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('dbo.OrdenViaje') AND c.name = 'estadoAprobacion';
-- 55: las 12 FK (10 nuevas y las 2 de documentos sin cascada). Debe dar 12 filas, NO_ACTION, is_not_trusted = 0.
SELECT name, delete_referential_action_desc, is_not_trusted, is_disabled FROM sys.foreign_keys WHERE name IN (
 'FK_FirmaDigital_UsuarioFirmante','FK_MovimientoDineroViaje_ViajeProgreso','FK_MovimientoDineroViaje_UsuarioRegistro',
 'FK_MovimientoDineroViaje_UsuarioAnulacion','FK_OrdenViajeAjuste_UsuarioAdmin','FK_SeguimientoExportacion_DespachoOrigen',
 'FK_SeguimientoExportacion_DespachoDestino','FK_SeguimientoExportacion_UsuarioRegistro','FK_SeguimientoExportacion_UsuarioModificacion',
 'FK_SeguimientoHito_UsuarioCorreccion','FK_DocumentosFactura_Factura','FK_DocumentosCPIC_CPIC') ORDER BY name;
-- 54 y 55: nada sin validar. Debe dar 0 filas.
SELECT 'CHECK ' + name FROM sys.check_constraints WHERE is_not_trusted = 1 OR is_disabled = 1
UNION ALL SELECT 'FK ' + name FROM sys.foreign_keys WHERE is_not_trusted = 1 OR is_disabled = 1;
-- 57 y 58: las 24 piezas, las 18 tablas y el trigger de CPIC_Productos ya no existen. Debe dar 0 filas.
SELECT v.n FROM (VALUES
 ('sp_InsertarAbastecimientoCombustible'),('sp_InsertarDespachoObra'),('sp_InsertarIngresoEcuador'),('sp_InsertarIndicador'),
 ('sp_MQ_InsertarParteDiario'),('sp_MQ_GenerarNumeroParte'),('sp_MQ_ObtenerAsignacionActiva'),('sp_MQ_ObtenerDatosOperador'),
 ('sp_MQ_ObtenerHistorialPartes'),('sp_SE_BuscarDespachosDisponibles'),('sp_SE_Dashboard_Graficos'),('sp_SE_Dashboard_KPIs'),
 ('sp_SE_Insertar'),('sp_SE_ListarEnCurso'),('sp_SE_Listar'),('sp_SE_ObtenerDespachoPorId'),('sp_SE_ObtenerPorId'),
 ('sp_LD_ActualizarConductorDominanteViajes'),('vw_DespachosCompletos'),('vw_Auditoria'),('vw_IndicadoresExcelCompatible'),
 ('VW_OperacionesConPlantas'),('vw_SegmentosCompletos'),('fn_ValidarDisponibilidadRecursos'),
 ('PartesDiariosTrabajo'),('AsignacionesMaquinaria'),('Operadores'),('EquiposMaquinaria'),('Distrito'),('Provincia'),
 ('Departamento'),('Pais'),('CPIC_Productos'),('DetalleAlimentacion'),('DetalleApoyoSeguridad'),('DetalleEncapada'),
 ('DetalleMovilidad'),('DetalleSegmento'),('SegmentosOrdenViaje'),('ProductosOperacion'),('OperacionesSubTramo'),('SubTramos'),
 ('TR_CPIC_Productos_Auditoria')) v(n)
WHERE OBJECT_ID('dbo.' + v.n) IS NOT NULL;
-- 58: columnas. Debe dar NULL, NULL y nvarchar -1.
SELECT COL_LENGTH('dbo.Usuarios', 'idOperador') AS idOperador, COL_LENGTH('dbo.OrdenViaje', 'tipoViaje') AS tipoViaje,
       (SELECT TYPE_NAME(user_type_id) FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ViajesEnProgreso') AND name = 'observaciones') AS tipoObservaciones,
       (SELECT max_length FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ViajesEnProgreso') AND name = 'observaciones') AS largo;
```

La lista exacta que sostienen las pruebas está en `WebSGV.Tests/Integracion/IntegridadBdTests.cs` (`Los_CHECK_son_exactamente_los_esperados`, `Las_FK_de_la_migracion_55_existen_sin_cascada`, `Lo_borrado_en_E3_8_ya_no_existe`). Tras la 60 la FK `FK_OrdenViajeAjuste_UsuarioAdmin` de la 55 ya no existe (se va con su tabla): el control de la 55 da 11 filas.

**Migraciones 59 y 60** (`59_BorrarRevocarFirmaYPortador.sql`, `60_BorrarTablasGrifoYGuias.sql`, tras publicar `92d4dc6`): la 59 borra `sp_DC_RevocarFirma` y `MovimientoDineroViaje.idConductorPortador` con su FK; la 60 borra 8 tablas (`DetalleTicketEcuador`, `IngresoCombustibleEcuador`, `DespachoCombustibleObra`, `AbastecimientoCombustible` con su trigger sin archivo, `LugarAbastecimiento`, `TipoCarro`, `GuiasTransportista` con `TR_GuiasTransportista_Auditoria` y `OrdenViajeAjuste`) y las FK de `DetalleOrdenViaje.idGuia` y `Manifiesto.idGuia` (las columnas se quedan, M-110). **Orden: `sp_LD_RegistrarMovimientoViaje` → 59 → 60** (ese SP deja de aceptar `@idConductorPortador`; `aplicar-migraciones.ps1` lo aplica solo por la línea `-- Requiere SP:` de la 59, y la 59 se detiene con 59002 si el SP viejo sigue). Necesita publicado un código igual o posterior a `92d4dc6` (el de antes de la migración 43 enviaba `@idConductorPortador`). Se detienen sin borrar nada si: otro objeto nombra el SP o la columna (59001/59002); hay más de 1 movimiento con portador o alguno activo (59003); hay más filas que en pruebas el 2026-11-21 (60001: `LugarAbastecimiento` 2, `TipoCarro` 6, las demás 0); llega otra FK desde una tabla que se queda (60002); `DetalleOrdenViaje` o `Manifiesto` tienen filas con `idGuia` (60003); o un objeto que se queda nombra una tabla de la lista (60005). No se tocan `camionetas`, `volquetes`, `Obras` ni `ClientesObra` (M-109). **Antes, medir en `sgvTransporte`** (solo SELECT) y guardar el resultado; si algo pasa de lo medido, decidir con el usuario si se respalda o se conserva:

```sql
-- Filas de las tablas que borra la 60 y de las que se quedan (NULL = no existe).
SELECT v.n AS tabla, (SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = OBJECT_ID('dbo.' + v.n) AND p.index_id < 2) AS filas
FROM (VALUES ('DetalleTicketEcuador'),('IngresoCombustibleEcuador'),('DespachoCombustibleObra'),('AbastecimientoCombustible'),
             ('LugarAbastecimiento'),('TipoCarro'),('GuiasTransportista'),('OrdenViajeAjuste'),
             ('camionetas'),('volquetes'),('Obras'),('ClientesObra'),('DetalleOrdenViaje'),('Manifiesto')) v(n);
-- FK que llegan a las tablas de la 60 desde otras. Solo deben salir las 6 entre ellas mismas y DetalleOrdenViaje/Manifiesto.idGuia.
SELECT OBJECT_NAME(f.parent_object_id) AS desde, f.name, OBJECT_NAME(f.referenced_object_id) AS hacia FROM sys.foreign_keys f
WHERE OBJECT_NAME(f.referenced_object_id) IN ('DetalleTicketEcuador','IngresoCombustibleEcuador','DespachoCombustibleObra',
      'AbastecimientoCombustible','LugarAbastecimiento','TipoCarro','GuiasTransportista','OrdenViajeAjuste') ORDER BY hacia, desde;
-- 59: movimientos con portador (en pruebas: 1, anulado) y si el SP sigue.
SELECT COUNT(*) AS conPortador, SUM(CASE WHEN activo = 1 THEN 1 ELSE 0 END) AS activos
FROM dbo.MovimientoDineroViaje WHERE idConductorPortador IS NOT NULL;
SELECT OBJECT_ID('dbo.sp_DC_RevocarFirma') AS spRevocarFirma;
```

**Controles después de las migraciones 59 y 60** (solo SELECT; mismo modo que los de arriba):

```sql
-- El SP, las 8 tablas y sus 2 triggers ya no existen. Debe dar 0 filas.
SELECT v.n AS sigue FROM (VALUES
 ('sp_DC_RevocarFirma'),('DetalleTicketEcuador'),('IngresoCombustibleEcuador'),('DespachoCombustibleObra'),
 ('AbastecimientoCombustible'),('LugarAbastecimiento'),('TipoCarro'),('GuiasTransportista'),('OrdenViajeAjuste'),
 ('TR_AbastecimientoCombustible_Auditoria'),('TR_GuiasTransportista_Auditoria')) v(n)
WHERE OBJECT_ID('dbo.' + v.n) IS NOT NULL;
-- 59: columna y su FK. Debe dar NULL y 0.
SELECT COL_LENGTH('dbo.MovimientoDineroViaje', 'idConductorPortador') AS idConductorPortador,
       (SELECT COUNT(*) FROM sys.foreign_keys WHERE name = 'FK_MovimientoDineroViaje_Portador') AS fkPortador;
-- 59: sp_LD_RegistrarMovimientoViaje sin @idConductorPortador. Debe dar 0.
SELECT COUNT(*) AS parametroPortador FROM sys.parameters
WHERE object_id = OBJECT_ID('dbo.sp_LD_RegistrarMovimientoViaje') AND name = '@idConductorPortador';
-- 60: camionetas, volquetes, Obras y ClientesObra siguen con las filas medidas antes; las 2 columnas idGuia, con 0 FK.
SELECT t.name, SUM(p.rows) AS filas FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id < 2
 WHERE t.name IN ('camionetas','volquetes','Obras','ClientesObra') GROUP BY t.name ORDER BY t.name;
SELECT OBJECT_NAME(c.object_id) AS tabla, c.name,
       (SELECT COUNT(*) FROM sys.foreign_key_columns fc WHERE fc.parent_object_id = c.object_id AND fc.parent_column_id = c.column_id) AS fks
FROM sys.columns c WHERE c.name = 'idGuia' AND c.object_id IN (OBJECT_ID('dbo.DetalleOrdenViaje'), OBJECT_ID('dbo.Manifiesto'));
-- Registradas. Debe dar 2 filas.
SELECT archivo, fechaAplicacion FROM dbo.SchemaVersion
WHERE archivo IN ('59_BorrarRevocarFirmaYPortador.sql', '60_BorrarTablasGrifoYGuias.sql');
```

**Migración 61** (`61_IntentoAcceso.sql`, E4-4): crea `dbo.IntentoAcceso` (contadores contra la fuerza bruta del login, la firma, la recuperación de contraseña y `VerificarDocumento`; las claves de cuenta son SHA-256, nunca el texto escrito, y un usuario con caracteres fuera de `[A-Za-z0-9._@-]` cuenta en `USR_invalido` / `RECUP_USR_invalido`) con `PK_IntentoAcceso`, `CK_IntentoAcceso_Fallidos` e `IX_IntentoAcceso_VenceUtc`. Es aditiva: no toca datos. **Orden: migración 61 → `aplicar-procedimientos.ps1`** (`sp_US_ReservarIntentoAcceso`, `sp_US_DevolverIntentoAcceso` y `sp_US_QuitarIntentoAcceso`, que usan la tabla) **→ publicar el código de E4-4**. Con el código publicado antes, el login sigue funcionando sobre la caché del proceso y deja un error en el log cada minuto. Antes, comprobar que ningún usuario queda fuera del formato (debe dar 0; si no, esa cuenta no podrá entrar):

```sql
SELECT COUNT(*) AS fueraDeFormato FROM dbo.Usuarios
WHERE nombreUsuario COLLATE Latin1_General_BIN2 LIKE '%[^-A-Za-z0-9._@]%'
   OR LTRIM(RTRIM(ISNULL(email, ''))) COLLATE Latin1_General_BIN2 LIKE '%[^-A-Za-z0-9._@]%';
```

Control posterior (solo SELECT):

```sql
-- Tabla, restricciones e índice: 4 filas.
SELECT name FROM sys.objects WHERE name IN ('IntentoAcceso', 'PK_IntentoAcceso', 'CK_IntentoAcceso_Fallidos')
UNION ALL SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.IntentoAcceso') AND name = 'IX_IntentoAcceso_VenceUtc';
-- Los 3 SP: 3 filas.
SELECT name FROM sys.procedures WHERE name IN ('sp_US_ReservarIntentoAcceso', 'sp_US_DevolverIntentoAcceso', 'sp_US_QuitarIntentoAcceso');
-- Registrada: 1 fila.
SELECT archivo, fechaAplicacion FROM dbo.SchemaVersion WHERE archivo = '61_IntentoAcceso.sql';
-- Tras publicar y un login fallido de prueba: solo prefijos IP_/UIP_/USR_/REAUT_/RECUP_/RESET_/VERIF_ (las de cuenta, con 64 hex o *_invalido).
SELECT LEFT(clave, 10) AS prefijo, COUNT(*) FROM dbo.IntentoAcceso GROUP BY LEFT(clave, 10);
SELECT Accion, COUNT(*) FROM AuditoriaLog WHERE Accion IN ('LOGIN_FALLIDO', 'LOGIN_BLOQUEADO') GROUP BY Accion;
```

## 5. Procedimientos almacenados

**Obligatorio antes de este paso y del 4:** ensayar el mismo commit sobre el `.bacpac` más reciente con `.\herramientas\respaldo\ensayar-migracion.ps1` (migraciones, SP, ejecución de humo y `[FactBD]` en una copia local) y tener `RESULTADO: OK`. `aplicar-procedimientos.ps1` vuelve a hacer el humo al terminar, pero en producción eso llega tarde: el SP ya quedó aplicado.

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

**Controles de E3-2 (solo SELECT):** `SELECT COUNT(*) FROM Despachos WHERE estadoDespacho = 'EN_PROCESO'` y `SELECT estado, COUNT(*) FROM SeguimientoExportacion GROUP BY estado`. Desde E3-2 los SP dejan los despachos rechazados en `PROGRAMADO` y el seguimiento solo en `EN_CURSO`/`FINALIZADO`/`CANCELADO`; si salen `EN_PROCESO`, `COMPLETADO` o `PENDIENTE`, anotarlos como un `D-` nuevo (la corrección de pruebas es D-030).

## 6. SP retirados

Ninguno los llama el código publicado. Los 8 `sp_SE_*` sin uso que borraba `borrar-sp-se-sin-uso.sql` los borra ahora la migración 57 (paso 4); el script se quitó del repo.

1. `WebSGV/Database/Scripts/borrar_sp_AprobarLiquidacion.sql` (`DROP PROCEDURE IF EXISTS sp_AprobarLiquidacion`). En `sgvActualizada` ya no existe y su archivo ya no está en `StoredProcedures/`.

```powershell
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

1. **Choferes:** en el sitio (ya sobre `sgvTransporte`, paso 9), `RegistroChoferes` → "Actualizar con la lista de choferes actuales" con `docs/DOCUMENTACION/LISTA DE CHOFERES ACTUALES (corregida).xlsx` (fuera de git; usa `sp_RegistrarConductorImportado`, del paso 5). Deja inactivos a los que ya no trabajan. Después, `privado/registrar_choferes_listas_2026-10-24.sql` (fuera de git desde E4-1: tiene DNI y apellidos; está en la carpeta `privado/` de la PC de la oficina) (registra como inactivos los de las listas de 2025 y aplica el estado ACTIVO/BAJA de la lista de octubre; se puede repetir) y los scripts sueltos de registro de choferes que haya en `privado/`. Revisar antes qué hace cada uno: sus nombres y DNI no se copian a ningún documento.
2. **Cargas de terceros con CPIC, MCI y facturas** (`herramientas/carga-historica-correo`, herramienta `TercerosConDocumentos`, con los archivos de tareas `tareas_terceros_2026-11-06.txt` y `tareas_polinplast_2026-11-06.txt` de la PC de la oficina; ver su README). **Ojo:** `Ejecutar.ps1` hoy solo usa `WebSGV\connectionStrings.config` (pruebas): para correrlo contra producción hay que prepararlo antes (no cambiar la cadena de pruebas a mano). Las GRE de Polinplast que se subieron por la bandeja: volver a subirlas desde `docs/DOCUMENTACION/GRE SUNAT NOV 2026`. Lo mismo con las demás cargas históricas del correo (`CompletarConCorreo`, `AdjuntarPdfFacturas`), si se quieren en producción.
3. **Seguimiento recalculado:** solo si `sgvTransporte` tiene esos viajes de octubre de 2026. Con el sitio ya sobre `sgvTransporte`:

   ```powershell
   .\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo WebSGV\Database\Scripts\recalcular_ingreso_salida_ecuador_2026-10-23.sql
   .\WebSGV\Database\sql.ps1 -Entorno produccion -ConfirmoProduccion -Archivo WebSGV\Database\Scripts\recalcular_afrecho_descarga_en_peru_2026-10-27.sql
   .\herramientas\sitio-local\actualizar-gps.ps1 -Sitio https://sgvtransporte.somee.com -Ids <los idSeguimiento que devolvió cada script>
   ```

   (o "Actualizar GPS" en el detalle de cada viaje). Cada script devuelve los viajes que tocó. `actualizar-gps.ps1` entra con `admin.transporte` y su contraseña de `privado/CREDENCIALES_USUARIOS.txt`: en producción, `-Usuario` con una cuenta que exista allí y esté en ese archivo.

## 9. Cambiar la cadena del servidor

1. Con un cliente FTP, subir a la raíz del sitio (`/www.sgvtransporte.somee.com/`) el contenido de `connectionStrings.Production.config` **con el nombre `connectionStrings.config`** (con `Encrypt=True;TrustServerCertificate=True`, U-084) (el `Web.config` lee `configSource="connectionStrings.config"`). Hacerlo desde una copia fuera del repo y borrarla después. `publicar.ps1` nunca sube este archivo.
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
