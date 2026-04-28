# Sistema multi-agente — `.opencode/agents/`

Un orquestador + 6 subagentes especializados que coordinan el desarrollo del
proyecto WebSGV. Cada agente vive como un archivo Markdown con su prompt de
sistema y instrucciones de invocación.

## Mapa rápido

| Agente         | Responsabilidad principal                              | Modelo recomendado    | Runtime               |
|----------------|--------------------------------------------------------|-----------------------|-----------------------|
| `orchestrator` | Hablar con el usuario, planificar, delegar             | Claude Sonnet 4.6 (Copilot) | OpenCode (sesión activa) |
| `arquitecto`   | Decisiones de diseño de alto nivel                     | openai/gpt-5.5 → fallback Sonnet 4.6 | OpenCode `task`       |
| `developer`    | Escribir código C# / .aspx / actualizar .csproj        | openai/gpt-5.1-codex-max → fallback Sonnet 4.6 | OpenCode `task` |
| `dba`          | T-SQL + ejecución vía SQL MCP Server                   | github-copilot/gpt-5.3-codex | OpenCode `task` + MCP |
| `qa`           | Casos de prueba manuales y SQL de verificación         | openai/gpt-5.4        | OpenCode `task`       |
| `reviewer`     | Auditoría de calidad/seguridad/convenciones            | Claude Sonnet 4.6 (Copilot) | OpenCode `task`       |
| `writer`       | Documentación en `docs/` y `AGENTS.md`                 | openai/gpt-5.4        | OpenCode `task`       |

## Único agente que habla con el usuario

**Solo el `orchestrator`** mantiene conversación con la persona. Todos los
demás reciben un JSON `task_assignment` y devuelven un JSON `task_result`.
Las plantillas exactas están en `.opencode/state/templates/`.

## Cómo se ejecuta una tarea

```
Usuario  →  orchestrator  →  task_assignment JSON  →  subagente
                                                          │
                                                          ▼
Usuario  ←  orchestrator  ←  task_result JSON     ←  subagente
```

1. El usuario describe lo que quiere
2. El `orchestrator` lee `.opencode/state/project-state.json` y planifica
3. Crea `TASK-XXX` con subtareas `TASK-XXX.1`, `.2`, ...
4. Para cada subtarea genera un `task_assignment` (ver plantilla)
5. Invoca al subagente correcto (ver columna *Runtime*)
6. Recibe el `task_result`, lo valida y registra en `project-state.json`
7. Si hay `blockers` u `open_questions`, los resuelve antes de avanzar
8. Cuando toda la `TASK-XXX` está cerrada, reporta al usuario en lenguaje
   natural

## Reglas globales

- El orquestador **no implementa** — solo delega.
- El DBA **versiona el `.sql` en git ANTES de desplegar**, incluso si tiene
  acceso al MCP. El `.sql` es la fuente de verdad.
- El reviewer **no modifica** archivos — solo reporta findings.
- El writer toca **solo** `docs/` y `AGENTS.md`.
- Cualquier blocker detenido por un subagente se eleva al usuario antes de
  improvisar.

## Skills + agentes

Los agentes pueden cargar skills de `.opencode/skills/` cuando aplican:

| Skill                              | Quién la usa     |
|------------------------------------|------------------|
| `frontend-design`                  | developer        |
| `systematic-debugging`             | developer, dba   |
| `verification-before-completion`   | todos            |
| `test-driven-development`          | qa, developer    |
| `nueva-pagina-aspx`                | developer        |
| `nueva-sp-sql`                     | dba              |

## Configuración del MCP de SQL Server (DBA)

El agente DBA aprovecha el **SQL MCP Server** que ya viene en `dab-config.json`.
Para activarlo y exponerlo a OpenCode, sigue:

`.opencode/mcp/sqlserver-setup.md`

## Estado del sistema

- Plan global y métricas: `.opencode/state/project-state.json`
- Plantillas de mensajes: `.opencode/state/templates/`
- Tras cada handoff, el orquestador actualiza el estado.
