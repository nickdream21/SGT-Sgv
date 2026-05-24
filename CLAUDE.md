# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

**SGV (Sistema de Gestión de Viajes)** — a transport/logistics management system for a Peruvian company. ASP.NET **Web Forms** (.NET Framework 4.8), single Visual Studio 2022 solution: `WebSGV.sln` / `WebSGV/WebSGV.csproj`. All source lives under `WebSGV/`. There is no JS/Node build pipeline, no CI workflow, and no test project.

A side tool (`dab-config.json` + `.env`) exposes the database over REST/GraphQL via Azure Data API Builder — it is not consumed by the web app.

## Build

```powershell
# Build (Windows, MSBuild required — Framework 4.8 + WebForms GUID)
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal

# There is NO `dotnet build` — the project type is not SDK-style
```

Run via IIS Express or Visual Studio (no Kestrel). Default startup page is `Views/Login.aspx` (set via `<defaultDocument>` in `WebSGV/Web.config`).

NuGet uses the **legacy `packages.config` model** — packages restore to the top-level `packages/` directory. Do not convert to PackageReference.

## Config and Secrets

`WebSGV/Web.config` splits secrets into gitignored sibling files:

- `WebSGV/connectionStrings.config` — DB connection string (`ConexionSGV`).
- `WebSGV/appSettings.Secrets.config` — SMTP and API keys.
- Root `.env` — consumed only by `dab-config.json`, not the web app.

Non-sensitive `appSettings` (company name, RUC, PDF archive path) live inline in `Web.config` and are read by `Helpers/EmpresaConfigHelper.cs`.

## Architecture

### Request flow

1. User hits a `.aspx` URL directly (no MVC routing — `App_Start/RouteConfig.cs` is empty).
2. `Site.Master` / `Site.Mobile.Master` renders the shell; all view pages extend it.
3. Code-behind (`Page_Load`) verifies session, checks role, loads data on `!IsPostBack`.
4. Data access goes directly to SQL Server via `SqlCommand` (no ORM, no repository abstraction) using the `ConexionSGV` connection string.
5. Writes invoke stored procedures (named `sp_*`); reads may use inline SQL or SPs.

### Authentication and session

Session-based only (no ASP.NET Forms Auth). After login, three session keys are set:

| Key | Content |
|---|---|
| `Session["UsuarioID"]` | Integer user ID (presence = authenticated) |
| `Session["Rol"]` | Role string, compared UPPERCASED |
| `Session["Nombre"]` | Display name |

Cookie: `SGV_SessionId`, 30-minute timeout, `HttpOnly`, `SameSite=Lax`.

`Global.asax.cs` `Application_Error` silently swallows ViewState exceptions and redirects to `~/Views/Login.aspx?error=sesion` — if you see unexpected login redirects during development, that is why.

### Authorization

Two overlapping helpers exist — prefer `SecurityHelper` (newer) for new code:

- `WebSGV/WebSGV/Helpers/SecurityHelper.cs` — `ExigirSesion()`, `ExigirRolAdmin()`, `ExigirRolAdminOSupervisor()`, `ExigirRolAdminOGrifo()`, plus `AgregarHeadersSeguridad()`.
- `WebSGV/Views/RolesHelper.cs` (namespace `WebSGV.Helpers`) — `ValidarAccesoSeccion(seccion)`, `TienePermiso(seccion)`.

Role constants (always compare `.ToUpper().Trim()`):

| Constant | DB value | Also accepts |
|---|---|---|
| `ROL_ADMIN` | `ADMIN` | `ADMINISTRADOR` |
| `ROL_ADMIN_SISTEMA` | `ADMINISTRADOR DE SISTEMA` | — |
| `ROL_CONDUCTOR` | `CONDUCTOR` | `CHOFER` |
| `ROL_SUPERVISOR` | `SUPERVISOR` | — |
| `ROL_ADMIN_GRIFO` | `ADMINISTRADOR DE GRIFO` | — |
| `ROL_ADMIN_MAQUINARIA` | `ADMINISTRADOR DE MAQUINARIA` | — |
| `ROL_OPERADOR` | `OPERADOR` | — |

Roles are stored as plain text in the `Usuarios.rol` column — there is no separate roles table. The `Usuarios` table also stores PBKDF2 password hashes in format `{iterations}.{salt_base64}.{hash_base64}`.

### Business modules and page layout

All pages live under `WebSGV/Views/`. Role-specific dashboards redirect on login:

- ADMIN / SUPERVISOR → `Inicio.aspx`
- CONDUCTOR → `DashboardConductor.aspx`
- ADMINISTRADOR DE GRIFO → `DashboardGrifo.aspx`
- OPERADOR → `DashboardOperador.aspx`

Core business flow: **Despacho → ViajesProgreso → Liquidación del Conductor (firma digital) → Revisión Admin → PDF archivado**.

The digital signature is a PNG biometric trace captured on canvas, stored in the `FirmaDigital` table (append-only, SHA-256 hash). Admin approval writes to `OrdenViajeAjuste` without invalidating the conductor's original signature. Signed PDFs land in `~/App_Data/OrdenesViaje/`.

