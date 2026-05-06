# Estado del sistema multi-agente — `.opencode/state/`

Estado persistente del orquestador entre sesiones. **Schema v2** (refactor 2026-04-29). Todo versionado en git para que cualquier sesión nueva retome donde quedó la anterior.

## Archivos

### `project-state.json` (schema 2.0)

Estado vivo. Lo lee el orquestador al inicio de cada sesión y lo escribe tras cada handoff.

Campos principales:

| Campo                   | Tipo     | Descripción                                                          |
|-------------------------|----------|----------------------------------------------------------------------|
| `schema_version`        | string   | "2.0" (no editar manualmente)                                        |
| `project`               | string   | "WebSGV"                                                             |
| `last_updated`          | ISO date | Última escritura                                                     |
| `current_task_id`       | string   | TASK-XXX en curso o null                                             |
| `global_plan`           | array    | Tareas planificadas con estado                                       |
| `active_subtasks`       | array    | TASK-XXX.Y delegadas pendientes de cerrar                            |
| `context_summary`       | string   | Resumen comprimido cuando el contexto crece                          |
| `decisions_log`         | array    | Decisiones cerradas con ts                                           |
| `intent_routing_table`  | object   | **NUEVO v2**: mapa intent → owner + skills + evidence_required       |
| `parallelism_matrix`    | object   | **NUEVO v2**: reglas de paralelismo entre agentes                    |
| `risk_register`         | array    | **NUEVO v2**: riesgos abiertos con owner y mitigación                |
| `agents_registry`       | object   | Modelo + fallback + runtime + status de cada agente                  |
| `skills_registry`       | object   | **NUEVO v2**: 11 skills + scope + audiencia                          |
| `mcp_servers`           | object   | Configuración del SQL MCP Server (DAB)                               |
| `verification_gate`     | object   | **NUEVO v2**: reglas de cierre con evidence                          |
| `metrics`               | object   | Contadores (tasks, handoffs, parallel_groups, verification_rejections)|

### `templates/task_assignment.json` (v2)

Plantilla para delegar subtareas. Campos clave nuevos:

- `intent`: clasificador canónico (new_page, new_sp, bugfix, …).
- `context.skills_required`: skills que el subagente debe cargar.
- `context.files_forbidden`: archivos que no puede tocar.
- `context.decisions_context`: ADRs / decisions_log relevantes.
- `evidence_required`: qué evidencia se exigirá al cerrar.
- `parallel_group_id`: marcador de grupo paralelo.

### `templates/task_result.json` (v2)

Plantilla de respuesta. Campos clave nuevos:

- `skills_loaded`: skills efectivamente cargadas durante la tarea.
- `evidence`: build_exit_code, sp_smoke_test_output, manual_test_results, etc.
- `findings[*].category`: clasificación adicional (security/performance/…).
- `decisions_made`: elecciones tomadas con rationale.
- `rollback_plan`: obligatorio si `deployment_required=true`.

## Flujo de actualización

1. **Inicio de sesión** — orquestador lee `project-state.json`.
2. **Clasificación** — orquestador clasifica intent usando `intent_routing_table`.
3. **Pre-flight** — orquestador valida `risk_register`, `agents_registry.status`, `mcp_servers.status`, archivos prohibidos.
4. **Delegación** — rellena `task_assignment` v2 y lo entrega vía tool `task`.
5. **Recepción** — valida `task_result` contra `evidence_required` del intent.
6. **Verification gate** — si evidence falta → `status="needs_clarification"`, devolver al owner, incrementar `metrics.verification_rejections`.
7. **Cierre** — marca completed, mueve decisión a `decisions_log` si aplica, incrementa `metrics.total_tasks_completed` y `metrics.total_handoffs`.
8. **Compactación** — cuando contexto > 70%, resume decisiones antiguas en `context_summary`, incrementa `metrics.context_compactions`.

## Paralelismo

Subtareas con mismo `parallel_group_id` se lanzan en un solo turno (varias tool calls `task()` en paralelo). Reglas en `project-state.json` → `parallelism_matrix`. Tras todas completadas, orquestador incrementa `metrics.parallel_groups_executed`.

## ¿Por qué versionar este estado?

- **Continuidad**: si se cierra OpenCode, la siguiente sesión retoma desde `project-state.json`.
- **Trazabilidad**: `decisions_log` + `git log` = auditoría completa.
- **Colaboración**: el diff del archivo en merge muestra qué hizo cada rama.

## Reglas

- **Nunca** editar manualmente `project-state.json` durante una sesión activa — pedirle al orquestador.
- **Nunca** commitear secretos en `decisions_log`.
- Para reiniciar el sistema: copiar snapshot a `.opencode/state/snapshots/YYYY-MM-DD.json` antes de truncar.
- Cualquier cambio de `schema_version` requiere ADR del arquitecto.

## Migración v1 → v2 (registro histórico)

Cambios introducidos el 2026-04-29:
- Plantillas ganan `intent`, `evidence_required`, `skills_loaded`, `decisions_made`, `rollback_plan`, `parallel_group_id`.
- `project-state.json` gana `intent_routing_table`, `parallelism_matrix`, `risk_register`, `skills_registry`, `verification_gate`, métricas adicionales.
- 5 skills WebSGV-specific creadas: sql-injection-y-sqlcommand-seguro, itextsharp-pdf-webforms, auditoria-y-sesiones-sgv, msbuild-csproj-sync, viewstate-postback-webforms.
- Orchestrator gana verification gate + intent routing + paralelismo seguro.
