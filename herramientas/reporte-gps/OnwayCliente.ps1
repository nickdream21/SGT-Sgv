<#
    OnwayCliente.ps1 — acceso al Customer API de Location World / Entel Onway.

    Port en PowerShell de WebSGV/Services/GpsIntegracion/OnwayApiClient.cs, para poder
    generar reportes puntuales sin recompilar ni desplegar la web.

    Reglas heredadas del cliente C# (ver los comentarios de OnwayApiClient.cs):
      - Location World permite UN solo token Auth0 activo por client_id. Por eso aquí
        SIEMPRE se reusa el token cacheado en la tabla OnwayAuthCache mientras siga
        vigente, y nunca se fuerza uno nuevo dentro de un reintento.
      - La sesión Onway (userId) sí se puede renovar libremente.
      - Rate limit del API: 2 requests/segundo.
      - Los endpoints con rango de fechas aceptan como máximo 1 día por llamada.

    Las credenciales se leen de WebSGV/appSettings.Secrets.config (gitignored) y nunca
    se imprimen en consola.
#>

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$script:RaizRepo   = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:CarpetaWeb = Join-Path $script:RaizRepo "WebSGV"
$script:ApiBase    = "https://customer-api.location-world.com"
$script:AuthUrl    = "https://location-world.auth0.com/oauth/token"
$script:Domain     = "fleet"
$script:Subdomain  = "fleetpe"
$script:UA         = "SGV-WebSGV/1.0 (+https://sgv.serviciosgeneralesviviana.com)"
$script:PageSize   = 50
$script:UltimaLlamada = [DateTime]::MinValue

# Perú (y Ecuador continental) están en UTC-5 todo el año.
$script:OffsetHorasLocal = 5

function ConvertTo-Utc([DateTime]$local)  { return $local.AddHours($script:OffsetHorasLocal) }
function ConvertFrom-Utc([DateTime]$utc)  { return $utc.AddHours(-$script:OffsetHorasLocal) }

# ---------------------------------------------------------------------------
# Configuración y BD
# ---------------------------------------------------------------------------

function Get-Secretos {
    $xml = [xml](Get-Content (Join-Path $script:CarpetaWeb "appSettings.Secrets.config") -Raw)
    $h = @{}
    foreach ($n in $xml.appSettings.add) { $h[$n.key] = $n.value }
    return $h
}

function Get-ConnString([string]$archivo = "connectionStrings.config") {
    $xml = [xml](Get-Content (Join-Path $script:CarpetaWeb $archivo) -Raw)
    return $xml.connectionStrings.add.connectionString
}

function Invoke-Sql([string]$query, [hashtable]$parametros = @{}, [string]$archivoConn = "connectionStrings.config") {
    $cn = New-Object System.Data.SqlClient.SqlConnection (Get-ConnString $archivoConn)
    try {
        $cn.Open()
        $cmd = $cn.CreateCommand()
        $cmd.CommandText = $query
        $cmd.CommandTimeout = 60
        foreach ($k in $parametros.Keys) { [void]$cmd.Parameters.AddWithValue($k, $parametros[$k]) }
        if ($query.TrimStart().ToUpper().StartsWith("SELECT")) {
            $da = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
            $dt = New-Object System.Data.DataTable
            [void]$da.Fill($dt)
            return ,$dt
        }
        return $cmd.ExecuteNonQuery()
    } finally { $cn.Close() }
}

# ---------------------------------------------------------------------------
# Autenticación
# ---------------------------------------------------------------------------

function Get-ExpiracionJwt([string]$token) {
    $carga = $token.Split(".")[1].Replace("-", "+").Replace("_", "/")
    while ($carga.Length % 4 -ne 0) { $carga += "=" }
    $obj = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($carga)) | ConvertFrom-Json
    return [DateTimeOffset]::FromUnixTimeSeconds($obj.exp).UtcDateTime
}

<#  Siembra en OnwayAuthCache un token generado a mano (Postman), sin pedirle uno nuevo
    a Auth0. Equivale al UPDATE de Database/Scripts/diagnostico_OnwayAuthCache_TokenActivoUnico.sql #>
