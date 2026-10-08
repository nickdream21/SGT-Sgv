<#
.SYNOPSIS
    Aplica en orden los scripts de Database/Schema que aún no estén registrados en
    dbo.SchemaVersion de la BD elegida, y los registra (archivo + hash SHA-256).

.DESCRIPTION
    - La conexión se lee de los archivos gitignored del proyecto:
        pruebas    -> WebSGV/connectionStrings.config            (sgvActualizada)
        produccion -> WebSGV/connectionStrings.Production.config (sgvTransporte)
    - 00_ControlMigraciones.sql (crea SchemaVersion) se ejecuta siempre primero.
    - Los scripts son idempotentes (IF COL_LENGTH/OBJECT_ID ...), así que registrar una
      BD donde ya se aplicaron a mano es seguro: se re-ejecutan sin efecto y se anotan.
    - Si un script ya registrado cambió después (hash distinto) solo se avisa; no se
      vuelve a ejecutar. Para cambios de esquema, crear un script NUEVO con el siguiente número.

.EXAMPLE
    .\aplicar-migraciones.ps1 -Entorno pruebas -SoloListar
    .\aplicar-migraciones.ps1 -Entorno pruebas
    .\aplicar-migraciones.ps1 -Entorno produccion -ConfirmoProduccion
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno,

    [switch]$SoloListar,

    # Obligatorio para escribir en producción (evita aplicar ahí por error).
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

$dirDatabase = $PSScriptRoot
$dirSchema   = Join-Path $dirDatabase 'Schema'
$dirWeb      = Split-Path $dirDatabase -Parent

$archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
$bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }

if ($Entorno -eq 'produccion' -and -not $SoloListar -and -not $ConfirmoProduccion) {
    throw "Para aplicar en PRODUCCIÓN agregue -ConfirmoProduccion (o use -SoloListar para ver los pendientes)."
}

# ── Conexión ────────────────────────────────────────────────────────────────
$rutaConexion = Join-Path $dirWeb $archivoConexion
if (-not (Test-Path $rutaConexion)) { throw "No existe $rutaConexion" }
[xml]$xml = Get-Content $rutaConexion -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
if (-not $cs) { throw "No se encontró la cadena 'ConexionSGV' en $archivoConexion" }
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) {
    throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'. Revise la configuración."
}

# ── sqlcmd ─────────────────────────────────────────────────────────────────
$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) {
    $sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\SQLCMD.EXE' -ErrorAction SilentlyContinue |
              Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $sqlcmd) { throw "No se encontró sqlcmd. Instale las 'Microsoft Command Line Utilities for SQL Server'." }

function Invoke-ScriptSql([string]$ruta) {
    # -C: confiar en el certificado (somee)  -I: QUOTED_IDENTIFIER ON (índices filtrados)
    # -f 65001: leer el archivo como UTF-8 (ñ, tildes)  -b: código de salida <> 0 ante error
    & $sqlcmd -C -I -b -f 65001 -S $b.DataSource -d $b.InitialCatalog -U $b.UserID -P $b.Password -i $ruta
    if ($LASTEXITCODE -ne 0) { throw "Falló $([IO.Path]::GetFileName($ruta)) (sqlcmd exit $LASTEXITCODE)." }
}

# Git entrega los archivos con CRLF o LF según la máquina (core.autocrlf), así que el hash se
# calcula con saltos LF. Se aceptan también registros antiguos hechos con el archivo en CRLF.
function Get-Sha256([byte[]]$bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($sha.ComputeHash($bytes)) -replace '-', '').ToLowerInvariant() } finally { $sha.Dispose() }
}
# GetString conserva el BOM como U+FEFF, así que GetBytes lo vuelve a escribir igual.
function Get-TextoLf([string]$ruta) {
    [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($ruta)).Replace("`r`n", "`n")
}
function Get-HashArchivo([string]$ruta) {
    Get-Sha256 ([Text.Encoding]::UTF8.GetBytes((Get-TextoLf $ruta)))
}
function Test-HashCoincide([string]$ruta, [string]$registrado) {
    $lf = Get-TextoLf $ruta
    return $registrado -eq (Get-Sha256 ([Text.Encoding]::UTF8.GetBytes($lf))) -or
           $registrado -eq (Get-Sha256 ([Text.Encoding]::UTF8.GetBytes($lf.Replace("`n", "`r`n"))))
}

Write-Host "Entorno: $Entorno  ->  $($b.DataSource) / $($b.InitialCatalog)" -ForegroundColor Cyan

# ── Registro actual ─────────────────────────────────────────────────────────
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
try {
    $existeTabla = [bool]((New-Object System.Data.SqlClient.SqlCommand("SELECT OBJECT_ID('dbo.SchemaVersion','U')", $conn)).ExecuteScalar() -isnot [DBNull])
    $registrados = @{}
    if ($existeTabla) {
        $r = (New-Object System.Data.SqlClient.SqlCommand("SELECT archivo, hashSha256 FROM dbo.SchemaVersion", $conn)).ExecuteReader()
        while ($r.Read()) { $registrados[$r.GetString(0)] = $r.GetString(1).Trim() }
        $r.Close()
    }
} finally { $conn.Close() }

$scripts = Get-ChildItem $dirSchema -Filter '*.sql' | Where-Object { $_.Name -match '^\d+_' } | Sort-Object Name
$control = $scripts | Where-Object { $_.Name -like '00_*' }
$pendientes = @(); $modificados = @()
foreach ($s in $scripts) {
    if ($s.Name -like '00_*') { continue }
    if (-not $registrados.ContainsKey($s.Name)) { $pendientes += $s }
    elseif (-not (Test-HashCoincide $s.FullName $registrados[$s.Name])) { $modificados += $s.Name }
}

if (-not $existeTabla) { Write-Host "La tabla dbo.SchemaVersion no existe todavía (se creará)." -ForegroundColor Yellow }
Write-Host "Registrados: $($registrados.Count)   Pendientes: $($pendientes.Count)"
$pendientes | ForEach-Object { Write-Host "  pendiente  $($_.Name)" }
$modificados | ForEach-Object { Write-Host "  AVISO: $_ cambió después de aplicarse (no se re-ejecuta; cree un script nuevo)." -ForegroundColor Yellow }

if ($SoloListar) { return }

# ── Aplicar ────────────────────────────────────────────────────────────────
if ($control) { Invoke-ScriptSql $control.FullName }

foreach ($s in $pendientes) {
    Write-Host "Aplicando $($s.Name) ..." -ForegroundColor Cyan
    Invoke-ScriptSql $s.FullName

    $conn = New-Object System.Data.SqlClient.SqlConnection($cs)
    $conn.Open()
    try {
        $cmd = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO dbo.SchemaVersion (archivo, hashSha256) VALUES (@a, @h)", $conn)
        [void]$cmd.Parameters.AddWithValue('@a', $s.Name)
        [void]$cmd.Parameters.AddWithValue('@h', (Get-HashArchivo $s.FullName))
        [void]$cmd.ExecuteNonQuery()
    } finally { $conn.Close() }
    Write-Host "  OK y registrado." -ForegroundColor Green
}

Write-Host "Listo. Migraciones aplicadas en esta ejecución: $($pendientes.Count)" -ForegroundColor Green
