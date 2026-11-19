<#
.SYNOPSIS
    Publica SGV con un paquete por versión (carpeta + .zip + manifiesto) y permite volver atrás.

.DESCRIPTION
    1. Compila WebSGV en Release y lo publica a carpeta (WPP FileSystem, con los candados del csproj:
       sin App_Data, Uploads ni Database) en
           <CarpetaPublicaciones>\<aaaaMMdd_HHmm>_<commit corto>\sitio\
       y escribe al lado manifiesto.txt (commit, rama, cambios sin confirmar, fecha y SHA-256 de cada
       archivo). Comprime la carpeta en <CarpetaPublicaciones>\<aaaaMMdd_HHmm>_<commit corto>.zip.
       La carpeta de publicaciones nunca puede estar dentro del repo.
    2. Comprueba el paquete: sin App_Data, Uploads, Database, archivos de secretos
       (connectionStrings*.config, appSettings.Secrets.config) ni *.pdb; con bin\WebSGV.dll, los
       binarios nativos de QuestPDF (bin\runtimes\win-x86 y win-x64) y Web.config transformado a
       Release (sin debug="true", con la regla ForzarHTTPS). Si algo falla, no sigue.
    3. Compara con el servidor por FTP (solo lectura: LIST/NLST/SIZE), sin entrar nunca en App_Data
       ni Uploads, y muestra (a) lo que se subiría y (b) los SOBRANTES: archivos
       .aspx/.ascx/.ashx/.asmx/.master/.cs/.js/.css/.dll/.config (y todo lo de Database/) que están en
       el servidor y no en el paquete. App_Data, Uploads y los archivos de secretos nunca son sobrantes.
    4. Sin -SoloListar escribe por pasos: primero sobrescribe el marcador publicacion.txt con "EN CURSO";
       si hay que tocar bin\ o Web.config pone app_offline.htm (sitio en mantenimiento) ANTES de todo;
       luego contenido, bin\, Web.config; solo si todo salió bien borra app_offline.htm. Al primer error
       de un paso no pasa al siguiente; si app_offline.htm pudo quedar puesto, lo dice en rojo (el sitio
       sigue en mantenimiento hasta repetir la publicación o usar -VolverA). Al terminar bien sube el
       marcador publicacion.txt definitivo (paquete y commit).
       Los sobrantes solo se borran con -BorrarSobrantes y escribiendo BORRAR cuando se pide.
    5. Solo se saltan archivos de mismo tamaño si se confía en los SHA-256 locales: el marcador del
       servidor coincide con ultimo-publicado.txt de esta máquina, no está EN CURSO, y bin/WebSGV.dll del
       servidor no es más nueva que el marcador (MDTM). Si no (otra PC publicó, una publicación falló, o
       se publicó desde Visual Studio y se nota en la DLL) todo lo de mismo tamaño se sube.
       Ojo: una publicación desde Visual Studio que no cambie bin/WebSGV.dll NO se detecta; después de
       publicar desde Visual Studio use -SubirTodo.

    Los archivos de secretos NO van en el paquete: el servidor conserva los suyos. Si cambian
    (p. ej. una clave nueva en appSettings.Secrets.config), se suben a mano (ver README.md).

    Credencial FTP: la misma de respaldo (privado\ftp-respaldo.xml, DPAPI), que se guarda con
        .\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial
    Sin credencial hace todo lo local y avisa que no pudo comparar con el servidor.

    Código de salida: 0 bien; 1 error (compilación, comprobaciones, FTP o subida).

.PARAMETER CarpetaPublicaciones
    Dónde guardar las publicaciones. Por defecto $env:SGV_CARPETA_PUBLICACIONES. Fuera del repo.
.PARAMETER SoloListar
    Compila, empaqueta, comprueba y compara con el servidor, sin escribir en él.
.PARAMETER BorrarSobrantes
    Además de subir, borra los sobrantes (pide escribir BORRAR).
.PARAMETER VolverA
    Carpeta o .zip de una publicación anterior: la vuelve a subir (completa) en vez de compilar.
    Si es anterior al commit 773dad0 (contraseñas sha256.), o no se puede saber, exige -Forzar.
    Si no tiene manifiesto.txt (sin comprobación de integridad) exige además -SinManifiesto.
.PARAMETER SinManifiesto
    Con -VolverA: acepta un paquete sin manifiesto.txt (no se puede comprobar que esté íntegro).
.PARAMETER SubirTodo
    Sube todos los archivos del paquete aunque parezcan iguales (por defecto se saltan los de mismo
    tamaño en el servidor y mismo SHA-256 que en la última publicación hecha con este script).
.PARAMETER ComprobarPaquete
    Solo corre las comprobaciones sobre una carpeta de sitio publicada (o una publicación con sitio\).

.EXAMPLE
    .\herramientas\publicar\publicar.ps1 -SoloListar                          # usa $env:SGV_CARPETA_PUBLICACIONES
    .\herramientas\publicar\publicar.ps1 -CarpetaPublicaciones D:\PublicacionesSGV
    .\herramientas\publicar\publicar.ps1 -BorrarSobrantes
    .\herramientas\publicar\publicar.ps1 -VolverA D:\PublicacionesSGV\20261119_1504_3b2f8b0.zip -SoloListar
    .\herramientas\publicar\publicar.ps1 -ComprobarPaquete D:\PublicacionesSGV\20261119_1504_3b2f8b0
#>
param(
    [string]$CarpetaPublicaciones,
    [switch]$SoloListar,
    [switch]$BorrarSobrantes,
    [string]$VolverA,
    [switch]$Forzar,
    [switch]$SinManifiesto,
    [switch]$PermitirCambiosSinConfirmar,
    [switch]$SubirTodo,
    [string]$ComprobarPaquete,

    [string]$Usuario   = 'SGVTRANSPORTES',
    [string]$Servidor  = 'ftp://sgvtransporte.somee.com',
    [string]$RutaSitio = '/www.sgvtransporte.somee.com',
    [string]$ArchivoCredencial
)

$ErrorActionPreference = 'Stop'
trap { Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red; exit 1 }
Import-Module (Join-Path $PSScriptRoot '..\comun\Ftp.psm1') -Force -DisableNameChecking

# Commit desde el que las contraseñas están en formato sha256.: volver a un paquete anterior impediría
# iniciar sesión (ese código no entiende los hashes nuevos).
$CommitContrasenasSha256 = '773dad0'

# =================================================================================================
# Funciones puras (sin red ni compilación): probar-publicar.ps1 las extrae de este archivo.
# Las rutas son relativas a la raíz del sitio; se aceptan '/' o '\' y no distinguen mayúsculas.
# =================================================================================================

