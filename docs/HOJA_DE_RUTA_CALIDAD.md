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

**Etapa actual: 4 — fase EJECUCIÓN (plan escrito el 2026-11-21; E4-1, E4-2, E4-4, E4-5 y E4-6 hechas; E4-3 espera U-081, sigue E4-7)** (la Etapa 3 se cerró el 2026-11-21 con nota 8; la Etapa 2, ese mismo día con nota 7). De la Etapa 1 quedan acciones del usuario: rotaciones de credenciales y los PDF de la laptop. La ejecución la hace Opus 5.5.
Reparto de modelos: Fable 5.2 analiza y planifica; Opus 5.5 ejecuta.

**Qué sigue (para retomar en otra máquina):**
- **Etapa 4, ejecutar**: una tarea a la vez, en el orden de la tabla E4-1 a E4-16 de la sección "Etapa 4" de abajo. E4-1 (datos personales fuera del árbol) y E4-2 (pruebas de las guardas) no dependen de nadie.
- **Decisiones del usuario que frenan tareas**: U-081 (matriz de permisos) frena E4-3; U-079 (segundo factor) y U-080 (segundo administrador de sistema) frenan E4-14. **Urgente y fuera del código**: U-078 (el repositorio de GitHub es público) y las rotaciones U-001 a U-004. Siguen U-073, U-077, U-017 y U-011.
- **Abierto de la Etapa 3** (no frena la 4): M-107, M-109 y M-110 (lo que no borraron las migraciones 59 y 60); datos de prueba D-032, D-033 y D-034; pruebas P-024, P-025, P-027 y P-028; U-075 y U-076.
- **Ensayo en la laptop**: ya funciona (2026-11-21) con LocalDB, `sqlpackage` como herramienta de dotnet y sin Google Drive: `.\herramientas\respaldo\ensayar-migracion.ps1 -Carpeta C:\RespaldosSGV -RespaldoNuevo` (exporta un `.bacpac` nuevo a esa carpeta, fuera del repo).

| # | Etapa | Nota hoy | Meta | Estado |
|---|---|---|---|---|
| 1 | Operación y continuidad | 4 | 7 | **Cerrada 2026-11-20 — nota medida: 7** (monitores UptimeRobot y cron del GPS activos desde 2026-11-21; quedan las rotaciones de credenciales del usuario) |
| 2 | Ordenar el plan de mejoras | 3 | 7 | **Cerrada 2026-11-21 — nota medida: 7** (plan de 211 líneas y 33 KB, Top 20, pendientes repartidos con id; `CodigoFuenteLimpiezaTests` lo sostiene) |
| 3 | Integridad de la base de datos | 6,6 | 8 | **Cerrada 2026-11-21 — nota medida: 8** (30 CHECK de dominio y 10 FK nuevas, todo validado; 25/25 SP con transacción con `XACT_ABORT`; correlativo de viaje; 24 objetos y 18 tablas retirados; Query Store a 100 MB; cada migración ensayada y con su paso en `PASO_A_PRODUCCION.md`) |
| 4 | Seguridad avanzada | 7,5 | 9 | **En ejecución** (plan E4-1 a E4-16 escrito el 2026-11-21) |
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
| **E2-6** | **Que no vuelva a crecer.** Reglas nuevas en `CLAUDE.md` ("Keeping the plan up to date"), en los agentes `analista` y `desarrollador`, en la skill `nuevo-sp` y en la memoria: dónde va cada cosa; el historial es `git log` (`docs/archivo/HISTORIAL_2026.md` queda congelado, no se escribe); una tarea terminada se borra del plan. Prueba en `CodigoFuenteLimpiezaTests`: el plan pesa menos de 80 KB y no tiene líneas `- [x]`. | La prueba falla si al plan se le agrega una línea `[x]`; suite en verde. | **Hecho** 2026-11-21 (también revisor, probador, DECISIONES_NEGOCIO y los 4 documentos de pendientes; la memoria la actualiza el orquestador) |
| **E2-7** | **Cierre.** Medir la nota y anotarla; actualizar esta hoja y la memoria. | Ver "Cómo se mide". | **Hecho** 2026-11-21: nota 7 |

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

**Plan detallado (2026-11-21).** Medido con SELECT sobre `sys.*` en `sgvActualizada` (la BD del sitio):
82 tablas, 115 SP, 5 vistas, 3 funciones, 18 triggers, 10 CHECK y 126 FK; migraciones 51 de 51 y 98 SP
iguales al repo. Ninguna FK ni CHECK sin validar. `OrdenViaje` y las tablas de liquidación están vacías:
es el mejor momento para endurecerlas, antes de que entren los conductores (U-012).

Lo que se encontró:
- **Estados sin CHECK** (solo hay 10 CHECK). `SeguimientoExportacion.estado` tiene 5 992 `COMPLETADO`
  que el código no reconoce como cerrados (solo `FINALIZADO` y `CANCELADO`): hoy cuentan como abiertos
  (causa de M-023); los escribe `ImportarSeguimiento.aspx.cs:762-780`. `sp_RechazarLiquidacion.sql:95`
  escribe `'EN_PROCESO'`, que ningún otro código conoce. 19 despachos (272–290) están `PROGRAMADO` con
  `activo=0`. `OrdenViaje.estadoAprobacion` tiene DEFAULT `'APROBADO'` y admite NULL (M-050). 3 usuarios
  activos con roles retirados (U-015). `FirmaDigital.etapa` tiene 20 NULL históricos (append-only).
- **Llaves**: 10 FK que faltan (5 huérfanos: `SeguimientoExportacion.idUsuarioModificacion = 1`, usuario
  que no existe); 14 FK duplicadas (tablas vacías); 3 índices repetidos en `Despachos`; única tabla sin PK:
  `CPIC_Productos` (vacía); cascadas en `DocumentosCPIC` y `DocumentosFactura` que dejarían PDF huérfanos.
- **Sobrantes**: 17 SP en la BD sin archivo (M-048, M-049), 2 SP sin uso (M-054, M-055), 5 vistas y 1
  función sin uso, unas 35 tablas de Grifo, Maquinaria y del modelo viejo (88 filas, ninguna de negocio
  actual) y 12 triggers `TR_*_Auditoria` vivos sin archivo en el repo. `Indicadores` y `UploadHistory`
  no se tocan (`CargarExcel` sin decidir).
- **Transacciones**: 18 de los 24 SP con transacción tienen TRY/CATCH pero no `SET XACT_ABORT ON` (un
  corte por tiempo de espera deja la transacción abierta en el pool). `sp_DC_RegistrarFirmaAdmin.sql:87-92`
  devuelve `ERROR_MESSAGE()` al usuario; `sp_DC_CerrarViajesProgreso.sql:112-115` relanza con `RAISERROR`.
- **Correlativo de viaje (M-051)**: `COUNT+1` con `CAST(... AS VARCHAR(3))`: desde el viaje 1000 del año
  sale `VP-AAAA-00*`, choca con el índice único y **el despacho no se crea**; igual si se borra un viaje
  o dos se crean a la vez. Hoy va por el 081.
- **Privilegios**: la app entra como `db_owner` y somee no deja crear logins desde SQL (U-073). Query
  Store: 89 MB usados, tope 1 000 MB, con un plan de 500 MB (M-053).
- **Herramientas**: nada ensaya una migración antes de la BD viva; `aplicar-procedimientos.ps1` no hace
  ejecución de humo (M-052). En la PC de oficina hay SQL Server Express, LocalDB y `sqlpackage`, y
  `ensayar-restauracion.ps1` ya importa un `.bacpac`.

