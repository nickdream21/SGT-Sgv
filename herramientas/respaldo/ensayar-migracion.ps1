<#
.SYNOPSIS
    Ensaya las migraciones y los procedimientos pendientes sobre una COPIA LOCAL de la BD (desde un
    .bacpac) antes de aplicarlos a la BD viva.

.DESCRIPTION
    1. Toma el .bacpac más reciente de <Carpeta>\bd\ (o -Bacpac; con -RespaldoNuevo antes exporta uno
       nuevo con respaldar-bd.ps1, que solo lee la BD de pruebas) e informa su fecha.
    2. Lo importa con sqlpackage en una BD NUEVA sgvEnsayo_aaaaMMdd_HHmm de un SQL Server LOCAL
       (autenticación de Windows; nunca somee ni una BD que ya exista).
    3. Corre WebSGV\Database\aplicar-migraciones.ps1 y aplicar-procedimientos.ps1 con -Entorno ensayo
       (este último incluye la ejecución de humo de M-052).
    4. Corre las pruebas de integración ([FactBD], WebSGV.Tests.Integracion) contra el ensayo con
       SGV_TEST_CONNSTR (BaseDePruebas acepta sgvEnsayo_* solo en un servidor local). Compile antes.
    5. Si se indica -ArchivoControl, lo ejecuta con sql.ps1 -Entorno ensayo (consultas SELECT de
       control: se muestran en la salida).
    6. Borra la BD de ensayo (salvo -Conservar): tiene datos reales. Borra también el login SQL que
       crea la importación: solo los SqlLogin del model.xml del .bacpac, de tipo 'S', que no existían
       antes del ensayo y cuyo SID es el de un usuario de la BD de ensayo.

    La salida completa queda en <Carpeta>\ensayos\ensayo-migracion_aaaaMMdd_HHmm.log (fuera del repo,
    junto a los respaldos; no contiene claves) y una línea de resumen en <Carpeta>\ensayos\ensayos.log.

    Código de salida: 0 todo bien; 1 falló la importación, una migración, un procedimiento, el humo,
    una prueba o el archivo de control.

.PARAMETER Bacpac
    Archivo .bacpac. Si falta, el más reciente de <Carpeta>\bd\.

.PARAMETER Carpeta
    Carpeta de respaldos (la de respaldar-bd.ps1). Por defecto $env:SGV_CARPETA_RESPALDO. Si solo se
    da -Bacpac, se usa la carpeta padre de la del .bacpac.

.PARAMETER RespaldoNuevo
    Antes del ensayo exporta un .bacpac nuevo de la BD de pruebas con respaldar-bd.ps1 -Carpeta.

.PARAMETER Servidor
    SQL Server local. Por defecto "." y, si no responde, "(localdb)\MSSQLLocalDB".

.PARAMETER NombreBd
    Nombre de la BD de ensayo (sgvEnsayo_*). Por defecto sgvEnsayo_aaaaMMdd_HHmm.

.PARAMETER ArchivoControl
    Archivo .sql con consultas de control (SELECT) que se ejecuta al final contra el ensayo.

.PARAMETER SinPruebas
    No corre las pruebas de integración (solo importación, migraciones, procedimientos y humo).

.PARAMETER Conservar
    No borra la BD de ensayo al terminar (para revisarla a mano; bórrela después).

.PARAMETER QuitarLoginsImportados
    Con -Conservar: conserva la BD pero borra igual el login SQL que creó la importación (misma regla
    que sin -Conservar; el usuario de la BD queda huérfano, no hace falta: el acceso es con Windows).
    Lo usa preparar-bd-pruebas.ps1 para la copia persistente sgvEnsayo_pruebas.

.EXAMPLE
    .\herramientas\respaldo\ensayar-migracion.ps1 -Carpeta 'G:\Unidades compartidas\Respaldos_Sistemas_SGV'
    .\herramientas\respaldo\ensayar-migracion.ps1 -RespaldoNuevo -ArchivoControl .\control.sql
    .\herramientas\respaldo\ensayar-migracion.ps1 -Conservar -SinPruebas
#>
param(
    [string]$Bacpac,
    [string]$Carpeta,
    [switch]$RespaldoNuevo,
    [string]$Servidor,
    [string]$NombreBd,
    [string]$ArchivoControl,
    [switch]$SinPruebas,
    [switch]$Conservar,
    [switch]$QuitarLoginsImportados
)

