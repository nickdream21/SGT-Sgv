<#
.SYNOPSIS
    Prueba la función de retención de respaldar-bd.ps1 (Get-RespaldosABorrar) con fechas inventadas.
.DESCRIPTION
    Extrae la función de respaldar-bd.ps1 sin ejecutar el script (no toca la BD ni archivos) y
    comprueba varios escenarios. Sale con código 1 si alguno falla. No usa Pester.
.EXAMPLE
    .\herramientas\respaldo\probar-retencion.ps1
#>
$ErrorActionPreference = 'Stop'

$script = Join-Path $PSScriptRoot 'respaldar-bd.ps1'
$errores = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($script, [ref]$null, [ref]$errores)
if ($errores -and $errores.Count -gt 0) { throw "respaldar-bd.ps1 tiene errores de sintaxis: $($errores[0].Message)" }
$def = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Get-RespaldosABorrar' }, $true) |
       Select-Object -First 1
if (-not $def) { throw "No se encontró la función Get-RespaldosABorrar en respaldar-bd.ps1." }
. ([scriptblock]::Create($def.Extent.Text))

$fallos = 0
function Comprobar([string]$caso, [bool]$condicion, [string]$detalle) {
    if ($condicion) { Write-Host "OK     $caso" -ForegroundColor Green }
    else { Write-Host "FALLA  $caso -> $detalle" -ForegroundColor Red; $script:fallos++ }
}
function Nombre([datetime]$f) { 'sgvActualizada_{0}.bacpac' -f $f.ToString('yyyyMMdd_HHmm') }

$hoy = Get-Date '2026-11-19 03:00'

# 1) Menos de 30: no se borra nada.
$n1 = @(0..9 | ForEach-Object { Nombre ($hoy.AddDays(-$_)) })
$b1 = @(Get-RespaldosABorrar -Nombres $n1 -Hoy $hoy)
Comprobar 'Menos de 30 respaldos: no borra nada' ($b1.Count -eq 0) "borraría $($b1.Count)"

# 1b) Exactamente 30: no se borra nada.
$n1b = @(0..29 | ForEach-Object { Nombre ($hoy.AddDays(-$_)) })
$b1b = @(Get-RespaldosABorrar -Nombres $n1b -Hoy $hoy)
Comprobar 'Exactamente 30: no borra nada' ($b1b.Count -eq 0) "borraría $($b1b.Count)"

# 2) Más de 30 dentro de un mismo mes: 40 respaldos de noviembre, dos por día (cada 12 h).
#    Se quedan los 30 más recientes y el primero del mes; se borran los otros 9.
$inicioMes = Get-Date '2026-11-01 00:30'
$n2 = @(0..39 | ForEach-Object { Nombre ($inicioMes.AddHours(12 * $_)) })
$hoy2 = $inicioMes.AddHours(12 * 39).AddHours(1)
$b2 = @(Get-RespaldosABorrar -Nombres $n2 -Hoy $hoy2)
$esperado2 = @($n2[1..9])
Comprobar 'Más de 30 en un mes: borra 9' ($b2.Count -eq 9) "borraría $($b2.Count)"
Comprobar 'Más de 30 en un mes: conserva el primero del mes' (-not ($b2 -contains $n2[0])) "borraría $($n2[0])"
Comprobar 'Más de 30 en un mes: borra justo los 9 intermedios' ((@(Compare-Object $b2 $esperado2).Count) -eq 0) ($b2 -join ', ')

# 3) 14 meses de historia, un respaldo diario (hasta hoy).
$desde = Get-Date '2025-09-19 03:00'
$dias = [int]($hoy - $desde).TotalDays
$n3 = @(0..$dias | ForEach-Object { Nombre ($desde.AddDays($_)) })
$b3 = @(Get-RespaldosABorrar -Nombres $n3 -Hoy $hoy)
$quedan = @($n3 | Where-Object { $b3 -notcontains $_ })
$recientes = @($n3 | Select-Object -Last 30)
$faltanRecientes = @($recientes | Where-Object { $quedan -notcontains $_ })
Comprobar '14 meses: conserva los 30 más recientes' ($faltanRecientes.Count -eq 0) ($faltanRecientes -join ', ')

# Mensuales esperados fuera de los 30 recientes: el día 1 de cada mes de 2025-12 a 2026-10 (límite =
# hoy - 12 meses = 2025-11-19; el 2025-11-01 queda antes del límite y se borra). El 2026-11-01 ya está
# entre los 30 recientes.
$mensualesEsperados = @()
$m = Get-Date '2025-12-01 03:00'
while ($m -lt (Get-Date '2026-11-01')) { $mensualesEsperados += Nombre $m; $m = $m.AddMonths(1) }
$faltanMensuales = @($mensualesEsperados | Where-Object { $quedan -notcontains $_ })
Comprobar '14 meses: conserva el día 1 de cada mes dentro de 12 meses' ($faltanMensuales.Count -eq 0) ($faltanMensuales -join ', ')

$extra = @($quedan | Where-Object { ($recientes -notcontains $_) -and ($mensualesEsperados -notcontains $_) })
Comprobar '14 meses: no conserva nada más' ($extra.Count -eq 0) ($extra -join ', ')
Comprobar '14 meses: borra los meses de más de 12 meses (sep/oct/nov 2025)' (
    ($b3 -contains (Nombre (Get-Date '2025-09-19 03:00'))) -and
    ($b3 -contains (Nombre (Get-Date '2025-10-01 03:00'))) -and
    ($b3 -contains (Nombre (Get-Date '2025-11-01 03:00')))) 'algún respaldo antiguo se conservaría'
Comprobar '14 meses: total conservado = 30 + 11 mensuales' ($quedan.Count -eq 41) "quedan $($quedan.Count)"

# 4) Nombres que no siguen el patrón (parciales, otros archivos) nunca se borran.
$n4 = @($n3) + 'sgvActualizada_20250101_0000.parcial.bacpac' + 'otro.bacpac' + 'respaldos.log'
$b4 = @(Get-RespaldosABorrar -Nombres $n4 -Hoy $hoy)
Comprobar 'Ignora nombres fuera del patrón' (($b4 -notcontains 'otro.bacpac') -and ($b4 -notcontains 'respaldos.log') -and ($b4 -notcontains 'sgvActualizada_20250101_0000.parcial.bacpac')) ($b4 -join ', ')

# 5) El orden de entrada no importa.
$b5 = @(Get-RespaldosABorrar -Nombres ($n3 | Sort-Object { Get-Random }) -Hoy $hoy)
Comprobar 'El orden de entrada no cambia el resultado' ((@(Compare-Object $b3 $b5).Count) -eq 0) 'resultado distinto'

""
if ($fallos -gt 0) { Write-Host "$fallos prueba(s) fallaron." -ForegroundColor Red; exit 1 }
Write-Host "Todas las pruebas de retención pasaron." -ForegroundColor Green
exit 0
