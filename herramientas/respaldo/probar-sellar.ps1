<#
.SYNOPSIS
    Prueba las funciones puras de sellar-registros.ps1 (canonización, hash, archivo de sello,
    comparación de dos sellos, elección del base y retención) sin BD ni archivos.
.DESCRIPTION
    Extrae las funciones de sellar-registros.ps1 del árbol de sintaxis (no ejecuta el script) y
    comprueba los casos. Sale con código 1 si alguno falla. No usa Pester.
.EXAMPLE
    .\herramientas\respaldo\probar-sellar.ps1
#>
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'sellar-registros.ps1'
$errores = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$errores)
if ($errores -and $errores.Count -gt 0) { throw "sellar-registros.ps1 tiene errores de sintaxis: $($errores[0].Message) (línea $($errores[0].Extent.StartLineNumber))" }
$nombres = 'Get-OrdenColumnas', 'ConvertTo-ValorCanonico', 'ConvertTo-FilaCanonica', 'Get-Sha256Texto', 'Get-HashFila', 'Get-HashDefinicion',
           'New-SelloVacio', 'New-TablaSello', 'ConvertTo-LineasSello', 'Get-HashLineas', 'ConvertFrom-LineasSello', 'Format-ListaIds',
           'Test-TriggersSello', 'Compare-Sellos', 'Get-NombreSello', 'Get-FechaNombreSello', 'Get-SellosABorrar', 'Select-SelloBase', 'Test-NombreSelloDeBd'
foreach ($n in $nombres) {
    $def = $ast.FindAll({ param($x) $x -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $x.Name -eq $n }, $false) |
           Select-Object -First 1
    if (-not $def) { throw "No se encontró la función $n en sellar-registros.ps1." }
    . ([scriptblock]::Create($def.Extent.Text))
}

$fallos = 0
function Comprobar([string]$caso, [bool]$condicion, [string]$detalle) {
    if ($condicion) { Write-Host "OK     $caso" -ForegroundColor Green }
    else { Write-Host "FALLA  $caso -> $detalle" -ForegroundColor Red; $script:fallos++ }
}

# Las pruebas corren con la cultura del sitio (es-PE: coma decimal) para ver que no se cuela.
$culturaAnterior = [Threading.Thread]::CurrentThread.CurrentCulture
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('es-PE')