| Tarea | Qué | Aceptación | Estado |
|---|---|---|---|
| **E3-1** | **Herramientas y pruebas** (no toca datos). `-Entorno ensayo` (solo servidor local) en `sql.ps1`, `aplicar-migraciones.ps1` y `aplicar-procedimientos.ps1`; `herramientas/respaldo/ensayar-migracion.ps1` (respaldo → BD `sgvEnsayo_*` local → migraciones y SP pendientes → `[FactBD]` → controles → borra); `BaseDePruebas.cs` acepta `sgvEnsayo_*` en servidor local; M-052: humo con `dm_exec_describe_first_result_set_for_object` y lista de 4 permitidos; migración **52** con 11 de los 12 triggers heredados (el de Grifo queda para E3-8); hash de `16_HoraPeru.sql` reconocido; `IntegridadBdTests.cs` (PK, objetos con archivo o en lista de retiro, triggers de `FirmaDigital`, FK y CHECK validados). | Ensayo OK sobre el `.bacpac` del día; `-SoloListar` sin avisos; suite en verde. Cierra M-052. | Hecho |
| **E3-2** | **Bugs de estado.** `sp_RechazarLiquidacion.sql:95` → `PROGRAMADO`; `sp_DC_RegistrarFirmaAdmin` y `sp_DC_CerrarViajesProgreso` → `THROW`; `ImportarSeguimiento.DerivarEstado`: COMPLETADO → FINALIZADO, PENDIENTE → EN_CURSO; pruebas. | 0 `EN_PROCESO` en los SP; pruebas nuevas en verde; `probador`: rechazar una liquidación e importar STATUS. | Hecha |
| **E3-3** | **Correlativo de viaje.** Migración **53**: tabla `Correlativo(serie, anio, ultimo)` y `sp_SiguienteCorrelativo` (`UPDLOCK, HOLDLOCK`, sin tope de cifras); lo usan `sp_CrearDespacho`, `sp_CrearViajeProgreso` y `sp_LD_ReubicarDespachoEnViajeDelConductor`. | Pruebas: no se repite tras borrar un viaje; el 1000 sale `VP-AAAA-1000`; `probador`: crear y programar un despacho. Cierra M-051. | Hecha |
| **E3-4** | **`XACT_ABORT`.** `SET XACT_ABORT ON` en los 18 SP; prueba de CI: todo `.sql` con `BEGIN TRAN` lo tiene y todo `CATCH` relanza. | Prueba de CI y `[FactBD]` de lotes y flujo en verde. | Hecha |
| **E3-5** | **Corregir datos** (D-029, D-030, D-031), con respaldo previo y **aprobación del usuario**: script con controles que se detiene si los conteos no coinciden. | Las consultas de control dan 0; `probador`: tablero de seguimiento. | Hecha |
| **E3-6** | **Restricciones.** Migración **54**: CHECK de estados, tipos, etapa y rol: `rol IN ('ADMIN', 'ADMINISTRADOR', 'ADMINISTRADOR DE TRANSPORTE', 'ADMINISTRADOR DE SISTEMA', 'CONDUCTOR', 'CHOFER', 'SUPERVISOR', 'CONTABILIDAD') OR (activo = 0 AND rol IN ('ADMINISTRADOR DE GRIFO', 'ADMINISTRADOR DE MAQUINARIA', 'OPERADOR'))` (textos medidos en pruebas el 2026-11-21; los 3 retirados quedan inactivos tras U-015), regla ANULADO ⇔ `activo=0`, `estadoAprobacion` NOT NULL con DEFAULT `PENDIENTE`, NOT NULL que faltan, montos `>= 0`. Migración **55**: las 10 FK, cascadas de documentos a NO ACTION, fuera las 14 FK y los 3 índices repetidos, índices de apoyo. | `is_not_trusted = 0`; lista exacta en `IntegridadBdTests`; ensayo OK; `probador`: usuarios, anular lote, liquidar. Cierra M-050. | Hecha |
| **E3-7** | **Query Store.** Migración **56**: `MAX_STORAGE_SIZE_MB = 100`. | Valor comprobado por prueba. Cierra M-053. | Hecha |
| **E3-8** | **Borrado** (al final, con respaldo nuevo, ensayo y **orden del usuario**: U-074). Migración **57**: DROP de 24 objetos (18 SP, 5 vistas y 1 función). Migración **58**: DROP de 18 tablas retiradas, `Usuarios.idOperador` y `OrdenViaje.tipoViaje`; `ViajesEnProgreso.observaciones` a `nvarchar(max)`. Código: `VehiculoService.cs:72-98` (sin las tablas de Grifo), `sp_DC_InsertarOrdenViajeConductor` sin `tipoViaje`, scripts de grifo y maquinaria. Lo que el sitio publicado todavía nombra queda para después: `sp_DC_RevocarFirma` y `FirmaService.RevocarFirma` (M-054), tablas de Grifo, `GuiasTransportista` y `OrdenViajeAjuste` (M-106). | 0 objetos en la BD sin archivo salvo el trigger de Grifo; suite y `probar-publicar.ps1` en verde; `probador`: tractos, carretas y firmas. Cierra M-048, M-049, M-055, M-057 y M-058. | Hecha |
| **E3-9** | **Cierre.** Medir la nota; hoja, plan y `docs/PASO_A_PRODUCCION.md` (pasos de las migraciones 52 a 58 con sus controles). | Ver "Cómo se mide". | Hecha |

Orden: el de la tabla (primero lo que no toca datos; lo destructivo al final). Cada migración es
idempotente, se detiene con `THROW` si encuentra datos que violen la regla y pasa antes por
`ensayar-migracion.ps1`. Cada tarea: `desarrollador` → `revisor` (y `probador` donde se indica) → commit.

**Cómo se mide el 8:** 0 SP, vistas, funciones y triggers en la BD sin archivo; `aplicar-procedimientos
-SoloListar` con 0 distintos y 0 errores de humo fuera de los 4 permitidos; los CHECK y las 10 FK
existen, validados, con 0 valores fuera de dominio, 0 huérfanos, 0 FK o índices repetidos y 0 tablas
sin PK; 0 SP con transacción sin `XACT_ABORT ON`; `estadoAprobacion` sin DEFAULT `APROBADO`; el viaje
1000 se crea; Query Store a 100 MB; cada migración ensayada y con su paso en `PASO_A_PRODUCCION.md`.
No pasa de 8 porque la app sigue como `db_owner` (los triggers append-only se pueden saltar), no hay
respaldo a un punto en el tiempo (solo el `.bacpac` diario), `Indicadores`/`CargarExcel` siguen sin
decidir y `sgvTransporte` no tendrá estas reglas hasta el paso a producción (U-011).

**Decisiones tomadas por Claude:** sin FK en `AuditoriaLog.IdUsuario`, `Auditoria.IdRegistro` ni
`FirmaDigital.idDocumento` (auditoría polimórfica; las firmas sobreviven al retiro de la orden, DN-04 y
DN-06); el CHECK de `FirmaDigital.etapa` acepta NULL; sin CHECK en `tipoRuta` (crece con cada tercero);
no se pasa a `nvarchar`; `SYSUTCDATETIME` en las firmas se queda; el correlativo va en tabla y no en
`SEQUENCE`; de los 12 triggers heredados, 11 se versionan (migración 52) y no se borran; el de Grifo no se borró en E3-8 (el sitio publicado aún consulta su tabla) y se fue con su tabla en la migración 60.

**Decisiones del usuario** (2026-11-21): aprobadas D-029 a D-031 y U-015 (aplicadas en E3-5) y
autorizado U-074 (borrado de E3-8). Las migraciones 54 a 58 se aplican en cuanto pasen el ensayo y la
revisión, de preferencia fuera del horario de uso.

