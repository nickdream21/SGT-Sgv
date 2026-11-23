# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

**SGV (Sistema de Gestión de Viajes)** — a transport/logistics management system for a Peruvian company. ASP.NET **Web Forms** (.NET Framework 4.8), single Visual Studio 2022 solution: `WebSGV.sln` (web project `WebSGV/WebSGV.csproj` + xUnit tests in `WebSGV.Tests/`). All web source lives under `WebSGV/`. There is no JS/Node build pipeline. CI: `.github/workflows/build.yml` runs `nuget restore` + `msbuild /t:Build` on every PR/push to master (windows-latest).

## Build

```powershell
# Build (Windows, MSBuild required — Framework 4.8 + WebForms GUID)
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal

# There is NO `dotnet build` — the project type is not SDK-style
```

Run via IIS Express or Visual Studio (no Kestrel). Default startup page is `Views/Login.aspx` (set via `<defaultDocument>` in `WebSGV/Web.config`).

### Running and testing in the browser

You can launch and test the app yourself — don't tell the user it isn't possible (see `herramientas/sitio-local/README.md`):

1. Build, then run `herramientas/sitio-local/iniciar-sitio.ps1` **in the background** (it no-ops if the site already answers). Site: `https://localhost:44390` (or `http://localhost:50291` on a machine without `.vs\`), against the TEST DB.
2. Browser: use the Claude in Chrome tools (`mcp__claude-in-chrome__*`; the extension works in Chrome or Edge — the office PC uses Edge). If `list_connected_browsers` is empty, the extension is not connected on this machine: give the user the steps in that README (install from claude.ai/chrome, same claude.ai account, restart Chrome, `/chrome` in Claude Code) and meanwhile test over HTTP.
3. Without a browser: `herramientas/sitio-local/probar-importacion.ps1 -Archivos <zip/pdf>, <docx>` logs in and runs RegistroDespacho's "Leer documentos", printing the preview (read-only). Follow its curl + hidden-field pattern for other pages.
4. Test users per role are in `privado/CREDENCIALES_USUARIOS.txt` (gitignored, copied by hand to each machine). Read them from the file in scripts; never paste the passwords into chat. Only on the local site / TEST DB.
5. Real sample documents (Vitapro ZIP, Word programaciones, driver list) are in `docs/DOCUMENTACION/` (gitignored).

`.ps1` files with accents must be saved as UTF-8 **with BOM**: Windows PowerShell 5.1 reads BOM-less files as ANSI and fails to parse.

NuGet uses the **legacy `packages.config` model** — packages restore to the top-level `packages/` directory. Do not convert to PackageReference.

## Config and Secrets

`WebSGV/Web.config` splits secrets into gitignored sibling files:

- `WebSGV/connectionStrings.config` — DB connection string (`ConexionSGV`).
- `WebSGV/appSettings.Secrets.config` — SMTP and API keys.

Azure Data API Builder (`dab-config*.json`, root `.env`) was removed on 2026-11-19 (unused, exposed `Usuarios` with password hashes); don't re-add it (enforced by `CodigoFuenteLimpiezaTests`). To query the DB use `WebSGV/Database/sql.ps1`.

Non-sensitive `appSettings` (company name, RUC, PDF archive path) live inline in `Web.config` and are read by `Helpers/EmpresaConfigHelper.cs`.

## Architecture

### Request flow

1. User hits a `.aspx` URL directly. Pure WebForms: no MVC, Razor, bundling, FriendlyUrls or AjaxControlToolkit (removed 2026-10-07; don't re-add). `App_Start/BundleConfig.cs` only maps the ScriptManager `"jquery"` resource.
2. `Site.Master` renders the shell (one responsive master; there is no mobile master); all view pages extend it.
3. Code-behind (`Page_Load`) verifies session, checks role, loads data on `!IsPostBack`.
4. Data access goes directly to SQL Server via `SqlCommand` (no ORM, no repository abstraction) using the `ConexionSGV` connection string.
5. Writes invoke stored procedures (named `sp_*`); reads may use inline SQL or SPs.

### Authentication and session

Session-based only (no ASP.NET Forms Auth). After login, three session keys are set (`SesionHelper.Iniciar`); read and write all three always via `SesionHelper` (`IdUsuario`, `Rol`, `Nombre`, `HaySesion`, `*Actual`), never `Session["..."]` directly (enforced by `CodigoFuenteLimpiezaTests`):

| Key | Content |
|---|---|
| `Session["UsuarioID"]` | Integer user ID (`int`; a valid value = authenticated) |
| `Session["Rol"]` | Role string, compared UPPERCASED |
| `Session["Nombre"]` | Display name |

Cookie: `SGV_SessionId`, 30-minute timeout, `HttpOnly`, `SameSite=Lax`.

`Global.asax.cs` `Application_Error` silently swallows ViewState exceptions and redirects to `~/Views/Login.aspx?error=sesion` — if you see unexpected login redirects during development, that is why.

### Authorization

Two overlapping helpers exist — prefer `SecurityHelper` (newer) for new code:

- `WebSGV/Helpers/SecurityHelper.cs` — `ExigirSesion()`, `ExigirRolAdmin()`, `ExigirRolAdminOSupervisor()`, plus `AgregarHeadersSeguridad()`. Without a session they go to Login; with a session but the wrong role they go to that role's home page (`RolesHelper.UrlInicioSegunRol`, the single source for per-role home pages), so the user is not logged out. The guards end the response (`Response.Redirect(url, true)`); keep them at the top of `Page_Load`, outside any `try`. `[WebMethod]`s skip `Page_Load`, so each one must check session/role itself (`EnableSession = true`).
- `WebSGV/Views/RolesHelper.cs` (namespace `WebSGV.Helpers`) — `ValidarAccesoSeccion(seccion)`, `TienePermiso(seccion)` (session role) and `TienePermiso(seccion, rol)`: the pure per-section matrix, pinned by `RolesHelperTests` (change a permission → change that test on purpose).

Role constants (always compare `.ToUpper().Trim()`). `EsRolAdmin` accepts `ADMIN`, `ADMINISTRADOR` and `ADMINISTRADOR DE TRANSPORTE`, but **not** `ADMINISTRADOR DE SISTEMA` (only users and audit):

| Constant | DB value | Also accepts |
|---|---|---|
| `ROL_ADMIN` | `ADMIN` | `ADMINISTRADOR` |
| `ROL_ADMIN_TRANSPORTE` | `ADMINISTRADOR DE TRANSPORTE` | — (same permissions as `ROL_ADMIN`) |
| `ROL_ADMIN_SISTEMA` | `ADMINISTRADOR DE SISTEMA` | — |
| `ROL_CONDUCTOR` | `CONDUCTOR` | `CHOFER` |
| `ROL_SUPERVISOR` | `SUPERVISOR` | — |
| `ROL_CONTABILIDAD` | `CONTABILIDAD` | — |

Roles are stored as plain text in the `Usuarios.rol` column — there is no separate roles table. The `Usuarios` table also stores PBKDF2 password hashes in format `sha256.{iterations}.{salt_base64}.{hash_base64}` (PBKDF2-HMAC-SHA256); the older `{iterations}.{salt_base64}.{hash_base64}` (SHA1, 10000) still verifies and is converted on the next login.

### Business modules and page layout

All pages live under `WebSGV/Views/`. Role-specific dashboards redirect on login (`RolesHelper.UrlInicioSegunRol`):

- ADMIN / ADMINISTRADOR / ADMINISTRADOR DE TRANSPORTE / SUPERVISOR → `Inicio.aspx`
- CONDUCTOR / CHOFER → `DashboardConductor.aspx`
- ADMINISTRADOR DE SISTEMA → `DashboardAdminSistema.aspx`
- CONTABILIDAD → `LiquidacionesAprobadasContabilidad.aspx`

Core business flow: **Despacho → ViajesProgreso → Declaración del conductor (firma) → Revisión Admin (firma, POR_CONFORMAR) → Conformidad del conductor (firma, APROBADO) → PDF archivado**.

Signatures are electronic signatures (Ley 27269), not certificate-based: a handwritten PNG trace on canvas plus the re-entered password (`ReautenticacionHelper`) for the conductor, session credentials for the admin. They live in the append-only `FirmaDigital` table (`etapa` DECLARACION / REVISION / CONFORMIDAD / ANULACION, `codigoVerificacion`, SHA-256 of the PDF, `huellaContenido` = SHA-256 of amounts and reasons). Every discount/reimbursement in `DescuentosReintegros` needs its reason, and the conductor must sign his conformity with the final amounts before the order becomes APROBADO; reverting or withdrawing anulls the later signatures (`sp_LD_DevolverLiquidacionARevision`). Each signature produces its own PDF (PDF/A-2B, verification code in the footer, evidence page) in `~/App_Data/OrdenesViaje/`. **A signed PDF is never regenerated**: `ArchivoOrdenViajeService` serves the stored file only if its hash matches. Public check: `Views/VerificarDocumento.aspx`. Legal texts the signer accepts live only in `TextosFirma` (`Services/Liquidaciones/FirmaLiquidacion.cs`).

### Services and PDF generation

- `Services/PdfOrdenViajeService.cs` — travel order PDFs.
- `Services/FirmaService.cs` — digital signature embedding.
- `Services/Reportes/ReporteTabular.cs` — shared Excel + PDF layout for tabular reports (company header, filters, totals, summary). New report exports should build a `ReporteTabular` instead of styling ClosedXML/QuestPDF by hand.

PDFs use **QuestPDF 2026.5.0** (Community license, set in `Global.asax.cs` and in the services' static constructors — do NOT reintroduce iTextSharp/iText). QuestPDF ships native Skia binaries that MSBuild copies to `bin\runtimes\win-x86|x64\native\` via the package's `build\net4\QuestPDF.targets` import in the csproj — they must be deployed along with the app. Excel import/export uses **ClosedXML 0.105** only (EPPlus was removed for licensing reasons).

### Database

SQL Server hosted on somee.com. Schema and stored procedures are tracked as `.sql` files in `WebSGV/Database/` but **are not auto-applied**:

- `Database/Schema/` — DDL migrations numbered `01_`, `02_`, ... Apply them with `Database/aplicar-migraciones.ps1 -Entorno pruebas|produccion` (add `-SoloListar` for a dry run; production also needs `-ConfirmoProduccion`); a migration that needs a new SP first declares it in its header with `-- Requiere SP: sp_X.sql` and the script applies that SP file right before it (for an already-applied migration whose file can't change, the map `$SpRequeridosPorMigracion` in the script does the same). It runs the pending ones in order and records each in `dbo.SchemaVersion` (created by `00_ControlMigraciones.sql`). Migrations must be idempotent (`IF COL_LENGTH/OBJECT_ID ...`); never edit an applied one — add a new number.
- `Database/StoredProcedures/` — one file per proc (`CREATE OR ALTER`); naming convention: `sp_DC_*` (Dashboard Conductor), `sp_LD_*` (Liquidaciones), `sp_SE_*` (Seguimiento Exportación), etc. Every `sp_*` called from C# should have its file here.
- `Database/Scripts/` — ad-hoc migration / diagnostic scripts.

Any new SP must be executed manually against the DB (SSMS / sqlcmd) before calling code will work. `Database/aplicar-procedimientos.ps1 -Entorno pruebas -SoloListar` compares every SP file with the DB (FALTA / DISTINTO); without `-SoloListar` it applies the differing files. The repo is the source of truth for SPs. It also runs a smoke check (M-052): every repo SP is compiled with `sys.dm_exec_describe_first_result_set_for_object`; an error outside `$ErroresHumoPermitidos` (4 known false positives) exits 1 without "Listo". **Rehearse first**: `herramientas/respaldo/ensayar-migracion.ps1` imports the latest `.bacpac` into a local `sgvEnsayo_*` DB, runs migrations, SPs + smoke and the `[FactBD]` tests there, then drops it; the three DB scripts take `-Entorno ensayo -BaseDeDatos sgvEnsayo_*` (local server and Windows auth only). `Database/Scripts/reparar-tildes-bd.ps1` fixes mojibake (UTF-8 read as Windows-1252) in DB objects outside the repo.

To query or run a script against the DB, use `WebSGV/Database/sql.ps1 "<query>"` or `-Archivo file.sql` (test DB by default; reads the password from `connectionStrings.config` so it never appears on the command line; `-Texto` returns long text such as object definitions untruncated; production needs `-Entorno produccion -ConfirmoProduccion`). `aplicar-migraciones.ps1` hashes migrations with LF line endings (CRLF-era hashes are still accepted), so checkouts on different machines don't show false "changed" warnings.

Integration tests (`WebSGV.Tests/Integracion/`, `[FactBD]`) run against the TEST DB using `WebSGV/connectionStrings.config` or `SGV_TEST_CONNSTR`; they refuse any DB other than `sgvActualizada` or a local `sgvEnsayo_*` and are skipped in CI. `BaseDeDatosTests` is read-only (every `sp_*` called from C# exists, no `getdate()`, all migrations applied, no plaintext passwords). `FlujoDespachoLiquidacionTests` writes through the real services inside `using (new TransaccionDePrueba())` (a `TransactionScope` never completed, so everything rolls back, including the services' own `BeginTransaction` commits); any new test that writes must do the same. Put error cases last: an SP that runs `ROLLBACK` in its `CATCH` aborts the test's transaction too, and with `SET XACT_ABORT ON` (every SP with `BEGIN TRAN`, E3-4) any error raised by an SP, even a business `THROW`, leaves it aborted. With sqlcmd against somee use `-C -I -f 65001` (trust cert, QUOTED_IDENTIFIER for filtered indexes, UTF-8 for ñ).

### Front-end libraries

Page-specific CSS/JS lives in files, not inline: `Content/paginas/<Pagina>.css` and `Scripts/paginas/<Pagina>.js` (subfolders mirror `Views/`, e.g. `Exportacion/`; `Site.Master` → `SiteMaster.*`). Reference them with `<%= WebSGV.Helpers.RecursoHelper.Url("~/Scripts/paginas/X.js") %>` (adds `?v=` with the file date so browsers don't use a stale copy) and register them as `<Content>` in `WebSGV.csproj`. A `.js` file can't contain `<%= %>`: the page declares the server values right before it, inline — `var SGV = Object.assign(window.SGV || {}, { txtFecha: '<%= txtFecha.ClientID %>' });` — and the script uses `SGV.txtFecha` (e.g. `$('#' + SGV.txtFecha)`). Only `Error.aspx`/`Error404.aspx` keep inline CSS (they must render even if static files fail). **Enter** never submits the form by itself (a disabled submit is the first element of the `<form>` in `Site.Master`, and `SiteMaster.js` cancels Enter): to make Enter search or save, wrap the inputs in `<div data-enter="<%= btnX.ClientID %>">`; without it Enter does nothing (intended for create forms and anything that moves money). Never put real `type=password` inputs in `Site.Master` outside `<template id="tplCamposContrasena">` (Edge would offer saved passwords in search boxes). Use `SGV.avisar(mensaje[, { tipo, trasRecargar }])` (`Scripts/paginas/SiteMaster.js`) instead of `alert()`: alerts block the page; pass `trasRecargar: true` when a `location.reload()` follows. Likewise never `confirm()`: in JS use `SGV.confirmar(mensaje[, alAceptar][, { titulo, textoAceptar, peligro }])` (returns `Promise<bool>`), and on server buttons put `data-confirmar="texto"` (`\n` = line break; optional `data-confirmar-titulo`, `-aceptar`, `-peligro="true"`) instead of `OnClientClick="return confirm(...)"`: `SiteMaster.js` cancels the click, asks, and repeats it on accept (validators and UpdatePanel still work).

`Site.Master` loads jQuery 3.6.4 → Popper → Bootstrap **4.6.2** once, in `<head>` (jQuery UI was removed in E5-7: use `type="date"`, not `.datepicker()`). Pages must NOT load jQuery/Bootstrap again (a second jQuery replaces `$` and drops `.modal()`, `.dropdown()`, `.tab()`), and must use Bootstrap 4 syntax (`data-toggle`, `data-dismiss`, `class="close"`), not Bootstrap 5 (`data-bs-*`, `btn-close`). The ScriptManager `"jquery"` resource (unobtrusive validation) maps to `Scripts/jquery-si-falta.js`, which only loads jQuery if missing.

## Adding pages and stored procedures

Step-by-step in the skills `nueva-pagina` and `nuevo-sp` (`.claude/skills/`). Never skip: a page or file not registered in `WebSGV.csproj` does not deploy, and a new SP does nothing until it is applied to the DB.

## Working with subagents (mandatory since 2026-11-14)

The main session is the **orchestrator**: it talks to the user, decides, delegates, checks evidence and closes. It does not delegate the conversation, decisions about the business or the final word on quality. Subagents live in `.claude/agents/` (Claude Code cannot nest them, so only the main session launches them):

| Agent | Does | Never |
|---|---|---|
| `analista` | Reads code/DB (SELECT only), root cause, one recommended plan with exact files | edits files or writes to the DB |
| `desarrollador` | Implements the plan, tests, build, full test suite | commits, publishes, touches the plan or the pending/decision documents |
| `revisor` | Reviews `git diff`: bugs, security, real data, deploy, conventions; verified findings + verdict | edits |
| `probador` | Runs the local site, tests end to end by role and at 1366/390 px, with evidence | edits repo code, writes on the published site |

Flow, scaled to the task (use only the agents that add value):

1. **Small and obvious** (a typo, one-line fix, a question): the orchestrator does it directly.
2. **Bug or feature**: `analista` → orchestrator approves the plan (asks the user only for business decisions) → `desarrollador` → `revisor` and `probador` **in parallel** → if there are findings, back to `desarrollador` with the exact list (max. 2 rounds, then report to the user) → orchestrator updates the plan and the pending documents (see "Keeping the plan up to date"), checks `git status` and reports.
3. **Review/audit**: `revisor` and/or `probador` in parallel, plus `analista` for an area.

Orchestrator rules: give each subagent a self-contained brief (goal, context already known, files, acceptance criteria, what *not* to do); never trust a report without its evidence (build/test output, file:line, test table); launch independent agents in the same message. **One commit per finished task** (standing user instruction, 2026-11-14): as soon as a task is approved by `revisor`/`probador` and the plan is updated, commit only that task's files (never mix tasks; stage by path, never `git add -A`); work on several tasks in sequence, not interleaved in the working tree. Push and publish only when the user says so. Report to the user in Spanish, short: what changed, evidence, what is pending.

## Conventions

- **Language**: all identifiers, UI text, and SQL are in Spanish (es-PE). Match existing naming: `Agregar`, `Buscar`, `Registrar`, `Despacho`, `Liquidacion`.
- **Globalization**: `culture="es-PE"` configured in `Web.config`. Use `FechaHelper.cs` for dates and `NumeroALetrasHelper.cs` for number-to-words.
- **Auditing**: use `AuditoriaHelper.Registrar(accion, tabla, id, descripcion)` for all INSERT/UPDATE/DELETE operations. Audit failures are swallowed and must never break the main operation.
- **Passwords**: always use `PasswordHelper.HashPassword` / `PasswordHelper.VerifyPassword`. Do not implement alternative hashing. Plaintext passwords are no longer accepted (migrated with `Database/Scripts/migrar-contrasenas-texto-plano.ps1`).
- **Errors shown to users**: never display `ex.Message` directly — use `MensajeErrorHelper.ParaUsuario(ex)` (shows only `ErrorNegocioException` / SQL `RAISERROR` messages, logs the rest and shows a generic text). Throw `ErrorNegocioException` (`Services/Common/ErrorNegocio.cs`) for messages meant for the user; when wrapping, pass the inner exception.
- **Uploaded files** (`~/Uploads`) are hidden from direct URLs (Web.config `hiddenSegments`). To let a user open one, use `DocumentoHelper.UrlDescarga(rutaVirtual)` (per-session key → `Views/DescargarDocumento.aspx`). Use `DocumentoHelper.NombreSeguro(...)` for user text inside file names.
- **SQL injection**: all queries use parameterized `SqlCommand.Parameters.AddWithValue(...)`. Never concatenate user input into SQL strings.

## Reference Docs

Functional/process documentation (Spanish) in `docs/`:

- `docs/PLAN_MEJORAS_SGV.md` — **current improvement plan**: status, Top 20 and the open improvements (`M-xxx`). Read it first when resuming work. Machine setup is in `docs/PREPARAR_MAQUINA.md` and the production checklist in `docs/PASO_A_PRODUCCION.md`.
- `docs/PENDIENTES_USUARIO.md` — what only the user can do (`U-xxx`): business decisions, credentials and external panels, data to obtain.
- `docs/DATOS_POR_CORREGIR.md` — DB records to fix or load (`D-xxx`), with their control query; never written without the user's approval.
- `docs/PRUEBAS_PENDIENTES.md` — what is still to be tested in the browser, on a phone, with a real order/driver or on the published site (`P-xxx`), for the `probador` or the user.
- `docs/FLUJO_DE_TRABAJO_SGV.md` — end-to-end business flow and role permission matrix.
- `docs/DECISIONES_NEGOCIO.md` — business rules agreed with the user (`DN-xx`, with date/commit and where the code applies them); add new ones there.
- `docs/GUIA_CREACION_ROLES.md` — step-by-step guide for adding a new role.
- `docs/PASO_A_PRODUCCION.md` — the single ordered procedure to move the published site from `sgvActualizada` to `sgvTransporte` (control SELECTs, migrations, SPs, data reloads, connection switch); only when the user decides.
- `docs/PREPARAR_MAQUINA.md` — setting up a new machine: files outside git (incl. `WebSGV/appSettings.Secrets.config.example`), regenerating `privado/ftp-respaldo.xml`, tools and environment variables.

## Keeping the plan up to date (mandatory)

The plan is the only shared context between machines and sessions — never let it fall behind:

The same rules are written (in Spanish) under "Reglas" in the five pending/decision documents below, and are partly enforced by `CodigoFuenteLimpiezaTests` (no `[x]`, id format and uniqueness, last id, plan size, DNI-shaped numbers):

| What it is | Where it goes |
|---|---|
| Code or DB improvement | `docs/PLAN_MEJORAS_SGV.md` (`M-`) |
| Only the user can do it (decision, credential, external panel, data or document to obtain) | `docs/PENDIENTES_USUARIO.md` (`U-`) |
| Records to fix, complete or load | `docs/DATOS_POR_CORREGIR.md` (`D-`) |
| Test in the browser, on a phone, with a real role, order or driver, or on the published site | `docs/PRUEBAS_PENDIENTES.md` (`P-`) |
| Business rule agreed with the user | `docs/DECISIONES_NEGOCIO.md` (`DN-`) |
| Step of the move to production (`sgvTransporte`) | `docs/PASO_A_PRODUCCION.md` |
| Step every publication needs | "Antes y después de publicar" in `herramientas/publicar/README.md` |

- **New id**: the number after the `Último id usado` line in that document's header, and that line is updated in the same change. Ids are never reused (not even those of deleted or retired items) and never renumbered.
- **Format**: every loose end is its own `- [ ] **M-001** title — what and where (file if known) · A/B/C`, never buried inside another item or only in a chat reply. A: risk to data, money or security, or blocks real use; B: clear value; C: minor.
- **When a task is done** it is deleted from its document in the same commit, never marked `[x]`: the commit is the history (`git log`; `docs/archivo/HISTORIAL_2026.md` is frozen, never written to). Whatever it leaves pending gets a new id. Decisions (`DN-`) are not deleted: a changed one is marked "reemplazada por DN-yy".
- **In the same commit as any feature, fix, migration or SP**, update the "Última actualización" line of `docs/PLAN_MEJORAS_SGV.md` (date + previous commit), plus `docs/PASO_A_PRODUCCION.md` / the publish README if it affects production or publishing (migrations, SPs, DLLs, secrets, manual steps).
- **No personal data in `docs/`**: no DNI, driver's licences, phone numbers, birth dates, addresses or drivers' names (nor passwords or tokens): use the role, the id, the plate or the document number.
- The plan stays under 80 KB.
- What is applied only to the test DB the orchestrator also keeps in its own Claude memory (outside the repo).
- Before finishing a task, check `git status` for untracked files: commit them, gitignore them, or say why they stay.

`AGENTS.md` at the repo root only points other AI tools to this file; there is no other agent system in the repo.
