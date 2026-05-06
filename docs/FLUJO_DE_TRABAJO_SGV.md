# Sistema SGV — Flujo de Trabajo y Roles

## 📋 Descripción General

**SGV (Sistema de Gestión de Viajes)** es una aplicación web ASP.NET Web Forms (.NET Framework 4.8) orientada a la gestión de transporte de carga. Administra el ciclo completo desde la programación de despachos, seguimiento de viajes, liquidación de gastos firmada digitalmente por conductores, hasta la aprobación administrativa con ajustes auditados. Incluye además módulos de abastecimiento de combustible, gestión de maquinaria pesada y parte diario de operadores.

---

## 👥 Roles del Sistema

El sistema maneja **7 roles** definidos en `WebSGV/Views/RolesHelper.cs`:

| Rol | Constante | Valor en BD | Redirección Login | Descripción |
|-----|-----------|-------------|-------------------|-------------|
| **Administrador de Transporte** | `ROL_ADMIN` | `ADMIN` (también acepta `ADMINISTRADOR`) | `Inicio.aspx` | Gestión operativa completa: despachos, viajes, liquidaciones, registros y reportes |
| **Administrador de Sistema** | `ROL_ADMIN_SISTEMA` | `ADMINISTRADOR DE SISTEMA` | `Inicio.aspx` | Todo lo del Admin + módulo de auditoría exclusivo |
| **Conductor** | `ROL_CONDUCTOR` | `CONDUCTOR` (también `CHOFER`) | `DashboardConductor.aspx` | Liquidación firmada de viajes asignados y consulta de historial |
| **Administrador de Grifo** | `ROL_ADMIN_GRIFO` | `ADMINISTRADOR DE GRIFO` | `DashboardGrifo.aspx` | Gestión integral de abastecimiento de combustible |
| **Administrador de Maquinaria** | `ROL_ADMIN_MAQUINARIA` | `ADMINISTRADOR DE MAQUINARIA` | `Inicio.aspx` | Gestión de equipos, obras, operadores y asignaciones |
| **Operador** | `ROL_OPERADOR` | `OPERADOR` | `DashboardOperador.aspx` | Registro de parte diario de trabajo en maquinaria pesada |
| **Supervisor** | `ROL_SUPERVISOR` | `SUPERVISOR` | `Inicio.aspx` | Permisos similares a ADMIN en despachos, órdenes de viaje, registros y abastecimiento |

> Los roles se almacenan como texto en la columna `rol` de la tabla `Usuarios`. No existe tabla de roles separada. Las comparaciones siempre se hacen en mayúsculas (`.ToUpper()`).

---

## 🔑 Matriz de Permisos por Sección

| Sección (`TienePermiso`) | ADMIN | ADMIN_SISTEMA | CONDUCTOR | ADMIN_GRIFO | ADMIN_MAQUINARIA | OPERADOR | SUPERVISOR |
|--------------------------|:-----:|:-------------:|:---------:|:-----------:|:----------------:|:--------:|:----------:|
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

## 🔄 Flujo de Trabajo General

### Flujo de Transporte (Admin de Transporte / Supervisor)

```
Programación de Despacho (Admin / Supervisor)
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
  → Trazo PNG almacenado en tabla FirmaDigital (append-only, hash SHA-256)
  → idFirmaConductor registrado en OrdenViaje
        │
        ▼
Liquidación Pendiente de Revisión (Admin / Supervisor)
        │
        ▼
Admin Revisa: Aprobar / Rechazar / Editar
        │
        ├── Si Aprueba →
        │     · Firma del Admin registrada (Nivel C — solo metadata)
        │     · Ajustes (Descuentos / Reintegros) guardados en OrdenViajeAjuste
        │     · No invalida la firma original del conductor
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
Registro de Tickets (costo USD + galones)
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
  · Registra Parte Diario de Trabajo (número: PT-YYYY-XXXXXX)
    - Odómetro inicio/fin, horómetro inicio/fin
    - Consumo: petróleo, gasolina, aceite, grasa
    - Carretera, sector, labor realizada
    - Reclamos y observaciones
        │
        ▼
Historial de partes accesible en Dashboard
```

