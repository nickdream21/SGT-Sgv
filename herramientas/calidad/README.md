# Calidad: cobertura de pruebas y analizadores

Cobertura de líneas de `WebSGV.dll` con el recolector "Code Coverage" que trae
`Microsoft.NET.Test.Sdk` (no hay que instalar nada). El CI (`.github/workflows/build.yml`) la mide
en cada corrida, falla si baja del mínimo y sube el XML como artefacto `cobertura`.

| Archivo | Qué es |
|---|---|
| `WebSGV.Tests/cobertura.runsettings` | Solo `WebSGV.dll`; sin `*.designer.cs` ni código generado |
| `medir-cobertura.ps1` | Lee el XML: total, por carpeta de `WebSGV/` y, con `-PorClase`, por clase; `-Comparar` sale con 1 si algo baja del mínimo |
| `cobertura-minima.json` | Mínimos de total, `Services` y `Helpers`: la cifra del CI redondeada hacia abajo al entero |

## Medir en la máquina

```powershell
msbuild WebSGV.sln /t:Build /p:Configuration=Release /nologo /verbosity:minimal

# Como el CI (sin BD): una cadena a una BD no permitida hace que las [FactBD] se salten.
$env:SGV_TEST_CONNSTR = 'Data Source=stub;Initial Catalog=stub;'
dotnet test WebSGV.Tests/WebSGV.Tests.csproj -c Release --no-build `
  --settings WebSGV.Tests/cobertura.runsettings `
  --collect "Code Coverage;Format=cobertura" --results-directory TestResults/cobertura
Remove-Item Env:SGV_TEST_CONNSTR

./herramientas/calidad/medir-cobertura.ps1 -Directorio TestResults/cobertura -Comparar
./herramientas/calidad/medir-cobertura.ps1 -Directorio TestResults/cobertura -PorClase
```

Sin `-Archivo`, el script toma el `*.cobertura.xml` más reciente bajo `-Directorio` (por defecto
`TestResults/`, ignorado por git). Con la BD de pruebas conectada la cifra sube (corren las
`[FactBD]`); el mínimo se fija siempre con la de CI, sin BD. Debug da décimas más que Release
(el CI compila en Release).

## El mínimo solo sube

Si una carpeta gana un entero, se sube su mínimo en `cobertura-minima.json` en el mismo commit.
Nunca se baja: si el CI falla por cobertura, se agregan pruebas.

# Analizadores con trinquete (E6-3)

`Microsoft.CodeAnalysis.NetAnalyzers` (paquete de desarrollo en `WebSGV/packages.config`, sus dos
DLL como `<Analyzer>` en `WebSGV.csproj`; no se publica) corre en cada compilación de WebSGV.

| Archivo | Qué es |
|---|---|
| `WebSGV/SGV.globalconfig` | Siempre: todo apagado (`dotnet_analyzer_diagnostic.severity = none`, lo mínimo) salvo CA2100, CA3007, CA5350, CA5368, CA2213 y CA2025 **en error**: rompen el build normal |
| `WebSGV/SGV.trinquete.globalconfig` | Solo con `/p:SgvContarAvisos=true`: CA1031, CA2000, CA3002, CA3003 y CA1062 como aviso |
| `contar-avisos.ps1` | Compila WebSGV con el trinquete y cuenta por regla; sale con 1 si alguna sube, si alguna baja sin actualizar la base o si el build falla |
| `avisos-base.json` | Cuenta por regla que no puede subir |

El build normal (Debug y Release) sigue con 0 avisos: con `TreatWarningsAsErrors` las reglas del
trinquete romperían, por eso solo se encienden al medir. El script compila con `/t:Compile`
(no copia nada a `bin\`), `/p:TreatWarningsAsErrors=false` y la carpeta intermedia
`WebSGV/obj/contar-avisos/` (ignorada por git; la borra antes para que el compilador corra
siempre). Tarda alrededor de un minuto por las reglas de flujo (CA3002, CA3003).

```powershell
./herramientas/calidad/contar-avisos.ps1                 # compara con la base (lo que hace el CI)
./herramientas/calidad/contar-avisos.ps1 -Detalle CA1031 # lista archivo:línea de una regla
./herramientas/calidad/contar-avisos.ps1 -Actualizar     # baja la base (solo si ninguna sube)
```

Cada aviso cuenta una vez (archivo, línea, columna y regla): las de flujo repiten el mismo punto
por cada origen del dato. El CI compila en Release; la cuenta se mide en Release.

## Reglas en error y falsos positivos

Un falso positivo de una regla en error se suprime en el código, junto al método o la clase, con
`[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CAxxxx:...", Justification = "motivo")]`;
nunca en el globalconfig ni con `#pragma` sin motivo. Un caso real se corrige.

## La base solo baja

Si una regla sube, se corrige el código nuevo (o se suprime un falso positivo con su motivo); la
base no se sube. Si baja, el script también falla (trinquete estricto): se corre `-Actualizar` y
se commitea `avisos-base.json` en el mismo commit.
