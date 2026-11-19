<#
.SYNOPSIS
    Exporta la BD de SGV a un .bacpac en una carpeta propia (fuera de somee) y aplica la retención.

.DESCRIPTION
    El usuario de la app no tiene permiso BACKUP en somee, así que el respaldo es un .bacpac
    (esquema + datos) exportado con sqlpackage /Action:Export. Solo lee la BD.

    Lee la cadena ConexionSGV de WebSGV\connectionStrings.config (pruebas) o
    connectionStrings.Production.config (producción), ambos gitignored. La contraseña se pasa a
    sqlpackage en un archivo de respuesta temporal (@archivo) dentro de %TEMP% que se borra al
    terminar: no aparece en la línea de comandos ni en la salida (se enmascara por si acaso).

    Destino: <Carpeta>\bd\<nombreBD>_aaaaMMdd_HHmm.bacpac. Se escribe primero como
    *.parcial.bacpac y se renombra solo si sqlpackage termina bien; si falla, sale con código <> 0
    y no aplica la retención.

    Retención: conserva los 30 más recientes y, de los anteriores, el primero de cada mes de los
    últimos 12 meses; borra el resto. Cada ejecución añade una línea a <Carpeta>\bd\respaldos.log.

    Producción: solo con -ConfirmoProduccion.

.EXAMPLE
    .\herramientas\respaldo\respaldar-bd.ps1 -Carpeta D:\RespaldosSGV
    .\herramientas\respaldo\respaldar-bd.ps1 -SoloListar          # usa $env:SGV_CARPETA_RESPALDO
    .\herramientas\respaldo\respaldar-bd.ps1 -Carpeta D:\RespaldosSGV -Entorno produccion -ConfirmoProduccion
