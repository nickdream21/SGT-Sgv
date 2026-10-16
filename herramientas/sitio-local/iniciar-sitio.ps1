<#
.SYNOPSIS
    Levanta WebSGV en IIS Express (sin abrir Visual Studio) para probarlo en el navegador.

.DESCRIPTION
    Usa la configuración que crea Visual Studio (.vs\WebSGV\config\applicationhost.config:
    https://localhost:44390 y http://localhost:50291). Si la máquina nunca abrió la solución
    en Visual Studio y no existe, sirve la carpeta WebSGV en http://localhost:50291.
    Compile antes (msbuild WebSGV.sln). Si el sitio ya responde, no hace nada.

    Para dejarlo corriendo en segundo plano desde Claude Code: ejecútelo con
    run_in_background y espere la línea "IIS Express is running.".

.EXAMPLE
    .\herramientas\sitio-local\iniciar-sitio.ps1
#>
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$iis = Join-Path $env:ProgramFiles 'IIS Express\iisexpress.exe'
if (-not (Test-Path $iis)) { throw "No está IIS Express ($iis). Instale Visual Studio con la carga de trabajo ASP.NET o IIS Express 10." }

foreach ($url in 'https://localhost:44390/Views/Login.aspx', 'http://localhost:50291/Views/Login.aspx') {
    $codigo = & curl.exe -sk -o NUL -w '%{http_code}' --max-time 5 $url
    if ($codigo -eq '200') { "El sitio ya está corriendo: $url"; return }
}

$config = Join-Path $repo '.vs\WebSGV\config\applicationhost.config'
if (Test-Path $config) {
    "Sitio: https://localhost:44390/Views/Login.aspx (también http://localhost:50291)"
    & $iis /config:$config /site:WebSGV
}
else {
    "Sin .vs\ (la solución no se abrió en Visual Studio en esta máquina): solo http."
    "Sitio: http://localhost:50291/Views/Login.aspx"
    & $iis /path:(Join-Path $repo 'WebSGV') /port:50291
}