try {
# -------------------------------------------------------------------------------------------------
# 1) Valores canónicos
# -------------------------------------------------------------------------------------------------
$v = ConvertTo-ValorCanonico ([datetime]::new(2026, 11, 22, 10, 5, 7, 123))
Comprobar 'Fecha con milisegundos: ISO con 7 decimales' ($v -eq '2026-11-22T10:05:07.1230000') $v
$v1 = ConvertTo-ValorCanonico ([datetime]::new(2026, 11, 22, 10, 5, 7, 123))
$v2 = ConvertTo-ValorCanonico ([datetime]::new(2026, 11, 22, 10, 5, 7, 127))
Comprobar 'Fechas que difieren en milisegundos: distintas' ($v1 -ne $v2) "$v1 / $v2"
$v = ConvertTo-ValorCanonico ([datetime]::new(2026, 11, 22, 10, 5, 7).AddTicks(1234567))
Comprobar 'datetime2(7): conserva los ticks' ($v -eq '2026-11-22T10:05:07.1234567') $v
$v = ConvertTo-ValorCanonico ([DateTimeOffset]::new(2026, 1, 2, 3, 4, 5, [TimeSpan]::FromHours(-5)))
Comprobar 'datetimeoffset: con su desfase' ($v -eq '2026-01-02T03:04:05.0000000-05:00') $v
$v = ConvertTo-ValorCanonico ([decimal]'1234.50')
Comprobar 'Decimal invariante con cultura es-PE (punto, conserva la escala)' ($v -eq '1234.50') $v
$v = ConvertTo-ValorCanonico ([double]0.1)
Comprobar 'Double invariante (R)' ($v -eq '0.1') $v
$v = ConvertTo-ValorCanonico ([byte[]](0, 15, 171, 255))
Comprobar 'Binario en hex' ($v -eq '0x000FABFF') $v
Comprobar 'Bit: 1/0' ((ConvertTo-ValorCanonico $true) -eq '1' -and (ConvertTo-ValorCanonico $false) -eq '0') ''
Comprobar 'DBNull y $null: NULL' ($null -eq (ConvertTo-ValorCanonico ([DBNull]::Value)) -and $null -eq (ConvertTo-ValorCanonico $null)) ''
Comprobar 'Cadena vacía: no es NULL' ((ConvertTo-ValorCanonico '') -eq '') ''
Comprobar 'Entero invariante' ((ConvertTo-ValorCanonico ([int]1234567)) -eq '1234567') ''
$g = [guid]'0f8fad5b-d9cb-469f-a165-70867728950e'
Comprobar 'Guid en formato D' ((ConvertTo-ValorCanonico $g) -eq '0f8fad5b-d9cb-469f-a165-70867728950e') ''

# -------------------------------------------------------------------------------------------------
# 2) Filas canónicas y hash
# -------------------------------------------------------------------------------------------------
$cols = Get-OrdenColumnas @('Descripcion', 'accion', 'IdAuditoria', 'FechaHora')
Comprobar 'Orden de columnas por nombre sin mayúsculas' (($cols -join ',') -eq 'accion,Descripcion,FechaHora,IdAuditoria') ($cols -join ',')
$cols2 = Get-OrdenColumnas @('FechaHora', 'IdAuditoria', 'accion', 'Descripcion')
Comprobar 'Orden de columnas: no depende del orden de entrada' (($cols -join ',') -eq ($cols2 -join ',')) ($cols2 -join ',')
[string[]]$plano = Get-OrdenColumnas @('b', 'a')
Comprobar 'Orden de columnas: arreglo plano de cadenas (no un arreglo dentro de otro)' ($plano.Count -eq 2 -and $plano[0] -eq 'a' -and ($plano -join ',') -eq 'a,b') ($plano -join ',')

$hNull  = Get-HashFila @('a', 'b') @('x', [DBNull]::Value)
$hVacio = Get-HashFila @('a', 'b') @('x', '')
Comprobar 'NULL y cadena vacía dan hashes distintos' ($hNull -ne $hVacio) ''
$hTextoN = Get-HashFila @('a', 'b') @('x', 'N')
Comprobar "La cadena 'N' no se confunde con NULL" ($hTextoN -ne $hNull) ''
$h1 = Get-HashFila @('a', 'b') @('ab', 'c')
$h2 = Get-HashFila @('a', 'b') @('a', 'bc')
Comprobar 'Sin ambigüedad al mover texto entre columnas' ($h1 -ne $h2) ''
$h3 = Get-HashFila @('a', 'b') @('x;1:b', '')
$h4 = Get-HashFila @('a', 'b') @('x', ';1:b')
Comprobar 'Sin ambigüedad con separadores dentro del valor' ($h3 -ne $h4) ''
$hA = Get-HashFila @('a', 'b') @('x', 'y')
$hB = Get-HashFila @('a', 'c') @('x', 'y')
Comprobar 'El nombre de la columna entra en el hash' ($hA -ne $hB) ''
Comprobar 'Hash determinista y en hex de 64' ((Get-HashFila @('a') @('x')) -eq (Get-HashFila @('a') @('x')) -and (Get-HashFila @('a') @('x')) -match '^[0-9a-f]{64}$') ''
$hFecha1 = Get-HashFila @('f') @([datetime]::new(2026, 11, 22, 10, 5, 7, 123))
$hFecha2 = Get-HashFila @('f') @([datetime]::new(2026, 11, 22, 10, 5, 7, 124))
Comprobar 'Fila con fecha: un milisegundo cambia el hash' ($hFecha1 -ne $hFecha2) ''
$hEs = Get-HashFila @('m') @([decimal]'10.5')
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
$hInv = Get-HashFila @('m') @([decimal]'10.5')
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::GetCultureInfo('es-PE')
Comprobar 'Hash de un decimal igual en es-PE e invariante' ($hEs -eq $hInv) ''
Comprobar 'Conocido: SHA-256 de "abc"' ((Get-Sha256Texto 'abc') -eq 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad') ''
Comprobar 'Definición de trigger: CRLF y LF dan el mismo hash' ((Get-HashDefinicion "CREATE TRIGGER x`r`nAS`r`nTHROW") -eq (Get-HashDefinicion "CREATE TRIGGER x`nAS`nTHROW")) ''
Comprobar 'Definición de trigger: espacios y saltos al final o al principio no cuentan (.bacpac importado)' ((Get-HashDefinicion "`r`nCREATE TRIGGER x`r`nAS THROW`r`n`r`n") -eq (Get-HashDefinicion "CREATE TRIGGER x`nAS THROW")) ''
Comprobar 'Definición de trigger: un comentario agregado al final sí cambia el hash' ((Get-HashDefinicion "AS THROW 50000") -ne (Get-HashDefinicion "AS THROW 50000`n-- cambiado")) ''
Comprobar 'Definición de trigger: un cambio de texto cambia el hash' ((Get-HashDefinicion "AS THROW 50000") -ne (Get-HashDefinicion "AS RETURN")) ''

# -------------------------------------------------------------------------------------------------
# 3) Sellos de prueba
# -------------------------------------------------------------------------------------------------
$defUpd = "CREATE TRIGGER trg_T_NoUpdate ON T INSTEAD OF UPDATE AS THROW 50001, 'No', 1;"
$defDel = "CREATE TRIGGER trg_T_NoDelete ON T INSTEAD OF DELETE AS THROW 50002, 'No', 1;"
function Nuevo-Sello([hashtable]$Filas, [string[]]$Columnas = @('Id', 'Texto'), [switch]$SinTriggers, [datetime]$Fecha = (Get-Date '2026-11-21 23:00')) {
    $s = New-SelloVacio -Fecha $Fecha -Bd 'sgvPrueba' -Entorno 'ensayo'
    foreach ($tabla in 'AuditoriaLog', 'FirmaDigital') {
        $t = New-TablaSello -Nombre $tabla -Pk 'Id' -Columnas (Get-OrdenColumnas $Columnas)
        foreach ($id in ($Filas.Keys | Sort-Object)) {
            $vals = @($t.Columnas | ForEach-Object { if ($_ -eq 'Id') { $id } elseif ($_ -eq 'Texto') { $Filas[$id] } else { [DBNull]::Value } })
            $t.Hashes[[long]$id] = Get-HashFila $t.Columnas $vals
            $t.Filas++
            if ($null -eq $t.MaxId -or $id -gt $t.MaxId) { $t.MaxId = [long]$id }
        }
        $s.Tablas[$tabla] = $t
        if (-not $SinTriggers) {
            [void]$s.Triggers.Add([pscustomobject]@{ Tabla = $tabla; Nombre = "trg_${tabla}_NoUpdate"; Desactivado = $false; InsteadOf = $true; Eventos = 'UPDATE'; HashDefinicion = (Get-HashDefinicion $defUpd) })
            [void]$s.Triggers.Add([pscustomobject]@{ Tabla = $tabla; Nombre = "trg_${tabla}_NoDelete"; Desactivado = $false; InsteadOf = $true; Eventos = 'DELETE'; HashDefinicion = (Get-HashDefinicion $defDel) })
        }
    }
    return $s
}
$filasBase = @{ 1 = 'uno'; 2 = 'dos'; 3 = 'tres' }
$base = Nuevo-Sello $filasBase

$r = Compare-Sellos $null $base
Comprobar 'Primer sello (sin base) con triggers activos: OK' ($r.Estado -eq 'OK') ($r.Fallas -join ' | ')

$r = Compare-Sellos $base (Nuevo-Sello $filasBase)
Comprobar 'Igual: OK' ($r.Estado -eq 'OK' -and $r.Fallas.Count -eq 0) ($r.Fallas -join ' | ')

$r = Compare-Sellos $base (Nuevo-Sello @{ 1 = 'uno'; 2 = 'DOS'; 3 = 'tres' })
Comprobar 'Fila cambiada: FALLA con el id' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'CAMBIADA.*Id 2') ($r.Fallas -join ' | ')

$r = Compare-Sellos $base (Nuevo-Sello @{ 1 = 'uno'; 3 = 'tres' })
Comprobar 'Fila borrada: FALLA con el id' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'BORRADA.*Id 2') ($r.Fallas -join ' | ')

