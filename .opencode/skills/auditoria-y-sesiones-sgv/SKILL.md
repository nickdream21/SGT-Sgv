---
name: auditoria-y-sesiones-sgv
description: Patrón canónico de WebSGV para validación de sesión, control de roles y registro de auditoría — incluye las firmas reales de RolesHelper y AuditoriaHelper, claves de Session correctas y casos de uso obligatorios
license: MIT
compatibility: opencode
metadata:
  audience: developers, reviewers, qa
  workflow: security
---

# Auditoría y sesiones en WebSGV

## Por qué importa

Cada página y endpoint debe (1) validar que hay sesión, (2) validar que el rol tiene permiso, (3) registrar auditoría cuando muta datos. Estos tres puntos son la línea de defensa principal — autenticación es por **session-state**, no Forms Auth, y un `Page_Load` sin estos tres pasos es un bug de seguridad.

> **Quién la carga:** `developer` en cada nueva página o endpoint, `reviewer` en cada auditoría, `qa` para diseñar casos negativos por rol.

## Las dos claves de sesión que coexisten

WebSGV mantiene **dos** representaciones del usuario en sesión por razones históricas. Ambas se setean en login (`Views/Login.aspx.cs:71-74`) y en reconstrucción de sesión (`Site.Master.cs:110-113`):

| Clave | Tipo | Uso típico |
|---|---|---|
| `Session["UsuarioID"]` | `string` | **Validación de sesión activa** (`!= null`). Es lo que `RolesHelper.TieneSesionActiva()` consulta. |
| `Session["IdUsuario"]` | `int` | **Identidad del usuario** para FK en INSERT/UPDATE y auditoría. |
| `Session["Rol"]` | `string` | Rol; **siempre** comparar con `.Trim().ToUpper()`. |
| `Session["Nombre"]` | `string` | Nombre de display + auditoría. |

**Regla:** valida con `Session["UsuarioID"] != null`; persiste FKs con `Session["IdUsuario"]`.

## Patrón canónico de Page_Load

```csharp
using System;
using WebSGV.Helpers;

namespace WebSGV.Views
{
    public partial class MiPagina : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // 1. Validación de sesión + rol en una llamada
            //    Si falla, redirige automáticamente (a Login si no hay sesión,
            //    o a la página por defecto del rol si no tiene permiso).
            RolesHelper.ValidarAccesoSeccion("DESPACHO");

            if (!IsPostBack)
            {
                CargarDatos();
            }
        }
    }
}
```

`ValidarAccesoSeccion` ya hace dos cosas:
1. Si `Session["UsuarioID"] == null` → redirige a Login.
2. Si el rol no está en el switch para esa sección → llama a `RedirigirSegunRol()`.

### Patrón explícito (alternativa)

Si necesitas lógica adicional entre la validación de sesión y la de rol:

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    if (Session["UsuarioID"] == null)
    {
        Response.Redirect("~/Views/Login.aspx");
        return;
    }

    string rol = (Session["Rol"] as string ?? "").Trim().ToUpper();
    if (!RolesHelper.TienePermiso("ABASTECIMIENTO"))
    {
        Response.Redirect("~/Views/Inicio.aspx");
        return;
    }

    if (!IsPostBack) CargarDatos();
}
```

## Firmas reales del API (NO inventar variantes)

```csharp
// RolesHelper - todas son estáticas
RolesHelper.ObtenerRolActual()           : string
RolesHelper.TieneSesionActiva()          : bool
RolesHelper.TienePermiso(seccion)        : bool                  // ← UN parámetro
RolesHelper.ValidarAccesoSeccion(seccion): void  (redirige)      // ← UN parámetro
RolesHelper.RedirigirSegunRol()          : void
RolesHelper.ObtenerNombreUsuario()       : string
RolesHelper.EsAdmin() / EsConductor() / EsAdminGrifo()
        / EsAdminMaquinaria() / EsOperador() / EsAdminSistema()
