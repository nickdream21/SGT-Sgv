# Calidad: cobertura de pruebas

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