$r = Compare-Sellos $base (Nuevo-Sello @{ 1 = 'uno'; 2 = 'dos'; 3 = 'tres'; 4 = 'cuatro'; 5 = 'cinco' })
Comprobar 'Filas nuevas: OK y contadas' ($r.Estado -eq 'OK' -and $r.FilasNuevas['AuditoriaLog'] -eq 2 -and $r.Avisos.Count -eq 0) (($r.Fallas + $r.Avisos) -join ' | ')

$r = Compare-Sellos (Nuevo-Sello @{ 1 = 'uno'; 3 = 'tres' }) (Nuevo-Sello $filasBase)
Comprobar 'Fila nueva con id menor que el máximo anterior: OK con aviso' ($r.Estado -eq 'OK' -and ($r.Avisos -join ' ') -match 'id menor') (($r.Fallas + $r.Avisos) -join ' | ')

$x = Nuevo-Sello $filasBase; $x.Triggers[0].Desactivado = $true
$r = Compare-Sellos $base $x
Comprobar 'Trigger desactivado: FALLA' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'DESACTIVADO' -and ($r.Fallas -join ' ') -match 'ningún trigger activo que bloquee UPDATE') ($r.Fallas -join ' | ')
$r = Compare-Sellos $null $x
Comprobar 'Trigger desactivado ya en el primer sello: FALLA' ($r.Estado -eq 'FALLA') ($r.Fallas -join ' | ')

