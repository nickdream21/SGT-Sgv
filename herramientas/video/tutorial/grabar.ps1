<#
.SYNOPSIS
  Graba el monitor con ffmpeg (ddagrab) mientras Claude opera Edge, para armar después un tutorial.

.EXAMPLE
  .\grabar.ps1 -Iniciar -Nombre registrar-despacho -SoloEdge
  .\grabar.ps1 -Calibracion '{"screenX":0,"screenY":0,"outerWidth":1536,...,"marcoAncho":1568,"marcoAlto":750}'
  .\grabar.ps1 -Detener
  .\grabar.ps1 -Estado

  La toma queda en docs/DOCUMENTACION/capacitacion/tomas/<aaaaMMdd_HHmmss>_<nombre>.mkv con su .json al lado
  (hora de inicio y fin, ventana de Edge, calibración). Ver README.md de esta carpeta.
#>
param(
    [switch]$Iniciar,
    [switch]$Detener,
    [switch]$Estado,
    [string]$Nombre = 'toma',
    [string]$Calibracion,
    [int]$Fps = 30,
    [string]$Codificador,      # forzar uno: h264_nvenc, h264_qsv, h264_amf o libx264
    [switch]$SoloEdge,         # graba solo el rectángulo de la ventana de Edge (no el resto del escritorio)
    [string]$Guardian          # uso interno: proceso oculto que lanza y detiene ffmpeg
)

$ErrorActionPreference = 'Stop'
$raiz = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$dirTomas = Join-Path $raiz 'docs\DOCUMENTACION\capacitacion\tomas'
$archivoEstado = Join-Path $dirTomas '.grabacion-activa.json'
$utf8 = New-Object System.Text.UTF8Encoding($false)

function Get-Ahora {
    $l = Get-Date
    [pscustomobject]@{
        local = $l.ToString('yyyy-MM-ddTHH:mm:ss.fffzzz')
        utc   = $l.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
    }
}
# El guardián y este script leen y escriben el mismo JSON: lectura y escritura con reintentos, y la escritura
# es atómica (archivo temporal + File.Replace), así nunca se lee un archivo a medias.
function Read-Json([string]$ruta) {
    for ($i = 1; ; $i++) {
        try { return ([System.IO.File]::ReadAllText($ruta, $utf8) | ConvertFrom-Json) }
        catch { if ($i -ge 20) { throw }; Start-Sleep -Milliseconds 100 }
    }
}
function Write-Json([string]$ruta, $obj) {
    $tmp = "$ruta.$PID.tmp"
    [System.IO.File]::WriteAllText($tmp, ($obj | ConvertTo-Json -Depth 6), $utf8)
    for ($i = 1; ; $i++) {
        try {
            if ([System.IO.File]::Exists($ruta)) { [System.IO.File]::Replace($tmp, $ruta, [NullString]::Value) }
            else { [System.IO.File]::Move($tmp, $ruta) }
            return
        } catch {
            if ($i -ge 20) { Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue; throw }
            Start-Sleep -Milliseconds 100
        }
    }
}
function Set-Propiedad($obj, [string]$nombre, $valor) {
    if ($obj.PSObject.Properties[$nombre]) { $obj.$nombre = $valor }
    else { $obj | Add-Member -NotePropertyName $nombre -NotePropertyValue $valor }
}
function Test-Vivo([int]$id) {
    if (-not $id) { return $false }
    return [bool](Get-Process -Id $id -ErrorAction SilentlyContinue)
}
function Stop-FfmpegHuerfano([string]$json) {
    # Si el guardián murió, ffmpeg puede seguir grabando: se detiene (solo si ese PID sigue siendo ffmpeg).
    if (-not $json -or -not (Test-Path -LiteralPath $json)) { return $false }
    $t = Read-Json $json
    if (-not $t.pidFfmpeg) { return $false }
    $p = Get-Process -Id $t.pidFfmpeg -ErrorAction SilentlyContinue
    if (-not $p -or $p.ProcessName -ne 'ffmpeg') { return $false }
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    return $true
}

