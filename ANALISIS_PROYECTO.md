# 📋 Análisis Completo del Proyecto — Sistema de Gestión de Transporte (SGT-SGV)

> **Generado por:** Arquitecto de Software Senior (IA)  
> **Fecha:** 22 de mayo de 2026  
> **Versión del análisis:** 1.0  

---

## 1. 🧭 Resumen Ejecutivo

**SGT-SGV** (Sistema de Gestión de Transporte — Servicios Generales Viviana) es una aplicación web empresarial desarrollada en **ASP.NET Web Forms / .NET Framework 4.8** que gestiona integralmente las operaciones de una empresa peruana de transporte y logística: **SERVICIOS GENERALES VIVIANA E.I.R.L.** (RUC 20483851171). El sistema cubre los flujos de despacho de vehículos, liquidación y firma digital de órdenes de viaje por conductores, abastecimiento de combustible en grifo propio, gestión de maquinaria pesada en obras, y seguimiento de exportaciones. Toda la lógica crítica está respaldada por un robusto sistema de trazabilidad (auditoría completa, firmas digitales con biometría de trazo y hash SHA-256) que garantiza el no-repudio de documentos regulatorios y el cumplimiento de normas ISO 9001/14001/45001 y BASC.

---

## 2. 🛠️ Tecnologías y Stack

### Frontend

| Tecnología | Versión | Uso |
|---|---|---|
| **ASP.NET Web Forms** | .NET 4.8 | Motor de páginas y servidor de controles |
| **Bootstrap** | 5.x | Grid, componentes, responsive |
| **jQuery** | 3.7.0 | AJAX, manipulación DOM, interactividad |
| **AjaxControlToolkit** | 20.1.0 | Controles AJAX para Web Forms |
| **System.Web.DataVisualization** | Framework 4.8 | Gráficas de barras / líneas en dashboards |
| CSS personalizado | — | `Site.css`, `responsive-mobile.css` |

### Backend

| Tecnología | Versión | Uso |
|---|---|---|
| **C#** | Compatible VS2022 (Roslyn 4.2) | Lenguaje principal |
| **.NET Framework** | **4.8** | Runtime |
| **ASP.NET Web Forms** | 4.8 | Arquitectura de páginas |
| **ADO.NET directo** | — | Acceso a datos (sin ORM) |
| **iTextSharp** | 5.5.13.4 (AGPL) | Generación de PDFs corporativos |
| **EPPlus / ClosedXML** | 8.1.1 / 0.105.0 | Exportación a Excel |
| **BouncyCastle** | 2.4.0 | Criptografía (firmas digitales) |
| **Newtonsoft.Json** | 13.0.3 | Serialización JSON |
| **Microsoft.AspNet.FriendlyUrls** | 1.0.2 | URLs amigables |
| **Microsoft.Web.Optimization** | 1.1.3 | Bundling y minificación de JS/CSS |

### Base de datos

| Tecnología | Detalle |
|---|---|
| **SQL Server** | Hosted en somee.com (`sgvActualizada.mssql.somee.com`) |
| **Base de datos** | `sgvActualizada` — 71 tablas, 135+ Stored Procedures |
| **Acceso** | ADO.NET — `SqlConnection` + `SqlCommand` |
| **Procedimientos** | Prefijos por dominio: `sp_DC_*`, `sp_LD_*`, `sp_MQ_*`, `sp_SE_*` |

### Otras herramientas / tecnologías

| Herramienta | Propósito |
|---|---|
| **IIS / IIS Express** | Servidor web (puerto HTTPS 44390 en desarrollo) |
| **MSBuild / Visual Studio 2022** | Compilación y despliegue |
| **Git** | Control de versiones |
| **Claude Code (MCP mssql-sgv)** | Consulta de esquema DB desde el IDE |
| **PBKDF2-SHA256** | Hashing de contraseñas (10,000 iteraciones, sal aleatoria) |
| **Canvas API (HTML5)** | Captura de firma biométrica del conductor |

---

## 3. 📁 Estructura del Proyecto