$x = Nuevo-Sello $filasBase; $x.Triggers[1].HashDefinicion = Get-HashDefinicion "CREATE TRIGGER trg_T_NoDelete ON T INSTEAD OF DELETE AS RETURN;"
$r = Compare-Sellos $base $x
Comprobar 'Definición de trigger cambiada: FALLA' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'definición del trigger trg_AuditoriaLog_NoDelete CAMBIÓ') ($r.Fallas -join ' | ')

$x = Nuevo-Sello $filasBase; $x.Triggers.RemoveAt(1)
$r = Compare-Sellos $base $x
Comprobar 'Trigger que desaparece: FALLA (y la tabla queda sin bloqueo de DELETE)' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'DESAPARECIÓ' -and ($r.Fallas -join ' ') -match 'bloquee DELETE') ($r.Fallas -join ' | ')

$r = Compare-Sellos $null (Nuevo-Sello $filasBase -SinTriggers)
Comprobar 'Tabla sin triggers de solo inserción: FALLA' ($r.Estado -eq 'FALLA' -and @($r.Fallas | Where-Object { $_ -match 'bloquee' }).Count -eq 4) ($r.Fallas -join ' | ')

$x = Nuevo-Sello $filasBase
[void]$x.Triggers.Add([pscustomobject]@{ Tabla = 'FirmaDigital'; Nombre = 'trg_Otro'; Desactivado = $false; InsteadOf = $false; Eventos = 'INSERT'; HashDefinicion = 'h' })
$r = Compare-Sellos $base $x
Comprobar 'Trigger nuevo (activo): OK con aviso' ($r.Estado -eq 'OK' -and ($r.Avisos -join ' ') -match 'trigger nuevo trg_Otro') (($r.Fallas + $r.Avisos) -join ' | ')

# Columna nueva (migración): las filas viejas se comparan con las columnas del base (HashesBase).
$x = Nuevo-Sello $filasBase -Columnas @('Id', 'Texto', 'Nueva')
foreach ($t in $x.Tablas.Values) {
    $t.HashesBase = New-Object 'System.Collections.Generic.SortedDictionary[long,string]'
    foreach ($id in $filasBase.Keys) { $t.HashesBase[[long]$id] = Get-HashFila @('Id', 'Texto') @($id, $filasBase[$id]) }
}
$r = Compare-Sellos $base $x
Comprobar 'Columna nueva sin cambios en las filas: OK con aviso' ($r.Estado -eq 'OK' -and ($r.Avisos -join ' ') -match 'columna\(s\) nueva\(s\) Nueva') (($r.Fallas + $r.Avisos) -join ' | ')
$x.Tablas['AuditoriaLog'].HashesBase[[long]3] = Get-HashFila @('Id', 'Texto') @(3, 'otro')
$r = Compare-Sellos $base $x
Comprobar 'Columna nueva y una fila vieja cambiada: FALLA' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'CAMBIADA.*Id 3') ($r.Fallas -join ' | ')

$r = Compare-Sellos (Nuevo-Sello $filasBase -Columnas @('Id', 'Texto', 'Vieja')) (Nuevo-Sello $filasBase)
Comprobar 'Columna que desaparece: FALLA' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'ya no existe.*Vieja') ($r.Fallas -join ' | ')

