<#
.SYNOPSIS
    Lee la cobertura de WebSGV.dll (XML cobertura de "dotnet test --collect Code Coverage") e
    imprime el % de líneas total, por carpeta de primer nivel de WebSGV y, si se pide, por clase.

.DESCRIPTION
    Con -Comparar contrasta el resultado con herramientas/calidad/cobertura-minima.json y sale con
    código 1 si el total o alguna carpeta queda por debajo de su mínimo (lo usa el CI, E6-1).

    Las líneas se cuentan como el propio recolector (las de cada método, igual que lines-valid y
    lines-covered del encabezado del XML), así el total coincide con el que da Visual Studio.

.PARAMETER Archivo
    XML cobertura a leer. Si se omite, se usa el *.cobertura.xml más reciente bajo -Directorio.

.PARAMETER Directorio
    Carpeta donde buscar el XML (recursivo). Por defecto, TestResults en la raíz del repo.

.PARAMETER PorClase
    Imprime también cada clase (las anidadas y las que genera el compilador se suman a su clase).

.PARAMETER Comparar
    Compara con el archivo de mínimos y sale con código 1 si algo baja.

.PARAMETER Minimos
    Archivo JSON de mínimos. Por defecto, cobertura-minima.json junto a este script.

.EXAMPLE
    ./herramientas/calidad/medir-cobertura.ps1 -Directorio TestResults/cobertura -Comparar
#>
[CmdletBinding()]
param(
    [string]$Archivo,
    [string]$Directorio,
    [switch]$PorClase,
    [switch]$Comparar,
    [string]$Minimos
)

$ErrorActionPreference = 'Stop'
$raizRepo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

if (-not $Archivo) {
    if (-not $Directorio) { $Directorio = Join-Path $raizRepo 'TestResults' }
    if (-not (Test-Path $Directorio)) { throw "No existe la carpeta '$Directorio'." }
    $hallado = Get-ChildItem -Path $Directorio -Recurse -File -Filter '*.cobertura.xml' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $hallado) { throw "No hay ningún *.cobertura.xml bajo '$Directorio'." }
    $Archivo = $hallado.FullName
}
if (-not (Test-Path $Archivo)) { throw "No existe el archivo '$Archivo'." }
if (-not $Minimos) { $Minimos = Join-Path $PSScriptRoot 'cobertura-minima.json' }

Write-Host "Cobertura: $Archivo"
$xml = New-Object System.Xml.XmlDocument
$xml.Load((Resolve-Path $Archivo).Path)

