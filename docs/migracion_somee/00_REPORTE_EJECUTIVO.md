# 🧭 Reporte Ejecutivo — Plan de Migración SGT-SGV a Somee Premium
**Fecha:** 2026-05-22  
**Propietario:** SERVICIOS GENERALES VIVIANA E.I.R.L.  
**Base origen:** `sgvActualizada` (Somee estándar)  
**Base destino:** Nueva cuenta Somee Premium  
**Estado:** ✅ Análisis completado — Pendiente ejecución con aprobación

---

## 📊 Resumen de la Base de Datos Analizada

| Elemento | Valor |
|---|---|
| Total de tablas | **71** |
| Total de Stored Procedures | **135** |
| Total de Triggers | **38** |
| Total de Claves Foráneas | **111** |
| Índices secundarios | **77** |
| Tablas sin PK | **3** (2 backups + CPIC_Productos) |

---

## ✅ Tablas que DEFINITIVAMENTE Deben Conservar Datos (Categoría A)

Estas tablas contienen información real y deben migrarse a la nueva base.

| Tabla | Registros | Datos |
|---|---|---|
| `Conductor` | 85 | Conductores reales con DNI y datos personales |
| `Tracto` | 100 | 100 cabezales/tractores activos con placas reales |
| `Carreta` | 134 | 134 semiremolques activos |
| `camionetas` | 23 | Flota de camionetas |
| `volquetes` | 40 | Flota de volquetes |
| `Cliente` | 4 | Vitapro, Novopan, SEGAFEX, CAMAFRA |
| `Producto` | 5 | Productos NICOVITA (Vitapro) |
| `EstacionesPeaje` | 31 | Peajes de Perú y Ecuador |
| `Roles` | 6 | 6 roles del sistema |
| `FormatoControlado` | 2 | Formatos F-05 y F-06 (PDF obligatorios) |
| `TipoCarro` | 6 | Tipos de vehículo |
| `LugarAbastecimiento` | 2 | Puntos de combustible |
| `Planta` | 6 | Plantas Lima, Trujillo, Chiclayo, Manta, Guayaquil, Quito |
| `PlantaCarga` | 2 | Plantas de carga por cliente |
| `PlantaDescarga` | 5 | Plantas de descarga por cliente |
| `Ruta` | 2 | Sullana-Trujillo y Sullana-Guayaquil |
| `Lugares` | 7 | Catálogo de lugares operativos |
| `Operadores` | 1 | Operador de maquinaria |
| `EquiposMaquinaria` | 1 | Equipo de maquinaria |
| `ClientesObra` | 1 | Cliente de obra |
| `Obras` | 1 | Obra/proyecto |
| `Pais`, `Departamento`, `Provincia`, `Distrito` | 0 cada una | Vacías ahora, estructuras necesarias |

---

## 🔴 Tablas que Deben Quedar VACÍAS en Producción (Categoría B)

Estas tablas contienen datos de prueba generados durante el desarrollo.

| Grupo | Tablas principales | Registros a limpiar |
|---|---|---|
| **Despachos** | `Despachos`, `ViajesEnProgreso` | 308 + 74 = **382** |
| **Órdenes de viaje** | `OrdenViaje`, todos los `Detalle*` | 32 + varios = **~100** |
| **Finanzas viaje** | `Egresos`, `Ingresos`, `IngresosAdicionales`, `DescuentosReintegros` | 85 |
| **Documentos** | `CPIC`, `Factura`, `DocumentosCPIC`, `DocumentosFactura` | 121 |
| **Combustible** | `AbastecimientoCombustible` | 4 |
| **Maquinaria** | `AsignacionesMaquinaria`, `Liquidaciones`, `SubTramos` | 3 |
| **Vacías** | 20+ tablas con 0 registros | 0 (solo estructura) |

---

## 🟠 Tablas que Requieren Tu Decisión

### 1. `Usuarios` (96 registros)
**Problema:** Hay usuarios que parecen de prueba mezclados con usuarios reales.

