<#
    AnalisisTramo.ps1 — cálculo de métricas de un tramo a partir de los datos del GPS.

    Criterio de movimiento: un intervalo entre dos posiciones consecutivas se considera
    DETENIDO cuando el odómetro no avanzó (< 50 m) y la velocidad de ambos extremos fue
    <= UmbralKmh. Las rachas detenidas de >= MinutosParada se consolidan como "parada";
    las más cortas (semáforos, frenadas, peajes) se reportan aparte como detenciones menores.

    El odómetro (campo `mileage` del historial, no documentado en el swagger pero presente
    en la respuesta real) es la fuente principal de distancia: coincide con la columna
    "Odómetro (Km)" del portal de Onway.
#>

function Get-DistanciaHaversine([double]$lat1, [double]$lon1, [double]$lat2, [double]$lon2) {
    $r = 6371.0088
    $dLat = ($lat2 - $lat1) * [Math]::PI / 180
    $dLon = ($lon2 - $lon1) * [Math]::PI / 180
    $a = [Math]::Sin($dLat / 2) * [Math]::Sin($dLat / 2) +
         [Math]::Cos($lat1 * [Math]::PI / 180) * [Math]::Cos($lat2 * [Math]::PI / 180) *
         [Math]::Sin($dLon / 2) * [Math]::Sin($dLon / 2)
    return $r * 2 * [Math]::Atan2([Math]::Sqrt($a), [Math]::Sqrt(1 - $a))
}

function Format-Duracion([TimeSpan]$ts) {
    $h = [int][Math]::Floor($ts.TotalHours)
    return ("{0} h {1:00} min" -f $h, $ts.Minutes)
}

function Format-HoraLocal([DateTime]$utc) {
    return (ConvertFrom-Utc $utc).ToString("dd/MM/yyyy HH:mm:ss")
}

<#  Normaliza a UTC lo que venga: ConvertFrom-Json de PowerShell 5.1 a veces ya convierte
    las cadenas ISO 8601 en [DateTime] (con Kind=Local), y otras las deja como texto. #>
