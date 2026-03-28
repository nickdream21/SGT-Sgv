# Sistema SGV — Flujo de Trabajo y Roles

## 📋 Descripción General

**SGV (Sistema de Gestión de Viajes)** es una aplicación web ASP.NET Web Forms (.NET Framework 4.8) orientada a la gestión de transporte de carga. Administra el ciclo completo desde la programación de despachos, seguimiento de viajes, liquidación de gastos por parte de conductores, hasta la aprobación y reportes financieros.

---

## 👥 Roles del Sistema

El sistema maneja **4 roles** definidos en `RolesHelper.cs`:

| Rol | Constante | Descripción |
|-----|-----------|-------------|
| **Administrador de Transporte** | `ADMIN` / `ADMINISTRADOR` | Gestión operativa completa: despachos, viajes, liquidaciones, registros y reportes |
| **Administrador de Grifo** | `ADMINISTRADOR DE GRIFO` | Gestión de abastecimiento de combustible: registro, búsqueda, reportes, anulación/eliminación y registro de conductores |
| **Conductor** | `CONDUCTOR` / `CHOFER` | Liquidación de viajes asignados y consulta de historial |
| **Administrador de Sistema** | `ADMINISTRADOR DE SISTEMA` | Acceso total (incluye todo lo del Admin de Transporte) + Auditoría del sistema |

---

## 🔄 Flujo de Trabajo General

### Flujo de Transporte (Admin de Transporte)
```
Programación de Despacho (Admin)
        │
        ▼
Asignación de Conductor, Tracto, Carreta
        │
        ▼
Viaje en Progreso (el conductor ve el viaje en su Dashboard)
        │
        ▼
Conductor Liquida el Viaje (registra ingresos y gastos)
        │
        ▼
Liquidación Pendiente de Revisión (Admin)
        │
        ▼
Admin Revisa: Aprobar / Rechazar / Editar
        │
        ├── Si Aprueba → Se generan Descuentos y Reintegros → Pasa a Historial/Reportes
        └── Si Rechaza → Regresa al Conductor para corrección
```

### Flujo de Abastecimiento (Admin de Grifo)
```
Registro de Abastecimiento (Admin Grifo)
        │
        ├── Modo Manual: Selección de conductor, placa, carreta, ruta, producto
        └── Modo Viaje: Datos prellenados desde orden de viaje activa
        │
        ▼
Registro de Tickets de Combustible (costo USD + galones)
        │
        ▼
Cálculos Automáticos: GL totales, GL consumidos, rendimiento KM/GL
        │
        ▼
Guardar Abastecimiento → Número correlativo generado
        │
        ▼
Gestión Posterior (BuscarAbastecimiento.aspx):
        ├── ✏️ Editar datos (GL, montos, producto, ruta, tipo)
        ├── 🔄 Cambiar Tipo (Abastecimiento ↔ Mantenimiento ↔ Otro)
        ├── 🚫 Anular → Marca como ANULADO (no editable)
        └── 🗑️ Eliminar → Eliminación permanente con confirmación
```

---

## 🔧 Rol: Administrador de Transporte (`ADMIN`)

### 1. Gestión de Despachos

| Página | Función |
|--------|---------|
| `RegistroDespacho.aspx` | **Agregar Despacho** — Programación de viajes. Se crea un lote de despachos con: fecha de programación, cliente, número de pedido, tipo de operación (nacional/internacional), planta, documentación (factura, CPIC) y asignación de conductores con sus tractos y carretas. |
| `ListaDespachos.aspx` | **Listar Despachos** — Vista general de todos los despachos creados. Permite ver detalles, filtrar y gestionar los despachos existentes. Muestra viajes activos y despachos por viaje. |
| `EditarDespacho.aspx` | **Editar Despacho** — Modificar datos de un despacho existente (solo si es editable/no ha sido liquidado). |

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
| `LiquidacionesPendientes.aspx` | **Liquidaciones Pendientes** — Lista de liquidaciones que los conductores han completado y enviado pero que aún no han sido revisadas por el administrador. |
| `DetalleOrdenViaje.aspx` | **Detalle de Orden de Viaje** — Vista completa de una liquidación con todos los datos financieros. |
| `AgregarOrdenViaje.aspx` | **Crear Orden de Viaje** — Generación de la orden de viaje a partir de los datos del despacho finalizado. |