---

## 🔧 Rol: Administrador de Transporte (`ADMIN`) y Supervisor

### 1. Gestión de Despachos

| Página | Función |
|--------|---------|
| `RegistroDespacho.aspx` | **Agregar Despacho** — Programación de viajes. Crea lote con: fecha, cliente, número de pedido, tipo de operación (nacional/internacional), planta, documentación (factura, CPIC) y asignación de conductores con tractos y carretas. |
| `ListaDespachos.aspx` | **Listar Despachos** — Vista general de todos los despachos. Permite ver detalles, filtrar y gestionar. Muestra viajes activos y despachos por viaje. |
| `EditarDespacho.aspx` | **Editar Despacho** — Modificar un despacho existente (solo si no ha sido liquidado). |

**Flujo de Despacho:**
1. Admin crea un nuevo despacho (`RegistroDespacho.aspx`)
2. Selecciona cliente, tipo de operación, planta
3. Agrega documentación base (factura, CPIC si es internacional)
4. Asigna uno o más conductores con su tracto y carreta
5. Genera el lote → Se crean los viajes en progreso
6. Los despachos se visualizan en `ListaDespachos.aspx`
7. Desde la lista puede editar o gestionar documentación (facturas, CPIC, guías)

### 2. Órdenes de Viaje y Liquidaciones

| Página | Función |
|--------|---------|
| `LiquidacionesPendientes.aspx` | **Liquidaciones Pendientes** — Lista de liquidaciones firmadas por conductores pendientes de revisión del admin. |
| `DetalleOrdenViaje.aspx` | **Detalle de Orden de Viaje** — Vista completa de una liquidación con todos los datos financieros y estado de firma. |
| `AgregarOrdenViaje.aspx` | **Crear Orden de Viaje** — Generación de la orden a partir del despacho finalizado (camino alternativo del admin). |
| `BuscarOrdenViaje.aspx` | **Búsqueda Avanzada** — Búsqueda de órdenes por múltiples criterios. |
| `FirmarLiquidacion.aspx` | **Firma Digital** — Canvas biométrico del conductor. Previo al envío de la liquidación. |

**Acciones sobre liquidaciones pendientes:**
- ✅ **Aprobar** — Acepta la liquidación. Se registra firma del admin y se guardan ajustes en `OrdenViajeAjuste`. Se genera PDF firmado.
- ❌ **Rechazar** — Devuelve la liquidación al conductor con observaciones.
- ✏️ **Editar** — Permite al admin modificar datos antes de aprobar.
- 👁️ **Ver** — Consulta el detalle completo sin modificar.

### 3. Historial y Reportes

| Página | Sección | Función |
|--------|---------|---------|
| `ReportesOrdenesViaje.aspx` | **Liquidaciones** | Liquidaciones aprobadas con filtros por fecha y factor de conversión (USD → S/). Incluye detalle de descuentos y reintegros. Exportable a Excel y PDF. |
| `ReportesOrdenesViaje.aspx` | **Viajes Activos Sin Liquidación** | Conductores con viajes en progreso que aún no han enviado su liquidación. |
| `Reportes.aspx` | **Reportes Avanzados** | Módulo independiente con filtros por conductor, vehículo, producto, tipo de transacción. Exportación avanzada. Vista de resultados en `ReporteResultado.aspx`. |

### 4. Módulo de Registros (Maestros)

| Página | Función |
|--------|---------|
| `RegistroChoferes.aspx` | Registrar, editar y gestionar **conductores** |
| `RegistroTractos.aspx` | Registrar, editar y gestionar **tractos** (camiones) |
| `RegistroSemiremolques.aspx` | Registrar, editar y gestionar **semiremolques** (carretas) |
| `RegistroClientes.aspx` | Registrar, editar y gestionar **clientes de transporte** |
| `RegistroPeajes.aspx` | Registrar, editar y gestionar **estaciones de peaje** |
| `RegistroPlantas.aspx` | Registrar, editar y gestionar **plantas** (puntos de carga/descarga) |
| `RegistroRutas.aspx` | Registrar, editar y gestionar **rutas de transporte** |
| `RegistroProductos.aspx` | Registrar, editar y gestionar **productos transportados** |

