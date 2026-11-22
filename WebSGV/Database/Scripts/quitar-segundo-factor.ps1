<#
.SYNOPSIS
    Emergencia: quita el segundo factor de un usuario que perdió el celular y sus códigos de recuperación
    cuando no hay otro ADMINISTRADOR DE SISTEMA que lo haga desde Gestión de usuarios (E4-14).

.DESCRIPTION
    Muestra el usuario (id, nombre de usuario, rol, estado del factor), pide escribir su nombre de usuario
    para confirmar y, en una transacción, ejecuta sp_US_QuitarSegundoFactor (borra el factor y sus códigos y
    renueva selloSesion: sus sesiones abiertas se cierran) y registra SEGUNDO_FACTOR_QUITADO en AuditoriaLog
    con el usuario de Windows que corrió el script. Al volver a entrar solo se le pide la contraseña (o el
    alta, si Seguridad.ExigirSegundoFactor está en true).
    Usa conexion-bd.ps1 (conexión cifrada, la clave nunca aparece en pantalla). Nunca imprime secretos.
    Producción solo con -ConfirmoProduccion.

.EXAMPLE
    .\quitar-segundo-factor.ps1 -IdUsuario 12
    .\quitar-segundo-factor.ps1 -IdUsuario 12 -Entorno produccion -ConfirmoProduccion
    .\quitar-segundo-factor.ps1 -IdUsuario 12 -Entorno ensayo -BaseDeDatos sgvEnsayo_20261122_1000
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$IdUsuario,

    [ValidateSet('pruebas', 'produccion', 'ensayo')]
    [string]$Entorno = 'pruebas',

    [string]$BaseDeDatos,
    [string]$Servidor,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

if ($Entorno -eq 'produccion' -and -not $ConfirmoProduccion) {
    throw "Para modificar PRODUCCIÓN agregue -ConfirmoProduccion."
}

$dirDatabase = Split-Path $PSScriptRoot -Parent
$dirWeb = Split-Path $dirDatabase -Parent
. (Join-Path $dirDatabase 'conexion-bd.ps1')
$con = Get-ConexionSgv -Entorno $Entorno -BaseDeDatos $BaseDeDatos -Servidor $Servidor -DirWeb $dirWeb
Write-Host "Entorno: $Entorno  ->  $($con.Servidor) / $($con.Bd)" -ForegroundColor Cyan

$conn = New-Object System.Data.SqlClient.SqlConnection($con.Cadena)
$conn.Open()
try {
    $cmd = New-Object System.Data.SqlClient.SqlCommand(@"
SELECT u.nombreUsuario, u.rol, u.activo,
       f.activo AS factorActivo,
       (SELECT COUNT(*) FROM dbo.UsuarioCodigoRecuperacion c WHERE c.idUsuario = u.idUsuario AND c.usadoUtc IS NULL) AS codigos
FROM dbo.Usuarios u
LEFT JOIN dbo.UsuarioSegundoFactor f ON f.idUsuario = u.idUsuario
WHERE u.idUsuario = @id
"@, $conn)
    [void]$cmd.Parameters.AddWithValue('@id', $IdUsuario)
    $r = $cmd.ExecuteReader()
    if (-not $r.Read()) { $r.Close(); throw "No existe el usuario $IdUsuario." }
    $nombreUsuario = ([string]$r['nombreUsuario']).Trim()
    $rol = ([string]$r['rol']).Trim()
    $cuentaActiva = [bool]$r['activo']
    $estadoFactor = if ($r.IsDBNull(3)) { 'sin segundo factor' } elseif ([bool]$r['factorActivo']) { 'ACTIVO' } else { 'alta sin confirmar' }
    $codigos = [int]$r['codigos']
    $r.Close()

    Write-Host ("Usuario {0}: {1}  rol {2}  cuenta {3}  segundo factor: {4}  códigos sin usar: {5}" -f `
        $IdUsuario, $nombreUsuario, $rol, $(if ($cuentaActiva) { 'activa' } else { 'inactiva' }), $estadoFactor, $codigos)

    if ($estadoFactor -eq 'sin segundo factor') {
        Write-Host "No hay nada que quitar." -ForegroundColor Yellow
        return
    }

    $escrito = Read-Host "Para confirmar, escriba el nombre de usuario ($nombreUsuario)"
    if ($escrito.Trim() -cne $nombreUsuario) {
        Write-Host "No coincide: no se cambió nada." -ForegroundColor Yellow
        return
    }

    $operador = "$env:USERDOMAIN\$env:USERNAME"
    $tx = $conn.BeginTransaction()
    try {
        $q = New-Object System.Data.SqlClient.SqlCommand('sp_US_QuitarSegundoFactor', $conn, $tx)
        $q.CommandType = [System.Data.CommandType]::StoredProcedure
        [void]$q.Parameters.AddWithValue('@idUsuario', $IdUsuario)
        $quitado = [bool]$q.ExecuteScalar()

        if ($quitado) {
            $a = New-Object System.Data.SqlClient.SqlCommand(@"
INSERT INTO AuditoriaLog
    (FechaHora, IdUsuario, NombreUsuario, RolUsuario, Accion, TablaAfectada,
     IdRegistroAfectado, Descripcion, ValoresAnteriores, ValoresNuevos, DireccionIP, Navegador)
VALUES
    (dbo.fn_AhoraPeru(), NULL, @nombre, '', 'SEGUNDO_FACTOR_QUITADO', 'Usuarios',
     @id, @descripcion, NULL, NULL, '', 'quitar-segundo-factor.ps1')
"@, $conn, $tx)
            [void]$a.Parameters.AddWithValue('@nombre', 'Script de emergencia (' + $operador + ')')
            [void]$a.Parameters.AddWithValue('@id', [string]$IdUsuario)
            [void]$a.Parameters.AddWithValue('@descripcion',
                "Segundo factor quitado con quitar-segundo-factor.ps1 (usuario $nombreUsuario); sus sesiones se cierran")
            [void]$a.ExecuteNonQuery()
        }
        $tx.Commit()
    }
    catch {
        # Si la transacción ya no existe (conexión caída), el error que importa es el original.
        $errorOriginal = $_
        try { $tx.Rollback() } catch {}
        throw $errorOriginal
    }

    if ($quitado) {
        Write-Host "Listo: se quitó el segundo factor de $nombreUsuario y se cerraron sus sesiones (auditado)." -ForegroundColor Green
    } else {
        Write-Host "No había nada que quitar (otro lo quitó antes)." -ForegroundColor Yellow
    }
}
finally { $conn.Close() }
