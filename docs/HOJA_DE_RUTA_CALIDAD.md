# Hoja de ruta de calidad SGV

Índice corto de las etapas de mejora que salieron del análisis del 2026-11-19. **Leer esto primero al
retomar el trabajo**: dice en qué etapa estamos y qué sigue. El detalle de cada tarea está en
`docs/PLAN_MEJORAS_SGV.md` y en los documentos que enlaza; el historial, en `git log`.

## Cómo se trabaja (acordado con el usuario, 2026-11-19)

1. Una etapa a la vez, en el orden de la tabla. No se abre la siguiente hasta cerrar la actual.
2. Cada etapa tiene dos fases: **PLAN** (el analista investiga, se escriben las tareas con archivos y
   criterio de aceptación, el usuario resuelve las decisiones) y **EJECUCIÓN** (una tarea a la vez:
   desarrollador → revisor y probador → commit por tarea).
3. Al cerrar una etapa: se vuelve a medir su nota, se anota aquí y se pasa a la siguiente.
4. Lo que solo puede hacer el usuario (rotar contraseñas, decisiones de negocio, publicar) se lista
   aparte en cada etapa para que no frene lo demás.

## Estado

**Etapa actual: 2 — fase EJECUCIÓN, sigue E2-6** (plan detallado escrito el 2026-11-21). De la Etapa 1 quedan acciones del usuario: rotaciones de credenciales y los PDF de la laptop. La ejecución la hace Opus 5.5.
Reparto de modelos: Fable 5.2 analiza y planifica; Opus 5.5 ejecuta.

| # | Etapa | Nota hoy | Meta | Estado |
|---|---|---|---|---|
| 1 | Operación y continuidad | 4 | 7 | **Cerrada 2026-11-20 — nota medida: 7** (monitores UptimeRobot y cron del GPS activos desde 2026-11-21; quedan las rotaciones de credenciales del usuario) |
| 2 | Ordenar el plan de mejoras | 3 | 7 | **En ejecución (5 de 7)** |
| 3 | Integridad de la base de datos | 6,6 | 8 | Pendiente |
| 4 | Seguridad avanzada | 7,5 | 9 | Pendiente |
| 5 | Interfaz y usabilidad | 6,5 | 8 | Pendiente |
| 6 | Arquitectura y medición | 5,5 | 7,5 | Pendiente |

Nota global al empezar: **≈ 6 / 10** (medida por área el 2026-11-19; el 8/10 de
`PLAN_MEJORAS_SGV.md` mide el avance del código, no la operación).

## Etapas

### Etapa 1 — Operación y continuidad (4 → 7)

Por qué va primero: es la nota más baja y el único hueco donde un fallo significa perder datos que no
se pueden rehacer (los PDF firmados nunca se regeneran y deben conservarse 5 años).

Alcance:
- Respaldo propio de la BD, de `Uploads` y de `App_Data/OrdenesViaje`, fuera de somee, con una
  restauración ensayada y escrita como procedimiento.
- Rotación de credenciales expuestas: token de apisperu, FTP de somee, `nickdream` en producción,
  usuarios de prueba. Retirar `.env`/DAB con la conexión de producción.
- Publicar lo atrasado (31 commits desde `cce61fd`) y borrar en el servidor las páginas retiradas.
- Monitoreo: aviso ante errores, vigilancia de disponibilidad y de la última corrida del GPS
  automático (desactivado desde el 27/10 sin aviso).
- Publicación con vuelta atrás (guardar el paquete de cada versión).

#### Plan detallado (analista, 2026-11-19)

Lo comprobado: en la PC de la oficina hay `sqlcmd`, `bcp` y un SQL Server local para ensayar la
restauración; falta `sqlpackage`. El usuario de la BD puede exportar `.bacpac` pero no hacer BACKUP
nativo. `sgvActualizada` ocupa 155 MB en archivos (81 MB son Query Store; los datos, unos 20 MB).
Para publicar `master` no falta ninguna migración ni SP (51/51 y 98 iguales). En local hay 263
archivos en `Uploads` (59 MB) y 31 PDF firmados (8,4 MB), con su SHA-256 en `FirmaDigital`.