Fuera de la etapa: usuario de mínimo privilegio (depende del panel de somee: U-073), M-059 y M-060.

Riesgos: la BD de pruebas es la del sitio (por eso el ensayo local primero y respaldo antes de E3-5,
E3-6 y E3-8); un CHECK nuevo hace fallar un INSERT que antes pasaba (por eso E3-2 va antes y el
`probador` recorre los flujos); el borrado solo se revierte desde el `.bacpac`.

### Etapa 4 — Seguridad avanzada (7,5 → 9)

- Segundo factor (TOTP) para los roles de administración.
- Content-Security-Policy (primero solo reporte) y Permissions-Policy; un único `SGV.escaparHtml`.
- Registrar `LOGIN_FALLIDO` y bloqueos; contadores de bloqueo en la BD (hoy en memoria).
- Escaneo de dependencias en CI; retirar scripts sueltos y la versión duplicada de select2.

#### Plan detallado (4 analistas, 2026-11-21)

Medido en solo lectura sobre el commit `9705543`, la BD `sgvActualizada` y el sitio publicado (`92d4dc6`, solo GET anónimos):

- **Lo que ya está bien (no se toca):** los 44 puntos de entrada (42 `.aspx` y 2 `.ashx`) y los 23 `[WebMethod]` comprueban sesión y rol; ningún acceso a objetos ajenos (el conductor solo ve y firma lo suyo, contabilidad solo APROBADO); 0 SQL concatenado con datos del usuario, 0 redirecciones abiertas, 0 rutas manipulables; el id de sesión se regenera al entrar, el sello de sesión cierra las sesiones al cambiar rol, estado o contraseña; el login no delata qué usuarios existen (mismo mensaje y mismo tiempo); 0 XSS explotable en 81 sumideros JS revisados (`SGV.avisar` y `SGV.confirmar` pintan texto; `MarcadoCodificacionTests` vigila el marcado). Sitio publicado: HTTPS forzado con HSTS, sin cabeceras de versión, y `/Web.config`, `/App_Data`, `/Uploads`, `/bin`, `/Database` y `/packages.config` dan 404. NuGetAudit sobre la solución: 0 paquetes vulnerables.
- **Exposición (lo más grave, no estaba anotado):** el repositorio de GitHub es **público**. En su historial están el token de apisperu (U-001), una contraseña antigua de la BD con el login vigente, y DNI y nombres de conductores (U-033); ningún secreto actual aparece. En el árbol actual hay 33 números que coinciden con DNI, licencias o teléfonos de la tabla `Conductor`: 41 DNI con apellidos en `WebSGV/Database/Scripts/registrar_choferes_listas_2026-10-24.sql` y otros en `ListaChoferesService.cs`, `ImportacionPedidoModels.cs`, `LectorDocumentosPedidoTests.cs` y los de M-061. En `docs/` hay documentos reales sin seguimiento ni `.gitignore` (programaciones de Word, videos): un `git add docs` los publicaría. Secret scanning activo; alertas de Dependabot apagadas; `master` sin protección.
- **Acceso:** los contadores de intentos (login, firma, cambio y recuperación de contraseña, verificación pública) viven en `HttpRuntime.Cache` (`ControlIntentosLogin.cs:234-249`): se pierden al reciclar el sitio. `LOGIN_FALLIDO` no se registra nunca (0 filas; solo existe como filtro en `Auditoria.aspx:108`; M-062). El bloqueo por cuenta no mira la IP: cualquiera bloquea una cuenta ajena 15 minutos con 5 intentos. Sin segundo factor. **Un solo ADMINISTRADOR DE SISTEMA activo y sin correo**: si pierde la contraseña, nadie se la restablece. Usuarios activos: 1 administrador de sistema, 2 de transporte, 1 de contabilidad y 85 conductores (84 nunca han entrado; 85 hashes siguen en el formato viejo hasta su próximo ingreso). Sin `machineKey` fijo: el secreto del TOTP no puede cifrarse con `MachineKey.Protect`.
- **Autorización:** ninguna prueba de CI exige las guardas (conviven 5 estilos; un `[WebMethod]` nuevo sin guarda pasaría el CI). Dos fuentes de verdad: 9 secciones de `TienePermiso` no las usa ninguna página (M-064), la matriz da FACTURA y REGISTRO a SUPERVISOR pero `FacturasFlete`, `RegistroProductos` y `RegistroRutas` usan `ExigirRolAdmin`, y ADMIN tiene `DASHBOARD_CONDUCTOR` pero la página lo rebota. `Session["IdConductor"]` se lee directo en 3 páginas. Cambios de estado por GET: `Login.aspx?error=sesion` cierra una sesión válida (`Login.aspx.cs:46-59`). `GpsAutomatico.ashx?iniciar=1` acepta un POST de formulario.
- **Subidas:** `DestinoDescarga`, `RegistroChoferes` y `CargarExcel` abren Excel sin tope de tamaño descomprimido (`CargarExcel` además no borra su temporal si falla y acepta `.xls`); los PDF dentro de un ZIP se aceptan solo por la extensión (M-067).
- **Navegador:** no se envía Content-Security-Policy, Permissions-Policy, COOP ni CORP. Línea base: 23 `<script>` inline (todos solo `var SGV = …`), 94 manejadores `on*=` en marcado y 11 en cadenas JS, 13 `return confirm(` (contra la regla), 21 `RegisterStartupScript`, 157 `style=`, 1 origen externo (teselas de OpenStreetMap). WebForms obliga a `script-src 'unsafe-inline'` (32 `LinkButton`, validadores, UpdatePanel; .NET 4.8 no pone nonce) y los 17 `AutoPostBack` usan `setTimeout('cadena')`, que pide `'unsafe-eval'`. 5 copias de la función de escape además de `SGV.escaparHtml` (una, en `SeguimientoViajeMapa.js:85`, no escapa comillas). select2 en dos versiones (4.0.13 y 4.1.0-rc.0), 5 scripts y 1 CSS sin uso que se publican. `DashboardConductor`, `DashboardExportacion`, `RecuperarContrasena` y `RestablecerContrasena` sin `no-store` (botón Atrás tras cerrar sesión). El borrador de liquidación queda 45 días en `localStorage`.
- **Infraestructura:** la app entra como `db_owner` (U-073): puede desactivar los 6 triggers de solo inserción de `FirmaDigital`, `AuditoriaLog` y `Auditoria`, y nada fuera de la BD lo detectaría (0 triggers DDL). La conexión a la BD va sin cifrar (`Encrypt=True` probado: conecta). SQL Server 2022 sin parches (16.0.1000.6) y FTP sin TLS, ambos de somee. Los `.bacpac` van sin cifrar a Drive. El CI no audita dependencias. `publicar.ps1` no comprueba `customErrors`, `requireSSL` ni `httpErrors` del paquete. 2 `ex.Message` llegan al usuario (M-065 y `ActualizacionGpsAutomatica.cs:84`). El log no tiene tope de tamaño por archivo.

