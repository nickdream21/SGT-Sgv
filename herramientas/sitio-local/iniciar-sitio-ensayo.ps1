<#
.SYNOPSIS
    Levanta una copia del sitio contra una BD de ENSAYO local (sgvEnsayo_*), para sacar capturas de la
    ayuda o grabar videos sin tocar la BD de pruebas ni el sitio de 44390.

.DESCRIPTION
    1. Comprueba que la BD exista en el SQL Server local y se llame sgvEnsayo_* (créela con
       herramientas\respaldo\ensayar-migracion.ps1 -Conservar -SinPruebas y anonimícela con
       herramientas\respaldo\anonimizar-ensayo.sql: tiene datos reales hasta entonces).
    2. Copia la salida compilada de WebSGV (compile antes) a %LOCALAPPDATA%\SGV-sitio-ensayo\, sin
       Uploads, App_Data, obj, Database ni los archivos de secretos.
    3. Escribe allí un connectionStrings.config a esa BD con autenticación de Windows y un
       appSettings.Secrets.config SIN correo (SMTP), ApisPeru ni GPS: el ensayo no manda correos ni
       consulta servicios externos. No toca WebSGV\connectionStrings.config.
    4. Deja ENSAYO.txt con el nombre de la BD (herramientas\manual\capturar.mjs rehúsa un sitio sin él).
    5. Levanta IIS Express en http://localhost:50391 (otro puerto que el sitio de pruebas).

    Para dejarlo corriendo desde Claude Code: ejecútelo con run_in_background y espere
    "IIS Express is running.". Al terminar, cierre IIS Express y borre la BD de ensayo.

.PARAMETER BaseDeDatos
    BD de ensayo (sgvEnsayo_*), obligatoria.

.PARAMETER Servidor
    SQL Server local. Por defecto "(localdb)\MSSQLLocalDB".

.PARAMETER Puerto
    Puerto http. Por defecto 50391.

.EXAMPLE
    .\herramientas\sitio-local\iniciar-sitio-ensayo.ps1 -BaseDeDatos sgvEnsayo_20261124_2003
#>
param(
    [Parameter(Mandatory = $true)][string]$BaseDeDatos,
    [string]$Servidor = '(localdb)\MSSQLLocalDB',
    [int]$Puerto = 50391
)

$ErrorActionPreference = 'Stop'
if ($BaseDeDatos -notmatch '^sgvEnsayo_[A-Za-z0-9_]+$') { throw "La BD debe llamarse sgvEnsayo_* (se pidió '$BaseDeDatos'). Nunca sgvActualizada ni sgvTransporte." }
if ($Servidor -notmatch '^(\.|localhost|\(localdb\)\\[A-Za-z0-9_]+|\.\\[A-Za-z0-9_]+)$') { throw "Solo un SQL Server local ('.', localhost o (localdb)\...); se pidió '$Servidor'." }

$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$web = Join-Path $repo 'WebSGV'
if (-not (Test-Path (Join-Path $web 'bin\WebSGV.dll'))) { throw "No existe WebSGV\bin\WebSGV.dll: compile antes (msbuild WebSGV.sln /p:Configuration=Debug)." }
$iis = Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
if (-not (Test-Path $iis)) { throw "No está IIS Express ($iis)." }

# La BD existe y es la pedida.
$cadena = "Data Source=$Servidor;Initial Catalog=$BaseDeDatos;Integrated Security=True;Connect Timeout=30"
$conn = New-Object System.Data.SqlClient.SqlConnection($cadena)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand(); $cmd.CommandText = 'SELECT DB_NAME()'
    $nombre = [string]$cmd.ExecuteScalar()
}
finally { $conn.Dispose() }
if ($nombre -ne $BaseDeDatos) { throw "La conexión abrió '$nombre' y no '$BaseDeDatos'." }

$codigo = & curl.exe -s -o NUL -w '%{http_code}' --max-time 3 "http://localhost:$Puerto/Views/Login.aspx"
if ($codigo -eq '200') { throw "Ya hay un sitio en http://localhost:$Puerto. Ciérrelo antes (la copia se reescribe)." }

$destino = Join-Path $env:LOCALAPPDATA 'SGV-sitio-ensayo'
if (Test-Path $destino) { Remove-Item -Recurse -Force $destino }
New-Item -ItemType Directory -Force $destino | Out-Null

& robocopy.exe $web $destino /E /NFL /NDL /NJH /NJS /NP /XD 'Uploads' 'App_Data' 'obj' 'Database' '.vs' `
    /XF 'connectionStrings.config' 'connectionStrings.Production.config' 'appSettings.Secrets.config' '*.user' | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy falló (código $LASTEXITCODE)." }
New-Item -ItemType Directory -Force (Join-Path $destino 'App_Data'), (Join-Path $destino 'Uploads') | Out-Null

$utf8 = New-Object System.Text.UTF8Encoding($false)
$xmlCadena = [System.Security.SecurityElement]::Escape($cadena)
[IO.File]::WriteAllText((Join-Path $destino 'connectionStrings.config'), @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Copia de ensayo (iniciar-sitio-ensayo.ps1): BD local $BaseDeDatos, autenticación de Windows. -->
<connectionStrings>
  <add name="ConexionSGV" connectionString="$xmlCadena" providerName="System.Data.SqlClient" />
</connectionStrings>
"@, $utf8)
[IO.File]::WriteAllText((Join-Path $destino 'appSettings.Secrets.config'), @"
<?xml version="1.0" encoding="utf-8"?>
<!-- Copia de ensayo: sin SMTP, ApisPeru ni GPS (no se manda correo ni se consultan servicios externos). -->
<appSettings>
</appSettings>
"@, $utf8)
[IO.File]::WriteAllText((Join-Path $destino 'ENSAYO.txt'), $BaseDeDatos, $utf8)

"Copia: $destino"
"BD de ensayo: $BaseDeDatos en $Servidor"
"Sitio: http://localhost:$Puerto/Views/Login.aspx"
& $iis /path:$destino /port:$Puerto
