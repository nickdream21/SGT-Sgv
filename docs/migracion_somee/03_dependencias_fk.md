# 🔗 Mapa de Dependencias y Claves Foráneas
**Base de datos:** `sgvActualizada`  
**Total FKs analizadas:** 111  
**Fecha:** 2026-05-22

---

## 🗺️ Árbol de Dependencias por Módulo

Este árbol define el **orden correcto de inserción** para migraciones y el **orden correcto de eliminación** para limpiezas.

### Orden de Inserción (de independiente → dependiente)

```
NIVEL 0 — Sin dependencias (insertar primero)
├── Roles
├── TipoCarro
├── LugarAbastecimiento
├── FormatoControlado
├── Pais
├── EstacionesPeaje
├── Lugares
├── Conductor
├── Tracto
├── camionetas
├── volquetes
├── Operadores
└── ClientesObra

NIVEL 1 — Dependen de Nivel 0
├── Departamento → Pais
├── Planta (independiente)
├── Cliente (independiente)
├── EquiposMaquinaria (independiente)
├── Obras → ClientesObra
└── Usuarios → Conductor, Operadores

NIVEL 2 — Dependen de Nivel 0-1
├── Provincia → Departamento
├── Ruta → Cliente
├── Producto → Cliente
├── PlantaCarga → Cliente
├── PlantaDescarga → Cliente
├── Carreta (independiente)
└── FirmaDigital (auto-referencia idFirmaAnulada → idFirma)

NIVEL 3 — Dependen de Nivel 0-2
├── Distrito → Provincia
├── Factura → Cliente
├── Liquidaciones (semi-independiente)
└── ViajesEnProgreso → Conductor, OrdenViaje(nullable)

NIVEL 4 — Dependen de Nivel 0-3
├── CPIC → Factura
├── OrdenViaje → Conductor, Tracto, Carreta, Cliente, Producto,
│               ViajesEnProgreso, FirmaDigital, Usuarios, CPIC
├── SubTramos → Liquidaciones
└── IngresoCombustibleEcuador → Conductor, Tracto, ViajesEnProgreso

NIVEL 5 — Dependen de Nivel 0-4
├── Despachos → Conductor, Tracto, Carreta, Cliente, CPIC, Factura,
│               OrdenViaje, ViajesEnProgreso, Producto
├── AsignacionesMaquinaria → Operadores, EquiposMaquinaria, Obras
├── CPIC_Productos → CPIC, Producto
├── DocumentosCPIC → CPIC
├── DocumentosFactura → Factura
├── GuiasTransportista → OrdenViaje
├── Manifiesto → CPIC, GuiasTransportista
├── OperacionesSubTramo → SubTramos, Cliente, CPIC, Factura, PlantaCarga, PlantaDescarga
├── SegmentosOrdenViaje → OrdenViaje, Cliente, CPIC, Factura
└── Liquidaciones → (OrdenViaje via SubTramos)

NIVEL 6 — Dependen de Nivel 0-5
├── AbastecimientoCombustible → Tracto, Conductor, Carreta, LugarAbastecimiento,
│                               Ruta, TipoCarro, OrdenViaje, camionetas, volquetes
├── CategoriasAdicionales → OrdenViaje
├── DescuentosReintegros → OrdenViaje
├── DetalleCombustible → OrdenViaje
├── DetalleAlimentacion → OrdenViaje
├── DetalleApoyoSeguridad → OrdenViaje
├── DetalleEncapada → OrdenViaje
├── DetalleHospedaje → OrdenViaje
├── DetalleMovilidad → OrdenViaje
├── DetalleOrdenViaje → GuiasTransportista, Producto
├── DetallePeajes → OrdenViaje
├── DetalleReparacionesVarios → OrdenViaje
├── DetalleSegmento → SegmentosOrdenViaje, Producto
├── DetalleTicketEcuador → IngresoCombustibleEcuador
├── Egresos → OrdenViaje
├── Ingresos → OrdenViaje
├── IngresosAdicionales → OrdenViaje
├── OrdenViajeAjuste → OrdenViaje, FirmaDigital
├── PartesDiariosTrabajo → AsignacionesMaquinaria, Operadores
├── ProductosOperacion → OperacionesSubTramo, Producto
├── DespachoCombustibleObra → AbastecimientoCombustible, Conductor, Obras, Tracto
└── UploadHistory (sin FKs)

NIVEL 7 — Dependen de Nivel 0-6
├── Auditoria (escrita por triggers de otras tablas)
├── AuditoriaLog (escrita por triggers de otras tablas)
└── SeguimientoExportacion (semi-independiente, sin FK explícita)
```