| Decisión necesaria | Usuarios en duda |
|---|---|
| ¿`admin` (id=1) es real o de prueba? | Nombre genérico, rol `Administrador` (no coincide exactamente con tabla Roles) |
| ¿`operador` (id=2) es real o de prueba? | Nombre genérico |
| ¿`STEPHANYSGV` (id=3) es válido? | Tiene rol `Usuario` que no existe en tabla `Roles` |
| ¿Los 88 conductores (DNI como username) son todos válidos? | Parecen reales — deberían migrarse |

**Recomendación:** Migrar solo los usuarios con DNI como username (conductores) y crear manualmente los usuarios administrativos en producción con contraseñas seguras nuevas.

### 2. `Indicadores` (1507 registros)
**Problema:** 1507 registros de KPIs desde enero 2025 con datos que parecen reales (números de pedido, conductores reales, placas reales).

- **Opción A:** Migrarlos como histórico de operaciones reales ✅
- **Opción B:** No migrarlos y empezar desde cero en producción

**Recomendación:** Consultar con el equipo operativo si estos son datos de producción real o de un piloto.

### 3. `SeguimientoExportacion` (679 registros)
**Situación:** 679 registros, todos con estado `COMPLETADO`, registrados el 12-13 de mayo 2026. Hay además 2 tablas de backup de esas fechas con 5,899 + 409 registros adicionales.

- El hecho de que todo esté `COMPLETADO` y sean de la misma semana sugiere que fueron datos de prueba de carga.
- Las tablas de backup (`_Backup_20260512_*`) son evidencia de que se importaron datos de forma experimental.

**Recomendación:** **NO migrar** los 679 registros ni las tablas de backup. Empezar desde cero en producción.

---

## ⚠️ Riesgos Detectados

| Riesgo | Nivel | Descripción | Mitigación |
|---|---|---|---|
| **FK duplicadas en AbastecimientoCombustible** | Medio | Hay FKs con nombre automático `FK__Abastecim__*` que duplican FKs nombradas. Indica re-creación sin limpieza. | Al generar el DDL para nueva BD, limpiar las FK duplicadas |
| **Usuarios con rol `Usuario`** | Medio | `STEPHANYSGV` tiene rol `Usuario` que no existe en tabla `Roles`. El sistema podría fallar o redirigir mal. | No migrar ese usuario; revisar si el rol debía ser diferente |
| **SP `sp_PruebaDespacho`** | Bajo | SP con nombre de prueba que podría confundir en producción | No eliminarlo, pero documentarlo como SP de desarrollo |
| **Tablas de backup en BD** | Bajo | `SeguimientoExportacion_Backup_*` sin PK ocupan ~6,300 registros innecesarios | No migrar a nueva BD |
| **Triggers de auditoría deshabilitados** | Alto | Si el script de limpieza falla a mitad, los triggers podrían quedar deshabilitados | El script incluye rehabilitación en el bloque CATCH |
| **FirmaDigital en origen tiene 9 registros** | Informativo | Son firmas de documentos de prueba. No migrar a producción. | El script las limpia correctamente |
| **connectionStrings.config** | Crítico | Cambiar la cadena de conexión apunta el sistema a la nueva BD. Acción irreversible sin rollback manual. | Conservar el valor anterior anotado antes de cambiar |

---

## 📋 Orden Recomendado de Migración

```
1. ✅ Backup de sgvActualizada (Somee actual)
2. ✅ Generar DDL completo con SSMS (estructura, SPs, triggers, índices)
3. 👤 Crear nueva BD en cuenta Somee Premium
4. ✅ Ejecutar DDL en nueva BD (sin las 2 tablas de backup)
5. 👤 Decidir sobre Usuarios, Indicadores y SeguimientoExportacion
6. ✅ Ejecutar 06_script_migracion_datos_maestros.sql
7. ✅ Ejecutar 08_script_validacion_post_migracion.sql
8. ✅ Pruebas funcionales completas (login, despacho, firma, PDF)
9. 👤 Actualizar connectionStrings.config
10. ✅ Republicar la app en Somee Premium
11. ✅ Segunda ronda de pruebas funcionales en ambiente premium
12. 👤 Apuntar dominio final
```

