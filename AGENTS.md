# AGENTS.md

Repo-specific notes for OpenCode sessions. Generic .NET / ASP.NET advice is omitted.

## What this is

- ASP.NET **Web Forms** app (`.aspx` + code-behind), **.NET Framework 4.8**, MSBuild / Visual Studio 2022 solution.
- Single project: `WebSGV/WebSGV.csproj`, solution `WebSGV.sln`.
- NuGet uses the **legacy `packages.config`** model (`WebSGV/packages.config`) with packages restored to the top-level `packages/` directory. Do **not** convert to PackageReference casually.
- No JS/Node build, no test project, no CI workflow (the `.github/workflows/` folder is empty).
- There is an Azure Data API Builder config (`dab-config.json` + `.env`) exposing the same DB over REST/GraphQL/MCP, but it is **not** wired into the Web Forms app — it's a side tool.

## Build / run

- Build with MSBuild or Visual Studio. There is no `dotnet build` workflow (Framework 4.8 + WebForms project type `{FAE04EC0-...}`).
- Default startup page is `Views/Login.aspx` (set via `<defaultDocument>` in `WebSGV/Web.config`).
- Run via IIS Express / Visual Studio. No Kestrel.

## Secrets / config layout (important)

`WebSGV/Web.config` pulls secrets from sibling files that are **gitignored**:

- `WebSGV/connectionStrings.config` — DB connection (`ConexionSGV`).
- `WebSGV/appSettings.Secrets.config` — SMTP and other secrets.
- Root `.env` — only consumed by `dab-config.json` (`@env('DATABASE_CONNECTION_STRING')`), not by the web app.

`.gitignore` lines `**/connectionStrings.config` and `**/appSettings.Secrets.config` are intentional. Never commit replacements. The current files in the working tree contain real credentials for the somee.com SQL host — treat as sensitive even though they're locally present.

Non-secret `appSettings` (e.g. `Empresa.RazonSocial`, `Empresa.Ruc`, `OrdenViaje.RutaArchivo`) live inline in `Web.config` and are read by `Helpers/EmpresaConfigHelper.cs` for PDF generation.

## Architecture quirks

- All pages live under `WebSGV/Views/` (not under root). URL routing is minimal — `App_Start/RouteConfig.cs` is essentially empty; pages are reached by direct `.aspx` paths.
- `Global.asax.cs` `Application_Start` calls `AuditoriaHelper.CrearTablaAuditoriaSiNoExiste()` — the audit table is auto-created on first run.
- `Application_Error` swallows ViewState exceptions and redirects to `~/Views/Login.aspx?error=sesion`. If you see silent login redirects, that's why.
- Authentication is **session-based**, not Forms Auth. Session keys: `Session["UsuarioID"]`, `Session["Rol"]`, `Session["Nombre"]`. Cookie name `SGV_SessionId`, 30 min timeout.

### Roles

`WebSGV/Views/RolesHelper.cs` (note: file is in `Views/` but namespace is `WebSGV.Helpers`) is the single source of truth for authorization. Use its constants and `ValidarAccesoSeccion(...)` / `TienePermiso(...)` rather than inlining role string checks. Recognized roles:

`ADMIN` (also accepts `ADMINISTRADOR`, `ADMINISTRADOR DE SISTEMA`), `CONDUCTOR` (also `CHOFER`), `SUPERVISOR`, `ADMINISTRADOR DE GRIFO`, `ADMINISTRADOR DE MAQUINARIA`, `OPERADOR`.

Role comparisons are uppercase and trimmed — keep new roles consistent.

### Database

- SQL Server (somee.com). Schema and stored procs are tracked **as `.sql` files** in `WebSGV/Database/`:
  - `Schema/` — DDL migrations, prefixed `01_`, `02_`, ... apply in order.
  - `Scripts/` — ad-hoc migration / data scripts (`script_*.sql`, `diagnostico_*.sql`).
  - `StoredProcedures/` — one file per proc, prefix convention `sp_DC_*` for "Dashboard Conductor" flow.