function ConvertTo-RutaSitio {
    param([string]$Ruta)
    return $Ruta.Replace('\', '/').Trim('/')
}

# $true si la ruta NUNCA se toca en el servidor: cualquier cosa dentro de App_Data/ o Uploads/ (en
# cualquier nivel) y los archivos de secretos (connectionStrings*.config, appSettings.Secrets.config).
function Test-RutaProtegida {
    param([string]$Ruta)
    $r = ConvertTo-RutaSitio $Ruta
    $segmentos = @($r -split '/' | Where-Object { $_ })
    if ($segmentos.Count -eq 0) { return $true }
    for ($i = 0; $i -lt $segmentos.Count - 1; $i++) {
        if ($segmentos[$i] -ieq 'App_Data' -or $segmentos[$i] -ieq 'Uploads') { return $true }
    }
    $hoja = $segmentos[-1]
    if ($hoja -ieq 'App_Data' -or $hoja -ieq 'Uploads') { return $true }
    if ($hoja -like 'connectionStrings*.config') { return $true }      # -like no distingue mayúsculas
    if ($hoja -ieq 'appSettings.Secrets.config') { return $true }
    return $false
}

# Archivos que escribe este script en la raíz del sitio y que nunca son sobrantes ni protegidos:
# app_offline.htm (mantenimiento durante la subida) y publicacion.txt (marcador de la última publicación).
function Test-RutaDeControl {
    param([string]$Ruta)
    $r = ConvertTo-RutaSitio $Ruta
    return ($r -ieq 'app_offline.htm') -or ($r -ieq 'publicacion.txt')
}

# $true si un archivo del servidor que no está en el paquete cuenta como sobrante (código o config
# que IIS sirve o compila). Todo lo de Database/ también: el sitio no lo lee y no debe estar publicado.
function Test-RutaCandidataSobrante {
    param([string]$Ruta)
    $r = ConvertTo-RutaSitio $Ruta
    if (Test-RutaProtegida $r) { return $false }
    if (Test-RutaDeControl $r) { return $false }
    if (($r -split '/')[0] -ieq 'Database') { return $true }
    $ext = [IO.Path]::GetExtension($r).ToLowerInvariant()
    return @('.aspx', '.ascx', '.ashx', '.asmx', '.master', '.cs', '.js', '.css', '.dll', '.config') -contains $ext
}

# Plan de publicación.
#   $Paquete : objetos { Ruta; Tamano; Sha } del paquete.
#   $Remotos : objetos { Ruta; Tamano } del servidor (Tamano -1 si no se sabe).
#   $HashesPublicados : hashtable Ruta -> SHA-256 de la última publicación hecha con este script (o $null).
# Devuelve objetos { Ruta; Accion; Motivo; Tamano }:
#   Subir    (Motivo: nuevo | tamaño distinto | sin confirmar | forzado)
#   Igual    (mismo tamaño en el servidor y mismo SHA-256 que lo último publicado)
#   Borrar   (sobrante, solo con -BorrarSobrantes) / Sobrante (sin -BorrarSobrantes)
# Nunca devuelve Subir/Borrar para una ruta protegida, aunque venga en el paquete o falte en él.
function Get-PlanPublicacion {
    param([object[]]$Paquete, [object[]]$Remotos, [hashtable]$HashesPublicados,
          [switch]$BorrarSobrantes, [switch]$SubirTodo)
    $remoto = New-Object 'System.Collections.Generic.Dictionary[string,long]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($r in @($Remotos)) { if ($r) { $remoto[(ConvertTo-RutaSitio $r.Ruta)] = [long]$r.Tamano } }
    $publicado = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
    if ($HashesPublicados) { foreach ($k in $HashesPublicados.Keys) { $publicado[(ConvertTo-RutaSitio $k)] = [string]$HashesPublicados[$k] } }
    $enPaquete = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

    $plan = @()
    foreach ($p in @($Paquete)) {
        if (-not $p) { continue }
        $ruta = ConvertTo-RutaSitio $p.Ruta
        [void]$enPaquete.Add($ruta)
        if ((Test-RutaProtegida $ruta) -or (Test-RutaDeControl $ruta)) { continue }
        $tamR = -1L
        $existe = $remoto.TryGetValue($ruta, [ref]$tamR)
        $shaPub = $null
        $hayPub = $publicado.TryGetValue($ruta, [ref]$shaPub)
        $accion = 'Subir'
        $motivo = if (-not $existe) { 'nuevo' }
                  elseif ($tamR -ge 0 -and $tamR -ne [long]$p.Tamano) { 'tamaño distinto' }
                  elseif ($SubirTodo) { 'forzado' }
                  elseif ($tamR -eq [long]$p.Tamano -and $hayPub -and $shaPub -ieq [string]$p.Sha) { $accion = 'Igual'; '' }
                  else { 'sin confirmar' }
        $plan += New-Object PSObject -Property ([ordered]@{ Ruta = $ruta; Accion = $accion; Motivo = $motivo; Tamano = [long]$p.Tamano })
    }
    foreach ($ruta in $remoto.Keys) {
        if ($enPaquete.Contains($ruta)) { continue }
        if (-not (Test-RutaCandidataSobrante $ruta)) { continue }
        $plan += New-Object PSObject -Property ([ordered]@{
            Ruta = $ruta; Accion = $(if ($BorrarSobrantes) { 'Borrar' } else { 'Sobrante' }); Motivo = 'no está en el paquete'; Tamano = $remoto[$ruta] })
    }
    return $plan
}

# Grupo de subida: 0 = contenido, 1 = bin/, 2 = Web.config raíz (al final: reinicia la aplicación);
# -1 = archivo de control (app_offline.htm, publicacion.txt): no se sube como parte del paquete.
function Get-OrdenSubida {
    param([string]$Ruta)
    $r = ConvertTo-RutaSitio $Ruta
    if (Test-RutaDeControl $r) { return -1 }
    if ($r -ieq 'Web.config') { return 2 }
    if ($r -ilike 'bin/*') { return 1 }
    return 0
}

# Pasos de la subida, en orden. Devuelve objetos { Paso; Rutas }, con Paso:
#   MarcadorEnCurso  sobrescribir publicacion.txt con "EN CURSO ..." (siempre el primero que escribe: si
#                    algo falla después, la próxima publicación no confía en los SHA-256 de la anterior)
#   Mantenimiento  subir app_offline.htm (solo si hay algo de bin/ o Web.config que subir; va ANTES del
#                  contenido para no servir nunca páginas nuevas con la DLL vieja)
#   Contenido      archivos del grupo 0
#   Bin            archivos de bin/
#   WebConfig      Web.config raíz
#   QuitarMantenimiento  borrar app_offline.htm (también si ya estaba en el servidor de una subida fallida)
#   Marcador       subir publicacion.txt con el paquete y el commit
# El que ejecuta se detiene al terminar el primer paso con errores: no pasa al siguiente.
function Get-PasosSubida {
    param([string[]]$Rutas, [switch]$MantenimientoEnServidor)
    $rs = @($Rutas | Where-Object { $_ } | ForEach-Object { ConvertTo-RutaSitio $_ } | Where-Object { (Get-OrdenSubida $_) -ge 0 })
    $g0 = @($rs | Where-Object { (Get-OrdenSubida $_) -eq 0 } | Sort-Object)
    $g1 = @($rs | Where-Object { (Get-OrdenSubida $_) -eq 1 } | Sort-Object)
    $g2 = @($rs | Where-Object { (Get-OrdenSubida $_) -eq 2 })
    $tocaApp = ($g1.Count + $g2.Count) -gt 0
    $pasos = @([pscustomobject]@{ Paso = 'MarcadorEnCurso'; Rutas = @('publicacion.txt') })
    if ($tocaApp) { $pasos += [pscustomobject]@{ Paso = 'Mantenimiento'; Rutas = @('app_offline.htm') } }
    if ($g0.Count) { $pasos += [pscustomobject]@{ Paso = 'Contenido'; Rutas = $g0 } }
    if ($g1.Count) { $pasos += [pscustomobject]@{ Paso = 'Bin'; Rutas = $g1 } }
    if ($g2.Count) { $pasos += [pscustomobject]@{ Paso = 'WebConfig'; Rutas = $g2 } }
    if ($tocaApp -or $MantenimientoEnServidor) { $pasos += [pscustomobject]@{ Paso = 'QuitarMantenimiento'; Rutas = @('app_offline.htm') } }
    $pasos += [pscustomobject]@{ Paso = 'Marcador'; Rutas = @('publicacion.txt') }
    return $pasos
}

# Ejecuta los pasos de Get-PasosSubida (salvo 'Marcador', que el script sube al final, tras los
# sobrantes). $Accion: scriptblock (paso, ruta) que hace la escritura real (o una simulada en pruebas);
# para Contenido/Bin/WebConfig se llama una vez por ruta, para los demás una vez con la ruta del paso.
# Termina el paso en curso y, si tuvo errores, no sigue con los siguientes.
# MantenimientoPuesto pasa a $true ANTES de intentar subir app_offline.htm (el STOR pudo llegar aunque
# falle después la comprobación) y a $false solo si se borró bien.
# Devuelve { Errores; Subidos; MantenimientoPuesto; Hechos (pasos intentados); PasoFallido }.
function Invoke-PasosSubida {
    param([object[]]$Pasos, [scriptblock]$Accion, [switch]$MantenimientoEnServidor)
    $errores = 0; $subidos = 0; $puesto = [bool]$MantenimientoEnServidor
    $hechos = @(); $fallido = $null
    foreach ($paso in $Pasos) {
        if ($paso.Paso -eq 'Marcador') { continue }
        $hechos += $paso.Paso
        $erroresPaso = 0
        if ($paso.Paso -in 'Contenido', 'Bin', 'WebConfig') {
            $n = 0
            foreach ($ruta in $paso.Rutas) {
                $n++
                try { & $Accion $paso.Paso $ruta | Out-Null; $subidos++ }
                catch { $erroresPaso++; Write-Host "ERROR al subir $ruta : $($_.Exception.Message)" -ForegroundColor Red }
                if ($n % 25 -eq 0) { Write-Host "  $($paso.Paso): $n de $($paso.Rutas.Count)..." }
            }
            Write-Host "$($paso.Paso): subidos $($paso.Rutas.Count - $erroresPaso) de $($paso.Rutas.Count)."
        } else {
            if ($paso.Paso -eq 'Mantenimiento') { $puesto = $true }
            try {
                $msg = & $Accion $paso.Paso $paso.Rutas[0]
                if ($msg) { Write-Host $msg }
                if ($paso.Paso -eq 'QuitarMantenimiento') { $puesto = $false }
            } catch {
                $erroresPaso++
                Write-Host "ERROR en el paso $($paso.Paso) ($($paso.Rutas[0])) : $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        $errores += $erroresPaso
        if ($erroresPaso) {
            Write-Host "Hubo errores en el paso '$($paso.Paso)': no se sigue con los siguientes." -ForegroundColor Red
            $fallido = $paso.Paso
            break
        }
    }
    return [pscustomobject]@{ Errores = $errores; Subidos = $subidos; MantenimientoPuesto = $puesto; Hechos = $hechos; PasoFallido = $fallido }
}

# Marcador publicacion.txt (raíz del sitio): qué paquete y commit se publicó por última vez.
function ConvertTo-TextoMarcador {
    param([string]$Paquete, [string]$Commit, [datetime]$Fecha, [string]$Equipo)
    return @(
        'SGV - última publicación (lo escribe herramientas\publicar\publicar.ps1)'
        "Paquete: $Paquete"
        "Commit: $Commit"
        "Fecha: $($Fecha.ToString('yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture))"
        "Equipo: $Equipo"
    ) -join "`r`n"
}

# Marcador que se escribe ANTES de tocar nada: nunca coincide con un ultimo-publicado.txt (los nombres
# de paquete no tienen espacios ni empiezan por 'EN CURSO'). Si la publicación no llega al final, queda así.
function ConvertTo-TextoMarcadorEnCurso {
    param([string]$Paquete, [datetime]$Fecha, [string]$Equipo)
    $f = $Fecha.ToString('yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
    return @(
        "EN CURSO $Paquete $f"
        "Paquete: EN CURSO $Paquete $f"
        'Commit: EN-CURSO'
        "Equipo: $Equipo"
        'Si esto sigue así, la publicación no terminó: la próxima subirá todo lo de mismo tamaño.'
    ) -join "`r`n"
}

# Lee publicacion.txt. Devuelve { Paquete; Commit; EnCurso } o $null si no hay texto o no tiene Paquete.
function ConvertFrom-Marcador {
    param([string]$Texto)
    if ([string]::IsNullOrWhiteSpace($Texto)) { return $null }
    $m = [ordered]@{ Paquete = $null; Commit = $null; EnCurso = $false }
    $lineas = @($Texto -split "`r?`n")
    if ($lineas[0] -match '^\s*EN CURSO') { $m.EnCurso = $true }
    foreach ($l in $lineas) {
        if ($l -match '^Paquete:\s*(.+?)\s*$') { $m.Paquete = $Matches[1] }
        elseif ($l -match '^Commit:\s*(\S+)') { $m.Commit = $Matches[1] }
    }
    if ($m.Paquete -match '^EN CURSO') { $m.EnCurso = $true }
    if (-not $m.Paquete) { return $null }
    return New-Object PSObject -Property $m
}

# $true solo si el marcador del servidor corresponde a la última publicación registrada en esta máquina
# (mismo nombre de paquete y mismo commit) y no es un marcador EN CURSO.
function Test-MarcadorCoincide {
    param($Marcador, [string]$Paquete, [string]$Commit)
    if (-not $Marcador -or [string]::IsNullOrWhiteSpace($Paquete) -or [string]::IsNullOrWhiteSpace($Commit)) { return $false }
    if ($Marcador.EnCurso) { return $false }
    return ([string]$Marcador.Paquete -ieq $Paquete) -and ([string]$Marcador.Commit -ieq $Commit)
}

# ¿Se puede confiar en los SHA-256 de la última publicación local para saltar archivos de mismo tamaño?
# $FechaMarcador / $FechaDll: fecha (MDTM) de publicacion.txt y de bin/WebSGV.dll en el servidor
# ($null si no se pudo leer). Si la DLL es más nueva que el marcador, alguien publicó sin este script
# (p. ej. Visual Studio). Ante cualquier duda: no confiar. Devuelve { Confiar; Motivo }.
function Get-ConfianzaHashes {
    param($Marcador, [string]$Paquete, [string]$Commit, $FechaMarcador, $FechaDll)
    $no = { param($m) [pscustomobject]@{ Confiar = $false; Motivo = $m } }
    if (-not $Marcador) { return & $no 'el servidor no tiene marcador publicacion.txt (o no se pudo leer)' }
    if ($Marcador.EnCurso) { return & $no "la publicación anterior no terminó (marcador: $($Marcador.Paquete))" }
    if ([string]::IsNullOrWhiteSpace($Paquete)) { return & $no 'esta máquina no tiene registro de la última publicación (ultimo-publicado.txt)' }
    if (-not (Test-MarcadorCoincide -Marcador $Marcador -Paquete $Paquete -Commit $Commit)) {
        return & $no "el marcador del servidor ($($Marcador.Paquete)) no es la última publicación de esta máquina ($Paquete): publicó otra máquina"
    }
    if ($null -eq $FechaMarcador -or $null -eq $FechaDll) { return & $no 'no se pudo leer la fecha (MDTM) del marcador o de bin/WebSGV.dll' }
    if ([datetime]$FechaDll -gt [datetime]$FechaMarcador) {
        return & $no 'bin/WebSGV.dll del servidor es más nueva que el marcador: se publicó sin este script (¿Visual Studio?)'
    }
    return [pscustomobject]@{ Confiar = $true; Motivo = 'el marcador coincide con la última publicación de esta máquina' }
}

# Código de 'git merge-base --is-ancestor': 0 = sí lo contiene, 1 = no, otro (128: commit desconocido) = $null.
function ConvertFrom-CodigoAncestro {
    param([int]$Codigo)
    if ($Codigo -eq 0) { return $true }
    if ($Codigo -eq 1) { return $false }
    return $null
}

# Comprobaciones del paquete. $Rutas: archivos del paquete (relativos al sitio). $WebConfig: texto del
# Web.config publicado (o $null). Devuelve objetos { Comprobacion; Ok; Detalle }.
function Test-ContenidoPaquete {
    param([string[]]$Rutas, [string]$WebConfig)
    $rs = @($Rutas | Where-Object { $_ } | ForEach-Object { ConvertTo-RutaSitio $_ })
    $res = New-Object System.Collections.Generic.List[object]
    function Add-Res($nombre, $malas, $detalleOk) {
        $m = @($malas | Where-Object { $_ })
        $res.Add((New-Object PSObject -Property ([ordered]@{
            Comprobacion = $nombre; Ok = ($m.Count -eq 0)
            Detalle = $(if ($m.Count -eq 0) { $detalleOk } else { ($m | Select-Object -First 5) -join ', ' }) })))
    }
    $primer = { param($r) ($r -split '/')[0] }
    Add-Res 'Sin App_Data'  ($rs | Where-Object { (& $primer $_) -ieq 'App_Data' -or $_ -imatch '(^|/)App_Data/' }) 'no hay'
    Add-Res 'Sin Uploads'   ($rs | Where-Object { (& $primer $_) -ieq 'Uploads'  -or $_ -imatch '(^|/)Uploads/' })  'no hay'
    Add-Res 'Sin Database'  ($rs | Where-Object { (& $primer $_) -ieq 'Database' }) 'no hay'
    Add-Res 'Sin archivos de secretos' ($rs | Where-Object {
        $h = ($_ -split '/')[-1]; $h -like 'connectionStrings*.config' -or $h -ieq 'appSettings.Secrets.config' }) 'no hay connectionStrings*.config ni appSettings.Secrets.config'
    Add-Res 'Sin *.pdb'     ($rs | Where-Object { $_ -ilike '*.pdb' }) 'no hay'
    Add-Res 'Con bin/WebSGV.dll' $(if ($rs -icontains 'bin/WebSGV.dll') { @() } else { @('falta bin/WebSGV.dll') }) 'está'
    foreach ($arq in 'win-x86', 'win-x64') {
        $n = @($rs | Where-Object { $_ -ilike "bin/runtimes/$arq/native/QuestPdf*.dll" })
        Add-Res "Con QuestPDF nativo ($arq)" $(if ($n.Count) { @() } else { @("falta bin/runtimes/$arq/native/QuestPdf*.dll") }) (($n | ForEach-Object { ($_ -split '/')[-1] }) -join ', ')
    }
    if ($rs -inotcontains 'Web.config' -or [string]::IsNullOrWhiteSpace($WebConfig)) {
        Add-Res 'Web.config de Release' @('falta Web.config') ''
    } else {
        $malas = @()
        try {
            $x = [xml]$WebConfig
            $comp = $x.SelectSingleNode('/configuration/system.web/compilation')
            if ($comp -and $comp.GetAttribute('debug') -ieq 'true') { $malas += 'compilation debug="true"' }
            if (-not $x.SelectSingleNode("/configuration/system.webServer/rewrite/rules/rule[@name='ForzarHTTPS']")) { $malas += 'sin la regla ForzarHTTPS (¿no se transformó a Release?)' }
        } catch { $malas += "Web.config no es XML válido: $($_.Exception.Message)" }
        Add-Res 'Web.config de Release' $malas 'sin debug="true", con ForzarHTTPS'
    }
    return $res.ToArray()
}

# Lee manifiesto.txt. Devuelve { Commit; Rama; CambiosSinConfirmar; Fecha; Archivos (hashtable Ruta -> @{Sha; Tamano}) }.
function ConvertFrom-Manifiesto {
    param([string[]]$Lineas)
    $info = [ordered]@{ Commit = $null; Rama = $null; CambiosSinConfirmar = $null; Fecha = $null
                        Archivos = @{} }
    $enArchivos = $false
    foreach ($l in $Lineas) {
        if ($null -eq $l) { continue }
        $l = $l.TrimEnd("`r")
        if ($l -match '^--- Archivos') { $enArchivos = $true; continue }
        if ($enArchivos) {
            if ($l -match '^([0-9a-fA-F]{64})\s+(\d+)\s+(.+)$') {
                $info.Archivos[(ConvertTo-RutaSitio $Matches[3])] = @{ Sha = $Matches[1].ToLowerInvariant(); Tamano = [long]$Matches[2] }
            }
            continue
        }
        if ($l -match '^Commit:\s*(\S+)') { $info.Commit = $Matches[1] }
        elseif ($l -match '^Rama:\s*(.*)$') { $info.Rama = $Matches[1].Trim() }
        elseif ($l -match '^Cambios sin confirmar:\s*(.*)$') { $info.CambiosSinConfirmar = $Matches[1].Trim() }
        elseif ($l -match '^Fecha:\s*(.*)$') { $info.Fecha = $Matches[1].Trim() }
    }
    return New-Object PSObject -Property $info
}

# Compara los archivos de una carpeta (objetos { Ruta; Tamano; Sha }) con el manifiesto. Devuelve los
# problemas (texto); vacío si coincide todo.
function Compare-PaqueteConManifiesto {
    param([object[]]$Archivos, [hashtable]$Manifiesto)
    $problemas = @()
    $vistos = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $man = New-Object 'System.Collections.Generic.Dictionary[string,object]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($k in $Manifiesto.Keys) { $man[$k] = $Manifiesto[$k] }
    foreach ($a in @($Archivos)) {
        if (-not $a) { continue }
        $ruta = ConvertTo-RutaSitio $a.Ruta
        [void]$vistos.Add($ruta)
        $m = $null
        if (-not $man.TryGetValue($ruta, [ref]$m)) { $problemas += "no está en el manifiesto: $ruta"; continue }
        if ([string]$m.Sha -ine [string]$a.Sha) { $problemas += "SHA-256 distinto: $ruta" }
    }
    foreach ($k in $man.Keys) { if (-not $vistos.Contains($k)) { $problemas += "falta en la carpeta: $k" } }
    return $problemas
}

# ¿Se permite volver a un paquete? $ContieneCommitContrasenas: $true si el commit del paquete ya incluye
# 773dad0, $false si es anterior, $null si no se pudo saber (sin manifiesto o commit desconocido).
# Cada riesgo exige su propia opción: -Forzar (commit anterior o desconocido) y -SinManifiesto (sin
# manifiesto.txt: no se puede comprobar la integridad). Devuelve { Permitido; Aviso; Faltan }.
function Get-DecisionVolverA {
    param($ContieneCommitContrasenas, [bool]$HayManifiesto = $true, [switch]$Forzar, [switch]$SinManifiesto, [string]$Commit = '773dad0')
    $avisos = @(); $faltan = @()
    if (-not $HayManifiesto) {
        $avisos += "El paquete NO tiene manifiesto.txt: no se puede comprobar que sus archivos estén íntegros ni de qué commit es."
        if (-not $SinManifiesto) { $faltan += '-SinManifiesto' }
    }
    if ($ContieneCommitContrasenas -ne $true) {
        $avisos += $(if ($ContieneCommitContrasenas -eq $false) {
            "El paquete es ANTERIOR al commit $Commit (contraseñas en formato sha256.): con ese código nadie podría iniciar sesión."
        } else {
            "No se pudo comprobar si el paquete incluye el commit $Commit (contraseñas sha256.): si es anterior, nadie podría iniciar sesión."
        })
        if (-not $Forzar) { $faltan += '-Forzar' }
    }
    return [pscustomobject]@{
        Permitido = ($faltan.Count -eq 0)
        Aviso     = $(if ($avisos.Count) { $avisos -join ' ' } else { $null })
        Faltan    = $faltan
    }
}

# La confirmación de borrado es exactamente la palabra BORRAR (mayúsculas, sin nada más salvo espacios).
function Test-ConfirmacionBorrar {
    param([string]$Texto)
    return ($null -ne $Texto) -and ($Texto.Trim() -ceq 'BORRAR')
}

# =================================================================================================
# Utilidades (con disco, git o red)
# =================================================================================================
function Format-Tamano([long]$bytes) {
    if ($bytes -ge 1MB) { return ('{0:N1} MB' -f ($bytes / 1MB)) }
    return ('{0:N0} KB' -f ($bytes / 1KB))
}

# Archivos de una carpeta como objetos { Ruta; Tamano; Sha } (Ruta relativa con '/').
function Get-ArchivosCarpeta([string]$Carpeta, [switch]$ConHash) {
    $raiz = (Resolve-Path -LiteralPath $Carpeta).Path.TrimEnd('\') + '\'
    Get-ChildItem -LiteralPath $raiz -Recurse -File -Force | Sort-Object FullName | ForEach-Object {
        $sha = if ($ConHash) { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() } else { $null }
        New-Object PSObject -Property ([ordered]@{ Ruta = $_.FullName.Substring($raiz.Length).Replace('\', '/'); Tamano = $_.Length; Sha = $sha })
    }
}

# Acepta la carpeta del sitio o la de una publicación (con sitio\ dentro).
function Resolve-CarpetaSitio([string]$Carpeta) {
    $c = (Resolve-Path -LiteralPath $Carpeta).Path
    $sub = Join-Path $c 'sitio'
    if ((Test-Path -LiteralPath $sub -PathType Container) -and -not (Test-Path -LiteralPath (Join-Path $c 'Web.config'))) { return $sub }
    return $c
}

function Invoke-ComprobacionesPaquete([string]$CarpetaSitio) {
    $archivos = @(Get-ArchivosCarpeta $CarpetaSitio)
    $wc = Join-Path $CarpetaSitio 'Web.config'
    $texto = if (Test-Path -LiteralPath $wc) { [IO.File]::ReadAllText($wc) } else { $null }
    $res = @(Test-ContenidoPaquete -Rutas ($archivos | ForEach-Object { $_.Ruta }) -WebConfig $texto)
    Write-Host ""
    Write-Host "Comprobaciones del paquete ($CarpetaSitio):"
    foreach ($r in $res) {
        if ($r.Ok) { Write-Host ("  OK     {0}: {1}" -f $r.Comprobacion, $r.Detalle) -ForegroundColor Green }
        else { Write-Host ("  FALLA  {0}: {1}" -f $r.Comprobacion, $r.Detalle) -ForegroundColor Red }
    }
    return (@($res | Where-Object { -not $_.Ok }).Count -eq 0)
}

# Ejecuta git sin que su stderr (o un código distinto de 0) dispare el trap: con
# $ErrorActionPreference = 'Stop', PowerShell 5.1 convierte el stderr redirigido de un programa en error.
# Devuelve { Salida (líneas); Codigo }.
function Invoke-Git {
    param([string[]]$Argumentos)
    $eap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $salida = & git -C $raizRepo @Argumentos 2>$null
        $codigo = $LASTEXITCODE
    } finally { $ErrorActionPreference = $eap }
    return [pscustomobject]@{ Salida = @($salida | ForEach-Object { [string]$_ }); Codigo = $codigo }
}

function Get-RutaCompleta([string]$Ruta) {
    return [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Ruta)).TrimEnd('\')
}

$raizRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\')
if (-not $ArchivoCredencial) { $ArchivoCredencial = Join-Path $raizRepo 'privado\ftp-respaldo.xml' }

function Assert-FueraDelRepo([string]$Ruta, [string]$Que) {
    if (($Ruta + '\').StartsWith($raizRepo + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Que '$Ruta' está dentro del repositorio. Use una carpeta fuera del repo."
    }
}

# =================================================================================================
# -ComprobarPaquete: solo las comprobaciones
# =================================================================================================
if ($ComprobarPaquete) {
    $sitio = Resolve-CarpetaSitio $ComprobarPaquete
    if (Invoke-ComprobacionesPaquete $sitio) { ""; "Paquete apto."; exit 0 }
    ""; Write-Host "Paquete NO apto: no se debe publicar." -ForegroundColor Red; exit 1
}

if ($BorrarSobrantes -and $SoloListar) { throw "-BorrarSobrantes no tiene sentido con -SoloListar (no se escribe en el servidor)." }

# =================================================================================================
# Carpeta de publicaciones
# =================================================================================================
if ([string]::IsNullOrWhiteSpace($CarpetaPublicaciones)) { $CarpetaPublicaciones = $env:SGV_CARPETA_PUBLICACIONES }
if ([string]::IsNullOrWhiteSpace($CarpetaPublicaciones) -and $VolverA) {
    $CarpetaPublicaciones = Split-Path (Get-RutaCompleta $VolverA)
}
if ([string]::IsNullOrWhiteSpace($CarpetaPublicaciones)) {
    throw "Indique -CarpetaPublicaciones <ruta> o defina la variable de entorno SGV_CARPETA_PUBLICACIONES."
}
$CarpetaPublicaciones = Get-RutaCompleta $CarpetaPublicaciones
Assert-FueraDelRepo $CarpetaPublicaciones 'La carpeta de publicaciones'
New-Item -ItemType Directory -Force $CarpetaPublicaciones | Out-Null
$archivoUltimo = Join-Path $CarpetaPublicaciones 'ultimo-publicado.txt'
$archivoLog    = Join-Path $CarpetaPublicaciones 'publicaciones.log'

$temporalVolver = $null
try {
    if ($VolverA) {
        # =========================================================================================
        # -VolverA: paquete de una publicación anterior
        # =========================================================================================
        $origen = Get-RutaCompleta $VolverA
        if (-not (Test-Path -LiteralPath $origen)) { throw "No existe '$origen'." }
        if ($origen -ilike '*.zip') {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $temporalVolver = Join-Path $env:TEMP ('sgv-volver-' + [guid]::NewGuid().ToString('N'))
            [IO.Compression.ZipFile]::ExtractToDirectory($origen, $temporalVolver)
            $dirPublicacion = $temporalVolver
        } else {
            $dirPublicacion = $origen
        }
        $sitio = Join-Path $dirPublicacion 'sitio'
        $rutaManifiesto = Join-Path $dirPublicacion 'manifiesto.txt'
        if (-not (Test-Path -LiteralPath $sitio -PathType Container)) { throw "'$origen' no es una publicación de este script (falta sitio\)." }
        $man = $null; $contiene = $null
        if (Test-Path -LiteralPath $rutaManifiesto) {
            $man = ConvertFrom-Manifiesto ([IO.File]::ReadAllLines($rutaManifiesto))
            if ($man.Commit) {
                # 0 = lo contiene, 1 = es anterior, 128 = commit desconocido en este clon ($null: no se sabe).
                $eap = $ErrorActionPreference
                $ErrorActionPreference = 'Continue'
                try {
                    & git -C $raizRepo merge-base --is-ancestor $CommitContrasenasSha256 $man.Commit 2>$null | Out-Null
                    $codigoAncestro = $LASTEXITCODE
                } finally { $ErrorActionPreference = $eap }
                $contiene = ConvertFrom-CodigoAncestro $codigoAncestro
            }
        }
        "Volver a: $origen"
        "  commit $(if ($man) { $man.Commit } else { '(sin manifiesto)' }), rama $(if ($man) { $man.Rama }), fecha $(if ($man) { $man.Fecha })"
        $decision = Get-DecisionVolverA -ContieneCommitContrasenas $contiene -HayManifiesto ($null -ne $man) -Forzar:$Forzar -SinManifiesto:$SinManifiesto -Commit $CommitContrasenasSha256
        if ($decision.Aviso) { Write-Host "AVISO: $($decision.Aviso)" -ForegroundColor Yellow }
        if (-not $decision.Permitido) { throw "No se vuelve a ese paquete sin $($decision.Faltan -join ' y ')." }
        if ($man) {
            $problemas = @(Compare-PaqueteConManifiesto -Archivos @(Get-ArchivosCarpeta $sitio -ConHash) -Manifiesto $man.Archivos)
            if ($problemas.Count) { throw "El paquete no coincide con su manifiesto: $(($problemas | Select-Object -First 5) -join '; ')" }
            "  los $($man.Archivos.Count) archivos coinciden con el manifiesto (SHA-256)."
        }
        $SubirTodo = $true      # volver atrás sube el paquete completo
        $nombrePublicacion = [IO.Path]::GetFileName($origen) -replace '\.zip$', ''
        $commit = if ($man -and $man.Commit) { $man.Commit } else { '(desconocido)' }
    } else {
        # =========================================================================================
        # Compilar y publicar a carpeta
        # =========================================================================================
        $g = Invoke-Git @('rev-parse', 'HEAD')
        if ($g.Codigo -ne 0 -or -not $g.Salida) { throw "No se pudo leer el commit actual con git (código $($g.Codigo)). ¿Está git instalado y es un clon del repo?" }
        $commit = $g.Salida[0].Trim()
        $corto  = $commit.Substring(0, 7)
        $g = Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD')
        $rama   = if ($g.Codigo -eq 0 -and $g.Salida) { $g.Salida[0].Trim() } else { '(desconocida)' }
        # Cambios que afectan al paquete: lo que se compila o publica (WebSGV, la solución, los paquetes NuGet).
        $g = Invoke-Git @('status', '--porcelain', '--untracked-files=all', '--', 'WebSGV', 'WebSGV.sln', 'packages')
        if ($g.Codigo -ne 0) { throw "git status falló (código $($g.Codigo)): no se puede saber si hay cambios sin confirmar." }
        $cambios = @($g.Salida | Where-Object { $_ })
        if ($cambios.Count -and -not $PermitirCambiosSinConfirmar) {
            throw "Hay $($cambios.Count) cambio(s) sin confirmar en WebSGV/ (p. ej. $($cambios[0].Trim())). Confirme (commit) antes de publicar o use -PermitirCambiosSinConfirmar."
        }
        $nombrePublicacion = (Get-Date).ToString('yyyyMMdd_HHmm') + '_' + $corto
        $dirPublicacion = Join-Path $CarpetaPublicaciones $nombrePublicacion
        $n = 2
        while (Test-Path -LiteralPath $dirPublicacion) { $dirPublicacion = Join-Path $CarpetaPublicaciones "$nombrePublicacion`_$n"; $n++ }
        $nombrePublicacion = Split-Path $dirPublicacion -Leaf
        $sitio = Join-Path $dirPublicacion 'sitio'
        New-Item -ItemType Directory -Force $sitio | Out-Null

        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (-not (Test-Path $vswhere)) { throw "No se encontró vswhere.exe (Visual Studio)." }
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not $msbuild) { throw "No se encontró MSBuild de Visual Studio." }
        if (-not (Test-Path (Join-Path $raizRepo 'packages\QuestPDF.2026.5.0'))) { throw "Faltan los paquetes NuGet: ejecute 'nuget restore WebSGV.sln' antes." }

        "Publicación: $nombrePublicacion (commit $corto, rama $rama$(if ($cambios.Count) { ", $($cambios.Count) cambio(s) sin confirmar" }))"
        "Compilando y publicando a $sitio ..."
        # Archivo de respuesta: deja pasar ';' y rutas con espacios sin pelear con las comillas de PowerShell.
        $rsp = Join-Path $env:TEMP ("sgv-publicar-" + [guid]::NewGuid().ToString('N') + '.rsp')
        $argumentos = @(
            "`"$(Join-Path $raizRepo 'WebSGV\WebSGV.csproj')`""
            '/nologo', '/verbosity:minimal', '/t:Build'
            '/p:Configuration=Release', '/p:Platform=AnyCPU'
            '/p:DeployOnBuild=true', '/p:DeployDefaultTarget=WebPublish', '/p:WebPublishMethod=FileSystem', '/p:PublishProvider=FileSystem'
            "/p:publishUrl=`"$sitio`""
            '/p:DeleteExistingFiles=true', '/p:ExcludeApp_Data=true', '/p:ExcludeGeneratedDebugSymbol=true'
            '/p:ExcludeFilesFromDeployment="connectionStrings.config;appSettings.Secrets.config"'
        )
        [IO.File]::WriteAllLines($rsp, [string[]]$argumentos, (New-Object Text.UTF8Encoding($false)))
        $eap = $ErrorActionPreference
        try {
            # Continue: si MSBuild escribe en stderr (o la salida de este script se redirige), que no lo
            # convierta en excepción el trap; el resultado lo dice el código de salida.
            $ErrorActionPreference = 'Continue'
            & $msbuild "@$rsp"
            $codigoMsbuild = $LASTEXITCODE
            $ErrorActionPreference = $eap
            if ($codigoMsbuild -ne 0) { throw "La compilación/publicación falló (MSBuild código $codigoMsbuild). La carpeta $dirPublicacion queda incompleta." }
        } finally {
            $ErrorActionPreference = $eap
            Remove-Item -LiteralPath $rsp -Force -ErrorAction SilentlyContinue
        }

        # Manifiesto (fuera de sitio\, para no subirlo).
        $archivos = @(Get-ArchivosCarpeta $sitio -ConHash)
        $lineas = @(
            'SGV - manifiesto de publicación'
            "Commit: $commit"
            "Rama: $rama"
            "Cambios sin confirmar: $(if ($cambios.Count) { 'SÍ (' + $cambios.Count + ')' } else { 'no' })"
            "Fecha: $((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))"
            "Equipo: $env:COMPUTERNAME"
            'Configuración: Release'
        )
        if ($cambios.Count) { $lineas += ($cambios | ForEach-Object { "  $_" }) }
        $lineas += "Archivos: $($archivos.Count) ($(Format-Tamano (($archivos | Measure-Object Tamano -Sum).Sum)))"
        $lineas += '--- Archivos (SHA-256  bytes  ruta) ---'
        $lineas += ($archivos | ForEach-Object { '{0}  {1}  {2}' -f $_.Sha, $_.Tamano, $_.Ruta })
        [IO.File]::WriteAllLines((Join-Path $dirPublicacion 'manifiesto.txt'), [string[]]$lineas, (New-Object Text.UTF8Encoding($true)))

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = "$dirPublicacion.zip"
        [IO.Compression.ZipFile]::CreateFromDirectory($dirPublicacion, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
        ""
        "Paquete : $dirPublicacion ($($archivos.Count) archivos)"
        "Zip     : $zip ($(Format-Tamano (Get-Item -LiteralPath $zip).Length))"
        "Manifiesto: $(Join-Path $dirPublicacion 'manifiesto.txt')"
    }

    # =============================================================================================
    # Comprobaciones del paquete
    # =============================================================================================
    if (-not (Invoke-ComprobacionesPaquete $sitio)) {
        Write-Host "Paquete NO apto: no se compara ni se sube nada." -ForegroundColor Red
        exit 1
    }

    # =============================================================================================
    # Servidor (solo lectura)
    # =============================================================================================
    if (-not (Test-Path -LiteralPath $ArchivoCredencial)) {
        ""
        Write-Host "FALTA LA CREDENCIAL FTP ($ArchivoCredencial): no se comparó con el servidor ni se subió nada." -ForegroundColor Yellow
        Write-Host "Guárdela una vez con: .\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial" -ForegroundColor Yellow
        # Vista previa sin servidor: los pasos si hubiera que subir el paquete completo.
        $pasosPrevia = @(Get-PasosSubida -Rutas @(Get-ArchivosCarpeta $sitio | ForEach-Object { $_.Ruta }))
        $textoPrevia = @($pasosPrevia | ForEach-Object { if ($_.Paso -in 'Contenido', 'Bin', 'WebConfig') { "$($_.Paso) ($($_.Rutas.Count))" } else { $_.Paso } }) -join ' -> '
        "Pasos de la subida si se subiera todo el paquete: $textoPrevia"
        if ($SoloListar) { exit 0 } else { exit 1 }
    }
    $cred = Read-CredencialFtp -Archivo $ArchivoCredencial -ComandoGuardar '.\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial'
    $con = New-ConexionFtp -Servidor $Servidor -RutaSitio $RutaSitio -Credencial $cred.GetNetworkCredential()
    ""
    "Listando el servidor $($con.Base) (sin App_Data ni Uploads) ..."
    $listado = Get-ListadoFtpRecursivo -Conexion $con -Carpeta '' -Omitir { param($rel) Test-RutaProtegida $rel }
    foreach ($e in $listado.Errores) { Write-Host $e -ForegroundColor Red }
    if ($listado.Errores.Count) {
        Write-Host "No se pudo listar todo el servidor: no se sube ni se borra nada." -ForegroundColor Red
        exit 1
    }

    # Marcador del servidor (solo lectura, RETR) contra ultimo-publicado.txt de esta máquina.
    $marcador = $null
    try { $marcador = ConvertFrom-Marcador (Read-TextoFtp $con (Get-UrlFtp $con 'publicacion.txt') 'RETR') } catch { $marcador = $null }
    $hashesPublicados = $null
    $paqueteUltimo = $null; $commitUltimo = $null; $mu = $null
    if (Test-Path -LiteralPath $archivoUltimo) {
        $dirUltimo = ([string](Get-Content -LiteralPath $archivoUltimo -TotalCount 1)).Trim()
        $manUltimo = if ($dirUltimo) { Join-Path $dirUltimo 'manifiesto.txt' } else { $null }
        if ($manUltimo -and (Test-Path -LiteralPath $manUltimo)) {
            $mu = ConvertFrom-Manifiesto ([IO.File]::ReadAllLines($manUltimo))
            $paqueteUltimo = Split-Path $dirUltimo -Leaf
            $commitUltimo = $mu.Commit
        }
    }
    ""
    "Marcador del servidor (publicacion.txt): $(if ($marcador) { "$($marcador.Paquete), commit $($marcador.Commit)" } else { 'no existe o no se pudo leer' })"
    "Última publicación de esta máquina    : $(if ($paqueteUltimo) { "$paqueteUltimo, commit $commitUltimo" } else { 'sin registro' })"
    # Fechas (MDTM) del marcador y de la DLL: si falla cualquiera, $null -> no se confía (lo seguro).
    $fechaMarcador = $null; $fechaDll = $null
    if ($marcador -and -not $marcador.EnCurso) {
        try { $fechaMarcador = Get-FechaModificacionFtp $con (Get-UrlFtp $con 'publicacion.txt') } catch { $fechaMarcador = $null }
        try { $fechaDll = Get-FechaModificacionFtp $con (Get-UrlFtp $con 'bin/WebSGV.dll') } catch { $fechaDll = $null }
    }
    $confianza = Get-ConfianzaHashes -Marcador $marcador -Paquete $paqueteUltimo -Commit $commitUltimo -FechaMarcador $fechaMarcador -FechaDll $fechaDll
    if ($mu -and $confianza.Confiar) {
        $hashesPublicados = @{}
        foreach ($k in $mu.Archivos.Keys) { $hashesPublicados[$k] = $mu.Archivos[$k].Sha }
        "  Se confía en lo publicado ($($confianza.Motivo)): se saltan los archivos de mismo tamaño y mismo SHA-256."
    } elseif (-not $SubirTodo) {
        Write-Host "  No se confía en lo publicado: $($confianza.Motivo). Todo lo de mismo tamaño se trata como 'sin confirmar' y se sube (como -SubirTodo)." -ForegroundColor Yellow
    }
    $mantenimientoEnServidor = @($listado.Archivos | Where-Object { $_.Ruta -ieq 'app_offline.htm' }).Count -gt 0
    if ($mantenimientoEnServidor) {
        Write-Host "  AVISO: el servidor tiene app_offline.htm (sitio en mantenimiento, quizá de una subida fallida). Se quitará al terminar bien." -ForegroundColor Yellow
    }
    $paquete = @(Get-ArchivosCarpeta $sitio -ConHash)
    $plan = @(Get-PlanPublicacion -Paquete $paquete -Remotos $listado.Archivos -HashesPublicados $hashesPublicados -BorrarSobrantes:$BorrarSobrantes -SubirTodo:$SubirTodo)
    $aSubir    = @($plan | Where-Object { $_.Accion -eq 'Subir' } | Sort-Object @{ e = { Get-OrdenSubida $_.Ruta } }, Ruta)
    $sobrantes = @($plan | Where-Object { $_.Accion -eq 'Sobrante' -or $_.Accion -eq 'Borrar' } | Sort-Object Ruta)
    $iguales   = @($plan | Where-Object { $_.Accion -eq 'Igual' }).Count

    ""
    "En el servidor: $($listado.Archivos.Count) archivo(s) fuera de App_Data/Uploads. En el paquete: $($paquete.Count)."
    $cambiados = @($aSubir | Where-Object { $_.Motivo -eq 'nuevo' -or $_.Motivo -eq 'tamaño distinto' })
    "(a) Se subirían $($aSubir.Count) archivo(s) ($(Format-Tamano (($aSubir | Measure-Object Tamano -Sum).Sum))); sin cambios: $iguales."
    "    Nuevos o con tamaño distinto: $($cambiados.Count)"
    $cambiados | ForEach-Object { "      {0,-16} {1}" -f $_.Motivo, $_.Ruta }
    $sinConf = @($aSubir | Where-Object { $_.Motivo -eq 'sin confirmar' -or $_.Motivo -eq 'forzado' }).Count
    if ($sinConf) { "    Mismo tamaño pero sin poder confirmar que son iguales (se suben igual): $sinConf" }
    ""
    "(b) Sobrantes en el servidor (no están en el paquete): $($sobrantes.Count)"
    $sobrantes | ForEach-Object { "      $($_.Ruta)" }
    if ($sobrantes.Count -and -not $BorrarSobrantes) { "    (se borran solo con -BorrarSobrantes)" }
    ""
    "Pasos de la subida: " + ((@(Get-PasosSubida -Rutas ($aSubir | ForEach-Object { $_.Ruta }) -MantenimientoEnServidor:$mantenimientoEnServidor) |
        ForEach-Object { if ($_.Paso -in 'Contenido', 'Bin', 'WebConfig') { "$($_.Paso) ($($_.Rutas.Count))" } else { $_.Paso } }) -join ' -> ')

    if ($SoloListar) { ""; "(-SoloListar: no se escribió nada en el servidor)"; exit 0 }

    # =============================================================================================
    # Escritura en el servidor (STOR / DELE). Solo sin -SoloListar.
    # =============================================================================================
    $errores = 0
    $subidos = 0
    $carpetasCreadas = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

    # Sube bytes a una ruta del sitio (crea las carpetas que falten) y comprueba el tamaño final.
    function Send-ArchivoSitio([string]$Ruta, [byte[]]$Bytes) {
        if ((Test-RutaProtegida $Ruta)) { throw "ruta protegida: no se escribe en $Ruta" }   # defensa extra
        $segs = $Ruta.Split('/'); $acum = ''
        for ($k = 0; $k -lt $segs.Count - 1; $k++) {
            $acum = if ($acum) { "$acum/$($segs[$k])" } else { $segs[$k] }
            if ($carpetasCreadas.Add($acum)) {
                try { (New-PeticionFtp $con (Get-UrlFtp $con $acum) 'MKD').GetResponse().Close() } catch { }   # ya existe
            }
        }
        $url = Get-UrlFtp $con $Ruta
        Invoke-ConReintento {
            $req = New-PeticionFtp $con $url 'STOR'
            $req.ContentLength = $Bytes.Length
            $s = $req.GetRequestStream(); try { $s.Write($Bytes, 0, $Bytes.Length) } finally { $s.Close() }
            $req.GetResponse().Close()
            $tam = Get-TamanoFtp $con $url
            if ($tam -ne $Bytes.Length) { throw "en el servidor quedaron $tam bytes de $($Bytes.Length)" }
        } | Out-Null
    }
    function Remove-ArchivoSitio([string]$Ruta) {
        (New-PeticionFtp $con (Get-UrlFtp $con $Ruta) 'DELE').GetResponse().Close()
    }

    $textoMantenimiento = '<!DOCTYPE html><html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>SGV - Actualizando</title></head>' +
        '<body style="font-family:Segoe UI,Arial,sans-serif;text-align:center;padding:48px 16px;color:#333">' +
        '<h1 style="font-size:1.4em">Actualizando el sistema, vuelve en unos minutos.</h1>' +
        '<p>SGV - Sistema de Gestión de Viajes</p></body></html>'
    $pasos = @(Get-PasosSubida -Rutas ($aSubir | ForEach-Object { $_.Ruta }) -MantenimientoEnServidor:$mantenimientoEnServidor)
    $accion = {
        param($paso, $ruta)
        switch ($paso) {
            'MarcadorEnCurso' {
                Send-ArchivoSitio 'publicacion.txt' ([Text.Encoding]::UTF8.GetBytes((ConvertTo-TextoMarcadorEnCurso -Paquete $nombrePublicacion -Fecha (Get-Date) -Equipo $env:COMPUTERNAME)))
                "Marcador publicacion.txt = EN CURSO (si algo falla, la próxima publicación no confiará en lo publicado)."
            }
            'Mantenimiento' {
                Send-ArchivoSitio 'app_offline.htm' ([Text.Encoding]::UTF8.GetBytes($textoMantenimiento))
                "app_offline.htm puesto: el sitio muestra 'Actualizando el sistema' durante la subida."
            }
            'QuitarMantenimiento' { Remove-ArchivoSitio 'app_offline.htm'; "app_offline.htm quitado: el sitio vuelve a atender." }
            default { Send-ArchivoSitio $ruta ([IO.File]::ReadAllBytes((Join-Path $sitio ($ruta.Replace('/', '\'))))) }
        }
    }
    $resultado = Invoke-PasosSubida -Pasos $pasos -Accion $accion -MantenimientoEnServidor:$mantenimientoEnServidor
    $errores = $resultado.Errores
    $subidos = $resultado.Subidos
    $mantenimientoPuesto = $resultado.MantenimientoPuesto
    "Subidos en total: $subidos de $($aSubir.Count)."

    $borrados = 0
    if ($BorrarSobrantes -and $sobrantes.Count) {
        if ($errores) {
            Write-Host "Hubo errores al subir: no se borra ningún sobrante." -ForegroundColor Red
        } else {
            ""
            "Se borrarán $($sobrantes.Count) sobrante(s) del servidor (lista arriba). App_Data, Uploads y secretos no se tocan."
            $resp = Read-Host "Escriba BORRAR para confirmar"
            if (Test-ConfirmacionBorrar $resp) {
                foreach ($s in $sobrantes) {
                    if ((Test-RutaProtegida $s.Ruta) -or -not (Test-RutaCandidataSobrante $s.Ruta)) { continue }
                    try { Remove-ArchivoSitio $s.Ruta; $borrados++ }
                    catch { $errores++; Write-Host "ERROR al borrar $($s.Ruta) : $($_.Exception.Message)" -ForegroundColor Red }
                }
                "Borrados: $borrados de $($sobrantes.Count) (las carpetas vacías quedan)."
            } else {
                "No se escribió BORRAR: no se borró nada."
            }
        }
    }

    # Marcador: solo si todo salió bien (así otra máquina sabe qué hay publicado).
    if (-not $errores) {
        try {
            Send-ArchivoSitio 'publicacion.txt' ([Text.Encoding]::UTF8.GetBytes((ConvertTo-TextoMarcador -Paquete $nombrePublicacion -Commit $commit -Fecha (Get-Date) -Equipo $env:COMPUTERNAME)))
            "Marcador publicacion.txt actualizado ($nombrePublicacion, commit $commit)."
        } catch {
            $errores++
            Write-Host "ERROR al subir el marcador publicacion.txt : $($_.Exception.Message) (el sitio quedó publicado; la próxima vez se subirá todo lo de mismo tamaño)." -ForegroundColor Red
        }
    }

    $estado = if ($errores) { 'ERROR' } else { 'OK' }
    Add-Content -LiteralPath $archivoLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  {1}  {2}  subidos {3}/{4}  borrados {5}  errores {6}{7}{8}' -f
        (Get-Date), $estado, $nombrePublicacion, $subidos, $aSubir.Count, $borrados, $errores,
        $(if ($VolverA) { '  (vuelta atrás)' } else { '' }), $(if ($mantenimientoPuesto) { '  APP_OFFLINE PUESTO' } else { '' }))
    if ($mantenimientoPuesto) {
        ""
        Write-Host "ATENCIÓN: app_offline.htm SIGUE PUESTO en el servidor: el sitio está EN MANTENIMIENTO (muestra 'Actualizando el sistema')." -ForegroundColor Red
        Write-Host "Repita la publicación (o use -VolverA con la versión anterior); al terminar bien se quita solo." -ForegroundColor Red
    }
    if ($errores -and $resultado.PasoFallido -ne 'MarcadorEnCurso') {
        Write-Host "El marcador publicacion.txt queda 'EN CURSO': la próxima publicación subirá todo lo de mismo tamaño." -ForegroundColor Yellow
    }
    if ($errores) { exit 1 }
    if ($VolverA -and $temporalVolver) { Remove-Item -LiteralPath $archivoUltimo -Force -ErrorAction SilentlyContinue }   # zip extraído a TEMP: no hay carpeta que registrar
    else { Set-Content -LiteralPath $archivoUltimo -Encoding UTF8 -Value $dirPublicacion }
    "Publicación terminada: $nombrePublicacion"
    exit 0
} finally {
    if ($temporalVolver) { Remove-Item -LiteralPath $temporalVolver -Recurse -Force -ErrorAction SilentlyContinue }
}
