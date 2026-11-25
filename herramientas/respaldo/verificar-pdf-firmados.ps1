<#
.SYNOPSIS
    Comprueba que los PDF firmados de una carpeta (copia de App_Data\OrdenesViaje) coinciden con los
    hashes SHA-256 registrados en la BD.

.DESCRIPTION
    Lee de la BD (solo SELECT) cada PDF registrado:
      - FirmaDigital.rutaPdfFirmado + hashDocumento (todas las firmas con PDF, vigentes o anuladas:
        un PDF anulado sigue siendo evidencia y también se conserva). Excepción (E6-14, M-101): la
        firma ANULADA de una orden RETIRADA (la orden ya no existe en OrdenViaje: la retiró el conductor
        o se retiraron datos de prueba) se verifica si su PDF está, pero si falta no cuenta como
        faltante: se informa aparte como RETIRADA SIN PDF.
      - OrdenViaje.rutaPdfFirmado + hashPdfFirmado (documento vigente de la orden; normalmente es el
        mismo archivo que la última firma y se cuenta una sola vez).
    Las rutas de la BD son virtuales (~/App_Data/OrdenesViaje/aaaa/mm/archivo.pdf); se buscan en
    -Carpeta como aaaa\mm\archivo.pdf.

    Informa:
      OK         el archivo existe y su SHA-256 coincide.
      FALTANTE   está en la BD pero no en la carpeta.
      RETIRADA SIN PDF  firma anulada de una orden retirada cuyo PDF no está (no cuenta como error).
      DISTINTO   existe, pero su SHA-256 no coincide (si dos firmas usaron la misma ruta, el archivo
                 solo puede coincidir con una; se indica con cuál).
      SOBRANTE   archivo de la carpeta sin registro en la BD (no cuenta como error).
    Las versiones anteriores que guarda respaldar-archivos.ps1 (*__anterior_aaaaMMdd_HHmmss.*) y los
    temporales *.descargando no se cuentan como sobrantes; si el archivo actual es DISTINTO y una
    versión anterior coincide, se indica.

    Código de salida: 0 si no hay faltantes ni distintos; 1 en otro caso.

.EXAMPLE
    .\herramientas\respaldo\verificar-pdf-firmados.ps1 -Carpeta WebSGV\App_Data\OrdenesViaje
    .\herramientas\respaldo\verificar-pdf-firmados.ps1 -Carpeta D:\RespaldosSGV     # usa D:\RespaldosSGV\archivos\OrdenesViaje
    .\herramientas\respaldo\verificar-pdf-firmados.ps1                               # usa $env:SGV_CARPETA_RESPALDO
#>
param(
    # Carpeta con la copia de OrdenesViaje (aaaa\mm\*.pdf). Si es la raíz de respaldos (contiene
    # archivos\OrdenesViaje), se usa esa subcarpeta. Por defecto: $env:SGV_CARPETA_RESPALDO.
    [string]$Carpeta,

    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno = 'pruebas',

    [switch]$ConfirmoProduccion,

    # Muestra también la lista de los OK.
    [switch]$Detalle
)

$ErrorActionPreference = 'Stop'

if ($Entorno -eq 'produccion' -and -not $ConfirmoProduccion) {
    throw "Para usar PRODUCCION agregue -ConfirmoProduccion."
}
if ([string]::IsNullOrWhiteSpace($Carpeta)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
if ([string]::IsNullOrWhiteSpace($Carpeta)) {
    throw "Indique -Carpeta <ruta a la copia de OrdenesViaje> o defina la variable de entorno SGV_CARPETA_RESPALDO."
}
$carpetaCompleta = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Carpeta)).TrimEnd('\')
$sub = Join-Path $carpetaCompleta 'archivos\OrdenesViaje'
if (Test-Path -LiteralPath $sub -PathType Container) { $carpetaCompleta = $sub }
if (-not (Test-Path -LiteralPath $carpetaCompleta -PathType Container)) {
    throw "No existe la carpeta '$carpetaCompleta'."
}

$raizRepo        = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
$dirWeb          = Join-Path $raizRepo 'WebSGV'
$archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
$bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }

[xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'." }
$b.ApplicationIntent = 'ReadOnly'
$b['Encrypt'] = $true   # somee siempre cifrado (E4-10); TrustServerCertificate como venga

# --- Registros de la BD (solo SELECT) --------------------------------------------------------------
$consulta = @"
SELECT 'FirmaDigital' AS origen, f.idFirma AS id, f.idDocumento, ISNULL(f.etapa, '') AS etapa,
       f.rutaPdfFirmado AS ruta, LOWER(LTRIM(RTRIM(f.hashDocumento))) AS hash, f.fechaHoraFirma AS fecha,
       CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.FirmaDigital a WHERE a.idFirmaAnulada = f.idFirma) THEN 1 ELSE 0 END AS bit) AS anulada,
       CAST(CASE WHEN f.tipoDocumento = 'ORDEN_VIAJE'
                  AND NOT EXISTS (SELECT 1 FROM dbo.OrdenViaje o WHERE CAST(o.idOrdenViaje AS varchar(50)) = f.idDocumento)
                 THEN 1 ELSE 0 END AS bit) AS ordenRetirada
FROM dbo.FirmaDigital f
WHERE f.rutaPdfFirmado IS NOT NULL AND f.hashDocumento IS NOT NULL
UNION ALL
SELECT 'OrdenViaje', o.idOrdenViaje, CAST(o.idOrdenViaje AS varchar(50)), '',
       o.rutaPdfFirmado, LOWER(LTRIM(RTRIM(o.hashPdfFirmado))), NULL, CAST(0 AS bit), CAST(0 AS bit)
FROM dbo.OrdenViaje o
WHERE o.rutaPdfFirmado IS NOT NULL AND o.hashPdfFirmado IS NOT NULL
"@

$tabla = New-Object System.Data.DataTable
$con = New-Object System.Data.SqlClient.SqlConnection($b.ConnectionString)
try {
    $con.Open()
    $cmd = $con.CreateCommand()
    $cmd.CommandText = $consulta
    $cmd.CommandTimeout = 120
    $lector = $cmd.ExecuteReader()
    $tabla.Load($lector)
    $lector.Close()
} finally {
    $con.Dispose()
}