| Tarea | Qué | Aceptación | Estado |
|---|---|---|---|
| **E4-1** | **Datos personales fuera del árbol.** Borrar `Database/Scripts/registrar_choferes_listas_2026-10-24.sql` (ya aplicado); números ficticios en `ListaChoferesService.cs`, `ImportacionPedidoModels.cs`, `LectorDocumentosPedidoTests.cs`, `DocumentoConductor.cs`, `RegistroConductorService.cs`, `DocumentoConductorTests.cs` y `ConductorLargosBdTests.cs` (que inserte su propio conductor); revisar los 2 de `CodigoFuenteLimpiezaTests.cs`; `.gitignore`: `/docs/*.docx`, `/docs/*.rar`, `/docs/*.pdf`, `/docs/video/`, `/docs/TRABAJO DE INVESTIGACION/`. | `[FactBD]` nueva: 0 números de 8 o 9 cifras de `git ls-files` coinciden con `Conductor` (hoy 33); `git status` limpio; suite en verde. Cierra M-061. | Hecha |
| **E4-2** | **Guardas sostenidas por pruebas.** `WebSGV.Tests/GuardasAccesoTests.cs`: tabla página → guarda esperada (primera sentencia de `Page_Load`, fuera de `try`) y lista cerrada de públicas; cada `[WebMethod]` empieza por una comprobación de una lista cerrada. `DashboardConductor` pasa a `SecurityHelper.ExigirRolConductor()`; `IdConductor` solo vía `SesionHelper` (3 páginas; ampliar la prueba que ya prohíbe `Session["..."]`); `AgregarHeadersSeguridad()` desde `Site.Master.cs` y en las páginas sin master; fuera el `returnUrl` muerto de `DescargarPdfOrdenViaje`. | La prueba falla al quitar una guarda o agregar una página o WebMethod sin ella (comprobado a mano) y pasa con los 44 + 23; `probador`: conductor a 390 px y botón Atrás tras cerrar sesión. | Hecha |
| **E4-3** | **Una sola matriz de permisos** (tras U-081). Quitar las secciones sin página y ADMIN de `DASHBOARD_CONDUCTOR`; las 3 páginas con `ExigirRolAdmin` usan la matriz; menú de `Site.Master` y `docs/FLUJO_DE_TRABAJO_SGV.md` iguales. | `RolesHelperTests` exige que toda sección de la matriz se use y que toda página con sección la consulte. Cierra M-064. | Pendiente |
| **E4-4** | **Intentos de acceso en la BD.** Migración **61** `IntentoAcceso(clave PK, fallidos, primerFalloUtc, bloqueadoHastaUtc, venceUtc)`; la clave de usuario es `USR_` + SHA-256, nunca el texto (a veces se escribe la contraseña en ese campo). SP `sp_US_LeerIntentoAcceso`, `sp_US_GuardarIntentoAcceso` (limpia vencidos) y `sp_US_QuitarIntentoAcceso`. `AlmacenBdIntentos : IAlmacenIntentosLogin` (si la BD falla, usa la caché y deja log) para login, `ReautenticacionHelper`, recuperación y `VerificarDocumento`. Tres límites: cuenta+IP 5 → 15 min; cuenta 30 en 1 h → 15 min; IP 20 → 5 min. `AuditoriaHelper.RegistrarAcceso`: `LOGIN_FALLIDO` solo si la cuenta existe (incluida la inactiva, sin cambiar mensaje ni tiempo) y `LOGIN_BLOQUEADO` una vez por bloqueo; nunca el texto de un usuario inexistente. | `ControlIntentosLoginTests` en verde; `[FactBD]`: 5 fallos bloquean y una instancia nueva del control sigue bloqueando; 5 fallos desde una IP no bloquean la cuenta desde otra; consulta de control por `Accion`; ensayo de la migración; `probador`: login fallido, bloqueo y filtro de Auditoría. Cierra M-062. | Hecha |
| **E4-5** | **Cabeceras del navegador.** En `Web.config`: `Content-Security-Policy` aplicada (`frame-ancestors 'self'; object-src 'none'; base-uri 'self'; form-action 'self'`), `Content-Security-Policy-Report-Only` (`default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https://tile.openstreetmap.org; font-src 'self'; connect-src 'self'; frame-src 'self'`, con informes), `Permissions-Policy` (cámara, micrófono, geolocalización y pagos apagados; `clipboard-write=(self)`), COOP y CORP `same-origin` (sin COEP: rompería el mapa). `CspInforme.ashx`: anónimo, solo POST, 8 KB, 20/min por IP y 300/h en total, registra directiva, origen y ruta sin querystring, responde 204. | `ConfiguracionSeguridadTests` fija cada cabecera; prueba del receptor (tamaño, límite, sin querystring en el log); `probador`: todos los roles a 1366 y 390 px, PDF en el iframe de contabilidad, mapa y firma. Tras publicar: P-031. | Hecha |
| **E4-6** | **JS propio limpio.** Un único `SGV.escaparHtml` (fuera las 5 copias); los 12 `return confirm(` pasan a `data-confirmar` y se retira su interceptor de `SiteMaster.js`; `JavaScriptStringEncode` en `DashboardConductor.aspx.cs:725`; `ReportesOrdenesViaje.js:49` escapado; select2 solo 4.0.13 y sin el tema de Bootstrap 5; fuera `Scripts/Custom/JavaScript.js`, `Scripts/JavaScript.js`, `Scripts/OrdenViaje.js`, `Scripts/SGVScripts.js`, `Scripts/agregarOrdenViaje.js` y `Content/Site.css` (también del csproj); `rel="noopener"` en 6 enlaces; el borrador de `localStorage` se borra al cerrar sesión. | Pruebas en `CodigoFuenteLimpiezaTests`: 0 funciones de escape fuera de `SiteMaster.js`, 0 `confirm(` en `Views`, una versión por biblioteca en `Scripts/lib`, todo archivo de `Scripts/` y `Content/` propio referenciado; `probador`: buscadores de `ListaDespachos` y `RegistroDespacho`, liquidaciones, tablero y mapa. | Hecha |
| **E4-7** | **Subidas.** `ExcelSubidoHelper.Validar` (extensión `.xlsx`, firma ZIP y `LimitesImportacionSeguimiento.ValidarDescomprimido`) en `DestinoDescarga`, `RegistroChoferes`, `CargarExcel` e `ImportarSeguimiento`; `CargarExcel` borra su temporal en `finally` y deja de aceptar `.xls`; `ImportacionPedidoService.PdfsDelZip` pasa a `ignorados` lo que no sea PDF (`ArchivoSubidoHelper.EsPdf`). | Prueba con un xlsx inflado generado en memoria; prueba de fuente que exige el helper en cada subida de Excel; caso nuevo en `BandejaDocumentosTests` con un `.pdf` falso. Cierra M-067. | Pendiente |
| **E4-8** | **Peticiones forjadas y fugas menores.** `GpsAutomatico.ashx?iniciar=1` exige `Content-Type: application/json` (415 si no); `Login.aspx?error=sesion` ya no cierra una sesión vigente (conservar el rompe-bucles con `SesionVigenteHelper`); `MensajeErrorHelper` en `ReportesOrdenesViaje.aspx.cs:1123` y `ActualizacionGpsAutomatica.cs:84`; `LogSGV` con `fileSizeLimitBytes` de 20 MB y `rollOnFileSizeLimit`; `<trace enabled="false"/>` y `cookieSameSite="Lax"` explícitos; `publicar.ps1` rechaza un paquete con `customErrors` Off, sin `requireSSL`, con `httpErrors` Detailed o sin HSTS. Según U-082: `VerificarDocumento` sin motivo de anulación ni códigos de las otras firmas. | Pruebas del 415, del resumen sin texto técnico y de `probar-publicar.ps1` con un paquete sin transformar; `probador`: no hay bucle Login ↔ página protegida y "Actualizar GPS" funciona. Cierra M-065. | Pendiente |
| **E4-9** | **Dependencias en CI.** `build.yml`: `msbuild /t:Restore /p:RestorePackagesConfig=true /p:NuGetAudit=true /p:NuGetAuditMode=all /p:NuGetAuditLevel=low /warnaserror:NU1903,NU1904` (alta y crítica hacen fallar; el resto avisa), `npx retire` sobre `Scripts/lib` y `Content/lib` (Bootstrap 4.6.2 como excepción escrita: el fallo del carrusel no aplica), corrida semanal programada, `permissions: contents: read`, acciones fijadas por SHA. `.github/dependabot.yml`: `nuget` y `github-actions`, semanal, agrupado, sin subidas mayores de QuestPDF ni ClosedXML. | En una rama de prueba con Newtonsoft.Json 12.0.1 el CI falla con NU1903 (confirma que el NuGet de `windows-latest` audita `packages.config`; en local ya se comprobó); en `master` pasa. | Pendiente |
| **E4-10** | **Conexión cifrada a la BD.** `Encrypt=True` en `Database/conexion-bd.ps1`, `respaldar-bd.ps1` (`/SourceEncryptConnection:True`) y la plantilla; pasos en `PREPARAR_MAQUINA.md`, el README de publicar y `PASO_A_PRODUCCION.md` (U-084). | `sql.ps1 "SELECT 1"`, el respaldo, el ensayo y el sitio local funcionan; una prueba fija `Encrypt=True` en la plantilla. | Pendiente |
| **E4-11** | **Sello externo de firmas y auditoría** (lo que se puede hacer mientras la app sea `db_owner`). `herramientas/respaldo/sellar-registros.ps1`, llamado por la tarea diaria: SHA-256 por fila de `FirmaDigital`, `AuditoriaLog` y `Auditoria`, guardado junto al `.bacpac`; al día siguiente, una fila que ya existía y cambió o falta, o un trigger de solo inserción desactivado, es FALLA con aviso por correo. `docs/PROCEDIMIENTO_RESPALDO.md`. | En una BD `sgvEnsayo_*`: desactivar un trigger y alterar una fila da FALLA; sin cambios, OK; funciones puras probadas como en `probar-publicar.ps1`. | Pendiente |
| **E4-12** | **Cuentas de administración.** M-066: `EXISTS` de otro ADMINISTRADOR DE SISTEMA activo (`UPDLOCK, HOLDLOCK`) al desactivar y al cambiar de rol; sin "Desactivar" ni "Restablecer" en la fila propia. Pedir la contraseña del administrador (`ReautenticacionHelper`, propósito nuevo) antes de restablecer la de otro, cambiar un rol o quitar el segundo factor. Duración máxima de sesión de 12 h. | `[FactBD]`: desactivar al último administrador de sistema activo cambia 0 filas y da mensaje de negocio; `probador`: Gestión de usuarios. Cierra M-066. | Pendiente |
| **E4-13** | **Núcleo del segundo factor** (sin interfaz). `Helpers/Totp.cs` (RFC 6238 con `HMACSHA1`, 30 s, 6 dígitos, tolerancia ±1 paso, hora UTC), `Helpers/Base32.cs`, `Helpers/CifradoSecreto.cs` (AES-256-CBC + HMAC-SHA256, formato `v1.iv.ct.tag`, clave `Seguridad.ClaveSegundoFactor` en `appSettings.Secrets.config` y su `.example`). Sin paquete NuGet para el OTP. | `TotpTests` con los vectores de los RFC 4226 y 6238; Base32 `foobar` → `MZXW6YTBOI`; cifrado de ida y vuelta y rechazo de un byte alterado. | Pendiente |
| **E4-14** | **Segundo factor en uso** (tras U-079 y U-080). Migración **62**: `UsuarioSegundoFactor` y `UsuarioCodigoRecuperacion` (fuera de `Usuarios`). SP `sp_US_RegistrarPasoTotp` (un código no vale dos veces), `sp_US_UsarCodigoRecuperacion`, `sp_US_QuitarSegundoFactor` (renueva el sello y audita). `Views/SegundoFactor.aspx` (paso intermedio: la sesión no existe hasta el código; 5 fallos → 15 min con el almacén de E4-4) y `Views/MiSeguridad.aspx` (alta con QR y secreto en texto, 10 códigos de recuperación de un solo uso guardados como hash). `GestionUsuarios`: "Quitar segundo factor" sobre otro usuario; `Database/Scripts/quitar-segundo-factor.ps1` para la emergencia. Interruptor `Seguridad.ExigirSegundoFactor` y `RolesHelper.ExigeSegundoFactor(rol)`. QR con QRCoder en SVG (comprobar antes que compila en net48 con `packages.config`; si no, QR propio). | Pruebas de tolerancia y de reuso; `[FactBD]`: un código de recuperación no se usa dos veces y quitar el factor cambia el sello; ensayo de la migración; `probador` a 1366 y 390 px: alta, entrada con código, código repetido rechazado, bloqueo, código de recuperación. Tras publicar: P-030. | Pendiente |
| **E4-15** | **CSP de scripts aplicada** (tras E4-5 publicada y P-031). Medido en E4-5: el `eval` no sale solo de los 17 `AutoPostBack` (`setTimeout('__doPostBack…')`), sino de MicrosoftAjax en **cada** página con ScriptManager (`Type.parse`/`registerClass`, ~51 por carga, desde `ScriptResource.axd`), de WebUIValidation y del UpdatePanel: sin `'unsafe-eval'` dejarían de funcionar los UpdatePanel y los PageMethods de todo el sitio. Por eso la política aplicada lleva `script-src 'self' 'unsafe-inline' 'unsafe-eval'` y el resto de directivas de la Report-Only (`default-src`, `img-src`, `connect-src`, `font-src`, `frame-src`, `style-src`), que no dieron ningún informe en el recorrido. Los 94 `on*=` del marcado y los 11 de cadenas JS pasan a `data-accion` con delegación (prepara el día en que se quite ScriptManager, Etapa 6). | Prueba que cuenta los manejadores inline y no deja subir el número (meta 0 propios); 0 informes fuera de `eval`/`inline` en 7 días de uso real antes de aplicarla y 0 bloqueos `CSP (aplicada)` después. | Pendiente |
| **E4-16** | **Cierre.** Medir la nota; hoja, plan, `PASO_A_PRODUCCION.md` (migraciones 61 y 62, clave del segundo factor, `Encrypt=True`) y README de publicar. | Ver "Cómo se mide". | Pendiente |

