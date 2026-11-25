<#
.SYNOPSIS
    Aviso de la tarea "SGV - Respaldo diario" (E6-14, M-123): si un paso falló, manda UN correo con
    SGV_SCRIPT_AVISO (herramientas\comun\enviar-aviso.ps1).

.DESCRIPTION
    programar-respaldo.ps1 lo llama al final de la tarea con los códigos de salida de cada paso:
      -CodigoBd        respaldar-bd.ps1        (0 bien; otro = falló la exportación o su copia)
      -CodigoSello     sellar-registros.ps1    (0 OK; 1 FALLA de integridad; 2 no se pudo sellar)
      -CodigoArchivos  respaldar-archivos.ps1  (0 bien; 1 error del espejo; 2 la verificación de los
                                                PDF firmados encontró faltantes o distintos)
    La FALLA de integridad del sello (código 1) ya la avisa sellar-registros.ps1 con su detalle: aquí
    no se repite, para que una FALLA mande exactamente un correo. Si además falló otro paso, este
    correo lo nombra a él.

    Sin -ScriptAviso ni SGV_SCRIPT_AVISO no envía nada (lo dice en la salida). Un aviso que no se pudo
    enviar se muestra y no cambia el resultado de la tarea: este script siempre sale con 0.

.EXAMPLE
    .\herramientas\respaldo\avisar-tarea.ps1 -CodigoBd 1 -CodigoSello 0 -CodigoArchivos 0 -Log D:\RespaldosSGV\tarea-ultima-ejecucion.log
#>
param(
    [int]$CodigoBd = 0,
    [int]$CodigoSello = 0,
    [int]$CodigoArchivos = 0,

    # Registro de la ejecución (tarea-ultima-ejecucion.log): sus últimas líneas van en el correo.
    [string]$Log,

    [string]$ScriptAviso = $env:SGV_SCRIPT_AVISO
)

$ErrorActionPreference = 'Stop'

# Pasos que fallaron y que nadie avisó todavía. Función pura: la prueban AvisoCorreoTests.
function Get-FallasTarea([int]$Bd, [int]$Sello, [int]$Archivos) {
    $f = @()
    if ($Bd -ne 0) { $f += "Respaldo de la BD (respaldar-bd.ps1): código $Bd." }
    if ($Sello -ne 0 -and $Sello -ne 1) { $f += "Sello de firmas y auditoría (sellar-registros.ps1): código $Sello, no se pudo sellar." }
    if ($Archivos -eq 2) { $f += "Verificación de los PDF firmados (verificar-pdf-firmados.ps1): hay faltantes o distintos." }
    elseif ($Archivos -ne 0) { $f += "Espejo de archivos por FTP (respaldar-archivos.ps1): código $Archivos." }
    return ,$f
}

$fallas = Get-FallasTarea $CodigoBd $CodigoSello $CodigoArchivos
if ($fallas.Count -eq 0) {
    if ($CodigoSello -eq 1) { "Aviso de la tarea: solo falló el sello, que ya mandó su propio aviso." }
    else { "Aviso de la tarea: todos los pasos terminaron bien; no se envía nada." }
    exit 0
}
if ([string]::IsNullOrWhiteSpace($ScriptAviso)) {
    "Aviso de la tarea: falló un paso, pero no hay -ScriptAviso ni SGV_SCRIPT_AVISO; el aviso queda en esta salida."
    exit 0
}

$cuerpo = "La tarea 'SGV - Respaldo diario' del equipo $env:COMPUTERNAME terminó con pasos fallidos:`r`n`r`n" +
          (($fallas | ForEach-Object { "- $_" }) -join "`r`n") +
          $(if ($CodigoSello -eq 1) { "`r`n- Sello de firmas y auditoría: FALLA de integridad (avisada en otro correo)." } else { '' }) +
          "`r`n`r`nVer herramientas\respaldo\README.md y docs\PROCEDIMIENTO_RESPALDO.md."
if (-not [string]::IsNullOrWhiteSpace($Log) -and (Test-Path -LiteralPath $Log -PathType Leaf)) {
    $ultimas = @(Get-Content -LiteralPath $Log -Tail 60 -ErrorAction SilentlyContinue)
    if ($ultimas.Count) { $cuerpo += "`r`n`r`nÚltimas líneas de ${Log}:`r`n`r`n" + ($ultimas -join "`r`n") }
}
try {
    & $ScriptAviso -Asunto "SGV: FALLA en el respaldo diario ($env:COMPUTERNAME)" -Cuerpo $cuerpo
} catch {
    Write-Host "No se pudo enviar el aviso de la tarea con ${ScriptAviso}: $($_.Exception.Message)" -ForegroundColor Red
}
exit 0