$ErrorActionPreference = 'Stop'
$inicio = Get-Date
$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
$dirDatabase = Join-Path $raizRepo 'WebSGV\Database'
. (Join-Path $dirDatabase 'conexion-bd.ps1')

function Resolve-Ruta([string]$Ruta) {
    [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Ruta))
}

function Invoke-Consulta([string]$CadenaConexion, [string]$Sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($CadenaConexion)
    $conn.Open()
    try {
        $cmd = New-Object System.Data.SqlClient.SqlCommand($Sql, $conn)
        $cmd.CommandTimeout = 300
        $tabla = New-Object System.Data.DataTable
        (New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($tabla) | Out-Null
        return , $tabla
    } finally { $conn.Close() }
}

function Get-CadenaLocal([string]$Srv, [string]$Bd) {
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $Srv
    $b['Initial Catalog'] = $Bd
    $b['Integrated Security'] = $true
    $b['TrustServerCertificate'] = $true
    $b['Connect Timeout'] = 10
    $b.ConnectionString
}

# Ejecuta otro script en un proceso aparte (así su salida, también la de sqlcmd, queda en el log y su
# "exit" no corta este script). Devuelve el código de salida.
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

# --- Respaldo a usar -------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($Carpeta) -and [string]::IsNullOrWhiteSpace($Bacpac)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
if ($Carpeta) { $Carpeta = Resolve-Ruta $Carpeta }

if ($RespaldoNuevo) {
    if (-not $Carpeta) { throw "-RespaldoNuevo necesita -Carpeta (o SGV_CARPETA_RESPALDO)." }
    if ($Bacpac) { throw "Use -RespaldoNuevo o -Bacpac, no los dos." }
    Write-Host "Exportando un respaldo nuevo de la BD de pruebas (respaldar-bd.ps1, solo lectura)..." -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'respaldar-bd.ps1') -Carpeta $Carpeta
    if ($LASTEXITCODE -ne 0) { throw "respaldar-bd.ps1 falló (código $LASTEXITCODE): no se ensaya." }
}

