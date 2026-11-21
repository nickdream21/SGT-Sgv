# 📦 Inventario Completo de la Base de Datos Actual
**Base de datos:** `sgvActualizada` — SQL Server en Somee.com  
**Servidor:** `sgvActualizada.mssql.somee.com`  
**Fecha de análisis:** 2026-05-22 (SOLO LECTURA — sin modificaciones)

---

## 🗂️ Resumen General

| Elemento | Cantidad |
|---|---|
| Tablas | **71** |
| Tablas con PK | 68 |
| Tablas sin PK | 3 (`CPIC_Productos`, 2 backups) |
| Stored Procedures | **135** |
| Triggers | **38** (11 tablas con triggers de auditoría + FirmaDigital con 2 append-only) |
| Índices secundarios | **77** |
| Claves foráneas | **111** |

---

## 📊 Inventario de Tablas — Registros y Descripción

| # | Tabla | Registros | Columnas | Tiene PK | Descripción |
|---|---|---|---|---|---|
| 1 | `AbastecimientoCombustible` | **4** | 30 | ✅ `idAbastecimientoCombustible` | Registros de carga de combustible |
| 2 | `AsignacionesMaquinaria` | **1** | 9 | ✅ `idAsignacion` | Asignación operador-equipo-obra |
| 3 | `Auditoria` | **207** | 11 | ✅ `idAuditoria` | Log de auditoría legacy (tabla antigua) |
| 4 | `AuditoriaLog` | **199** | 13 | ✅ `IdAuditoria` | Log de auditoría moderno (con índices) |
| 5 | `camionetas` | **23** | 8 | ✅ `id` | Catálogo de camionetas |
| 6 | `Carreta` | **134** | 5 | ✅ `idCarreta` | Semiremolques / trailers |
| 7 | `CategoriasAdicionales` | **13** | 6 | ✅ `idCategoriaAdicional` | Categorías adicionales de OrdenViaje |
| 8 | `Cliente` | **4** | 4 | ✅ `idCliente` | Clientes de la empresa |
| 9 | `ClientesObra` | **1** | 7 | ✅ `idClienteObra` | Clientes de obras de maquinaria |
| 10 | `Conductor` | **85** | 11 | ✅ `idConductor` | Conductores de vehículos |
| 11 | `CPIC` | **57** | 7 | ✅ `idCPIC` | Documentos CPIC internacionales |
| 12 | `CPIC_Productos` | **6** | 4 | ❌ (sin PK) | Productos de un CPIC |
| 13 | `Departamento` | **0** | 3 | ✅ `idDepartamento` | Catálogo geográfico — vacía |
| 14 | `DescuentosReintegros` | **15** | 10 | ✅ `idDescuentoReintegro` | Descuentos/reintegros en OrdenViaje |
| 15 | `DespachoCombustibleObra` | **0** | 16 | ✅ `idDespachoObra` | Despacho de combustible a obras |
| 16 | `Despachos` | **308** | 26 | ✅ `idDespacho` | Despachos de transporte (operacional) |
| 17 | `DetalleAlimentacion` | **0** | 9 | ✅ `idDetalleAlimentacion` | Detalle de gastos de alimentación |
| 18 | `DetalleApoyoSeguridad` | **0** | 9 | ✅ `idDetalleApoyoSeguridad` | Detalle de apoyo de seguridad |
| 19 | `DetalleCombustible` | **2** | 9 | ✅ `idDetalleCombustible` | Detalle de combustible por viaje |
| 20 | `DetalleEncapada` | **0** | 9 | ✅ `idDetalleEncapada` | Detalle de encapados |
| 21 | `DetalleHospedaje` | **0** | 9 | ✅ `idDetalleHospedaje` | Detalle de hospedaje |
| 22 | `DetalleMovilidad` | **0** | 9 | ✅ `idDetalleMovilidad` | Detalle de movilidad |
| 23 | `DetalleOrdenViaje` | **0** | 4 | ✅ `idDetalleOrdenViaje` | Detalle general de OrdenViaje |
| 24 | `DetallePeajes` | **55** | 10 | ✅ `idDetallePeaje` | Peajes cobrados por viaje |
| 25 | `DetalleReparacionesVarios` | **3** | 9 | ✅ `idDetalleReparacionesVarios` | Reparaciones y varios |
| 26 | `DetalleSegmento` | **0** | 6 | ✅ `idDetalleSegmento` | Detalle de segmento de orden |
| 27 | `DetalleTicketEcuador` | **0** | 6 | ✅ `idDetalle` | Tickets de Ecuador |
| 28 | `Distrito` | **0** | 3 | ✅ `idDistrito` | Catálogo geográfico — vacía |
| 29 | `DocumentosCPIC` | **14** | 11 | ✅ `idDocumento` | Archivos adjuntos de CPIC |
| 30 | `DocumentosFactura` | **17** | 11 | ✅ `idDocumento` | Archivos adjuntos de Factura |
| 31 | `Egresos` | **32** | 26 | ✅ `idEgresos` | Egresos de OrdenViaje |
| 32 | `EquiposMaquinaria` | **1** | 9 | ✅ `idEquipo` | Equipos de maquinaria pesada |
| 33 | `EstacionesPeaje` | **31** | 4 | ✅ `idEstacion` | Catálogo de estaciones de peaje |
| 34 | `Factura` | **33** | 6 | ✅ `idFactura` | Facturas vinculadas a despachos |
| 35 | `FirmaDigital` | **9** | 18 | ✅ `idFirma` | Firmas digitales — **APPEND-ONLY** |
| 36 | `FormatoControlado` | **2** | 7 | ✅ `idFormato` | Catálogo de formatos ISO/BASC |
| 37 | `GuiasTransportista` | **0** | 9 | ✅ `idGuia` | Guías de transportista |
| 38 | `Indicadores` | **1507** | 37 | ✅ `idIndicador` | Indicadores KPI operacionales |
| 39 | `IngresoCombustibleEcuador` | **0** | 10 | ✅ `idIngreso` | Ingresos de combustible Ecuador |
| 40 | `Ingresos` | **31** | 16 | ✅ `idIngreso` | Ingresos de OrdenViaje |
| 41 | `IngresosAdicionales` | **7** | 6 | ✅ `idIngresoAdicional` | Ingresos adicionales de OrdenViaje |
| 42 | `Liquidaciones` | **1** | 8 | ✅ `idLiquidacion` | Liquidaciones finales |
| 43 | `LugarAbastecimiento` | **2** | 2 | ✅ `idLugarAbastecimiento` | Catálogo de lugares de abastecimiento |
| 44 | `Lugares` | **7** | 4 | ✅ `idLugar` | Catálogo de lugares geográficos |
| 45 | `Manifiesto` | **0** | 4 | ✅ `idManifiesto` | Manifiestos de carga |
| 46 | `Obras` | **1** | 8 | ✅ `idObra` | Obras/proyectos de maquinaria |
| 47 | `OperacionesSubTramo` | **1** | 12 | ✅ `idOperacion` | Operaciones por sub-tramo |
| 48 | `Operadores` | **1** | 6 | ✅ `idOperador` | Operadores de maquinaria |
| 49 | `OrdenViaje` | **32** | 33 | ✅ `idOrdenViaje` | Órdenes de viaje / liquidaciones |
| 50 | `OrdenViajeAjuste` | **0** | 9 | ✅ `idAjuste` | Ajustes administrativos a OrdenViaje |
| 51 | `Pais` | **0** | 2 | ✅ `idPais` | Catálogo de países — vacía |
| 52 | `PartesDiariosTrabajo` | **0** | 27 | ✅ `idParte` | Partes diarios de maquinaria |
| 53 | `Planta` | **6** | 4 | ✅ `idPlanta` | Plantas de carga/descarga |
| 54 | `PlantaCarga` | **2** | 6 | ✅ `idPlantaCarga` | Plantas de carga específicas |
| 55 | `PlantaDescarga` | **5** | 6 | ✅ `idPlanta` | Plantas de descarga específicas |
| 56 | `Producto` | **5** | 3 | ✅ `idProducto` | Productos transportados |
| 57 | `ProductosOperacion` | **2** | 7 | ✅ `idProductoOperacion` | Productos de una operación |
| 58 | `Provincia` | **0** | 3 | ✅ `idProvincia` | Catálogo geográfico — vacía |
| 59 | `Roles` | **6** | 6 | ✅ `idRol` | Catálogo de roles del sistema |
| 60 | `Ruta` | **2** | 4 | ✅ `idRuta` | Rutas predefinidas |
| 61 | `SegmentosOrdenViaje` | **0** | 16 | ✅ `idSegmento` | Segmentos de un viaje internacional |
| 62 | `SeguimientoExportacion` | **679** | 44 | ✅ `idSeguimiento` | Seguimiento de exportaciones |
| 63 | `SeguimientoExportacion_Backup_20260512_115847` | **5899** | 44 | ❌ (sin PK) | **TABLA DE BACKUP — eliminar en nueva BD** |
| 64 | `SeguimientoExportacion_Backup_20260512_120720` | **409** | 44 | ❌ (sin PK) | **TABLA DE BACKUP — eliminar en nueva BD** |
| 65 | `SubTramos` | **1** | 15 | ✅ `idSubTramo` | Sub-tramos de viaje |
| 66 | `TipoCarro` | **6** | 2 | ✅ `idTipoCarro` | Tipos de vehículo |
| 67 | `Tracto` | **100** | 5 | ✅ `idTracto` | Tractos/cabezales (100% activos) |
| 68 | `UploadHistory` | **0** | 9 | ✅ `UploadID` | Historial de subidas de archivo |
| 69 | `Usuarios` | **96** | 15 | ✅ `idUsuario` | Usuarios del sistema |
| 70 | `ViajesEnProgreso` | **74** | 18 | ✅ `idViajeProgreso` | Viajes en progreso / activos |
| 71 | `volquetes` | **40** | 9 | ✅ `id` | Catálogo de volquetes |