**Acciones sobre liquidaciones pendientes:**
- ✅ **Aprobar** — Acepta la liquidación; en este momento se calculan y registran los **descuentos** y **reintegros** correspondientes al conductor.
- ❌ **Rechazar** — Devuelve la liquidación al conductor con observaciones para corrección.
- ✏️ **Editar** — Permite al admin modificar datos de la liquidación antes de aprobar.
- 👁️ **Ver** — Consulta el detalle completo de la liquidación sin modificar.

### 3. Historial de Liquidaciones Aprobadas

Desde `ReportesOrdenesViaje.aspx` (pestaña Liquidaciones), el administrador puede consultar el historial de todas las liquidaciones que ha aprobado, funcionando como un registro/control de las operaciones finalizadas.

### 4. Reportes

| Página | Sección | Función |
|--------|---------|---------|
| `ReportesOrdenesViaje.aspx` | **Liquidaciones** | Reporte de liquidaciones aprobadas con filtros por fecha y factor de conversión (dólar a soles). Incluye detalle de **descuentos** y **reintegros** generados en la aprobación. Exportable a Excel y PDF. |
| `ReportesOrdenesViaje.aspx` | **Viajes Activos Sin Liquidación** | Muestra qué conductores tienen viajes en progreso que aún **no han enviado su liquidación**. Permite dar seguimiento a viajes pendientes. |

### 5. Módulo de Registros (Maestros)

| Página | Función |
|--------|---------|
| `RegistroChoferes.aspx` | Registrar, editar y gestionar **conductores** |
| `RegistroTractos.aspx` | Registrar, editar y gestionar **tractos** (camiones) |
| `RegistroSemiremolques.aspx` | Registrar, editar y gestionar **semiremolques** (carretas) |
| `RegistroClientes.aspx` | Registrar, editar y gestionar **clientes** |
| `RegistroPeajes.aspx` | Registrar, editar y gestionar **peajes** (estaciones de peaje en rutas) |
| `RegistroPlantas.aspx` | Registrar, editar y gestionar **plantas** (puntos de carga/descarga) |

### 6. Otros Módulos del Admin

| Página | Función |
|--------|---------|
| `AgregarCPIC.aspx` | Gestión de documentos CPIC (operaciones internacionales) |
| `BuscarOrdenViaje.aspx` | Búsqueda de órdenes de viaje por múltiples criterios |
| `AgregarIndicadores.aspx` | Registro de indicadores de tiempo en operaciones (horas de salida, llegada, carga, descarga, etc.) |
| `RegistroRutas.aspx` | Gestión de rutas de transporte |
| `RegistroProductos.aspx` | Gestión de productos transportados |

---

## ⛽ Rol: Administrador de Grifo (`ADMINISTRADOR DE GRIFO`)

El Administrador de Grifo es responsable de la gestión integral del abastecimiento de combustible para la flota de vehículos.

### 1. Dashboard (`DashboardGrifo.aspx`)

Pantalla de inicio con visión general de la operación de abastecimiento:
- Viajes activos pendientes de abastecimiento
- Acceso rápido a registro y búsqueda de abastecimientos

### 2. Registro de Abastecimiento (`AgregarAbastecimiento.aspx`)

| Función | Descripción |
|---------|-------------|
| **Modo Manual** | Selección libre de conductor, placa tracto, carreta, tipo de vehículo, ruta y producto |
| **Modo Viaje** | Datos prellenados desde un viaje activo (conductor, placas, ruta, GL asignados) |
| **Tipo de Registro** | `ABASTECIMIENTO` (rutina), `MANTENIMIENTO` (servicio técnico), `OTRO` (especial) |
| **Tickets** | Tabla dinámica de tickets con costo USD y galones por ticket |
| **Cálculos automáticos** | GL Total = GL Ruta + GL Comprados, GL Consumidos, Rendimiento KM/GL |
| **Sincronización** | GL Comprados y Monto Total se sincronizan automáticamente desde tickets |

**Validación condicional por tipo:**
- **VIAJE PROGRAMADO**: Todos los campos obligatorios
- **ABASTECIMIENTO**: Placa y conductor obligatorios; producto, GL Ruta y tickets opcionales
- **MANTENIMIENTO / OTRO**: Solo fecha, hora y lugar son obligatorios; detalle en observaciones

