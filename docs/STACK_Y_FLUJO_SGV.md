# Sistema SGV — Stack Tecnológico, Roles, Funciones y Flujo de Trabajo

---

## 🏗️ Stack Tecnológico

### Plataforma & Framework

| Capa | Tecnología |
|---|---|
| **Lenguaje** | C# |
| **Framework** | .NET Framework 4.8 |
| **Tipo de aplicación** | ASP.NET Web Forms |
| **Servidor web** | IIS / IIS Express (puerto 44390, HTTPS) |
| **Cultura / Localización** | `es-PE` (Español Perú) |

---

### Paquetes NuGet principales

| Paquete | Versión | Uso |
|---|---|---|
| **iTextSharp** | 5.5.13.4 | Generación de PDFs |
| **BouncyCastle.Cryptography** | 2.4.0 | Criptografía / firma digital |
| **System.Security.Cryptography.Xml** | 8.0.2 | Firmas XML digitales |
| **ClosedXML** | 0.105.0 | Lectura/escritura de archivos Excel (.xlsx) |
| **EPPlus** | 8.1.1 | Exportación/importación Excel |
| **DocumentFormat.OpenXml** | 3.1.1 | Manejo de formato Open XML |
| **AjaxControlToolkit** | 20.1.0 | Controles AJAX para Web Forms |
| **Newtonsoft.Json** | 13.0.3 | Serialización/deserialización JSON |
| **Microsoft.AspNet.Mvc** | 5.2.9 | ASP.NET MVC 5 (coexiste con Web Forms) |
| **Microsoft.AspNet.FriendlyUrls** | 1.0.2 | URLs amigables |
| **Microsoft.AspNet.Web.Optimization** | 1.1.3 | Bundling & minificación de JS/CSS |
| **Microsoft.Net.Compilers** | 4.2.0 | Compilador Roslyn (C# moderno en .NET 4.8) |

---

### Frontend

| Tecnología | Detalle |
|---|---|
| **Bootstrap 5** | Framework CSS (incluye RTL, grid, utilities) |
| **jQuery / AJAX** | Mediante ScriptManager y AjaxControlToolkit |
| **CSS personalizado** | `Site.css`, `responsive-mobile.css` |

---

### Base de Datos

- **Motor:** SQL Server
- **Conexión:** Cadena en `connectionStrings.config` (archivo externo, no versionado en Git)
- **Scripts:** `Database/Schema/` (estructura) y `Database/Scripts/` (migraciones)
- **Acceso a datos:** ADO.NET directo (sin ORM)

---

### Seguridad

- **Firma digital** de documentos (BouncyCastle + Cryptography.Xml)
- **Sesiones** con timeout de 30 min y cookie segura
- **Contraseñas** con hash PBKDF2 — formato `{iter}.{salt}.{hash}` (`PasswordHelper.cs`, `HashHelper.cs`)
- Configuración de secretos en archivos externos (`appSettings.Secrets.config`)

---

## 👥 Roles del Sistema (7 roles)

| Rol | Valor en BD | Pantalla inicial |
|---|---|---|
| **Administrador de Transporte** | `ADMIN` | `Inicio.aspx` |
| **Administrador de Sistema** | `ADMINISTRADOR DE SISTEMA` | `Inicio.aspx` |
| **Conductor / Chofer** | `CONDUCTOR` | `DashboardConductor.aspx` |
| **Administrador de Grifo** | `ADMINISTRADOR DE GRIFO` | `DashboardGrifo.aspx` |
| **Administrador de Maquinaria** | `ADMINISTRADOR DE MAQUINARIA` | `Inicio.aspx` |
| **Operador** | `OPERADOR` | `DashboardOperador.aspx` |
| **Supervisor** | `SUPERVISOR` | `Inicio.aspx` |

> Los roles **no tienen tabla propia** en BD. Se almacenan como texto libre en `Usuarios.rol` y se comparan siempre en mayúsculas (`.ToUpper()`).

---

### Estructura de la tabla `Usuarios`

| Columna | Tipo | Descripción |
|---|---|---|
| `idUsuario` | `INT (PK, AI)` | Identificador único auto-incremental |
| `nombreUsuario` | `VARCHAR` | Login del usuario (único) |
| `nombre` | `VARCHAR` | Nombre completo para mostrar en la interfaz |
| `contrasena` | `VARCHAR` | Hash PBKDF2 en formato `{iter}.{salt}.{hash}` |
| `rol` | `VARCHAR` | Nombre del rol (texto libre, se compara en UPPER) |
| `activo` | `BIT` | 1 = activo, 0 = inactivo |
| `idConductor` | `INT (FK, NULL)` | Solo para rol CONDUCTOR, referencia a tabla `Conductores` |

---

### Datos de Sesión (después del Login)

| Clave en Session | Contenido |
|---|---|
| `Session["UsuarioID"]` | ID del usuario (string) |
| `Session["IdUsuario"]` | ID del usuario (int) |
| `Session["Rol"]` | Rol en texto (ej: `"CONDUCTOR"`) |
| `Session["Nombre"]` | Nombre completo |
| `Session["NombreUsuario"]` | Login |
| `Session["IdConductor"]` | Solo para rol CONDUCTOR |
| `Session["IdOperador"]` | Solo para rol OPERADOR |

---

## 🔐 Matriz de Permisos

| Sección | ADMIN | ADMIN_SISTEMA | CONDUCTOR | ADMIN_GRIFO | ADMIN_MAQUINARIA | OPERADOR | SUPERVISOR |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Despachos | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Facturas / CPIC | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Órdenes de Viaje | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Registros Maestros | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Consultas / Indicadores | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Registro Conductores | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| Abastecimiento | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ✅ |
| Dashboard Grifo | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | ❌ |
| Auditoría | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| Dashboard Conductor / Mis Viajes | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Firma Liquidación | ❌ | ❌ | ✅ | ❌ | ❌ | ❌ | ❌ |
| Dashboard Operador / Parte Diario | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ❌ |

---

## 🚛 ROL: Administrador de Transporte (`ADMIN`)

### Módulo 1 — Gestión de Despachos

| Página | Función |
|---|---|
| `RegistroDespacho.aspx` | Crear despacho: cliente, operación, planta, factura, CPIC (internacional), asignar conductores + tracto + carreta → Genera `ViajesProgreso` |
| `ListaDespachos.aspx` | Ver todos los despachos activos y viajes por despacho |
| `EditarDespacho.aspx` | Editar despacho (solo si no fue liquidado) |

### Módulo 2 — Órdenes de Viaje y Liquidaciones

| Página | Función |
|---|---|
| `LiquidacionesPendientes.aspx` | Ver liquidaciones firmadas por conductores pendientes de revisión |
| `DetalleOrdenViaje.aspx` | Ver detalle completo: ingresos, gastos, peajes, alimentación, reparaciones, estado de firma |
| `AgregarOrdenViaje.aspx` | Crear orden manualmente a partir del despacho |
| `BuscarOrdenViaje.aspx` | Búsqueda avanzada por múltiples criterios |

**Acciones sobre liquidaciones:**
- ✅ **Aprobar** — Registra firma admin + ajustes en `OrdenViajeAjuste` + genera PDF firmado archivado en `~/App_Data/OrdenesViaje`
- ❌ **Rechazar** — Devuelve al conductor con observaciones
- ✏️ **Editar** — Modifica datos antes de aprobar
- 👁️ **Ver** — Consulta sin modificar

### Módulo 3 — Reportes y Consultas

| Página | Función |
|---|---|
| `ReportesOrdenesViaje.aspx` | Liquidaciones aprobadas con filtros por fecha y factor USD→S/. Descuentos y reintegros. Exportable a Excel y PDF. |
| `Reportes.aspx` + `ReporteResultado.aspx` | Filtros avanzados por conductor, vehículo, producto, tipo de transacción |
| `AgregarIndicadores.aspx` | Indicadores de tiempo operativo (horas salida, llegada, carga, descarga) |

### Módulo 4 — Maestros / Registros

| Página | Función |
|---|---|
| `RegistroChoferes.aspx` | Conductores |
| `RegistroTractos.aspx` | Tractos (camiones) |
| `RegistroSemiremolques.aspx` | Semiremolques (carretas) |
| `RegistroClientes.aspx` | Clientes de transporte |
| `RegistroPeajes.aspx` | Estaciones de peaje |
| `RegistroPlantas.aspx` | Plantas (puntos de carga/descarga) |
| `RegistroRutas.aspx` | Rutas de transporte |
| `RegistroProductos.aspx` | Productos transportados |

### Módulo 5 — Documentación Internacional

| Página | Función |
|---|---|
| `AgregarCPIC.aspx` / `BuscarCPIC.aspx` | Documentos CPIC para operaciones internacionales |
| `AgregarFactura.aspx` / `BuscarFactura.aspx` | Gestión de facturas |

---

## 🚗 ROL: Conductor (`CONDUCTOR`)

### Dashboard del Conductor — `DashboardConductor.aspx`

Al iniciar sesión el conductor ve sus viajes asignados en progreso y puede:

- Ver detalle de su viaje activo
- Registrar ingresos y gastos del viaje:
  - Peajes (por estación, fecha, comprobante, S/ y $)
  - Alimentación
  - Apoyo / Seguridad
  - Reparaciones
  - Movilidad
  - Encarpada
  - Hospedaje
  - Otros gastos
- Registrar ingresos adicionales
- Ver historial de viajes anteriores

### Liquidación y Firma Digital — `FirmarLiquidacion.aspx`

- Canvas biométrico donde el conductor dibuja su firma digital
- El trazo PNG se almacena en tabla `FirmaDigital` (append-only, hash SHA-256)
- Se registra `idFirmaConductor` en `OrdenViaje`
- Liquidación queda en estado **"Pendiente de revisión"**

### Restricciones del Conductor

| | |
|---|---|
| ❌ | No puede ver despachos de otros conductores |
| ❌ | No puede crear ni editar maestros (clientes, rutas, etc.) |
| ❌ | No puede aprobar liquidaciones |
| ❌ | No puede ver reportes globales |
| ✅ | Solo ve sus propios viajes (filtrado por `Session["IdConductor"]`) |

---

## ⛽ ROL: Administrador de Grifo (`ADMINISTRADOR DE GRIFO`)

### Dashboard del Grifo — `DashboardGrifo.aspx`

- Lista viajes activos pendientes de abastecimiento
- **[Abastecer]** → `AgregarAbastecimiento.aspx` (Modo Viaje, datos prellenados)
- **[Agregar manual]** → `AgregarAbastecimiento.aspx` (Modo Manual, dropdowns)

### Flujo de Abastecimiento

```
DashboardGrifo.aspx — lista viajes activos
        │
        ├── [Abastecer] → AgregarAbastecimiento.aspx
        │     · Registro de tickets (costo USD + galones)
        │     · Cálculos: GL Total, GL Consumidos, Rendimiento KM/GL
        │     · Número correlativo generado al guardar
        │
        ├── [Despacho a Obra] → RegistrarDespachoObra.aspx
        │     · Cisterna lleva combustible a una obra
        │     · Registra galones salida / retorno
        │     · galonesAbastecidos = salida - retorno
        │
        ├── [Retorno Ecuador] → RegistrarRetornoEcuador.aspx
        │     · Ingreso de combustible desde Ecuador con tickets detallados
        │
        └── Gestión posterior (BuscarAbastecimiento.aspx):
              ├── Editar datos (GL, montos, producto, ruta, tipo)
              ├── Cambiar Tipo (ABASTECIMIENTO ↔ MANTENIMIENTO ↔ OTRO)
              ├── Anular → Marca como ANULADO (no editable)
              └── Eliminar → Eliminación permanente con confirmación
```

---

## ⚙️ ROL: Administrador de Maquinaria (`ADMINISTRADOR DE MAQUINARIA`)

### Configuración de Maestros

- `RegistroEquiposMaquinaria.aspx` — Topadoras, motoniveladoras, etc.
- `RegistroObras.aspx` — Obras
- `RegistroClientesObra.aspx` — Clientes de obra
- `RegistroOperadores.aspx` — Operadores de maquinaria

### Asignaciones

- `AsignacionesMaquinaria.aspx` — Vincula Operador + Equipo + Obra (estado ACTIVA)

---

## 👷 ROL: Operador (`OPERADOR`)

### Dashboard del Operador — `DashboardOperador.aspx`

- Ve su asignación activa (equipo + obra + cliente)
- Registra **Parte Diario de Trabajo** (número: `PT-YYYY-XXXXXX`):
  - Odómetro inicio/fin
  - Horómetro inicio/fin
  - Consumo: petróleo, gasolina, aceite, grasa
  - Carretera, sector, labor realizada
  - Reclamos y observaciones
- Historial de partes diarios

---

## 🔄 Flujo de Trabajo Completo — Transporte (Admin ↔ Conductor)

```
[ADMIN] RegistroDespacho.aspx
    │  Crea despacho: cliente, planta, operación, factura, CPIC
    │  Asigna: conductor + tracto + carreta
    │  → Genera registros en ViajesProgreso
    ▼
[CONDUCTOR] DashboardConductor.aspx
    │  Ve su viaje asignado
    │  Registra gastos e ingresos durante el viaje
    │  (Peajes, alimentación, reparaciones, etc.)
    ▼
[CONDUCTOR] FirmarLiquidacion.aspx
    │  Firma digitalmente con canvas biométrico
    │  → PNG del trazo → tabla FirmaDigital (hash SHA-256)
    │  → Liquidación pasa a estado "Pendiente"
    ▼
[ADMIN] LiquidacionesPendientes.aspx
    │  Revisa la liquidación firmada
    │  Consulta detalle: DetalleOrdenViaje.aspx
    │
    ├── ✅ APROBAR
    │     · Firma del admin registrada (solo metadata, Nivel C)
    │     · Ajustes guardados en OrdenViajeAjuste
    │     · NO invalida firma original del conductor
    │     · PDF firmado generado → archivado en ~/App_Data/OrdenesViaje
    │     → Pasa a Historial / Reportes
    │
    ├── ❌ RECHAZAR
    │     → Regresa al conductor con observaciones
    │     → Conductor debe corregir y volver a firmar
    │
    └── ✏️ EDITAR → Modifica datos antes de aprobar
    ▼
[ADMIN] ReportesOrdenesViaje.aspx
    │  Consulta historial de liquidaciones aprobadas
    │  Filtros: fecha, conductor, factor USD→S/
    │  Descuentos y reintegros
    └── Exportación a Excel / PDF
```

---

## 🔧 Contexto del Negocio

**Empresa:** Servicios Generales Viviana E.I.R.L.
**RUC:** 20483851171
**Rubro:** Transporte y Construcción
**Domicilio Fiscal:** Jr. Cañete Nro. 416 Dpto. 100 — Cercado de Lima, Lima
**Web:** www.serviciosgviviana.somee.com
