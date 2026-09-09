<#
    Generar-ReporteTramos.ps1 — reporte en Excel de los tramos recorridos por un vehículo,
    con datos del GPS (Entel Onway / Location World).

    Uso típico (token generado a mano en Postman, porque Location World solo permite un
    token activo por client_id):

        .\Generar-ReporteTramos.ps1 -AccessToken "eyJ..." -ExpiresIn 86400

    Si ya hay un token vigente en la tabla OnwayAuthCache, basta con:

        .\Generar-ReporteTramos.ps1

    Para recalcular sin volver a consultar el API (usa el JSON crudo ya descargado en datos\):

        .\Generar-ReporteTramos.ps1 -SinDescargar

    Los tramos a reportar se definen en $Tramos, más abajo: es lo único que hay que editar
    para generar el mismo reporte sobre otras fechas u otro vehículo.
#>

[CmdletBinding()]
param(
    [string]$AccessToken,
    [int]$ExpiresIn = 0,
    [switch]$SinDescargar,
    [string]$Salida,
    [double]$UmbralKmh = 3,
    [double]$MinutosParada = 3
)

$ErrorActionPreference = "Stop"
. "$PSScriptRoot\OnwayCliente.ps1"
. "$PSScriptRoot\AnalisisTramo.ps1"
. "$PSScriptRoot\ExcelWriter.ps1"

# ===========================================================================
#  Definición de los tramos (horas en hora local de Perú, UTC-5)
# ===========================================================================
$Placa = "CBV-829"

$Tramos = @(
    @{
        Clave      = "T1"
        Nombre     = "Tramo 1: Base → Planta Trujillo"
        Origen     = "Base Sullana (-4.956336, -80.697525)"
        Destino    = "Planta Trujillo (-8.136977, -79.013210)"
        SalidaLocal  = [DateTime]::new(2026, 8, 26, 16, 32, 27)
        LlegadaLocal = [DateTime]::new(2026, 8, 27,  1, 15, 45)
    },
    @{
        Clave      = "T2"
        Nombre     = "Tramo 2: Base → Planta Guayaquil (Ecuador)"
        Origen     = "Base Sullana (-4.956250, -80.697334)"
        Destino    = "Planta Guayaquil, Ecuador (-2.175920, -79.794890)"
        SalidaLocal  = [DateTime]::new(2026, 8, 28,  5, 53,  1)
        LlegadaLocal = [DateTime]::new(2026, 8, 29,  8, 43, 43)
    }
)

$carpetaDatos  = Join-Path $PSScriptRoot "datos"
$carpetaSalida = Join-Path $PSScriptRoot "salida"
if (-not $Salida) { $Salida = Join-Path $carpetaSalida ("ReporteGPS_{0}_2tramos.xlsx" -f $Placa) }
foreach ($c in @($carpetaDatos, $carpetaSalida)) { if (-not (Test-Path $c)) { [void](New-Item -ItemType Directory -Path $c -Force) } }

function Save-Json($objeto, [string]$ruta) {
    [IO.File]::WriteAllText($ruta, (@($objeto) | ConvertTo-Json -Depth 6 -Compress), (New-Object Text.UTF8Encoding($false)))
}
function Read-Json([string]$ruta) {
    if (-not (Test-Path $ruta)) { return @() }
    $texto = [IO.File]::ReadAllText($ruta)
    if ([string]::IsNullOrWhiteSpace($texto)) { return @() }
    return @(ConvertFrom-Json $texto)
}