```bash
SGT-Sgv/
├── WebSGV/                              # Proyecto principal (único)
│   ├── App_Start/
│   │   ├── BundleConfig.cs             # Bundling JS/CSS
│   │   └── RouteConfig.cs             # Rutas MVC mínimas (sin uso activo)
│   │
│   ├── Views/                          # 56 páginas .aspx
│   │   ├── Login.aspx (.cs)            # 🔑 Punto de entrada y autenticación
│   │   ├── Dashboard.aspx (.cs)        # Panel admin principal
│   │   ├── DashboardConductor.aspx     # Espacio de trabajo del conductor
│   │   ├── DashboardGrifo.aspx         # Panel administrador de grifo
│   │   ├── DashboardOperador.aspx      # Panel operador de maquinaria
│   │   ├── DashboardExportacion.aspx   # KPIs exportaciones
│   │   │
│   │   ├── RegistroDespacho.aspx       # Crear/editar despacho
│   │   ├── ListaDespachos.aspx         # Listado despachos
│   │   ├── EditarDespacho.aspx         # Edición de despacho
│   │   │
│   │   ├── AgregarOrdenViaje.aspx      # Liquidación por conductor
│   │   ├── BuscarOrdenViaje.aspx       # Búsqueda órdenes
│   │   ├── DetalleOrdenViaje.aspx      # Vista detalle orden
│   │   ├── FirmarLiquidacion.aspx      # ✍️ Firma digital canvas
│   │   ├── LiquidacionesPendientes.aspx# Aprobación admin
│   │   │
│   │   ├── AgregarAbastecimiento.aspx  # Registro combustible
│   │   ├── BuscarAbastecimiento.aspx   # Búsqueda abastecimiento
│   │   ├── ReporteAbastecimiento.aspx  # Reporte grifo
│   │   │
│   │   ├── RegistroChoferes.aspx       # CRUD conductores
│   │   ├── RegistroClientes.aspx       # CRUD clientes
│   │   ├── RegistroTractos.aspx        # CRUD tractores/cabezales
│   │   ├── RegistroSemiremolques.aspx  # CRUD semiremolques
│   │   ├── RegistroPeajes.aspx         # CRUD peajes
│   │   ├── RegistroPlantas.aspx        # CRUD plantas
│   │   ├── RegistroRutas.aspx          # CRUD rutas
│   │   ├── RegistroProductos.aspx      # CRUD productos (carga)
│   │   │
│   │   ├── RegistroOperadores.aspx     # CRUD operadores maquinaria
│   │   ├── RegistroEquiposMaquinaria.aspx  # CRUD equipos pesados
│   │   ├── RegistroObras.aspx          # CRUD obras/proyectos
│   │   ├── RegistroClientesObra.aspx   # CRUD clientes de obra
│   │   ├── AsignacionesMaquinaria.aspx # Asignación operador↔equipo↔obra
│   │   │
│   │   ├── AgregarCPIC.aspx            # Documentos internacionales
│   │   ├── BuscarCPIC.aspx
│   │   ├── AgregarFactura.aspx         # Facturas
│   │   ├── BuscarFactura.aspx
│   │   │
│   │   ├── AgregarIndicadores.aspx     # KPIs manuales
│   │   ├── ConsultaAuditoria.aspx      # Consulta log auditoría
│   │   ├── Auditoria.aspx              # Vista completa auditoría
│   │   │
│   │   ├── Exportacion/
│   │   │   ├── DashboardExportacion.aspx (.cs)
│   │   │   └── RegistroSeguimiento.aspx
│   │   │
│   │   ├── DescargarPdfOrdenViaje.aspx # Descarga PDF con control de acceso
│   │   ├── DescargarPdfAbastecimiento.aspx
│   │   ├── RecuperarContrasena.aspx    # Recuperación contraseña
│   │   ├── RestablecerContrasena.aspx
│   │   ├── RolesHelper.cs              # Matriz centralizada de permisos
│   │   ├── Error.aspx / Error404.aspx
│   │
│   ├── Services/                       # Lógica de negocio (capa servicio)
│   │   ├── PdfOrdenViajeService.cs     # PDF SGV-CDF-F-05 (Orden de Viaje)
│   │   ├── PdfAbastecimientoService.cs # PDF SGV-CDF-F-06 (Abastecimiento)
│   │   ├── FirmaService.cs             # Orquestador de firma digital
│   │   └── RegistroDespachoService.cs  # Lógica de creación de despachos
│   │
│   ├── Helpers/                        # Utilidades transversales
│   │   ├── SecurityHelper.cs           # Sesión y control de acceso
│   │   ├── AuditoriaHelper.cs          # Registro auditoría (auto-crea tabla)
│   │   ├── EmpresaConfigHelper.cs      # Datos empresa + formatos controlados
│   │   ├── PasswordHelper.cs           # PBKDF2 hashing/verificación
│   │   ├── HashHelper.cs               # SHA-256 para integridad de PDF
│   │   ├── FechaHelper.cs              # Timezone Perú (UTC-5)
│   │   └── NumeroALetrasHelper.cs      # Números a texto en español
│   │
│   ├── Models/
│   │   ├── DniResponse.cs              # DTO respuesta API DNI
│   │   └── DniService.cs               # Consulta DNI en apis.net.pe
│   │
│   ├── Database/                       # Scripts SQL (despliegue manual)
│   │   ├── Schema/                     # DDL incremental (01_ a 05_)
│   │   ├── Scripts/                    # Migraciones de datos
│   │   └── StoredProcedures/           # 135+ SPs organizados por prefijo
│   │
│   ├── Content/                        # Bootstrap 5 + CSS custom
│   ├── Scripts/                        # jQuery + Bootstrap JS + scripts propios
│   │
│   ├── Global.asax.cs                  # Ciclo de vida de la aplicación
│   ├── Site.Master (.cs)               # Master page con navbar y sesión
│   ├── Site.Mobile.Master (.cs)        # Variante móvil
│   │
│   ├── Web.config                      # Config principal (referencia externos)
│   ├── connectionStrings.config        # 🔒 Gitignored — cadena de conexión DB
│   └── appSettings.Secrets.config      # 🔒 Gitignored — SMTP + otros secretos
│
├── docs/                               # Documentación técnica
│   ├── STACK_Y_FLUJO_SGV.md
│   ├── FLUJO_DE_TRABAJO_SGV.md
│   ├── GUIA_CREACION_ROLES.md
│   └── MEJORAS_UI_ABASTECIMIENTO.md
│
├── WebSGV.sln                          # Solución Visual Studio
├── AGENTS.md                           # Sistema multi-agente IA
└── .claude/                            # Configuración Claude Code (MCP, hooks)
```

