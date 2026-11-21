<#
.SYNOPSIS
    Aplica en orden los scripts de Database/Schema que aún no estén registrados en
    dbo.SchemaVersion de la BD elegida, y los registra (archivo + hash SHA-256).

.DESCRIPTION
    - La conexión se lee de los archivos gitignored del proyecto:
        pruebas    -> WebSGV/connectionStrings.config            (sgvActualizada)
        produccion -> WebSGV/connectionStrings.Production.config (sgvTransporte)
      Con -Entorno ensayo usa una BD sgvEnsayo_* de un SQL Server LOCAL (autenticación de Windows,
      sin leer esos archivos): la crea herramientas/respaldo/ensayar-migracion.ps1 desde un .bacpac.
    - 00_ControlMigraciones.sql (crea SchemaVersion) se ejecuta siempre primero.
    - Los scripts son idempotentes (IF COL_LENGTH/OBJECT_ID ...), así que registrar una
      BD donde ya se aplicaron a mano es seguro: se re-ejecutan sin efecto y se anotan.
    - Si un script ya registrado cambió después (hash distinto) solo se avisa; no se
      vuelve a ejecutar. Para cambios de esquema, crear un script NUEVO con el siguiente número.
    - $HashesAceptados: registros antiguos con un hash que no es el del archivo, revisados a mano
      (el objeto de la BD es equivalente al archivo). Solo valen mientras el archivo no cambie.

.EXAMPLE
    .\aplicar-migraciones.ps1 -Entorno pruebas -SoloListar
    .\aplicar-migraciones.ps1 -Entorno pruebas
    .\aplicar-migraciones.ps1 -Entorno produccion -ConfirmoProduccion
    .\aplicar-migraciones.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_20261121_1000
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pruebas', 'produccion', 'ensayo')]
    [string]$Entorno,

    # Solo con -Entorno ensayo (BD sgvEnsayo_* en un SQL Server local).
    [string]$BaseDeDatos,
    [string]$Servidor,

    [switch]$SoloListar,

    # Obligatorio para escribir en producción (evita aplicar ahí por error).
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

$dirDatabase = $PSScriptRoot
$dirSchema   = Join-Path $dirDatabase 'Schema'
$dirWeb      = Split-Path $dirDatabase -Parent

if ($Entorno -eq 'produccion' -and -not $SoloListar -and -not $ConfirmoProduccion) {
    throw "Para aplicar en PRODUCCIÓN agregue -ConfirmoProduccion (o use -SoloListar para ver los pendientes)."
}

# ── Conexión ────────────────────────────────────────────────────────────────
. (Join-Path $dirDatabase 'conexion-bd.ps1')
$con = Get-ConexionSgv -Entorno $Entorno -BaseDeDatos $BaseDeDatos -Servidor $Servidor -DirWeb $dirWeb
$cs  = $con.Cadena
[void](Get-RutaSqlcmd)   # falla aquí, antes de leer nada, si no está sqlcmd

function Invoke-ScriptSql([string]$ruta) {
    # -C: confiar en el certificado (somee)  -I: QUOTED_IDENTIFIER ON (índices filtrados)
    # -f 65001: leer el archivo como UTF-8 (ñ, tildes)  -b: código de salida <> 0 ante error
    # La clave viaja en SQLCMDPASSWORD, no en la línea de comandos.
    Invoke-SqlcmdSgv $con @('-C', '-I', '-b', '-f', '65001', '-i', $ruta)
    if ($LASTEXITCODE -ne 0) { throw "Falló $([IO.Path]::GetFileName($ruta)) (sqlcmd exit $LASTEXITCODE)." }
}

# Registros con un hash que no es el del archivo, revisados y aceptados. Cada entrada exige el hash
# registrado Y el hash (LF) del archivo de hoy: si el archivo vuelve a cambiar, el aviso vuelve.
#   16_HoraPeru.sql: en pruebas se aplicó el 2026-10-07 a las 13:19 con una versión anterior al
#   commit e09f4bf (15:27 del mismo día). Revisado el 2026-11-21: dbo.fn_AhoraPeru de la BD es la del
#   archivo (salvo CREATE / CREATE OR ALTER) y no queda getdate() en módulos ni DEFAULT, que es lo que
#   hacen los pasos 2 y 3 (lo sostiene BaseDeDatosTests.NingunObjetoUsaLaHoraDelServidor).
# Clave "<entorno>|<archivo>": lo revisado en una BD no vale para otra (producción no se revisó). Un
# ensayo es la copia de un .bacpac, así que acepta la entrada de cualquier entorno si el hash
# registrado coincide (la copia trae el SchemaVersion de su origen).
$HashesAceptados = @{
    'pruebas|16_HoraPeru.sql' = @{
        Registrado = 'da4ce86308f3395298090b7ae9f627ee573e57e7144f6e53d801778d803fbbfb'
        Archivo    = 'beae758689b20f689bf6644e83468378a29adbb6dd6a5a135b48afc410e50ac5'
    }
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
function Test-HashAceptado([string]$ruta, [string]$registrado) {
    $archivo = [IO.Path]::GetFileName($ruta)
    $claves = if ($Entorno -eq 'ensayo') { @($HashesAceptados.Keys | Where-Object { $_ -like "*|$archivo" }) } else { @("$Entorno|$archivo") }
    foreach ($clave in $claves) {
        $a = $HashesAceptados[$clave]
        if (($null -ne $a) -and ($a.Registrado -eq $registrado) -and ($a.Archivo -eq (Get-HashArchivo $ruta))) { return $true }
    }
    return $false
}
function Test-HashCoincide([string]$ruta, [string]$registrado) {
    $lf = Get-TextoLf $ruta
    return $registrado -eq (Get-Sha256 ([Text.Encoding]::UTF8.GetBytes($lf))) -or
           $registrado -eq (Get-Sha256 ([Text.Encoding]::UTF8.GetBytes($lf.Replace("`n", "`r`n"))))
}

Write-Host "Entorno: $Entorno  ->  $($con.Servidor) / $($con.Bd)" -ForegroundColor Cyan

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
$pendientes = @(); $modificados = @(); $aceptados = @()
foreach ($s in $scripts) {
    if ($s.Name -like '00_*') { continue }
    if (-not $registrados.ContainsKey($s.Name)) { $pendientes += $s }
    elseif (Test-HashCoincide $s.FullName $registrados[$s.Name]) { }
    elseif (Test-HashAceptado $s.FullName $registrados[$s.Name]) { $aceptados += $s.Name }
    else { $modificados += $s.Name }
}

if (-not $existeTabla) { Write-Host "La tabla dbo.SchemaVersion no existe todavía (se creará)." -ForegroundColor Yellow }
Write-Host "Registrados: $($registrados.Count)   Pendientes: $($pendientes.Count)"
$pendientes | ForEach-Object { Write-Host "  pendiente  $($_.Name)" }
$aceptados | ForEach-Object { Write-Host "  $_ conserva un hash anterior ya revisado (ver `$HashesAceptados en este script)." -ForegroundColor DarkGray }
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
