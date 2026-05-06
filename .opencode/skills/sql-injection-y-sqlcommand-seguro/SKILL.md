---
name: sql-injection-y-sqlcommand-seguro
description: Patrón canónico de SqlCommand parametrizado en WebSGV — using, SqlDbType explícito, DBNull.Value para opcionales, y anti-patrones de inyección SQL detectados en el código real
license: MIT
compatibility: opencode
metadata:
  audience: developers, reviewers
  workflow: security
---

# SQL injection y SqlCommand seguro

## Por qué importa

WebSGV se conecta a SQL Server (somee.com) con `ConfigurationManager.ConnectionStrings["ConexionSGV"]`. Toda concatenación de strings SQL es **vulnerabilidad explotable**. Esta skill define el patrón único aceptado en el proyecto.

> **Quién la carga:** `developer` siempre que escribe acceso a datos; `reviewer` siempre que audita C# / .aspx.cs.

## La regla de oro

```
NINGÚN SqlCommand puede contener interpolación, concatenación,
String.Format ni $"..." que mezcle datos del usuario con SQL.
```

Si necesitas armar un IN dinámico, GroupBy condicional, o ORDER BY con columna variable → ver sección **"Casos avanzados"** abajo. NUNCA inventes shortcut.

## Patrón canónico (memorizar)

```csharp
using System.Data;
using System.Data.SqlClient;
using System.Configuration;

string connStr = ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString;

using (SqlConnection conn = new SqlConnection(connStr))
using (SqlCommand cmd = new SqlCommand("sp_LQ_BuscarPorChofer", conn))
{
    cmd.CommandType = CommandType.StoredProcedure;

    cmd.Parameters.Add("@IdChofer", SqlDbType.Int).Value = idChofer;
    cmd.Parameters.Add("@FechaDesde", SqlDbType.Date).Value =
        (object)fechaDesde ?? DBNull.Value;
    cmd.Parameters.Add("@Estado", SqlDbType.NVarChar, 20).Value =
        string.IsNullOrEmpty(estado) ? (object)DBNull.Value : estado;

    conn.Open();
    using (SqlDataReader reader = cmd.ExecuteReader())
    {
        while (reader.Read())
        {
            // ...
        }
    }
}
```

### Reglas del patrón

1. **Doble `using`**: `SqlConnection` y `SqlCommand`. Ambos cierran y liberan handles, incluso si hay excepción.
2. **`SqlDbType` explícito** con tamaño cuando aplica (`NVarChar(N)`, `Decimal(p,s)`).
   - `AddWithValue("@x", valor)` se acepta solo en código existente; en código nuevo prefiere `Add(...).Value = ...`. El motivo: `AddWithValue` infiere tipo y a veces elige `NVarChar(4000)` o `Decimal(38,0)` rompiendo planes de ejecución.
3. **`DBNull.Value` para opcionales**: nunca pases `null` directo, SQL lo recibe como "ausente".
   - Cast obligatorio: `(object)valor ?? DBNull.Value` cuando `valor` es value-type (`int?`, `DateTime?`, `decimal?`).
4. **`SqlDataReader` también dentro de `using`**.
5. **Stored procedures, no SQL inline**: si la query es estable, vive en `Database/StoredProcedures/sp_XX_*.sql`. Solo SELECTs ad-hoc de admin pueden ir inline.

## Anti-patrones (REJECT en review)

### ❌ Concatenación
```csharp
// ALERTA ROJA — inyectable
string sql = "SELECT * FROM Choferes WHERE Dni = '" + dni + "'";
var cmd = new SqlCommand(sql, conn);
```
**Fix:** parámetro `@Dni` con `SqlDbType.NVarChar(15)`.

### ❌ String.Format / interpolación
```csharp
// ALERTA ROJA — sigue siendo inyectable
string sql = $"DELETE FROM Despachos WHERE Id = {id}";
```
**Fix:** parámetro `@Id` con `SqlDbType.Int`.