**Estadísticas de archivos:**

| Tipo | Cantidad |
|---|---|
| Páginas `.aspx` | 56 |
| Archivos C# (`.cs`) | 133 |
| Scripts SQL (`.sql`) | 92+ |
| Archivos CSS | 8 |
| Archivos JS propios | 6+ |

---

## 4. 🧩 Módulos Principales

### Módulo 1: 🔐 Autenticación y Autorización

**Responsabilidad:** Control de acceso al sistema, gestión de sesión, hashing de contraseñas, bloqueo por fuerza bruta, permisos por rol.

**Tecnologías:** ASP.NET Session State, PBKDF2-SHA256, Application State (bloqueo IP).

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Views/Login.aspx.cs` | Validación de credenciales, inicio de sesión |
| `Helpers/SecurityHelper.cs` | Guards de sesión y rol (`ExigirSesion`, `EsAdmin`, etc.) |
| `Helpers/PasswordHelper.cs` | Hashing PBKDF2 y verificación de contraseñas |
| `Views/RolesHelper.cs` | Matriz centralizada de permisos por sección |
| `Views/RecuperarContrasena.aspx.cs` | Flujo de recuperación vía email |
| `Site.Master.cs` | Verificación de sesión en carga de cada página |

**Roles del sistema:**

| Rol | Descripción | Dashboard por defecto |
|---|---|---|
| `ADMIN` | Administrador general | Dashboard.aspx |
| `ADMINISTRADOR DE SISTEMA` | Superadmin (auditoría + config) | Dashboard.aspx |
| `CONDUCTOR` / `CHOFER` | Conductor de vehículo | DashboardConductor.aspx |
| `ADMINISTRADOR DE GRIFO` | Gestor de combustible | DashboardGrifo.aspx |
| `ADMINISTRADOR DE MAQUINARIA` | Gestor de equipos pesados | Dashboard.aspx |
| `OPERADOR` | Operador de maquinaria | DashboardOperador.aspx |
| `SUPERVISOR` | Supervisión general | Dashboard.aspx |

---

### Módulo 2: 🚛 Gestión de Despachos

**Responsabilidad:** Creación y seguimiento de órdenes de despacho. Asignación de conductor, tracto, semiremolque, ruta y cliente. Vinculación de documentos (facturas, CPIC internacional).

**Tecnologías:** ADO.NET, Stored Procedures (`sp_LD_*`), Web Forms con AJAX.

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Views/RegistroDespacho.aspx.cs` | Creación de despacho |
| `Views/ListaDespachos.aspx.cs` | Vista y filtrado de despachos |
| `Views/EditarDespacho.aspx.cs` | Modificación de despachos existentes |
| `Services/RegistroDespachoService.cs` | Lógica de negocio del despacho |
| `Views/AgregarFactura.aspx.cs` | Vinculación de facturas |
| `Views/AgregarCPIC.aspx.cs` | Documentos internacionales (CPIC) |

---

### Módulo 3: 📝 Órdenes de Viaje y Liquidaciones

**Responsabilidad:** Registro de gastos del viaje por el conductor (combustible, peajes, viáticos, reparaciones). Firma digital con trazo biométrico. Aprobación administrativa con segunda firma. Generación y archivo de PDF SGV-CDF-F-05.

