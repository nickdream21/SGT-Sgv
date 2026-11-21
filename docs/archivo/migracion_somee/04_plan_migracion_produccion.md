# 📋 Plan de Migración a Producción — Somee Premium
**Proyecto:** SGT-SGV — SERVICIOS GENERALES VIVIANA E.I.R.L.  
**Origen:** `sgvActualizada` (Somee free/estándar)  
**Destino:** Nueva base de datos en cuenta Somee **Premium**  
**Fecha del plan:** 2026-05-22  
**Autor:** Análisis automatizado + revisión humana requerida en pasos marcados con 👤

---

## ⚠️ Principios de Oro de Esta Migración

1. **La base actual NO se toca.** Todos los cambios se hacen en la nueva base destino.
2. **Nada se destruye sin aprobación explícita del propietario.**
3. **Cada fase tiene un punto de verificación antes de continuar.**
4. **La migración es reversible hasta el paso 8 (cambio de connectionString).**
5. **Los scripts destructivos (Categoría 07) SOLO se ejecutan en la nueva base.**

---

## 🗺️ Fases de Migración

---

### FASE 1 — Backup Completo de la Base Actual
**Responsable:** Propietario del sistema + Somee  
**Riesgo:** Bajo  
**Reversible:** N/A (es el punto de recuperación)

**Acciones:**
1. Desde el panel de Somee (cuenta actual), generar backup `.bak` de `sgvActualizada`.
2. Descargar el archivo `.bak` a almacenamiento local seguro.
3. Verificar la integridad del backup (tamaño del archivo, fecha de creación).
4. Guardar también los archivos en `~/App_Data/OrdenesViaje/` (PDFs archivados).

**Checkpoints:**
- [ ] Backup `.bak` descargado y verificado
- [ ] PDFs archivados respaldados localmente
- [ ] Archivo `connectionStrings.config` respaldado

---

### FASE 2 — Generar Script DDL Completo de Estructura
**Responsable:** DBA / Desarrollador  
**Riesgo:** Bajo (solo lectura)  
**Herramienta recomendada:** SQL Server Management Studio (SSMS) → Tasks → Generate Scripts

**Acciones:**
1. Abrir SSMS → conectar a `sgvActualizada.mssql.somee.com`.
2. Clic derecho en BD `sgvActualizada` → Tasks → Generate Scripts.
3. Seleccionar: **Todos los objetos** (tablas, vistas, funciones, SPs, triggers, índices, constraints).
4. Opciones avanzadas:
   - Script for Server Version: **SQL Server 2019** (compatible con Somee)
   - Include IF NOT EXISTS: ✅
   - Script Indexes: ✅
   - Script Triggers: ✅
   - Script Foreign Keys: ✅
   - Script Primary Keys: ✅
   - Script Unique Keys: ✅
   - Script Data: ❌ (solo estructura)
5. Guardar como: `docs/migracion_somee/estructura_completa_DDL.sql`

**Verificar que el DDL incluya:**
- [ ] 71 tablas (excluyendo las 2 de backup si se desea)
- [ ] 135 stored procedures
- [ ] 38 triggers (especialmente `trg_FirmaDigital_NoDelete` y `trg_FirmaDigital_NoUpdate`)
- [ ] 77 índices secundarios
- [ ] 111 FK constraints

---

### FASE 3 — Crear Nueva Base de Datos en Somee Premium
**Responsable:** Propietario del sistema  
**Riesgo:** Bajo  
**👤 ACCIÓN MANUAL REQUERIDA**

**Acciones:**
1. Ingresar al panel de Somee Premium.
2. Crear nueva base de datos (ej: `sgvProduccion` o el nombre elegido).
3. Anotar:
   - Servidor: `_______.mssql.somee.com`
   - Base de datos: `_______`
   - Usuario SQL: `_______`
   - Contraseña: `_______`
4. Verificar que la versión de SQL Server de la nueva cuenta sea compatible (2019+).
5. Verificar límites: tamaño máximo de BD, número de conexiones, tiempo de timeout.

**Checkpoints:**
- [ ] Nueva BD creada y accesible
- [ ] Credenciales anotadas de forma segura
- [ ] Prueba de conexión desde SSMS exitosa

---

