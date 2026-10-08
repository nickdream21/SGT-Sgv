# Plan de mejoras SGV — estado y tareas pendientes

Última actualización: **2026-10-08** (laptop). Este documento es el punto de partida para seguir trabajando
desde cualquier máquina. No contiene contraseñas: solo indica dónde están.

## 1. Estado actual

| | Al empezar (2026-10-07) | Ahora |
|---|---|---|
| Puntuación general | 5.5 / 10 | **8 / 10** |
| Tests | 215 | **273** (265 unitarios + 8 de integración) |
| Paquetes NuGet | 40 | 23 |
| Procedimientos del repo vs BD de pruebas | sin control, 2 inexistentes | **98/98 iguales** (100 en BD, 2 de legado por borrar) |

- Todo se trabaja contra la BD de **PRUEBAS** (`sgvActualizada`). El sistema todavía no lo usa nadie.
- **Producción (`sgvTransporte`) NO está actualizada**: el código de `master` no se puede publicar
  hasta seguir la lista de la sección 6 (en orden).

## 2. Preparar la laptop de casa

1. **Clon del repo.** El historial se reescribió el 2026-07-10 (purga de PDFs). Si el clon de la laptop
   es anterior a esa fecha, **no hagas `git pull`**: vuelve a clonar
   (`git clone https://github.com/nickdream21/SGT-Sgv.git`) o `git fetch && git reset --hard origin/master`
   (esto descarta cambios locales sin subir). **Nunca hagas push desde un clon viejo**: volvería a subir
   el historial purgado (ya pasó una vez con la rama `claude/claude-md-docs-miUln`, que se borró).
2. **Archivos que NO están en git** (cópialos desde la PC de la oficina, p. ej. con la transferencia de
   archivos de AnyDesk, a la misma ruta dentro del repo):
   - `WebSGV/connectionStrings.config` → BD de pruebas (la que usa la app en local y los tests).
   - `WebSGV/connectionStrings.Production.config` → solo para el paso a producción.
   - `WebSGV/appSettings.Secrets.config` → SMTP, credenciales de Onway (GPS).
   - `privado/` → `CREDENCIALES_USUARIOS.txt` (un usuario por rol en pruebas, panel de somee) y
     `cambiar_password_nickdream_PROD.sql`.
   - `.env` y `.claude/settings.json` (solo si usas Data API Builder o el MCP de SQL).
3. **Herramientas:** Visual Studio 2022+ (MSBuild, IIS Express), .NET SDK (para `dotnet test`) y
   *Microsoft Command Line Utilities for SQL Server* (sqlcmd, ODBC 18).
4. **Comprobar:**
   ```powershell
   msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
   dotnet test WebSGV.Tests\WebSGV.Tests.csproj --no-build      # 273 en verde (8 de integración)
   ```
   Sin `connectionStrings.config` los 8 de integración salen como "omitidos" (no fallan).

## 3. Lo que ya está hecho

| Commit | Contenido |
|---|---|
| `2979a41` | Guards que ahora sí detienen la página; WebMethods con sesión/rol; XSS; reset de contraseña con URL fija; página de error sin detalles; regeneración de sesión al login; bugs de datos (gastos de OV, `Session["Usuario"]`, correlativo de abastecimiento, CPIC en transacción); 22 SPs al repo; registro de migraciones; jQuery/Bootstrap unificados; Excel de Reportes a servicio. |
| `df1dd3f` | `/Uploads` privado (descarga por llave de sesión); cambio de contraseña obligatorio en servidor; bloqueo de login por cuenta; sin contraseñas en texto plano; auditoría faltante; errores técnicos fuera de la pantalla (`MensajeErrorHelper`). |
| `e09f4bf` | Hora de Perú en BD y código (`dbo.fn_AhoraPeru()`, migración 16); token GPS sin carreras (`sp_getapplock`) y reintentos 429/5xx; últimas consultas de ListaDespachos/RegistroDespacho a servicios. |
| `f96f145` | 17 paquetes sin uso fuera (MVC, bundling, FriendlyUrls, AjaxControlToolkit, compilador deprecado); tests de integración; `aplicar-procedimientos.ps1`; `sp_SE_Listar` corregido (la grilla de registros recientes salía vacía); tildes corruptas reparadas en BD y repo. |

