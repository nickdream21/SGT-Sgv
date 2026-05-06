---
name: viewstate-postback-webforms
description: Patrones canónicos de ASP.NET Web Forms en WebSGV — IsPostBack vs primera carga, ViewState vs Session vs hidden field, repoblado de DropDownList tras postback, control event lifecycle y por qué Application_Error redirige a Login
license: MIT
compatibility: opencode
metadata:
  audience: developers, qa
  workflow: webforms-runtime
---

# ViewState y postbacks en WebSGV

## Por qué importa

Web Forms tiene un ciclo de vida que la mayoría de desarrolladores modernos no conocen. Tres bugs típicos en este repo derivan de no entenderlo:

1. **DropDownList se vacía** o pierde la selección después de un postback.
2. **Datos del grid se duplican** en cada submit.
3. **Excepción de ViewState** silenciosa que aterriza al usuario en `Login.aspx?error=sesion`.

> **Quién la carga:** `developer` cuando toca `Page_Load`, `OnClick`, controles con eventos. `qa` para diseñar casos de doble-postback.

## El ciclo de vida resumido

Para cada request, ASP.NET ejecuta en este orden:

1. `Page_PreInit`
2. `Page_Init` (controles instanciados, ViewState aún no aplicado)
3. **ViewState/control state se restauran**
4. **PostBack data se aplica a controles** (TextBox.Text, etc.)
5. `Page_Load` (aquí ya `IsPostBack == true` si vino de submit)
6. **Control events** (`Button_Click`, `DropDownList_SelectedIndexChanged`)
7. `Page_PreRender`
8. ViewState se serializa
9. Render → HTML al cliente

**Implicación crítica:** los handlers de eventos corren **DESPUÉS** de `Page_Load`. Si en `Page_Load` lees el valor del grid o del DropDownList esperando el nuevo valor, lo tienes; pero si en `Page_Load` **rellenas** el DropDownList sin cuidado, sobrescribes la selección del usuario antes de que su evento corra.

## La regla de oro: `if (!IsPostBack)`

Toda inicialización de controles enlazados a datos va dentro de `if (!IsPostBack)`:

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    RolesHelper.ValidarAccesoSeccion("DESPACHO");

    if (!IsPostBack)
    {
        CargarConductores();      // ← rellena DropDownList
        CargarVehiculos();
        CargarGrid();
    }
}
```

Si pones `CargarConductores()` fuera del `if`, en cada postback se vuelve a llenar y el `SelectedValue` se resetea **antes** de que `DropDown_SelectedIndexChanged` corra → bug clásico.

## ViewState vs Session vs Hidden Field — cuándo usar cada uno

| Necesidad | Mecanismo | Ejemplo en repo |
|---|---|---|
| Estado de un control en la página actual entre postbacks | **ViewState automático** del control | `GridView.PageIndex` |
| Datos custom de la página actual entre postbacks | `ViewState["clave"]` | `ViewState["IdDespacho"]` |
| Datos del usuario que cruzan páginas | `Session["clave"]` | `Session["IdUsuario"]` |
| Pasar dato del cliente al servidor sin que esté en ViewState | `<asp:HiddenField>` | Firma canvas → `hfFirmaBase64` |
| Dato grande / que no debe viajar al cliente | `Cache` o re-query DB | listas de catálogos |

### ❌ NO meter colecciones grandes en ViewState
ViewState viaja en cada request como base64. Una `DataTable` de 5000 filas en ViewState = 2 MB que sube y baja en cada click. Si necesitas ese dato:
- Re-consultar la DB en cada postback (la DB no se queja).
- O usar `Cache[...]` con clave por usuario.

### ❌ NO meter objetos no serializables en ViewState
ViewState requiere `[Serializable]`. Si pones un objeto custom sin marcarlo, el siguiente postback explota con excepción de deserialización → la atrapa `Application_Error` → **usuario aterriza en Login**.

## Patrón canónico de DropDownList con postback

```aspx
<asp:DropDownList ID="ddlConductor" runat="server"
    AutoPostBack="true"
    OnSelectedIndexChanged="ddlConductor_SelectedIndexChanged" />
