# Plan de mejoras SGV — estado y tareas pendientes

> **En qué etapa estamos y qué sigue:** `docs/HOJA_DE_RUTA_CALIDAD.md` (hoja de ruta por etapas desde el 2026-11-19; leerla primero).

Última actualización: **2026-11-21**, commit anterior `cdb9da7` — E2-3: historial archivado en `docs/archivo/HISTORIAL_2026.md` (cabecera larga, secciones 2, 3, 5 y 6, "Hechas" y las `[x]` que quedaban entre las abiertas); el historial es `git log` (un commit por tarea; la tabla de la antigua sección 3 y las tareas hechas hasta hoy están en ese archivo). Este documento es el punto de partida para seguir trabajando
desde cualquier máquina. No contiene contraseñas: solo indica dónde están.

## 1. Estado actual

| | Al empezar (2026-10-07) | Ahora |
|---|---|---|
| Puntuación general | 5.5 / 10 | **8 / 10** |
| Tests | 215 | **649** en verde (2026-10-21; incluye los de integración, varios escriben con rollback) |
| Paquetes NuGet | 40 | 23 |
| Procedimientos del repo vs BD de pruebas | sin control, 2 inexistentes | **79/79 iguales** (la BD tiene exactamente los del repo) |

- Todo se trabaja contra la BD de **PRUEBAS** (`sgvActualizada`). El sistema todavía no lo usa nadie.
- **Producción (`sgvTransporte`) NO está actualizada**: el código de `master` no se puede publicar
  hasta seguir `docs/PASO_A_PRODUCCION.md` (en orden).

## 2. Preparar la laptop de casa

Vigente en `docs/PREPARAR_MAQUINA.md`; el texto anterior está en `docs/archivo/HISTORIAL_2026.md`.

## 4. Tareas pendientes (en pruebas)

Ordenadas por valor. Al terminar, la tarea se borra del plan (el commit es el historial). Todo pendiente va como su propio `[ ]` en esta lista
(no escondido dentro del texto de una tarea hecha).

- [ ] **Secretos fuera del paquete**: desde E1-4 `publicar.ps1` no sube `connectionStrings.config` ni `appSettings.Secrets.config`; al rotar un secreto (p. ej. `ApisPeruToken`, `GpsAutomaticoClave`) hay que subir ese archivo a mano por FTP. No publicar también desde Visual Studio (o usar `-SubirTodo` después).
- [ ] **E1-7 tras publicar**: poner `SmtpFrom` (`sgv-avisos@serviciosgeneralesviviana.com`) y las claves SMTP en el `appSettings.Secrets.config` del servidor; provocar una vez un error real en el sitio publicado y ver llegar el correo; ver un aviso de postback parcial real (solo cubierto por pruebas unitarias).
- [~] **E1-6 tras publicar**: crear los dos monitores de UptimeRobot (`docs/OPERACION.md`, poner ahí la URL real) y ver una vez el 503 real de `?gps=1` (con la app arriba más de 60 min sin cron) — solo está cubierto por pruebas unitarias. — 2026-11-21: monitores creados ("SGV - Sitio y BD" y "SGV - GPS y Onway", palabra clave `"ok":true`, cada 5 min, correo), ambos Up. Falta solo ver una vez el 503 real (ver el ítem siguiente).
- [ ] **El log no distingue la corrida manual del GPS de la del cron** (ambas escriben "Actualización automática del GPS"). Añadir "(manual)"/"(programada)".
- [ ] **Validar `Onway_sin_respuesta` en el servidor**: tras publicar, comprobar una vez que el monitor de UptimeRobot de `?gps=1` marca el 503 (p. ej. credenciales de Onway inválidas en pruebas más de 2 h) y que los `.so`/`.dylib` de linux/osx que ya estén en el servidor se borren a mano por FTP.
- [ ] **PDF firmados que solo están en la laptop** (la verificación diaria del respaldo da FALLA hasta subirlos: 31 faltantes al 2026-11-20, todos de firmas anuladas): 14 firmas (380–385, 392–395, 680–682, 686) y la copia de la firma 684 de `OV-2026-000003_REVISION.pdf` se generaron en el sitio local de la otra máquina; todas anuladas (liquidaciones de prueba). Copiarlas al respaldo o aceptar que `verificar-pdf-firmados.ps1` las marque como faltantes.

### Abiertas

- [ ] **Correr `Database/Scripts/borrar-sp-se-sin-uso.sql` en `sgvActualizada`** (2026-11-21, E2-2): los 8 `sp_SE_*` sin uso siguen en la BD y `60853e8` (publicado) no los llama. Antes, el SELECT de control en `sys.sql_modules` (`docs/PASO_A_PRODUCCION.md`, paso 6). Lo hace Claude con orden del usuario o como tarea de la Etapa 3.
- [ ] **Decisión del usuario: cómo pasar a producción** (2026-11-21, E2-2): A, poner al día la `sgvTransporte` existente, o B, restaurar en ella un `.bacpac` de `sgvActualizada` (recomendación: B, porque los datos reales y los archivos del servidor son de `sgvActualizada`). Ver `docs/PASO_A_PRODUCCION.md`, paso 0.
- [ ] **Retiro de módulos sin uso (2026-11-19), parte pendiente** (hechas: 1 páginas huérfanas, 2 Grifo, 3 Maquinaria/Operador): (4) migración `52` con el DROP de los 9 SP (`sp_InsertarIndicador`, 3 de grifo, 5 `sp_MQ_*`): **pedir confirmación antes de aplicarla**, porque sgvActualizada es la BD del sitio publicado. Las tablas y sus datos se conservan.
- [ ] **Tras publicar el retiro de módulos, borrar a mano en somee** (la publicación FTP no borra; si quedan dan 500): `Views/Dashboard.aspx`, `Views/AgregarIndicadores.aspx`, `Content/paginas/Dashboard.css`, `Content/paginas/AgregarIndicadores.css`, `Scripts/paginas/Dashboard.js`. **Grifo:** `Views/{DashboardGrifo,AgregarAbastecimiento,BuscarAbastecimiento,ReporteAbastecimiento,RegistrarDespachoObra,RegistrarRetornoEcuador,DescargarPdfAbastecimiento}.aspx`, `Content/paginas/{AgregarAbastecimiento,BuscarAbastecimiento,DashboardGrifo,RegistrarDespachoObra,RegistrarRetornoEcuador,ReporteAbastecimiento}.css`, `Scripts/paginas/{AgregarAbastecimiento,BuscarAbastecimiento,DashboardGrifo,RegistrarDespachoObra,RegistrarRetornoEcuador}.js`. Decidir si se conserva la carpeta `App_Data/Abastecimientos` del servidor (PDF ya generados). **Maquinaria:** `Views/{AsignacionesMaquinaria,DashboardOperador,RegistroOperadores,RegistroEquiposMaquinaria,RegistroObras,RegistroClientesObra}.aspx`, `Content/paginas/DashboardOperador.css`, `Scripts/paginas/DashboardOperador.js`.
- [ ] **Decisión del usuario: desactivar los usuarios `admin.grifo`, `admin.maquinaria` y `operador`** (ids 91, 92 y 94 en sgvActualizada) cuando sus módulos estén retirados. Recomendado: desactivar desde GestionUsuarios, no borrar (conserva la auditoría). Quitar también sus entradas de `privado/CREDENCIALES_USUARIOS.txt`.
- [ ] **Documentos viejos que aún nombran páginas o roles borrados** (FLUJO, STACK y GUIA ya se limpiaron de grifo): `ANALISIS_PROYECTO.md`, `flujoactual.md` y `configuraciondenuevosagentes.md` (raíz); `.opencode/skills/auditoria-y-sesiones-sgv/SKILL.md` (enseña `TienePermiso("ABASTECIMIENTO")` y `EsAdminGrifo()`), `.opencode/agents/{orchestrator,qa,writer}.md`, `.opencode/skills/nueva-pagina-aspx/SKILL.md`. Decidir si se actualizan o se borran.
- [ ] **Mensaje para un usuario con rol retirado o desconocido**: hoy vuelve a Login con "Tu sesión ha expirado o no es válida…" y cada intento deja un LOGIN en auditoría. Decidir si merece un mensaje propio ("tu cuenta no tiene un rol vigente") y rechazarlo antes de crear la sesión.
- [ ] **SaldosConductores: el enlace a la orden no sirve para el administrador** (ya existía; QA 2026-11-19): `SaldosConductores.aspx:93` enlaza a `DetalleOrdenViaje.aspx?id=`, pero esa página redirige a su inicio a quien no es conductor (`DetalleOrdenViaje.aspx.cs`, `if (!SecurityHelper.EsConductor())`). Apuntar el enlace a la pantalla de revisión/PDF del administrador o permitirle ver el detalle. No se pudo ver con datos reales (`OrdenViaje` vacía).
- [ ] **Restos sin uso tras retirar Grifo**: la regla `:where(div).total-row` de `Content/responsive-mobile.css` y los scripts históricos `Database/Scripts/script_{AgregarColumnaRutaDescripcion,AgregarColumnaTipoAbastecimiento,AgregarPdfAbastecimiento,CrearTablasDespachoObra,CrearTablasIngresoEcuador,TrazabilidadVolqueteCamioneta}.sql` (DDL de tablas que se conservan). **No borrar las tablas** `AbastecimientoCombustible`, `DespachoCombustibleObra` ni `IngresoCombustibleEcuador` sin tocar antes `VehiculoService.cs` (las consulta al eliminar tractos y carretas).
- [ ] **Probar en el navegador la corrección de Gestión de Usuarios** (parte 3; solo tiene pruebas unitarias): editar a `operador`, cancelar, "Nuevo usuario" → el desplegable no ofrece OPERADOR; editar a `operador` sin cambiar el rol y guardar → lo conserva (esto último escribe en la BD: hacerlo solo si se decide no desactivarlo).
- [ ] **Gestión de Usuarios: no se puede filtrar por rol retirado** (los usuarios 91, 92 y 94 solo salen con "Todos los roles"). Deja de importar si se desactivan o reasignan.
- [ ] **"Auditoría del Sistema" sale dos veces en el menú del administrador de sistema** (en Administración y en Configuración; visto en QA 2026-11-19, no se verificó si es anterior). Dejar una.
- [ ] **Restos tras retirar Maquinaria**: `Database/Scripts/script_CrearTablasMaquinaria.sql` (DDL de tablas conservadas, sigue en el csproj) y la columna `Usuarios.idOperador` con su FK, que ya ningún código lee. Decidir si se quitan junto con las tablas de maquinaria (hoy solo tienen 1 fila de maestros cada una).
- [ ] **QA pendiente por falta de datos**: probar con un SUPERVISOR activo (los 2 de la BD están desactivados) y el botón "Ver" del historial del conductor cuando exista una liquidación.

