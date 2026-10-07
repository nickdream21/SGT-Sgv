<#
.SYNOPSIS
    Repara las tildes corruptas (mojibake) en procedimientos, triggers, funciones y vistas de la BD.

.DESCRIPTION
    Algunos objetos se crearon alguna vez con sqlcmd sin -f 65001 y sus textos quedaron como
    "LiquidaciA3n" en lugar de "Liquidacion" con tilde (UTF-8 leido como Windows-1252). Eso
    afecta comentarios y tambien mensajes que ve el usuario (p. ej. los triggers de auditoria).

    El script busca los objetos afectados, corrige el texto (solo secuencias que decodifican a
    UTF-8 valido) y los recrea con el mismo nombre, todo en UNA transaccion: si uno falla, no se
    cambia ninguno. Los triggers deshabilitados se vuelven a deshabilitar.

    Archivo en ASCII puro a proposito (PowerShell 5.1 lee los .ps1 sin BOM como ANSI).

.EXAMPLE
    .\reparar-tildes-bd.ps1 -Entorno pruebas -SoloListar
    .\reparar-tildes-bd.ps1 -Entorno pruebas
    .\reparar-tildes-bd.ps1 -Entorno produccion -ConfirmoProduccion
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno,

    [switch]$SoloListar,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

$dirWeb = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
$bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }
if ($Entorno -eq 'produccion' -and -not $SoloListar -and -not $ConfirmoProduccion) {
    throw "Para modificar PRODUCCION agregue -ConfirmoProduccion (o use -SoloListar)."
}

[xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'." }

# ---- reparacion del texto ----------------------------------------------------
$cp1252 = [Text.Encoding]::GetEncoding(1252)
$utf8Estricto = New-Object Text.UTF8Encoding($false, $true)
# Primer caracter: U+00C3, U+00C2 o U+00E2. Siguientes: el rango 0x80-0xBF y los caracteres
# especiales de Windows-1252 (0x80-0x9F) en sus posiciones Unicode.
$especiales = @(0x152,0x153,0x160,0x161,0x178,0x17D,0x17E,0x192,0x2C6,0x2DC,0x2013,0x2014,0x2018,0x2019,0x201A,0x201C,0x201D,0x201E,0x2020,0x2021,0x2022,0x2026,0x2030,0x2039,0x203A,0x20AC,0x2122) |
              ForEach-Object { '\u{0:X4}' -f $_ }
$patron = '[\xC3\xC2\xE2][\x80-\xBF' + ($especiales -join '') + ']{1,3}'
$evaluador = [System.Text.RegularExpressions.MatchEvaluator] {
    param($m)
    try { return $utf8Estricto.GetString($cp1252.GetBytes($m.Value)) } catch { return $m.Value }
}

Write-Host "Entorno: $Entorno  ->  $($b.DataSource) / $($b.InitialCatalog)" -ForegroundColor Cyan

$conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @"
SELECT QUOTENAME(SCHEMA_NAME(o.schema_id)) + '.' + QUOTENAME(o.name) AS nombre, o.type, m.definition,
       ISNULL(t.is_disabled, 0) AS deshabilitado,
       CASE WHEN o.type = 'TR' THEN QUOTENAME(OBJECT_SCHEMA_NAME(o.parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(o.parent_object_id)) END AS tabla
FROM sys.sql_modules m
JOIN sys.objects o ON o.object_id = m.object_id
LEFT JOIN sys.triggers t ON t.object_id = o.object_id
WHERE o.type IN ('P', 'TR', 'V', 'FN', 'IF', 'TF')
  AND m.definition COLLATE Latin1_General_BIN LIKE N'%' + NCHAR(195) + N'[' + NCHAR(128) + N'-' + NCHAR(191) + N']%'
ORDER BY o.type, o.name
"@
    $r = $cmd.ExecuteReader()
    $objetos = New-Object System.Collections.Generic.List[object]
    while ($r.Read()) {
        $objetos.Add([pscustomobject]@{ Nombre = $r.GetString(0); Tipo = $r.GetString(1).Trim(); Definicion = $r.GetString(2); Deshabilitado = $r.GetBoolean(3); Tabla = if ($r.IsDBNull(4)) { $null } else { $r.GetString(4) } })
    }
    $r.Close()

    Write-Host "Objetos con tildes corruptas: $($objetos.Count)"
    $objetos | ForEach-Object { Write-Host ("  {0,-3} {1}" -f $_.Tipo, $_.Nombre) }
    if ($SoloListar -or $objetos.Count -eq 0) { return }

    $drop = @{ 'P' = 'PROCEDURE'; 'TR' = 'TRIGGER'; 'V' = 'VIEW'; 'FN' = 'FUNCTION'; 'IF' = 'FUNCTION'; 'TF' = 'FUNCTION' }
    $tx = $conn.BeginTransaction()
    try {
        $reparados = 0; $omitidos = New-Object System.Collections.Generic.List[string]
        foreach ($o in $objetos) {
            $nueva = [regex]::Replace($o.Definicion, $patron, $evaluador)
            # Punto de guardado por objeto: si un objeto ya estaba roto en la BD (no se puede volver
            # a crear tal como esta), se deja intacto y se informa, sin perder el resto.
            $tx.Save('obj')
            try {
                (New-Object System.Data.SqlClient.SqlCommand("DROP $($drop[$o.Tipo]) $($o.Nombre)", $conn, $tx)).ExecuteNonQuery() | Out-Null
                (New-Object System.Data.SqlClient.SqlCommand($nueva, $conn, $tx)).ExecuteNonQuery() | Out-Null
                if ($o.Tipo -eq 'TR' -and $o.Deshabilitado) {
                    (New-Object System.Data.SqlClient.SqlCommand("DISABLE TRIGGER $($o.Nombre) ON $($o.Tabla)", $conn, $tx)).ExecuteNonQuery() | Out-Null
                }
                $reparados++
            }
            catch {
                $estado = (New-Object System.Data.SqlClient.SqlCommand("SELECT XACT_STATE()", $conn, $tx)).ExecuteScalar()
                if ($estado -ne 1) { throw }   # transaccion inutilizable: se revierte todo
                $tx.Rollback('obj')
                $omitidos.Add("$($o.Nombre): $($_.Exception.InnerException.Message)")
            }
        }
        $tx.Commit()
        Write-Host "Listo: $reparados objetos recreados con las tildes corregidas." -ForegroundColor Green
        if ($omitidos.Count -gt 0) {
            Write-Host "Omitidos (ya estaban rotos en la BD; no se pueden recrear tal como estan, se dejaron intactos):" -ForegroundColor Yellow
            $omitidos | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
        }
    }
    catch {
        $tx.Rollback()
        throw
    }
}
finally { $conn.Close() }
