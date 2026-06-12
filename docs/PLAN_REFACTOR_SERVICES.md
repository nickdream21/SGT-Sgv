# Plan — Refactor de lógica y acceso a datos hacia `Services/`

> Documento de seguimiento del esfuerzo de extracción de lógica de negocio y SQL
> desde los code-behind (`.aspx.cs`) hacia clases en `WebSGV/Services/`.
> Sirve para no perder el objetivo entre sesiones. Última actualización: 2026-06-12.

## 1. Objetivo

Sacar lógica de negocio y acceso a datos de los code-behind de Web Forms hacia clases
**`Services/`** reutilizables y testeables, **empezando por el flujo de dinero**
(despacho → viaje → liquidación → revisión). Reduce duplicación, permite pruebas
unitarias y aísla el SQL.

**Fuera de alcance acordado:** todo lo de **grifo/combustible** (Abastecimiento,
DashboardGrifo, etc.) y **operadores/maquinaria**.

> **Nota (2026-06-11):** a pedido del usuario se incorporó `Reportes.aspx` (4.920 líneas,
> antes fuera de alcance). Todo su SQL crudo se extrajo a `ReportesService` (ver Fase D).

## 2. Mecánica acordada (reglas del refactor)

- **Lógica pura** → clases `public static` sin dependencias (`System.Web`, BD, HttpContext).
  El code-behind conserva el método original como **adaptador delgado** que delega.
- **Acceso a datos (orquestación BD)** → clases `Service` estáticas que usan `DbHelper`
  (estilo `NotificacionService`). **Sin dobles de prueba ni interfaces de repositorio.**
  El code-behind llama al Service; el Service ejecuta el SQL. **SQL/SP movido verbatim**:
  no se cambian consultas, stored procedures ni el comportamiento.
- En BD, el code-behind **conserva**: sesión, validación, saneamiento, enlace a controles,
  lectura de `Request.Form`, mapeo a DTO, auditoría, PDF, firma y `try/catch`.
- **Un módulo (página) = un commit** que compila.
- **Verificación** (sin BD real): `MSBuild` limpio + los **177 tests xUnit** existentes en verde.
  No se añaden tests de BD (no hay dobles, por decisión).
- Servicios deben permanecer **sin `System.Web`** (Session/Request/controles se pasan como
  parámetros o se quedan en el code-behind).

## 3. Lo hecho hasta hoy ✅

### Fase A — Lógica pura (sin System.Web) + tests + seguridad dab
| Commit | Contenido |
|---|---|
| `df9fbfb` | Pass 1: `LiquidacionCalculos`, `DespachoValidaciones` + tests xUnit; **dab-config**: `dab-config.production.json` bloqueado (auth AzureAD/JWT, sin introspección/MCP, sin anonymous) y `dab-config.json` local gitignored. |
| `825bc0b` | Pass 2: `Common/MontoHelper`, `OrdenViaje/OrdenViajeValidaciones`, `Facturas/FacturaValidaciones` + tests; unifica `ValidarNumeroPedido`. |

Resultado: **177 tests** xUnit en verde (montos, validaciones de fecha/hora/orden,
formato de factura, etc.).

### Fase B — Orquestación de BD (un módulo por commit)
| Commit | Página | Service | Qué se extrajo |
|---|---|---|---|
| `bbd4484` | DetalleOrdenViaje | `OrdenViaje/DetalleOrdenViajeService` | 9 consultas de lectura (cabecera, ingresos, egresos, peajes, reparaciones, hospedaje, combustible). |
| `d3d5548` | LiquidacionesAprobadasContabilidad | `Liquidaciones/LiquidacionesContabilidadService` | Consulta de aprobadas (sesión/saneamiento/balance quedan fuera). |
| `6322b22` | EditarDespacho | `Despachos/EditarDespachoService` | Estado, catálogos, carga del despacho y UPDATE. |
| `8a18bfa` | LiquidacionesPendientes | `Liquidaciones/LiquidacionesPendientesService` | nº de orden, SPs (pendientes/aprobar/rechazar), buscar conductores, aprobadas, revertir, marcar rechazada. |
| `46bff5b` | DashboardConductor | `Conductor/DashboardConductorService` | estaciones peaje, generar nº orden, datos de viaje, conteos de ownership, retirar liquidación. |
| `087eaf6` | RegistroDespacho | `Despachos/RegistroDespachoService` | catálogos, plantas por ámbito, crear viaje en progreso, validar documentos duplicados. |
| `54ed2af` | BuscarOrdenViaje | `OrdenViaje/BuscarOrdenViajeService` | catálogos + cargas de la orden (básicos, ingresos, egresos, adicionales, guías, productos, existencia). |
| `fc30015` | ListaDespachos | `Despachos/ListaDespachosService` | contar viajes activos, todos los conductores, anular/eliminar lote. |
| `12b4994` | Facturas (Agregar+Buscar) | `Facturas/FacturaConsultasService` | clientes, contar por número, todas, por id/número, documentos, info documento. |
| `3c68f22` | AgregarOrdenViaje | `OrdenViaje/AgregarOrdenViajeService` | contar por número, buscar id usuario por nombre, tabla/estaciones de peaje. |

