# Estado del sistema multi-agente — `.opencode/state/`

Aquí vive el estado persistente del orquestador entre sesiones de OpenCode.
Todos los archivos están versionados en git para que cualquier sesión nueva
sepa en qué se quedó la anterior.

## Archivos

### `project-state.json`

Estado vivo del proyecto. Lo lee el orquestador al inicio de cada sesión
y lo escribe tras cada handoff.

Esquema (campos principales):

| Campo                          | Tipo    | Descripción                                                |
|--------------------------------|---------|------------------------------------------------------------|
| `project`                      | string  | Nombre del proyecto (`WebSGV`)                             |
| `last_updated`                 | ISO date| Última escritura                                           |
| `current_task_id`              | string  | TASK-XXX en curso, null si no hay nada activo              |
| `global_plan`                  | array   | Lista de tareas planificadas (TASK-XXX) con su estado      |
| `active_subtasks`              | array   | Subtareas TASK-XXX.Y delegadas y pendientes de cerrar      |
| `context_summary`              | string  | Resumen comprimido cuando el contexto crece                |
| `decisions_log`                | array   | Decisiones cerradas con fecha y task_id                    |
| `agents_registry`              | object  | Modelo + runtime + status de cada agente                   |
| `metrics`                      | object  | Contadores (tasks, handoffs, compactations)                |

### `templates/task_assignment.json`

Plantilla que el orquestador rellena para delegar una subtarea atómica a un
subagente. Campos clave: `task_id`, `to`, `objective`, `context`, `deliverables`,
`constraints`.

### `templates/task_result.json`

Plantilla que cualquier subagente rellena al devolver resultado al
orquestador. Campos clave: `status`, `artifacts`, `summary`, `findings`,
`blockers`, `deployment_required`, `next_suggested_agent`.

## Flujo recomendado de actualización

1. **Inicio de sesión**: el orquestador lee `project-state.json`
2. **Cada delegación**: rellena un `task_assignment` y lo entrega al subagente
3. **Cada respuesta**: valida el `task_result` contra la plantilla, agrega
   los `artifacts` a `active_subtasks`
4. **Cierre de subtarea**: marca `completed`, mueve a `decisions_log` si
   aplica, incrementa `metrics.total_tasks_completed`
5. **Compactación**: cuando el contexto crece (>70%), resume las decisiones
   antiguas en `context_summary` e incrementa `metrics.context_compactions`

## ¿Por qué versionar este estado?

- **Continuidad entre sesiones**: si se cierra OpenCode, la siguiente sesión
  retoma exactamente donde quedó leyendo `project-state.json`.
- **Trazabilidad**: el `decisions_log` es histórico explícito; combinado con
  `git log`, da auditoría completa.
- **Colaboración**: si dos personas trabajan en ramas, el merge de
  `project-state.json` muestra qué se hizo en cada una.

## Reglas

- **Nunca** edites manualmente `project-state.json` durante una sesión activa
  — pídele al orquestador que lo haga.
- **Nunca** commitees secretos en `decisions_log` (passwords, tokens).
- Si quieres reiniciar el sistema multi-agente, copia un snapshot del archivo
  actual en `state/snapshots/YYYY-MM-DD.json` antes de truncarlo.
