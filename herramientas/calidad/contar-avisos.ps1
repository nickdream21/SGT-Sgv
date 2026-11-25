<#
.SYNOPSIS
    Trinquete de analizadores (E6-3): cuenta por regla los avisos de CA1031, CA2000, CA3002, CA3003
    y CA1062 en WebSGV y falla si alguna regla sube respecto de avisos-base.json.

.DESCRIPTION
    En la compilación normal esas reglas están apagadas (con TreatWarningsAsErrors romperían el
    build). Este script compila solo el código de WebSGV (/t:Compile, sin copiar nada a bin\) con
    /p:SgvContarAvisos=true, que suma WebSGV/SGV.trinquete.globalconfig (las 5 reglas como aviso),
    y /p:TreatWarningsAsErrors=false, en una carpeta intermedia aparte (obj\contar-avisos\, que se
    borra antes para que el compilador corra siempre).

    Cada aviso cuenta una vez (archivo, línea, columna y regla). Sale con 1 si alguna regla sube,
    si aparece una regla que no está en la base o si la compilación falla (p. ej. por una de las
    reglas en error de SGV.globalconfig). Si una regla baja, también sale con 1 (trinquete
    estricto): hay que correr -Actualizar y commitear avisos-base.json.

.PARAMETER Actualizar
    Reescribe avisos-base.json con las cifras de esta corrida, solo si ninguna sube.

.PARAMETER Log
    Lee un log de MSBuild ya generado en vez de compilar (p. ej. para revisar una corrida).

.PARAMETER Detalle
    Lista los avisos de esta regla (archivo:línea).

.EXAMPLE
    ./herramientas/calidad/contar-avisos.ps1
    ./herramientas/calidad/contar-avisos.ps1 -Actualizar
    ./herramientas/calidad/contar-avisos.ps1 -Detalle CA3002
#>
[CmdletBinding()]
param(
    [string]$Configuracion = 'Release',
    [string]$Base,
    [string]$Log,
    [switch]$Actualizar,
    [string]$Detalle
)

$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$proyecto = Join-Path $raiz 'WebSGV\WebSGV.csproj'
$carpetaWeb = Join-Path $raiz 'WebSGV'
if (-not $Base) { $Base = Join-Path $PSScriptRoot 'avisos-base.json' }

function Buscar-MSBuild {
    $enRuta = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($enRuta) { return $enRuta.Source }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $ruta = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($ruta) { return $ruta }
    }
    throw 'No se encontró MSBuild (ni en el PATH ni con vswhere).'
}

if (-not $Log) {
    $msbuild = Buscar-MSBuild
    $obj = Join-Path $carpetaWeb 'obj\contar-avisos'
    if (Test-Path $obj) { Remove-Item $obj -Recurse -Force }
    $Log = Join-Path ([IO.Path]::GetTempPath()) ('sgv-contar-avisos-' + [Guid]::NewGuid().ToString('N') + '.log')

    Write-Host "Compilando WebSGV ($Configuracion) con las reglas del trinquete..."
    & $msbuild $proyecto /t:Compile "/p:Configuration=$Configuracion" /p:SgvContarAvisos=true `
        /p:TreatWarningsAsErrors=false '/p:IntermediateOutputPath=obj\contar-avisos\' `
        /nologo /verbosity:quiet "/flp:LogFile=$Log;Verbosity=quiet;Encoding=UTF-8" | Out-Null
    $codigo = $LASTEXITCODE
    if ($codigo -ne 0) {
        Get-Content $Log -Encoding UTF8 | Where-Object { $_ -match ': error ' } | Select-Object -Unique | ForEach-Object { Write-Host $_ }
        Write-Host "La compilación falló (código $codigo): corregir los errores de arriba." -ForegroundColor Red
        exit 1
    }
}

if (-not (Test-Path $Log)) { throw "No existe el log $Log" }