Detalle de reglas y convenciones nuevas: ver `CLAUDE.md`.

## 4. Tareas pendientes (en pruebas)

Ordenadas por valor. Marca `[x]` al terminar.

- [x] **Borrar procedimientos de legado sin uso (40).** Hecho el 2026-10-08 con la migración
      `Schema/17_BorrarProcedimientosLegado.sql` (aplicada en pruebas: 140 → 100 procedimientos).
      Respaldo en `Database/Scripts/respaldo_procedimientos_legado_2026-10-08.sql`. También se borró
      `Database/Despliegue_Exportacion.sql` (obsoleto: recreaba la versión vieja de `sp_SE_Listar` y
      apuntaba a producción). Lista:
      `ActualizarTipoViaje, ActualizarTipoViajeAutomatico, CrearCPICTemporal, GenerarNumeroOrdenViaje,
      InsertarDetalleSegmento, InsertarEgresos, InsertarGastoAdicional, InsertarIngresoAdicional,
      InsertarIngresos, InsertarLiquidacionCompleta, InsertarSegmentoOrdenViaje,
      InsertarSegmentoOrdenViaje_Legacy, InsertarTracto, ObtenerEstadisticasLiquidaciones,
      ObtenerLiquidacionesPorOrden, ObtenerPlantasCargaPorCliente, ObtenerPlantasDescargaPorCliente,
      ObtenerSegmentosOrden, sp_ActualizarAbastecimientoCombustible, sp_ActualizarFactura,
      sp_ActualizarIndicador, sp_BuscarIndicadorPorNumeroPedido, sp_GenerarReporteFinanciero_BalanceGeneral,
      sp_InsertarDespacho, sp_InsertarFactura, sp_InsertarOrdenViaje, sp_ObtenerDespachosViajeActivo,
      sp_ObtenerHistorialLiquidacionesConductor, sp_ObtenerObservacionesRechazo,
      sp_ObtenerViajeActivoConductor, sp_ObtenerViajesActivosParaGrifo, sp_PruebaDespacho,
      sp_RegistrarCPIC, sp_ReporteRendimientoPorRuta, sp_SE_Dashboard_Mensual, sp_SE_Eliminar,
      sp_SE_GridListar, ValidarPlantaCliente, ValidarSegmentosOrden, ValidarUnicidadGuias`.
- [ ] **Borrar 2 procedimientos más sin uso**: `InsertarOperacionSubTramo` e
      `InsertarSegmentoOrdenViajeConGuias`. Están en la BD pero no en el repo; nada los llama (ni código
      ni otros objetos). Mismo método: respaldo + migración 18.
- [ ] **Probar "Verificar GPS"** (`Views/Exportacion/RegistroSeguimiento.aspx`) contra el API real de
      Onway. El cambio del token (bloqueo + reintentos) no se probó en vivo. Onway permite **un solo token
      activo**: si producción tiene uno vigente, pruebas no podrá renovar hasta que expire.
- [ ] **Partir `Views/Reportes.aspx.cs`** (~4 000 líneas): cada `GenerarReporteXxx` + `ConfigurarGridViewXxx`
      a una clase por reporte; el SQL ya está en `ReportesService`.
- [ ] **Sacar el JavaScript en línea a archivos `.js`** (~8 000 líneas de JS y ~10 000 de CSS dentro de
      las `.aspx`; los más grandes: `Dashboard`, `LiquidacionesPendientes`, `DashboardConductor`,
      `Exportacion/DashboardExportacion`).
- [ ] **`FirmarLiquidacion.aspx`** (página sin master) usa Bootstrap 5 y jQuery 3.7 locales; el resto
      del sitio usa Bootstrap 4.6.2. Decidir si se unifica (requiere revisión visual).
