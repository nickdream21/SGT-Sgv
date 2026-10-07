<#
.SYNOPSIS
    Compara y aplica los procedimientos de Database/StoredProcedures contra una BD.

.DESCRIPTION
    El repo es la fuente de verdad de los procedimientos. Con -SoloListar compara cada
    procedimiento definido en los archivos .sql con el de la BD y muestra:
      FALTA      -> está en el repo pero no existe en la BD
      DISTINTO   -> existe pero su texto difiere del archivo
    Sin -SoloListar ejecuta los archivos que tengan algún procedimiento FALTA o DISTINTO
    (todos son re-ejecutables: CREATE OR ALTER o IF OBJECT_ID ... DROP).
    Con -Todos ejecuta todos los archivos.

    La conexión se lee igual que en aplicar-migraciones.ps1 (archivos gitignored).
    Aplicar ANTES las migraciones: varios procedimientos usan dbo.fn_AhoraPeru() (migración 16).

.EXAMPLE
    .\aplicar-procedimientos.ps1 -Entorno pruebas -SoloListar
    .\aplicar-procedimientos.ps1 -Entorno pruebas
    .\aplicar-procedimientos.ps1 -Entorno produccion -ConfirmoProduccion
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno,

    [switch]$SoloListar,
    [switch]$Todos,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

$dirDatabase = $PSScriptRoot
$dirSps      = Join-Path $dirDatabase 'StoredProcedures'
$dirWeb      = Split-Path $dirDatabase -Parent

$archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
$bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }

if ($Entorno -eq 'produccion' -and -not $SoloListar -and -not $ConfirmoProduccion) {
    throw "Para modificar PRODUCCIÓN agregue -ConfirmoProduccion (o use -SoloListar)."
}

[xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'." }

$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) {
    $sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\SQLCMD.EXE' -ErrorAction SilentlyContinue |
              Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $sqlcmd) { throw "No se encontró sqlcmd." }

# Compara ignorando espacios, mayúsculas y la forma de la cabecera: SQL Server guarda
# "CREATE PROCEDURE [dbo].[x]" aunque se haya ejecutado "CREATE OR ALTER PROCEDURE x".
function Normalizar([string]$texto) {
    $t = ($texto -replace '\s+', ' ').Trim().ToLowerInvariant()
    $t = [regex]::Replace($t, '\b(?:create(?: or alter)?|alter) proc(?:edure)? (?:\[?dbo\]?\.)?\[?([a-z0-9_]+)\]?', 'create procedure $1')
    # Los comentarios previos a la cabecera no cuentan (SQL Server los guarda solo si van en el mismo lote).
    $i = $t.IndexOf('create procedure ')
    if ($i -gt 0) { $t = $t.Substring($i) }
    return $t
}

Write-Host "Entorno: $Entorno  ->  $($b.DataSource) / $($b.InitialCatalog)" -ForegroundColor Cyan

# Definiciones actuales en la BD
$enBd = @{}
$conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
try {
    $r = (New-Object System.Data.SqlClient.SqlCommand("SELECT o.name, m.definition FROM sys.procedures o JOIN sys.sql_modules m ON m.object_id = o.object_id", $conn)).ExecuteReader()
    while ($r.Read()) { $enBd[$r.GetString(0).ToLowerInvariant()] = Normalizar $r.GetString(1) }
    $r.Close()
} finally { $conn.Close() }

# Procedimientos definidos en cada archivo (lotes separados por GO)
$aEjecutar = New-Object System.Collections.Generic.List[string]
$faltan = 0; $distintos = 0; $iguales = 0
foreach ($archivo in Get-ChildItem $dirSps -Filter '*.sql' | Sort-Object Name) {
    $texto = [IO.File]::ReadAllText($archivo.FullName)
    $lotes = [regex]::Split($texto, '(?im)^\s*GO\s*$')
    $cambia = $false
    foreach ($lote in $lotes) {
        $m = [regex]::Match($lote, '(?i)\b(?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+PROC(?:EDURE)?\s+(?:\[?dbo\]?\.)?\[?([A-Za-z0-9_]+)\]?')
        if (-not $m.Success) { continue }
        $nombre = $m.Groups[1].Value
        $clave = $nombre.ToLowerInvariant()
        if (-not $enBd.ContainsKey($clave)) {
            Write-Host ("  FALTA     {0,-50} ({1})" -f $nombre, $archivo.Name) -ForegroundColor Yellow; $faltan++; $cambia = $true
        }
        elseif ($enBd[$clave] -ne (Normalizar $lote)) {
            Write-Host ("  DISTINTO  {0,-50} ({1})" -f $nombre, $archivo.Name); $distintos++; $cambia = $true
        }
        else { $iguales++ }
    }
    if ($cambia -or $Todos) { $aEjecutar.Add($archivo.FullName) }
}
Write-Host "Iguales: $iguales   Distintos: $distintos   Faltan: $faltan   Archivos a ejecutar: $($aEjecutar.Count)"

if ($SoloListar -or $aEjecutar.Count -eq 0) { return }

foreach ($ruta in $aEjecutar) {
    Write-Host "Ejecutando $([IO.Path]::GetFileName($ruta)) ..." -ForegroundColor Cyan
    & $sqlcmd -C -I -b -f 65001 -S $b.DataSource -d $b.InitialCatalog -U $b.UserID -P $b.Password -i $ruta
    if ($LASTEXITCODE -ne 0) { throw "Falló $([IO.Path]::GetFileName($ruta)) (sqlcmd exit $LASTEXITCODE)." }
}
Write-Host "Listo." -ForegroundColor Green