---

## ⚡ Triggers (38 eventos sobre 13 tablas)

### Triggers de Auditoría (INSERT/UPDATE/DELETE → tabla Auditoria/AuditoriaLog)
| Tabla | Trigger | Eventos |
|---|---|---|
| `AbastecimientoCombustible` | `TR_AbastecimientoCombustible_Auditoria` | INSERT, UPDATE, DELETE |
| `Carreta` | `TR_Carreta_Auditoria` | INSERT, UPDATE, DELETE |
| `Conductor` | `TR_Conductor_Auditoria` | INSERT, UPDATE, DELETE |
| `CPIC` | `TR_CPIC_Auditoria` | INSERT, UPDATE, DELETE |
| `CPIC_Productos` | `TR_CPIC_Productos_Auditoria` | INSERT, UPDATE, DELETE |
| `Factura` | `TR_Factura_Auditoria` | INSERT, UPDATE, DELETE |
| `GuiasTransportista` | `TR_GuiasTransportista_Auditoria` | INSERT, UPDATE, DELETE |
| `Indicadores` | `TR_Indicadores_Auditoria` | INSERT, UPDATE, DELETE |
| `Liquidaciones` | `TR_Liquidaciones_Auditoria` | INSERT, UPDATE, DELETE |
| `OrdenViaje` | `TR_OrdenViaje_Auditoria` | INSERT, UPDATE, DELETE |
| `Tracto` | `TR_Tracto_Auditoria` | INSERT, UPDATE, DELETE |
| `UploadHistory` | `TR_UploadHistory_Auditoria` | INSERT, UPDATE, DELETE |

