# Análisis flujo de trabajo y ruta de archivo

**Session ID:** ses_22e13aac6ffehLXAioHHMS6YcE
**Created:** 27/4/2026, 21:30:19
**Updated:** 27/4/2026, 21:34:59

---

## User

necesito que analicen el flujo del trabajo del sistema y si hay una rchivo que lo tenga que se actualice  y me digas su ruta.

---

## Assistant (Orchestrator · Claude Sonnet 4.6 · 140.4s)

**Tool: task**

**Input:**
```json
{
  "description": "Analizar flujo de trabajo del sistema",
  "subagent_type": "explore",
  "prompt": "Analiza el flujo de trabajo completo del sistema WebSGV (ASP.NET Web Forms). \n\n1. Lee el archivo `docs/FLUJO_DE_TRABAJO_SGV.md` completamente.\n2. Explora la estructura de carpetas bajo `WebSGV/Views/` para identificar todos los módulos/páginas existentes.\n3. Revisa `WebSGV/Database/Schema/` y `WebSGV/Database/StoredProcedures/` para entender las entidades y operaciones de negocio.\n4. Lee `WebSGV/Views/RolesHelper.cs` para entender los roles y permisos.\n5. Revisa brevemente `docs/GUIA_CREACION_ROLES.md` y `docs/MEJORAS_UI_ABASTECIMIENTO.md`.\n6. Revisa `Global.asax.cs` y `App_Start/RouteConfig.cs`.\n\nCon toda esa información, produce:\n- Un resumen detallado del flujo de trabajo actual del sistema (módulos, entidades, roles, procesos principales).\n- Una lista de discrepancias o elementos que están en el código pero NO están documentados en `docs/FLUJO_DE_TRABAJO_SGV.md`.\n- Una lista de elementos documentados que ya no existen o están desactualizados.\n- El contenido completo y actualizado que debería tener `docs/FLUJO_DE_TRABAJO_SGV.md`.\n\nDevuelve todo esto en tu respuesta final."
}
```

**Output:**
```
task_id: ses_22e1385c5ffetehih7bY6tpLhN (for resuming to continue this task if needed)

<task_result>
Now I have a comprehensive picture of the entire system. Let me compile the full analysis.

---

## Análisis Completo del Sistema WebSGV

---

## 1. Resumen Detallado del Flujo de Trabajo Actual

### Roles Reales (según `RolesHelper.cs`)

El sistema tiene **7 roles definidos en código**, no 4 como indica la documentación actual:

| Constante | Valor String | Redirección Login | Descripción |
|---|---|---|---|
| `ROL_ADMIN` | `"ADMIN"` (también acepta `"ADMINISTRADOR"`) | `Inicio.aspx` | Gestión completa de transporte |
| `ROL_ADMIN_SISTEMA` | `"ADMINISTRADOR DE SISTEMA"` | `Inicio.aspx` | Admin + Auditoría |
| `ROL_CONDUCTOR` | `"CONDUCTOR"` (también `"CHOFER"`) | `DashboardConductor.aspx` | Liquidación de viajes |
| `ROL_ADMIN_GRIFO` | `"ADMINISTRADOR DE GRIFO"` | `DashboardGrifo.aspx` | Abastecimiento de combustible |
| `ROL_ADMIN_MAQUINARIA` | `"ADMINISTRADOR DE MAQUINARIA"` | `Inicio.aspx` | Módulo de maquinaria/obras |
| `ROL_OPERADOR` | `"OPERADOR"` | `DashboardOperador.aspx` | Parte diario de maquinaria |
| `ROL_SUPERVISOR` | `"SUPERVISOR"` | `Inicio.aspx` | Similar a ADMIN (permisos por sección) |

### Módulos Identificados en `Views/`

#### A. Módulo Transporte (ADMIN / ADMIN_SISTEMA / SUPERVISOR)
- `Login.aspx`, `Inicio.aspx` — Autenticación y landing
- `RegistroDespacho.aspx` — Crear despachos/lotes de viaje
- `ListaDespachos.aspx` — Listar y gestionar lotes
- `EditarDespacho.aspx` — Editar despachos
- `AgregarOrdenViaje.aspx` — Crear orden de viaje desde despacho finalizado
- `DetalleOrdenViaje.aspx` — Ver detalle completo de liquidación
- `LiquidacionesPendientes.aspx` — Revisión admin (aprobar/rechazar)
- `BuscarOrdenViaje.aspx` — Búsqueda avanzada de órdenes
- `AgregarIndicadores.aspx` — Registro de tiempos operativos
- `ReportesOrdenesViaje.aspx` — Liquidaciones aprobadas + viajes activos sin liquidar
- `Reportes.aspx` — Reportes avanzados (gran módulo independiente ~5000 líneas)
- `AgregarCPIC.aspx`, `BuscarCPIC.aspx` — Gestión documentos internacionales CPIC
- `AgregarFactura.aspx`, `BuscarFactura.aspx` — Gestión de facturas
- `CargarExcel.aspx` — Carga masiva de datos desde Excel
- `DescargarPdfOrdenViaje.aspx`, `DescargarPdfAbastecimiento.aspx` — Descarga de PDFs
- `FirmarLiquidacion.aspx` — Firma digital canvas del conductor (nuevo flujo)

#### B. Módulo Maestros/Registros
- `RegistroChoferes.aspx` — Conductores
- `RegistroTractos.aspx` — Tractos
- `RegistroSemiremolques.aspx` — Semiremolques/carretas
- `RegistroClientes.aspx` — Clientes de transporte
- `RegistroPeajes.aspx` — Estaciones de peaje
- `RegistroPlantas.aspx` — Plantas (puntos de carga/descarga)
- `RegistroRutas.aspx` — Rutas
- `RegistroProductos.aspx` — Productos transportados

#### C. Módulo Conductor
- `DashboardConductor.aspx` — Dashboard con pestañas Viajes/Liquidar/Historial
- `FirmarLiquidacion.aspx` — Firma digital del conductor antes de enviar liquidación

#### D. Módulo Abastecimiento (ADMIN_GRIFO / ADMIN)
- `DashboardGrifo.aspx` — Vista general de viajes activos para abastecer
- `AgregarAbastecimiento.aspx` — Registro (modo manual + modo viaje)
- `BuscarAbastecimiento.aspx` — Búsqueda/edición/anulación/eliminación
- `ReporteAbastecimiento.aspx` — Reportes exportables
- `RegistrarDespachoObra.aspx` — Despacho de cisterna a obra (NUEVO, no documentado)
- `RegistrarRetornoEcuador.aspx` — Registro de ingresos de combustible desde Ecuador (NUEVO, no documentado)

#### E. Módulo Maquinaria (ADMIN_MAQUINARIA / ADMIN)
- `RegistroEquiposMaquinaria.aspx` — Registro de equipos
- `RegistroObras.aspx` — Registro de obras
- `RegistroClientesObra.aspx` — Clientes de obras
- `RegistroOperadores.aspx` — Registro de operadores de maquinaria
- `AsignacionesMaquinaria.aspx` — Asignación operador-equipo-obra
- `DashboardOperador.aspx` — Dashboard del operador de maquinaria (Parte Diario)

#### F. Módulo Auditoría (ADMIN_SISTEMA)
- `Auditoria.aspx` — Log de auditoría principal
- `ConsultaAuditoria.aspx` — Segunda página de consulta de auditoría (DUPLICADA/no documentada)

#### G. Páginas de Infraestructura/Misc
- `RecuperarContrasena.aspx`, `RestablecerContrasena.aspx` — Recuperación de contraseña
- `Error.aspx`, `Error404.aspx` — Manejo de errores
- `Dashboard.aspx` — Dashboard genérico (uso a confirmar)
- `ReporteResultado.aspx` — Vista de resultados de reporte (auxiliar de `Reportes.aspx`)
- `TestGenerarPdfOrdenViaje.aspx` — Página de prueba, solo en desarrollo
- `WebForm2.aspx` — Página placeholder/test, debe eliminarse

### Entidades de Base de Datos (inferidas de Schema + SPs)

**Tablas Core de Transporte:**
- `Usuarios`, `Conductores`, `Tracto`, `Semiremolques`
- `Despachos` (agrupados en Lotes)
- `ViajeProgreso`
- `OrdenViaje` (liquidación)
- `Clientes`, `Plantas`, `Rutas`, `Productos`, `Peajes`

**Tablas Financieras (OrdenViaje):**
- `Ingresos`, `Egresos`
- `DetalleHospedaje`, `DetallePeaje`, `DetalleReparacion`, `DetalleCombustible`, `GastoAdicional`, `IngresoAdicional`
- `DescuentosReintegros` (generados al aprobar)
- `OrdenViajeAjuste` (Schema 03 — ajustes post-firma con registro)

**Tablas de Firma Digital:**
- `FirmaDigital` (Schema 02 — tabla append-only con imagen trazo PNG + hash SHA-256)
- `FormatoControlado` (Schema 01 — catálogo ISO)
- Columnas agregadas a `OrdenViaje`: `rutaPdfFirmado`, `hashPdfFirmado`, `idFirmaConductor`, `idFirmaAdmin`, `fechaEnvioFirmado`, `fechaAprobacionFirmada` (Schema 04)

**Tablas de Abastecimiento:**
- `AbastecimientoCombustible` (columnas dinámicas: `tipoAbastecimiento`, `rutaDescripcion`)
- `DespachoCombustibleObra` (nuevo — despacho a obra)
- `IngresoCombustibleEcuador`, `DetalleTicketEcuador` (nuevo — combustible Ecuador)

**Tablas de Maquinaria:**
- `EquiposMaquinaria`, `Obras`, `ClientesObra`, `Operadores`
- `AsignacionesMaquinaria`
- `PartesDiariosTrabajo`

**Auditoría:**
- `AuditoriaLog` (auto-creada en `Application_Start`)

### Flujo de Firma Digital (nuevo, no documentado)

El sistema implementa un ciclo de firma digital biométrica en las liquidaciones:

```
Conductor llena liquidación en DashboardConductor
        │
        ▼