- [ ] **Probar con un conductor real el ciclo de la liquidación**: enviar → rechazar → reenviar (pide firma nueva; el PDF viejo sale ANULADO) y retirar una firmada (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **Quitar `@idConductorFallback` de `sp_DC_ObtenerDatosViajeParaLiquidacion`** (tarea 2), después de la prueba con un conductor real (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **Ajustar `Seguridad.IteracionesContrasena`** con los ms de "Hash de contraseña ... convertido" en `App_Data/logs` del servidor (tarea 7); no volver a un DLL anterior (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **Recuperar contraseña en el sitio publicado**: comprobar que el enlace sale con `App.UrlPublica` (publicación Release) (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **Celular real**: Login, panel del conductor, Liquidaciones pendientes y Verificar documento; en Edge, el Buscar de Seguimiento no ofrece contraseñas y Enter filtra (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **Sitio publicado**: mapa de SeguimientoViaje, un error remoto → `Error.aspx` y, con CONTABILIDAD, el autocompletado de conductor (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **Avisar a la administradora**: una orden rechazada se firma solo tras reenviarla; no se rechaza una liquidación enviada a conformidad o aprobada (usar Revertir); no se revierte una pagada sin anular el pago; el administrador de sistema solo ve usuarios y auditoría; 13 usuarios con cambio de contraseña pendiente no pueden firmar ni descargar PDF hasta cambiarla (salió de las verificaciones tras la publicación del 2026-11-16 al archivar).
- [ ] **10 firmas huérfanas del 2026-11-04** (`FirmaDigital.idFirma` 380-385 y 392-395, `idDocumento` 596, 597, 610, 611, 612): de liquidaciones retiradas antes de la tarea 2; sin código de verificación ni anulación, figuran como vigentes. Decidir si se les registra una ANULACION (motivo RETIRO_CONDUCTOR) con un script.
- [ ] **`sp_DC_RevocarFirma` y `FirmaService.RevocarFirma` quedaron sin uso** (decidir si se borran). El `ERROR_MESSAGE()` de `sp_LD_DevolverLiquidacionARevision` ya se quitó en la tarea 4.
- [ ] **Separación de funciones nominal**: el administrador de sistema puede crear o editar otra cuenta con rol de administración desde Gestión de usuarios y entrar con ella. Decidir si se limita (p. ej. que solo pueda asignar roles operativos con aprobación) o se acepta con la auditoría como control.
- [ ] **Rol SUPERVISOR sin probar en la tarea 4** (su contraseña no está en `privado/CREDENCIALES_USUARIOS.txt`; por código no cambió) y **el usuario `contabilidad` no inicia sesión** con la contraseña del archivo (¿desactualizada?). Revisar ambos.
- [ ] **Celular de un conductor: coma decimal** (visto por el probador en Edge headless es-PE): en un `type=number`, "12,50" podría enviarse como 1250. Confirmar en un celular real; si pasa, avisar en el JS antes de enviar.
- [ ] **GPS caído al enviar**: con la regla nueva, un viaje que la BD marca nacional pero cruzó a Ecuador (terceros) no se puede enviar mientras el GPS no responda (decisión: mejor que borrar dólares). Si molesta en la práctica, permitir que la administración marque el viaje como internacional.
- [ ] **Usuario de prueba `********` es un conductor real** (W. D. A. B.) y su conductor (`idConductor=1`) está inactivo, por lo que GestionUsuarios no deja editarlo ("Debe asociar un conductor"). Decidir si se reemplaza por una cuenta de prueba propia en `privado/CREDENCIALES_USUARIOS.txt`; el cambio de conductor vinculado cierra la sesión (probado en BD), pero no se probó desde la pantalla.
- [ ] **Usuario, en cada máquina (oficina y laptop): borrar `.env` y `dab-config.json` de la raíz** (gitignored; `.env` tiene una cadena de conexión real). Confirmar que no hay un DAB desplegado en Azure.
- [ ] **Usuario: cambiar la contraseña de aplicación de Gmail `SGV avisos`** (cuenta `sistemas.cibertic@`): quedó escrita en el chat del 2026-11-19. Borrarla en myaccount.google.com, crear otra y ponerla en `appSettings.Secrets.config` (`SmtpPass`) de cada máquina y del servidor sin pegarla en el chat.
- [ ] **Decidir el futuro de `.opencode/`** (sistema de agentes OpenCode anterior a `.claude/agents/`): actualizarlo o borrarlo (Etapa 2). `.opencode/agents/dba.md:67` aún lista `sp_AB_*` (Abastecimiento, módulo retirado); `configuraciondenuevosagentes.md` (raíz) es una transcripción histórica con DAB.
- [ ] **Antes de publicar la 8B, consultar estados en producción**: `SELECT estadoViaje, estadoAprobacion, COUNT(*) FROM OrdenViaje GROUP BY estadoViaje, estadoAprobacion` en sgvTransporte. Si hay órdenes `COMPLETADO` con `estadoAprobacion` distinto de `APROBADO` (p. ej. un valor antiguo `COMPLETADO`, que aún aceptan `DashboardConductor.aspx.cs:1084` y `DetalleOrdenViaje.aspx.cs:84`), desaparecerían de Aprobadas, Contabilidad, Reportes y Saldos: corregirlas antes. En pruebas no hay ninguna.
- [ ] **Saldos: encabezado del conductor con monedas de signo opuesto** (ya existía; `SaldosConductores.aspx.cs:79-90`): con neto +100 S/ y −50 US$ dice "A descontar S/ 100,00 · US$ 50,00" mientras el resumen pone los US$ en "A reintegrar". Mostrar el sentido por moneda.
- [ ] **Secciones de `TienePermiso` sin ninguna página** (FACTURA, CPIC, CONSULTAS, INDICADORES, DASHBOARD_EXPORTACION, MIS_VIAJES, MI_PERFIL, DASHBOARD_GRIFO, AUDITORIA, DASHBOARD_OPERADOR, PARTE_DIARIO) y ADMIN con acceso a DASHBOARD_CONDUCTOR/OPERADOR: decidir si se depuran (la tarea 8C solo las fijó con pruebas).
- [ ] **`privado/CREDENCIALES_USUARIOS.txt` guarda la contraseña de producción de nickdream en texto plano**: en la tarea 8B y la 8D-1 dos probadores lo volcaron por error en su salida interna. Sacar de ahí la de producción y, si se quiere, cambiar las de prueba. Para probar por HTTP sin leer el archivo: llevar el script `entrar-sgv.ps1` (hoy solo en el scratchpad de la sesión del 2026-11-16) a `herramientas/sitio-local/` y nombrarlo en el README.
- [ ] **Decisión del usuario: rellenar `idUsuarioModificacion` de seguimientos** escritos desde el Word con el usuario de AuditoriaLog (120 en sgvActualizada, 2026-10-27 a 2026-11-11; revisar producción). Script aparte con escritura; sin rellenar solo se pierde "último en modificar" en esa columna.
- [ ] **Usuario desechable `qa.auditoria8a`** (id 153, inactivo, creado al probar la tarea 8A en sgvActualizada): borrarlo o dejarlo.
- **Bloque 6 (exportación, reportes y paneles), analizado 2026-11-18.** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] **Antes de publicar B6-7: revisar en producción** `SELECT estadoAprobacion, COUNT(*) FROM OrdenViaje GROUP BY estadoAprobacion`: órdenes con un estado antiguo (COMPLETADO/RECHAZADO) o NULL solo salen con "Todos" en el reporte personalizado. Además la columna tiene DEFAULT 'APROBADO' (un INSERT que la omita la daría por aprobada sin firma; hoy ningún INSERT del repo la omite).
  - [ ] **ReportesOrdenesViaje: tras Buscar en "Viajes activos" la página vuelve a la pestaña "Liquidaciones"**; y en esa pestaña "Todos los estados" da lo mismo que "Abierto". Menor, previo.
  - [ ] **Grifo (fuera de alcance): `AgregarAbastecimiento.aspx.cs:~879` pone `ViajesEnProgreso.estadoViaje = 'ABASTECIDO'`**, lo que saca el viaje de "Viajes activos" y de todo flujo que busque ABIERTO. Revisar si Grifo vuelve al alcance.
  - [ ] **BD de pruebas sin SUPERVISOR activo** (`prueba.supervisor` y `qa.auditoria8a` con activo=0): los probadores no pueden probar ese rol. Reactivar uno de prueba (decisión del usuario) y anotar su credencial en `privado/CREDENCIALES_USUARIOS.txt`.
  - [ ] **ImportarSeguimiento: mostrar qué filas se omitieron** (hoy solo cuántas): que `sp_SE_ImportarLote` devuelva un segundo resultado con fila/pedido/tracto de cada omitida. Opcional.
  - [ ] **ImportarSeguimiento: el criterio "mismo tracto + mismo día" también mira viajes de despachos de clientes no exportadores** (CRISAR, POLINPLAST, NOVOPAN, AFRECHO); si un tracto hace uno de esos el mismo día que una exportación, la fila del Excel se omite. Riesgo bajo (0 casos hoy); aceptado.
  - [ ] **Seguimiento: 4 pares de viajes importados con el mismo tracto el mismo día** (A6L-927 2025-06-24, C6L-700 y AMN-912 2025-01-06, TBM-898 2024-03-23): parecen duplicados de la carga histórica. Revisar con el usuario.
  - [ ] **Seguimiento: viajes importados de 2024 cuentan como "Retorno" y no como "Cerrados"** (con 2024 e importados: Activos 500, Retorno 482, Cerrados 0; ya pasaba antes de B6-2). Revisar con el analista la fase que `HitosSeguimiento` asigna a los importados sin hitos de cierre.
  - [ ] **Decisión del usuario (bloque 6) D1** (2026-11-19: `Dashboard.aspx` y `AgregarIndicadores.aspx` ya borradas; **queda decidir solo `CargarExcel.aspx`**): borrar `Dashboard.aspx`, `CargarExcel.aspx` y `AgregarIndicadores.aspx` (sin enlace en el menú; leen/escriben la tabla `Indicadores` vieja, 2024-08 a 2025-05; Dashboard compara 'domingo' con la BD en inglés y tiene Puyango fijo; CargarExcel dice "completado" aunque pierda filas y nunca se usó). Recomendado: sí (B6-5); `Indicadores` se queda (historial de placas).
  - [ ] **ReportesOrdenesViaje: `ResponderSolicitudInvalida(ex.Message)` para cualquier `ArgumentException`** (`aspx.cs:~1123`): si una librería (ClosedXML/QuestPDF) lanza una, su texto técnico llegaría al usuario. Menor.
  - [ ] **Tablero de seguimiento: con `GpsAutomatico.ashx` bloqueado o caído, el respaldo hace la consulta pesada por postback** (`RegistroSeguimiento.js:~73-98`); probadores: abrir el tablero con `?auto=0` para no actualizar el GPS (en B6-8 un probador actualizó así 22 viajes activos: `fhUltimaConsultaGps` y última posición, lo mismo que hace cualquier apertura del tablero).
  - [ ] **Bloque 7 (operador, grifo, maquinaria), pasada de seguridad 2026-11-18**: nada grave (guards en todas las páginas, sin WebMethods ni subidas, SQL fijo). Si Grifo vuelve al alcance: ReporteAbastecimiento exporta HTML como .xls (doble codificación, `alert`, posible fórmula en observaciones) → `ReporteTabular`; excluye a SUPERVISOR; Maquinaria manda a Login en vez del inicio del rol.
- [ ] **Etapa 3: quitar el DEFAULT 'APROBADO' de `OrdenViaje.estadoAprobacion`** (un INSERT que omita la columna daría la orden por aprobada sin firma; hoy ningún INSERT del repo la omite) (salió de B6-7 al archivar).
- **Bloque 5 (registros y catálogos), analizado 2026-11-17.** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] Depurar el modelo de 13 vehículos (tractos 90–98 con modelo "1"…"8", tracto 102 sin modelo, carretas 127–129): hoy se pueden editar sin tocarlo, pero el dato no sirve (decisión/escritura del usuario).
  - [ ] **Usuario (URGENTE): rotar el token de apisperu** en apisperu.com y revocar el anterior: el actual está en el historial de git (desde `e503199`) y en los .js del sitio publicado hasta publicar B5-6; el `appSettings.Secrets.config` local tiene el MISMO token. Poner el nuevo como `ApisPeruToken` en el Secrets local y del servidor. Opcional: purgar el historial (no hace falta si se rota).
  - [ ] **Usuario: cambiar las contraseñas de `privado/CREDENCIALES_USUARIOS.txt`** (empezando por la de producción de nickdream): el 2026-11-18 un comando del probador de B5-6 mostró por error el archivo dentro de su sesión de Claude (no está en el repo ni en informes, sí en el registro local de esa sesión).
  - [ ] Mensajes de conductor duplicado con los espacios de relleno de las columnas CHAR ("NOMBRE      APELLIDO"): colapsar espacios en `RegistroConductorService.BuscarDuplicado`/`ObtenerDocumentos`.
  - [ ] Opcional (B5-8): impedir que dos administradores de sistema se desactiven a la vez dejando el sistema sin ninguno (`NOT EXISTS` de otro ADMINISTRADOR DE SISTEMA activo en el `WHERE`); y ocultar "Desactivar" en la fila propia (hoy el servidor lo rechaza con aviso).
  - [ ] Login de una cuenta inactiva: no deja fila `LOGIN_FALLIDO` en AuditoriaLog (observado por el probador de B5-8; revisar si es intencional).
  - [ ] **Ruido en `AuditoriaLog` de sgvActualizada (solo inserción, no se puede borrar)**: ids **6079–6135** (57 filas, 2026-11-17 16:34:04–16:34:11, usuario "Sistema", `FIRMA_CONTRASENA_INCORRECTA` ×49 y `CAMBIO_CONTRASENA_INCORRECTA` ×8, `IdRegistroAfectado = 42` = un CONDUCTOR real) los dejó una corrida de pruebas durante B5-3 (causa corregida); ids **6428–6429**: el probador renombró por error la PlantaCarga 3 y la restauró a "Trujillo" (dato idéntico). Usuario: dejarlas o insertar una fila NOTA que lo explique.
  - [ ] `DbHelper.FijarConexionParaPruebas` sigue `public` porque lo usan `herramientas/cuadro-status-general` y `herramientas/perfil-llegada` (no son el ensamblado de pruebas): opcional darles `InternalsVisibleTo` y hacerlo `internal`.
  - [ ] Opcional: la regex de carnet del navegador acepta `ABCDEFG` (sin dígitos) que el servidor rechaza; alinearla con `DocumentoConductor.EsCarnetValido`.
  - [ ] **Decisiones del usuario (bloque 5)**: D1 conductor duplicado D. R. C. C. (ids 70/75, DNI ******** y ********, ambos inactivos): ¿cuál es el correcto? (script aparte); D4 ¿ADMINISTRADOR DE GRIFO debe registrar conductores y aplicar la Lista de choferes? (recomendado: quitárselo; SUPERVISOR se queda); D5 borrar RegistroProductos y RegistroRutas (sin enlace, repiten CatalogoCliente; recomendado sí); D6 recortar el espacio final de la placa del tracto 75 (`T4I-868 `, 0 despachos). (Según el historial, `docs/archivo/HISTORIAL_2026.md:771-774`, D1 ya estaría resuelto: confirmar en E2-4.)
- [ ] **Probar a mano el primer envío de liquidación tras publicar la tarea B**: que redirija a la firma, el número sea OV-2026-000007 y la auditoría `GUARDAR_LIQUIDACION` lo registre (no se probó en pantalla para no crear una liquidación real).
- [ ] **Finalizar lote: copias huérfanas tras un commit en duda** (revisor, pendientes de la tarea 10): si el lote en realidad no se guardó, las copias definitivas del primer intento quedan sueltas en `~/Uploads` (sus rutas están en `App_Data/logs`). Sin riesgo para los datos; limpiar a mano si llega a pasar.
- [ ] **Lista de lotes: filtrar por la 2.ª factura o CPIC muestra solo esos despachos del lote** (el filtro del SP va antes de agrupar; pasarlo a un EXISTS como el de conductor) (salió de la tarea 11, «Lista de lotes con 2 facturas/2 CPIC», al archivar).
- [ ] **Decisión de negocio: ¿exigir el PDF de la factura comercial a los clientes con perfil CPIC (Novopan y otros)?** Hoy solo se exige a los clientes con pedido (ZIP); ninguna factura de clientes CPIC tiene PDF (20), así que exigirlo pasaría todos sus lotes a "pendiente". Por eso "Documentos" de un lote de Novopan dice "Completo" aunque sus facturas no tengan PDF (tarea 17).
- [ ] **Antes de aplicar un SP en sgvActualizada, ejecutarlo de prueba** (lección de la tarea 17: un SP con error rompió 15 min la lista de lotes del sitio publicado). Añadir a `aplicar-procedimientos.ps1` una ejecución de humo de los SP de lectura (o un `SET PARSEONLY`/`sp_describe_first_result_set`) antes de dar "Listo".
- [ ] **Subir los PDF de las facturas de Novopan 484–492** (sin PDF) desde la tarjeta "Factura y CPIC por despacho" cuando se publique.
- [ ] **Datos de CPIC por corregir (con aprobación del usuario, script aparte)**: CPIC 0003959 (id 223) con `fechaEmision` 2025-11-03 (¿2026?); CPIC 0003923 (id 34) sin factura (¿F222-00008499?).
- [ ] **Editar lote no puede pasar un lote a una pareja factura+CPIC que ya existe** (50018, revisor tarea 11): se hace desde "Factura y CPIC por despacho". Anotarlo en el manual.
- [ ] **Borrar a mano en somee `Views/EditarDespacho.aspx`** (y `Content/paginas/EditarDespacho.css`, `Scripts/paginas/EditarDespacho.js`) tras publicar las tareas 12 y 13: la publicación FTP no borra y la página daría 500.
- **Tarea 14: límites de archivos** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] Bandeja con dos pestañas: hay una sola lectura por sesión; si se lee en la pestaña B y se registra en la A, se registra la lectura de B con los índices de A. Poner un id de lectura en un campo oculto y compararlo al registrar.
  - [ ] Probar en el navegador el aviso "La lectura venció" y el reemplazo de un manifiesto (no había datos para hacerlo sin escribir; cubiertos por pruebas unitarias).
  - [ ] Probar en el sitio publicado un envío de más de 25 MB: en somee podría salir "conexión restablecida" en vez del aviso.
  - [ ] Bandeja: los CPIC/MCI, GRE y facturas de flete de dentro de un ZIP no tienen comprobación de firma propia (solo las guías al guardarse).
- **Tarea 15: emparejamiento de la bandeja** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] RegistroDespacho con Word: una fila del Word cuyas placas no coinciden con ningún programado, o una guía que no está en el Word, siguen arrancando en "Despacho nuevo" aunque queden programados libres (`ImportacionPedidoService.cs:~598`, `ProgramacionCarga.cs:~182-186`). No completa un programado no elegido, pero podría crear un despacho de más: valorar marcarlas `PorElegir` cuando haya programados libres.
  - [ ] Detalles menores de la tarea 15: el aviso de una CPIC en espera dice "no se eligió el camión" aunque las guías se pusieran en "No registrar ahora" a propósito; la lista de programados de RegistroDespacho ofrece también el que ya completa otra guía.
  - [ ] Probar de punta a punta, con despachos programados reales pendientes, el caso "por elegir" en la Bandeja y en RegistroDespacho (hoy hay 0 programados esperando documentos; cubierto por pruebas y por reflexión).
- **Tarea 16: limpieza del bloque 4** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] **Decisión de negocio: ¿cuándo está "completo" el manifiesto de un lote internacional?** Hoy un despacho cuenta como completo solo si tiene MCI de CRUCE y de RETORNO; en la BD ninguno tiene ambos (las cargas de Novopan tienen solo RETORNO y las descargas solo CRUCE), así que los 12 lotes internacionales salen "Pendiente". Confirmar con la usuaria qué manifiestos exige cada tipo de operación.
  - [ ] FacturasFlete: al corregir una factura (ACTUALIZADA) el PDF anterior queda en `Uploads/FacturasFlete` sin fila que lo apunte; valorar una limpieza de huérfanos.
- [ ] **Páginas sin enlace en el menú** (decidir una por una; 2026-11-19: Dashboard y AgregarIndicadores ya borradas, RegistrarRetornoEcuador se va con Grifo): Dashboard (además da `pageLoad is not defined`), AgregarIndicadores, CargarExcel, RegistrarRetornoEcuador, RegistroProductos, RegistroRutas.
- [ ] **Credenciales**: `privado/CREDENCIALES_USUARIOS.txt` guarda una contraseña de producción en texto plano y durante la auditoría se mostró en la salida de herramientas locales (también la del usuario de prueba `operador`). Cambiar ambas y dejar en el archivo solo usuarios de prueba.

- [ ] **Celular: pantallas que no se pudieron ver por falta de datos** (2026-11-15): FirmarLiquidacion con una orden pendiente, la pestaña Liquidar con viaje activo (fechas `.dc-par` y que el aviso no tape la barra `.dc-barra`), el rol OPERADOR (usuario de prueba sin vincular) y MovimientosViaje (servicio apagado en pruebas).
- [ ] **Celular: filtros antes del resultado** (2026-11-15): en ReporteCargas el listado empieza a ~3 pantallas (y=2902) y en el tablero de RegistroSeguimiento la tabla a y≈1151 (6 botones de exportación + filtros); DashboardExportacion tiene chips de 26 px y textos de 11.5 px. Rediseño (filtros plegables) pendiente de decidir.

- [ ] **Enter en formularios de alta**: decidido (orquestador, 2026-11-14) que no guarda, solo el botón; los dos modales de Saldos tampoco (mueven dinero). Confirmar con el usuario; si quiere que guarde en alguno, marcarlo con `data-enter`.
- [ ] **Edge rellena solo "Contraseña actual" en el modal del menú** (visto por el probador: con la contraseña guardada en Edge basta escribir la nueva dos veces). Es el comportamiento normal del gestor de contraseñas y quien usa ese navegador ya puede entrar con la guardada; decidir si se quiere evitar.
- [ ] **Probar a mano el envío del correo y el restablecimiento con un enlace válido**: con una cuenta de prueba que tenga correo (hoy solo 1 de 92 usuarios tiene; no usar la de una persona real). No se probó el SMTP ni el flujo completo.
- [ ] **Registrar correos de los usuarios** si se quiere que "Olvidé mi contraseña" sirva: hoy 91 de 92 no tienen; mientras tanto el administrador restablece desde Gestión de usuarios. Nota: cualquiera puede bloquear 1 h la recuperación de una cuenta conocida con 3 pedidos (costo normal del límite).
- [ ] **Probar a mano el cambio de contraseña con espacio final** (2026-11-14): el probador no pudo cambiar contraseñas (bloqueado por permisos). En el sitio local, por el modal del menú: nueva = actual + espacio → debe entrar escribiéndola sin el espacio; restaurarla después.
- [ ] **Facturas de flete de Vitapro por registrar** (al 2026-11-14 sin factura de flete): pedidos 4400134426, 4400134496, 4400134478, 4400134530, 4400134534, 4400134620, 4400134660, 4400134741, 4400134752, 4400134759 y 4400134786 (cargas del 31/10 al 11/11). En SUNAT (consulta del 25/10 al 14/11) hay 5 E001 nuevas a Vitapro del 13/11: E001-18727 a 18731. Además F319-00021039 de MALDONADO TORRES (24/10) sin factura de flete, y el pedido de prueba 9900000001.
- [ ] **E001-18719 anulada en SUNAT** (tiene nota de crédito de anulación) pero sigue activa en `FacturaFlete`: el pedido figura con 2 facturas (E001-18719 y E001-18725). Decidir: desactivarla (o que la pantalla permita anular una factura de flete) para que no cuente doble. La E001-18711 (también anulada) no se registró.
- [ ] **Mostrar la nueva pantalla a la administradora**: confirmar que "Descuentos y reintegros (quincena)" es el reporte que le piden (saldo por liquidación, por quincena, por conductor) y si la pestaña "Descuentos y reintegros" de Reportes → Liquidaciones y viajes (solo los ajustes de la revisión) se quita o se deja.
- [ ] **Borrar `sp_LD_ActualizarConductorDominanteViajes`** de la BD y del repo cuando el código nuevo esté publicado (ya no se llama).
- [ ] **Número de viaje por conteo** (`VP-AAAA-NNN` = cantidad de viajes del año + 1 en `sp_CrearDespacho`/`sp_CrearViajeProgreso`): si se borran viajes puede repetirse un número; pasar a un correlativo (hoy sin repetidos).
- [ ] **El día que los conductores empiecen a liquidar en el sistema**: poner `Liquidaciones.ConductoresEnSistema` en `true` en `Web.config` y publicar (hasta entonces sus viajes de liquidación se cierran solos en cada corrida del GPS automático, que necesita cron-job.org activo o que alguien abra el tablero). Ese día cada conductor empieza con el viaje de sus despachos desde esa fecha.
- [ ] **Inicio y término de descarga en Ecuador**: el tablero sigue avisando "Falta inicio y término de descarga". Preguntar al usuario si se estiman igual (desde el ingreso y la salida de la bodega) o se quita el aviso.
- [ ] **Borrador de la liquidación: probar en un celular real** (cerrar el navegador a mitad y volver a abrir; en Edge de escritorio solo se compiló).
- [ ] **Manual de uso del sistema** (pedido del usuario 2026-11-13, al terminar de regularizar desde octubre): cómo se trabaja de punta a punta, por rol. Cómo se programa (Word), cómo se suben los archivos (ZIP, PDF, Bandeja), cómo se vinculan los documentos al pedido y al despacho, seguimiento y GPS, facturas de flete, y del lado del conductor: liquidar el viaje, firmar y dar la conformidad desde el celular. Con capturas del sitio. Sirve para presentar el sistema y luego repartir las funciones a cada persona. Reemplaza la "guía de una hoja" de la puesta en marcha de conductores (ver más abajo).
- [ ] **Videos de uso con voz** (pedido del usuario 2026-11-13): grabaciones del uso real, con narración, un video por tema (programar, subir y vincular documentos, seguimiento, revisión y aprobación de liquidaciones, y el conductor liquidando desde el celular). Junta la tarea "Video de capacitación: ver si conviene uno por tema" (falta el viaje internacional, liquidación rechazada y corregida, retirar liquidación). Grabarlos cuando el flujo esté estable y con datos reales de octubre en adelante, no con los de prueba.

- [ ] **Retomar "Dinero y pagos del viaje" cuando el usuario lo decida** (apagada el 2026-11-13; ver `docs/archivo/HISTORIAL_2026.md`). Para activarla: `MovimientosViaje.Habilitado` en `true` en `Web.config` y publicar; revisar antes con el usuario si siguen valiendo las decisiones del 2026-11-11 (sin "Recibí" del conductor, registran administración y monitoreo, pago del saldo fuera). Mientras siga apagada, **no** aplicar la migración que borra `idConductorPortador` (ítem "Borrar `MovimientoDineroViaje.idConductorPortador`") sin volver a probar la pantalla.

- [ ] **Arreglo de la aproximación por grupo ya publicado (2026-11-14; el mapa del 8376 dice "Venía de descargar en Chao… y no entró a la base"); falta revisar que no volvieron horas GRUPO** en 7669, 7754, 7755, 7789, 7885, 7890, 8376 y 8381: el código publicado todavía aproxima a las unidades estacionadas; si las puso de nuevo antes de publicar, volver a correr `Database/Scripts/quitar-aproximados-grupo-con-gps.sql` y `herramientas/sitio-local/actualizar-gps.ps1 -Ids ...`.
- [ ] **El mapa del viaje (`ObtenerRuta`) toma la sesión mientras carga**: con la caché fría (primera apertura tras publicar) son hasta 10 s en los que otra pantalla del mismo usuario espera. Pasarlo a un servicio sin sesión de escritura si vuelve a molestar. Lo mismo vale para los Excel grandes.
- [ ] **`FacturasFlete.aspx` tardó 11 s una vez en el sitio local** (0,6 s las demás): no se pudo repetir; si vuelve a pasar, medir sus tres consultas.
- [ ] **`AgregarFactura.aspx` tiene scroll horizontal a 1366 px** (pantalla antigua).
- [ ] **Datos de prueba en la BD del sitio**: despachos 7–11 (pedidos 5544332211, 9900000001, guías GT231231, GR-E2E-0001…) y 2353–2355 (`demo-video`, EG07-00012345) salen en las listas y en los conteos de documentos; darlos de baja antes de que entren más usuarios.
- [ ] **Unidad sin equipo GPS (F6O-779)**: sus viajes no avanzan solos; el tablero podría marcarlos "sin GPS: llenar a mano" en vez de consultarlos en cada actualización.
- [ ] **Mapa del viaje: la primera apertura tras publicar o reciclar el sitio sigue lenta** (la caché en memoria está vacía y los días de hoy y ayer se piden al API): si molesta, guardar también el día en curso en disco con su hora.
- [ ] **Registro de despacho: vistas previas del ZIP y de la descarga** (2026-11-12): se ordenó la del Word; la del ZIP (Word + documentos) y la de "Registrar la descarga" conservan su diseño y no se probaron en esta ronda.
- [ ] **Lista de despachos: en "Registro manual" y en el detalle de un lote siguen arriba las 4 tarjetas de totales y la pestaña "Viajes activos" marcada**, que ahí no dicen nada: ocultarlas fuera de las dos listas.
- **Finalizar viaje y eliminar/anular lote usan `confirm()` del navegador** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] **Pasar a `data-confirmar` los otros 21 `OnClientClick="return confirm(...)"`** (activar/desactivar de catálogos, BuscarAbastecimiento, CargarExcel, DestinoDescarga, RegistroChoferes, el de GestionUsuarios.aspx.cs): usan el modal propio por el interceptor salvo que el botón valide (entonces sale el nativo). Quedan 3 `confirm()` nativos: AgregarAbastecimiento, DashboardGrifo (Grifo fuera de alcance) y `DashboardOperador.js`.
  - [ ] **Probar en el navegador los diálogos de RegistroDespacho** (Finalizar/Cancelar lote, Quitar, Reabrir) con un lote de prueba en curso: el probador no tenía lote abierto y no debía crear uno.