### FASE 4 — Restaurar Estructura en Nueva Base
**Responsable:** DBA / Desarrollador  
**Riesgo:** Medio  
**Herramienta:** SSMS o sqlcmd

**Acciones:**
1. Conectar SSMS a la nueva BD de Somee Premium.
2. Ejecutar `estructura_completa_DDL.sql` en el orden correcto:
   - Primero: tablas sin dependencias (Nivel 0-1 del árbol FK)
   - Luego: tablas con FK (Nivel 2-7)
   - Luego: índices secundarios
   - Luego: stored procedures y funciones
   - **Al final:** triggers (especialmente `trg_FirmaDigital_*`)
3. Verificar que no existan errores de creación.
4. **NO ejecutar** las tablas de backup: `SeguimientoExportacion_Backup_*`.

**Script de verificación post-estructura:**
```sql
-- Verificar conteo de objetos creados
SELECT 
    (SELECT COUNT(*) FROM sys.tables) AS tablas_creadas,
    (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0) AS sps_creados,
    (SELECT COUNT(*) FROM sys.triggers WHERE parent_class=1) AS triggers_creados,
    (SELECT COUNT(*) FROM sys.foreign_keys) AS fks_creadas;
-- Esperado: ~69 tablas, 135 SPs, 38 triggers, 111 FKs
```

**Checkpoints:**
- [ ] Todas las tablas creadas sin errores
- [ ] Todos los SPs creados
- [ ] Triggers de FirmaDigital activos y correctos
- [ ] FKs creadas correctamente (sin duplicados)
- [ ] Sin las tablas de backup

---

### FASE 5 — Migrar Datos Maestros (Categoría A)
**Responsable:** DBA / Desarrollador  
**Riesgo:** Bajo-Medio  
**Script:** `06_script_migracion_datos_maestros.sql`

**Orden de inserción:**
1. Roles, TipoCarro, LugarAbastecimiento, FormatoControlado, EstacionesPeaje, Lugares
2. Planta, Cliente
3. Producto, PlantaCarga, PlantaDescarga, Ruta
4. Conductor, Tracto, Carreta, camionetas, volquetes
5. Operadores, ClientesObra, EquiposMaquinaria
6. Obras
7. Usuarios (**solo tras decisión del propietario sobre usuarios de prueba**)

**👤 DECISIONES REQUERIDAS ANTES DE ESTA FASE:**
- [ ] ¿Qué usuarios migrar? (`admin`, `operador`, `STEPHANYSGV` — ¿son de prueba?)
- [ ] ¿Migrar `Indicadores` (1507 registros)?
- [ ] ¿Migrar `CategoriasAdicionales` (13 registros)?
- [ ] ¿Migrar `SeguimientoExportacion` (679 registros, todos COMPLETADO)?

**Checkpoints post-fase 5:**
- [ ] Conteo de registros maestros coincide con origen
- [ ] Constraints de unicidad respetados (sin duplicados de DNI, placas, nombres de rol)
- [ ] Login con usuarios migrados funciona correctamente

---

### FASE 6 — Dejar Vacías las Tablas Transaccionales
**Responsable:** DBA  
**Riesgo:** Bajo (ya está en nueva BD vacía — no hay datos que limpiar aún)  
**Nota:** Las tablas de Categoría B simplemente se crean vacías en la Fase 4. No hay acción adicional a menos que se hayan importado datos accidentalmente.

**Verificación:**
```sql
-- Verificar que tablas transaccionales estén vacías
SELECT 'Despachos' AS tabla, COUNT(*) AS registros FROM Despachos
UNION ALL SELECT 'OrdenViaje', COUNT(*) FROM OrdenViaje
UNION ALL SELECT 'ViajesEnProgreso', COUNT(*) FROM ViajesEnProgreso
UNION ALL SELECT 'FirmaDigital', COUNT(*) FROM FirmaDigital
UNION ALL SELECT 'AuditoriaLog', COUNT(*) FROM AuditoriaLog
UNION ALL SELECT 'SeguimientoExportacion', COUNT(*) FROM SeguimientoExportacion;
-- Todas deben devolver 0
```

---

### FASE 7 — Validación Funcional Completa
**Responsable:** Propietario del sistema + QA  
**Riesgo:** Bajo (sin datos reales aún)  
**Script:** `08_script_validacion_post_migracion.sql`

