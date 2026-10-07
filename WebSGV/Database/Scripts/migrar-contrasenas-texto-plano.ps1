<#
.SYNOPSIS
    Convierte a hash PBKDF2 las contraseñas que siguen en texto plano en dbo.Usuarios.

.DESCRIPTION
    Usa WebSGV.Helpers.PasswordHelper de la propia app (WebSGV\bin\WebSGV.dll: compilar antes),
    así el hash es idéntico al que genera el sistema. Cada usuario sigue entrando con la MISMA
    contraseña; solo cambia cómo se guarda. No modifica requiereCambioContrasena.

    Debe ejecutarse en cada BD ANTES de publicar la versión que quita el respaldo de texto plano
    de PasswordHelper.VerifyPassword (si no, esos usuarios no podrán iniciar sesión).

    Nunca imprime contraseñas. Todo va en una transacción: si algo falla no se cambia nada.
    Cada UPDATE exige que la contraseña siga siendo la leída (si alguien la cambió entre medio,
    esa fila se omite).

.EXAMPLE
    .\migrar-contrasenas-texto-plano.ps1 -Entorno pruebas -SoloListar
    .\migrar-contrasenas-texto-plano.ps1 -Entorno pruebas
    .\migrar-contrasenas-texto-plano.ps1 -Entorno produccion -ConfirmoProduccion
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
    throw "Para modificar PRODUCCIÓN agregue -ConfirmoProduccion (o use -SoloListar)."
}

$dll = Join-Path $dirWeb 'bin\WebSGV.dll'
if (-not (Test-Path $dll)) { throw "No existe $dll. Compile la solución primero." }
Add-Type -Path $dll

[xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'." }

Write-Host "Entorno: $Entorno  ->  $($b.DataSource) / $($b.InitialCatalog)" -ForegroundColor Cyan

$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()
try {
    $pendientes = New-Object System.Collections.Generic.List[object]
    $vacias = 0
    $r = (New-Object System.Data.SqlClient.SqlCommand("SELECT idUsuario, contrasena FROM dbo.Usuarios", $conn)).ExecuteReader()
    while ($r.Read()) {
        $actual = if ($r.IsDBNull(1)) { '' } else { $r.GetString(1) }
        if (-not [WebSGV.Helpers.PasswordHelper]::NeedsMigration($actual)) { continue }
        if ([string]::IsNullOrEmpty($actual)) { $vacias++; continue }
        $pendientes.Add([pscustomobject]@{ Id = $r.GetInt32(0); Actual = $actual })
    }
    $r.Close()

    Write-Host "Contraseñas en texto plano a convertir: $($pendientes.Count)"
    if ($vacias -gt 0) { Write-Host "Usuarios con contraseña vacía (se omiten; no pueden iniciar sesión de todos modos): $vacias" -ForegroundColor Yellow }
    if ($SoloListar -or $pendientes.Count -eq 0) { return }

    $tx = $conn.BeginTransaction()
    try {
        $actualizados = 0
        foreach ($p in $pendientes) {
            $hash = [WebSGV.Helpers.PasswordHelper]::HashPassword($p.Actual)
            if (-not [WebSGV.Helpers.PasswordHelper]::VerifyPassword($p.Actual, $hash)) { throw "El hash generado no verifica (idUsuario $($p.Id))." }

            $cmd = New-Object System.Data.SqlClient.SqlCommand(
                "UPDATE dbo.Usuarios SET contrasena = @nuevo WHERE idUsuario = @id AND contrasena = @actual", $conn, $tx)
            [void]$cmd.Parameters.AddWithValue('@nuevo', $hash)
            [void]$cmd.Parameters.AddWithValue('@id', $p.Id)
            [void]$cmd.Parameters.AddWithValue('@actual', $p.Actual)
            $actualizados += $cmd.ExecuteNonQuery()
        }
        $tx.Commit()
        Write-Host "Listo: $actualizados contraseñas convertidas a PBKDF2." -ForegroundColor Green
    }
    catch {
        $tx.Rollback()
        throw
    }
}
finally { $conn.Close() }
