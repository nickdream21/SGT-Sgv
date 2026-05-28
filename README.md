# WebSGV — Sistema de Gestión Viviana

[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4)](#stack-tecnológico)
[![App](https://img.shields.io/badge/ASP.NET-Web%20Forms-0078D4)](#arquitectura-de-alto-nivel)
[![DB](https://img.shields.io/badge/SQL%20Server-Relacional-CC2927)](#base-de-datos-y-stored-procedures)
[![Idioma](https://img.shields.io/badge/Docs-es--PE-0A7E3E)](#documentación-relacionada)

Plataforma web para la operación de transporte y construcción de **Servicios Generales Viviana E.I.R.L.**. WebSGV centraliza despachos, órdenes de viaje, liquidaciones, abastecimiento, reportes y auditoría, con control por roles y trazabilidad de eventos.

## Resumen ejecutivo

- **Tipo de aplicación:** ASP.NET Web Forms sobre .NET Framework 4.8.
- **Enfoque:** operación diaria (despachos, viajes, firmas, reportes) con reglas de negocio en C# + SQL Server.
- **Seguridad:** autenticación basada en sesión, helpers de hash de contraseñas y auditoría persistente.
- **Estado del repo:** solución única `WebSGV.sln` con proyecto principal `WebSGV/WebSGV.csproj`.

## Stack tecnológico

| Capa | Tecnología principal |
|---|---|
| Backend | C#, ASP.NET Web Forms (.NET Framework 4.8) |
| Datos | SQL Server + ADO.NET (sin ORM) |
| UI | Bootstrap 5, jQuery, AjaxControlToolkit |
| Documentos | iTextSharp 5.5.13.4, EPPlus, ClosedXML |
| Build | MSBuild / Visual Studio 2022 |
| Configuración auxiliar | Data API Builder (`dab-config.json`) para uso externo/MCP |

> NuGet usa **`packages.config` (legacy)**. No migres a `PackageReference` sin evaluación técnica previa.

## Arquitectura de alto nivel

WebSGV sigue un enfoque Web Forms clásico con páginas `.aspx` + code-behind:

1. **Presentación (Views):** páginas por módulo de negocio (`WebSGV/Views/`).
2. **Lógica de aplicación:** code-behind y servicios/helpers en `Helpers/` y `Services/`.
3. **Persistencia:** SQL Server mediante `SqlConnection`, `SqlCommand` y stored procedures.
4. **Auditoría y sesión:** validación por sesión + registro de eventos en tabla de auditoría.

### Módulos funcionales clave

- Gestión de despachos y viajes.
- Liquidaciones y firma digital.
- Abastecimiento y control de gastos.
- Registros maestros (conductores, unidades, clientes, rutas, productos).
- Reportes operativos y consultas.
- Gestión de usuarios y roles.

## Requisitos previos

- **Windows** con Visual Studio 2022 (workload ASP.NET y desarrollo web).
- **.NET Framework 4.8 Developer Pack**.
- **SQL Server** accesible con permisos para ejecutar scripts y stored procedures.
- **IIS Express** (incluido con Visual Studio) o IIS local.
- **NuGet Restore** habilitado.

## Instalación y configuración local

### 1) Clona el repositorio

```powershell
git clone <URL_DEL_REPOSITORIO>
```

### 2) Configura secretos locales (obligatorio)

Este proyecto separa secretos en archivos **no versionados**:

- `WebSGV/connectionStrings.config`
- `WebSGV/appSettings.Secrets.config`
- `.env` (solo para `dab-config.json`, no para la app Web Forms)

Usa `WebSGV/connectionStrings.Local.config.example` como referencia para tu entorno local y solicita credenciales al equipo.

> ⚠️ Nunca publiques credenciales reales en GitHub, issues o pull requests.

### 3) Restaura paquetes NuGet

- Abre `WebSGV.sln` en Visual Studio.
- Ejecuta **Restore NuGet Packages**.

## Build y ejecución

## Opción A — Visual Studio (recomendado)

1. Abre `WebSGV.sln`.
2. Define `WebSGV` como Startup Project.
3. Ejecuta con **IIS Express**.
4. Accede a la URL local que muestra Visual Studio.

La aplicación usa como página inicial:

- `Views/Login.aspx` (definido en `WebSGV/Web.config` como `defaultDocument`).

## Opción B — MSBuild

Compila la solución desde Developer Command Prompt:

```powershell
msbuild WebSGV.sln /t:Build /p:Configuration=Debug
```

## Estructura del repositorio

```text
SGT-Sgv/
├─ WebSGV.sln
├─ WebSGV/
│  ├─ WebSGV.csproj
│  ├─ Views/                  # Páginas ASPX por módulo
│  ├─ Helpers/                # Seguridad, auditoría, utilitarios
│  ├─ Services/               # PDF, firma, lógica de integración
│  ├─ Database/
│  │  ├─ Schema/              # DDL versionado por orden
│  │  ├─ StoredProcedures/    # 1 archivo .sql por SP
│  │  └─ Scripts/             # scripts de migración/diagnóstico
│  ├─ App_Start/
│  └─ Web.config
├─ docs/                      # Documentación técnica y funcional
├─ packages/                  # Dependencias NuGet (packages.config)
└─ .opencode/                 # Sistema multi-agente del proyecto
```

## Base de datos y stored procedures

El repositorio versiona SQL como archivos fuente, pero **no despliega automáticamente**.

### Flujo recomendado

1. Agrega/actualiza scripts en `WebSGV/Database/`.
2. Versiona cambios en Git.
3. Ejecuta manualmente en SQL Server (SSMS o herramienta equivalente).
4. Verifica resultados en entorno de destino.

### Importante

- `Schema/` contiene cambios estructurales (DDL) en orden.
- `StoredProcedures/` contiene definición por procedimiento.
- `Scripts/` contiene migraciones y diagnósticos puntuales.
- Si el SP no está desplegado en BD, el código que lo consume fallará.

## Seguridad y buenas prácticas

- No commitees `connectionStrings.config`, `appSettings.Secrets.config` ni `.env` con secretos.
- Usa helpers existentes para contraseñas (`PasswordHelper`, `HashHelper`).
- Usa `AuditoriaHelper` para registrar acciones relevantes.
- Respeta control de acceso con `RolesHelper` (no hardcodees roles ad hoc).
- Mantén `App_Data/` y `Uploads/` fuera de commits de artefactos temporales.

## Convenciones de desarrollo

- Idioma de dominio: español (`Registro`, `Liquidación`, `Chofer`, etc.).
- Cultura de aplicación: `es-PE`.
- Nuevas páginas Web Forms requieren 3 archivos:
  - `.aspx`
  - `.aspx.cs`
  - `.aspx.designer.cs`
- Registra nuevos archivos en `WebSGV.csproj` para evitar fallos de despliegue.

## Troubleshooting rápido

| Síntoma | Causa probable | Acción recomendada |
|---|---|---|
| Redirección inesperada a Login | Sesión expirada o excepción de ViewState | Verifica `Session["UsuarioID"]`, tiempo de sesión y postback de controles |
| Error al invocar SP | SP no desplegado en SQL Server destino | Ejecuta script de `Database/StoredProcedures/` en la BD correcta |
| Página nueva no aparece/publica | Archivo no incluido en `.csproj` | Registra `.aspx`, `.aspx.cs`, `.designer.cs` en `WebSGV.csproj` |
| Datos sensibles visibles en repo local | Secretos en archivos de config | Migra secretos a archivos gitignored y rota credenciales si hubo exposición |

## Roadmap sugerido (breve)

- Estandarizar checklist de despliegue SQL por ambiente.
- Fortalecer observabilidad de errores (logging centralizado por módulo).
- Incrementar cobertura de documentación operativa por rol.
- Evaluar pipeline CI para build de validación en pull requests.

## Documentación relacionada

- `docs/FLUJO_DE_TRABAJO_SGV.md`
- `docs/GUIA_CREACION_ROLES.md`
- `docs/MEJORAS_UI_ABASTECIMIENTO.md`
- `docs/STACK_Y_FLUJO_SGV.md`
- `AGENTS.md` (guía técnica para sesiones asistidas por agentes)

## Licencia y nota legal

Este repositorio **no declara actualmente una licencia OSS formal** en la raíz (por ejemplo, MIT/Apache-2.0).

Hasta definir una licencia explícita, considera el código como de uso interno del propietario del proyecto y no redistribuible sin autorización.