**Tecnologías:** iTextSharp (PDF), Canvas API (firma), SHA-256, ADO.NET, FirmaService.

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Views/AgregarOrdenViaje.aspx.cs` | Carga de gastos por el conductor |
| `Views/FirmarLiquidacion.aspx.cs` | Captura firma canvas + envío |
| `Views/LiquidacionesPendientes.aspx.cs` | Revisión y aprobación por admin |
| `Views/DetalleOrdenViaje.aspx.cs` | Vista de detalle de orden |
| `Services/FirmaService.cs` | Orquestación de firmas (conductor + admin) |
| `Services/PdfOrdenViajeService.cs` | Generación PDF con firma embebida |
| `Helpers/HashHelper.cs` | SHA-256 para integridad del documento |

---

### Módulo 4: ⛽ Abastecimiento de Combustible (Grifo)

**Responsabilidad:** Registro de abastecimientos de combustible en grifo propio. Métricas de consumo por vehículo/conductor/ruta. Generación de recibo PDF (SGV-CDF-F-06). Dashboard del administrador de grifo.

**Tecnologías:** ADO.NET, iTextSharp, Bootstrap Tabs, EPPlus (Excel).

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Views/AgregarAbastecimiento.aspx.cs` | Registro de carga de combustible |
| `Views/BuscarAbastecimiento.aspx.cs` | Búsqueda y filtrado |
| `Views/ReporteAbastecimiento.aspx.cs` | Reporte exportable a Excel |
| `Views/DashboardGrifo.aspx.cs` | Panel del administrador de grifo |
| `Services/PdfAbastecimientoService.cs` | Generación PDF SGV-CDF-F-06 |
| `Views/DescargarPdfAbastecimiento.aspx.cs` | Descarga con control de acceso |

---

### Módulo 5: 🏗️ Maquinaria Pesada y Obras

**Responsabilidad:** Gestión de operadores de maquinaria, equipos (excavadoras, motoniveladoras, volquetes), obras/proyectos y asignaciones. Registro diario de horas de trabajo por el operador.

**Tecnologías:** ADO.NET, Stored Procedures (`sp_MQ_*`), Bootstrap móvil-first.

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Views/RegistroOperadores.aspx.cs` | Alta/edición de operadores |
| `Views/RegistroEquiposMaquinaria.aspx.cs` | Alta/edición de equipos pesados |
| `Views/RegistroObras.aspx.cs` | Alta/edición de obras |
| `Views/AsignacionesMaquinaria.aspx.cs` | Asignar operador + equipo a obra |
| `Views/DashboardOperador.aspx.cs` | Registro diario de horas del operador |

---

### Módulo 6: 🌐 Seguimiento de Exportaciones

**Responsabilidad:** Trazabilidad de embarques de exportación: destino, códigos HS, cantidades, peso, estado aduanero y entrega. KPIs por mes y destino.

**Tecnologías:** ADO.NET, `sp_SE_*`, ClosedXML (Excel), gráficas DataVisualization.

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Views/Exportacion/RegistroSeguimiento.aspx.cs` | Alta de seguimiento de embarque |
| `Views/Exportacion/DashboardExportacion.aspx.cs` | KPIs y gráficas de exportación |
| `Database/Schema/05_SeguimientoExportacion.sql` | DDL de la tabla de seguimiento |

---

### Módulo 7: 🔍 Auditoría y Trazabilidad

**Responsabilidad:** Registro automático de cada operación significativa (INSERT, UPDATE, DELETE, LOGIN, APROBACIÓN) con valores antes/después, usuario, rol, IP y user agent. No-repudio de documentos mediante firma digital append-only.

**Tecnologías:** ADO.NET, tabla `AuditoriaLog` (auto-creada), tabla `FirmaDigital` (append-only con triggers).

**Archivos clave:**

| Archivo | Rol |
|---|---|
| `Helpers/AuditoriaHelper.cs` | Logger de operaciones de negocio |
| `Views/Auditoria.aspx.cs` | Vista de log de auditoría |
| `Views/ConsultaAuditoria.aspx.cs` | Consulta filtrada del log |
| `Database/Schema/02_FirmaDigital.sql` | DDL con trigger append-only |

---

### Módulo 8: 🗂️ Maestros (Datos de Referencia)

**Responsabilidad:** Mantenimiento de catálogos del sistema: conductores, clientes, tractos, semiremolques, peajes, plantas, rutas, productos.

**Archivos clave:** `RegistroChoferes.aspx`, `RegistroClientes.aspx`, `RegistroTractos.aspx`, `RegistroSemiremolques.aspx`, `RegistroPeajes.aspx`, `RegistroPlantas.aspx`, `RegistroRutas.aspx`, `RegistroProductos.aspx`.