---

## ✅ Checklist ANTES de Cambiar el ConnectionString

- [ ] Backup completo de `sgvActualizada` descargado localmente
- [ ] PDFs de `~/App_Data/OrdenesViaje/` respaldados
- [ ] Nueva BD creada y accesible en Somee Premium
- [ ] DDL ejecutado sin errores (71-2 tablas, 135 SPs, 38 triggers)
- [ ] Datos maestros verificados (85 conductores, 100 tractos, 134 carretas, etc.)
- [ ] Test de login exitoso con cada rol
- [ ] Test de creación de despacho exitoso
- [ ] Test de firma digital exitoso
- [ ] Test de generación de PDF exitoso
- [ ] Triggers de FirmaDigital activos (INSTEAD OF DELETE y UPDATE)
- [ ] FormatoControlado con SGV-CDF-F-05 y SGV-CDF-F-06 activos
- [ ] `connectionStrings.config` anterior anotado (para posible rollback)
- [ ] Decisión tomada sobre Usuarios, Indicadores y SeguimientoExportacion

---

## ✅ Checklist DESPUÉS de Publicar en Somee Premium

- [ ] Login con rol ADMIN funciona y redirige a Dashboard correcto
- [ ] Login con rol CONDUCTOR funciona y redirige a DashboardConductor
- [ ] Login con rol ADMINISTRADOR DE GRIFO redirige a DashboardGrifo
- [ ] Login con rol OPERADOR redirige a DashboardOperador
- [ ] Crear nuevo Despacho → se genera número correlativo
- [ ] Crear nueva Orden de Viaje → se genera número `OV-YYYY-NNNNNN`
- [ ] Firma digital del conductor → PDF generado con hash SHA-256
- [ ] Descarga de PDF firmado funciona desde el navegador
- [ ] Aprobación admin → segunda firma insertada en FirmaDigital
- [ ] Intentar UPDATE en FirmaDigital → debe fallar (trigger INSTEAD OF)
- [ ] Registrar Abastecimiento → se genera número correlativo + PDF F-06
- [ ] AuditoriaLog registra cada operación automáticamente
- [ ] Dashboard de Exportación carga sin errores (con tabla vacía)
- [ ] Reportes Excel se generan sin error
- [ ] Recuperación de contraseña por email funciona (SMTP configurado)
- [ ] No hay errores 500 en ninguna pantalla con rol normal de uso

---

## 📂 Archivos Generados en `docs/migracion_somee/`

| Archivo | Descripción |
|---|---|
| `00_REPORTE_EJECUTIVO.md` | Este documento |
| `01_inventario_bd_actual.md` | Inventario completo: 71 tablas, 135 SPs, 38 triggers |
| `02_clasificacion_tablas.md` | Clasificación A/B/C/D/X de las 71 tablas |
| `03_dependencias_fk.md` | Árbol de dependencias y orden de inserción/limpieza |
| `04_plan_migracion_produccion.md` | Plan en 9 fases con checkpoints y estimación de tiempos |
| `05_script_validacion_pre_migracion.sql` | Inspección completa de la BD origen (SOLO LECTURA) |
| `06_script_migracion_datos_maestros.sql` | Script INSERT de datos maestros con TRY/CATCH y ROLLBACK |
| `07_script_limpieza_transaccional_SOLO_NUEVA_BD.sql` | Limpieza de datos de prueba — con guardia de seguridad anti-equivocación |
| `08_script_validacion_post_migracion.sql` | 10 tests de validación post-migración |

---

> **Nota final:** La base de datos original `sgvActualizada` no fue modificada en ningún momento durante este análisis. Todos los scripts destructivos están marcados y comentados. Ninguno debe ejecutarse sin revisión y aprobación explícita del propietario del sistema.
