param(
    # Sitio publicado (o https://localhost:44390 para probar contra el sitio local).
    [string]$Sitio = 'https://sgvtransporte.somee.com'
)
# Llama a la actualización automática del GPS con la clave de appSettings.Secrets.config (no se muestra).
# Para el Programador de tareas de Windows: ver README.md.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
[xml]$secretos = Get-Content (Join-Path $repo 'WebSGV\appSettings.Secrets.config') -Raw
$clave = ($secretos.appSettings.add | Where-Object { $_.key -eq 'GpsAutomaticoClave' }).value
if (-not $clave) { throw 'Falta GpsAutomaticoClave en WebSGV\appSettings.Secrets.config.' }
$respuesta = & curl.exe -s -k -w " [HTTP %{http_code}]" -H "X-SGV-Clave: $clave" "$Sitio/Views/Exportacion/GpsAutomatico.ashx"
"$(Get-Date -Format 'dd/MM HH:mm:ss') $respuesta"