---

### Módulo 9: 📄 Generación de Documentos PDF

**Responsabilidad:** Generación de documentos regulatorios con branding corporativo, firma embebida, código de formato controlado (ISO), hash de integridad y número correlativo.

**Tecnologías:** iTextSharp 5 (AGPL), BouncyCastle, SHA-256.

**Documentos generados:**

| Código | Nombre | Servicio |
|---|---|---|
| `SGV-CDF-F-05` | Orden de Viaje / Liquidación | `PdfOrdenViajeService.cs` |
| `SGV-CDF-F-06` | Recibo de Abastecimiento | `PdfAbastecimientoService.cs` |

---

## 5. 🔄 Flujos Principales del Sistema

### Flujo 1: Despacho → Orden de Viaje → Firma → Aprobación → PDF archivado

```
[ADMIN] RegistroDespacho.aspx
     ├── Selecciona: cliente, tipo operación, planta, tracto, semiremolque
     ├── Asigna: conductor(es) → crea ViajesProgreso por conductor
     └── Vincula: facturas (Factura) + permisos internacionales (CPIC)
           ↓
[CONDUCTOR] AgregarOrdenViaje.aspx
     ├── Selecciona ViajeProgreso finalizado
     ├── Ingresa gastos: combustible, peajes, viáticos, reparaciones
     └── Crea OrdenViaje (estado=PENDIENTE, aprobacion=PENDIENTE)
           ↓
[CONDUCTOR] FirmarLiquidacion.aspx
     ├── Dibuja firma en canvas HTML5 (PNG biométrico)
     ├── FirmaService.RegistrarFirmaConductor()
     │    ├── Genera PDF SGV-CDF-F-05 con firma embebida (iTextSharp)
     │    ├── Calcula SHA-256 del PDF
     │    ├── Inserta FirmaDigital (append-only)
     │    └── Archiva PDF: ~/App_Data/OrdenesViaje/{YYYY}/{MM}/OV-{YYYY}-{N}.pdf
     └── Estado orden → FIRMADA
           ↓
[ADMIN] LiquidacionesPendientes.aspx
     ├── Revisa orden + firma del conductor
     ├── Puede registrar ajuste (OrdenViajeAjuste)
     ├── Aprueba o rechaza con observaciones
     └── FirmaService.RegistrarFirmaAdmin() → segunda entrada FirmaDigital
           ↓
Estado final: APROBADA — PDF inmutable en App_Data
```

---

### Flujo 2: Abastecimiento de Combustible

```
[ADMIN / ADMIN_GRIFO] AgregarAbastecimiento.aspx
     ├── Selecciona: vehículo (tracto/volquete/camioneta), conductor, ruta
     ├── Ingresa: galones asignados, galones reales, producto (Diesel/GLP/Gasolina)
     ├── Genera PDF SGV-CDF-F-06
     └── Registra en AbastecimientoCombustible
           ↓
[ADMIN_GRIFO] DashboardGrifo.aspx
     ├── Tab 1: Viajes activos sin abastecer
     ├── Tab 2: Historial de abastecimientos
     └── Acción rápida: crear abastecimiento especial
           ↓
[ADMIN] ReporteAbastecimiento.aspx
     └── Filtros + exportación Excel (EPPlus)
```

---

### Flujo 3: Maquinaria Pesada

```
[ADMIN / ADMIN_MAQ] Alta de maestros
     ├── RegistroOperadores.aspx → crea usuario con rol OPERADOR
     ├── RegistroEquiposMaquinaria.aspx → dozer, excavadora, motoniveladora...
     ├── RegistroObras.aspx → proyecto/obra con cliente
     └── AsignacionesMaquinaria.aspx → operador + equipo → obra (rango fechas)
           ↓
[OPERADOR] DashboardOperador.aspx (móvil-first)
     ├── Visualiza asignación activa
     └── Registra: horas trabajadas, combustible, mantenimiento, incidencias
```

---

### Flujo 4: Seguimiento de Exportaciones

```
[SUPERVISOR / ADMIN] RegistroSeguimiento.aspx
     ├── Ingresa: país destino, códigos HS, cantidad, peso, valor
     └── Vincula a Despacho + OrdenViaje → crea SeguimientoExportacion
           ↓
DashboardExportacion.aspx
     ├── KPIs mensuales: embarques, valor, por país
     ├── Gráficas: etapas del pipeline de exportación
     └── Estados: pendiente aduana → en tránsito → entregado
```

---

## 6. 📊 Estado Actual del Proyecto

### ✅ Lo que ya está implementado