#>
param(
    [string]$Carpeta,

    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno = 'pruebas',

    [switch]$SoloListar,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

# Función pura de retención: recibe los nombres de los .bacpac y "hoy"; devuelve los nombres a
# borrar. Los nombres que no siguen el patrón <bd>_aaaaMMdd_HHmm.bacpac se ignoran (nunca se borran).
# probar-retencion.ps1 la extrae de este archivo para probarla.
function Get-RespaldosABorrar {
    param(
        [string[]]$Nombres,
        [datetime]$Hoy,
        [int]$Recientes = 30,
        [int]$Meses = 12
    )
    $cultura = [Globalization.CultureInfo]::InvariantCulture
    $items = @()
    foreach ($n in $Nombres) {
        if ($n -match '_(\d{8})_(\d{4})\.bacpac$') {
            $f = [datetime]::MinValue
            if ([datetime]::TryParseExact($Matches[1] + $Matches[2], 'yyyyMMddHHmm', $cultura,
                    [Globalization.DateTimeStyles]::None, [ref]$f)) {
                $items += New-Object PSObject -Property @{ Nombre = $n; Fecha = $f }
            }
        }
    }
    if ($items.Count -le $Recientes) { return @() }

    $ordenados = @($items | Sort-Object Fecha, Nombre -Descending)
    $anteriores = $ordenados[$Recientes..($ordenados.Count - 1)]

    # Primero de cada mes (sobre todos los respaldos, para que el elegido no cambie con el tiempo).
    $primeroDelMes = @{}
    foreach ($i in ($items | Sort-Object Fecha, Nombre)) {
        $clave = $i.Fecha.ToString('yyyy-MM')
        if (-not $primeroDelMes.ContainsKey($clave)) { $primeroDelMes[$clave] = $i.Nombre }
    }
    $limite = $Hoy.Date.AddMonths(-$Meses)

    $borrar = @()
    foreach ($i in $anteriores) {
        $esMensual = ($primeroDelMes[$i.Fecha.ToString('yyyy-MM')] -eq $i.Nombre) -and ($i.Fecha -ge $limite)
        if (-not $esMensual) { $borrar += $i.Nombre }
    }
    return $borrar
}

function Format-Tamano([long]$bytes) {
    if ($bytes -ge 1MB) { return ('{0:N1} MB' -f ($bytes / 1MB)) }
    return ('{0:N0} KB' -f ($bytes / 1KB))
}

# --- Parámetros y validaciones -------------------------------------------------------------------
if ($Entorno -eq 'produccion' -and -not $ConfirmoProduccion) {
    throw "Para usar PRODUCCION agregue -ConfirmoProduccion."
}
if ([string]::IsNullOrWhiteSpace($Carpeta)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
if ([string]::IsNullOrWhiteSpace($Carpeta)) {
    throw "Indique -Carpeta <ruta> o defina la variable de entorno SGV_CARPETA_RESPALDO."
}

$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
# Rutas relativas se resuelven contra la ubicación actual de PowerShell (no la del proceso).
$carpetaCompleta = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Carpeta)).TrimEnd('\') + '\'
if ($carpetaCompleta.StartsWith($raizRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta '$($carpetaCompleta.TrimEnd('\'))' está dentro del repositorio. Use una carpeta fuera del repo (el respaldo contiene datos reales)."
}
$carpetaBd = Join-Path $carpetaCompleta.TrimEnd('\') 'bd'

$dirWeb          = Join-Path $raizRepo 'WebSGV'
$archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
$bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }

[xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'." }

$sqlpackage = (Get-Command sqlpackage -ErrorAction SilentlyContinue).Source
if (-not $sqlpackage) { throw "No se encontró sqlpackage (instalar con: dotnet tool install -g microsoft.sqlpackage)." }

$inicio      = Get-Date
$nombreFinal = '{0}_{1}.bacpac' -f $b.InitialCatalog, $inicio.ToString('yyyyMMdd_HHmm')
$rutaFinal   = Join-Path $carpetaBd $nombreFinal
$rutaParcial = Join-Path $carpetaBd ($nombreFinal -replace '\.bacpac$', '.parcial.bacpac')
$rutaLog     = Join-Path $carpetaBd 'respaldos.log'

function Get-NombresExistentes {
    if (-not (Test-Path $carpetaBd)) { return @() }
    @(Get-ChildItem $carpetaBd -File -Filter "$($b.InitialCatalog)_*.bacpac" |
      Where-Object { $_.Name -notlike '*.parcial.bacpac' } | Select-Object -ExpandProperty Name)
}

"Entorno : $Entorno ($($b.InitialCatalog) en $($b.DataSource))"
"Destino : $rutaFinal"

if ($SoloListar) {
    $existentes = Get-NombresExistentes
    $aBorrar = @(Get-RespaldosABorrar -Nombres (@($existentes) + $nombreFinal) -Hoy $inicio)
    "Respaldos existentes: $($existentes.Count)"
    if ($aBorrar.Count -eq 0) { "La retención no borraría nada." }
    else { "La retención borraría $($aBorrar.Count):"; $aBorrar | ForEach-Object { "  $_" } }
    "(-SoloListar: no se exportó ni se borró nada)"
    exit 0
}

if (Test-Path $rutaFinal) { throw "Ya existe $rutaFinal (otro respaldo en el mismo minuto). Espere un minuto y repita." }
New-Item -ItemType Directory -Force $carpetaBd | Out-Null
if (Test-Path $rutaParcial) { Remove-Item $rutaParcial -Force }

# --- Exportación -------------------------------------------------------------------------------
# La contraseña va en un archivo de respuesta en %TEMP% (perfil del usuario), borrado en finally.
$rsp = Join-Path $env:TEMP ('sgv-respaldo-' + [guid]::NewGuid().ToString('N') + '.rsp')
$lineas = @(
    '/Action:Export'
    "/SourceServerName:`"$($b.DataSource)`""
    "/SourceDatabaseName:`"$($b.InitialCatalog)`""
    "/SourceUser:`"$($b.UserID)`""
    "/SourcePassword:`"$($b.Password)`""
    # Necesario: el certificado TLS de somee no coincide con el nombre del servidor (*.mssql.somee.com)
    # y la validación falla ("El nombre de entidad de seguridad de destino es incorrecto", probado el
    # 2026-11-19). La propia cadena de la app usa TrustServerCertificate=True. Quitarlo si somee lo corrige.
    '/SourceTrustServerCertificate:True'
    "/TargetFile:`"$rutaParcial`""
    '/p:CommandTimeout=600'
)
[IO.File]::WriteAllLines($rsp, $lineas, (New-Object Text.UTF8Encoding($false)))

$clave = $b.Password
$codigo = 1
$preferencia = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    & $sqlpackage "@$rsp" 2>&1 | ForEach-Object {
        $t = "$_"
        if ($clave) { $t = $t.Replace($clave, '********') }
        $t
    }
    $codigo = $LASTEXITCODE
} finally {
    $ErrorActionPreference = $preferencia
    Remove-Item $rsp -Force -ErrorAction SilentlyContinue
}
$duracion = (Get-Date) - $inicio
$textoDuracion = '{0:hh\:mm\:ss}' -f $duracion

if ($codigo -ne 0 -or -not (Test-Path $rutaParcial) -or (Get-Item $rutaParcial).Length -eq 0) {
    if (Test-Path $rutaParcial) { Remove-Item $rutaParcial -Force -ErrorAction SilentlyContinue }
    Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  ERROR  {1}  sqlpackage código {2}  duración {3}' -f $inicio, $Entorno, $codigo, $textoDuracion)
    Write-Host "ERROR: sqlpackage terminó con código $codigo. No se aplicó la retención." -ForegroundColor Red
    if ($codigo -eq 0) { $codigo = 1 }
    exit $codigo
}

Move-Item $rutaParcial $rutaFinal
$tamano = (Get-Item $rutaFinal).Length

# --- Retención ---------------------------------------------------------------------------------
$aBorrar = @(Get-RespaldosABorrar -Nombres (Get-NombresExistentes) -Hoy $inicio)
foreach ($n in $aBorrar) {
    Remove-Item (Join-Path $carpetaBd $n) -Force
    "Retención: borrado $n"
}

Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  OK  {1}  {2}  {3}  duración {4}  borrados {5}' -f $inicio, $Entorno, $nombreFinal, (Format-Tamano $tamano), $textoDuracion, $aBorrar.Count)

""
"Respaldo listo"
"  Archivo : $rutaFinal"
"  Tamaño  : $(Format-Tamano $tamano)"
"  Duración: $textoDuracion"
"  Retención: $($aBorrar.Count) borrado(s)"
exit 0
