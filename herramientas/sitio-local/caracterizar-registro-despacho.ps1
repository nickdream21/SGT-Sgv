<#
.SYNOPSIS
    Caracterización de RegistroDespacho (E6-21): guarda lo que el usuario ve tras "Leer documentos",
    "Ver tractos por descargar" y "Leer programación" con documentos reales y, con -Comparar, dice si
    cambió algo. Es la red de seguridad para partir la página (E6-22 a E6-26).

.DESCRIPTION
    Por cada juego fijo (lista $Juegos, abajo) inicia sesión con una cookie nueva, abre
    RegistroDespacho.aspx y envía solo los botones de lectura de la lista blanca $PostsPermitidos.
    Cualquier otro POST (crear lote, programar, registrar entregas, finalizar...) aborta el script
    antes de enviarse: nunca registra ni crea nada. El único otro POST es el de Login.aspx (btnLogin).

    La vista previa se guarda normalizada en privado\caracterizacion\<juego>.txt (fuera de git): solo
    el texto del recuadro de documentos y el de descarga por pedido (filas, columnas, lo elegido en
    cada lista y cuántas opciones tiene, casillas, avisos, sellos y su tono, botones a la vista); sin
    ViewState, campos ocultos, ids, scripts ni comentarios; la fecha de hoy pasa a <HOY>, las rutas a
    <RUTA> y los GUID a <GUID>.

    Juegos (docs\DOCUMENTACION, gitignored; los pedidos de descarga dependen de la BD de pruebas):
      zip-word        ZIP del correo del pedido 4400134741 + Word DESPACHO 09-11-26
      zip-registrado  ZIP del pedido 4400133406 (DOC TRANSPORTE) + Word DESPACHO 09-10-26
      solo-pdf        los PDF del ZIP del pedido 4400134752, sueltos (se extraen a %TEMP%)
      solo-word       Word DESPACHO 10-11-26 ("Programar con el Word")
      descarga-word   "Ver tractos por descargar" del pedido 4400134752 y luego "Leer programación"
                      con el Word DESPACHO 10-11-26 (que trae su sección GUAYAQUIL-VITAPRO)
    Un juego que falla (falta un documento, el pedido ya no está pendiente, la página no muestra el
    recuadro o da error) no se guarda: se deja el guardado anterior, se avisa y el script sale con 1.
    Al comparar, un juego fallido, o un guardado de una corrida fallida, cuenta siempre como diferencia.
    Si el sitio no responde, se detiene con "El sitio no responde en ..." (exit 1).

    Contraseña de privado\CREDENCIALES_USUARIOS.txt, sin imprimirla. Solo contra el sitio local y
    nunca si connectionStrings.config apunta a producción (sgvTransporte).

.PARAMETER Comparar
    No guarda: vuelve a correr y muestra las diferencias contra lo guardado (exit 0 si no hay, 1 si hay).
    La corrida nueva queda en privado\caracterizacion\<juego>.nuevo.txt para revisarla. Por consola los
    números de 8 dígitos salen como ########.

.PARAMETER Juegos
    Solo esos juegos: -Juegos solo-word, descarga-word (o "solo-word,descarga-word" con powershell -File).

.NOTES
    Caracteriza el HTML que devuelve el servidor: lo que el JavaScript de la página cambia después en el
    navegador (select2, plegar entregas, avisos) no entra.

.EXAMPLE
    .\herramientas\sitio-local\caracterizar-registro-despacho.ps1             # guarda
    .\herramientas\sitio-local\caracterizar-registro-despacho.ps1 -Comparar   # compara
    .\herramientas\sitio-local\caracterizar-registro-despacho.ps1 -Comparar -Juegos solo-word