### 3. Búsqueda y Gestión (`BuscarAbastecimiento.aspx`)

| Acción | Descripción |
|--------|-------------|
| **Buscar** | Búsqueda por número de abastecimiento (exacta y LIKE) |
| **Ver** | Visualización completa: datos del vehículo, combustible, rendimiento, observaciones |
| **Editar** | Modo edición: modificar GL, montos, producto, ruta, observaciones |
| **Cambiar Tipo** | Dropdown para cambiar entre ABASTECIMIENTO, MANTENIMIENTO, OTRO (no aplica a VIAJE PROGRAMADO) |
| **Anular** | Marca el registro como `ANULADO` — banner rojo, edición bloqueada, auditoría registrada |
| **Eliminar** | Eliminación permanente con doble confirmación — auditoría registrada |

**Estados del registro:**
| Estado | Badge | Editable | Descripción |
|--------|-------|----------|-------------|
| ABASTECIMIENTO | 🟢 Verde | ✅ | Rutina operativa |
| VIAJE PROGRAMADO | 🔵 Azul | ✅ (tipo no cambiable) | Asociado a orden de viaje |
| MANTENIMIENTO | 🟠 Naranja | ✅ | Servicio técnico |
| OTRO | ⚫ Gris | ✅ | Uso especial |
| ANULADO | 🔴 Rojo | ❌ | Registro anulado, solo eliminar |

### 4. Reportes (`ReporteAbastecimiento.aspx`)

| Función | Descripción |
|---------|-------------|
| **Filtros** | Por rango de fechas, conductor, placa |
| **Vista** | Tabla con todos los abastecimientos registrados |
| **Exportar Excel** | Exportación con detección dinámica de columnas (tipoAbastecimiento, rutaDescripcion) |

### 5. Registro de Conductores

El Admin de Grifo también puede registrar y gestionar conductores desde `RegistroChoferes.aspx`, permitiéndole mantener actualizada la base de datos de personal sin depender del Admin de Transporte.

### Permisos (`RolesHelper.cs`)

| Sección | Acceso |
|---------|--------|
| `ABASTECIMIENTO` | ✅ |
| `DASHBOARD_GRIFO` | ✅ |
| `REGISTRO_CONDUCTORES` | ✅ |
| `DESPACHO`, `ORDEN_VIAJE`, `REGISTRO` (completo) | ❌ |
| `AUDITORIA` | ❌ |

---

## 🚛 Rol: Conductor (`CONDUCTOR`)

### Dashboard del Conductor (`DashboardConductor.aspx`)

El conductor accede a un dashboard con **3 pestañas**:

#### Pestaña 1: Mis Viajes
- Muestra los viajes activos que le han sido asignados por el administrador.
- Ve información del viaje: número, fecha de inicio, cliente, tracto, carreta.

#### Pestaña 2: Liquidar
- Si tiene un viaje asignado pendiente de liquidación, puede completar el formulario de liquidación.
- **¿Qué registra el conductor al liquidar?**
  - **Ingresos**: Montos recibidos en soles (S/) y dólares (USD).
  - **Gastos por concepto**, cada uno con detalle de comprobante, fecha y observaciones:
    - Peajes (por estación)
    - Alimentación
    - Apoyo y seguridad
    - Reparaciones
    - Movilidad
    - Encarpada
    - Hospedaje
    - Combustible
    - Gastos financieros
    - Gastos adicionales
  - **Ingresos adicionales**
  - **Observaciones generales**
- Una vez completado, envía la liquidación para revisión del administrador.

#### Pestaña 3: Historial
- Muestra el historial completo de todas las liquidaciones que el conductor ha realizado.
- Incluye liquidaciones aprobadas, rechazadas y pendientes.

---

## 🛡️ Rol: Administrador de Sistema (`ADMINISTRADOR DE SISTEMA`)

### Acceso Completo
Tiene **todos los permisos del Administrador de Transporte** más acceso exclusivo al módulo de auditoría.

### Auditoría del Sistema (`Auditoria.aspx`)