### Services and PDF generation

- `Services/PdfOrdenViajeService.cs` — travel order PDFs.
- `Services/PdfAbastecimientoService.cs` — fuel supply vouchers.
- `Services/FirmaService.cs` — digital signature embedding.

All use **iTextSharp 5.5.13.4** (legacy AGPL version, not iText 7) + EPPlus 8 / ClosedXML. EPPlus requires `ExcelPackage.License.SetNonCommercialPersonal(...)` called at startup (done in `Global.asax.cs`).

### Helper inventory

| File | Purpose |
|---|---|
| `Helpers/AuditoriaHelper.cs` | Write audit events to `AuditoriaLog` table (auto-created on first run) |
| `Helpers/EmpresaConfigHelper.cs` | Read company data from `Web.config` appSettings |
| `Helpers/FechaHelper.cs` | Date formatting for es-PE locale |
| `Helpers/NumeroALetrasHelper.cs` | Number-to-words conversion (Peruvian Spanish) |
| `Helpers/PasswordHelper.cs` | PBKDF2 hash + verify; handles legacy plaintext migration |
| `Helpers/HashHelper.cs` | SHA-256 hashing utilities |
| `WebSGV/Helpers/SecurityHelper.cs` | Session guards, role checks, HTTP security headers |

### Database

SQL Server hosted on somee.com. Schema and stored procedures are tracked as `.sql` files in `WebSGV/Database/` but **are not auto-applied**:

- `Database/Schema/` — DDL migrations numbered `01_`, `02_`, ... apply in order.
- `Database/StoredProcedures/` — one file per proc; naming convention: `sp_DC_*` (Dashboard Conductor), `sp_LD_*` (Liquidaciones), `sp_MQ_*` (Maquinaria), `sp_SE_*` (Seguimiento Exportación), etc.
- `Database/Scripts/` — ad-hoc migration / diagnostic scripts.

Any new SP must be executed manually against the DB (SSMS / sqlcmd) before calling code will work.

## Adding a New Page

Every new page requires **exactly three files** AND **registration in `WebSGV.csproj`**:

1. `WebSGV/Views/NombrePagina.aspx` — markup with `MasterPageFile="~/Site.Master"` and `Inherits="WebSGV.Views.NombrePagina"`.
2. `WebSGV/Views/NombrePagina.aspx.cs` — code-behind; verify session and role in `Page_Load` before `!IsPostBack` logic.
3. `WebSGV/Views/NombrePagina.aspx.designer.cs` — auto-generated partial class (can start empty).

In `WebSGV.csproj`, add both a `<Content>` entry and two `<Compile>` entries (`.aspx.cs` with `<SubType>ASPXCodeBehind</SubType>` and `.aspx.designer.cs`). Without `.csproj` registration the page does not deploy.

Page_Load template:
```csharp
protected void Page_Load(object sender, EventArgs e)
{
    SecurityHelper.ExigirRolAdmin(); // or appropriate guard
    SecurityHelper.AgregarHeadersSeguridad();
    if (!IsPostBack)
        CargarDatos();
}
```

## Adding a Stored Procedure

1. Create `WebSGV/Database/StoredProcedures/sp_MODULO_Nombre.sql` using the idempotent pattern (`IF OBJECT_ID ... DROP` before `CREATE`). Include `SET NOCOUNT ON`.
2. Execute the file manually against the somee.com database.
3. Call from C# using `CommandType.StoredProcedure`; pass nullable parameters as `(object)value ?? DBNull.Value`.

## Conventions

- **Language**: all identifiers, UI text, and SQL are in Spanish (es-PE). Match existing naming: `Agregar`, `Buscar`, `Registrar`, `Despacho`, `Liquidacion`.
- **Globalization**: `culture="es-PE"` configured in `Web.config`. Use `FechaHelper.cs` for dates and `NumeroALetrasHelper.cs` for number-to-words.
- **Auditing**: use `AuditoriaHelper.Registrar(accion, tabla, id, descripcion)` for all INSERT/UPDATE/DELETE operations. Audit failures are swallowed and must never break the main operation.
- **Passwords**: always use `PasswordHelper.HashPassword` / `PasswordHelper.VerifyPassword`. Do not implement alternative hashing.
- **SQL injection**: all queries use parameterized `SqlCommand.Parameters.AddWithValue(...)`. Never concatenate user input into SQL strings.

## Reference Docs

Functional/process documentation (Spanish) in `docs/`:

- `docs/FLUJO_DE_TRABAJO_SGV.md` — end-to-end business flow and role permission matrix.
- `docs/GUIA_CREACION_ROLES.md` — step-by-step guide for adding a new role.
- `docs/MEJORAS_UI_ABASTECIMIENTO.md` — UI conventions for the fuel supply module.

Also see `AGENTS.md` at the repo root for additional architecture notes and the multi-agent coordination system under `.opencode/`.