### Fase C — Transacciones de escritura (modelos a `Models/` + DTO de entrada)
| Commit | Página | Service / Modelos | Qué se extrajo |
|---|---|---|---|
| `29b40e3` | DashboardConductor | `Conductor/LiquidacionConductorService` + `Models/Conductor/LiquidacionConductorModels` | **Transacción completa de envío de liquidación** (`btnEnviarLiquidacion_Click`): orden (insertar/actualizar re-liquidación), ingresos/egresos principales, ingresos/gastos adicionales, descuentos/reintegros, gastos detallados (peaje/reparación/hospedaje/combustible) y cierre de viajes en progreso — todos los `sp_DC_*` movidos verbatim dentro de una única transacción. Los modelos `GastoFinanciero`, `IngresoAdicionalData`, `GastoAdicionalData` se movieron a `WebSGV.Models.Conductor`; el code-behind arma un `LiquidacionConductorInput` (parseo de `Request.Form`/hidden fields/JSON) y conserva sesión, validación, ownership, auditoría, notificación, redirect y el `try/catch` que muestra el mensaje. |
| `4bd2d24` | LiquidacionesPendientes | `Liquidaciones/LiquidacionesPendientesService.ObtenerDetalleLiquidacion` + `Models/Liquidaciones/DetalleLiquidacionModels` | **Armador de DTO `ObtenerDetalleLiquidacion`** (sólo lectura): cabecera + ingresos/egresos principales y desglosados + ítems detallados (peajes/reparaciones/hospedaje/combustible) + adicionales + descuentos/reintegros, sobre una sola conexión con varios readers — SQL movido verbatim. Los DTO `DetalleLiquidacion`/`DetallePeajeItem`/`DetalleGenericoItem`/`ItemAdicional` se movieron a `WebSGV.Models.Liquidaciones` (los consumen también los services de PDF/Firma por reflexión/`var`). El `[WebMethod]` del code-behind conserva la validación de sesión y delega. |
| `22b4bea` | LiquidacionesPendientes | `Liquidaciones/LiquidacionesPendientesService` (Aprobar/Corregir/PDF) | **Transacciones de aprobación con ajustes y corrección de aprobada** + las dos consultas de archivado de PDF. `AprobarConAjustes` (resuelve nº orden → UPSERT `DescuentosReintegros` → `sp_AprobarLiquidacion`) y `CorregirAjustesAprobada` (UPSERT + registro en `observaciones`) movidas verbatim, devolviendo DTOs de resultado (`ResultadoAprobacionAjustes`/`ResultadoCorreccionAjustes`); los `[WebMethod]` conservan sesión, validación de montos/motivo, el objeto anónimo `{success,message}` (que `AprobarLiquidacionConFirma` consume por `dynamic`), el PDF y la auditoría. `GarantizarPdfArchivadoOV` delega su `SELECT rutaPdfFirmado`/`UPDATE ruta+hash` a `ObtenerRutaPdfArchivado`/`GuardarRutaPdfArchivado`; la orquestación de disco/PDF (HostingEnvironment + `PdfOrdenViajeService`) sigue en el code-behind. |
| `904cbf2` | RegistroDespacho | `Despachos/RegistroDespachoService` + `Models/Despachos/RegistroDespachoModels` | **Creadores del lote y lectores de viaje**: `CrearDocumentoBaseSeparado` (`sp_CrearFactura`/`sp_CrearCPIC`), `CrearDespachoIndividual` (`sp_CrearDespacho`), `ObtenerViajesAbiertosConductor` y `ObtenerInfoViaje` movidos verbatim (SP con parámetros de salida y readers→DTO). Los modelos `LoteDespachos`/`DocumentacionBase`/`ConductorLote`/`ViajeEnProgreso` se movieron a `WebSGV.Models.Despachos`; el code-behind conserva la orquestación del lote (`ProcesarLoteCompleto`: recorrer conductores, auditoría, limpieza de UI, `Session`) y los métodos quedaron como adaptadores delgados. |
| `24918c1` | ListaDespachos (1/2) | `Despachos/ListaDespachosService` + `Models/Despachos/ListaDespachosModels` | **Lectores de DTO**: `ObtenerViajesActivos`, `ObtenerDespachosDelViaje`, `ObtenerLotesRegistrados`, `ObtenerLotePorId`, `ObtenerIdsDespachosDeLote`, `ObtenerDespachosDelLote` (filtros como parámetros). Helpers `GetSafeValue`/`LeerDespachoDesdeReader`/`LeerLoteDesdeReader`/`ParsearIdLoteVirtual` movidos al service; modelos `ViajeActivo`/`DespachoViaje`/`DespachoConConductor`/`LoteRegistrado` a `WebSGV.Models.Despachos`. El code-behind lee los controles de filtro y delega. |
| `ca51ded` | ListaDespachos (2/2) | `Despachos/ListaDespachosService.GuardarCambiosLote` | **Transacción de edición de lote**: actualizar despachos + recalcular conductor dominante + gestionar/desvincular factura y CPIC (`sp_LD_*`). El code-behind lee/parsea los controles del formulario de edición y arma un `GuardarCambiosLoteInput`; el service ejecuta la transacción (rollback + re-propagación). |
| `48aca26` | BuscarOrdenViaje | `OrdenViaje/BuscarOrdenViajeService.GuardarCambios` + `Models/OrdenViaje/EditarOrdenViajeModels` | **Transacción de edición de orden de viaje desde la búsqueda**: `UPDATE` verbatim de OrdenViaje, Ingresos (recalculando totales base + adicionales en el service), IngresosAdicionales, Egresos, CategoriasAdicionales y GuiasTransportista en una sola transacción. El code-behind lee/parsea los controles (TextBox/DropDownList/Repeater) y arma un `EditarOrdenViajeInput`; los modelos `EditarOrdenViajeInput`/`IngresoAdicionalEditar`/`GastoAdicionalEditar` viven en `WebSGV.Models.OrdenViaje`. Validación de campos, auditoría, recarga de la UI y `try/catch` permanecen en la página. |