- Autenticación segura con PBKDF2 + anti-fuerza bruta por IP
- Gestión completa de despachos (alta, edición, listado, búsqueda)
- Liquidaciones de conductores con firma digital biométrica (canvas PNG)
- Aprobación administrativa con segunda firma de constancia
- Generación de PDFs corporativos con formato controlado (SGV-CDF-F-05, SGV-CDF-F-06)
- Módulo completo de abastecimiento de combustible con dashboard de grifo
- Módulo de maquinaria pesada: maestros + asignaciones + log diario del operador
- Módulo de seguimiento de exportaciones con KPIs y gráficas
- Sistema de auditoría automático (AuditoriaLog, antes/después)
- Firma digital append-only con SHA-256 para no-repudio (FirmaDigital)
- Mantenimiento de maestros: conductores, clientes, vehículos, rutas, plantas, peajes, productos
- Gestión de CPIC (documentos internacionales) y facturas
- Sistema de roles con 7 niveles y matriz de permisos centralizada
- Recuperación de contraseña vía email (SMTP configurado)
- Interfaz responsive (Bootstrap 5 + CSS móvil-first)
- Dashboard diferenciado por rol con tabs y KPIs

### 🔧 Lo que parece estar en desarrollo / incompleto

- Integración con Power BI (documentado en `Documentacion_Tecnica_PBI_SGV_MARZO2025.md` pero no implementada en el código)
- Módulo de indicadores KPI (`AgregarIndicadores.aspx`) — parece en proceso de definición
- Posiblemente: notificaciones push o alertas por email en eventos críticos (liquidación pendiente, etc.)
- Módulo de reportería avanzada (más allá del reporte de abastecimiento)

### ⚠️ Posibles deudas técnicas detectadas

| Área | Deuda Técnica | Impacto |
|---|---|---|
| **Framework** | .NET Framework 4.8 (legacy); no hay ruta a .NET 8+ por Web Forms | Alto — bloquea migración a nube moderna |
| **ORM** | ADO.NET directo — SQL disperso en código C# | Medio — dificulta mantenimiento y testing |
| **Web Forms** | Arquitectura de página completa — ViewState, postbacks, lifecycle complejo | Alto — productividad y testabilidad |
| **PDF** | iTextSharp 5 (AGPL) — licencia copyleft; versión 7+ requiere licencia comercial | Medio — riesgo legal si se distribuye |
| **Paquetes** | `packages.config` (legacy) en lugar de `PackageReference` | Bajo — actualización más difícil |
| **Tests** | Sin proyecto de tests; sin pipeline CI/CD | Alto — regresiones no detectadas automáticamente |
| **Roles** | Strings hardcodeados para comparación de roles (parcialmente mitigado por constantes en RolesHelper) | Bajo-Medio |
| **Sesión** | Session State en memoria — sticky sessions necesarias para escalar horizontalmente | Medio — escalabilidad limitada |
| **DB Schema** | Migraciones manuales (scripts SQL sin versionado automático) | Medio — riesgo en despliegues |
| **Secretos** | Correctamente gitignoreados, pero sin gestión de secretos (Azure Key Vault, etc.) | Bajo — aceptable en escala actual |

---

## 7. 🏛️ Recomendaciones Arquitectónicas

### Prioridad Alta

1. **Migración gradual a ASP.NET Core (Blazor Server o Razor Pages):**  
   Web Forms está en modo mantenimiento. Una migración módulo a módulo (empezando por los más simples: maestros CRUD) reduciría la deuda técnica más crítica.

2. **CI/CD con GitHub Actions / Azure DevOps:**  
   Agregar un pipeline básico: build → test → deploy a staging. Actualmente no hay automatización de despliegue.

3. **Suite de tests de integración:**  
   Con ADO.NET directo, lo más valioso son tests de integración contra una BD de prueba que validen los stored procedures y las reglas de negocio (liquidaciones, firmas, auditoría).

### Prioridad Media

4. **Migrar a `PackageReference` en el .csproj:**  
   Reemplazar `packages.config` mejora la gestión de dependencias transitivas y facilita actualizaciones de seguridad.

5. **Reemplazar iTextSharp 5 por una alternativa:**  
   Opciones: `QuestPDF` (MIT, moderno) o `iText 7` (licencia comercial con opciones open source). El primero es especialmente atractivo para nuevos módulos.

6. **Versionado de esquema de BD:**  
   Adoptar Flyway, DbUp o similar para aplicar los scripts SQL de forma ordenada, repetible y trazable en cada entorno.

7. **Centralizar las cadenas de conexión en un gestor de secretos:**  
   Azure Key Vault o similar, especialmente si el sistema crece y se despliega en múltiples entornos.

### Prioridad Baja / Largo Plazo

