param(
    # Viajes desde esta fecha (AAAA-MM-DD).
    [string]$Desde = '2026-09-01',
    # Tope de llamadas al API de Onway (2 por segundo; lo que ya está en caché no cuenta).
    [int]$MaxLlamadas = 600
)
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = (Resolve-Path "$aqui\..\..").Path
$bin = Join-Path $aqui 'bin'
# El historial se comparte con el cuadro STATUS GENERAL (mismo formato, por placa y día).
$cache = Join-Path $raiz 'herramientas\cuadro-status-general\cache'
$salida = Join-Path $raiz 'WebSGV\Services\Exportacion\PerfilesLlegada.json'
New-Item -ItemType Directory -Force $bin, $cache | Out-Null

# 1) Compilar contra el WebSGV.dll ya compilado (msbuild WebSGV.sln antes).
$webBin = Join-Path $raiz 'WebSGV\bin'
if (-not (Test-Path "$webBin\WebSGV.dll")) { throw "Falta WebSGV\bin\WebSGV.dll: compila la solución primero." }
Copy-Item "$webBin\*.dll" $bin -Force
$csc = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1
if (-not $csc) { throw 'No se encontró csc.exe (Roslyn) de Visual Studio.' }
$nativas = 'zlib1', 'QuestPdfSkia', 'qpdf', 'libwinpthread-1', 'libstdc++-6', 'libgcc_s_seh-1'
$refs = Get-ChildItem "$bin\*.dll" | Where-Object { $nativas -notcontains $_.BaseName } | ForEach-Object { "/r:$($_.FullName)" }
$refs += '/r:System.Configuration.dll', '/r:System.Data.dll', '/r:System.Transactions.dll', '/r:System.Web.dll',
         '/r:System.Xml.Linq.dll', "/r:$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"
& $csc.FullName /nologo /langversion:latest /out:"$bin\PerfilLlegada.exe" @refs "$aqui\PerfilLlegada.cs"
if ($LASTEXITCODE -ne 0) { throw 'No compiló PerfilLlegada.cs.' }

# 2) Config: binding redirects de Web.config y credenciales de Onway en su sitio.
[xml]$web = Get-Content (Join-Path $raiz 'WebSGV\Web.config') -Raw -Encoding UTF8
$secretos = Join-Path $raiz 'WebSGV\appSettings.Secrets.config'
$config = "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><startup><supportedRuntime version=`"v4.0`" sku=`".NETFramework,Version=v4.8`"/></startup>" +
          "<appSettings file=`"$secretos`"/>$($web.configuration.runtime.OuterXml)</configuration>"
Set-Content "$bin\PerfilLlegada.exe.config" $config -Encoding UTF8

# 3) Correr. Después: compilar de nuevo (el JSON va dentro de WebSGV.dll) y publicar.
& "$bin\PerfilLlegada.exe" $raiz $cache $salida "--desde=$Desde" "--max-llamadas=$MaxLlamadas"