| Tarea | Qué | Quién | Estado |
|---|---|---|---|
| **E1-1** | Respaldo de la BD: `herramientas/respaldo/respaldar-bd.ps1` exporta un `.bacpac` fechado a una carpeta fuera del repo; conserva 30 diarios y 12 mensuales. Aceptación: sale un `.bacpac` sin errores. | Claude | **Hecho** (1,6 MB en 33 s) |
| **E1-2** | Espejo de archivos: `respaldar-archivos.ps1` baja por FTP `Uploads` y `App_Data/OrdenesViaje` (nunca borra en local) y compara el SHA-256 de cada PDF con `FirmaDigital`/`OrdenViaje`. Aceptación: 0 faltantes, 0 distintos. | Claude (necesita la credencial FTP guardada por el usuario) | **Hecho** (1023 archivos, 0 errores; copia verificada por SHA-256 sobre Google Drive) |
| **E1-3** | Restauración ensayada: `ensayar-restauracion.ps1` importa el `.bacpac` al SQL local y compara conteos; `docs/PROCEDIMIENTO_RESPALDO.md`; tarea programada de Windows. Aceptación: conteos iguales y el sitio local abre una liquidación firmada sobre la BD restaurada. | Claude + usuario | **Hecho** (ensayo OK, 24 972 filas iguales; tarea diaria registrada a las 23:00) |
| **E1-4** | Publicación con paquete y vuelta atrás: perfil `Paquete.pubxml` y `herramientas/publicar/publicar.ps1` (zip por versión, sube sin tocar App_Data ni Uploads, lista y borra sobrantes solo con confirmación). Aceptación: `-SoloListar` muestra exactamente las páginas retiradas. | Claude | **Hecho** (revisor: 2 rondas); falta validarlo contra el FTP real |
| **E1-5** | Publicar `master` (31 commits), borrar sobrantes, comprobar 404 de las páginas retiradas y la lista de verificación del plan. Requiere E1-1 y E1-2 antes. | Usuario da la orden; Claude acompaña | **Hecho** 2026-11-20 (`20261120_1311_60853e8`) |
| **E1-6** | Salud y vigilancia: `Salud.ashx` anónimo (200/503 con `SELECT 1`; `?gps=1` da 503 si hay viajes activos y el GPS lleva más de 60 min sin consultar), regla pura con pruebas. | Claude | **Hecho** (revisor y probador, 2 rondas); falta publicar y crear los monitores |
| **E1-7** | Correo ante errores: `CorreoHelper` común, `AvisoErrores` con límite (1 por tipo cada 30 min, 10 al día), desde `Application_Error`. Requiere SMTP configurado. | Claude | **Hecho** (revisor 2 rondas y probador; correo real recibido); `SmtpFrom` en el servidor: hecho 2026-11-20 |
| **E1-8** | Retiro de DAB del repo y rotaciones. | Claude + usuario | **Hecho** el retiro (revisor: aprobado con cambios, corregidos); rotaciones y borrar `.env`/`dab-config.json` locales: usuario |
| **E1-9** | Cierre técnico: paquete sin linux/osx ni documentos, log sin valores del querystring, Salud vigila a Onway, respaldo a Drive verificado. | Claude | **Hecho** (revisor 2 rondas y probador) |
| **E1-10** | `publicar.ps1`: sobrantes borrados dentro del mantenimiento; cancela si quedarían páginas viejas en vivo; validado contra somee en solo lectura (261 sobrantes). | Claude | **Hecho** (revisor 2 rondas) |