if (-not ('SgvVentana' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class SgvVentana {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    public static RECT Rect(IntPtr h) {
        RECT r;
        // DWMWA_EXTENDED_FRAME_BOUNDS (9): sin los bordes invisibles de Windows 10/11.
        if (DwmGetWindowAttribute(h, 9, out r, Marshal.SizeOf(typeof(RECT))) != 0) GetWindowRect(h, out r);
        return r;
    }
}
'@
}

function Get-VentanaEdge {
    [void][SgvVentana]::SetProcessDPIAware()
    $h = [SgvVentana]::GetForegroundWindow()
    $procId = [uint32]0
    [void][SgvVentana]::GetWindowThreadProcessId($h, [ref]$procId)
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    $enPrimerPlano = $true
    if (-not $proc -or $proc.ProcessName -ne 'msedge') {
        $enPrimerPlano = $false
        $proc = Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
        if (-not $proc) { return $null }
        $h = $proc.MainWindowHandle
    }
    $r = [SgvVentana]::Rect($h)
    [pscustomobject]@{
        x = $r.Left; y = $r.Top; ancho = $r.Right - $r.Left; alto = $r.Bottom - $r.Top
        maximizada = [SgvVentana]::IsZoomed($h); minimizada = [SgvVentana]::IsIconic($h)
        enPrimerPlano = $enPrimerPlano
    }
}

function Get-Pantalla {
    [void][SgvVentana]::SetProcessDPIAware()
    Add-Type -AssemblyName System.Windows.Forms
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    [pscustomobject]@{ ancho = $b.Width; alto = $b.Height }
}

function Select-Codificador {
    if ($Codificador) { return $Codificador }
    foreach ($c in 'h264_nvenc', 'h264_qsv', 'h264_amf') {
        $null = & ffmpeg -hide_banner -loglevel quiet -f lavfi -i 'color=c=black:s=640x360:r=30' -frames:v 2 -c:v $c -f null - 2>$null
        if ($LASTEXITCODE -eq 0) { return $c }
    }
    return 'libx264'
}

function Get-ArgsCodificador([string]$c) {
    switch ($c) {
        'h264_nvenc' { '-c:v h264_nvenc -preset p4 -cq 23 -pix_fmt yuv420p' }
        'h264_qsv'   { '-c:v h264_qsv -preset veryfast -global_quality 23 -pix_fmt nv12' }
        'h264_amf'   { '-c:v h264_amf -quality speed -rc cqp -qp_i 22 -qp_p 24 -pix_fmt yuv420p' }
        default      { '-c:v libx264 -preset ultrafast -crf 20 -pix_fmt yuv420p' }
    }
}

function Get-Calibracion([string]$texto) {
    $c = $texto | ConvertFrom-Json
    foreach ($k in 'screenX', 'screenY', 'outerWidth', 'outerHeight', 'innerWidth', 'innerHeight') {
        if ($null -eq $c.$k) { throw "Falta '$k' en -Calibracion." }
    }
    $dpr = if ($c.devicePixelRatio) { [double]$c.devicePixelRatio } else { 1.0 }
    $marcoAncho = if ($c.marcoAncho) { [double]$c.marcoAncho } else { [double]$c.innerWidth }
    $marcoAlto = if ($c.marcoAlto) { [double]$c.marcoAlto } else { [double]$c.innerHeight }
    # Borde lateral = (outer - inner) / 2; el inferior se asume igual al lateral; lo demás de alto es la barra del navegador.
    $borde = ([double]$c.outerWidth - [double]$c.innerWidth) / 2
    $barra = [double]$c.outerHeight - [double]$c.innerHeight - $borde
    [pscustomobject]@{
        viewportX = [math]::Round(([double]$c.screenX + $borde) * $dpr, 1)
        viewportY = [math]::Round(([double]$c.screenY + $barra) * $dpr, 1)
        viewportAncho = [math]::Round([double]$c.innerWidth * $dpr, 1)
        viewportAlto = [math]::Round([double]$c.innerHeight * $dpr, 1)
        escala = [math]::Round(([double]$c.innerWidth * $dpr) / $marcoAncho, 5)
        marcoAncho = $marcoAncho; marcoAlto = $marcoAlto
        devicePixelRatio = $dpr
        origen = $c
        hora = (Get-Ahora).local
    }
}

# ---------------------------------------------------------------- guardián (proceso oculto)
if ($Guardian) {
    $toma = Read-Json $Guardian
    $senal = [System.IO.Path]::ChangeExtension($Guardian, '.detener')
    $log = [System.IO.Path]::ChangeExtension($Guardian, '.log')
    $region = ''
    if ($toma.recorte) {
        $r = $toma.recorte
        $region = ":offset_x=$($r.x):offset_y=$($r.y):video_size=$($r.ancho)x$($r.alto)"
    }
    $entrada = "ddagrab=output_idx=0:framerate=$($toma.fps):draw_mouse=0$region,hwdownload,format=bgra"
    $argumentos = "-hide_banner -loglevel warning -stats -stats_period 0.2 -f lavfi -i `"$entrada`" " +
                  (Get-ArgsCodificador $toma.codificador) + " -g $($toma.fps) -r $($toma.fps) -y `"$($toma.video)`""
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'ffmpeg'
    $psi.Arguments = $argumentos
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardOutput = $true
    $lanzado = Get-Ahora
    $p = [System.Diagnostics.Process]::Start($psi)
    $sb = New-Object System.Text.StringBuilder
    try {
    Set-Propiedad $toma 'pidFfmpeg' $p.Id
    Set-Propiedad $toma 'ffmpegArgs' $argumentos
    Write-Json $Guardian $toma

    $buf = New-Object char[] 4096
    $tarea = $p.StandardError.ReadAsync($buf, 0, $buf.Length)
    $inicioFijado = $false
    $finPedido = $null
    while ($true) {
        if ($tarea.Wait(100)) {
            $n = $tarea.Result
            if ($n -le 0) { break }
            $trozo = New-Object string ($buf, 0, $n)
            [void]$sb.Append($trozo)
            if (-not $inicioFijado) {
                # ffmpeg escribe "frame=  N" partido en varios trozos: se busca en lo último acumulado.
                $texto = $sb.ToString()
                $cola = $texto.Substring([math]::Max(0, $texto.Length - 400))
                $m = [regex]::Matches($cola, 'frame=\s*(\d+)\s')
                if ($m.Count -gt 0) {
                    $cuadros = [int]$m[$m.Count - 1].Groups[1].Value
                    if ($cuadros -gt 0) {
                        # Hora del primer cuadro = ahora - cuadros ya codificados.
                        $t = (Get-Date).AddSeconds(-($cuadros / [double]$toma.fps))
                        $toma = Read-Json $Guardian
                        Set-Propiedad $toma 'inicioLocal' $t.ToString('yyyy-MM-ddTHH:mm:ss.fffzzz')
                        Set-Propiedad $toma 'inicioUtc' $t.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
                        Set-Propiedad $toma 'ffmpegLanzadoLocal' $lanzado.local
                        Write-Json $Guardian $toma
                        $inicioFijado = $true
                    }
                }
            }
            $tarea = $p.StandardError.ReadAsync($buf, 0, $buf.Length)
        }
        if (-not $finPedido -and (Test-Path -LiteralPath $senal)) {
            $finPedido = Get-Ahora
            try { $p.StandardInput.Write('q'); $p.StandardInput.Flush() } catch { }
        }
        if ($finPedido -and -not $p.HasExited -and ((Get-Date) - [datetime]::Parse($finPedido.local)).TotalSeconds -gt 15) {
            $p.Kill()
        }
        if ($p.HasExited -and $tarea.IsCompleted -and $tarea.Result -le 0) { break }
    }
    } finally {
        # Pase lo que pase en el guardián, ffmpeg no se queda grabando: 'q' y, si no cierra, Kill.
        if (-not $p.HasExited) {
            try { $p.StandardInput.Write('q'); $p.StandardInput.Flush() } catch { }
            if (-not $p.WaitForExit(10000)) { try { $p.Kill() } catch { } }
        }
    }
    $p.WaitForExit()
    [System.IO.File]::WriteAllText($log, $sb.ToString(), $utf8)
    $toma = Read-Json $Guardian
    if (-not $finPedido) { $finPedido = Get-Ahora }
    Set-Propiedad $toma 'finPedidoLocal' $finPedido.local
    Set-Propiedad $toma 'codigoSalidaFfmpeg' $p.ExitCode
    $dur = $null
    try {
        $ErrorActionPreference = 'Continue'   # en PS 5.1, 2>$null con Stop convierte cualquier aviso en error
        $dur = & ffprobe -v error -show_entries format=duration -of csv=p=0 "$($toma.video)" 2>$null
    } catch { $dur = $null } finally { $ErrorActionPreference = 'Stop' }
    if ($dur) {
        $d = [double]::Parse(($dur | Select-Object -First 1), [Globalization.CultureInfo]::InvariantCulture)
        Set-Propiedad $toma 'duracion' ([math]::Round($d, 3))
        if ($toma.inicioLocal) {
            $fin = [datetimeoffset]::Parse($toma.inicioLocal).AddSeconds($d)
            Set-Propiedad $toma 'finLocal' $fin.ToString('yyyy-MM-ddTHH:mm:ss.fffzzz')
            Set-Propiedad $toma 'finUtc' $fin.UtcDateTime.ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
        }
    }
    Write-Json $Guardian $toma
    Remove-Item -LiteralPath $senal -ErrorAction SilentlyContinue
    exit 0
}

# ---------------------------------------------------------------- comandos
$activa = $null
if (Test-Path -LiteralPath $archivoEstado) {
    $activa = Read-Json $archivoEstado
    if (-not (Test-Vivo $activa.pidGuardian)) {
        if (Stop-FfmpegHuerfano $activa.json) { Write-Output "Aviso: el guardián había muerto; se detuvo el ffmpeg que seguía grabando ($($activa.video))." }
        Remove-Item -LiteralPath $archivoEstado
        $activa = $null
    }
}

if ($Iniciar) {
    if ($activa) { throw "Ya hay una grabación en curso: $($activa.video). Deténla con -Detener." }
    if (-not (Get-Command ffmpeg -ErrorAction SilentlyContinue)) { throw 'No se encontró ffmpeg en el PATH.' }
    New-Item -ItemType Directory -Force -Path $dirTomas | Out-Null
    $nombreSeguro = ($Nombre -replace '[^\w\-]+', '-').Trim('-')
    if (-not $nombreSeguro) { $nombreSeguro = 'toma' }
    $base = Join-Path $dirTomas ((Get-Date -Format 'yyyyMMdd_HHmmss') + '_' + $nombreSeguro)
    $cod = Select-Codificador
    $toma = [pscustomobject]@{
        nombre = $nombreSeguro
        video = "$base.mkv"
        fps = $Fps
        codificador = $cod
        pantalla = Get-Pantalla
        ventanaEdge = Get-VentanaEdge
        pedidoLocal = (Get-Ahora).local
    }
    if ($SoloEdge) {
        $v = $toma.ventanaEdge
        if (-not $v -or $v.minimizada) { throw '-SoloEdge: no hay una ventana de Edge visible.' }
        $px = [math]::Max(0, $v.x); $py = [math]::Max(0, $v.y)
        $pw = [math]::Min($v.ancho, $toma.pantalla.ancho - $px); $ph = [math]::Min($v.alto, $toma.pantalla.alto - $py)
        $pw -= $pw % 2; $ph -= $ph % 2   # el codificador pide medidas pares
        Set-Propiedad $toma 'recorte' ([pscustomobject]@{ x = $px; y = $py; ancho = $pw; alto = $ph })
    }
    if ($Calibracion) { Set-Propiedad $toma 'calibracion' (Get-Calibracion $Calibracion) }
    Write-Json "$base.json" $toma
    $g = Start-Process powershell -WindowStyle Hidden -PassThru -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Guardian', "`"$base.json`"")
    Write-Json $archivoEstado ([pscustomobject]@{ pidGuardian = $g.Id; json = "$base.json"; video = "$base.mkv" })
    $limite = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 200
        $leida = Read-Json "$base.json"
    } while (-not $leida.inicioLocal -and (Get-Date) -lt $limite -and (Test-Vivo $g.Id))
    if (-not $leida.inicioLocal) {
        $log = "$base.log"
        throw "ffmpeg no empezó a grabar en 15 s. Revisa $log (o ejecuta -Detener)."
    }
    Write-Output "Grabando ($cod, $Fps fps): $base.mkv"
    Write-Output "Inicio: $($leida.inicioLocal)  (UTC $($leida.inicioUtc))"
    if ($leida.ventanaEdge) {
        $v = $leida.ventanaEdge
        Write-Output "Ventana de Edge: x=$($v.x) y=$($v.y) $($v.ancho)x$($v.alto) (primer plano: $($v.enPrimerPlano))"
    } else { Write-Output 'Aviso: no se encontró una ventana de Edge.' }
    exit 0
}

