# Hoja de ruta de calidad SGV

Índice corto de las etapas de mejora que salieron del análisis del 2026-11-19. **Leer esto primero al
retomar el trabajo**: dice en qué etapa estamos y qué sigue. El detalle de cada tarea y el historial
siguen en `docs/PLAN_MEJORAS_SGV.md`.

## Cómo se trabaja (acordado con el usuario, 2026-11-19)

1. Una etapa a la vez, en el orden de la tabla. No se abre la siguiente hasta cerrar la actual.
2. Cada etapa tiene dos fases: **PLAN** (el analista investiga, se escriben las tareas con archivos y
   criterio de aceptación, el usuario resuelve las decisiones) y **EJECUCIÓN** (una tarea a la vez:
   desarrollador → revisor y probador → commit por tarea).
3. Al cerrar una etapa: se vuelve a medir su nota, se anota aquí y se pasa a la siguiente.
4. Lo que solo puede hacer el usuario (rotar contraseñas, decisiones de negocio, publicar) se lista
   aparte en cada etapa para que no frene lo demás.

## Estado

**Etapa actual: 1 — fase EJECUCIÓN, solo falta E1-5 (publicar)** (E1-1 a E1-4 y E1-6 a E1-10 hechas; respaldo diario a Google Drive funcionando desde el 2026-11-20; E1-5 espera la orden del usuario y las rotaciones de credenciales).
Reparto de modelos: Fable 5.2 analiza y planifica; Opus 5.5 ejecuta.

| # | Etapa | Nota hoy | Meta | Estado |
|---|---|---|---|---|
| 1 | Operación y continuidad | 4 | 7 | **En ejecución (9 de 10 hechas; falta publicar)** |
| 2 | Ordenar el plan de mejoras | 3 | 7 | Pendiente |
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
| **E1-5** | Publicar `master` (31 commits), borrar sobrantes, comprobar 404 de las páginas retiradas y la lista de verificación del plan. Requiere E1-1 y E1-2 antes. | Usuario da la orden; Claude acompaña | Pendiente |
| **E1-6** | Salud y vigilancia: `Salud.ashx` anónimo (200/503 con `SELECT 1`; `?gps=1` da 503 si hay viajes activos y el GPS lleva más de 60 min sin consultar), regla pura con pruebas. | Claude | **Hecho** (revisor y probador, 2 rondas); falta publicar y crear los monitores |
| **E1-7** | Correo ante errores: `CorreoHelper` común, `AvisoErrores` con límite (1 por tipo cada 30 min, 10 al día), desde `Application_Error`. Requiere SMTP configurado. | Claude | **Hecho** (revisor 2 rondas y probador; correo real recibido); falta `SmtpFrom` en el servidor al publicar |
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
- somee: cambiar la contraseña FTP y volver a guardarla en Visual Studio; opcional, la de la BD.
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
