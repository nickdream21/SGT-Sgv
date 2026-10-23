<#
.SYNOPSIS
    Pulsa "Actualizar GPS" en el detalle de cada viaje del seguimiento (SeguimientoViaje.aspx),
    como lo haría el usuario, y muestra el mensaje que devuelve.

.DESCRIPTION
    Sirve para recalcular viajes después de un cambio en la trazabilidad (por ejemplo, después de
    un script de Database\Scripts que vacía las horas que puso el GPS). Inicia sesión con el
    usuario de pruebas admin.transporte; la contraseña se lee de privado\CREDENCIALES_USUARIOS.txt
    y no se imprime. Solo contra el sitio local (BD de pruebas).

.EXAMPLE
    .\herramientas\sitio-local\actualizar-gps.ps1 -Ids 7250, 7256, 7273
#>
param(
    [Parameter(Mandatory)] [int[]] $Ids,
    [string] $Sitio = 'https://localhost:44390',
    [string] $Usuario = 'admin.transporte'
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ($Sitio -notmatch '^https?://(localhost|127\.0\.0\.1)(:\d+)?$') { throw "Solo contra el sitio local (BD de pruebas): $Sitio" }
# Una sesión por proceso: así se pueden correr varios a la vez (ASP.NET atiende de a uno por sesión).
$jar = Join-Path $env:TEMP "sgv-cookies-gps-$PID.txt"
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
function Pedir($url, $campos) {
    $a = @('-sk', '-L', '-b', $jar, '-c', $jar, '--max-time', '320')
    if ($campos) { foreach ($k in $campos.Keys) { $a += @('--data-urlencode', "$k=$($campos[$k])") } }
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

foreach ($id in $Ids) {
    $url = "$Sitio/Views/Exportacion/SeguimientoViaje.aspx?id=$id"
    $html = Pedir $url
    $f = Campos $html
    $boton = NombreDe $html 'btnActualizarGps'
    if (-not $boton) { "{0}: no se encontró el botón Actualizar GPS" -f $id; continue }
    $f['__EVENTTARGET'] = $boton
    $html = Pedir $url $f
    # Volver() redirige con el mensaje en la URL y la página lo muestra.
    $m = [regex]::Match($html, 'class="[^"]*sx-mensaje[^"]*"[^>]*>(.*?)</div>', 'Singleline').Groups[1].Value
    $texto = [System.Net.WebUtility]::HtmlDecode(($m -replace '<[^>]+>', ' ' -replace '\s+', ' ')).Trim()
    "{0}: {1}" -f $id, $(if ($texto) { $texto } else { '(sin mensaje)' })
}