# archivo(línea,columna): warning CAxxxx: mensaje
$patron = '^\s*(?<archivo>.+?)\((?<linea>\d+),(?<col>\d+)(,\d+,\d+)?\):\s+warning\s+(?<regla>CA\d{4}):'
$vistos = @{}
$avisos = New-Object System.Collections.Generic.List[object]
foreach ($linea in (Get-Content $Log -Encoding UTF8)) {
    $m = [regex]::Match($linea, $patron)
    if (-not $m.Success) { continue }
    $archivo = $m.Groups['archivo'].Value.Trim()
    if ($archivo.StartsWith($carpetaWeb, [StringComparison]::OrdinalIgnoreCase)) {
        $archivo = $archivo.Substring($carpetaWeb.Length).TrimStart('\', '/')
    }
    $archivo = $archivo -replace '\\', '/'
    $clave = '{0}|{1}|{2}|{3}' -f $archivo.ToLowerInvariant(), $m.Groups['linea'].Value, $m.Groups['col'].Value, $m.Groups['regla'].Value
    if ($vistos.ContainsKey($clave)) { continue }
    $vistos[$clave] = $true
    $avisos.Add([pscustomobject]@{ Regla = $m.Groups['regla'].Value; Archivo = $archivo; Linea = [int]$m.Groups['linea'].Value })
}

$actual = @{}
foreach ($a in $avisos) { $actual[$a.Regla] = 1 + [int]$actual[$a.Regla] }

$baseJson = $null
$cifrasBase = @{}
if (Test-Path $Base) {
    $baseJson = Get-Content $Base -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($p in $baseJson.reglas.PSObject.Properties) { $cifrasBase[$p.Name] = [int]$p.Value }
} elseif (-not $Actualizar) {
    throw "No existe $Base (correr con -Actualizar para crearla)."
}

$reglasTrinquete = @('CA1031', 'CA1062', 'CA2000', 'CA3002', 'CA3003')
$reglas = $reglasTrinquete + @($cifrasBase.Keys) + @($actual.Keys) | Sort-Object -Unique
$suben = @(); $bajan = @()
Write-Host ''
Write-Host ('{0,-8} {1,6} {2,6} {3,6}' -f 'Regla', 'Base', 'Ahora', 'Dif.')
foreach ($r in $reglas) {
    $b = if ($cifrasBase.ContainsKey($r)) { $cifrasBase[$r] } else { 0 }
    $n = [int]$actual[$r]
    $dif = $n - $b
    $marca = if ($dif -gt 0) { '  SUBE' } elseif ($dif -lt 0) { '  baja' } else { '' }
    Write-Host ('{0,-8} {1,6} {2,6} {3,6}{4}' -f $r, $b, $n, $dif, $marca)
    if ($dif -gt 0) { $suben += $r } elseif ($dif -lt 0) { $bajan += $r }
}
Write-Host ''

if ($Detalle) {
    $avisos | Where-Object Regla -eq $Detalle | Sort-Object Archivo, Linea |
        ForEach-Object { Write-Host ('{0}:{1}' -f $_.Archivo, $_.Linea) }
    Write-Host ''
}

if ($suben.Count -gt 0 -and $null -ne $baseJson) {
    Write-Host ("Subieron: {0}. Corregir el código nuevo (o suprimir un falso positivo con [SuppressMessage] y su motivo); la base no se sube." -f ($suben -join ', ')) -ForegroundColor Red
    Write-Host "Para ver cuáles: -Detalle <regla>."
    exit 1
}

if ($Actualizar) {
    # JSON escrito a mano: igual en Windows PowerShell 5.1 y en pwsh (ConvertTo-Json sangra distinto).
    $descripcion = 'Avisos por regla de los analizadores con trinquete (E6-3). La cifra no puede subir; si baja, se baja aquí con herramientas/calidad/contar-avisos.ps1 -Actualizar.'
    $filas = $reglas | ForEach-Object { '    "{0}": {1}' -f $_, [int]$actual[$_] }
    $texto = "{`n  ""descripcion"": ""$descripcion"",`n  ""reglas"": {`n" + ($filas -join ",`n") + "`n  }`n}`n"
    [IO.File]::WriteAllText($Base, $texto, (New-Object System.Text.UTF8Encoding $false))
    Write-Host "Base actualizada: $Base"
    exit 0
}

if ($bajan.Count -gt 0) {
    # Trinquete estricto: si baja y no se baja la base, el margen ganado se podría volver a gastar.
    Write-Host ("Bajó: {0}. Corre -Actualizar y commitea avisos-base.json." -f ($bajan -join ', ')) -ForegroundColor Red
    exit 1
}
Write-Host 'Ninguna regla del trinquete subió.'
exit 0
