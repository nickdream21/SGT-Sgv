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

    La conexión se lee igual que en aplicar-migraciones.ps1 (archivos gitignored; con -Entorno ensayo,
    una BD sgvEnsayo_* de un SQL Server local con la autenticación de Windows).
    Aplicar ANTES las migraciones: varios procedimientos usan dbo.fn_AhoraPeru() (migración 16).

    Ejecución de humo (M-052), también con -SoloListar: cada procedimiento del repo que exista en la
    BD se compila con sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(...), 0), que
    detecta tablas o columnas que ya no existen sin ejecutar nada, y con
    sys.dm_sql_referenced_entities (la primera solo informa el primer error). Un error que no esté
    en $ErroresHumoPermitidos se informa y el script termina con código 1, sin "Listo".

.EXAMPLE
    .\aplicar-procedimientos.ps1 -Entorno pruebas -SoloListar
    .\aplicar-procedimientos.ps1 -Entorno pruebas
    .\aplicar-procedimientos.ps1 -Entorno produccion -ConfirmoProduccion
    .\aplicar-procedimientos.ps1 -Entorno ensayo -BaseDeDatos sgvEnsayo_20261121_1000
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pruebas', 'produccion', 'ensayo')]
    [string]$Entorno,

    # Solo con -Entorno ensayo (BD sgvEnsayo_* en un SQL Server local).
    [string]$BaseDeDatos,
    [string]$Servidor,

    [switch]$SoloListar,
    [switch]$Todos,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

$dirDatabase = $PSScriptRoot
$dirSps      = Join-Path $dirDatabase 'StoredProcedures'
$dirWeb      = Split-Path $dirDatabase -Parent

if ($Entorno -eq 'produccion' -and -not $SoloListar -and -not $ConfirmoProduccion) {
    throw "Para modificar PRODUCCIÓN agregue -ConfirmoProduccion (o use -SoloListar)."
}

. (Join-Path $dirDatabase 'conexion-bd.ps1')
$con = Get-ConexionSgv -Entorno $Entorno -BaseDeDatos $BaseDeDatos -Servidor $Servidor -DirWeb $dirWeb
$cs  = $con.Cadena
[void](Get-RutaSqlcmd)

# Errores de la ejecución de humo que son falsos positivos conocidos (procedimiento -> error_number):
# SQL dinámico (11514) y tablas temporales (4902, 11526), cuyo resultado no se puede describir sin
# ejecutar. Medido en sgvActualizada el 2026-11-21. Añadir aquí solo con el motivo.
$ErroresHumoPermitidos = @{
    'sp_SE_CorregirHito'         = 11514   # EXEC sp_executesql con la columna elegida
    'sp_SE_ImportarLote'         = 4902    # usa #lote, que crea quien lo llama
    'sp_SE_RegistrarHitoGps'     = 11514   # EXEC sp_executesql con la columna elegida
    'sp_SE_SincronizarDespachos' = 11526   # INSERT ... desde una tabla temporal
}
# Errores que la DMV puede devolver junto al principal ("no se pudo determinar el resultado"): solo
# se aceptan acompañando al permitido de ese procedimiento, nunca solos.
$ErroresHumoSecundarios = @(11529, 11501)

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

Write-Host "Entorno: $Entorno  ->  $($con.Servidor) / $($con.Bd)" -ForegroundColor Cyan

# Definiciones actuales en la BD
$enBd = @{}
$conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
try {
    $r = (New-Object System.Data.SqlClient.SqlCommand("SELECT o.name, m.definition FROM sys.procedures o JOIN sys.sql_modules m ON m.object_id = o.object_id", $conn)).ExecuteReader()
    while ($r.Read()) { $enBd[$r.GetString(0).ToLowerInvariant()] = Normalizar $r.GetString(1) }
    $r.Close()
} finally { $conn.Close() }

# Procedimientos definidos en cada archivo (lotes separados por GO)
# Orden de ejecución: primero los archivos con algún procedimiento NUEVO (FALTA) y después los que
# cambian (DISTINTO), alfabético dentro de cada grupo. Un SP modificado puede llamar a uno nuevo
# (E3-3: sp_CrearDespacho -> sp_SiguienteNumeroViaje); en orden alfabético puro el llamador quedaba
# aplicado antes y, hasta crear el nuevo (o para siempre si un sqlcmd intermedio fallaba), la BD
# respondía con el error 2812 (procedimiento inexistente).
$aEjecutarNuevos = New-Object System.Collections.Generic.List[string]
$aEjecutar = New-Object System.Collections.Generic.List[string]
$nombresRepo = New-Object System.Collections.Generic.List[string]
$faltan = 0; $distintos = 0; $iguales = 0
foreach ($archivo in Get-ChildItem $dirSps -Filter '*.sql' | Sort-Object Name) {
    $texto = [IO.File]::ReadAllText($archivo.FullName)
    $lotes = [regex]::Split($texto, '(?im)^\s*GO\s*$')
    $cambia = $false; $nuevo = $false
    foreach ($lote in $lotes) {
        $m = [regex]::Match($lote, '(?i)\b(?:CREATE(?:\s+OR\s+ALTER)?|ALTER)\s+PROC(?:EDURE)?\s+(?:\[?dbo\]?\.)?\[?([A-Za-z0-9_]+)\]?')
        if (-not $m.Success) { continue }
        $nombre = $m.Groups[1].Value
        $clave = $nombre.ToLowerInvariant()
        $nombresRepo.Add($nombre)
        if (-not $enBd.ContainsKey($clave)) {
            Write-Host ("  FALTA     {0,-50} ({1})" -f $nombre, $archivo.Name) -ForegroundColor Yellow; $faltan++; $cambia = $true; $nuevo = $true
        }
        elseif ($enBd[$clave] -ne (Normalizar $lote)) {
            Write-Host ("  DISTINTO  {0,-50} ({1})" -f $nombre, $archivo.Name); $distintos++; $cambia = $true
        }
        else { $iguales++ }
    }
    if ($nuevo) { $aEjecutarNuevos.Add($archivo.FullName) }
    elseif ($cambia -or $Todos) { $aEjecutar.Add($archivo.FullName) }
}
$aEjecutar.InsertRange(0, $aEjecutarNuevos)
Write-Host "Iguales: $iguales   Distintos: $distintos   Faltan: $faltan   Archivos a ejecutar: $($aEjecutar.Count)"
if ($aEjecutar.Count -gt 0) {
    Write-Host ("Orden de ejecución: " + (($aEjecutar | ForEach-Object { [IO.Path]::GetFileName($_) }) -join ', '))
}

