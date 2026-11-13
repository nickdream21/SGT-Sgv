---
name: nueva-pagina
description: Pasos para crear una página .aspx nueva en WebSGV (los tres archivos, el registro en WebSGV.csproj y la plantilla de Page_Load). Úsala al agregar cualquier pantalla nueva.
---

# Agregar una página nueva

Toda página nueva necesita **exactamente tres archivos** Y **registrarse en `WebSGV.csproj`**:

1. `WebSGV/Views/NombrePagina.aspx`: el markup, con `MasterPageFile="~/Site.Master"` e `Inherits="WebSGV.Views.NombrePagina"`.
2. `WebSGV/Views/NombrePagina.aspx.cs`: el code-behind; verifica sesión y rol en `Page_Load` antes de la lógica de `!IsPostBack`.
3. `WebSGV/Views/NombrePagina.aspx.designer.cs`: la clase parcial autogenerada (puede empezar vacía).

En `WebSGV.csproj` agrega una entrada `<Content>` y dos `<Compile>` (`.aspx.cs` con `<SubType>ASPXCodeBehind</SubType>` y `.aspx.designer.cs`). Sin ese registro la página no se despliega.

El CSS/JS propio de la página va en `Content/paginas/` y `Scripts/paginas/` (ver CLAUDE.md, "Front-end libraries"), también registrados como `<Content>`.

Plantilla de Page_Load:

```csharp
protected void Page_Load(object sender, EventArgs e)
{
    SecurityHelper.ExigirRolAdmin(); // o la guarda que corresponda
    SecurityHelper.AgregarHeadersSeguridad();
    if (!IsPostBack)
        CargarDatos();
}
```