$x = Nuevo-Sello $filasBase; $x.Tablas.Remove('FirmaDigital')
$x.Triggers = New-Object System.Collections.ArrayList (, @($x.Triggers | Where-Object { $_.Tabla -ne 'FirmaDigital' }))
$r = Compare-Sellos $base $x
Comprobar 'Tabla que no se pudo sellar: FALLA' ($r.Estado -eq 'FALLA' -and ($r.Fallas -join ' ') -match 'FirmaDigital: la tabla ya no') ($r.Fallas -join ' | ')

# -------------------------------------------------------------------------------------------------
# 4) Archivo de sello: ida y vuelta, alteración
# -------------------------------------------------------------------------------------------------
$s = Nuevo-Sello $filasBase
$s.Anterior = 'sello-sgvPrueba_20261120_2300.txt.gz'; $s.AnteriorHash = ('a' * 64)
$s.Tablas['FirmaDigital'].Hashes.Clear(); $s.Tablas['FirmaDigital'].Filas = 0; $s.Tablas['FirmaDigital'].MaxId = $null
$lineas = ConvertTo-LineasSello $s
$hash = Get-HashLineas $lineas
$lineas.Add("fin`t$hash")
$leido = ConvertFrom-LineasSello $lineas.ToArray()
$r = Compare-Sellos $s $leido
Comprobar 'Ida y vuelta: mismo contenido (comparación OK), hash, anterior y tabla vacía' ($r.Estado -eq 'OK' -and $leido.HashGlobal -eq $hash -and $leido.Anterior -eq $s.Anterior -and $leido.AnteriorHash -eq $s.AnteriorHash -and $leido.Tablas['FirmaDigital'].Filas -eq 0 -and $leido.Tablas['AuditoriaLog'].MaxId -eq 3 -and $leido.Fecha -eq $s.Fecha) ($r.Fallas -join ' | ')
Comprobar 'Ida y vuelta: triggers' ($leido.Triggers.Count -eq 4 -and $leido.Triggers[0].InsteadOf -and -not $leido.Triggers[0].Desactivado -and $leido.Triggers[0].Eventos -eq 'UPDATE') ''

$alterado = $lineas.ToArray()
$i = [Array]::FindIndex($alterado, [Predicate[string]] { param($l) $l.StartsWith("fila`tAuditoriaLog`t2`t") })
$alterado[$i] = "fila`tAuditoriaLog`t2`t" + ('0' * 64)
$error1 = $null; try { [void](ConvertFrom-LineasSello $alterado) } catch { $error1 = $_.Exception.Message }
Comprobar 'Sello con una línea alterada: hash global no coincide' ($error1 -match 'hash global no coincide') "$error1"
$error2 = $null; try { [void](ConvertFrom-LineasSello @($lineas.ToArray() | Select-Object -SkipLast 1)) } catch { $error2 = $_.Exception.Message }
Comprobar 'Sello cortado: incompleto' ($error2 -match 'incompleto') "$error2"
$error3 = $null; try { [void](ConvertFrom-LineasSello @('otra cosa', "fin`t" + ('0' * 64))) } catch { $error3 = $_.Exception.Message }
Comprobar 'Archivo que no es sello: rechazado' ($error3 -match 'no es un sello') "$error3"