function Set-TokenManual([string]$accessToken, [int]$expiresIn = 0) {
    $expira = if ($expiresIn -gt 0) { [DateTime]::UtcNow.AddSeconds($expiresIn) } else { Get-ExpiracionJwt $accessToken }
    # La expiración real la manda el JWT; si difiere de expires_in, gana la del JWT.
    $expiraJwt = Get-ExpiracionJwt $accessToken
    if ($expiraJwt -lt $expira) { $expira = $expiraJwt }

    [void](Invoke-Sql @"
MERGE OnwayAuthCache AS destino
USING (SELECT 1 AS idCache) AS origen ON destino.idCache = origen.idCache
WHEN MATCHED THEN UPDATE SET
    accessToken = @token, tokenExpiraEn = @expira,
    onwayClientId = NULL, onwayUserId = NULL, fechaActualizacion = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (idCache, accessToken, tokenExpiraEn, fechaActualizacion)
    VALUES (1, @token, @expira, SYSUTCDATETIME());
"@ @{ "@token" = $accessToken; "@expira" = $expira })

    Write-Host ("  Token sembrado en OnwayAuthCache, vigente hasta {0}Z ({1} hora Peru)" -f `
        $expira.ToString("yyyy-MM-dd HH:mm:ss"), (ConvertFrom-Utc $expira).ToString("dd/MM HH:mm"))
    return $expira
}

function Get-TokenCacheado {
    try {
        $dt = Invoke-Sql "SELECT accessToken, tokenExpiraEn, onwayUserId FROM OnwayAuthCache WHERE idCache = 1"
        if ($dt.Rows.Count -eq 0) { return $null }
        $r = $dt.Rows[0]
        $exp = [DateTime]::SpecifyKind([DateTime]$r.tokenExpiraEn, [DateTimeKind]::Utc)
        if ($exp -le [DateTime]::UtcNow.AddMinutes(10)) { return $null }
        return @{ Token = [string]$r.accessToken; UserId = [string]$r.onwayUserId; Expira = $exp }
    } catch {
        Write-Host ("  Aviso: no se pudo leer OnwayAuthCache ({0})" -f $_.Exception.Message)
        return $null
    }
}

function Wait-RateLimit {
    $espera = $script:UltimaLlamada.AddMilliseconds(600) - (Get-Date)
    if ($espera.TotalMilliseconds -gt 0) { Start-Sleep -Milliseconds ([int]$espera.TotalMilliseconds) }
    $script:UltimaLlamada = Get-Date
}

function Invoke-OnwayGet([string]$url, [string]$token) {
    Wait-RateLimit
    return Invoke-RestMethod -Method Get -Uri $url -UserAgent $script:UA -Headers @{ Authorization = "Bearer $token" }
}

function Invoke-OnwayPost([string]$url, $cuerpo, [string]$token) {
    Wait-RateLimit
    $json = $cuerpo | ConvertTo-Json -Depth 6 -Compress
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Method Post -Uri $url -Body $json -ContentType "application/json" -UserAgent $script:UA -Headers $headers
}

<#  Devuelve @{ Token; UserId }. Reusa el token cacheado; solo pide uno nuevo a Auth0 si
    no hay ninguno vigente (y eso puede fallar con 401 si otro entorno ya tiene uno). #>
function Connect-Onway {
    $sec = Get-Secretos
    $cache = Get-TokenCacheado
    if ($cache) {
        Write-Host ("  Reusando token cacheado (vence {0} hora Peru)" -f (ConvertFrom-Utc $cache.Expira).ToString("dd/MM HH:mm"))
        $token = $cache.Token
    } else {
        Write-Host "  Sin token vigente en cache -> solicitando uno nuevo a Auth0"
        $r = Invoke-OnwayPost $script:AuthUrl @{
            client_id     = $sec.OnwayAuth0ClientId
            client_secret = $sec.OnwayAuth0ClientSecret
            audience      = $script:ApiBase
            grant_type    = "client_credentials"
        } $null
        $token = $r.access_token
        [void](Set-TokenManual $token $r.expires_in)
    }

    $sesion = Invoke-OnwayPost "$script:ApiBase/v1/$script:Domain/$script:Subdomain/sessions" `
        @{ username = $sec.OnwayUsername; password = $sec.OnwayPassword } $token
    [void](Invoke-Sql "UPDATE OnwayAuthCache SET onwayClientId = @cid, onwayUserId = @uid, fechaActualizacion = SYSUTCDATETIME() WHERE idCache = 1" `
        @{ "@cid" = $sesion.clientId; "@uid" = $sesion.userId })

    Write-Host "  Sesion Onway creada"
    return @{ Token = $token; UserId = $sesion.userId }
}

# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------

function Get-Dispositivos([hashtable]$con) {
    $todos = New-Object System.Collections.ArrayList
    $page = 0
    while ($true) {
        $url = "$script:ApiBase/v1/$script:Domain/$script:Subdomain/users/$($con.UserId)/devices" +
               "?deviceExpandEnum=none&deviceFieldSortEnum=imei&directionSortEnum=asc&page=$page&pageSize=$script:PageSize"
        $r = Invoke-OnwayGet $url $con.Token
        if (-not $r.content -or @($r.content).Count -eq 0) { break }
        foreach ($d in $r.content) { [void]$todos.Add($d) }
        if ($todos.Count -ge $r.records) { break }
        $page++
    }
    return $todos.ToArray()
}

function Get-DispositivoPorPlaca([hashtable]$con, [string]$placa) {
    $objetivo = $placa.Trim().ToUpper()
    return (Get-Dispositivos $con) | Where-Object { $_.alias -and $_.alias.Trim().ToUpper() -eq $objetivo } | Select-Object -First 1
}

<#  Recorre un rango UTC troceándolo en ventanas de $horasVentana (el API limita a 1 día
    por llamada) y pagina cada ventana. $construirUrl recibe (desdeStr, hastaStr, page). #>
function Invoke-ConsultaPorVentanas {
    param(
        [hashtable]$con, [DateTime]$desdeUtc, [DateTime]$hastaUtc,
        [scriptblock]$construirUrl, [string]$etiqueta = "datos",
        [int]$horasVentana = 12, [scriptblock]$invocar = $null
    )
    $todos = New-Object System.Collections.ArrayList
    $ini = $desdeUtc
    while ($ini -lt $hastaUtc) {
        $fin = $ini.AddHours($horasVentana)
        if ($fin -gt $hastaUtc) { $fin = $hastaUtc }
        $desdeStr = $ini.ToString("yyyy-MM-ddTHH:mm:ss") + "Z"
        $hastaStr = $fin.ToString("yyyy-MM-ddTHH:mm:ss") + "Z"

        $page = 0; $enVentana = 0
        while ($true) {
            $url = & $construirUrl $desdeStr $hastaStr $page
            $r = if ($invocar) { & $invocar $url } else { Invoke-OnwayGet $url $con.Token }
            if (-not $r -or -not $r.content -or @($r.content).Count -eq 0) { break }
            foreach ($x in $r.content) { [void]$todos.Add($x) }
            $enVentana += @($r.content).Count
            if ($enVentana -ge $r.records) { break }
            $page++
        }
        Write-Host ("    {0}: {1} -> {2} = {3} registros" -f $etiqueta, $desdeStr, $hastaStr, $enVentana)
        $ini = $fin
    }
    return $todos.ToArray()
}

function Get-Historial([hashtable]$con, [string]$deviceId, [DateTime]$desdeUtc, [DateTime]$hastaUtc) {
    $base = "$script:ApiBase/v1/$script:Domain/$script:Subdomain/users/$($con.UserId)/devices/$deviceId/history"
    return Invoke-ConsultaPorVentanas $con $desdeUtc $hastaUtc {
        param($d, $h, $p)
        "$base" + "?from=$([uri]::EscapeDataString($d))&to=$([uri]::EscapeDataString($h))&page=$p&pageSize=$script:PageSize"
    } "historial"
}

function Get-Viajes([hashtable]$con, [string]$deviceId, [DateTime]$desdeUtc, [DateTime]$hastaUtc) {
    $base = "$script:ApiBase/v1/$script:Domain/$script:Subdomain/users/$($con.UserId)/devices/$deviceId/trips"
    return Invoke-ConsultaPorVentanas $con $desdeUtc $hastaUtc {
        param($d, $h, $p)
        "$base" + "?from=$([uri]::EscapeDataString($d))&to=$([uri]::EscapeDataString($h))&page=$p&pageSize=$script:PageSize"
    } "viajes" 23
}

function Get-HistorialCanbus([hashtable]$con, [string]$deviceId, [DateTime]$desdeUtc, [DateTime]$hastaUtc) {
    $base = "$script:ApiBase/v1/$script:Domain/$script:Subdomain/users/$($con.UserId)/devices/$deviceId/history/canbus"
    try {
        return Invoke-ConsultaPorVentanas $con $desdeUtc $hastaUtc {
            param($d, $h, $p)
            "$base" + "?from=$([uri]::EscapeDataString($d))&to=$([uri]::EscapeDataString($h))&page=$p&pageSize=$script:PageSize"
        } "canbus"
    } catch {
        Write-Host ("    canbus no disponible para este equipo ({0})" -f $_.Exception.Message)
        return @()
    }
}

<#  POST .../devices/trips/operation-indicators — odómetro inicial/final, distancia,
    horas de operación / movimiento / ralentí y consumo de combustible. #>
function Get-IndicadoresOperacion([hashtable]$con, [string]$deviceId, [DateTime]$desdeUtc, [DateTime]$hastaUtc) {
    $base = "$script:ApiBase/v1/$script:Domain/$script:Subdomain/users/$($con.UserId)/devices/trips/operation-indicators"
    $cuerpo = @{ deviceIds = @($deviceId) }
    try {
        return Invoke-ConsultaPorVentanas $con $desdeUtc $hastaUtc {
            param($d, $h, $p)
            "$base" + "?from=$([uri]::EscapeDataString($d))&to=$([uri]::EscapeDataString($h))&page=$p&pageSize=$script:PageSize"
        } "indicadores" 23 { param($url) Invoke-OnwayPost $url $cuerpo $con.Token }
    } catch {
        Write-Host ("    indicadores de operacion no disponibles ({0})" -f $_.Exception.Message)
        return @()
    }
}