```

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    RolesHelper.ValidarAccesoSeccion("DESPACHO");
    if (!IsPostBack)
    {
        CargarConductores();   // ← UNA sola vez
    }
}

private void CargarConductores()
{
    ddlConductor.Items.Clear();
    ddlConductor.Items.Add(new ListItem("-- Seleccione --", ""));
    using (var cn = new SqlConnection(ConfigurationManager.ConnectionStrings["ConexionSGV"].ConnectionString))
    using (var cmd = new SqlCommand("sp_ListarConductoresActivos", cn) { CommandType = CommandType.StoredProcedure })
    {
        cn.Open();
        using (var rd = cmd.ExecuteReader())
            while (rd.Read())
                ddlConductor.Items.Add(new ListItem(rd["Nombre"].ToString(), rd["IdConductor"].ToString()));
    }
}

protected void ddlConductor_SelectedIndexChanged(object sender, EventArgs e)
{
    // En este punto ddlConductor.SelectedValue ya tiene la nueva selección.
    int idConductor = int.Parse(ddlConductor.SelectedValue);
    CargarVehiculosDeConductor(idConductor);
}
```

## Patrón GridView con paginación

```aspx
<asp:GridView ID="gvDespachos" runat="server"
    AllowPaging="true" PageSize="20"
    OnPageIndexChanging="gvDespachos_PageIndexChanging"
    DataKeyNames="IdDespacho">
</asp:GridView>
```

```csharp
protected void gvDespachos_PageIndexChanging(object sender, GridViewPageEventArgs e)
{
    gvDespachos.PageIndex = e.NewPageIndex;
    CargarGrid();   // re-query
}

private void CargarGrid()
{
    DataTable dt = ObtenerDespachosFiltrados();
    gvDespachos.DataSource = dt;
    gvDespachos.DataBind();
}
```

> No persistas el `DataTable` completo en ViewState. Re-consulta. La paginación visible es del grid, los datos vienen de la DB cada vez.

## Hidden field para firma canvas / archivos generados en cliente

```aspx
<asp:HiddenField ID="hfFirmaBase64" runat="server" />
```

```csharp
protected void btnGuardar_Click(object sender, EventArgs e)
{
    string base64 = hfFirmaBase64.Value;
    if (string.IsNullOrWhiteSpace(base64) || !base64.StartsWith("data:image/"))
    {
        lblError.Text = "Debe firmar antes de guardar";
        return;
    }
    byte[] firma = Convert.FromBase64String(base64.Substring(base64.IndexOf(",") + 1));
    FirmaService.GuardarFirma(idUsuario, firma);
}
```

## Application_Error y la "redirección silenciosa a Login"

`Global.asax.cs Application_Error` swallow excepciones de tipo:
- `ViewStateException`
- `HttpException` con código de "invalid viewstate"
- `CryptographicException` (MAC failure, típico tras reciclo de AppPool sin `<machineKey>` fijo)

Cuando ocurre, redirige a `~/Views/Login.aspx?error=sesion`. **Síntoma típico:** el usuario hace click en algo y "se desloguea" sin razón aparente.

### Causas frecuentes de ViewStateException
1. AppPool se reinicia entre el render y el postback → MAC inválida. Solución: `<machineKey>` fijo en `Web.config` (ya configurado en producción).
2. Se cambió el ensamblado entre render y postback (deploy en caliente con usuario abierto).
3. Objeto no-serializable metido en ViewState.
4. Intentar fingir un postback desde otro tab con ViewState viejo.

Si reportan el bug, primero descartar (1) y (2) antes de buscar bugs en código.

## Eventos comunes y su disparo

