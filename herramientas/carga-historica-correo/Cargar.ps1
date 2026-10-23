param(
    [Parameter(Mandatory)] [string[]]$Pedidos,
    [ValidateSet('prueba', 'aplicar')] [string]$Modo = 'prueba',
    [string]$Zips = 'docs\DOCUMENTACION\correo guias'
)
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = (Resolve-Path "$aqui\..\..").Path
$bin = Join-Path $aqui 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

# Compilar contra el WebSGV.dll ya compilado (msbuild WebSGV.sln antes).
$webBin = Join-Path $raiz 'WebSGV\bin'
if (-not (Test-Path "$webBin\WebSGV.dll")) { throw "Falta WebSGV\bin\WebSGV.dll: compila la solución primero." }
Copy-Item "$webBin\*.dll" $bin -Force
$csc = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1
if (-not $csc) { throw 'No se encontró csc.exe (Roslyn) de Visual Studio.' }
$nativas = 'zlib1', 'QuestPdfSkia', 'qpdf', 'libwinpthread-1', 'libstdc++-6', 'libgcc_s_seh-1', 'libgcc_s_dw2-1'
$refs = Get-ChildItem "$bin\*.dll" | Where-Object { $nativas -notcontains $_.BaseName } | ForEach-Object { "/r:$($_.FullName)" }
$refs += '/r:System.Configuration.dll', '/r:System.Data.dll', '/r:System.Web.dll', '/r:System.IO.Compression.dll',
         "/r:$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"
& $csc.FullName /nologo /langversion:latest /nowarn:1701,1702 /out:"$bin\CargaHistorica.exe" @refs "$aqui\CargaHistorica.cs"
if ($LASTEXITCODE -ne 0) { throw 'No compiló CargaHistorica.cs.' }

# Config: cadena de conexión (BD de pruebas) y binding redirects de Web.config.
Copy-Item (Join-Path $raiz 'WebSGV\connectionStrings.config') $bin -Force
[xml]$web = Get-Content (Join-Path $raiz 'WebSGV\Web.config') -Raw -Encoding UTF8
$config = "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><startup><supportedRuntime version=`"v4.0`" sku=`".NETFramework,Version=v4.8`"/></startup>" +
          "$($web.configuration.appSettings.OuterXml)<connectionStrings configSource=`"connectionStrings.config`"/>$($web.configuration.runtime.OuterXml)</configuration>"
Set-Content "$bin\CargaHistorica.exe.config" $config -Encoding UTF8

& "$bin\CargaHistorica.exe" (Join-Path $raiz $Zips) ($Pedidos -join ',') $Modo
