# Configuración del SQL MCP Server (Data API Builder) para WebSGV

Este documento explica cómo activar el **SQL MCP Server** que ya viene
configurado en `dab-config.json` y exponerlo al agente DBA de OpenCode.

> Basado en la doc oficial:
> [learn.microsoft.com/azure/data-api-builder/mcp/quickstart-visual-studio-code](https://learn.microsoft.com/es-es/azure/data-api-builder/mcp/quickstart-visual-studio-code)

---

## 0. Qué ya está hecho en el repo

- `dab-config.json` en la raíz: define DB, entidades (todas las tablas), SPs y
  tiene la sección `runtime.mcp.enabled: true` con tools `create-record`,
  `read-records`, `update-record`, `delete-record` activas.
- El connection-string del config es `@env('DATABASE_CONNECTION_STRING')`.

## 1. Prerrequisitos

| Herramienta              | Cómo verificar                | Cómo instalar                                  |
|--------------------------|-------------------------------|------------------------------------------------|
| .NET SDK 9.0+            | `dotnet --version`            | https://dotnet.microsoft.com/download          |
| Data API Builder ≥ 1.7   | `dab --version`               | `dotnet tool install --global Microsoft.DataApiBuilder` |
| OpenCode con soporte MCP | `opencode --version`          | (ya instalado)                                 |

## 2. Configurar la cadena de conexión (variable de entorno)

El `dab-config.json` lee `DATABASE_CONNECTION_STRING` desde un archivo `.env`
en la raíz del repo (ya gitignored) o desde el entorno del proceso.

### Opción A: Archivo `.env` en la raíz del repo (más simple)

Crea/edita `C:\Users\NICK\Downloads\WebSGV-master\WebSGV-master\WebSGV-master\.env`:

```text
DATABASE_CONNECTION_STRING=Server=tcp:<host-somee>.somee.com;Database=<basededatos>;User Id=<usuario>;Password=<password>;TrustServerCertificate=True;Encrypt=True;
```

> Toma los valores de `WebSGV/connectionStrings.config` (ConexionSGV).
> El archivo `.env` ya está en `.gitignore`.

### Opción B: Variable de entorno permanente (PowerShell, usuario actual)

```powershell
[Environment]::SetEnvironmentVariable(
  "DATABASE_CONNECTION_STRING",
  "Server=tcp:<host>.somee.com;Database=<db>;User Id=<u>;Password=<p>;TrustServerCertificate=True;Encrypt=True;",
  "User"
)
```

Reinicia la terminal después.

## 3. Probar el MCP de forma aislada

```powershell
cd C:\Users\NICK\Downloads\WebSGV-master\WebSGV-master\WebSGV-master
dab start --config dab-config.json
```

Si todo va bien, verás:

```
Now listening on: http://localhost:5000
MCP endpoint: http://localhost:5000/mcp
```

En otro terminal, opcionalmente, inspecciona con MCP Inspector:

```powershell
npx -y @modelcontextprotocol/inspector http://localhost:5000/mcp
```

Detén el servidor con `Ctrl+C` cuando termines.

## 4. Registrar el MCP en OpenCode

Edita (o crea) `opencode.jsonc` en la raíz del repo. Hay dos modos de transporte:

### Modo recomendado: stdio (OpenCode lanza DAB automáticamente)

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "sql-mcp-server": {
      "type": "local",
      "command": [
        "dab",
        "start",
        "--mcp-stdio",
        "role:anonymous",
        "--loglevel",
        "error",
        "--config",
        "dab-config.json"
      ],
      "enabled": true
    }
  },
  "permission": {
    "tools": {
      "sql-mcp-server*describe_entities":  "allow",
      "sql-mcp-server*read_records":       "allow",
      "sql-mcp-server*aggregate_records":  "allow",
      "sql-mcp-server*execute_entity":     "ask",
      "sql-mcp-server*create_record":      "ask",
      "sql-mcp-server*update_record":      "ask",
      "sql-mcp-server*delete_record":      "deny"
    }
  }
}
```

> Verifica el separador exacto (`*`, `:` o `_`) que usa tu versión de OpenCode
> para tools de MCP. Si `allow`/`ask`/`deny` no aplican, OpenCode te pedirá
> permiso interactivamente la primera vez.

### Modo alterno: HTTP (DAB corriendo en otra terminal)

```jsonc
{
  "mcp": {
    "sql-mcp-server": {
      "type": "remote",
      "url": "http://localhost:5000/mcp",
      "enabled": true
    }
  }
}
```

Y en una terminal aparte mantén `dab start` corriendo.

## 5. Reiniciar OpenCode

Cierra y abre OpenCode para que cargue el nuevo MCP. En la próxima sesión, el
agente DBA podrá invocar las 7 tools del SQL MCP Server.

## 6. Tabla completa de permisos recomendados (somee.com producción)

| Tool MCP                                | Default config | Justificación                                      |
|-----------------------------------------|----------------|----------------------------------------------------|
| `describe_entities`                     | allow          | Solo metadata, no toca datos                       |
| `read_records`                          | allow          | Lectura segura (RBAC limita campos)                |
| `aggregate_records`                     | allow          | Conteos/sumas, no muta datos                       |
| `execute_entity` (SPs `Obtener*`)       | allow*         | OpenCode pedirá confirmación si default es `ask`   |
| `execute_entity` (SPs `Insertar*`/`Actualizar*`/`Crear*`) | ask | Modifica datos productivos                  |
| `create_record`                         | ask            | INSERT directo en producción                       |
| `update_record`                         | ask            | UPDATE directo en producción                       |
| `delete_record`                         | deny           | Riesgo alto en producción                          |

> *El esquema de permisos por nombre individual de SP no está soportado a nivel
> de tool MCP — `execute_entity` es UN solo tool con parámetro `entity`. Si
> quieres distinguir, usa `ask` y juzga caso a caso, o crea entidades con
> `custom-tool: true` para SPs frecuentes y configúralas individualmente.

## 7. Limitaciones conocidas (importante)

- **No DDL**: el MCP no puede `CREATE/ALTER/DROP` tablas, columnas, SPs ni
  índices. Todo eso sigue siendo manual con SSMS o `sqlcmd`. El agente DBA
  debe escribir el `.sql` en el repo y pedirte que lo despliegues.
- **Sin JOINs en `read_records`**: para consultas cruzadas, usar vistas o SPs.
- **El usuario `anonymous` ve TODO**: la config actual de `dab-config.json`
  da acceso anónimo a todas las entidades. Si quieres restringir más, edita
  los `permissions` de cada entidad y crea roles distintos.

## 8. Checklist de seguridad antes de habilitarlo en producción

- [ ] `.env` con `DATABASE_CONNECTION_STRING` está en `.gitignore` (✅ ya lo está)
- [ ] `delete_record` está en `deny` (recomendado) o `ask`
- [ ] `host.mode` en `dab-config.json` es `"development"` solo localmente —
      si despliegas DAB en un servidor expuesto, cambiar a `"production"`
- [ ] CORS de `dab-config.json` no expone orígenes innecesarios
- [ ] Probaste primero con un caso de lectura (`read_records`) antes de
      ejecutar mutaciones
- [ ] Tienes backup de la DB de somee.com antes de cualquier `update_record`
      o `execute_entity` masivo

## 9. Troubleshooting

| Síntoma                                     | Causa probable                              | Solución                              |
|---------------------------------------------|---------------------------------------------|---------------------------------------|
| `dab` no se reconoce como comando           | Tool no instalado o PATH no actualizado     | `dotnet tool install -g Microsoft.DataApiBuilder` y reabrir terminal |
| `Connection refused` al iniciar dab          | Connection string mal formado               | Probar con SSMS primero                |
| OpenCode no ve el MCP                        | `opencode.jsonc` mal formado o no recargado | Validar JSON; reiniciar OpenCode       |
| Tool MCP devuelve `Unauthorized`             | Rol mal en `--mcp-stdio role:X`             | Usar `role:anonymous` (default)        |
| `read_records` devuelve `UnexpectedError`    | `orderby` pasado como string en vez de array | Pasar `["Campo asc"]` no `"Campo asc"` |