8. **API REST interna:**  
   Extraer la lógica de negocio de los code-behind a una capa de servicio + controladores Web API. Permitiría una futura SPA (React/Blazor WASM) sin reescribir la lógica.

9. **Caché de datos de referencia:**  
   Los maestros (clientes, conductores, rutas, peajes) se consultan frecuentemente. Un caché en Application state o Redis reduciría carga en la BD.

10. **Monitoreo y alertas:**  
    Agregar Application Insights o similar para capturar excepciones en producción, métricas de rendimiento y alertas ante errores críticos.

---

## 8. 🤖 Contexto para IA

> Este apartado es un resumen compacto y denso en información diseñado para ser usado como **contexto de sistema permanente** por otra IA (ChatGPT, Claude, Copilot, etc.) que trabaje en este proyecto.

---

### Sistema: SGT-SGV — Sistema de Gestión de Transporte

**Empresa:** SERVICIOS GENERALES VIVIANA E.I.R.L. | RUC: 20483851171 | Perú  
**Stack:** ASP.NET Web Forms / .NET Framework 4.8 / C# / SQL Server / ADO.NET directo / Bootstrap 5 / jQuery 3.7.0 / iTextSharp 5 (PDFs) / EPPlus+ClosedXML (Excel)  
**DB:** SQL Server en somee.com (`sgvActualizada`), 71 tablas, 135+ SPs. Acceso vía `SqlConnection`/`SqlCommand` con parámetros; **nunca concatenar SQL**.  
**Arquitectura:** Monolito de una sola solución Visual Studio (`WebSGV.sln`). Un proyecto: `WebSGV/`. Sin ORM. Sin pipeline CI/CD. Sin tests automatizados.

---

### Dominio del problema: Transporte y Logística

El sistema gestiona el ciclo de vida completo de un despacho de transporte terrestre:

1. **Despacho** — Un admin asigna conductor + tracto + semiremolque + ruta + cliente + facturas.
2. **Viaje en progreso** (`ViajesProgreso`) — El conductor registra el viaje activo.
3. **Orden de viaje / Liquidación** (`OrdenViaje`) — Al finalizar, el conductor registra todos sus gastos (combustible, peajes, viáticos, reparaciones). Estado: PENDIENTE → FIRMADA → APROBADA.
4. **Firma digital** — El conductor firma con trazo biométrico (Canvas PNG) + se genera PDF SGV-CDF-F-05 con SHA-256. Registro append-only en `FirmaDigital` (no se puede borrar ni modificar).
5. **Aprobación** — Un admin revisa, puede ajustar (`OrdenViajeAjuste`) y firma como constancia.
6. **PDF archivado** — `~/App_Data/OrdenesViaje/{YYYY}/{MM}/OV-{YYYY}-{N}.pdf` (inmutable).

**Módulos secundarios:** Grifo (combustible, PDF F-06), Maquinaria pesada (operadores, equipos, obras, asignaciones, log diario), Exportaciones (seguimiento embarques, KPIs).

---

### Reglas de negocio clave

| Regla | Detalle |
|---|---|
| **No-repudio de firma** | `FirmaDigital` es append-only. No se hace UPDATE ni DELETE. Anular = nueva fila con `idFirmaAnulada`. |
| **SHA-256 por documento** | Cada PDF tiene hash SHA-256 almacenado. Cualquier modificación posterior se detecta. |
| **Estados de OrdenViaje** | PENDIENTE → FIRMADA (conductor) → APROBADA (admin). Solo el admin puede aprobar/rechazar. |
| **Zona horaria** | Todo timestamp se registra en hora Perú (UTC-5) usando `FechaHelper.Ahora()`. El servidor puede estar en UTC. |
| **Formato controlado** | Cada PDF debe referenciar un registro en `FormatoControlado` (código, versión, fecha vigencia). |
| **Anti-fuerza bruta** | 5 intentos fallidos de login por IP → bloqueo de 300 segundos (Application state). |
| **Rol como texto** | Los roles se almacenan como texto en `Usuarios.rol`. Comparación: `String.Equals(..., OrdinalIgnoreCase)`. |
| **Auditoría automática** | Toda operación de negocio llama a `AuditoriaHelper.Registrar()`. La tabla se auto-crea si no existe. |
| **Conductores vs Usuarios** | Un usuario de rol CONDUCTOR tiene `Session["IdConductor"]` que es FK a `Conductores`. |
| **Grifo** | `AbastecimientoCombustible` tiene `tipoVehiculo` (tracto/volquete/camioneta) y métricas de consumo. |
| **Formato numérico** | Moneda en soles peruanos (S/). Decimales con coma en UI (`es-PE`), punto en BD. |

---

### Convenciones de código