### 5. Otros Módulos del Admin

| Página | Función |
|--------|---------|
| `AgregarCPIC.aspx` / `BuscarCPIC.aspx` | Gestión de documentos CPIC (operaciones internacionales) |
| `AgregarFactura.aspx` / `BuscarFactura.aspx` | Gestión de facturas |
| `AgregarIndicadores.aspx` | Registro de indicadores de tiempo operativo (horas de salida, llegada, carga, descarga) |
| `CargarExcel.aspx` | Carga masiva de datos desde archivo Excel |
| `DescargarPdfOrdenViaje.aspx` | Endpoint que genera y sirve el PDF de una orden de viaje (iTextSharp) |
| `DescargarPdfAbastecimiento.aspx` | Endpoint que genera y sirve el PDF de un abastecimiento |

---

## ⛽ Rol: Administrador de Grifo (`ADMINISTRADOR DE GRIFO`)

### 1. Dashboard (`DashboardGrifo.aspx`)

Pantalla de inicio con visión general de la operación:
- Viajes activos pendientes de abastecimiento
- Acceso rápido a registro y búsqueda

### 2. Registro de Abastecimiento (`AgregarAbastecimiento.aspx`)

| Función | Descripción |
|---------|-------------|
| **Modo Manual** | Selección libre de conductor, placa tracto, carreta, tipo de vehículo, ruta y producto |
| **Modo Viaje** | Datos prellenados desde un viaje activo (conductor, placas, ruta, GL asignados) |
| **Tipo de Registro** | `ABASTECIMIENTO` (rutina), `MANTENIMIENTO` (servicio técnico), `OTRO` (especial) |
| **Tickets** | Tabla dinámica de tickets con costo USD y galones por ticket |
| **Cálculos automáticos** | GL Total = GL Ruta + GL Comprados; GL Consumidos; Rendimiento KM/GL |

**Validación condicional por tipo:**
- **VIAJE PROGRAMADO**: Todos los campos obligatorios
- **ABASTECIMIENTO**: Placa y conductor obligatorios; producto, GL Ruta y tickets opcionales
- **MANTENIMIENTO / OTRO**: Solo fecha, hora y lugar son obligatorios

### 3. Despacho a Obra (`RegistrarDespachoObra.aspx`)

Módulo de despacho de cisterna hacia una obra específica.
- Registra galones de salida y galones de retorno
- `galonesAbastecidos = galones_salida − galones_retorno`
- Persiste en tabla `DespachoCombustibleObra`

### 4. Retorno Ecuador (`RegistrarRetornoEcuador.aspx`)

Registro de ingresos de combustible proveniente de Ecuador.
- Captura múltiples tickets detallados por operación
- Persiste en tablas `IngresoCombustibleEcuador` + `DetalleTicketEcuador`

### 5. Búsqueda y Gestión (`BuscarAbastecimiento.aspx`)

| Acción | Descripción |
|--------|-------------|
| **Buscar** | Búsqueda por número de abastecimiento (exacta y LIKE) |
| **Ver** | Visualización completa: datos del vehículo, combustible, rendimiento, observaciones |
| **Editar** | Modificar GL, montos, producto, ruta, observaciones |
| **Cambiar Tipo** | Cambiar entre ABASTECIMIENTO, MANTENIMIENTO, OTRO (no aplica a VIAJE PROGRAMADO) |
| **Anular** | Marca como `ANULADO` — banner rojo, edición bloqueada, auditoría registrada |
| **Eliminar** | Eliminación permanente con doble confirmación — auditoría registrada |

**Estados del registro:**

| Estado | Editable | Descripción |
|--------|----------|-------------|
| ABASTECIMIENTO | ✅ | Rutina operativa |
| VIAJE PROGRAMADO | ✅ (tipo no cambiable) | Asociado a orden de viaje |
| MANTENIMIENTO | ✅ | Servicio técnico |
| OTRO | ✅ | Uso especial |
| ANULADO | ❌ | Registro anulado, solo eliminar |

