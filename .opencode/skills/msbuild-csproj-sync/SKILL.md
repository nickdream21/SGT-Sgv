---
name: msbuild-csproj-sync
description: Editar WebSGV.csproj sin romper Visual Studio — orden de ItemGroups, DependentUpon correcto, SubType=ASPXCodeBehind, comando exacto de verificación con MSBuild y diagnóstico de páginas que no despliegan
license: MIT
compatibility: opencode
metadata:
  audience: developers
  workflow: build
---

# MSBuild + WebSGV.csproj sync

## Por qué importa

Web Forms en .NET Framework 4.8 con `packages.config` legacy usa el formato **viejo** de csproj (no el SDK-style nuevo). Eso significa que **cada archivo se enumera explícitamente** en `WebSGV.csproj`. Si agregas un `.aspx` al disco pero olvidas registrarlo, **no despliega**. Si lo registras mal, Visual Studio lo muestra como "huérfano" y MSBuild puede fallar silenciosamente.

> **Quién la carga:** `developer` siempre que crea/elimina/renombra archivos compilables o de contenido.

## Anatomía relevante de `WebSGV.csproj`

El archivo tiene principalmente tres `ItemGroup` que importan:

| ItemGroup | Para qué |
|---|---|
| `<ItemGroup>` con `<Compile Include="...">` | Archivos `.cs` (code-behind, helpers, services, models) |
| `<ItemGroup>` con `<Content Include="...">` | Archivos `.aspx`, `.master`, `.config`, `.css`, `.js`, imágenes |
| `<ItemGroup>` con `<None Include="...">` | Archivos que el proyecto "ve" pero no copia (ej. `.sql`, READMEs) |
| `<ItemGroup>` con `<Reference Include="...">` | DLLs (manejadas por NuGet vía `packages.config`) |

> Existen MÚLTIPLES `<ItemGroup>` del mismo tipo. Es válido. No tienes que consolidarlos.

## Las tres entradas obligatorias para una nueva `.aspx`

Para `Views/MiPagina.aspx`:

```xml
<!-- 1. En el ItemGroup de <Content> -->
<Content Include="Views\MiPagina.aspx">
  <SubType>Designer</SubType>
</Content>

<!-- 2. En el ItemGroup de <Compile> -->
<Compile Include="Views\MiPagina.aspx.cs">
  <DependentUpon>MiPagina.aspx</DependentUpon>
  <SubType>ASPXCodeBehind</SubType>
</Compile>

<!-- 3. También en el ItemGroup de <Compile> -->
<Compile Include="Views\MiPagina.aspx.designer.cs">
  <DependentUpon>MiPagina.aspx</DependentUpon>
</Compile>
```

### Reglas inviolables

1. **Path con backslash `\`**, no forward `/`. El csproj viejo es Windows-first.
2. **`DependentUpon` solo el nombre del archivo**, sin path: `MiPagina.aspx` (no `Views\MiPagina.aspx`).
3. **`SubType=ASPXCodeBehind` solo en el `.aspx.cs`**, NO en el `.designer.cs` ni en el `.aspx`.
4. **`SubType=Designer` en el `.aspx`** — sin esto VS no abre el editor visual.

## Otros tipos de archivos

### Helper o Service nuevo (`.cs` simple)
```xml
<Compile Include="Helpers\NuevoHelper.cs" />
```

### CSS / JS / imagen
```xml
<Content Include="Content\nuevo-estilo.css" />
<Content Include="Scripts\modulo.js" />
<Content Include="Images\logo-corp.png" />
```

### Master Page nueva
```xml
<Content Include="NuevaMaster.Master">
  <SubType>Designer</SubType>
</Content>
<Compile Include="NuevaMaster.Master.cs">
  <DependentUpon>NuevaMaster.Master</DependentUpon>
  <SubType>ASPXCodeBehind</SubType>
</Compile>
<Compile Include="NuevaMaster.Master.designer.cs">
  <DependentUpon>NuevaMaster.Master</DependentUpon>
</Compile>
```

### Stored procedure (.sql)
**No** registrar en csproj. Los `.sql` viven en `Database/` pero no son compilables ni contenido web. Si quieres que aparezcan en Solution Explorer:
```xml
<None Include="Database\StoredProcedures\sp_LQ_Buscar.sql" />
```
Esto es opcional y cosmético.

## Orden dentro del ItemGroup

**No alfabético obligatorio**, pero VS cuando "agrega" un archivo lo inserta agrupando con archivos hermanos del mismo directorio. Sigue ese estilo:

- Buscar el `<Compile Include="Views\...` más cercano al alfabéticamente correcto.
- Insertar tu entrada al lado.
- Si dudas, mira cómo `Views\Login.aspx` y compañeros están agrupados.