### Triggers de Integridad Documental — APPEND-ONLY (INSTEAD OF)
| Tabla | Trigger | Tipo | Descripción |
|---|---|---|---|
| `FirmaDigital` | `trg_FirmaDigital_NoDelete` | INSTEAD OF DELETE | **Bloquea DELETE** — no-repudio |
| `FirmaDigital` | `trg_FirmaDigital_NoUpdate` | INSTEAD OF UPDATE | **Bloquea UPDATE** — no-repudio |

> ⚠️ **CRÍTICO:** Los triggers de `FirmaDigital` deben copiarse exactamente a la nueva BD antes de cualquier migración de datos.

---

## 🔢 Stored Procedures por Módulo (135 total)

| Módulo | Prefijo | Cantidad | Propósito |
|---|---|---|---|
| **Dashboard Conductor / Liquidaciones** | `sp_DC_` | 26 | Órdenes de viaje, firmas, viajes activos |
| **Lotes Despacho** | `sp_LD_` | 18 | Gestión de lotes, CPICs, facturas |
| **Seguimiento Exportación** | `sp_SE_` | 9 | CRUD, KPIs, dashboard, gráficos |
| **Maquinaria** | `sp_MQ_` | 5 | Partes diarios, asignaciones, operadores |
| **Reportes** | `sp_Reporte*` / `sp_Generar*` | 17 | Reportes financieros, consumo, rutas |
| **Maestros / CRUD** | `sp_Obtener*`, `sp_Crear*`, `sp_Insertar*` | 38 | Conductores, tractos, clientes, etc. |
| **Legacy sin prefijo** | Sin prefijo estándar | 22 | SPs heredados del sistema anterior |
| **Prueba** | `sp_PruebaDespacho` | 1 | ⚠️ SP de prueba — revisar antes de migrar |