#>
param(
    [switch] $Comparar,
    [string[]] $Juegos,
    [string] $Sitio = 'https://localhost:44390',
    [string] $Usuario = 'admin.transporte'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$docs = Join-Path $repo 'docs\DOCUMENTACION'
$salida = Join-Path $repo 'privado\caracterizacion'
$temporal = Join-Path $env:TEMP 'sgv-caracterizacion'

if ($Sitio -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?$') { throw "Solo contra el sitio local (BD de pruebas): $Sitio" }
$conexion = Join-Path $repo 'WebSGV\connectionStrings.config'
if ((Test-Path $conexion) -and ((Get-Content $conexion -Raw) -match '(?i)(Initial Catalog|Database)\s*=\s*sgvTransporte\b')) {
    throw 'connectionStrings.config apunta a producción (sgvTransporte): la caracterización solo corre contra la BD de pruebas.'
}

# ── Lista blanca: los únicos botones que este script envía en RegistroDespacho (solo leen) ──
$PostsPermitidos = [ordered]@{
    'btnLeerDocumentos'           = 'Leer documentos'
    'btnVerCargasPendientes'      = 'Ver tractos por descargar'
    'btnLeerProgramacionDescarga' = 'Leer programación'
}
$PostIngreso = 'btnLogin'

# ── Juegos fijos ──
$ListaJuegos = @(
    @{ Nombre = 'zip-word'; Pasos = @(
        @{ Boton = 'btnLeerDocumentos'; Archivos = @('correo-2026-11-10\semana46documentosap4400134741viviana.zip', 'DESPACHO 09-11-26.docx') }) }
    @{ Nombre = 'zip-registrado'; Pasos = @(
        @{ Boton = 'btnLeerDocumentos'; Archivos = @('DOC TRANSPORTE\semana41documentosap4400133406viviana.zip', 'DESPACHO 09-10-26.docx') }) }
    @{ Nombre = 'solo-pdf'; Pasos = @(
        @{ Boton = 'btnLeerDocumentos'; PdfDelZip = 'correo-2026-11-10\semana46documentosap4400134752viviana.zip' }) }
    @{ Nombre = 'solo-word'; Pasos = @(
        @{ Boton = 'btnLeerDocumentos'; Archivos = @('DESPACHO 10-11-26.docx') }) }
    @{ Nombre = 'descarga-word'; Pasos = @(
        @{ Boton = 'btnVerCargasPendientes'; Pedido = '4400134752' }
        @{ Boton = 'btnLeerProgramacionDescarga'; Archivos = @('DESPACHO 10-11-26.docx') }) }
)
# Con "powershell -File" la lista llega como un solo texto ("a,b"): se separa por comas.
$Juegos = @($Juegos | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($Juegos) {
    $desconocidos = $Juegos | Where-Object { $_ -notin $ListaJuegos.Nombre }
    if ($desconocidos) { throw "Juegos desconocidos: $($desconocidos -join ', '). Hay: $($ListaJuegos.Nombre -join ', ')" }
    $ListaJuegos = $ListaJuegos | Where-Object { $_.Nombre -in $Juegos }
}

# ── HTTP (curl.exe, como probar-importacion.ps1) ──
function Attr($tag, $n) { [System.Net.WebUtility]::HtmlDecode([regex]::Match($tag, "\s$n=""([^""]*)""").Groups[1].Value) }
function NombreDe($html, $id) { [regex]::Match($html, ('name="((?:[^"]*\$)?' + $id + ')"')).Groups[1].Value }
# Todos los campos del formulario como los enviaría el navegador, sin ningún botón.
function Formulario($html) {
    $d = [ordered]@{}
    foreach ($m in [regex]::Matches($html, '<input[^>]*>')) {
        $t = $m.Value; $n = Attr $t 'name'; $tipo = (Attr $t 'type').ToLower()
        if (-not $n -or $tipo -in 'submit', 'button', 'file', 'image', 'reset') { continue }
        if ($t -match '\sdisabled') { continue }
        if ($tipo -in 'checkbox', 'radio') { if ($t -match '\schecked') { $d[$n] = $(if (Attr $t 'value') { Attr $t 'value' } else { 'on' }) }; continue }
        $d[$n] = Attr $t 'value'
    }
    foreach ($m in [regex]::Matches($html, '<select[^>]*>.*?</select>', 'Singleline')) {
        $n = Attr ([regex]::Match($m.Value, '<select[^>]*>').Value) 'name'; if (-not $n) { continue }
        $op = [regex]::Match($m.Value, '<option[^>]*selected="selected"[^>]*>')
        if (-not $op.Success) { $op = [regex]::Match($m.Value, '<option[^>]*>') }
        if ($op.Success) { $d[$n] = Attr $op.Value 'value' }
    }
    foreach ($m in [regex]::Matches($html, '<textarea[^>]*name="([^"]+)"[^>]*>(.*?)</textarea>', 'Singleline')) {
        $d[$m.Groups[1].Value] = [System.Net.WebUtility]::HtmlDecode($m.Groups[2].Value)
    }
    $d
}
$NoResponde = "El sitio no responde en $Sitio (¿iniciar-sitio.ps1?)"
# Solo "no se pudo conectar" (7) y "tiempo agotado" (28) son el sitio caído; otro código es un fallo de curl.
function Comprobar-Curl($codigo, $respuesta) {
    if ($codigo -eq 7 -or $codigo -eq 28) { throw $NoResponde }
    if ($codigo -ne 0) { throw "curl salió con código $codigo." }
    if (-not $respuesta.Trim()) { throw $NoResponde }
}
function Respuesta($salidaCurl) {
    $r = $salidaCurl | Out-String
    Comprobar-Curl $LASTEXITCODE $r
    $r
}
function Obtener($url) {
    Respuesta (& curl.exe -sk -L --max-time 300 -b $script:jar -c $script:jar $url)
}
# Valor entre comillas para la configuración de curl (-K): \ y " escapados, sin saltos de línea crudos.
function Citar($v) { '"' + ([string]$v).Replace('\', '\\').Replace('"', '\"').Replace("`r", '\r').Replace("`n", '\n').Replace("`t", '\t') + '"' }
# Único punto por donde sale un POST: comprueba la lista blanca antes de enviarlo.
function Publicar($url, $campos, $archivos, $campoArchivo) {
    $botones = @($campos.Keys | Where-Object { ($_ -split '\$')[-1] -match '^btn' } | ForEach-Object { ($_ -split '\$')[-1] })
    if ($campos['__EVENTTARGET']) { throw "POST cortado: evento '$($campos['__EVENTTARGET'])' no está en la lista blanca." }
    if ($botones.Count -ne 1) { throw "POST cortado: debe llevar exactamente un botón (lleva: $($botones -join ', '))." }
    $pagina = ([uri]$url).AbsolutePath
    if ($pagina -eq '/Views/Login.aspx') { $permitidos = @($PostIngreso) }
    elseif ($pagina -eq '/Views/RegistroDespacho.aspx') { $permitidos = @($PostsPermitidos.Keys) }
    else { throw "POST cortado: $pagina no está en la lista blanca." }
    if ($botones[0] -notin $permitidos) { throw "POST cortado: '$($botones[0])' no está en la lista blanca de $pagina." }

    # Los campos (la contraseña del ingreso y el ViewState, que pasa de los 32 K de la línea de órdenes de
    # Windows) van por la entrada estándar como configuración de curl (-K -): ni en los argumentos ni en un archivo.
    $config = New-Object System.Text.StringBuilder
    foreach ($k in $campos.Keys) { [void]$config.AppendLine('form-string = ' + (Citar "$k=$($campos[$k])")) }
    foreach ($f in $archivos) { [void]$config.AppendLine('form = ' + (Citar "$campoArchivo=@`"$f`"")) }
    $a = @('-sk', '-L', '--max-time', '300', '-b', $script:jar, '-c', $script:jar, '-K', '-', $url)
    CurlConEntrada $a $config.ToString()
}
# curl con la configuración por la entrada estándar, en bytes UTF-8 sin BOM. No "texto | curl.exe": PowerShell
# 5.1 antepone el BOM de la consola y curl rechaza esa línea ("unknown config option").
function CurlConEntrada($argumentos, $entrada) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo 'curl.exe'
    $psi.Arguments = ($argumentos | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }) -join ' '
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.StandardOutputEncoding = New-Object System.Text.UTF8Encoding($false)
    # .NET Framework crea el StreamWriter de la entrada con Console.InputEncoding y escribe su BOM al
    # arrancar: mientras se inicia curl, la consola va en UTF-8 sin BOM.
    $entradaConsola = [Console]::InputEncoding
    try {
        [Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false)
        $p = [System.Diagnostics.Process]::Start($psi)
    }
    finally { [Console]::InputEncoding = $entradaConsola }
    $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($entrada)
    $p.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    $p.StandardInput.BaseStream.Close()
    $r = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()
    Comprobar-Curl $p.ExitCode $r
    $r
}
function Pulsar($html, $boton, $extra, $archivos, $campoArchivo) {
    if (-not $PostsPermitidos.Contains($boton)) { throw "POST cortado: '$boton' no está en la lista blanca." }
    $nombre = NombreDe $html $boton
    if (-not $nombre) { throw "La página no muestra el botón $boton (¿cambió el marcado o falta el paso anterior?)." }
    $f = Formulario $html
    $f['__EVENTTARGET'] = ''; $f['__EVENTARGUMENT'] = ''
    if ($extra) { foreach ($k in $extra.Keys) { $f[$k] = $extra[$k] } }
    $f[$nombre] = $PostsPermitidos[$boton]
    $campo = if ($campoArchivo) { NombreDe $html $campoArchivo } else { $null }
    if ($archivos -and -not $campo) { throw "La página no muestra el selector $campoArchivo." }
    Publicar "$Sitio/Views/RegistroDespacho.aspx" $f $archivos $campo
}
function IniciarSesion {
    $linea = Get-Content (Join-Path $repo 'privado\CREDENCIALES_USUARIOS.txt') -Encoding UTF8 |
        Select-String ([regex]::Escape($Usuario) + '\s+(\S+)') | Select-Object -First 1
    if (-not $linea) { throw "No está $Usuario en privado\CREDENCIALES_USUARIOS.txt (cópielo desde la otra máquina)." }
    $html = Obtener "$Sitio/Views/Login.aspx"
    $f = Formulario $html
    $f[(NombreDe $html 'txtUsername')] = $Usuario
    $f[(NombreDe $html 'txtPassword')] = $linea.Matches[0].Groups[1].Value
    $f[(NombreDe $html $PostIngreso)] = 'Acceder'
    $html = Publicar "$Sitio/Views/Login.aspx" $f
    if ($html -match 'txtPassword') { throw 'No se pudo iniciar sesión (¿contraseña de pruebas distinta?).' }
}

# ── Normalización: el texto que ve el usuario en los dos recuadros ──
$zonaPeru = [TimeZoneInfo]::FindSystemTimeZoneById('SA Pacific Standard Time')
$hoy = [TimeZoneInfo]::ConvertTimeFromUtc([DateTime]::UtcNow, $zonaPeru)
function Tonos($tag) {
    $clase = Attr $tag 'class'
    $t = [regex]::Matches($clase, '\b(?:alert-(?!dismissible)[a-z]+|rd-notice-[a-z]+|sgv-estado-[a-z-]+|badge-[a-z]+|text-(?:danger|success|warning))\b') | ForEach-Object Value
    if ($t) { "{$($t -join ' ')} " } else { '' }
}
function Normalizar($html) {
    $ini = [regex]::Match($html, '<ol class="rd-pasos"')
    $fin = $html.IndexOf('<!-- ====== FASE 1')
    if (-not $ini.Success -or $fin -lt $ini.Index) { return "(la página no muestra el recuadro de documentos: ¿sesión vencida o error?)`n" + (Texto $html).Substring(0, [Math]::Min(400, (Texto $html).Length)) }
    $h = $html.Substring($ini.Index, $fin - $ini.Index)
    $h = [regex]::Replace($h, '<!--.*?-->|<script\b.*?</script>|<style\b.*?</style>|<template\b.*?</template>', '', 'Singleline')
    $h = [regex]::Replace($h, '<span class="sr-only">.*?</span>', '', 'Singleline')
    $h = [regex]::Replace($h, '<input[^>]*type="hidden"[^>]*>', '')
    # Listas: lo elegido (o la primera, como el navegador) y cuántas opciones hay.
    $h = [regex]::Replace($h, '<select[^>]*>(.*?)</select>', {
            param($m)
            $ops = [regex]::Matches($m.Groups[1].Value, '<option[^>]*>([^<]*)</option>')
            $sel = $ops | Where-Object { $_.Value -match 'selected="selected"' } | Select-Object -First 1
            if (-not $sel) { $sel = $ops | Select-Object -First 1 }
            $txt = if ($sel) { $sel.Groups[1].Value } else { '' }
            " [lista: $txt · $($ops.Count) op.] "
        }, 'Singleline')
    $h = [regex]::Replace($h, '<input[^>]*>', {
            param($m)
            $t = $m.Value; $tipo = (Attr $t 'type').ToLower()
            $des = if ($t -match '\sdisabled') { ' (deshabilitado)' } else { '' }
            switch ($tipo) {
                { $_ -in 'checkbox', 'radio' } { if ($t -match '\schecked') { " [x]$des " } else { " [ ]$des " } }
                { $_ -in 'submit', 'button' } { " (botón: $(Attr $t 'value'))$des " }
                'file' { " (archivo)$des " }
                default { " [$(Attr $t 'value')]$des " }
            }
        })
    $h = [regex]::Replace($h, '<textarea[^>]*>(.*?)</textarea>', ' [$1] ', 'Singleline')
    # Tono de avisos y sellos, fila ya registrada y paso actual.
    $h = [regex]::Replace($h, '<(span|div|label)\b[^>]*class="[^"]*"[^>]*>', { param($m) $m.Value + (Tonos $m.Value) })
    $h = [regex]::Replace($h, '<tr\b[^>]*>', { param($m) "`n" + $(if ((Attr $m.Value 'class') -match 'rd-fila-registrada') { '(registrada) ' } else { '' }) })
    $h = [regex]::Replace($h, '<li\b[^>]*aria-current="step"[^>]*>', "`n» ")
    $h = [regex]::Replace($h, '</t[dh]>', ' | ')
    $h = [regex]::Replace($h, '<br\s*/?>|</(div|p|li|tr|h\d|summary|table|small)>|<(li|h\d|p|div|summary)\b[^>]*>', "`n")
    $h = [regex]::Replace($h, '<[^>]+>', ' ')
    $h = [System.Net.WebUtility]::HtmlDecode($h)
    $lineas = $h -split "`n" | ForEach-Object { ($_ -replace '[\s ]+', ' ').Trim() -replace '^\|\s*', '' -replace '\s*\|$', '' } | Where-Object { $_ -and $_ -ne '|' }
    $t = ($lineas -join "`n")
    $t = $t.Replace($hoy.ToString('yyyy-MM-dd'), '<HOY>').Replace($hoy.ToString('dd/MM/yyyy'), '<HOY>')
    $t = [regex]::Replace($t, '(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b', '<GUID>')
    $t = [regex]::Replace($t, '(?i)\b[a-z]:\\[^\s|\]]+|~/App_Data/[^\s|\]]+', '<RUTA>')
    $t
}
function Texto($html) { [System.Net.WebUtility]::HtmlDecode(($html -replace '<[^>]+>', ' ' -replace '\s+', ' ')).Trim() }

function RutaDoc($relativa) {
    $r = Join-Path $docs $relativa
    if (-not (Test-Path -LiteralPath $r)) { throw "Falta el documento del juego: docs\DOCUMENTACION\$relativa" }
    $r
}
function PdfDelZip($juego, $relativa) {
    $dir = Join-Path $temporal $juego
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Path $dir | Out-Null
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((RutaDoc $relativa))
    try {
        foreach ($e in $zip.Entries | Where-Object { $_.Name -match '(?i)\.pdf$' }) {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($e, (Join-Path $dir $e.Name), $true)
        }
    } finally { $zip.Dispose() }
    @(Get-ChildItem $dir -Filter *.pdf | Sort-Object Name | ForEach-Object FullName)
}

function CorrerJuego($juego) {
    $script:jar = Join-Path $env:TEMP "sgv-caracterizacion-$($juego.Nombre).cookies"
    Remove-Item $script:jar -ErrorAction SilentlyContinue
    try {
        IniciarSesion
        $html = Obtener "$Sitio/Views/RegistroDespacho.aspx"
        $partes = @()
        $fallo = $null
        $n = 0
        :pasos foreach ($p in $juego.Pasos) {
            $n++
            $archivos = @()
            if ($p.Archivos) { $archivos = @($p.Archivos | ForEach-Object { RutaDoc $_ }) }
            if ($p.PdfDelZip) { $archivos = PdfDelZip $juego.Nombre $p.PdfDelZip }
            $nombres = ($archivos | ForEach-Object { [IO.Path]::GetFileName($_) }) -join ', '
            switch ($p.Boton) {
                'btnLeerDocumentos' { $html = Pulsar $html $p.Boton $null $archivos 'fileDocumentosPedido' }
                'btnVerCargasPendientes' {
                    $sel = [regex]::Match($html, 'id="[^"]*ddlPedidosPendientes".*?</select>', 'Singleline').Value
                    if ($sel -notmatch ('value="' + [regex]::Escape($p.Pedido) + '"')) {
                        $fallo = "el pedido $($p.Pedido) ya no está en la lista de pendientes de descarga de la BD de pruebas"
                        $partes += "=== Paso ${n}: $($PostsPermitidos[$p.Boton]) · pedido $($p.Pedido)`n($fallo)"
                        break pasos
                    }
                    $html = Pulsar $html $p.Boton @{ (NombreDe $html 'ddlPedidosPendientes') = $p.Pedido }
                    $nombres = "pedido $($p.Pedido)"
                }
                'btnLeerProgramacionDescarga' { $html = Pulsar $html $p.Boton $null $archivos 'fileProgramacionDescarga' }
                default { throw "POST cortado: '$($p.Boton)' no está en la lista blanca." }
            }
            $vista = Normalizar $html
            $partes += "=== Paso ${n}: $($PostsPermitidos[$p.Boton]) · $nombres`n$vista"
            if ($vista.StartsWith('(la página no muestra')) { $fallo = "paso ${n}: la página no muestra el recuadro de documentos"; break pasos }
        }
        [pscustomobject]@{ Texto = ($partes -join "`n`n") + "`n"; Fallo = $fallo }
    }
    finally { Remove-Item $script:jar -ErrorAction SilentlyContinue }
}

# Un guardado de una corrida fallida (versiones anteriores lo escribían) nunca vale como referencia.
$MarcaFallo = '(?m)^\((error al correr el juego|la página no muestra el recuadro|el pedido \d+ ya no está en la lista)'
# Por consola no salen números de 8 dígitos (DNI); lo guardado en privado\ los conserva.
function Enmascarar($linea) { [regex]::Replace([string]$linea, '(?<!\d)\d{8}(?!\d)', '########') }

New-Item -ItemType Directory -Force -Path $salida | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding($false)
$conDiferencias = 0
$fallidos = 0
try {
    foreach ($j in $ListaJuegos) {
        $archivo = Join-Path $salida "$($j.Nombre).txt"
        try { $r = CorrerJuego $j }
        catch {
            $m = $_.Exception.Message
            if ($m -like 'POST cortado*') { throw }
            if ($m -eq $NoResponde) { Write-Output $NoResponde; exit 1 }
            $r = [pscustomobject]@{ Texto = "(error al correr el juego: $m)`n"; Fallo = $m }
        }
        $texto = $r.Texto
        $lineas = ($texto -split "`n").Count
        if (-not $Comparar) {
            if ($r.Fallo) {
                $fallidos++
                "NO guardado $($j.Nombre) (se deja el guardado anterior): $(Enmascarar $r.Fallo)"
                continue
            }
            [IO.File]::WriteAllText($archivo, $texto, $utf8)
            "Guardado $($j.Nombre): $lineas líneas, $([Text.Encoding]::UTF8.GetByteCount($texto)) bytes."
            continue
        }
        $nuevo = Join-Path $salida "$($j.Nombre).nuevo.txt"
        [IO.File]::WriteAllText($nuevo, $texto, $utf8)
        if (-not (Test-Path $archivo)) { "$($j.Nombre): no hay caracterización guardada (corra sin -Comparar)."; $conDiferencias++; continue }
        $viejo = [IO.File]::ReadAllText($archivo, $utf8)
        if ($r.Fallo) { "$($j.Nombre): FALLÓ la corrida, cuenta como diferencia: $(Enmascarar $r.Fallo)"; $conDiferencias++; continue }
        if ($viejo -match $MarcaFallo) { "$($j.Nombre): el guardado es de una corrida fallida, cuenta como diferencia (vuelva a guardar)."; $conDiferencias++; continue }
        if ($viejo -ceq $texto) { "$($j.Nombre): 0 diferencias ($lineas líneas)."; continue }
        $conDiferencias++
        # Líneas quitadas y añadidas (como multiconjunto); si no hay ninguna, cambió solo el orden.
        $dif = @(Compare-Object ($viejo -split "`n") ($texto -split "`n") -CaseSensitive)
        if ($dif.Count -eq 0) { "$($j.Nombre): mismas líneas en otro orden (compare $archivo con $nuevo)."; continue }
        "$($j.Nombre): $($dif.Count) líneas distintas (<= guardado, => ahora):"
        $dif | Select-Object -First 40 | ForEach-Object { "  $($_.SideIndicator) $(Enmascarar $_.InputObject)" }
        if ($dif.Count -gt 40) { "  … y $($dif.Count - 40) más: compare $archivo con $nuevo" }
    }
}
finally { if (Test-Path $temporal) { Remove-Item $temporal -Recurse -Force -ErrorAction SilentlyContinue } }
if ($Comparar) {
    if ($conDiferencias -eq 0) { "Sin diferencias en $(@($ListaJuegos).Count) juego(s)."; exit 0 }
    "$conDiferencias juego(s) con diferencias."; exit 1
}
if ($fallidos -gt 0) { "$fallidos juego(s) sin guardar por fallo."; exit 1 }