if ($Calibracion) {
    # Sin -Iniciar: se agrega a la toma en curso o, si no hay, a la última grabada.
    $json = if ($activa) { $activa.json } else {
        (Get-ChildItem -LiteralPath $dirTomas -Filter '*.json' -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^\d{8}_\d{6}_[^.]+\.json$' } | Sort-Object Name -Descending | Select-Object -First 1).FullName
    }
    if (-not $json) { throw 'No hay toma a la que agregar la calibración.' }
    $toma = Read-Json $json
    $cal = Get-Calibracion $Calibracion
    Set-Propiedad $toma 'calibracion' $cal
    Write-Json $json $toma
    Write-Output "Calibración guardada en $json"
    Write-Output ("viewport en pantalla: ({0}, {1}) {2}x{3}; escala marco->pantalla {4}" -f $cal.viewportX, $cal.viewportY, $cal.viewportAncho, $cal.viewportAlto, $cal.escala)
    exit 0
}

if ($Detener) {
    if (-not $activa) { throw 'No hay ninguna grabación en curso.' }
    $senal = [System.IO.Path]::ChangeExtension($activa.json, '.detener')
    [System.IO.File]::WriteAllText($senal, (Get-Ahora).local, $utf8)
    $limite = (Get-Date).AddSeconds(25)
    while ((Test-Vivo $activa.pidGuardian) -and (Get-Date) -lt $limite) { Start-Sleep -Milliseconds 200 }
    if (Test-Vivo $activa.pidGuardian) {
        Stop-Process -Id $activa.pidGuardian -Force -ErrorAction SilentlyContinue
    }
    if (Stop-FfmpegHuerfano $activa.json) {
        Write-Output 'Aviso: se forzó el cierre de ffmpeg; el MKV puede quedar sin el último segundo.'
    }
    Remove-Item -LiteralPath $archivoEstado -ErrorAction SilentlyContinue
    $t = Read-Json $activa.json
    Write-Output "Toma detenida: $($t.video)"
    Write-Output "Inicio $($t.inicioLocal) · fin $($t.finLocal) · $($t.duracion) s · salida ffmpeg $($t.codigoSalidaFfmpeg)"
    Write-Output "Datos: $($activa.json)"
    exit 0
}

if ($Estado) {
    if ($activa) { Write-Output "Grabando: $($activa.video) (guardián PID $($activa.pidGuardian))" }
    else { Write-Output 'No hay grabación en curso.' }
    exit 0
}

Get-Help $PSCommandPath -Examples