```

> ⚠️ **No existe `RolesHelper.TienePermiso(rol, seccion)` con dos parámetros.** El helper lee el rol de la sesión internamente. Si ves esa firma en código antiguo o sugerencias de Copilot, está mal.

## Secciones reconocidas (tabla del switch en `RolesHelper.TienePermiso`)

| Sección | Roles permitidos |
|---|---|
| `DESPACHO`, `FACTURA`, `CPIC`, `ORDEN_VIAJE`, `REGISTRO`, `CONSULTAS`, `INDICADORES` | ADMIN, SUPERVISOR |
| `REGISTRO_CONDUCTORES` | ADMIN, ADMIN DE GRIFO, SUPERVISOR |
| `ABASTECIMIENTO` | ADMIN, ADMIN DE GRIFO, SUPERVISOR |
| `DASHBOARD_GRIFO` | ADMIN DE GRIFO, ADMIN |
| `AUDITORIA` | ADMINISTRADOR DE SISTEMA (solo) |
| `DASHBOARD_CONDUCTOR`, `MIS_VIAJES`, `MI_PERFIL` | CONDUCTOR, ADMIN |
| `DASHBOARD_OPERADOR`, `PARTE_DIARIO` | OPERADOR, ADMIN DE MAQUINARIA, ADMIN |

Si tu nueva página no encaja en una sección existente, agrega un nuevo `case` en `RolesHelper.cs` antes de codear el Page_Load. **Nunca** dupliques la lógica con `if (rol == "ADMIN" || rol == "SUPERVISOR")` inline.

## Auditoría — firma real

```csharp
AuditoriaHelper.Registrar(
    accion: "INSERT",                    // INSERT|UPDATE|DELETE|LOGIN|LOGOUT|APROBAR|RECHAZAR|...
    tablaAfectada: "Despachos",
    idRegistroAfectado: idDespacho,      // int o string, opcional
    descripcion: "Se creó despacho N° " + numero,
    valoresAnteriores: null,             // JSON o texto, opcional
    valoresNuevos: jsonNuevos);          // JSON o texto, opcional
```

El helper lee automáticamente `IdUsuario`, `Nombre`, `Rol`, IP y user-agent de la sesión actual y `HttpContext.Current.Request`. **Nunca** los pases manualmente.

### Cuándo es obligatorio llamar a `Registrar`

| Operación | ¿Auditar? |
|---|---|
| LOGIN exitoso | sí (`accion="LOGIN"`) |
| LOGIN fallido | sí (`accion="LOGIN_FALLIDO"`) |
| LOGOUT | sí |
| INSERT en tabla de negocio (Despachos, Choferes, OrdenViaje, Liquidacion, Abastecimiento, Maquinaria) | sí |
| UPDATE / DELETE en cualquier tabla de negocio | sí (con `valoresAnteriores`) |
| APROBAR / RECHAZAR liquidaciones, órdenes | sí |
| Generar PDF firmado | sí (`accion="GENERAR_PDF"`) |
| SELECT puro / cargas de listas | NO |
| Cambio de password | sí (sin loguear el hash, solo `accion="CAMBIO_PASSWORD"`) |

### Captura de "valores anteriores" antes de UPDATE

```csharp
// 1. SELECT actual
string valoresAnteriores = ObtenerJsonRegistro(id);

// 2. UPDATE
EjecutarUpdate(id, nuevosDatos);

// 3. Registrar
AuditoriaHelper.Registrar("UPDATE", "Despachos", id,
    "Modificación de despacho",
    valoresAnteriores,
    JsonConvert.SerializeObject(nuevosDatos));