---

## 📋 Listado Completo de FKs (111 relaciones)

### AbastecimientoCombustible (15 FKs)
| FK | Columna local | Tabla referenciada | Columna |
|---|---|---|---|
| `FK_Abast_Camioneta` | `idCamioneta` | `camionetas` | `id` |
| `FK__Abastecim__idCar__*` | `idCarreta` | `Carreta` | `idCarreta` |
| `FK__Abastecim__idCon__*` | `idConductor` | `Conductor` | `idConductor` |
| `FK__Abastecim__idLug__*` | `idLugarAbastecimiento` | `LugarAbastecimiento` | `idLugarAbastecimiento` |
| `FK__Abastecim__idOrd__*` | `idOrdenViaje` | `OrdenViaje` | `idOrdenViaje` |
| `FK__Abastecim__idRut__*` | `idRuta` | `Ruta` | `idRuta` |
| `FK_Abastecimiento_TipoCarro` | `idTipoCarro` | `TipoCarro` | `idTipoCarro` |
| `FK__Abastecim__idTra__*` | `idTracto` | `Tracto` | `idTracto` |
| `FK_Abast_Volquete` | `idVolquete` | `volquetes` | `id` |

> ⚠️ **Nota:** Existen FKs duplicadas en `AbastecimientoCombustible` (con nombres generados automáticamente `__*` además de las nombradas). Indicativo de una re-creación de FK sin eliminar la original. **Verificar en nueva BD que no existan duplicados.**

### AsignacionesMaquinaria (3 FKs)
| FK | Columna | Referencia |
|---|---|---|
| `FK_Asignacion_Equipo` | `idEquipo` | `EquiposMaquinaria.idEquipo` |
| `FK_Asignacion_Obra` | `idObra` | `Obras.idObra` |
| `FK_Asignacion_Operador` | `idOperador` | `Operadores.idOperador` |

### Despachos (9 FKs)
| FK | Columna | Referencia |
|---|---|---|
| `FK_Despachos_Carreta` | `idCarreta` | `Carreta.idCarreta` |
| `FK_Despachos_Cliente` | `idCliente` | `Cliente.idCliente` |
| `FK_Despachos_Conductor` | `idConductor` | `Conductor.idConductor` |
| `FK_Despachos_CPIC` | `idCPIC` | `CPIC.idCPIC` |
| `FK_Despachos_Factura` | `idFactura` | `Factura.idFactura` |
| `FK_Despachos_OrdenViaje` | `idOrdenViaje` | `OrdenViaje.idOrdenViaje` |
| `FK_Despachos_Producto` | `idProducto` | `Producto.idProducto` |
| `FK_Despachos_Tracto` | `idTracto` | `Tracto.idTracto` |
| `FK_Despachos_ViajeProgreso` | `idViajeProgreso` | `ViajesEnProgreso.idViajeProgreso` |

### OrdenViaje (13 FKs — tabla más relacionada)
| FK | Columna | Referencia |
|---|---|---|
| `FK__OrdenViaj__idCar__*` | `idCarreta` | `Carreta.idCarreta` |
| `FK__OrdenViaj__idCli__*` | `idCliente` | `Cliente.idCliente` |
| `FK__OrdenViaj__idCon__*` | `idConductor` | `Conductor.idConductor` |
| `FK_OrdenViaje_CPIC` | `idCPIC` | `CPIC.idCPIC` |
| `FK_OrdenViaje_FirmaConductor` | `idFirmaConductor` | `FirmaDigital.idFirma` |
| `FK_OrdenViaje_FirmaAdmin` | `idFirmaAdmin` | `FirmaDigital.idFirma` |
| `FK__OrdenViaj__idPro__*` | `idProducto` | `Producto.idProducto` |
| `FK__OrdenViaj__idTra__*` | `idTracto` | `Tracto.idTracto` |
| `FK_OrdenViaje_UsuarioRegistro` | `idUsuarioRegistro` | `Usuarios.idUsuario` |
| `FK_OrdenViaje_UsuarioAprobacion` | `idUsuarioAprobacion` | `Usuarios.idUsuario` |
| `FK_OrdenViaje_ViajesEnProgreso` | `idViajeProgreso` | `ViajesEnProgreso.idViajeProgreso` |

### FirmaDigital (1 FK auto-referencia)
| FK | Columna | Referencia |
|---|---|---|
| `FK_FirmaDigital_Anulada` | `idFirmaAnulada` | `FirmaDigital.idFirma` |

---

## ⚠️ FKs con ON DELETE CASCADE (5 casos — atención especial)

Estas FKs tienen `ON DELETE CASCADE`, lo que significa que **borrar el padre elimina automáticamente los hijos**.