**Decisiones del usuario** (recomendación entre paréntesis):
1. Dónde guardar los respaldos (carpeta de OneDrive o Google Drive en la PC de la oficina, fuera del repo).
2. Frecuencia (BD y archivos a diario a las 23:00; ensayo de restauración mensual).
3. Correo de avisos (una cuenta Gmail del sistema con contraseña de aplicación; avisa al correo del usuario; también activa "recuperar contraseña"). **Decidido 2026-11-19**: cuenta `sistemas.cibertic@serviciosgeneralesviviana.com` (solo la usa el área de sistemas) con el alias `sgv-avisos@`.
5. Respaldos: **decidido 2026-11-19**, unidad compartida de Google Workspace `Respaldos_Sistemas_SGV` (`G:\Unidades compartidas\Respaldos_Sistemas_SGV` en la PC de la oficina).
4. `FTPProfile.pubxml` de serviciosgiviana (borrarlo si ese sitio ya no se usa).

**Solo el usuario, en paneles externos:**
- apisperu: token nuevo, revocar el anterior, ponerlo en `appSettings.Secrets.config` (oficina, laptop, servidor).
- somee: cambiar la contraseña FTP y volver a guardarla con `respaldar-archivos.ps1 -GuardarCredencial` en cada PC; opcional, la de la BD.
- `nickdream` y usuarios de prueba: cambiar la contraseña desde el sitio; sacar la de producción de `privado/CREDENCIALES_USUARIOS.txt`.
- En cada máquina: borrar `.env` y `dab-config.json`.
- Gmail: cambiar la contraseña de aplicación "SGV avisos" (quedó escrita en el chat) y ponerla en `SmtpPass` sin pegarla en el chat.
- cron-job.org: tras E1-5, reactivar "SGV GPS automático" y probarlo.
- UptimeRobot: dos monitores cada 5 min sobre `/Salud.ashx` y `/Salud.ashx?gps=1` (tras E1-6 publicado).

**Riesgos:** el `.bacpac` no es una copia consistente si hay escrituras durante la exportación (se
exporta de noche; el primer ensayo dirá si valida); el FTP de somee va sin cifrar; el respaldo
programado depende de que la PC de la oficina esté encendida; el borrado de sobrantes nunca debe
tocar App_Data ni Uploads.

Fuera de esta etapa (pasa a la 3): borrar los SP retirados de la BD, Query Store a 100 MB, aviso de
`16_HoraPeru.sql` cambiada tras aplicarse.

### Etapa 2 — Ordenar el plan de mejoras (3 → 7)

- Partir `PLAN_MEJORAS_SGV.md` (1 527 líneas, 265 abiertas): "Top 20 priorizado", archivo aparte de
  datos por corregir, y procedimiento de operación en lugar de la bitácora de la sección 6.
- Cerrar las obsoletas (módulos retirados) y unir las duplicadas (credenciales, DAB, supervisor sin
  probar, unidades sin GPS, manual y videos).
- Retirar documentos viejos de la raíz y `.opencode/` (decisión del usuario: actualizar o borrar).

#### Plan detallado (3 analistas, 2026-11-21)

Medido: el plan tiene 1 563 líneas y 489 KB; 228 KB son la tabla de historial (sección 3), 137 KB las
"Abiertas" y 30 KB las dos líneas "Última actualización". Las **270 tareas abiertas** se clasificaron
una por una. Las tablas (con el número de línea del plan en el commit `48610b8`) y la auditoría de
documentos están en `privado/etapa2/` (fuera de git porque citan datos de personas; solo en la PC de
la oficina: si se pierden, se rehacen con los analistas).

| Clase | Cuántas | Qué se hace con ellas |
|---|---|---|
| MEJORA (código o BD vigente) | 98 | 41 son funciones del negocio; 25 de la etapa 5; 14 de la 6; 11 de la 3; 6 de la 4; 1 de esta etapa |
| USUARIO (decisión, panel externo, conseguir un dato) | 73 | Lista propia, para que no frenen lo demás |
| DATOS (corregir o cargar registros) | 26 | Archivo aparte |
| OPERACIÓN (paso repetible o de producción) | 23 | Procedimientos, no tareas |
| DUPLICADA | 23 | Se unen en la que queda |
| HECHA | 21 | Se cierran con su evidencia |
| OBSOLETA | 6 | Se cierran |