> **Pendiente de validación en runtime:** toca dinero y no hay pruebas de BD. Verificado
> con MSBuild limpio + 177 tests; falta una corrida real (envío y re-liquidación) antes
> de desplegar.

### Fase D — `Reportes.aspx` (incorporada a pedido del usuario)
Sólo lecturas (SP que rellenan `DataSet`); el code-behind conserva la lectura/parseo de
filtros, el armado de GridViews, los literales de indicadores y el manejo de errores.
| Commit | Qué se extrajo |
|---|---|
| `ea651f1` | **Lote 1/3**: nuevo `Reportes/ReportesService` con helper `LlenarDataSetSp`; catálogo `ObtenerLugaresAbastecimiento` + `ReportePedido`, `ReporteVehiculosAsignados`, `ReporteConductoresAsignados`, `ReporteBalanceFinanciero`, `ReporteViajesConductor`. |
| `a467465` | **Lote 2/3**: `ReporteProductosConductor`, `ReporteFinancieroConductor`, `ReporteCombustibleConductor`, `ReporteViajesVehiculo`, `ReporteConsumoCombustibleVehiculo` (conserva tipos/tamaños `SqlDbType`). |
| `fa14b2f` | **Lote 3/3**: `ReporteProductosMasTransportados`, `ReporteProductosPorCliente`, `ReporteProductosPorDestino`, `ReporteConsumoGeneralCombustible`, `ReporteRendimientoPorRuta`, `ReporteMantenimientoVehiculo`, `ReporteFinancieroBalanceGeneral`, `ReporteRendimientoPorVehiculo`, `ReporteRendimientoPorRutaCombustible`. |

Resultado: `Reportes.aspx.cs` quedó con **0** `SqlConnection`/`SqlCommand`/`SqlDataAdapter`.
SQL/SP movido verbatim (guardas `!= "0"`/`!= "Todas"` y tipos de parámetro preservados).

## 4. Lo que queda — "Pase de transacciones y modelos diferidos" ⏳

Es lo **entrelazado** con `Request.Form` / controles / modelos anidados de la página, que
**no** se puede mover a un service sin `System.Web` sin **reestructurar primero**. Es
código de **escritura/dinero**, así que requiere cuidado (idealmente, prueba en runtime).

**Prerrequisito común:** mover los modelos `[Serializable]` anidados en las páginas
(`LoteDespachos`, `ConductorLote`, `ViajeEnProgreso`, `DespachoViaje`, `LoteRegistrado`,
`DatosViajeParaLiquidacion`, `GastoFinanciero`, etc.) a `WebSGV/Models/` para que los
services puedan recibirlos/devolverlos sin depender de la página. Cuidar la serialización
en Session/ViewState.

Pendientes por página:
- ~~**DashboardConductor** — transacción de **envío de liquidación**~~ ✅ **Hecho** (Fase C):
  extraída a `LiquidacionConductorService.EnviarLiquidacion(LiquidacionConductorInput)`.