Orden: el de la tabla. Primero lo que quita exposición y no cambia el comportamiento (E4-1, E4-2), luego lo que el usuario nota poco (E4-3 a E4-12) y al final el segundo factor y la CSP de scripts, que necesitan decisiones, una publicación intermedia y prueba con celular. E4-3 y E4-14 esperan su decisión; el resto no. Cada tarea: `desarrollador` → `revisor` (y `probador` donde se indica) → commit; las migraciones pasan antes por `ensayar-migracion.ps1`.

**Cómo se mide el 9:** 0 números de `Conductor` en el árbol y repositorio privado o historial purgado (U-078); `GuardasAccesoTests` en verde con todas las páginas y WebMethods; una sola matriz de permisos; los contadores sobreviven a un reciclaje y hay filas `LOGIN_FALLIDO` y `LOGIN_BLOQUEADO` en una prueba real; las cuentas de administración y contabilidad entran con segundo factor y hay 2 administradores de sistema; CSP aplicada con 0 informes en 7 días; 0 copias de la función de escape, 0 `confirm(`, 0 archivos de `Scripts/` sin uso; el CI falla ante un paquete con vulnerabilidad alta; conexión a la BD cifrada; el sello diario detecta una fila alterada en el ensayo; rotaciones U-001 a U-004 hechas. Sin U-078 y sin las rotaciones la nota no pasa de 8.
No llega a 10 porque la app sigue como `db_owner` mientras somee no dé otro usuario (U-073), WebForms obliga a `'unsafe-inline'` en `script-src`, el FTP de somee no tiene TLS, el servidor de BD no está parchado (U-083) y los secretos están en claro en el servidor.