Solo 20 son de prioridad A. Además hay unas 50 líneas `[x]` mezcladas entre las abiertas, pendientes
escondidos dentro de ítems `[x]` (líneas 268, 297, 347, 443), 113 remisiones "ver sección 3" y datos
personales de conductores en 11 líneas. La sección 6 es casi toda bitácora ya resuelta por E1-5 y
contradice lo actual (publicar desde Visual Studio, DAB, `DeleteExistingFiles`).

**Estructura a la que se llega** (un documento por tipo de lector):

| Documento | Contenido |
|---|---|
| `docs/PLAN_MEJORAS_SGV.md` | Estado, **Top 20** y las mejoras abiertas agrupadas (negocio por módulo; etapas 3 a 6), cada una con id estable (`M-001`…), prioridad A/B/C y una o dos líneas. Meta: menos de 400 líneas y de 60 KB |
| `docs/PENDIENTES_USUARIO.md` | Lo que solo puede hacer el usuario: decisiones de negocio, paneles externos, datos por conseguir (`U-001`…) |
| `docs/DATOS_POR_CORREGIR.md` | Registros por corregir o cargar (`D-001`…), con la consulta o el script |
| `docs/PRUEBAS_PENDIENTES.md` | Lo que falta probar en el navegador o con un rol real (para el `probador`) |
| `docs/PASO_A_PRODUCCION.md` | Procedimiento único para pasar a `sgvTransporte`, en orden, con sus SELECT de control |
| `herramientas/publicar/README.md` | Sección "Antes y después de publicar" (lo de cada publicación) |
| `docs/PREPARAR_MAQUINA.md` | Lo que hoy es la sección 2 (laptop), al día |
| `docs/DECISIONES_NEGOCIO.md` | Decisiones y reglas que hoy solo están en el historial |
| `docs/archivo/HISTORIAL_2026.md` | Sección 3 y "Hechas", sin datos personales |