- ~~**RegistroDespacho** — creadores que reciben modelos: `CrearDocumentoBaseSeparado`,
  `CrearDespachoIndividual`, `ObtenerViajesAbiertosConductor`, `ObtenerInfoViaje`~~ ✅ **Hecho**
  (Fase C). La "finalización del lote" (`ProcesarLoteCompleto`) es orquestación in-memory
  (recorre conductores y llama a los creadores ya extraídos); no hay más SQL que mover.
- ~~**LiquidacionesPendientes**~~ ✅ **Hecho** (Fase C): `ObtenerDetalleLiquidacion`,
  `AprobarConAjustes`, `CorregirAjustesAprobada` y las consultas SQL de
  `GarantizarPdfArchivadoOV` extraídas al service. Sólo queda en el code-behind la
  orquestación PDF/disco (`GarantizarPdfArchivadoOV`/`ObtenerUrlPdfOrdenViaje`), que no
  es acceso a BD (HostingEnvironment + `PdfOrdenViajeService`).
- ~~**BuscarOrdenViaje** — transacción `GuardarCambios` (edición de la orden).~~ ✅ **Hecho**
  (commit `48aca26`): extraída a `BuscarOrdenViajeService.GuardarCambios(EditarOrdenViajeInput)`.
- ~~**ListaDespachos** — lecturas con DTO anidados (viajes/lotes), transacción
  `GuardarCambiosLote`~~ ✅ **Hecho** (Fase C). Quedan en el code-behind sólo binders de UI
  con SQL inline (`CargarDropDownListSP`, `ActualizarInformacionViajeDetalle`,
  `CargarGridConductoresLote`) que bindean controles/grilla directamente; su `GetSafeValue`
  permanece en la página. Dedup/extracción de esos binders es opcional (no es flujo de dinero).
- **Facturas** — escrituras transaccionales con **blobs de archivo** (guardar/editar factura,
  subir documentos) en Agregar/Buscar.
- **AgregarOrdenViaje** — transacción de **guardado** (insert/update de la orden + financiero)
  y los lectores que cargan el formulario de edición.
- **ReportesOrdenesViaje** — pendiente de revisar (no empezado).

## 5. Decisiones diferidas (otras mejoras)

- **Serilog / logging centralizado**: pospuesto (no se añadieron paquetes). Hoy se usa
  `System.Diagnostics.Debug/Trace`. Evaluar `ILogSGV` + Serilog en un pase aparte.
- **Dedup cosmético** (no toca dinero): `FormatearTamano(long)` y
  `ObtenerContentType`/`ObtenerIconoArchivo` duplicados en BuscarFactura/BuscarCPIC; familia
  `ObtenerClaseEstado/ObtenerTextoEstado/ObtenerClaseBoton` repetida en ~10 `Registro*.aspx.cs`.
- **Seguridad dab**: el `dab-config.json` abierto sigue en el **historial git** (sin secretos;
  connection string por `@env`). Ver rotación de password de somee pendiente.

## 6. Próximos pasos sugeridos (orden propuesto)

1. Mover modelos anidados a `WebSGV/Models/` (habilita casi todo lo de la sección 4).
   Hecho: `WebSGV.Models.Conductor` (DashboardConductor), `WebSGV.Models.Liquidaciones`
   (DetalleLiquidacion y sus ítems) y `WebSGV.Models.Despachos` (LoteDespachos/
   DocumentacionBase/ConductorLote/ViajeEnProgreso). Falta mover los de las páginas aún
   pendientes (incl. las copias propias de `AgregarOrdenViaje`, cuyo `GastoFinanciero`
   difiere por la fecha y **no** debe fusionarse sin cuidado).
2. Pase de transacciones de escritura, empezando por las de **mayor valor de dinero**:
   ~~DashboardConductor (envío liquidación)~~ ✅ → ~~LiquidacionesPendientes (AprobarConAjustes/
   Corregir/ObtenerDetalle)~~ ✅ → ~~RegistroDespacho (creación de lote)~~ ✅.
3. Transacciones restantes: ~~BuscarOrdenViaje~~ ✅, AgregarOrdenViaje, ~~ListaDespachos~~ ✅, Facturas.
4. ReportesOrdenesViaje.
5. Logging (Serilog) y dedup cosmético.

> Idealmente, los pasos de la sección 4 se validan ejecutando la app (no sólo compilando),
> porque tocan transacciones de dinero y no hay pruebas de BD.

## 7. Cómo verificar (recordatorio)

```powershell
# Build del proyecto web
& "<ruta>\MSBuild.exe" WebSGV\WebSGV.csproj /t:Build /p:Configuration=Debug /nologo /verbosity:minimal

# Tests
dotnet test WebSGV.Tests\WebSGV.Tests.csproj -c Debug
```
