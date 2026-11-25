<#
.SYNOPSIS
Deja entrar a un conductor anonimizado de una BD de ensayo (sgvEnsayo_*) con una clave de ensayo generada al
momento, para las capturas de la ayuda del celular del conductor (E5-21b, M-074).

.DESCRIPTION
Solo en una BD local sgvEnsayo_* (autenticación de Windows): rehúsa si DB_NAME() es otra, en la misma conexión
que escribe. Elige un usuario activo de rol CONDUCTOR con conductor asociado (el de más viajes, o -IdUsuario),
le pone el hash (PasswordHelper.HashPassword de bin\WebSGV.dll) de una clave aleatoria y le quita el cambio
obligatorio de contraseña. La clave no se imprime ni se guarda en el repositorio: va solo a -Salida (JSON con
usuario y clave, por defecto en %TEMP%), que lee herramientas/manual/capturar.mjs --credencial-conductor.
Borre ese archivo y la BD de ensayo al terminar.

.EXAMPLE
.\herramientas\respaldo\clave-conductor-ensayo.ps1 -BaseDeDatos sgvEnsayo_ayuda
#>
param(
    [Parameter(Mandatory = $true)][string]$BaseDeDatos,
    [string]$Servidor = '(localdb)\MSSQLLocalDB',
    [int]$IdUsuario = 0,
    [string]$Salida = (Join-Path $env:TEMP 'sgv-conductor-ensayo.json')
)
$ErrorActionPreference = 'Stop'

if ($BaseDeDatos -notmatch '^sgvEnsayo_[A-Za-z0-9_]+$') { throw 'Solo una BD sgvEnsayo_* (la de ensayo anonimizada).' }
if ($Servidor -notmatch '^\(localdb\)\\|^localhost|^\.\\|^\.$') { throw 'Solo un servidor local.' }

$raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
# La clave nunca dentro del repositorio (misma regla que capturar.mjs --credencial-conductor).
$salidaCompleta = [System.IO.Path]::GetFullPath($Salida)
if ($salidaCompleta.StartsWith([System.IO.Path]::GetFullPath($raiz), [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'La credencial del conductor no va dentro del repositorio: use -Salida fuera de él (por defecto %TEMP%).'
}
$dll = Join-Path $raiz 'WebSGV\bin\WebSGV.dll'
if (-not (Test-Path $dll)) { throw 'Compile primero la solución (falta WebSGV\bin\WebSGV.dll).' }
Add-Type -Path $dll

# Clave aleatoria de 20 caracteres (letras y dígitos), solo en memoria y en -Salida.
$bytes = New-Object byte[] 20
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$alfabeto = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789'
$clave = -join ($bytes | ForEach-Object { $alfabeto[$_ % $alfabeto.Length] })
$hash = [WebSGV.Helpers.PasswordHelper]::HashPassword($clave)

$conn = New-Object System.Data.SqlClient.SqlConnection("Server=$Servidor;Database=$BaseDeDatos;Integrated Security=true;TrustServerCertificate=true")
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = @'
SET NOCOUNT ON;
IF DB_NAME() NOT LIKE 'sgvEnsayo[_]%'
    THROW 50001, 'clave-conductor-ensayo solo corre en una BD sgvEnsayo_*.', 1;
DECLARE @id INT = NULLIF(@IdUsuario, 0);
IF @id IS NULL
    SELECT TOP 1 @id = u.idUsuario
    FROM dbo.Usuarios u
    LEFT JOIN dbo.ViajesEnProgreso v ON v.idConductor = u.idConductor
    WHERE u.activo = 1 AND UPPER(LTRIM(RTRIM(u.rol))) = 'CONDUCTOR' AND u.idConductor IS NOT NULL
    GROUP BY u.idUsuario
    ORDER BY COUNT(v.idConductor) DESC, u.idUsuario;
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios WHERE idUsuario = @id AND activo = 1 AND UPPER(LTRIM(RTRIM(rol))) = 'CONDUCTOR')
    THROW 50002, 'No hay un usuario conductor activo para el ensayo.', 1;
-- Solo una cuenta ya anonimizada (anonimizar-ensayo.sql): usuario 'usuario<id>' y conductor "Conductor Prueba".
IF NOT EXISTS (SELECT 1 FROM dbo.Usuarios u INNER JOIN dbo.Conductor c ON c.idConductor = u.idConductor
               WHERE u.idUsuario = @id AND u.nombreUsuario LIKE 'usuario%' AND c.nombre = 'Conductor' AND c.apPaterno = 'Prueba')
    THROW 50003, 'El conductor elegido no está anonimizado: corra antes herramientas/respaldo/anonimizar-ensayo.sql. No se cambió nada.', 1;
UPDATE dbo.Usuarios SET contrasena = @Hash, requiereCambioContrasena = 0 WHERE idUsuario = @id;
SELECT nombreUsuario FROM dbo.Usuarios WHERE idUsuario = @id;
'@
    [void]$cmd.Parameters.AddWithValue('@IdUsuario', $IdUsuario)
    [void]$cmd.Parameters.AddWithValue('@Hash', $hash)
    $usuario = [string]$cmd.ExecuteScalar()
}
finally { $conn.Close() }

$json = @{ usuario = $usuario; clave = $clave } | ConvertTo-Json -Compress
if ($usuario -notlike 'usuario*') { throw 'La BD no devolvió una cuenta anonimizada; no se escribió la credencial.' }
[System.IO.File]::WriteAllText($salidaCompleta, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Conductor de ensayo: $usuario (clave en $Salida; no se imprime). Borre ese archivo al terminar."