| Tarea | Qué | Aceptación | Estado |
|---|---|---|---|
| **E2-1** | **Rescatar antes de archivar.** `docs/DECISIONES_NEGOCIO.md` con las 6 decisiones que solo están en el historial (plan L109, L113, L127, L333, L1071, L1076) y las reglas de CPI/MCI del ítem L720 (L726–738). | Cada decisión cita el commit o la fecha y el código que la aplica; sin datos personales. | **Hecho** 2026-11-21 |
| **E2-2** | **Operación.** (a) README de publicar: "Antes y después de publicar" (orden migración → SP → DLL, y SP compatible con la DLL publicada; claves que necesita el archivo de secretos del servidor; carpetas con permiso de escritura; comprobaciones). (b) `WebSGV/appSettings.Secrets.config.example` con los nombres de las claves, sin valores (`<None>` en el csproj). (c) `docs/PASO_A_PRODUCCION.md` destilado de la sección 6 (pasos 1–5, 7 y 8; controles antes de las migraciones 30, 49, 50 y 51; scripts de borrado y de recálculo). (d) `docs/PREPARAR_MAQUINA.md`. (e) `docs/OPERACION.md:38` corregido (el sitio usa `sgvActualizada`). | Nada vigente de las secciones 2, 5 y 6 queda solo en el plan; ningún paso contradice a `publicar.ps1`; `probar-publicar.ps1` y la suite en verde. | **Hecho** 2026-11-21 |
| **E2-3** | **Archivar el historial.** Sección 3, "Hechas" y las `[x]` mezcladas pasan a `docs/archivo/HISTORIAL_2026.md`; los pendientes escondidos en ítems `[x]` (L268, L297, L347, L443) y los 6 puntos del ítem L253 salen como tareas propias; los datos personales se enmascaran. | El archivo conserva todas las filas con su commit; el plan ya no tiene sección 3 ni "Hechas"; 0 documentos de identidad en `docs/`. | **Hecho** 2026-11-21 |
| **E2-4** | **Partir y limpiar las abiertas** según las tablas de clasificación: cerrar 21 hechas y 6 obsoletas con su evidencia, unir 23 duplicadas, repartir el resto en los documentos de arriba con id, prioridad y texto corto; "ver sección 3" se cambia por el commit. **Top 20** al principio del plan. | **Tabla de conciliación** en `privado/etapa2/conciliacion.md`: cada una de las 270 líneas → id nuevo o "cerrada: motivo"; ninguna sin destino. Las marcadas "sin verificar" se comprueban antes de cerrarlas. | **Hecho** 2026-11-21: fueron 287 casillas (no 270): 29 cerradas (23 hechas, 6 obsoletas), 38 unidas, 3 a otros documentos; cifras en la bitácora |
| **E2-5** | **Documentos viejos.** Borrar de la raíz `ANALISIS_PROYECTO.md`, `configuraciondenuevosagentes.md`, `flujoactual.md`, `formulasDash`, `mcp-cursor-executed.txt`, `paNedd.txt`; mover a `docs/archivo/` `Documentacion_Tecnica_PBI_SGV_MARZO2025.md`, `PLAN_REFACTOR_SERVICES.md`, `PLAN_MEJORAS_INTERNAS.md` y `PLAN_MODERNIZACION.md`; fusionar `STACK_Y_FLUJO_SGV.md` en `FLUJO_DE_TRABAJO_SGV.md`; poner al día `README.md`; el ejemplo de `GUIA_CREACION_ROLES.md:198` pasa a un marcador. `.opencode/` y `opencode.jsonc` se borran y `AGENTS.md` queda en pocas líneas que remiten a `CLAUDE.md`. | Ninguna referencia rota (Grep de cada nombre); `probar-publicar.ps1` (usa `PLAN_MODERNIZACION.md` como texto de prueba) y la suite en verde. | **Hecho** 2026-11-21 (también `docs/migracion_somee/` a `docs/archivo/`) |
| **E2-6** | **Que no vuelva a crecer.** Reglas nuevas en `CLAUDE.md` ("Keeping the plan up to date"), en los agentes `analista` y `desarrollador`, en la skill `nuevo-sp` y en la memoria: dónde va cada cosa; el historial es `git log` (`docs/archivo/HISTORIAL_2026.md` queda congelado, no se escribe); una tarea terminada se borra del plan. Prueba en `CodigoFuenteLimpiezaTests`: el plan pesa menos de 80 KB y no tiene líneas `- [x]`. | La prueba falla si al plan se le agrega una línea `[x]`; suite en verde. | Pendiente |
| **E2-7** | **Cierre.** Medir la nota y anotarla; actualizar esta hoja y la memoria. | Ver "Cómo se mide". | Pendiente |

Orden: E2-1 y E2-2 primero (sacan lo que no se puede perder), luego E2-3, E2-4, E2-5, E2-6 y E2-7.
Cada una: `desarrollador` → `revisor` (comprueba con conteos y con la conciliación que no se perdió
nada) → commit. No hace falta `probador` (no cambia el sitio); E2-2 (b) toca el csproj: compilar.

**Cómo se mide el 7:** plan de menos de 400 líneas y 60 KB; 0 tareas hechas, obsoletas o duplicadas
entre las abiertas; toda abierta con id, prioridad y destino; Top 20 visible; operación fuera del plan
y sin contradicciones; 0 documentos que describan el sistema viejo; una prueba que lo sostenga. No
pasa de 7 porque seguirán abiertos unos 70 pendientes del usuario y unas 100 mejoras reales.