### ❌ Conexión sin using
```csharp
// MAJOR — leak de connection si lanza excepción entre Open() y Close()
SqlConnection conn = new SqlConnection(connStr);
conn.Open();
var cmd = new SqlCommand("...", conn);
cmd.ExecuteNonQuery();
conn.Close();
```
**Fix:** envolver en `using { }`.

### ❌ AddWithValue con null directo
```csharp
// BUG — si fechaDesde es null, falla en runtime
cmd.Parameters.AddWithValue("@FechaDesde", fechaDesde);
```
**Fix:** `(object)fechaDesde ?? DBNull.Value`.

### ❌ ORDER BY parametrizado mal hecho
```csharp
// NO FUNCIONA — SQL Server ignora el parámetro en ORDER BY
cmd.CommandText = "SELECT * FROM X ORDER BY @col";
cmd.Parameters.AddWithValue("@col", columna);
```
**Fix:** ver sección "Casos avanzados".

## Casos avanzados (cuando el patrón básico no alcanza)

### IN dinámico
```csharp
// Construir IN con parámetros, NO concatenar
var ids = new List<int> { 1, 2, 3 };
var paramNames = ids.Select((id, i) => "@id" + i).ToArray();
string sql = $"SELECT * FROM X WHERE Id IN ({string.Join(",", paramNames)})";

using (var cmd = new SqlCommand(sql, conn))
{
    for (int i = 0; i < ids.Count; i++)
        cmd.Parameters.Add(paramNames[i], SqlDbType.Int).Value = ids[i];
    // ...
}
```

### ORDER BY o columna variable
Whitelist obligatoria — jamás aceptes el nombre de columna del usuario directo:
```csharp
var columnasPermitidas = new HashSet<string> { "Fecha", "Monto", "Estado" };
if (!columnasPermitidas.Contains(orderBy))
    orderBy = "Fecha";  // default seguro

string sql = $"SELECT * FROM X ORDER BY {orderBy} DESC";
```

### Búsqueda LIKE
```csharp
cmd.Parameters.Add("@Filtro", SqlDbType.NVarChar, 100).Value = "%" + filtro + "%";
// SQL: WHERE Nombre LIKE @Filtro
```

## Lectura segura del SqlDataReader

```csharp
while (reader.Read())
{
    // Para columnas nullable, usar IsDBNull primero
    int? idAprobador = reader.IsDBNull(reader.GetOrdinal("IdAprobador"))
        ? (int?)null
        : reader.GetInt32(reader.GetOrdinal("IdAprobador"));

    decimal monto = reader.GetDecimal(reader.GetOrdinal("Monto"));
    DateTime fecha = reader.GetDateTime(reader.GetOrdinal("Fecha"));

    // Strings: ?.ToString() ?? "" para tolerar DBNull
    string desc = reader["Descripcion"]?.ToString() ?? "";
}
```

## Checklist para reviewer

- [ ] Cada `SqlConnection` y `SqlCommand` está dentro de `using`.
- [ ] Cero ocurrencias de `+`, `$"..."`, `String.Format` mezclando SQL con datos.
- [ ] Parámetros usan `Add(name, SqlDbType, [size])` o al menos `AddWithValue` con tipos correctos.
- [ ] Opcionales usan `(object)x ?? DBNull.Value`.
- [ ] Conexión obtiene cadena de `ConfigurationManager.ConnectionStrings["ConexionSGV"]`.
- [ ] Si hay query inline, ¿podría ser un SP? Sugerir migración.
- [ ] `SqlDataReader` también en `using`.
- [ ] Para columnas nullable, se usa `IsDBNull` antes de `GetX`.

## Referencia en el repo

- Ejemplo correcto: `WebSGV/Helpers/AuditoriaHelper.cs:71-90`
- Patrón de SP: `WebSGV/Database/StoredProcedures/` (todos los `sp_DC_*`, `sp_LQ_*`).
