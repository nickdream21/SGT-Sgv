# Sistema multi-agente — `.opencode/agents/`

Un orquestador + 6 subagentes especializados que coordinan el desarrollo del
proyecto WebSGV. **Schema v2** (refactor del 2026-04-29).

## Mapa rápido

| Agente         | Responsabilidad principal                              | Modelo recomendado                                   | subagent_type    |
|----------------|--------------------------------------------------------|------------------------------------------------------|------------------|
| `orchestrator` | Hablar con el usuario, planificar, delegar, gate       | Claude Sonnet 4.6 (Copilot)                          | (sesión activa)  |
| `arquitecto`   | Decisiones de diseño + ADRs                            | openai/gpt-5.5 → fallback Sonnet 4.6                 | `arquitecto`     |
| `developer`    | C# / .aspx / .csproj / Helpers / Services              | openai/gpt-5.1-codex-max → fallback Sonnet 4.6       | `developer`      |
| `dba`          | T-SQL + SQL MCP Server                                 | github-copilot/gpt-5.3-codex                         | `dba`            |
| `qa`           | Planes de prueba manuales + SQL de verificación        | openai/gpt-5.4                                       | `qa`             |
| `reviewer`     | Auditoría calidad/seguridad/convenciones (read-only)   | Claude Sonnet 4.6 (Copilot)                          | `reviewer`       |
| `writer`       | Documentación en `docs/` y `AGENTS.md`                 | openai/gpt-5.4                                       | `writer`         |

## Único agente que habla con el usuario

Solo el `orchestrator`. Todos los demás reciben `task_assignment` v2 y devuelven `task_result` v2. Plantillas en `.opencode/state/templates/`.

## Flujo de una tarea (v2)

```
Usuario → orchestrator
           ├─ clasifica intent (new_page|new_sp|bugfix|refactor|arch_decision|
           │                    code_review|qa_plan|doc_update|deploy_verify)
           ├─ pre-flight checks (estado, secretos, MCP, AGENTS.md)
           ├─ descompone en TASK-XXX.1 .. .N
           ├─ identifica grupos paralelos (parallel_group_id)
           ├─ delega vía task() con task_assignment v2
           ↓
        subagente carga skills_required → trabaja → devuelve task_result v2
           ↓
        orchestrator aplica VERIFICATION GATE
           ├─ si evidence falla → status=needs_clarification, devolver al owner
           └─ si pasa → registra, sigue
           ↓
        cuando todo cerrado → reporta al usuario en lenguaje natural
```

## Tabla intent → owner + skills + evidence

Definida canónicamente en `.opencode/state/project-state.json` →
`intent_routing_table`. Resumen:

| Intent          | Owner       | Skills auto-cargadas                                                                          | Evidence requerida                                  |
|-----------------|-------------|-----------------------------------------------------------------------------------------------|-----------------------------------------------------|
| new_page        | developer   | nueva-pagina-aspx + auditoria-y-sesiones-sgv + msbuild-csproj-sync + viewstate-postback-webforms | build_exit_code=0 + manual_test                    |
| new_sp          | dba         | nueva-sp-sql + sql-injection-y-sqlcommand-seguro                                              | sql versionado + sp_deployed=true + smoke_test      |
| bugfix          | developer   | systematic-debugging + verification-before-completion                                          | root_cause + build OK + repro/regression manual     |
| refactor        | developer   | msbuild-csproj-sync + verification-before-completion                                           | build OK + behavior_unchanged_proof                 |
| arch_decision   | arquitecto  | (según dominio)                                                                               | ADR documentado + tradeoffs                          |
| code_review     | reviewer    | sql-injection-y-sqlcommand-seguro + auditoria-y-sesiones-sgv (+ otras según código)           | findings clasificados                                |
| qa_plan         | qa          | test-driven-development + viewstate-postback-webforms                                          | casos con pasos + casos negativos por rol            |
| doc_update      | writer      | (según contenido)                                                                             | docs_diff + sin secretos                             |
| deploy_verify   | dba         | verification-before-completion                                                                | mcp describe_entities + execute_entity smoke         |

## Paralelismo seguro

Definido en `project-state.json` → `parallelism_matrix`.

| Combinación                | Política                                  |
|----------------------------|-------------------------------------------|
| developer + developer      | OK si archivos disjuntos                  |
| developer + dba            | OK si developer no depende de SP pendiente|
| developer + writer         | OK siempre                                |
| dba + dba                  | PROHIBIDO sobre misma DB                  |
| arquitecto + arquitecto    | PROHIBIDO                                 |
| writer + writer            | OK si carpetas distintas en docs/         |
| reviewer + cualquiera      | OK (reviewer solo lee)                    |
| qa + cualquiera            | OK (qa solo diseña casos)                 |

El orquestador marca subtareas paralelas con mismo `parallel_group_id` y las
lanza en un solo mensaje (varias tool calls `task()` en paralelo).

## Verification gate (no negociable)

El orquestador NO marca `completed` sin que `task_result.evidence` cumpla
los `evidence_required` del intent. Si falla:
- `status="needs_clarification"`
- devuelve al owner con motivo
- incrementa `metrics.verification_rejections`

## Reglas globales

- El orquestador **no implementa** — solo clasifica, delega, valida, reporta.
- El DBA **versiona el `.sql` en git ANTES de desplegar**, aunque tenga MCP.
- El reviewer **no modifica** archivos.
- El writer toca **solo** `docs/` y `AGENTS.md`.
- `Web.config`, `connectionStrings.config`, `appSettings.Secrets.config`, `.env`
  son archivos **prohibidos** en `task_assignment.files_to_modify` (RISK-004).
- Cualquier blocker se eleva al usuario antes de improvisar.

## Skills disponibles (11)

Globales:
- `frontend-design`
- `systematic-debugging`
- `verification-before-completion`
- `test-driven-development`

WebSGV-specific:
- `nueva-pagina-aspx`
- `nueva-sp-sql`
- `sql-injection-y-sqlcommand-seguro`
- `itextsharp-pdf-webforms`
- `auditoria-y-sesiones-sgv`
- `msbuild-csproj-sync`
- `viewstate-postback-webforms`

Audiencia por skill: ver `project-state.json` → `skills_registry`.

## Configuración del MCP de SQL Server (DBA)

`dab-config.json` ya está en el repo. Activación en
`.opencode/mcp/sqlserver-setup.md`.

Mientras `mcp_servers.sql-mcp-server.status == "pending_configuration"`, el
dba puede aún:
- Escribir los `.sql`.
- Pedir al orquestador que el usuario ejecute queries y reporte resultados.
- Marcar status="partial" hasta que MCP esté disponible.

## Estado del sistema

- Plan global, decisiones, riesgos, métricas: `.opencode/state/project-state.json`.
- Plantillas v2: `.opencode/state/templates/`.
- Tras cada handoff exitoso, orquestador actualiza el estado y métricas.