### 6. Reportes (`ReporteAbastecimiento.aspx`)

| Función | Descripción |
|---------|-------------|
| **Filtros** | Por rango de fechas, conductor, placa |
| **Vista** | Tabla con todos los abastecimientos registrados |
| **Exportar Excel** | Exportación con detección dinámica de columnas (`tipoAbastecimiento`, `rutaDescripcion`) |

### 7. Registro de Conductores

El Admin de Grifo puede registrar y gestionar conductores desde `RegistroChoferes.aspx`.

---

## 🚛 Rol: Conductor (`CONDUCTOR`)

### Dashboard del Conductor (`DashboardConductor.aspx`)

El conductor accede a un dashboard con **3 pestañas**:

#### Pestaña 1: Mis Viajes
- Viajes activos asignados por el administrador
- Información: número, fecha inicio, cliente, tracto, carreta

#### Pestaña 2: Liquidar
- Formulario de liquidación del viaje pendiente
- **Registra:**
  - **Ingresos**: Montos en S/ y USD
  - **Gastos por concepto** (con comprobante, fecha, observaciones):
    - Peajes (por estación), Alimentación, Apoyo y seguridad, Reparaciones, Movilidad, Encarpada, Hospedaje, Combustible, Gastos financieros, Gastos adicionales
  - **Ingresos adicionales**
  - **Observaciones generales**
- Antes del envío: firma biométrica en `FirmarLiquidacion.aspx`
  - Canvas HTML5 para dibujar el trazo
  - Hash SHA-256 del documento
  - Trazo PNG almacenado en `FirmaDigital` (append-only)
  - `idFirmaConductor` registrado en `OrdenViaje`
- Envía la liquidación para revisión del administrador

#### Pestaña 3: Historial
- Historial completo de liquidaciones (aprobadas, rechazadas, pendientes)

---

## 🔩 Rol: Administrador de Maquinaria (`ADMINISTRADOR DE MAQUINARIA`)

### Módulo de Maestros de Maquinaria

| Página | Función |
|--------|---------|
| `RegistroEquiposMaquinaria.aspx` | Registro y gestión de equipos (topadoras, motoniveladoras, etc.) |
| `RegistroObras.aspx` | Registro y gestión de obras |
| `RegistroClientesObra.aspx` | Registro y gestión de clientes de obra (entidad diferente a clientes de transporte) |
| `RegistroOperadores.aspx` | Registro y gestión de operadores de maquinaria |
| `AsignacionesMaquinaria.aspx` | Vinculación Operador → Equipo → Obra (estado `ACTIVA`) |

### Gestión de Asignaciones

1. Crear asignación vinculando un operador, un equipo y una obra
2. Cada operador puede tener una asignación `ACTIVA` a la vez
3. El operador verá su asignación al ingresar a `DashboardOperador.aspx`

---

## 🏗️ Rol: Operador (`OPERADOR`)

### Dashboard del Operador (`DashboardOperador.aspx`)

- Ve su asignación activa: equipo + obra + cliente
- **Registra Parte Diario de Trabajo** (número: `PT-YYYY-XXXXXX`):
  - Odómetro inicio/fin
  - Horómetro inicio/fin
  - Consumo de petróleo, gasolina, aceite, grasa
  - Carretera y sector de trabajo
  - Labor realizada
  - Reclamos y observaciones
- Historial de partes diarios registrados

---

## 🛡️ Rol: Administrador de Sistema (`ADMINISTRADOR DE SISTEMA`)

### Acceso Completo
Tiene **todos los permisos del Administrador de Transporte** más acceso exclusivo al módulo de auditoría.

### Auditoría del Sistema

| Página | Función |
|--------|---------|
| `Auditoria.aspx` | Log principal de auditoría con estadísticas, filtros y exportación |
| `ConsultaAuditoria.aspx` | Segunda vista de consulta de auditoría (filtros alternativos) |

| Función | Descripción |
|---------|-------------|
| **Log de auditoría** | Registro de todas las acciones del sistema |
| **Filtros** | Por fecha, acción, tabla afectada y usuario |
| **Estadísticas** | Total de registros, del día, usuarios activos (últimos 7 días), tablas afectadas |
| **Exportación** | Exportar log a Excel |