# ===========================================================================
#  1. Descarga
# ===========================================================================
if (-not $SinDescargar) {
    Write-Host "== Conectando al API de Onway"
    if ($AccessToken) { [void](Set-TokenManual $AccessToken $ExpiresIn) }
    $con = Connect-Onway

    Write-Host "== Buscando el dispositivo de la placa $Placa"
    $dispositivo = Get-DispositivoPorPlaca $con $Placa
    if (-not $dispositivo) { throw "No se encontró ningún dispositivo con alias '$Placa' en la cuenta." }
    Write-Host ("  {0} -> IMEI {1} ({2})" -f $dispositivo.alias, $dispositivo.imei, $dispositivo.deviceTypeDescription)
    Save-Json $dispositivo (Join-Path $carpetaDatos "dispositivo.json")

    foreach ($t in $Tramos) {
        $desdeUtc = ConvertTo-Utc $t.SalidaLocal
        $hastaUtc = ConvertTo-Utc $t.LlegadaLocal
        Write-Host ("== Descargando {0}" -f $t.Nombre)

        $historial = Get-Historial $con $dispositivo.id $desdeUtc $hastaUtc
        Write-Host ("  historial: {0} puntos" -f @($historial).Count)
        Save-Json $historial (Join-Path $carpetaDatos "$($t.Clave)_historial.json")

        # Los endpoints de viajes/indicadores agrupan por día y devuelven el viaje según el
        # día en que ARRANCÓ: hay que barrer también el día anterior y recortar después.
        $viajes = New-Object System.Collections.ArrayList
        $indicadores = New-Object System.Collections.ArrayList
        for ($dia = $desdeUtc.Date.AddDays(-1); $dia -le $hastaUtc.Date; $dia = $dia.AddDays(1)) {
            $ini = [DateTime]::SpecifyKind($dia, [DateTimeKind]::Utc)
            $fin = $ini.AddDays(1).AddSeconds(-1)
            foreach ($v in @(Get-Viajes $con $dispositivo.id $ini $fin)) { [void]$viajes.Add($v) }
            foreach ($x in @(Get-IndicadoresOperacion $con $dispositivo.id $ini $fin)) { [void]$indicadores.Add($x) }
        }
        Write-Host ("  viajes: {0} | indicadores: {1}" -f $viajes.Count, $indicadores.Count)
        Save-Json $viajes.ToArray()      (Join-Path $carpetaDatos "$($t.Clave)_viajes.json")
        Save-Json $indicadores.ToArray() (Join-Path $carpetaDatos "$($t.Clave)_indicadores.json")
    }
} else {
    Write-Host "== Modo -SinDescargar: usando el JSON ya guardado en datos\"
}

# ===========================================================================
#  2. Análisis
# ===========================================================================
$resultados = [ordered]@{}
foreach ($t in $Tramos) {
    Write-Host ("== Analizando {0}" -f $t.Nombre)
    $resultados[$t.Clave] = Get-MetricasTramo `
        -Historial   (Read-Json (Join-Path $carpetaDatos "$($t.Clave)_historial.json")) `
        -Viajes      (Read-Json (Join-Path $carpetaDatos "$($t.Clave)_viajes.json")) `
        -Indicadores (Read-Json (Join-Path $carpetaDatos "$($t.Clave)_indicadores.json")) `
        -Nombre $t.Nombre -Origen $t.Origen -Destino $t.Destino `
        -DesdeUtc (ConvertTo-Utc $t.SalidaLocal) -HastaUtc (ConvertTo-Utc $t.LlegadaLocal) `
        -UmbralKmh $UmbralKmh -MinutosParada $MinutosParada
}

# ===========================================================================
#  3. Excel
# ===========================================================================
$secciones = @(
    @{ Titulo = "IDENTIFICACIÓN DEL TRAMO"; Claves = @("Tramo", "Origen", "Destino", "Salida (hora Perú)", "Llegada (hora Perú)", "Duración total del tramo", "Duración total (horas)") }
    @{ Titulo = "TIEMPO EN MOVIMIENTO VS. DETENIDO"; Claves = @("Tiempo en movimiento", "Tiempo en movimiento (horas)", "Tiempo detenido", "Tiempo detenido (horas)", "% del tiempo en movimiento", "% del tiempo detenido", "Paradas (≥ $MinutosParada min)", "Detenciones menores (< $MinutosParada min)", "Tiempo en detenciones menores") }
    @{ Titulo = "DISTANCIA RECORRIDA"; Claves = @("Distancia por odómetro (km)", "Odómetro al inicio (km)", "Odómetro al final (km)", "Distancia sumando viajes del API (km)", "Distancia calculada sobre la traza GPS (km)", "Diferencia odómetro vs. viajes (%)") }
    @{ Titulo = "VELOCIDADES"; Claves = @("Velocidad promedio del tramo (km/h)", "Velocidad promedio en movimiento (km/h)", "Velocidad máxima registrada (km/h)") }
    @{ Titulo = "MOTOR Y HORAS DE OPERACIÓN"; Claves = @("Viajes (encendidos) del API", "Tiempo con motor en marcha según viajes", "Tiempo con motor en marcha (horas)", "Horas de operación (API)", "Horas en movimiento (API)", "Horas en ralentí (API)", "Combustible consumido (API)", "Horómetro por punto") }
    @{ Titulo = "CALIDAD DEL DATO GPS"; Claves = @("Puntos GPS analizados", "Frecuencia media de reporte", "Huecos de señal (> 20 min)") }
)