---

## 🔗 Resumen de Dependencias FK

Las 111 FKs cubren estas relaciones principales:

- `Despachos` → `Conductor`, `Tracto`, `Carreta`, `Cliente`, `CPIC`, `Factura`, `OrdenViaje`, `ViajesEnProgreso`, `Producto`
- `OrdenViaje` → `Conductor`, `Tracto`, `Carreta`, `Cliente`, `CPIC`, `Producto`, `ViajesEnProgreso`, `FirmaDigital`, `Usuarios`
- `AbastecimientoCombustible` → `Tracto`, `Conductor`, `Carreta`, `LugarAbastecimiento`, `Ruta`, `TipoCarro`, `OrdenViaje`, `camionetas`, `volquetes`
- `FirmaDigital` → `FirmaDigital` (auto-referencia para `idFirmaAnulada`)
- `CPIC` → `Factura`; `CPIC_Productos` → `CPIC`, `Producto`
- `ViajesEnProgreso` → `Conductor`, `OrdenViaje`
- `Usuarios` → `Conductor`, `Operadores`
- `SegmentosOrdenViaje` → `OrdenViaje`, `Cliente`, `CPIC`, `Factura`
- `SubTramos` → `Liquidaciones`; `OperacionesSubTramo` → `SubTramos`, `Cliente`, `CPIC`, `Factura`

---

## 🗓️ Rangos de Fechas en Tablas Operacionales

| Tabla | Primera fecha | Última fecha | Observación |
|---|---|---|---|
| `Despachos` | 2025-01-09 | 2026-05-22 | 17 meses de datos de prueba |
| `OrdenViaje` | 2025-08-31 | 2026-05-07 | 9 meses de datos de prueba |
| `SeguimientoExportacion` | 2026-05-12 | 2026-05-13 | Datos recientes, todos COMPLETADO |
| `Indicadores` | 2025-01-06 | — | 1507 registros de KPIs desde enero 2025 |

---

## ⚠️ Objetos Sospechosos Detectados

| Objeto | Tipo | Razón de alerta |
|---|---|---|
| `sp_PruebaDespacho` | Stored Procedure | Nombre contiene "Prueba" — SP de desarrollo |
| `CrearCPICTemporal` | Stored Procedure | Nombre contiene "Temporal" |
| `SeguimientoExportacion_Backup_20260512_115847` | Tabla | Tabla de backup — NO tiene PK — 5,899 registros |
| `SeguimientoExportacion_Backup_20260512_120720` | Tabla | Tabla de backup — NO tiene PK — 409 registros |
| Usuarios `admin`, `operador`, `STEPHANYSGV` | Datos | Podrían ser usuarios de prueba — revisar |
