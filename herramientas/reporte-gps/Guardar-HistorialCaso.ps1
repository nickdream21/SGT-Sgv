<#
    Guardar-HistorialCaso.ps1 — descarga el historial GPS de una placa y lo guarda como JSON
    para usarlo como caso de prueba de la trazabilidad (WebSGV.Tests/Datos/Trazabilidad/).

    Las horas van en hora local de Perú; el JSON guarda los puntos tal como los da el API
    (UTC), ordenados por hora de recepción, solo con los campos que usa la detección.

    Ejemplo:
      .\Guardar-HistorialCaso.ps1 -Placa ANU-794 -Desde "2026-10-18 12:00" -Hasta "2026-10-20 00:00" `
          -Salida ..\..\WebSGV.Tests\Datos\Trazabilidad\ANU-794_2026-10-18.json
#>
param(
    [Parameter(Mandatory)] [string]$Placa,
    [Parameter(Mandatory)] [DateTime]$Desde,
    [Parameter(Mandatory)] [DateTime]$Hasta,
    [Parameter(Mandatory)] [string]$Salida
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "OnwayCliente.ps1")
# Páginas grandes como en OnwayApiClient.cs (1 000 puntos): un día sale en 1-2 llamadas.
$script:PageSize = 1000

$con = Connect-Onway
$disp = Get-DispositivoPorPlaca $con $Placa
if (-not $disp) { throw "No hay un equipo GPS con la placa $Placa." }

$puntos = Get-Historial $con $disp.id (ConvertTo-Utc $Desde) (ConvertTo-Utc $Hasta)
$filas = $puntos |
    Sort-Object { if ($_.generateTime) { [DateTime]$_.generateTime } else { [DateTime]$_.messageTime } } |
    ForEach-Object {
        [ordered]@{
            messageTime      = $_.messageTime
            generateTime     = $_.generateTime
            lat              = $_.lat
            lng              = $_.lng
            speed            = $_.speed
            alertId          = $_.alertId
            alertDescription = if ($_.alertDescription) { [ordered]@{ es = $_.alertDescription.es; en = $_.alertDescription.en } } else { $null }
            # En las alertas de geocerca de Onway trae el nombre: "El dispositivo X salió de Base Viviana Sullana".
            body             = if ($_.body) { [ordered]@{ es = $_.body.es; en = $_.body.en } } else { $null }
        }
    }

$rutaSalida = if ([IO.Path]::IsPathRooted($Salida)) { $Salida } else { Join-Path (Get-Location) $Salida }
New-Item -ItemType Directory -Force (Split-Path -Parent $rutaSalida) | Out-Null
$json = ConvertTo-Json @($filas) -Depth 4 -Compress
[IO.File]::WriteAllText($rutaSalida, $json, (New-Object Text.UTF8Encoding $false))
Write-Host ("  {0} puntos de {1} guardados en {2}" -f @($filas).Count, $Placa, $rutaSalida)
