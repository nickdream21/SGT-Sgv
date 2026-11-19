<#
.SYNOPSIS
    Ensaya la restauración de un respaldo .bacpac de SGV en un SQL Server LOCAL y comprueba que esté completo.

.DESCRIPTION
    Un respaldo que nunca se restauró no está probado. Este script:

    1. Importa el .bacpac con sqlpackage /Action:Import a una BD NUEVA
       sgvRestaurada_aaaaMMdd_HHmm en un SQL Server local (nunca sobre una BD existente, nunca en somee,
       nunca con el nombre de una BD real: sgvActualizada / sgvTransporte).
    2. Compara la BD restaurada con el propio .bacpac (Origin.xml: número de objetos y total de filas;
       model.xml: nombres de tablas, procedimientos, vistas, funciones y triggers) y con la BD de somee
       (solo SELECT; filas por tabla y nombres de objetos). Una tabla que cambia a cada rato
       (auditoría, seguimiento GPS) con más filas en somee se informa como "cambió después del
       respaldo", no como error.
    3. Comprueba que existen y funcionan los triggers que impiden modificar o borrar FirmaDigital y
       AuditoriaLog: intenta UPDATE y DELETE dentro de una transacción en la BD RESTAURADA LOCAL, exige
       que fallen y hace ROLLBACK.
    4. Borra la BD restaurada al final (salvo -Conservar): contiene datos reales.

    La conexión local usa la autenticación de Windows (sin contraseña). La de somee se lee de
    WebSGV\connectionStrings.config igual que sql.ps1 y nunca se muestra.

    Código de salida:
      0  todo bien.
      1  la importación falló, faltan tablas u objetos respecto al .bacpac, el total de filas no
         coincide con el del .bacpac o un trigger de inmutabilidad no funciona.
      2  la restauración es fiel al .bacpac, pero hay diferencias con somee que no se explican solas
         (revisar: alguien pudo usar el sistema durante el ensayo, o se creó un objeto después).

.PARAMETER Bacpac
    Archivo .bacpac. Si falta, se usa el más reciente de <Carpeta>\bd\.

.PARAMETER Carpeta
    Carpeta de respaldos (la misma de respaldar-bd.ps1). Por defecto $env:SGV_CARPETA_RESPALDO.

.PARAMETER Servidor
    SQL Server local de destino. Por defecto el primero que responda y permita crear bases:
    "." (instancia por defecto) y luego "(localdb)\MSSQLLocalDB".

.PARAMETER NombreBd
    Nombre de la BD restaurada. Por defecto sgvRestaurada_aaaaMMdd_HHmm.

.PARAMETER Conservar
    No borra la BD restaurada al terminar (para revisarla a mano; bórrela después).

.PARAMETER SinSomee
    No compara con somee (por ejemplo en un desastre real, si somee no responde).

.EXAMPLE
    .\herramientas\respaldo\ensayar-restauracion.ps1 -Carpeta D:\RespaldosSGV
    .\herramientas\respaldo\ensayar-restauracion.ps1 -Bacpac D:\RespaldosSGV\bd\sgvActualizada_20261119_2300.bacpac -Conservar
    .\herramientas\respaldo\ensayar-restauracion.ps1 -Carpeta D:\RespaldosSGV -SinSomee
#>
param(
    [string]$Bacpac,
    [string]$Carpeta,
    [string]$Servidor,
    [string]$NombreBd,
    [switch]$Conservar,
    [switch]$SinSomee,

    [ValidateSet('pruebas', 'produccion')]
    [string]$Entorno = 'pruebas',
    [switch]$ConfirmoProduccion,

    # Tablas que cambian a cada rato: si en somee tienen más filas que en el respaldo, no es error.
    [string[]]$TablasCambiantes = @('dbo.AuditoriaLog', 'dbo.Auditoria', 'dbo.SeguimientoExportacion',
                                    'dbo.SeguimientoHito', 'dbo.PuntoControlGps')
)

$ErrorActionPreference = 'Stop'
$inicio = Get-Date
$bdReales = @('sgvActualizada', 'sgvTransporte')