if ([string]::IsNullOrWhiteSpace($Bacpac)) {
    if (-not $Carpeta) { throw "Indique -Bacpac <archivo>, -Carpeta <ruta> o defina SGV_CARPETA_RESPALDO." }
    $ultimo = Get-ChildItem (Join-Path $Carpeta 'bd') -File -Filter '*.bacpac' -ErrorAction SilentlyContinue |
              Where-Object { $_.Name -notlike '*.parcial.bacpac' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $ultimo) { throw "No hay archivos .bacpac en $(Join-Path $Carpeta 'bd')." }
    $Bacpac = $ultimo.FullName
}
$Bacpac = Resolve-Ruta $Bacpac
# Google Drive para escritorio (G:\) puede no mostrar un archivo recién renombrado durante unos
# segundos (pasa justo después de -RespaldoNuevo): se reintenta hasta ~30 s.
$itemBacpac = $null
for ($i = 0; $i -lt 30 -and -not $itemBacpac; $i++) {
    $itemBacpac = Get-Item -LiteralPath $Bacpac -ErrorAction SilentlyContinue
    if (-not $itemBacpac) { Start-Sleep -Seconds 1 }
}
if (-not $itemBacpac -or $itemBacpac.PSIsContainer) { throw "No existe el archivo $Bacpac." }
if ($Bacpac.StartsWith($raizRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "El .bacpac está dentro del repositorio. Los respaldos tienen datos reales: deben estar fuera del repo."
}
if (-not $Carpeta) { $Carpeta = Split-Path (Split-Path $Bacpac -Parent) -Parent }

# --- Destino ---------------------------------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($NombreBd)) { $NombreBd = 'sgvEnsayo_' + $inicio.ToString('yyyyMMdd_HHmm') }
if (-not (Test-NombreBdEnsayo $NombreBd)) { throw "Nombre de BD no válido: '$NombreBd' (debe ser sgvEnsayo_ + letras, números o _)." }

if ($Servidor) {
    if (-not (Test-ServidorLocal $Servidor)) { throw "El servidor '$Servidor' no es local. El ensayo solo usa un SQL Server de esta máquina." }
} else {
    foreach ($candidato in @('.', '(localdb)\MSSQLLocalDB')) {
        try {
            $t = Invoke-Consulta (Get-CadenaLocal $candidato 'master') "SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE') AS p"
            if ([int]$t.Rows[0].p -eq 1) { $Servidor = $candidato; break }
        } catch { }
    }
    if (-not $Servidor) { throw "No se encontró un SQL Server local que responda y permita crear bases. Use -Servidor." }
}
$cadenaMaster = Get-CadenaLocal $Servidor 'master'
$existe = Invoke-Consulta $cadenaMaster "SELECT COUNT(*) AS n FROM sys.databases WHERE name = N'$NombreBd'"
if ([int]$existe.Rows[0].n -gt 0) { throw "Ya existe la BD '$NombreBd' en '$Servidor'. El ensayo nunca importa sobre una BD existente." }

if ($ArchivoControl) {
    $ArchivoControl = Resolve-Ruta $ArchivoControl
    if (-not (Test-Path $ArchivoControl -PathType Leaf)) { throw "No existe el archivo de control $ArchivoControl." }
}

$sqlpackage = (Get-Command sqlpackage -ErrorAction SilentlyContinue).Source
if (-not $sqlpackage) {
    $enHerramientas = Join-Path $env:USERPROFILE '.dotnet\tools\sqlpackage.exe'
    if (Test-Path $enHerramientas) { $sqlpackage = $enHerramientas }
}
if (-not $sqlpackage) { throw "No se encontró sqlpackage (instalar con: dotnet tool install -g microsoft.sqlpackage)." }

$dllPruebas = Join-Path $raizRepo 'WebSGV.Tests\bin\Debug\net48\WebSGV.Tests.dll'
$vstest = $null
if (-not $SinPruebas) {
    if (-not (Test-Path $dllPruebas)) { throw "No existe $dllPruebas. Compile la solución (msbuild WebSGV.sln /p:Configuration=Debug) o use -SinPruebas." }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    # -all y no -latest: SSMS 22 también se registra en vswhere y no trae vstest.
    if (Test-Path $vswhere) { $vstest = & $vswhere -all -products * -find '**\vstest.console.exe' | Select-Object -First 1 }
    if (-not $vstest) { throw "No se encontró vstest.console.exe (Visual Studio o Build Tools con pruebas). Use -SinPruebas." }
}

# --- Log -------------------------------------------------------------------------------------------
$dirLog = Join-Path $Carpeta 'ensayos'
New-Item -ItemType Directory -Force $dirLog | Out-Null
$rutaLog = Join-Path $dirLog ("ensayo-migracion_{0}.log" -f $inicio.ToString('yyyyMMdd_HHmm'))
Start-Transcript -Path $rutaLog -Force | Out-Null

$fechaBacpac = $itemBacpac.LastWriteTime
Write-Host "Respaldo : $Bacpac ($('{0:N1} MB' -f ($itemBacpac.Length / 1MB)))"
Write-Host ("Fecha    : {0:yyyy-MM-dd HH:mm} (hace {1:N1} horas)" -f $fechaBacpac, ((Get-Date) - $fechaBacpac).TotalHours)
Write-Host "Destino  : [$NombreBd] en '$Servidor' (local, autenticación de Windows)"

$errores = New-Object System.Collections.Generic.List[string]
$creada = $false

# El .bacpac trae el login SQL de somee y sqlpackage lo crea en el servidor local (con una contraseña
# aleatoria). Como en ensayar-restauracion.ps1, solo se borran los SqlLogin que declara el model.xml
# del .bacpac y que no existían antes del ensayo; además deben ser de tipo 'S' (login SQL) y tener el
# SID de un usuario de la BD de ensayo (se consulta antes del DROP DATABASE).
Add-Type -AssemblyName System.IO.Compression.FileSystem
$loginsModelo = @()
$zip = [IO.Compression.ZipFile]::OpenRead($Bacpac)
try {
    $entrada = $zip.Entries | Where-Object { $_.FullName -eq 'model.xml' } | Select-Object -First 1
    if (-not $entrada) { throw "El .bacpac no contiene model.xml (¿archivo dañado?)." }
    $lector = New-Object IO.StreamReader($entrada.Open())
    try { [xml]$modelo = $lector.ReadToEnd() } finally { $lector.Close() }
} finally { $zip.Dispose() }
foreach ($el in $modelo.DataSchemaModel.Model.Element) {
    if ($el.Type -eq 'SqlLogin') { $loginsModelo += ($el.Name -replace '[\[\]]', '') }
}
function Get-LoginsSqlServidor {
    @((Invoke-Consulta $cadenaMaster "SELECT name FROM sys.server_principals WHERE type = 'S'").Rows | ForEach-Object { [string]$_.name })
}
$loginsAntes = Get-LoginsSqlServidor
# Logins creados por esta importación (se llama con la BD de ensayo todavía viva).
function Get-LoginsCreados {
    $conSid = @((Invoke-Consulta (Get-CadenaLocal $Servidor $NombreBd) "SELECT sp.name FROM sys.database_principals dp JOIN sys.server_principals sp ON sp.sid = dp.sid WHERE sp.type = 'S'").Rows | ForEach-Object { [string]$_.name })
    $ahora = Get-LoginsSqlServidor
    @($loginsModelo | Where-Object { ($ahora -contains $_) -and -not ($loginsAntes -contains $_) -and ($conSid -contains $_) })
}
function Remove-LoginsCreados([string[]]$Logins) {
    try {
        foreach ($l in $Logins) {
            Invoke-Consulta $cadenaMaster ('DROP LOGIN [' + $l.Replace(']', ']]') + ']') | Out-Null
            Write-Host "Login [$l] creado por la importación: borrado del servidor local."
        }
    } catch { $errores.Add("No se pudo borrar el login creado por la importación: bórrelo a mano. $($_.Exception.Message)") }
}

try {
    # --- Importación -------------------------------------------------------------------------------
    Write-Host ""
    Write-Host "== Importación (sqlpackage)" -ForegroundColor Cyan
    $inicioImport = Get-Date
    $preferencia = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $sqlpackage '/Action:Import' "/SourceFile:$Bacpac" "/TargetServerName:$Servidor" "/TargetDatabaseName:$NombreBd" `
            '/TargetTrustServerCertificate:True' '/p:CommandTimeout=600' 2>&1 | ForEach-Object {
                $t = "$_"
                if ($t -notmatch "^\s*(Deshabilitando|Habilitando) (índice|indice) |^\s*(Disabling|Enabling) index |^\s*Procesando tabla |^\s*Processing Table ") { "  $t" }
            } | Out-Host
        $codigoImport = $LASTEXITCODE
    } finally { $ErrorActionPreference = $preferencia }
    $creada = $true   # aunque falle, puede quedar una BD a medias: se limpia al final
    if ($codigoImport -ne 0) { $errores.Add("La importación falló (sqlpackage código $codigoImport)."); throw 'PARAR' }
    Write-Host ("  Importación terminada en {0:hh\:mm\:ss}." -f ((Get-Date) - $inicioImport))

    $cadenaEnsayo = Get-CadenaLocal $Servidor $NombreBd
    $bdActual = (Invoke-Consulta $cadenaEnsayo 'SELECT DB_NAME() AS n').Rows[0].n
    if ($bdActual -ne $NombreBd) { $errores.Add("Conexión inesperada a '$bdActual'."); throw 'PARAR' }

    $argsEnsayo = @('-Entorno', 'ensayo', '-BaseDeDatos', $NombreBd, '-Servidor', $Servidor)

    # --- Migraciones y procedimientos ----------------------------------------------------------------
    $c = Invoke-Paso 'Migraciones (aplicar-migraciones.ps1 -Entorno ensayo)' (Join-Path $dirDatabase 'aplicar-migraciones.ps1') $argsEnsayo
    if ($c -ne 0) { $errores.Add("aplicar-migraciones.ps1 falló (código $c)."); throw 'PARAR' }

    $c = Invoke-Paso 'Procedimientos y humo (aplicar-procedimientos.ps1 -Entorno ensayo)' (Join-Path $dirDatabase 'aplicar-procedimientos.ps1') $argsEnsayo
    if ($c -ne 0) { $errores.Add("aplicar-procedimientos.ps1 falló (código $c): procedimiento que no se aplica o error de humo.") }

    # --- Pruebas de integración ---------------------------------------------------------------------
    if (-not $SinPruebas) {
        Write-Host ""
        Write-Host "== Pruebas de integración contra [$NombreBd] (SGV_TEST_CONNSTR)" -ForegroundColor Cyan
        $anterior = $env:SGV_TEST_CONNSTR
        $env:SGV_TEST_CONNSTR = $cadenaEnsayo
        $preferencia = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & $vstest $dllPruebas '/TestCaseFilter:FullyQualifiedName~WebSGV.Tests.Integracion' '/logger:console;verbosity=minimal' 2>&1 |
                ForEach-Object { "  $_" } | Out-Host
            $codigoPruebas = $LASTEXITCODE
        } finally {
            $ErrorActionPreference = $preferencia
            $env:SGV_TEST_CONNSTR = $anterior
        }
        if ($codigoPruebas -ne 0) { $errores.Add("Pruebas de integración con fallos (vstest código $codigoPruebas).") }
    }

    # --- Controles --------------------------------------------------------------------------------
    if ($ArchivoControl) {
        $c = Invoke-Paso "Controles ($([IO.Path]::GetFileName($ArchivoControl)))" (Join-Path $dirDatabase 'sql.ps1') ($argsEnsayo + @('-Archivo', $ArchivoControl))
        if ($c -ne 0) { $errores.Add("El archivo de control falló (código $c).") }
    }
} catch {
    if ("$_" -ne 'PARAR') { $errores.Add("Error durante el ensayo: $($_.Exception.Message)") }
} finally {
    Write-Host ""
    if ($creada) {
        if ($Conservar -and $QuitarLoginsImportados) {
            Write-Host "BD conservada: [$NombreBd] en '$Servidor' (tiene datos reales: solo en este servidor local)." -ForegroundColor Yellow
            $loginsABorrar = @()
            try { $loginsABorrar = Get-LoginsCreados }
            catch { $errores.Add("No se pudo comprobar qué login creó la importación (no se borra ninguno): $($_.Exception.Message)") }
            Remove-LoginsCreados $loginsABorrar
        } elseif ($Conservar) {
            Write-Host "BD conservada: [$NombreBd] en '$Servidor'. Bórrela al terminar (tiene datos reales):" -ForegroundColor Yellow
            Write-Host "  sqlcmd -S $Servidor -E -C -Q `"ALTER DATABASE [$NombreBd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$NombreBd]`""
            try {
                $nuevos = Get-LoginsCreados
                if ($nuevos.Count -gt 0) { Write-Host "  y el login creado por la importación: DROP LOGIN [$($nuevos -join '], [')]" }
            } catch { Write-Host "  (no se pudo ver si la importación creó un login: revise sys.server_principals)" }
        } else {
            $loginsABorrar = @()
            try { $loginsABorrar = Get-LoginsCreados }
            catch { $errores.Add("No se pudo comprobar qué login creó la importación (no se borra ninguno): $($_.Exception.Message)") }
            try {
                [System.Data.SqlClient.SqlConnection]::ClearAllPools()
                Invoke-Consulta $cadenaMaster "IF DB_ID(N'$NombreBd') IS NOT NULL BEGIN ALTER DATABASE [$NombreBd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$NombreBd]; END" | Out-Null
                Write-Host "BD de ensayo [$NombreBd] borrada."
            } catch { $errores.Add("No se pudo borrar la BD [$NombreBd]: bórrela a mano. $($_.Exception.Message)") }
            Remove-LoginsCreados $loginsABorrar
        }
    }
}