**Decisiones tomadas por Claude** (el usuario pidió decidir con lógica; todas se revierten con git):
1. `.opencode/` y `opencode.jsonc`: **borrar**. Sin cambios de contenido desde 2026-05-29, mandan usar
   iTextSharp, EPPlus, Grifo y DAB, y contradicen a `CLAUDE.md`. `AGENTS.md` se conserva, mínimo.
2. El historial no se borra: se archiva en `docs/archivo/`.
3. Los ítems "probar en el navegador" no son mejoras: van a `PRUEBAS_PENDIENTES.md`.

**Decisiones del usuario** (no frenan la ejecución):
1. **Datos personales de conductores en el historial de git** (11 líneas del plan, ya subidas a
   GitHub). Los documentos nuevos no los llevarán; quitarlos del historial exige reescribirlo y
   `push --force` (como el 2026-07-10). Recomendación: no reescribir si el repositorio es privado.
2. **Top 20**: Claude propone el orden en E2-4; el usuario lo ajusta según el negocio.

**Prioridad A**: ver el **Top 20** al principio de `docs/PLAN_MEJORAS_SGV.md` (E2-4); el control de
estados antes de la migración 48 en producción está en `docs/PASO_A_PRODUCCION.md`, paso 3.

Riesgos: perder una decisión al archivar (por eso E2-1 va primero); remisiones rotas; que los agentes
sigan escribiendo en secciones que ya no existen (la parte de `CLAUDE.md` de E2-6 se adelanta al
commit de E2-4).

### Etapa 3 — Integridad de la base de datos (6,6 → 8)

- Restricciones CHECK en estados (`COMPLETADO`/`FINALIZADO` conviven) y `Usuarios.rol`.
- Llaves foráneas que faltan (17 columnas `id*`), PK en `CPIC_Productos`.
- Borrar los 17 SP sin archivo en el repo y las tablas de módulos retirados (con respaldo previo).
- Usuario de BD con mínimo privilegio, si somee lo permite; Query Store acotado.
- TRY/CATCH y `XACT_ABORT` en los SP con transacción; índices duplicados y FK sin índice.

### Etapa 4 — Seguridad avanzada (7,5 → 9)

- Segundo factor (TOTP) para los roles de administración.
- Content-Security-Policy (primero solo reporte) y Permissions-Policy; un único `SGV.escaparHtml`.
- Registrar `LOGIN_FALLIDO` y bloqueos; contadores de bloqueo en la BD (hoy en memoria).
- Escaneo de dependencias en CI; retirar scripts sueltos y la versión duplicada de select2.

### Etapa 5 — Interfaz y usabilidad (6,5 → 8)

- Bloqueo global de doble envío con indicador "Procesando…".
- Accesibilidad de formularios (231 etiquetas sin asociar), foco visible, `lang` en `Error.aspx`.
- ViewState y paginación en las tablas de listados; compresión.
- Variables de color comunes; ayuda dentro de la app, manual y videos.

### Etapa 6 — Arquitectura y medición (5,5 → 7,5)

- Cobertura y analizadores en CI.
- Sacar el SQL de los 26 code-behind hacia `Services/` (170 llamadas a `DbHelper`).
- Partir `RegistroDespacho` y `ListaDespachos`.
- Pruebas para `ArchivoOrdenViajeService` y la integración GPS; `catch` silenciosos.

## Bitácora de etapas