# --- Utilidades ----------------------------------------------------------------------------------
function Invoke-Consulta([string]$CadenaConexion, [string]$Sql) {
    $conn = New-Object System.Data.SqlClient.SqlConnection($CadenaConexion)
    $conn.Open()
    try {
        $cmd = New-Object System.Data.SqlClient.SqlCommand($Sql, $conn)
        $cmd.CommandTimeout = 300
        $tabla = New-Object System.Data.DataTable
        (New-Object System.Data.SqlClient.SqlDataAdapter($cmd)).Fill($tabla) | Out-Null
        return , $tabla
    } finally { $conn.Close() }
}

function Get-CadenaLocal([string]$Srv, [string]$Bd) {
    # PowerShell trata el builder como diccionario: se asigna por clave, no por propiedad.
    $b = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    $b['Data Source'] = $Srv
    $b['Initial Catalog'] = $Bd
    $b['Integrated Security'] = $true
    $b['TrustServerCertificate'] = $true
    $b['Connect Timeout'] = 10
    $b.ConnectionString
}

# Solo se aceptan nombres que apuntan a esta máquina.
function Test-ServidorLocal([string]$Srv) {
    $s = $Srv.Trim().ToLowerInvariant() -replace '^(tcp:|np:|lpc:)', ''
    if ($s -match 'somee') { return $false }
    $hostName = ($s -split '[\\,]')[0]
    $locales = @('.', '(local)', 'localhost', '127.0.0.1', '::1', $env:COMPUTERNAME.ToLowerInvariant())
    if ($s.StartsWith('(localdb)\')) { return $true }
    return ($locales -contains $hostName)
}

# Consultas comunes (las mismas en la BD restaurada y en somee; solo lectura).
$sqlObjetos = @"
SET NOCOUNT ON;
SELECT CASE WHEN o.type = 'U' THEN N'Tabla' WHEN o.type = 'P' THEN N'Procedimiento' WHEN o.type = 'V' THEN N'Vista'
            WHEN o.type IN ('FN','IF','TF') THEN N'Funcion' ELSE N'Trigger' END AS tipo,
       s.name + N'.' + o.name AS nombre
FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE o.is_ms_shipped = 0 AND o.type IN ('U','P','V','FN','IF','TF','TR');
"@
$sqlFilas = @"
SET NOCOUNT ON;
DECLARE @s nvarchar(max);
SELECT @s = STRING_AGG(CAST(N'SELECT N''' + REPLACE(s.name + N'.' + t.name, N'''', N'''''') + N''' AS tabla, COUNT_BIG(*) AS filas FROM '
                            + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) AS nvarchar(max)), N' UNION ALL ')
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0;
EXEC sp_executesql @s;
"@

function ConvertTo-Diccionario($Tabla, [string]$Clave, [string]$Valor) {
    $d = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($f in $Tabla.Rows) { $d[[string]$f[$Clave]] = $f[$Valor] }
    , $d
}

# --- Validaciones --------------------------------------------------------------------------------
if ($Entorno -eq 'produccion' -and -not $ConfirmoProduccion) {
    throw "Para comparar con PRODUCCION agregue -ConfirmoProduccion."
}

$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
if ([string]::IsNullOrWhiteSpace($Bacpac)) {
    if ([string]::IsNullOrWhiteSpace($Carpeta)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
    if ([string]::IsNullOrWhiteSpace($Carpeta)) {
        throw "Indique -Bacpac <archivo>, -Carpeta <ruta> o defina la variable de entorno SGV_CARPETA_RESPALDO."
    }
    $carpetaBd = Join-Path ($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Carpeta)) 'bd'
    $ultimo = Get-ChildItem $carpetaBd -File -Filter '*.bacpac' -ErrorAction SilentlyContinue |
              Where-Object { $_.Name -notlike '*.parcial.bacpac' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $ultimo) { throw "No hay archivos .bacpac en $carpetaBd." }
    $Bacpac = $ultimo.FullName
}
$Bacpac = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Bacpac))
if (-not (Test-Path $Bacpac -PathType Leaf)) { throw "No existe el archivo $Bacpac." }
if ($Bacpac.StartsWith($raizRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "El .bacpac está dentro del repositorio. Los respaldos tienen datos reales: deben estar fuera del repo."
}

if ([string]::IsNullOrWhiteSpace($NombreBd)) { $NombreBd = 'sgvRestaurada_' + $inicio.ToString('yyyyMMdd_HHmm') }
if ($NombreBd -notmatch '^[A-Za-z0-9_]+$') { throw "Nombre de BD no válido: '$NombreBd' (solo letras, números y _)." }
foreach ($real in $bdReales) {
    if ($NombreBd -like "*$real*") { throw "El destino '$NombreBd' usa el nombre de una BD real ($real). Elija otro nombre." }
}

if ($Servidor) {
    if (-not (Test-ServidorLocal $Servidor)) {
        throw "El servidor '$Servidor' no es local. El ensayo solo restaura en un SQL Server de esta máquina, nunca en somee."
    }
} else {
    foreach ($candidato in @('.', '(localdb)\MSSQLLocalDB')) {
        try {
            $t = Invoke-Consulta (Get-CadenaLocal $candidato 'master') "SELECT HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE') AS p"
            if ([int]$t.Rows[0].p -eq 1) { $Servidor = $candidato; break }
        } catch { }
    }
    if (-not $Servidor) { throw "No se encontró un SQL Server local que responda y permita crear bases ('.' ni '(localdb)\MSSQLLocalDB'). Use -Servidor." }
}

$cadenaMaster = Get-CadenaLocal $Servidor 'master'
$infoServidor = (Invoke-Consulta $cadenaMaster "SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) + N' ' + CAST(SERVERPROPERTY('Edition') AS nvarchar(100)) AS v, HAS_PERMS_BY_NAME(NULL, NULL, 'CREATE ANY DATABASE') AS p").Rows[0]
if ([int]$infoServidor.p -ne 1) { throw "El usuario de Windows no tiene permiso para crear bases en '$Servidor'." }
$existe = Invoke-Consulta $cadenaMaster "SELECT COUNT(*) AS n FROM sys.databases WHERE name = N'$NombreBd'"
if ([int]$existe.Rows[0].n -gt 0) { throw "Ya existe la BD '$NombreBd' en '$Servidor'. El ensayo nunca importa sobre una BD existente." }

$sqlpackage = (Get-Command sqlpackage -ErrorAction SilentlyContinue).Source
if (-not $sqlpackage) { throw "No se encontró sqlpackage (instalar con: dotnet tool install -g microsoft.sqlpackage)." }

# --- Lo que dice el propio .bacpac ---------------------------------------------------------------
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Read-EntradaZip($Zip, [string]$Nombre) {
    $e = $Zip.Entries | Where-Object { $_.FullName -eq $Nombre } | Select-Object -First 1
    if (-not $e) { throw "El .bacpac no contiene $Nombre (¿archivo dañado?)." }
    $r = New-Object IO.StreamReader($e.Open())
    try { $r.ReadToEnd() } finally { $r.Close() }
}
try {
    $zip = [IO.Compression.ZipFile]::OpenRead($Bacpac)
    try {
        [xml]$origen = Read-EntradaZip $zip 'Origin.xml'
        [xml]$modelo = Read-EntradaZip $zip 'model.xml'
    } finally { $zip.Dispose() }
} catch {
    Write-Host "ERROR: el archivo no se puede leer como .bacpac (dañado o incompleto): $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "RESULTADO: FALLÓ. Use un respaldo anterior." -ForegroundColor Red
    exit 1
}

$conteosOrigen = $origen.DacOrigin.Server.ObjectCounts
function Get-ConteoOrigen([string[]]$Nombres) {
    $n = 0
    foreach ($x in $Nombres) { $v = $conteosOrigen.$x; if ($v) { $n += [int]$v } }
    $n
}
$esperadoBacpac = [ordered]@{
    Tabla         = Get-ConteoOrigen 'Table'
    Procedimiento = Get-ConteoOrigen 'Procedure'
    Vista         = Get-ConteoOrigen 'View'
    Funcion       = Get-ConteoOrigen 'ScalarFunction', 'InlineTableValuedFunction', 'MultiStatementTableValuedFunction', 'TableValuedFunction'
    Trigger       = Get-ConteoOrigen 'DmlTrigger'
}
$filasBacpac = [long]$origen.DacOrigin.ExportStatistics.TableRowCountTotalTag

$tiposModelo = @{
    SqlTable = 'Tabla'; SqlProcedure = 'Procedimiento'; SqlView = 'Vista'; SqlDmlTrigger = 'Trigger'
    SqlScalarFunction = 'Funcion'; SqlInlineTableValuedFunction = 'Funcion'; SqlMultiStatementTableValuedFunction = 'Funcion'
}
$objetosModelo = @()
$loginsModelo = @()
foreach ($el in $modelo.DataSchemaModel.Model.Element) {
    $tipo = $tiposModelo[$el.Type]
    if ($tipo) { $objetosModelo += ('{0}|{1}' -f $tipo, ($el.Name -replace '[\[\]]', '')) }
    if ($el.Type -eq 'SqlLogin') { $loginsModelo += ($el.Name -replace '[\[\]]', '') }
}

"Respaldo : $Bacpac ($('{0:N1} MB' -f ((Get-Item $Bacpac).Length / 1MB)))"
"Exportado: $($origen.DacOrigin.Operation.Start) desde $(($origen.DacOrigin.Server.ServerVersion -split "`n")[0].Trim())"
"Destino  : [$NombreBd] en '$Servidor' (SQL Server $($infoServidor.v))"
""

# --- Importación ---------------------------------------------------------------------------------
$creada = $false
$errores = New-Object System.Collections.Generic.List[string]
$avisos  = New-Object System.Collections.Generic.List[string]
$info    = New-Object System.Collections.Generic.List[string]
$codigoSalida = 0

function Remove-BdRestaurada {
    # Solo borra la BD que este mismo ensayo creó (se comprobó al inicio que no existía).
    $sql = "IF DB_ID(N'$NombreBd') IS NOT NULL BEGIN ALTER DATABASE [$NombreBd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$NombreBd]; END"
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    Invoke-Consulta $cadenaMaster $sql | Out-Null
}

# El .bacpac trae el login SQL del usuario de somee y sqlpackage lo crea en el servidor local (con
# una contraseña aleatoria). Se borran solo los logins del .bacpac que no existían antes del ensayo.
function Get-LoginsServidor {
    @((Invoke-Consulta $cadenaMaster "SELECT name FROM sys.server_principals WHERE type IN ('S','U','G')").Rows | ForEach-Object { [string]$_.name })
}
$loginsAntes = Get-LoginsServidor
function Remove-LoginsCreados {
    $ahora = Get-LoginsServidor
    foreach ($l in $loginsModelo) {
        if (($ahora -contains $l) -and -not ($loginsAntes -contains $l)) {
            Invoke-Consulta $cadenaMaster ("DROP LOGIN " + '[' + $l.Replace(']', ']]') + ']') | Out-Null
            "Login [$l] creado por la importación: borrado del servidor local."
        }
    }
}

try {
    "Importando con sqlpackage..."
    $inicioImport = Get-Date
    $preferencia = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $sqlpackage '/Action:Import' "/SourceFile:$Bacpac" "/TargetServerName:$Servidor" "/TargetDatabaseName:$NombreBd" `
            '/TargetTrustServerCertificate:True' '/p:CommandTimeout=600' 2>&1 | ForEach-Object {
                # Se omite el detalle por índice y por tabla (cientos de líneas); se muestra lo demás.
                $t = "$_"
                if ($t -notmatch "^\s*(Deshabilitando|Habilitando) (índice|indice) |^\s*(Disabling|Enabling) index |^\s*Procesando tabla |^\s*Processing Table ") { "  $t" }
            }
        $codigoImport = $LASTEXITCODE
    } finally { $ErrorActionPreference = $preferencia }
    $duracionImport = (Get-Date) - $inicioImport
    $creada = $true   # aunque falle, sqlpackage puede dejar una BD a medias: se limpia al final

    if ($codigoImport -ne 0) {
        $errores.Add("La importación falló (sqlpackage código $codigoImport).")
        throw 'IMPORTACION'
    }
    ""
    "Importación terminada en {0:hh\:mm\:ss}." -f $duracionImport
    ""

    $cadenaRest = Get-CadenaLocal $Servidor $NombreBd
    $bdActual = (Invoke-Consulta $cadenaRest 'SELECT DB_NAME() AS n').Rows[0].n
    if ($bdActual -ne $NombreBd) { throw "Conexión inesperada a '$bdActual'." }

    # --- (b) Objetos ------------------------------------------------------------------------------
    $objRest = Invoke-Consulta $cadenaRest $sqlObjetos
    $nombresRest = @($objRest.Rows | ForEach-Object { '{0}|{1}' -f $_.tipo, $_.nombre })
    $setRest = New-Object 'System.Collections.Generic.HashSet[string]' ([string[]]$nombresRest), ([StringComparer]::OrdinalIgnoreCase)

    "Objetos            .bacpac  restaurada  somee"
    $objSomee = $null; $setSomee = $null
    if (-not $SinSomee) {
        $dirWeb = Join-Path $raizRepo 'WebSGV'
        $archivoConexion = if ($Entorno -eq 'produccion') { 'connectionStrings.Production.config' } else { 'connectionStrings.config' }
        $bdEsperada = if ($Entorno -eq 'produccion') { 'sgvTransporte' } else { 'sgvActualizada' }
        [xml]$xml = Get-Content (Join-Path $dirWeb $archivoConexion) -Raw
        $cadenaSomee = ($xml.connectionStrings.add | Where-Object { $_.name -eq 'ConexionSGV' } | Select-Object -First 1).connectionString
        $bs = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($cadenaSomee)
        if ($bs.InitialCatalog -ne $bdEsperada) { throw "$archivoConexion apunta a '$($bs.InitialCatalog)', se esperaba '$bdEsperada'." }
        try {
            $objSomee = Invoke-Consulta $cadenaSomee $sqlObjetos
            $setSomee = New-Object 'System.Collections.Generic.HashSet[string]' ([string[]]@($objSomee.Rows | ForEach-Object { '{0}|{1}' -f $_.tipo, $_.nombre })), ([StringComparer]::OrdinalIgnoreCase)
        } catch {
            $avisos.Add("No se pudo leer somee para comparar: $($_.Exception.GetType().Name). Repita con -SinSomee si somee no responde.")
            $SinSomee = $true
        }
    }
    foreach ($tipo in $esperadoBacpac.Keys) {
        $nRest  = @($objRest.Rows | Where-Object { $_.tipo -eq $tipo }).Count
        $nSomee = if ($objSomee) { @($objSomee.Rows | Where-Object { $_.tipo -eq $tipo }).Count } else { '-' }
        '  {0,-15} {1,8} {2,11} {3,6}' -f $tipo, $esperadoBacpac[$tipo], $nRest, $nSomee
        if ($nRest -ne $esperadoBacpac[$tipo]) { $errores.Add("$tipo`: el .bacpac declara $($esperadoBacpac[$tipo]) y la BD restaurada tiene $nRest.") }
    }
    foreach ($o in $objetosModelo) {
        if (-not $setRest.Contains($o)) { $errores.Add("Falta en la BD restaurada (está en el .bacpac): $($o -replace '\|', ' ')") }
    }
    if ($setSomee) {
        foreach ($o in $setSomee) {
            if (-not $setRest.Contains($o)) { $avisos.Add("Está en somee pero no en el respaldo (¿creado después?): $($o -replace '\|', ' ')") }
        }
        foreach ($o in $setRest) {
            if (-not $setSomee.Contains($o)) { $avisos.Add("Está en el respaldo pero ya no en somee (¿borrado después?): $($o -replace '\|', ' ')") }
        }
    }
    ""

    # --- (a) Filas por tabla ----------------------------------------------------------------------
    $filasRest = ConvertTo-Diccionario (Invoke-Consulta $cadenaRest $sqlFilas) 'tabla' 'filas'
    $totalRest = [long]0
    foreach ($v in $filasRest.Values) { $totalRest += [long]$v }
    "Filas: {0:N0} en la BD restaurada; el .bacpac declara {1:N0}." -f $totalRest, $filasBacpac
    if ($totalRest -ne $filasBacpac) { $errores.Add("Total de filas: el .bacpac declara $filasBacpac y la BD restaurada tiene $totalRest.") }

    if (-not $SinSomee) {
        $filasSomee = ConvertTo-Diccionario (Invoke-Consulta $cadenaSomee $sqlFilas) 'tabla' 'filas'
        $iguales = 0
        foreach ($t in ($filasRest.Keys | Sort-Object)) {
            if (-not $filasSomee.ContainsKey($t)) { continue }   # ya informado en objetos
            $r = [long]$filasRest[$t]; $s = [long]$filasSomee[$t]
            if ($r -eq $s) { $iguales++; continue }
            $texto = '{0}: respaldo {1:N0}, somee {2:N0} ({3:+#,0;-#,0})' -f $t, $r, $s, ($s - $r)
            if ($s -gt $r -and ($TablasCambiantes -contains $t)) { $info.Add("Cambió después del respaldo: $texto") }
            else { $avisos.Add("Filas distintas: $texto") }
        }
        "Tablas con las mismas filas que somee: $iguales de $($filasRest.Count)."
    }
    ""

    # --- (c) Triggers de inmutabilidad (solo en la BD restaurada local) ---------------------------
    "Triggers de inmutabilidad (UPDATE y DELETE en transacción + ROLLBACK, en [$NombreBd]):"
    $trig = Invoke-Consulta $cadenaRest "SELECT t.name, OBJECT_NAME(t.parent_id) AS tabla, t.is_disabled, t.is_instead_of_trigger, (SELECT COUNT(*) FROM sys.trigger_events e WHERE e.object_id = t.object_id AND e.type_desc = 'UPDATE') AS upd, (SELECT COUNT(*) FROM sys.trigger_events e WHERE e.object_id = t.object_id AND e.type_desc = 'DELETE') AS del FROM sys.triggers t WHERE t.parent_class = 1 AND OBJECT_NAME(t.parent_id) IN ('FirmaDigital', 'AuditoriaLog')"
    foreach ($tablaInm in @('FirmaDigital', 'AuditoriaLog')) {
        foreach ($op in @('UPDATE', 'DELETE')) {
            $col = if ($op -eq 'UPDATE') { 'upd' } else { 'del' }
            $activos = @($trig.Rows | Where-Object { $_.tabla -eq $tablaInm -and $_.$col -gt 0 -and -not $_.is_disabled })
            if ($activos.Count -eq 0) {
                $errores.Add("No hay un trigger activo que impida $op en $tablaInm.")
                '  {0,-13} {1,-7} FALTA EL TRIGGER' -f $tablaInm, $op
                continue
            }
            $columna = (Invoke-Consulta $cadenaRest "SELECT TOP 1 QUOTENAME(name) AS c FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.$tablaInm') AND is_identity = 0 AND is_computed = 0 AND system_type_id NOT IN (34, 35, 99, 189) ORDER BY column_id").Rows[0].c
            $sentencia = if ($op -eq 'UPDATE') { "UPDATE TOP (1) dbo.$tablaInm SET $columna = $columna;" } else { "DELETE TOP (1) FROM dbo.$tablaInm;" }
            $prueba = @"
SET NOCOUNT ON; SET XACT_ABORT OFF;
IF DB_NAME() <> N'$NombreBd' THROW 50000, N'Base inesperada', 1;
DECLARE @antes bigint = (SELECT COUNT_BIG(*) FROM dbo.$tablaInm);
DECLARE @r nvarchar(4000) = N'NO_FALLO';
BEGIN TRAN;
BEGIN TRY
    $sentencia
END TRY
BEGIN CATCH
    SET @r = N'FALLO: ' + ERROR_MESSAGE();
END CATCH
IF @@TRANCOUNT > 0 ROLLBACK;
SELECT @r AS r, (SELECT COUNT_BIG(*) FROM dbo.$tablaInm) - @antes AS dif;
"@
            $res = (Invoke-Consulta $cadenaRest $prueba).Rows[0]
            if ($res.r -like 'FALLO:*' -and [long]$res.dif -eq 0) {
                $msg = ([string]$res.r).Substring(7)
                if ($msg.Length -gt 70) { $msg = $msg.Substring(0, 70) + '...' }
                '  {0,-13} {1,-7} bloqueado ({2})  "{3}"' -f $tablaInm, $op, $activos[0].name, $msg
            } else {
                $errores.Add("El $op en $tablaInm NO fue bloqueado por el trigger.")
                '  {0,-13} {1,-7} NO BLOQUEADO' -f $tablaInm, $op
            }
        }
    }
    ""
} catch {
    if ("$_" -ne 'IMPORTACION') { $errores.Add("Error durante el ensayo: $($_.Exception.Message)") }
} finally {
    if ($creada) {
        if ($Conservar -and $errores.Count -eq 0) {
            "BD conservada: [$NombreBd] en '$Servidor'. Bórrela al terminar (tiene datos reales):"
            "  sqlcmd -S $Servidor -E -C -Q `"ALTER DATABASE [$NombreBd] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$NombreBd]`""
        } elseif ($Conservar) {
            "BD conservada para revisar el error: [$NombreBd] en '$Servidor'. Bórrela al terminar."
        } else {
            try { Remove-BdRestaurada; "BD restaurada [$NombreBd] borrada." }
            catch { $avisos.Add("No se pudo borrar la BD [$NombreBd]: bórrela a mano. $($_.Exception.Message)") }
        }
        if ($Conservar) {
            $nuevos = @($loginsModelo | Where-Object { -not ($loginsAntes -contains $_) })
            if ($nuevos.Count -gt 0) { "Al borrar la BD, borre también el login creado por la importación: DROP LOGIN [$($nuevos -join '], [')]" }
        } else {
            try { Remove-LoginsCreados }
            catch { $avisos.Add("No se pudo borrar el login creado por la importación: bórrelo a mano. $($_.Exception.Message)") }
        }
    }
}

# --- Resumen -------------------------------------------------------------------------------------
$duracion = (Get-Date) - $inicio
""
"===== Resumen del ensayo ({0:hh\:mm\:ss} en total) =====" -f $duracion
foreach ($m in $info)    { "  INFO   $m" }
foreach ($m in $avisos)  { "  AVISO  $m" }
foreach ($m in $errores) { "  ERROR  $m" }
if ($errores.Count -gt 0) {
    $codigoSalida = 1
    Write-Host "RESULTADO: FALLÓ. El respaldo NO se pudo restaurar completo." -ForegroundColor Red
} elseif ($avisos.Count -gt 0) {
    $codigoSalida = 2
    Write-Host "RESULTADO: restauración fiel al .bacpac, con diferencias frente a somee que hay que revisar." -ForegroundColor Yellow
} else {
    Write-Host "RESULTADO: OK. El respaldo se restauró completo y los triggers de inmutabilidad funcionan." -ForegroundColor Green
}

$rutaLog = Join-Path (Split-Path $Bacpac -Parent) 'ensayos.log'
try {
    $estado = @('OK', 'ERROR', 'AVISOS')[$codigoSalida]
    Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  {1}  {2}  servidor {3}  duración {4:hh\:mm\:ss}  errores {5}  avisos {6}' -f $inicio, $estado, (Split-Path $Bacpac -Leaf), $Servidor, $duracion, $errores.Count, $avisos.Count)
} catch { }
exit $codigoSalida
