<#
.SYNOPSIS
    "Programar con el Word" por HTTP en el sitio local: sube el Word a RegistroDespacho, muestra la
    vista previa y, solo con -Confirmar, pulsa "Programar despachos" con lo que el sitio propuso
    (chofer, tracto, carreta y hora de cada fila; las filas "Ya programado" no se tocan).

.DESCRIPTION
    Escribe en la BD de pruebas (sgvActualizada), igual que el botón. -Seccion elige la sección de
    carga del Word por su texto (por defecto, la que el sitio muestra primero). -RegistrarEntregas
    pulsa "Registrar entregas" (secciones de descarga de terceros: POLINPLAST-VIRU, AFRECHO-HUAQUILLAS).
    Contraseña de privado\CREDENCIALES_USUARIOS.txt, sin imprimirla. Solo contra el sitio local; por
    http (el certificado de IIS Express no lo acepta HttpClient desde PowerShell).

.EXAMPLE
    .\herramientas\sitio-local\programar-word.ps1 -Word "docs\DOCUMENTACION\DESPACHO 25-10-26.docx" -Seccion TRUJILLO-VITAPRO
    .\herramientas\sitio-local\programar-word.ps1 -Word "docs\DOCUMENTACION\DESPACHO 25-10-26.docx" -Seccion TRUJILLO-VITAPRO -Confirmar
