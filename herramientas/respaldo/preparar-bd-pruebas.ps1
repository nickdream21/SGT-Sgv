<#
.SYNOPSIS
    Crea, pone al día o rehace la COPIA LOCAL de pruebas sgvEnsayo_pruebas, contra la que corren por
    defecto las pruebas de integración ([FactBD]) en vez de sgvActualizada (U-098).

.DESCRIPTION
    - Si la copia no existe en el SQL Server local ("(localdb)\MSSQLLocalDB" o "."), la crea con
      ensayar-migracion.ps1 -NombreBd sgvEnsayo_pruebas -Conservar -QuitarLoginsImportados: importa el
      .bacpac más reciente de <Carpeta>\bd\ (o -Bacpac; -RespaldoNuevo exporta uno antes, solo lee la BD
      de pruebas), aplica migraciones, procedimientos y humo, y borra el login SQL que crea la
      importación (misma regla que el ensayo). Si la importación falla, borra la copia a medias.
    - Si ya existe, solo la pone al día sin reimportar: aplicar-migraciones.ps1 y
      aplicar-procedimientos.ps1 (con humo) con -Entorno ensayo -BaseDeDatos sgvEnsayo_pruebas.
      Hay que hacerlo después de agregar o aplicar una migración o un SP nuevos.
    - Con -Rehacer la borra primero y la vuelve a crear desde el .bacpac (datos más nuevos).

    La copia tiene datos reales (no se anonimiza: hay pruebas que dependen de ellos). Queda solo en el
    servidor local; nunca la copie a otro lado.

    Código de salida: 0 bien; 1 falló algo (el detalle sale en la consola y, al crear, en el log de
    <Carpeta>\ensayos\).

.PARAMETER Rehacer
    Borra la copia existente y la vuelve a crear desde el .bacpac.

.PARAMETER Bacpac
    Al crear o rehacer: archivo .bacpac (por defecto el más reciente de <Carpeta>\bd\).

.PARAMETER Carpeta
    Al crear o rehacer: carpeta de respaldos (por defecto $env:SGV_CARPETA_RESPALDO).

.PARAMETER RespaldoNuevo
    Al crear o rehacer: exporta antes un .bacpac nuevo de la BD de pruebas (respaldar-bd.ps1).

.PARAMETER Servidor
    SQL Server local donde buscar o crear la copia. Por defecto se busca en "(localdb)\MSSQLLocalDB" y
    "." (el mismo orden que usan las pruebas); al crear, ensayar-migracion.ps1 elige ("." y, si no
    responde, LocalDB).

.PARAMETER ConPruebas
    Al crear o rehacer: corre también las [FactBD] contra la copia (por defecto no: corra la suite).

.EXAMPLE
    .\herramientas\respaldo\preparar-bd-pruebas.ps1 -Carpeta C:\RespaldosSGV              # crear o poner al día
    .\herramientas\respaldo\preparar-bd-pruebas.ps1                                       # poner al día
    .\herramientas\respaldo\preparar-bd-pruebas.ps1 -Rehacer -RespaldoNuevo -Carpeta C:\RespaldosSGV
#>
param(
    [switch]$Rehacer,
    [string]$Bacpac,
    [string]$Carpeta,
    [switch]$RespaldoNuevo,
    [string]$Servidor,
    [switch]$ConPruebas
)

$ErrorActionPreference = 'Stop'
$NombreBd = 'sgvEnsayo_pruebas'   # BaseDePruebas.BdCopiaLocal (WebSGV.Tests/Integracion/BaseDePruebas.cs)
$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$dirDatabase = Join-Path $raizRepo 'WebSGV\Database'
. (Join-Path $dirDatabase 'conexion-bd.ps1')

function Get-CadenaLocal([string]$Srv, [string]$Bd, [int]$Espera = 3) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $Srv
    $b['Initial Catalog'] = $Bd
    $b['Integrated Security'] = $true
    $b['TrustServerCertificate'] = $true
    $b['Connect Timeout'] = $Espera
    $b.ConnectionString
}

function Invoke-Escalar([string]$CadenaConexion, [string]$Sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($CadenaConexion)
    $conn.Open()
    try {
        $cmd = New-Object System.Data.SqlClient.SqlCommand($Sql, $conn)
        $cmd.CommandTimeout = 300
        return $cmd.ExecuteScalar()
    } finally { $conn.Close() }
}

# Otro script en su propio proceso (su "exit" no corta este). Devuelve el código de salida.
function Invoke-Paso([string]$Titulo, [string]$Script, [string[]]$Argumentos) {
    Write-Host ""
    Write-Host "== $Titulo" -ForegroundColor Cyan
    $preferencia = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $Script @Argumentos 2>&1 | ForEach-Object { "  $_" } | Out-Host
        return $LASTEXITCODE
    } finally { $ErrorActionPreference = $preferencia }
}

function Remove-Copia([string]$Srv) {
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    Invoke-Escalar (Get-CadenaLocal $Srv 'master' 10) "IF DB_ID(N'$NombreBd') IS NOT NULL BEGIN ALTER DATABASE [$NombreBd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$NombreBd]; END" | Out-Null
}

# --- Dónde está la copia ----------------------------------------------------------------------------
if ($Servidor) {
    if (-not (Test-ServidorLocal $Servidor)) { throw "El servidor '$Servidor' no es local. La copia de pruebas solo vive en esta máquina." }
    $candidatos = @($Servidor)
} else {
    $candidatos = @('(localdb)\MSSQLLocalDB', '.')
}
$conCopia = @()
foreach ($c in $candidatos) {
    try {
        $id = Invoke-Escalar (Get-CadenaLocal $c 'master') "SELECT DB_ID(N'$NombreBd')"
        if ($null -ne $id -and -not ($id -is [DBNull])) { $conCopia += $c }
    } catch { }
}
if ($conCopia.Count -gt 1) {
    throw "Hay una copia [$NombreBd] en '$($conCopia -join "' y '")'. Las pruebas usan la primera; borre la otra o indique -Servidor."
}