# Carpeta de primer nivel dentro de WebSGV/ (Services, Helpers, Views...) o "(raíz)".
function Get-Carpeta([string]$ruta) {
    $normal = $ruta -replace '/', '\'
    $i = $normal.LastIndexOf('\WebSGV\', [StringComparison]::OrdinalIgnoreCase)
    if ($i -lt 0) { return '(otro)' }
    $relativa = $normal.Substring($i + 8)
    $barra = $relativa.IndexOf('\')
    if ($barra -lt 0) { return '(raíz)' }
    return $relativa.Substring(0, $barra)
}

# Clase "de usuario": sin las anidadas ni <>c, <Metodo>d__0, etc. (Ej.: WebSGV.Global.<>c -> WebSGV.Global)
function Get-ClaseBase([string]$nombre) {
    $base = ($nombre -split '[/+]')[0]
    $base = $base -replace '\.<[^>]*>.*$', ''
    return $base
}

$porCarpeta = @{}
$porClaseTabla = @{}
$totalValidas = 0
$totalCubiertas = 0

foreach ($clase in $xml.SelectNodes('//packages/package/classes/class')) {
    $lineas = $clase.SelectNodes('methods/method/lines/line')
    if ($lineas.Count -eq 0) { continue }
    $cubiertas = 0
    foreach ($l in $lineas) { if ([long]$l.GetAttribute('hits') -gt 0) { $cubiertas++ } }

    $carpeta = Get-Carpeta $clase.GetAttribute('filename')
    if (-not $porCarpeta.ContainsKey($carpeta)) { $porCarpeta[$carpeta] = @(0, 0) }
    $porCarpeta[$carpeta][0] += $lineas.Count
    $porCarpeta[$carpeta][1] += $cubiertas

    if ($PorClase) {
        $clave = $carpeta + '|' + (Get-ClaseBase $clase.GetAttribute('name'))
        if (-not $porClaseTabla.ContainsKey($clave)) { $porClaseTabla[$clave] = @(0, 0) }
        $porClaseTabla[$clave][0] += $lineas.Count
        $porClaseTabla[$clave][1] += $cubiertas
    }

    $totalValidas += $lineas.Count
    $totalCubiertas += $cubiertas
}

if ($totalValidas -eq 0) { throw "El XML no tiene líneas medidas (¿se instrumentó WebSGV.dll?)." }

# Sin redondear: la comparación con el mínimo usa la cifra exacta; solo se redondea al imprimir.
function Get-Porcentaje($cubiertas, $validas) {
    if ($validas -eq 0) { return 0.0 }
    return 100.0 * $cubiertas / $validas
}

$cultura = [Globalization.CultureInfo]::GetCultureInfo('es-PE')
# Truncado a un decimal (no redondeado): 36,96 se ve 36,9 y nunca "37,0 bajó del mínimo 37".
function Format-Pct([double]$pct) { return ([math]::Floor($pct * 10) / 10).ToString('0.0', $cultura) }

function Format-Fila([string]$nombre, $cubiertas, $validas, [int]$ancho) {
    $pct = (Format-Pct (Get-Porcentaje $cubiertas $validas))
    return ('{0} {1,6} %  ({2} de {3} líneas)' -f $nombre.PadRight($ancho), $pct, $cubiertas, $validas)
}

$resultado = [ordered]@{ total = Get-Porcentaje $totalCubiertas $totalValidas }
Write-Host ''
Write-Host (Format-Fila 'Total WebSGV' $totalCubiertas $totalValidas 22)
foreach ($carpeta in ($porCarpeta.Keys | Sort-Object)) {
    $v = $porCarpeta[$carpeta]
    $resultado[$carpeta] = Get-Porcentaje $v[1] $v[0]
    Write-Host (Format-Fila ('  ' + $carpeta) $v[1] $v[0] 22)
}

if ($PorClase) {
    Write-Host ''
    Write-Host 'Por clase (carpeta | clase):'
    $ancho = ($porClaseTabla.Keys | Measure-Object -Property Length -Maximum).Maximum + 2
    foreach ($clave in ($porClaseTabla.Keys | Sort-Object)) {
        $v = $porClaseTabla[$clave]
        Write-Host (Format-Fila ('  ' + $clave) $v[1] $v[0] $ancho)
    }
}

if ($Comparar) {
    if (-not (Test-Path $Minimos)) { throw "No existe el archivo de mínimos '$Minimos'." }
    $tope = Get-Content -Path $Minimos -Raw -Encoding UTF8 | ConvertFrom-Json
    Write-Host ''
    Write-Host "Mínimos ($Minimos):"
    $fallas = 0
    foreach ($p in $tope.minimos.PSObject.Properties) {
        $nombre = $p.Name
        $minimo = [double]$p.Value
        if (-not $resultado.Contains($nombre)) {
            Write-Host ("  {0}: no aparece en la cobertura (mínimo {1} %)" -f $nombre, $minimo)
            $fallas++
            continue
        }
        $medido = [double]$resultado[$nombre]
        if ($medido -lt $minimo) {
            Write-Host ("  {0}: {1} % BAJÓ del mínimo {2} %" -f $nombre, (Format-Pct $medido), $minimo)
            $fallas++
        }
        else {
            Write-Host ("  {0}: {1} % >= {2} % (bien)" -f $nombre, (Format-Pct $medido), $minimo)
        }
    }
    if ($fallas -gt 0) {
        Write-Host ''
        Write-Host "La cobertura bajó del mínimo en $fallas caso(s). Agrega pruebas; el mínimo no se baja."
        exit 1
    }
    Write-Host ''
    Write-Host 'Cobertura dentro de los mínimos. Si subió un entero, sube también el mínimo en el JSON.'
}
exit 0