**Pruebas funcionales requeridas:**

| # | Prueba | Módulo | Estado esperado |
|---|---|---|---|
| 1 | Login con cada rol | Auth | Redirige al dashboard correcto |
| 2 | Crear despacho nuevo | Despachos | Se registra con número correlativo |
| 3 | Crear orden de viaje | OrdenViaje | Se genera número `OV-YYYY-NNNNNN` |
| 4 | Firma digital del conductor | FirmaDigital | Se inserta fila, no se permite UPDATE/DELETE |
| 5 | Aprobación admin con segunda firma | FirmaDigital | Segunda fila insertada correctamente |
| 6 | Descarga PDF firmado | App_Data | PDF generado con hash SHA-256 correcto |
| 7 | Crear abastecimiento de combustible | Grifo | Se genera número correlativo, PDF F-06 |
| 8 | Registro en AuditoriaLog | Auditoría | Cada operación genera entrada automática |
| 9 | Dashboard de Exportación (vacío) | Exportación | Carga sin errores con datos vacíos |
| 10 | Reporte Excel de abastecimiento | Reportes | Exporta sin error (con datos de prueba mínimos) |

---

### FASE 8 — Cambiar ConnectionString del Proyecto
**Responsable:** Desarrollador  
**Riesgo:** Alto (cambio de producción)  
**👤 ACCIÓN MANUAL REQUERIDA**

**Acciones:**
1. Editar `WebSGV/connectionStrings.config`:
```xml
<connectionStrings>
  <add name="ConexionSGV"
       connectionString="Data Source=NUEVO_SERVIDOR.mssql.somee.com;
                         Initial Catalog=NUEVA_BD;
                         User ID=NUEVO_USUARIO;
                         Password=NUEVA_CONTRASENA;
                         Encrypt=True;
                         TrustServerCertificate=True;"
       providerName="System.Data.SqlClient" />
</connectionStrings>
```
2. Verificar que el archivo sigue siendo gitignoreado (no subir credenciales).
3. Republicar la aplicación web en Somee.

**Checkpoints:**
- [ ] `connectionStrings.config` actualizado con nueva cadena
- [ ] Archivo NO commiteado en git
- [ ] Aplicación web re-publicada en Somee

---

### FASE 9 — Prueba en Ambiente Premium Antes del Dominio Final
**Responsable:** Propietario del sistema  
**Riesgo:** Bajo

**Acciones:**
1. Acceder a la URL temporal de Somee Premium (antes del dominio propio).
2. Ejecutar todas las pruebas funcionales de la Fase 7 con la nueva BD.
3. Verificar que los PDFs se generan y archivan correctamente.
4. Verificar que los reportes Excel funcionan.
5. Crear al menos 1 despacho real de prueba, 1 orden de viaje y verificar la firma digital.
6. Si todo funciona → apuntar dominio final a la nueva instancia.

---

## ⏱️ Estimación de Tiempos

| Fase | Tarea | Tiempo estimado |
|---|---|---|
| 1 | Backup | 15-30 min |
| 2 | Generar DDL | 30-60 min |
| 3 | Crear BD Premium | 15 min (gestión Somee) |
| 4 | Restaurar estructura | 30-60 min |
| 5 | Migrar maestros | 30-60 min |
| 6 | Verificar vacíos | 10 min |
| 7 | Validación funcional | 2-4 horas |
| 8 | Cambiar connectionString + republish | 15-30 min |
| 9 | Pruebas en Premium | 1-2 horas |
| **TOTAL** | | **5-9 horas** |

---

## 🔄 Plan de Rollback (si algo falla)

| Situación | Acción de rollback |
|---|---|
| Error en Fase 4 (estructura) | Recrear la BD destino y volver a ejecutar DDL corregido |
| Error en Fase 5 (maestros) | Limpiar tablas en nueva BD y re-ejecutar script corregido |
| Error en Fase 8 (connectionString) | Revertir `connectionStrings.config` al valor anterior |
| Error después de Fase 9 (producción) | Revertir connectionString a la BD original (que no fue modificada) |

> ✅ La base original `sgvActualizada` permanece intacta en todo momento. El rollback siempre es posible mientras no se elimine la BD original.