**Decisiones tomadas por Claude:** TOTP propio y sin NuGet (60 líneas con los vectores del RFC; una dependencia menos que vigilar); el secreto se cifra con una clave de `appSettings.Secrets.config` y no con `MachineKey` ni DPAPI (no sobreviven a una mudanza ni a un respaldo); restablecer la contraseña no quita el segundo factor; no hay "recordar este dispositivo" al inicio; los umbrales de bloqueo de E4-4; sin nonce en la CSP (.NET 4.8 no lo pone en sus scripts) y `'unsafe-inline'` se queda en `script-src` y `style-src` (los 157 `style=` no se tocan en esta etapa); `'unsafe-eval'` también se queda mientras exista ScriptManager (lo pide MicrosoftAjax en cada página, medido en E4-5); Bootstrap 4.6.2 se queda (sin soporte, pero su único fallo conocido es del carrusel, que no se usa); el sello externo en vez de tablas ledger (M-111).

**Decisiones del usuario:** U-078 (repositorio público: pasarlo a privado o purgar el historial), U-079 (segundo factor: qué roles, desde cuándo y cómo se recupera), U-080 (segundo administrador de sistema), U-081 (SUPERVISOR en Facturas de flete, Productos y Rutas; ADMIN en el panel del conductor), U-082 (qué muestra la verificación pública). Acciones en paneles: U-083 (somee: parches y FTPS), U-084 (`Encrypt=True` en el servidor), U-085 (respaldos cifrados), U-086 (Dependabot y protección de `master`), más U-001 a U-005, U-033 y U-073.

Fuera de la etapa: M-068 (necesita los tiempos del log del servidor), M-111 y el usuario de BD de mínimo privilegio (U-073).