| Fecha | Etapa | Hecho |
|---|---|---|
| 2026-11-19 | — | Análisis por áreas (5 analistas) y hoja de ruta creada. |
| 2026-11-19 | 1 | Plan detallado escrito (E1-1 a E1-8); empieza la ejecución por E1-1. |
| 2026-11-19 | 1 | E1-1 hecha: `herramientas/respaldo/respaldar-bd.ps1` (bacpac + retención). |
| 2026-11-19 | 1 | E1-6 hecha: `Salud.ashx` (BD y cron del GPS) para UptimeRobot; `docs/OPERACION.md`. |
| 2026-11-19 | 1 | E1-4 hecha: `herramientas/publicar/publicar.ps1` (paquete por versión, mantenimiento, vuelta atrás); revisor 2 rondas. Se arreglaron los agentes revisor y probador (no cargaban). |
| 2026-11-19 | 1 | E1-3 hecha: restauración ensayada en el SQL local (todo igual a somee) y `docs/PROCEDIMIENTO_RESPALDO.md`. |
| 2026-11-19 | 1 | E1-2 código hecho: `respaldar-archivos.ps1` y `verificar-pdf-firmados.ps1`; falta la descarga real con la contraseña FTP. |
| 2026-11-19 | 1 | E1-8 hecha: Data API Builder retirado del repo con prueba que impide que vuelva. SMTP de avisos configurado y probado (alias sgv-avisos@). Primer .bacpac real en la unidad compartida de Drive. |
| 2026-11-19 | 1 | E1-7 hecha: aviso por correo ante errores (`AvisoErrores`, `CorreoHelper`), con límite y sin datos sensibles. |
| 2026-11-20 | 1 | E1-9 hecha: cierre técnico. Primera copia completa en Drive (1023 archivos) y tarea diaria registrada. Solo falta publicar (E1-5). |
| 2026-11-20 | 1 | E1-10 hecha: publicación segura frente a los 261 sobrantes del servidor. Solo falta publicar (E1-5). |
| 2026-11-20 | 1 | E1-5 hecha: publicado `60853e8` con `publicar.ps1` (254 subidos, 261 sobrantes borrados con respaldo previo). **Etapa 1 cerrada.** Nota de operación 4 → 6,5: respaldo diario BD + archivos verificado y restauración ensayada, publicación con paquete y vuelta atrás, Salud.ashx, aviso por correo de errores; falta (usuario) monitores UptimeRobot, cron del GPS y rotaciones para llegar a 7. |
| 2026-11-21 | 1 | Cron de cron-job.org "SGV GPS automático" reactivado (prueba 202, cada 10 min) y 2 monitores de UptimeRobot (palabra clave `"ok":true` cada 5 min, aviso por correo): "SGV - Sitio y BD" (`Salud.ashx`) y "SGV - GPS y Onway" (`?gps=1`), ambos Up. Nota de operación: **7**. |
| 2026-11-21 | 2 | Plan detallado: 270 abiertas clasificadas una por una (98 mejoras, 73 del usuario, 26 de datos, 23 de operación, 23 duplicadas, 21 hechas, 6 obsoletas) y auditoría de documentos; tareas E2-1 a E2-7. |
| 2026-11-21 | 2 | E2-1 hecha: `docs/DECISIONES_NEGOCIO.md` con 24 decisiones (DN-01 a DN-24) rescatadas del historial del plan, cada una con fecha/commit y dónde la aplica el código; sin datos personales. |
| 2026-11-21 | 2 | E2-2 hecha: lo de cada publicación en `herramientas/publicar/README.md` ("Antes y después de publicar": orden migración → SP → DLL, claves del servidor, permisos de escritura, comprobaciones), plantilla `WebSGV/appSettings.Secrets.config.example`, `docs/PASO_A_PRODUCCION.md` (procedimiento único a `sgvTransporte` con sus SELECT de control), `docs/PREPARAR_MAQUINA.md` e índice de procedimientos en `docs/OPERACION.md`. Secciones 2, 5 y 6 del plan marcadas como historial. |
| 2026-11-21 | 2 | E2-3 hecha: historial del plan a `docs/archivo/HISTORIAL_2026.md` (las dos líneas largas de la cabecera, secciones 2, 3, 5 y 6, "Hechas" y las 149 `[x]`/tachadas que seguían en la sección 4 (más 25 sub-tareas `[x]`), con sus sub-líneas; 990 líneas, 392 KB). El plan pasa de 1571 líneas y 479 KB a 632 líneas y 92 KB, sin sección 3 ni "Hechas" y con 0 `[x]`; 1526 líneas no vacías del original, 0 sin destino. Pendientes escondidos sacados como `[ ]` propias: 7 del ítem de verificaciones del 2026-11-16 (el punto 1 ya estaba hecho), el DEFAULT 'APROBADO' (B6-7, Etapa 3) y el filtro por la 2.ª factura/CPIC (tarea 11); el borrado de `Database/` del servidor ya se hizo en E1-5 y el cambio de contraseñas de prueba ya estaba como tarea y en `docs/PASO_A_PRODUCCION.md`. Abiertas `[ ]`: 273 → 285 (las 4 últimas, tras la revisión: tablero en el navegador, subir Uploads, documentación por la Bandeja y la decisión de purgar el historial de git). DNI y nombres de conductores enmascarados en plan y archivo (17 líneas). También enmascarados los tres usuarios-DNI de `docs/migracion_somee/02_clasificacion_tablas.md:142` y la fecha de nacimiento y dirección de un conductor que quedaban en el archivo. |
| 2026-11-21 | 2 | E2-4 hecha: las 287 casillas abiertas del plan (`d52f964`) conciliadas una por una en `privado/etapa2/conciliacion.md` (script `verificar-conciliacion.py`: OK). 29 cerradas (23 hechas y 6 obsoletas, cada una con su evidencia: SELECT en `sgvActualizada`, archivo o commit; 3 de ellas, U-006, D-016 y D-017, tras la revisión: sus ids quedan retirados, sin renumerar), 38 unidas a la que queda, 3 a documentos existentes (`docs/PASO_A_PRODUCCION.md` paso 3 y README de publicar). El resto, con id, prioridad y texto corto: plan 99 mejoras `M-` (198 líneas, 31,2 KB; antes 633 líneas y 92 KB) con **Top 20**; nuevos `docs/PENDIENTES_USUARIO.md` (71 `U-`, 94 líneas, 20,4 KB; U-010 partida del Query Store), `docs/DATOS_POR_CORREGIR.md` (26 `D-`, 48 líneas, 11,5 KB; cada A y B con su consulta de control o "sin consulta") y `docs/PRUEBAS_PENDIENTES.md` (23 `P-`, 42 líneas, 6,2 KB; P-023 partida de la fila 292; 1 `[~]`). D1 del bloque 5 confirmada resuelta (conductores 70/75 inactivos). `CLAUDE.md` y agentes `analista`, `desarrollador` y `probador` apuntan a los documentos nuevos. |
| 2026-11-21 | 2 | E2-5 hecha: borrados de la raíz `ANALISIS_PROYECTO.md`, `configuraciondenuevosagentes.md`, `flujoactual.md`, `formulasDash` (mismo SHA-256 que `WebSGV/dashboard/formulasDash`, que se queda), `mcp-cursor-executed.txt`, `paNedd.txt` y `opencode.jsonc`, y `.opencode/` entero (23 archivos); movidos a `docs/archivo/` `Documentacion_Tecnica_PBI_SGV_MARZO2025.md`, `PLAN_REFACTOR_SERVICES.md`, `PLAN_MEJORAS_INTERNAS.md`, `PLAN_MODERNIZACION.md` y `docs/migracion_somee/` (foto del esquema de 2026-05; `.gitignore` y `PASO_A_PRODUCCION.md` apuntan a la ruta nueva); `STACK_Y_FLUJO_SGV.md` fusionado en `FLUJO_DE_TRABAJO_SGV.md` (solo lo vigente: servidor, seguridad, datos de la empresa) y borrado; `README.md` reescrito (stack real e índice de `docs/` y `herramientas/`); `AGENTS.md` reducido a remitir a `CLAUDE.md`; ejemplo de contraseña de `GUIA_CREACION_ROLES.md` cambiado por un marcador; `probar-publicar.ps1` usa `dashboard/LEEME.md` como ejemplo de `.md`. No había skill de iTextSharp en `.claude/skills/`. M-047 cerrada. |
