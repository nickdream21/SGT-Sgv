<#
.SYNOPSIS
    Ejecuta una consulta o un archivo .sql contra la BD de PRUEBAS (o produccion) sin exponer la clave.

.DESCRIPTION
    Herramienta de uso diario (para personas y para agentes de IA). Lee la cadena ConexionSGV de
    connectionStrings.config (pruebas) o connectionStrings.Production.config (produccion), ambos
    gitignored, y llama a sqlcmd pasando la clave por la variable SQLCMDPASSWORD: nunca aparece en la
    linea de comandos ni en la salida.

    Usa siempre -C -I -f 65001 (certificado de somee, QUOTED_IDENTIFIER para indices filtrados y UTF-8).
    Los archivos pueden tener lotes separados por GO. Con -Salida guarda el resultado en un archivo.
    Con -Texto devuelve las columnas sin cabecera ni recorte (para extraer definiciones de objetos).

    Produccion: solo con -ConfirmoProduccion.

.EXAMPLE
    .\sql.ps1 "SELECT COUNT(*) FROM sys.procedures"
    .\sql.ps1 -Archivo .\Scripts\diagnostico.sql
    .\sql.ps1 "SELECT name FROM sys.tables" -Salida C:\temp\tablas.txt
    .\sql.ps1 "SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.sp_SE_Listar'))" -Texto
    .\sql.ps1 "SELECT DB_NAME()" -Entorno produccion -ConfirmoProduccion
#>
param(
    [Parameter(Position = 0)]
    [string]$Consulta,

    [string]$Archivo,

    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno = 'pruebas',

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

$dirWeb          = Split-Path $PSScriptRoot -Parent
$archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
$bdEsperada      = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }

[xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
$cs = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
$b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cs)
if ($b.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($b.InitialCatalog)', se esperaba '$bdEsperada'." }

$sqlcmd = (Get-Command sqlcmd -ErrorAction SilentlyContinue).Source
if (-not $sqlcmd) {
    $sqlcmd = Get-ChildItem 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\*\Tools\Binn\SQLCMD.EXE' -ErrorAction SilentlyContinue |
              Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $sqlcmd) { throw "No se encontro sqlcmd (instalar Microsoft Command Line Utilities for SQL Server)." }

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

$argumentos = @('-S', $b.DataSource, '-d', $b.InitialCatalog, '-U', $b.UserID,
                '-C', '-I', '-f', '65001', '-b', '-W', '-s', '|')
if ($Archivo) { $argumentos += @('-i', (Resolve-Path $Archivo).Path) }
else          { $argumentos += @('-Q', "SET NOCOUNT ON; $Consulta") }
if ($Salida)  { $argumentos += @('-o', $Salida) }

$env:SQLCMDPASSWORD = $b.Password
try {
    & $sqlcmd @argumentos
    $codigo = $LASTEXITCODE
} finally {
    Remove-Item Env:SQLCMDPASSWORD -ErrorAction SilentlyContinue
}
exit $codigo