function ConvertTo-InstanteUtc($valor) {
    if ($valor -is [DateTime]) {
        if ($valor.Kind -eq [DateTimeKind]::Unspecified) { return [DateTime]::SpecifyKind($valor, [DateTimeKind]::Utc) }
        return $valor.ToUniversalTime()
    }
    return [DateTime]::SpecifyKind(
        [DateTime]::Parse([string]$valor, [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::AdjustToUniversal),
        [DateTimeKind]::Utc)
}

function ConvertTo-PuntosOrdenados($historial) {
    $lista = New-Object System.Collections.ArrayList
    foreach ($p in @($historial)) {
        [void]$lista.Add([PSCustomObject]@{
            Utc       = ConvertTo-InstanteUtc $p.messageTime
            Lat       = [double]$p.lat
            Lng       = [double]$p.lng
            Velocidad = [double]$p.speed
            Odometro  = if ($null -ne $p.mileage) { [double]$p.mileage } else { $null }
            Direccion = [string]$p.address
        })
    }
    return (@($lista) | Sort-Object Utc)
}

<#  Recorta los viajes del API al rango del tramo y los devuelve como filas listas para Excel. #>
function Get-ViajesDelTramo($viajes, [DateTime]$desdeUtc, [DateTime]$hastaUtc) {
    $filas = New-Object System.Collections.ArrayList
    $vistos = @{}
    $ordenados = @($viajes) | Where-Object { $_ -and $_.startedOn } |
        Sort-Object { ConvertTo-InstanteUtc $_.startedOn }

    foreach ($v in $ordenados) {
        if ($vistos.ContainsKey([string]$v.tripNumber)) { continue }
        $ini = ConvertTo-InstanteUtc $v.startedOn
        $fin = ConvertTo-InstanteUtc $v.endedOn
        if ($fin -le $desdeUtc -or $ini -ge $hastaUtc) { continue }   # no solapa el tramo
        $vistos[[string]$v.tripNumber] = $true

        $iniRec = if ($ini -lt $desdeUtc) { $desdeUtc } else { $ini }
        $finRec = if ($fin -gt $hastaUtc) { $hastaUtc } else { $fin }
        [void]$filas.Add([PSCustomObject]@{
            InicioUtc      = $ini
            FinUtc         = $fin
            MinutosEnTramo = ($finRec - $iniRec).TotalMinutes
            MinutosTotales = [double]$v.elapsedTimeInMinutes
            Recortado      = ($ini -lt $desdeUtc -or $fin -gt $hastaUtc)
            DireccionIni   = [string]$v.startAddress
            DireccionFin   = [string]$v.endAddress
            Distancia      = [double]$v.scoreboard.distance
            VelPromedio    = [double]$v.scoreboard.averageSpeed
            VelMaxima      = [double]$v.scoreboard.maxSpeed
            Score          = [double]$v.scoreboard.score
            Frenadas       = [int]$v.scoreboard.suddenBreakings
            Aceleraciones  = [int]$v.scoreboard.harshAccelerations
            Excesos        = [int]$v.scoreboard.overSpeeds
        })
    }
    return $filas.ToArray()
}

function Get-MetricasTramo {
    param(
        [Parameter(Mandatory)] $Historial,
        $Viajes, $Indicadores,
        [Parameter(Mandatory)][string]$Nombre,
        [Parameter(Mandatory)][string]$Origen,
        [Parameter(Mandatory)][string]$Destino,
        [Parameter(Mandatory)][DateTime]$DesdeUtc,
        [Parameter(Mandatory)][DateTime]$HastaUtc,
        [double]$UmbralKmh = 3,
        [double]$MinutosParada = 3,
        [double]$MinutosHueco = 20
    )

    $pts = @(ConvertTo-PuntosOrdenados $Historial)
    if ($pts.Count -lt 2) { throw "El tramo '$Nombre' no trajo suficientes puntos GPS ($($pts.Count))." }

    $duracionTotal = $HastaUtc - $DesdeUtc
    $kmHaversine   = 0.0
    $velMaxima     = 0.0
    $huecos        = New-Object System.Collections.ArrayList
    $intervalos    = New-Object System.Collections.ArrayList

    # --- Paso 1: clasificar cada intervalo entre posiciones consecutivas
    for ($i = 0; $i -lt $pts.Count; $i++) {
        if ($pts[$i].Velocidad -gt $velMaxima) { $velMaxima = $pts[$i].Velocidad }
        if ($i -eq 0) { continue }

        $a = $pts[$i - 1]; $b = $pts[$i]
        $seg = ($b.Utc - $a.Utc).TotalSeconds
        if ($seg -le 0) { continue }

        $dOdo = $null
        if ($null -ne $a.Odometro -and $null -ne $b.Odometro -and $a.Odometro -gt 0 -and $b.Odometro -gt 0) {
            $dOdo = $b.Odometro - $a.Odometro
        }
        $kmHaversine += Get-DistanciaHaversine $a.Lat $a.Lng $b.Lat $b.Lng

        $enMovimiento = if ($null -ne $dOdo) {
            ($dOdo -ge 0.05) -or ($a.Velocidad -gt $UmbralKmh -and $b.Velocidad -gt $UmbralKmh)
        } else {
            ($a.Velocidad -gt $UmbralKmh -or $b.Velocidad -gt $UmbralKmh)
        }

        if ($seg -ge ($MinutosHueco * 60)) {
            [void]$huecos.Add([PSCustomObject]@{
                InicioUtc = $a.Utc; FinUtc = $b.Utc; Segundos = $seg
                Km = $(if ($null -ne $dOdo) { $dOdo } else { 0 })
                Direccion = $a.Direccion
            })
        }

        [void]$intervalos.Add([PSCustomObject]@{
            Desde = $a.Utc; Hasta = $b.Utc; Segundos = $seg
            EnMovimiento = $enMovimiento; Punto = $a
        })
    }

    $segMovimiento = (@($intervalos) | Where-Object EnMovimiento     | Measure-Object Segundos -Sum).Sum
    $segDetenido   = (@($intervalos) | Where-Object { -not $_.EnMovimiento } | Measure-Object Segundos -Sum).Sum
    if ($null -eq $segMovimiento) { $segMovimiento = 0.0 }
    if ($null -eq $segDetenido)   { $segDetenido = 0.0 }

    # --- Paso 2: agrupar intervalos detenidos consecutivos en rachas
    $paradas         = New-Object System.Collections.ArrayList
    $menoresConteo   = 0
    $menoresSegundos = 0.0
    $racha = $null

    foreach ($iv in @($intervalos) + @($null)) {   # el $null final cierra la última racha
        if ($null -ne $iv -and -not $iv.EnMovimiento) {
            if ($null -eq $racha) {
                $racha = [PSCustomObject]@{ InicioUtc = $iv.Desde; FinUtc = $iv.Hasta; Segundos = 0.0; Punto = $iv.Punto }
            }
            $racha.Segundos += $iv.Segundos
            $racha.FinUtc = $iv.Hasta
            continue
        }
        if ($null -ne $racha) {
            if ($racha.Segundos -ge ($MinutosParada * 60)) {
                [void]$paradas.Add([PSCustomObject]@{
                    InicioUtc = $racha.InicioUtc; FinUtc = $racha.FinUtc; Segundos = $racha.Segundos
                    Lat = $racha.Punto.Lat; Lng = $racha.Punto.Lng
                    Direccion = $racha.Punto.Direccion; Odometro = $racha.Punto.Odometro
                })
            } else {
                $menoresConteo++
                $menoresSegundos += $racha.Segundos
            }
            $racha = $null
        }
    }

    # ---- distancias
    $odoIni = ($pts | Where-Object { $null -ne $_.Odometro -and $_.Odometro -gt 0 } | Select-Object -First 1).Odometro
    $odoFin = ($pts | Where-Object { $null -ne $_.Odometro -and $_.Odometro -gt 0 } | Select-Object -Last 1).Odometro
    $kmOdometro = if ($null -ne $odoIni -and $null -ne $odoFin) { $odoFin - $odoIni } else { $null }

    $filasViajes = @(Get-ViajesDelTramo $Viajes $DesdeUtc $HastaUtc)
    $kmViajes    = ($filasViajes | Measure-Object Distancia -Sum).Sum
    $minMotor    = ($filasViajes | Measure-Object MinutosEnTramo -Sum).Sum

    $kmReferencia = if ($null -ne $kmOdometro -and $kmOdometro -gt 0) { $kmOdometro } else { $kmHaversine }
    $hMovimiento  = $segMovimiento / 3600
    $hDetenido    = $segDetenido / 3600
    $hTotal       = $duracionTotal.TotalHours

    $desvio = if ($kmViajes -gt 0) { [Math]::Abs($kmReferencia - $kmViajes) / $kmViajes * 100 } else { 0 }

    $resumen = [ordered]@{
        "Tramo"                                  = $Nombre
        "Origen"                                 = $Origen
        "Destino"                                = $Destino
        "Salida (hora Perú)"                     = Format-HoraLocal $DesdeUtc
        "Llegada (hora Perú)"                    = Format-HoraLocal $HastaUtc
        "Duración total del tramo"               = Format-Duracion $duracionTotal
        "Duración total (horas)"                 = [Math]::Round($hTotal, 2)
        "Tiempo en movimiento"                   = Format-Duracion ([TimeSpan]::FromSeconds($segMovimiento))
        "Tiempo en movimiento (horas)"           = [Math]::Round($hMovimiento, 2)
        "Tiempo detenido"                        = Format-Duracion ([TimeSpan]::FromSeconds($segDetenido))
        "Tiempo detenido (horas)"                = [Math]::Round($hDetenido, 2)
        "% del tiempo en movimiento"             = [Math]::Round(($hMovimiento / $hTotal) * 100, 1)
        "% del tiempo detenido"                  = [Math]::Round(($hDetenido / $hTotal) * 100, 1)
        "Paradas (≥ $MinutosParada min)"         = $paradas.Count
        "Detenciones menores (< $MinutosParada min)" = $menoresConteo
        "Tiempo en detenciones menores"          = Format-Duracion ([TimeSpan]::FromSeconds($menoresSegundos))
        "Distancia por odómetro (km)"            = if ($null -ne $kmOdometro) { [Math]::Round($kmOdometro, 2) } else { "no reportado" }
        "Odómetro al inicio (km)"                = if ($null -ne $odoIni) { [Math]::Round($odoIni, 2) } else { "no reportado" }
        "Odómetro al final (km)"                 = if ($null -ne $odoFin) { [Math]::Round($odoFin, 2) } else { "no reportado" }
        "Distancia sumando viajes del API (km)"  = [Math]::Round($kmViajes, 2)
        "Distancia calculada sobre la traza GPS (km)" = [Math]::Round($kmHaversine, 2)
        "Diferencia odómetro vs. viajes (%)"     = [Math]::Round($desvio, 1)
        "Velocidad promedio del tramo (km/h)"    = [Math]::Round($kmReferencia / $hTotal, 1)
        "Velocidad promedio en movimiento (km/h)" = if ($hMovimiento -gt 0) { [Math]::Round($kmReferencia / $hMovimiento, 1) } else { 0 }
        "Velocidad máxima registrada (km/h)"     = [Math]::Round($velMaxima, 1)
        "Viajes (encendidos) del API"            = $filasViajes.Count
        "Tiempo con motor en marcha según viajes" = Format-Duracion ([TimeSpan]::FromMinutes($minMotor))
        "Tiempo con motor en marcha (horas)"     = [Math]::Round($minMotor / 60, 2)
        "Puntos GPS analizados"                  = $pts.Count
        "Frecuencia media de reporte"            = ("1 punto cada {0:N0} s" -f (($HastaUtc - $DesdeUtc).TotalSeconds / [Math]::Max($pts.Count - 1, 1)))
        "Huecos de señal (> $MinutosHueco min)"  = $huecos.Count
    }

    # Indicadores de operación del API (referencia; los entrega redondeados a horas enteras)
    $ind = @($Indicadores) | Where-Object { $_ -and $null -ne $_.distanceTraveled } | Select-Object -First 1
    if ($ind) {
        $resumen["Horas de operación (API)"]           = [double]$ind.operationHours
        $resumen["Horas en movimiento (API)"]          = [double]$ind.operationHoursMovement
        $resumen["Horas en ralentí (API)"]             = [double]$ind.operationHoursRalenti
        $resumen["Combustible consumido (API)"]        = if ([double]$ind.fuelConsumptionQty -gt 0) { [double]$ind.fuelConsumptionQty } else { "el equipo no reporta combustible" }
    } else {
        $resumen["Horas de operación (API)"]    = "no disponible"
        $resumen["Horas en movimiento (API)"]   = "no disponible"
        $resumen["Horas en ralentí (API)"]      = "no disponible"
        $resumen["Combustible consumido (API)"] = "el equipo no reporta combustible"
    }
    $resumen["Horómetro por punto"] = "no expuesto por el API (requiere CANBUS; esta unidad no lo reporta)"

    return @{
        Resumen = $resumen
        Paradas = $paradas.ToArray()
        Viajes  = $filasViajes
        Huecos  = $huecos.ToArray()
    }
}