- [ ] **Mapa del viaje: las paradas se calculan en el navegador** (250 m, 30 min). Si se quieren en el Excel o en el dashboard hay que llevarlas al servidor (`ObtenerRuta`).
- [ ] **Borrar `MovimientoDineroViaje.idConductorPortador`** (y su FK) con una migración nueva, después de publicar el código de la revisión general: el sitio publicado todavía lee la columna.
- [ ] **Liquidación que el conductor nunca conforma**: hoy queda en `POR_CONFORMAR` sin plazo. Definir el procedimiento (recordatorio, plazo y cierre con constancia de que no firmó o con firma en papel adjunta).
- [ ] **Unificar el aspecto de las pantallas de administración** (pedido del usuario: "en algunas tiene un diseño y en otras otro"). Solo 6 pantallas usan `sgv-base.css`; el resto tiene ~20 encabezados distintos (`header-container`, `page-header`, `aud-page-header`, `rpt-header`, `gf-header`, `op-header`…), títulos en mayúsculas azules, paneles celestes y 13 scripts con `alert()`. Por fases, con captura antes y después: (1) encabezado y botones comunes en todas; (2) catálogos `Registro*` con una sola plantilla; (3) `Buscar*`/`Agregar*`; (4) tableros de grifo y operador; (5) `Dashboard.css` (1916 líneas).
- [ ] **Juntar pantallas que hacen lo mismo**: `BuscarFactura` + `AgregarFactura`, `BuscarCPIC` + `AgregarCPIC`, `BuscarAbastecimiento` + `AgregarAbastecimiento` y `BuscarOrdenViaje` + `AgregarOrdenViaje` (lista con buscar, nuevo y editar en una sola, como Facturas de flete); los 12 catálogos `Registro*` (misma grilla + formulario) sobre una plantilla común; `Auditoria` + `ConsultaAuditoria`; `Dashboard` + `Inicio`; `LiquidacionesAprobadasContabilidad` como pestaña de Gestión de liquidaciones. Los documentos del pedido ya viven en la Bandeja: `AgregarFactura`/`AgregarCPIC` quedarían solo para corregir.
- [ ] **PDF de la liquidación, mejoras de respaldo** (validar con el contador y el abogado laboral): la dirección de verificación sale del host de la petición (configurarla en `Web.config` para que nunca salga `localhost`); la sección "Comprobantes sustentatorios" lista los peajes con la fecha del envío y sin número (ocultarla si no hay comprobantes); citar el D.S. 029-2021-PCM junto al D.S. 052-2008-PCM y decir "firma electrónica" simple; enviar al conductor una copia del PDF firmado (correo o WhatsApp) para que la evidencia no dependa solo de la BD; para gastos en Ecuador sin comprobante, declaración jurada por día según el Reglamento de la Ley del Impuesto a la Renta, y planilla de movilidad para la movilidad local.
- [ ] **Test de integración** para la regla "Dinero entregado no menor que las entregas unidas" (va al final del test: el rechazo revierte la transacción) y limpiar los PDF que dejan los tests en `App_Data/OrdenesViaje` (órdenes revertidas).
- **Contraseñas** — lo hecho está en `docs/archivo/HISTORIAL_2026.md`; queda:
  - [ ] **Decisión del usuario: cuándo poner `Seguridad.ExigirPoliticaAlIngresar` en true** (Web.config). Al activarla, quien entre con una contraseña que no cumple queda en el modal hasta cambiarla (un conductor en ruta, desde el celular). Recomendado: al presentar el sistema a los conductores, avisándoles antes.
  - [ ] **Probar en el sitio la opción en true** con una cuenta de prueba de contraseña débil (requiere escribir datos de prueba; todas las cuentas de prueba actuales ya cumplen). Cubierto por pruebas xUnit (`ExigeCambioAlIngresar`) y de código fuente.
  - [ ] **Decisión del usuario: ¿prohibir contraseñas solo numéricas?** (un teléfono o `2580258025` hoy se acepta si no es secuencia ni repetición).
