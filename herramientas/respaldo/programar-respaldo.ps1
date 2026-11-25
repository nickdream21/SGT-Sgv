<#
.SYNOPSIS
    Registra (o quita) la tarea de Windows "SGV - Respaldo diario": respaldar-bd.ps1,
    sellar-registros.ps1 (sello de firmas y auditoría, E4-11) y respaldar-archivos.ps1 todos los días
    a las 23:00.

.DESCRIPTION
    La tarea corre con el usuario de Windows actual y SOLO CUANDO ESE USUARIO TIENE LA SESIÓN INICIADA
    (puede estar bloqueada). Motivo de la elección:
      - "Ejecutar aunque el usuario no haya iniciado sesión" con contraseña obligaría a guardar la
        contraseña de Windows en el Programador de tareas (no se quiere).
      - Sin guardar contraseña (S4U) la tarea no puede descifrar la credencial FTP guardada con
        DPAPI en privado\ftp-respaldo.xml (respaldar-archivos.ps1 fallaría) y no tiene credenciales
        de red.
    Para no perder respaldos si la PC estaba apagada o sin sesión a las 23:00, la tarea tiene
    "ejecutar lo antes posible si se perdió una ejecución": corre al iniciar sesión la próxima vez.

    Aviso por correo (E6-14): si un paso falla, la tarea llama a avisar-tarea.ps1, que manda un correo
    con el script de la variable de entorno de usuario SGV_SCRIPT_AVISO (herramientas\comun\enviar-aviso.ps1;
    la FALLA del sello la avisa el propio sello, una sola vez). La variable se lee al correr la tarea:
    cambiarla no obliga a registrarla de nuevo. Sin ella, el aviso es solo el resultado de la tarea.

    La salida de cada ejecución queda en <Carpeta>\tarea-ultima-ejecucion.log (se reemplaza cada vez);
    el historial está en <Carpeta>\bd\respaldos.log y <Carpeta>\archivos\respaldos.log.

    Antes de registrarla: guardar la credencial FTP (respaldar-archivos.ps1 -GuardarCredencial) con
    este mismo usuario y probar a mano los dos scripts con la misma -Carpeta.

.EXAMPLE
    .\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV -SoloListar
    .\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV
    .\herramientas\respaldo\programar-respaldo.ps1 -Carpeta D:\RespaldosSGV -Hora 22:30
    .\herramientas\respaldo\programar-respaldo.ps1 -Quitar
#>
param(
    [string]$Carpeta,
    [string]$Hora = '23:00',
    [switch]$SoloListar,
    [switch]$Quitar
)

$ErrorActionPreference = 'Stop'
$nombreTarea = 'SGV - Respaldo diario'

if ($Quitar) {
    $t = Get-ScheduledTask -TaskName $nombreTarea -ErrorAction SilentlyContinue
    if (-not $t) { "No existe la tarea '$nombreTarea'. Nada que quitar."; exit 0 }
    if ($SoloListar) { "(-SoloListar) Se quitaría la tarea '$nombreTarea'."; exit 0 }
    Unregister-ScheduledTask -TaskName $nombreTarea -Confirm:$false
    "Tarea '$nombreTarea' quitada."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Carpeta)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