- [ ] **`Empresa.Web`** en `Web.config` (y escrito a mano en `LiquidacionesPendientes.aspx`) apunta a
      `www.serviciosgviviana.somee.com`, que no responde. Decidir si debe ser `sgvtransporte.somee.com`
      o la web comercial de la empresa (sale impreso en los PDF).
- [ ] **Serilog 2.12 → 4.x** (opcional; requiere nuevas dependencias y binding redirects).
- [ ] **Confirmar el CI en verde** en GitHub → Actions para `f96f145` y los siguientes commits.
- [ ] Opcional: tests de integración que escriban (en transacción con rollback) para los flujos de
      despacho y liquidación.

## 5. Herramientas del repo

| Script | Para qué |
|---|---|
| `WebSGV/Database/sql.ps1 "<consulta>" \| -Archivo x.sql [-Texto] [-Salida f] [-Entorno produccion -ConfirmoProduccion]` | Consulta o script contra la BD (pruebas por defecto) sin exponer la clave: la lee de `connectionStrings.config`. `-Texto` devuelve textos largos sin recortar (definiciones de objetos). Úsalo en lugar de escribir sqlcmd a mano. |
| `WebSGV/Database/aplicar-migraciones.ps1 -Entorno pruebas\|produccion [-SoloListar] [-ConfirmoProduccion]` | Aplica en orden las migraciones de `Database/Schema/` pendientes y las registra en `dbo.SchemaVersion`. |
| `WebSGV/Database/aplicar-procedimientos.ps1 -Entorno ... [-SoloListar] [-Todos]` | Compara cada SP del repo con la BD (FALTA / DISTINTO) y aplica los que difieren. El repo es la fuente de verdad. |
| `WebSGV/Database/Scripts/migrar-contrasenas-texto-plano.ps1 -Entorno ...` | Convierte a PBKDF2 las contraseñas que sigan en texto plano (requiere compilar antes). |
| `WebSGV/Database/Scripts/reparar-tildes-bd.ps1 -Entorno ...` | Repara tildes corruptas en procedimientos, triggers y vistas de la BD que no están en el repo. |
| `privado/cambiar_password_nickdream_PROD.sql` | Cambio de contraseña de la cuenta de administrador de sistema en producción (fuera de git). |

Con sqlcmd contra somee usar siempre `-C -I -f 65001` (certificado, QUOTED_IDENTIFIER, UTF-8).

## 6. Paso a producción (al final, en este orden)

1. `aplicar-migraciones.ps1 -Entorno produccion -SoloListar`, revisar, y luego con `-ConfirmoProduccion`
   (incluye la 16, hora de Perú: el código nuevo usa `dbo.fn_AhoraPeru()`).
2. `aplicar-procedimientos.ps1 -Entorno produccion -SoloListar` y revisar cada DISTINTO (si la BD tuviera
   algo más nuevo que el repo, actualizar primero el archivo del repo); luego con `-ConfirmoProduccion`.
3. `Scripts/reparar-tildes-bd.ps1 -Entorno produccion -SoloListar` y luego con `-ConfirmoProduccion`.
4. **Crítico:** `Scripts/migrar-contrasenas-texto-plano.ps1 -Entorno produccion -SoloListar` y luego con
   `-ConfirmoProduccion`. Si se publica sin esto, los usuarios con contraseña en texto plano (en pruebas
   eran 66 conductores) no podrán iniciar sesión.
5. `privado/cambiar_password_nickdream_PROD.sql` en SSMS (paso 1 lista la cuenta; el nombre exacto no
   se pudo confirmar).
6. Publicar desde Visual Studio con configuración **Release** (perfil `FTPProfile1`, sitio
   `https://sgvtransporte.somee.com`). Release fija `App.UrlPublica`, quita `debug`, activa
   `customErrors` y HTTPS. Verificar que se suba `bin\runtimes\win-x86\` (app pool de 32 bits; QuestPDF).
7. Con `connectionStrings` de producción, correr los tests de integración **no** está permitido (solo
   aceptan pruebas): verificar a mano el login con cada rol y "Ver historial" en RegistroDespacho.