- These are **not auto-applied**. New schema/proc work means: add the `.sql` file here AND run it against the DB manually. Code that calls a new proc will fail until the proc is deployed.

### PDF / signature flow

- `Services/PdfOrdenViajeService.cs`, `Services/PdfAbastecimientoService.cs`, `Services/FirmaService.cs` use **iTextSharp 5.5.13.4** (note: the legacy AGPL version, not iText 7) plus EPPlus 8 / ClosedXML.
- Signed PDFs are archived under `~/App_Data/OrdenesViaje` (`OrdenViaje.RutaArchivo` in `Web.config`). `App_Data/` and `Uploads/` contain runtime artifacts — don't commit churn there.

## Conventions

- Spanish identifiers and UI text throughout (`Agregar`, `Buscar`, `Liquidacion`, etc.). Match existing naming when adding pages.
- Globalization is `es-PE`. Dates and decimals follow Peru locale; `Helpers/FechaHelper.cs` and `Helpers/NumeroALetrasHelper.cs` exist for this.
- Passwords: use `Helpers/PasswordHelper.cs` / `HashHelper.cs` — do not roll new hashing.
- Auditing: write through `Helpers/AuditoriaHelper.cs` so events land in the auto-created audit table.
- New `.aspx` pages need all three files (`.aspx`, `.aspx.cs`, `.aspx.designer.cs`) **and** must be added to `WebSGV.csproj` `<Content>` / `<Compile>` item groups, otherwise they won't deploy.

## Sistema multi-agente

Este repo tiene un sistema de coordinación multi-agente bajo `.opencode/`:

- **`.opencode/agents/`** — 7 prompts: `orchestrator` (único que habla con el
  usuario) + 6 subagentes especializados: `arquitecto`, `developer`, `dba`,
  `qa`, `reviewer`, `writer`. Lee `.opencode/agents/README.md` para el mapa
  de modelo/runtime de cada uno.
- **`.opencode/state/`** — estado persistente entre sesiones:
  `project-state.json` (plan global + métricas), `templates/` (esquemas JSON
  `task_assignment` y `task_result` para handoffs entre agentes).
- **`.opencode/skills/`** — 6 skills cargables: `frontend-design`,
  `systematic-debugging`, `verification-before-completion`,
  `test-driven-development`, `nueva-pagina-aspx`, `nueva-sp-sql`.
- **`.opencode/mcp/`** — configuración del **SQL MCP Server** (Data API Builder)
  que el agente DBA usa para leer la DB, diagnosticar bugs y ejecutar SPs ya
  desplegados. El MCP **no soporta DDL** — los `.sql` siguen requiriendo
  despliegue manual con SSMS/sqlcmd. Setup detallado en
  `.opencode/mcp/sqlserver-setup.md`.

Reglas del sistema:
- Solo el `orchestrator` mantiene conversación con el usuario.
- Toda delegación entre agentes va por JSON (`task_assignment` → `task_result`).
- El DBA versiona el `.sql` en `Database/` **antes** de desplegar, aunque
  tenga acceso al MCP. El archivo en git es la fuente de verdad.
- El reviewer no modifica archivos — solo reporta findings clasificados.
- Tras cualquier despliegue de SP, verificar via MCP `describe_entities` y
  `execute_entity` con caso happy path.

## Existing docs worth reading

Functional/process docs live in `docs/` (Spanish):

- `docs/FLUJO_DE_TRABAJO_SGV.md` — end-to-end business flow.
- `docs/GUIA_CREACION_ROLES.md` — how to add a new role (matches `RolesHelper.cs`).
- `docs/MEJORAS_UI_ABASTECIMIENTO.md` — UI conventions for the abastecimiento module.

Consult these before making cross-cutting changes to roles or the dispatch/liquidation flow.