| Tabla hijo | Tabla padre | FK | Riesgo |
|---|---|---|---|
| `DetalleTicketEcuador` | `IngresoCombustibleEcuador` | `FK_TicketEc_Ingreso` | Bajo (ambas vacías) |
| `DocumentosCPIC` | `CPIC` | `FK_DocumentosCPIC_CPIC` | Medio (14 docs ligados a 57 CPICs) |
| `DocumentosFactura` | `Factura` | `FK_DocumentosFactura_Factura` | Medio (17 docs ligados a 33 facturas) |
| `OperacionesSubTramo` | `SubTramos` | `FK_OperacionesSubTramo_SubTramo` | Bajo (1 registro) |
| `ProductosOperacion` | `OperacionesSubTramo` | `FK_ProductosOperacion_Operacion` | Bajo (2 registros) |

> ✅ **En el script de limpieza,** no se necesita limpiar manualmente `DocumentosCPIC` ni `DocumentosFactura` antes de limpiar `CPIC` y `Factura` — el CASCADE lo gestiona. Sin embargo, se eliminan explícitamente primero por legibilidad.

---

## 🔄 Diagrama de Módulos con sus Tablas Relacionadas

```
MÓDULO TRANSPORTE (núcleo)
  Conductor ──────────────────────────────────┐
  Tracto ──────────────────────────────────────┤
  Carreta ─────────────────────────────────────┤
  Cliente ──────────────────────────────────── Despachos
  CPIC ────────────────────────────────────────┤
  Factura ─────────────────────────────────────┤
  ViajesEnProgreso ────────────────────────────┘
       │
       ▼
    OrdenViaje
       │
       ├── FirmaDigital (append-only)
       ├── OrdenViajeAjuste
       ├── Egresos / Ingresos / IngresosAdicionales
       ├── DescuentosReintegros
       ├── DetalleCombustible / DetallePeajes
       ├── DetalleAlimentacion / DetalleHospedaje / ...
       └── CategoriasAdicionales

MÓDULO GRIFO
  AbastecimientoCombustible → Tracto, Conductor, Carreta,
                              LugarAbastecimiento, Ruta, TipoCarro,
                              camionetas, volquetes, OrdenViaje

MÓDULO MAQUINARIA
  Operadores → Usuarios
  EquiposMaquinaria
  Obras → ClientesObra
  AsignacionesMaquinaria → Operadores, EquiposMaquinaria, Obras
  PartesDiariosTrabajo → AsignacionesMaquinaria, Operadores

MÓDULO EXPORTACIÓN
  SeguimientoExportacion (semi-independiente, sin FK a otras tablas)

MÓDULO AUDITORÍA (escritura por triggers)
  Auditoria ← triggers de tablas de negocio
  AuditoriaLog ← triggers modernos
  FirmaDigital ← FirmaService.cs (append-only)
```

---

## 📋 Orden Recomendado de Limpieza (DELETE en nueva BD)

Para limpiar datos en orden correcto respetando FKs (de más dependiente a menos dependiente):

```
1. DetalleTicketEcuador
2. DetalleSegmento
3. ProductosOperacion
4. OperacionesSubTramo
5. DetalleOrdenViaje
6. DetalleCombustible, DetalleAlimentacion, DetalleApoyoSeguridad,
   DetalleEncapada, DetalleHospedaje, DetalleMovilidad,
   DetalleReparacionesVarios, DetallePeajes, DetalleEncapada
7. Egresos, Ingresos, IngresosAdicionales, DescuentosReintegros
8. CategoriasAdicionales
9. GuiasTransportista (→ luego DetalleOrdenViaje ya fue)
10. PartesDiariosTrabajo
11. DespachoCombustibleObra
12. AbastecimientoCombustible
13. OrdenViajeAjuste
14. SegmentosOrdenViaje
15. Manifiesto
16. OrdenViaje (una vez limpiadas todas las tablas hijas)
17. Despachos
18. ViajesEnProgreso
19. IngresoCombustibleEcuador
20. SubTramos (→ OperacionesSubTramo ya fue)
21. Liquidaciones
22. DocumentosCPIC (cascade desde CPIC, pero eliminar explícito)
23. DocumentosFactura (cascade desde Factura, pero eliminar explícito)
24. CPIC_Productos
25. CPIC
26. Factura
27. AsignacionesMaquinaria
28. SeguimientoExportacion
29. UploadHistory
30. AuditoriaLog, Auditoria (últimas — logs del proceso de limpieza)
31. FirmaDigital (últimas — requiere deshabilitar temporalmente triggers)
```
