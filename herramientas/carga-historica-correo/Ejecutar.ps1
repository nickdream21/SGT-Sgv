param(
    [Parameter(Mandatory)] [ValidateSet('AnalizarCorreo', 'CompletarConCorreo', 'AdjuntarPdfFacturas', 'TercerosConDocumentos', 'ConciliarFletes')] [string]$Herramienta,
    [ValidateSet('prueba', 'aplicar')] [string]$Modo = 'prueba',
    [string]$Zips = 'docs\DOCUMENTACION\informacion',
    [string]$Tareas,
    [string]$Desde = '2026-10-01',
    [string]$Hasta = '2026-10-31'
)
# Compila y corre una de las herramientas del correo contra el WebSGV.dll ya compilado (msbuild WebSGV.sln antes),
# con la cadena de conexión de WebSGV\connectionStrings.config (BD de pruebas). Ver README.md.
$ErrorActionPreference = 'Stop'
$aqui = $PSScriptRoot
$raiz = (Resolve-Path "$aqui\..\..").Path
$bin = Join-Path $aqui 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$webBin = Join-Path $raiz 'WebSGV\bin'
if (-not (Test-Path "$webBin\WebSGV.dll")) { throw "Falta WebSGV\bin\WebSGV.dll: compila la solución primero." }
Copy-Item "$webBin\*.dll" $bin -Force
$csc = Get-ChildItem "${env:ProgramFiles}\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'Roslyn' } | Select-Object -First 1
if (-not $csc) { throw 'No se encontró csc.exe (Roslyn) de Visual Studio.' }
$nativas = 'zlib1', 'QuestPdfSkia', 'qpdf', 'libwinpthread-1', 'libstdc++-6', 'libgcc_s_seh-1', 'libgcc_s_dw2-1'
$refs = Get-ChildItem "$bin\*.dll" | Where-Object { $nativas -notcontains $_.BaseName } | ForEach-Object { "/r:$($_.FullName)" }
$refs += '/r:System.Configuration.dll', '/r:System.Data.dll', '/r:System.Web.dll', '/r:System.IO.Compression.dll', '/r:System.Transactions.dll',
         "/r:$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\netstandard.dll"
& $csc.FullName /nologo /langversion:latest /nowarn:1701,1702 /out:"$bin\$Herramienta.exe" @refs "$aqui\$Herramienta.cs"
if ($LASTEXITCODE -ne 0) { throw "No compiló $Herramienta.cs." }

Copy-Item (Join-Path $raiz 'WebSGV\connectionStrings.config') $bin -Force
[xml]$web = Get-Content (Join-Path $raiz 'WebSGV\Web.config') -Raw -Encoding UTF8
$config = "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><startup><supportedRuntime version=`"v4.0`" sku=`".NETFramework,Version=v4.8`"/></startup>" +
          "$($web.configuration.appSettings.OuterXml)<connectionStrings configSource=`"connectionStrings.config`"/>$($web.configuration.runtime.OuterXml)</configuration>"
Set-Content "$bin\$Herramienta.exe.config" $config -Encoding UTF8

$carpetaZips = Join-Path $raiz $Zips
switch ($Herramienta) {
    'AnalizarCorreo'      { & "$bin\$Herramienta.exe" $carpetaZips }
    'CompletarConCorreo'  { & "$bin\$Herramienta.exe" $carpetaZips $Modo (Join-Path $raiz 'WebSGV') }
    'AdjuntarPdfFacturas' { & "$bin\$Herramienta.exe" $carpetaZips (Join-Path $raiz 'WebSGV') $Modo }
    'ConciliarFletes'     { & "$bin\$Herramienta.exe" $Desde $Hasta }
    'TercerosConDocumentos' { & "$bin\$Herramienta.exe" (Resolve-Path $Tareas).Path $Modo (Join-Path $raiz 'WebSGV') }
}