| Control | Evento | Cuándo dispara |
|---|---|---|
| `Button` | `Click` | Submit del form |
| `LinkButton` | `Click` | Submit del form (renderiza `<a>` con `__doPostBack`) |
| `DropDownList` con `AutoPostBack="true"` | `SelectedIndexChanged` | Cambio de selección |
| `TextBox` con `AutoPostBack="true"` | `TextChanged` | Pierde foco con valor distinto |
| `GridView` | `RowCommand`, `PageIndexChanging`, `Sorting` | Acción en fila / paginación / sort |
| `CheckBox` con `AutoPostBack="true"` | `CheckedChanged` | Toggle |

## Anti-patrones (REJECT en review)

### ❌ Cargar datos fuera de `!IsPostBack`
```csharp
protected void Page_Load(object sender, EventArgs e)
{
    CargarConductores();   // ← BUG: se ejecuta en cada postback
    CargarVehiculos();
}
```
**Síntoma:** el dropdown pierde selección al hacer click en otro botón.

### ❌ Doble submit por falta de validación
Botón sin `OnClientClick="this.disabled=true; return true;"` ni flag → usuario hace doble click → INSERT duplicado.
**Fix:** flag en sesión o `Page.IsValid` + chequeo de duplicado en SP.

### ❌ DataTable enorme en ViewState
```csharp
ViewState["GridData"] = miDataTableDe5000Filas;  // ← MAL
```
**Fix:** re-query en `CargarGrid()`.

### ❌ Objeto no serializable en ViewState
```csharp
ViewState["Conexion"] = new SqlConnection(...);  // ← explota
```

### ❌ Confiar en `Request.Form["ddlX"]` antes de eventos
Trabajar con valores postback raw en `Page_Load` salta el modelo de eventos. Usa `ddlX.SelectedValue` después del bind.

### ❌ Olvidar `EnableEventValidation`
Si manipulas `<asp:DropDownList>` con JavaScript en cliente, el postback puede tirar `EventValidationException`. Soluciones:
- No manipular las opciones por JS: rellenar siempre desde servidor.
- Como último recurso: `EnableEventValidation="false"` en la directiva de página (auditar con cuidado, abre superficie de tampering).

## Casos de prueba recomendados (qa)

Para cada página con postbacks:

1. **Doble click rápido** en submit → ¿se duplica el INSERT?
2. **Tab duplicado:** abrir misma página en dos tabs, hacer login distinto en uno, intentar postback en el otro.
3. **Selección + postback no relacionado:** seleccionar item en DropDown, hacer click en otro botón → ¿se mantuvo la selección?
4. **Refresh tras submit:** F5 después de `Click` → ¿hay confirmación o se reenvía?
5. **Sesión expirada:** dejar página 31 minutos (timeout 30), hacer click → ¿redirige a Login limpiamente?

## Checklist para developer

- [ ] Toda carga de catálogos / grids está dentro de `if (!IsPostBack)`.
- [ ] Eventos de controles tienen su handler registrado en el `.aspx` y declarado en `.aspx.designer.cs`.
- [ ] Datos grandes NO viajan por ViewState.
- [ ] Objetos en ViewState son `[Serializable]`.
- [ ] Botones de submit con efecto monetario o de creación tienen guardia anti-doble-submit.
- [ ] Hidden fields para datos de cliente están validados en el handler antes de usar.
- [ ] Si la página manipula DropDowns por JS, hay decisión consciente sobre `EnableEventValidation`.

## Referencias en el repo

- `WebSGV/Global.asax.cs` — `Application_Error` y la redirección silenciosa.
- `WebSGV/Site.Master.cs:110-133` — reconstrucción de sesión desde cookie persistente.
- `WebSGV/Views/LiquidacionesPendientes.aspx.cs` — buen ejemplo de GridView + filtros + WebMethod.
- `WebSGV/Views/Despacho*.aspx.cs` — ejemplos de DropDown encadenados (conductor → vehículo).
- `WebSGV/Web.config` — `<sessionState timeout="30">`, `<machineKey>` (si está fijo).