- [ ] **Aviso de datos personales y banco de datos ante la ANPD** (fuera del sistema): la firma registra nombre,
      DNI, trazo, IP y dispositivo (Ley 29733, D.S. 016-2024-JUS). Inscribir el banco de datos de trabajadores y
      entregar el aviso de tratamiento; revisarlo con el área legal.
- [ ] **Cláusula de autorización de descuentos en el contrato del conductor** (fuera del sistema, área laboral):
      la conformidad ya autoriza cada descuento, pero conviene tenerla también en el contrato.
- [ ] **Viajes a Ecuador: declaración jurada de alimentación y movilidad** (con el contador): SUNAT admite
      sustentar esos gastos en el exterior con declaración jurada hasta el 30 % del tope; definir si el PDF la incluye.
- [ ] **Respaldo de `App_Data/OrdenesViaje` fuera del hosting** (conservación mínima de 5 años, art. 87 del Código
      Tributario): hoy los PDF firmados solo están en el disco de somee.

- [ ] **Bandeja: CPIC de Vitapro unida a una carga de Polinplast** (visto el 2026-11-11): las C.P.I 0003886 y 0003888 (Vitapro,
      setiembre, factura F222-00008414/8417 que no está en el sistema) se proponían como "carga de POLINPLAST" porque una
      carreta de sus MCI coincidía con un despacho de Polinplast. No se subieron. La unión por carreta de un tercero debería
      exigir que el cliente o el remitente de la CPIC sea el del despacho.
- [ ] **MCI de la CPIC 0003923 (pedido 4400133436, descargas del 11/10)**: 10 MCI (0019468–0019477) quedaron sin registrar
      porque la bandeja dice que la CPIC "no está en el sistema": la CPIC 34 "0003923" tiene `idFactura` NULL desde la
      reimportación del 16/10 (la 24 quedó "-ANULADA"). Ponerle la factura F222-00008499 a la CPIC 34 y volver a subir los 10 MCI.
- [ ] **7 MCI que piden elegir el despacho** (tracto y carreta distintos de los despachos de su CPIC): 0019340 (CPIC 0003900),
      0019347 (0003902), 0019447 y 0019453 (0003921), 0019460 y 0019467 (0003922), 0019677 (0003950). Ver con el GPS o el Word
      a qué despacho fue cada unidad y subirlos eligiendo el despacho.
- [ ] **MCI 0019404 y 0019542: su despacho ya tenía un manifiesto de cruce**: ver si son un segundo ejemplar o si van a otro despacho.
- [ ] **CPIC de retornos y cargas de Ecuador sin despacho**: 0003933 (Tadel, factura 001-500-000000742), 0003942 (001-002-000005326),
      0003944 (Inbalnor, 001-002-000008619) y 0003945 (Vitapro Inb., 001-002-000008620), del 15 al 25/10, con 10 MCI
      (ASU-748, ASU-862, BSC-900, CBV-761, BFI-890, BNH-712, BPT-919, BFR-730). La bandeja no encuentra un despacho de carga
      para ellas: decidir cómo se registran los retornos Ecuador→Perú (ver memoria del retorno Inbalnor→Vitapro).
- [ ] **CPIC 0003954 y 0003952 (Fortidex)**: en la 0003954 no se lee la factura comercial (casilla 18) y la 0003952 solo existe
      como "CPI FORTIDEX 0003952.pdf". Fortidex no está en Clientes: decidir si se registra.
- [ ] **CPIC que solo están como borrador**: 0003892, 0003906, 0003964, 0003965 y 0003966 (las 3 últimas serían de los pedidos
      4400134741, 752 y 759). Pedir las firmadas a transportes.
- [ ] **Setiembre antes del 21/09 (fuera del período a regularizar)**: 20 CPIC (0003870–0003891, Vitapro, Cartavio y Novopan) con
      ~120 MCI y las facturas de flete E001-18557 a 18642 están en `docs/DOCUMENTACION/DOCUMENTOS TRANSPORTE`, pero sus cargas no
      están en el sistema. Solo si se decide regularizar setiembre.
- [ ] **CPIC y MCI del pedido 4400134786** (carga del 11/11) y descargas de ese pedido: llegan en el Word y el correo de los próximos días.
- [ ] **Retorno con carga hecho por otro tracto** (2026-11-10): en el Word del 08/11, IMPORTACION-TRUJILLO trae la
      carreta TBE-995 con **BNH-712** (Augusto Alcántara), pero su viaje de ida (7864) tiene a AVN-717, que ya volvió
      a la base el 04/11 y ahora descarga el pedido 4400134620. `sp_SE_RegistrarRetornoConCarga` solo anota el
      retorno; no guarda el tracto ni el chofer de la vuelta, así que el GPS no sigue esa vuelta. Decidir si se
      guarda (como el tracto 2 de la descarga) y si el retorno debe marcar la llegada a Trujillo.
- [ ] **Pedido 4400134786 (F222-00008630, 8 guías del 10/11) espera el Word del 10/11**: son las cargas del 11/11
      (BNG-758, BPT-788, BFI-817, ARN-913, TBM-815, CBU-928, CBX-832, AVN-758), aún sin programar. Con el Word,
      programarlas y luego `CompletarConCorreo` con `docs/DOCUMENTACION/correo-2026-11-10` (ya está el ZIP).
- [ ] **¿E001-18719 y E001-18725 cobran la misma carga dos veces?** (2026-11-10): las dos son por F222-00008561 (TG 1300232,
      CPI 0003949, 237,6 t, USD 14.731,20); solo cambian la fecha (05 y 06/11) y la OS (4301332252 y 4301332251). Vitapro
      recibió las dos en eBIZ. Si la 18719 se reemplazó por error de OS, falta su nota de anulación (NCE) en SUNAT y darla de
      baja en Facturas de flete; si no, se cobró dos veces. Decisión de la oficina.
- [ ] **Afrecho del 24/10 (Maldonado, Paita: AVM-877 y F6O-779) sin factura de flete**: no hay nada en el correo de transportes.
      Desde el 2026-11-11 ya tiene su CPIC 0003948 (Alicorp-Preciado, factura comercial F319-00021039, US$ 2 160) y sus 2 MCI;
      falta solo la factura de flete nuestra.
- [ ] **CPI de los pedidos 4400134741, 752 y 759**: todavía no se envían en el correo (los hilos solo tienen factura,
      PL y guías). Subirlas por la bandeja cuando transportes las mande.