$inicio = Get-Date
if ($conCopia.Count -eq 1 -and -not $Rehacer) {
    # --- Poner al día sin reimportar ----------------------------------------------------------------
    $srv = $conCopia[0]
    if ($Bacpac -or $RespaldoNuevo) { Write-Host "La copia ya existe: -Bacpac y -RespaldoNuevo solo se usan con -Rehacer." -ForegroundColor Yellow }
    $creada = Invoke-Escalar (Get-CadenaLocal $srv 'master') "SELECT create_date FROM sys.databases WHERE name = N'$NombreBd'"
    Write-Host "Copia    : [$NombreBd] en '$srv', creada el $('{0:yyyy-MM-dd HH:mm}' -f $creada) (sus datos son los del .bacpac de ese día; -Rehacer para traer datos nuevos)."
    $argsCopia = @('-Entorno', 'ensayo', '-BaseDeDatos', $NombreBd, '-Servidor', $srv)
    $codigo = Invoke-Paso 'Migraciones (aplicar-migraciones.ps1 -Entorno ensayo)' (Join-Path $dirDatabase 'aplicar-migraciones.ps1') $argsCopia
    if ($codigo -eq 0) {
        $codigo = Invoke-Paso 'Procedimientos y humo (aplicar-procedimientos.ps1 -Entorno ensayo)' (Join-Path $dirDatabase 'aplicar-procedimientos.ps1') $argsCopia
    }
    Write-Host ""
    if ($codigo -ne 0) {
        Write-Host "RESULTADO: FALLÓ la puesta al día de [$NombreBd] (código $codigo). Revise la salida; si la copia quedó mal, use -Rehacer." -ForegroundColor Red
        exit 1
    }
    Write-Host ("RESULTADO: OK. [{0}] en '{1}' al día ({2:mm\:ss})." -f $NombreBd, $srv, ((Get-Date) - $inicio)) -ForegroundColor Green
    exit 0
}

# --- Crear (o rehacer) desde el .bacpac -------------------------------------------------------------
if ($conCopia.Count -eq 1) {
    $srv = $conCopia[0]
    # Un login SQL con el SID de un usuario de la copia no se toca: la copia de este script ya no tiene
    # ninguno (los borra al crearla); si aparece, lo creó otra herramienta y se informa.
    try {
        $huerfanos = Invoke-Escalar (Get-CadenaLocal $srv $NombreBd) "SELECT STRING_AGG(sp.name, ', ') FROM sys.database_principals dp JOIN sys.server_principals sp ON sp.sid = dp.sid WHERE sp.type = 'S'"
        if ($huerfanos -and -not ($huerfanos -is [DBNull])) { Write-Host "Aviso: los logins SQL [$huerfanos] tienen usuario en la copia y no los creó este script; no se borran (revíselos)." -ForegroundColor Yellow }
    } catch { }
    Write-Host "Borrando la copia [$NombreBd] de '$srv' (-Rehacer)..." -ForegroundColor Cyan
    Remove-Copia $srv
    if (-not $Servidor) { $Servidor = $srv }   # se rehace en el mismo servidor
}

$argsEnsayo = @{ NombreBd = $NombreBd; Conservar = $true; QuitarLoginsImportados = $true }
if (-not $ConPruebas) { $argsEnsayo.SinPruebas = $true }
if ($Bacpac) { $argsEnsayo.Bacpac = $Bacpac }
if ($Carpeta) { $argsEnsayo.Carpeta = $Carpeta }
if ($RespaldoNuevo) { $argsEnsayo.RespaldoNuevo = $true }
if ($Servidor) { $argsEnsayo.Servidor = $Servidor }

# En este mismo proceso: su "exit" solo termina ese script y deja $LASTEXITCODE.
& (Join-Path $PSScriptRoot 'ensayar-migracion.ps1') @argsEnsayo
$codigo = $LASTEXITCODE

$srvCopia = if ($Servidor) { @($Servidor) } else { @('.', '(localdb)\MSSQLLocalDB') }
Write-Host ""
if ($codigo -ne 0) {
    # Si la importación no terminó (no hay dbo.SchemaVersion), la copia a medias se borra: las pruebas
    # no deben correr contra ella. Si terminó y falló otro paso, se conserva para ponerla al día luego.
    foreach ($c in $srvCopia) {
        try {
            $id = Invoke-Escalar (Get-CadenaLocal $c 'master') "SELECT DB_ID(N'$NombreBd')"
            if ($null -eq $id -or $id -is [DBNull]) { continue }
            $sv = Invoke-Escalar (Get-CadenaLocal $c 'master') "SELECT CASE WHEN OBJECT_ID(N'[$NombreBd].dbo.SchemaVersion') IS NULL THEN 0 ELSE 1 END"
            if ([int]$sv -eq 0) {
                Remove-Copia $c
                Write-Host "La importación no terminó: copia a medias [$NombreBd] borrada de '$c'." -ForegroundColor Yellow
            }
        } catch { }
    }
    Write-Host "RESULTADO: FALLÓ la preparación de [$NombreBd] (código $codigo). Revise la salida de arriba." -ForegroundColor Red
    exit 1
}
Write-Host ("RESULTADO: OK. Copia de pruebas [{0}] lista ({1:hh\:mm\:ss}). Las [FactBD] corren contra ella; SGV_TEST_BD=actualizada para usar sgvActualizada." -f $NombreBd, ((Get-Date) - $inicio)) -ForegroundColor Green
exit 0