## Comando de verificación obligatorio

```powershell
# Desde la raíz del repo (donde está WebSGV.sln)
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal
```

### Lectura del output

| Output | Significado |
|---|---|
| `Build succeeded.` + `0 Error(s)` | OK |
| `error CS0103: The name 'Foo' does not exist` | Falta `using` o referencia |
| `error CS0246: The type or namespace name 'X' could not be found` | Helper sin `<Compile Include>` o assembly faltante |
| `error CS0017: Program with multiple entry points` | Revisar si hay un `.cs` duplicado en dos ItemGroups |
| Build silencioso pero la página tira 404 | ¿Está la `.aspx` en `<Content Include>`? |
| `error MSB3030: Could not copy file` | Path con `/` en lugar de `\`, o archivo no existe en disco |
| Warning `MSB3245: Could not resolve this reference` | NuGet no restauró → ejecutar `nuget restore WebSGV.sln` |

### Verbosity higher cuando hay duda

```powershell
msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:detailed > build.log
# luego buscar 'error' o 'warning' en build.log
```

## Diagnóstico rápido: "agregué la página y no aparece"

Checklist en orden:

1. **¿Existen los 3 archivos en disco?**
   ```powershell
   Test-Path WebSGV\Views\MiPagina.aspx
   Test-Path WebSGV\Views\MiPagina.aspx.cs
   Test-Path WebSGV\Views\MiPagina.aspx.designer.cs
   ```
2. **¿Las 3 entradas están en el csproj?**
   ```powershell
   Select-String -Path WebSGV\WebSGV.csproj -Pattern "MiPagina"
   ```
   Debe mostrar 3 líneas.
3. **¿El namespace en el `.aspx.cs` es `WebSGV.Views`?**
4. **¿El `Inherits` del `.aspx` apunta exactamente a `WebSGV.Views.MiPagina`?**
5. **¿El build dice 0 errores?**

## Eliminar una página correctamente

1. Borrar los 3 archivos físicos.
2. Borrar las 3 entradas del csproj (1 Content + 2 Compile).
3. Buscar referencias muertas: `Select-String -Path WebSGV -Pattern "MiPagina" -Recurse`.
4. Build limpio.

## Anti-patrones (REJECT en review)

### ❌ Forward slash en paths
```xml
<Content Include="Views/MiPagina.aspx" />
```
**Fix:** `Views\MiPagina.aspx`.

### ❌ DependentUpon con path
```xml
<Compile Include="Views\MiPagina.aspx.cs">
  <DependentUpon>Views\MiPagina.aspx</DependentUpon>  <!-- ← MAL -->
</Compile>
```
**Fix:** `<DependentUpon>MiPagina.aspx</DependentUpon>`.

### ❌ SubType en lugar incorrecto
```xml
<Compile Include="Views\MiPagina.aspx.designer.cs">
  <SubType>ASPXCodeBehind</SubType>  <!-- ← MAL, esto va en el .aspx.cs -->
</Compile>
```

### ❌ Archivo en disco pero no en csproj
Síntoma: la página tira 404 en runtime aunque está creada.
**Fix:** agregar `<Content Include="...">`.

### ❌ Edición manual con duplicados
Si pegas dos veces la misma entrada, MSBuild a veces lo tolera con warning, a veces falla. Buscar duplicados antes de commit:
```powershell
Select-String -Path WebSGV\WebSGV.csproj -Pattern "MiPagina" | Sort-Object Line | Get-Unique
```

## Evidence template para `task_result.evidence`

```json
{
  "command": "msbuild WebSGV.sln /p:Configuration=Debug /nologo /verbosity:minimal",
  "output_excerpt": "Build succeeded.\n    0 Warning(s)\n    0 Error(s)\nTime Elapsed 00:00:08.42",
  "verified_at": "2026-04-29T15:30:00Z"
}
```

Si hay warnings nuevos respecto al baseline, listarlos también.

## Checklist para developer al cerrar tarea

- [ ] Cada `.aspx` nuevo tiene 3 archivos en disco.
- [ ] Cada `.aspx` nuevo tiene 3 entradas en `WebSGV.csproj`.
- [ ] Cada `.cs` nuevo (helper/service/model) tiene `<Compile Include>`.
- [ ] Paths usan `\`, no `/`.
- [ ] `DependentUpon` no incluye path.
- [ ] `SubType=ASPXCodeBehind` solo en `.aspx.cs`.
- [ ] MSBuild output: 0 errores, capturado en `task_result.evidence`.
- [ ] Sin duplicados en csproj.
