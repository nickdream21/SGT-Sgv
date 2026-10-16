<#
.SYNOPSIS
    Prueba por HTTP la importación de documentos de RegistroDespacho (lo mismo que hace el
    usuario en el navegador): inicia sesión, sube los archivos con "Leer documentos" y
    resume la vista previa (programación, advertencias y cada tracto con lo elegido).

.DESCRIPTION
    Sirve cuando no hay navegador conectado (extensión Claude in Chrome). Solo lee: no
    pulsa "Crear lote" ni "Finalizar lote". Inicia sesión con el usuario de pruebas
    admin.transporte; la contraseña se lee de privado\CREDENCIALES_USUARIOS.txt y no se
    imprime. Solo contra el sitio local (BD de pruebas). Deja el HTML de la respuesta en
    $env:TEMP\sgv-vista-previa.html.

.EXAMPLE
    $d = 'docs\DOCUMENTACION'
    .\herramientas\sitio-local\probar-importacion.ps1 -Archivos "$d\pedido.zip", "$d\DESPACHO 09-10-26.docx"
#>
param(
    [Parameter(Mandatory)] [string[]] $Archivos,
    [string] $Sitio = 'https://localhost:44390',
    [string] $Usuario = 'admin.transporte'
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ($Sitio -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?$') { throw "Solo contra el sitio local (BD de pruebas): $Sitio" }
$jar = Join-Path $env:TEMP 'sgv-cookies.txt'
Remove-Item $jar -ErrorAction SilentlyContinue

function Campos($html) {
    $d = [ordered]@{}
    foreach ($m in [regex]::Matches($html, '<input[^>]*type="hidden"[^>]*>')) {
        $n = [regex]::Match($m.Value, 'name="([^"]+)"').Groups[1].Value
        if ($n) { $d[$n] = [System.Net.WebUtility]::HtmlDecode([regex]::Match($m.Value, 'value="([^"]*)"').Groups[1].Value) }
    }
    $d
}
function NombreDe($html, $id) { [regex]::Match($html, ('name="((?:[^"]*\$)?' + $id + ')"')).Groups[1].Value }
function Texto($html) { [System.Net.WebUtility]::HtmlDecode(($html -replace '<[^>]+>', ' ' -replace '\s+', ' ')).Trim() }
function Pedir($url, $campos, $archivos, $campoArchivo) {
    $a = @('-sk', '-L', '-b', $jar, '-c', $jar)
    if ($campos) { foreach ($k in $campos.Keys) { $a += @('--form-string', "$k=$($campos[$k])") } }
    foreach ($f in $archivos) { $a += @('-F', "$campoArchivo=@`"$f`"") }
    & curl.exe @a $url | Out-String
}

# Inicio de sesión
$linea = Get-Content (Join-Path $repo 'privado\CREDENCIALES_USUARIOS.txt') -Encoding UTF8 |
    Select-String ([regex]::Escape($Usuario) + '\s+(\S+)') | Select-Object -First 1
if (-not $linea) { throw "No está $Usuario en privado\CREDENCIALES_USUARIOS.txt (cópielo desde la otra máquina)." }
$html = Pedir "$Sitio/Views/Login.aspx"
$f = Campos $html
$f[(NombreDe $html 'txtUsername')] = $Usuario
$f[(NombreDe $html 'txtPassword')] = $linea.Matches[0].Groups[1].Value
$f[(NombreDe $html 'btnLogin')] = 'Acceder'
$html = Pedir "$Sitio/Views/Login.aspx" $f
if ($html -match 'txtPassword') { throw 'No se pudo iniciar sesión (¿contraseña de pruebas distinta?).' }

# Leer documentos
$url = "$Sitio/Views/RegistroDespacho.aspx"
$html = Pedir $url
$f = Campos $html
$f[(NombreDe $html 'btnLeerDocumentos')] = 'Leer documentos'
$html = Pedir $url $f ($Archivos | ForEach-Object { (Resolve-Path $_).Path }) (NombreDe $html 'fileDocumentosPedido')
[IO.File]::WriteAllText((Join-Path $env:TEMP 'sgv-vista-previa.html'), $html, [Text.Encoding]::UTF8)

$m = [regex]::Match($html, 'id="[^"]*_lblMensajeImportacion"[^>]*>(.*?)</span>', 'Singleline').Groups[1].Value
if ($m.Trim()) { "MENSAJE: $(Texto $m)" }
$m = [regex]::Match($html, 'id="[^"]*pnlProgramacionCarga"[^>]*>(.*?)</div>\s*<!--', 'Singleline').Groups[1].Value
if ($m) { "PROGRAMACIÓN: $(Texto $m)" }
$m = [regex]::Match($html, 'id="[^"]*pnlAdvertenciasImportacion"[^>]*>(.*?)</div>', 'Singleline').Groups[1].Value
if ($m) { "ADVERTENCIAS: $(Texto $m)" }
$m = [regex]::Match($html, 'id="[^"]*txtFechaProgramacionImp"[^>]*value="([^"]*)"').Groups[1].Value
if ($m) { "Fecha de programación: $m" }
$m = [regex]::Match($html, 'id="[^"]*ddlPlantaImp".*?<option selected="selected"[^>]*>([^<]*)<', 'Singleline').Groups[1].Value
if ($m) { "Planta de carga: $m" }

foreach ($fila in [regex]::Matches($html, '<tr class="[^"]*">\s*<td><input[^>]*chkIncluir.*?</tr>\s*(<tr class="rd-fila-programacion">.*?</tr>)?', 'Singleline')) {
    $transporte = Texto ([regex]::Match($fila.Value, '<div class="rd-placa">([^<]*)</div>').Groups[1].Value)
    $elegido = [regex]::Matches($fila.Value, '<select[^>]*>.*?</select>', 'Singleline') | ForEach-Object {
        Texto ([regex]::Match($_.Value, '<option selected="selected"[^>]*>([^<]*)<').Groups[1].Value) }
    "--- $transporte | $($elegido -join ' | ')"
    if ($fila.Groups[1].Success) { "    $(Texto $fila.Groups[1].Value)" }
}
