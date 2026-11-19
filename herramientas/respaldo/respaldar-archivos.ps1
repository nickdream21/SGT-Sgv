<#
.SYNOPSIS
    Espejo incremental de los archivos del sitio publicado (Uploads y PDF firmados de
    App_Data\OrdenesViaje) hacia una carpeta propia, fuera de somee y fuera del repo.

.DESCRIPTION
    Baja por FTP (solo LIST/NLST/SIZE/RETR: nunca escribe, renombra ni borra en el servidor):
        <RutaSitio>/Uploads               -> <Carpeta>\archivos\Uploads\...
        <RutaSitio>/App_Data/OrdenesViaje -> <Carpeta>\archivos\OrdenesViaje\...
    Se excluye toda carpeta llamada _temp (borradores, p. ej. Uploads\Factura\_temp).

    Incremental: baja solo lo que no existe en local o cuyo tamaño difiere. Nunca borra nada en
    local: un archivo borrado en el servidor sigue en el respaldo. Si un archivo cambió de tamaño,
    la versión local anterior se conserva como <nombre>__anterior_aaaaMMdd_HHmmss.<ext> antes de
    poner la nueva. Cada archivo se baja a <nombre>.descargando y se renombra solo si llegó entero
    (mismo tamaño que en el servidor); ante un error de red se reintenta una vez.

    Al final llama a verificar-pdf-firmados.ps1 sobre <Carpeta>\archivos\OrdenesViaje (SHA-256
    contra la BD) y añade una línea a <Carpeta>\archivos\respaldos.log.

    Credencial: la contraseña FTP se guarda una sola vez con -GuardarCredencial en
    privado\ftp-respaldo.xml (Export-Clixml: cifrada con DPAPI, solo la puede leer el mismo usuario
    de Windows en la misma máquina). Repetir -GuardarCredencial tras rotar la contraseña FTP.
    La contraseña nunca se imprime. Ojo: el FTP de somee no admite TLS, así que viaja sin cifrar
    por la red, igual que al publicar.

    Código de salida: 0 todo bien; 1 hubo errores de descarga o de listado; 2 la descarga fue bien
    pero la verificación de PDF firmados encontró faltantes o distintos (o no se pudo hacer).

.EXAMPLE
    .\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial          # una vez por máquina/usuario
    .\herramientas\respaldo\respaldar-archivos.ps1 -Carpeta D:\RespaldosSGV -SoloListar
    .\herramientas\respaldo\respaldar-archivos.ps1 -Carpeta D:\RespaldosSGV
    .\herramientas\respaldo\respaldar-archivos.ps1                              # usa $env:SGV_CARPETA_RESPALDO
#>
param(
    [string]$Carpeta,
    [switch]$SoloListar,
    [switch]$GuardarCredencial,

    [string]$Usuario   = 'SGVTRANSPORTES',
    [string]$Servidor  = 'ftp://sgvtransporte.somee.com',
    [string]$RutaSitio = '/www.sgvtransporte.somee.com'
)

$ErrorActionPreference = 'Stop'

# FTP común (listado Unix/IIS, NLST+SIZE, credencial DPAPI): herramientas\comun\Ftp.psm1.
Import-Module (Join-Path $PSScriptRoot '..\comun\Ftp.psm1') -Force -DisableNameChecking

# =================================================================================================
# Funciones puras (sin red ni BD): probar-espejo.ps1 las extrae de este archivo para probarlas.
# =================================================================================================

# $true si algún segmento de la ruta es _temp (borradores que no se respaldan).
function Test-RutaExcluida {
    param([string]$Ruta)
    foreach ($s in ($Ruta -split '[/\\]')) { if ($s -ieq '_temp') { return $true } }
    return $false
}