if (-not $SoloListar) {
    foreach ($ruta in $aEjecutar) {
        Write-Host "Ejecutando $([IO.Path]::GetFileName($ruta)) ..." -ForegroundColor Cyan
        Invoke-SqlcmdSgv $con @('-C', '-I', '-b', '-f', '65001', '-i', $ruta)
        if ($LASTEXITCODE -ne 0) { throw "Falló $([IO.Path]::GetFileName($ruta)) (sqlcmd exit $LASTEXITCODE)." }
    }
}

# ── Ejecución de humo (M-052) ───────────────────────────────────────────────
# Solo compila: dm_exec_describe_first_result_set_for_object no ejecuta el procedimiento.
$erroresHumo = New-Object System.Collections.Generic.List[string]
$revisados = 0; $permitidos = 0
$conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
try {
    foreach ($nombre in ($nombresRepo | Sort-Object -Unique)) {
        $cmd = New-Object System.Data.SqlClient.SqlCommand(@"
SELECT d.error_number, d.error_message
FROM sys.dm_exec_describe_first_result_set_for_object(OBJECT_ID(@nombre), 0) d
WHERE d.error_number IS NOT NULL;
SELECT CASE WHEN OBJECT_ID(@nombre, 'P') IS NULL THEN 0 ELSE 1 END;
"@, $conn)
        $cmd.CommandTimeout = 120
        [void]$cmd.Parameters.AddWithValue('@nombre', 'dbo.' + $nombre)
        $r = $cmd.ExecuteReader()
        $filas = @()
        while ($r.Read()) { $filas += [pscustomobject]@{ Numero = [int]$r.GetValue(0); Mensaje = [string]$r.GetValue(1) } }
        [void]$r.NextResult(); [void]$r.Read(); $existe = [int]$r.GetValue(0) -eq 1
        $r.Close()
        if (-not $existe) { continue }   # FALTA (con -SoloListar): ya se informó arriba
        $revisados++

        # Segunda comprobación: la DMV de arriba informa solo el PRIMER error, así que en los 4
        # permitidos un objeto o una columna que falte más adelante quedaría tapado. Las
        # dependencias (dm_sql_referenced_entities) fallan con el error 2020 si el SP referencia
        # algo que no existe. Medido el 2026-11-21: 0 errores en los 115 SP de la BD.
        $dep = New-Object System.Data.SqlClient.SqlCommand("SELECT COUNT(*) FROM sys.dm_sql_referenced_entities(@nombre, 'OBJECT');", $conn)
        $dep.CommandTimeout = 120
        [void]$dep.Parameters.AddWithValue('@nombre', 'dbo.' + $nombre)
        try { [void]$dep.ExecuteScalar() }
        catch [System.Data.SqlClient.SqlException] {
            $ex = $_.Exception
            $erroresHumo.Add(("{0}: error {1} en sus dependencias: {2}" -f $nombre, $ex.Number, ($ex.Message -replace '\s+', ' ').Trim()))
            continue
        }

        if ($filas.Count -eq 0) { continue }
        # Se revisan TODAS las filas de error: un SP permitido solo pasa si cada una es su número
        # permitido o uno de los secundarios que la DMV añade al mismo error.
        $aceptables = @($ErroresHumoPermitidos[$nombre]) + $ErroresHumoSecundarios | Where-Object { $null -ne $_ }
        $ajenas = @($filas | Where-Object { $aceptables -notcontains $_.Numero })
        if ($ErroresHumoPermitidos.ContainsKey($nombre) -and $ajenas.Count -eq 0 -and
            @($filas | Where-Object { $_.Numero -eq $ErroresHumoPermitidos[$nombre] }).Count -gt 0) { $permitidos++; continue }
        $f = if ($ajenas.Count -gt 0) { $ajenas[0] } else { $filas[0] }
        $erroresHumo.Add(("{0}: error {1}: {2}" -f $nombre, $f.Numero, ($f.Mensaje -replace '\s+', ' ').Trim()))
    }
} finally { $conn.Close() }

Write-Host "Humo: $revisados procedimientos compilados, $permitidos con un error permitido, $($erroresHumo.Count) con error."
if ($erroresHumo.Count -gt 0) {
    foreach ($e in $erroresHumo) { Write-Host "  ERROR HUMO  $e" -ForegroundColor Red }
    Write-Host "La ejecución de humo encontró procedimientos que no compilan contra la BD (ver arriba)." -ForegroundColor Red
    exit 1
}

if ($SoloListar -or $aEjecutar.Count -eq 0) { return }
Write-Host "Listo." -ForegroundColor Green