```

## Anti-patrones (REJECT en review)

### ❌ Comparar rol inline
```csharp
string rol = Session["Rol"]?.ToString() ?? "";
if (rol == "Admin" || rol == "ADMIN" || rol == "Administrador")
{
    // ... acceso
}
```
**Problemas:** sin trim, sin uppercase consistente, no contempla `ADMINISTRADOR DE SISTEMA`, duplica lógica.
**Fix:** `RolesHelper.EsAdmin()` o `RolesHelper.TienePermiso("SECCION_X")`.

### ❌ Page_Load sin validación
```csharp
protected void Page_Load(object sender, EventArgs e)
{
    if (!IsPostBack) CargarGrid();
}
```
**Problema:** cualquiera con la URL accede.
**Fix:** primera línea = `RolesHelper.ValidarAccesoSeccion("...")`.

### ❌ Operación sensible sin auditar
```csharp
EjecutarDelete(id);
Response.Redirect("Lista.aspx");
// ← no se sabe quién borró qué
```
**Fix:** `AuditoriaHelper.Registrar("DELETE", "TablaX", id, "...", valoresAnteriores, null);` antes del redirect.

### ❌ Confundir las dos claves de sesión
```csharp
int idUsuario = Convert.ToInt32(Session["UsuarioID"]);  // ← string, falla si tiene formato no-int
```
**Fix:** `int idUsuario = Convert.ToInt32(Session["IdUsuario"]);`.

### ❌ Pasar usuario manualmente a AuditoriaHelper
```csharp
AuditoriaHelper.Registrar(idUsuario, "INSERT", ...);  // ← no existe esta sobrecarga
```
**Fix:** la firma es `(accion, tablaAfectada, idRegistroAfectado, descripcion, valoresAnteriores, valoresNuevos)`. El usuario se toma solo de la sesión.

## Endpoints async / WebMethod / .ashx

Las llamadas AJAX no pasan por `Page_Load`. Aplica el mismo patrón al inicio del handler:

```csharp
[WebMethod]
public static object MiEndpoint(...)
{
    var ctx = HttpContext.Current;
    if (ctx.Session["UsuarioID"] == null)
        throw new HttpException(401, "Sesión expirada");

    string rol = (ctx.Session["Rol"] as string ?? "").Trim().ToUpper();
    if (!RolesHelper.TienePermiso("ABASTECIMIENTO"))
        throw new HttpException(403, "Sin permiso");

    // ... lógica
}
```

> Nota: dentro de `WebMethod static`, `Session` no está expuesta directamente — usar `HttpContext.Current.Session`. Ver `LiquidacionesPendientes.aspx.cs:605, 664, 1254` como referencia.

## Application_Error y la "redirección silenciosa a Login"

`Global.asax.cs` swallow excepciones de ViewState y redirige a `~/Views/Login.aspx?error=sesion`. Si reportas "el usuario aterriza en login sin razón", revisa primero esto antes de tocar código.

## Checklist para reviewer

- [ ] Page_Load arranca con `RolesHelper.ValidarAccesoSeccion(...)` o validación equivalente.
- [ ] Si hay validación explícita: usa `Session["UsuarioID"] != null` y `RolesHelper.TienePermiso(...)`.
- [ ] No hay comparación inline del rol con strings literales.
- [ ] Operaciones que mutan datos llaman a `AuditoriaHelper.Registrar` con acción correcta.
- [ ] UPDATE/DELETE incluyen `valoresAnteriores`.
- [ ] FKs de usuario usan `Session["IdUsuario"]` (int), no `Session["UsuarioID"]` (string).
- [ ] Endpoints WebMethod hacen su propia validación.
- [ ] Si la nueva página requiere sección nueva, ¿se agregó el case en `RolesHelper.TienePermiso`?

## Referencias en el repo

- `WebSGV/Views/RolesHelper.cs` — fuente de verdad de roles y secciones.
- `WebSGV/Helpers/AuditoriaHelper.cs:29-97` — firma real de `Registrar`.
- `WebSGV/Views/Login.aspx.cs:71-74` — qué claves se setean en sesión.
- `WebSGV/Site.Master.cs:110-133` — reconstrucción de sesión por cookie persistente.
- `WebSGV/Views/LiquidacionesPendientes.aspx.cs` — buen ejemplo de WebMethod con validación + auditoría.
