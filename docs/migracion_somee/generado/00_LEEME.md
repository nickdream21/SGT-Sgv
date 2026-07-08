# Scripts generados de migración — SGT-SGV → somee Pro

Generados automáticamente desde la base actual `sgvActualizada` (lectura directa vía
`System.Data.SqlClient`, la base origen **no se modificó**). Reemplazan al DDL manual de
SSMS: el repo solo tenía ~73 SPs versionados; la base real tiene **135** objetos
programables — todos están aquí.

## Contenido

| Archivo | Qué contiene | ¿En git? |
|---|---|---|
| `01_tablas.sql` | 69 tablas (CREATE TABLE + PK/UNIQUE + defaults). Excluidas las 2 tablas `SeguimientoExportacion_Backup_*`. | ✅ sí |
| `02_datos_maestros.sql` | INSERTs de datos maestros reales: conductores (85), flota (Tracto 100, Carreta 134, camionetas, volquetes), clientes, catálogos, plantas, rutas, **Usuarios solo CONDUCTOR (85)** e **Indicadores (1507)**. | ❌ **NO** (PII + hashes) |
| `03_indices.sql` | Índices secundarios (no PK/UNIQUE). | ✅ sí |
| `04_foreign_keys.sql` | Las 111 FK como `ALTER TABLE ... ADD CONSTRAINT`. | ✅ sí |
| `05_objetos_programables.sql` | 155 objetos: vistas, funciones, procedimientos y triggers (incl. `trg_FirmaDigital_NoUpdate/NoDelete` append-only). Definiciones exactas. | ✅ sí |
| `usuarios_admin_nuevos.sql` | Plantilla para crear los usuarios administrativos con contraseñas nuevas (los admin de prueba NO se migran). | ✅ sí |

> ⚠️ `02_datos_maestros.sql` **no se versiona** (está en `.gitignore`): contiene DNIs y
> nombres de conductores reales y hashes de contraseñas. Se conserva solo localmente para
> ejecutar la migración. No lo subas a ningún repositorio ni lo compartas por canales abiertos.

## Qué NO se migra (queda vacío en producción — solo estructura)

Todas las tablas transaccionales/de prueba: `Despachos`, `ViajesEnProgreso`, `OrdenViaje` y
`Detalle*`, `Egresos`, `Ingresos`, `CPIC`, `Factura`, `Documentos*`, `FirmaDigital`,
`AbastecimientoCombustible`, `AsignacionesMaquinaria`, `Liquidaciones`, `Auditoria(Log)`,
`SeguimientoExportacion` (679) y sus 2 backups (6 308). Usuarios admin de prueba tampoco.

## Orden de ejecución (conectado a la NUEVA base Pro)

Este orden evita cualquier problema de dependencias de FK (se cargan los datos **antes** de
crear las FK):

```
1. 01_tablas.sql
2. 02_datos_maestros.sql
3. 03_indices.sql
4. 04_foreign_keys.sql
5. 05_objetos_programables.sql
6. usuarios_admin_nuevos.sql   (tras editar hashes/roles)
```

Con `sqlcmd` (ajusta servidor/BD/usuario de la cuenta Pro):

```powershell
$s="NUEVOSERVIDOR.mssql.somee.com"; $d="NUEVA_BD"; $u="NUEVO_USER"; $p="NUEVO_PASS"
foreach ($f in "01_tablas","02_datos_maestros","03_indices","04_foreign_keys","05_objetos_programables","usuarios_admin_nuevos") {
    sqlcmd -S $s -d $d -U $u -P $p -b -i "docs\migracion_somee\generado\$f.sql"
    if ($LASTEXITCODE -ne 0) { Write-Error "Fallo en $f"; break }
}
```

## Verificación post-carga

Ejecutar `docs/migracion_somee/08_script_validacion_post_migracion.sql`, o rápidamente:

```sql
SELECT (SELECT COUNT(*) FROM sys.tables)  AS tablas,     -- esperado 69
       (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped=0) AS sps,
       (SELECT COUNT(*) FROM Conductor)   AS conductores,-- 85
       (SELECT COUNT(*) FROM Tracto)      AS tractos,    -- 100
       (SELECT COUNT(*) FROM Carreta)     AS carretas,   -- 134
       (SELECT COUNT(*) FROM Usuarios)    AS usuarios,   -- 85 (conductores) + los admin creados
       (SELECT COUNT(*) FROM Despachos)   AS despachos;  -- 0
```

## Después: apuntar la app a la nueva base

Actualizar la cadena de conexión (mismo nombre `ConexionSGV`, el código C# no cambia) en los
**tres** lugares, con el **password nuevo** de la cuenta Pro:
`WebSGV/connectionStrings.config`, `.env` (DAB) y `.claude/settings.json` (MCP). Los tres están
gitignoreados. Republicar la app y correr las pruebas funcionales del
`00_REPORTE_EJECUTIVO.md`.
