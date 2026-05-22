# 🏷️ Clasificación de Tablas por Categoría de Migración
**Base de datos:** `sgvActualizada`  
**Fecha:** 2026-05-22  
**Modo:** SOLO LECTURA — clasificación basada en análisis de estructura y datos

---

## Leyenda de Categorías

| Categoría | Símbolo | Acción en nueva BD |
|---|---|---|
| A — Estructura + Datos a Conservar | 🟢 | Migrar con datos |
| B — Estructura conservar, Datos limpiar | 🔴 | Crear estructura, dejar vacía |
| C — Auditoría / Firma / Trazabilidad | 🟡 | Crear estructura exacta, dejar vacía |
| D — Requiere validación humana | 🟠 | NO tocar sin autorización explícita |
| X — Eliminar de nueva BD | ⛔ | No migrar estructura ni datos |

---

## 🟢 CATEGORÍA A — Estructura + Datos Maestros a Conservar

Estas tablas contienen catálogos o datos de referencia necesarios para que el sistema funcione. **Deben migrarse con sus datos reales.**

| Tabla | Registros | Justificación | Dependencias previas |
|---|---|---|---|
| `Roles` | 6 | Catálogo de roles del sistema — 6 roles definidos | Ninguna |
| `TipoCarro` | 6 | Catálogo de tipos de vehículo | Ninguna |
| `LugarAbastecimiento` | 2 | Catálogo de puntos de combustible | Ninguna |
| `FormatoControlado` | 2 | Formatos ISO/BASC (F-05, F-06) — requeridos para PDFs | Ninguna |
| `Pais` | 0 | Catálogo geográfico (actualmente vacío) | Ninguna |
| `Departamento` | 0 | Catálogo geográfico (actualmente vacío) | `Pais` |
| `Provincia` | 0 | Catálogo geográfico (actualmente vacío) | `Departamento` |
| `Distrito` | 0 | Catálogo geográfico (actualmente vacío) | `Provincia` |
| `EstacionesPeaje` | 31 | Catálogo de estaciones de peaje — datos reales de rutas Perú/Ecuador | Ninguna |
| `Lugares` | 7 | Catálogo de lugares geográficos operativos | Ninguna |
| `Planta` | 6 | Plantas de carga/descarga (Lima, Trujillo, Chiclayo, Manta, Guayaquil, Quito) | Ninguna |
| `Cliente` | 4 | Clientes de la empresa (Vitapro, Novopan, SEGAFEX, CAMAFRA) | Ninguna |
| `Producto` | 5 | Productos de Vitapro (alimentos para camarón) | `Cliente` |
| `PlantaCarga` | 2 | Plantas de carga específicas por cliente | `Cliente` |
| `PlantaDescarga` | 5 | Plantas de descarga específicas por cliente | `Cliente` |
| `Ruta` | 2 | Rutas predefinidas (Sullana-Trujillo, Sullana-Guayaquil) | `Cliente` |
| `Conductor` | 85 | **85 conductores reales** con DNI y datos personales | Ninguna |
| `Tracto` | 100 | **100 tractos activos** con placas reales | Ninguna |
| `Carreta` | 134 | **134 semiremolques activos** con placas reales | Ninguna |
| `camionetas` | 23 | Flota de camionetas | Ninguna |
| `volquetes` | 40 | Flota de volquetes | Ninguna |
| `Operadores` | 1 | Operadores de maquinaria (módulo nuevo) | Ninguna |
| `EquiposMaquinaria` | 1 | Equipos de maquinaria pesada | Ninguna |
| `ClientesObra` | 1 | Clientes de obras (maquinaria) | Ninguna |
| `Obras` | 1 | Obras/proyectos de maquinaria | `ClientesObra` |

> ⚠️ **Nota sobre `Conductor`:** La columna `licencia` no existe — el esquema usa `DNI`, `nombre`, `apPaterno`, `apMaterno`, `fechaNacimiento`, `direccion`, `telefono`, `correo`, `carnetExtranjeria`, `activo`. Verificar antes de copiar.

---

## 🔴 CATEGORÍA B — Estructura a Conservar, Datos a Limpiar