#>
param(
    [Parameter(Mandatory)] [string] $Word,
    [string] $Seccion,
    [switch] $Confirmar,
    [switch] $RegistrarEntregas,
    [string] $Sitio = 'http://localhost:50291',
    [string] $Usuario = 'admin.transporte'
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ($Sitio -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?$') { throw "Solo contra el sitio local (BD de pruebas): $Sitio" }

function Texto($html) { [System.Net.WebUtility]::HtmlDecode(($html -replace '<[^>]+>', ' ' -replace '\s+', ' ')).Trim() }
function Attr($tag, $n) { [System.Net.WebUtility]::HtmlDecode([regex]::Match($tag, " $n=""([^""]*)""").Groups[1].Value) }
function NombreDe($html, $id) { [regex]::Match($html, ('name="((?:[^"]*\$)?' + $id + ')"')).Groups[1].Value }
# Todos los campos del formulario como los enviaría el navegador (sin botones).
function Formulario($html) {
    $d = [ordered]@{}
    foreach ($m in [regex]::Matches($html, '<input[^>]*>')) {
        $t = $m.Value; $n = Attr $t 'name'; $tipo = (Attr $t 'type').ToLower()
        if (-not $n -or $tipo -in 'submit', 'button', 'file', 'image') { continue }
        if ($tipo -in 'checkbox', 'radio') { if ($t -match '\schecked') { $d[$n] = $(if (Attr $t 'value') { Attr $t 'value' } else { 'on' }) } ; continue }
        $d[$n] = Attr $t 'value'
    }
    foreach ($m in [regex]::Matches($html, '<select[^>]*>.*?</select>', 'Singleline')) {
        $n = Attr ([regex]::Match($m.Value, '<select[^>]*>').Value) 'name'; if (-not $n) { continue }
        $op = [regex]::Match($m.Value, '<option[^>]*selected="selected"[^>]*>')
        if (-not $op.Success) { $op = [regex]::Match($m.Value, '<option[^>]*>') }
        $d[$n] = Attr $op.Value 'value'
    }
    foreach ($m in [regex]::Matches($html, '<textarea[^>]*name="([^"]+)"[^>]*>(.*?)</textarea>', 'Singleline')) {
        $d[$m.Groups[1].Value] = [System.Net.WebUtility]::HtmlDecode($m.Groups[2].Value)
    }
    $d
}
Add-Type -AssemblyName System.Net.Http
[Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$manejador = New-Object System.Net.Http.HttpClientHandler
$manejador.CookieContainer = New-Object System.Net.CookieContainer
$cliente = New-Object System.Net.Http.HttpClient($manejador)
$cliente.Timeout = [TimeSpan]::FromMinutes(5)
function Pedir($url, $campos, $archivo, $campoArchivo) {
    if (-not $campos) { return $cliente.GetStringAsync($url).Result }
    $cuerpo = New-Object System.Net.Http.MultipartFormDataContent
    foreach ($k in $campos.Keys) { $cuerpo.Add((New-Object System.Net.Http.StringContent([string]$campos[$k], [Text.Encoding]::UTF8)), "`"$k`"") }
    if ($archivo) {
        $bytes = New-Object System.Net.Http.ByteArrayContent(,[IO.File]::ReadAllBytes($archivo))
        $cuerpo.Add($bytes, "`"$campoArchivo`"", "`"$([IO.Path]::GetFileName($archivo))`"")
    }
    $r = $cliente.PostAsync($url, $cuerpo).Result
    $r.Content.ReadAsStringAsync().Result
}
function Resumen($html) {
    $m = [regex]::Match($html, 'id="[^"]*_lblMensajeImportacion"[^>]*>(.*?)</span>', 'Singleline').Groups[1].Value
    if ($m.Trim()) { "MENSAJE: $(Texto $m)" }
    $m = [regex]::Match($html, 'id="[^"]*pnlAvisosProg"[^>]*>(.*?)</div>', 'Singleline').Groups[1].Value
    if ($m) { "AVISOS: $(Texto $m)" }
    "Fecha de carga: $(Attr ([regex]::Match($html, '<input[^>]*txtFechaProg"[^>]*>').Value) 'value')"
    foreach ($fila in [regex]::Matches($html, '<tr>\s*<td><input[^>]*chkIncluirProg.*?</tr>', 'Singleline')) {
        $hora = Attr ([regex]::Match($fila.Value, '<input[^>]*txtHoraProg"[^>]*>').Value) 'value'
        $elegido = [regex]::Matches($fila.Value, '<select[^>]*>.*?</select>', 'Singleline') | ForEach-Object {
            Texto ([regex]::Match($_.Value, '<option selected="selected"[^>]*>([^<]*)<').Groups[1].Value) }
        $estado = Texto ([regex]::Match($fila.Value, '<td>((?:(?!<td).)*)</td>\s*</tr>', 'Singleline').Groups[1].Value)
        "--- $hora | $($elegido -join ' | ') | $estado"
    }
}

$linea = Get-Content (Join-Path $repo 'privado\CREDENCIALES_USUARIOS.txt') -Encoding UTF8 |
    Select-String ([regex]::Escape($Usuario) + '\s+(\S+)') | Select-Object -First 1
if (-not $linea) { throw "No está $Usuario en privado\CREDENCIALES_USUARIOS.txt." }
$html = Pedir "$Sitio/Views/Login.aspx"
$f = Formulario $html
$f[(NombreDe $html 'txtUsername')] = $Usuario
$f[(NombreDe $html 'txtPassword')] = $linea.Matches[0].Groups[1].Value
$f[(NombreDe $html 'btnLogin')] = 'Acceder'
$html = Pedir "$Sitio/Views/Login.aspx" $f
if ($html -match 'txtPassword') { throw 'No se pudo iniciar sesión.' }

$url = "$Sitio/Views/RegistroDespacho.aspx"
$html = Pedir $url
$f = Formulario $html
$f[(NombreDe $html 'btnLeerDocumentos')] = 'Leer documentos'
$html = Pedir $url $f (Resolve-Path $Word).Path (NombreDe $html 'fileDocumentosPedido')
if ($html -notmatch 'id="[^"]*pnlProgramacionNueva"') { Texto ([regex]::Match($html, 'id="[^"]*_lblMensajeImportacion"[^>]*>(.*?)</span>', 'Singleline').Groups[1].Value); throw 'El sitio no mostró "Programar con el Word".' }

if ($Seccion) {
    $sel = [regex]::Match($html, 'id="[^"]*ddlSeccionProg".*?</select>', 'Singleline').Value
    $op = [regex]::Matches($sel, '<option[^>]*value="([^"]*)"[^>]*>([^<]*)<') | Where-Object { $_.Groups[2].Value -match [regex]::Escape($Seccion) } | Select-Object -First 1
    if (-not $op) { throw "No hay una sección '$Seccion' en el Word." }
    $f = Formulario $html
    $n = NombreDe $html 'ddlSeccionProg'
    $f[$n] = [System.Net.WebUtility]::HtmlDecode($op.Groups[1].Value)
    $f['__EVENTTARGET'] = $n
    $html = Pedir $url $f
    "Sección: $(Texto $op.Groups[2].Value)"
}
Resumen $html
$entregas = [regex]::Match($html, 'id="[^"]*pnlEntregasTerceros"[^>]*>(.*?)<input[^>]*btnRegistrarEntregas', 'Singleline').Groups[1].Value
if ($entregas) { "ENTREGAS DE TERCEROS: $(Texto $entregas)" }
if ($RegistrarEntregas) {
    if (-not $entregas) { throw 'El Word no tiene entregas de clientes terceros.' }
    $f = Formulario $html
    $f['__EVENTTARGET'] = ''
    $f[(NombreDe $html 'btnRegistrarEntregas')] = 'Registrar entregas'
    $html = Pedir $url $f
    "=== DESPUÉS DE REGISTRAR LAS ENTREGAS"
    $m = [regex]::Match($html, 'id="[^"]*_lblMensajeImportacion"[^>]*>(.*?)</span>', 'Singleline').Groups[1].Value
    "MENSAJE: $(Texto $m)"
    return
}
if (-not $Confirmar) { "(vista previa: agregue -Confirmar para programar)"; return }

$f = Formulario $html
$f['__EVENTTARGET'] = ''
$f[(NombreDe $html 'btnProgramarDespachos')] = 'Programar despachos'
$html = Pedir $url $f
"=== DESPUÉS DE PROGRAMAR"
Resumen $html
