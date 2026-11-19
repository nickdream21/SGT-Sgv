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

**Etapa actual: 1 — fase PLAN** (iniciada 2026-11-19).

| # | Etapa | Nota hoy | Meta | Estado |
|---|---|---|---|---|
| 1 | Operación y continuidad | 4 | 7 | **PLAN en curso** |
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

Plan detallado: *(se escribe al terminar la fase PLAN)*

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
