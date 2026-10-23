param(
    # Carpeta con los Word de programación (se buscan en subcarpetas).
    [string]$Word = '',
    # Tope de llamadas al API de Onway (2 por segundo; lo que ya está en caché no cuenta).
    [int]$MaxLlamadas = 600
)
# Aprende los viajes de Crisar a La Troncal del GPS y los agrega a PerfilesLlegada.json (ver README.md).
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = (Resolve-Path "$aqui\..\..").Path
if (-not $Word) { $Word = Join-Path $raiz 'docs\DOCUMENTACION\2026' }
$bin = Join-Path $aqui 'bin'
$cache = Join-Path $raiz 'herramientas\cuadro-status-general\cache'
$perfiles = Join-Path $raiz 'WebSGV\Services\Exportacion\PerfilesLlegada.json'
$informe = Join-Path $aqui 'crisar-la-troncal.md'
New-Item -ItemType Directory -Force $bin, $cache | Out-Null

$webBin = Join-Path $raiz 'WebSGV\bin'
if (-not (Test-Path "$webBin\WebSGV.dll")) { throw "Falta WebSGV\bin\WebSGV.dll: compila la solución primero." }
Copy-Item "$webBin\*.dll" $bin -Force
$csc = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1
if (-not $csc) { throw 'No se encontró csc.exe (Roslyn) de Visual Studio.' }
$nativas = 'zlib1', 'QuestPdfSkia', 'qpdf', 'libwinpthread-1', 'libstdc++-6', 'libgcc_s_seh-1'
$refs = Get-ChildItem "$bin\*.dll" | Where-Object { $nativas -notcontains $_.BaseName } | ForEach-Object { "/r:$($_.FullName)" }
$refs += '/r:System.Configuration.dll', '/r:System.Data.dll', '/r:System.Transactions.dll', '/r:System.Web.dll',
         '/r:System.Xml.Linq.dll', '/r:System.IO.Compression.dll', "/r:$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"
& $csc.FullName /nologo /langversion:latest /out:"$bin\PerfilCrisar.exe" @refs "$aqui\PerfilCrisar.cs"
if ($LASTEXITCODE -ne 0) { throw 'No compiló PerfilCrisar.cs.' }

[xml]$web = Get-Content (Join-Path $raiz 'WebSGV\Web.config') -Raw -Encoding UTF8
$secretos = Join-Path $raiz 'WebSGV\appSettings.Secrets.config'
$config = "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><startup><supportedRuntime version=`"v4.0`" sku=`".NETFramework,Version=v4.8`"/></startup>" +
          "<appSettings file=`"$secretos`"/>$($web.configuration.runtime.OuterXml)</configuration>"
Set-Content "$bin\PerfilCrisar.exe.config" $config -Encoding UTF8

# Después: compilar de nuevo (el JSON va dentro de WebSGV.dll) y publicar.
& "$bin\PerfilCrisar.exe" $raiz $cache $perfiles $Word "--max-llamadas=$MaxLlamadas" "--informe=$informe"
