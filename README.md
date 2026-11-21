# SGV — Sistema de Gestión de Viajes

Aplicación web de **Servicios Generales Viviana E.I.R.L.** para la operación de transporte de carga: despachos, seguimiento de viajes (con GPS), liquidación de gastos firmada electrónicamente por el conductor, revisión y conformidad, PDF archivados y reportes. Sitio publicado: https://sgvtransporte.somee.com

## Stack

| Capa | Tecnología |
|---|---|
| Backend | C#, ASP.NET Web Forms puro (.NET Framework 4.8); sin MVC, FriendlyUrls, bundling ni AjaxControlToolkit |
| Datos | SQL Server + ADO.NET (`SqlCommand` parametrizado, procedimientos `sp_*`), sin ORM |
| Interfaz | Bootstrap 4.6.2, jQuery 3.6.4, jQuery UI (cargados una vez en `Site.Master`) |
| PDF | QuestPDF 2026.5.0 (licencia Community; PDF/A-2B en los documentos firmados) |
| Excel | ClosedXML 0.105 |
| Pruebas | xUnit (`WebSGV.Tests/`) |
| Build | MSBuild / Visual Studio 2022; NuGet con `packages.config` |

## Compilar y probar

```powershell
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
```

No hay `dotnet build`. Las reglas del proyecto (arquitectura, convenciones, seguridad, base de datos, cómo levantar el sitio local y correr las pruebas) están en [`CLAUDE.md`](CLAUDE.md). Para preparar una PC nueva (herramientas, archivos de secretos, primera compilación): [`docs/PREPARAR_MAQUINA.md`](docs/PREPARAR_MAQUINA.md).

Los secretos van en archivos que no se versionan (`WebSGV/connectionStrings.config`, `WebSGV/appSettings.Secrets.config`); nunca los subas al repositorio.

## Documentación (`docs/`)

| Documento | Contenido |
|---|---|
| [`HOJA_DE_RUTA_CALIDAD.md`](docs/HOJA_DE_RUTA_CALIDAD.md) | Etapas de mejora: en cuál estamos y qué sigue (leer primero) |
| [`PLAN_MEJORAS_SGV.md`](docs/PLAN_MEJORAS_SGV.md) | Plan de mejoras: tareas abiertas |
| [`PENDIENTES_USUARIO.md`](docs/PENDIENTES_USUARIO.md) | Lo que solo puede decidir o hacer el usuario |
| [`DATOS_POR_CORREGIR.md`](docs/DATOS_POR_CORREGIR.md) | Datos de la BD por corregir, con sus consultas de control |
| [`PRUEBAS_PENDIENTES.md`](docs/PRUEBAS_PENDIENTES.md) | Lo que falta probar en el navegador, celular o sitio publicado |
| [`DECISIONES_NEGOCIO.md`](docs/DECISIONES_NEGOCIO.md) | Reglas de negocio acordadas |
| [`FLUJO_DE_TRABAJO_SGV.md`](docs/FLUJO_DE_TRABAJO_SGV.md) | Flujo de trabajo, roles, matriz de permisos y stack |
| [`GUIA_CREACION_ROLES.md`](docs/GUIA_CREACION_ROLES.md) | Cómo agregar un rol nuevo |
| [`CLIENTES_TERCEROS.md`](docs/CLIENTES_TERCEROS.md) | Clientes terceros: dónde cargan y descargan, y cómo encajan con Vitapro |
| [`RUTA_AFRECHO.md`](docs/RUTA_AFRECHO.md) | Rutas del afrecho aprendidas del GPS |
| [`OPERACION.md`](docs/OPERACION.md) | Vigilancia del sitio publicado y avisos de errores |
| [`PROCEDIMIENTO_RESPALDO.md`](docs/PROCEDIMIENTO_RESPALDO.md) | Respaldo y restauración de la BD |
| [`PASO_A_PRODUCCION.md`](docs/PASO_A_PRODUCCION.md) | Paso ordenado a la BD de producción |
| [`PREPARAR_MAQUINA.md`](docs/PREPARAR_MAQUINA.md) | Preparar una PC de desarrollo |

Lo histórico (planes terminados, análisis viejos, historial del plan) está en [`docs/archivo/`](docs/archivo/).

## Herramientas (`herramientas/`)

| Carpeta | Para qué |
|---|---|
| [`publicar`](herramientas/publicar/README.md) | Publicar SGV |
| [`sitio-local`](herramientas/sitio-local/README.md) | Levantar el sitio local y probarlo |
| [`respaldo`](herramientas/respaldo/README.md) | Respaldo propio de la BD |
| [`gps-automatico`](herramientas/gps-automatico/README.md) | Actualización automática del GPS del seguimiento |
| [`reporte-gps`](herramientas/reporte-gps/README.md) | Reporte GPS de tramos (Excel) |
| [`reporte-tiempos`](herramientas/reporte-tiempos/README.md) | Reporte de tiempos por tramo |
| [`perfil-llegada`](herramientas/perfil-llegada/README.md) | Perfiles de hora de llegada a planta |
| [`cuadro-status-general`](herramientas/cuadro-status-general/README.md) | Cuadro STATUS GENERAL desde los Word y el GPS |
| [`carga-historica-correo`](herramientas/carga-historica-correo/README.md) | Carga histórica de los documentos del correo |
| [`video`](herramientas/video/README.md) | Videos del SGV |

## Licencia

Sin licencia de código abierto: uso interno del propietario del proyecto, no redistribuible sin autorización.