| Función | Descripción |
|---------|-------------|
| **Log de auditoría** | Registro de todas las acciones realizadas en el sistema (inserciones, actualizaciones, eliminaciones, logins, aprobaciones, rechazos, liquidaciones, etc.) |
| **Filtros** | Por fecha, acción, tabla afectada y usuario |
| **Estadísticas** | Total de registros, registros del día, usuarios activos (últimos 7 días), tablas afectadas |
| **Exportación** | Exportar log de auditoría a Excel |

**Acciones auditadas:**
- `INSERT` — Creación de registros
- `UPDATE` — Modificación de registros
- `DELETE` — Eliminación de registros
- `ANULAR` — Anulación de registros (ej: abastecimiento)
- `LOGIN` — Inicio de sesión exitoso
- `LOGIN_FALLIDO` — Intento de login fallido
- `LOGOUT` — Cierre de sesión
- `APROBAR` — Aprobación de liquidación
- `RECHAZAR` — Rechazo de liquidación
- `LIQUIDAR` — Envío de liquidación por conductor
- `RETIRAR` — Retiro de operación

---

## 🔐 Funcionalidades Comunes (Todos los Roles)

| Función | Ubicación |
|---------|-----------|
| **Login** | `Login.aspx` — Autenticación con protección anti-fijación de sesión |
| **Cambiar Contraseña** | Modal en `Site.Master` — Disponible para todos los roles |
| **Recuperar Contraseña** | `RecuperarContrasena.aspx` → `RestablecerContrasena.aspx` |
| **Cerrar Sesión** | Botón en navbar, registrado en auditoría |

---

## 🗂️ Estructura de Navegación (Navbar)

### Menú Admin / Admin Sistema
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
│   └── Plantas
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
│   └── Reporte
├── Registro
│   └── Conductores
└── Configuración
    └── Cambiar Contraseña
```

### Menú Conductor
```
├── Inicio (Dashboard)
└── Configuración
    └── Cambiar Contraseña
```

---

## 📊 Flujo de Datos Financieros (Liquidación)

```
Conductor registra liquidación:
    ├── Ingresos (S/ y USD)
    ├── Gastos por concepto (peajes, alimentación, reparaciones, etc.)
    └── Envía para revisión
            │
            ▼
Admin revisa liquidación pendiente:
    ├── Aprueba → Se calculan Descuentos y Reintegros
    │               ├── Descuento: Monto que se descuenta al conductor
    │               └── Reintegro: Monto que se devuelve al conductor
    │               └── Queda registrado en Reportes
    └── Rechaza → Vuelve al conductor con observaciones
```

---

## 🛠️ Stack Tecnológico

| Componente | Tecnología |
|-----------|------------|
| Framework | ASP.NET Web Forms (.NET Framework 4.8) |
| Lenguaje | C# 7.3 |
| Base de datos | SQL Server |
| ORM/Acceso a datos | ADO.NET (SqlConnection, SqlCommand, DataTable) |
| Frontend | Bootstrap 4.6, jQuery 3.6, Font Awesome 5, Select2 4.1 |
| Exportación | ClosedXML (Excel), iTextSharp (PDF), EPPlus |
| Serialización | Newtonsoft.Json |
| Autenticación | Sesión ASP.NET con cookie temporal + anti-fijación de sesión |
| Auditoría | Tabla `AuditoriaLog` con helper `AuditoriaHelper.cs` |

---

## 🗄️ Columnas Dinámicas (Migraciones)

El sistema utiliza detección dinámica de columnas mediante `ColumnaExisteEnTabla()` (consulta `sys.columns`) para soportar columnas que pueden no existir si la migración SQL aún no se ha ejecutado:

| Tabla | Columna | Script de Migración | Descripción |
|-------|---------|--------------------|--------------|
| `AbastecimientoCombustible` | `tipoAbastecimiento` | `script_AgregarColumnaTipoAbastecimiento.sql` | Tipo: ABASTECIMIENTO, MANTENIMIENTO, OTRO, VIAJE PROGRAMADO, ANULADO |
| `AbastecimientoCombustible` | `rutaDescripcion` | `script_AgregarColumnaRutaDescripcion.sql` | Descripción libre de la ruta del viaje |

---

*Documento actualizado como referencia del flujo de trabajo actual del sistema SGV.*