$prefijo = '~/App_Data/OrdenesViaje/'
function ConvertTo-RutaRelativa([string]$ruta) {
    $r = $ruta.Trim()
    if ($r.StartsWith($prefijo, [StringComparison]::OrdinalIgnoreCase)) { $r = $r.Substring($prefijo.Length) }
    elseif ($r -match '(?i)OrdenesViaje[/\\](.+)$') { $r = $Matches[1] }
    return $r.Replace('/', '\').TrimStart('\')
}

# Esperados únicos por (ruta, hash): la fila de OrdenViaje que repite una firma no se cuenta dos veces.
$esperados = [ordered]@{}
foreach ($fila in ($tabla.Rows | Sort-Object { if ($_.origen -eq 'FirmaDigital') { 0 } else { 1 } }, id)) {
    $rel = ConvertTo-RutaRelativa ([string]$fila.ruta)
    $clave = $rel.ToLowerInvariant() + '|' + [string]$fila.hash
    if ($esperados.Contains($clave)) { continue }
    $desc = if ($fila.origen -eq 'FirmaDigital') {
        'firma {0} (orden {1}{2}{3}{4})' -f $fila.id, $fila.idDocumento,
            $(if ($fila.etapa) { ', ' + $fila.etapa } else { '' }),
            $(if ($fila.anulada) { ', anulada' } else { '' }),
            $(if ($fila.ordenRetirada) { ', orden retirada' } else { '' })
    } else { 'OrdenViaje {0}' -f $fila.id }
    $esperados[$clave] = New-Object PSObject -Property ([ordered]@{
        Relativa = $rel; Hash = [string]$fila.hash; Descripcion = $desc
        # Anulada y de una orden que ya no existe: si su PDF falta, no es un faltante (M-101).
        Retirada = ($fila.origen -eq 'FirmaDigital' -and [bool]$fila.anulada -and [bool]$fila.ordenRetirada) })
}

# --- Archivos de la carpeta -----------------------------------------------------------------------
$sha = [Security.Cryptography.SHA256]::Create()
$cacheHash = @{}
function Get-Sha256([string]$ruta) {
    if (-not $cacheHash.ContainsKey($ruta)) {
        $fs = [IO.File]::OpenRead($ruta)
        try { $cacheHash[$ruta] = ([BitConverter]::ToString($sha.ComputeHash($fs))).Replace('-', '').ToLowerInvariant() }
        finally { $fs.Dispose() }
    }
    return $cacheHash[$ruta]
}

$patronAnterior = '__anterior_\d{8}_\d{6}'
$archivos = @(Get-ChildItem -LiteralPath $carpetaCompleta -Recurse -File)
$referenciados = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

$ok = @(); $faltantes = @(); $distintos = @(); $retiradasSinPdf = @()
foreach ($e in $esperados.Values) {
    [void]$referenciados.Add($e.Relativa)
    $fisica = Join-Path $carpetaCompleta $e.Relativa
    if (-not (Test-Path -LiteralPath $fisica -PathType Leaf)) {
        if ($e.Retirada) { $retiradasSinPdf += $e } else { $faltantes += $e }
        continue
    }
    $h = Get-Sha256 $fisica
    if ($h -eq $e.Hash) { $ok += $e; continue }

    $nota = ''
    $mismaRuta = @($esperados.Values | Where-Object { $_ -ne $e -and $_.Relativa -eq $e.Relativa -and $_.Hash -eq $h })
    if ($mismaRuta.Count -gt 0) { $nota = "la ruta la usó también $($mismaRuta[0].Descripcion) y el archivo coincide con esa" }
    else {
        $base = [IO.Path]::GetFileNameWithoutExtension($fisica)
        $anteriores = @(Get-ChildItem -LiteralPath (Split-Path $fisica) -File |
            Where-Object { $_.Name -like "${base}__anterior_*" -and $_.Name -match $patronAnterior })
        $coincide = $anteriores | Where-Object { (Get-Sha256 $_.FullName) -eq $e.Hash } | Select-Object -First 1
        if ($coincide) { $nota = "la versión anterior guardada $($coincide.Name) sí coincide" }
    }
    $distintos += New-Object PSObject -Property ([ordered]@{
        Relativa = $e.Relativa; Hash = $e.Hash; Descripcion = $e.Descripcion; HashArchivo = $h; Nota = $nota })
}

$sobrantes = @(); $versiones = 0
foreach ($a in $archivos) {
    $rel = $a.FullName.Substring($carpetaCompleta.Length).TrimStart('\')
    if ($a.Name -match $patronAnterior -or $a.Name -like '*.descargando') { $versiones++; continue }
    if (-not $referenciados.Contains($rel)) { $sobrantes += $rel }
}

# --- Informe ----------------------------------------------------------------------------------------
"Carpeta : $carpetaCompleta"
"BD      : $($b.InitialCatalog) ($Entorno)"
"Registros en la BD con PDF: $($esperados.Count)   Archivos en la carpeta: $($archivos.Count)"
""
if ($Detalle -and $ok.Count) { "OK:"; $ok | ForEach-Object { "  {0}  {1}" -f $_.Relativa, $_.Descripcion }; "" }
if ($faltantes.Count) {
    Write-Host "FALTANTES (en la BD, no en la carpeta):" -ForegroundColor Red
    $faltantes | ForEach-Object { "  {0}  {1}" -f $_.Relativa, $_.Descripcion }
    ""
}
if ($distintos.Count) {
    Write-Host "DISTINTOS (el SHA-256 no coincide):" -ForegroundColor Red
    foreach ($d in $distintos) {
        "  {0}  {1}" -f $d.Relativa, $d.Descripcion
        "      BD {0}  archivo {1}" -f $d.Hash.Substring(0, 16), $d.HashArchivo.Substring(0, 16)
        if ($d.Nota) { "      ($($d.Nota))" }
    }
    ""
}
if ($retiradasSinPdf.Count) {
    "RETIRADAS SIN PDF (firmas anuladas de órdenes retiradas; no cuentan como error): $($retiradasSinPdf.Count)"
    if ($Detalle) { $retiradasSinPdf | ForEach-Object { "  {0}  {1}" -f $_.Relativa, $_.Descripcion } }
    ""
}
if ($sobrantes.Count) {
    "SOBRANTES (en la carpeta, sin registro en la BD):"
    $sobrantes | ForEach-Object { "  $_" }
    ""
}
"Resumen: verificados OK $($ok.Count)  faltantes $($faltantes.Count)  distintos $($distintos.Count)  sobrantes $($sobrantes.Count)  retiradas sin PDF $($retiradasSinPdf.Count)" +
    $(if ($versiones) { "  (versiones anteriores/temporales no contadas: $versiones)" } else { '' })

if ($faltantes.Count -gt 0 -or $distintos.Count -gt 0) { exit 1 }
exit 0
