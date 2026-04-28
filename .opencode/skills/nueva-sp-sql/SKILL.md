---
name: nueva-sp-sql
description: Guía para crear stored procedures en WebSGV — convención de nombres, estructura del archivo .sql, registro en Database/StoredProcedures/, y despliegue manual obligatorio a SQL Server
license: MIT
compatibility: opencode
metadata:
  audience: developers
  workflow: database
---

# Nueva Stored Procedure en WebSGV

## Regla fundamental

Los stored procedures **NO se auto-aplican**. Crear el archivo `.sql` aquí es solo la mitad del trabajo — el proc debe ejecutarse manualmente contra la base de datos en somee.com antes de que el código C# pueda llamarlo.

## Convención de nombres

| Módulo | Prefijo | Ejemplo |
|--------|---------|---------|
| Dashboard Conductor | `sp_DC_` | `sp_DC_GetOrdenesViajeActivas` |
| Órdenes de Viaje | `sp_OV_` | `sp_OV_BuscarPorFecha` |
| Abastecimiento | `sp_AB_` | `sp_AB_RegistrarDespacho` |
| Facturas | `sp_FA_` | `sp_FA_ListarPendientes` |
| Liquidaciones | `sp_LQ_` | `sp_LQ_GetPendientes` |
| Choferes / Conductores | `sp_CH_` | `sp_CH_BuscarActivos` |
| Maquinaria | `sp_MQ_` | `sp_MQ_AsignarEquipo` |
| General / Admin | `sp_` | `sp_AuditoriaRegistrar` |

> Si no hay prefijo de módulo claro, usa `sp_` + nombre descriptivo en español.

## Paso 1 — Crear el archivo .sql

Ubicación: `WebSGV/Database/StoredProcedures/sp_MODULO_NombreProc.sql`

### Plantilla estándar

```sql
-- =============================================
-- Autor:        [Nombre]
-- Fecha:        [YYYY-MM-DD]
-- Descripción:  [Qué hace este proc]
-- Módulo:       [Nombre del módulo]
-- =============================================

IF OBJECT_ID('dbo.sp_MODULO_NombreProc', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_MODULO_NombreProc
GO

CREATE PROCEDURE dbo.sp_MODULO_NombreProc
    @Parametro1     INT,
    @Parametro2     NVARCHAR(100)   = NULL,   -- parámetro opcional
    @FechaDesde     DATE            = NULL,
    @FechaHasta     DATE            = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Validaciones de entrada
    IF @Parametro1 IS NULL OR @Parametro1 <= 0
    BEGIN
        RAISERROR('Parametro1 es requerido y debe ser mayor a 0', 16, 1);
        RETURN;
    END

    -- Lógica principal
    SELECT
        col1,
        col2,
        FORMAT(fecha_col, 'dd/MM/yyyy') AS FechaFormateada  -- formato es-PE
    FROM
        Tabla t
        INNER JOIN OtraTabla o ON t.ID = o.TablaID
    WHERE
        t.ID = @Parametro1
        AND (@Parametro2 IS NULL OR t.Columna = @Parametro2)
        AND (@FechaDesde IS NULL OR t.Fecha >= @FechaDesde)
        AND (@FechaHasta IS NULL OR t.Fecha <= @FechaHasta)
    ORDER BY
        t.Fecha DESC;

END
GO
```

## Paso 2 — Registrar como script de migración (si aplica)

Si el proc acompaña un cambio de esquema, agregar también un script en:

`WebSGV/Database/Scripts/script_YYYYMMDD_descripcion.sql`

## Paso 3 — Llamar desde C# (code-behind)

```csharp
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

// En el método que necesita el proc:
string connStr = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

using (var conn = new SqlConnection(connStr))
using (var cmd = new SqlCommand("sp_MODULO_NombreProc", conn))
{
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.AddWithValue("@Parametro1", valorInt);
    cmd.Parameters.AddWithValue("@Parametro2", (object)valorString ?? DBNull.Value);

    conn.Open();
    using (var reader = cmd.ExecuteReader())
    {
        while (reader.Read())
        {
            // leer columnas
            var col1 = reader["col1"].ToString();
        }
    }
}
```

## Paso 4 — Despliegue manual OBLIGATORIO

**El código C# fallará con "Could not find stored procedure" hasta que el proc esté en la DB.**

```sql
-- Verificar que el proc existe ANTES de llamarlo desde código:
SELECT name, create_date, modify_date
FROM sys.objects
WHERE name = 'sp_MODULO_NombreProc' AND type = 'P'
```

Pasos:
1. Conectar a la DB (somee.com) con SQL Server Management Studio o Azure Data Studio
2. Ejecutar el archivo `.sql` completo
3. Verificar con el `SELECT` de arriba que el proc aparece
4. Solo entonces probar el código C#

## Checklist de verificación

- [ ] Nombre sigue convención de prefijo de módulo
- [ ] Archivo `.sql` creado en `Database/StoredProcedures/`
- [ ] `SET NOCOUNT ON` incluido
- [ ] `IF OBJECT_ID ... DROP` antes del `CREATE` (idempotente)
- [ ] Parámetros opcionales tienen `= NULL` y filtro con `@Param IS NULL OR ...`
- [ ] Proc ejecutado manualmente en la DB
- [ ] `SELECT sys.objects` confirma que existe
- [ ] Llamada C# usa `CommandType.StoredProcedure`
- [ ] Parámetros nulos usan `(object)valor ?? DBNull.Value`