# -------------------------------------------------------------------------------------------------
# 5) Elección del base y retención
# -------------------------------------------------------------------------------------------------
function C($n, $f, $e, $err = $null, $bd = 'sgvPrueba', $hash = $null, $ant = $null, $antHash = $null) {
    [pscustomobject]@{ Nombre = $n; Fecha = (Get-Date $f); Estado = $e; Error = $err; Bd = $bd; HashGlobal = $hash; Anterior = $ant; AnteriorHash = $antHash; Sello = $null }
}
$e = Select-SelloBase @((C 's1' '2026-11-19' 'OK'), (C 's2' '2026-11-20' 'OK'), (C 's3' '2026-11-21' 'FALLA')) -Bd 'sgvPrueba'
Comprobar 'Base: el más reciente OK (una FALLA no se cura sola)' ($e.Base.Nombre -eq 's2' -and $e.Fallas.Count -eq 0) ($e | Out-String)
$e = Select-SelloBase @((C 's1' '2026-11-19' 'OK'), (C 's2' '2026-11-20' $null 'hash global no coincide')) -Bd 'sgvPrueba'
Comprobar 'Base: un sello dañado más reciente es FALLA y se usa el anterior válido' ($e.Base.Nombre -eq 's1' -and $e.Fallas.Count -eq 1 -and $e.Fallas[0] -match 's2') ($e | Out-String)
$e = Select-SelloBase @((C 's1' '2026-11-19' 'FALLA'), (C 's2' '2026-11-20' 'FALLA'), (C 's3' '2026-11-21' 'FALLA')) -Bd 'sgvPrueba'
Comprobar 'Base: sin ninguno OK, el MÁS ANTIGUO válido de la cadena, con aviso' ($e.Base.Nombre -eq 's1' -and $e.Avisos.Count -eq 1 -and $e.Avisos[0] -match 'más antiguo') ($e | Out-String)
$e = Select-SelloBase @((C 's0' '2026-11-18' $null 'dañado'), (C 's1' '2026-11-19' 'FALLA'), (C 's2' '2026-11-20' 'FALLA')) -Bd 'sgvPrueba'
Comprobar 'Base: sin OK, el más antiguo VÁLIDO (no uno dañado), y el dañado es FALLA' ($e.Base.Nombre -eq 's1' -and $e.Fallas.Count -eq 1 -and $e.Fallas[0] -match 's0') ($e | Out-String)
$e = Select-SelloBase @() -Bd 'sgvPrueba'
Comprobar 'Base: sin sellos, ninguna' ($null -eq $e.Base -and $e.Fallas.Count -eq 0) ''

# FALLAS seguidas (BD sin triggers): fila cambiada entre el 2 y el 3 -> el 4 también la reporta.
$f1 = Nuevo-Sello $filasBase -SinTriggers
$f2 = Nuevo-Sello $filasBase -SinTriggers
$f3 = Nuevo-Sello @{ 1 = 'uno'; 2 = 'ALTERADO'; 3 = 'tres' } -SinTriggers
$cands = @()
$i = 0
foreach ($s in $f1, $f2, $f3) {
    $i++
    $s.Estado = (Compare-Sellos $null $s).Estado
    $c = C "f$i" "2026-11-1$i" $s.Estado
    $c.Sello = $s
    $cands += $c
}
Comprobar 'FALLAS seguidas: los tres sellos son FALLA (sin triggers)' (@($cands | Where-Object { $_.Estado -eq 'FALLA' }).Count -eq 3) ''
$e = Select-SelloBase $cands -Bd 'sgvPrueba'
$r = Compare-Sellos $e.Base.Sello (Nuevo-Sello @{ 1 = 'uno'; 2 = 'ALTERADO'; 3 = 'tres' } -SinTriggers)
Comprobar 'FALLAS seguidas: el cuarto sello sigue reportando la fila 2 CAMBIADA' ($e.Base.Nombre -eq 'f1' -and ($r.Fallas -join ' ') -match 'CAMBIADA.*Id 2') (($r.Fallas -join ' | ') + ' base=' + $e.Base.Nombre)
$hoyR = Get-Date '2026-11-22 23:00'
$nombresR = @(0..39 | ForEach-Object { Get-NombreSello -Bd 'sgvPrueba' -Fecha ($hoyR.AddDays(-$_)) })
$masAntiguoNoMensual = Get-NombreSello -Bd 'sgvPrueba' -Fecha ($hoyR.AddDays(-38))   # 15-oct: no es el primero del mes
$sinProteger = @(Get-SellosABorrar -Nombres $nombresR -Hoy $hoyR)
$b = @(Get-SellosABorrar -Nombres $nombresR -Hoy $hoyR -Proteger @($masAntiguoNoMensual))
Comprobar 'Retención: protege el sello más antiguo de la cadena cuando es el base' (($sinProteger -contains $masAntiguoNoMensual) -and ($b -notcontains $masAntiguoNoMensual)) ($b -join ', ')

