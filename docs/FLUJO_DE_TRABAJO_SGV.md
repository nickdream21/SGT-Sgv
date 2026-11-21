# Sistema SGV — Flujo de Trabajo y Roles

## 📋 Descripción General

**SGV (Sistema de Gestión de Viajes)** es una aplicación web ASP.NET Web Forms (.NET Framework 4.8) orientada a la gestión de transporte de carga. Administra el ciclo completo desde la programación de despachos, seguimiento de viajes, liquidación de gastos firmada digitalmente por conductores, hasta la aprobación administrativa con ajustes auditados.

Las reglas de negocio acordadas con el usuario (qué es "aprobada", qué se bloquea, reglas de CPI/MCI…) están en `docs/DECISIONES_NEGOCIO.md`.

---

## 👥 Roles del Sistema

El sistema maneja **5 roles** (6 constantes `ROL_*`: `ADMIN` y `ADMINISTRADOR DE TRANSPORTE` tienen los mismos permisos) definidos en `WebSGV/Views/RolesHelper.cs`; los alias `ADMINISTRADOR` y `CHOFER` se aceptan como `ADMIN` y `CONDUCTOR`:

| Rol | Constante | Valor en BD | Redirección Login | Descripción |
|-----|-----------|-------------|-------------------|-------------|
| **Administrador de Transporte** | `ROL_ADMIN_TRANSPORTE` (y `ROL_ADMIN` para `ADMIN` / `ADMINISTRADOR`) | `ADMINISTRADOR DE TRANSPORTE` (también `ADMIN`, `ADMINISTRADOR`) | `Inicio.aspx` | Gestión operativa completa: despachos, viajes, liquidaciones, registros y reportes |
| **Administrador de Sistema** | `ROL_ADMIN_SISTEMA` | `ADMINISTRADOR DE SISTEMA` | `DashboardAdminSistema.aspx` | Gestión de usuarios y auditoría; no opera despachos, liquidaciones ni reportes |
| **Conductor** | `ROL_CONDUCTOR` | `CONDUCTOR` (también `CHOFER`) | `DashboardConductor.aspx` | Liquidación firmada de viajes asignados y consulta de historial |
| **Contabilidad** | `ROL_CONTABILIDAD` | `CONTABILIDAD` | `LiquidacionesAprobadasContabilidad.aspx` | Consulta de liquidaciones aprobadas y sus PDF (menú propio: Liquidaciones Aprobadas) |
| **Supervisor** | `ROL_SUPERVISOR` | `SUPERVISOR` | `Inicio.aspx` | Permisos similares a ADMIN en despachos, órdenes de viaje y registros |

> La página de inicio de cada rol sale de `RolesHelper.UrlInicioSegunRol` (login, redirecciones y logo del menú).

> Los roles se almacenan como texto en la columna `rol` de la tabla `Usuarios`. No existe tabla de roles separada. Las comparaciones siempre se hacen en mayúsculas (`.ToUpper()`).

---

## 🔑 Matriz de Permisos por Sección

ADMIN\* = `ADMIN`, `ADMINISTRADOR` y `ADMINISTRADOR DE TRANSPORTE` (`RolesHelper.EsRolAdmin`); CONDUCTOR incluye `CHOFER`. Los roles retirados (`ADMINISTRADOR DE GRIFO`, `ADMINISTRADOR DE MAQUINARIA`, `OPERADOR`) no tienen ningún permiso.

| Sección (`TienePermiso(seccion, rol)`) | ADMIN\* | SUPERVISOR | CONDUCTOR | CONTABILIDAD | ADMIN_SISTEMA |
|----------------------------------------|:------:|:----------:|:---------:|:------------:|:-------------:|
| `DESPACHO` / `FACTURA` / `CPIC` / `ORDEN_VIAJE` / `REGISTRO` / `CONSULTAS` / `INDICADORES` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `REGISTRO_CONDUCTORES` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `AUDITORIA` | ❌ | ❌ | ❌ | ❌ | ✅ |
| `SEGUIMIENTO_EXPORTACION` / `DASHBOARD_EXPORTACION` | ✅ | ✅ | ❌ | ❌ | ❌ |
| `DASHBOARD_CONDUCTOR` / `MIS_VIAJES` / `MI_PERFIL` | ✅ | ❌ | ✅ | ❌ | ❌ |
| `LIQUIDACIONES_CONTABILIDAD` | ✅ | ✅ | ❌ | ✅ | ❌ |
| Cualquier otra sección | ❌ | ❌ | ❌ | ❌ | ❌ |

> Matriz verificada por `RolesHelperTests` (tabla literal; además comprueba que toda sección usada en el código existe en ella).

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
        │     · Ajustes (Descuentos / Reintegros) guardados en DescuentosReintegros
        │     · No invalida la firma original del conductor
        │     · PDF firmado generado y archivado en ~/App_Data/OrdenesViaje
        │     → Pasa a Historial / Reportes
        └── Si Rechaza → Regresa al Conductor para corrección