Estas tablas contienen registros de prueba o históricos de desarrollo. **La estructura (DDL) debe migrarse, pero los datos deben quedar vacíos en la nueva base de producción.**

### Transaccionales principales
| Tabla | Registros | Justificación |
|---|---|---|
| `Despachos` | 308 | Despachos desde 2025-01-09 — registros de prueba/piloto |
| `ViajesEnProgreso` | 74 | Viajes asociados a despachos de prueba |
| `OrdenViaje` | 32 | Órdenes de viaje de prueba (ago 2025 — may 2026) |
| `OrdenViajeAjuste` | 0 | Vacía — solo estructura |
| `Liquidaciones` | 1 | Un solo registro — dato de prueba |
| `SubTramos` | 1 | Vinculado a la liquidación de prueba |
| `OperacionesSubTramo` | 1 | Vinculado a sub-tramo de prueba |
| `ProductosOperacion` | 2 | Vinculado a operación de prueba |
| `CPIC` | 57 | CPICs ligados a despachos de prueba |
| `CPIC_Productos` | 6 | Productos de CPICs de prueba |
| `Factura` | 33 | Facturas ligadas a despachos de prueba |
| `DocumentosCPIC` | 14 | Archivos adjuntos de CPICs de prueba |
| `DocumentosFactura` | 17 | Archivos adjuntos de facturas de prueba |
| `AsignacionesMaquinaria` | 1 | Una asignación de prueba |
| `PartesDiariosTrabajo` | 0 | Vacía — solo estructura |
| `DespachoCombustibleObra` | 0 | Vacía — solo estructura |

### Detalles de OrdenViaje (todos vacíos o mínimos)
| Tabla | Registros | Justificación |
|---|---|---|
| `AbastecimientoCombustible` | 4 | 4 registros de prueba del grifo |
| `Egresos` | 32 | Egresos de órdenes de viaje de prueba |
| `Ingresos` | 31 | Ingresos de órdenes de viaje de prueba |
| `IngresosAdicionales` | 7 | Ingresos adicionales de prueba |
| `DescuentosReintegros` | 15 | Descuentos de órdenes de prueba |
| `DetalleCombustible` | 2 | Detalles de combustible de prueba |
| `DetallePeajes` | 55 | Peajes de órdenes de viaje de prueba |
| `DetalleReparacionesVarios` | 3 | Reparaciones de prueba |
| `DetalleAlimentacion` | 0 | Vacía |
| `DetalleApoyoSeguridad` | 0 | Vacía |
| `DetalleEncapada` | 0 | Vacía |
| `DetalleHospedaje` | 0 | Vacía |
| `DetalleMovilidad` | 0 | Vacía |
| `DetalleOrdenViaje` | 0 | Vacía |
| `DetalleSegmento` | 0 | Vacía |
| `DetalleTicketEcuador` | 0 | Vacía |
| `GuiasTransportista` | 0 | Vacía |
| `Manifiesto` | 0 | Vacía |
| `SegmentosOrdenViaje` | 0 | Vacía |
| `IngresoCombustibleEcuador` | 0 | Vacía |
| `UploadHistory` | 0 | Vacía |

### Seguimiento de Exportaciones
| Tabla | Registros | Justificación |
|---|---|---|
| `SeguimientoExportacion` | 679 | Registros de mayo 2026 (todos COMPLETADO) — probable prueba de carga masiva |

> ⚠️ **Nota:** Los 679 registros de `SeguimientoExportacion` son todos de estado COMPLETADO y fechas 12-13 mayo 2026. Probablemente son un lote de prueba de la funcionalidad recién implementada. **Confirmar con el usuario si deben migrarse o limpiarse.**

---

## 🟡 CATEGORÍA C — Auditoría / Firma / Trazabilidad

Tablas con integridad documental y no-repudio. **Su estructura, triggers y restricciones deben copiarse exactamente. En la nueva BD de producción deben iniciar vacías.**

| Tabla | Registros | Descripción | Restricción especial |
|---|---|---|---|
| `FirmaDigital` | 9 | Firmas digitales biométricas con hash SHA-256 | **APPEND-ONLY** via 2 triggers INSTEAD OF |
| `Auditoria` | 207 | Log de auditoría legacy (tabla original) | Triggers de otras tablas escriben aquí |
| `AuditoriaLog` | 199 | Log de auditoría moderno con 4 índices secundarios | Trigger `TR_*_Auditoria` de 12 tablas |