**Acciones auditadas:**

| Acción | Descripción |
|--------|-------------|
| `INSERT` | Creación de registros |
| `UPDATE` | Modificación de registros |
| `DELETE` | Eliminación de registros |
| `ANULAR` | Anulación de registros |
| `LOGIN` | Inicio de sesión exitoso |
| `LOGIN_FALLIDO` | Intento de login fallido |
| `LOGOUT` | Cierre de sesión |
| `APROBAR` | Aprobación de liquidación |
| `RECHAZAR` | Rechazo de liquidación |
| `LIQUIDAR` | Envío de liquidación por conductor |
| `RETIRAR` | Retiro/cancelación de liquidación ya enviada (`sp_DC_RetirarLiquidacion`) |

---

## 🔐 Funcionalidades Comunes (Todos los Roles)

| Función | Ubicación |
|---------|-----------|
| **Login** | `Login.aspx` — Autenticación con anti-fijación de sesión |
| **Cambiar Contraseña** | Modal en `Site.Master` |
| **Recuperar Contraseña** | `RecuperarContrasena.aspx` → `RestablecerContrasena.aspx` |
| **Cerrar Sesión** | Botón en navbar, registrado en auditoría |

---

## ✍️ Flujo de Firma Digital

El sistema implementa un ciclo de firma digital biométrica en las liquidaciones:

```
Conductor llena liquidación en DashboardConductor.aspx
        │
        ▼
FirmarLiquidacion.aspx — Canvas de firma biométrica
  · Trazo dibujado en canvas HTML5
  · Hash SHA-256 del documento generado
  · Imagen PNG del trazo almacenada en tabla FirmaDigital (append-only)
  · idFirmaConductor registrado en OrdenViaje
        │
        ▼
Admin revisa en LiquidacionesPendientes.aspx
  · Al aprobar: firma del admin registrada (Nivel C — solo metadata)
  · idFirmaAdmin registrado en OrdenViaje
  · Ajustes (descuentos/reintegros) guardados en OrdenViajeAjuste con FK a FirmaDigital
  · La firma original del conductor NO se invalida
        │
        ▼
PDF firmado generado y archivado en ~/App_Data/OrdenesViaje
  · rutaPdfFirmado y hashPdfFirmado en OrdenViaje para verificación futura
```

**Tablas involucradas:**

| Tabla | Descripción |
|-------|-------------|
| `FirmaDigital` | Append-only. Imagen PNG del trazo + hash SHA-256 + metadatos |
| `OrdenViajeAjuste` | Descuentos/reintegros formales con FK a `FirmaDigital` |
| `FormatoControlado` | Catálogo ISO 9001/14001/45001/BASC para encabezados de documentos |

**Columnas en `OrdenViaje`:**
`idFirmaConductor`, `idFirmaAdmin`, `rutaPdfFirmado`, `hashPdfFirmado`, `fechaEnvioFirmado`, `fechaAprobacionFirmada`

---

## 📊 Flujo de Datos Financieros (Liquidación)

```
Conductor registra liquidación:
    ├── Ingresos (S/ y USD)
    ├── Gastos por concepto (peajes, alimentación, reparaciones, etc.)
    └── Firma y envía para revisión
            │
            ▼
Admin revisa liquidación pendiente:
    ├── Aprueba →
    │     · Ajustes (Descuentos y Reintegros) guardados en OrdenViajeAjuste
    │     · Descuento: Monto a descontar al conductor
    │     · Reintegro: Monto a devolver al conductor
    │     · PDF firmado archivado en App_Data/
    │     · Queda registrado en Reportes
    └── Rechaza → Vuelve al conductor con observaciones
```

---

## 🗂️ Estructura de Navegación (Navbar)