```

---

## 🔧 Rol: Administrador de Transporte (`ADMIN`) y Supervisor

### 1. Gestión de Despachos

| Página | Función |
|--------|---------|
| `RegistroDespacho.aspx` | **Agregar Despacho** — Programación de viajes. Crea lote con: fecha, cliente, número de pedido, tipo de operación (nacional/internacional), planta, documentación (factura, CPIC) y asignación de conductores con tractos y carretas. |
| `ListaDespachos.aspx` | **Listar Despachos** — Vista general de todos los despachos. Permite ver detalles, filtrar y gestionar. Muestra viajes activos y despachos por viaje. |

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
| `FirmarLiquidacion.aspx` | **Firma Digital** — Canvas biométrico del conductor. Previo al envío de la liquidación. |

**Acciones sobre liquidaciones pendientes:**
- ✅ **Aprobar** — Acepta la liquidación. Se registra firma del admin y se guardan los ajustes en `DescuentosReintegros`. Se genera PDF firmado.
- ❌ **Rechazar** — Devuelve la liquidación al conductor con observaciones.
- ✏️ **Editar** — Permite al admin modificar datos antes de aprobar.
- 👁️ **Ver** — Consulta el detalle completo sin modificar.

### 3. Historial y Reportes

| Página | Sección | Función |
|--------|---------|---------|
| `ReportesOrdenesViaje.aspx` | **Liquidaciones** | Liquidaciones aprobadas con filtros por fecha y factor de conversión (USD → S/). Incluye detalle de descuentos y reintegros. Exportable a Excel y PDF. |
| `ReportesOrdenesViaje.aspx` | **Viajes Activos Sin Liquidación** | Conductores con viajes en progreso que aún no han enviado su liquidación. |

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
| `CargarExcel.aspx` | Carga masiva de datos desde archivo Excel |
| `DescargarPdfOrdenViaje.aspx` | Endpoint que sirve el PDF de una orden de viaje (QuestPDF; un PDF firmado se sirve archivado, nunca se regenera) |

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

## 🛡️ Rol: Administrador de Sistema (`ADMINISTRADOR DE SISTEMA`)

### Acceso Completo
Tiene **todos los permisos del Administrador de Transporte** más acceso exclusivo al módulo de auditoría.

### Auditoría del Sistema

| Página | Función |
|--------|---------|
| `Auditoria.aspx` | Log principal de auditoría con estadísticas, filtros y exportación |

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
  · Ajustes (descuentos/reintegros) guardados en DescuentosReintegros (una fila por orden)
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
| `DescuentosReintegros` | Descuentos/reintegros de la aprobación (una fila por orden). `OrdenViajeAjuste` existe (migración 03) pero hoy no se usa |
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
    │     · Ajustes (Descuentos y Reintegros) guardados en DescuentosReintegros
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
├── Herramientas
│   ├── Cargar Excel
│   └── Reportes Avanzados
└── Configuración
    ├── Cambiar Contraseña
    └── Auditoría del Sistema (solo Admin Sistema)
```

### Menú Conductor
```
├── Inicio (DashboardConductor)
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
| Exportación PDF | QuestPDF 2026.5.0 (licencia Community; PDF/A-2B en las órdenes firmadas). iTextSharp se retiró |
| Exportación Excel | ClosedXML 0.105 (`ReporteTabular` para los reportes tabulares). EPPlus se retiró por licencia |
| Serialización | Newtonsoft.Json |
| Autenticación | Sesión ASP.NET (`SGV_SessionId`, 30 min, anti-fijación) |
| Auditoría | Tabla `AuditoriaLog` auto-creada en `Application_Start` via `AuditoriaHelper.cs` |

---

## ⚠️ Notas de Infraestructura

- **`Application_Error` (`Global.asax.cs`)**: todo error se registra una vez con `LogSGV` (`App_Data/logs`). ViewState inválido o vencido → advertencia y redirección a `Login.aspx?error=sesion`; 404 → info; el resto → error con ruta y usuario. En Release (`customErrors` RemoteOnly) el usuario remoto va a `Error.aspx` con un mensaje genérico; en desarrollo se ve la pantalla de error de ASP.NET.
- **Routing**: `RouteConfig.cs` está vacío. No hay routing real — las páginas se acceden por URL física `.aspx`.
- **PDFs archivados**: En `~/App_Data/OrdenesViaje` — **no commit** de esta carpeta.
- **Secretos**: `connectionStrings.config` y `appSettings.Secrets.config` están **gitignoreados**. Nunca commitear.

---

*Documento actualizado — Abril 2026. Refleja el estado real del código en `WebSGV/Views/` y `WebSGV/Database/`.*