FirmarLiquidacion.aspx — Canvas de firma biométrica
  · Dibuja trazo en canvas HTML5
  · Se genera hash SHA-256 del documento
  · Se almacena imagenTrazoPng en tabla FirmaDigital (append-only)
  · idFirmaConductor queda registrado en OrdenViaje
        │
        ▼
Admin revisa en LiquidacionesPendientes.aspx
  · Al aprobar: se registra firma del admin (Nivel C, solo metadata)
  · idFirmaAdmin registrado en OrdenViaje
  · Los ajustes (descuentos/reintegros) se guardan en OrdenViajeAjuste
  · No invalidan la firma original del conductor
        │
        ▼
PDF firmado generado y archivado en ~/App_Data/OrdenesViaje
  · rutaPdfFirmado y hashPdfFirmado en OrdenViaje para verificación futura
```

### `Global.asax.cs` y `RouteConfig.cs`

- **`Application_Start`**: Registra rutas MVC (vacías), bundles y **auto-crea tabla `AuditoriaLog`** si no existe.
- **`Application_Error`**: Captura errores globales. Si es error de ViewState, redirige a `Login.aspx?error=sesion` silenciosamente. Otros errores solo se loguean en Debug (no van a `Error.aspx` — comportamiento parcialmente oculto).
- **`RouteConfig`**: Ignora todas las rutas `Views/*.aspx` — no hay routing real. Acceso directo por URL física `.aspx`.

---

## 2. Discrepancias: En el Código pero NO Documentados en `FLUJO_DE_TRABAJO_SGV.md`

### Roles no documentados
1. **`ROL_SUPERVISOR`** (`"SUPERVISOR"`) — Existe en `RolesHelper.cs` con permisos equivalentes a ADMIN en la mayoría de secciones (DESPACHO, ORDEN_VIAJE, REGISTRO, ABASTECIMIENTO, REGISTRO_CONDUCTORES). No tiene mención alguna en el documento.
2. **`ROL_ADMIN_MAQUINARIA`** (`"ADMINISTRADOR DE MAQUINARIA"`) — Completamente ausente del documento. Tiene acceso a `DASHBOARD_OPERADOR`, `PARTE_DIARIO`, y redirige a `Inicio.aspx`.
3. **`ROL_OPERADOR`** (`"OPERADOR"`) — Completamente ausente del documento. Tiene dashboard propio `DashboardOperador.aspx` para el módulo de parte diario de maquinaria.

### Módulo Maquinaria completo (no documentado)
4. `RegistroEquiposMaquinaria.aspx` — Gestión de equipos (topadoras, motoniveladoras, etc.)
5. `RegistroObras.aspx` — Gestión de obras
6. `RegistroClientesObra.aspx` — Clientes de obras (entidad diferente a clientes de transporte)
7. `RegistroOperadores.aspx` — Operadores de maquinaria
8. `AsignacionesMaquinaria.aspx` — Asignación operador → equipo → obra, con estado `ACTIVA`
9. `DashboardOperador.aspx` — Dashboard del operador con sesión propia (`Session["IdOperador"]`), carga asignación activa y partes diarios

### Flujo de Firma Digital (no documentado)
10. `FirmarLiquidacion.aspx` — Página de firma biométrica canvas para el conductor. Flujo separado, pre-envío.
11. Tabla `FirmaDigital` — Estructura append-only con imagen PNG del trazo, hash SHA-256, metadatos de integridad/identidad/trazabilidad.
12. Tabla `OrdenViajeAjuste` — Los descuentos/reintegros tienen su propia tabla formal con FK a `FirmaDigital` para firma del admin.
13. Tabla `FormatoControlado` — Catálogo ISO 9001/14001/45001/BASC para encabezados de documentos controlados.
14. Columnas `idFirmaConductor`, `idFirmaAdmin`, `rutaPdfFirmado`, `hashPdfFirmado`, `fechaEnvioFirmado`, `fechaAprobacionFirmada` en `OrdenViaje`.

### Páginas de abastecimiento no documentadas
15. `RegistrarDespachoObra.aspx` — Despacho de cisterna a obra (acceso: ADMIN_GRIFO + ADMIN). Usa tabla `DespachoCombustibleObra`.
16. `RegistrarRetornoEcuador.aspx` — Registro de ingresos de combustible proveniente de Ecuador con tickets detallados. Usa tablas `IngresoCombustibleEcuador` + `DetalleTicketEcuador`.

### Páginas de facturas no documentadas
17. `AgregarFactura.aspx` / `BuscarFactura.aspx` — Gestión de facturas (entidad aparte de CPIC). El documento menciona "AgregarCPIC" y "BuscarOrdenViaje" pero no estas páginas.

### Otras páginas no documentadas
18. `CargarExcel.aspx` — Carga masiva de datos desde Excel.
19. `Reportes.aspx` — Gran módulo de reportes avanzados (~5000 líneas) independiente de `ReportesOrdenesViaje.aspx`. Tiene filtros por conductor, vehículo, producto, tipo de transacción, exportación.
20. `ReporteResultado.aspx` — Vista auxiliar de resultados de `Reportes.aspx`.
21. `ConsultaAuditoria.aspx` — Segunda página de consulta de auditoría (distinta de `Auditoria.aspx`). No está documentada ni su relación con la primera.
22. `Dashboard.aspx` — Dashboard genérico (no documentado, uso incierto).
23. `DescargarPdfOrdenViaje.aspx` / `DescargarPdfAbastecimiento.aspx` — Endpoints de descarga de PDFs (generan/sirven el archivo iTextSharp). No documentados.

### Comportamiento de `Application_Error`
24. `Global.asax.cs` solo maneja errores de ViewState redirigiendo a Login. Otros errores solo van a Debug, no a `Error.aspx`. El documento menciona esto parcialmente en `AGENTS.md` pero no en `FLUJO_DE_TRABAJO_SGV.md`.

### Acción de auditoría no documentada
25. La acción `"RETIRAR"` está en el documento pero no en los SPs — en cambio existe `sp_DC_RetirarLiquidacion.sql` que sugiere que un conductor puede retirar/cancelar una liquidación ya enviada.

---

## 3. Elementos Documentados que Ya No Existen o Están Desactualizados

1. **"El sistema maneja 4 roles"** — Falso. Hay 7 roles operativos: ADMIN, ADMIN_SISTEMA, CONDUCTOR, ADMIN_GRIFO, ADMIN_MAQUINARIA, OPERADOR, SUPERVISOR.

2. **`AgregarOrdenViaje.aspx` descrita como "Generación de la orden de viaje a partir del despacho finalizado"** — Puede estar desactualizada. El flujo real parece ser que el conductor liquida directamente desde `DashboardConductor.aspx` usando los SPs `sp_DC_*`, y `AgregarOrdenViaje.aspx` podría ser un camino alternativo del admin. Requiere verificación.

3. **Tabla de migración dinámica `tipoAbastecimiento`** — Documentada como columna con detección dinámica (`ColumnaExisteEnTabla`). Sin embargo, el SP `sp_InsertarAbastecimientoCombustible_ALTER.sql` ya existe en StoredProcedures, sugiriendo que la migración probablemente ya fue ejecutada en producción y la detección dinámica es legacy.

4. **Sección de "Historial de Liquidaciones Aprobadas" desde `ReportesOrdenesViaje.aspx` (pestaña Liquidaciones)** — El documento describe esto pero existe además el gran módulo `Reportes.aspx` que tiene funcionalidad más amplia y no está documentado, creando ambigüedad sobre cuál usar.

5. **Menú Navbar documentado para Admin** — No incluye las entradas de Maquinaria, ni las páginas de Facturas/CPIC/Excel que sí existen. El menú real en `Site.Master` es probablemente más extenso.

6. **`TestGenerarPdfOrdenViaje.aspx` y `WebForm2.aspx`** — Estas páginas existen en `Views/` y están incluidas en el proyecto. Son artefactos de desarrollo que no deben estar en producción y no están documentadas (correcto que no lo estén, pero deben removerse).

---

## 4. Contenido Completo y Actualizado para `docs/FLUJO_DE_TRABAJO_SGV.md`

```markdown
# Sistema SGV — Flujo de Trabajo y Roles

## Descripción General

**SGV (Sistema de Gestión de Viajes)** es una aplicación web ASP.NET Web Forms (.NET Framework 4.8) orientada a la gestión de transporte de carga. Administra el ciclo completo desde la programación de despachos, seguimiento de viajes, liquidación de gastos firmada digitalmente por conductores, hasta la aprobación administrativa con ajustes auditados. Incluye además módulos de abastecimiento de combustible, gestión de maquinaria pesada y parte diario de operadores.

---

## Roles del Sistema

El sistema maneja **7 roles** definidos en `WebSGV/Views/RolesHelper.cs`:

| Rol | Constante | Valor en BD | Redirección Login | Descripción |
|-----|-----------|-------------|-------------------|-------------|
| **Administrador de Transporte** | `ROL_ADMIN` | `ADMIN` (también acepta `ADMINISTRADOR`) | `Inicio.aspx` | Gestión operativa completa: despachos, viajes, liquidaciones, registros y reportes |
| **Administrador de Sistema** | `ROL_ADMIN_SISTEMA` | `ADMINISTRADOR DE SISTEMA` | `Inicio.aspx` | Todo lo del Admin + módulo de auditoría exclusivo |
| **Conductor** | `ROL_CONDUCTOR` | `CONDUCTOR` (también `CHOFER`) | `DashboardConductor.aspx` | Liquidación firmada de viajes asignados y consulta de historial |
| **Administrador de Grifo** | `ROL_ADMIN_GRIFO` | `ADMINISTRADOR DE GRIFO` | `DashboardGrifo.aspx` | Gestión integral de abastecimiento de combustible |
| **Administrador de Maquinaria** | `ROL_ADMIN_MAQUINARIA` | `ADMINISTRADOR DE MAQUINARIA` | `Inicio.aspx` | Gestión de equipos, obras, operadores y asignaciones |
| **Operador** | `ROL_OPERADOR` | `OPERADOR` | `DashboardOperador.aspx` | Registro de parte diario de trabajo en maquinaria pesada |
| **Supervisor** | `ROL_SUPERVISOR` | `SUPERVISOR` | `Inicio.aspx` | Permisos similares a ADMIN (despachos, órdenes de viaje, registros, abastecimiento) |

> Los roles se almacenan como texto en la columna `rol` de la tabla `Usuarios`. No existe tabla de roles separada. Las comparaciones siempre se hacen en mayúsculas (`.ToUpper()`).

---

## Matriz de Permisos por Sección

| Sección (`TienePermiso`) | ADMIN | ADMIN_SISTEMA | CONDUCTOR | ADMIN_GRIFO | ADMIN_MAQUINARIA | OPERADOR | SUPERVISOR |
|--------------------------|-------|---------------|-----------|-------------|------------------|----------|------------|
| `DESPACHO` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `FACTURA` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `CPIC` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `ORDEN_VIAJE` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `REGISTRO` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `CONSULTAS` / `INDICADORES` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `REGISTRO_CONDUCTORES` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| `ABASTECIMIENTO` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| `DASHBOARD_GRIFO` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| `AUDITORIA` | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| `DASHBOARD_CONDUCTOR` / `MIS_VIAJES` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| `DASHBOARD_OPERADOR` / `PARTE_DIARIO` | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ |

---

## Flujo de Trabajo General

### Flujo de Transporte (Admin de Transporte / Supervisor)

```
Programación de Despacho (Admin)
        │
        ▼
Asignación de Conductor, Tracto, Carreta → se crean ViajesProgreso
        │
        ▼
Viaje en Progreso (conductor lo ve en su Dashboard)
        │
        ▼
Conductor Liquida el Viaje (registra ingresos y gastos)
        │
        ▼
Conductor Firma Digitalmente (canvas biométrico en FirmarLiquidacion.aspx)
  → Firma almacenada en tabla FirmaDigital (append-only, hash SHA-256)
  → idFirmaConductor registrado en OrdenViaje
        │
        ▼
Liquidación Pendiente de Revisión (Admin)
        │
        ▼
Admin Revisa: Aprobar / Rechazar / Editar
        │
        ├── Si Aprueba →
        │     · Firma del Admin registrada (Nivel C, metadata)
        │     · Ajustes (Descuentos/Reintegros) guardados en OrdenViajeAjuste
        │     · No invalida firma original del conductor
        │     · PDF firmado generado y archivado en ~/App_Data/OrdenesViaje
        │     → Pasa a Historial / Reportes
        └── Si Rechaza → Regresa al Conductor para corrección
```

### Flujo de Abastecimiento (Admin de Grifo)

```
DashboardGrifo.aspx — lista viajes activos pendientes de abastecimiento
        │
        ├── [Abastecer] → AgregarAbastecimiento.aspx (Modo Viaje, datos prellenados)
        └── [Agregar manual] → AgregarAbastecimiento.aspx (Modo Manual, dropdowns)
        │
        ▼
Registro de tickets (costo USD + galones)
Cálculos automáticos: GL Total, GL Consumidos, Rendimiento KM/GL
        │
        ▼
Guardar → número correlativo generado
        │
        ├── [Despacho a Obra] → RegistrarDespachoObra.aspx
        │     Cisterna lleva combustible a una obra
        │     Registra galones salida / retorno → galonesAbastecidos = salida - retorno
        │
        ├── [Retorno Ecuador] → RegistrarRetornoEcuador.aspx
        │     Ingreso de combustible desde Ecuador con tickets detallados
        │
        └── Gestión posterior (BuscarAbastecimiento.aspx):
              ├── Editar datos (GL, montos, producto, ruta, tipo)
              ├── Cambiar Tipo (ABASTECIMIENTO ↔ MANTENIMIENTO ↔ OTRO)
              ├── Anular → Marca como ANULADO (no editable)
              └── Eliminar → Eliminación permanente con confirmación
```

### Flujo de Maquinaria (Admin de Maquinaria / Operador)

```
Admin Maquinaria configura maestros:
  · RegistroEquiposMaquinaria.aspx (topadoras, motoniveladoras, etc.)
  · RegistroObras.aspx
  · RegistroClientesObra.aspx
  · RegistroOperadores.aspx
        │
        ▼
AsignacionesMaquinaria.aspx — Vincula Operador + Equipo + Obra (estado ACTIVA)
        │
        ▼
Operador inicia sesión → DashboardOperador.aspx
  · Ve su asignación activa (equipo + obra + cliente)
  · Registra Parte Diario de Trabajo (numeroParte PT-YYYY-XXXXXX)
    - Odómetro inicio/fin, horómetro inicio/fin
    - Consumo de petróleo, gasolina, aceite, grasa
    - Carretera, sector, labor realizada
    - Reclamos y observaciones
        │
        ▼
Historial de partes accesible en Dashboard
```

---

## Rol: Administrador de Transporte (`ADMIN`) y Supervisor

### 1. Gestión de Despachos

| Página | Función |
|--------|---------|
| `RegistroDespacho.aspx` | **Agregar Despacho** — Crea lote de despachos con: fecha
</task_result>
```

---

## Assistant (Orchestrator · Claude Sonnet 4.6 · 3.6s)

Ahora con ese análisis completo, voy a actualizar el archivo `docs/FLUJO_DE_TRABAJO_SGV.md`:

**Tool: read**

**Input:**
```json
{
  "filePath": "C:\\Users\\NICK\\Downloads\\WebSGV-master\\WebSGV-master\\WebSGV-master\\docs\\FLUJO_DE_TRABAJO_SGV.md"
}
```

**Output:**
```
<path>C:\Users\NICK\Downloads\WebSGV-master\WebSGV-master\WebSGV-master\docs\FLUJO_DE_TRABAJO_SGV.md</path>
<type>file</type>
<content>
1: # Sistema SGV — Flujo de Trabajo y Roles
2: 
3: ## 📋 Descripción General
4: 
5: **SGV (Sistema de Gestión de Viajes)** es una aplicación web ASP.NET Web Forms (.NET Framework 4.8) orientada a la gestión de transporte de carga. Administra el ciclo completo desde la programación de despachos, seguimiento de viajes, liquidación de gastos por parte de conductores, hasta la aprobación y reportes financieros.
6: 
7: ---
8: 
9: ## 👥 Roles del Sistema
10: 
11: El sistema maneja **4 roles** definidos en `RolesHelper.cs`:
12: 
13: | Rol | Constante | Descripción |
14: |-----|-----------|-------------|
15: | **Administrador de Transporte** | `ADMIN` / `ADMINISTRADOR` | Gestión operativa completa: despachos, viajes, liquidaciones, registros y reportes |
16: | **Administrador de Grifo** | `ADMINISTRADOR DE GRIFO` | Gestión de abastecimiento de combustible: registro, búsqueda, reportes, anulación/eliminación y registro de conductores |
17: | **Conductor** | `CONDUCTOR` / `CHOFER` | Liquidación de viajes asignados y consulta de historial |
18: | **Administrador de Sistema** | `ADMINISTRADOR DE SISTEMA` | Acceso total (incluye todo lo del Admin de Transporte) + Auditoría del sistema |
19: 
20: ---
21: 
22: ## 🔄 Flujo de Trabajo General
23: 
24: ### Flujo de Transporte (Admin de Transporte)
25: ```
26: Programación de Despacho (Admin)
27:         │
28:         ▼
29: Asignación de Conductor, Tracto, Carreta
30:         │
31:         ▼
32: Viaje en Progreso (el conductor ve el viaje en su Dashboard)
33:         │
34:         ▼
35: Conductor Liquida el Viaje (registra ingresos y gastos)
36:         │
37:         ▼
38: Liquidación Pendiente de Revisión (Admin)
39:         │
40:         ▼
41: Admin Revisa: Aprobar / Rechazar / Editar
42:         │
43:         ├── Si Aprueba → Se generan Descuentos y Reintegros → Pasa a Historial/Reportes
44:         └── Si Rechaza → Regresa al Conductor para corrección
45: ```
46: 
47: ### Flujo de Abastecimiento (Admin de Grifo)
48: ```
49: Registro de Abastecimiento (Admin Grifo)
50:         │
51:         ├── Modo Manual: Selección de conductor, placa, carreta, ruta, producto
52:         └── Modo Viaje: Datos prellenados desde orden de viaje activa
53:         │
54:         ▼
55: Registro de Tickets de Combustible (costo USD + galones)
56:         │
57:         ▼
58: Cálculos Automáticos: GL totales, GL consumidos, rendimiento KM/GL
59:         │
60:         ▼
61: Guardar Abastecimiento → Número correlativo generado
62:         │
63:         ▼
64: Gestión Posterior (BuscarAbastecimiento.aspx):
65:         ├── ✏️ Editar datos (GL, montos, producto, ruta, tipo)
66:         ├── 🔄 Cambiar Tipo (Abastecimiento ↔ Mantenimiento ↔ Otro)
67:         ├── 🚫 Anular → Marca como ANULADO (no editable)
68:         └── 🗑️ Eliminar → Eliminación permanente con confirmación
69: ```
70: 
71: ---
72: 
73: ## 🔧 Rol: Administrador de Transporte (`ADMIN`)
74: 
75: ### 1. Gestión de Despachos
76: 
77: | Página | Función |
78: |--------|---------|
79: | `RegistroDespacho.aspx` | **Agregar Despacho** — Programación de viajes. Se crea un lote de despachos con: fecha de programación, cliente, número de pedido, tipo de operación (nacional/internacional), planta, documentación (factura, CPIC) y asignación de conductores con sus tractos y carretas. |
80: | `ListaDespachos.aspx` | **Listar Despachos** — Vista general de todos los despachos creados. Permite ver detalles, filtrar y gestionar los despachos existentes. Muestra viajes activos y despachos por viaje. |
81: | `EditarDespacho.aspx` | **Editar Despacho** — Modificar datos de un despacho existente (solo si es editable/no ha sido liquidado). |
82: 
83: **Flujo de Despacho:**
84: 1. Admin crea un nuevo despacho (`RegistroDespacho.aspx`)
85: 2. Selecciona cliente, tipo de operación, planta
86: 3. Agrega documentación base (factura, CPIC si es internacional)
87: 4. Asigna uno o más conductores con su tracto y carreta
88: 5. Genera el lote → Se crean los viajes en progreso
89: 6. Los despachos se visualizan en `ListaDespachos.aspx`
90: 7. Desde la lista puede editar o gestionar documentación (facturas, CPIC, guías)
91: 
92: ### 2. Órdenes de Viaje y Liquidaciones
93: 
94: | Página | Función |
95: |--------|---------|
96: | `LiquidacionesPendientes.aspx` | **Liquidaciones Pendientes** — Lista de liquidaciones que los conductores han completado y enviado pero que aún no han sido revisadas por el administrador. |
97: | `DetalleOrdenViaje.aspx` | **Detalle de Orden de Viaje** — Vista completa de una liquidación con todos los datos financieros. |
98: | `AgregarOrdenViaje.aspx` | **Crear Orden de Viaje** — Generación de la orden de viaje a partir de los datos del despacho finalizado. |
99: 
100: **Acciones sobre liquidaciones pendientes:**
101: - ✅ **Aprobar** — Acepta la liquidación; en este momento se calculan y registran los **descuentos** y **reintegros** correspondientes al conductor.
102: - ❌ **Rechazar** — Devuelve la liquidación al conductor con observaciones para corrección.
103: - ✏️ **Editar** — Permite al admin modificar datos de la liquidación antes de aprobar.
104: - 👁️ **Ver** — Consulta el detalle completo de la liquidación sin modificar.
105: 
106: ### 3. Historial de Liquidaciones Aprobadas
107: 
108: Desde `ReportesOrdenesViaje.aspx` (pestaña Liquidaciones), el administrador puede consultar el historial de todas las liquidaciones que ha aprobado, funcionando como un registro/control de las operaciones finalizadas.
109: 
110: ### 4. Reportes
111: 
112: | Página | Sección | Función |
113: |--------|---------|---------|
114: | `ReportesOrdenesViaje.aspx` | **Liquidaciones** | Reporte de liquidaciones aprobadas con filtros por fecha y factor de conversión (dólar a soles). Incluye detalle de **descuentos** y **reintegros** generados en la aprobación. Exportable a Excel y PDF. |
115: | `ReportesOrdenesViaje.aspx` | **Viajes Activos Sin Liquidación** | Muestra qué conductores tienen viajes en progreso que aún **no han enviado su liquidación**. Permite dar seguimiento a viajes pendientes. |
116: 
117: ### 5. Módulo de Registros (Maestros)
118: 
119: | Página | Función |
120: |--------|---------|
121: | `RegistroChoferes.aspx` | Registrar, editar y gestionar **conductores** |
122: | `RegistroTractos.aspx` | Registrar, editar y gestionar **tractos** (camiones) |
123: | `RegistroSemiremolques.aspx` | Registrar, editar y gestionar **semiremolques** (carretas) |
124: | `RegistroClientes.aspx` | Registrar, editar y gestionar **clientes** |
125: | `RegistroPeajes.aspx` | Registrar, editar y gestionar **peajes** (estaciones de peaje en rutas) |
126: | `RegistroPlantas.aspx` | Registrar, editar y gestionar **plantas** (puntos de carga/descarga) |
127: 
128: ### 6. Otros Módulos del Admin
129: 
130: | Página | Función |
131: |--------|---------|
132: | `AgregarCPIC.aspx` | Gestión de documentos CPIC (operaciones internacionales) |
133: | `BuscarOrdenViaje.aspx` | Búsqueda de órdenes de viaje por múltiples criterios |
134: | `AgregarIndicadores.aspx` | Registro de indicadores de tiempo en operaciones (horas de salida, llegada, carga, descarga, etc.) |
135: | `RegistroRutas.aspx` | Gestión de rutas de transporte |
136: | `RegistroProductos.aspx` | Gestión de productos transportados |
137: 
138: ---
139: 
140: ## ⛽ Rol: Administrador de Grifo (`ADMINISTRADOR DE GRIFO`)
141: 
142: El Administrador de Grifo es responsable de la gestión integral del abastecimiento de combustible para la flota de vehículos.
143: 
144: ### 1. Dashboard (`DashboardGrifo.aspx`)
145: 
146: Pantalla de inicio con visión general de la operación de abastecimiento:
147: - Viajes activos pendientes de abastecimiento
148: - Acceso rápido a registro y búsqueda de abastecimientos
149: 
150: ### 2. Registro de Abastecimiento (`AgregarAbastecimiento.aspx`)
151: 
152: | Función | Descripción |
153: |---------|-------------|
154: | **Modo Manual** | Selección libre de conductor, placa tracto, carreta, tipo de vehículo, ruta y producto |
155: | **Modo Viaje** | Datos prellenados desde un viaje activo (conductor, placas, ruta, GL asignados) |
156: | **Tipo de Registro** | `ABASTECIMIENTO` (rutina), `MANTENIMIENTO` (servicio técnico), `OTRO` (especial) |
157: | **Tickets** | Tabla dinámica de tickets con costo USD y galones por ticket |
158: | **Cálculos automáticos** | GL Total = GL Ruta + GL Comprados, GL Consumidos, Rendimiento KM/GL |
159: | **Sincronización** | GL Comprados y Monto Total se sincronizan automáticamente desde tickets |
160: 
161: **Validación condicional por tipo:**
162: - **VIAJE PROGRAMADO**: Todos los campos obligatorios
163: - **ABASTECIMIENTO**: Placa y conductor obligatorios; producto, GL Ruta y tickets opcionales
164: - **MANTENIMIENTO / OTRO**: Solo fecha, hora y lugar son obligatorios; detalle en observaciones
165: 
166: ### 3. Búsqueda y Gestión (`BuscarAbastecimiento.aspx`)
167: 
168: | Acción | Descripción |
169: |--------|-------------|
170: | **Buscar** | Búsqueda por número de abastecimiento (exacta y LIKE) |
171: | **Ver** | Visualización completa: datos del vehículo, combustible, rendimiento, observaciones |
172: | **Editar** | Modo edición: modificar GL, montos, producto, ruta, observaciones |
173: | **Cambiar Tipo** | Dropdown para cambiar entre ABASTECIMIENTO, MANTENIMIENTO, OTRO (no aplica a VIAJE PROGRAMADO) |
174: | **Anular** | Marca el registro como `ANULADO` — banner rojo, edición bloqueada, auditoría registrada |
175: | **Eliminar** | Eliminación permanente con doble confirmación — auditoría registrada |
176: 
177: **Estados del registro:**
178: | Estado | Badge | Editable | Descripción |
179: |--------|-------|----------|-------------|
180: | ABASTECIMIENTO | 🟢 Verde | ✅ | Rutina operativa |
181: | VIAJE PROGRAMADO | 🔵 Azul | ✅ (tipo no cambiable) | Asociado a orden de viaje |
182: | MANTENIMIENTO | 🟠 Naranja | ✅ | Servicio técnico |
183: | OTRO | ⚫ Gris | ✅ | Uso especial |
184: | ANULADO | 🔴 Rojo | ❌ | Registro anulado, solo eliminar |
185: 
186: ### 4. Reportes (`ReporteAbastecimiento.aspx`)
187: 
188: | Función | Descripción |
189: |---------|-------------|
190: | **Filtros** | Por rango de fechas, conductor, placa |
191: | **Vista** | Tabla con todos los abastecimientos registrados |
192: | **Exportar Excel** | Exportación con detección dinámica de columnas (tipoAbastecimiento, rutaDescripcion) |
193: 
194: ### 5. Registro de Conductores
195: 
196: El Admin de Grifo también puede registrar y gestionar conductores desde `RegistroChoferes.aspx`, permitiéndole mantener actualizada la base de datos de personal sin depender del Admin de Transporte.
197: 
198: ### Permisos (`RolesHelper.cs`)
199: 
200: | Sección | Acceso |
201: |---------|--------|
202: | `ABASTECIMIENTO` | ✅ |
203: | `DASHBOARD_GRIFO` | ✅ |
204: | `REGISTRO_CONDUCTORES` | ✅ |
205: | `DESPACHO`, `ORDEN_VIAJE`, `REGISTRO` (completo) | ❌ |
206: | `AUDITORIA` | ❌ |
207: 
208: ---
209: 
210: ## 🚛 Rol: Conductor (`CONDUCTOR`)
211: 
212: ### Dashboard del Conductor (`DashboardConductor.aspx`)
213: 
214: El conductor accede a un dashboard con **3 pestañas**:
215: 
216: #### Pestaña 1: Mis Viajes
217: - Muestra los viajes activos que le han sido asignados por el administrador.
218: - Ve información del viaje: número, fecha de inicio, cliente, tracto, carreta.
219: 
220: #### Pestaña 2: Liquidar
221: - Si tiene un viaje asignado pendiente de liquidación, puede completar el formulario de liquidación.
222: - **¿Qué registra el conductor al liquidar?**
223:   - **Ingresos**: Montos recibidos en soles (S/) y dólares (USD).
224:   - **Gastos por concepto**, cada uno con detalle de comprobante, fecha y observaciones:
225:     - Peajes (por estación)
226:     - Alimentación
227:     - Apoyo y seguridad
228:     - Reparaciones
229:     - Movilidad
230:     - Encarpada
231:     - Hospedaje
232:     - Combustible
233:     - Gastos financieros
234:     - Gastos adicionales
235:   - **Ingresos adicionales**
236:   - **Observaciones generales**
237: - Una vez completado, envía la liquidación para revisión del administrador.
238: 
239: #### Pestaña 3: Historial
240: - Muestra el historial completo de todas las liquidaciones que el conductor ha realizado.
241: - Incluye liquidaciones aprobadas, rechazadas y pendientes.
242: 
243: ---
244: 
245: ## 🛡️ Rol: Administrador de Sistema (`ADMINISTRADOR DE SISTEMA`)
246: 
247: ### Acceso Completo
248: Tiene **todos los permisos del Administrador de Transporte** más acceso exclusivo al módulo de auditoría.
249: 
250: ### Auditoría del Sistema (`Auditoria.aspx`)
251: 
252: | Función | Descripción |
253: |---------|-------------|
254: | **Log de auditoría** | Registro de todas las acciones realizadas en el sistema (inserciones, actualizaciones, eliminaciones, logins, aprobaciones, rechazos, liquidaciones, etc.) |
255: | **Filtros** | Por fecha, acción, tabla afectada y usuario |
256: | **Estadísticas** | Total de registros, registros del día, usuarios activos (últimos 7 días), tablas afectadas |
257: | **Exportación** | Exportar log de auditoría a Excel |
258: 
259: **Acciones auditadas:**
260: - `INSERT` — Creación de registros
261: - `UPDATE` — Modificación de registros
262: - `DELETE` — Eliminación de registros
263: - `ANULAR` — Anulación de registros (ej: abastecimiento)
264: - `LOGIN` — Inicio de sesión exitoso
265: - `LOGIN_FALLIDO` — Intento de login fallido
266: - `LOGOUT` — Cierre de sesión
267: - `APROBAR` — Aprobación de liquidación
268: - `RECHAZAR` — Rechazo de liquidación
269: - `LIQUIDAR` — Envío de liquidación por conductor
270: - `RETIRAR` — Retiro de operación
271: 
272: ---
273: 
274: ## 🔐 Funcionalidades Comunes (Todos los Roles)
275: 
276: | Función | Ubicación |
277: |---------|-----------|
278: | **Login** | `Login.aspx` — Autenticación con protección anti-fijación de sesión |
279: | **Cambiar Contraseña** | Modal en `Site.Master` — Disponible para todos los roles |
280: | **Recuperar Contraseña** | `RecuperarContrasena.aspx` → `RestablecerContrasena.aspx` |
281: | **Cerrar Sesión** | Botón en navbar, registrado en auditoría |
282: 
283: ---
284: 
285: ## 🗂️ Estructura de Navegación (Navbar)
286: 
287: ### Menú Admin / Admin Sistema
288: ```
289: ├── Despacho
290: │   ├── Agregar Despacho
291: │   └── Listar Despachos
292: ├── Orden de Viaje
293: │   ├── Liquidaciones Pendientes
294: │   └── Reportes
295: ├── Registro
296: │   ├── Conductores
297: │   ├── Tractos
298: │   ├── Semiremolques
299: │   ├── Clientes
300: │   ├── Peajes
301: │   └── Plantas
302: └── Configuración
303:     ├── Cambiar Contraseña
304:     └── Auditoría del Sistema (solo Admin Sistema)
305: ```
306: 
307: ### Menú Administrador de Grifo
308: ```
309: ├── Inicio (DashboardGrifo)
310: ├── Abastecimiento
311: │   ├── Registrar Abastecimiento
312: │   ├── Buscar Abastecimiento
313: │   └── Reporte
314: ├── Registro
315: │   └── Conductores
316: └── Configuración
317:     └── Cambiar Contraseña
318: ```
319: 
320: ### Menú Conductor
321: ```
322: ├── Inicio (Dashboard)
323: └── Configuración
324:     └── Cambiar Contraseña
325: ```
326: 
327: ---
328: 
329: ## 📊 Flujo de Datos Financieros (Liquidación)
330: 
331: ```
332: Conductor registra liquidación:
333:     ├── Ingresos (S/ y USD)
334:     ├── Gastos por concepto (peajes, alimentación, reparaciones, etc.)
335:     └── Envía para revisión
336:             │
337:             ▼
338: Admin revisa liquidación pendiente:
339:     ├── Aprueba → Se calculan Descuentos y Reintegros
340:     │               ├── Descuento: Monto que se descuenta al conductor
341:     │               └── Reintegro: Monto que se devuelve al conductor
342:     │               └── Queda registrado en Reportes
343:     └── Rechaza → Vuelve al conductor con observaciones
344: ```
345: 
346: ---
347: 
348: ## 🛠️ Stack Tecnológico
349: 
350: | Componente | Tecnología |
351: |-----------|------------|
352: | Framework | ASP.NET Web Forms (.NET Framework 4.8) |
353: | Lenguaje | C# 7.3 |
354: | Base de datos | SQL Server |
355: | ORM/Acceso a datos | ADO.NET (SqlConnection, SqlCommand, DataTable) |
356: | Frontend | Bootstrap 4.6, jQuery 3.6, Font Awesome 5, Select2 4.1 |
357: | Exportación | ClosedXML (Excel), iTextSharp (PDF), EPPlus |
358: | Serialización | Newtonsoft.Json |
359: | Autenticación | Sesión ASP.NET con cookie temporal + anti-fijación de sesión |
360: | Auditoría | Tabla `AuditoriaLog` con helper `AuditoriaHelper.cs` |
361: 
362: ---
363: 
364: ## 🗄️ Columnas Dinámicas (Migraciones)
365: 
366: El sistema utiliza detección dinámica de columnas mediante `ColumnaExisteEnTabla()` (consulta `sys.columns`) para soportar columnas que pueden no existir si la migración SQL aún no se ha ejecutado:
367: 
368: | Tabla | Columna | Script de Migración | Descripción |
369: |-------|---------|--------------------|--------------|
370: | `AbastecimientoCombustible` | `tipoAbastecimiento` | `script_AgregarColumnaTipoAbastecimiento.sql` | Tipo: ABASTECIMIENTO, MANTENIMIENTO, OTRO, VIAJE PROGRAMADO, ANULADO |
371: | `AbastecimientoCombustible` | `rutaDescripcion` | `script_AgregarColumnaRutaDescripcion.sql` | Descripción libre de la ruta del viaje |
372: 
373: ---
374: 
375: *Documento actualizado como referencia del flujo de trabajo actual del sistema SGV.*

(End of file - total 375 lines)
</content>
```

---

## Assistant (Orchestrator · Claude Sonnet 4.6 · 124.0s)

Ahora escribo el archivo actualizado con toda la información nueva:

**Tool: write**

**Input:**
```json
{
  "filePath": "C:\\Users\\NICK\\Downloads\\WebSGV-master\\WebSGV-master\\WebSGV-master\\docs\\FLUJO_DE_TRABAJO_SGV.md",
  "content": "# Sistema SGV — Flujo de Trabajo y Roles\n\n## 📋 Descripción General\n\n**SGV (Sistema de Gestión de Viajes)** es una aplicación web ASP.NET Web Forms (.NET Framework 4.8) orientada a la gestión de transporte de carga. Administra el ciclo completo desde la programación de despachos, seguimiento de viajes, liquidación de gastos firmada digitalmente por conductores, hasta la aprobación administrativa con ajustes auditados. Incluye además módulos de abastecimiento de combustible, gestión de maquinaria pesada y parte diario de operadores.\n\n---\n\n## 👥 Roles del Sistema\n\nEl sistema maneja **7 roles** definidos en `WebSGV/Views/RolesHelper.cs`:\n\n| Rol | Constante | Valor en BD | Redirección Login | Descripción |\n|-----|-----------|-------------|-------------------|-------------|\n| **Administrador de Transporte** | `ROL_ADMIN` | `ADMIN` (también acepta `ADMINISTRADOR`) | `Inicio.aspx` | Gestión operativa completa: despachos, viajes, liquidaciones, registros y reportes |\n| **Administrador de Sistema** | `ROL_ADMIN_SISTEMA` | `ADMINISTRADOR DE SISTEMA` | `Inicio.aspx` | Todo lo del Admin + módulo de auditoría exclusivo |\n| **Conductor** | `ROL_CONDUCTOR` | `CONDUCTOR` (también `CHOFER`) | `DashboardConductor.aspx` | Liquidación firmada de viajes asignados y consulta de historial |\n| **Administrador de Grifo** | `ROL_ADMIN_GRIFO` | `ADMINISTRADOR DE GRIFO` | `DashboardGrifo.aspx` | Gestión integral de abastecimiento de combustible |\n| **Administrador de Maquinaria** | `ROL_ADMIN_MAQUINARIA` | `ADMINISTRADOR DE MAQUINARIA` | `Inicio.aspx` | Gestión de equipos, obras, operadores y asignaciones |\n| **Operador** | `ROL_OPERADOR` | `OPERADOR` | `DashboardOperador.aspx` | Registro de parte diario de trabajo en maquinaria pesada |\n| **Supervisor** | `ROL_SUPERVISOR` | `SUPERVISOR` | `Inicio.aspx` | Permisos similares a ADMIN en despachos, órdenes de viaje, registros y abastecimiento |\n\n> Los roles se almacenan como texto en la columna `rol` de la tabla `Usuarios`. No existe tabla de roles separada. Las comparaciones siempre se hacen en mayúsculas (`.ToUpper()`).\n\n---\n\n## 🔑 Matriz de Permisos por Sección\n\n| Sección (`TienePermiso`) | ADMIN | ADMIN_SISTEMA | CONDUCTOR | ADMIN_GRIFO | ADMIN_MAQUINARIA | OPERADOR | SUPERVISOR |\n|--------------------------|:-----:|:-------------:|:---------:|:-----------:|:----------------:|:--------:|:----------:|\n| `DESPACHO` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |\n| `FACTURA` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |\n| `CPIC` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |\n| `ORDEN_VIAJE` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |\n| `REGISTRO` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |\n| `CONSULTAS` / `INDICADORES` | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |\n| `REGISTRO_CONDUCTORES` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |\n| `ABASTECIMIENTO` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |\n| `DASHBOARD_GRIFO` | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |\n| `AUDITORIA` | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |\n| `DASHBOARD_CONDUCTOR` / `MIS_VIAJES` | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |\n| `DASHBOARD_OPERADOR` / `PARTE_DIARIO` | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ |\n\n---\n\n## 🔄 Flujo de Trabajo General\n\n### Flujo de Transporte (Admin de Transporte / Supervisor)\n\n```\nProgramación de Despacho (Admin / Supervisor)\n        │\n        ▼\nAsignación de Conductor, Tracto, Carreta → se crean ViajesProgreso\n        │\n        ▼\nViaje en Progreso (conductor lo ve en su Dashboard)\n        │\n        ▼\nConductor Liquida el Viaje (registra ingresos y gastos)\n        │\n        ▼\nConductor Firma Digitalmente (canvas biométrico en FirmarLiquidacion.aspx)\n  → Trazo PNG almacenado en tabla FirmaDigital (append-only, hash SHA-256)\n  → idFirmaConductor registrado en OrdenViaje\n        │\n        ▼\nLiquidación Pendiente de Revisión (Admin / Supervisor)\n        │\n        ▼\nAdmin Revisa: Aprobar / Rechazar / Editar\n        │\n        ├── Si Aprueba →\n        │     · Firma del Admin registrada (Nivel C — solo metadata)\n        │     · Ajustes (Descuentos / Reintegros) guardados en OrdenViajeAjuste\n        │     · No invalida la firma original del conductor\n        │     · PDF firmado generado y archivado en ~/App_Data/OrdenesViaje\n        │     → Pasa a Historial / Reportes\n        └── Si Rechaza → Regresa al Conductor para corrección\n```\n\n### Flujo de Abastecimiento (Admin de Grifo)\n\n```\nDashboardGrifo.aspx — lista viajes activos pendientes de abastecimiento\n        │\n        ├── [Abastecer] → AgregarAbastecimiento.aspx (Modo Viaje, datos prellenados)\n        └── [Agregar manual] → AgregarAbastecimiento.aspx (Modo Manual, dropdowns)\n        │\n        ▼\nRegistro de Tickets (costo USD + galones)\nCálculos automáticos: GL Total, GL Consumidos, Rendimiento KM/GL\n        │\n        ▼\nGuardar → número correlativo generado\n        │\n        ├── [Despacho a Obra] → RegistrarDespachoObra.aspx\n        │     Cisterna lleva combustible a una obra\n        │     Registra galones salida / retorno → galonesAbastecidos = salida - retorno\n        │\n        ├── [Retorno Ecuador] → RegistrarRetornoEcuador.aspx\n        │     Ingreso de combustible desde Ecuador con tickets detallados\n        │\n        └── Gestión posterior (BuscarAbastecimiento.aspx):\n              ├── Editar datos (GL, montos, producto, ruta, tipo)\n              ├── Cambiar Tipo (ABASTECIMIENTO ↔ MANTENIMIENTO ↔ OTRO)\n              ├── Anular → Marca como ANULADO (no editable)\n              └── Eliminar → Eliminación permanente con confirmación\n```\n\n### Flujo de Maquinaria (Admin de Maquinaria / Operador)\n\n```\nAdmin Maquinaria configura maestros:\n  · RegistroEquiposMaquinaria.aspx (topadoras, motoniveladoras, etc.)\n  · RegistroObras.aspx\n  · RegistroClientesObra.aspx\n  · RegistroOperadores.aspx\n        │\n        ▼\nAsignacionesMaquinaria.aspx — Vincula Operador + Equipo + Obra (estado ACTIVA)\n        │\n        ▼\nOperador inicia sesión → DashboardOperador.aspx\n  · Ve su asignación activa (equipo + obra + cliente)\n  · Registra Parte Diario de Trabajo (número: PT-YYYY-XXXXXX)\n    - Odómetro inicio/fin, horómetro inicio/fin\n    - Consumo: petróleo, gasolina, aceite, grasa\n    - Carretera, sector, labor realizada\n    - Reclamos y observaciones\n        │\n        ▼\nHistorial de partes accesible en Dashboard\n```\n\n---\n\n## 🔧 Rol: Administrador de Transporte (`ADMIN`) y Supervisor\n\n### 1. Gestión de Despachos\n\n| Página | Función |\n|--------|---------|\n| `RegistroDespacho.aspx` | **Agregar Despacho** — Programación de viajes. Crea lote con: fecha, cliente, número de pedido, tipo de operación (nacional/internacional), planta, documentación (factura, CPIC) y asignación de conductores con tractos y carretas. |\n| `ListaDespachos.aspx` | **Listar Despachos** — Vista general de todos los despachos. Permite ver detalles, filtrar y gestionar. Muestra viajes activos y despachos por viaje. |\n| `EditarDespacho.aspx` | **Editar Despacho** — Modificar un despacho existente (solo si no ha sido liquidado). |\n\n**Flujo de Despacho:**\n1. Admin crea un nuevo despacho (`RegistroDespacho.aspx`)\n2. Selecciona cliente, tipo de operación, planta\n3. Agrega documentación base (factura, CPIC si es internacional)\n4. Asigna uno o más conductores con su tracto y carreta\n5. Genera el lote → Se crean los viajes en progreso\n6. Los despachos se visualizan en `ListaDespachos.aspx`\n7. Desde la lista puede editar o gestionar documentación (facturas, CPIC, guías)\n\n### 2. Órdenes de Viaje y Liquidaciones\n\n| Página | Función |\n|--------|---------|\n| `LiquidacionesPendientes.aspx` | **Liquidaciones Pendientes** — Lista de liquidaciones firmadas por conductores pendientes de revisión del admin. |\n| `DetalleOrdenViaje.aspx` | **Detalle de Orden de Viaje** — Vista completa de una liquidación con todos los datos financieros y estado de firma. |\n| `AgregarOrdenViaje.aspx` | **Crear Orden de Viaje** — Generación de la orden a partir del despacho finalizado (camino alternativo del admin). |\n| `BuscarOrdenViaje.aspx` | **Búsqueda Avanzada** — Búsqueda de órdenes por múltiples criterios. |\n| `FirmarLiquidacion.aspx` | **Firma Digital** — Canvas biométrico del conductor. Previo al envío de la liquidación. |\n\n**Acciones sobre liquidaciones pendientes:**\n- ✅ **Aprobar** — Acepta la liquidación. Se registra firma del admin y se guardan ajustes en `OrdenViajeAjuste`. Se genera PDF firmado.\n- ❌ **Rechazar** — Devuelve la liquidación al conductor con observaciones.\n- ✏️ **Editar** — Permite al admin modificar datos antes de aprobar.\n- 👁️ **Ver** — Consulta el detalle completo sin modificar.\n\n### 3. Historial y Reportes\n\n| Página | Sección | Función |\n|--------|---------|---------|\n| `ReportesOrdenesViaje.aspx` | **Liquidaciones** | Liquidaciones aprobadas con filtros por fecha y factor de conversión (USD → S/). Incluye detalle de descuentos y reintegros. Exportable a Excel y PDF. |\n| `ReportesOrdenesViaje.aspx` | **Viajes Activos Sin Liquidación** | Conductores con viajes en progreso que aún no han enviado su liquidación. |\n| `Reportes.aspx` | **Reportes Avanzados** | Módulo independiente con filtros por conductor, vehículo, producto, tipo de transacción. Exportación avanzada. Vista de resultados en `ReporteResultado.aspx`. |\n\n### 4. Módulo de Registros (Maestros)\n\n| Página | Función |\n|--------|---------|\n| `RegistroChoferes.aspx` | Registrar, editar y gestionar **conductores** |\n| `RegistroTractos.aspx` | Registrar, editar y gestionar **tractos** (camiones) |\n| `RegistroSemiremolques.aspx` | Registrar, editar y gestionar **semiremolques** (carretas) |\n| `RegistroClientes.aspx` | Registrar, editar y gestionar **clientes de transporte** |\n| `RegistroPeajes.aspx` | Registrar, editar y gestionar **estaciones de peaje** |\n| `RegistroPlantas.aspx` | Registrar, editar y gestionar **plantas** (puntos de carga/descarga) |\n| `RegistroRutas.aspx` | Registrar, editar y gestionar **rutas de transporte** |\n| `RegistroProductos.aspx` | Registrar, editar y gestionar **productos transportados** |\n\n### 5. Otros Módulos del Admin\n\n| Página | Función |\n|--------|---------|\n| `AgregarCPIC.aspx` / `BuscarCPIC.aspx` | Gestión de documentos CPIC (operaciones internacionales) |\n| `AgregarFactura.aspx` / `BuscarFactura.aspx` | Gestión de facturas |\n| `AgregarIndicadores.aspx` | Registro de indicadores de tiempo operativo (horas de salida, llegada, carga, descarga) |\n| `CargarExcel.aspx` | Carga masiva de datos desde archivo Excel |\n| `DescargarPdfOrdenViaje.aspx` | Endpoint que genera y sirve el PDF de una orden de viaje (iTextSharp) |\n| `DescargarPdfAbastecimiento.aspx` | Endpoint que genera y sirve el PDF de un abastecimiento |\n\n---\n\n## ⛽ Rol: Administrador de Grifo (`ADMINISTRADOR DE GRIFO`)\n\n### 1. Dashboard (`DashboardGrifo.aspx`)\n\nPantalla de inicio con visión general de la operación:\n- Viajes activos pendientes de abastecimiento\n- Acceso rápido a registro y búsqueda\n\n### 2. Registro de Abastecimiento (`AgregarAbastecimiento.aspx`)\n\n| Función | Descripción |\n|---------|-------------|\n| **Modo Manual** | Selección libre de conductor, placa tracto, carreta, tipo de vehículo, ruta y producto |\n| **Modo Viaje** | Datos prellenados desde un viaje activo (conductor, placas, ruta, GL asignados) |\n| **Tipo de Registro** | `ABASTECIMIENTO` (rutina), `MANTENIMIENTO` (servicio técnico), `OTRO` (especial) |\n| **Tickets** | Tabla dinámica de tickets con costo USD y galones por ticket |\n| **Cálculos automáticos** | GL Total = GL Ruta + GL Comprados; GL Consumidos; Rendimiento KM/GL |\n\n**Validación condicional por tipo:**\n- **VIAJE PROGRAMADO**: Todos los campos obligatorios\n- **ABASTECIMIENTO**: Placa y conductor obligatorios; producto, GL Ruta y tickets opcionales\n- **MANTENIMIENTO / OTRO**: Solo fecha, hora y lugar son obligatorios\n\n### 3. Despacho a Obra (`RegistrarDespachoObra.aspx`)\n\nMódulo de despacho de cisterna hacia una obra específica.\n- Registra galones de salida y galones de retorno\n- `galonesAbastecidos = galones_salida − galones_retorno`\n- Persiste en tabla `DespachoCombustibleObra`\n\n### 4. Retorno Ecuador (`RegistrarRetornoEcuador.aspx`)\n\nRegistro de ingresos de combustible proveniente de Ecuador.\n- Captura múltiples tickets detallados por operación\n- Persiste en tablas `IngresoCombustibleEcuador` + `DetalleTicketEcuador`\n\n### 5. Búsqueda y Gestión (`BuscarAbastecimiento.aspx`)\n\n| Acción | Descripción |\n|--------|-------------|\n| **Buscar** | Búsqueda por número de abastecimiento (exacta y LIKE) |\n| **Ver** | Visualización completa: datos del vehículo, combustible, rendimiento, observaciones |\n| **Editar** | Modificar GL, montos, producto, ruta, observaciones |\n| **Cambiar Tipo** | Cambiar entre ABASTECIMIENTO, MANTENIMIENTO, OTRO (no aplica a VIAJE PROGRAMADO) |\n| **Anular** | Marca como `ANULADO` — banner rojo, edición bloqueada, auditoría registrada |\n| **Eliminar** | Eliminación permanente con doble confirmación — auditoría registrada |\n\n**Estados del registro:**\n\n| Estado | Editable | Descripción |\n|--------|----------|-------------|\n| ABASTECIMIENTO | ✅ | Rutina operativa |\n| VIAJE PROGRAMADO | ✅ (tipo no cambiable) | Asociado a orden de viaje |\n| MANTENIMIENTO | ✅ | Servicio técnico |\n| OTRO | ✅ | Uso especial |\n| ANULADO | ❌ | Registro anulado, solo eliminar |\n\n### 6. Reportes (`ReporteAbastecimiento.aspx`)\n\n| Función | Descripción |\n|---------|-------------|\n| **Filtros** | Por rango de fechas, conductor, placa |\n| **Vista** | Tabla con todos los abastecimientos registrados |\n| **Exportar Excel** | Exportación con detección dinámica de columnas (`tipoAbastecimiento`, `rutaDescripcion`) |\n\n### 7. Registro de Conductores\n\nEl Admin de Grifo puede registrar y gestionar conductores desde `RegistroChoferes.aspx`.\n\n---\n\n## 🚛 Rol: Conductor (`CONDUCTOR`)\n\n### Dashboard del Conductor (`DashboardConductor.aspx`)\n\nEl conductor accede a un dashboard con **3 pestañas**:\n\n#### Pestaña 1: Mis Viajes\n- Viajes activos asignados por el administrador\n- Información: número, fecha inicio, cliente, tracto, carreta\n\n#### Pestaña 2: Liquidar\n- Formulario de liquidación del viaje pendiente\n- **Registra:**\n  - **Ingresos**: Montos en S/ y USD\n  - **Gastos por concepto** (con comprobante, fecha, observaciones):\n    - Peajes (por estación), Alimentación, Apoyo y seguridad, Reparaciones, Movilidad, Encarpada, Hospedaje, Combustible, Gastos financieros, Gastos adicionales\n  - **Ingresos adicionales**\n  - **Observaciones generales**\n- Antes del envío: firma biométrica en `FirmarLiquidacion.aspx`\n  - Canvas HTML5 para dibujar el trazo\n  - Hash SHA-256 del documento\n  - Trazo PNG almacenado en `FirmaDigital` (append-only)\n  - `idFirmaConductor` registrado en `OrdenViaje`\n- Envía la liquidación para revisión del administrador\n\n#### Pestaña 3: Historial\n- Historial completo de liquidaciones (aprobadas, rechazadas, pendientes)\n\n---\n\n## 🔩 Rol: Administrador de Maquinaria (`ADMINISTRADOR DE MAQUINARIA`)\n\n### Módulo de Maestros de Maquinaria\n\n| Página | Función |\n|--------|---------|\n| `RegistroEquiposMaquinaria.aspx` | Registro y gestión de equipos (topadoras, motoniveladoras, etc.) |\n| `RegistroObras.aspx` | Registro y gestión de obras |\n| `RegistroClientesObra.aspx` | Registro y gestión de clientes de obra (entidad diferente a clientes de transporte) |\n| `RegistroOperadores.aspx` | Registro y gestión de operadores de maquinaria |\n| `AsignacionesMaquinaria.aspx` | Vinculación Operador → Equipo → Obra (estado `ACTIVA`) |\n\n### Gestión de Asignaciones\n\n1. Crear asignación vinculando un operador, un equipo y una obra\n2. Cada operador puede tener una asignación `ACTIVA` a la vez\n3. El operador verá su asignación al ingresar a `DashboardOperador.aspx`\n\n---\n\n## 🏗️ Rol: Operador (`OPERADOR`)\n\n### Dashboard del Operador (`DashboardOperador.aspx`)\n\n- Ve su asignación activa: equipo + obra + cliente\n- **Registra Parte Diario de Trabajo** (número: `PT-YYYY-XXXXXX`):\n  - Odómetro inicio/fin\n  - Horómetro inicio/fin\n  - Consumo de petróleo, gasolina, aceite, grasa\n  - Carretera y sector de trabajo\n  - Labor realizada\n  - Reclamos y observaciones\n- Historial de partes diarios registrados\n\n---\n\n## 🛡️ Rol: Administrador de Sistema (`ADMINISTRADOR DE SISTEMA`)\n\n### Acceso Completo\nTiene **todos los permisos del Administrador de Transporte** más acceso exclusivo al módulo de auditoría.\n\n### Auditoría del Sistema\n\n| Página | Función |\n|--------|---------|\n| `Auditoria.aspx` | Log principal de auditoría con estadísticas, filtros y exportación |\n| `ConsultaAuditoria.aspx` | Segunda vista de consulta de auditoría (filtros alternativos) |\n\n| Función | Descripción |\n|---------|-------------|\n| **Log de auditoría** | Registro de todas las acciones del sistema |\n| **Filtros** | Por fecha, acción, tabla afectada y usuario |\n| **Estadísticas** | Total de registros, del día, usuarios activos (últimos 7 días), tablas afectadas |\n| **Exportación** | Exportar log a Excel |\n\n**Acciones auditadas:**\n\n| Acción | Descripción |\n|--------|-------------|\n| `INSERT` | Creación de registros |\n| `UPDATE` | Modificación de registros |\n| `DELETE` | Eliminación de registros |\n| `ANULAR` | Anulación de registros |\n| `LOGIN` | Inicio de sesión exitoso |\n| `LOGIN_FALLIDO` | Intento de login fallido |\n| `LOGOUT` | Cierre de sesión |\n| `APROBAR` | Aprobación de liquidación |\n| `RECHAZAR` | Rechazo de liquidación |\n| `LIQUIDAR` | Envío de liquidación por conductor |\n| `RETIRAR` | Retiro/cancelación de liquidación ya enviada (`sp_DC_RetirarLiquidacion`) |\n\n---\n\n## 🔐 Funcionalidades Comunes (Todos los Roles)\n\n| Función | Ubicación |\n|---------|-----------|\n| **Login** | `Login.aspx` — Autenticación con anti-fijación de sesión |\n| **Cambiar Contraseña** | Modal en `Site.Master` |\n| **Recuperar Contraseña** | `RecuperarContrasena.aspx` → `RestablecerContrasena.aspx` |\n| **Cerrar Sesión** | Botón en navbar, registrado en auditoría |\n\n---\n\n## ✍️ Flujo de Firma Digital\n\nEl sistema implementa un ciclo de firma digital biométrica en las liquidaciones:\n\n```\nConductor llena liquidación en DashboardConductor.aspx\n        │\n        ▼\nFirmarLiquidacion.aspx — Canvas de firma biométrica\n  · Trazo dibujado en canvas HTML5\n  · Hash SHA-256 del documento generado\n  · Imagen PNG del trazo almacenada en tabla FirmaDigital (append-only)\n  · idFirmaConductor registrado en OrdenViaje\n        │\n        ▼\nAdmin revisa en LiquidacionesPendientes.aspx\n  · Al aprobar: firma del admin registrada (Nivel C — solo metadata)\n  · idFirmaAdmin registrado en OrdenViaje\n  · Ajustes (descuentos/reintegros) guardados en OrdenViajeAjuste con FK a FirmaDigital\n  · La firma original del conductor NO se invalida\n        │\n        ▼\nPDF firmado generado y archivado en ~/App_Data/OrdenesViaje\n  · rutaPdfFirmado y hashPdfFirmado en OrdenViaje para verificación futura\n```\n\n**Tablas involucradas:**\n\n| Tabla | Descripción |\n|-------|-------------|\n| `FirmaDigital` | Append-only. Imagen PNG del trazo + hash SHA-256 + metadatos |\n| `OrdenViajeAjuste` | Descuentos/reintegros formales con FK a `FirmaDigital` |\n| `FormatoControlado` | Catálogo ISO 9001/14001/45001/BASC para encabezados de documentos |\n\n**Columnas en `OrdenViaje`:**\n`idFirmaConductor`, `idFirmaAdmin`, `rutaPdfFirmado`, `hashPdfFirmado`, `fechaEnvioFirmado`, `fechaAprobacionFirmada`\n\n---\n\n## 📊 Flujo de Datos Financieros (Liquidación)\n\n```\nConductor registra liquidación:\n    ├── Ingresos (S/ y USD)\n    ├── Gastos por concepto (peajes, alimentación, reparaciones, etc.)\n    └── Firma y envía para revisión\n            │\n            ▼\nAdmin revisa liquidación pendiente:\n    ├── Aprueba →\n    │     · Ajustes (Descuentos y Reintegros) guardados en OrdenViajeAjuste\n    │     · Descuento: Monto a descontar al conductor\n    │     · Reintegro: Monto a devolver al conductor\n    │     · PDF firmado archivado en App_Data/\n    │     · Queda registrado en Reportes\n    └── Rechaza → Vuelve al conductor con observaciones\n```\n\n---\n\n## 🗂️ Estructura de Navegación (Navbar)\n\n### Menú Admin / Supervisor\n```\n├── Despacho\n│   ├── Agregar Despacho\n│   └── Listar Despachos\n├── Orden de Viaje\n│   ├── Liquidaciones Pendientes\n│   ├── Buscar Orden de Viaje\n│   └── Reportes\n├── Registro\n│   ├── Conductores\n│   ├── Tractos\n│   ├── Semiremolques\n│   ├── Clientes\n│   ├── Peajes\n│   ├── Plantas\n│   ├── Rutas\n│   └── Productos\n├── Documentos\n│   ├── Facturas\n│   └── CPIC\n├── Herramientas\n│   ├── Indicadores\n│   ├── Cargar Excel\n│   └── Reportes Avanzados\n└── Configuración\n    ├── Cambiar Contraseña\n    └── Auditoría del Sistema (solo Admin Sistema)\n```\n\n### Menú Administrador de Grifo\n```\n├── Inicio (DashboardGrifo)\n├── Abastecimiento\n│   ├── Registrar Abastecimiento\n│   ├── Buscar Abastecimiento\n│   ├── Despacho a Obra\n│   ├── Retorno Ecuador\n│   └── Reporte\n├── Registro\n│   └── Conductores\n└── Configuración\n    └── Cambiar Contraseña\n```\n\n### Menú Administrador de Maquinaria\n```\n├── Inicio\n├── Maquinaria\n│   ├── Equipos\n│   ├── Obras\n│   ├── Clientes de Obra\n│   ├── Operadores\n│   └── Asignaciones\n└── Configuración\n    └── Cambiar Contraseña\n```\n\n### Menú Conductor\n```\n├── Inicio (DashboardConductor)\n└── Configuración\n    └── Cambiar Contraseña\n```\n\n### Menú Operador\n```\n├── Inicio (DashboardOperador)\n└── Configuración\n    └── Cambiar Contraseña\n```\n\n---\n\n## 🛠️ Stack Tecnológico\n\n| Componente | Tecnología |\n|-----------|------------|\n| Framework | ASP.NET Web Forms (.NET Framework 4.8) |\n| Lenguaje | C# 7.3 |\n| Base de datos | SQL Server (somee.com) |\n| Acceso a datos | ADO.NET (SqlConnection, SqlCommand, DataTable) |\n| Frontend | Bootstrap 4.6, jQuery 3.6, Font Awesome 5, Select2 4.1 |\n| Exportación PDF | iTextSharp 5.5.13.4 (**NO** iText 7 — versión AGPL legacy) |\n| Exportación Excel | EPPlus 8, ClosedXML |\n| Serialización | Newtonsoft.Json |\n| Autenticación | Sesión ASP.NET (`SGV_SessionId`, 30 min, anti-fijación) |\n| Auditoría | Tabla `AuditoriaLog` auto-creada en `Application_Start` via `AuditoriaHelper.cs` |\n\n---\n\n## 🗄️ Columnas Dinámicas (Migraciones)\n\nEl sistema usa detección dinámica de columnas via `ColumnaExisteEnTabla()` (consulta `sys.columns`) para columnas que pueden no existir si la migración aún no se aplicó:\n\n| Tabla | Columna | Script de Migración | Descripción |\n|-------|---------|--------------------|-------------|\n| `AbastecimientoCombustible` | `tipoAbastecimiento` | `script_AgregarColumnaTipoAbastecimiento.sql` | Tipo: ABASTECIMIENTO, MANTENIMIENTO, OTRO, VIAJE PROGRAMADO, ANULADO |\n| `AbastecimientoCombustible` | `rutaDescripcion` | `script_AgregarColumnaRutaDescripcion.sql` | Descripción libre de la ruta del viaje |\n\n---\n\n## ⚠️ Notas de Infraestructura\n\n- **`Application_Error` (`Global.asax.cs`)**: Errores de ViewState redirigen silenciosamente a `Login.aspx?error=sesion`. Otros errores solo se loguean en Debug (no van a `Error.aspx`).\n- **Routing**: `RouteConfig.cs` está vacío. No hay routing real — las páginas se acceden por URL física `.aspx`.\n- **Páginas de artefacto** (no usar en producción): `TestGenerarPdfOrdenViaje.aspx`, `WebForm2.aspx`.\n- **PDFs archivados**: En `~/App_Data/OrdenesViaje` — **no commit** de esta carpeta.\n- **Secretos**: `connectionStrings.config` y `appSettings.Secrets.config` están **gitignoreados**. Nunca commitear.\n\n---\n\n*Documento actualizado — Abril 2026. Refleja el estado real del código en `WebSGV/Views/` y `WebSGV/Database/`.*\n"
}
```

**Output:**
```
Wrote file successfully.
```

---

## Assistant (Orchestrator · Claude Sonnet 4.6 · 11.7s)

El análisis está completo y el archivo ha sido actualizado. Aquí el resumen de lo que se hizo:

---

## ✅ Archivo actualizado

**Ruta:** `docs/FLUJO_DE_TRABAJO_SGV.md`

---

## 📋 Principales cambios realizados

### Roles — de 4 a 7
El documento anterior decía que había **4 roles**. En realidad el código define **7**:
- ✅ Ya documentados: ADMIN, ADMIN_SISTEMA, CONDUCTOR, ADMIN_GRIFO
- 🆕 Agregados: **SUPERVISOR**, **ADMINISTRADOR DE MAQUINARIA**, **OPERADOR**

### Módulo de Maquinaria (completamente nuevo en el doc)
Flujo completo de: Equipos → Obras → Clientes de Obra → Operadores → Asignaciones → Parte Diario (`DashboardOperador.aspx`).

### Flujo de Firma Digital (completamente nuevo en el doc)
Canvas biométrico en `FirmarLiquidacion.aspx`, tabla `FirmaDigital` (append-only con hash SHA-256), `OrdenViajeAjuste` para ajustes firmados del admin, y columnas de firma en `OrdenViaje`.

### Abastecimiento — 2 módulos nuevos
- `RegistrarDespachoObra.aspx` — despacho de cisterna a obra
- `RegistrarRetornoEcuador.aspx` — ingreso de combustible desde Ecuador

### Otras páginas no documentadas
Facturas, reportes avanzados (`Reportes.aspx`), carga Excel, descarga de PDFs, `ConsultaAuditoria.aspx`.

### Correcciones menores
Matriz de permisos completa, menús de navegación actualizados para todos los roles, notas de infraestructura (`Application_Error`, routing, artefactos de dev).

---