$filasResumen = New-Object System.Collections.ArrayList
[void]$filasResumen.Add(@("Concepto") + @($Tramos | ForEach-Object { $_.Nombre }))
foreach ($sec in $secciones) {
    [void]$filasResumen.Add(@(@{ V = $sec.Titulo; S = "seccion" }) + @($Tramos | ForEach-Object { @{ V = ""; S = "seccion" } }))
    foreach ($clave in $sec.Claves) {
        $fila = New-Object System.Collections.ArrayList
        [void]$fila.Add(@{ V = $clave; S = "texto" })
        foreach ($t in $Tramos) {
            $r = $resultados[$t.Clave].Resumen
            [void]$fila.Add($(if ($r.Contains($clave)) { $r[$clave] } else { "" }))
        }
        [void]$filasResumen.Add($fila.ToArray())
    }
}
[void]$filasResumen.Add(@(""))
[void]$filasResumen.Add(@(@{ V = "Criterio de parada: velocidad ≤ $UmbralKmh km/h y odómetro sin avance durante $MinutosParada minutos o más."; S = "texto" }))
[void]$filasResumen.Add(@(@{ V = "Fuente: Customer API de Location World / Entel Onway (historial de posiciones, viajes e indicadores de operación). Odómetro tomado del campo 'mileage' de cada posición."; S = "texto" }))
[void]$filasResumen.Add(@(@{ V = ("Generado el {0} para la placa {1}." -f (Get-Date).ToString("dd/MM/yyyy HH:mm"), $Placa); S = "texto" }))

$hojas = New-Object System.Collections.ArrayList
[void]$hojas.Add(@{ Nombre = "Resumen"; Anchos = @(44, 34, 34); Filas = $filasResumen.ToArray() })

foreach ($t in $Tramos) {
    $res = $resultados[$t.Clave]

    $filasParadas = New-Object System.Collections.ArrayList
    [void]$filasParadas.Add(@("#", "Inicio (hora Perú)", "Fin (hora Perú)", "Duración", "Duración (h)", "Latitud", "Longitud", "Ubicación", "Odómetro (km)"))
    $n = 0
    foreach ($p in @($res.Paradas)) {
        $n++
        [void]$filasParadas.Add(@(
            $n,
            (Format-HoraLocal $p.InicioUtc),
            (Format-HoraLocal $p.FinUtc),
            (Format-Duracion ([TimeSpan]::FromSeconds($p.Segundos))),
            [Math]::Round($p.Segundos / 3600, 2),
            $p.Lat.ToString("F6", [Globalization.CultureInfo]::InvariantCulture),
            $p.Lng.ToString("F6", [Globalization.CultureInfo]::InvariantCulture),
            $p.Direccion,
            $(if ($null -ne $p.Odometro) { [Math]::Round([double]$p.Odometro, 2) } else { "" })
        ))
    }
    if ($n -eq 0) { [void]$filasParadas.Add(@("", "Sin paradas de $MinutosParada minutos o más en este tramo.")) }
    [void]$hojas.Add(@{ Nombre = "$($t.Clave) Paradas"; Anchos = @(5, 21, 21, 14, 12, 13, 13, 52, 14); Filas = $filasParadas.ToArray() })

    $filasViajes = New-Object System.Collections.ArrayList
    [void]$filasViajes.Add(@("#", "Inicio (hora Perú)", "Fin (hora Perú)", "Duración (min)", "Dentro del tramo (min)", "Distancia (km)", "Vel. promedio (km/h)", "Score", "Frenadas bruscas", "Aceleraciones bruscas", "Excesos de velocidad", "Desde", "Hasta"))
    $n = 0
    foreach ($v in @($res.Viajes)) {
        $n++
        [void]$filasViajes.Add(@(
            $n,
            (Format-HoraLocal $v.InicioUtc),
            (Format-HoraLocal $v.FinUtc),
            [Math]::Round($v.MinutosTotales, 1),
            [Math]::Round($v.MinutosEnTramo, 1),
            [Math]::Round($v.Distancia, 2),
            [Math]::Round($v.VelPromedio, 1),
            [Math]::Round($v.Score, 0),
            [int]$v.Frenadas,
            [int]$v.Aceleraciones,
            [int]$v.Excesos,
            $v.DireccionIni,
            $v.DireccionFin
        ))
    }
    if ($n -eq 0) { [void]$filasViajes.Add(@("", "El API no reportó viajes en este tramo.")) }
    [void]$hojas.Add(@{ Nombre = "$($t.Clave) Viajes"; Anchos = @(5, 21, 21, 15, 20, 14, 18, 8, 16, 20, 18, 46, 46); Filas = $filasViajes.ToArray() })
}

$ruta = New-LibroExcel -Ruta $Salida -Hojas $hojas.ToArray()

Write-Host ""
Write-Host "== Reporte generado: $ruta"
foreach ($t in $Tramos) {
    $r = $resultados[$t.Clave].Resumen
    Write-Host ("   {0}" -f $t.Nombre)
    Write-Host ("     distancia {0} km | total {1} | movimiento {2} | detenido {3} | {4} paradas" -f `
        $r["Distancia por odómetro (km)"], $r["Duración total del tramo"], $r["Tiempo en movimiento"], $r["Tiempo detenido"], $r["Paradas (≥ $MinutosParada min)"])
}