> 🔴 **CRÍTICO — FirmaDigital:**
> - Tiene 2 triggers `INSTEAD OF DELETE` y `INSTEAD OF UPDATE` que deben recrearse antes de insertar datos.
> - Auto-referencia: `idFirmaAnulada → idFirma` (la anulación de firma se registra como nueva fila, nunca borrando la original).
> - `imagenTrazoPng` es `varbinary` — puede contener datos PNG grandes.
> - En producción limpia: debe estar vacía, pero con todos los constraints y triggers activos.

---

## 🟠 CATEGORÍA D — Requiere Validación Humana

Estas tablas no se pueden clasificar con certeza sin decisión del propietario del sistema.

### `Usuarios` (96 registros)
**¿Por qué revisar?**  
Contiene 96 usuarios: 88 conductores (por DNI), 3 administradores, 2 administradores de grifo, 1 operador, más usuarios potencialmente de prueba:
- `idUsuario=1` → `admin` / rol: `Administrador` — ⚠️ ¿Es usuario real o de prueba?
- `idUsuario=2` → `operador` / rol: `Operador` — ⚠️ Nombre genérico, probablemente de prueba
- `idUsuario=3` → `STEPHANYSGV` / rol: `Usuario` — ⚠️ Rol desconocido `Usuario`, no aparece en tabla Roles
- `idUsuario=89-91` → `47876756`, `75020176`, `46328546` con rol `Administrador` — ⚠️ Verificar si son reales
- `idUsuario=94-98` → cuentas especializadas (`adminsistema`, `grifo_admin`, `admin.grifo`, etc.)

**Decisión requerida:** ¿Cuáles usuarios deben migrarse a producción? ¿Cuáles son de prueba?

### `Indicadores` (1507 registros)
**¿Por qué revisar?**  
- 1507 registros desde enero 2025 con números de pedido reales (ej: `4400104085`), nombres de conductores y placas de tractos.
- Tiene 37 columnas — estructura muy amplia con datos históricos de KPIs.
- Trigger activo: `TR_Indicadores_Auditoria`.
- **Podrían ser datos reales de operaciones** que deban conservarse, o bien datos de un periodo piloto.
- **Decisión:** ¿Migrar los 1507 registros o limpiar y comenzar desde cero?

### `CategoriasAdicionales` (13 registros)
**¿Por qué revisar?**  
- Está relacionada con `OrdenViaje` via `numeroOrdenViaje`.
- Los 13 registros podrían ser categorías configurables (catálogo) o datos operacionales ligados a órdenes de viaje de prueba.
- Si son catálogo configurable → Categoría A.
- Si son datos transaccionales → Categoría B.

---

## ⛔ CATEGORÍA X — Eliminar de Nueva BD (no migrar)

| Tabla | Registros | Justificación |
|---|---|---|
| `SeguimientoExportacion_Backup_20260512_115847` | 5,899 | **Tabla de backup temporal** creada el 12/05/2026 a las 11:58 — no tiene PK |
| `SeguimientoExportacion_Backup_20260512_120720` | 409 | **Tabla de backup temporal** creada el 12/05/2026 a las 12:07 — no tiene PK |

> Estas tablas son backups intermedios creados durante el desarrollo del módulo de exportación. No deben migrarse a la nueva base de producción.

---

## 📋 Tabla Resumen de Clasificación

| Categoría | Cantidad | Total registros |
|---|---|---|
| 🟢 A — Migrar con datos | 25 tablas | ~550 registros de maestros |
| 🔴 B — Limpiar datos | 37 tablas | ~1,300 registros de prueba |
| 🟡 C — Estructura exacta, vacías | 3 tablas | 415 registros de auditoría |
| 🟠 D — Validación humana requerida | 3 tablas | ~1,616 registros |
| ⛔ X — No migrar | 2 tablas | 6,308 registros |
| **TOTAL** | **71 tablas** | **~10,189 registros** |