$duracion = (Get-Date) - $inicio
Write-Host ""
Write-Host ("===== Resumen del ensayo de migración ({0:hh\:mm\:ss}) =====" -f $duracion)
foreach ($m in $errores) { Write-Host "  ERROR  $m" -ForegroundColor Red }
if ($errores.Count -gt 0) {
    Write-Host "RESULTADO: FALLÓ. No aplique estas migraciones ni procedimientos a la BD viva." -ForegroundColor Red
} else {
    Write-Host "RESULTADO: OK. Migraciones, procedimientos, humo$(if (-not $SinPruebas) { ', pruebas' })$(if ($ArchivoControl) { ' y controles' }) en verde sobre el respaldo del $('{0:yyyy-MM-dd HH:mm}' -f $fechaBacpac)." -ForegroundColor Green
}
Write-Host "Log: $rutaLog"
Stop-Transcript | Out-Null

try {
    $estado = if ($errores.Count -gt 0) { 'ERROR' } else { 'OK' }
    Add-Content -Path (Join-Path $dirLog 'ensayos.log') -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  {1}  {2}  bd {3}  servidor {4}  duración {5:hh\:mm\:ss}  errores {6}{7}' -f $inicio, $estado, (Split-Path $Bacpac -Leaf), $NombreBd, $Servidor, $duracion, $errores.Count, $(if ($Conservar) { '  (conservada)' } else { '' }))
} catch { }
if ($errores.Count -gt 0) { exit 1 } else { exit 0 }
