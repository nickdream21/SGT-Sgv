<#
.SYNOPSIS
    Ejecuta una consulta o un archivo .sql contra la BD de PRUEBAS (o produccion, o un ensayo local) sin exponer la clave.

.DESCRIPTION
    Herramienta de uso diario (para personas y para agentes de IA). Lee la cadena ConexionSGV de
    connectionStrings.config (pruebas) o connectionStrings.Production.config (produccion), ambos
    gitignored, y llama a sqlcmd pasando la clave por la variable SQLCMDPASSWORD: nunca aparece en la
    linea de comandos ni en la salida.

    Usa siempre -C -I -f 65001 (certificado de somee, QUOTED_IDENTIFIER para indices filtrados y UTF-8).
    Los archivos pueden tener lotes separados por GO. Con -Salida guarda el resultado en un archivo.
    Con -Texto devuelve las columnas sin cabecera ni recorte (para extraer definiciones de objetos).

    Produccion: solo con -ConfirmoProduccion.
    Ensayo: -Entorno ensayo -BaseDeDatos sgvEnsayo_<algo> [-Servidor .]: solo un SQL Server LOCAL con
    autenticacion de Windows; no lee connectionStrings.config (ver conexion-bd.ps1).

.EXAMPLE
    .\sql.ps1 "SELECT COUNT(*) FROM sys.procedures"
    .\sql.ps1 -Archivo .\Scripts\diagnostico.sql
    .\sql.ps1 "SELECT name FROM sys.tables" -Salida C:\temp\tablas.txt
    .\sql.ps1 "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.sp_SE_CambiarEstado'))" -Texto
    .\sql.ps1 "SELECT DB_NAME()" -Entorno produccion -ConfirmoProduccion
    .\sql.ps1 "SELECT COUNT(*) FROM dbo.SchemaVersion" -Entorno ensayo -BaseDeDatos sgvEnsayo_20261121_1000
#>
param(
    [Parameter(Position = 0)]
    [string]$Consulta,

    [string]$Archivo,

    [ValidateSet('pruebas', 'produccion', 'ensayo')]
    [string]$Entorno = 'pruebas',

    # Solo con -Entorno ensayo (BD sgvEnsayo_* en un SQL Server local).
    [string]$BaseDeDatos,
    [string]$Servidor,

    [string]$Salida,
    [switch]$Texto,
    [switch]$ConfirmoProduccion
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Consulta) -eq [string]::IsNullOrWhiteSpace($Archivo)) {
    throw "Indique una consulta o -Archivo (solo uno)."
}
if ($Entorno -eq 'produccion' -and -not $ConfirmoProduccion) {
    throw "Para usar PRODUCCION agregue -ConfirmoProduccion."
}

$dirWeb = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'conexion-bd.ps1')
$con = Get-ConexionSgv -Entorno $Entorno -BaseDeDatos $BaseDeDatos -Servidor $Servidor -DirWeb $dirWeb
$cs  = $con.Cadena
$sqlcmd = Get-RutaSqlcmd

if ($Texto) {
    # sqlcmd no permite "sin cabecera" y "sin recorte" a la vez: se usa SqlClient. Cada fila sale
    # como el texto de su primera columna; los lotes se separan por lineas GO.
    $sqlTexto = if ($Archivo) { [IO.File]::ReadAllText((Resolve-Path $Archivo).Path) } else { $Consulta }
    $lineas = New-Object System.Collections.Generic.List[string]
    $conn = New-Object System.Data.SqlClient.SqlConnection($cs); $conn.Open()
    try {
        foreach ($lote in [regex]::Split($sqlTexto, '(?im)^\s*GO\s*$')) {
            if ([string]::IsNullOrWhiteSpace($lote)) { continue }
            $cmd = New-Object System.Data.SqlClient.SqlCommand($lote, $conn); $cmd.CommandTimeout = 300
            $r = $cmd.ExecuteReader()
            do { while ($r.Read()) { $lineas.Add([string]$r.GetValue(0)) } } while ($r.NextResult())
            $r.Close()
        }
    } finally { $conn.Close() }
    if ($Salida) { [IO.File]::WriteAllLines($Salida, $lineas, (New-Object Text.UTF8Encoding($true))) }
    else         { $lineas }
    exit 0
}

$argumentos = @(Get-ArgumentosSqlcmd $con) + @('-C', '-I', '-f', '65001', '-b', '-W', '-s', '|')
if ($Archivo) { $argumentos += @('-i', (Resolve-Path $Archivo).Path) }
else          { $argumentos += @('-Q', "SET NOCOUNT ON; $Consulta") }
if ($Salida)  { $argumentos += @('-o', $Salida) }

if (-not $con.Integrada) { $env:SQLCMDPASSWORD = $con.Clave }
try {
    & $sqlcmd @argumentos
    $codigo = $LASTEXITCODE
} finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
}
exit $codigo
