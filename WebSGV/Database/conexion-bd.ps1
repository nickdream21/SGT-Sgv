<#
.SYNOPSIS
    Conexión común de sql.ps1, aplicar-migraciones.ps1 y aplicar-procedimientos.ps1 (se carga con ". ").

.DESCRIPTION
    Entornos:
      pruebas    -> WebSGV/connectionStrings.config            (sgvActualizada)
      produccion -> WebSGV/connectionStrings.Production.config (sgvTransporte)
      ensayo     -> SQL Server LOCAL (".", localhost, el nombre de esta PC o (localdb)\...) con la
                    autenticación de Windows y una BD sgvEnsayo_* indicada con -BaseDeDatos. Nunca lee
                    connectionStrings.config ni acepta un servidor remoto (somee incluido).
                    La crea herramientas/respaldo/ensayar-migracion.ps1 a partir de un .bacpac.
#>

# \z y no $: "$" aceptaría también un salto de línea final.
$script:PatronBdEnsayo = '\AsgvEnsayo_[A-Za-z0-9_]+\z'

# Solo nombres que apuntan a esta máquina (misma regla que ensayar-restauracion.ps1 y BaseDePruebas.cs).
function Test-ServidorLocal([string]$Servidor) {
    if ([string]::IsNullOrWhiteSpace($Servidor)) { return $false }
    $s = $Servidor.Trim().ToLowerInvariant() -replace '^(tcp:|np:|lpc:)', ''
    if ($s -match 'somee') { return $false }
    if ($s.StartsWith('(localdb)\')) { return $true }
    $equipo = ($s -split '[\\,]')[0]
    $locales = @('.', '(local)', 'localhost', '127.0.0.1', '::1', $env:COMPUTERNAME.ToLowerInvariant())
    return ($locales -contains $equipo)
}

function Test-NombreBdEnsayo([string]$Nombre) {
    return (-not [string]::IsNullOrWhiteSpace($Nombre)) -and ($Nombre -cmatch $script:PatronBdEnsayo)
}

<#
    Devuelve un objeto con: Entorno, Cadena (SqlClient), Servidor, Bd, Integrada, Usuario, Clave.
    Clave es $null con la autenticación de Windows. Lanza un error si algo no cuadra.
#>
function Get-ConexionSgv {
    param(
        [Parameter(Mandatory = $true)][string]$Entorno,
        [string]$BaseDeDatos,
        [string]$Servidor,
        [Parameter(Mandatory = $true)][string]$DirWeb
    )

    if ($Entorno -eq 'ensayo') {
        if (-not (Test-NombreBdEnsayo $BaseDeDatos)) {
            throw "Con -Entorno ensayo indique -BaseDeDatos sgvEnsayo_<algo> (solo letras, números y _). Recibido: '$BaseDeDatos'."
        }
        if ([string]::IsNullOrWhiteSpace($Servidor)) { $Servidor = '.' }
        if (-not (Test-ServidorLocal $Servidor)) {
            throw "El servidor '$Servidor' no es local. El entorno ensayo solo trabaja con un SQL Server de esta máquina."
        }
        $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
        # PowerShell trata el builder como diccionario: se asigna por clave.
        $b['Data Source'] = $Servidor
        $b['Initial Catalog'] = $BaseDeDatos
        $b['Integrated Security'] = $true
        $b['TrustServerCertificate'] = $true
        $b['Connect Timeout'] = 15
        return [pscustomobject]@{
            Entorno = $Entorno; Cadena = $b.ConnectionString; Servidor = $Servidor; Bd = $BaseDeDatos
            Integrada = $true; Usuario = $null; Clave = $null
        }
    }

    if ($BaseDeDatos -or $Servidor) {
        throw "-BaseDeDatos y -Servidor solo se usan con -Entorno ensayo (pruebas y produccion toman la conexión del archivo de configuración)."
    }
    $archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
    $bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }
    $ruta = Join-Path $DirWeb $archivoConexion
    if (-not (Test-Path $ruta)) { throw "No existe $ruta" }
    [xml]$xml = Get-Content $ruta -Raw
    $cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
    if (-not $cs) { throw "No se encontró la cadena 'ConexionSGV' en $archivoConexion" }
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
    if ($b.InitialCatalog -ne $bdEsperada) {
        throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'. Revise la configuración."
    }
    return [pscustomobject]@{
        Entorno = $Entorno; Cadena = $cs; Servidor = $b.DataSource; Bd = $b.InitialCatalog
        Integrada = [bool]$b.IntegratedSecurity; Usuario = $b.UserID; Clave = $b.Password
    }
}

# Argumentos de sqlcmd para la conexión (sin la clave: va en $env:SQLCMDPASSWORD, ver Invoke-SqlcmdSgv).
function Get-ArgumentosSqlcmd($Conexion) {
    $a = @('-S', $Conexion.Servidor, '-d', $Conexion.Bd)
    if ($Conexion.Integrada) { $a += '-E' } else { $a += @('-U', $Conexion.Usuario) }
    return $a
}

function Get-RutaSqlcmd {
    $sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
    if (-not $sqlcmd) {
        $sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\SQLCMD.EXE' -ErrorAction SilentlyContinue |
                  Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $sqlcmd) { throw "No se encontró sqlcmd. Instale las 'Microsoft Command Line Utilities for SQL Server'." }
    return $sqlcmd
}

# Ejecuta sqlcmd con la conexión; la clave (si hay) viaja por SQLCMDPASSWORD, nunca en la línea de
# comandos. La salida de sqlcmd sale tal cual; el código queda en $LASTEXITCODE.
function Invoke-SqlcmdSgv($Conexion, [string[]]$Argumentos) {
    $sqlcmd = Get-RutaSqlcmd
    $todos = @(Get-ArgumentosSqlcmd $Conexion) + $Argumentos
    if (-not $Conexion.Integrada) { $env:SQLCMDPASSWORD = $Conexion.Clave }
    try {
        & $sqlcmd @todos
    } finally {
        Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
    }
}