- [ ] **Documentos en espera en la bandeja** (propuesta del 2026-11-09, por diseñar): guardar en la BD lo que no se pudo unir y reintentar la unión cuando llegue el Word, el ZIP o la CPIC, para que no importe el orden de llegada ni haya que volver a subir nada. Hoy lo no registrado se avisa, pero se pierde.
- [ ] **Bandeja como entrada única**: que acepte también el Word de programación (hoy solo en Registro de despacho).
- [ ] **Guía con fecha posterior a la programada**: la ventana (`EnVentana`) no ve el programado del día anterior y propone un despacho nuevo: riesgo de duplicado (0 casos en 60 días, revisión aproximada del 2026-11-09). Ampliar 1 o 2 días hacia atrás y avisar si esa carreta ya está programada.
- [ ] **Reporte de cargas sin el MCI**: `ReporteCargasService` tiene sus propios conteos de documentos (tampoco usa el perfil del cliente) y no cuenta el manifiesto. Alinearlo con `sp_LD_ObtenerLotesRegistrados`.
- [ ] **18 despachos de descarga de Vitapro de octubre sin MCI** (visto el 2026-11-09, al contar el manifiesto como pendiente): subirlos por la bandeja junto con el histórico de CPIC y MCI. El 2026-11-11 se subieron 249 MCI: volver a contar en el tablero cuáles siguen sin MCI.
- [ ] **Cambiar la contraseña FTP de somee** (`SGVTRANSPORTES`): se escribió en un chat el 2026-11-06 para correr `subir-uploads.ps1`. Cambiarla en el panel de somee y volver a guardarla con `respaldar-archivos.ps1 -GuardarCredencial`.
- [ ] **Cargas históricas de terceros y el tablero**: al registrar despachos pasados con `TercerosConDocumentos`, `sp_SE_SincronizarDespachos` crea sus viajes EN_CURSO; cerrarlos después con `sp_SE_CambiarEstado` (se hizo con los 75 de 2026-11-06) o que la herramienta lo haga sola.
- [ ] **Pantallas pendientes de pulir**: la celda de contacto de Registro de clientes (texto más bajo que el resto de la fila) y la vista del conductor en un celular real. Revisados en Edge el 2026-11-06: admin de sistema (panel, Gestión de usuarios con botones de acción en una línea, Auditoría), contabilidad y conductor (escritorio); las grillas GridView llevan un borde suave en todo el sistema. Grifo y maquinaria están fuera de alcance.
- [ ] **GRE de Camafra Motors (EG03-15365, 01/10) y de Edificaciones & Construcciones del Norte (EG03-15827, 07/11)**: no son clientes del sistema (la segunda sin carreta). Decidir si son fletes a registrar como clientes nuevos.
- [ ] **ZIP de pedidos de Vitapro que no están en el sistema**: 4400132346, 132431, 132479, 132540, 132580 (semanas 37–38, setiembre). La bandeja propone despachos nuevos y sus facturas; no se subieron. Decidir si se registra setiembre. (El 4400134786 ya se registró con el Word del 10/11.)
- [ ] **9 GRE de Polinplast de setiembre sin PDF** (EG03-15116 a 15340, despachos 3038–3046): son anteriores al 01/10, inicio de la descarga de SUNAT. Bajarlas de SUNAT (Consulta de GRE, 01–30/09) y subirlas por la bandeja.
- [ ] **Expediente completo muestra igual "Para completar, suba…"**: ocultar esa ayuda cuando no falta nada (detalle de texto en `BandejaDocumentos.aspx.cs`).
- [ ] **GRE de Vitapro (494) sin subir**: la bandeja guardaría su número y PDF en cada carga; el perfil PEDIDO no las exige. Decidir si se quieren en el expediente.
- [ ] **Quitar el panel viejo "Documentos del lote" de ListaDespachos** cuando el expediente esté probado en el sitio: hoy solo se llega con `?manual=1&lote=` (registro a mano de números sin documento). Pasar ese registro manual al expediente y borrar `pnlManifiestosLote` y su código.
- [ ] **Subir un PDF suelto desde el expediente** (guía escaneada, MCI de un conductor): hoy todo entra por la zona de subida de la bandeja, que lo reconoce por su contenido; un escaneo sin texto no se reconoce y solo queda el registro manual.
- [ ] **Despachos de Polinplast creados desde la GRE sin viaje** (3543–3548, 13 al 15/10, más los 16 de setiembre–octubre): no tienen viaje del chofer ni GPS. Ver si hace falta su viaje en el tablero.
- [ ] **E001-18698 (Polinplast, S/ 1 652)** dice "a Ascope - Casa Grande", pero su GRE EG03-15315 (BNH-865, 28/09) llevó la carga a **Olmos** (Plantaciones del Sol); la tarifa (S/ 1 400) no es la de Casa Grande (S/ 1 950). Confirmar con quien factura.
- [ ] **E001-18693 (Polinplast) cita la guía del remitente "T700-0000651"**: le falta un dígito; su GRE EG03-15200 confirma que es la **T700-00001651**. La conciliación ya la une por nuestra guía de transportista (con aviso). Avisar a quien factura.
- [ ] **E001-18689 (Crisar, US$ 6 500): sobrestadía** de 10 y 3 días de 5 unidades paradas en la bodega de Ecuador (C.P.I 0003880 y 0003887, de setiembre): no es flete de una carga y sale "sin carga". Decidir si la conciliación muestra aparte los cobros que no son de una carga (sobrestadías, falsos fletes).
- [ ] **Novopan del Ecuador no está en Clientes** (RUC 1790374343001): su factura E001-18665 queda sin cliente (se concilia igual por la factura comercial). Decidir si se registra como cliente.
- [ ] **Publicar los PDF de terceros**: CPIC y MCI de Crisar/Novopan y las 37 facturas quedaron en `WebSGV\Uploads\CPIC|Manifiesto\2026\11\` y `Uploads\FacturasFlete\2026\` de la PC de la oficina: copiarlos por FTP.
- [ ] **PDF escaneados (sin texto):** 11 de la carpeta de documentos (MCI de Novopan con nombre "MCI FACTURA …") son imágenes:
      la bandeja no los puede leer. Adjuntarlos en el panel del lote o, si se vuelven frecuentes, agregar OCR.
- [ ] **Generar la CPIC y el MCI desde el sistema** (hoy se arman en Excel): el sistema ya tiene todos sus datos.
- [ ] **Capacidad del hosting (medida el 2026-11-05)**: sgvActualizada (la BD del sitio) tiene tope de **500 MB de datos**
      y 30 MB de log (modo SIMPLE, sin riesgo de log lleno); usa 57 MB, de los que ~30 MB son el **Query Store**, que puede
      crecer hasta 1000 MB (más que la BD). Crecimiento estimado con todos los choferes liquidando (~300 liquidaciones al
      mes, 2 firmas PNG de ~11 KB cada una; ~300 despachos, seguimiento, hitos y auditoría): **11–16 MB al mes → 2 a 3 años**.
      Decidir: (a) bajar el tope del Query Store a 100 MB (`ALTER DATABASE ... SET QUERY_STORE (MAX_STORAGE_SIZE_MB = 100)`)
      y (b) pasar a **sgvTransporte** del plan Premium (2000 MB de datos, 1000 MB de log, 4 GB de respaldos; hoy sin uso).
      El disco del sitio (Premium) tiene 10 GB y usa 237 MB; los PDF de liquidación pesan ~300 KB → ~110 MB al mes con los
      documentos subidos (la caché GPS se borra a los 30 días): ~7 años. El plan Premium de 6 meses tenía una factura
      pendiente en somee (US$ 51,90): si no se paga, se cae el sitio.
- [ ] **Subir las facturas de flete de cada semana** en Reportes → Facturas de flete (las de octubre ya están en
      sgvActualizada). Al publicar, subirlas también en el sitio publicado si apunta a otra BD.
- [ ] **Confirmar con Vitapro (o quien factura) las placas de las guías**: las guías T660-00016196 (BPT-919), T660-00016305
      (BNH-795) y las de los pedidos 4400133403 / 4400133406 salieron con unidades que no cargaron (lo cargado se registró
      con el Word y el GPS). Si alguien revisa las guías contra el despacho verá placas distintas: ya está anotado en la
      observación de cada despacho.
- [ ] **E001-18645**: el crédito dice US$ 12 889,20 y el total US$ 12 889,80; revisarlo con quien factura (¿nota de crédito?).
- [ ] **E001-18649 menciona la factura comercial F222-00008441**, que no existe en los ZIP; el pedido **4400132845**
      (F222-00008443, 8 unidades, 24/09) sale "por facturar" por US$ 14 731,20, exactamente lo que cobra la E001-18649.
      Casi seguro es un error de tipeo en la factura: confirmarlo y, si es así, decidir si se emite una nota o solo se une
      a mano (falta la opción de corregir la factura comercial de una factura de flete en la pantalla).
- [ ] **Datos de prueba viejos en la conciliación**: lotes "sin documentos" del 04/09 (pedido 5544332211, F222-12344),
      12/09 (4400132174, F232-00002311) y 09/10 (9900000001, F999-00000001): decidir si se anulan.
- [ ] **Cobranza:** registrar los pagos de las facturas de flete (fecha y monto pagado, saldo, vencidas). Hoy solo se
      muestra el vencimiento. Falta decidir de dónde sale el pago (estado de cuenta del banco, aviso del cliente).
- [ ] **Correlativos de CPIC:** registrar cada CPIC usado (de qué pedido o cliente, o anulado) para ver los números que
      faltan. En las facturas de octubre a Vitapro se usaron 31 CPIC entre el 0003892 y el 0003946 y quedan 24 números
      sin explicar (los de otros clientes o anulados).
- [ ] **"Carga para exportación" se deduce del último despacho del cliente y planta** (`ProgramacionDespachoService`,
      `p.EsExportacion`): los 3 despachos de prueba de `demo-video` del 03/11 (A0Z-931) hicieron que la programación por
      Word del 03 al 05/11 quedara sin exportación y sin viaje en el tablero (se corrigió a mano el 2026-11-05, 27
      despachos). Deducirlo de la ruta del Word o del cliente, y decidir si se borran los 3 despachos de `demo-video`.
- [ ] **Ver la pantalla nueva en un celular real** (2026-11-03): en Edge se probó en ancho de escritorio (la ventana
      estaba maximizada) y en 390 px con capturas; falta un equipo real: teclado numérico, la barra fija del total,
      la hoja de resumen desde abajo y la firma con el dedo en un equipo de gama baja.
- [ ] **Decidir de dónde sale el dinero entregado** (2026-11-03, decisión de negocio): hoy la administradora lo escribe
      al aprobar. Si el adelanto se entrega al programar el despacho, convendría registrarlo ahí (o traerlo de
      caja/contabilidad) y que la revisión solo lo confirme.
- [ ] **Salida/llegada que el seguimiento no marca** (2026-11-03): todos los viajes abiertos tienen seguimiento, pero
      los que empiezan con un tercero (Crisar en Cartavio, Polinplast en Chapaira) no tienen salida de la base, y los
      que terminan en la cochera o en Vitapro Trujillo no tienen llegada: ahí el chofer las escribe. Se podría buscar
      en el historial del GPS la última salida de la base Sullana antes de la carga (como hace `HoraLlegadaGpsService`
      con la llegada).
- [ ] **Video de capacitación: ver si conviene uno por tema** (2026-11-03): solo cubre el viaje nacional sin peajes ni
      hospedaje/reparaciones; faltan el viaje internacional (peajes en soles y dólares), "liquidación rechazada y
      corregida" y "retirar liquidación".
- [ ] **Datos de ejemplo del video en la BD de pruebas** (2026-11-03): viajes 1252–1254 / despachos 2353–2355 del
      conductor de prueba y sus órdenes `OV-2026-000003` a `OV-2026-000005` (firmadas y pendientes de aprobar). Aprobarlas o
      dejarlas; no son datos reales. También el viaje `VP-SIM-1111` y la orden `OV-2026-000006` (aprobada) de la revisión general del 2026-11-11 (salió del historial al archivar).
- [ ] **Hosting: evitar el arranque en frío** (2026-11-03): el primer acceso tras un rato sin visitas tarda ~4 s
      (el primer PDF ya no suma: se precalienta). Poner un ping cada 5–10 min a `Views/Login.aspx` (p. ej. UptimeRobot;
      es un servicio externo, no se puede hacer desde el código) y cronometrar una firma real en el hosting, para
      confirmar que las idas y vueltas a la BD son menores que desde la oficina.
- [ ] **Conductores: puesta en marcha con su propio usuario** (2026-11-03): piloto con 3–5 conductores, capacitación
      corta con su celular, guía de una hoja con capturas, periodo de convivencia con carga en papel y un
      responsable de soporte por WhatsApp. En el sistema: guardar un borrador del formulario de liquidación (hoy solo avisa
      al cerrar) y probar la vista de celular y el lienzo de firma en equipos de gama baja (el formulario ya se
      simplificó para el celular el 2026-11-03 y el video de capacitación ya se regrabó con esa versión).
- [ ] **Confirmar en el servidor qué BD usa el sitio publicado** (2026-11-03): el repo lo apunta a `sgvActualizada`
      pero no puede ver el archivo del servidor; antes del paso a producción real, comprobarlo.
- [ ] **Reporte de cargas: probar en el navegador y publicar** las pestañas, los dos gráficos (tooltip, ancho de celular)
      y que la pestaña se mantenga al buscar; solo se probó por HTTP (página, Excel y PDF).
- [ ] **Reporte de cargas: más pestañas si se piden** (2026-11-02): el usuario eligió solo "Resumen"; quedaron
      propuestas "Facturas" (una fila por factura con valor, unidades y si tiene PDF) y "Documentación" (cada
      despacho pendiente con su antigüedad), y el detalle propio por producto/cliente.
- [ ] **Reporte de cargas: peso de los terceros** (2026-11-02): Crisar, Polinplast y el afrecho no tienen líneas de
      producto (`DespachoCarga`), así que salen "sin detalle" y no suman toneladas ni entran al gráfico por cliente.
      Decidir si se registra su peso (de la guía o la balanza) para que el reporte refleje toda la carga.
- [ ] **Interfaz de documentos (2026-11-03): probar en el navegador lo que solo se compiló**: registrar una factura y un CPIC de prueba desde la tarjeta, el paginado (pasar a la página 2 con un filtro puesto, que el pie no quede tapado por la columna fija de acciones) y el ancho de página; el Chrome se congeló antes de verlo.
- [ ] **Interfaz de documentos: decidir** si "Con documentos pendientes" queda como filtro por defecto al abrir Lotes registrados, si se compactan las 13 columnas de la lista (Operación, Ámbito, Planta y Creado) y si el número de pedido también se anota desde "Documentos" (hoy solo en Editar).
- [ ] **Registro de despacho: tira de pasos** ("1 Word → 2 ZIP → 3 Revisar pendientes") y botón "Ver lotes pendientes" al terminar de programar, para el usuario que no usa la terminal.
- [ ] **Documentos pendientes: probar en el navegador** el recuadro, el filtro y "Documentos" del lote (adjuntar un PDF
      de factura y guardar una guía de prueba); solo se probó por HTTP y con el test de integración.
- [ ] **Documentos pendientes: limpiar lo histórico** (2026-11-02): de los 42 lotes pendientes, 38 tienen más de 3
      días; muchos son de agosto–septiembre (pruebas antiguas, "Falta guía · Sin PDF de factura") y de octubre sin PDF
      de factura (ver "PDF de las facturas de la carga histórica"). Decidir con la oficina cuáles anular, completar o
      dejar, para que el recuadro muestre solo lo real.
- [ ] **Documentos pendientes: ¿qué documentos pide cada cliente tercero?** (Crisar, Polinplast, MALDONADO/afrecho):
      hoy se les piden pedido, factura y guía como a Vitapro, y salen pendientes. Si alguno no trae pedido o guía,
      ajustar la regla en `sp_LD_ObtenerLotesRegistrados`/`EstadoDocumental` por cliente.
- [ ] **Guía o factura escritas a mano en una carga programada**: el ZIP ya no reconoce esa carga
      (`FiltroSinDocumentos` exige que no tenga guía ni factura) y crearía otro despacho. Hoy solo hay un aviso en
      "Documentos". Si pasa, hacer que el ZIP complete también una carga con la misma guía ya escrita.
- [ ] **Facturado vs cargado** (fase 2 del control de documentos): guardar sacos y kg de la factura al leer el ZIP
      (migración nueva en `Factura`; los pesos del CPIC hoy se graban en 0) y un reporte por cliente/pedido que
      compare con `DespachoCarga`. Requiere saber qué trae la factura de cada tercero.

- [ ] **Retorno con carga: documentos y datos que faltan** (2026-11-02): el flujo solo anota en el viaje de ida que la
      unidad vuelve con carga y a qué hora. Falta decidir con la oficina: (1) remitente/cliente de la carga (Inbalnor,
      Novopan, harina de Manta…) y dónde descarga exactamente (¿planta Vitapro Trujillo?); (2) si el chofer cobra o se
      liquida el retorno como un despacho aparte (hoy no genera despacho ni toca la liquidación); (3) dónde guardar la
      factura y el CPIC de retorno (ver "Generar el CPI y el manifiesto"); (4) que el GPS detecte la llegada y descarga
      en Trujillo (hoy el viaje sigue como "Retorno de Ecuador" hasta cerrar en la base) para dar la hora real; (5) si el
      retorno puede salir con otro tracto o chofer que el de ida (hoy se une por carreta o tracto y no guarda el relevo).
- [ ] **Descargas de Guayaquil de los pedidos 4400134233, 4400134236, 4400134238, 4400134293, 4400134335, 4400134386
      y 4400134426** (Words 26-10 a 01-11): sin registrar. **Desde el 2026-11-02 sus cargas ya tienen guía y factura**
      (ZIP del correo), así que ya se puede registrar cada descarga con el Word ("Registrar la descarga de un pedido ya
      cargado"); igual 4400134113 y 4400134496.
- [ ] **Subir por FTP los 23 PDF de factura** de `WebSGV\Uploads\Factura\2026\11\` (PC de la oficina) a la misma ruta
      del sitio publicado: la BD (sgvActualizada) ya los tiene registrados y, hasta copiarlos, el sitio dice "el archivo
      no existe" al abrirlos. También falta en el sitio el de F222-00008545 (ver "PDF de las facturas de la carga histórica").
- [ ] **RegistroDespacho: la guía no encuentra su programado si Vitapro la emite 2 días antes de cargar**
      (2026-11-02): `ImportacionPedidoService.UsarProgramados` busca programados hasta 1 día después de la guía (las
      del 27/10 son de la carga del 29/10 y las del 28/10 de la del 30/10), y sin Word un mismo tracto programado dos
      veces en la ventana queda ambiguo. Con los ZIP del 02/11 se usó `herramientas/carga-historica-correo`
      (`CompletarConCorreo`: mismo tracto de la guía a +2 días, el más próximo, pedidos en orden). Llevar esa regla a la
      pantalla con tests (`ProgramacionCargaTests`) para que leer un ZIP suelto no deje guías "sin programado".
- [ ] **Septiembre sin despachos**: el seguimiento tiene 95 viajes de Vitapro del 16/09 al 30/09 sin despacho de carga
      y en `docs/DOCUMENTACION/informacion` están 10 de sus ZIP (pedidos 4400132637 a 4400133136, guías del 18/09 al
      29/09; faltan los de 4400132479, 4400132540 y 4400132580). El usuario pidió solo octubre (2026-11-02); si se
      quiere, `CargaHistorica.cs` con la ventana cambiada a septiembre. Los 8 viajes del 30/09 siguen EN_CURSO.
- [ ] **Tracto 2 y chofer de descarga del STATUS GENERAL** (2026-11-02): desde la programación del 30/10 salían vacíos porque la descarga
      se registra con el pedido (ZIP) y esos viajes aún no lo tienen. Los 22 viajes del 30/10 y 31/10 se completaron con las secciones
      GUAYAQUIL-VITAPRO de los Words 30-10 y 01-11 (`Scripts/completar_descarga_viajes_2026-10-30_31.sql`, solo pruebas). Faltan los 8 del 02/11
      (sus descargas llegarán en los Words de los próximos días) y, para todos, registrar el despacho de DESCARGA cuando lleguen los ZIP.
      Idea: que "Programar con el Word" complete tracto 2 y chofer de descarga del viaje por la carreta sin esperar el pedido.

- [ ] **Probar el dashboard de exportación nuevo en el navegador y publicarlo** (2026-10-29): se probó por HTTP y con capturas de Edge sin interfaz (escritorio y 576 px) contra sgvActualizada; falta revisarlo a mano (clic en tramos, tooltips) y volver a publicar.
- [ ] **Registrar inicio/término de carga y descarga si se quieren separar la espera de la carga** (2026-10-29): hoy el dashboard mide la planta de ingreso a salida porque esas horas no se llenan (ni el GPS las ve). Decisión del negocio.
- [ ] **Generar el CPI y el manifiesto (MCI) para quitarle trabajo a la administradora de transporte** (2026-10-23,
      pedido del usuario): hoy los arma a mano en Excel (plantillas + `BASE CHARITO`, que es un .xls con choferes,
      tractos y carretas: chasis, año, certificado CAN, libreta de tripulante) y el sistema solo guarda el escaneo.
      Análisis hecho con 51 CPI (0003893–0003945) y 288 MCI del 22/09 al 23/10, sacados de su carpeta de escaneos
      (`docs/DOCUMENTACION/DOC TRANSPORTE/DOCS TRANSPORTE`, gitignored, solo en la PC de la oficina; tiene también
      documentos ajenos y sensibles: abrir solo los C.P.I / MCI). Reglas encontradas:
      - Un CPI por factura y un MCI por camión; el CPI suma los sacos y pesos de sus MCI. Correlativos propios de ella.
        Si hay varias versiones del archivo vale la última modificada; el BORRADOR se hace al llegar la factura, sin el
        día, y el día se pone cuando salen los camiones.
      - Vitapro ida: todo sale de la factura F222 + packing list (casilla 16 = total CPT, flete = TOT. CARGO, bruto =
        total del packing list). Flete por camión 1188 bolsas × 1,55 = 1841,40; tramos 0,725 / 0,25 / 0,575 por bolsa.
      - Cartavio: 620 bolsas de 50 kg por camión (+0,2 kg de tara), 2540 por camión + 50 de otros gastos por CPI.
        Novopan (Quito → Piura/Trujillo, CPT Tumbes): 14 pallets y 715 por camión, volumen en m3 y anexo de productos.
        Inbalnor → Vitapro: 1300 sacos de 25 kg (+0,1 kg), 1700 por camión. Tadel / Productos Pesqueros (harina de
        Manta → Vitapro): 2000 sacos, 7200.
      - El flete va en la columna del remitente (Vitapro ida, Cartavio, Novopan) o del destinatario (los que traen
        carga a Vitapro).
      - Errores de copia a mano encontrados: año 2025 en el CPI 0003942 y en 6 MCI de Novopan, tramos que no suman
        (0003925), bruto mal escrito en un borrador (17 892,80), manifiesto con "MONOFASICO" en vez de "MONOFOSFATO".
- [ ] **CPI: respuestas de la administradora** (PDF con capturas en `docs/DOCUMENTACION/DOC TRANSPORTE/PREGUNTAS CPI Y
      MANIFIESTOS.pdf`, gitignored): ¿Cartavio subió a 2600 por camión? (0003936: 7 camiones, 18 200); ¿cuántos camiones
      y dónde están los MCI de la harina de Manta (0003933, 0003942)?; ¿cómo decide la columna del flete?; ¿qué fecha
      va en los retornos (Inbalnor: 22/10, el camión salió de Trujillo el 21/10)?; ¿qué le quita más tiempo?; ¿se anuló
      el 0003899? Con eso, diseñar el generador (plantilla por cliente/ruta; ella pone el número y el día de salida).
- [ ] **Contemplar el retorno Ecuador → Perú en el flujo** (desde el 22/10/2026): Novopan, Inbalnor y la harina de
      Manta son carga de vuelta de los mismos camiones que bajaron con Vitapro 1–4 días antes.
- [ ] **Jave: confirmar la regla con la administradora**: la llegada a planta se dejó en la primera llegada al
      acceso (cuando llegó, aunque después esperara); el ingreso es la última entrada tras más de 1 h afuera. Si la
      oficina quiere la llegada también en la última entrada, o si alguna espera dura menos de 1 h (BSD-716 el
      17/10: entradas 14:32 y 15:17 con 41 min afuera; se tomó 14:32), ajustar `MinutosEsperaAfuera`. Revisar con
      ella ASU-748 y ASU-862 del 22/10 (viajes 7341 y 7342): descargan 12:29–14:32, esperan 5 h afuera junto al
      acceso y vuelven a las 20:28 por 5 min antes de irse; quedó ingreso 12:29 y salida 20:34 (antes 14:32).
- [ ] **Publicar el código de "entra y sale a esperar"**: el sitio publicado sigue con la regla anterior; los viajes
      nuevos en Jave volverían a quedar con el primer ingreso hasta publicar (los de octubre ya se recalcularon).
- [ ] **GPS de BFR-730 sin señal**: no manda un solo punto desde el 16/10/2026; mientras tanto va aproximado con su
      grupo. Avisar a quien ve los equipos GPS (Onway) para revisarlo.
- [ ] **Hora al grifo de Crisar: volver a medir** con `Generar-PerfilCrisar.ps1` cuando haya más días (hoy 16 días de
      cola): el error típico pasó de 1,7 h a 1,5 h; lo que queda es cuándo empieza a descargar cada día (2,3–6,7 h
      después de llegar) y las que salen tarde y duermen antes del grifo. Si la oficina sabe por qué hay días lentos
      (otros camiones en la fila, horario de la bodega), eso se podría usar.
- [ ] **Regenerar el perfil de Crisar cada mes** con `herramientas/perfil-llegada/Generar-PerfilCrisar.ps1` y los
      Word nuevos (siempre después de `Generar-Perfil.ps1`, que reescribe el archivo entero).
- [ ] **Llegada estimada: comparar con la llegada real** unos días (lo que dijo el reporte a cierta hora vs la
      llegada que marcó el GPS) y ajustar la ventana de km/hora si se desvía; regenerar `PerfilesLlegada.json`
      cada mes con `herramientas/perfil-llegada/Generar-Perfil.ps1` (y recompilar: va dentro de la DLL).
- [ ] **Reporte del grifo sin abrir el tablero**: con el GPS automático los datos ya están al día, pero el texto
      se arma al abrir RegistroSeguimiento. Para que el grifo lo reciba solo falta un envío (correo o WhatsApp).
- [ ] **Grifo Santa Rosa: confirmar con Eliana y el grifo** el formato del reporte y si necesitan algo más (galones/importe del
      abastecimiento, quién lo autoriza) o si alguna unidad carga en otro grifo.
- [ ] **Llegada estimada para terceros** (Crisar a Cartavio y a La Troncal, Novopan, Polinplast): hoy solo
      Vitapro (carga a Trujillo, descarga a Jave/Inbalnor). Mismo método con sus viajes del seguimiento.
- [ ] **Llegada estimada desde TCI y el Complex**: hoy no se estima mientras la unidad espera en la aduana
      (depende del trámite). Ver si con más datos conviene dar un rango igual.
- [ ] **Excel de STATUS: agregar el N° de factura** del cuadro de la administradora (hoy el seguimiento no lo guarda;      está en la factura del despacho de carga, se puede traer con el `idDespachoOrigen`).
- [ ] **Publicar** (la BD del sitio ya tiene la migración 33) el código del STATUS de la unidad, la cola de Trujillo, la salida de bodega en Ecuador y el GPS en paralelo: hasta
      entonces el sitio sigue mostrando "Rumbo a Trujillo" a las unidades en la cola. (El viaje 7381 ya se corrigió
      con `corregir_seguimiento_7381_2026-10-21.sql` el 21/10 y el GPS nuevo, corrido desde el sitio local, ya llenó
      las llegadas a Trujillo en sgvActualizada.)
- [ ] **Registrar las descargas del 21/10 que no tienen viaje**: AMQ-859 (salió de base 10:55), ARN-917 (07:45) y
      ASU-748 (en la noche del 20/10) van a Ecuador sin despacho de descarga; las cargas del 20/10 con pedido
      `PENDIENTE` (BNG-758, ASU-862, TBM-815, CBU-928, BPT-788, BPS-816) siguen "Descarga sin tracto asignado".
      Con el Word de la descarga, como el pedido 4400133879 (los Word del 20 y 21/10 no estaban en la PC de la oficina).
- [ ] **GPS de BFR-730**: Onway no tiene ningún dato suyo en septiembre ni en octubre (descargas del 02, 07, 09
      y 17/10 sin trazabilidad). Revisar con Location World si el equipo funciona o si está registrado con otra placa.
- [ ] **F60-779 no está en Onway** (carga del 03/10, pedido 4400133288): confirmar si tiene GPS o con qué placa.
- [ ] **Cuadro de octubre, revisar con la administradora**: carreta T0Q-991 en dos cargas seguidas (AVO-818 el
      09/10 y ANU-794 el 10/10; el GPS ve a los dos en Vitapro, uno de los Word tiene mal la carreta; AVO-818 quedó
      sin viaje); BPT-919 con T4G-972 del 02/10 (el GPS no lo ve en Vitapro: ¿no fue?); BFK-729 con TDX-994 del
      15/10 (descarga completa, el GPS no ve la carga); BPS-816 → C4Z-745 del 15/10 (la descarga se corta después
      del CEBAF del 16/10).
- [ ] **Cuadro de octubre: 2 cargas sin pedido no se crearon** (AVO-818 del 09/10 y BPT-919 del 02/10). Las 6 del
      20/10 ya se crearon a mano (7341–7346, pedido `PENDIENTE`): al registrar su despacho, la sincronización las adopta.
- [ ] **CPIC del pedido 4400133879**: su descarga (despachos 820–825 del 21/10) se registró sin CPIC; cuando
      llegue, agregarlo al lote en ListaDespachos (editar lote → CPIC).
- [ ] **Publicar el cambio del GPS de los desvíos** (`SeguimientoExportacionGpsService`: la planta donde entra la
      unidad reemplaza la bodega ya puesta). Los dos SPs ya están en sgvActualizada; hasta publicar, si una unidad
      se desvía sin que se cargue la lista, el sitio sigue mostrando la planta anterior.
- [ ] **Planta del despacho de descarga vs lista de destinos**: el despacho de descarga del pedido 4400133879 (820–825)
      y del 4400133881 (874–879) sigue con planta Milagro aunque 5 unidades van a Jave; el seguimiento ya está bien.
      Decidir si la lista también debe cambiar la planta del despacho (ListaDespachos agrupa el lote por planta y
      la liquidación podría usarla).
- [ ] **Desvío avisado tarde**: si la lista llega cuando la unidad ya entró a la planta, el SP no la toca (manda el
      GPS). Confirmar con transporte si hace falta avisar en el tablero cuando la lista y el GPS no coinciden.
- [ ] **Despacho en dos momentos: probar en el navegador** la vista previa (select2, avisos) y "Completa el
      despacho" al leer el ZIP. "Programar despachos" ya se usó por HTTP el 2026-10-21 con el Word del 20/10
      (11 cargas del 21/10 en sgvActualizada).
- [ ] **JUSTO CESPEDES (ANU-794 / T3Y-995, 05:40 del 21/10) sin programar**: no está en el catálogo de
      conductores y su DNI no figura en ningún documento (solo el nombre corto en los Word del 18–20/10; maneja
      ANU-794 desde el 18/10). Con su DNI y nombre completo: registrarlo en RegistroChoferes y volver a subir el
      Word del 20/10 en RegistroDespacho (las otras 11 salen "Ya programado"; se programa solo la suya).
- [ ] **Afrecho: preguntar a la oficina** qué moneda se le factura (quedó PEN) y si lleva guía o CPI/MCI; y dónde
      descarga el viaje de octubre: AVM-877 está en TCI desde el 26/10 14:41 (los anteriores descargaban en
      Huaquillas sin pasar por TCI; el comprador de octubre es de Balsas). Cuando salga, revisar que el GPS ponga
      la descarga correcta y, si es otro lugar, crear su geocerca.
- [ ] **Ruta IMPORTACION (Jave → Trujillo)**: decidir con la oficina si es un viaje aparte del seguimiento o el
      tramo de vuelta del viaje de exportación (mismo camión; carga en Jave en la misma visita de la descarga) y
      qué cliente / documentos lleva. Hasta entonces "Programar con el Word" no la programa. Ver
      `docs/RUTA_AFRECHO.md` sección 3.
- [ ] **Despachos de descarga de los pedidos 4400134113, 4400134233, 4400134236 y 4400134238**: el seguimiento ya
      sigue al tracto de descarga, pero el despacho de DESCARGA (liquidación del chofer de descarga, CPIC) se
      registra como siempre cuando llegue el ZIP de cada pedido (completa sus cargas) y después con el Word en
      "Descarga por pedido".
- [ ] **"Actualizar GPS" del detalle de un viaje borra el aproximado con el grupo** (`gpsAproximadoDe = NULL` y
      la posición vuelve al tracto con GPS) hasta la siguiente actualización de todos (10 min con cron-job.org).
      Decidir si el detalle también debe aproximar con el grupo.
- [ ] **F6O-779 sin GPS** (ARMANDO FRANCO, viaje 7786): no tiene equipo en Onway. En la captura del usuario sale
      **A4A-809** en Paita (parado en -5.1007, -81.0737 desde el 23/10 18:24, a 1,4 km de Alicorp): confirmar si
      ese equipo va en F6O-779 o si A4A-809 es otra unidad. El aproximado con su grupo no lo cubre (pide 2
      compañeras con señal y el afrecho solo tiene a AVM-877): decidir si con 1 basta para el afrecho.
- [ ] **Octubre: viaje 7221 sin despacho** (03/10, F60-779 / T7A-996, "ARMANDO FRANCO"; guía
      T660-00016228 de FRANCO MOGOLLON LUIS, pedido 4400133288): el chofer no está en el catálogo y la oficina
      no tiene sus datos (2026-10-22). Si aparecen su DNI y nombre completo: registrarlo en RegistroChoferes y
      correr `herramientas/carga-historica-correo/Cargar.ps1 -Pedidos 4400133288` (solo toma ese viaje). El
      tracto **F6O-779** ya está en el catálogo (id 102, 2026-10-24; el Word escribía F60-779); sigue sin GPS.
      El usuario dio sus datos (2026-10-24): F. M. L. A., DNI ********, conductor 200; falta
      correr `Cargar.ps1 -Pedidos 4400133288`. El viaje 7247 (LUIS OJEDA) ya se registró: el usuario dio sus datos
      (L. M. O. B., DNI ********, conductor 172, sin licencia) → despacho 1740 con la guía
      T660-00016283.
- [ ] **Productos 7530511 y 7530723 sin catálogo**: vienen en 9 despachos de la carga histórica (la carga
      guarda código y descripción, sin `idProducto`). Aprobarlos en RegistroDespacho la próxima vez que salgan
      en un pedido (o en el catálogo de productos).
- [ ] **PDF de las facturas de la carga histórica** (2026-11-02: adjuntados en sgvActualizada desde los ZIP los de
      octubre que faltaban; queda subirlos por FTP, ver arriba): los 14 pedidos del 01–19/10 tienen su factura
      (número, fecha, valor, moneda) pero sin el PDF adjunto. Y el PDF de la factura F222-00008545 (cargas del
      22/10) se guardó en `WebSGV/Uploads` de la laptop, porque se importó en el sitio local; en el sitio
      publicado no está. Decidir si se suben los PDF a mano desde ListaDespachos.
- [ ] **Despacho de prueba 11 en sgvActualizada** (09/10, pedido 9900000001, guía GR-E2E-0001, viaje
      VP-2026-011 cerrado): es de una prueba de punta a punta, ahora que la BD tiene datos reales. Anularlo.
- [ ] **Jefes de convoy**: el WhatsApp del Word del 21/10 nombra dos por pedido (4400133950: Mercedes
      Chapoñan y Miguel Rivas; 4400133952: Adrián Hidalgo y Juan Ortiz). El sistema no tiene dónde guardarlo:
      decidir con la administradora si hace falta un campo en el lote de descarga o en el seguimiento.
- [ ] **Sección GUAYAQUIL-AZÚCAR del Word**: desde 2026-10-21 completa el viaje del seguimiento de Crisar
      (lugar de entrega y el relevo como tracto y chofer de descarga, "Registrar entregas"). Falta decidir si
      además se registra como despacho de descarga (factura que manda el chofer).
- [ ] **Seguir los 7 viajes de Crisar del 19/10** (seguimiento 7466–7472, primeros de terceros): hoy "UNIDAD
      CARGADA EN BASE SULLANA"; los tractos de descarga del Word del 21/10 salen con las carretas el 22/10.
      Comparar el STATUS y el "Excel STATUS EXTRA" con el Excel de la administradora y ver el detalle
      (SeguimientoViaje) de uno en el navegador. Los de GUAYAQUIL-AZÚCAR del Word del 19/10 (carretas T6P-989,
      T0Q-991, TAN-976, TDN-986, TBE-995) no tienen viaje: su carga es la sección AZÚCAR-CARTAVIO del Word del
      **14/10** (cargaron el 15/10; el Word ya está en la PC de la oficina). El GPS (2026-10-22) los ve cruzar el
      20/10, dormir en el parqueo de Naranjal, descargar en La Troncal el 21/10 (2–9 h) y volver a la base el 22/10.
      **Hecho el 22/10** (con el visto bueno del usuario, por los mismos servicios de la pantalla): despachos
      1151–1155 (Crisar, Cartavio, 15/10) y entregas del Word del 19/10 → viajes 7524–7528 con su relevo; el GPS
      ya los marcó (11–12 hitos): 3 descargados en su totalidad y 2 retornando a base.
- [ ] **Terceros: viajes ya en curso sin despacho**: los de antes de registrar a los clientes no tienen despacho
      de carga, así que no entran al seguimiento; si se quieren, programarlos con su Word (fecha de carga de hasta
      7 días atrás) o crearlos a mano.
- [ ] **TALSA, Rapel y destinos nuevos de Polinplast** sin geocerca: el GPS busca la entrega entre los destinos
      conocidos; uno nuevo queda "Sin lugar de entrega" hasta agregar su geocerca en `GeocercasTerceros`.
- [ ] **Registrar en el catálogo los clientes CRISAR y POLINPLAST y las plantas nacionales Cartavio y
      Chapaira** (RegistroClientes / RegistroPlantas) para que "Programar con el Word" los proponga solos en
      las secciones de terceros (hoy avisa y deja elegirlos). Luego probar en el navegador el selector "Sección
      del Word" con un Word que traiga Vitapro y azúcar (p. ej. el del 18/10).
- [ ] **Geocercas de terceros: dibujarlas sobre el terreno** (hoy círculos en `GeocercasTerceros`, sacados de
      las paradas del GPS) antes de usarlas para marcar horas, y comprobarlas con casos marcados por la
      administradora. (-3.536, -80.184 ya se sabe que es TCI y -8.1386, -79.0176 un 2.º punto de Novopan.)
- [ ] **Preguntas de terceros**: Rapel (la agrícola Rapel está en Piura, pero el GPS lleva esos viajes a Jayanca:
      ¿otro fundo o un packing?) y dónde se entrega a TALSA (agroindustrial del Grupo Rocío, campos en Virú, Chao
      y Zaña); la guía de Polinplast (Rosmery); cómo se registra el falso flete. Resueltos con GPS y web:
      ACP = Agrícola Cerro Prieto (Pacanga, Chepén) y la cochera de Trujillo (estacionamiento de la calle Fundo
      Larrea, -8.1386, -79.0176; `docs/CLIENTES_TERCEROS.md` §4.3 y §6).
- [ ] **Completar las 11 cargas del 21/10 con el correo** cuando llegue (despachos 809–819, viajes del
      seguimiento 7371–7381 con pedido PENDIENTE): primera prueba real de "completar con el correo".
      5 de ellas se sumaron al viaje abierto de su chofer (VP 342, 343, 345, 347, 349), con la misma regla de la
      importación: revisar al liquidar que sea lo esperado.
- [ ] **Descarga de una carga que todavía no tiene documentos**: `DescargaPedidoService` exige pedido y detalle
      de carga, así que la descarga a Ecuador se registra después de completar la carga. Si el correo se
      retrasa más que la descarga, el tracto 2 del seguimiento se pone a mano en el detalle del viaje.
- [ ] **Cambio de chofer con varias guías sin unir**: el descarte solo une si queda una guía y un programado;
      con más, se eligen a mano. Se podría proponer con el GPS (qué tracto programado estuvo en la planta a la
      hora de la guía).
- [ ] **Programado editado a mano**: si en ListaDespachos se le pone pedido o guía a un despacho programado,
      ya no aparece como "esperando documentos" y el correo crearía otro despacho. Revisar si hace falta avisarlo.
- [ ] **Hora de la programación en el seguimiento**: el despacho programado guarda la hora del Word
      (`horaDespacho`), pero `sp_SE_SincronizarDespachos` sigue pasando solo la fecha a F.H. Programación
      (los despachos del lote guardan en `horaDespacho` la hora del registro, no la programada).
- [ ] **Mapa de la ruta: teselas de OpenStreetMap** (`tile.openstreetmap.org`) sirven para poco tráfico; si el
      sistema crece, pasar a un proveedor con clave (CARTO, MapTiler). CARTO ya pide clave.
- [ ] **Trazabilidad: revisar las 10 diferencias de más de 1 h de la prueba de septiembre** (8 en el Complex:
      el GPS ve la unidad en la cola u horas antes o después que el Excel) y las 2 plantas cruzadas (TBM-898 →
      CBU-945 y BSD-716 → T7J-861 del 07/09: el GPS las ve en la planta contraria a la del Excel; probable fila
      invertida en el Excel). Hay que mirarlas con la administradora.

- [ ] **Seguimiento: probar en el navegador lo que escribe** (corregir una hora, guardar los datos que el GPS
      no sabe, editar datos del viaje, nuevo viaje, cerrar/reabrir, "Actualizar GPS") y los paneles de
      evidencia/corrección (JS). Por HTTP solo se probó lo que lee; las escrituras, con la prueba de
      integración `SeguimientoViajesBdTests` (rollback). No había navegador conectado el 2026-10-20.
- [ ] **Seguimiento: guardar la hora de la programación del Word en el lote con documentos** (columna HORARIO):
      al programar con el Word ya se guarda (ver "Hora de la programación en el seguimiento"); en el lote que se
      arma con ZIP + Word todavía se guarda la hora del registro.
- [ ] **Seguimiento: crear también el viaje al finalizar el lote** (al programar con el Word ya se crea; el
      lote con documentos sigue esperando a que se abra el tablero, con `sp_SE_SincronizarDespachos`).
- [ ] **Seguimiento: confirmar que cada despacho de descarga en Ecuador se enlaza a su carga** (`idDespachoCarga`).
      Si no, la sincronización lo une por pedido y carreta; un despacho de descarga sin carreta o con otra
      carreta no completa el viaje.
- [ ] **GPS automático: revisar cuánto tarda en el sitio publicado** (local: 1.ª corrida 49 viajes en 122 s; con
      viajes quietos, 23 analizados + 26 sin movimiento en 20 s con el historial en memoria). Si somee corta las
      corridas largas, bajar `Presupuesto` en `ActualizacionGpsAutomatica`.
- [ ] **Seguimiento: cortar la ventana de la descarga cuando el viaje ya volvió a base** (hoy se leen hasta 12
      días desde la salida de planta; con la caché cuesta poco, pero sigue leyendo días de más).
- [ ] **Seguimiento: medir "Actualizar GPS" en el sitio con los viajes abiertos** (cuántos viajes alcanza por
      clic con la caché). Solo se midió la caché sola con T7J-860 (8 días: 16 s la primera vez, 5,2 s después).
- [ ] **Seguimiento: espacio de `~/App_Data/HistorialGps` en somee** (3–45 KB por tracto y día, se borra a los
      30 días): revisar la cuota tras unas semanas de uso.
- [ ] **Seguimiento: vincular el despacho** (`idDespachoOrigen`/`idDespachoDestino`): la pantalla vieja tenía
      el buscador de despachos; la nueva todavía no lo muestra (las columnas siguen y el GPS las usa).

- [ ] **Trazabilidad: qué escribir en F.H.LL. Base cuando la unidad pasa directo** sin entrar a la base. El
      motor ya lo detecta (`PasoDirectoBase`) y "Verificar GPS" lo avisa, pero no llena la columna.
- [ ] **Trazabilidad: más casos reales** de la administradora (pasa directo, entra y sale de inmediato,
      duerme en base, cambio de tracto en base) para el banco de `WebSGV.Tests/Datos/Trazabilidad`.
- [ ] **Trazabilidad: más casos de descarga en Ecuador** (hoy dos: BFI-922 a Jave y BSD-716 a Inbalnor
      con paso directo por TCI); falta uno en que la unidad **duerma en TCI** hasta el otro día y uno que no
      pase por el Complex, si existe.
- [ ] **Trazabilidad: tabla `PuntoControlGps` de exportación sin uso.** "Verificar GPS" ya no usa sus 20
      puntos (`BASE_*`, `TRUJILLO_*`, `BODEGA_NACIONAL_*`, `CEBAF_LLEGADA`, `CRUCE_ECUADOR`, `TCI_*`, Jave e
      Inbalnor). Revisar si `HoraLlegadaGpsService` usa otros puntos de esa tabla antes de borrarlos.
- [ ] **Trazabilidad: geocercas en BD con editor en mapa** (hoy en código, `GeocercasTrazabilidad`), cuando
      haya más zonas que las de Sullana y Trujillo.
- [ ] **Trazabilidad: unir los tramos por la carreta** con el Word de programación (columna CAMBIO: qué
      tracto lleva la carreta en la carga y cuál en la descarga) para armar el viaje completo sin escribir
      los tractos a mano.
- [ ] **GPS: revisar el `OnwayAuth0ClientSecret` en cada máquina** contra el correo oficial de Location World
      (credenciales del Customer API). En una máquina le faltaba un carácter (63 de 64) y Auth0 respondía 401;
      se corrigió el 2026-10-19. Recordar: **un solo token cada 24 h**; vive en `OnwayAuthCache` de la BD de
      pruebas y lo reusan la web y `herramientas/reporte-gps`.
- [ ] **Reporte GPS, fase 2: status sugerido por unidad** (dónde está y si va cargada o vacía, para la
      columna STATUS del Google Sheets "PROGRAMACION Y STATUS VITAPRO"), desde la última posición y los
      puntos de control. Primero afinar las reglas con casos reales de la administradora de transporte.
- [ ] **Destino de descarga: probar con el Excel real** que manda el cliente a la administradora de transporte
      (se espera el 2026-10-19). Hoy el lector se armó con una captura (columnas Fecha, Pedido, Unidad,
      Placa Camión "tracto - carreta", Empresa, Lugar de recepción); ajustarlo si el Excel real difiere.
- [ ] **Destino de descarga: pedir a quien envía la lista que la mande en Excel**, no en imagen por WhatsApp
      (decisión del usuario: se le hablará a la administradora/remitente).
- [ ] **Destino de descarga: probar en el navegador la descarga por pedido con un pedido real** repartido
      entre Milagro y Jave (no había cargas pendientes en pruebas; solo se probó por HTTP la pantalla nueva
      y la carga de RegistroDespacho). Verificar que el lote se registre con una planta por tracto y que
      `ListaDespachos` lo muestre como dos lotes (agrupa por planta).
- [ ] **Destino de descarga: la lista también podría decir la fecha de recepción en bodega** (se guarda en
      `fechaRecepcion`, hoy no se usa en ninguna pantalla).
- [ ] **Conectar la extensión Claude in Chrome en la PC de casa** (en la oficina ya está, en Edge). Pasos
      en `herramientas/sitio-local/README.md`.
- [ ] **Alias de choferes** (recordar la corrección del usuario cuando elige a mano un nombre del Word que no
      se reconoció). Requiere migración (tabla de alias por conductor). Hoy no hace falta: 64/64 nombres
      reales se reconocen.
- [ ] **Otros clientes del Word** (Polinplast, Cartavio): fuera hasta mapear sus facturas y su forma de
      trabajo. Hoy sus secciones se ignoran.
- [ ] **Prueba de punta a punta con un pedido real** (correo + Word → carga → descarga → viaje →
      liquidación y firma → aprobación → PDF → contabilidad), en pruebas.
- [ ] **Dashboards / indicadores**: el usuario tiene temas que comentar antes (al final).

- [ ] **Alinear `PdfAbastecimientoService`** al estilo del nuevo PDF de la Orden de Viaje (fuera de alcance
      mientras Grifo siga fuera; ver memoria de alcance).
- [ ] **Serilog 2.12 → 4.x** (opcional; requiere nuevas dependencias y binding redirects).
- [ ] **BFR-730 no tiene equipo GPS en Onway** (o tiene otra placa): sus relevos de Crisar no se pueden seguir.
      Revisar con quien administra Onway.
- [ ] **Guía T660-00016530 del pedido 4400133881**: dice chofer PIEDRA VEGA DANILO y tracto T7J-861, pero la administradora tiene
      HUGO FLORES / BNG-758 (viaje manual 7341, pedido `PENDIENTE`). Confirmar con quien emitió la guía y enlazar el despacho 873
      con el viaje 7341 (o corregir el chofer/tracto).
- [ ] **Tablero del seguimiento con las cargas de mañana y formulario manual de RegistroDespacho plegado**: falta verlo en el navegador (probado solo por HTTP el 2026-10-21) (salió del historial al archivar).
- [ ] **Subir al sitio los demás Uploads de la oficina** (CPIC, MCI, facturas comerciales) con `herramientas/publicar/subir-uploads.ps1` (las 76 facturas de flete ya se subieron el 2026-11-14) (salió del historial al archivar).
- [ ] **Subir la documentación pendiente por la Bandeja del sitio** (después de publicar la preparación para los conductores, 2026-11-13) (salió del historial al archivar).
- [ ] **Cambiar el carnet de extranjería real del ejemplo por uno ficticio** (p. ej. `001234567`) en `Services/Conductor/DocumentoConductor.cs`, `Services/Conductor/RegistroConductorService.cs`, `WebSGV.Tests/DocumentoConductorTests.cs` y `WebSGV.Tests/Integracion/ConductorLargosBdTests.cs` (esta prueba no debe depender de un registro real: que inserte su propio conductor dentro de `TransaccionDePrueba`). Es de un conductor real (conductor 218). (salió de la revisión de E2-3)
- [ ] **Decisión del usuario: purgar del historial de git los datos personales de conductores** (DNI, nombres y el carnet de extranjería real del ejemplo de `DocumentoConductor`) con filter-repo + push --force, como la purga de PDF del 2026-07-10; recomendado solo si el repo es o será público (ver `docs/HOJA_DE_RUTA_CALIDAD.md`, Etapa 2, decisiones del usuario 1).

## 5. Herramientas del repo

Vigente en `CLAUDE.md` (sección Database), en los `herramientas/*/README.md` y en `docs/PASO_A_PRODUCCION.md`; el texto anterior está en `docs/archivo/HISTORIAL_2026.md`.

## 6. Paso a producción (al final, en este orden)

Vigente en `docs/PASO_A_PRODUCCION.md` y en `herramientas/publicar/README.md` ("Antes y después de publicar"); el texto anterior está en `docs/archivo/HISTORIAL_2026.md`.