# Plan del espejo. $Remotos: objetos con Ruta ('Uploads/x/y.pdf') y Tamano (-1 si se desconoce).
# $Locales: hashtable Ruta -> tamaño de la copia local (la clave usa '/' y no distingue mayúsculas).
# Devuelve un objeto por remoto con Accion: Bajar (no existe en local), Reemplazar (tamaño distinto:
# se guarda la versión anterior), Igual o Excluido (_temp). Los archivos solo locales no aparecen:
# el espejo nunca borra.
function Get-PlanEspejo {
    param([object[]]$Remotos, [hashtable]$Locales)
    $idx = New-Object 'System.Collections.Generic.Dictionary[string,long]' ([StringComparer]::OrdinalIgnoreCase)
    if ($Locales) { foreach ($k in $Locales.Keys) { $idx[([string]$k).Replace('\', '/')] = [long]$Locales[$k] } }
    $plan = @()
    foreach ($r in $Remotos) {
        $ruta = ([string]$r.Ruta).Replace('\', '/')
        $tam = [long]$r.Tamano
        $local = -1L
        $existe = $idx.TryGetValue($ruta, [ref]$local)
        $accion = if (Test-RutaExcluida $ruta) { 'Excluido' }
                  elseif (-not $existe) { 'Bajar' }
                  elseif ($tam -lt 0 -or $local -eq $tam) { 'Igual' }
                  else { 'Reemplazar' }
        $plan += New-Object PSObject -Property ([ordered]@{
            Ruta = $ruta; Tamano = $tam; TamanoLocal = $(if ($existe) { $local } else { -1L }); Accion = $accion })
    }
    return $plan
}

# Nombre con que se conserva la versión anterior de un archivo que cambió en el servidor.
function Get-NombreVersionAnterior {
    param([string]$Nombre, [datetime]$Fecha, [int]$Contador = 1)
    $ext = [IO.Path]::GetExtension($Nombre)
    $base = if ($ext) { $Nombre.Substring(0, $Nombre.Length - $ext.Length) } else { $Nombre }
    $sufijo = '__anterior_' + $Fecha.ToString('yyyyMMdd_HHmmss', [Globalization.CultureInfo]::InvariantCulture)
    if ($Contador -gt 1) { $sufijo += '_' + $Contador }
    return $base + $sufijo + $ext
}

# Pone en su sitio un archivo ya descargado completo ($Temporal). Si $Final existe, lo renombra antes
# a su versión anterior (nunca lo borra). Devuelve el nombre de la versión anterior o $null.
function Move-DescargaASuSitio {
    param([string]$Temporal, [string]$Final, [datetime]$Fecha)
    $anterior = $null
    if (Test-Path -LiteralPath $Final) {
        $dir = Split-Path $Final
        $n = 1
        do {
            $anterior = Get-NombreVersionAnterior -Nombre (Split-Path $Final -Leaf) -Fecha $Fecha -Contador $n
            $n++
        } while (Test-Path -LiteralPath (Join-Path $dir $anterior))
        Move-Item -LiteralPath $Final -Destination (Join-Path $dir $anterior)
    }
    Move-Item -LiteralPath $Temporal -Destination $Final
    return $anterior
}

# =================================================================================================
# Utilidades
# =================================================================================================
function Format-Tamano([long]$bytes) {
    if ($bytes -ge 1MB) { return ('{0:N1} MB' -f ($bytes / 1MB)) }
    return ('{0:N0} KB' -f ($bytes / 1KB))
}

$raizRepo      = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')).TrimEnd('\') + '\'
$dirPrivado    = Join-Path $raizRepo 'privado'
$archivoCred   = Join-Path $dirPrivado 'ftp-respaldo.xml'
$cmdGuardar    = '.\herramientas\respaldo\respaldar-archivos.ps1 -GuardarCredencial'

# Lista recursivamente una carpeta remota (ruta relativa al sitio, p. ej. 'App_Data/OrdenesViaje'),
# sin entrar en _temp. Agrega a $script:remotos objetos { Ruta = '<prefijoLocal>/<rel>'; Tamano; Sitio }.
function Add-ListadoRemoto([string]$rutaSitio, [string]$prefijoLocal) {
    $l = Get-ListadoFtpRecursivo -Conexion $script:con -Carpeta $rutaSitio -Omitir { param($rel) Test-RutaExcluida $rel }
    foreach ($e in $l.Errores) {
        $script:errores++
        Write-Host $e -ForegroundColor Red
    }
    foreach ($a in $l.Archivos) {
        $script:remotos.Add((New-Object PSObject -Property @{ Ruta = "$prefijoLocal/$($a.Ruta)"; Tamano = $a.Tamano; Sitio = "$rutaSitio/$($a.Ruta)" }))
    }
}

# =================================================================================================
# -GuardarCredencial
# =================================================================================================
if ($GuardarCredencial) {
    $ignorado = $false
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git) {
        & git -C $raizRepo check-ignore -q 'privado/ftp-respaldo.xml' 2>$null
        $ignorado = ($LASTEXITCODE -eq 0)
    } else {
        $gi = Join-Path $raizRepo '.gitignore'
        $ignorado = (Test-Path $gi) -and [bool](Select-String -Path $gi -Pattern '^\s*/?privado/?\s*$' -Quiet)
    }
    if (-not $ignorado) {
        throw "privado/ no está en .gitignore: no se guarda la credencial (se podría subir al repo). Agregue '/privado/' a .gitignore."
    }
    $sec = Read-Host "Contraseña FTP de $Usuario" -AsSecureString
    if ($sec.Length -eq 0) { throw "Contraseña vacía: no se guardó nada." }
    $cred = New-Object System.Management.Automation.PSCredential($Usuario, $sec)
    New-Item -ItemType Directory -Force $dirPrivado | Out-Null
    $cred | Export-Clixml -Path $archivoCred
    "Credencial guardada en $archivoCred (cifrada para el usuario de Windows $env:USERDOMAIN\$env:USERNAME)."

    # Prueba de conexión (solo lista la raíz del sitio).
    $script:con = New-ConexionFtp -Servidor $Servidor -RutaSitio $RutaSitio -Credencial $cred.GetNetworkCredential()
    $base = $script:con.Base
    try {
        Read-TextoFtp $script:con ($base + '/') 'NLST' | Out-Null
        "Conexión FTP comprobada: OK."
    } catch {
        Write-Host "AVISO: la credencial se guardó, pero no se pudo listar $base : $($_.Exception.Message)" -ForegroundColor Yellow
        Write-Host "Si la contraseña es incorrecta, repita $cmdGuardar" -ForegroundColor Yellow
    }
    exit 0
}

# =================================================================================================
# Validaciones (antes de crear nada)
# =================================================================================================
if ([string]::IsNullOrWhiteSpace($Carpeta)) { $Carpeta = $env:SGV_CARPETA_RESPALDO }
if ([string]::IsNullOrWhiteSpace($Carpeta)) {
    throw "Indique -Carpeta <ruta> o defina la variable de entorno SGV_CARPETA_RESPALDO."
}
$carpetaCompleta = [IO.Path]::GetFullPath($ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Carpeta)).TrimEnd('\') + '\'
if ($carpetaCompleta.StartsWith($raizRepo, [StringComparison]::OrdinalIgnoreCase)) {
    throw "La carpeta '$($carpetaCompleta.TrimEnd('\'))' está dentro del repositorio. Use una carpeta fuera del repo (el respaldo contiene datos reales)."
}
$carpetaArchivos = Join-Path $carpetaCompleta.TrimEnd('\') 'archivos'
$rutaLog = Join-Path $carpetaArchivos 'respaldos.log'

$cred = Read-CredencialFtp -Archivo $archivoCred -ComandoGuardar $cmdGuardar
$script:con = New-ConexionFtp -Servidor $Servidor -RutaSitio $RutaSitio -Credencial $cred.GetNetworkCredential()
$base = $script:con.Base

$origenes = @(
    @{ Sitio = 'Uploads';               Local = 'Uploads' }
    @{ Sitio = 'App_Data/OrdenesViaje'; Local = 'OrdenesViaje' }
)

"Servidor: $base (usuario $($cred.UserName))"
"Destino : $carpetaArchivos"

# =================================================================================================
# Listado remoto y plan
# =================================================================================================
$inicio = Get-Date
$script:errores = 0
$script:remotos = New-Object System.Collections.Generic.List[object]
foreach ($o in $origenes) {
    "Listando /$($o.Sitio) ..."
    Add-ListadoRemoto $o.Sitio $o.Local
}

$locales = @{}
foreach ($o in $origenes) {
    $dirLocal = Join-Path $carpetaArchivos $o.Local
    if (-not (Test-Path -LiteralPath $dirLocal)) { continue }
    $raizLocal = $dirLocal.TrimEnd('\') + '\'
    Get-ChildItem -LiteralPath $dirLocal -Recurse -File |
        Where-Object { $_.Name -notmatch '__anterior_\d{8}_\d{6}' -and $_.Name -notlike '*.descargando' } |
        ForEach-Object { $locales[$o.Local + '/' + $_.FullName.Substring($raizLocal.Length).Replace('\', '/')] = $_.Length }
}

$plan = @(Get-PlanEspejo -Remotos $script:remotos.ToArray() -Locales $locales)
$porSitio = @{}; foreach ($r in $script:remotos) { $porSitio[$r.Ruta] = $r.Sitio }
$aBajar = @($plan | Where-Object { $_.Accion -eq 'Bajar' -or $_.Accion -eq 'Reemplazar' })
$iguales = @($plan | Where-Object { $_.Accion -eq 'Igual' }).Count
$revisados = @($plan | Where-Object { $_.Accion -ne 'Excluido' }).Count

if ($SoloListar) {
    ""
    "En el servidor: $revisados archivo(s). Sin cambios: $iguales. Por bajar: $($aBajar.Count) ($(Format-Tamano (($aBajar | Measure-Object Tamano -Sum).Sum)))."
    $aBajar | Group-Object { $_.Ruta.Substring(0, [Math]::Max(0, $_.Ruta.LastIndexOf('/'))) } |
        Sort-Object Name | ForEach-Object { "{0,5}  {1}" -f $_.Count, $_.Name }
    $reemp = @($aBajar | Where-Object { $_.Accion -eq 'Reemplazar' })
    if ($reemp.Count) {
        ""
        "Cambiaron de tamaño (se guardaría la versión anterior):"
        $reemp | ForEach-Object { "  {0}  local {1} -> servidor {2}" -f $_.Ruta, $_.TamanoLocal, $_.Tamano }
    }
    if ($script:errores) { Write-Host "Hubo $($script:errores) error(es) al listar." -ForegroundColor Red }
    "(-SoloListar: no se bajó ni se creó nada)"
    if ($script:errores) { exit 1 }
    exit 0
}

# =================================================================================================
# Descarga
# =================================================================================================
New-Item -ItemType Directory -Force $carpetaArchivos | Out-Null
$bajados = 0; $reemplazados = 0; $bytesBajados = 0L
$i = 0
foreach ($p in $aBajar) {
    $i++
    $final = Join-Path $carpetaArchivos ($p.Ruta.Replace('/', '\'))
    $temporal = "$final.descargando"
    $url = Get-UrlFtp $script:con $porSitio[$p.Ruta]
    try {
        New-Item -ItemType Directory -Force (Split-Path $final) | Out-Null
        Invoke-ConReintento {
            if (Test-Path -LiteralPath $temporal) { Remove-Item -LiteralPath $temporal -Force }
            $resp = (New-PeticionFtp $script:con $url 'RETR').GetResponse()
            try {
                $entrada = $resp.GetResponseStream()
                $salida = [IO.File]::Create($temporal)
                try { $entrada.CopyTo($salida) } finally { $salida.Dispose(); $entrada.Dispose() }
            } finally { $resp.Close() }
            $largo = (Get-Item -LiteralPath $temporal).Length
            if ($p.Tamano -ge 0 -and $largo -ne $p.Tamano) {
                throw "llegaron $largo bytes de $($p.Tamano)"
            }
        } | Out-Null
        $largo = (Get-Item -LiteralPath $temporal).Length
        $anterior = Move-DescargaASuSitio -Temporal $temporal -Final $final -Fecha (Get-Date)
        $bajados++; $bytesBajados += $largo
        if ($anterior) { $reemplazados++; "  cambió $($p.Ruta) (versión anterior: $anterior)" }
        if ($bajados % 25 -eq 0) { "  bajados $bajados de $($aBajar.Count)..." }
    } catch {
        $script:errores++
        if (Test-Path -LiteralPath $temporal) { Remove-Item -LiteralPath $temporal -Force -ErrorAction SilentlyContinue }
        Write-Host "ERROR al bajar $($p.Ruta) : $($_.Exception.Message)" -ForegroundColor Red
    }
}

# =================================================================================================
# Verificación de PDF firmados
# =================================================================================================
$dirOrdenes = Join-Path $carpetaArchivos 'OrdenesViaje'
$verificacion = 'NO SE PUDO'
if (Test-Path -LiteralPath $dirOrdenes) {
    ""
    "--- Verificación de PDF firmados ---"
    try {
        & (Join-Path $PSScriptRoot 'verificar-pdf-firmados.ps1') -Carpeta $dirOrdenes
        $verificacion = if ($LASTEXITCODE -eq 0) { 'OK' } else { 'FALLA' }
    } catch {
        Write-Host "No se pudo verificar: $($_.Exception.Message)" -ForegroundColor Yellow
    }
} else {
    Write-Host "No hay copia de OrdenesViaje: no se verificó." -ForegroundColor Yellow
}

$duracion = '{0:hh\:mm\:ss}' -f ((Get-Date) - $inicio)
$estado = if ($script:errores) { 'ERROR' } else { 'OK' }
Add-Content -Path $rutaLog -Encoding UTF8 -Value ('{0:yyyy-MM-dd HH:mm}  {1}  revisados {2}  bajados {3} ({4}, cambiados {5})  sin cambios {6}  errores {7}  verificación {8}  duración {9}' -f
    $inicio, $estado, $revisados, $bajados, (Format-Tamano $bytesBajados), $reemplazados, $iguales, $script:errores, $verificacion, $duracion)

""
"Espejo terminado"
"  Revisados  : $revisados"
"  Bajados    : $bajados ($(Format-Tamano $bytesBajados)); de ellos cambiados: $reemplazados"
"  Sin cambios: $iguales"
"  Errores    : $($script:errores)"
"  Verificación de PDF firmados: $verificacion"
"  Duración   : $duracion"

if ($script:errores) { exit 1 }
if ($verificacion -ne 'OK') { exit 2 }
exit 0