if ([string]::IsNullOrWhiteSpace($Carpeta)) {
    throw "Indique -Carpeta <ruta> o defina la variable de entorno SGV_CARPETA_RESPALDO."
}
if (-not [IO.Path]::IsPathRooted($Carpeta)) { throw "Use una ruta completa para -Carpeta (p. ej. D:\RespaldosSGV): la tarea no corre en la carpeta actual." }
$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
$carpetaCompleta = [IO.Path]::GetFullPath($Carpeta).TrimEnd('\')
if (($carpetaCompleta + '\').StartsWith($raizRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta '$carpetaCompleta' está dentro del repositorio. Use una carpeta fuera del repo (el respaldo contiene datos reales)."
}
$horaInicio = [datetime]::MinValue
if (-not [datetime]::TryParseExact($Hora, 'HH:mm', [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::None, [ref]$horaInicio)) {
    throw "Hora no válida: '$Hora' (formato HH:mm, p. ej. 23:00)."
}

$scriptBd       = Join-Path $PSScriptRoot 'respaldar-bd.ps1'
$scriptArchivos = Join-Path $PSScriptRoot 'respaldar-archivos.ps1'
$scriptSello    = Join-Path $PSScriptRoot 'sellar-registros.ps1'
$scriptAvisar   = Join-Path $PSScriptRoot 'avisar-tarea.ps1'
foreach ($s in @($scriptBd, $scriptSello, $scriptArchivos, $scriptAvisar)) { if (-not (Test-Path $s)) { throw "No se encontró $s." } }

# sqlpackage es una herramienta global de dotnet: se asegura su carpeta en el PATH de la tarea.
$sqlpackage = (Get-Command sqlpackage -ErrorAction SilentlyContinue).Source
$dirSqlpackage = if ($sqlpackage) { Split-Path $sqlpackage -Parent } else { Join-Path $env:USERPROFILE '.dotnet\tools' }

function ConvertTo-Literal([string]$s) { "'" + $s.Replace("'", "''") + "'" }
$log = [IO.Path]::Combine($carpetaCompleta, 'tarea-ultima-ejecucion.log')   # la unidad puede no existir aún
$comando = @(
    "`$env:Path += ';' + $(ConvertTo-Literal $dirSqlpackage)"
    "New-Item -ItemType Directory -Force $(ConvertTo-Literal $carpetaCompleta) | Out-Null"
    "Start-Transcript -Path $(ConvertTo-Literal $log) -Force | Out-Null"
    # try/catch: si el respaldo de la BD lanza un error, igual se intenta el de archivos.
    "try { & $(ConvertTo-Literal $scriptBd) -Carpeta $(ConvertTo-Literal $carpetaCompleta); `$cBd = `$LASTEXITCODE } catch { `$_; `$cBd = 1 }"
    # E4-11: sello de FirmaDigital/AuditoriaLog/Auditoria. No depende del .bacpac (corre aunque la
    # exportación haya fallado) y su FALLA no toca el respaldo, que ya quedó guardado.
    "try { & $(ConvertTo-Literal $scriptSello) -Carpeta $(ConvertTo-Literal $carpetaCompleta); `$cSe = `$LASTEXITCODE } catch { `$_; `$cSe = 2 }"
    "try { & $(ConvertTo-Literal $scriptArchivos) -Carpeta $(ConvertTo-Literal $carpetaCompleta); `$cAr = `$LASTEXITCODE } catch { `$_; `$cAr = 1 }"
    "Stop-Transcript | Out-Null"
    # E6-14: un correo si falló un paso (SGV_SCRIPT_AVISO); su salida se agrega al registro. Nunca cambia el resultado.
    "try { `$sAv = & $(ConvertTo-Literal $scriptAvisar) -CodigoBd `$cBd -CodigoSello `$cSe -CodigoArchivos `$cAr -Log $(ConvertTo-Literal $log) *>&1 | Out-String; Add-Content -LiteralPath $(ConvertTo-Literal $log) -Value `$sAv -Encoding UTF8 } catch { }"
    # Resultado de la tarea: el primer paso que falló (sello: 11 = FALLA de integridad, 12 = no se pudo sellar).
    "if (`$cBd -ne 0) { exit `$cBd }; if (`$cSe -ne 0) { exit (10 + `$cSe) }; exit `$cAr"
) -join '; '
$argumentos = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command "' + $comando.Replace('"', '\"') + '"'

$usuario = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$accion    = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $argumentos -WorkingDirectory $PSScriptRoot
$disparo   = New-ScheduledTaskTrigger -Daily -At $horaInicio
$principal = New-ScheduledTaskPrincipal -UserId $usuario -LogonType Interactive -RunLevel Limited
$ajustes   = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
                 -MultipleInstances IgnoreNew -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$tarea     = New-ScheduledTask -Action $accion -Trigger $disparo -Principal $principal -Settings $ajustes `
                 -Description "SGV: respaldo diario de la BD (.bacpac), sello de firmas y auditoría y espejo de archivos por FTP en $carpetaCompleta. Creada por herramientas\respaldo\programar-respaldo.ps1."

"Tarea      : $nombreTarea"
"Cuándo     : todos los días a las $($horaInicio.ToString('HH:mm')); si se perdió (PC apagada o sin sesión), apenas sea posible"
"Usuario    : $usuario, solo con la sesión iniciada (no guarda la contraseña de Windows)"
"Límite     : 2 horas; si ya está corriendo, no se inicia otra"
"Carpeta    : $carpetaCompleta"
"Registro   : $log"
$avisoUsuario = [Environment]::GetEnvironmentVariable('SGV_SCRIPT_AVISO', 'User')
"Aviso      : $(if ($avisoUsuario) { "SGV_SCRIPT_AVISO = $avisoUsuario" } else { 'SGV_SCRIPT_AVISO sin definir: una FALLA no manda correo (ver herramientas\respaldo\README.md)' })"
"Programa   : powershell.exe"
"Argumentos : $argumentos"
"Directorio : $PSScriptRoot"

if ($SoloListar) {
    ""
    "(-SoloListar: no se registró nada)"
    exit 0
}

if (Get-ScheduledTask -TaskName $nombreTarea -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $nombreTarea -Confirm:$false
    "Se reemplaza la tarea existente."
}
Register-ScheduledTask -TaskName $nombreTarea -InputObject $tarea | Out-Null
""
"Tarea registrada. Para probarla ahora: Start-ScheduledTask -TaskName '$nombreTarea'"
"Ver su último resultado: Get-ScheduledTaskInfo -TaskName '$nombreTarea' (LastTaskResult 0 = bien)"
