# =============================================================================
#  Regenera el reporte de tiempos por tramo a partir del Excel de seguimiento.
#
#  Uso:
#    .\Generar.ps1
#    .\Generar.ps1 -Excel "..\..\WebSGV\dashboard\STATUS GENERAL VIVIANA.xlsx" -Anio 2026
#
#  Requiere Node (para leer el xlsx y calcular) y Excel instalado (para armar el libro).
# =============================================================================
param(
  [string]$Excel  = "$PSScriptRoot\..\..\WebSGV\dashboard\STATUS GENERAL VIVIANA dashboard reunion.xlsx",
  [string]$Salida = "$PSScriptRoot\..\..\WebSGV\dashboard"
)
$ErrorActionPreference = 'Stop'

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("sgv-tiempos-" + [guid]::NewGuid().ToString('N').Substring(0,8))
$xl  = Join-Path $tmp 'xl'
$tsv = Join-Path $tmp 'tsv'
New-Item -ItemType Directory -Force $tmp, $tsv | Out-Null

Write-Host "1/5  Descomprimiendo el Excel de origen..."
if (-not (Test-Path $Excel)) { throw "No se encontro el archivo de origen: $Excel" }
Add-Type -AssemblyName System.IO.Compression.FileSystem
# ExtractToDirectory falla si la carpeta destino ya existe, por eso no se crea antes.
[System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $Excel), $xl)

Write-Host "2/5  Calculando tramos y ciclos..."
& node "$PSScriptRoot\modelo.js" $xl (Join-Path $tmp 'modelo.json')
if ($LASTEXITCODE -ne 0) { throw "modelo.js fallo" }

Write-Host "3/5  Preparando las tablas del libro..."
& node "$PSScriptRoot\export.js" (Join-Path $tmp 'modelo.json') $tsv
& node "$PSScriptRoot\importable.js" (Join-Path $tmp 'modelo.json') (Join-Path $tsv 'importar.tsv')

Write-Host "4/5  Armando el libro de Excel..."
& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\construir.ps1" `
    -Tsv $tsv -Salida (Join-Path $Salida 'Tiempos por tramo - Exportacion 2026.xlsx')

Write-Host "5/5  Armando el archivo de importacion al sistema..."
& powershell -NoProfile -ExecutionPolicy Bypass -File "$PSScriptRoot\importable.ps1" `
    -Tsv (Join-Path $tsv 'importar.tsv') -Salida (Join-Path $Salida 'Importar al sistema - Seguimiento 2026.xlsx')

Remove-Item $tmp -Recurse -Force
Write-Host ""
Write-Host "Listo. Los dos libros quedaron en: $Salida"