# Cadena y BD
$e = Select-SelloBase @((C 's1' '2026-11-19' 'OK'), (C 's2' '2026-11-20' 'OK' -bd 'sgvOtra')) -Bd 'sgvPrueba'
Comprobar 'Base: un sello de otra BD no sirve de base (aviso)' ($e.Base.Nombre -eq 's1' -and $e.Fallas.Count -eq 0 -and ($e.Avisos -join ' ') -match "sgvOtra") ($e | Out-String)
$e = Select-SelloBase @((C 's1' '2026-11-19' 'OK' -bd 'sgvOtra')) -Bd 'sgvPrueba'
Comprobar 'Base: solo sellos de otra BD -> ninguna base' ($null -eq $e.Base -and $e.Avisos.Count -eq 1) ($e | Out-String)
$h1 = 'a' * 64
$e = Select-SelloBase @((C 's1' '2026-11-19' 'OK' -hash $h1), (C 's2' '2026-11-20' 'OK' -hash ('b' * 64) -ant 's1' -antHash $h1)) -Bd 'sgvPrueba'
Comprobar 'Cadena: el anterior del base tiene el hash declarado -> sin aviso' ($e.Base.Nombre -eq 's2' -and $e.Avisos.Count -eq 0) ($e | Out-String)
$e = Select-SelloBase @((C 's1' '2026-11-19' 'OK' -hash ('c' * 64)), (C 's2' '2026-11-20' 'OK' -hash ('b' * 64) -ant 's1' -antHash $h1)) -Bd 'sgvPrueba'
Comprobar 'Cadena rota: el anterior del base cambió -> AVISO, no FALLA' ($e.Base.Nombre -eq 's2' -and $e.Fallas.Count -eq 0 -and ($e.Avisos -join ' ') -match 'Cadena rota') ($e | Out-String)
$e = Select-SelloBase @((C 's2' '2026-11-20' 'OK' -hash ('b' * 64) -ant 's1' -antHash $h1)) -Bd 'sgvPrueba'
Comprobar 'Cadena: el anterior ya borrado por la retención -> sin aviso' ($e.Base.Nombre -eq 's2' -and $e.Avisos.Count -eq 0) ($e | Out-String)
Comprobar 'Nombre de sello de exactamente esta BD' ((Test-NombreSelloDeBd 'sello-sgvEnsayo_x_20261122_2300.txt.gz' 'sgvEnsayo_x') -and -not (Test-NombreSelloDeBd 'sello-sgvEnsayo_x_y_20261122_2300.txt.gz' 'sgvEnsayo_x') -and -not (Test-NombreSelloDeBd 'sello-sgvEnsayo_x_20261122_2300.txt.gz.parcial' 'sgvEnsayo_x')) ''

$n = Get-NombreSello -Bd 'sgvActualizada' -Fecha (Get-Date '2026-11-22 23:05')
Comprobar 'Nombre del sello' ($n -eq 'sello-sgvActualizada_20261122_2305.txt.gz') $n
Comprobar 'Fecha desde el nombre' ((Get-FechaNombreSello $n) -eq (Get-Date '2026-11-22 23:05')) ''
Comprobar 'Nombre que no es sello: sin fecha' ($null -eq (Get-FechaNombreSello 'sgvActualizada_20261122_2305.bacpac')) ''

$hoy = Get-Date '2026-11-22 23:00'
$nombres = @(0..39 | ForEach-Object { Get-NombreSello -Bd 'sgvActualizada' -Fecha ($hoy.AddDays(-$_)) }) + 'notas.txt'
$b = @(Get-SellosABorrar -Nombres $nombres -Hoy $hoy)
# 40 diarios del 14-oct al 22-nov: quedan 30 (24-oct en adelante) + el primero de octubre presente (14-oct).
Comprobar 'Retención: 40 sellos diarios -> borra 9, conserva el primero del mes y no toca otros archivos' ($b.Count -eq 9 -and $b -notcontains 'sello-sgvActualizada_20261014_2300.txt.gz' -and $b -notcontains 'notas.txt') ($b -join ', ')
$p = Get-NombreSello -Bd 'sgvActualizada' -Fecha ($hoy.AddDays(-35))
$b = @(Get-SellosABorrar -Nombres $nombres -Hoy $hoy -Proteger @($p))
Comprobar 'Retención: nunca borra el sello base protegido' ($b.Count -eq 8 -and $b -notcontains $p) ($b -join ', ')
Comprobar 'Retención: 30 o menos, nada' (@(Get-SellosABorrar -Nombres ($nombres | Select-Object -First 30) -Hoy $hoy).Count -eq 0) ''

} finally {
    [Threading.Thread]::CurrentThread.CurrentCulture = $culturaAnterior
}

""
if ($fallos -gt 0) { Write-Host "$fallos caso(s) FALLARON." -ForegroundColor Red; exit 1 }
Write-Host "Todos los casos OK." -ForegroundColor Green
exit 0