Riesgos: una CSP o una guarda mal puesta deja una pantalla sin funcionar (por eso Report-Only primero, las pruebas de fuente y el `probador` por rol); el segundo factor puede dejar fuera al único administrador de sistema (por eso U-080 antes, los códigos de recuperación, el script de emergencia y el interruptor apagado al publicar); perder `Seguridad.ClaveSegundoFactor` invalida todos los secretos (se respalda con los demás secretos; los códigos de recuperación siguen sirviendo); el almacén en BD añade unas 3 consultas por login; cambiar los `AutoPostBack` toca 5 páginas con mucho uso.

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
| 2026-11-21 | 2 | E2-6 hecha: reglas de los pendientes iguales en `CLAUDE.md` ("Keeping the plan up to date", con la tabla de dónde va cada cosa), los agentes `analista`, `desarrollador`, `revisor` y `probador`, la skill `nuevo-sp` y "Reglas" de `PLAN_MEJORAS_SGV`, `PENDIENTES_USUARIO`, `DATOS_POR_CORREGIR`, `PRUEBAS_PENDIENTES` y `DECISIONES_NEGOCIO`; cada cabecera con `Último id usado` (M-100, U-072, D-028, P-023, DN-24; los retirados U-006, D-016, D-017 y M-047 no se reutilizan). Pruebas nuevas en `CodigoFuenteLimpiezaTests`: plan < 80 KB, sin `- [x]` en los 5 documentos, ids únicos, del prefijo del documento y no mayores que el último usado, y ningún número con forma de DNI en `docs/` (comprobado que fallan al agregar una `[x]` o un DNI de 8 cifras). |
| 2026-11-21 | 2 | E2-7: Etapa 2 cerrada con **nota 7**. Medido: plan de 211 líneas y 33 261 B (meta < 400 y < 60 KB); 0 `[x]` y 0 hechas/duplicadas entre las abiertas (verificado en E2-4 y sostenido por la prueba); 99 M, 71 U, 26 D y 23 P, todas con id y prioridad; Top 20 al principio del plan; operación en `PASO_A_PRODUCCION`, `PREPARAR_MAQUINA` y el README de publicar; 0 documentos vigentes con el stack viejo (las menciones que quedan dicen que se retiró). Sigue: plan de la Etapa 3 (Fable 5.2). |
| 2026-11-21 | 3 | Plan detallado de la Etapa 3: la BD medida con SELECT (82 tablas, 115 SP, 10 CHECK, 126 FK); tareas E3-1 a E3-9; nuevos D-029 a D-032, U-073, U-074, M-101 y P-024. Sigue E3-1 (Opus 5.5). |
| 2026-11-21 | 3 | E3-1 hecha: `-Entorno ensayo` (solo SQL Server local, autenticación de Windows, BD `sgvEnsayo_*`; conexión común en `WebSGV/Database/conexion-bd.ps1`) en `sql.ps1`, `aplicar-migraciones.ps1` y `aplicar-procedimientos.ps1`; `herramientas/respaldo/ensayar-migracion.ps1` (último `.bacpac` u otro con `-RespaldoNuevo` → `sgvEnsayo_*` → migraciones → SP y humo → `[FactBD]` → `-ArchivoControl` → borra; log en `<carpeta de respaldos>\ensayos\`); `BaseDePruebas` acepta `sgvEnsayo_*` solo en servidor local; humo (M-052, cerrada) con `dm_exec_describe_first_result_set_for_object` y 4 permitidos (comprobado que detecta una columna renombrada y una tabla inexistente); migración **52** con los triggers `TR_*_Auditoria` (eran 12, no 14: van 11 y el de Grifo queda para E3-8), ensayada recreando dos borrados y registrada en pruebas; hash de `16_HoraPeru.sql` aceptado en `$HashesAceptados` tras comparar `fn_AhoraPeru`; `IntegridadBdTests` (6 pruebas) en verde contra pruebas y contra un ensayo. Sigue E3-2. |
| 2026-11-21 | 3 | E3-2 hecha: `sp_RechazarLiquidacion` deja los despachos en `PROGRAMADO` (0 `EN_PROCESO` en los SP); `sp_DC_RegistrarFirmaAdmin` y `sp_DC_CerrarViajesProgreso` con `ROLLBACK` + `THROW` (ya no filtran texto técnico al conductor ni al motivo del retiro); `EstadoSeguimiento.DerivarDeImportacion` (EN_CURSO/FINALIZADO; cierre = término de descarga, salida de bodega o llegada a la base final, decisión del usuario); `sp_SE_ImportarLote` no reabre al reimportar un viaje FINALIZADO/CANCELADO. Pruebas: `ErroresYEstadosEnSpTests`, `EstadoSeguimientoTests`, `[FactBD]` de rechazo y reimportación. Ensayo local OK (221 `[FactBD]`); 4 SP aplicados en pruebas (98 iguales). `probador`: pantallas OK a 1366/390; la importación no tiene vista previa y no se probó con datos reales. Sigue E3-3. |
| 2026-11-21 | 3 | E3-3 hecha: migración **53** (`Correlativo(serie, anio, ultimo)`, sembrada con el máximo `VP-AAAA-NNN` de cada año sin `VP-SIM-1111`; en pruebas `VP 2026 = 80`), `sp_SiguienteCorrelativo` (MERGE con `UPDLOCK, HOLDLOCK`) y `sp_SiguienteNumeroViaje` (único con `'VP-'`, salta números ya usados, 4+ cifras desde el 1000) usados por `sp_CrearDespacho`, `sp_CrearViajeProgreso` (ahora con transacción) y `sp_LD_ReubicarDespachoEnViajeDelConductor`; `aplicar-procedimientos.ps1` aplica primero los SP nuevos (FALTA) y luego los cambiados. Pruebas: `CorrelativoViajeBdTests` (año 2099: no repite tras borrar, 1000 con 4 cifras, salta usados; ruta real del despacho) y CI sin `COUNT(*)+1`. Ensayo local OK (227 `[FactBD]`); 53 y 5 SP aplicados en pruebas (100 iguales); 67 pruebas afectadas en verde. Riesgos aceptados: la fila del correlativo queda bloqueada hasta el commit del lote (dos creaciones de viaje a la vez esperan en vez de fallar) y un 1205 raro entre finalizar lote y reubicar en ListaDespachos (sin pérdida de datos). No se creó un despacho real con el `probador` (BD con datos reales): la ruta real la cubre la `[FactBD]`. M-051 cerrada. Sigue E3-4. |
| 2026-11-21 | 3 | E3-4 hecha: `SET XACT_ABORT ON` junto al `SET NOCOUNT ON` de los 18 SP con `BEGIN TRAN` que no lo tenían (los 25 lo tienen ahora); todos sus `CATCH` ya hacían `ROLLBACK` + `THROW`, sin TRY/CATCH internos que siguieran la transacción, y los 6 SP sin TRY/CATCH hacen `ROLLBACK`/`RETURN` antes de cualquier `RAISERROR` dentro de la transacción. Los llamadores en C# con `SqlTransaction`/`TransactionScope` (editar lote, bandeja, finalizar lote, liquidación del conductor) deshacen y relanzan: ninguno sigue usando la transacción tras un error. Prueba de CI `TransaccionesEnSpTests` (XACT_ABORT antes del primer `BEGIN TRAN`; todo `CATCH` con `THROW`/`RAISERROR`; sin comentarios). Ensayo local OK (227 `[FactBD]`, 1 omitida); 18 SP aplicados en pruebas (100 iguales, humo sin errores); suite completa 2533 en verde, 1 omitida. Sigue E3-5 (espera D-029 a D-031). |
| 2026-11-21 | 3 | E3-5 hecha (aprobada por el usuario ese día): respaldo `sgvActualizada_20261121_1654.bacpac`; script `WebSGV/Database/Scripts/corregir_datos_etapa3_2026-11-21.sql` (una transacción con `XACT_ABORT`; guardas THROW 50030 si los conteos no son los esperados ni 0, control final THROW 50031; una fila de `AuditoriaLog` por corrección con los ids y valores anteriores). D-029: 19 despachos 272-290 a `ANULADO`; D-030: 5 992 seguimientos `COMPLETADO` (todos con fecha de cierre según la regla de `sp_SE_ImportarLote`) a `FINALIZADO`; D-031: 5 `idUsuarioModificacion` inexistentes a NULL; U-015: usuarios 91, 92 y 94 desactivados con `selloSesion` nuevo (no borrados). Ensayado en `sgvEnsayo_20261121_1654` (dos veces: la segunda no cambia nada; una guarda provocada aborta sin cambios; 227 `[FactBD]` en verde) y aplicado en pruebas: controles 19/5992/5/3 → 0/0/0/0. M-023 queda en parte: los 2 520 importados de 2024 ya salen Cerrados; siguen abiertos 247 importados de 2024 en `EN_CURSO`. U-074 autorizado. Sigue E3-6. |
| 2026-11-21 | 3 | E3-6 hecha: migración **54** (30 CHECK: estados y tipos de Despachos, ViajesEnProgreso, OrdenViaje, SeguimientoExportacion y FirmaDigital (etapa con NULL), rol con los retirados solo inactivos, moneda de FacturaFlete, ANULADO ⇔ `activo = 0` en Despachos y montos >= 0 en liquidación, facturas, CPIC y carga; `OrdenViaje.estadoAprobacion` sin DEFAULT `APROBADO`, NOT NULL con DEFAULT `PENDIENTE`; NOT NULL en `OrdenViaje.estadoViaje` y `Usuarios.rol`) y **55** (fuera 14 FK repetidas, buscadas por estructura; `DocumentosFactura`/`DocumentosCPIC` sin cascada; 10 FK nuevas; fuera `IX_Despachos_Fecha`, `IX_Despachos_FechaDespacho` e `IX_Despachos_Conductor`; 5 índices de apoyo). Cada regla cuenta antes y se detiene con THROW; todo WITH CHECK. Dominios medidos en pruebas: 0 valores fuera, 0 huérfanos. Pruebas: `IntegridadBdTests` (listas exactas de CHECK y FK, sin FK ni índices repetidos, DEFAULT `PENDIENTE`) y `RestriccionesDominioBdTests`; 11 archivos de pruebas `[FactBD]` que escribían fuera de dominio, corregidos. Tras el `revisor`: `sp_SE_ImportarLote` normaliza el estado del Excel (COMPLETADO → FINALIZADO; PENDIENTE, NULL u otro → EN_CURSO), porque ImportarSeguimiento publicado (`60853e8`) aún manda COMPLETADO/PENDIENTE y la 54 lo rechazaría: va **antes** de la 54; activar un usuario con rol retirado avisa (`UsuarioEstadoService`); la 54 conserva la intercalación al pasar columnas a NOT NULL. Revisados contra `60853e8` los demás SP que reciben estado, tipo, origen o moneda: el tipo de operación sale de listas validadas y el resto ya tenía CHECK o validación. Ensayo sobre `sgvActualizada_20261121_1742.bacpac`: OK (54 en 182 ms, 55 en 200 ms; 240 `[FactBD]` en verde, 1 omitida); suite sin BD 2333 en verde. Aplicada en `sgvActualizada` el 2026-11-21: primero `sp_SE_ImportarLote` (100 SP iguales), luego 54 (1 641 ms) y 55 (1 328 ms; 14 FK repetidas quitadas, 2 cascadas, 10 FK, 3 índices quitados, índices de apoyo); suite completa contra pruebas 2 573 en verde, 1 omitida; `probador` OK en usuarios (aviso al activar al usuario 94), anular lote, liquidar hasta el resumen, seguimiento y dashboard, sin errores nuevos en el log. Datos de la prueba: D-033. Cierra M-050. Sigue E3-7. |
| 2026-11-21 | 3 | E3-7 hecha: migración **56** (`56_QueryStoreAcotado.sql`): Query Store de la BD actual (`DB_NAME()`, SQL dinámico) con `MAX_STORAGE_SIZE_MB = 100`, captura y limpieza por tamaño `AUTO` y `READ_WRITE`; idempotente; THROW 56001 con el mensaje original si falta permiso. Ensayo sobre `sgvActualizada_20261121_1810.bacpac` OK (241 `[FactBD]`, 1 omitida; el `.bacpac` no trae datos del Query Store: 0 MB). Aplicada en `sgvActualizada`: antes READ_WRITE, 96 de 1000 MB, AUTO/AUTO; después READ_WRITE, 96 de 100 MB (la limpieza AUTO lo baja al 80 % al pasar del 90 %: P-026). `IntegridadBdTests` y `BaseDeDatosTests` contra pruebas: 25 en verde. Cierra M-053. Sigue E3-8. |
| 2026-11-21 | 3 | E3-8 hecha: migración **57** (DROP de 18 SP, 5 vistas y 1 función: Grifo, Maquinaria, Indicadores, el seguimiento viejo y `sp_LD_ActualizarConductorDominanteViajes`; se detiene si un objeto que se queda los nombra) y **58** (fuera `Usuarios.idOperador` y `OrdenViaje.tipoViaje`; `ViajesEnProgreso.observaciones` de ntext a nvarchar(max) con su intercalación; DROP de 18 tablas: Maquinaria, ubigeo, `CPIC_Productos` con su trigger, 4 detalles viejos de la liquidación y el modelo de segmentos y subtramos; 2 filas en total, con tope por tabla y control de FK entrantes). Fuera porque el sitio publicado (`60853e8`) los nombra o cuelgan de algo que se queda: `sp_DC_RevocarFirma` (M-054), las tablas de Grifo y las que cuelgan de ellas, `GuiasTransportista` y `OrdenViajeAjuste` (M-106). Código: `VehiculoService` ya no consulta las tablas de Grifo, `sp_DC_InsertarOrdenViajeConductor` sin `tipoViaje`, fuera la regla `.total-row` y 8 scripts y el SP viejo del repo. `aplicar-migraciones.ps1` aplica antes de una migración los SP que declare con `-- Requiere SP:`; la 58 se detiene si un objeto que se queda nombra algo que borra (58005) y quita antes todas las FK de sus tablas. Ensayo sobre `sgvActualizada_20261121_1834.bacpac` OK (el SP requerido se aplica solo antes de la 58; 243 `[FactBD]`, 1 omitida); `probar-publicar.ps1` OK; suite sin BD 2333 en verde. Aplicada en `sgvActualizada` el 2026-11-21: `aplicar-migraciones.ps1` aplicó la 57 (24 objetos), el SP requerido y la 58 (18 tablas, 3 columnas); SP 99 iguales, humo 0 errores; suite completa contra pruebas 2 576 en verde, 1 omitida; `probador` OK (tractos y carretas con aviso de placa, VerificarDocumento, liquidaciones del admin y del conductor, detalle de viaje, observación con ñ en nvarchar(max), liquidar, enviar sin firmar y retirar, menús de admin.transporte, contabilidad y admin.sistema, casos negativos; sin errores nuevos en el log). Datos de la prueba: D-034. Sigue E3-9. |
| 2026-11-21 | 3 | E3-9: **Etapa 3 cerrada con nota 8.** Medido en `sgvActualizada` (con el analista): 58 migraciones, 0 pendientes; 99 SP iguales al repo y 0 errores de humo; 37 CHECK y 92 FK, todos validados; 0 FK ni índices repetidos; 0 tablas sin PK de 65; 25 de 25 SP con transacción con `XACT_ABORT ON`; `estadoAprobacion` NOT NULL con DEFAULT `PENDIENTE`; Query Store 4 de 100 MB, READ_WRITE; solo `TR_AbastecimientoCombustible_Auditoria` sin archivo, a propósito (M-106). `PASO_A_PRODUCCION.md` con los pasos y controles de las migraciones 52 a 58 (SELECT ejecutables antes y después) y `aplicar-migraciones.ps1` que aplica solo los SP que cada migración necesita (`-- Requiere SP:` y el mapa para la 54), ensayado desde un `.bacpac` anterior a la 54 (54 a 58 seguidas, 243 `[FactBD]` en verde). No pasa de 8: la app entra como `db_owner` (U-073), no hay respaldo a un punto en el tiempo (U-077), `Indicadores`/`CargarExcel` sin decidir (U-017) y `sgvTransporte` no tendrá estas reglas hasta el paso a producción (U-011). Sigue la Etapa 4, fase de análisis. |
| 2026-11-21 | 4 | Análisis con 4 analistas (acceso y sesión; autorización, descargas y subidas; navegador; configuración, dependencias y BD) y plan escrito: E4-1 a E4-16. Sin huecos de acceso ni XSS explotable; lo más grave es la exposición (repositorio público con datos personales en el árbol y el token de apisperu en el historial) y que nada sostiene las guardas en CI. Nuevos U-078 a U-086, P-030 a P-032 y M-111. |
| 2026-11-21 | 4 | E4-1 hecha: fuera del árbol el script de choferes de 2026-10-24 (copia en `privado/`, y `PASO_A_PRODUCCION.md` apunta allí), números ficticios en 4 archivos de código y 4 de pruebas, sin usuario por defecto en `grabar-liquidacion.mjs`, `.gitignore` para los documentos sueltos de `docs/`; `DatosPersonalesRepoTests` (`[FactBD]`) cruza el árbol con `Conductor`: 87 coincidencias antes, 0 después. Los valores siguen en el historial (U-078). Nombres reales en el árbol: M-112. |
| 2026-11-21 | 4 | E4-2 hecha: `GuardasAccesoTests` (prueba de fuente en CI: 36 páginas con su guarda como primera sentencia, 7 públicas y 1 manual; 23 WebMethods con `EnableSession` y sus comprobaciones negadas antes de cualquier otra sentencia; `no-store` en `Site.Master` y en las páginas sin master); `DashboardConductor` con `ExigirRolConductor` fuera de `try`; `SesionHelper.IdConductor`; `DescargarPdfOrdenViaje` con `ExigirSesion`. Revisor aprobado (2 rondas en la prueba); `probador`: 4 roles, 1366 y 390 px, Atrás tras cerrar sesión y 6 exportaciones OK. Nuevos M-113, P-033 y P-034. |
| 2026-11-21 | 4 | E4-4 hecha: migración **61** (`IntentoAcceso`) y SP atómicos `sp_US_ReservarIntentoAcceso` (los tres niveles en un viaje, `UPDLOCK, HOLDLOCK`, limpieza que no propaga errores) y `sp_US_DevolverIntentoAcceso`, más `sp_US_QuitarIntentoAcceso`; ensayo OK y aplicados en pruebas. Login con tres límites (IP 20→5 min; cuenta e IP 5→15 min; cuenta 30/h→15 min); usuarios fuera de `[A-Za-z0-9._@-]` cuentan en `USR_invalido` (la colación igualaba variantes de ancho completo o con U+FEFF); `LOGIN_FALLIDO` (también cuentas inactivas) y `LOGIN_BLOQUEADO`; firma, recuperación y verificación pública en la BD; fuera `LimiteConsultas`. Revisor: 3 rondas; `probador`: mensaje y tiempo iguales, bloqueo que sobrevive al reinicio, 10 logins en paralelo contados exactos. Nuevos M-114, M-115, U-087 y P-035. |
| 2026-11-22 | 4 | E4-5 hecha: en `Web.config`, CSP aplicada (`frame-ancestors 'self'; object-src 'none'; base-uri 'self'; form-action 'self'`), CSP en modo informe (solo `report-uri`: con `report-to` Chrome no usa `report-uri` y no entrega por http), `Permissions-Policy`, COOP y CORP `same-origin`; receptor anónimo `CspInforme.ashx` (solo POST, 8 KB, 20/min por IP y 300/h, cada combinación una vez por hora, ruta sin querystring). `probador`: 0 bloqueos de la parte aplicada en 3 roles a 1366 y 390 px; inventario: solo `script-src` por `eval`, de MicrosoftAjax en cada página, WebUIValidation y AutoPostBack (E4-15 ajustada). Revisor 2 rondas. Nuevos P-036 y P-037. |
| 2026-11-22 | 4 | E4-6 hecha: un único `SGV.escaparHtml` (fuera 5 copias, la del mapa no escapaba comillas); 12 `return confirm(` a `data-confirmar` y fuera su interceptor; `JavaScriptStringEncode` en `DashboardConductor.aspx.cs`; select2 solo 4.0.13 sin el tema de Bootstrap 5; fuera 5 scripts y `Content/Site.css` sin uso; `rel="noopener"`; los borradores de liquidación del navegador se borran al llegar a Login tras cerrar sesión (no si cancela el aviso de salir ni si vence la sesión). 5 pruebas nuevas en `CodigoFuenteLimpiezaTests` sobre todo el código propio. `probador`: 0 fallos (11 páginas con confirmación, tablas con respuestas simuladas porque la BD de pruebas no tiene órdenes). Revisor 2 rondas. Nuevos M-116, M-117, P-038 a P-040 y D-035. |