### Menú Admin / Supervisor
```
├── Despacho
│   ├── Agregar Despacho
│   └── Listar Despachos
├── Orden de Viaje
│   ├── Liquidaciones Pendientes
│   ├── Buscar Orden de Viaje
│   └── Reportes
├── Registro
│   ├── Conductores
│   ├── Tractos
│   ├── Semiremolques
│   ├── Clientes
│   ├── Peajes
│   ├── Plantas
│   ├── Rutas
│   └── Productos
├── Documentos
│   ├── Facturas
│   └── CPIC
├── Herramientas
│   ├── Indicadores
│   ├── Cargar Excel
│   └── Reportes Avanzados
└── Configuración
    ├── Cambiar Contraseña
    └── Auditoría del Sistema (solo Admin Sistema)
```

### Menú Administrador de Grifo
```
├── Inicio (DashboardGrifo)
├── Abastecimiento
│   ├── Registrar Abastecimiento
│   ├── Buscar Abastecimiento
│   ├── Despacho a Obra
│   ├── Retorno Ecuador
│   └── Reporte
├── Registro
│   └── Conductores
└── Configuración
    └── Cambiar Contraseña
```

### Menú Administrador de Maquinaria
```
├── Inicio
├── Maquinaria
│   ├── Equipos
│   ├── Obras
│   ├── Clientes de Obra
│   ├── Operadores
│   └── Asignaciones
└── Configuración
    └── Cambiar Contraseña
```

### Menú Conductor
```
├── Inicio (DashboardConductor)
└── Configuración
    └── Cambiar Contraseña
```

### Menú Operador
```
├── Inicio (DashboardOperador)
└── Configuración
    └── Cambiar Contraseña
```

---

## 🛠️ Stack Tecnológico

| Componente | Tecnología |
|-----------|------------|
| Framework | ASP.NET Web Forms (.NET Framework 4.8) |
| Lenguaje | C# 7.3 |
| Base de datos | SQL Server (somee.com) |
| Acceso a datos | ADO.NET (SqlConnection, SqlCommand, DataTable) |
| Frontend | Bootstrap 4.6, jQuery 3.6, Font Awesome 5, Select2 4.1 |
| Exportación PDF | iTextSharp 5.5.13.4 (**NO** iText 7 — versión AGPL legacy) |
| Exportación Excel | EPPlus 8, ClosedXML |
| Serialización | Newtonsoft.Json |
| Autenticación | Sesión ASP.NET (`SGV_SessionId`, 30 min, anti-fijación) |
| Auditoría | Tabla `AuditoriaLog` auto-creada en `Application_Start` via `AuditoriaHelper.cs` |

---

## 🗄️ Columnas Dinámicas (Migraciones)

El sistema usa detección dinámica de columnas via `ColumnaExisteEnTabla()` (consulta `sys.columns`) para columnas que pueden no existir si la migración aún no se aplicó:

| Tabla | Columna | Script de Migración | Descripción |
|-------|---------|--------------------|-------------|
| `AbastecimientoCombustible` | `tipoAbastecimiento` | `script_AgregarColumnaTipoAbastecimiento.sql` | Tipo: ABASTECIMIENTO, MANTENIMIENTO, OTRO, VIAJE PROGRAMADO, ANULADO |
| `AbastecimientoCombustible` | `rutaDescripcion` | `script_AgregarColumnaRutaDescripcion.sql` | Descripción libre de la ruta del viaje |

---

## ⚠️ Notas de Infraestructura

- **`Application_Error` (`Global.asax.cs`)**: Errores de ViewState redirigen silenciosamente a `Login.aspx?error=sesion`. Otros errores solo se loguean en Debug (no van a `Error.aspx`).
- **Routing**: `RouteConfig.cs` está vacío. No hay routing real — las páginas se acceden por URL física `.aspx`.
- **Páginas de artefacto** (no usar en producción): `TestGenerarPdfOrdenViaje.aspx`, `WebForm2.aspx`.
- **PDFs archivados**: En `~/App_Data/OrdenesViaje` — **no commit** de esta carpeta.
- **Secretos**: `connectionStrings.config` y `appSettings.Secrets.config` están **gitignoreados**. Nunca commitear.

---

*Documento actualizado — Abril 2026. Refleja el estado real del código en `WebSGV/Views/` y `WebSGV/Database/`.*