- **Nombres de SP:** prefijo + módulo: `sp_DC_` (Dashboard Conductor), `sp_LD_` (Lotes Despacho), `sp_MQ_` (Maquinaria), `sp_SE_` (Seguimiento Exportación).
- **Páginas en español:** todas las páginas, variables, métodos y propiedades están en español (e.g., `ObtenerRol()`, `FechaViaje`, `EstadoAprobacion`).
- **Code-behind:** la lógica de UI va en `Page_Load`, `Page_Init` y event handlers. La lógica de negocio va en `Services/`. Los helpers van en `Helpers/`.
- **Sin ORM:** todo acceso a datos es ADO.NET directo con parámetros `SqlParameter`. Nunca concatenar strings en SQL.
- **ViewState:** keyed al SessionID del usuario (protección anti-tampering en Site.Master).
- **Configuración externa:** DB en `connectionStrings.config` (gitignored); SMTP en `appSettings.Secrets.config` (gitignored). Siempre referenciar secretos desde `ConfigurationManager`.
- **PDF archivado:** la ruta base está en `appSettings["OrdenViaje.RutaArchivo"]` = `~/App_Data/OrdenesViaje`.
- **Boostrap 5:** uso de clases utilitarias modernas. Sin Bootstrap 4. Las tarjetas usan gradiente azul corporativo `#0B3D91` y rojo `#C8102E`.

---

### Decisiones arquitectónicas importantes

1. **Web Forms (no MVC/Razor Pages):** decisión histórica. Toda la UI usa `<asp:*>` controls, `UpdatePanel`, `ScriptManager`. No hay routing de controladores.
2. **ADO.NET directo:** sin Entity Framework ni Dapper. Los stored procedures encapsulan toda la lógica SQL; C# solo llama al SP y mapea resultados.
3. **Firma append-only:** la integridad documental es crítica (cumplimiento ISO/BASC). La tabla `FirmaDigital` tiene triggers que bloquean UPDATE/DELETE.
4. **Sesión en memoria:** no se usa SQL Session State. Escalar horizontalmente requeriría sticky sessions o migrar.
5. **PDF con iTextSharp 5 (AGPL):** librería legacy pero funcional. Los PDFs se generan en memoria y opcionalmente se archivan en disco.
6. **Roles como texto libre:** no hay tabla de roles. Se comparan strings. La enumeración efectiva está en las constantes de `RolesHelper`.
7. **Sin ORM por decisión explícita:** los SPs tienen lógica compleja de negocio (validaciones, transacciones) que sería difícil de reproducir con LINQ.

---

## 📝 Notas Adicionales

### 🔒 Seguridad: puntos fuertes

- Las contraseñas usan **PBKDF2-SHA256 con 10,000 iteraciones y sal de 128 bits** — un estándar sólido para .NET Framework 4.8.
- El código de login tiene **comparación en tiempo constante** (previene timing attacks).
- Los **secretos están gitignoreados** correctamente y referenciados por `configSource`.
- El **ViewState está keyed al SessionID** — previene ataques de replay de ViewState.
- Los PDFs se sirven a través de una página de descarga con **verificación de acceso**, no desde un directorio estático.

### 📍 Contexto geográfico y regulatorio

- La empresa opera en **Perú**. Los documentos siguen la nomenclatura **BASC** (Business Alliance for Secure Commerce) e **ISO 9001/14001/45001**.
- El DNI de conductores se consulta en tiempo real via `apis.net.pe` (DniService.cs).
- La moneda es **soles peruanos (PEN, S/)**.
- El formato de fecha es `dd/MM/yyyy` en UI y la zona horaria es **UTC-5 (Perú)** sin horario de verano.

### 🧠 Para la IA trabajando en este proyecto

- **Siempre usa parámetros SQL** — el codebase es coherente en esto.
- **Registra auditoría** en toda nueva operación de negocio: `AuditoriaHelper.Registrar(...)`.
- **Respeta el ciclo de vida de Web Forms:** `Page_Init` → `Page_Load` → eventos de control → `Page_PreRender`.
- **No modifies `FirmaDigital` directamente** — es append-only por diseño de compliance.
- **Usa `FechaHelper.Ahora()`** para timestamps — nunca `DateTime.Now` directamente.
- **Los nuevos SPs** deben seguir el prefijo del módulo (`sp_DC_`, `sp_LD_`, etc.).
- **Las nuevas páginas** deben agregar la verificación de rol al inicio de `Page_Load` usando `RolesHelper.ValidarAccesoSeccion()`.
- **Los PDFs** deben referenciar un registro en `FormatoControlado` para cumplimiento ISO.

---

*Documento generado el 22/05/2026. Para actualizar, re-ejecutar el análisis con Claude Code en el directorio raíz del proyecto.*
